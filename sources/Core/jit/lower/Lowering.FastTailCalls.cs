// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private void LowerFastTailCall(GenTreeCall call)
    {
#if FEATURE_FASTTAILCALL && TARGET_AMD64
        var compiler = CompilerInstance;
        assert((compiler.info.compFlags & CORINFO_FLG_SYNCH) == 0);
        assert(!compiler.opts.IsReversePInvoke);
        assert(!call.IsUnmanaged);
        assert(!compiler.compLocallocUsed);
        assert(call.IsFastTailCall);

        if (compiler.compMethodRequiresPInvokeFrame)
        {
            assert(compiler.compCurBB is not null);
            InsertPInvokeMethodEpilog(compiler.compCurBB, call);
        }

        var putargs = new List<GenTreePutArgStk>();
        foreach (var arg in call.Args.Args)
        {
            if (arg.Node is GenTreePutArgStk putarg)
            {
                putargs.Add(putarg);
            }
        }

        GenTree? startNonGCNode = null;
        if (putargs.Count != 0)
        {
            GenTree firstPutargStk = putargs[0];
            var firstPutargStkOp = FirstOperand(firstPutargStk);
            for (var i = 1; i < putargs.Count; i++)
            {
                firstPutargStk = FirstNode(firstPutargStk, putargs[i]);
                firstPutargStkOp = FirstNode(firstPutargStkOp, FirstOperand(putargs[i]));
            }

            foreach (var put in putargs)
            {
                var overwrittenStart = unchecked((uint)put.ArgOffset);
                var overwrittenEnd = unchecked(overwrittenStart + (uint)put.StackByteSize);

                for (var callerArgLclNum = 0; callerArgLclNum < compiler.info.compArgsCount; callerArgLclNum++)
                {
                    ref var callerArgDsc = ref compiler.lvaGetDesc(callerArgLclNum);
                    if (callerArgDsc.lvIsRegArg)
                    {
                        continue;
                    }

                    ref readonly var abiInfo = ref compiler.lvaGetParameterAbiInfo(callerArgLclNum);
                    assert(abiInfo.HasExactlyOneStackSegment);
                    ref readonly var segment = ref abiInfo.Segments[0];
                    var argStart = unchecked((uint)segment.StackOffset);
                    var argEnd = unchecked(argStart + (uint)segment.StackSize);
                    if ((overwrittenEnd <= argStart) || (overwrittenStart >= argEnd))
                    {
                        continue;
                    }

#if DEBUG
                    JITDUMP($"PUTARG_STK [{put.TreeId:D6}] overwrites [{overwrittenStart:D6}..{overwrittenEnd:D6}); parameter V{callerArgLclNum:D3} lives in [{argStart:D6}..{argEnd:D6}); may need defensive copies\n");
#endif
                    var lookForUsesFrom = put.Next ??
                        throw new InvalidOperationException("Stack argument must precede the tail call.");
                    if ((overwrittenStart != argStart) || (put.Op1.Oper is GT_FIELD_LIST))
                    {
                        JITDUMP("Non-atomic copy may be self-interfering. Expanding search...\n");
                        lookForUsesFrom = firstPutargStkOp;
                    }

                    RehomeArgForFastTailCall(callerArgLclNum, firstPutargStkOp, lookForUsesFrom, call);
                    callerArgDsc = ref compiler.lvaGetDesc(callerArgLclNum);
                    if (!callerArgDsc.lvPromoted)
                    {
                        continue;
                    }

                    var fieldsEnd = callerArgDsc.lvFieldLclStart + callerArgDsc.lvFieldCnt;
                    for (var field = callerArgDsc.lvFieldLclStart; field < fieldsEnd; field++)
                    {
                        RehomeArgForFastTailCall(field, firstPutargStkOp, lookForUsesFrom, call);
                    }
                }
            }

            startNonGCNode = new GenTree(GT_START_NONGC, TYP_VOID);
            BlockRange().InsertBefore(firstPutargStk, startNonGCNode);

            var currentBlock = compiler.compCurBB ??
                throw new InvalidOperationException("Fast tailcall lowering requires a current block.");
            if ((compiler.fgBBcount == 1) && !currentBlock.HasFlag(BBF_GC_SAFE_POINT))
            {
                assert(compiler.fgFirstBB == currentBlock);
                BlockRange().InsertBefore(startNonGCNode, new GenTree(GT_NO_OP, TYP_VOID));
            }
        }

        if (compiler.compIsProfilerHookNeeded)
        {
            InsertProfTailCallHook(call, startNonGCNode);
        }
#else
        throw new NotImplementedException("Fast tailcall lowering outside AMD64 with FEATURE_FASTTAILCALL is not ported.");
#endif
    }

#if FEATURE_FASTTAILCALL && TARGET_AMD64
    private static GenTree FirstNode(GenTree first, GenTree second)
        => ReferenceEquals(LIR.LastNode(first, second), first) ? second : first;

    private GenTree FirstOperand(GenTree node)
    {
        GenTree? result = null;
        Visit(node);
        return result ?? throw new InvalidOperationException("Stack argument has no operand.");

        void Visit(GenTree current)
        {
            _ = current.VisitOperands(operand => {
                result = result is null ? operand : FirstNode(result, operand);
                if (operand.IsContained)
                {
                    Visit(operand);
                }
                return GenTree.VisitResult.Continue;
            });
        }
    }

    private void RehomeArgForFastTailCall(int lclNum, GenTree insertTempBefore,
        GenTree lookForUsesStart, GenTreeCall callNode)
    {
        var compiler = CompilerInstance;
        var tmpLclNum = BAD_VAR_NUM;
        for (var treeNode = lookForUsesStart; treeNode != callNode;
             treeNode = treeNode.Next ?? throw new InvalidOperationException("Tail-call argument use extends beyond the call."))
        {
            if (!treeNode.Oper.IsLocal && (treeNode.Oper is not GT_LCL_ADDR))
            {
                continue;
            }

            var local = treeNode.AsLclVarCommon();
            if (local.LclNum != lclNum)
            {
                continue;
            }

            if (tmpLclNum == BAD_VAR_NUM)
            {
                tmpLclNum = compiler.lvaGrabTemp(true, "Fast tail call lowering is creating a new local variable");
                ref var callerArgDsc = ref compiler.lvaGetDesc(lclNum);
                var tmpType = callerArgDsc.Type.ActualType;
                ref var tmpDsc = ref compiler.lvaGetDesc(tmpLclNum);
                tmpDsc.Type = tmpType;
                tmpDsc.lvDoNotEnregister = callerArgDsc.lvDoNotEnregister;

                var value = compiler.gtNewLclvNode(tmpType, lclNum);
                if (tmpType is TYP_STRUCT)
                {
                    compiler.lvaSetStruct(tmpLclNum, callerArgDsc.Layout ??
                        throw new InvalidOperationException("Struct parameter has no layout."),
                        unsafeValueClsCheck: false);
                }

                var store = compiler.gtNewStoreLclVarNode(tmpLclNum, value);
                BlockRange().InsertBefore(insertTempBefore, LIR.SeqTree(compiler, store));
                ContainCheckRange(value, store);
                _ = LowerNode(store);
            }

            local.LclNum = tmpLclNum;
        }
    }
#endif
}
