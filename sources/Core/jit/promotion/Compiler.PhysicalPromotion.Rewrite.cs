// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotion.cpp and promotiondecomposition.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed unsafe partial class PhysicalPromotionReplaceVisitor(
        Compiler compiler, PhysicalPromotionAggregateInfoMap aggregates, PhysicalPromotionLiveness liveness)
    {
        private readonly Compiler _compiler = compiler;
        private readonly PhysicalPromotionAggregateInfoMap _aggregates = aggregates;
        private readonly PhysicalPromotionLiveness _liveness = liveness;
        private int _pendingReadBacks;
        private BasicBlock? _currentBlock;
        private Statement? _currentStatement;
        private bool _madeChanges;
        private bool _mayHaveForwardSub;
        private BasicBlock CurrentBlock => _currentBlock ??
            throw new InvalidOperationException("Physical promotion has not started a block.");
        private Statement CurrentStatement => _currentStatement ??
            throw new InvalidOperationException("Physical promotion has not started a statement.");

        public Statement? StartBlock(BasicBlock block)
        {
            _currentBlock = block;
            assert(_pendingReadBacks == 0);
            if (!ReferenceEquals(block, _compiler.fgFirstBB))
            {
                return block.FirstStmt;
            }

            Statement? lastInserted = null;
            foreach (var aggregate in _aggregates.Aggregates)
            {
                ref var descriptor = ref _compiler.lvaGetDesc(aggregate.LclNum);
                if (!descriptor.lvIsParam && !descriptor.lvIsOSRLocal)
                {
                    continue;
                }

                JITDUMP($"Processing fields of {(descriptor.lvIsParam ? "parameter" : "OSR-local")} " +
                    $"V{aggregate.LclNum:D2} in entry BB {FMT_BB(block.bbNum)}\n");
                for (var index = 0; index < aggregate.Replacements.Count; index++)
                {
                    var replacement = aggregate.Replacements[index];
                    ClearNeedsWriteBack(replacement);
                    if (!_liveness.IsReplacementLiveIn(block, aggregate.LclNum, index))
                    {
                        JITDUMP($"  V{replacement.LclNum:D2} ({replacement.Description}) " +
                            "ignored because it is not live-in to entry BB\n");
                        continue;
                    }

                    if (!descriptor.lvIsParam ||
                        !_compiler.PhysicalPromotionMapsToParameterRegister(
                            aggregate.LclNum, replacement.Offset, replacement.AccessType, allowBitwiseExtraction: true))
                    {
                        SetNeedsReadBack(replacement);
                        JITDUMP($"  V{replacement.LclNum:D2} ({replacement.Description}) " +
                            "marked as needing read back\n");
                        continue;
                    }

                    var tree = _compiler.PhysicalPromotionCreateReadBack(aggregate.LclNum, replacement);
                    var statement = _compiler.fgNewStmtFromTree(tree);
                    JITDUMP($"  V{replacement.LclNum:D2} ({replacement.Description}) " +
                        "is read back eagerly because it is a register parameter\n");
                    DISPSTMT(statement);
                    if (lastInserted is null)
                    {
                        _compiler.fgInsertStmtAtBeg(block, statement);
                    }
                    else
                    {
                        _compiler.fgInsertStmtAfter(block, lastInserted, statement);
                    }

                    lastInserted = statement;
                }
            }

            return lastInserted is null ? block.FirstStmt : lastInserted.NextStmt;
        }

        public void EndBlock()
        {
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
                            JITDUMP($"Reading back replacement V{aggregate.LclNum:D2}." +
                                $"[{replacement.Offset:D3}..{replacement.Offset + replacement.AccessType.Size:D3}) " +
                                $"-> V{replacement.LclNum:D2} near the end of {FMT_BB(CurrentBlock.bbNum)}:\n");
                            var tree = _compiler.PhysicalPromotionCreateReadBack(aggregate.LclNum, replacement);
                            var statement = _compiler.fgNewStmtFromTree(tree);
                            DISPSTMT(statement);
                            _compiler.fgInsertStmtNearEnd(CurrentBlock, statement);
                        }
                        else
                        {
                            JITDUMP($"Skipping reading back dead replacement V{aggregate.LclNum:D2}." +
                                $"[{replacement.Offset:D3}..{replacement.Offset + replacement.AccessType.Size:D3}) " +
                                $"-> V{replacement.LclNum:D2} near the end of {FMT_BB(CurrentBlock.bbNum)}\n");
                        }

                        ClearNeedsReadBack(replacement);
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

            foreach (var local in CurrentStatement.LocalsTreeList)
            {
                if (local.Type is TYP_STRUCT)
                {
                    continue;
                }

                var aggregate = _aggregates.Lookup(local.LclNum);
                if (aggregate is null)
                {
                    continue;
                }

                var index = LowerBound(aggregate.Replacements, local.LclOffs, static replacement => replacement.Offset);
                if ((index < aggregate.Replacements.Count) &&
                    (aggregate.Replacements[index].Offset == local.LclOffs))
                {
                    InsertPreStatementReadBackIfNecessary(aggregate.LclNum, aggregate.Replacements[index]);
                }
            }
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
