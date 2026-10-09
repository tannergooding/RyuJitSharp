// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, promotion.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    private PhaseStatus PhysicalPromotionRunImplementation()
    {
        var aggregates = PhysicalPromotionSelectCandidates();
        if (aggregates is null)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        assert(fgFirstBB is not null);
        assert(fgFirstBB.bbPreds is null);
        var liveness = new PhysicalPromotionLiveness(this, aggregates);
        liveness.Run();

        JITDUMP("Making replacements\n\n");
        var dfsTree = _dfsTree ??= fgComputeDfs();
        var replacer = new PhysicalPromotionReplaceVisitor(this, aggregates, liveness, dfsTree);
        replacer.PrepareReadBacks();
        for (var index = dfsTree.PostOrderCount; index > 0; index--)
        {
            var block = dfsTree.GetPostOrder(index - 1);
            var first = replacer.StartBlock(block);
#if DEBUG
            JITDUMP("\nReplacing in ");
            if (verbose)
            {
                block.dspBlockHeader();
            }
            JITDUMP("\n");
#endif
            foreach (var statement in new StatementList(first))
            {
                replacer.StartStatement(statement);
                DISPSTMT(statement);
                replacer.WalkTree(ref statement.RootNodeRef);
                if (replacer.MadeChanges)
                {
                    fgSequenceLocals(statement);
                    gtUpdateStmtSideEffects(statement);
                    JITDUMP("New statement:\n");
                    DISPSTMT(statement);
                }

                if (replacer.MayHaveForwardSubOpportunity)
                {
                    JITDUMP("Invoking forward sub due to a potential opportunity\n");
                    while ((statement != block.FirstStmt) &&
                        fgForwardSubStatement(statement.PrevStmt!))
                    {
                        fgRemoveStmt(block, statement.PrevStmt!);
                    }
                }
            }

            replacer.EndBlock();
        }

        Statement? previous = null;
        foreach (var aggregate in aggregates.Aggregates)
        {
            if (lvaGetDesc(aggregate.LclNum).lvSuppressedZeroInit)
            {
                // Promotion can invalidate the assumption that prolog zeroing covers the entire local.
                PhysicalPromotionExplicitlyZeroInitReplacementLocals(
                    aggregate.LclNum, aggregate.Replacements, ref previous);
            }
        }

        return PhaseStatus.MODIFIED_EVERYTHING;
    }
}
