// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerCompare(GenTree cmp)
    {
#if TARGET_XARCH
        var comparison = cmp.AsOp();
#if LOWER_DECOMPOSE_LONGS
        if (comparison.Op1.Type is TYP_LONG)
        {
            throw new NotImplementedException("x86 long comparison decomposition is not ported.");
        }
#endif

        if (comparison.Op2.Oper.IsIntegralConst && !CompilerInstance.opts.MinOpts)
        {
            var next = OptimizeConstCompare(comparison);
            if (next != cmp)
            {
                return next;
            }
        }

        if ((comparison.Op1.Type == comparison.Op2.Type) &&
            varTypeIsSmall(comparison.Op1.Type) && varTypeIsUnsigned(comparison.Op1.Type))
        {
            // Codegen can compare the common small width without first extending it.
            // Unsigned small operands therefore require an unsigned comparison.
            comparison.IsUnsigned = true;
        }

        ContainCheckCompare(comparison);

        return cmp.Next;
#else
        throw new NotImplementedException("Non-xarch comparison lowering is not ported.");
#endif
    }

#if TARGET_XARCH
    private GenTree? OptimizeConstCompare(GenTreeOp cmp)
    {
        assert(cmp.Op2.Oper.IsIntegralConst);
        var op1 = cmp.Op1;
        var op2 = cmp.Op2.AsIntConCommon();
        var op2Value = op2.IntegralValue;
        var op1Type = op1.Type;

        if (IsContainableMemoryOp(op1) && varTypeIsSmall(op1Type) && FitsIn(op1Type, op2Value))
        {
            // Matching widths permit a contained memory comparison and can reduce
            // the instruction encoding, for example when comparing a byte to 200.
            op2.Type = op1Type;
        }
        else if ((op1.Oper is GT_CAST) && !op1.HasOverflowCheck)
        {
            var cast = op1.AsCast();
            var castToType = cast.CastType;
            var castOp = cast.CastOp;
            if ((castToType is TYP_UBYTE) && FitsIn(TYP_UBYTE, op2Value))
            {
                // Narrow only operations whose low byte is unaffected by narrowing.
                // In particular, shifts, division and multiplication are excluded.
                var removeCast = (castOp.Oper is GT_LCL_VAR or GT_CALL or GT_OR or GT_XOR or GT_AND) ||
                    IsContainableMemoryOp(castOp);
                if (removeCast)
                {
                    assert(!castOp.HasOverflowCheckEx);
                    castOp.Type = castToType;
                    op2.Type = castToType;
                    castOp.IsContained = false;
                    if (castOp.Oper is GT_OR or GT_XOR or GT_AND)
                    {
                        castOp.AsOp().Op1.IsContained = false;
                        castOp.AsOp().Op2.IsContained = false;
                        ContainCheckBinary(castOp.AsOp());
                    }

                    cmp.Op1 = castOp;
                    BlockRange().Remove(cast);
                }
            }
            else if ((castToType is TYP_BYTE) && FitsIn(TYP_BYTE, op2Value))
            {
                // The sign-bit-shift encoding of x < 0 / x >= 0 assumes a value
                // sign-extended to the actual register width; retain those casts.
                var isSignBitTest = (op2Value == 0) && (cmp.Oper is GT_LT or GT_GE);
                var removeCast = !isSignBitTest &&
                    ((castOp.Oper is GT_LCL_VAR or GT_CALL or GT_OR or GT_XOR or GT_AND) ||
                        IsContainableMemoryOp(castOp));
                if (removeCast)
                {
                    assert(!castOp.HasOverflowCheckEx);
                    castOp.Type = castToType;
                    op2.Type = castToType;
                    castOp.IsContained = false;
                    if (castOp.Oper is GT_OR or GT_XOR or GT_AND)
                    {
                        castOp.AsOp().Op1.IsContained = false;
                        castOp.AsOp().Op2.IsContained = false;
                        ContainCheckBinary(castOp.AsOp());
                    }

                    cmp.Op1 = castOp;
                    BlockRange().Remove(cast);
                }
            }
        }
        else if ((op1.Oper is GT_AND) && (cmp.Oper is GT_EQ or GT_NE))
        {
            var andOp1 = op1.AsOp().Op1;
            var andOp2 = op1.AsOp().Op2;

            // (x & bit) == bit is equivalent to (x & bit) != 0 for a single bit.
            if ((op2Value != 0) && BitOperations.IsPow2(unchecked((ulong)op2Value)) &&
                andOp2.Oper.IsIntegralConst && (andOp2.AsIntConCommon().IntegralValue == op2Value))
            {
                op2Value = 0;
                op2.IntegralValue = 0;
                cmp.SetOper(cmp.Oper.ReverseRelop, GenTree.PRESERVE_VN);
            }

            var optimizeToAnd = (op2Value == 0) && (cmp.Oper is GT_NE);
            var optimizeToNotAnd = (op2Value == 0) && (cmp.Oper is GT_EQ);
            if (andOp2.IsIntegralConst(1) && (op1.Type.ActualType == cmp.Type) &&
                (optimizeToAnd || optimizeToNotAnd))
            {
                // Branches and conditional selections still require a relational operand.
                if (BlockRange().TryGetUse(cmp, out var cmpUse) && (cmpUse.User().Oper is not GT_JTRUE) &&
                    !cmpUse.User().Oper.IsConditional)
                {
                    var next = cmp.Next;
                    if (optimizeToNotAnd)
                    {
                        var notNode = CompilerInstance.gtNewUnaryNode(GT_NOT, andOp1.Type, andOp1);
                        op1.AsOp().Op1 = notNode;
                        BlockRange().InsertAfter(andOp1, notNode);
                    }

                    cmpUse.ReplaceWith(op1);
                    BlockRange().Remove(cmp.Op2);
                    BlockRange().Remove(cmp);

                    return next;
                }
            }

            if (op2Value == 0)
            {
                BlockRange().Remove(op1);
                BlockRange().Remove(op2);
                cmp.SetOper(cmp.Oper is GT_EQ ? GT_TEST_EQ : GT_TEST_NE, GenTree.PRESERVE_VN);
                cmp.Op1 = andOp1;
                cmp.Op2 = andOp2;
                andOp1.IsContained = false;
                andOp2.IsContained = false;

                if (IsContainableMemoryOp(andOp1) && andOp2.Oper.IsIntegralConst)
                {
                    var mask = unchecked((nuint)andOp2.AsIntCon().IconValue);
                    if (mask <= byte.MaxValue)
                    {
                        andOp1.Type = TYP_UBYTE;
                        andOp2.Type = TYP_UBYTE;
                    }
                    else if ((mask <= ushort.MaxValue) && (andOp1.Type.Size == 2))
                    {
                        // Avoid introducing the length-changing 0x66 prefix unless
                        // the memory operand is already 16-bit.
                        andOp1.Type = TYP_USHORT;
                        andOp2.Type = TYP_USHORT;
                    }
                }
            }
            else if (andOp2.Oper.IsIntegralConst && GenTree.Compare(andOp2, op2))
            {
                // (x & mask) == mask becomes (~x & mask) == 0 for a multibit mask.
                andOp1.IsContained = false;
                var notNode = CompilerInstance.gtNewUnaryNode(GT_NOT, andOp1.Type, andOp1);
                cmp.Op1.AsOp().Op1 = notNode;
                BlockRange().InsertAfter(andOp1, notNode);

                // BashToZeroConst returns a new managed representation. Detach the
                // source first, then restore its position and replace the owning edge.
                var afterConstant = op2.Next;
                assert(afterConstant is not null);
                BlockRange().Remove(op2);
                op2 = op2.BashToZeroConst(op2.Type).AsIntConCommon();
                BlockRange().InsertBefore(afterConstant, op2);
                cmp.Op2 = op2;
            }
        }

        if ((cmp.Oper is GT_TEST_EQ or GT_TEST_NE) && TryReduceSingleBitTestOps(cmp))
        {
            cmp.SetOper(cmp.Oper is GT_TEST_EQ ? GT_BITTEST_EQ : GT_BITTEST_NE);
            cmp.Op2.IsContained = false;

            return cmp.Next;
        }

        if ((cmp.Oper is GT_EQ or GT_NE) && op2.IsIntegralConst(0) &&
            (op1.Oper.IsCompare || (op1.Oper is GT_SETCC)) && BlockRange().TryGetUse(cmp, out var use))
        {
            if (cmp.Oper is GT_EQ)
            {
                var reversed = CompilerInstance.gtReverseCond(op1);
                assert(reversed == op1);
            }

            op1.Type = cmp.Type;
            var next = cmp.Next;
            use.ReplaceWith(op1);
            BlockRange().Remove(cmp.Op2);
            BlockRange().Remove(cmp);

            return next;
        }

        if ((((cmp.Oper is GT_EQ or GT_NE) && op2.IsIntegralConst(0) && op1.SupportsSettingZeroFlag()) ||
             ((cmp.Oper is GT_GT or GT_GE or GT_LT or GT_LE) && op2.IsIntegralConst(0) &&
                op1.SupportsSettingFlagsAsCompareToZero())) &&
            BlockRange().TryGetUse(cmp, out var flagUse) && IsProfitableToSetZeroFlag(op1))
        {
            var isUnsignedZeroCompare = cmp.IsUnsigned && op2.IsIntegralConst(0);
            if (isUnsignedZeroCompare && (cmp.Oper is GT_GE or GT_LT))
            {
                // These conditions are constant, not carry-based tests of the producer.
                return cmp;
            }

            op1.Flags |= GTF_SET_FLAGS;
            op1.IsUnusedValue = true;
            var next = cmp.Next;
            BlockRange().Remove(cmp);
            BlockRange().Remove(op2);

            var cmpCondition = GenCondition.FromRelop(cmp);
            if (isUnsignedZeroCompare)
            {
                if (cmp.Oper is GT_GT)
                {
                    cmpCondition = new GenCondition(GenCondition.NE);
                }
                else if (cmp.Oper is GT_LE)
                {
                    cmpCondition = new GenCondition(GenCondition.EQ);
                }
            }

            var setcc = CompilerInstance.gtNewCC(GT_SETCC, cmp.Type, cmpCondition);
            BlockRange().InsertAfter(op1, setcc);
            flagUse.ReplaceWith(setcc);

            return next;
        }

        return cmp;
    }
#endif
}
