// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, helperexpansion.cpp and fgbasic.cpp.

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    private delegate bool HelperExpansionFunction(ref BasicBlock block, Statement statement, GenTreeCall call);

    private PhaseStatus fgExpandHelper(HelperExpansionFunction expansionFunction, bool skipRarelyRunBlocks = false)
    {
        var result = PhaseStatus.MODIFIED_NOTHING;
        for (var block = fgFirstBB; block is not null; block = block.Next)
        {
            if (skipRarelyRunBlocks && block.isRunRarely)
            {
                continue;
            }

#if DEBUG
            var originalBlock = block;
#endif
            while (fgExpandHelperForBlock(ref block, expansionFunction))
            {
                result = PhaseStatus.MODIFIED_EVERYTHING;
#if DEBUG
                assert(originalBlock != block);
                originalBlock = block;
#endif
            }
        }

        if (result is PhaseStatus.MODIFIED_EVERYTHING)
        {
            fgInvalidateDfsTree();
        }

        return result;
    }

    private static bool fgExpandHelperForBlock(ref BasicBlock block, HelperExpansionFunction expansionFunction)
    {
        for (var stmt = block.GetFirstNonPhiDef(); stmt is not null; stmt = stmt.NextStmt)
        {
            if ((stmt.RootNode.Flags & GTF_CALL) == 0)
            {
                continue;
            }

            foreach (var tree in stmt.TreeList)
            {
                if ((tree is GenTreeCall call) && expansionFunction(ref block, stmt, call))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private ref GenTree? fgSplitBlockBeforeTree(BasicBlock block, Statement stmt, GenTree tree,
        out Statement? firstNewStmt, out BasicBlock bottomBlock)
    {
        ref var use = ref gtSplitTree(block, stmt, tree, out firstNewStmt, out _);
        var originalFlags = block.FlagsRaw;
        var previous = block;

        if (stmt == block.FirstStmt)
        {
            block = fgSplitBlockAtBeginning(previous);
        }
        else
        {
            var before = stmt.PrevStmt ?? throw new FatalJitException("A non-first statement needs a predecessor.");
            assert(before != block.LastStmt);
#if DEBUG
            JITDUMP($"Splitting {FMT_BB(previous.bbNum)} after statement {FMT_STMT(before.Id)}\n");
#endif
            block = fgSplitBlockAfterStatement(previous, before);
        }

        previous.FlagsRaw = originalFlags & (~(BBF_SPLIT_LOST | BBF_RETLESS_CALL) | BBF_GC_SAFE_POINT);
        block.SetFlags(originalFlags & (BBF_SPLIT_GAINED | BBF_IMPORTED | BBF_GC_SAFE_POINT | BBF_RETLESS_CALL));
        assert(previous.Kind is BBJ_ALWAYS && previous.JumpsToNext && previous.Next == block);

        bottomBlock = block;
        return ref use;
    }

    private int SplitAtTreeAndReplaceItWithLocal(BasicBlock block, Statement stmt, GenTree tree,
        out BasicBlock topBlock, out BasicBlock bottomBlock)
    {
        var previous = block;
        ref var use = ref fgSplitBlockBeforeTree(block, stmt, tree, out var firstNewStmt, out block);

        // Splitting can insert block operations after morph. Defer morphing stmt
        // until its owning use is replaced, since morphing can invalidate that use.
        while ((firstNewStmt is not null) && (firstNewStmt != stmt))
        {
            fgMorphStmtBlockOps(block, firstNewStmt);
            firstNewStmt = firstNewStmt.NextStmt;
        }

        var temp = lvaGrabTemp(shortLifetime: true, "replacement local");
        lvaTable[temp].Type = tree.Type;
        use = gtNewLclvNode(tree.Type, temp);

        fgMorphStmtBlockOps(block, stmt);
        gtUpdateStmtSideEffects(stmt);
        topBlock = previous;
        bottomBlock = block;
        return temp;
    }

    private GenTreeLclVar SpillExpression(GenTree expression, BasicBlock block, in DebugInfo debugInfo)
    {
        var temp = lvaGrabTemp(shortLifetime: true, "spilling expr");
        var stmt = fgNewStmtFromTree(gtNewTempStore(temp, expression), di: debugInfo);
        fgInsertStmtAtEnd(block, stmt);
        gtSetStmtInfo(stmt);
        fgSetStmtSeq(stmt);
        return gtNewLclVarNode(lvaTable[temp].Type, temp);
    }

    private static void InheritFlags(BasicBlock destination, BasicBlock source)
    {
        if (!source.HasFlag(BBF_INTERNAL))
        {
            destination.RemoveFlags(BBF_INTERNAL);
            destination.SetFlags(BBF_IMPORTED);
        }
    }
}
