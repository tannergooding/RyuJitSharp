// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus optOptimizeFlow()
    {
        noway_assert(opts.OptimizationEnabled);

        var modified = fgUpdateFlowGraph(doTailDuplication: true);

        // TODO: Always rely on profile synthesis to identify cold blocks.
        if (!fgIsUsingProfileWeights)
        {
            modified |= fgExpandRarelyRunBlocks();
        }

        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    public PhaseStatus optOptimizePreLayout()
    {
        assert(opts.OptimizationEnabled);

        var modified = fgUpdateFlowGraph();

        // TODO: Always rely on profile synthesis to identify cold blocks.
        if (!fgIsUsingProfileWeights)
        {
            modified |= fgExpandRarelyRunBlocks();
        }

        // Run a late pass of unconditional-to-conditional branch optimization, skipping handler blocks.
        for (var block = fgFirstBB; block != fgFirstFuncletBB; block = block.Next)
        {
            assert(block is not null);
            modified |= fgOptimizeBranch(block);
        }

        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    public PhaseStatus optOptimizePostLayout()
    {
        assert(opts.OptimizationEnabled);
        var status = PhaseStatus.MODIFIED_NOTHING;

        foreach (var block in Blocks)
        {
            if ((block.Kind is BBJ_COND) && block.CanRemoveJumpToTarget(block.TrueTarget, this))
            {
                var test = block.GetLastNode();
                assert(test is not null && test.Oper.IsConditionalJump);

                // LSRA is complete, so reversal must not introduce an IR node.
                var condition = test;
                if (test.Oper is GT_JTRUE)
                {
                    condition = test.AsUnOp().Op1.SkipCopyOrReload;
                }

                if (!gtTryReverseCond(condition))
                {
                    continue;
                }

                var oldTrueEdge = block.TrueEdge;
                var oldFalseEdge = block.FalseEdge;
                block.TrueEdge = oldFalseEdge;
                block.FalseEdge = oldTrueEdge;

                assert(block.CanRemoveJumpToTarget(block.FalseTarget, this));
                status = PhaseStatus.MODIFIED_EVERYTHING;
            }
        }

        return status;
    }
}
