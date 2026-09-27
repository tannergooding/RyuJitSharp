// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class AsyncTransformation(Compiler compiler)
    {
        private readonly Compiler _compiler = compiler;
        private readonly List<AsyncState> _states = [];
        private int _returnedContinuationVar = BAD_VAR_NUM;
        private int _newContinuationVar = BAD_VAR_NUM;
        private int _reuseContinuationVar = BAD_VAR_NUM;
        private int _dataArrayVar = BAD_VAR_NUM;
        private int _gcDataArrayVar = BAD_VAR_NUM;
        private int _resultBaseVar = BAD_VAR_NUM;
        private int _exceptionVar = BAD_VAR_NUM;
        private BasicBlock? _lastSuspensionBB;
        private BasicBlock? _lastResumptionBB;
        private BasicBlock? _sharedReturnBB;
        private BasicBlock? _sharedFinishContextHandlingWithContinuationContextBB;
        private BasicBlock? _sharedFinishContextHandlingWithoutContinuationContextBB;
        private int _sharedFinishContextHandlingResumedVar = BAD_VAR_NUM;
        private int _sharedFinishContextHandlingExecContextVar = BAD_VAR_NUM;
        private int _sharedFinishContextHandlingSyncContextVar = BAD_VAR_NUM;

        private readonly struct AsyncLivenessPolicy : ILivenessPolicy
        {
            public static bool IsLIR => true;
            public static bool EliminateDeadCode => false;
            public static bool TrackAddressExposedLocals => true;
        }

        private readonly record struct AsyncAwaitCounts(int Normal, int Tail);

        private sealed class AsyncCallDefinitionInfo
        {
            public GenTreeLclVarCommon? DefinitionNode { get; set; }
            public GenTree? InsertAfter { get; set; }
        }

        private sealed class AsyncState(
            int number, AsyncContinuationLayoutBuilder layout, BasicBlock callBlock,
            GenTreeCall call, AsyncCallDefinitionInfo definition, BasicBlock suspension,
            BasicBlock resumption, bool resumeReachable, VARSET_TP mutatedSincePreviousResumption)
        {
            public int Number { get; } = number;
            public AsyncContinuationLayoutBuilder Layout { get; } = layout;
            public BasicBlock CallBlock { get; } = callBlock;
            public GenTreeCall Call { get; } = call;
            public AsyncCallDefinitionInfo CallDefInfo { get; } = definition;
            public BasicBlock SuspensionBB { get; } = suspension;
            public BasicBlock ResumptionBB { get; } = resumption;
            public bool ResumeReachable { get; } = resumeReachable;
            public VARSET_TP MutatedSincePreviousResumption { get; } = mutatedSincePreviousResumption;
        }

        private AsyncAwaitCounts FindAwaits(List<BasicBlock> normalAwaitBlocks,
            List<BasicBlock> tailAwaitBlocks, List<GenTree> continuationMemberOffsets)
        {
            var normalCount = 0;
            var tailCount = 0;

            foreach (var block in _compiler.Blocks)
            {
                var hasNormalAwait = false;
                var hasTailAwait = false;

                foreach (var tree in block)
                {
                    if (tree.Oper is GT_CONTINUATION_MEMBER_OFFSET)
                    {
                        continuationMemberOffsets.Add(tree);
                        continue;
                    }

                    if (tree is not GenTreeCall call || !call.IsAsync || call.IsTailCall)
                    {
                        continue;
                    }

                    if (call.GetAsyncInfo().IsTailAwait)
                    {
                        hasTailAwait = true;
                        tailCount++;
                    }
                    else
                    {
                        hasNormalAwait = true;
                        normalCount++;
                    }
                }

                if (hasNormalAwait)
                {
                    normalAwaitBlocks.Add(block);
                }

                if (hasTailAwait)
                {
                    tailAwaitBlocks.Add(block);
                }
            }

            return new AsyncAwaitCounts(normalCount, tailCount);
        }

        private int GetReturnedContinuationVar()
        {
            if (_returnedContinuationVar == BAD_VAR_NUM)
            {
                _returnedContinuationVar = _compiler.lvaGrabTemp(false, "returned continuation");
                _compiler.lvaGetDesc(_returnedContinuationVar).Type = TYP_REF;
            }

            return _returnedContinuationVar;
        }

        private int GetNewContinuationVar()
        {
            if (_newContinuationVar == BAD_VAR_NUM)
            {
                _newContinuationVar = _compiler.lvaGrabTemp(false, "new continuation");
                _compiler.lvaGetDesc(_newContinuationVar).Type = TYP_REF;
            }

            return _newContinuationVar;
        }

        private int GetResultBaseVar()
        {
            if ((_resultBaseVar == BAD_VAR_NUM) || !_compiler.lvaHaveManyLocals())
            {
                _resultBaseVar = _compiler.lvaGrabTemp(false, "object for resuming result base");
                _compiler.lvaGetDesc(_resultBaseVar).Type = TYP_REF;
            }

            return _resultBaseVar;
        }

        private int GetExceptionVar()
        {
            if ((_exceptionVar == BAD_VAR_NUM) || !_compiler.lvaHaveManyLocals())
            {
                _exceptionVar = _compiler.lvaGrabTemp(false, "object for resuming exception");
                _compiler.lvaGetDesc(_exceptionVar).Type = TYP_REF;
            }

            return _exceptionVar;
        }

        private void CreateSharedReturnBB()
        {
            var block = _compiler.fgNewBBafter(BBJ_RETURN, _compiler.fgLastBBInMainFunction(), false);
            _sharedReturnBB = block;
            block.bbSetRunRarely();
            block.clearTryIndex();
            block.clearHndIndex();

            if (_compiler.fgIsUsingProfileWeights)
            {
                block.SetFlags(BBF_PROF_WEIGHT);
            }

            var continuation = _compiler.gtNewLclvNode(TYP_REF, GetNewContinuationVar());
            var ret = _compiler.gtNewUnaryNode(GT_RETURN_SUSPEND, TYP_VOID, continuation);
            block.InsertAtEnd(continuation);
            block.InsertAtEnd(ret);

            JITDUMP($"Created shared return BB {FMT_BB(block.bbNum)}\n");
            DISPRANGE(block);
        }

        private BasicBlock CreateTailAwaitSuspension(BasicBlock block, GenTreeCall call)
        {
            _lastSuspensionBB ??= _compiler.fgLastBBInMainFunction();

            var suspendBB = _compiler.fgNewBBafter(BBJ_RETURN, _lastSuspensionBB, false);
            suspendBB.clearTryIndex();
            suspendBB.clearHndIndex();
            suspendBB.inheritWeightPercentage(block, 0);
            _lastSuspensionBB = suspendBB;

            if (_sharedReturnBB is not null)
            {
                suspendBB.SetKindAndTargetEdge(BBJ_ALWAYS, _compiler.fgAddRefPred(_sharedReturnBB, suspendBB));
            }

            JITDUMP($"  Creating tail suspension {FMT_BB(suspendBB.bbNum)}\n");

            var returnedContinuation = _compiler.gtNewLclvNode(TYP_REF, GetReturnedContinuationVar());
            if (suspendBB.Kind is BBJ_RETURN)
            {
                var ret = _compiler.gtNewUnaryNode(GT_RETURN_SUSPEND, TYP_VOID, returnedContinuation);
                suspendBB.InsertAtEnd(returnedContinuation);
                suspendBB.InsertAtEnd(ret);
            }
            else
            {
                var store = _compiler.gtNewStoreLclVarNode(GetNewContinuationVar(), returnedContinuation);
                suspendBB.InsertAtEnd(returnedContinuation);
                suspendBB.InsertAtEnd(store);
            }

            return suspendBB;
        }

        private BasicBlock CreateOSRJumpBB(GenTree osrAddress)
        {
            var jump = _compiler.fgNewBBafter(BBJ_THROW, _compiler.fgLastBBInMainFunction(), false);
            jump.bbSetRunRarely();
            jump.clearTryIndex();
            jump.clearHndIndex();

            if (_compiler.fgIsUsingProfileWeights)
            {
                jump.SetFlags(BBF_PROF_WEIGHT);
            }

            JITDUMP($"    Created {FMT_BB(jump.bbNum)} for transitions back into OSR method\n");

            var nonlocalJump = _compiler.gtNewUnaryNode(GT_NONLOCAL_JMP, TYP_VOID, osrAddress);
            jump.InsertAtEnd(LIR.SeqTree(_compiler, nonlocalJump));
            return jump;
        }
    }
}
