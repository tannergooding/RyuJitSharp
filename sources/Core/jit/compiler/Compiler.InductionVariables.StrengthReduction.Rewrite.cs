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
            var stepValue = _scevContext.Materialize(iv.Step)
                ?? throw new FatalJitException("A constant IV step must materialize.");
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
                var replacement = RephraseIV(cursor.IV
                    ?? throw new FatalJitException("A replacement cursor needs its recurrence."), iv, newUse);
#if DEBUG
                JITDUMP($"    Replacing use [{cursor.Tree.TreeId:D6}] with " +
                    $"[{replacement.TreeId:D6}]. Before:\n");
#endif
                DISPSTMT(cursor.Stmt);

                var link = _compiler.gtFindLink(cursor.Stmt, cursor.Tree);
                ref var use = ref link.result;
                if (Unsafe.IsNullRef(ref use))
                {
                    throw new FatalJitException("The strength-reduced use was lost from its statement.");
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
#if DEBUG
                    JITDUMP($"      Replacing [{store.Data.TreeId:D6}] with a zero constant\n");
#endif
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
            var domTree = _compiler._domTree
                ?? throw new FatalJitException("Strength reduction requires dominators.");
            BasicBlock? insertionPoint = null;
            foreach (var backEdge in _loop.BackEdges)
            {
                insertionPoint = insertionPoint is null
                    ? backEdge.SourceBlock
                    : domTree.Intersect(insertionPoint, backEdge.SourceBlock);
            }

#if TARGET_ARM64
            throw new FatalJitException("ARM64 post-indexed IV update placement is not ported.");
#else
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
#endif
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
