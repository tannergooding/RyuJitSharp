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
        private interface IReplacementVisitor<TSelf>
            where TSelf : struct, IReplacementVisitor<TSelf>
        {
            bool Visit(PhysicalPromotionReplacement replacement);
        }

        private readonly struct WriteBackBeforeCurrentStatementVisitor(
            PhysicalPromotionReplaceVisitor owner, int local) : IReplacementVisitor<WriteBackBeforeCurrentStatementVisitor>
        {
            public bool Visit(PhysicalPromotionReplacement replacement) =>
                owner.WriteBackBeforeCurrentStatement(local, replacement);
        }

        private readonly struct WriteBackBeforeUseVisitor(
            PhysicalPromotionReplaceVisitor owner, PhysicalPromotionDecompositionStatementList prefix, int local)
            : IReplacementVisitor<WriteBackBeforeUseVisitor>
        {
            public bool Visit(PhysicalPromotionReplacement replacement) =>
                owner.WriteBackBeforeUse(prefix, local, replacement);
        }

        private readonly Compiler _compiler = compiler;
        private readonly PhysicalPromotionAggregateInfoMap _aggregates = aggregates;
        private readonly PhysicalPromotionLiveness _liveness = liveness;
        private readonly FlowGraphDfsTree _dfsTree = dfsTree;
        private readonly BitVecTraits _postOrderTraits = dfsTree.PostOrderTraits();
        private BitVecTraits? _replacementTraits;
        private BitVec[] _pendingReadBacksByBlock = [];
        private BitVec[] _currentStructFields = [];
        private BitVec _requiresAlreadyReadBackOnEntry = [];
        private BitVec _requiresReadBackOnExit = [];
        private BitVec _needsReadBack = [];
        private BitVec _structCurrent = [];
        private BitVec _reconcileReadBacks = [];
        private BitVec _entryReadBacks = [];
        private ReplacementInfo[] _replacementInfo = [];
        private BasicBlock? _currentBlock;
        private Statement? _currentStatement;
        private bool _madeChanges;
        private bool _mayHaveForwardSub;
        private readonly record struct ReplacementInfo(PhysicalPromotionAggregateInfo Aggregate, int Index);

        private BasicBlock CurrentBlock
        {
            get
            {
                var currentBlock = _currentBlock;
                if (currentBlock is null)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.PhysicalPromotionHasNotStartedABlock);
                }

                return currentBlock;
            }
        }

        private Statement CurrentStatement
        {
            get
            {
                var currentStatement = _currentStatement;
                if (currentStatement is null)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.PhysicalPromotionHasNotStartedAStatement);
                }

                return currentStatement;
            }
        }

        private BitVecTraits ReplacementTraits
        {
            get
            {
                var replacementTraits = _replacementTraits;
                if (replacementTraits is null)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.PhysicalPromotionHasNotPreparedReadbacks);
                }

                return replacementTraits;
            }
        }

        public void StartBlock(BasicBlock block)
        {
            _currentBlock = block;

            if (block == _compiler.fgFirstBB)
            {
                BitVecOps.Assign(ReplacementTraits, ref _needsReadBack, _entryReadBacks);
                BitVecOps.Assign(ReplacementTraits, ref _structCurrent, _entryReadBacks);
                return;
            }

            if (BitVecOps.IsMember(_postOrderTraits, _requiresAlreadyReadBackOnEntry, block.bbPostorderNum))
            {
                BitVecOps.ClearD(ReplacementTraits, _needsReadBack);
                BitVecOps.ClearD(ReplacementTraits, _structCurrent);
                return;
            }

            var first = true;
            BitVecOps.ClearD(ReplacementTraits, _reconcileReadBacks);
            foreach (var predecessor in block.PredBlocks)
            {
                if (!_dfsTree.Contains(predecessor))
                {
                    continue;
                }

                assert(predecessor.bbPostorderNum > block.bbPostorderNum);
                BitVecOps.UnionD(ReplacementTraits, _reconcileReadBacks,
                    _pendingReadBacksByBlock[predecessor.bbPostorderNum]);
                if (first)
                {
                    BitVecOps.Assign(ReplacementTraits, ref _needsReadBack,
                        _pendingReadBacksByBlock[predecessor.bbPostorderNum]);
                    BitVecOps.Assign(ReplacementTraits, ref _structCurrent,
                        _currentStructFields[predecessor.bbPostorderNum]);
                }
                else
                {
                    BitVecOps.IntersectionD(ReplacementTraits, _needsReadBack,
                        _pendingReadBacksByBlock[predecessor.bbPostorderNum]);
                    BitVecOps.IntersectionD(ReplacementTraits, _structCurrent,
                        _currentStructFields[predecessor.bbPostorderNum]);
                }

                first = false;
            }

            if (first)
            {
                BitVecOps.ClearD(ReplacementTraits, _needsReadBack);
                BitVecOps.ClearD(ReplacementTraits, _structCurrent);
            }
            else
            {
                BitVecOps.DiffD(ReplacementTraits, _reconcileReadBacks, _needsReadBack);
                _ = BitVecOps.VisitBits(ReplacementTraits, _structCurrent, replacementIndex =>
                {
                    var info = _replacementInfo[replacementIndex];
                    if (!_liveness.IsReplacementLiveIn(block, info.Aggregate.LclNum, info.Index))
                    {
                        BitVecOps.RemoveElemD(ReplacementTraits, _structCurrent, replacementIndex);
                        BitVecOps.RemoveElemD(ReplacementTraits, _needsReadBack, replacementIndex);
                    }

                    return true;
                });
            }

            _ = BitVecOps.VisitBits(ReplacementTraits, _reconcileReadBacks, replacementIndex =>
            {
                var info = _replacementInfo[replacementIndex];
                var aggregate = info.Aggregate;
                if (!_liveness.IsReplacementLiveIn(block, aggregate.LclNum, info.Index))
                {
                    return true;
                }

                foreach (var predecessor in block.PredBlocks)
                {
                    if (_dfsTree.Contains(predecessor) &&
                        (predecessor.bbPostorderNum > block.bbPostorderNum) &&
                        BitVecOps.IsMember(ReplacementTraits,
                            _pendingReadBacksByBlock[predecessor.bbPostorderNum], replacementIndex))
                    {
                        InsertReadBackAtEnd(predecessor, aggregate.LclNum, aggregate.Replacements[info.Index]);
                        BitVecOps.RemoveElemD(ReplacementTraits,
                            _pendingReadBacksByBlock[predecessor.bbPostorderNum], replacementIndex);
                    }
                }

                return true;
            });

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
            assert(BitVecOps.IsSubset(ReplacementTraits, _needsReadBack, _structCurrent));
            var materialize = BitVecOps.IsMember(_postOrderTraits, _requiresReadBackOnExit,
                CurrentBlock.bbPostorderNum);

            _ = BitVecOps.VisitBits(ReplacementTraits, _needsReadBack, replacementIndex =>
            {
                var info = _replacementInfo[replacementIndex];
                var aggregate = info.Aggregate;
                var replacement = aggregate.Replacements[info.Index];
                if (_liveness.IsReplacementLiveOut(CurrentBlock, aggregate.LclNum, info.Index))
                {
                    if (materialize || (replacement.ReadBackPlacement == CurrentBlock))
                    {
                        InsertReadBackAtEnd(CurrentBlock, aggregate.LclNum, replacement);
                        ClearNeedsReadBack(replacement);
                    }
                }
                else
                {
                    JITDUMP($"Skipping reading back dead replacement V{aggregate.LclNum:D2}." +
                        $"[{replacement.Offset:D3}..{replacement.Offset + replacement.AccessType.Size:D3}) " +
                        $"-> V{replacement.LclNum:D2} near the end of {FMT_BB(CurrentBlock.bbNum)}\n");
                    ClearNeedsReadBack(replacement);
                }

                return true;
            });

            BitVecOps.Assign(ReplacementTraits, ref _pendingReadBacksByBlock[CurrentBlock.bbPostorderNum],
                _needsReadBack);
            BitVecOps.Assign(ReplacementTraits, ref _currentStructFields[CurrentBlock.bbPostorderNum],
                _structCurrent);
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
            if (BitVecOps.IsEmpty(ReplacementTraits, _needsReadBack))
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

                var index = LowerBound(aggregate.Replacements, occurrence.LclOffs);
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
            if (NeedsReadBack(replacement))
            {
                JITDUMP($"Reading back replacement V{local:D2}.[{replacement.Offset:D3}.." +
                    $"{replacement.Offset + replacement.AccessType.Size:D3}) -> V{replacement.LclNum:D2} " +
                    $"before [{CurrentStatement.RootNode.TreeId:D6}]:\n");
                var tree = _compiler.PhysicalPromotionCreateReadBack(local, replacement);
                var statement = _compiler.fgNewStmtFromTree(tree);
                DISPSTMT(statement);
                _compiler.fgInsertStmtBefore(CurrentBlock, CurrentStatement, statement);
                ClearNeedsReadBack(replacement);
            }
        }

        public void SetNeedsWriteBack(PhysicalPromotionReplacement replacement)
        {
            BitVecOps.RemoveElemD(ReplacementTraits, _structCurrent, replacement.ReplacementIndex);
            assert(!NeedsReadBack(replacement));
        }

        public void ClearNeedsWriteBack(PhysicalPromotionReplacement replacement)
        {
            BitVecOps.AddElemD(ReplacementTraits, _structCurrent, replacement.ReplacementIndex);
        }

        public void SetNeedsReadBack(PhysicalPromotionReplacement replacement)
        {
            BitVecOps.AddElemD(ReplacementTraits, _needsReadBack, replacement.ReplacementIndex);
        }

        public void ClearNeedsReadBack(PhysicalPromotionReplacement replacement)
        {
            BitVecOps.RemoveElemD(ReplacementTraits, _needsReadBack, replacement.ReplacementIndex);
        }

        public bool NeedsReadBack(PhysicalPromotionReplacement replacement) =>
            BitVecOps.IsMember(ReplacementTraits, _needsReadBack, replacement.ReplacementIndex);

        public bool NeedsWriteBack(PhysicalPromotionReplacement replacement) =>
            !BitVecOps.IsMember(ReplacementTraits, _structCurrent, replacement.ReplacementIndex);

        private void VisitOverlappingReplacements<TVisitor>(int local, int offset, int size, ref TVisitor visitor)
            where TVisitor : struct, IReplacementVisitor<TVisitor>
        {
            var aggregate = _aggregates.Lookup(local);
            if ((aggregate is null) ||
                !aggregate.OverlappingReplacements(offset, size, out var first, out var end))
            {
                return;
            }

            for (var index = first; index < end; index++)
            {
                if (!visitor.Visit(aggregate.Replacements[index]))
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

            var previous = CurrentStatement.PrevStmt;
            if (previous is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.NonFirstStatementMustHaveAPredecessor);
            }
            var previousRoot = previous.RootNode;
            if (previousRoot.Oper.IsLocalStore && (previousRoot.AsLclVarCommon().LclNum == local))
            {
                _mayHaveForwardSub = true;
            }
        }

        private bool WriteBackBeforeCurrentStatement(int local, PhysicalPromotionReplacement replacement)
        {
            if (NeedsWriteBack(replacement))
            {
                var tree = _compiler.PhysicalPromotionCreateWriteBack(local, replacement);
                var statement = _compiler.fgNewStmtFromTree(tree);
                JITDUMP($"Writing back {replacement.Description} before {FMT_STMT(CurrentStatement.Id)}\n");
                DISPSTMT(statement);
                _compiler.fgInsertStmtBefore(CurrentBlock, CurrentStatement, statement);
                ClearNeedsWriteBack(replacement);
            }

            return true;
        }

        private void WriteBackBeforeCurrentStatement(int local, int offset, int size)
        {
            var visitor = new WriteBackBeforeCurrentStatementVisitor(this, local);
            VisitOverlappingReplacements(local, offset, size, ref visitor);
        }

        private bool WriteBackBeforeUse(PhysicalPromotionDecompositionStatementList prefix, int local,
            PhysicalPromotionReplacement replacement)
        {
            if (NeedsWriteBack(replacement))
            {
                prefix.AddStatement(_compiler.PhysicalPromotionCreateWriteBack(local, replacement));
                ClearNeedsWriteBack(replacement);
            }

            return true;
        }

        private void WriteBackBeforeUse(ref GenTree use, int local, int offset, int size)
        {
            var prefix = new PhysicalPromotionDecompositionStatementList();
            var visitor = new WriteBackBeforeUseVisitor(this, prefix, local);
            VisitOverlappingReplacements(local, offset, size, ref visitor);

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
            JITDUMP($"Fields of [{local.TreeId:D6}] in range [{local.LclOffs:D3}.." +
                $"{local.LclOffs + size:D3}) need to be read back: {reason}\n");
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
