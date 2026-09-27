// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class AsyncTransformation
    {
        private struct CallLocalExclusionVisitor(
            AsyncTransformation transformation, BasicBlock block, HashSet<int> excludedLocals) : ILocalDefVisitor
        {
            public readonly GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
            {
                if (def.IsEntire(transformation._compiler))
                {
                    var local = def.LclNum;
                    if (transformation.IsCallDefLiveInEHSucc(block, local))
                    {
                        JITDUMP($"  V{local:D2} is fully defined but live into an EH successor\n");
                    }
                    else
                    {
                        JITDUMP($"  V{local:D2} is fully defined and will not be considered live\n");
                        _ = excludedLocals.Add(local);
                    }
                }

                return GenTree.VisitResult.Continue;
            }
        }

        private void CreateLiveSetForSuspension(BasicBlock block, GenTreeCall call,
            IReadOnlyList<GenTree> defs, AsyncAnalysis analyses, AsyncContinuationLayoutBuilder builder)
        {
            var excludedLocals = new HashSet<int>();
            var visitor = new CallLocalExclusionVisitor(this, block, excludedLocals);
            _ = call.VisitLogicalLocalDefs(_compiler, ref visitor);

#if TARGET_WASM
            if (_compiler.lvaWasmSpArg != BAD_VAR_NUM)
            {
                _ = excludedLocals.Add(_compiler.lvaWasmSpArg);
            }
#endif

            analyses.GetLiveLocals(builder, local =>
                !_compiler.lvaGetDesc(local).lvOnlyUsedOnSynchronousPath && !excludedLocals.Contains(local));
            LiftLIREdges(block, defs, builder);

#if DEBUG
            if (_compiler.verbose)
            {
                JITDUMP($"  {builder.Locals.Count} live locals\n");
                if (builder.Locals.Count != 0)
                {
                    var separator = "    ";
                    foreach (var local in builder.Locals)
                    {
                        JITDUMP($"{separator}V{local:D2} ({_compiler.lvaGetDesc(local).Type.Name})");
                        separator = ", ";
                    }

                    JITDUMP("\n");
                }
            }
#endif
        }

        private bool IsCallDefLiveInEHSucc(BasicBlock block, int lclNum)
        {
            if (!_compiler.ehIsInsideNonAsyncContextRestoreRegion(block))
            {
                return false;
            }

            ref var descriptor = ref _compiler.lvaGetDesc(lclNum);
            if (!descriptor.lvTracked)
            {
                return true;
            }

            var index = descriptor._varIndex;
            return block.VisitEHSuccs(_compiler, successor =>
                VarSetOps.IsMember(_compiler, successor.bbLiveIn, index)
                    ? BasicBlockVisit.Abort
                    : BasicBlockVisit.Continue) is BasicBlockVisit.Abort;
        }

        private void LiftLIREdges(BasicBlock block, IReadOnlyList<GenTree> defs,
            AsyncContinuationLayoutBuilder layoutBuilder)
        {
            foreach (var tree in defs)
            {
                if (!block.TryGetUse(tree, out var use))
                {
                    throw new InvalidOperationException("An outstanding LIR edge has no use");
                }

                if (tree.IsInvariant ||
                    ((tree.Oper is GT_LCL_VAR) &&
                     !_compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum).IsAddressExposed))
                {
                    block.Remove(tree);
                    block.InsertBefore(use.User(), tree);
                    continue;
                }

                var newLclNum = use.ReplaceWithLclVar(_compiler);
                layoutBuilder.AddLocal(newLclNum);
                var newUse = use.Def();
                block.Remove(newUse);
                block.InsertBefore(use.User(), newUse);
            }
        }

        private unsafe bool ContinuationNeedsKeepAlive(AsyncAnalysis analyses)
        {
            if (_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
            {
                return false;
            }

            const CorInfoOptions genericsContextFrom =
                CORINFO_GENERICS_CTXT_FROM_METHODDESC | CORINFO_GENERICS_CTXT_FROM_METHODTABLE;

            return ((_compiler.info.compMethodInfo->options & genericsContextFrom) != 0) &&
                analyses.IsLive(_compiler.info.compTypeCtxtArg);
        }

        private unsafe void BuildContinuation(BasicBlock block, GenTreeCall call, bool needsKeepAlive,
            AsyncContinuationLayoutBuilder builder)
        {
            if (call._returnType is not TYP_VOID)
            {
                var returnLayout = call._returnType is TYP_STRUCT
                    ? _compiler.typGetObjLayout(call.RetClsHnd)
                    : null;
                builder.AddReturn(new AsyncReturnTypeInfo(call._returnType, returnLayout));
                JITDUMP("  Call has return; continuation will have return value\n");
            }

            if (_compiler.MethodHasPatchpoint || _compiler.opts.IsOSR)
            {
                JITDUMP($"  Method {(_compiler.MethodHasPatchpoint ? "has patchpoints" : "is an OSR method")}; keeping OSR address at the beginning of non-GC data\n");
                builder.NeedsOSRAddress = true;
            }

            if (_compiler.ehIsInsideNonAsyncContextRestoreRegion(block))
            {
                builder.NeedsException = true;
                JITDUMP($"  {FMT_BB(block.bbNum)} is in try region {block.TryIndex}; continuation will have exception\n");
            }

            if (call.GetAsyncInfo().ContinuationContextHandling is
                ContinuationContextHandling.ContinueOnCapturedContext)
            {
                builder.NeedsContinuationContext = true;
                JITDUMP("  Continuation continues on captured context; continuation will have context\n");
            }

            if (needsKeepAlive)
            {
                builder.NeedsKeepAlive = true;
                JITDUMP("  Continuation will have keep alive object\n");
            }

            if (call.GetAsyncInfo().NeedsToSaveAndRestoreExecutionContext)
            {
                builder.NeedsExecutionContext = true;
                JITDUMP("  Call has async-only save and restore of ExecutionContext; continuation will have ExecutionContext\n");
            }
        }
    }
}
