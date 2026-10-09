// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotion.cpp and promotiondecomposition.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed unsafe partial class PhysicalPromotionReplaceVisitor(
        Compiler compiler, PhysicalPromotionAggregateInfoMap aggregates, PhysicalPromotionLiveness liveness,
        FlowGraphDfsTree dfsTree)
    {
        private readonly Compiler _compiler = compiler;
        private readonly PhysicalPromotionAggregateInfoMap _aggregates = aggregates;
        private readonly PhysicalPromotionLiveness _liveness = liveness;
        private readonly FlowGraphDfsTree _dfsTree = dfsTree;
        private readonly BitVecTraits _postOrderTraits = dfsTree.PostOrderTraits();
        private BitVecTraits? _readBackTraits;
        private BitVec[] _pendingReadBacksByBlock = [];
        private BitVec[] _currentStructFields = [];
        private BitVec _processedBlocks = [];
        private BitVec _requiresAlreadyReadBackOnEntry = [];
        private BitVec _requiresReadBackOnExit = [];
        private int _pendingReadBacks;
        private BasicBlock? _currentBlock;
        private Statement? _currentStatement;
        private bool _madeChanges;
        private bool _mayHaveForwardSub;
        private BasicBlock CurrentBlock => _currentBlock ??
            throw new InvalidOperationException("Physical promotion has not started a block.");
        private Statement CurrentStatement => _currentStatement ??
            throw new InvalidOperationException("Physical promotion has not started a statement.");
        private BitVecTraits ReadBackTraits => _readBackTraits ??
            throw new InvalidOperationException("Physical promotion has not prepared readbacks.");

        public Statement? StartBlock(BasicBlock block)
        {
            _currentBlock = block;
            assert(_pendingReadBacks == 0);
            foreach (var aggregate in _aggregates.Aggregates)
            {
                ref readonly var descriptor = ref _compiler.lvaGetDesc(aggregate.LclNum);
                for (var index = 0; index < aggregate.Replacements.Count; index++)
                {
                    var replacement = aggregate.Replacements[index];
                    assert(!replacement.NeedsReadBack);
                    assert(replacement.NeedsWriteBack);
                    if (!_liveness.IsReplacementLiveIn(block, aggregate.LclNum, index))
                    {
                        continue;
                    }

                    var pending = false;
                    var structCurrent = false;
                    if (block == _compiler.fgFirstBB)
                    {
                        pending = descriptor.lvIsParam || descriptor.lvIsOSRLocal;
                        structCurrent = pending;
                    }
                    else if (!BitVecOps.IsMember(_postOrderTraits, _requiresAlreadyReadBackOnEntry,
                                 block.bbPostorderNum))
                    {
                        var hasPred = false;
                        pending = true;
                        structCurrent = true;
                        foreach (var edge in block.PredEdges)
                        {
                            var predecessor = edge.SourceBlock;
                            if (!_dfsTree.Contains(predecessor))
                            {
                                continue;
                            }

                            assert(BitVecOps.IsMember(_postOrderTraits, _processedBlocks,
                                predecessor.bbPostorderNum));
                            hasPred = true;
                            pending &= BitVecOps.IsMember(ReadBackTraits,
                                _pendingReadBacksByBlock[predecessor.bbPostorderNum], replacement.ReadBackIndex);
                            structCurrent &= BitVecOps.IsMember(ReadBackTraits,
                                _currentStructFields[predecessor.bbPostorderNum], replacement.ReadBackIndex);
                        }

                        pending &= hasPred;
                        structCurrent &= hasPred;
                    }

                    if (structCurrent)
                    {
                        ClearNeedsWriteBack(replacement);
                    }

                    if (pending)
                    {
                        assert(structCurrent);
                        SetNeedsReadBack(replacement);
                    }
                    else
                    {
                        // Only pending predecessors have current struct fields. A join load could
                        // overwrite a replacement updated on another path with a stale field.
                        foreach (var edge in block.PredEdges)
                        {
                            var predecessor = edge.SourceBlock;
                            if (!_dfsTree.Contains(predecessor))
                            {
                                continue;
                            }

                            if (BitVecOps.IsMember(_postOrderTraits, _processedBlocks, predecessor.bbPostorderNum) &&
                                BitVecOps.IsMember(ReadBackTraits,
                                    _pendingReadBacksByBlock[predecessor.bbPostorderNum], replacement.ReadBackIndex))
                            {
                                InsertReadBackAtEnd(predecessor, aggregate.LclNum, replacement);
                                BitVecOps.RemoveElemD(ReadBackTraits,
                                    _pendingReadBacksByBlock[predecessor.bbPostorderNum], replacement.ReadBackIndex);
                            }
                        }
                    }
                }
            }

            return block.FirstStmt;
        }

        private void InsertReadBackAtEnd(BasicBlock block, int structLclNum, PhysicalPromotionReplacement replacement)
        {
            JITDUMP($"Reading back V{structLclNum:D2}.[{replacement.Offset:D3}.." +
                $"{replacement.Offset + replacement.AccessType.Size:D3}) -> V{replacement.LclNum:D2} " +
                $"near the end of {FMT_BB(block.bbNum)}\n");
            var tree = _compiler.PhysicalPromotionCreateReadBack(structLclNum, replacement);
            var statement = _compiler.fgNewStmtFromTree(tree);
            _compiler.fgInsertStmtNearEnd(block, statement);
        }

        private bool MustMaterializeReadBacks(BasicBlock block)
        {
            if (block.HasPotentialEHSuccs(_compiler) ||
                (block.Kind is BBJ_CALLFINALLY or BBJ_EHFINALLYRET or BBJ_EHFILTERRET or BBJ_EHCATCHRET))
            {
                return true;
            }

            return block.VisitRegularSuccs(_compiler, successor =>
                BitVecOps.IsMember(_postOrderTraits, _requiresAlreadyReadBackOnEntry, successor.bbPostorderNum)
                    ? BasicBlockVisit.Abort : BasicBlockVisit.Continue) == BasicBlockVisit.Abort;
        }

        public void EndBlock()
        {
            var materialize = BitVecOps.IsMember(_postOrderTraits, _requiresReadBackOnExit,
                CurrentBlock.bbPostorderNum);
            var pendingReadBacks = _pendingReadBacksByBlock[CurrentBlock.bbPostorderNum] =
                BitVecOps.MakeEmpty(ReadBackTraits);
            var currentStructFields = _currentStructFields[CurrentBlock.bbPostorderNum] =
                BitVecOps.MakeEmpty(ReadBackTraits);
            BitVecOps.AddElemD(_postOrderTraits, _processedBlocks, CurrentBlock.bbPostorderNum);

            foreach (var aggregate in _aggregates.Aggregates)
            {
                for (var index = 0; index < aggregate.Replacements.Count; index++)
                {
                    var replacement = aggregate.Replacements[index];
                    assert(!replacement.NeedsReadBack || !replacement.NeedsWriteBack);
                    if (replacement.NeedsReadBack)
                    {
                        if (_liveness.IsReplacementLiveOut(CurrentBlock, aggregate.LclNum, index))
                        {
                            if (materialize || (replacement.ReadBackPlacement == CurrentBlock))
                            {
                                InsertReadBackAtEnd(CurrentBlock, aggregate.LclNum, replacement);
                            }
                            else
                            {
                                BitVecOps.AddElemD(ReadBackTraits, pendingReadBacks, replacement.ReadBackIndex);
                            }
                        }
                        else
                        {
                            JITDUMP($"Skipping reading back dead replacement V{aggregate.LclNum:D2}." +
                                $"[{replacement.Offset:D3}..{replacement.Offset + replacement.AccessType.Size:D3}) " +
                                $"-> V{replacement.LclNum:D2} near the end of {FMT_BB(CurrentBlock.bbNum)}\n");
                        }

                        ClearNeedsReadBack(replacement);
                    }

                    if (!replacement.NeedsWriteBack)
                    {
                        // Reading back leaves the original current until a replacement store invalidates it.
                        BitVecOps.AddElemD(ReadBackTraits, currentStructFields, replacement.ReadBackIndex);
                    }

                    SetNeedsWriteBack(replacement);
                }
            }

            assert(_pendingReadBacks == 0);
        }

        public void StartStatement(Statement statement)
        {
            _currentStatement = statement;
            _madeChanges = false;
            _mayHaveForwardSub = false;
            InsertPreStatementWriteBacks();
            InsertPreStatementReadBacks();
        }

        public bool MadeChanges => _madeChanges;
        public bool MayHaveForwardSubOpportunity => _mayHaveForwardSub;

        private void InsertPreStatementWriteBacks()
        {
            if ((CurrentStatement.RootNode.Flags & GTF_CALL) == 0)
            {
                return;
            }

            var walker = new PhysicalPromotionTreeWalker(this, prepass: true);
            _ = walker.WalkTree(ref CurrentStatement.RootNodeRef, null);
        }

        private void InsertPreStatementReadBacks()
        {
            if (_pendingReadBacks == 0)
            {
                return;
            }

            if (((CurrentStatement.RootNode.Flags & (GTF_EXCEPT | GTF_CALL)) != 0) &&
                _compiler.ehBlockHasExnFlowDsc(CurrentBlock))
            {
                JITDUMP("Reading back pending replacements before statement with possible exception " +
                    "side effect inside block in try region\n");
                foreach (var aggregate in _aggregates.Aggregates)
                {
                    foreach (var replacement in aggregate.Replacements)
                    {
                        InsertPreStatementReadBackIfNecessary(aggregate.LclNum, replacement);
                    }
                }

                return;
            }

            _ = CurrentStatement.VisitLogicalLocalOccurrencesViaLocalsTreeList(occurrence => {
                if ((occurrence.Node.Oper is not GT_LCL_ADDR) && (occurrence.GetAccessType(_compiler) is TYP_STRUCT))
                {
                    return GenTree.VisitResult.Continue;
                }

                var aggregate = _aggregates.Lookup(occurrence.LclNum);
                if (aggregate is null)
                {
                    return GenTree.VisitResult.Continue;
                }

                var index = LowerBound(aggregate.Replacements, occurrence.LclOffs,
                    static replacement => replacement.Offset);
                if ((index < aggregate.Replacements.Count) &&
                    (aggregate.Replacements[index].Offset == occurrence.LclOffs))
                {
                    InsertPreStatementReadBackIfNecessary(aggregate.LclNum, aggregate.Replacements[index]);
                }

                return GenTree.VisitResult.Continue;
            });
        }

        private void InsertPreStatementReadBackIfNecessary(int local, PhysicalPromotionReplacement replacement)
        {
            if (replacement.NeedsReadBack)
            {
#if DEBUG
                JITDUMP($"Reading back replacement V{local:D2}.[{replacement.Offset:D3}.." +
                    $"{replacement.Offset + replacement.AccessType.Size:D3}) -> V{replacement.LclNum:D2} " +
                    $"before [{CurrentStatement.RootNode.TreeId:D6}]:\n");
#endif
                var tree = _compiler.PhysicalPromotionCreateReadBack(local, replacement);
                var statement = _compiler.fgNewStmtFromTree(tree);
                DISPSTMT(statement);
                _compiler.fgInsertStmtBefore(CurrentBlock, CurrentStatement, statement);
                ClearNeedsReadBack(replacement);
            }
        }

        public void SetNeedsWriteBack(PhysicalPromotionReplacement replacement)
        {
            replacement.NeedsWriteBack = true;
            assert(!replacement.NeedsReadBack);
        }

        public void ClearNeedsWriteBack(PhysicalPromotionReplacement replacement)
        {
            replacement.NeedsWriteBack = false;
        }

        public void SetNeedsReadBack(PhysicalPromotionReplacement replacement)
        {
            if (!replacement.NeedsReadBack)
            {
                replacement.NeedsReadBack = true;
                _pendingReadBacks++;
            }
        }

        public void ClearNeedsReadBack(PhysicalPromotionReplacement replacement)
        {
            if (replacement.NeedsReadBack)
            {
                assert(_pendingReadBacks > 0);
                replacement.NeedsReadBack = false;
                _pendingReadBacks--;
            }
        }

        private void VisitOverlappingReplacements(int local, int offset, int size,
            Func<PhysicalPromotionReplacement, bool> visit)
        {
            var aggregate = _aggregates.Lookup(local);
            if ((aggregate is null) ||
                !aggregate.OverlappingReplacements(offset, size, out var first, out var end))
            {
                return;
            }

            for (var index = first; index < end; index++)
            {
                if (!visit(aggregate.Replacements[index]))
                {
                    break;
                }
            }
        }

        public void CheckForwardSubForLastUse(int local)
        {
            if ((_currentBlock?.FirstStmt is null) ||
                ReferenceEquals(_currentBlock.FirstStmt, _currentStatement))
            {
                return;
            }

            var previous = CurrentStatement.PrevStmt
                ?? throw new InvalidOperationException("A non-first statement must have a predecessor.");
            var previousRoot = previous.RootNode;
            if (previousRoot.Oper.IsLocalStore && (previousRoot.AsLclVarCommon().LclNum == local))
            {
                _mayHaveForwardSub = true;
            }
        }

        private void WriteBackBeforeCurrentStatement(int local, int offset, int size)
        {
            VisitOverlappingReplacements(local, offset, size, replacement =>
            {
                if (replacement.NeedsWriteBack)
                {
                    var tree = _compiler.PhysicalPromotionCreateWriteBack(local, replacement);
                    var statement = _compiler.fgNewStmtFromTree(tree);
#if DEBUG
                    JITDUMP($"Writing back {replacement.Description} before {FMT_STMT(CurrentStatement.Id)}\n");
#endif
                    DISPSTMT(statement);
                    _compiler.fgInsertStmtBefore(CurrentBlock, CurrentStatement, statement);
                    ClearNeedsWriteBack(replacement);
                }

                return true;
            });
        }

        private void WriteBackBeforeUse(ref GenTree use, int local, int offset, int size)
        {
            var prefix = new PhysicalPromotionDecompositionStatementList();
            VisitOverlappingReplacements(local, offset, size, replacement =>
            {
                if (replacement.NeedsWriteBack)
                {
                    prefix.AddStatement(_compiler.PhysicalPromotionCreateWriteBack(local, replacement));
                    ClearNeedsWriteBack(replacement);
                }

                return true;
            });

            if (prefix.Count != 0)
            {
                use = prefix.PrefixTo(use, _compiler);
                _madeChanges = true;
            }
        }

        private void MarkForReadBack(GenTreeLclVarCommon local, int size, string reason)
        {
            assert(_compiler.fgGetTopLevelQmark(CurrentStatement.RootNode, out _) is null);
            var aggregate = _aggregates.Lookup(local.LclNum);
            if ((aggregate is null) ||
                !aggregate.OverlappingReplacements(local.LclOffs, size, out var first, out var end))
            {
                return;
            }

            var deaths = _liveness.GetDeathsForStructLocal(local);
#if DEBUG
            JITDUMP($"Fields of [{local.TreeId:D6}] in range [{local.LclOffs:D3}.." +
                $"{local.LclOffs + size:D3}) need to be read back: {reason}\n");
#endif
            for (var index = first; index < end; index++)
            {
                var replacement = aggregate.Replacements[index];
                if (!deaths.IsReplacementDying(index))
                {
                    SetNeedsReadBack(replacement);
                    JITDUMP($"  V{replacement.LclNum:D2} ({replacement.Description}) marked\n");
                }
                else
                {
                    JITDUMP($"  V{replacement.LclNum:D2} ({replacement.Description}) not marked (is dying)\n");
                }

                ClearNeedsWriteBack(replacement);
            }
        }
    }
}
