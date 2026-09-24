// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private BitVecTraits? _blockSequenceVisitedTraits;
    private BitVec _blockSequenceVisitedSet = [];
    private bool _hasCriticalEdges;

    private void setBlockSequence()
    {
        assert(!_blockSequencingDone);
        if (_blockSequencingDone)
        {
            throw new FatalJitException("LSRA block sequencing can only be initialized once.");
        }

        assert(_blockSequence is null && _blockSequenceCount == 0);
        if (_blockSequence is not null || _blockSequenceCount != 0)
        {
            throw new FatalJitException("LSRA block sequence was already initialized.");
        }

        var blockCount = _compiler.fgBBcount;
        _blockSequenceVisitedTraits = new BitVecTraits(_compiler, blockCount);
        _blockSequenceVisitedSet = BitVecOps.MakeEmpty(_blockSequenceVisitedTraits);
        _blockSequence = new BasicBlock[blockCount];

        if (_compiler.opts.OptimizationEnabled)
        {
            var dfsTree = _compiler.fgComputeDfs(useProfile: true);
            _compiler._dfsTree = dfsTree;
            var loops = FlowGraphNaturalLoops.Find(dfsTree);
            _compiler._loops = loops;

            void AddBlock(BasicBlock block)
            {
                _blockSequence![_blockSequenceCount++] = block;
            }

            if (loops.NumLoops == 0)
            {
                for (var index = dfsTree.PostOrderCount; index != 0; index--)
                {
                    AddBlock(dfsTree.GetPostOrder(index - 1));
                }
            }
            else
            {
                var loopVisitorTraits = dfsTree.PostOrderTraits();
                var loopVisitorBlocks = BitVecOps.MakeEmpty(loopVisitorTraits);

                void VisitLoopAware(BasicBlock block)
                {
                    if (!BitVecOps.TryAddElemD(loopVisitorTraits, loopVisitorBlocks, block.bbPostorderNum))
                    {
                        return;
                    }

                    AddBlock(block);
                    var loop = loops.GetLoopByHeader(block);
                    if (loop is not null)
                    {
                        _ = loop.VisitLoopBlocksReversePostOrder(loopBlock => {
                            VisitLoopAware(loopBlock);
                            return BasicBlockVisit.Continue;
                        });
                    }
                }

                for (var index = dfsTree.PostOrderCount; index != 0; index--)
                {
                    VisitLoopAware(dfsTree.GetPostOrder(index - 1));
                }
            }

            var block = _compiler.fgLastBB
                ?? throw new FatalJitException("LSRA requires a last block when completing its block sequence.");
            while (_blockSequenceCount < blockCount)
            {
                if (!dfsTree.Contains(block))
                {
                    block.bbPostorderNum = _blockSequenceCount;
                    _blockSequence![_blockSequenceCount++] = block;
                }

                if (_blockSequenceCount < blockCount)
                {
                    block = block.Prev
                        ?? throw new FatalJitException("The LSRA block sequence does not cover the compiler flow graph.");
                }
            }
        }
        else
        {
            foreach (var block in _compiler.Blocks)
            {
                block.bbPostorderNum = _blockSequenceCount;
                _blockSequence[_blockSequenceCount++] = block;
            }
        }

        assert(_blockSequenceCount == blockCount);
        if (_blockSequenceCount != blockCount)
        {
            throw new FatalJitException("The LSRA block sequence does not cover every compiler block.");
        }

        _bbNumMaxBeforeResolution = (uint)_compiler.fgBBNumMax;
        var blockInfo = new LsraBlockInfo[_compiler.fgBBNumMax + 1];
        _blockInfo = blockInfo;
        _hasCriticalEdges = false;
        blockInfo[0].weight = BB_UNITY_WEIGHT;
#if TRACK_LSRA_STATS
        blockInfo[0].stats = new uint[(int)LsraStat.COUNT];
#endif

        assert(_compiler.fgPredsComputed);
        if (!_compiler.fgPredsComputed)
        {
            throw new FatalJitException("LSRA block metadata requires computed predecessor lists.");
        }

        JITDUMP("Start LSRA Block Sequence: \n");
        for (var index = 0; index < _blockSequenceCount; index++)
        {
            visitBlock(_blockSequence[index]);
        }

        _blockSequencingDone = true;

#if DEBUG
        foreach (var block in _compiler.Blocks)
        {
            assert(isBlockVisited(block));
        }

        JITDUMP("Final LSRA Block Sequence:\n");
        for (var block = startBlockSequence(); block is not null; block = moveToNextBlock())
        {
            var info = blockInfo[block.bbNum];
            JITDUMP($"{FMT_BB(block.bbNum)} ({refCntWtd2str(info.weight, padForDecimalPlaces: true),6})");
            if (info.hasCriticalInEdge)
            {
                JITDUMP(" critical-in");
            }
            if (info.hasCriticalOutEdge)
            {
                JITDUMP(" critical-out");
            }
            if (info.hasEHBoundaryIn)
            {
                JITDUMP(" EH-in");
            }
            if (info.hasEHBoundaryOut)
            {
                JITDUMP(" EH-out");
            }
            if (info.hasEHPred)
            {
                JITDUMP(" has EH pred");
            }
            JITDUMP("\n");
        }

        JITDUMP("\n");
#endif

        void visitBlock(BasicBlock block)
        {
            JITDUMP($"Current block: {FMT_BB(block.bbNum)}\n");
            markBlockVisited(block);

            ref var info = ref blockInfo[block.bbNum];
            info.predBBNum = 0;
            info.hasCriticalInEdge = false;
            info.hasCriticalOutEdge = false;
            info.weight = block.getBBWeight(_compiler);
            info.hasEHBoundaryIn = block.hasEHBoundaryIn;
            info.hasEHBoundaryOut = block.hasEHBoundaryOut;
            info.hasEHPred = false;
#if TRACK_LSRA_STATS
            info.stats = new uint[(int)LsraStat.COUNT];
#endif

            var isCallFinallyPairTail = block.isBBCallFinallyPairTail;
            if (isCallFinallyPairTail)
            {
                info.hasEHBoundaryIn = true;
                info.hasEHBoundaryOut = true;
            }

            var uniquePred = getUniquePred(block) is not null;
            foreach (var predEdge in block.PredEdges)
            {
                var predBlock = predEdge.SourceBlock;
                if (!uniquePred)
                {
                    if (predBlock.NumSucc > 1)
                    {
                        info.hasCriticalInEdge = true;
                        _hasCriticalEdges = true;
                    }
                    else if (predBlock.Kind == BBJ_SWITCH)
                    {
                        assert(false, "Switch with single successor.");
                    }
                }

                if (!isCallFinallyPairTail && (predBlock.hasEHBoundaryOut || predBlock.isBBCallFinallyPairTail))
                {
                    if (uniquePred)
                    {
                        info.hasEHBoundaryIn = true;
                    }
                    else
                    {
                        info.hasEHPred = true;
                    }
                }
            }

            var numSuccessors = block.NumSucc;
            var checkCriticalOutEdge = numSuccessors > 1;
            if (!checkCriticalOutEdge && block.Kind == BBJ_SWITCH)
            {
                assert(false, "Switch with single successor.");
            }

            if (checkCriticalOutEdge)
            {
                foreach (var successor in block.Succs)
                {
                    if (getUniquePred(successor) is null)
                    {
                        info.hasCriticalOutEdge = true;
                        _hasCriticalEdges = true;
                        break;
                    }
                }
            }
        }
    }

    private BasicBlock? getUniquePred(BasicBlock block)
    {
        if (block == _compiler.fgFirstBB)
        {
            return null;
        }

        var predEdges = block.PredEdges.GetEnumerator();
        if (!predEdges.MoveNext())
        {
            return null;
        }

        var predBlock = predEdges.Current.SourceBlock;
        return predEdges.MoveNext() ? null : predBlock;
    }

    private BasicBlock startBlockSequence()
    {
        if (!_blockSequencingDone)
        {
            setBlockSequence();
        }
        else
        {
            clearVisitedBlocks();
        }

        var currentBlock = _compiler.fgFirstBB;
        assert(currentBlock is not null);
        if (currentBlock is null)
        {
            throw new FatalJitException("LSRA requires a non-empty compiler flow graph.");
        }

        _currentBlockSequenceNumber = 0;
        _currentBlockNumber = (uint)currentBlock.bbNum;
        assert(_blockSequence![0] == currentBlock);
        markBlockVisited(currentBlock);
        return currentBlock;
    }

    private BasicBlock? moveToNextBlock()
    {
        var nextBlock = getNextBlock();
        _currentBlockSequenceNumber++;
        if (nextBlock is not null)
        {
            _currentBlockNumber = (uint)nextBlock.bbNum;
        }

        return nextBlock;
    }

    private BasicBlock? getNextBlock()
    {
        assert(_blockSequencingDone);
        if (!_blockSequencingDone)
        {
            throw new FatalJitException("LSRA block traversal requires an initialized block sequence.");
        }

        var nextBlockSequenceNumber = _currentBlockSequenceNumber + 1;
        return nextBlockSequenceNumber < _blockSequenceCount ? _blockSequence![nextBlockSequenceNumber] : null;
    }

    private void markBlockVisited(BasicBlock block)
    {
        var traits = _blockSequenceVisitedTraits;
        assert(traits is not null);
        if (traits is null)
        {
            throw new FatalJitException("LSRA block visitation requires initialized block traits.");
        }

        BitVecOps.AddElemD(traits, _blockSequenceVisitedSet, block.bbPostorderNum);
    }

    private void clearVisitedBlocks()
    {
        var traits = _blockSequenceVisitedTraits;
        assert(traits is not null);
        if (traits is null)
        {
            throw new FatalJitException("LSRA block visitation requires initialized block traits.");
        }

        BitVecOps.ClearD(traits, _blockSequenceVisitedSet);
    }

    private bool isBlockVisited(BasicBlock block)
    {
        var traits = _blockSequenceVisitedTraits;
        assert(traits is not null);
        if (traits is null)
        {
            throw new FatalJitException("LSRA block visitation requires initialized block traits.");
        }

        return BitVecOps.IsMember(traits, _blockSequenceVisitedSet, block.bbPostorderNum);
    }
}
