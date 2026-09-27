// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
//
// Based on the RyuJIT compiler from dotnet/runtime, fgopt.cpp.

using System;
using System.Collections.Generic;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed class ThreeOptLayout
    {
        private const int MaxSwaps = 1000;

        private readonly Compiler _compiler;
        private readonly int _numCandidateBlocks;
        private readonly bool _hasEH;
        // The native priority queue is a max-heap with source/target bbID tie breaks.
        private readonly List<FlowEdge> _cutPoints = [];
        private BasicBlock[] _blockOrder;
        private BasicBlock[] _tempOrder;

        internal ThreeOptLayout(Compiler compiler, BasicBlock[] initialLayout, int numHotBlocks, bool hasEH)
        {
            _compiler = compiler;
            _blockOrder = initialLayout;
            // The phase invalidates DFS after layout, so its postorder array can hold swap scratch.
            _tempOrder = (compiler._dfsTree ??
                throw new InvalidOperationException("Three-opt layout requires the DFS tree.")).GetPostOrder();
            assert((_tempOrder.Length >= numHotBlocks) && !ReferenceEquals(initialLayout, _tempOrder));
            _numCandidateBlocks = numHotBlocks;
            _hasEH = hasEH;
        }

        internal bool Run()
        {
            assert(_numCandidateBlocks > 0);
            RunThreeOpt();
            return ReorderBlockList();
        }

        private static bool EdgeCmp(FlowEdge left, FlowEdge right)
        {
            assert(left != right);
            var leftWeight = left.LikelyWeight;
            var rightWeight = right.LikelyWeight;

            if (leftWeight == rightWeight)
            {
                var leftSource = left.SourceBlock;
                var rightSource = right.SourceBlock;
                return leftSource == rightSource
                    ? left.DestinationBlock.bbID < right.DestinationBlock.bbID
                    : leftSource.bbID < rightSource.bbID;
            }

            return leftWeight < rightWeight;
        }

        private void PushCutPoint(FlowEdge edge)
        {
            var index = _cutPoints.Count;
            _cutPoints.Add(edge);

            for (var parent = (index - 1) / 2; (index != 0) && EdgeCmp(_cutPoints[parent], edge);
                index = parent, parent = (index - 1) / 2)
            {
                _cutPoints[index] = _cutPoints[parent];
            }

            _cutPoints[index] = edge;
        }

        private FlowEdge PopCutPoint()
        {
            var root = _cutPoints[0];
            var last = _cutPoints[^1];
            var size = _cutPoints.Count - 1;
            var index = 0;

            for (var child = (2 * index) + 1; child < size; index = child, child = (2 * index) + 1)
            {
                var right = child + 1;
                if ((right < size) && EdgeCmp(_cutPoints[child], _cutPoints[right]))
                {
                    child = right;
                }

                if (EdgeCmp(last, _cutPoints[child]))
                {
                    _cutPoints[index] = _cutPoints[child];
                }
                else
                {
                    break;
                }
            }

            if (size != 0)
            {
                _cutPoints[index] = last;
            }
            _cutPoints.RemoveAt(size);

            return root;
        }

        private bool IsCandidateBlock(BasicBlock block)
        {
            var position = block.bbPreorderNum;
            return (position >= 0) && (position < _numCandidateBlocks) && (_blockOrder[position] == block);
        }

        private weight_t GetLayoutCost(int startPos, int endPos)
        {
            assert(startPos <= endPos);
            assert(endPos < _numCandidateBlocks);
            var layoutCost = BB_ZERO_WEIGHT;

            for (var position = startPos; position < endPos; position++)
            {
                layoutCost += GetCost(_blockOrder[position], _blockOrder[position + 1]);
            }

            layoutCost += _blockOrder[endPos].bbWeight;
            return layoutCost;
        }

        private weight_t GetCost(BasicBlock block, BasicBlock next)
        {
            var maxCost = block.bbWeight;
            var fallthroughEdge = _compiler.fgGetPredForBlock(next, block);

            if (fallthroughEdge is not null)
            {
                var cost = maxCost - fallthroughEdge.LikelyWeight;
                return cost > 0.0 ? cost : 0.0;
            }

            return maxCost;
        }

        private weight_t GetPartitionCostDelta(int s2Start, int s3Start, int s3End, int s4End)
        {
            var s2Block = _blockOrder[s2Start];
            var s2BlockPrev = _blockOrder[s2Start - 1];
            var s3Block = _blockOrder[s3Start];
            var s3BlockPrev = _blockOrder[s3Start - 1];
            var lastBlock = _blockOrder[s3End];

            var currCost = GetCost(s2BlockPrev, s2Block) + GetCost(s3BlockPrev, s3Block);
            var newCost = GetCost(s2BlockPrev, s3Block) + GetCost(lastBlock, s2Block);

            if (s3End < s4End)
            {
                var s4StartBlock = _blockOrder[s3End + 1];
                currCost += GetCost(lastBlock, s4StartBlock);
                newCost += GetCost(s3BlockPrev, s4StartBlock);
            }
            else
            {
                assert(s3End == s4End);
                currCost += lastBlock.bbWeight;
                newCost += s3BlockPrev.bbWeight;
            }

            return newCost - currCost;
        }

        private void SwapPartitions(int s1Start, int s2Start, int s3Start, int s3End, int s4End)
        {
#if DEBUG
            var currLayoutCost = GetLayoutCost(s1Start, s4End);
#endif
            var s1Size = s2Start - s1Start;
            var s2Size = s3Start - s2Start;
            var s3Size = (s3End + 1) - s3Start;
            // Swap S2 and S3 while preserving each partition's internal order and the S4 suffix.
            Array.Copy(_blockOrder, s1Start, _tempOrder, s1Start, s1Size);
            Array.Copy(_blockOrder, s3Start, _tempOrder, s1Start + s1Size, s3Size);
            Array.Copy(_blockOrder, s2Start, _tempOrder, s1Start + s1Size + s3Size, s2Size);

            var numBlocks = (s4End - s1Start) + 1;
            var swappedSize = s1Size + s2Size + s3Size;
            assert(numBlocks >= swappedSize);
            Array.Copy(_blockOrder, s1Start + swappedSize, _tempOrder, s1Start + swappedSize, numBlocks - swappedSize);
            (_blockOrder, _tempOrder) = (_tempOrder, _blockOrder);

            for (var i = s2Start; i <= s4End; i++)
            {
                _blockOrder[i].bbPreorderNum = i;
            }

#if DEBUG
            if (currLayoutCost < uint.MaxValue)
            {
                var newLayoutCost = GetLayoutCost(s1Start, s4End);
                assert((newLayoutCost < currLayoutCost) ||
                    fgProfileWeightsEqual(newLayoutCost, currLayoutCost, 0.001));
            }
#endif
        }

        private bool ConsiderEdge(FlowEdge edge, bool addToQueue = true)
        {
            if (addToQueue && edge.Visited)
            {
                return false;
            }

            var source = edge.SourceBlock;
            var destination = edge.DestinationBlock;
            if (!IsCandidateBlock(source) || !IsCandidateBlock(destination) || (source == destination) ||
                destination.IsFirst ||
                (_hasEH && (!BasicBlock.sameTryRegion(source, destination) || _compiler.bbIsTryBeg(destination))))
            {
                return false;
            }

            if (addToQueue)
            {
                edge.Visited = true;
                PushCutPoint(edge);
            }

            return true;
        }

        private void AddNonFallthroughSuccs(int position)
        {
            assert(position < _numCandidateBlocks);
            var block = _blockOrder[position];
            var next = ((position + 1) >= _numCandidateBlocks) ? null : _blockOrder[position + 1];

            foreach (var edge in block.Succs.Edges)
            {
                if (edge.DestinationBlock != next)
                {
                    _ = ConsiderEdge(edge);
                }
            }
        }

        private void AddNonFallthroughPreds(int position)
        {
            assert(position < _numCandidateBlocks);
            var block = _blockOrder[position];
            var prev = position == 0 ? null : _blockOrder[position - 1];

            foreach (var edge in block.PredEdges)
            {
                if (edge.SourceBlock != prev)
                {
                    _ = ConsiderEdge(edge);
                }
            }
        }

        private bool RunGreedyThreeOptPass(int startPos, int endPos)
        {
            assert(_cutPoints.Count == 0);
            assert(startPos < endPos);
            var modified = false;

            JITDUMP("Running greedy 3-opt pass.\n");
            for (var position = startPos; position <= endPos; position++)
            {
                AddNonFallthroughSuccs(position);
            }

            var numSwaps = 0;
            while ((_cutPoints.Count != 0) && (numSwaps < MaxSwaps))
            {
                var candidateEdge = PopCutPoint();
                candidateEdge.Visited = false;

                var source = candidateEdge.SourceBlock;
                var destination = candidateEdge.DestinationBlock;
                var srcPos = source.bbPreorderNum;
                var dstPos = destination.bbPreorderNum;
                assert((srcPos >= startPos) && (srcPos <= endPos));
                assert((dstPos >= startPos) && (dstPos <= endPos));
                assert(dstPos != startPos);
                assert(srcPos != dstPos);

                if ((srcPos + 1) == dstPos)
                {
                    assert(modified);
                    continue;
                }

                assert(_blockOrder[srcPos] == source);
                assert(_blockOrder[dstPos] == destination);

                int s2Start;
                int s3Start;
                int s3End;
                weight_t costChange;

                if (srcPos < dstPos)
                {
                    s2Start = srcPos + 1;
                    s3Start = dstPos;
                    s3End = endPos;
                    costChange = GetPartitionCostDelta(s2Start, s3Start, s3End, endPos);
                }
                else
                {
                    // For a backward edge, fix the cuts around S2 and S3's end, then
                    // search for the best start of S3 without splitting a call-finally pair.
                    s2Start = dstPos;
                    s3Start = srcPos;
                    s3End = srcPos;
                    costChange = BB_ZERO_WEIGHT;

                    var s2Block = _blockOrder[s2Start];
                    var s2BlockPrev = _blockOrder[s2Start - 1];
                    var lastBlock = _blockOrder[s3End];
                    var currCostBase = GetCost(s2BlockPrev, s2Block) +
                        ((s3End < endPos) ? GetCost(lastBlock, _blockOrder[s3End + 1]) : lastBlock.bbWeight);
                    var newCostBase = GetCost(lastBlock, s2Block);

                    for (var position = s2Start + 1; position <= s3End; position++)
                    {
                        var s3Block = _blockOrder[position];
                        var s3BlockPrev = _blockOrder[position - 1];
                        if (_hasEH && (s3Block.Kind is BBJ_CALLFINALLYRET))
                        {
                            continue;
                        }

                        var currCost = currCostBase + GetCost(s3BlockPrev, s3Block);
                        var newCost = newCostBase + GetCost(s2BlockPrev, s3Block) +
                            ((s3End < endPos) ? GetCost(s3BlockPrev, _blockOrder[s3End + 1]) : s3BlockPrev.bbWeight);
                        var delta = newCost - currCost;

                        if (delta < costChange)
                        {
                            costChange = delta;
                            s3Start = position;
                        }
                    }
                }

                if ((costChange >= BB_ZERO_WEIGHT) || fgProfileWeightsEqual(costChange, BB_ZERO_WEIGHT, 0.001))
                {
                    continue;
                }

                JITDUMP($"Swapping partitions [{FMT_BB(_blockOrder[s2Start].bbNum)}, {FMT_BB(_blockOrder[s3Start - 1].bbNum)}] and [{FMT_BB(_blockOrder[s3Start].bbNum)}, {FMT_BB(_blockOrder[s3End].bbNum)}] (cost change = {costChange:f6})\n");
                SwapPartitions(startPos, s2Start, s3Start, s3End, endPos);
                assert((source.bbPreorderNum + 1) == destination.bbPreorderNum);

                AddNonFallthroughSuccs(s2Start - 1);
                AddNonFallthroughPreds(s2Start);
                AddNonFallthroughSuccs(s3Start - 1);
                AddNonFallthroughPreds(s3Start);
                AddNonFallthroughSuccs(s3End);
                if (s3End < endPos)
                {
                    AddNonFallthroughPreds(s3End + 1);
                }

                modified = true;
                numSwaps++;
            }

            _cutPoints.Clear();
            return modified;
        }

        private void RunThreeOpt()
        {
            if (_numCandidateBlocks < 3)
            {
                JITDUMP("Not enough blocks to partition anything. Skipping reordering.\n");
                return;
            }

            CompactHotJumps();

            var endPos = _numCandidateBlocks - 1;
            JITDUMP($"Initial layout cost: {GetLayoutCost(0, endPos):f6}\n");
            var modified = RunGreedyThreeOptPass(0, endPos);

            if (modified)
            {
                JITDUMP($"Final layout cost: {GetLayoutCost(0, endPos):f6}\n");
            }
            else
            {
                JITDUMP("No changes made.\n");
            }
        }

        private bool ReorderBlockList()
        {
            BasicBlock[]? lastHotBlocks = null;
            if (_hasEH)
            {
                lastHotBlocks = new BasicBlock[_compiler.compHndBBtabCount + 1];
                lastHotBlocks[0] = _compiler.fgFirstBB ??
                    throw new InvalidOperationException("The layout requires an entry block.");
                foreach (var clause in new EHClauses(_compiler))
                {
                    lastHotBlocks[clause.ebdTryBeg.bbTryIndex] = clause.ebdTryBeg;
                }
            }

            JITDUMP("Reordering block list\n");
            var modified = false;
            for (var i = 1; i < _numCandidateBlocks; i++)
            {
                var block = _blockOrder[i - 1];
                var blockToMove = _blockOrder[i];
                if (!_hasEH)
                {
                    if (block.Next != blockToMove)
                    {
                        _compiler.fgUnlinkBlock(blockToMove);
                        _compiler.fgInsertBBafter(block, blockToMove);
                        modified = true;
                    }

                    continue;
                }

                assert(lastHotBlocks is not null);
                lastHotBlocks[block.bbTryIndex] = block;
                if (blockToMove.isBBCallFinallyPairTail || _compiler.bbIsTryBeg(blockToMove))
                {
                    continue;
                }

                var insertionPoint = BasicBlock.sameTryRegion(block, blockToMove)
                    ? block : lastHotBlocks[blockToMove.bbTryIndex];
                if (insertionPoint.isBBCallFinallyPair)
                {
                    insertionPoint = insertionPoint.Next ??
                        throw new InvalidOperationException("A call-finally head must have a paired tail.");
                    assert(blockToMove != insertionPoint);
                }

                if (insertionPoint.Next == blockToMove)
                {
                    continue;
                }

                if (blockToMove.isBBCallFinallyPair)
                {
                    var callFinallyRet = blockToMove.Next ??
                        throw new InvalidOperationException("A call-finally head must have a paired tail.");
                    if (callFinallyRet != insertionPoint)
                    {
                        _compiler.fgUnlinkRange(blockToMove, callFinallyRet);
                        _compiler.fgMoveBlocksAfter(blockToMove, callFinallyRet, insertionPoint);
                        modified = true;
                    }
                }
                else
                {
                    _compiler.fgUnlinkBlock(blockToMove);
                    _compiler.fgInsertBBafter(insertionPoint, blockToMove);
                    modified = true;
                }
            }

            if (!_hasEH)
            {
                return modified;
            }

            if (modified)
            {
                _compiler.fgFindTryRegionEnds();
            }

            JITDUMP("Moving try regions\n");
            foreach (var clause in new EHClauses(_compiler))
            {
                var tryBeg = clause.ebdTryBeg;
                if (!IsCandidateBlock(tryBeg) || tryBeg.IsFirst)
                {
                    continue;
                }

                var insertionPoint = _blockOrder[tryBeg.bbPreorderNum - 1];
                var parentIndex = insertionPoint.hasTryIndex
                    ? insertionPoint.TryIndex : EHblkDsc.NO_ENCLOSING_INDEX;
                if (parentIndex != clause.ebdEnclosingTryIndex)
                {
                    continue;
                }

                if (insertionPoint.isBBCallFinallyPair)
                {
                    insertionPoint = insertionPoint.Next ??
                        throw new InvalidOperationException("A call-finally head must have a paired tail.");
                }
                if (insertionPoint.Next == tryBeg)
                {
                    continue;
                }

                var tryLast = clause.ebdTryLast;
                _compiler.fgUnlinkRange(tryBeg, tryLast);
                _compiler.fgMoveBlocksAfter(tryBeg, tryLast, insertionPoint);
                modified = true;
                if (parentIndex != EHblkDsc.NO_ENCLOSING_INDEX)
                {
                    _compiler.fgFindTryRegionEnds();
                }
            }

            return modified;
        }

        private void CompactHotJumps()
        {
            JITDUMP("Compacting hot jumps\n");

            for (var i = 0; i < _numCandidateBlocks; i++)
            {
                var block = _blockOrder[i];
                FlowEdge edge;
                FlowEdge? unlikelyEdge;

                if (block.Kind is BBJ_ALWAYS)
                {
                    edge = block.TargetEdge;
                    unlikelyEdge = null;
                }
                else if (block.Kind is BBJ_COND)
                {
                    if (block.TrueEdge.Likelihood > 0.5)
                    {
                        edge = block.TrueEdge;
                        unlikelyEdge = block.FalseEdge;
                    }
                    else
                    {
                        edge = block.FalseEdge;
                        unlikelyEdge = block.TrueEdge;
                    }

                    var unlikelyTarget = unlikelyEdge.DestinationBlock;
                    if ((unlikelyEdge.Likelihood == 0.5) && IsCandidateBlock(unlikelyTarget) &&
                        (unlikelyTarget.bbPreorderNum == (i + 1)))
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }

                if (!ConsiderEdge(edge, addToQueue: false))
                {
                    continue;
                }

                var target = edge.DestinationBlock;
                if ((block.Kind is BBJ_COND) && (i >= target.bbPreorderNum))
                {
                    assert(unlikelyEdge is not null);
                    var unlikelyTarget = unlikelyEdge.DestinationBlock;
                    if (!ConsiderEdge(unlikelyEdge, addToQueue: false) ||
                        (i >= unlikelyTarget.bbPreorderNum))
                    {
                        continue;
                    }

                    edge = unlikelyEdge;
                    target = unlikelyTarget;
                }

                var srcPos = i;
                var dstPos = target.bbPreorderNum;
                if ((srcPos + 1) == dstPos)
                {
                    continue;
                }

                assert(dstPos != 0);
                var fallthroughEdge = _compiler.fgGetPredForBlock(target, _blockOrder[dstPos - 1]);
                if ((fallthroughEdge is not null) && (fallthroughEdge.LikelyWeight >= edge.LikelyWeight))
                {
                    continue;
                }

                JITDUMP($"Creating fallthrough along {FMT_BB(block.bbNum)} -> {FMT_BB(target.bbNum)}\n");
                if (srcPos < dstPos)
                {
                    var offset = target.isBBCallFinallyPair ? 2 : 1;
                    for (var pos = dstPos - 1; pos != srcPos; pos--)
                    {
                        var blockToMove = _blockOrder[pos];
                        _blockOrder[pos + offset] = blockToMove;
                        blockToMove.bbPreorderNum += offset;
                    }

                    _blockOrder[srcPos + 1] = target;
                    target.bbPreorderNum = srcPos + 1;
                    if (target.isBBCallFinallyPair)
                    {
                        var tail = target.Next ??
                            throw new InvalidOperationException("A call-finally head must have a paired tail.");
                        _blockOrder[srcPos + 2] = tail;
                        tail.bbPreorderNum = srcPos + 2;
                    }
                }
                else
                {
                    for (var pos = srcPos - 1; pos >= dstPos; pos--)
                    {
                        var blockToMove = _blockOrder[pos];
                        _blockOrder[pos + 1] = blockToMove;
                        blockToMove.bbPreorderNum++;
                    }

                    _blockOrder[dstPos] = block;
                    block.bbPreorderNum = dstPos;
                }

                assert((block.bbPreorderNum + 1) == target.bbPreorderNum);
            }
        }
    }
}
