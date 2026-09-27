// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, flowgraph.cpp and fgopt.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public bool fgFuncletsAreCold()
    {
        for (var block = fgFirstFuncletBB; block is not null; block = block.Next)
        {
            if (!block.isRunRarely)
            {
                return false;
            }
        }

        return true;
    }

    public PhaseStatus fgDetermineFirstColdBlock()
    {
        assert(fgFirstColdBlock is null);
        if (!opts.compProcedureSplitting)
        {
            JITDUMP("No procedure splitting will be done for this method\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        if ((compHndBBtabCount > 0) && !opts.compProcedureSplittingEH)
        {
            JITDUMP("No procedure splitting will be done for this method with EH (by request)\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }
#endif

        BasicBlock? firstColdBlock = null;
        BasicBlock? prevToFirstColdBlock = null;
        var forceSplit = false;
#if DEBUG
        forceSplit = JitConfig.JitStressProcedureSplitting != 0;
#endif
        if (forceSplit)
        {
            assert(fgFirstBB is not null);
            firstColdBlock = fgFirstBB.Next;
            prevToFirstColdBlock = fgFirstBB;
            JITDUMP("JitStressProcedureSplitting is enabled: Splitting after the first basic block\n");
        }
        else
        {
            var inFuncletSection = false;
            BasicBlock? lastBlock = null;
            for (var block = fgFirstBB; block is not null; lastBlock = block, block = block.Next)
            {
                if (block == fgFirstFuncletBB)
                {
                    inFuncletSection = true;
                }

                if (firstColdBlock is not null)
                {
                    if (!block.isRunRarely)
                    {
                        firstColdBlock = null;
                        prevToFirstColdBlock = null;
                        if (inFuncletSection)
                        {
                            if (fgFuncletsAreCold())
                            {
                                firstColdBlock = fgFirstFuncletBB;
                                assert(firstColdBlock is not null);
                                prevToFirstColdBlock = firstColdBlock.Prev;
                            }

                            break;
                        }
                    }
                }
                else
                {
                    // Do not split between funclets: they share unwind constraints.
                    if (inFuncletSection)
                    {
                        if (fgFuncletsAreCold())
                        {
                            firstColdBlock = block;
                            prevToFirstColdBlock = lastBlock;
                        }

                        break;
                    }

                    if (block.isRunRarely &&
                        ((lastBlock is null) || (lastBlock.Kind is not BBJ_COND) || (fgGetCodeEstimate(block) >= 8)))
                    {
                        firstColdBlock = block;
                        prevToFirstColdBlock = lastBlock;
                    }
                }
            }
        }

        if (firstColdBlock == fgFirstBB)
        {
            firstColdBlock = null;
        }

        if (firstColdBlock is not null)
        {
            noway_assert(prevToFirstColdBlock is not null);
            // A jump to the cold section costs five bytes; native keeps a lone block smaller than eight hot.
            if (!forceSplit && firstColdBlock.IsLast && (fgGetCodeEstimate(firstColdBlock) < 8))
            {
                firstColdBlock = null;
            }
            else if (prevToFirstColdBlock.isBBCallFinallyPair)
            {
                firstColdBlock = firstColdBlock.Next;
            }
        }

        for (var block = firstColdBlock; block is not null; block = block.Next)
        {
            block.SetFlags(BBF_COLD);
        }

#if DEBUG
        if (verbose)
        {
            if (firstColdBlock is not null)
            {
                jitprintf($"fgFirstColdBlock is {FMT_BB(firstColdBlock.bbNum)}.\n");
            }
            else
            {
                jitprintf("fgFirstColdBlock is NULL.\n");
            }
        }
#endif
        fgFirstColdBlock = firstColdBlock;
        return PhaseStatus.MODIFIED_EVERYTHING;
    }

    public uint fgGetCodeEstimate(BasicBlock block)
    {
        uint costSz = block.Kind switch {
            BBJ_ALWAYS or BBJ_EHCATCHRET or BBJ_LEAVE or BBJ_COND => 2,
            BBJ_CALLFINALLY => 5,
            BBJ_CALLFINALLYRET => 0,
            BBJ_SWITCH => 10,
            BBJ_THROW or BBJ_EHFINALLYRET or BBJ_EHFAULTRET or BBJ_EHFILTERRET => 1,
            BBJ_RETURN => 3,
            _ => throw new FatalJitException("Unexpected block kind for code-size estimation."),
        };

        for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
        {
            costSz = unchecked(costSz + stmt.CostSz);
        }

        return costSz;
    }
}
