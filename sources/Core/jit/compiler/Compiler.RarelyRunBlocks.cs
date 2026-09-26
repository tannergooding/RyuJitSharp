// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public bool fgExpandRarelyRunBlocks()
    {
        var result = false;
        JITDUMP("\n*************** In fgExpandRarelyRunBlocks()\n");

        // Find the lexically earliest predecessor affected by a newly cold block.
        // Pred lists are not necessarily in lexical order.
        static BasicBlock? NewRunRarely(BasicBlock bPrev)
        {
            BasicBlock? bPrevPrev = null;

            if (bPrev.Kind is BBJ_CALLFINALLYRET)
            {
                var pair = bPrev.Prev;
                noway_assert(pair is not null && pair.isBBCallFinallyPair);
                bPrevPrev = pair;
            }

            var pred = bPrev.bbPreds;
            while (pred is not null)
            {
                if (bPrevPrev is null)
                {
                    bPrevPrev = pred.SourceBlock;
                    continue;
                }

                for (var candidate = pred.SourceBlock; candidate is not null; candidate = candidate.Next)
                {
                    if (candidate == bPrevPrev)
                    {
                        bPrevPrev = pred.SourceBlock;
                        break;
                    }
                    else if (candidate == bPrev)
                    {
                        break;
                    }
                }

                pred = pred.NextPredEdge;
            }

            if (bPrevPrev is not null)
            {
                for (var candidate = bPrevPrev; candidate is not null; candidate = candidate.Next)
                {
                    if (candidate == bPrev)
                    {
                        return bPrevPrev;
                    }
                }
            }

            return null;
        }

        var bPrev = fgFirstBB;
        assert(bPrev is not null);

        for (var block = bPrev.Next; block is not null; bPrev = block, block = block.Next)
        {
            if (bPrev.isRunRarely || bPrev.hasProfileWeight)
            {
                continue;
            }

            string? reason = null;
            var setRarelyRun = false;

            switch (bPrev.Kind)
            {
                case BBJ_ALWAYS:
                {
                    if (bPrev.Target.isRunRarely)
                    {
                        reason = "Unconditional jump to a rarely run block";
                        setRarelyRun = true;
                    }
                    break;
                }

                case BBJ_CALLFINALLY:
                {
                    if (bPrev.isBBCallFinallyPair && block.isRunRarely)
                    {
                        reason = "Call of finally followed rarely run continuation block";
                        setRarelyRun = true;
                    }
                    break;
                }

                case BBJ_CALLFINALLYRET:
                {
                    if (bPrev.Target.isRunRarely)
                    {
                        reason = "Finally continuation is a rarely run block";
                        setRarelyRun = true;
                    }
                    break;
                }

                case BBJ_COND:
                {
                    if (bPrev.TrueTarget.isRunRarely && bPrev.FalseTarget.isRunRarely)
                    {
                        reason = "Both sides of a conditional jump are rarely run";
                        setRarelyRun = true;
                    }
                    break;
                }
            }

            if (setRarelyRun)
            {
                JITDUMP($"{reason}, marking {FMT_BB(bPrev.bbNum)} as rarely run\n");
                noway_assert(!bPrev.isRunRarely);
                bPrev.bbSetRunRarely();
                result = true;

                var backtrack = NewRunRarely(bPrev);
                if (backtrack is not null)
                {
                    block = backtrack;
                }
            }
        }

        bPrev = fgFirstBB;
        assert(bPrev is not null);

        for (var block = bPrev.Next; block is not null; bPrev = block, block = block.Next)
        {
            if (!block.isRunRarely && !block.isBBCallFinallyPairTail)
            {
                var rare = true;

                foreach (var predecessor in block.PredBlocks)
                {
                    if (!predecessor.isRunRarely)
                    {
                        rare = false;
                        break;
                    }
                }

                if (rare && bbIsHandlerBeg(block))
                {
                    rare = false;
                }

                if (rare)
                {
                    block.bbSetRunRarely();
                    result = true;
                    JITDUMP($"All branches to {FMT_BB(block.bbNum)} are from rarely run blocks, marking as rarely run\n");

                    if (block.isBBCallFinallyPair)
                    {
                        var pairTail = block.Next;
                        assert(pairTail is not null);
                        pairTail.bbSetRunRarely();
                        JITDUMP($"Also marking the BBJ_CALLFINALLYRET at {FMT_BB(pairTail.bbNum)} as rarely run\n");
                    }
                }
            }

            if (bPrev.isBBCallFinallyPair && (bPrev.bbWeight != block.bbWeight) && !bPrev.hasProfileWeight)
            {
                if (block.isRunRarely)
                {
                    bPrev.bbWeight = block.bbWeight;
                    JITDUMP($"Marking the BBJ_CALLFINALLY block at {FMT_BB(bPrev.bbNum)} as rarely run because {FMT_BB(block.bbNum)} is rarely run\n");
                }
                else if (bPrev.isRunRarely)
                {
                    block.bbWeight = bPrev.bbWeight;
                    JITDUMP($"Marking the BBJ_CALLFINALLYRET block at {FMT_BB(block.bbNum)} as rarely run because {FMT_BB(bPrev.bbNum)} is rarely run\n");
                }
                else
                {
                    bPrev.bbWeight = block.bbWeight;
                }

                noway_assert(block.bbWeight == bPrev.bbWeight);
            }
        }

        return result;
    }
}
