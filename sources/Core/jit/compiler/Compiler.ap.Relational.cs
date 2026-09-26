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

    public AssertionIndex optGlobalAssertionIsEqualOrNotEqual(ASSERT_TP assertions, GenTree op1, GenTree op2)
    {
        ArgumentNullException.ThrowIfNull(vnStore);
        assert(apTraits is not null);

        if (BitVecOps.IsEmpty(apTraits, assertions))
        {
            return NO_ASSERTION_INDEX;
        }

        var op1VN = vnStore.VNNormalValue(op1._vnPair.Conservative);
        var op2VN = vnStore.VNNormalValue(op2._vnPair.Conservative);
        var result = NO_ASSERTION_INDEX;
        _ = BitVecOps.VisitBits(apTraits, assertions, bitIndex =>
        {
            var index = GetAssertionIndex((ushort)bitIndex);
            if (index > optAssertionCount)
            {
                return false;
            }

            var assertion = optGetAssertion(index);
            if (!assertion.CanPropEqualOrNotEqual)
            {
                return true;
            }

            if ((assertion.Op1.VN == op1VN) && (assertion.Op2.VN == op2VN))
            {
                result = index;
                return false;
            }

            if (assertion.KindIs(OAK_EQUAL) && assertion.Op1.KindIs(O1K_EXACT_TYPE) &&
                (assertion.Op2.VN == op2VN) && (op1.Type is TYP_I_IMPL))
            {
                var app = new VNFuncApp();
                if (vnStore.GetVNFunc(op1VN, ref app) &&
                    app.FuncIs(VNF_InvariantNonNullLoad) && (assertion.Op1.VN == app.GetArg(0)))
                {
                    result = index;
                    return false;
                }
            }

            return true;
        });

        return result;
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

        if ((op1.Flags & GTF_SIDE_EFFECT) != 0)
        {
            return null;
        }

        if (op1.Oper is not (GT_LCL_VAR or GT_IND))
        {
            return null;
        }

        if (op2.IsIntegralConst(0) && (op1.Type is TYP_REF))
        {
#if DEBUG
            JITDUMP($"Checking PHI [{op1.TreeId:D6}] arguments for non-nullness\n");
#endif
            var op1VNForPhi = vnStore.VNNormalValue(op1._vnPair.Conservative);
            if (optVisitReachingAssertions(op1VNForPhi,
                (reachingVN, reachingAssertions) =>
                    optAssertionVNIsNonNull(reachingVN, reachingAssertions)
                        ? AssertVisit.Continue : AssertVisit.Abort) is AssertVisit.Continue)
            {
                JITDUMP("... all of PHI's arguments are never null!\n");
                result = gtNewIconNode(TYP_INT, tree.Oper is GT_EQ ? 0 : 1);
                return optAssertionProp_Update(result, tree, statement);
            }
        }

        var assertionIndex = optGlobalAssertionIsEqualOrNotEqual(assertions, op1, op2);
        if (assertionIndex == NO_ASSERTION_INDEX)
        {
            return null;
        }

        var matchingAssertion = optGetAssertion(assertionIndex);
        var assertionIsEqual = matchingAssertion.KindIs(OAK_EQUAL);
        var allowReverse = true;
        var constantVN = vnStore.VNNormalValue(op2._vnPair.Conservative);
        if (vnStore.IsVNConstant(constantVN))
        {
#if DEBUG
            if (verbose)
            {
                assert(compCurBB is not null);
                jitprintf($"\nVN relop based constant assertion prop in {FMT_BB(compCurBB.bbNum)}:\n");
                jitprintf($"Assertion index=#{assertionIndex:D2}: ");
                printTreeId(op1);
                jitprintf(assertionIsEqual ? " == " : " != ");
                if (op1.Type.ActualType is TYP_INT)
                {
                    jitprintf($"{vnStore.ConstantValue<int>(constantVN)}\n");
                }
                else if (op1.Type is TYP_LONG)
                {
                    jitprintf($"{vnStore.ConstantValue<long>(constantVN)}\n");
                }
                else if (op1.Type is TYP_FLOAT)
                {
                    jitprintf($"{formatFloat(vnStore.ConstantValue<float>(constantVN), "F6")}\n");
                }
                else if (op1.Type is TYP_DOUBLE)
                {
                    jitprintf($"{formatFloat(vnStore.ConstantValue<double>(constantVN), "F6")}\n");
                }
                else
                {
                    jitprintf($"{vnStore.ConstantValue<nint>(constantVN)}\n");
                }
                gtDispTree(tree, topOnly: true);
            }
#endif
            var threading = NodeThreading.AllTrees;
            GenTree constant;
            if (op1.Type.ActualType is TYP_INT)
            {
                constant = new GenTreeIntCon(TYP_INT, vnStore.ConstantValue<int>(constantVN),
                    null, op1, threading);
            }
            else if (op1.Type is TYP_LONG)
            {
#if TARGET_64BIT
                constant = new GenTreeIntCon(TYP_LONG, unchecked((nint)vnStore.ConstantValue<long>(constantVN)),
                    null, op1, threading);
#else
                constant = new GenTreeLngCon(vnStore.ConstantValue<long>(constantVN), op1, threading);
#endif
            }
            else if (op1.Type is TYP_DOUBLE)
            {
                var value = vnStore.ConstantValue<double>(constantVN);
                constant = new GenTreeDblCon(TYP_DOUBLE, value, op1, threading);
                allowReverse = !double.IsNaN(value);
            }
            else if (op1.Type is TYP_FLOAT)
            {
                var value = vnStore.ConstantValue<float>(constantVN);
                constant = new GenTreeDblCon(TYP_FLOAT, value, op1, threading);
                allowReverse = !float.IsNaN(value);
            }
            else if (op1.Type is TYP_REF or TYP_BYREF)
            {
                constant = new GenTreeIntCon(op1.Type,
                    unchecked((nint)vnStore.ConstantValue<nuint>(constantVN)), null, op1, threading);
            }
            else
            {
                throw new InvalidOperationException($"Unknown global relop operand type {op1.Type}.");
            }

            if (vnStore.IsVNHandle(constantVN) && constant.Oper.IsCnsIntOrI)
            {
                constant.Flags |= vnStore.GetHandleFlags(constantVN) & GTF_ICON_HDL_MASK;
            }

            constant._vnPair.SetBoth(constantVN);
            tree.AsOp().Op1 = constant;

            var foldResult = assertionIsEqual;
            if (tree.Oper is GT_NE)
            {
                foldResult = !foldResult;
            }

            tree._vnPair.SetBoth(foldResult
                ? vnStore.VNForIntCon(1) : vnStore.VNZeroForType(TYP_INT));
        }
        else if ((op1.Oper is GT_LCL_VAR) && (op2.Oper is GT_LCL_VAR))
        {
#if DEBUG
            if (verbose)
            {
                assert(compCurBB is not null);
                jitprintf($"\nVN relop based copy assertion prop in {FMT_BB(compCurBB.bbNum)}:\n");
                jitprintf($"Assertion index=#{assertionIndex:D2}: V{op1.AsLclVarCommon().LclNum:D2}." +
                    $"{op1.AsLclVarCommon().SsaNum:D2} {(assertionIsEqual ? "==" : "!=")} " +
                    $"V{op2.AsLclVarCommon().LclNum:D2}.{op2.AsLclVarCommon().SsaNum:D2}\n");
                gtDispTree(tree, topOnly: true);
            }
#endif
            if (op1.Type is TYP_FLOAT or TYP_DOUBLE)
            {
                var first = new GenTreeDblCon(op1.Type, 0.0, op1, NodeThreading.AllTrees);
                var second = new GenTreeDblCon(op2.Type, 0.0, op2, NodeThreading.AllTrees);
                first._vnPair.SetBoth(ValueNumStore.NoVN);
                second._vnPair.SetBoth(ValueNumStore.NoVN);
                tree.AsOp().Op1 = first;
                tree.AsOp().Op2 = second;
            }
            else
            {
                noway_assert(varTypeIsIntegralOrI(op1.Type));
                var target = op2.AsLclVarCommon();
                var source = op1.AsLclVarCommon();
                source.LclNum = target.LclNum;
                source.SsaNum = target.SsaNum;
            }
        }
        else
        {
            return null;
        }

        if (allowReverse && matchingAssertion.KindIs(OAK_NOT_EQUAL))
        {
            _ = gtReverseCond(tree);
        }

        result = fgMorphTree(tree);
#if DEBUG
        if (verbose)
        {
            gtDispTree(result, topOnly: true);
        }
#endif
        return optAssertionProp_Update(result, tree, statement);
    }
}
