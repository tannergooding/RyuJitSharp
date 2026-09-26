// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    public void optSetMappedBlockTargets(BasicBlock block, BasicBlock clone,
        IReadOnlyDictionary<BasicBlock, BasicBlock> redirectMap)
    {
        assert(clone.Kind is BBJ_ALWAYS);
        assert(!clone.HasInitializedTarget);

        BasicBlock Redirect(BasicBlock target)
        {
            return redirectMap.TryGetValue(target, out var redirected) ? redirected : target;
        }

        switch (block.Kind)
        {
            case BBJ_ALWAYS:
            case BBJ_CALLFINALLY:
            case BBJ_CALLFINALLYRET:
            case BBJ_LEAVE:
            case BBJ_EHCATCHRET:
            case BBJ_EHFILTERRET:
            {
                var edge = fgAddRefPred(Redirect(block.Target), clone);
                clone.SetKindAndTargetEdge(block.Kind, edge);
                break;
            }

            case BBJ_COND:
            {
                var trueEdge = fgAddRefPred(Redirect(block.TrueTarget), clone, block.TrueEdge);
                var falseEdge = fgAddRefPred(Redirect(block.FalseTarget), clone, block.FalseEdge);
                clone.SetCond(trueEdge, falseEdge);
                break;
            }

            case BBJ_EHFINALLYRET:
            {
                var targets = block.EhfTargets
                    ?? throw new FatalJitException("A finally return requires its successor table.");
                var successors = new FlowEdge[targets.Succs.Length];
                for (var index = 0; index < successors.Length; index++)
                {
                    var originalEdge = targets.Succs[index];
                    successors[index] = fgAddRefPred(
                        Redirect(originalEdge.DestinationBlock), clone, originalEdge);
                }
                clone.SetEhf(new BBJumpTable(successors));
                break;
            }

            case BBJ_SWITCH:
            {
                var oldTargets = block.SwitchTargets;
                var newTargets = new BBswtDesc(this, oldTargets);
                var uniqueSuccessors = newTargets.Succs;
                var uniqueCount = 0;

                for (var index = 0; index < newTargets.Cases.Length; index++)
                {
                    var originalEdge = oldTargets.Cases[index];
                    var edge = fgAddRefPred(Redirect(originalEdge.DestinationBlock), clone);
                    // Transfer a switch edge's likelihood when its final duplicate case is added.
                    if (edge.DupCount == originalEdge.DupCount)
                    {
                        edge.Likelihood = originalEdge.Likelihood;
                        uniqueSuccessors[uniqueCount++] = edge;
                    }
                    newTargets.Cases[index] = edge;
                }
                clone.SwitchTargets = newTargets;
                break;
            }

            default:
            {
                assert(block.NumSucc == 0);
                clone.SetKindAndTargetEdge(block.Kind, null);
                break;
            }
        }

        assert(clone.Kind == block.Kind);
    }
}
