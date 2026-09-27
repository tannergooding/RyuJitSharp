// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class AsyncTransformation
    {
        private AsyncState? FindReusableSuspension(BasicBlock block, GenTreeCall call,
            AsyncCallDefinitionInfo definition, AsyncContinuationLayoutBuilder builder,
            bool resumeReachable, VARSET_TP mutatedSinceResumption)
        {
            if (_compiler.opts.OptimizationDisabled)
            {
                return null;
            }

            var lastNode = block.LastNode;
            if (lastNode != call && lastNode != definition.DefinitionNode)
            {
                return null;
            }

            var target = block.UniqueSucc;
            if (target is null)
            {
                return null;
            }

            foreach (var predecessor in target.PredBlocks)
            {
                if (predecessor == block || (predecessor.Kind is not BBJ_ALWAYS) || !predecessor.isEmpty())
                {
                    continue;
                }

                var limit = 128;
                for (var i = _states.Count - 1; i >= 0 && limit-- > 0; i--)
                {
                    var state = _states[i];
                    assert(state.ResumptionBB.Kind is BBJ_ALWAYS);
                    if ((state.ResumptionBB.UniqueSucc == predecessor) &&
                        IsReusableSuspension(state, block, call, definition, builder,
                            resumeReachable, mutatedSinceResumption))
                    {
                        return state;
                    }
                }
            }

            return null;
        }

        private static bool EqualLocalDefinitions(GenTreeLclVarCommon lhs, GenTreeLclVarCommon rhs)
        {
            if ((lhs.Oper != rhs.Oper) || (lhs.LclNum != rhs.LclNum) || (lhs.LclOffs != rhs.LclOffs))
            {
                return false;
            }

            if ((lhs.Oper is GT_LCL_FLD or GT_STORE_LCL_FLD) &&
                !ReferenceEquals(lhs.AsLclFld().Layout, rhs.AsLclFld().Layout))
            {
                return false;
            }

            return true;
        }

        private static List<GenTree> CollectWellKnownArgs(GenTreeCall call, WellKnownArg kind)
        {
            var result = new List<GenTree>();
            foreach (var arg in call.Args.Args)
            {
                if (arg.WellKnownArg == kind)
                {
                    result.Add(arg.Node);
                }
            }

            return result;
        }

        private unsafe bool IsReusableSuspension(AsyncState state, BasicBlock block, GenTreeCall call,
            AsyncCallDefinitionInfo definition, AsyncContinuationLayoutBuilder builder,
            bool resumeReachable, VARSET_TP mutatedSinceResumption)
        {
            var previous = state.Call;
#if DEBUG
            JITDUMP($"  Sibling to the join is resumption for async call [{previous.TreeId:D6}]; checking for possible tail merging of suspension points\n");
#endif

            if (!BasicBlock.sameEHRegion(block, state.CallBlock))
            {
                JITDUMP("    Not same EH region\n");
                return false;
            }

            if ((definition.DefinitionNode is null) != (state.CallDefInfo.DefinitionNode is null))
            {
                JITDUMP("    No; disagreement on presence of return value\n");
                return false;
            }

            if (definition.DefinitionNode is not null)
            {
                var priorDefinition = state.CallDefInfo.DefinitionNode ??
                    throw new System.InvalidOperationException("Reusable state lacks its return definition");
                if (!EqualLocalDefinitions(definition.DefinitionNode, priorDefinition))
                {
#if DEBUG
                    JITDUMP($"    No; disagreement on return value destination " +
                        $"([{definition.DefinitionNode.TreeId:D6}] does not equal [{priorDefinition.TreeId:D6}])\n");
#endif
                    return false;
                }
            }

            if (call._returnType != previous._returnType)
            {
                JITDUMP($"    No; disagreement on return type ({call._returnType.Name} vs {previous._returnType.Name})\n");
                return false;
            }

            if (call._returnType is TYP_STRUCT)
            {
                var thisLayout = _compiler.typGetObjLayout(call.RetClsHnd);
                var otherLayout = _compiler.typGetObjLayout(previous.RetClsHnd);
                if (!ClassLayout.AreCompatible(thisLayout, otherLayout))
                {
#if DEBUG
                    JITDUMP($"    No; disagreement on return type ({thisLayout.ClassName} vs {otherLayout.ClassName})\n");
#endif
                    return false;
                }
            }

            var currentInfo = call.GetAsyncInfo();
            var previousInfo = previous.GetAsyncInfo();
            if (currentInfo.ContinuationContextHandling != previousInfo.ContinuationContextHandling)
            {
                JITDUMP($"    No; disagreement on continuation context handling " +
                    $"({(int)currentInfo.ContinuationContextHandling} vs {(int)previousInfo.ContinuationContextHandling})\n");
                return false;
            }

            if (currentInfo.NeedsToSaveAndRestoreExecutionContext != previousInfo.NeedsToSaveAndRestoreExecutionContext)
            {
                JITDUMP("    No; disagreement on whether execution context needs to be saved and restored " +
                    $"({(currentInfo.NeedsToSaveAndRestoreExecutionContext ? "yes" : "no")} vs " +
                    $"{(previousInfo.NeedsToSaveAndRestoreExecutionContext ? "yes" : "no")})\n");
                return false;
            }

            if (state.ResumeReachable != resumeReachable)
            {
                JITDUMP($"    No; disagreement on resume reachability " +
                    $"({(state.ResumeReachable ? "yes" : "no")} vs {(resumeReachable ? "yes" : "no")})\n");
                return false;
            }

            if (resumeReachable)
            {
                foreach (var local in builder.Locals)
                {
                    ref var descriptor = ref _compiler.lvaGetDesc(local);
                    var currentSet = GetLocalSaveSet(in descriptor, mutatedSinceResumption);
                    var previousSet = GetLocalSaveSet(in descriptor, state.MutatedSincePreviousResumption);
                    if (currentSet != previousSet)
                    {
                        JITDUMP($"    No; disagreement on save set for V{local:D2} ({(int)currentSet} vs {(int)previousSet})\n");
                        return false;
                    }
                }
            }

            WellKnownArg[] kinds = [
                WellKnownArg.AsyncAwaiter,
                WellKnownArg.AsyncResumedUse,
                WellKnownArg.AsyncResumedDef,
                WellKnownArg.AsyncExecutionContext,
                WellKnownArg.AsyncSynchronizationContext,
            ];

            foreach (var kind in kinds)
            {
                var currentNodes = CollectWellKnownArgs(call, kind);
                var previousNodes = CollectWellKnownArgs(previous, kind);
                if (currentNodes.Count != previousNodes.Count)
                {
#if DEBUG
                    JITDUMP($"    No; disagreement on number of {kind} arguments " +
                        $"({currentNodes.Count} vs {previousNodes.Count})\n");
#endif
                    return false;
                }

                for (var i = 0; i < currentNodes.Count; i++)
                {
                    var node = currentNodes[i];
                    if (!node.Oper.IsAnyLocal && !node.IsInvariant)
                    {
#if DEBUG
                        JITDUMP($"    No; {kind} argument is too complex\n");
#endif
                        return false;
                    }

                    if (!GenTree.Compare(node, previousNodes[i]))
                    {
#if DEBUG
                        JITDUMP($"    No; disagreement on value of {kind} " +
                            $"argument ([{node.TreeId:D6}] vs [{previousNodes[i].TreeId:D6}])\n");
#endif
                        return false;
                    }
                }
            }

            return AsyncContinuationLayoutBuilder.Equals(builder, state.Layout);
        }

        private static void HandleReusedSuspension(BasicBlock block, GenTreeCall call)
        {
            WellKnownArg[] kinds = [
                WellKnownArg.AsyncAwaiter,
                WellKnownArg.AsyncResumedUse,
                WellKnownArg.AsyncResumedDef,
                WellKnownArg.AsyncExecutionContext,
                WellKnownArg.AsyncSynchronizationContext,
            ];

            foreach (var kind in kinds)
            {
                for (var arg = call.Args.FindWellKnownArg(kind); arg is not null;
                     arg = call.Args.FindWellKnownArg(kind))
                {
                    assert(arg.Node.Oper.IsAnyLocal || arg.Node.IsInvariant);
                    block.Remove(arg.Node);
                    call.Args.RemoveUnsafe(arg);
                }
            }
        }
    }
}
