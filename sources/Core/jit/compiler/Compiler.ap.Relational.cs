// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, assertionprop.cpp.

using System;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.Compiler.optOp1Kind;
using static RyuJitSharp.Compiler.optOp2Kind;

namespace RyuJitSharp;

public partial class Compiler
{
    public void optAssertionProp_RangeProperties(ASSERT_TP? assertions, GenTree tree,
        Statement? statement, BasicBlock? block, out bool isKnownNonZero, out bool isKnownNonNegative)
    {
        isKnownNonZero = false;
        isKnownNonNegative = false;
        if (optLocalAssertionProp || !varTypeIsIntegral(tree.Type) ||
            BitVecOps.MaybeUninit(assertions))
        {
            return;
        }

        assert(apTraits is not null);
        if (BitVecOps.IsEmpty(apTraits, assertions))
        {
            return;
        }

        isKnownNonNegative = tree.IsNeverNegative(this);
        isKnownNonZero = tree.IsNeverZero();
        if (isKnownNonZero && isKnownNonNegative)
        {
            return;
        }

        assert(vnStore is not null);
        var treeVN = vnStore.VNNormalValue(tree._vnPair.Conservative);
        var nonZero = isKnownNonZero;
        var nonNegative = isKnownNonNegative;
        _ = BitVecOps.VisitBits(apTraits, assertions, bitIndex =>
        {
            var assertion = optGetAssertion(GetAssertionIndex((ushort)bitIndex));
            if (assertion.IsConstantInt32Assertion && (assertion.Op1.VN == treeVN))
            {
                if (assertion.KindIs(OAK_NOT_EQUAL) && (assertion.Op2.IntConstant == 0))
                {
                    nonZero = true;
                }
                else if (!assertion.KindIs(OAK_NOT_EQUAL))
                {
                    nonNegative = assertion.Op2.IntConstant >= 0;
                    nonZero = assertion.Op2.IntConstant != 0;
                }
            }

            if (assertion.IsRelop && assertion.Op2.KindIs(O2K_CONST_INT) &&
                (assertion.Op1.VN == treeVN) && (assertion.Op2.IntConstant >= 0))
            {
                if (assertion.KindIs(OAK_LT_UN, OAK_LE_UN))
                {
                    nonNegative = true;
                }
                else if (assertion.KindIs(OAK_GE, OAK_GT))
                {
                    nonNegative = true;
                    nonZero = assertion.KindIs(OAK_GT) || (assertion.Op2.IntConstant > 0);
                }
            }

            return true;
        });

        if (nonZero && nonNegative)
        {
            isKnownNonZero = nonZero;
            isKnownNonNegative = nonNegative;
            return;
        }

        assert(block is not null);
        var range = RangeCheck.GetRange(this, tree, block, assertions, fast: true);
        if (range.IsConstantRange())
        {
            nonNegative |= range.LowerLimit.Constant >= 0;
            nonZero |= (range.LowerLimit.Constant > 0) || (range.UpperLimit.Constant < 0);
        }

        isKnownNonZero = nonZero;
        isKnownNonNegative = nonNegative;
    }

    public GenTree? optAssertionProp_RelOp(ASSERT_TP? assertions, GenTree tree,
        Statement? statement, BasicBlock? block)
    {
        assert(tree.Oper.IsCompare);

        if (!optLocalAssertionProp)
        {
            ArgumentNullException.ThrowIfNull(assertions);
            ArgumentNullException.ThrowIfNull(statement);
            ArgumentNullException.ThrowIfNull(block);
            return optAssertionPropGlobal_RelOp(assertions, tree, statement, block);
        }

        if (tree.Oper is not (GT_EQ or GT_NE))
        {
            return null;
        }

        return optAssertionPropLocal_RelOp(assertions, tree, statement);
    }

    public GenTree? optAssertionPropGlobal_RelOp(ASSERT_TP assertions, GenTree tree,
        Statement statement, BasicBlock block)
    {
        assert(!optLocalAssertionProp);
        ArgumentNullException.ThrowIfNull(vnStore);
        assert(apTraits is not null);

        var result = tree;
        var op1 = tree.AsOp().Op1;
        var op2 = tree.AsOp().Op2;

        if (op2.IsIntegralConst(0) && tree.Oper.IsCmpCompare)
        {
            optAssertionProp_RangeProperties(assertions, op1, statement, block,
                out var isNonZero, out var isNeverNegative);

            if ((tree.Oper is GT_GE or GT_LT) && isNeverNegative)
            {
                result = tree.Oper is GT_GE ? gtNewTrue() : gtNewFalse();
            }
            else if ((tree.Oper is GT_GT or GT_LE) && isNeverNegative && isNonZero)
            {
                result = tree.Oper is GT_GT ? gtNewTrue() : gtNewFalse();
            }
            else if ((tree.Oper is GT_EQ or GT_NE) && isNonZero)
            {
                result = tree.Oper is GT_NE ? gtNewTrue() : gtNewFalse();
            }

            if (result != tree)
            {
                result = gtWrapWithSideEffects(result, tree, GTF_ALL_EFFECT);
                return optAssertionProp_Update(result, tree, statement);
            }
        }

        var relopVN = optConservativeNormalVN(tree);
        var op1VN = optConservativeNormalVN(op1);
        var op2VN = optConservativeNormalVN(op2);
        if (!BitVecOps.IsEmpty(apTraits, assertions))
        {
            var falseVN = vnStore.VNZeroForType(TYP_INT);
            _ = BitVecOps.VisitBits(apTraits, assertions, bitIndex =>
            {
                var index = GetAssertionIndex((ushort)bitIndex);
                var assertion = optGetAssertion(index);
                if (assertion.IsRelop && (assertion.Op1.VN == op1VN) &&
                    (!assertion.Op2.KindIs(O2K_VN_ADD_CNS) || (assertion.Op2.Cns == 0)) &&
                    (assertion.Op2.VN == op2VN))
                {
                    var assertionOper = AssertionDsc.ToCompareOper(assertion.Kind, out var isUnsigned);
                    if (((tree.Oper == assertionOper) || (tree.Oper == assertionOper.ReverseRelop)) &&
                        (tree.AsOp().IsUnsigned == isUnsigned))
                    {
                        result = gtNewIconNode(TYP_INT, tree.Oper == assertionOper ? 1 : 0);
                    }
                }
                else if (assertion.CanPropEqualOrNotEqual && (assertion.Op1.VN == relopVN) &&
                    (assertion.Op2.VN == falseVN))
                {
                    result = gtNewIconNode(TYP_INT, assertion.KindIs(OAK_EQUAL) ? 0 : 1);
                }

                if (result == tree)
                {
                    return true;
                }

#if DEBUG
                JITDUMP($"Found matching assertion #{index:D2} for tree {tree.TreeId:D6}.");
#endif
                result = gtWrapWithSideEffects(result, tree, GTF_ALL_EFFECT);
                JITDUMP(". Folded into:\n");
                DISPTREE(result);
                result = optAssertionProp_Update(result, tree, statement);
                return false;
            });

            if (result != tree)
            {
                return result;
            }
        }

        if (varTypeIsIntegral(op1.Type))
        {
            var relopRange = RangeCheck.GetRangeFromAssertions(this, tree, assertions);
            if (relopRange.IsConstantRange())
            {
                if (!relopRange.IsSingleValueConstant(out var relopResult))
                {
                    var relopApp = new VNFuncApp();
                    if (vnStore.IsVNRelop(relopVN, ref relopApp) &&
                        (((relopApp.GetArg(0) == op1VN) && (relopApp.GetArg(1) == op2VN)) ||
                         ((relopApp.GetArg(0) == op2VN) && (relopApp.GetArg(1) == op1VN))))
                    {
                        // Both VNs already came from these operands.
                    }
                    else
                    {
                        var op1Range = RangeCheck.GetRange(this, op1, block, assertions, fast: true);
                        var op2Range = RangeCheck.GetRange(this, op2, block, assertions, fast: true);
                        relopRange = RangeOps.EvalRelop(tree.Oper, tree.AsOp().IsUnsigned, op1Range, op2Range);
                    }
                }

                if (!relopRange.IsSingleValueConstant(out relopResult) &&
                    (op1.Type is TYP_INT) && op2.IsIntCnsFitsInI32 &&
                    (tree.Oper is GT_LE or GT_LT or GT_GE or GT_GT))
                {
                    var op1Range = RangeCheck.GetRange(this, op1, block, assertions, fast: false);
                    var op2Range = RangeCheck.GetRange(this, op2, block, assertions, fast: false);
                    relopRange = RangeOps.EvalRelop(tree.Oper, tree.AsOp().IsUnsigned, op1Range, op2Range);
                }

                if (relopRange.IsSingleValueConstant(out relopResult))
                {
                    assert(relopResult is 0 or 1);
                    result = gtWrapWithSideEffects(
                        relopResult == 1 ? gtNewTrue() : gtNewFalse(), tree, GTF_ALL_EFFECT);
                    return optAssertionProp_Update(result, tree, statement);
                }
            }
        }

        if (tree.Oper is not (GT_EQ or GT_NE))
        {
            return null;
        }

        if (op2.IsIntegralConst(0) && varTypeIsI(op1.Type) && optAssertionVNIsNonNull(op1VN, assertions))
        {
#if DEBUG
            JITDUMP($"Proved [{op1.TreeId:D6}] non-null\n");
#endif
            result = gtWrapWithSideEffects(tree.Oper is GT_EQ ? gtNewFalse() : gtNewTrue(), tree, GTF_ALL_EFFECT);
            return optAssertionProp_Update(result, tree, statement);
        }

        var op1ObjectVN = ValueNumStore.NoVN;
        var app = new VNFuncApp();
        if ((op1.Type is TYP_I_IMPL) && vnStore.GetVNFunc(op1VN, ref app) &&
            app.FuncIs(VNF_InvariantNonNullLoad))
        {
            op1ObjectVN = app.GetArg(0);
        }

        _ = BitVecOps.VisitBits(apTraits, assertions, bitIndex =>
        {
            var index = GetAssertionIndex((ushort)bitIndex);
            var assertion = optGetAssertion(index);
            if (!assertion.CanPropEqualOrNotEqual || (assertion.Op2.VN != op2VN))
            {
                return true;
            }

            if ((assertion.Op1.KindIs(O1K_VN) && (assertion.Op1.VN == op1VN)) ||
                (assertion.Op1.KindIs(O1K_EXACT_TYPE) && (assertion.Op1.VN == op1ObjectVN)))
            {
#if DEBUG
                JITDUMP($"Found matching assertion #{index:D2} for tree {tree.TreeId:D6}.");
#endif
                var value = assertion.KindIs(OAK_EQUAL) == (tree.Oper is GT_EQ);
                result = gtWrapWithSideEffects(value ? gtNewTrue() : gtNewFalse(), tree, GTF_ALL_EFFECT);
                JITDUMP(". Folded into:\n");
                DISPTREE(result);
                result = optAssertionProp_Update(result, tree, statement);
                return false;
            }

            return true;
        });

        return result != tree ? result : null;
    }
}
