// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_WASM
    public PhaseStatus fgWasmControlFlow()
    {
        assert(fgNodeThreading == NodeThreading.LIR);

        var fgWasm = new FgWasm(this);
        var dfsTree = fgWasm.WasmDfs(out var hasBlocksOnlyReachableViaEH);

        if (hasBlocksOnlyReachableViaEH)
        {
            JITDUMP("\nThere are blocks only reachable via EH\n");
            NYI_WASM("Method has blocks only reachable via EH");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        assert(dfsTree.IsForWasm);
        var loops = FlowGraphNaturalLoops.Find(dfsTree);
        assert(loops.ImproperLoopHeaders == 0);

        var tryRegions = FlowGraphTryRegions.Build(this, dfsTree);
#if DEBUG
        if (verbose)
        {
            FlowGraphTryRegions.Dump(tryRegions);
        }
#endif

        if (tryRegions.HasSideEntry())
        {
            NYI_WASM("Try region side entry");
            throw new FatalJitException(CorJitResult.CORJIT_SKIPPED);
        }

        var dfsCount = dfsTree.PostOrderCount;
        JITDUMP($"\nCreating try-aware / loop-aware RPO ({dfsCount} blocks)\n");

        var initialLayout = new BasicBlock[dfsCount + 1];
        var numBlocks = 0;

        void AddToSequence(BasicBlock block)
        {
            JITDUMP($"{numBlocks:D3} {FMT_BB(block.bbNum)}\n");
            block.bbPreorderNum = numBlocks;
            initialLayout[numBlocks++] = block;
        }

        fgVisitBlocksInTryAwareLoopAwareRPO(dfsTree, tryRegions, loops, AddToSequence);
        assert(numBlocks == dfsCount);

        var bb0 = new BasicBlock(firstNode: null, lastNode: null);
#if DEBUG
        bb0.bbNum = 0;
#endif
        bb0.SetFlags(BBF_EMPTY);
        bb0.bbPreorderNum = numBlocks;
        bb0.bbPostorderNum = dfsTree.PostOrderCount;
        initialLayout[numBlocks] = bb0;

        fgWasmIntervals = [];
        var scratch = new WasmInterval?[numBlocks];

        for (var cursor = 0; cursor < numBlocks; cursor++)
        {
            var block = initialLayout[cursor];
            var loop = loops.GetLoopByHeader(block);
            var tryRegion = tryRegions.GetTryRegionByHeader(block);

            if (loop is not null)
            {
                var endCursor = 0;

                if (tryRegions.NumTryCatchRegions() == 0)
                {
                    endCursor = cursor + loop.NumLoopBlocks();

#if DEBUG
                    var endCursorCheck = cursor;
                    while ((endCursorCheck < numBlocks) && loop.ContainsBlock(initialLayout[endCursorCheck]))
                    {
                        endCursorCheck++;
                    }

                    assert(endCursor == endCursorCheck);
#endif
                }
                else
                {
                    var addCodeDscMap = fgHasAddCodeDscMap ? fgGetAddCodeDscMap() : null;
                    _ = loop.VisitLoopBlocksPostOrder(loopBlock => {
                        endCursor = Math.Max(endCursor, loopBlock.bbPreorderNum + 1);

                        var innerTry = tryRegions.GetTryRegionByHeader(loopBlock);
                        if ((addCodeDscMap is not null) && (innerTry is not null) &&
                            innerTry.RequiresRuntimeResumption())
                        {
                            var blockData = bbThrowIndex(loopBlock, out _);

                            foreach (var key in addCodeDscMap.Keys)
                            {
                                if (key.Data != blockData)
                                {
                                    continue;
                                }

                                if (addCodeDscMap.TryGetValue(key, out var add) && add.acdUsed &&
                                    (add.acdDstBlk is not null))
                                {
                                    endCursor = Math.Max(endCursor, add.acdDstBlk.bbPreorderNum + 1);
                                }
                            }
                        }

                        return BasicBlockVisit.Continue;
                    });

                    assert(endCursor >= (cursor + loop.NumLoopBlocks()));

                    if (endCursor > (cursor + loop.NumLoopBlocks()))
                    {
                        JITDUMP(
                            $"Loop L{loop.Index:D2} end extent extended by " +
                            $"{endCursor - (cursor + loop.NumLoopBlocks())} blocks to accommodate partially enclosed trys\n");
                    }
                }

                assert(endCursor > 0);
                assert(endCursor <= numBlocks);
                fgWasmIntervals.Add(WasmInterval.NewLoop(block, initialLayout[endCursor]));
            }

            if ((tryRegion is not null) && tryRegion.RequiresRuntimeResumption())
            {
                var tryEndCursor = checked(cursor + (int)tryRegion.NumBlocks());
                fgWasmIntervals.Add(WasmInterval.NewTry(block, initialLayout[tryEndCursor]));

                assert(block.Kind is BBJ_COND);
                var catchResume = block.TrueTarget;
                assert(catchResume.HasFlag(BBF_CATCH_RESUMPTION));
                var wrapperEndCursor = Math.Max(tryEndCursor, catchResume.bbPreorderNum);
                fgWasmIntervals.Add(WasmInterval.NewExnRefWrapper(block, initialLayout[wrapperEndCursor]));
            }

            var successors = new WasmSuccessorEnumerator(this, block, useProfile: true);
            for (var successor = successors.NextSuccessor(); successor is not null;
                 successor = successors.NextSuccessor())
            {
                var successorNum = successor.bbPreorderNum;

                if (successorNum <= cursor)
                {
                    JITDUMP(
                        $"Backedge {FMT_BB(block.bbNum)}[{cursor}] -> " +
                        $"{FMT_BB(successor.bbNum)}[{successorNum}]\n");
                    assert(loops.GetLoopByHeader(successor) is not null);
                    continue;
                }

                ref var blockTryDsc = ref ehGetBlockTryDsc(block);
                var isCrossingTryCatchExit = !Unsafe.IsNullRef(in blockTryDsc) &&
                    blockTryDsc.HasCatchHandler &&
                    !bbInTryRegions(ehGetIndex(blockTryDsc), successor);

                if ((successorNum == (cursor + 1)) && (block.Kind is not BBJ_SWITCH) &&
                    !successor.HasFlag(BBF_THROW_HELPER) && !isCrossingTryCatchExit)
                {
                    continue;
                }

                if ((successorNum >= numBlocks) && !isCrossingTryCatchExit)
                {
                    continue;
                }

                var existingBlock = scratch[successorNum];
                if (existingBlock is not null)
                {
                    JITDUMP(
                        $"Subsumed {FMT_BB(block.bbNum)}[{cursor}] -> " +
                        $"{FMT_BB(successor.bbNum)}[{successorNum}]\n");
                    assert(existingBlock.Start() <= cursor);
                    continue;
                }

                var blockStart = block;
                if (block.hasTryIndex)
                {
                    for (var tryIndex = block.TryIndex; tryIndex != EHblkDsc.NO_ENCLOSING_INDEX;)
                    {
                        ref var tryDsc = ref ehGetDsc(tryIndex);
                        if (bbInTryRegions(tryIndex, successor))
                        {
                            break;
                        }

                        if (tryDsc.HasCatchHandler)
                        {
                            blockStart = tryDsc.ebdTryBeg;
                        }

                        tryIndex = tryDsc.ebdEnclosingTryIndex;
                    }
                }

                var branch = WasmInterval.NewBlock(blockStart, initialLayout[successorNum]);
                fgWasmIntervals.Add(branch);
                scratch[successorNum] = branch;

                JITDUMP(
                    $"Adding block interval for {FMT_BB(blockStart.bbNum)}[{blockStart.bbPreorderNum}] -> " +
                    $"{FMT_BB(successor.bbNum)}[{successorNum}]\n");
            }
        }

#if DEBUG
        if (verbose)
        {
            JITDUMP("\n-------------- Initial set of wasm intervals\n");
            foreach (var interval in fgWasmIntervals)
            {
                interval.Dump();
            }

            JITDUMP("--------------\n\n");
        }

        foreach (var tryInterval in fgWasmIntervals)
        {
            if (!tryInterval.IsTry())
            {
                continue;
            }

            foreach (var loopInterval in fgWasmIntervals)
            {
                if (!loopInterval.IsLoop())
                {
                    continue;
                }

                var tryStart = tryInterval.Start();
                var tryEnd = tryInterval.End();
                var loopStart = loopInterval.Start();
                var loopEnd = loopInterval.End();
                var disjoint = (tryEnd <= loopStart) || (loopEnd <= tryStart);
                var loopContains = (loopStart <= tryStart) && (tryEnd <= loopEnd);
                var tryContains = (tryStart <= loopStart) && (loopEnd <= tryEnd);

                if (!(disjoint || loopContains || tryContains))
                {
                    JITDUMP("Try interval ");
                    tryInterval.Dump();
                    JITDUMP(" overlaps loop interval ");
                    loopInterval.Dump();
                    assert(false, "Try and loop intervals must perfectly nest");
                }
            }
        }

        // Crossing a try/wrapper end requires an explicit branch, even to the next block.
        // A plain Block interval must provide the label for that branch.
        for (var cursor = 0; cursor < numBlocks; cursor++)
        {
            var block = initialLayout[cursor];
            var next = initialLayout[cursor + 1];
            var fallsToNext = ((block.Kind is BBJ_ALWAYS or BBJ_CALLFINALLYRET) && (block.Target == next)) ||
                ((block.Kind is BBJ_COND) && (block.FalseTarget == next));
            if (!fallsToNext)
            {
                continue;
            }

            var endsTryOrWrapper = false;
            var hasBlockTarget = false;
            foreach (var interval in fgWasmIntervals)
            {
                if (interval.End() != cursor + 1)
                {
                    continue;
                }

                if (interval.IsTry() || interval.IsExnRefWrapper())
                {
                    endsTryOrWrapper = true;
                }
                else if (!interval.IsLoop() && (interval.Start() <= cursor))
                {
                    hasBlockTarget = true;
                }
            }

            if (endsTryOrWrapper && !hasBlockTarget)
            {
                JITDUMP($"{FMT_BB(block.bbNum)}[{cursor}] -> {FMT_BB(next.bbNum)}[{cursor + 1}] " +
                    "crosses a Try/ExnRefWrapper end without a Block target\n");
                assert(false, "Wasm fall-through across a Try/ExnRefWrapper end needs a Block target");
            }
        }
#endif

        fgWasmIntervals.Sort((left, right) => left.Start().CompareTo(right.Start()));

        void ResolveInterval(WasmInterval current)
        {
            foreach (var prior in fgWasmIntervals)
            {
                if (prior == current)
                {
                    break;
                }

                assert(prior.Start() <= current.Start());
                var priorChain = prior.FetchAndUpdateChain();
                assert(priorChain.Start() <= current.Start());

                if ((current.Start() < priorChain.ChainEnd()) && (current.End() > priorChain.ChainEnd()))
                {
                    current.SetChain(priorChain);
                    break;
                }

                if ((current.Start() < prior.End()) && (current.End() > prior.End()))
                {
                    current.SetChain(priorChain);
                    break;
                }
            }
        }

        foreach (var interval in fgWasmIntervals)
        {
            ResolveInterval(interval);
        }

#if DEBUG
        if (verbose)
        {
            JITDUMP("\n-------------- After finding conflicts\n");
            foreach (var interval in fgWasmIntervals)
            {
                interval.Dump();
            }

            JITDUMP("--------------\n\n");
        }
#endif

        bool ComesBefore(WasmInterval left, WasmInterval right)
        {
            var leftChain = left.Chain();
            var rightChain = right.Chain();

            if (leftChain.Start() != rightChain.Start())
            {
                return leftChain.Start() < rightChain.Start();
            }

            if (left.End() != right.End())
            {
                return left.End() > right.End();
            }

            if (left.IsLoop() != right.IsLoop())
            {
                return left.IsLoop();
            }

            if (left.IsLoop() && right.IsLoop())
            {
                return false;
            }

            if (left.IsBlock() != right.IsBlock())
            {
                return left.IsBlock();
            }

            if (left.IsBlock() && (left.IsExnRefWrapper() != right.IsExnRefWrapper()))
            {
                return right.IsExnRefWrapper();
            }

            return false;
        }

        fgWasmIntervals.Sort((left, right) => ComesBefore(left, right) ? -1 : ComesBefore(right, left) ? 1 : 0);

#if DEBUG
        if (verbose)
        {
            JITDUMP("\n-------------- After sorting\n");
            foreach (var interval in fgWasmIntervals)
            {
                interval.Dump();
            }

            JITDUMP("--------------\n\n");
        }
#endif

        JITDUMP("Reordering block list\n");
        BasicBlock? lastBlock = null;

        for (var cursor = 0; cursor < numBlocks; cursor++)
        {
            var block = initialLayout[cursor];

            if (cursor == 0)
            {
                assert(block == fgFirstBB);
                lastBlock = block;
            }
            else
            {
                var previousBlock = lastBlock
                    ?? throw new InvalidOperationException("Wasm block reordering requires a preceding block.");
                fgUnlinkBlock(block);
                fgInsertBBafter(previousBlock, block);

                ref var previousHandler = ref ehIsBlockHndLast(previousBlock);
                if (!Unsafe.IsNullRef(in previousHandler) && block.hasHndIndex &&
                    BasicBlock.sameHndRegion(previousBlock, block))
                {
                    ref var handler = ref ehGetBlockHndDsc(block);
                    fgSetHndEnd(ref handler, block);
                }

                lastBlock = block;
            }

            if (block.Kind is BBJ_COND)
            {
                var trueNum = block.TrueTarget.bbPreorderNum;
                var falseNum = block.FalseTarget.bbPreorderNum;

                if (trueNum == falseNum)
                {
                    fgRemoveConditionalJump(block);
                }
                else if (trueNum == (cursor + 1))
                {
                    JITDUMP(
                        $"Reversing condition in {FMT_BB(block.bbNum)} to allow fall through to " +
                        $"{FMT_BB(block.TrueTarget.bbNum)}\n");

                    var test = block.LastLIRNode
                        ?? throw new InvalidOperationException("A conditional Wasm block requires a final LIR node.");
                    assert(test.Oper is GT_JTRUE);
                    var conditional = test.AsUnOp();
                    var condition = gtReverseCond(conditional.Op1);
                    assert(condition == conditional.Op1);
                    conditional.Op1 = condition;
                    (block.TrueEdgeRef, block.FalseEdgeRef) = (block.FalseEdge, block.TrueEdge);
                }
                else
                {
                    JITDUMP($"NOT Reversing condition in {FMT_BB(block.bbNum)}\n");
                }
            }
        }

        fgIndexToBlockMap = initialLayout;

#if DEBUG
        {
            var order = 0;
            foreach (var block in Blocks)
            {
                if (block.bbPreorderNum != order)
                {
                    JITDUMP(
                        $"Blocks out of order: {FMT_BB(block.bbNum)} has order {order}, " +
                        $"but bbPreorderNum={block.bbPreorderNum}\n");
                }

                assert(block.bbPreorderNum == order, "block order disagrees with preorder num");
                order++;
            }
        }

        {
            var regionClosed = new bool[compFuncInfoCount];
            var previousRegion = uint.MaxValue;
            foreach (var block in Blocks)
            {
                var region = bbFuncletRegionOf(block);
                assert(region < compFuncInfoCount);

                if (region != previousRegion)
                {
                    if (previousRegion != uint.MaxValue)
                    {
                        regionClosed[previousRegion] = true;
                    }

                    if (regionClosed[region])
                    {
                        JITDUMP(
                            $"Wasm function region {region} is not contiguous: " +
                            $"{FMT_BB(block.bbNum)} re-enters it\n");
                    }

                    assert(!regionClosed[region], "wasm function region (main method / funclet) blocks are not contiguous");
                    previousRegion = region;
                }
            }
        }
#endif

#if DEBUG
        fgDumpWasmControlFlow();
        fgDumpWasmControlFlowDot();
        assert(!fgTrysContiguous());
#endif

        return PhaseStatus.MODIFIED_EVERYTHING;
    }
#endif
}
