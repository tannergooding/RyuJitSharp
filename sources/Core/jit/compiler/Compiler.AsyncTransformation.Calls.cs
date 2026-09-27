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
        private void TransformTailAwaits(IReadOnlyList<BasicBlock> blocks)
        {
            foreach (var initialBlock in blocks)
            {
                var block = initialBlock;
                bool found;
                do
                {
                    found = false;
                    foreach (var tree in block)
                    {
                        if (tree is GenTreeCall call && call.IsAsync && !call.IsTailCall &&
                            call.GetAsyncInfo().IsTailAwait)
                        {
                            block = TransformTailAwait(block, call);
                            found = true;
                            break;
                        }
                    }
                }
                while (found);
            }
        }

        private BasicBlock TransformTailAwait(BasicBlock block, GenTreeCall call)
        {
#if DEBUG
            JITDUMP($"Transforming tail await [{call.TreeId:D6}] in {FMT_BB(block.bbNum)}\n");
#endif

            var definition = CanonicalizeCallDefinition(block, call, null);
            var suspension = CreateTailAwaitSuspension(block, call);
            return CreateCheckAndSuspendAfterCall(block, call, definition, suspension);
        }

        private AsyncCallDefinitionInfo CanonicalizeCallDefinition(
            BasicBlock block, GenTreeCall call, AsyncAnalysis? analyses)
        {
            var definition = new AsyncCallDefinitionInfo { InsertAfter = call };
            var retbufArg = call.Args.RetBufferArg;

            analyses?.Update(call);

            if ((call.Type is not TYP_VOID) && !call.IsUnusedValue)
            {
                assert(retbufArg is null);
                var next = call.Next ?? throw new InvalidOperationException("Async call result has no use");

                // Keep an existing local store unless EH requires the old value after resumption.
                if (!next.Oper.IsLocalStore || (next.AsLclVarCommon().Data != call) ||
                    _compiler.ehIsInsideNonAsyncContextRestoreRegion(block))
                {
                    if (!block.TryGetUse(call, out var use))
                    {
                        throw new InvalidOperationException("Async call result has no LIR use");
                    }

                    var newLclNum = use.ReplaceWithLclVar(_compiler);

                    if (call.IsMultiRegCall && (use.User().Oper is GT_STORE_LCL_VAR))
                    {
                        ref var destination = ref _compiler.lvaGetDesc(use.User().AsLclVar().LclNum);
                        if (_compiler.lvaGetPromotionType(in destination) is PROMOTION_TYPE_INDEPENDENT)
                        {
                            _compiler.lvaSetVarDoNotEnregister(newLclNum, DoNotEnregisterReason.LocalField);
                            JITDUMP("  Call is multi-reg stored to an independently promoted local; decomposing store\n");

                            for (var i = 0; i < destination.lvFieldCnt; i++)
                            {
                                var fieldLclNum = destination.lvFieldLclStart + i;
                                ref var field = ref _compiler.lvaGetDesc(fieldLclNum);
                                var value = _compiler.gtNewLclFldNode(field.Type, newLclNum, field.lvFldOffset);
                                var store = _compiler.gtNewStoreLclVarNode(fieldLclNum, value);
                                block.InsertBefore(use.User(), value);
                                block.InsertBefore(use.User(), store);
                                DISPTREERANGE(block, store);
                            }

                            assert(use.Def().Oper is GT_LCL_VAR);
                            block.Remove(use.Def());
                            block.Remove(use.User());
                        }
                    }
                }
                else
                {
                    analyses?.Update(next);
                }

                var callStore = call.Next ?? throw new InvalidOperationException("Async call result has no local store");
                assert(callStore.Oper.IsLocalStore && callStore.AsLclVarCommon().Data == call);
                definition.DefinitionNode = callStore.AsLclVarCommon();
                definition.InsertAfter = callStore;
            }

            if (retbufArg is not null)
            {
                assert(call.Type is TYP_VOID);
                var retbufNode = retbufArg.Node;
                noway_assert(retbufNode.Oper is GT_LCL_ADDR);
                definition.DefinitionNode = retbufNode.AsLclVarCommon();
            }

            return definition;
        }

        private BasicBlock CreateCheckAndSuspendAfterCall(
            BasicBlock block, GenTreeCall call, AsyncCallDefinitionInfo definition, BasicBlock suspendBB)
        {
            var continuationArg = new GenTree(GT_ASYNC_CONTINUATION, TYP_REF) {
                HasOrderingSideEffect = true,
            };
            var storeContinuation =
                _compiler.gtNewStoreLclVarNode(GetReturnedContinuationVar(), continuationArg);
            var insertAfter = definition.InsertAfter ??
                throw new InvalidOperationException("Async call has no insertion point");
            block.InsertAfter(insertAfter, continuationArg);
            block.InsertAfter(continuationArg, storeContinuation);

            var alwaysSuspends = call.GetAsyncInfo().AlwaysSuspends;
            GenTree lastNode = storeContinuation;
            if (!alwaysSuspends)
            {
                var nullValue = _compiler.gtNewNull();
                var returnedContinuation = _compiler.gtNewLclvNode(TYP_REF, GetReturnedContinuationVar());
                var nonNull = _compiler.gtNewBinaryNode(GT_NE, TYP_INT, returnedContinuation, nullValue);
                var branch = _compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, nonNull);

                block.InsertAfter(storeContinuation, nullValue);
                block.InsertAfter(nullValue, returnedContinuation);
                block.InsertAfter(returnedContinuation, nonNull);
                block.InsertAfter(nonNull, branch);
                lastNode = branch;
            }

            var remainder = _compiler.fgSplitBlockAfterNode(block, lastNode);
            JITDUMP($"  Remainder is {FMT_BB(remainder.bbNum)}\n");

            var debugInfo = call.GetAsyncInfo().CallAsyncDebugInfo.GetRoot();
            if (!debugInfo.GetParent(out _))
            {
                var awaitOffset = debugInfo.Location.Offset;
                block.bbCodeOffsEnd = awaitOffset + 1;
                remainder.bbCodeOffs = awaitOffset + 1;
            }

            if (alwaysSuspends)
            {
                _compiler.fgRemoveRefPred(block.TargetEdge);
                var edge = _compiler.fgAddRefPred(suspendBB, block);
                block.SetKindAndTargetEdge(BBJ_ALWAYS, edge);

                if (_compiler.fgPgoConsistent)
                {
#if DEBUG
                    JITDUMP($"Marking profile inconsistent due to always-suspend helper [{call.TreeId:D6}]\n");
#endif
                    _compiler.fgPgoConsistent = false;
                }
            }
            else
            {
                var edge = _compiler.fgAddRefPred(suspendBB, block);
                block.SetCond(edge, block.TargetEdge);
                block.TrueEdge.Likelihood = 0;
                block.FalseEdge.Likelihood = 1;
            }

            return remainder;
        }
    }
}
