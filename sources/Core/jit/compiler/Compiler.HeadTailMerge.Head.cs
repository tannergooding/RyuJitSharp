// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgopt.cpp.

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool fgTryOneHeadMergeCore(BasicBlock block, bool early)
    {
        if ((block.Kind is not BBJ_COND) || (block.TrueEdge == block.FalseEdge))
        {
            return false;
        }

        bool GetCandidate(BasicBlock successor, out Statement? first)
        {
            first = null;
            if ((successor.GetUniquePred(this) != block) ||
                !BasicBlock.sameEHRegion(block, successor))
            {
                return false;
            }

            foreach (var stmt in successor.Statements)
            {
                if (stmt.RootNode.Oper is not GT_NOP)
                {
                    first = stmt;
                    break;
                }
            }

            return (first is not null) &&
                ((first != successor.LastStmt) || !successor.HasTerminator);
        }

        if (!GetCandidate(block.FalseTarget, out var next) ||
            !GetCandidate(block.TrueTarget, out var destination))
        {
            return false;
        }

        assert(next is not null);
        assert(destination is not null);
        if (!GenTree.Compare(next.RootNode, destination.RootNode))
        {
            return false;
        }

        JITDUMP($"Both succs of {FMT_BB(block.bbNum)} start with the same tree\n");
#if DEBUG
        if (verbose)
        {
            gtDispStmt(next);
        }
#endif
        if (gtTreeContainsTailCall(next.RootNode) || gtTreeContainsTailCall(destination.RootNode))
        {
            JITDUMP("But one is a tailcall\n");
            return false;
        }

        JITDUMP("Checking if we can move it into the predecessor...\n");
        if (!fgCanMoveFirstStatementIntoPredCore(early, next, block))
        {
            return false;
        }

        JITDUMP("We can; moving statement\n");
        fgUnlinkStmt(block.FalseTarget, next);
        fgInsertStmtNearEnd(block, next);
        fgUnlinkStmt(block.TrueTarget, destination);
        block.CopyFlags(block.FalseTarget, BBF_COPY_PROPAGATE);
        return true;
    }

    private bool fgHeadMergeCore(BasicBlock block, bool early)
    {
        var changed = false;
        var count = 0;
        while (fgTryOneHeadMergeCore(block, early))
        {
            changed = true;
            count++;
        }

        if (count > 0)
        {
            JITDUMP($"Did {count} head merges in {FMT_BB(block.bbNum)}\n");
        }

        return changed;
    }

    private bool fgCanMoveFirstStatementIntoPredCore(bool early, Statement firstStmt, BasicBlock pred)
    {
        if (!pred.HasTerminator)
        {
            return true;
        }

        var terminator = pred.LastStmt!.RootNode;
        var tree = firstStmt.RootNode;
        var terminatorFlags = terminator.Flags;
        var treeFlags = tree.Flags;

        if (early)
        {
            if (gtHasLocalsWithAddrOp(terminator))
            {
                terminatorFlags |= GTF_GLOB_REF;
            }

            if (gtHasLocalsWithAddrOp(tree))
            {
                treeFlags |= GTF_GLOB_REF;
            }
        }

        if ((terminatorFlags & GTF_ASG) != 0)
        {
            JITDUMP("  no; terminator contains embedded store\n");
            return false;
        }

        if ((treeFlags & GTF_ASG) != 0)
        {
            if (!tree.Oper.IsLocalStore)
            {
                JITDUMP("  cannot reorder with GTF_ASG without top-level store");
                return false;
            }

            var store = tree.AsLclVarCommon();
            if ((store.Data.Flags & GTF_ASG) != 0)
            {
                JITDUMP("  cannot reorder with embedded store");
                return false;
            }

            var descriptor = lvaGetDesc(store.LclNum);
            if ((terminatorFlags & GTF_ALL_EFFECT) != 0)
            {
                if (early ? descriptor.lvHasLdAddrOp : descriptor.IsAddressExposed)
                {
                    JITDUMP("  cannot reorder store to exposed local with any side effect\n");
                    return false;
                }

                if (((terminatorFlags & (GTF_CALL | GTF_EXCEPT)) != 0) && pred.HasPotentialEHSuccs(this))
                {
                    JITDUMP("  cannot reorder store with exception throwing tree and potential EH successor\n");
                    return false;
                }
            }

            if (gtHasRef(terminator, store.LclNum))
            {
                JITDUMP("  cannot reorder with interfering use\n");
                return false;
            }

            if (descriptor.lvIsStructField && gtHasRef(terminator, descriptor.lvParentLcl))
            {
                JITDUMP("  cannot reorder with interfering use of parent struct local\n");
                return false;
            }

            if (descriptor.lvPromoted)
            {
                for (var i = 0; i < descriptor.lvFieldCnt; i++)
                {
                    if (gtHasRef(terminator, descriptor.lvFieldLclStart + i))
                    {
                        JITDUMP("  cannot reorder with interfering use of struct field\n");
                        return false;
                    }
                }
            }

            treeFlags &= ~GTF_ASG;
        }

        if (((terminatorFlags & GTF_CALL) != 0) && ((treeFlags & GTF_ALL_EFFECT) != 0))
        {
            JITDUMP("  cannot reorder call with any side effect\n");
            return false;
        }

        if (((terminatorFlags & GTF_GLOB_REF) != 0) && ((treeFlags & GTF_PERSISTENT_SIDE_EFFECTS) != 0))
        {
            JITDUMP("  cannot reorder global reference with persistent side effects\n");
            return false;
        }

        if (((terminatorFlags & GTF_ORDER_SIDEEFF) != 0) &&
            ((treeFlags & (GTF_GLOB_REF | GTF_ORDER_SIDEEFF)) != 0))
        {
            JITDUMP("  cannot reorder ordering side effect\n");
            return false;
        }

        if (((treeFlags & GTF_ORDER_SIDEEFF) != 0) &&
            ((terminatorFlags & (GTF_GLOB_REF | GTF_ORDER_SIDEEFF)) != 0))
        {
            JITDUMP("  cannot reorder ordering side effect\n");
            return false;
        }

        if (((terminatorFlags & GTF_EXCEPT) != 0) && ((treeFlags & GTF_SIDE_EFFECT) != 0))
        {
            JITDUMP("  cannot reorder exception with side effect\n");
            return false;
        }

        return true;
    }
}
