// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fginline.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgPostInlineNoReturnCleanup()
    {
        if (!doesMethodHaveNoReturnCalls())
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var modified = false;
        foreach (var block in Blocks)
        {
            if (block.Kind is BBJ_THROW)
            {
                continue;
            }

            Statement? trimStatement = null;
            GenTreeCall? noReturnCall = null;
            foreach (var statement in block.Statements)
            {
                if ((statement.RootNode.Flags & GTF_CALL) == 0)
                {
                    continue;
                }

                var finder = new NoReturnCallFinder();
                _ = finder.WalkTree(ref statement.RootNodeRef, null);
                if (finder.Result is GenTreeCall call)
                {
                    trimStatement = statement;
                    noReturnCall = call;
                    break;
                }
            }

            if (trimStatement is null)
            {
                continue;
            }

            assert(noReturnCall is not null);
            // Early splitting cannot walk qmarks, even when the no-return call is outside their conditional arms.
            if ((trimStatement.RootNode != noReturnCall) && gtTreeContainsOper(trimStatement.RootNode, GT_QMARK))
            {
#if DEBUG
                JITDUMP($"\nfgPostInlineNoReturnCleanup: {FMT_BB(block.bbNum)} statement with no-return call " +
                    $"[{noReturnCall.TreeId:D6}] contains a qmark; skipping\n");
#endif
                continue;
            }

#if DEBUG
            JITDUMP($"\nfgPostInlineNoReturnCleanup: {FMT_BB(block.bbNum)} contains no-return call " +
                $"[{noReturnCall.TreeId:D6}]; trimming\n");
#endif
            if (trimStatement.RootNode != noReturnCall)
            {
                _ = gtSplitTree(block, trimStatement, noReturnCall, out _, out _, early: true);
                trimStatement.RootNode = noReturnCall;
                gtUpdateStmtSideEffects(trimStatement);
            }

            while (block.LastStmt != trimStatement)
            {
                var lastStatement = block.LastStmt;
                assert(lastStatement is not null);
                fgRemoveStmt(block, lastStatement);
            }

            fgConvertBBToThrowBB(block);
            modified = true;
        }

        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private struct NoReturnCallFinder : IGenTreeVisitor<NoReturnCallFinder>
    {
        private readonly GenTreeStack _ancestors;

        public NoReturnCallFinder()
        {
            _ancestors = [];
        }

        public static bool DoPreOrder => true;

        public static bool UseExecutionOrder => true;

        public GenTreeCall? Result { get; private set; }

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use.Oper is GT_QMARK)
            {
                return WALK_SKIP_SUBTREES;
            }

            if ((use.Oper is GT_CALL) && use.AsCall().IsNoReturn)
            {
                Result = use.AsCall();
                return WALK_ABORT;
            }

            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<NoReturnCallFinder>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
