// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, inductionvariableopts.cpp.

using System;
using System.Collections.Generic;
using System.Numerics;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class StrengthReductionContext
    {
        private bool InitializeCursors(GenTreeLclVarCommon primaryLocal, ScevAddRec primaryIV)
        {
            _cursors1.Clear();
            _cursors2.Clear();
            _intermediateIVStores.Clear();
            var visited = _loopInfo.VisitOccurrences(_loop, primaryLocal.LclNum, (block, stmt, tree) => {
                if (IsUseExpectedToBeRemoved(block, stmt, tree))
                {
                    return true;
                }
                if (tree.Oper is not GT_LCL_VAR || tree.SsaNum != primaryLocal.SsaNum)
                {
                    return false;
                }
                var iv = _scevContext.Analyze(block, tree);
                if (iv is null)
                {
                    return false;
                }
                assert(Scev.Equals(_scevContext.Simplify(iv, _assumptions), primaryIV));
                _cursors1.Add(new CursorInfo(block, stmt, tree, primaryIV));
                _cursors2.Add(new CursorInfo(block, stmt, tree, primaryIV));
                return true;
            });
            if (!visited || _cursors1.Count == 0)
            {
                JITDUMP("  Could not create cursors for all loop uses of primary IV\n");
                return false;
            }

            ExpandStoredCursors(_cursors1, _cursors2);
            JITDUMP($"  Found {_cursors1.Count} cursors using primary IV V{primaryLocal.LclNum:D2}\n");
#if DEBUG
            if (_compiler.verbose)
            {
                for (var i = 0; i < _cursors1.Count; i++)
                {
                    var cursor = _cursors1[i];
                    jitprintf($"    [{i}] [{cursor.Tree.TreeId:D6}]: ");
                    if (cursor.IV is not ScevAddRec iv)
                    {
                        throw new FatalJitException("An initialized IV cursor needs a recurrence.");
                    }
                    iv.Dump(_compiler);
                    jitprintf("\n");
                }
            }
#endif
            return true;
        }

        private bool IsUseExpectedToBeRemoved(BasicBlock block, Statement stmt, GenTreeLclVarCommon tree)
        {
            if (_compiler.optIsUpdateOfIVWithoutSideEffects(stmt.RootNode, tree.LclNum))
            {
                return true;
            }

            var insideExitTest = (block.Kind is BBJ_COND) && (stmt == block.LastStmt) &&
                (!_loop.ContainsBlock(block.TrueTarget) || !_loop.ContainsBlock(block.FalseTarget));
            if (!insideExitTest)
            {
                return false;
            }

            var test = block.LastStmt!.RootNode.AsUnOp().Op1;
            if (!_compiler.optCanAndShouldChangeExitTest(test, dump: false))
            {
                return false;
            }

            var domTree = _compiler._domTree
                ?? throw new FatalJitException("Strength reduction requires dominators.");
            foreach (var edge in _loop.BackEdges)
            {
                if (!domTree.Dominates(block, edge.SourceBlock))
                {
                    return false;
                }
            }
            if (_loop.MayExecuteBlockMultipleTimesPerIteration(block))
            {
                return false;
            }
            return _scevContext.ComputeExitNotTakenCount(block) is not null;
        }

        private void AdvanceCursors(List<CursorInfo> cursors, List<CursorInfo> nextCursors)
        {
            for (var i = 0; i < cursors.Count; i++)
            {
                var cursor = cursors[i];
                var nextCursor = nextCursors[i];
                assert((nextCursor.Block == cursor.Block) && (nextCursor.Stmt == cursor.Stmt));
                var cursorIV = cursor.IV
                    ?? throw new FatalJitException("An active IV cursor needs a recurrence.");
                nextCursor.Tree = cursor.Tree;
                nextCursor.AdvancedTree = cursor.Tree;
                do
                {
                    var current = nextCursor.Tree;
                    var parent = _compiler.optFindIVParent(nextCursor.Stmt, current);
                    nextCursor.AdvancedTree = parent;
                    if ((parent is null) || ((parent.Oper is GT_COMMA) && parent.AsOp().Op1 == current))
                    {
                        nextCursor.IV = null;
                        break;
                    }
                    nextCursor.Tree = parent;
                    var parentIV = _scevContext.Analyze(nextCursor.Block, parent);
                    if (parentIV is null)
                    {
                        nextCursor.IV = null;
                        break;
                    }
                    parentIV = _scevContext.Simplify(parentIV, _assumptions);
                    nextCursor.IV = parentIV as ScevAddRec;
                    if (nextCursor.IV is null)
                    {
                        break;
                    }
                } while (Scev.Equals(nextCursor.IV, cursorIV));
            }

#if DEBUG
            if (_compiler.verbose)
            {
                for (var i = 0; i < nextCursors.Count; i++)
                {
                    var cursor = nextCursors[i];
                    jitprintf($"    [{i}] [{(cursor.AdvancedTree?.TreeId ?? 0):D6}]: ");
                    if (cursor.IV is null)
                    {
                        jitprintf("<null IV>");
                    }
                    else
                    {
                        cursor.IV.Dump(_compiler);
                    }
                    jitprintf("\n");
                }
            }
#endif
        }

        private void ExpandStoredCursors(List<CursorInfo> cursors, List<CursorInfo> otherCursors)
        {
            for (var i = 0; i < cursors.Count; i++)
            {
                while (true)
                {
                    var cursor = cursors[i];
                    var cursorIV = cursor.IV
                        ?? throw new FatalJitException("An active IV cursor needs a recurrence.");
                    var current = cursor.Tree;
                    var parent = _compiler.optFindIVParent(cursor.Stmt, current);
                    if ((parent is null) || ((parent.Oper is GT_COMMA) && parent.AsOp().Op1 == current))
                    {
                        break;
                    }
                    if (parent.Oper is GT_STORE_LCL_VAR)
                    {
                        var storedLocal = parent.AsLclVarCommon();
                        if ((storedLocal.Data == current) && ((current.Flags & GTF_SIDE_EFFECT) == 0) &&
                            storedLocal.HasSsaName &&
                            !_compiler.optLocalHasNonLoopUses(storedLocal.LclNum, _loop, _loopInfo))
                        {
                            var numCreated = 0;
                            var expanded = _loopInfo.VisitOccurrences(_loop, storedLocal.LclNum,
                                (block, stmt, use) => {
                                    if (use == parent)
                                    {
                                        return true;
                                    }
                                    if ((use.Oper is not GT_LCL_VAR) || (use.SsaNum != storedLocal.SsaNum))
                                    {
                                        return false;
                                    }
                                    var iv = _scevContext.Analyze(block, use);
                                    if (iv is null ||
                                        !Scev.Equals(_scevContext.Simplify(iv, _assumptions), cursorIV))
                                    {
                                        return false;
                                    }
                                    cursors.Add(new CursorInfo(block, stmt, use, cursorIV));
                                    otherCursors.Add(new CursorInfo(block, stmt, use, cursorIV));
                                    numCreated++;
                                    return true;
                                });
                            if (expanded)
                            {
#if DEBUG
                                JITDUMP($"  [{current.TreeId:D6}] was the data of store " +
                                    $"[{parent.TreeId:D6}]; expanded to {numCreated} new cursors, " +
                                    "and will replace with a store of 0\n");
#endif
                                _intermediateIVStores.Add(new CursorInfo(cursor.Block, cursor.Stmt, parent, null));
                                var last = cursors.Count - 1;
                                (cursors[i], cursors[last]) = (cursors[last], cursors[i]);
                                (otherCursors[i], otherCursors[last]) = (otherCursors[last], otherCursors[i]);
                                cursors.RemoveAt(last);
                                otherCursors.RemoveAt(last);
                                i--;
                                break;
                            }
                            cursors.RemoveRange(cursors.Count - numCreated, numCreated);
                            otherCursors.RemoveRange(otherCursors.Count - numCreated, numCreated);
                        }
                        break;
                    }

                    var parentIV = _scevContext.Analyze(cursor.Block, parent);
                    if (parentIV is null ||
                        !Scev.Equals(_scevContext.Simplify(parentIV, _assumptions), cursorIV))
                    {
                        break;
                    }
                    cursor.Tree = parent;
                }
            }
        }

        private bool CheckAdvancedCursors(List<CursorInfo> cursors, out ScevAddRec? nextIV)
        {
            ScevAddRec? commonIV = null;
#if !TARGET_ARM64
            var allowRephrasingNextIV = true;
#endif
            for (var i = 0; i < cursors.Count; i++)
            {
                var cursor = cursors[i];
                if (cursor.IV is not null)
                {
#if TARGET_ARM64
                    throw new FatalJitException("ARM64 address-mode scaling in IV strength reduction is not ported.");
#else
                    var allowRephrasingViaScaling = true;
                    if (commonIV is null)
                    {
                        commonIV = cursor.IV;
                        allowRephrasingNextIV = allowRephrasingViaScaling;
                        continue;
                    }

                    var rephrasable = ComputeRephrasableIV(cursor.IV,
                        allowRephrasingViaScaling, commonIV, allowRephrasingNextIV);
                    if (rephrasable is not null)
                    {
                        commonIV = rephrasable;
                        allowRephrasingNextIV &= allowRephrasingViaScaling;
                        continue;
                    }
#endif
                }
                JITDUMP($"    [{i}] does not match; will not advance\n");
                nextIV = null;
                return false;
            }

            nextIV = commonIV;
#pragma warning disable CA1508 // ARM64 cursor scaling is not yet implemented.
            return commonIV is not null;
#pragma warning restore CA1508
        }

        private static int Gcd(int a, int b)
        {
            while (a != 0)
            {
                var next = unchecked(b % a);
                b = a;
                a = next;
            }
            return b;
        }

        private static long Gcd(long a, long b)
        {
            while (a != 0)
            {
                var next = unchecked(b % a);
                b = a;
                a = next;
            }
            return b;
        }

        private ScevAddRec? ComputeRephrasableIVByScaling(ScevAddRec first,
            bool allowFirstScaling, ScevAddRec second, bool allowSecondScaling, bool isLong)
        {
            if (!first.Start.GetConstantValue(_compiler, out var start) ||
                (isLong ? start != 0 : unchecked((int)start) != 0) ||
                !second.Start.GetConstantValue(_compiler, out start) ||
                (isLong ? start != 0 : unchecked((int)start) != 0) ||
                !first.Step.GetConstantValue(_compiler, out var firstStep) ||
                !second.Step.GetConstantValue(_compiler, out var secondStep))
            {
                return null;
            }

            var gcd = isLong
                ? Gcd(firstStep, secondStep)
                : Gcd(unchecked((int)firstStep), unchecked((int)secondStep));
            if ((!allowFirstScaling && (gcd != (isLong ? firstStep : unchecked((int)firstStep)))) ||
                (!allowSecondScaling && (gcd != (isLong ? secondStep : unchecked((int)secondStep)))))
            {
                return null;
            }
            if (gcd == (isLong ? firstStep : unchecked((int)firstStep)))
            {
                return first;
            }
            if (gcd == (isLong ? secondStep : unchecked((int)secondStep)))
            {
                return second;
            }
            if (gcd is 1 or -1)
            {
                return null;
            }
            return _scevContext.NewAddRec(first.Start, _scevContext.NewConstant(first.Type, gcd));
        }

        private ScevAddRec? ComputeRephrasableIV(ScevAddRec first, bool allowFirstScaling,
            ScevAddRec second, bool allowSecondScaling)
        {
            if (!Scev.Equals(first.Start, second.Start))
            {
                return null;
            }
            if (Scev.Equals(first.Step, second.Step))
            {
                return first;
            }
            return first.Type switch {
                TYP_INT => ComputeRephrasableIVByScaling(first,
                    allowFirstScaling, second, allowSecondScaling, isLong: false),
                TYP_LONG => ComputeRephrasableIVByScaling(first,
                    allowFirstScaling, second, allowSecondScaling, isLong: true),
                _ => null,
            };
        }

        private GenTree RephraseIV(ScevAddRec iv, ScevAddRec sourceIV, GenTree sourceTree)
        {
            assert(Scev.Equals(iv.Start, sourceIV.Start));
            if (Scev.Equals(iv.Step, sourceIV.Step))
            {
                return sourceTree;
            }
            if (!iv.Step.GetConstantValue(_compiler, out var ivStep) ||
                !sourceIV.Step.GetConstantValue(_compiler, out var sourceStep))
            {
                throw new FatalJitException("Rephrasing an IV requires constant steps.");
            }
            assert(iv.Type == sourceIV.Type);
            if (iv.Type is TYP_INT)
            {
                var scale = unchecked((int)ivStep) / unchecked((int)sourceStep);
                assert(unchecked((int)ivStep) % unchecked((int)sourceStep) == 0);
                return (scale > 0) && BitOperations.IsPow2((uint)scale)
                    ? _compiler.gtNewBinaryNode(GT_LSH, TYP_INT, sourceTree,
                        _compiler.gtNewIconNode(TYP_INT, BitOperations.Log2((uint)scale)))
                    : _compiler.gtNewBinaryNode(GT_MUL, TYP_INT, sourceTree,
                        _compiler.gtNewIconNode(TYP_INT, scale));
            }
            if (iv.Type is TYP_LONG)
            {
                var scale = ivStep / sourceStep;
                assert(ivStep % sourceStep == 0);
                return (scale > 0) && BitOperations.IsPow2((ulong)scale)
                    ? _compiler.gtNewBinaryNode(GT_LSH, TYP_LONG, sourceTree,
                        _compiler.gtNewLconNode(BitOperations.Log2((ulong)scale)))
                    : _compiler.gtNewBinaryNode(GT_MUL, TYP_LONG, sourceTree,
                        _compiler.gtNewLconNode(scale));
            }
            throw new FatalJitException("Only 32- and 64-bit IVs can be rephrased.");
        }
    }
}
