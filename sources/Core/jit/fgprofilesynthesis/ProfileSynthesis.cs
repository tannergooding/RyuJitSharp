// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class ProfileSynthesis
{
    public const weight_t epsilon = 0.001;
    private const weight_t throwLikelihood = 0;

    public static PhaseStatus AdjustThrowEdgeLikelihoods(Compiler compiler)
    {
        var dfsTree = compiler._dfsTree;
        assert(dfsTree is not null);
        var traits = dfsTree.PostOrderTraits();
        var willThrow = BitVecOps.MakeEmpty(traits);

        void TweakLikelihoods(BasicBlock block)
        {
            assert(block.Kind is BBJ_COND);
            FlowEdge throwEdge;
            FlowEdge normalEdge;
            if (BitVecOps.IsMember(traits, willThrow, block.TrueTarget.bbPostorderNum))
            {
                throwEdge = block.TrueEdge;
                normalEdge = block.FalseEdge;
            }
            else
            {
                throwEdge = block.FalseEdge;
                normalEdge = block.TrueEdge;
            }
            throwEdge.Likelihood = throwLikelihood;
            normalEdge.Likelihood = 1.0 - throwLikelihood;
        }

        var modified = false;
        for (var i = 0; i < dfsTree.PostOrderCount; i++)
        {
            var block = dfsTree.GetPostOrder(i);
            if (block.Kind is BBJ_THROW)
            {
                JITDUMP($"{FMT_BB(block.bbNum)} will throw.\n");
                BitVecOps.AddElemD(traits, willThrow, i);
            }
            else if ((block.UniqueSucc is BasicBlock uniqueSucc) &&
                BitVecOps.IsMember(traits, willThrow, uniqueSucc.bbPostorderNum))
            {
                JITDUMP($"{FMT_BB(block.bbNum)} flows into a throw block.\n");
                BitVecOps.AddElemD(traits, willThrow, i);
            }
            else
            {
                var anyPathThrows = false;
                var allPathsThrow = true;
                foreach (var succBlock in block.Succs)
                {
                    if (BitVecOps.IsMember(traits, willThrow, succBlock.bbPostorderNum))
                    {
                        anyPathThrows = true;
                    }
                    else
                    {
                        allPathsThrow = false;
                    }
                }
                if (anyPathThrows)
                {
                    if (allPathsThrow)
                    {
                        JITDUMP($"{FMT_BB(block.bbNum)} flows into a throw block.\n");
                        BitVecOps.AddElemD(traits, willThrow, i);
                    }
                    else if ((block.Kind is BBJ_COND) && block.TrueEdge.isHeuristicBased)
                    {
                        JITDUMP($"{FMT_BB(block.bbNum)} can flow into a throw block.\n");
                        assert(block.FalseEdge.isHeuristicBased);
                        TweakLikelihoods(block);
                        modified = true;
                    }
                }
            }
        }

        if (modified && compiler.fgIsUsingProfileWeights)
        {
            JITDUMP($"Modified edge likelihoods. Data {(compiler.fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
            compiler.fgPgoConsistent = false;
        }

        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
