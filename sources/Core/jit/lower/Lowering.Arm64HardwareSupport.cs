// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool _ffrTrashed;

    private GenTree? LowerCnsMask(GenTreeMskCon mask)
    {
#if DEBUG
        if (JitConfig.JitUseScalableVectorT != 0)
        {
            throw new FatalJitException(CORJIT_IMPLLIMITATION, "ARM64 scalable mask lowering requires scalable representation.");
        }
#endif

        if (mask.IsZero ||
            (EvaluateSimdMaskToPattern<simd16_t>(TYP_BYTE, mask.SimdMaskVal) != SveMaskPattern.SveMaskPatternNone) ||
            (EvaluateSimdMaskToPattern<simd16_t>(TYP_SHORT, mask.SimdMaskVal) != SveMaskPattern.SveMaskPatternNone) ||
            (EvaluateSimdMaskToPattern<simd16_t>(TYP_INT, mask.SimdMaskVal) != SveMaskPattern.SveMaskPatternNone) ||
            (EvaluateSimdMaskToPattern<simd16_t>(TYP_LONG, mask.SimdMaskVal) != SveMaskPattern.SveMaskPatternNone))
        {
            return mask.Next;
        }

        LABELEDDISPTREERANGE("lowering cns mask to cns vector (before)", BlockRange(), mask);

        // Predicates cannot be loaded PC-relative. Byte conversion preserves every predicate bit.
        var vector = CompilerInstance.gtNewVconNode(TYP_SIMD16);
        EvaluateSimdCvtMaskToVector(TYP_BYTE, vector.SimdVal.AsSpan<byte>()[..16], mask.SimdMaskVal);
        BlockRange().InsertBefore(mask, vector);

        var converted = CompilerInstance.gtNewSimdCvtVectorToMaskNode(TYP_MASK, vector, TYP_BYTE, 16);
        BlockRange().InsertBefore(mask, converted.AsHWIntrinsic().GetOp(1));
        BlockRange().InsertBefore(mask, converted);
        if (BlockRange().TryGetUse(mask, out var use))
        {
            use.ReplaceWith(converted);
        }
        else
        {
            converted.IsUnusedValue = true;
        }

        BlockRange().Remove(mask);
        LABELEDDISPTREERANGE("lowering cns mask to cns vector (after)", BlockRange(), vector);
        return vector.Next;
    }

    private GenTree? LowerHWIntrinsicCndSel(GenTreeHWIntrinsic node)
    {
        var id = node.HWIntrinsicId;
        assert(HWIntrinsicInfo.IsSveConditionalSelect(id));
        var mask = node.GetOp(1);
        var trueValue = node.GetOp(2);
        var falseValue = node.GetOp(3);

        if (trueValue.Oper.IsHWIntrinsic && (trueValue.AsHWIntrinsic().HWIntrinsicId == id))
        {
            var nested = trueValue.AsHWIntrinsic();
            var nestedMask = nested.GetOp(1);
            var nestedValue = nested.GetOp(2);
            assert(varTypeIsMask(nestedMask.Type));

            if (nestedValue.Oper.IsHWIntrinsic)
            {
                var nestedId = nestedValue.AsHWIntrinsic().HWIntrinsicId;
                if (nestedMask.IsTrueMask(node.SimdBaseType) &&
                    !HWIntrinsicInfo.IsReduceOperation(nestedId) &&
                    (!HWIntrinsicInfo.IsZeroingMaskedOperation(nestedId) || falseValue.IsZeroForSelect))
                {
                    LABELEDDISPTREERANGE("Removed nested conditionalselect (before)", BlockRange(), node);
                    node.SetOp(2, nestedValue);
                    nested.GetOp(3).IsUnusedValue = true;
                    BlockRange().Remove(nestedMask);
                    BlockRange().Remove(nested);
                    LABELEDDISPTREERANGE("Removed nested conditionalselect (after)", BlockRange(), node);
                    return node;
                }
            }
        }
        else if (mask.IsTrueMask(node.SimdBaseType))
        {
            if (!trueValue.Oper.IsHWIntrinsic ||
                !HWIntrinsicInfo.IsEmbeddedMaskedOperation(trueValue.AsHWIntrinsic().HWIntrinsicId))
            {
                LABELEDDISPTREERANGE("Lowered ConditionalSelect(True, op2, op3) to op2 (before)", BlockRange(), node);
                if (BlockRange().TryGetUse(node, out var use))
                {
                    use.ReplaceWith(trueValue);
                }
                else
                {
                    trueValue.IsUnusedValue = true;
                }

                falseValue.IsUnusedValue = true;
                mask.IsUnusedValue = true;
                var next = node.Next;
                BlockRange().Remove(node);
                LABELEDDISPTREERANGE("Lowered ConditionalSelect(True, op2, op3) to op2 (after)", BlockRange(), trueValue);
                return next;
            }
        }

        ContainCheckHWIntrinsic(node);
        return node.Next;
    }

    private void LowerHWIntrinsicFusedMultiplyAddScalar(GenTreeHWIntrinsic node)
    {
        assert(node.HWIntrinsicId is NI_AdvSimd_FusedMultiplyAddScalar);
        assert(varTypeIsFloating(node.SimdBaseType));

        bool LowerOperand(GenTree operand)
        {
            if (operand.Oper.IsHWIntrinsic)
            {
                var intrinsic = operand.AsHWIntrinsic();
                if ((intrinsic.HWIntrinsicId is NI_AdvSimd_Arm64_DuplicateToVector64) ||
                    ((intrinsic.HWIntrinsicId is NI_Vector_CreateScalarUnsafe) && (intrinsic.SimdSize == 8)))
                {
                    var value = intrinsic.GetOp(1);
                    if ((value.Oper is GT_NEG) && (value.Type == node.SimdBaseType))
                    {
                        intrinsic.SetOp(1, value.AsUnOp().Op1);
                        BlockRange().Remove(value);
                        return true;
                    }
                }
            }

            return false;
        }

        var firstNegated = LowerOperand(node.GetOp(1));
        var secondNegated = LowerOperand(node.GetOp(2));
        var thirdNegated = LowerOperand(node.GetOp(3));
        if (firstNegated)
        {
            node.ChangeHWIntrinsicId(secondNegated != thirdNegated
                ? NI_AdvSimd_FusedMultiplyAddNegatedScalar
                : NI_AdvSimd_FusedMultiplySubtractNegatedScalar);
        }
        else if (secondNegated != thirdNegated)
        {
            node.ChangeHWIntrinsicId(NI_AdvSimd_FusedMultiplySubtractScalar);
        }
    }

    private void StoreFFRValue(GenTreeHWIntrinsic node)
    {
        assert((node.HWIntrinsicId is NI_Sve_SetFfr) ||
            IsFfrIntrinsic(node.HWIntrinsicId));

        var local = CompilerInstance.getFFRegisterVarNum();
        var register = new GenTreePhysReg(REG_FFR, TYP_MASK);
        var store = CompilerInstance.gtNewStoreLclVarNode(local, register);
        BlockRange().InsertAfter(node, register, store);
        _ffrTrashed = false;
    }

    private static bool IsFfrIntrinsic(NamedIntrinsic id)
    {
        return id is
            NI_Sve_GatherVectorByteZeroExtendFirstFaulting or
            NI_Sve_GatherVectorFirstFaulting or
            NI_Sve_GatherVectorInt16SignExtendFirstFaulting or
            NI_Sve_GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting or
            NI_Sve_GatherVectorInt32SignExtendFirstFaulting or
            NI_Sve_GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting or
            NI_Sve_GatherVectorSByteSignExtendFirstFaulting or
            NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting or
            NI_Sve_GatherVectorUInt16ZeroExtendFirstFaulting or
            NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting or
            NI_Sve_GatherVectorUInt32ZeroExtendFirstFaulting or
            NI_Sve_GatherVectorWithByteOffsetFirstFaulting or
            NI_Sve_LoadVectorByteZeroExtendFirstFaulting or
            NI_Sve_LoadVectorFirstFaulting or
            NI_Sve_LoadVectorInt16SignExtendFirstFaulting or
            NI_Sve_LoadVectorInt32SignExtendFirstFaulting or
            NI_Sve_LoadVectorSByteSignExtendFirstFaulting or
            NI_Sve_LoadVectorUInt16ZeroExtendFirstFaulting or
            NI_Sve_LoadVectorUInt32ZeroExtendFirstFaulting or
            NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt16 or
            NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt32 or
            NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt64 or
            NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt16 or
            NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt32 or
            NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt64 or
            NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt32 or
            NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt64 or
            NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt32 or
            NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt64 or
            NI_Sve_LoadVectorInt32NonFaultingSignExtendToInt64 or
            NI_Sve_LoadVectorInt32NonFaultingSignExtendToUInt64 or
            NI_Sve_LoadVectorNonFaulting or
            NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt16 or
            NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt32 or
            NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt64 or
            NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt16 or
            NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt32 or
            NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt64 or
            NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt32 or
            NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt64 or
            NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt32 or
            NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt64 or
            NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToInt64 or
            NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToUInt64;
    }

    private bool TryContainingCselOp(GenTreeHWIntrinsic parent, GenTreeHWIntrinsic child)
    {
        if (!HWIntrinsicInfo.IsSveConditionalSelect(child.HWIntrinsicId))
        {
            return false;
        }

        if (child.GetOp(2).Oper.IsHWIntrinsic && ((child.GetOp(2).Flags & GTF_HW_EM_OP) != 0))
        {
            assert(child.GetOp(2).IsContained);
            return false;
        }

        var canContain = false;
        if (child.GetOp(3).IsVectorZero)
        {
            canContain = parent.HWIntrinsicId switch {
                NI_Sve_AddAcross or NI_Sve_OrAcross or NI_Sve_XorAcross => true,
                NI_Sve_MaxAcross => varTypeIsUnsigned(parent.SimdBaseType),
                _ => false,
            };

            if (canContain)
            {
                MakeSrcContained(child, child.GetOp(3));
                MakeSrcContained(parent, child);
            }
        }

        return canContain;
    }
}
#endif
