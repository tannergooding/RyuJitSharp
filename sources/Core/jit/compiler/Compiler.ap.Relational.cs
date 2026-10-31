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

    public bool optIsNeverNegativeCheckedBound(ValueNum vn, ASSERT_TP assertions)
    {
        ArgumentNullException.ThrowIfNull(vnStore);
        if (!vnStore.IsVNCheckedBound(vn))
        {
            return false;
        }

        var range = RangeCheck.GetRangeFromAssertions(this, vn, assertions);
        return range.LowerLimit.IsConstant && (range.LowerLimit.GetConstant() >= 0);
    }

    public ValueNum optGetPreferredBoundForRange(ValueNum vn, ASSERT_TP assertions)
    {
        if (BitVecOps.MaybeUninit(assertions) || !optAssertionHasAssertionsForVN(vn))
        {
            return ValueNumStore.NoVN;
        }

        ArgumentNullException.ThrowIfNull(assertions);
        assert(apTraits is not null);
        var iterator = new BitVecOps.Iter(apTraits, assertions);
        uint bitIndex = 0;
        while (iterator.NextElem(ref bitIndex))
        {
            var assertion = optGetAssertion(GetAssertionIndex((ushort)bitIndex));
            if (assertion.IsRelop && (assertion.Op1.VN == vn) &&
                assertion.Op2.KindIs(O2K_VN_ADD_CNS) &&
                optIsNeverNegativeCheckedBound(assertion.Op2.VN, assertions))
            {
                return assertion.Op2.VN;
            }
        }

        return ValueNumStore.NoVN;
    }

    private static Range GetBoundMinusXRange(Compiler compiler, GenTree tree, BasicBlock block, ValueNum boundVN)
    {
        tree = tree.EffectiveVal;
        while ((tree.Oper is GT_LCL_VAR) && tree.AsLclVar().HasSsaName)
        {
            ref var local = ref compiler.lvaGetDesc(tree.AsLclVar().LclNum);
            ref var definition = ref local.GetPerSsaData(tree.AsLclVar().SsaNum);
            var defStore = definition.DefNode;
            if ((local.Type is not TYP_INT) || (defStore is null) ||
                (defStore.Oper is not GT_STORE_LCL_VAR))
            {
                return new(new(LimitType.Unknown));
            }

            var operand = defStore.Data;
            if (operand is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAStoreHasNoValue);
            }

            tree = operand.EffectiveVal;
            var definingBlock = definition.Block;
            if (definingBlock is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAStoreHasNoDefiningBlock2);
            }

            block = definingBlock;
        }

        if ((tree.Oper is not GT_SUB) || (tree.Type is not TYP_INT) ||
            (compiler.optConservativeNormalVN(tree.AsOp().Op1) != boundVN))
        {
            return new(new(LimitType.Unknown));
        }

        var xRange = RangeCheck.GetRange(
            compiler, tree.AsOp().Op2, block, assertions: null, fast: false, preferredBoundVN: boundVN);
        var xLow = xRange.LowerLimit;
        var xHigh = xRange.UpperLimit;
        if (xLow.IsConstant && (xLow.GetConstant() >= 0) &&
            xHigh.IsBinOpArray && (xHigh.VN == boundVN) &&
            (xHigh.GetConstant() <= 0) && (xHigh.GetConstant() > int.MinValue))
        {
            return new(
                new Limit(LimitType.Constant, -xHigh.GetConstant()),
                new Limit(LimitType.BinOpArray, boundVN, -xLow.GetConstant()));
        }

        return new(new(LimitType.Unknown));
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
                else if (assertion.KindIs(OAK_LT, OAK_LT_UN) &&
                    (tree.Oper is GT_LE or GT_GT) &&
                    (tree.AsOp().IsUnsigned == assertion.KindIs(OAK_LT_UN)) &&
                    (assertion.Op2.VN == op2VN) &&
                    (!assertion.Op2.KindIs(O2K_VN_ADD_CNS) || (assertion.Op2.Cns == 0)))
                {
                    var addVN = ValueNumStore.NoVN;
                    var addConstant = 0;
                    if (vnStore.IsVNBinFuncWithConst(op1VN, VNF_ADD, ref addVN, ref addConstant) &&
                        (addConstant == 1) && (addVN == assertion.Op1.VN))
                    {
                        result = gtNewIconNode(TYP_INT, tree.Oper is GT_LE ? 1 : 0);
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

                JITDUMP($"Found matching assertion #{index:D2} for tree {tree.TreeId:D6}.");
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
                    (op1.Type is TYP_INT) && (tree.Oper is GT_LE or GT_LT or GT_GE or GT_GT))
                {
                    if (op2.IsIntCnsFitsInI32)
                    {
                        var op1Range = new Range(new Limit(LimitType.Unknown));
                        var boundVN = ValueNumStore.NoVN;
                        var xVN = ValueNumStore.NoVN;
                        if (vnStore.IsVNBinFunc(op1VN, VNF_SUB, ref boundVN, ref xVN) &&
                            optIsNeverNegativeCheckedBound(boundVN, assertions))
                        {
                            op1Range = GetBoundMinusXRange(this, op1, block, boundVN);
                        }

                        if (op1Range.LowerLimit.IsUnknown)
                        {
                            op1Range = RangeCheck.GetRange(
                                this, op1, block, assertions, fast: false,
                                preferredBoundVN: optGetPreferredBoundForRange(op1VN, assertions));
                        }

                        var op2Range = RangeCheck.GetRange(this, op2, block, assertions, fast: false);
                        relopRange = RangeOps.EvalRelop(tree.Oper, tree.AsOp().IsUnsigned, op1Range, op2Range);
                    }
                    else if ((tree.Oper is GT_GT or GT_LE) && tree.AsOp().IsUnsigned &&
                        (op2.Type is TYP_INT) && optIsNeverNegativeCheckedBound(op2VN, assertions))
                    {
                        var op1Range = RangeCheck.GetRange(
                            this, op1, block, assertions, fast: false, preferredBoundVN: op2VN);
                        var op2Range = new Range(new Limit(LimitType.BinOpArray, op2VN, 0));
                        relopRange = RangeOps.EvalRelop(tree.Oper, true, op1Range, op2Range);
                    }
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
            JITDUMP($"Proved [{op1.TreeId:D6}] non-null\n");
            result = gtWrapWithSideEffects(tree.Oper is GT_EQ ? gtNewFalse() : gtNewTrue(), tree, GTF_ALL_EFFECT);
            return optAssertionProp_Update(result, tree, statement);
        }

        var op1ObjectVN = ValueNumStore.NoVN;
        var app = new VNFuncApp();
        if (((op1.Type == TYP_I_IMPL)) && vnStore.GetVNFunc(op1VN, ref app) &&
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
                JITDUMP($"Found matching assertion #{index:D2} for tree {tree.TreeId:D6}.");
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
