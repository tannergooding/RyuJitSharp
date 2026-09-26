// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, assertionprop.cpp.

namespace RyuJitSharp;

public partial class Compiler
{
    private struct VNAssertionPropVisitor : IGenTreeVisitor<VNAssertionPropVisitor>
    {
        private readonly Compiler _compiler;
        private readonly BasicBlock _block;
        private readonly Statement _statement;
        private readonly GenTreeStack _ancestors;

        public static bool DoPostOrder => true;

        public static bool UseExecutionOrder => true;

        public VNAssertionPropVisitor(Compiler compiler, BasicBlock block, Statement statement)
        {
            _compiler = compiler;
            _block = block;
            _statement = statement;
            _ancestors = [];
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            _compiler.optVnNonNullPropCurStmt(_block, _statement, use);
            return _compiler.optVNBasedFoldCurStmt(_block, _statement, user, use);
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<VNAssertionPropVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    public fgWalkResult optVNBasedFoldCurStmt(BasicBlock block, Statement statement, GenTree? parent, GenTree tree)
    {
        if (!tree.CanCse || (tree.Type is TYP_STRUCT))
        {
            return WALK_CONTINUE;
        }

        switch (tree.Oper)
        {
            case GT_ADD:
            case GT_SUB:
            case GT_DIV:
            case GT_MOD:
            case GT_UDIV:
            case GT_UMOD:
            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            case GT_OR:
            case GT_XOR:
            case GT_AND:
            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_NOT:
            case GT_NEG:
            case GT_CAST:
            case GT_BITCAST:
            case GT_INTRINSIC:
#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
#endif
            case GT_ARR_LENGTH:
            {
                break;
            }

            case GT_BLK:
            case GT_IND:
            {
                assert(vnStore is not null);
                var vn = tree._vnPair.Conservative;
                if (vnStore.VNNormalValue(vn) != vn)
                {
                    return WALK_CONTINUE;
                }
                break;
            }

            case GT_JTRUE:
            {
                break;
            }

            case GT_MUL:
            {
                if ((tree.Flags & GTF_MUL_64RSLT) != 0)
                {
                    return WALK_CONTINUE;
                }
                break;
            }

            case GT_LCL_VAR:
            case GT_LCL_FLD:
            {
                if (lclNumIsCSE(tree.AsLclVarCommon().LclNum))
                {
                    return WALK_CONTINUE;
                }
                break;
            }

            case GT_CALL:
            {
                var call = tree.AsCall();
                if (!call.IsPure(this) && !call.IsSpecialIntrinsic())
                {
                    return WALK_CONTINUE;
                }
                break;
            }

            default:
            {
                return WALK_CONTINUE;
            }
        }

        var replacement = optVNBasedFoldExpr(block, parent, tree);
        if (replacement is null)
        {
            return WALK_CONTINUE;
        }

        optAssertionProp_Update(replacement, tree, statement);

#if DEBUG
        JITDUMP($"After VN-based fold of [{tree.TreeId:D6}]:\n");
        if (verbose)
        {
            gtDispStmt(statement);
        }
#endif
        return WALK_CONTINUE;
    }

    public void optVnNonNullPropCurStmt(BasicBlock block, Statement statement, GenTree tree)
    {
        var empty = BitVecOps.UninitVal();
        GenTree? replacement = null;
        if (tree.Oper is GT_CALL)
        {
            replacement = optNonNullAssertionProp_Call(empty, tree.AsCall());
        }
        else if (tree.Oper.IsIndir)
        {
            replacement = optAssertionProp_Ind(empty, tree, statement);
        }

        if (replacement is not null)
        {
            assert(replacement == tree);
            _ = optAssertionProp_Update(replacement, tree, statement);
        }
    }

    public Statement? optVNAssertionPropCurStmt(BasicBlock block, Statement statement)
    {
        if (block.CatchType is BBCT_FAULT)
        {
            return statement;
        }

        var previous = statement == block.FirstStmt ? null : statement.PrevStmt;
        optAssertionPropagatedCurrentStmt = false;

        var visitor = new VNAssertionPropVisitor(this, block, statement);
        _ = visitor.WalkTree(ref statement.RootNodeRef, null);

        if (optAssertionPropagatedCurrentStmt)
        {
            _ = fgMorphBlockStmt(block, statement, message: nameof(optVNAssertionPropCurStmt));
        }

        return previous is null ? block.FirstStmt : previous.NextStmt;
    }
}
