// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus StressSplitTree()
    {
        if (compStressCompile(STRESS_SPLIT_TREES_RANDOMLY, 10))
        {
            SplitTreesRandomly();
            return PhaseStatus.MODIFIED_EVERYTHING;
        }

        if (compStressCompile(STRESS_SPLIT_TREES_REMOVE_COMMAS, 10))
        {
            SplitTreesRemoveCommas();
            return PhaseStatus.MODIFIED_EVERYTHING;
        }

        return PhaseStatus.MODIFIED_NOTHING;
    }

    private void SplitTreesRandomly()
    {
#if DEBUG
        var rng = new CLRRandom(info.compMethodHash() ^ 0x077cc4d4);

        // Splitting creates a lot of new locals. Limit how many we create here.
        var maxLvaCount = uint.Max(unchecked((uint)lvaCount * 2), 50000u);
        var numSplit = 0;
        var splitLimit = JitConfig.JitStressSplitTreeLimit;

        foreach (var block in Blocks)
        {
            for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
            {
                if ((splitLimit >= 0) && (numSplit >= splitLimit))
                {
                    JITDUMP($"Reached split limit ({splitLimit}) -- stopping\n");
                    return;
                }

                var numTrees = 0;

                foreach (var tree in stmt.TreeList)
                {
                    if (tree.Oper is GT_JTRUE) // Preserve the relop invariant.
                    {
                        continue;
                    }

                    numTrees++;
                }

                var splitTree = rng.Next(numTrees);

                foreach (var tree in stmt.TreeList)
                {
                    if (tree.Oper is GT_JTRUE)
                    {
                        continue;
                    }

                    if (splitTree == 0)
                    {
                        JITDUMP($"Splitting STMT{stmt.Id:D5} at [{tree.TreeId:D6}]\n");
                        _ = gtSplitTree(block, stmt, tree, out var newStmt, out var split);

                        if (split)
                        {
                            while ((newStmt is not null) && (newStmt != stmt))
                            {
                                fgMorphStmtBlockOps(block, newStmt);
                                newStmt = newStmt.NextStmt;
                            }

                            fgMorphStmtBlockOps(block, stmt);
                            gtUpdateStmtSideEffects(stmt);
                            numSplit++;
                        }

                        break;
                    }

                    splitTree--;
                }

                if (lvaCount > maxLvaCount)
                {
                    JITDUMP($"Created too many locals (at {lvaCount}) -- stopping\n");
                    return;
                }
            }
        }

        JITDUMP($"Split {numSplit} trees\n");
#endif
    }

    private void SplitTreesRemoveCommas()
    {
        // Splitting creates a lot of new locals. Limit how many we create here.
        var maxLvaCount = uint.Max(unchecked((uint)lvaCount * 2), 50000u);

        foreach (var block in Blocks)
        {
            var stmt = block.GetFirstNonPhiDef();

            while (stmt is not null)
            {
                var nextStmt = stmt.NextStmt;

                foreach (var tree in stmt.TreeList)
                {
                    if (tree.Oper is not GT_COMMA)
                    {
                        continue;
                    }

                    // A reversed comma would need to move into the next node in execution order.
                    assert(!tree.IsReverseOp);

#if DEBUG
                    JITDUMP($"Removing COMMA [{tree.TreeId:D6}]\n");
#endif
                    ref var use = ref gtSplitTree(block, stmt, tree, out var newStmt, out _);
                    GenTree? op1SideEffects = null;
                    gtExtractSideEffList(tree.AsOp().Op1, ref op1SideEffects);

                    if (op1SideEffects is not null)
                    {
                        var op1Stmt = fgNewStmtFromTree(op1SideEffects);
                        fgInsertStmtBefore(block, stmt, op1Stmt);
                        newStmt ??= op1Stmt;
                    }

                    use = tree.AsOp().Op2;

                    for (var cur = newStmt; (cur is not null) && (cur != stmt); cur = cur.NextStmt)
                    {
                        fgMorphStmtBlockOps(block, cur);
                    }

                    fgMorphStmtBlockOps(block, stmt);
                    gtUpdateStmtSideEffects(stmt);

                    if (lvaCount > maxLvaCount)
                    {
                        JITDUMP($"Created too many locals (at {lvaCount}) -- stopping\n");
                        return;
                    }

                    // Morphing can introduce commas, and the original statement may have more.
                    // Restart at the earliest newly introduced statement.
                    nextStmt = newStmt ?? stmt;
                    break;
                }

                stmt = nextStmt;
            }
        }

#if DEBUG
        foreach (var block in Blocks)
        {
            for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
            {
                foreach (var tree in stmt.TreeList)
                {
                    assert(tree.Oper is not GT_COMMA);
                }
            }
        }
#endif
    }
}
