// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerSignedDivOrMod(GenTreeOp node)
    {
        assert(node.Oper is GT_DIV or GT_MOD);

        if (varTypeIsIntegral(node.Type))
        {
            if (TryLowerConstIntDivOrMod(node, out var next))
            {
                return next;
            }
            assert(next is null);
        }

        LowerDivOrMod(node);
        return node.Next;
    }

    private bool TryLowerConstIntDivOrMod(GenTreeOp divMod, out GenTree? nextNode)
    {
        assert(divMod.Oper is GT_DIV or GT_MOD);
        nextNode = null;

        var compiler = CompilerInstance;
        var dividend = divMod.Op1;
        var divisor = divMod.Op2;
        var type = divMod.Type;
        assert(type is TYP_INT or TYP_LONG);
#if USE_HELPERS_FOR_INT_DIV
        assert(false, "unreachable: integral GT_DIV/GT_MOD should get morphed into helper calls");
#endif

#if TARGET_ARM64
        if ((divMod.Oper is GT_MOD) && divisor.IsIntegralConstPow2)
        {
            var loweredMod = LowerModPow2(divMod);
            nextNode = loweredMod.Next;
            return true;
        }

#endif
#if TARGET_ARMARCH
        assert(divMod.Oper is not GT_MOD);
#endif
#if TARGET_WASM
        return false;
#endif
        if (!divisor.Oper.IsCnsIntOrI || dividend.Oper.IsCnsIntOrI)
        {
            return false;
        }

#if TARGET_ARM64
        var divisorValue = (long)divisor.AsIntCon().IconValue;
#else
        var divisorValue = type is TYP_INT ? (long)(int)divisor.AsIntCon().IconValue : (long)divisor.AsIntCon().IconValue;
#endif
        if (divisorValue is 0 or -1)
        {
            return false;
        }

        var isDiv = divMod.Oper is GT_DIV;
        if (isDiv && (((type is TYP_INT) && (divisorValue == int.MinValue))
#if TARGET_64BIT
            || ((type is TYP_LONG) && (divisorValue == long.MinValue))
#endif
            ))
        {
            ChangeDivisionOper(divMod, GT_EQ);
            nextNode = divMod;
            return true;
        }

        var absDivisor = divisorValue == long.MinValue
            ? 1ul << 63
            : (ulong)Math.Abs(divisorValue);

        if (!ulong.IsPow2(absDivisor))
        {
            if (compiler.opts.MinOpts)
            {
                return false;
            }

#if TARGET_XARCH || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
            long magic;
            int shift;
            if (type is TYP_INT)
            {
                magic = MagicDivide.GetSigned32Magic((int)divisorValue, out shift);
            }
            else
            {
#if TARGET_64BIT
                magic = MagicDivide.GetSigned64Magic(divisorValue, out shift);
#else
                throw new InvalidOperationException("64-bit signed magic division requires a 64-bit target.");
#endif
            }
            divisor.AsIntCon().IconValue = (nint)magic;

            var mulhi = compiler.gtNewBinaryNode(GT_MULHI, type, divisor, dividend);
            BlockRange().InsertBefore(divMod, mulhi);

            var requiresAdjustment = (divisorValue < 0) != (magic < 0);
            if (requiresAdjustment || !isDiv)
            {
                var use = new LIR.Use(BlockRange(), ref mulhi.Op2Ref, mulhi);
                dividend = ReplaceWithLclVar(use);
            }

            GenTree adjusted = mulhi;
            if (requiresAdjustment)
            {
                var clone = compiler.gtNewLclvNode(dividend.Type, dividend.AsLclVar().LclNum);
                adjusted = compiler.gtNewBinaryNode(divisorValue > 0 ? GT_ADD : GT_SUB, type, mulhi, clone);
                BlockRange().InsertBefore(divMod, clone, adjusted);
            }

            var signShift = compiler.gtNewIconNode(type, type is TYP_INT ? 31 : 63);
            var signBit = compiler.gtNewBinaryNode(GT_RSZ, type, adjusted, signShift);
            BlockRange().InsertBefore(divMod, signShift, signBit);

            var adjustedUse = new LIR.Use(BlockRange(), ref signBit.Op1Ref, signBit);
            var adjustedRead = ReplaceWithLclVar(adjustedUse);
            adjusted = compiler.gtNewLclvNode(type, adjustedRead.LclNum);
            BlockRange().InsertBefore(divMod, adjusted);

            if (shift != 0)
            {
                var shiftBy = compiler.gtNewIconNode(TYP_INT, shift);
                adjusted = compiler.gtNewBinaryNode(GT_RSH, type, adjusted, shiftBy);
                BlockRange().InsertBefore(divMod, shiftBy, adjusted);
            }

            if (isDiv)
            {
                ChangeDivisionOper(divMod, GT_ADD);
                divMod.Op1 = adjusted;
                divMod.Op2 = signBit;
            }
            else
            {
                var quotient = compiler.gtNewBinaryNode(GT_ADD, type, adjusted, signBit);
                var original = compiler.gtNewLclvNode(type, dividend.AsLclVar().LclNum);
                var constant = compiler.gtNewIconNode(type, (nint)divisorValue);
                var product = compiler.gtNewBinaryNode(GT_MUL, type, quotient, constant);
                BlockRange().InsertBefore(divMod, original, quotient, constant, product);
                ChangeDivisionOper(divMod, GT_SUB);
                divMod.Op1 = original;
                divMod.Op2 = product;
            }

            nextNode = mulhi;
            return true;
#else
            return false;
#endif
        }

        if (!BlockRange().TryGetUse(divMod, out var resultUse))
        {
            return false;
        }

        var dividendUse = new LIR.Use(BlockRange(), ref divMod.Op1Ref, divMod);
        dividend = ReplaceWithLclVar(dividendUse);
        var lclNum = dividend.AsLclVar().LclNum;
        var adjustment = compiler.gtNewBinaryNode(GT_RSH, type, dividend,
            compiler.gtNewIconNode(TYP_INT, type is TYP_INT ? 31 : 63));
        if (absDivisor == 2)
        {
            adjustment.SetOper(GT_RSZ);
        }
        else
        {
            adjustment = compiler.gtNewBinaryNode(GT_AND, type, adjustment,
                compiler.gtNewIconNode(type, unchecked((nint)(absDivisor - 1))));
        }

        var adjustedDividend = compiler.gtNewBinaryNode(GT_ADD, type, adjustment,
            compiler.gtNewLclvNode(type, lclNum));
        GenTree replacement;
        if (isDiv)
        {
            divisor.AsIntCon().IconValue = BitOperations.Log2(absDivisor);
            replacement = compiler.gtNewBinaryNode(GT_RSH, type, adjustedDividend, divisor);
            ContainCheckShiftRotate(replacement.AsOp());
            if (divisorValue < 0)
            {
                replacement = new GenTreeUnOp(GT_NEG, type, replacement);
                ContainCheckNode(replacement);
            }
        }
        else
        {
            divisor.AsIntCon().IconValue = unchecked((nint)~(absDivisor - 1));
            var mask = compiler.gtNewBinaryNode(GT_AND, type, adjustedDividend, divisor);
            replacement = compiler.gtNewBinaryNode(GT_SUB, type, compiler.gtNewLclvNode(type, lclNum), mask);
        }

        BlockRange().Remove(divisor);
        BlockRange().Remove(dividend);
        InsertTreeBeforeAndContainCheck(divMod, replacement);
        BlockRange().Remove(divMod);
        resultUse.ReplaceWith(replacement);
        nextNode = replacement.Next;
        return true;
    }
}
