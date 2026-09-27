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
        private GenTreeLclVarCommon? FindAndRemoveCommonAsyncResumedDef()
        {
            if (_states.Count <= 1)
            {
                return null;
            }

            GenTreeLclVarCommon? common = null;
            foreach (var state in _states)
            {
                var definition = _compiler.gtCallGetDefinedAsyncResumedLclAddr(state.Call);
                if (definition is null ||
                    (common is not null && !GenTree.Compare(common, definition)))
                {
                    return null;
                }

                common = definition;
            }

            JITDUMP("  Found common async resumed def node:\n");
            if (common is not null)
            {
                DISPTREE(common);
            }
            foreach (var state in _states)
            {
                var argument = state.Call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedDef) ??
                    throw new InvalidOperationException("Common resumed def has no call argument");
                state.CallBlock.Remove(argument.Node);
                state.Call.Args.RemoveUnsafe(argument);
            }

            return common;
        }

        private BasicBlock CreateSharedFinishContextHandlingBB(
            SuspensionContextHelper helper, AsyncContinuationLayout layout,
            GenTree? invariantResumed, bool execMayVary, bool syncMayVary)
        {
            var sharedReturn = _sharedReturnBB ??
                throw new InvalidOperationException("Shared context handling requires a return block");
            var block = _compiler.fgNewBBbefore(BBJ_ALWAYS, sharedReturn, false);
            block.SetKindAndTargetEdge(BBJ_ALWAYS, _compiler.fgAddRefPred(sharedReturn, block));
            block.bbSetRunRarely();
            block.clearTryIndex();
            block.clearHndIndex();
            if (_compiler.fgIsUsingProfileWeights)
            {
                block.SetFlags(BBF_PROF_WEIGHT);
            }

            GenTree resumed;
            if (invariantResumed is null)
            {
                if (_sharedFinishContextHandlingResumedVar == BAD_VAR_NUM)
                {
                    _sharedFinishContextHandlingResumedVar = _compiler.lvaGrabTemp(
                        false, "resumed for shared finish context handling");
                    _compiler.lvaGetDesc(_sharedFinishContextHandlingResumedVar).Type = TYP_INT;
                }

                resumed = _compiler.gtNewLclvNode(TYP_INT,
                    _sharedFinishContextHandlingResumedVar);
            }
            else
            {
                resumed = _compiler.gtCloneExpr(invariantResumed);
            }

            var execLocal = _compiler.lvaAsyncExecutionContextVar;
            if (execMayVary)
            {
                if (_sharedFinishContextHandlingExecContextVar == BAD_VAR_NUM)
                {
                    _sharedFinishContextHandlingExecContextVar = _compiler.lvaGrabTemp(
                        false, "exec context for shared finish context handling");
                    _compiler.lvaGetDesc(_sharedFinishContextHandlingExecContextVar).Type = TYP_REF;
                }

                execLocal = _sharedFinishContextHandlingExecContextVar;
            }

            var syncLocal = _compiler.lvaAsyncSynchronizationContextVar;
            if (syncMayVary)
            {
                if (_sharedFinishContextHandlingSyncContextVar == BAD_VAR_NUM)
                {
                    _sharedFinishContextHandlingSyncContextVar = _compiler.lvaGrabTemp(
                        false, "sync context for shared finish context handling");
                    _compiler.lvaGetDesc(_sharedFinishContextHandlingSyncContextVar).Type = TYP_REF;
                }

                syncLocal = _sharedFinishContextHandlingSyncContextVar;
            }

            InsertFinishContextHandlingCall(block, layout, helper, resumed,
                _compiler.gtNewLclvNode(TYP_REF, execLocal),
                _compiler.gtNewLclvNode(TYP_REF, syncLocal));
            return block;
        }

        private AsyncContinuationLayout CreateResumptionsAndSuspensions(
            IReadOnlyList<GenTree> continuationMemberOffsets)
        {
            AsyncContinuationLayout? sharedLayout = null;
            if (_states.Count > 1)
            {
                JITDUMP("Creating shared layout:\n");
                var builders = new List<AsyncContinuationLayoutBuilder>(_states.Count);
                foreach (var state in _states)
                {
                    builders.Add(state.Layout);
                }

                var sharedBuilder = AsyncContinuationLayoutBuilder.CreateSharedLayout(
                    _compiler, builders);
                sharedLayout = sharedBuilder.Create(continuationMemberOffsets);

                var withContext = 0;
                var withoutContext = 0;
                var resumedMayVary = false;
                var execMayVary = false;
                var syncMayVary = false;
                GenTree? invariantResumed = null;

                foreach (var state in _states)
                {
                    var helper = GetSuspensionContextHelper(state.Call);
                    if (helper is SuspensionContextHelper.WithContinuationContext)
                    {
                        withContext++;
                    }
                    else if (helper is SuspensionContextHelper.WithoutContinuationContext)
                    {
                        withoutContext++;
                    }

                    if (helper is SuspensionContextHelper.None)
                    {
                        continue;
                    }

                    var resumedArg = state.Call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedUse) ??
                        throw new InvalidOperationException("Async state has no resumed indicator");
                    var resumed = resumedArg.Node;
                    if (resumed.IsInvariant || (resumed.Oper is GT_LCL_VAR))
                    {
                        if ((invariantResumed is null) || GenTree.Compare(invariantResumed, resumed))
                        {
                            invariantResumed = resumed;
                        }
                        else
                        {
                            resumedMayVary = true;
                        }
                    }
                    else
                    {
                        resumedMayVary = true;
                    }

                    var exec = state.Call.Args.FindWellKnownArg(WellKnownArg.AsyncExecutionContext) ??
                        throw new InvalidOperationException("Async state has no execution context");
                    var sync = state.Call.Args.FindWellKnownArg(WellKnownArg.AsyncSynchronizationContext) ??
                        throw new InvalidOperationException("Async state has no synchronization context");
                    execMayVary |= (exec.Node.Oper is not GT_LCL_VAR) ||
                        (exec.Node.AsLclVar().LclNum != _compiler.lvaAsyncExecutionContextVar);
                    syncMayVary |= (sync.Node.Oper is not GT_LCL_VAR) ||
                        (sync.Node.AsLclVar().LclNum != _compiler.lvaAsyncSynchronizationContextVar);
                }

                if (withContext > 1)
                {
                    JITDUMP($"Using shared path for final context handling with continuation context -- needed by {withContext} awaits\n");
                    _sharedFinishContextHandlingWithContinuationContextBB =
                        CreateSharedFinishContextHandlingBB(
                            SuspensionContextHelper.WithContinuationContext, sharedLayout,
                            resumedMayVary ? null : invariantResumed, execMayVary, syncMayVary);
                }

                if (withoutContext > 1)
                {
                    JITDUMP($"Using shared path for final context handling without continuation context -- needed by {withoutContext} awaits\n");
                    _sharedFinishContextHandlingWithoutContinuationContextBB =
                        CreateSharedFinishContextHandlingBB(
                            SuspensionContextHelper.WithoutContinuationContext, sharedLayout,
                            resumedMayVary ? null : invariantResumed, execMayVary, syncMayVary);
                }
            }

            AsyncContinuationLayout? layout = null;
            JITDUMP($"Creating suspensions and resumptions for {_states.Count} states\n");
            foreach (var state in _states)
            {
                JITDUMP($"State {state.Number} suspend @ {FMT_BB(state.SuspensionBB.bbNum)}, resume @ {FMT_BB(state.ResumptionBB.bbNum)}\n");
                layout = sharedLayout ?? state.Layout.Create(continuationMemberOffsets);
                CreateSuspension(state.CallBlock, state.Call, state.SuspensionBB,
                    state.Number, layout, state.Layout,
                    state.ResumeReachable, state.MutatedSincePreviousResumption);
                CreateResumption(state.CallBlock, state.Call, state.ResumptionBB,
                    state.CallDefInfo, layout, state.Layout);
                CreateDebugInfoForSuspensionPoint(layout, state.Layout);
                JITDUMP("\n");
            }

            return layout ??
                throw new InvalidOperationException("No async states were created");
        }

        private void CreateResumption(BasicBlock callBlock, GenTreeCall call,
            BasicBlock resumption, AsyncCallDefinitionInfo definition,
            AsyncContinuationLayout layout, AsyncContinuationLayoutBuilder subLayout)
        {
            var debugOffset = new GenTreeILOffset(call.GetAsyncInfo().CallAsyncDebugInfo);
            resumption.InsertAtEnd(LIR.SeqTree(_compiler, debugOffset));
            if (layout.Size > 0)
            {
                RestoreFromDataOnResumption(layout, subLayout, resumption);
            }

            StoreResumedDef(callBlock, call, resumption);
            var resultBlock = subLayout.NeedsException
                ? RethrowExceptionOnResumption(callBlock, layout, resumption)
                : resumption;
            if ((call._returnType is not TYP_VOID) && (definition.DefinitionNode is not null))
            {
                CopyReturnValueOnResumption(call, definition, layout, resultBlock);
            }
        }
    }
}
