// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, assertionprop.cpp.

using System;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.Compiler.optOp2Kind;

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree? optAssertionProp_AddMulSub(ASSERT_TP? assertions, GenTreeOp tree,
        Statement? statement, BasicBlock? block)
    {
        assert(tree.Oper is GT_MUL or GT_ADD or GT_SUB);

        if (!optLocalAssertionProp && varTypeIsIntegral(tree.Type) && tree.HasOverflowCheck)
        {
            assert(block is not null);
            var op1Range = RangeCheck.GetRange(this, tree.Op1, block, assertions, fast: true);
            var op2Range = RangeCheck.GetRange(this, tree.Op2, block, assertions, fast: true);
            if (op1Range.IsConstantRange() && op2Range.IsConstantRange())
            {
                var result = tree.Oper switch
                {
                    GT_MUL => RangeOps.Multiply(op1Range, op2Range, tree.IsUnsigned),
                    GT_ADD => RangeOps.Add(op1Range, op2Range, tree.IsUnsigned),
                    GT_SUB => RangeOps.Subtract(op1Range, op2Range, tree.IsUnsigned),
                    _ => throw new InvalidOperationException(),
                };
                if (result.IsConstantRange())
                {
                    tree.Flags &= ~GTF_OVERFLOW;
                    return optAssertionProp_Update(tree, tree, statement);
                }
            }
        }

        return null;
    }

    public GenTree? optAssertionProp_ModDiv(ASSERT_TP? assertions, GenTreeOp tree,
        Statement? statement, BasicBlock? block)
    {
        assert(tree.Oper is GT_DIV or GT_UDIV or GT_MOD or GT_UMOD);
        optAssertionProp_RangeProperties(assertions, tree.Op1, statement, block,
            out _, out var op1IsNotNegative);
        optAssertionProp_RangeProperties(assertions, tree.Op2, statement, block,
            out var op2IsNotZero, out var op2IsNotNegative);

        var changed = false;
        if (op1IsNotNegative && op2IsNotNegative && (tree.Oper is GT_DIV or GT_MOD))
        {
            JITDUMP("Converting DIV/MOD to unsigned UDIV/UMOD since both operands are never negative...\n");
            tree.SetOper(tree.Oper is GT_DIV ? GT_UDIV : GT_UMOD, GenTree.PRESERVE_VN);
            changed = true;
        }

        if (op2IsNotZero && ((tree.Flags & GTF_DIV_MOD_NO_BY_ZERO) == 0))
        {
            JITDUMP("Divisor for DIV/MOD is proven to be never negative...\n");
            tree.Flags |= GTF_DIV_MOD_NO_BY_ZERO;
            tree.HasOrderingSideEffect = true;
            changed = true;
        }

        if ((op1IsNotNegative || op2IsNotNegative) && ((tree.Flags & GTF_DIV_MOD_NO_OVERFLOW) == 0))
        {
            JITDUMP("DIV/MOD is proven to never overflow...\n");
            tree.Flags |= GTF_DIV_MOD_NO_OVERFLOW;
            tree.HasOrderingSideEffect = true;
            changed = true;
        }

        return changed ? optAssertionProp_Update(tree, tree, statement) : null;
    }

    public GenTree? optAssertionProp_Cast(ASSERT_TP? assertions, GenTreeCast cast,
        Statement? statement, BasicBlock? block)
    {
        var operand = cast.CastOp;
        if (!varTypeIsIntegral(cast.Type) || !varTypeIsIntegral(operand.Type))
        {
            return null;
        }

        var local = operand.EffectiveVal;
#if TARGET_64BIT
        if (!cast.IsUnsigned && (local.Type.ActualType is TYP_INT) &&
            (cast.Type is TYP_LONG))
        {
            assert(statement is not null || optLocalAssertionProp);
            if (block is not null && statement is not null)
            {
                optAssertionProp_RangeProperties(assertions, local, statement, block,
                    out _, out var isKnownNonNegative);
                if (isKnownNonNegative)
                {
                    cast.Flags |= GTF_UNSIGNED;
                }
            }
        }
#endif

        if (optLocalAssertionProp)
        {
            return optLocalAssertionPropCast(assertions, cast);
        }

        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(block);
        var canDropCast = cast.Type.ActualType == local.Type.ActualType;
        if (canDropCast && (local.Oper is GT_LCL_VAR))
        {
            if (lvaGetDesc(local.AsLclVar().LclNum).lvNormalizeOnLoad)
            {
                canDropCast = false;
            }
        }
        else if (canDropCast && varTypeIsSmall(cast.CastType))
        {
            canDropCast = false;
        }

        if (!canDropCast && !cast.HasOverflowCheck)
        {
            return null;
        }

        var castRange = IntegralRange.ForCastInput(cast);
        var castLower = IntegralRange.SymbolicToRealValue(castRange.LowerBound);
        var castUpper = IntegralRange.SymbolicToRealValue(castRange.UpperBound);
        if ((castLower >= int.MinValue) && (castUpper <= int.MaxValue))
        {
            var castToTypeRange = new Range(
                new(LimitType.Constant, (int)castLower),
                new(LimitType.Constant, (int)castUpper));
            if (castToTypeRange.IsConstantRange())
            {
                var inputRange = RangeCheck.GetRange(this, operand, block, assertions, fast: true);
                if (inputRange.IsConstantRange() &&
                    (inputRange.LowerLimit.Constant >= castToTypeRange.LowerLimit.Constant) &&
                    (inputRange.UpperLimit.Constant <= castToTypeRange.UpperLimit.Constant))
                {
                    if (canDropCast)
                    {
#if DEBUG
                        JITDUMP($"Removing cast {cast.TreeId:D6} as redundant based on VN assertions.\n");
#endif
                        return optAssertionProp_Update(operand, cast, statement);
                    }

                    assert(cast.HasOverflowCheck);
#if DEBUG
                    JITDUMP($"Clearing overflow flag for cast {cast.TreeId:D6} based on VN assertions.\n");
#endif
                    cast.Flags &= ~GTF_OVERFLOW;
                    return optAssertionProp_Update(cast, cast, statement);
                }
            }
        }

        return null;
    }

    public GenTree? optAssertionProp_BndsChk(ASSERT_TP? assertions, GenTree tree,
        Statement? statement, BasicBlock? block)
    {
        assert(tree.Oper is GT_BOUNDS_CHECK);
        if (optLocalAssertionProp)
        {
            return null;
        }

        assert(statement is not null);
        assert(block is not null);
        ArgumentNullException.ThrowIfNull(vnStore);
        assert(apTraits is not null);
        var check = tree.AsBoundsChk();
        var indexTree = check.Index;
        var lengthTree = check.ArrayLength;
        var indexVN = optConservativeNormalVN(indexTree);
        var lengthVN = optConservativeNormalVN(lengthTree);
        var indexRange = new Range(new(LimitType.Undef));
        var lengthRange = new Range(new(LimitType.Undef));

        Range GetIndexRange()
        {
            if (indexRange.IsUndef())
            {
                indexRange = RangeCheck.GetRange(this, indexTree, block, assertions, fast: true);
            }

            return indexRange;
        }

        Range GetLengthRange()
        {
            if (lengthRange.IsUndef())
            {
                lengthRange = RangeCheck.GetRange(this, lengthTree, block, assertions, fast: true);
            }

            return lengthRange;
        }

        GenTree DropBoundsCheck(string reason)
        {
#if DEBUG
            assert(compCurBB is not null);
            JITDUMP($"\nRemoving redundant ({reason}) bounds check in {FMT_BB(compCurBB.bbNum)}:\n");
            DISPTREE(tree);
#endif
            GenTree? sideEffects = null;
            // Proving the bounds check redundant also proves the length load cannot fault.
            gtExtractSideEffList(lengthTree, ref sideEffects, GTF_ASG);
            gtExtractSideEffList(indexTree, ref sideEffects);
            return optAssertionProp_Update(sideEffects ?? gtNewNothingNode(), check, statement);
        }

        if (!BitVecOps.MaybeUninit(assertions))
        {
            string? matchingReason = null;
            _ = BitVecOps.VisitBits(apTraits, assertions, bitIndex =>
            {
                var assertion = optGetAssertion(GetAssertionIndex((ushort)bitIndex));
                if (!assertion.IsBoundsCheckNoThrow)
                {
                    return true;
                }

                assert((assertion.Op2.Cns == 0) && assertion.Op2.IsVNNeverNegative);
                if (assertion.Op2.VN != lengthVN)
                {
                    return true;
                }

                if (assertion.Op1.VN == indexVN)
                {
                    matchingReason = "a[i] followed by a[i]";
                    return false;
                }

                if (GetIndexRange().IsConstantRange() && (GetIndexRange().LowerLimit.Constant >= 0))
                {
                    var previousRange = RangeCheck.GetRangeFromAssertions(this, assertion.Op1.VN, null);
                    if (previousRange.IsConstantRange() &&
                        (previousRange.LowerLimit.Constant >= GetIndexRange().UpperLimit.Constant))
                    {
                        matchingReason = "currIdx upper bound covered by prevIdx lower bound";
                        return false;
                    }
                }

                return true;
            });
            if (matchingReason is not null)
            {
                return DropBoundsCheck(matchingReason);
            }
        }

        var op0 = ValueNumStore.NoVN;
        var op1 = ValueNumStore.NoVN;
        if (vnStore.IsVNBinFunc(indexVN, VNF_ADD, ref op0, ref op1))
        {
            if (!vnStore.IsVNInt32Constant(op1))
            {
                (op0, op1) = (op1, op0);
            }

            if ((op0 == lengthVN) && vnStore.IsVNInt32Constant(op1))
            {
                var range = RangeCheck.GetRangeFromAssertions(this, lengthTree, assertions);
                var lengthLower = range.LowerLimit.GetConstant();
                var delta = vnStore.GetConstantInt32(op1);
                if ((lengthLower > 0) && (delta < 0) && (delta > int.MinValue) &&
                    (lengthLower >= -delta))
                {
                    return DropBoundsCheck("a[a.Length-cns] when a.Length is known to be >= cns");
                }
            }
        }
        else if (vnStore.IsVNBinFunc(indexVN, VNF_UMOD, ref op0, ref op1) && (op1 == lengthVN))
        {
            // A zero length throws during the remainder before reaching the check.
            return DropBoundsCheck("a[X u% a.Length] is always within bounds");
        }

        if (GetIndexRange().IsConstantRange() && GetLengthRange().IsConstantRange())
        {
            var indexLower = GetIndexRange().LowerLimit.GetConstant();
            var indexUpper = GetIndexRange().UpperLimit.GetConstant();
            var lengthLower = GetLengthRange().LowerLimit.GetConstant();
            if ((indexLower == 0) && (indexUpper == 0) && (lengthLower <= 0) &&
                !BitVecOps.MaybeUninit(assertions))
            {
                _ = BitVecOps.VisitBits(apTraits, assertions, bitIndex =>
                {
                    var assertion = optGetAssertion(GetAssertionIndex((ushort)bitIndex));
                    if (assertion.IsConstantInt32Assertion && assertion.KindIs(OAK_NOT_EQUAL) &&
                        (assertion.Op1.VN == lengthVN) && (assertion.Op2.IntConstant == 0))
                    {
                        lengthLower = 1;
                        return false;
                    }

                    return true;
                });
            }

            if ((indexLower >= 0) && (indexUpper < lengthLower))
            {
                return DropBoundsCheck("upper bound of index is less than lower bound of length");
            }
        }

        return null;
    }
}
