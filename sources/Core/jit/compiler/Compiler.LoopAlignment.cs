// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_LOOP_ALIGN
    private bool shouldAlignLoop(FlowGraphNaturalLoop loop, BasicBlock top)
    {
        if (loop.Child is not null)
        {
            JITDUMP($"Skipping alignment for L{loop.Index:D2}; not an innermost loop\n");
            return false;
        }

        if (top == fgFirstBB)
        {
            // Alignment instructions cannot be added in the prolog.
            JITDUMP($"Skipping alignment for L{loop.Index:D2}; loop starts in first block\n");
            return false;
        }

        if (top.HasFlag(BBF_COLD))
        {
            JITDUMP($"Skipping alignment for cold loop L{loop.Index:D2}\n");
            return false;
        }

        var hasCall = loop.VisitLoopBlocks(block => {
            foreach (var tree in block)
            {
                if (tree.Oper is GT_CALL)
                {
                    return BasicBlockVisit.Abort;
                }
            }

            return BasicBlockVisit.Continue;
        }) is BasicBlockVisit.Abort;

        if (hasCall)
        {
            JITDUMP($"Skipping alignment for L{loop.Index:D2}; loop contains call\n");
            return false;
        }

        var previous = top.Prev;
        assert(previous is not null);
        if (previous.Kind is BBJ_CALLFINALLY)
        {
            assert(!previous.isBBCallFinallyPair);
            // Padding after a retless callfinally changes the reported EH region range.
            JITDUMP($"Skipping alignment for L{loop.Index:D2}; its top block follows a CALLFINALLY block\n");
            return false;
        }

        if (previous.isBBCallFinallyPairTail)
        {
            // The paired return block cannot contain instructions, including padding.
            JITDUMP($"Skipping alignment for L{loop.Index:D2}; its top block follows a CALLFINALLY/ALWAYS pair\n");
            return false;
        }

        var topWeight = top.getBBWeight(this);
        var compareWeight = opts.compJitAlignLoopMinBlockWeight * BB_UNITY_WEIGHT;
        if (topWeight < compareWeight)
        {
            JITDUMP($"Skipping alignment for L{loop.Index:D2} that starts at {FMT_BB(top.bbNum)}, " +
                $"weight={FMT_WT(topWeight)} < {FMT_WT(compareWeight)}.\n");
            return false;
        }

        JITDUMP($"Aligning L{loop.Index:D2} that starts at {FMT_BB(top.bbNum)}, " +
            $"weight={FMT_WT(topWeight)} >= {FMT_WT(compareWeight)}.\n");
        return true;
    }

    public PhaseStatus placeLoopAlignInstructions()
    {
        JITDUMP("*************** In placeLoopAlignInstructions()\n");
        assert(codeGen is not null);
        if (!codeGen.ShouldAlignLoops)
        {
            JITDUMP("Not aligning loops; ShouldAlignLoops is false\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (!fgMightHaveNaturalLoops)
        {
#if DEBUG
            var checkLoops = FlowGraphNaturalLoops.Find(fgComputeDfs());
            assert(checkLoops.NumLoops == 0);
#endif
            JITDUMP("Not checking for any loops as fgMightHaveNaturalLoops is false\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var loops = FlowGraphNaturalLoops.Find(fgComputeDfs());
        if (loops.NumLoops == 0)
        {
            JITDUMP("No natural loops found\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var blockToLoop = BlockToNaturalLoopMap.Build(loops);
        var loopTraits = new BitVecTraits(this, loops.NumLoops);
        var seenLoops = BitVecOps.MakeEmpty(loopTraits);
        var alignedLoops = BitVecOps.MakeEmpty(loopTraits);
        var madeChanges = false;
        var minBlockSoFar = BB_MAX_WEIGHT;
        BasicBlock? bbHavingAlign = null;

        foreach (var block in Blocks)
        {
            var loop = blockToLoop.GetLoop(block);
            if ((loop is not null) && BitVecOps.TryAddElemD(loopTraits, seenLoops, loop.Index) &&
                shouldAlignLoop(loop, block))
            {
                block.SetFlags(BBF_LOOP_ALIGN);
                BitVecOps.AddElemD(loopTraits, alignedLoops, loop.Index);
                Metrics.LoopAlignmentCandidates++;

                var previous = block.Prev;
                assert((previous is not null) && !previous.HasFlag(BBF_COLD));
                if (bbHavingAlign is null)
                {
                    bbHavingAlign = previous;
                    JITDUMP($"Marking {FMT_BB(previous.bbNum)} before the loop with BBF_HAS_ALIGN " +
                        $"for loop at {FMT_BB(block.bbNum)}\n");
                }
                else
                {
                    JITDUMP($"Marking {FMT_BB(bbHavingAlign.bbNum)} that ends with unconditional jump " +
                        $"with BBF_HAS_ALIGN for loop at {FMT_BB(block.bbNum)}\n");
                }

                madeChanges = true;
                bbHavingAlign.SetFlags(BBF_HAS_ALIGN);
                minBlockSoFar = BB_MAX_WEIGHT;
                bbHavingAlign = null;
                continue;
            }

            // Prefer padding hidden after a retained jump, choosing the lowest-weight
            // candidate outside already aligned loops, even if the preheader is colder.
            if (opts.compJitHideAlignBehindJmp && (block.Kind is BBJ_ALWAYS) &&
                !block.CanRemoveJumpToNext(this) && (block.bbWeight < minBlockSoFar))
            {
                if ((loop is null) || !BitVecOps.IsMember(loopTraits, alignedLoops, loop.Index))
                {
                    minBlockSoFar = block.bbWeight;
                    bbHavingAlign = block;
                    JITDUMP($"{FMT_BB(block.bbNum)}, bbWeight={FMT_WT(block.bbWeight)} " +
                        "ends with unconditional 'jmp' \n");
                }
            }
        }

        JITDUMP($"Found {Metrics.LoopAlignmentCandidates} candidates for loop alignment\n");
        return madeChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
#endif
}
