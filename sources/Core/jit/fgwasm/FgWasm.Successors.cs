// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.BasicBlockFlags;

namespace RyuJitSharp;

#if TARGET_WASM
internal sealed class WasmSuccessorEnumerator
{
    private readonly BasicBlock[] _successors;
    private int _index;

    internal WasmSuccessorEnumerator(Compiler compiler, BasicBlock block, bool useProfile = false)
    {
        Block = block;
        var successors = new List<BasicBlock>();
        _ = FgWasm.VisitWasmSuccs(compiler, block, successor => {
            successors.Add(successor);
            return BasicBlockVisit.Continue;
        }, useProfile);
        _successors = [.. successors];
    }

    internal BasicBlock Block { get; }

    internal BasicBlock? NextSuccessor()
    {
        if (_index >= _successors.Length)
        {
            return null;
        }

        return _successors[_index++];
    }
}

internal sealed partial class FgWasm
{
    internal static BasicBlockVisit VisitWasmSuccs(
        Compiler compiler, BasicBlock block, Func<BasicBlock, BasicBlockVisit> visit, bool useProfile = false)
    {
        bool Visit(BasicBlock successor)
        {
            return visit(successor) is BasicBlockVisit.Continue;
        }

        bool IsGeneralizedTryEntry(BasicBlock candidate)
        {
            if (!candidate.hasTryIndex)
            {
                return false;
            }

            if (compiler.bbIsTryBeg(candidate))
            {
                return true;
            }

            foreach (var predecessor in candidate.PredBlocks)
            {
                if (predecessor.HasFlag(BBF_ASYNC_RESUMPTION) || predecessor.HasFlag(BBF_CATCH_RESUMPTION))
                {
                    return true;
                }
            }

            return false;
        }

        if (compiler.fgHasAddCodeDscMap)
        {
            var addCodeDscMap = compiler.fgGetAddCodeDscMap();
            var isTrySideEntry = IsGeneralizedTryEntry(block);

            if ((block == compiler.fgFirstBB) || compiler.bbIsFuncletBeg(block) || isTrySideEntry)
            {
                var blockData = compiler.bbThrowIndex(block, out _);

                foreach (var key in addCodeDscMap.Keys)
                {
                    var matches = key.Data == blockData;

                    if (!matches && isTrySideEntry && (key.Designator is Compiler.AcdKeyDesignator.KD_TRY))
                    {
                        matches = compiler.bbInTryRegions(checked((ushort)key.RegionIndex), block);
                    }

                    if (matches && addCodeDscMap.TryGetValue(key, out var add) && add.acdUsed)
                    {
                        var destination = add.acdDstBlk
                            ?? throw new InvalidOperationException("A used add-code descriptor has no destination block.");

                        if (compiler.bbIsInSameFunclet(block, destination) && !Visit(destination))
                        {
                            return BasicBlockVisit.Abort;
                        }
                    }
                }
            }
        }

        if (compiler.bbIsTryBeg(block) && (compiler.fgTryRegions is not null))
        {
            var region = compiler.fgTryRegions.GetTryRegionByHeader(block);
            if (region is not null)
            {
                foreach (var unreachableBlock in region.UnreachableBlocks())
                {
                    if (!Visit(unreachableBlock))
                    {
                        return BasicBlockVisit.Abort;
                    }
                }
            }
        }

        switch (block.Kind)
        {
            case BBJ_EHFINALLYRET:
            case BBJ_EHCATCHRET:
            case BBJ_EHFILTERRET:
            case BBJ_LEAVE:
            case BBJ_THROW:
            case BBJ_RETURN:
            case BBJ_EHFAULTRET:
            {
                break;
            }

            case BBJ_CALLFINALLY:
            {
                if (block.isBBCallFinallyPair)
                {
                    var successor = block.Next
                        ?? throw new InvalidOperationException("A call-finally pair requires a following block.");
                    if (!Visit(successor))
                    {
                        return BasicBlockVisit.Abort;
                    }
                }

                break;
            }

            case BBJ_CALLFINALLYRET:
            case BBJ_ALWAYS:
            {
                var successor = block.Target
                    ?? throw new InvalidOperationException("An unconditional Wasm branch requires a target.");
                if (!Visit(successor))
                {
                    return BasicBlockVisit.Abort;
                }

                break;
            }

            case BBJ_COND:
            {
                var trueTarget = block.TrueTarget
                    ?? throw new InvalidOperationException("A conditional Wasm branch requires a true target.");
                var falseTarget = block.FalseTarget
                    ?? throw new InvalidOperationException("A conditional Wasm branch requires a false target.");

                if (block.TrueEdge == block.FalseEdge)
                {
                    if (!Visit(falseTarget))
                    {
                        return BasicBlockVisit.Abort;
                    }
                }
                else if (useProfile && (block.TrueEdge.Likelihood < block.FalseEdge.Likelihood))
                {
                    if (!Visit(trueTarget) || !Visit(falseTarget))
                    {
                        return BasicBlockVisit.Abort;
                    }
                }
                else if (!Visit(falseTarget) || !Visit(trueTarget))
                {
                    return BasicBlockVisit.Abort;
                }

                break;
            }

            case BBJ_SWITCH:
            {
                foreach (var successor in block.SwitchTargets.Succs)
                {
                    if (!Visit(successor.DestinationBlock))
                    {
                        return BasicBlockVisit.Abort;
                    }
                }

                break;
            }

            default:
            {
                NO_WAY($"Unexpected Wasm successor block kind {block.Kind}.");
                break;
            }
        }

        return BasicBlockVisit.Continue;
    }

    internal FlowGraphDfsTree WasmDfs(out bool hasBlocksOnlyReachableViaEH)
    {
        var compiler = Comp();
        compiler.fgInvalidateDfsTree();

        if (compiler.compHndBBtabCount > 0)
        {
            compiler.fgTryRegions = FlowGraphTryRegions.Build(compiler, null);
        }

        var postOrder = new BasicBlock[compiler.fgBBcount];
        var hasCycle = false;

        void VisitPreorder(BasicBlock block, int preorderNum)
        {
            block.bbPreorderNum = preorderNum;
            block.bbPostorderNum = -1;
        }

        void VisitPostorder(BasicBlock block, int postorderNum)
        {
            block.bbPostorderNum = postorderNum;
            assert(postorderNum < compiler.fgBBcount);
            postOrder[postorderNum] = block;
        }

        void VisitEdge(BasicBlock block, BasicBlock successor)
        {
            if ((successor.bbPreorderNum <= block.bbPreorderNum) && (successor.bbPostorderNum == -1))
            {
                hasCycle = true;
            }
        }

        var entryBlocks = new List<BasicBlock>();
        assert(compiler.fgEntryBB is null);
        assert(compiler.fgGlobalMorphDone);

        JITDUMP("Determining Wasm DFS entry points\n");

        for (var xtNum = compiler.compHndBBtabCount - 1; xtNum >= 0; xtNum--)
        {
            ref var ehDsc = ref compiler.compHndBBtab[xtNum];
            JITDUMP($"{FMT_BB(ehDsc.ebdHndBeg.bbNum)} is handler entry\n");
            entryBlocks.Add(ehDsc.ebdHndBeg);
            if (ehDsc.HasFilter)
            {
                var filter = ehDsc.ebdFilter
                    ?? throw new InvalidOperationException("An EH filter entry requires a filter block.");
                JITDUMP($"{FMT_BB(filter.bbNum)} is filter entry\n");
                entryBlocks.Add(filter);
            }
        }

        entryBlocks.Sort((left, right) =>
            compiler.funGetFuncIdx(right).CompareTo(compiler.funGetFuncIdx(left)));

        hasBlocksOnlyReachableViaEH = false;

        foreach (var block in compiler.Blocks)
        {
            if (compiler.bbIsFuncletBeg(block))
            {
                continue;
            }

            var onlyHasEHPreds = true;
            var hasPreds = false;
            foreach (var predecessor in block.PredBlocks)
            {
                hasPreds = true;

                if (predecessor.Kind is BBJ_EHCATCHRET or BBJ_EHFILTERRET or BBJ_EHFAULTRET)
                {
                    continue;
                }

                onlyHasEHPreds = false;
                break;
            }

            if (hasPreds && onlyHasEHPreds)
            {
                JITDUMP($"{FMT_BB(block.bbNum)} is only reachable via EH\n");
                entryBlocks.Add(block);
                hasBlocksOnlyReachableViaEH = true;
            }
        }

        var firstBlock = compiler.fgFirstBB
            ?? throw new InvalidOperationException("Wasm DFS requires a method entry block.");
        JITDUMP($"{FMT_BB(firstBlock.bbNum)} is method entry\n");
        entryBlocks.Add(firstBlock);

        JITDUMP("Running Wasm DFS\n");
        JITDUMP("Entry blocks: ");
        foreach (var entry in entryBlocks)
        {
            JITDUMP($" {FMT_BB(entry.bbNum)}");
        }

        JITDUMP("\n");

        var traits = new BitVecTraits(compiler, compiler.fgBBNumMax + 1);
        var visited = BitVecOps.MakeEmpty(traits);
        var preorderIndex = 0;
        var postorderIndex = 0;
        var pending = new Stack<(BasicBlock Block, WasmSuccessorEnumerator Successors)>();

        void VisitEntry(BasicBlock entry)
        {
            BitVecOps.AddElemD(traits, visited, entry.bbNum);
            pending.Push((entry, new WasmSuccessorEnumerator(compiler, entry, useProfile: true)));
            VisitPreorder(entry, preorderIndex++);

            while (pending.Count > 0)
            {
                var (block, successors) = pending.Peek();
                var successor = successors.NextSuccessor();

                if (successor is null)
                {
                    _ = pending.Pop();
                    VisitPostorder(block, postorderIndex++);
                    continue;
                }

                _ = pending.Pop();
                pending.Push((block, successors));
                if (BitVecOps.TryAddElemD(traits, visited, successor.bbNum))
                {
                    pending.Push((successor, new WasmSuccessorEnumerator(compiler, successor, useProfile: true)));
                    VisitPreorder(successor, preorderIndex++);
                }

                VisitEdge(block, successor);
            }
        }

        assert(entryBlocks.Count > 0);
        foreach (var entry in entryBlocks)
        {
            if (!BitVecOps.IsMember(traits, visited, entry.bbNum))
            {
                VisitEntry(entry);
            }
        }

        assert(preorderIndex == postorderIndex);

        return new FlowGraphDfsTree(compiler, postOrder, postorderIndex, hasCycle, profileAware: true, forWasm: true);
    }
}
#endif
