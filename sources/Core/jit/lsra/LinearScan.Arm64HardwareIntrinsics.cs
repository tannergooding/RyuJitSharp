// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if TARGET_ARM64 && FEATURE_HW_INTRINSICS
    private int buildHWIntrinsicArm64(GenTreeHWIntrinsic intrinsic, out int destinationCount)
    {
        var id = intrinsic.HWIntrinsicId;
        var category = HWIntrinsicInfo.lookupCategory(id);
        var operandCount = intrinsic.Operands.Length;
        var delayFreeOperand = getArm64DelayFreeOperand(intrinsic);
        var addressOperand = getArm64VectorAddress(intrinsic);
        var consecutiveOperand = getArm64ConsecutiveOperand(intrinsic, out var consecutiveDestination);
        GenTreeHWIntrinsic? embeddedOperand = null;
        if (HWIntrinsicInfo.IsSveConditionalSelect(id) &&
            ((intrinsic.GetOp(2).Flags & GTF_HW_EM_OP) != 0))
        {
            embeddedOperand = intrinsic.GetOp(2).AsHWIntrinsic();
            assert(delayFreeOperand is null);
            delayFreeOperand = getArm64DelayFreeOperand(embeddedOperand, intrinsic);
        }

        GenTreeHWIntrinsic? containedSelect = null;
        for (var index = 1; index <= operandCount; index++)
        {
            var candidate = intrinsic.GetOp(index);
            if (candidate.IsContained && candidate.Oper is GT_HWINTRINSIC &&
                HWIntrinsicInfo.IsSveConditionalSelect(candidate.AsHWIntrinsic().HWIntrinsicId))
            {
                containedSelect = candidate.AsHWIntrinsic();
                break;
            }
        }

        buildArm64IntrinsicImmediate(intrinsic, category);
        buildArm64IntrinsicTemporaries(intrinsic, embeddedOperand);
        var sourceCount = 0;
        for (var operandNumber = 1; operandNumber <= operandCount; operandNumber++)
        {
            var operand = intrinsic.GetOp(operandNumber);
            var candidates = getArm64IntrinsicCandidates(intrinsic, operandNumber, category);
            if (ReferenceEquals(operand, embeddedOperand))
            {
                assert(!ReferenceEquals(operand, addressOperand));
                sourceCount += buildArm64EmbeddedUses(embeddedOperand, delayFreeOperand);
            }
            else if (ReferenceEquals(operand, addressOperand))
            {
                sourceCount += buildAddrUses(operand, candidates);
            }
            else if (ReferenceEquals(operand, consecutiveOperand))
            {
                assert(candidates == SRBM_NONE);
                sourceCount += buildConsecutiveRegistersForUse(operand, delayFreeOperand);
            }
            else if (ReferenceEquals(operand, delayFreeOperand))
            {
                if (operand.IsContained)
                {
                    sourceCount += buildOperandUses(operand);
                }
                else
                {
                    var use = buildUse(operand, candidates);
                    sourceCount++;
                    switch (operandNumber)
                    {
                        case 1:
                        {
                            assert(_targetPreferredUse is null);
                            assert(_targetPreferredUse2 is null);
                            assert(_targetPreferredUse3 is null);
                            _targetPreferredUse = use;
                            break;
                        }

                        case 2:
                        {
                            assert(_targetPreferredUse is null);
                            assert(_targetPreferredUse2 is null);
                            assert(_targetPreferredUse3 is null);
                            _targetPreferredUse2 = use;
                            break;
                        }

                        case 3:
                        {
                            assert(_targetPreferredUse is null);
                            assert(_targetPreferredUse2 is null);
                            assert(_targetPreferredUse3 is null);
                            _targetPreferredUse3 = use;
                            break;
                        }

                        default:
                        {
                            throw new FatalJitException("ARM64 RMW operand position exceeds three.");
                        }
                    }
                }
            }
            else if (ReferenceEquals(operand, containedSelect))
            {
                for (var index = 1; index <= containedSelect.Operands.Length; index++)
                {
                    var containedOperand = containedSelect.GetOp(index);
                    sourceCount += varTypeUsesMaskReg(containedOperand.Type)
                        ? buildOperandUses(containedOperand, candidates)
                        : buildArm64DelayFreeUses(containedOperand, delayFreeOperand, candidates);
                }
            }
            else if ((category is HW_Category_SIMDByIndexedElement) &&
                (intrinsic.SimdBaseType.Size is 2) && !HWIntrinsicInfo.HasImmediateOperand(id))
            {
                if (operandNumber is 2 or 3)
                {
                    sourceCount += buildArm64DelayFreeUses(operand, null, candidates);
                }
                else
                {
                    sourceCount += buildOperandUses(operand, candidates);
                }
            }
            else if ((delayFreeOperand is not null) &&
                (varTypeUsesSameRegType(delayFreeOperand.Type, operand.Type) ||
                 (delayFreeOperand.IsMultiRegNode && varTypeUsesFloatReg(operand.Type))))
            {
                sourceCount += buildArm64DelayFreeUses(operand, delayFreeOperand, candidates);
            }
            else
            {
                sourceCount += buildOperandUses(operand, candidates);
            }
        }

        buildInternalRegisterUses();

        destinationCount = HWIntrinsicInfo.IsMultiReg(id)
            ? intrinsic.GetMultiRegCount(_compiler)
            : intrinsic.IsValue ? 1 : 0;
        if (consecutiveDestination)
        {
            buildConsecutiveRegistersForDef(intrinsic, destinationCount);
        }
        else if (destinationCount is 1 or 2)
        {
            _ = buildDef(intrinsic, SRBM_NONE);
            if (destinationCount == 2)
            {
                _ = buildDef(intrinsic, SRBM_NONE, 1);
            }
        }
        else
        {
            assert(destinationCount == 0);
        }
        return sourceCount;
    }

    private int buildArm64EmbeddedUses(GenTreeHWIntrinsic embedded, GenTree? delayFreeOperand)
    {
        assert((embedded.Flags & GTF_HW_EM_OP) != 0);
        assert(embedded.HWIntrinsicId is not NI_Sve_ConvertVectorToMask);
        buildArm64IntrinsicImmediate(embedded, HWIntrinsicInfo.lookupCategory(embedded.HWIntrinsicId));
        if (delayFreeOperand is null)
        {
            return buildOperandUses(embedded);
        }

        if (HWIntrinsicInfo.IsFmaIntrinsic(embedded.HWIntrinsicId))
        {
            assert(embedded.Operands.Length == 3);
            var blockSequence = _blockSequence
                ?? throw new FatalJitException("Embedded FMA allocation requires block sequence.");
            GenTree? user = null;
            if (blockSequence[_currentBlockSequenceNumber].TryGetUse(embedded, out var use))
            {
                user = use.User();
            }
            var resultOperand = embedded.GetResultOpNumForRmwIntrinsic(
                user, embedded.GetOp(1), embedded.GetOp(2), embedded.GetOp(3));
            if (resultOperand != 0)
            {
                delayFreeOperand = embedded.GetOp(resultOperand);
            }
        }

        var sourceCount = 0;
        for (var operandNumber = 1; operandNumber <= embedded.Operands.Length; operandNumber++)
        {
            var operand = embedded.GetOp(operandNumber);
            if (ReferenceEquals(operand, delayFreeOperand))
            {
                _targetPreferredUse = buildUse(operand);
                sourceCount++;
            }
            else
            {
                RefPosition? use = null;
                var count = buildDelayFreeUses(operand, null, SRBM_NONE, ref use);
                sourceCount += count;
                assert(((use is not null) && use.delayRegFree) || (count == 0));
            }
        }
        return sourceCount;
    }

    private void buildArm64IntrinsicTemporaries(GenTreeHWIntrinsic intrinsic, GenTreeHWIntrinsic? embedded)
    {
        switch (intrinsic.HWIntrinsicId)
        {
            case NI_Sve2_GatherVectorInt16SignExtendNonTemporal:
            case NI_Sve2_GatherVectorInt32SignExtendNonTemporal:
            case NI_Sve2_GatherVectorNonTemporal:
            case NI_Sve2_GatherVectorUInt16ZeroExtendNonTemporal:
            case NI_Sve2_GatherVectorUInt32ZeroExtendNonTemporal:
            case NI_Sve2_Scatter16BitNarrowingNonTemporal:
            case NI_Sve2_Scatter32BitNarrowingNonTemporal:
            case NI_Sve2_ScatterNonTemporal:
            {
                if (!varTypeIsSimd(intrinsic.GetOp(2).Type))
                {
                    _ = buildInternalFloatRegisterDefForNode(intrinsic, internalFloatRegCandidates());
                }
                break;
            }

            case NI_Sve_ConditionalSelect:
            {
                if (embedded?.HWIntrinsicId is NI_Sve_MultiplyAddRotateComplex)
                {
                    _ = buildInternalFloatRegisterDefForNode(intrinsic, internalFloatRegCandidates());
                    _setInternalRegistersDelayFree = true;
                }
                break;
            }

            case NI_Vector_Create:
            case NI_Vector_CreateScalarUnsafe:
            {
                if ((intrinsic.Type is TYP_SIMD) && varTypeIsFloating(intrinsic.SimdBaseType))
                {
                    _ = buildInternalIntRegisterDefForNode(intrinsic, _availableIntRegs);
                }
                break;
            }

            default:
            {
                break;
            }
        }
    }

    private int buildArm64DelayFreeUses(GenTree operand, GenTree? rmwOperand, SingleTypeRegSet candidates)
    {
        RefPosition? use = null;
        return buildDelayFreeUses(operand, rmwOperand, candidates, ref use);
    }

    private void buildArm64IntrinsicImmediate(GenTree intrinsic, HWIntrinsicCategory category)
    {
        var node = intrinsic.AsHWIntrinsic();
        var id = node.HWIntrinsicId;
        if (!HWIntrinsicInfo.HasImmediateOperand(id) ||
            ((HWIntrinsicInfo.lookupFlags(id) & HW_Flag_NoJmpTableIMM) != 0))
        {
            return;
        }

        var operandCount = node.Operands.Length;
        var simdSize = node.SimdSize;
        if (category is HW_Category_SIMDByIndexedElement)
        {
            var indexed = node.GetOp(operandCount - 1);
            simdSize = indexed.Type.Size;
        }
        HWIntrinsicInfo.lookupImmBounds(id, simdSize, node.SimdBaseType, 1, out var lower, out var upper);
        if ((lower == 0) && (upper == 1))
        {
            return;
        }

        GenTree? immediate;
        if (category is HW_Category_SIMDByIndexedElement or
            HW_Category_ShiftLeftByImmediate or HW_Category_ShiftRightByImmediate)
        {
            immediate = node.GetOp(operandCount);
        }
        else
        {
            immediate = id switch
            {
                NI_AdvSimd_DuplicateSelectedScalarToVector64 or
                NI_AdvSimd_DuplicateSelectedScalarToVector128 or
                NI_AdvSimd_Extract or NI_AdvSimd_Insert or NI_AdvSimd_InsertScalar or
                NI_AdvSimd_LoadAndInsertScalar or
                NI_AdvSimd_LoadAndInsertScalarVector64x2 or
                NI_AdvSimd_LoadAndInsertScalarVector64x3 or
                NI_AdvSimd_LoadAndInsertScalarVector64x4 or
                NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2 or
                NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3 or
                NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4 or
                NI_AdvSimd_Arm64_DuplicateSelectedScalarToVector128 => node.GetOp(2),

                NI_AdvSimd_ExtractVector64 or NI_AdvSimd_ExtractVector128 or
                NI_AdvSimd_StoreSelectedScalar or NI_AdvSimd_Arm64_StoreSelectedScalar or
                NI_Sha3_XorRotateRight or NI_Sve_AddRotateComplex or
                NI_Sve_Prefetch16Bit or NI_Sve_Prefetch32Bit or
                NI_Sve_Prefetch64Bit or NI_Sve_Prefetch8Bit or
                NI_Sve_ExtractVector or NI_Sve_TrigonometricMultiplyAddCoefficient =>
                    node.GetOp(3),

                NI_Sve_CreateTrueMaskByte or NI_Sve_CreateTrueMaskDouble or
                NI_Sve_CreateTrueMaskInt16 or NI_Sve_CreateTrueMaskInt32 or
                NI_Sve_CreateTrueMaskInt64 or NI_Sve_CreateTrueMaskSByte or
                NI_Sve_CreateTrueMaskSingle or NI_Sve_CreateTrueMaskUInt16 or
                NI_Sve_CreateTrueMaskUInt32 or NI_Sve_CreateTrueMaskUInt64 or
                NI_Sve_Count16BitElements or NI_Sve_Count32BitElements or
                NI_Sve_Count64BitElements or NI_Sve_Count8BitElements =>
                    node.GetOp(1),

                NI_Sve_ShiftRightArithmeticForDivide => node.GetOp(2),

                NI_Sve_GatherPrefetch8Bit or NI_Sve_GatherPrefetch16Bit or
                NI_Sve_GatherPrefetch32Bit or NI_Sve_GatherPrefetch64Bit =>
                    node.GetOp(varTypeIsSimd(node.GetOp(2).Type) ? 3 : 4),

                NI_Sve_SaturatingDecrementBy16BitElementCount or
                NI_Sve_SaturatingDecrementBy32BitElementCount or
                NI_Sve_SaturatingDecrementBy64BitElementCount or
                NI_Sve_SaturatingDecrementBy8BitElementCount or
                NI_Sve_SaturatingIncrementBy16BitElementCount or
                NI_Sve_SaturatingIncrementBy32BitElementCount or
                NI_Sve_SaturatingIncrementBy64BitElementCount or
                NI_Sve_SaturatingIncrementBy8BitElementCount or
                NI_Sve_SaturatingDecrementBy16BitElementCountScalar or
                NI_Sve_SaturatingDecrementBy32BitElementCountScalar or
                NI_Sve_SaturatingDecrementBy64BitElementCountScalar or
                NI_Sve_SaturatingIncrementBy16BitElementCountScalar or
                NI_Sve_SaturatingIncrementBy32BitElementCountScalar or
                NI_Sve_SaturatingIncrementBy64BitElementCountScalar =>
                    getPairedArm64Immediate(node, 2, 3),

                NI_Sve_MultiplyAddRotateComplexBySelectedScalar or
                NI_Sve2_MultiplyAddRotateComplexBySelectedScalar or
                NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar or
                NI_Sve2_DotProductRotateComplexBySelectedIndex =>
                    getPairedArm64Immediate(node, 4, 5),

                NI_Sve_MultiplyAddRotateComplex or NI_Sve2_MultiplyAddRotateComplex or
                NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplex or
                NI_Sve2_DotProductRotateComplex => node.GetOp(4),

                NI_AdvSimd_Arm64_InsertSelectedScalar =>
                    getContainedArm64InsertImmediate(node),
                _ => throw new FatalJitException($"ARM64 intrinsic {id} has unhandled immediate requirements."),
            };
        }

        if ((immediate is not null) && !immediate.IsContainedIntOrIImmed)
        {
            _ = buildInternalIntRegisterDefForNode(intrinsic, _availableIntRegs);
        }
    }

    private GenTree getPairedArm64Immediate(GenTreeHWIntrinsic intrinsic, int first, int second)
    {
        var firstOperand = intrinsic.GetOp(first);
        var secondOperand = intrinsic.GetOp(second);
        assert(firstOperand.IsContainedIntOrIImmed == secondOperand.IsContainedIntOrIImmed);
        _setInternalRegistersDelayFree = true;
        return firstOperand;
    }

    private static GenTree? getContainedArm64InsertImmediate(GenTreeHWIntrinsic intrinsic)
    {
        assert(intrinsic.GetOp(2).IsContainedIntOrIImmed);
        assert(intrinsic.GetOp(4).IsContainedIntOrIImmed);
        return null;
    }

    private GenTree? getArm64DelayFreeOperand(GenTreeHWIntrinsic intrinsic)
        => getArm64DelayFreeOperand(intrinsic, null);

    private GenTree? getArm64DelayFreeOperand(GenTreeHWIntrinsic intrinsic, GenTreeHWIntrinsic? user)
    {
        var id = intrinsic.HWIntrinsicId;
        var isRmw = intrinsic.IsRmwHWIntrinsic(_compiler);
        switch (id)
        {
            case NI_Vector_CreateScalarUnsafe:
            {
                return varTypeIsFloating(intrinsic.GetOp(1).Type) ? intrinsic.GetOp(1) : null;
            }

            case NI_AdvSimd_Arm64_DuplicateToVector64:
            {
                return intrinsic.GetOp(1).Type is TYP_DOUBLE ? intrinsic.GetOp(1) : null;
            }

            case NI_Vector_ToScalar:
            {
                return varTypeIsFloating(intrinsic.Type) ? intrinsic.GetOp(1) : null;
            }

            case NI_Vector_ToVector128Unsafe:
            case NI_Vector_AsVector128Unsafe:
            case NI_Vector_AsVector3:
            case NI_Vector_GetLower:
            {
                return intrinsic.GetOp(1);
            }

            case NI_Sve_CreateBreakPropagateMask:
            case NI_Sve2_BitwiseSelect:
            case NI_Sve2_BitwiseSelectLeftInverted:
            case NI_Sve2_BitwiseSelectRightInverted:
            {
                assert(isRmw);
                return intrinsic.GetOp(2);
            }

            case NI_Sve2_AddCarryWideningEven:
            case NI_Sve2_AddCarryWideningOdd:
            {
                assert(isRmw);
                return intrinsic.GetOp(3);
            }

            case NI_Sve2_ConvertToSingleOdd:
            case NI_Sve2_ConvertToSingleOddRoundToOdd:
            {
                assert(isRmw);
                if ((user?.HWIntrinsicId is NI_Sve_ConditionalSelect) &&
                    user.GetOp(3).IsVectorZero &&
                    !user.GetOp(1).IsTrueMask(user.SimdBaseType))
                {
                    return user.GetOp(3);
                }
                return intrinsic.GetOp(1);
            }

            default:
            {
                if (isRmw)
                {
                    if (HWIntrinsicInfo.IsExplicitMaskedOperation(id))
                    {
                        return intrinsic.GetOp(2);
                    }
                    if (HWIntrinsicInfo.IsOptionalEmbeddedMaskedOperation(id))
                    {
                        return user is not null ? intrinsic.GetOp(1) : null;
                    }
                    return intrinsic.GetOp(1);
                }
                if ((intrinsic.Operands.Length == 1) && (user is not null) &&
                    !HWIntrinsicInfo.IsReduceOperation(id))
                {
                    assert(user.HWIntrinsicId is NI_Sve_ConditionalSelect);
                    return user.GetOp(1).IsTrueMask(user.SimdBaseType) &&
                        user.GetOp(3).IsVectorZero
                        ? intrinsic.GetOp(1)
                        : user.GetOp(3);
                }
                return null;
            }
        }
    }

    private static GenTree? getArm64VectorAddress(GenTreeHWIntrinsic intrinsic)
    {
        if (intrinsic.IsMemoryLoad(out var address) || intrinsic.IsMemoryStore(out address))
        {
            return address;
        }

        if (intrinsic.HWIntrinsicId is
            NI_Sve_GatherPrefetch8Bit or NI_Sve_GatherPrefetch16Bit or
            NI_Sve_GatherPrefetch32Bit or NI_Sve_GatherPrefetch64Bit or
            NI_Sve_Prefetch16Bit or NI_Sve_Prefetch32Bit or
            NI_Sve_Prefetch64Bit or NI_Sve_Prefetch8Bit)
        {
            var operand = intrinsic.GetOp(2);
            return varTypeIsSimd(operand.Type) ? null : operand;
        }
        return null;
    }

    private static SingleTypeRegSet getArm64IntrinsicCandidates(
        GenTreeHWIntrinsic intrinsic, int operandNumber, HWIntrinsicCategory category)
    {
        var id = intrinsic.HWIntrinsicId;
        var operand = intrinsic.GetOp(operandNumber);
        if (HWIntrinsicInfo.IsLowVectorOperation(id))
        {
            assert(!varTypeUsesMaskReg(operand.Type));
            var restrict = id switch
            {
                NI_Sve_DotProductBySelectedScalar or
                NI_Sve_FusedMultiplyAddBySelectedScalar or
                NI_Sve_FusedMultiplySubtractBySelectedScalar or
                NI_Sve_MultiplyAddRotateComplexBySelectedScalar or
                NI_Sve2_DotProductRotateComplexBySelectedIndex or
                NI_Sve2_MultiplyAddBySelectedScalar or
                NI_Sve2_MultiplyAddRotateComplexBySelectedScalar or
                NI_Sve2_MultiplyBySelectedScalarWideningEvenAndAdd or
                NI_Sve2_MultiplyBySelectedScalarWideningOddAndAdd or
                NI_Sve2_MultiplySubtractBySelectedScalar or
                NI_Sve2_MultiplyBySelectedScalarWideningEvenAndSubtract or
                NI_Sve2_MultiplyBySelectedScalarWideningOddAndSubtract or
                NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateEven or
                NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateOdd or
                NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateEven or
                NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateOdd or
                NI_Sve2_MultiplyRoundedDoublingSaturateBySelectedScalarAndAddHigh or
                NI_Sve2_MultiplyRoundedDoublingSaturateBySelectedScalarAndSubtractHigh or
                NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar =>
                    operandNumber == 3,

                NI_Sve_MultiplyBySelectedScalar or
                NI_Sve2_MultiplyBySelectedScalar or
                NI_Sve2_MultiplyBySelectedScalarWideningEven or
                NI_Sve2_MultiplyBySelectedScalarWideningOdd or
                NI_Sve2_MultiplyDoublingBySelectedScalarSaturateHigh or
                NI_Sve2_MultiplyDoublingWideningSaturateEvenBySelectedScalar or
                NI_Sve2_MultiplyDoublingWideningSaturateOddBySelectedScalar or
                NI_Sve2_MultiplyRoundedDoublingBySelectedScalarSaturateHigh =>
                    operandNumber == 2,

                _ => throw new FatalJitException($"Unhandled ARM64 low-vector intrinsic {id}."),
            };
            if (restrict)
            {
                var elementSize = id is NI_Sve2_DotProductRotateComplexBySelectedIndex
                    ? intrinsic.SimdBaseType is TYP_BYTE ? 4 : 8
                    : intrinsic.SimdBaseType.Size;
                var lowEight = SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V3 |
                    SRBM_V4 | SRBM_V5 | SRBM_V6 | SRBM_V7;
                return elementSize == 8
                    ? lowEight | SRBM_V8 | SRBM_V9 | SRBM_V10 | SRBM_V11 |
                        SRBM_V12 | SRBM_V13 | SRBM_V14 | SRBM_V15
                    : lowEight;
            }
            return SRBM_NONE;
        }
        else if ((category is HW_Category_SIMDByIndexedElement) &&
            (intrinsic.SimdBaseType.Size is 2))
        {
            if (id is NI_Sve_DuplicateSelectedScalarToVector)
            {
                return SRBM_NONE;
            }
            var restrictedOperand = intrinsic.Operands.Length is 4 ||
                ((intrinsic.Operands.Length is 3) && !HWIntrinsicInfo.HasImmediateOperand(id))
                ? 3 : 2;
            if (operandNumber == restrictedOperand)
            {
                return SRBM_V0 | SRBM_V1 | SRBM_V2 | SRBM_V3 |
                    SRBM_V4 | SRBM_V5 | SRBM_V6 | SRBM_V7 |
                    SRBM_V8 | SRBM_V9 | SRBM_V10 | SRBM_V11 |
                    SRBM_V12 | SRBM_V13 | SRBM_V14 | SRBM_V15;
            }
        }
        else if (varTypeUsesMaskReg(operand.Type))
        {
            if ((operandNumber == 1) && HWIntrinsicInfo.IsLowMaskedOperation(id))
            {
                return SRBM_LOWMASK;
            }
            return SRBM_ALLMASK;
        }
        return SRBM_NONE;
    }

    private static GenTree? getArm64ConsecutiveOperand(
        GenTreeHWIntrinsic intrinsic, out bool consecutiveDestination)
    {
        consecutiveDestination = false;
        if (!HWIntrinsicInfo.NeedsConsecutiveRegisters(intrinsic.HWIntrinsicId))
        {
            return null;
        }

        switch (intrinsic.HWIntrinsicId)
        {
            case NI_AdvSimd_Arm64_VectorTableLookup:
            case NI_AdvSimd_VectorTableLookup:
            case NI_Sve2_VectorTableLookup:
            {
                return intrinsic.GetOp(1);
            }

            case NI_AdvSimd_Arm64_Store:
            case NI_AdvSimd_Arm64_StoreVectorAndZip:
            case NI_AdvSimd_Arm64_VectorTableLookupExtension:
            case NI_AdvSimd_Store:
            case NI_AdvSimd_StoreVectorAndZip:
            case NI_AdvSimd_VectorTableLookupExtension:
            {
                return intrinsic.GetOp(2);
            }

            case NI_AdvSimd_StoreSelectedScalar:
            case NI_AdvSimd_Arm64_StoreSelectedScalar:
            {
                return intrinsic.GetOp(2).Type is TYP_STRUCT ? intrinsic.GetOp(2) : null;
            }

            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
            case NI_AdvSimd_LoadAndInsertScalarVector64x2:
            case NI_AdvSimd_LoadAndInsertScalarVector64x3:
            case NI_AdvSimd_LoadAndInsertScalarVector64x4:
            {
                consecutiveDestination = true;
                return intrinsic.GetOp(1);
            }

            case NI_Sve_StoreAndZipx2:
            case NI_Sve_StoreAndZipx3:
            case NI_Sve_StoreAndZipx4:
            {
                return intrinsic.GetOp(3);
            }

            case NI_AdvSimd_Load2xVector64AndUnzip:
            case NI_AdvSimd_Load3xVector64AndUnzip:
            case NI_AdvSimd_Load4xVector64AndUnzip:
            case NI_AdvSimd_Arm64_Load2xVector128AndUnzip:
            case NI_AdvSimd_Arm64_Load3xVector128AndUnzip:
            case NI_AdvSimd_Arm64_Load4xVector128AndUnzip:
            case NI_AdvSimd_Load2xVector64:
            case NI_AdvSimd_Load3xVector64:
            case NI_AdvSimd_Load4xVector64:
            case NI_AdvSimd_Arm64_Load2xVector128:
            case NI_AdvSimd_Arm64_Load3xVector128:
            case NI_AdvSimd_Arm64_Load4xVector128:
            case NI_AdvSimd_LoadAndReplicateToVector64x2:
            case NI_AdvSimd_LoadAndReplicateToVector64x3:
            case NI_AdvSimd_LoadAndReplicateToVector64x4:
            case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x2:
            case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x3:
            case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x4:
            case NI_Sve_Load2xVectorAndUnzip:
            case NI_Sve_Load3xVectorAndUnzip:
            case NI_Sve_Load4xVectorAndUnzip:
            {
                consecutiveDestination = true;
                return null;
            }

            default:
            {
                throw new FatalJitException(
                    $"ARM64 intrinsic {intrinsic.HWIntrinsicId} has unhandled consecutive register requirements.");
            }
        }
    }
#endif
}
