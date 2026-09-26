// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, redundantbranchopts.cpp, flowgraph.cpp and block.cpp.

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private enum ReachabilityResult
    {
        BudgetExceeded,
        Unreachable,
        Reachable,
    }

    private bool optReachable(BasicBlock fromBlock, BasicBlock toBlock, BasicBlock? excludedBlock)
    {
        var result = optReachableWithBudget(fromBlock, toBlock, excludedBlock, null);
        assert(result is not ReachabilityResult.BudgetExceeded);
        return result is ReachabilityResult.Reachable;
    }

    private ReachabilityResult optReachableWithBudget(
        BasicBlock? fromBlock, BasicBlock toBlock, BasicBlock? excludedBlock, int[]? budget)
    {
        if (fromBlock == toBlock)
        {
            return ReachabilityResult.Reachable;
        }

        if (optReachableBitVecTraits is null)
        {
            optReachableBitVecTraits = new BitVecTraits(this, fgBBNumMax + 1);
            optReachableBitVec = BitVecOps.MakeEmpty(optReachableBitVecTraits);
        }
        else
        {
            assert(optReachableBitVec is not null);
            assert(BitVecTraits.GetSize(optReachableBitVecTraits) == fgBBNumMax + 1);
            BitVecOps.ClearD(optReachableBitVecTraits, optReachableBitVec);
        }

        var stack = new Stack<BasicBlock?>();
        stack.Push(fromBlock);

        while (stack.Count != 0)
        {
            var nextBlock = stack.Pop();
            assert(nextBlock != toBlock);
            if (nextBlock == excludedBlock)
            {
                continue;
            }

            assert(nextBlock is not null);
            var budgetExceeded = false;
            var visit = nextBlock.VisitAllSuccs(this, successor => {
                if (successor == toBlock)
                {
                    return BasicBlockVisit.Abort;
                }

                if ((budget is not null) && (--budget[0] <= 0))
                {
                    budgetExceeded = true;
                    return BasicBlockVisit.Abort;
                }

                if (BitVecOps.TryAddElemD(optReachableBitVecTraits, optReachableBitVec,
                    successor.bbNum))
                {
                    stack.Push(successor);
                }

                return BasicBlockVisit.Continue;
            });

            if (visit is BasicBlockVisit.Abort)
            {
                return budgetExceeded ? ReachabilityResult.BudgetExceeded : ReachabilityResult.Reachable;
            }
        }

        return ReachabilityResult.Unreachable;
    }

    private BasicBlock? fgGetDomSpeculatively(BasicBlock block)
    {
        assert(_domTree is not null);
        BasicBlock? lastReachablePred = null;

        for (var edge = block.bbPreds; edge is not null; edge = edge.NextPredEdge)
        {
            var predecessor = edge.SourceBlock;
            if (predecessor == block)
            {
                continue;
            }

            if (predecessor.CountOfInEdges > 0)
            {
                if (lastReachablePred is not null)
                {
                    return block.bbIDom;
                }

                lastReachablePred = predecessor;
            }
            else if (predecessor == block.bbIDom)
            {
                return null;
            }
        }

        return lastReachablePred ?? block.bbIDom;
    }

    private static bool optRboBlockHasSideEffects(BasicBlock block)
    {
        if (block.IsLIR)
        {
            foreach (var node in block)
            {
                if ((node.Flags & GTF_SIDE_EFFECT) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var statement in block.Statements)
        {
            if (statement.IsPhiDefnStmt)
            {
                continue;
            }

            if ((statement.RootNode.Flags & GTF_SIDE_EFFECT) != 0)
            {
                return true;
            }
        }

        return false;
    }
}
