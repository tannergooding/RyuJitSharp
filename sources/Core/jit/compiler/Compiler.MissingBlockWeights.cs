// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgprofile.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgComputeBlockWeights()
    {
        fgModified = false;
#if DEBUG
        if (verbose)
        {
            fgDispBasicBlocks();
            jitprintf("\n");
        }
#endif
        if (fgIsUsingProfileWeights)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        return fgComputeMissingBlockWeights() ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    public bool fgComputeMissingBlockWeights()
    {
        uint iterations = 0;
        bool changed;
        var modified = false;
        do
        {
            changed = false;
            iterations++;

            foreach (var block in Blocks)
            {
                if (!block.hasProfileWeight && (block.bbPreds is not null))
                {
                    var newWeight = BB_MAX_WEIGHT;
                    if (block.CountOfInEdges == 1)
                    {
                        var source = block.bbPreds.SourceBlock;
                        var onlyNext = source.Kind is BBJ_ALWAYS ? source.Target : null;
                        if ((onlyNext == block) && source.hasProfileWeight)
                        {
                            newWeight = source.bbWeight;
                        }
                    }

                    var successor = block.Kind switch {
                        BBJ_ALWAYS => block.Target,
                        // Native retains the callfinally continuation quirk to avoid asmdiffs.
                        BBJ_CALLFINALLYRET => fgGetFinallyContinuation(block),
                        _ => null,
                    };

                    if ((successor is not null) && (successor.bbPreds is not null) &&
                        (successor.CountOfInEdges == 1))
                    {
                        noway_assert(successor.bbPreds.SourceBlock == block);
                        newWeight = successor.bbWeight;
                    }

                    if (bbIsHandlerBeg(block))
                    {
                        var source = block.bbPreds.SourceBlock;
                        // Native changes handler-entry weights only when splitting.
                        if (fgFirstColdBlock is not null)
                        {
                            newWeight = source.Kind is BBJ_CALLFINALLY ? source.bbWeight : BB_ZERO_WEIGHT;
                        }
                    }

                    if ((newWeight != BB_MAX_WEIGHT) && (block.bbWeight != newWeight))
                    {
                        changed = true;
                        modified = true;
                        block.bbWeight = newWeight;
                    }
                }
                else if (!block.hasProfileWeight && bbIsHandlerBeg(block) && !block.isRunRarely)
                {
                    if (fgFirstColdBlock is not null)
                    {
                        changed = true;
                        modified = true;
                        block.bbSetRunRarely();
                    }
                }
            }
        }
        // Branch removal can leave an unreachable cycle whose weights oscillate (native test b539509).
        while (changed && (iterations < 10));

#if DEBUG
        if (verbose && modified)
        {
            jitprintf("fgComputeMissingBlockWeights() adjusted the weight of some blocks\n");
            fgDispBasicBlocks();
            jitprintf("\n");
        }
#endif
        return modified;
    }
}
