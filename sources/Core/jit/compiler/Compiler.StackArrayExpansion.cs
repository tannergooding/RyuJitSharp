// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public PhaseStatus fgExpandStackArrayAllocations()
    {
        if (!MethodHasStackAllocatedArray)
        {
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var modified = false;
        foreach (var block in Blocks)
        {
            foreach (var stmt in block.Statements)
            {
                // Expansion splits the tree and leaves its remainder in stmt.
                // Rescan that remainder to handle multiple allocations in one statement.
                var expanded = true;
                while (expanded)
                {
                    expanded = false;
                    if ((stmt.RootNode.Flags & GTF_CALL) == 0)
                    {
                        break;
                    }

                    foreach (var tree in stmt.TreeList)
                    {
                        if ((tree.Oper is GT_CALL) && fgExpandStackArrayAllocation(block, stmt, tree.AsCall()))
                        {
                            modified = true;
                            expanded = true;
                            break;
                        }
                    }
                }
            }
        }

        // Allocation sites may have become unreachable or been removed.
        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private bool fgExpandStackArrayAllocation(BasicBlock block, Statement stmt, GenTreeCall call)
    {
        if (!call.IsHelperCall())
        {
            return false;
        }

        int lengthArgIndex;
        int typeArgIndex;
        switch (call.HelperNum)
        {
            case CORINFO_HELP_NEWARR_1_DIRECT:
            case CORINFO_HELP_NEWARR_1_VC:
            case CORINFO_HELP_NEWARR_1_PTR:
            case CORINFO_HELP_NEWARR_1_ALIGN8:
            {
                lengthArgIndex = 1;
                typeArgIndex = 0;
                break;
            }

            default:
            {
                return false;
            }
        }

        var stackLocalAddressArg = call.Args.FindWellKnownArg(WellKnownArg.StackArrayLocal);
        if (stackLocalAddressArg is null)
        {
            return false;
        }

#if DEBUG
        JITDUMP($"Expanding new array helper for stack allocated array at [{call.TreeId:D6}] in {FMT_BB(block.bbNum)}:\n");
        DISPTREE(call);
        JITDUMP("\n");
#endif

        ref var callUse = ref gtSplitTree(block, stmt, call, out var newStmt, out var split);
        if (split)
        {
            while ((newStmt is not null) && (newStmt != stmt))
            {
                fgMorphStmtBlockOps(block, newStmt);
                newStmt = newStmt.NextStmt;
            }
        }

        var stackLocalAddress = stackLocalAddressArg.Node;
        var typeArg = call.Args.GetUserArgByIndex(typeArgIndex);
        assert(typeArg is not null);
        var mtStore = gtNewStoreValueNode(TYP_I_IMPL, stackLocalAddress, typeArg.Node);
        var mtStmt = fgNewStmtFromTree(mtStore);
        fgInsertStmtBefore(block, stmt, mtStmt);

        var lengthArg = call.Args.GetUserArgByIndex(lengthArgIndex);
        assert(lengthArg is not null);
        var lengthArgInt = fgOptimizeCast(gtNewCastNode(TYP_INT, lengthArg.Node, false, TYP_INT));
        var lengthAddress = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, gtCloneExpr(stackLocalAddress),
            gtNewIconNode(TYP_I_IMPL, OFFSETOF__CORINFO_Array__length));
        var lengthStore = gtNewStoreValueNode(TYP_INT, lengthAddress, lengthArgInt);
        var lenStmt = fgNewStmtFromTree(lengthStore);
        fgInsertStmtBefore(block, stmt, lenStmt);

        callUse = gtCloneExpr(stackLocalAddress);
        DEBUG_DESTROY_NODE(call);
        fgMorphStmtBlockOps(block, stmt);
        gtUpdateStmtSideEffects(stmt);

        return true;
    }

    public void fgMorphStmtBlockOps(BasicBlock block, Statement stmt)
    {
        compCurBB = block;
        compCurStmt = stmt;
        var visitor = new MorphStatementBlockOpsVisitor(this);
        _ = visitor.WalkTree(ref stmt.RootNodeRef, null);
        gtSetStmtInfo(stmt);

        if (fgNodeThreading is NodeThreading.AllTrees)
        {
            fgSetStmtSeq(stmt);
        }
    }

    private struct MorphStatementBlockOpsVisitor(Compiler compiler) : IGenTreeVisitor<MorphStatementBlockOpsVisitor>
    {
        private readonly GenTreeStack _ancestors = [];

        public static bool DoPostOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use.IsBlkOp)
            {
                use = use.IsInitBlkOp ? compiler.fgMorphInitBlock(use) : compiler.fgMorphCopyBlock(use);
            }

            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<MorphStatementBlockOpsVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}
