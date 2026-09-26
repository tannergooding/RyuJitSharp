// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public bool fgMorphBlockStmt(BasicBlock block, Statement statement, bool allowFGChange = true,
        bool invalidateDFSTreeOnFGChange = true, string message = nameof(fgMorphBlockStmt))
    {
        fgRemoveRestOfBlock = false;
        compCurBB = block;
        compCurStmt = statement;

        var morphedTree = fgMorphTree(statement.RootNode);
        if (fgIsCommaThrow(morphedTree, forFolding: true))
        {
#if DEBUG
            if (verbose)
            {
                jitprintf("Folding a top-level fgIsCommaThrow stmt\n");
                jitprintf("Removing op2 as unreachable:\n");
                gtDispTree(morphedTree.AsOp().Op2);
                jitprintf("\n");
            }
#endif
            morphedTree = morphedTree.AsOp().Op1;
            noway_assert(morphedTree.Oper is GT_CALL);
        }

        if (fgIsThrow(morphedTree))
        {
#if DEBUG
            if (verbose)
            {
                jitprintf("We have a top-level fgIsThrow stmt\n");
                jitprintf("Removing the rest of block as unreachable:\n");
            }
#endif
            noway_assert((morphedTree.Flags & GTF_COLON_COND) == 0);
            fgRemoveRestOfBlock = true;
        }

        statement.RootNode = morphedTree;
        var removedStatement = fgCheckRemoveStmt(block, statement);
        if (allowFGChange && !removedStatement && (statement.NextStmt is null) && !fgRemoveRestOfBlock)
        {
            var foldResult = fgFoldConditional(block);
            if (invalidateDFSTreeOnFGChange && (foldResult is not FoldResult.FOLD_DID_NOTHING))
            {
                fgInvalidateDfsTree();
            }
            removedStatement = foldResult is FoldResult.FOLD_REMOVED_LAST_STMT;
        }

        if (!removedStatement)
        {
            gtSetStmtInfo(statement);
            if (fgNodeThreading is NodeThreading.AllTrees)
            {
                fgSetStmtSeq(statement);
            }
        }

#if DEBUG
        if (verbose)
        {
            jitprintf($"{message} {(removedStatement ? "removed" : "morphed")} tree:\n");
            gtDispTree(morphedTree);
            jitprintf("\n");
        }
#endif
        if (fgRemoveRestOfBlock)
        {
            for (var remaining = statement.NextStmt; remaining is not null;)
            {
                var next = remaining.NextStmt;
                fgRemoveStmt(block, remaining);
                remaining = next;
            }

            if (allowFGChange && ((block != fgFirstBB) || !fgFirstBB.HasFlag(BBF_INTERNAL)))
            {
                var wasThrow = block.Kind is BBJ_THROW;
                fgConvertBBToThrowBB(block);
                if (!wasThrow && invalidateDFSTreeOnFGChange)
                {
                    fgInvalidateDfsTree();
                }
            }

#if DEBUG
            if (verbose)
            {
                jitprintf($"\n{message} Block {FMT_BB(block.bbNum)} becomes a throw block.\n");
            }
#endif
            fgRemoveRestOfBlock = false;
        }

        return removedStatement;
    }
}
