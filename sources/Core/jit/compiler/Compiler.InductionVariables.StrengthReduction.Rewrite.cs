// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, inductionvariableopts.cpp.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class StrengthReductionContext
    {
        private bool TryReplaceUsesWithNewPrimaryIV(List<CursorInfo> cursors, ScevAddRec iv)
        {
            if (!iv.Step.GetConstantValue(_compiler, out _))
            {
                JITDUMP("    Skipping: step value is not a constant\n");
                return false;
            }

            var insertionPoint = FindUpdateInsertionPoint(cursors, out var afterStmt);
            if (insertionPoint is null)
            {
                JITDUMP("    Skipping: could not find a legal insertion point for the new IV update\n");
                return false;
            }

            var preheader = _loop.EntryEdge(0).SourceBlock;
            var initValue = _scevContext.Materialize(iv.Start);
            if (initValue is null)
            {
                JITDUMP("    Skipping: init value could not be materialized\n");
                return false;
            }

            JITDUMP("    Strength reducing\n");
            var stepValue = _scevContext.Materialize(iv.Step);
            if (stepValue is null)
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage135
                );
                return default!;
            }
            var newLocal = _compiler.lvaGrabTemp(false, "Strength reduced derived IV");
            var initStore = _compiler.gtNewTempStore(newLocal, initValue);
            var initStmt = _compiler.fgNewStmtFromTree(initStore);
            _compiler.fgInsertStmtNearEnd(preheader, initStmt);
            JITDUMP($"    Inserting init statement in preheader {FMT_BB(preheader.bbNum)}\n");
            DISPSTMT(initStmt);

            var nextValue = _compiler.gtNewBinaryNode(GT_ADD, iv.Type,
                _compiler.gtNewLclVarNode(iv.Type, newLocal), stepValue);
            var stepStore = _compiler.gtNewTempStore(newLocal, nextValue);
            var stepStmt = _compiler.fgNewStmtFromTree(stepStore);
            if (afterStmt is not null)
            {
                _compiler.fgInsertStmtAfter(insertionPoint, afterStmt, stepStmt);
            }
            else
            {
                _compiler.fgInsertStmtNearEnd(insertionPoint, stepStmt);
            }
            JITDUMP($"    Inserting step statement in {FMT_BB(insertionPoint.bbNum)}\n");
            DISPSTMT(stepStmt);

            foreach (var cursor in cursors)
            {
                var newUse = _compiler.gtNewLclVarNode(iv.Type, newLocal);
                var cursorIV = cursor.IV;
                if (cursorIV is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage136
                    );
                    return false;
                }

                var replacement = RephraseIV(cursorIV, iv, newUse);
                JITDUMP($"    Replacing use [{cursor.Tree.TreeId:D6}] with " +
                    $"[{replacement.TreeId:D6}]. Before:\n");
                DISPSTMT(cursor.Stmt);

                var link = _compiler.gtFindLink(cursor.Stmt, cursor.Tree);
                ref var use = ref link.result;
                if (Unsafe.IsNullRef(ref use))
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage137
                    );
                    return default!;
                }

                GenTree? sideEffects = null;
                _compiler.gtExtractSideEffList(cursor.Tree, ref sideEffects);
                use = sideEffects is null
                    ? replacement
                    : _compiler.gtNewBinaryNode(GT_COMMA, replacement.Type, sideEffects, replacement);
                JITDUMP("\n      After:\n\n");
                DISPSTMT(cursor.Stmt);
                _compiler.gtSetStmtInfo(cursor.Stmt);
                _compiler.fgSetStmtSeq(cursor.Stmt);
                _compiler.gtUpdateStmtSideEffects(cursor.Stmt);
            }

            if (_intermediateIVStores.Count > 0)
            {
                JITDUMP("    Deleting stores of intermediate IVs\n");
                foreach (var cursor in _intermediateIVStores)
                {
                    var store = cursor.Tree.AsLclVarCommon();
                    JITDUMP($"      Replacing [{store.Data.TreeId:D6}] with a zero constant\n");
                    store.DataRef = _compiler.gtNewZeroConNode(store.Data.Type.ActualType);
                    _compiler.gtSetStmtInfo(cursor.Stmt);
                    _compiler.fgSetStmtSeq(cursor.Stmt);
                    _compiler.gtUpdateStmtSideEffects(cursor.Stmt);
                }
            }
            return true;
        }

        private BasicBlock? FindUpdateInsertionPoint(List<CursorInfo> cursors, out Statement? afterStmt)
        {
            afterStmt = null;
            var domTree = _compiler._domTree;
            if (domTree is null)
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage130
                );
                afterStmt = default!;
                return default!;
            }
            BasicBlock? insertionPoint = null;
            foreach (var backEdge in _loop.BackEdges)
            {
                insertionPoint = insertionPoint is null
                    ? backEdge.SourceBlock
                    : domTree.Intersect(insertionPoint, backEdge.SourceBlock);
            }

            if (Configuration.TARGET_ARM64)
            {
                var postUseInsertionPoint = FindPostUseUpdateInsertionPoint(cursors, insertionPoint, out afterStmt);
                if (postUseInsertionPoint is not null)
                {
                    if (Configuration.DEBUG)
                    {
                        var latestUse = afterStmt;
                        if (latestUse is null)
                        {
                            FatalJitException.ThrowFatalJitException("Post-use placement needs a use statement.");
                            afterStmt = default!;
                            return default!;
                        }
                        JITDUMP($"    Found a legal insertion point after a last use of the IV in {FMT_BB(postUseInsertionPoint.bbNum)} after {FMT_STMT(latestUse.Id)}\n");
                    }
                    return postUseInsertionPoint;
                }
            }

            while ((insertionPoint is not null) && _loop.ContainsBlock(insertionPoint) &&
                _loop.MayExecuteBlockMultipleTimesPerIteration(insertionPoint))
            {
                insertionPoint = insertionPoint.bbIDom;
            }
            if ((insertionPoint is null) || !_loop.ContainsBlock(insertionPoint) ||
                !InsertionPointPostDominatesUses(insertionPoint, cursors))
            {
                return null;
            }
            JITDUMP($"    Found a legal insertion point in {FMT_BB(insertionPoint.bbNum)}\n");
            return insertionPoint;
        }

        private BasicBlock? FindPostUseUpdateInsertionPoint(List<CursorInfo> cursors,
            BasicBlock? backEdgeDominator, out Statement? afterStmt)
        {
            afterStmt = null;
            var blocksWithUses = new HashSet<BasicBlock>();
            foreach (var cursor in cursors)
            {
                _ = blocksWithUses.Add(cursor.Block);
            }

            while ((backEdgeDominator is not null) && _loop.ContainsBlock(backEdgeDominator))
            {
                if (!blocksWithUses.Contains(backEdgeDominator))
                {
                    backEdgeDominator = backEdgeDominator.bbIDom;
                    continue;
                }

                if (_loop.MayExecuteBlockMultipleTimesPerIteration(backEdgeDominator))
                {
                    return null;
                }

                Statement? latestStmt = null;
                foreach (var cursor in cursors)
                {
                    if (cursor.Block != backEdgeDominator)
                    {
                        continue;
                    }

                    if (latestStmt is null)
                    {
                        latestStmt = cursor.Stmt;
                    }
                    else
                    {
                        latestStmt = _compiler.gtLatestStatement(latestStmt, cursor.Stmt);
                    }
                }

                assert(latestStmt is not null);
                if (!InsertionPointPostDominatesUses(backEdgeDominator, cursors))
                {
                    return null;
                }

                afterStmt = latestStmt;
                return backEdgeDominator;
            }

            return null;
        }

        private bool InsertionPointPostDominatesUses(BasicBlock insertionPoint, List<CursorInfo> cursors)
        {
            foreach (var cursor in cursors)
            {
                if (insertionPoint == cursor.Block)
                {
                    if (insertionPoint.HasTerminator && (cursor.Stmt == insertionPoint.LastStmt))
                    {
                        return false;
                    }
                }
                else if (!_loop.IsPostDominatedOnLoopIteration(cursor.Block, insertionPoint))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
