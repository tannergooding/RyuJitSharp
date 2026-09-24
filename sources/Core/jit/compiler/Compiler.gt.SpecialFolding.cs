// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree gtFoldExprBinary(GenTreeOp tree)
    {
        assert(tree.Oper.IsBinary && !optValnumCSE_phase && opts.Tier0OptimizationEnabled);
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        if ((op1 is null) || (op2 is null) || tree.Oper.IsAtomic)
        {
            return tree;
        }

        if (op1.Oper.IsConst)
        {
            if (op2.Oper.IsConst)
            {
                return gtFoldExprBinaryConst(tree);
            }

            if (opts.OptimizationEnabled)
            {
                return gtFoldExprSpecial(tree);
            }
        }
        else if (op2.Oper.IsConst)
        {
            if (opts.OptimizationEnabled)
            {
                return gtFoldExprSpecial(tree);
            }
        }
        else if (tree.Oper.IsCompare)
        {
            return gtFoldExprCompare(tree);
        }

        return tree;
    }

    public GenTree gtFoldExprCompare(GenTree tree)
    {
        assert(tree.Oper.IsCompare);
        var op1 = tree.AsOp().Op1;
        var op2 = tree.AsOp().Op2;
        if (varTypeIsFloating(op1.Type))
        {
            return tree;
        }

        if (((tree.Flags & GTF_SIDE_EFFECT) != 0) || !GenTree.Compare(op1, op2, swapOk: true))
        {
            return tree;
        }

        if ((tree.Flags & GTF_ORDER_SIDEEFF) != 0)
        {
            // A potentially volatile first operand alone can still fold, matching native ordering rules.
            if (((op1.Flags & GTF_ORDER_SIDEEFF) == 0) || ((op2.Flags & GTF_ORDER_SIDEEFF) != 0))
            {
                return tree;
            }
        }

        GenTree constant;
        switch (tree.Oper)
        {
            case GT_EQ:
            case GT_LE:
            case GT_GE:
            {
                constant = gtNewIconNode(TYP_INT, 1);
                break;
            }

            case GT_NE:
            case GT_LT:
            case GT_GT:
            {
                constant = gtNewIconNode(TYP_INT, 0);
                break;
            }

            default:
            {
                assert(false);
                return tree;
            }
        }

        JITDUMP("\nFolding comparison with identical operands:\n");
        DISPTREE(tree);
        if (fgGlobalMorph)
        {
            fgMorphTreeDone(constant);
        }
        else
        {
            constant.Next = tree.Next;
            constant.Prev = tree.Prev;
        }

        JITDUMP($"Bashed to {(constant.AsIntConCommon().IconValue != 0 ? "true" : "false")}:\n");
        DISPTREE(constant);
        return constant;
    }

    public GenTree gtFoldExprConditional(GenTree tree)
    {
        assert(tree.Oper.IsConditional);
        var conditional = tree.AsConditional();
        var condition = conditional.Cond;
        var op1 = conditional.Op1;
        var op2 = conditional.Op2;
        if (condition.Oper.IsConst)
        {
            JITDUMP("\nFolding conditional op with constant condition:\n");
            DISPTREE(tree);
            assert(condition.Type == TYP_INT);
            GenTree replacement;
            if (condition.IsIntegralConst(0))
            {
                JITDUMP("Bashed to false path:\n");
                replacement = op2;
            }
            else
            {
                assert(condition.IsIntegralConst(1));
                JITDUMP("Bashed to true path:\n");
                replacement = op1;
            }

            if (fgGlobalMorph)
            {
                fgMorphTreeDone(replacement);
            }
            else
            {
                replacement.Next = tree.Next;
                replacement.Prev = tree.Prev;
            }

            DISPTREE(replacement);
            JITDUMP("\n");
            return replacement.Oper.IsCompare ? gtFoldExprCompare(replacement) : replacement;
        }

        assert(condition.Oper.IsCompare);
        if (((tree.Flags & GTF_SIDE_EFFECT) != 0) || !GenTree.Compare(op1, op2, swapOk: true))
        {
            return tree;
        }

        if ((tree.Flags & GTF_ORDER_SIDEEFF) != 0)
        {
            if (((op1.Flags & GTF_ORDER_SIDEEFF) == 0) || ((op2.Flags & GTF_ORDER_SIDEEFF) != 0))
            {
                return tree;
            }
        }

        JITDUMP("Bashed to first of two identical paths:\n");
        if (fgGlobalMorph)
        {
            fgMorphTreeDone(op1);
        }
        else
        {
            op1.Next = tree.Next;
            op1.Prev = tree.Prev;
        }

        DISPTREE(op1);
        JITDUMP("\n");
        return op1;
    }

    public GenTree gtFoldBoxNullable(GenTree tree)
    {
        assert(tree.Oper.IsBinary && (tree.Oper is GT_GT or GT_EQ or GT_NE));
        var oper = tree.Oper;
        if ((oper == GT_GT) && ((tree.Flags & GTF_UNSIGNED) == 0))
        {
            return tree;
        }

        var binary = tree.AsOp();
        GenTree operand;
        GenTree constant;
        if (binary.Op1.Oper.IsCnsIntOrI)
        {
            operand = binary.Op2;
            constant = binary.Op1;
        }
        else if (binary.Op2.Oper.IsCnsIntOrI)
        {
            operand = binary.Op1;
            constant = binary.Op2;
        }
        else
        {
            return tree;
        }

        if ((constant.AsIntConCommon().IconValue != 0) || (operand.Oper != GT_CALL))
        {
            return tree;
        }

        var call = operand.AsCall();
        if (!call.IsHelperCall(CORINFO_HELP_BOX_NULLABLE) || call.Args.AreArgsComplete)
        {
            return tree;
        }

#if DEBUG
        JITDUMP($"\nReplacing BOX_NULLABLE(&x) {oper.Name} null [{tree.TreeId:D6}] with x.hasValue\n");
#endif
        // Nullable<T>.hasValue is at offset zero.
        var source = call.Args.GetArgByIndex(1);
        assert(source is not null);
        var hasValue = gtNewIndir(TYP_UBYTE, source.Node);
        if (operand == binary.Op1)
        {
            binary.Op1 = hasValue;
        }
        else
        {
            binary.Op2 = hasValue;
        }

        constant.Type = TYP_INT;
        return tree;
    }

    public GenTree gtFoldExprSpecial(GenTree tree)
    {
        assert(tree.Oper.IsBinary);
        var type = tree.Type;
        var op1 = tree.AsOp().Op1;
        var op2 = tree.AsOp().Op2;
        var oper = tree.Oper;
        if (oper is GT_CAST or GT_COMMA)
        {
            return tree;
        }

        if (varTypeIsFloating(op1.Type))
        {
            return gtFoldExprSpecialFloating(tree.AsOp());
        }

        if ((oper != GT_QMARK) && !varTypeIsIntOrI(type))
        {
            return tree;
        }

        GenTree op;
        GenTree constant;
        if (op1.Oper.IsCnsIntOrI)
        {
            op = op2;
            constant = op1;
        }
        else if (op2.Oper.IsCnsIntOrI)
        {
            op = op1;
            constant = op2;
        }
        else
        {
            return tree;
        }

        var value = constant.AsIntConCommon().IconValue;
        var unsigned = (tree.Flags & GTF_UNSIGNED) != 0;

        GenTreeIntCon NewMorphedIntConNode(int result)
        {
            var icon = gtNewIconNode(TYP_INT, result);
            icon.SetMorphed(this);
            return icon;
        }

        GenTree NewZeroExtendNode(var_types resultType, GenTree operand, var_types castToType)
        {
            assert(varTypeIsIntegral(resultType) && !varTypeIsSmall(resultType) && !varTypeIsUnsigned(resultType));
            assert(varTypeIsUnsigned(castToType));
            var cast = gtNewCastNode(TYP_INT, operand, false, castToType);
            cast.SetMorphed(this);
            fgMorphTreeDone(cast);
            if (resultType == TYP_LONG)
            {
                cast = gtNewCastNode(TYP_LONG, cast, true, TYP_LONG);
                cast.SetMorphed(this);
                fgMorphTreeDone(cast);
            }

            return cast;
        }

        // Some phases require JTRUE's operand to remain a relop, rather than a side-effect COMMA.
        if (((op.Flags & GTF_SIDE_EFFECT) != 0) && oper.IsCompare && ((tree.Flags & GTF_RELOP_JMP_USED) != 0))
        {
            return tree;
        }

        switch (oper)
        {
            case GT_LE:
            {
                if (unsigned && (value == 0) && (op1 == constant))
                {
                    op = gtWrapWithSideEffects(NewMorphedIntConNode(1), op, GTF_ALL_EFFECT);
                    goto DONE_FOLD;
                }

                break;
            }

            case GT_GE:
            {
                if (unsigned && (value == 0) && (op2 == constant))
                {
                    op = gtWrapWithSideEffects(NewMorphedIntConNode(1), op, GTF_ALL_EFFECT);
                    goto DONE_FOLD;
                }

                break;
            }

            case GT_LT:
            {
                if (unsigned && (value == 0) && (op2 == constant))
                {
                    op = gtWrapWithSideEffects(NewMorphedIntConNode(0), op, GTF_ALL_EFFECT);
                    goto DONE_FOLD;
                }

                break;
            }

            case GT_GT:
            case GT_EQ:
            case GT_NE:
            {
                if ((oper == GT_GT) && unsigned && (value == 0) && (op1 == constant))
                {
                    op = gtWrapWithSideEffects(NewMorphedIntConNode(0), op, GTF_ALL_EFFECT);
                    goto DONE_FOLD;
                }

                if ((value == 0) && !fgAddrCouldBeNull(op))
                {
#if DEBUG
                    JITDUMP($"\nAttempting to optimize BOX(valueType)/non-null {oper.Name} null [{tree.TreeId:D6}]\n");
#endif
                    if ((oper == GT_GT) && !unsigned)
                    {
                        JITDUMP(" bailing; unexpected signed compare via GT_GT\n");
                    }
                    else
                    {
                        var wrapEffects = true;
                        if ((op.Oper == GT_BOX) && op.AsBox().IsBoxedValue)
                        {
                            assert(!gtTreeHasSideEffects(op.AsBox().BoxOp, GTF_SIDE_EFFECT));
                            wrapEffects = gtTryRemoveBoxUpstreamEffects(op.AsBox()) is null;
                        }

                        var compareResult = oper == GT_GT ? (op1 == op ? 1 : 0) : (oper == GT_EQ ? 0 : 1);
                        GenTree replacement = NewMorphedIntConNode(compareResult);
                        if (wrapEffects)
                        {
                            replacement = gtWrapWithSideEffects(replacement, op, GTF_ALL_EFFECT);
                        }

                        op = replacement;
                        goto DONE_FOLD;
                    }
                }
                else
                {
                    return gtFoldBoxNullable(tree);
                }

                break;
            }

            case GT_ADD:
            case GT_OR:
            {
                if (value == 0)
                {
                    goto DONE_FOLD;
                }

                break;
            }

            case GT_MUL:
            {
                if (value == 1)
                {
                    goto DONE_FOLD;
                }

                if (value == 0)
                {
                    op = gtWrapWithSideEffects(constant, op, GTF_ALL_EFFECT);
                    goto DONE_FOLD;
                }

                break;
            }

            case GT_DIV:
            case GT_UDIV:
            {
                if ((op2 == constant) && (value == 1) && !op1.Oper.IsConst)
                {
                    goto DONE_FOLD;
                }

                break;
            }

            case GT_SUB:
            {
                if ((op2 == constant) && (value == 0) && !op1.Oper.IsConst)
                {
                    goto DONE_FOLD;
                }

                break;
            }

            case GT_AND:
            {
                if (value == 0)
                {
                    op = gtWrapWithSideEffects(constant, op, GTF_ALL_EFFECT);
                    goto DONE_FOLD;
                }

                if (value == 0xFF)
                {
                    op = NewZeroExtendNode(type, op, TYP_UBYTE);
                    goto DONE_FOLD;
                }

                if (value == 0xFFFF)
                {
                    op = NewZeroExtendNode(type, op, TYP_USHORT);
                    goto DONE_FOLD;
                }

                if ((value == 0xFFFFFFFF) && varTypeIsLong(tree.Type))
                {
                    op = NewZeroExtendNode(type, op, TYP_UINT);
                    goto DONE_FOLD;
                }

                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROL:
            case GT_ROR:
            {
                if (value == 0)
                {
                    if (op2 != constant)
                    {
                        op = gtWrapWithSideEffects(constant, op, GTF_ALL_EFFECT);
                    }

                    goto DONE_FOLD;
                }

                break;
            }

            case GT_QMARK:
            {
                assert((op1 == constant) && (op2 == op) && (op2.Oper == GT_COLON));
                assert(value is 0 or 1);
                op = value != 0 ? op2.AsColon().ThenNode : op2.AsColon().ElseNode;
                if ((tree.Flags & GTF_COLON_COND) == 0)
                {
                    var visitor = new ClearColonCondVisitor();
                    _ = visitor.WalkTree(ref op, null);
                }

                goto DONE_FOLD;
            }
        }

        return tree;

    DONE_FOLD:
        JITDUMP("\nFolding binary operator with a constant operand:\n");
        DISPTREE(tree);
        JITDUMP("Transformed into:\n");
        DISPTREE(op);
        op.SetMorphed(this);
        return op;
    }
}
