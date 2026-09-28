// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
using System;

namespace RyuJitSharp;

public sealed partial class Rationalizer
{
    private bool CanKeepHWIntrinsicImmediate(GenTreeHWIntrinsic node, in CORINFO_SIG_INFO signature)
    {
        var id = node.HWIntrinsicId;
        if ((signature.numArgs == 0) || !HWIntrinsicInfo.HasImmediateOperand(id))
        {
            return false;
        }

        HWIntrinsicInfo.GetImmOpsPositions(id, signature.numArgs, out var firstPosition, out var secondPosition);
        var operands = node.Operands;
        var first = firstPosition < 0 ? (GenTree?)null : operands[operands.Length - 1 - firstPosition];
        var second = secondPosition < 0 ? (GenTree?)null : operands[operands.Length - 1 - secondPosition];
        var size = node.SimdSize;
        var baseType = node.SimdBaseType;
        var immediateSize = size;
        var immediateBaseType = baseType;

        if (second is not null)
        {
            CompilerInstance.getHWIntrinsicImmTypes(id, in signature, 2, ref immediateSize, ref immediateBaseType);
            HWIntrinsicInfo.lookupImmBounds(id, immediateSize, immediateBaseType, 2,
                out var lowerBound, out var upperBound);
            if (CompilerInstance.CheckHWIntrinsicImmRange(id, baseType, second,
                false, lowerBound, upperBound, false, out _))
            {
                second = null;
            }
            immediateSize = size;
            immediateBaseType = baseType;
        }

        CompilerInstance.getHWIntrinsicImmTypes(id, in signature, 1, ref immediateSize, ref immediateBaseType);
        HWIntrinsicInfo.lookupImmBounds(id, immediateSize, immediateBaseType, 1,
            out var firstLowerBound, out var firstUpperBound);
        return (second is null) && (first is not null) &&
            CompilerInstance.CheckHWIntrinsicImmRange(id, baseType, first, false,
                firstLowerBound, firstUpperBound, false, out _);
    }

    private bool IsHWIntrinsicCmpMaskExtractMsb(GenTreeHWIntrinsic node, out var_types baseType)
    {
        baseType = node.SimdBaseType switch {
            TYP_FLOAT => TYP_UINT,
            TYP_DOUBLE => TYP_ULONG,
            var type => varTypeToUnsigned(type),
        };

        return (node.HWIntrinsicId is NI_Vector_ExtractMostSignificantBits) &&
            (baseType is not TYP_ULONG) &&
            node.GetOp(1).IsVectorPerElementMask(CompilerInstance, baseType, node.SimdSize);
    }

    private static bool IsPrimitivePopCount(GenTree node) =>
        (node.Oper is GT_INTRINSIC) && (node.AsIntrinsic().IntrinsicName is NI_PRIMITIVE_PopCount);

    private static bool IsZeroCount(GenTree node) =>
        ((node.Oper is GT_INTRINSIC) &&
            (node.AsIntrinsic().IntrinsicName is NI_PRIMITIVE_TrailingZeroCount or NI_PRIMITIVE_LeadingZeroCount)) ||
        (node.Oper.IsHWIntrinsic && (node.AsHWIntrinsic().HWIntrinsicId is NI_ArmBase_LeadingZeroCount));

    private void ScalarizeHWIntrinsicCmpMaskReduction(GenTreeHWIntrinsic node, GenTree reduction,
        var_types baseType)
    {
        node.Type = baseType.ActualType;
        node.ChangeHWIntrinsicId(NI_Vector_ToScalar);
        node.SimdSize = 8;
        node.SimdBaseType = baseType;
        node.SetOp(1, reduction);
    }

    private GenTreeHWIntrinsic CreateHWIntrinsicCmpMaskReduction(GenTree operand, NamedIntrinsic across,
        NamedIntrinsic pairwise, var_types baseType, byte size)
    {
        if ((size == 8) && (baseType is TYP_UINT))
        {
            LIR.Use.MakeDummyUse(BlockRange, operand, out var use);
            _ = use.ReplaceWithLclVar(CompilerInstance);
            operand = use.Def() ?? throw new InvalidOperationException("Spilled reduction operand has no definition.");
            var second = CompilerInstance.gtClone(operand)
                ?? throw new InvalidOperationException("Failed to clone reduction operand.");
            BlockRange.InsertAfter(operand, second);
            var pair = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD8, pairwise, baseType, size, operand, second);
            BlockRange.InsertAfter(second, pair);
            return pair;
        }

        var reduction = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD8, across, baseType, size, operand);
        BlockRange.InsertAfter(operand, reduction);
        return reduction;
    }

    private bool RewriteHWIntrinsicCmpMaskExtractMsb(ref GenTree use, GenTreeStack parents)
    {
        var node = use.AsHWIntrinsic();
        if (parents.Count <= 1)
        {
            return false;
        }
        var parent = GetParent(parents);
        if (parent.Oper is not (GT_EQ or GT_NE))
        {
            return false;
        }
        var comparison = parent.AsOp();
        if (!(((comparison.Op1 == node) && comparison.Op2.IsIntegralConst(0)) ||
              ((comparison.Op2 == node) && comparison.Op1.IsIntegralConst(0))) ||
            !IsHWIntrinsicCmpMaskExtractMsb(node, out var baseType))
        {
            return false;
        }

        var reduction = CreateHWIntrinsicCmpMaskReduction(node.GetOp(1), NI_AdvSimd_Arm64_MaxAcross,
            NI_AdvSimd_MaxPairwise, baseType, node.SimdSize);
        ScalarizeHWIntrinsicCmpMaskReduction(node, reduction, baseType);
        var cast = CompilerInstance.gtNewCastNode(TYP_INT, node, true, TYP_INT);
        BlockRange.InsertAfter(node, cast);
        ReplaceUse(ref use, parents, node, cast);
        return true;
    }

    private bool RewriteHWIntrinsicCmpMaskExtractMsbPopCount(ref GenTree use, GenTreeStack parents)
    {
        var popCount = use.AsIntrinsic();
        assert(popCount.IntrinsicName is NI_PRIMITIVE_PopCount);
        if (!popCount.Op1.Oper.IsHWIntrinsic)
        {
            return false;
        }
        var extract = popCount.Op1.AsHWIntrinsic();
        if (!IsHWIntrinsicCmpMaskExtractMsb(extract, out var baseType))
        {
            return false;
        }

        var size = extract.SimdSize;
        var amount = CompilerInstance.gtNewIconNode(TYP_INT, baseType.Size * BITS_PER_BYTE - 1);
        BlockRange.InsertAfter(extract.GetOp(1), amount);
        var shift = CompilerInstance.gtNewSimdHWIntrinsicNode(
            Compiler.GetSimdTypeForSize(size), NI_AdvSimd_ShiftRightLogical, baseType, size, extract.GetOp(1), amount);
        BlockRange.InsertAfter(amount, shift);
        var reduction = CreateHWIntrinsicCmpMaskReduction(shift, NI_AdvSimd_Arm64_AddAcross,
            NI_AdvSimd_AddPairwise, baseType, size);
        ScalarizeHWIntrinsicCmpMaskReduction(extract, reduction, baseType);

        var cast = CompilerInstance.gtNewCastNode(TYP_INT, extract, true, TYP_INT);
        BlockRange.InsertAfter(extract, cast);
        BlockRange.Remove(popCount);
        ReplaceUse(ref use, parents, popCount, cast);
        return true;
    }

    private bool RewriteHWIntrinsicCmpMaskExtractMsbZeroCount(ref GenTree use, GenTreeStack parents)
    {
        var zeroCount = use;
        assert(IsZeroCount(zeroCount));
        var trailing = (zeroCount.Oper is GT_INTRINSIC) &&
            (zeroCount.AsIntrinsic().IntrinsicName is NI_PRIMITIVE_TrailingZeroCount);
        var operand = zeroCount.Oper.IsHWIntrinsic ? zeroCount.AsHWIntrinsic().GetOp(1) : zeroCount.AsIntrinsic().Op1;
        if (!operand.Oper.IsHWIntrinsic)
        {
            return false;
        }
        var extract = operand.AsHWIntrinsic();
        if (!IsHWIntrinsicCmpMaskExtractMsb(extract, out var baseType))
        {
            return false;
        }

        var size = extract.SimdSize;
        var type = Compiler.GetSimdTypeForSize(size);
        var indices = CompilerInstance.gtNewVconNode(type);
        var sentinel = CompilerInstance.gtNewVconNode(type);
        for (var index = 0; index < size / baseType.Size; index++)
        {
            var selected = trailing ? index + 1 : 31 - index;
            var other = trailing ? 33 : 32;
            switch (baseType)
            {
                case TYP_UBYTE:
                {
                    indices.SimdVal.u8[index] = (byte)selected;
                    sentinel.SimdVal.u8[index] = (byte)other;
                    break;
                }
                case TYP_USHORT:
                {
                    indices.SimdVal.u16[index] = (ushort)selected;
                    sentinel.SimdVal.u16[index] = (ushort)other;
                    break;
                }
                case TYP_UINT:
                {
                    indices.SimdVal.u32[index] = (uint)selected;
                    sentinel.SimdVal.u32[index] = (uint)other;
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
        }

        var value = extract.GetOp(1);
        BlockRange.InsertAfter(value, indices);
        BlockRange.InsertAfter(indices, sentinel);
        var select = CompilerInstance.gtNewSimdCndSelNode(type, value, indices, sentinel, baseType, size);
        BlockRange.InsertAfter(sentinel, select);
        var reduction = CreateHWIntrinsicCmpMaskReduction(select, NI_AdvSimd_Arm64_MinAcross,
            NI_AdvSimd_MinPairwise, baseType, size);
        ScalarizeHWIntrinsicCmpMaskReduction(extract, reduction, baseType);

        var cast = CompilerInstance.gtNewCastNode(TYP_INT, extract, true, TYP_INT);
        BlockRange.InsertAfter(extract, cast);
        GenTree result = cast;
        if (trailing)
        {
            var one = CompilerInstance.gtNewIconNode(TYP_INT, 1);
            result = new GenTreeOp(GT_SUB, TYP_INT, cast, one);
            BlockRange.InsertAfter(cast, one, result);
        }
        BlockRange.Remove(zeroCount);
        ReplaceUse(ref use, parents, zeroCount, result);
        return true;
    }

    private void RewriteHWIntrinsicExtractMsbArm64(GenTreeHWIntrinsic node, GenTreeStack parents, ref GenTree use)
    {
        var baseType = node.SimdBaseType;
        var size = node.SimdSize;
        var vectorType = Compiler.GetSimdTypeForSize(size);
        var value = node.GetOp(1);
        var mask = CompilerInstance.gtNewVconNode(vectorType);
        var shifts = CompilerInstance.gtNewVconNode(vectorType);

        switch (baseType)
        {
            case TYP_BYTE:
            case TYP_UBYTE:
            {
                baseType = TYP_UBYTE;
                mask.SimdVal.u64[0] = 0x8080808080808080;
                shifts.SimdVal.u64[0] = 0x00FFFEFDFCFBFAF9;
                if (size == 16)
                {
                    mask.SimdVal.u64[1] = 0x8080808080808080;
                    shifts.SimdVal.u64[1] = 0x00FFFEFDFCFBFAF9;
                }
                break;
            }
            case TYP_SHORT:
            case TYP_USHORT:
            {
                baseType = TYP_USHORT;
                mask.SimdVal.u64[0] = 0x8000800080008000;
                shifts.SimdVal.u64[0] = 0xFFF4FFF3FFF2FFF1;
                if (size == 16)
                {
                    mask.SimdVal.u64[1] = 0x8000800080008000;
                    shifts.SimdVal.u64[1] = 0xFFF8FFF7FFF6FFF5;
                }
                break;
            }
            case TYP_INT:
            case TYP_UINT:
            case TYP_FLOAT:
            {
                baseType = TYP_INT;
                mask.SimdVal.u64[0] = 0x8000000080000000;
                shifts.SimdVal.u64[0] = 0xFFFFFFE2FFFFFFE1;
                if (size == 16)
                {
                    mask.SimdVal.u64[1] = 0x8000000080000000;
                    shifts.SimdVal.u64[1] = 0xFFFFFFE4FFFFFFE3;
                }
                break;
            }
            case TYP_LONG:
            case TYP_ULONG:
            case TYP_DOUBLE:
            {
                baseType = TYP_LONG;
                mask.SimdVal.u64[0] = 0x8000000000000000;
                shifts.SimdVal.u64[0] = 0xFFFFFFFFFFFFFFC1;
                if (size == 16)
                {
                    mask.SimdVal.u64[1] = 0x8000000000000000;
                    shifts.SimdVal.u64[1] = 0xFFFFFFFFFFFFFFC2;
                }
                break;
            }
            default:
            {
                unreached();
                break;
            }
        }

        BlockRange.InsertAfter(value, mask);
        value = CompilerInstance.gtNewSimdBinOpNode(GT_AND, vectorType, value, mask, baseType, size);
        BlockRange.InsertAfter(mask, value);

        var shiftIntrinsic = ((size == 8) && varTypeIsLong(baseType))
            ? NI_AdvSimd_ShiftLogicalScalar : NI_AdvSimd_ShiftLogical;
        BlockRange.InsertAfter(value, shifts);
        value = CompilerInstance.gtNewSimdHWIntrinsicNode(vectorType, shiftIntrinsic, baseType, size, value, shifts);
        BlockRange.InsertAfter(shifts, value);

        if (varTypeIsByte(baseType) && (size == 16))
        {
            LIR.Use.MakeDummyUse(BlockRange, value, out var dummy);
            _ = dummy.ReplaceWithLclVar(CompilerInstance);
            value = dummy.Def() ?? throw new InvalidOperationException("Spilled vector operand has no definition.");
            var other = CompilerInstance.gtClone(value)
                ?? throw new InvalidOperationException("Failed to clone vector operand.");
            BlockRange.InsertAfter(value, other);

            var upper = CompilerInstance.gtNewSimdHWIntrinsicNode(vectorType,
                NI_AdvSimd_ZeroExtendWideningUpper, baseType, 16, value);
            BlockRange.InsertBefore(other, upper);
            var eight = CompilerInstance.gtNewIconNode(TYP_INT, 8);
            BlockRange.InsertBefore(other, eight);
            var shifted = CompilerInstance.gtNewSimdBinOpNode(GT_LSH, vectorType, upper, eight, TYP_USHORT, size);
            BlockRange.InsertBefore(other, shifted);
            var lower = CompilerInstance.gtNewSimdGetLowerNode(TYP_SIMD8, other, baseType, 16);
            BlockRange.InsertAfter(other, lower);
            value = CompilerInstance.gtNewSimdHWIntrinsicNode(vectorType,
                NI_AdvSimd_AddWideningLower, TYP_USHORT, 8, shifted, lower);
            BlockRange.InsertAfter(lower, value);
            baseType = TYP_USHORT;
        }

        if (!varTypeIsLong(baseType))
        {
            if ((size == 8) && (baseType is TYP_INT or TYP_UINT))
            {
                LIR.Use.MakeDummyUse(BlockRange, value, out var dummy);
                _ = dummy.ReplaceWithLclVar(CompilerInstance);
                value = dummy.Def() ?? throw new InvalidOperationException("Spilled vector operand has no definition.");
                var other = CompilerInstance.gtClone(value)
                    ?? throw new InvalidOperationException("Failed to clone vector operand.");
                BlockRange.InsertAfter(value, other);
                value = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD8,
                    NI_AdvSimd_AddPairwise, baseType, size, value, other);
                BlockRange.InsertAfter(other, value);
            }
            else
            {
                var reduction = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD8,
                    NI_AdvSimd_Arm64_AddAcross, baseType, size, value);
                BlockRange.InsertAfter(value, reduction);
                value = reduction;
            }
        }
        else if (size == 16)
        {
            var reduction = CompilerInstance.gtNewSimdHWIntrinsicNode(TYP_SIMD8,
                NI_AdvSimd_Arm64_AddPairwiseScalar, baseType, size, value);
            BlockRange.InsertAfter(value, reduction);
            value = reduction;
        }

        node.Type = baseType.ActualType;
        node.ChangeHWIntrinsicId(NI_Vector_ToScalar);
        node.SimdSize = 8;
        node.SimdBaseType = baseType;
        node.SetOp(1, value);

        if (baseType is not (TYP_INT or TYP_UINT))
        {
            var cast = CompilerInstance.gtNewCastNode(TYP_INT, node, true, TYP_INT);
            BlockRange.InsertAfter(node, cast);
            ReplaceUse(ref use, parents, node, cast);
        }
    }
}
#endif
