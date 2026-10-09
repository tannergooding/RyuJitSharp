// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.bbCatchType;

namespace RyuJitSharp;

public partial class Compiler
{
    public void fgVerifyHandlerTab()
    {
        if (compHndBBtabCount == 0)
        {
            return;
        }

        // Normalization separates handler/try starts and distinct try starts.
        // Multiple EH regions may share their last block.
        var handlerBegIsTryBegNormalizationDone = fgNormalizeEHDone;
        var multipleBegBlockNormalizationDone = fgNormalizeEHDone;
        var traits = new BitVecTraits(this, impInlineRoot.compEHID);
        var ids = BitVecOps.MakeEmpty(traits);

        assert(compHndBBtabCount <= compHndBBtab.Length);

        for (ushort XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            ref var HBtab = ref compHndBBtab[XTnum];
            assert(HBtab.ebdID < impInlineRoot.compEHID);
            assert(BitVecOps.TryAddElemD(traits, ids, HBtab.ebdID));
            assert(HBtab.ebdTryBeg is not null);
            assert(HBtab.ebdTryLast is not null);
            assert(HBtab.ebdHndBeg is not null);
            assert(HBtab.ebdHndLast is not null);
            assert(HBtab.ebdTryBeg.HasFlag(BBF_DONT_REMOVE));
            assert(HBtab.ebdHndBeg.HasFlag(BBF_DONT_REMOVE));
            assert(!HBtab.ebdTryBeg.HasFlag(BBF_REMOVED));
            assert(!HBtab.ebdTryLast.HasFlag(BBF_REMOVED));
            assert(!HBtab.ebdHndBeg.HasFlag(BBF_REMOVED));
            assert(!HBtab.ebdHndLast.HasFlag(BBF_REMOVED));

            if (HBtab.HasFilter)
            {
                assert(HBtab.ebdFilter is not null);
                assert(HBtab.ebdFilter.HasFlag(BBF_DONT_REMOVE));
                assert(!HBtab.ebdFilter.HasFlag(BBF_REMOVED));
            }

            if (fgFuncletsCreated)
            {
                assert(bbIsFuncletBeg(HBtab.ebdHndBeg));

                if (HBtab.HasFilter)
                {
                    assert(bbIsFuncletBeg(HBtab.ebdFilter));
                }
            }
        }

        // Compare lexical positions without renumbering blocks: diagnostics must
        // not change the numbering seen by later phases or non-Debug execution.
        var bbNumMax = fgBBNumMax;
        var blockNumMap = new int[bbNumMax + 1];
        var newBBnum = 1;

        foreach (var block in Blocks)
        {
            assert(!block.HasFlag(BBF_REMOVED));
            assert((1 <= block.bbNum) && (block.bbNum <= bbNumMax));
            assert(blockNumMap[block.bbNum] == 0);
            blockNumMap[block.bbNum] = newBBnum++;
        }

        // Before normalization, a handler start can also be a try start.
        // Record handler/filter starts separately for the final block checks.
        var blockHndBegSet = new bool[bbNumMax + 1];
        var isLegalFirstFunclet = false;
        var bbNumFirstFunclet = 0;

        if (fgFuncletsCreated)
        {
            assert(fgFirstFuncletBB is not null);
            assert(!fgFirstFuncletBB.HasFlag(BBF_REMOVED));
            bbNumFirstFunclet = blockNumMap[fgFirstFuncletBB.bbNum];
            assert(bbNumFirstFunclet != 0);
        }
        else
        {
            assert(fgFirstFuncletBB is null);
        }

        for (ushort XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            ref var HBtab = ref compHndBBtab[XTnum];
            var bbNumTryBeg = blockNumMap[HBtab.ebdTryBeg.bbNum];
            var bbNumTryLast = blockNumMap[HBtab.ebdTryLast.bbNum];
            var bbNumHndBeg = blockNumMap[HBtab.ebdHndBeg.bbNum];
            var bbNumHndLast = blockNumMap[HBtab.ebdHndLast.bbNum];
            var bbNumFilter = 0;

            if (HBtab.HasFilter)
            {
                bbNumFilter = blockNumMap[HBtab.ebdFilter.bbNum];
            }

            assert(bbNumTryBeg != 0);
            assert(bbNumTryLast != 0);
            assert(bbNumHndBeg != 0);
            assert(bbNumHndLast != 0);

            if (HBtab.HasFilter)
            {
                assert(bbNumFilter != 0);
            }

            // IL permits either ordering and nonadjacent try/handler regions.
            // A filter must precede its handler, and the whole handler is
            // disjoint from its own try.
            assert(bbNumTryBeg <= bbNumTryLast);
            assert(bbNumHndBeg <= bbNumHndLast);

            if (HBtab.HasFilter)
            {
                assert(bbNumFilter < bbNumHndBeg);
                assert((bbNumHndLast < bbNumTryBeg) || (bbNumTryLast < bbNumFilter));
            }
            else
            {
                assert((bbNumHndLast < bbNumTryBeg) || (bbNumTryLast < bbNumHndBeg));
            }

            if (fgFuncletsCreated)
            {
                if (bbNumTryLast < bbNumFirstFunclet)
                {
                    assert(HBtab.ebdEnclosingHndIndex == EHblkDsc.NO_ENCLOSING_INDEX);
                }
                else
                {
                    // A try in the funclet region must be wholly nested in a
                    // handler; normalization separates their first blocks.
                    if (multipleBegBlockNormalizationDone)
                    {
                        assert(bbNumTryBeg > bbNumFirstFunclet);
                    }
                    else
                    {
                        assert(bbNumTryBeg >= bbNumFirstFunclet);
                    }

                    assert(HBtab.ebdEnclosingHndIndex != EHblkDsc.NO_ENCLOSING_INDEX);
                }

                if (HBtab.HasFilter)
                {
                    assert(bbNumFirstFunclet <= bbNumFilter);

                    if (fgFirstFuncletBB == HBtab.ebdFilter)
                    {
                        assert(!isLegalFirstFunclet);
                        isLegalFirstFunclet = true;
                    }
                }
                else
                {
                    assert(bbNumFirstFunclet <= bbNumHndBeg);

                    if (fgFirstFuncletBB == HBtab.ebdHndBeg)
                    {
                        assert(!isLegalFirstFunclet);
                        isLegalFirstFunclet = true;
                    }
                }
            }

            // Clauses are inner-to-outer, so checking each immediate enclosing
            // region also verifies the entire nesting chain.
            if (HBtab.ebdEnclosingTryIndex != EHblkDsc.NO_ENCLOSING_INDEX)
            {
                assert(HBtab.ebdEnclosingTryIndex > XTnum);
                ref var HBtabOuter = ref ehGetDsc(HBtab.ebdEnclosingTryIndex);
                var bbNumOuterTryBeg = blockNumMap[HBtabOuter.ebdTryBeg.bbNum];
                var bbNumOuterTryLast = blockNumMap[HBtabOuter.ebdTryLast.bbNum];
                assert(bbNumOuterTryBeg != 0);
                assert(bbNumOuterTryLast != 0);
                assert(bbNumOuterTryBeg <= bbNumOuterTryLast);

                if (!EHblkDsc.ebdIsSameTry(HBtab, HBtabOuter))
                {
                    if (fgFuncletsCreated)
                    {
                        // Pulled-out handlers no longer lie in the enclosing
                        // try's lexical range. Main-function tries still do.
                        if (fgTrysContiguous() && (bbNumTryLast < bbNumFirstFunclet) &&
                            (bbNumOuterTryLast < bbNumFirstFunclet))
                        {
                            if (multipleBegBlockNormalizationDone)
                            {
                                assert(bbNumOuterTryBeg < bbNumTryBeg);
                            }
                            else
                            {
                                assert(bbNumOuterTryBeg <= bbNumTryBeg);
                            }

                            assert(bbNumTryLast <= bbNumOuterTryLast);
                        }

                        assert((bbNumHndLast < bbNumOuterTryBeg) || (bbNumOuterTryLast < bbNumHndBeg));
                    }
                    else
                    {
                        assert(fgTrysContiguous());

                        if (multipleBegBlockNormalizationDone)
                        {
                            assert(bbNumOuterTryBeg < bbNumTryBeg);
                        }
                        else
                        {
                            assert(bbNumOuterTryBeg <= bbNumTryBeg);
                        }

                        assert(bbNumOuterTryBeg < bbNumHndBeg);

                        assert(bbNumTryLast <= bbNumOuterTryLast);
                        assert(bbNumHndLast <= bbNumOuterTryLast);
                    }
                }
            }

            if (HBtab.ebdEnclosingHndIndex != EHblkDsc.NO_ENCLOSING_INDEX)
            {
                assert(HBtab.ebdEnclosingHndIndex > XTnum);
                ref var HBtabOuter = ref ehGetDsc(HBtab.ebdEnclosingHndIndex);
                var bbNumOuterHndBeg = blockNumMap[HBtabOuter.ebdHndBeg.bbNum];
                var bbNumOuterHndLast = blockNumMap[HBtabOuter.ebdHndLast.bbNum];
                assert(bbNumOuterHndBeg != 0);
                assert(bbNumOuterHndLast != 0);
                assert(bbNumOuterHndBeg <= bbNumOuterHndLast);

                if (fgFuncletsCreated)
                {
                    if (handlerBegIsTryBegNormalizationDone)
                    {
                        assert(bbNumOuterHndBeg < bbNumTryBeg);
                    }
                    else
                    {
                        assert(bbNumOuterHndBeg <= bbNumTryBeg);
                    }

                    assert(bbNumTryLast <= bbNumOuterHndLast);

                    assert((bbNumHndLast < bbNumOuterHndBeg) || (bbNumOuterHndLast < bbNumHndBeg));
                }
                else
                {
                    if (handlerBegIsTryBegNormalizationDone)
                    {
                        assert(bbNumOuterHndBeg < bbNumTryBeg);
                    }
                    else
                    {
                        assert(bbNumOuterHndBeg <= bbNumTryBeg);
                    }

                    assert(bbNumOuterHndBeg < bbNumHndBeg);

                    assert(bbNumTryLast <= bbNumOuterHndLast);
                    assert(bbNumHndLast <= bbNumOuterHndLast);
                }
            }

            assert(!blockHndBegSet[HBtab.ebdHndBeg.bbNum]);
            blockHndBegSet[HBtab.ebdHndBeg.bbNum] = true;

            if (HBtab.HasFilter)
            {
                assert(HBtab.ebdFilter.CatchType == BBCT_FILTER);
                assert(!blockHndBegSet[HBtab.ebdFilter.bbNum]);
                blockHndBegSet[HBtab.ebdFilter.bbNum] = true;
            }

            if (HBtab.HasFilter)
            {
                assert(HBtab.ebdHndBeg.CatchType == BBCT_FILTER_HANDLER);
            }
            else if (HBtab.HasCatchHandler)
            {
                assert(HBtab.ebdHndBeg.CatchType is not
                    (BBCT_NONE or BBCT_FAULT or BBCT_FINALLY or BBCT_FILTER or BBCT_FILTER_HANDLER));
            }
            else if (HBtab.HasFaultHandler)
            {
                assert(HBtab.ebdHndBeg.CatchType == BBCT_FAULT);
            }
            else if (HBtab.HasFinallyHandler)
            {
                assert(HBtab.ebdHndBeg.CatchType == BBCT_FINALLY);
            }
        }

        assert(!fgFuncletsCreated || isLegalFirstFunclet);

        // First assignments win because clauses are ordered innermost first.
        // Raw block indices encode the table index plus one; zero means no EH.
        var blockTryIndex = new ushort[bbNumMax + 1];
        var blockHndIndex = new ushort[bbNumMax + 1];

        for (ushort XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            ref var HBtab = ref compHndBBtab[XTnum];
            var blockEnd = HBtab.ebdTryLast.Next;

            for (var block = HBtab.ebdTryBeg; block != blockEnd; block = block.Next)
            {
                assert(block is not null);

                if (blockTryIndex[block.bbNum] == 0)
                {
                    blockTryIndex[block.bbNum] = (ushort)(XTnum + 1);
                }
            }

            blockEnd = HBtab.ebdHndLast.Next;

            for (var block = HBtab.HasFilter ? HBtab.ebdFilter : HBtab.ebdHndBeg;
                block != blockEnd; block = block.Next)
            {
                assert(block is not null);

                if (blockHndIndex[block.bbNum] == 0)
                {
                    blockHndIndex[block.bbNum] = (ushort)(XTnum + 1);
                }
            }
        }

        if (fgFuncletsCreated)
        {
            // Funclets retain their true enclosing try even after relocation
            // removes them from its lexical range. Ignore mutual-protect tries.
            for (ushort XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
            {
                ref var HBtab = ref compHndBBtab[XTnum];
                var enclosingTryIndex = ehTrueEnclosingTryIndex(XTnum);

                if (enclosingTryIndex != EHblkDsc.NO_ENCLOSING_INDEX)
                {
                    var blockEnd = HBtab.ebdHndLast.Next;

                    for (var block = HBtab.HasFilter ? HBtab.ebdFilter : HBtab.ebdHndBeg;
                        block != blockEnd; block = block.Next)
                    {
                        assert(block is not null);

                        if (blockTryIndex[block.bbNum] == 0)
                        {
                            blockTryIndex[block.bbNum] = (ushort)(enclosingTryIndex + 1);
                        }
                    }
                }
            }
        }

        foreach (var block in Blocks)
        {
            assert(!fgTrysContiguous() || (block.bbTryIndex == blockTryIndex[block.bbNum]));
            assert(block.bbHndIndex == blockHndIndex[block.bbNum]);

            if (!blockHndBegSet[block.bbNum])
            {
                assert(block.CatchType == BBCT_NONE);
                assert(!fgFuncletsCreated || !bbIsFuncletBeg(block));
            }

            switch (block.Kind)
            {
                case BBJ_EHFINALLYRET:
                {
                    ref var ehDsc = ref ehGetDsc(block.HndIndex);
                    assert(ehDsc.HasFinallyHandler);
                    break;
                }

                case BBJ_EHFAULTRET:
                {
                    ref var ehDsc = ref ehGetDsc(block.HndIndex);
                    assert(ehDsc.HasFaultHandler);
                    break;
                }

                case BBJ_EHFILTERRET:
                {
                    ref var ehDsc = ref ehGetDsc(block.HndIndex);
                    assert(ehDsc.HasFilter);
                    assert((blockNumMap[ehDsc.ebdFilter.bbNum] <= blockNumMap[block.bbNum]) &&
                        (blockNumMap[block.bbNum] < blockNumMap[ehDsc.ebdHndBeg.bbNum]));
                    break;
                }

                case BBJ_EHCATCHRET:
                {
                    ref var ehDsc = ref ehGetDsc(block.HndIndex);
                    assert(ehDsc.HasCatchHandler);
                    break;
                }
            }
        }
    }
}
#endif
