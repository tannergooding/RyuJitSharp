// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerUnsignedDivOrMod(GenTreeOp divMod)
    {
        if (TryLowerConstIntUDivOrUMod(divMod, out var nextNode))
        {
            return nextNode;
        }
        else
        {
            LowerDivOrMod(divMod);
        }

        return divMod.Next;
    }

    private bool TryLowerConstIntUDivOrUMod(GenTreeOp divMod, out GenTree? nextNode)
    {
        assert(divMod.Oper is GT_UDIV or GT_UMOD);
#if USE_HELPERS_FOR_INT_DIV
        assert(varTypeIsFloating(divMod.Type));
#endif
#if TARGET_ARMARCH
        assert(divMod.Oper is not GT_UMOD);
#endif
        nextNode = null;
        var compiler = CompilerInstance;
        var dividend = divMod.Op1;
        var divisor = divMod.Op2;
#if !TARGET_64BIT
        if (dividend.Oper is GT_LONG)
        {
            return false;
        }
#endif
        if (!divisor.Oper.IsCnsIntOrI || dividend.Oper.IsCnsIntOrI)
        {
            return false;
        }

        var type = divMod.Type;
        assert(type is TYP_INT or TYP_I_IMPL);
        var divisorValue = type is TYP_INT
            ? unchecked((ulong)(uint)divisor.AsIntCon().IconValue)
#if TARGET_64BIT
            : unchecked((ulong)(nuint)divisor.AsIntCon().IconValue);
#else
            : unchecked((ulong)(uint)divisor.AsIntCon().IconValue);
#endif
        if (divisorValue == 0)
        {
            return false;
        }

        var isDiv = divMod.Oper is GT_UDIV;
        if (ulong.IsPow2(divisorValue))
        {
            ChangeDivisionOper(divMod, isDiv ? GT_RSZ : GT_AND);
            divisor.AsIntCon().IconValue = isDiv
                ? BitOperations.Log2(divisorValue)
                : unchecked((nint)(divisorValue - 1));
            ContainCheckNode(divMod);
            nextNode = divMod.Next;
            return true;
        }

        if (isDiv && (((type is TYP_INT) && (divisorValue > uint.MaxValue / 2))
            || ((type is TYP_I_IMPL) && (divisorValue > ulong.MaxValue / 2))))
        {
            ChangeDivisionOper(divMod, GT_GE);
            divMod.IsUnsigned = true;
#if TARGET_ARM64
            nextNode = LowerCompare(divMod);
#else
            nextNode = LowerNode(divMod);
#endif
            return true;
        }

#if TARGET_XARCH || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
        if (compiler.opts.MinOpts || divisorValue < 3)
        {
            return false;
        }

        var bits = type is TYP_INT ? 32u : 64u;
        if ((dividend.Oper is GT_AND) && dividend.AsOp().Op2.Oper.IsCnsIntOrI)
        {
            var mask = unchecked((ulong)(nuint)dividend.AsOp().Op2.AsIntCon().IconValue);
            if (mask != 0)
            {
                bits = Math.Min(bits, (uint)(64 - BitOperations.LeadingZeroCount(mask)));
            }
        }
        else if ((dividend.Oper is GT_RSZ) && dividend.AsOp().Op2.Oper.IsCnsIntOrI)
        {
            var shift = unchecked((ulong)(nuint)dividend.AsOp().Op2.AsIntCon().IconValue);
            if (shift < bits)
            {
                bits -= (uint)shift;
            }
        }

        ulong magic;
        bool increment;
        int preShift;
        int postShift;
        var simpleMul = false;
        if (type is TYP_INT)
        {
            magic = MagicDivide.GetUnsigned32Magic((uint)divisorValue, out increment, out preShift, out postShift, bits);
#if TARGET_64BIT
#if TARGET_XARCH
            var useNativeWidthMagic = increment || ((preShift != 0) && (unchecked((int)magic) < 0));
#else
            var useNativeWidthMagic = increment || (preShift != 0);
#endif
            if (useNativeWidthMagic)
            {
                magic = MagicDivide.GetUnsigned64Magic(divisorValue, out increment, out preShift, out postShift, bits);
            }
            else
            {
                postShift += 32;
                simpleMul = true;
            }
#endif
        }
        else
        {
#if TARGET_64BIT
            magic = MagicDivide.GetUnsigned64Magic(divisorValue, out increment, out preShift, out postShift, bits);
#else
            throw new InvalidOperationException("Native-width division must use the 32-bit path on this target.");
#endif
        }

        if (!isDiv)
        {
            var dividendUse = new LIR.Use(BlockRange(), ref divMod.Op1Ref, divMod);
            dividend = ReplaceWithLclVar(dividendUse);
        }

        GenTree? firstNode = null;
        var adjustedDividend = dividend;
#if TARGET_ARM64
        var widen = (type is not TYP_I_IMPL) && !simpleMul;
#else
        var widen = type is not TYP_I_IMPL;
#endif
        if (increment)
        {
            adjustedDividend = new GenTreeUnOp(GT_INC_SATURATE, type, adjustedDividend);
            BlockRange().InsertBefore(divMod, adjustedDividend);
            firstNode = adjustedDividend;
            assert(preShift == 0);
        }
        else if (preShift != 0)
        {
            var shiftBy = compiler.gtNewIconNode(TYP_INT, preShift);
            adjustedDividend = compiler.gtNewBinaryNode(GT_RSZ, type, adjustedDividend, shiftBy);
            BlockRange().InsertBefore(divMod, shiftBy, adjustedDividend);
            firstNode = shiftBy;
        }
        else if (widen)
        {
            adjustedDividend = compiler.gtNewCastNode(TYP_I_IMPL, adjustedDividend, true, TYP_I_IMPL);
            BlockRange().InsertBefore(divMod, adjustedDividend);
            firstNode = adjustedDividend;
        }

#if TARGET_XARCH
        if ((firstNode is not null) && !simpleMul)
        {
            adjustedDividend.RegNum = compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2)
                ? REG_RDX : REG_RAX;
        }
#endif
        if (widen)
        {
            divisor.Type = TYP_I_IMPL;
        }
        divisor.AsIntCon().IconValue = unchecked((nint)magic);

        GenTree lastNode = divMod;
        if (isDiv && (postShift == 0) && (type is TYP_I_IMPL))
        {
            ChangeDivisionOper(divMod, GT_MULHI);
            divMod.Op1 = adjustedDividend;
            divMod.IsUnsigned = true;
        }
        else
        {
#if TARGET_ARM64
            var multiplyOper = simpleMul ? GT_MUL_LONG : GT_MULHI;
#else
            var multiplyOper = simpleMul ? GT_MUL : GT_MULHI;
#endif
            var mulhi = compiler.gtNewBinaryNode(multiplyOper,
                TYP_I_IMPL, adjustedDividend, divisor);
            mulhi.IsUnsigned = true;
            BlockRange().InsertBefore(divMod, mulhi);
            firstNode ??= mulhi;
            GenTree quotient = mulhi;
            if (postShift != 0)
            {
                var shiftBy = compiler.gtNewIconNode(TYP_INT, postShift);
                BlockRange().InsertBefore(divMod, shiftBy);
                if (isDiv && (type is TYP_I_IMPL))
                {
                    ChangeDivisionOper(divMod, GT_RSZ);
                    divMod.Op1 = mulhi;
                    divMod.Op2 = shiftBy;
                }
                else
                {
                    quotient = compiler.gtNewBinaryNode(GT_RSZ, TYP_I_IMPL, mulhi, shiftBy);
                    BlockRange().InsertBefore(divMod, quotient);
                }
            }

            if (!isDiv)
            {
                var original = compiler.gtNewLclvNode(dividend.Type, dividend.AsLclVar().LclNum);
                var constant = compiler.gtNewIconNode(type, unchecked((nint)divisorValue));
                var product = compiler.gtNewBinaryNode(GT_MUL, type, quotient, constant);
                BlockRange().InsertBefore(divMod, constant, product, original);
                ChangeDivisionOper(divMod, GT_SUB);
                divMod.Op1 = original;
                divMod.Op2 = product;
            }
            else if (type is not TYP_I_IMPL)
            {
                var cast = new GenTreeCast(TYP_INT, quotient, TYP_INT, divMod, NodeThreading.LIR);
                BlockRange().ReplaceNode(divMod, cast);
                lastNode = cast;
            }
        }

        if (firstNode is not null)
        {
            ContainCheckRange(firstNode, lastNode);
        }

        nextNode = lastNode.Next;
        return true;
#else
        return false;
#endif
    }
}
