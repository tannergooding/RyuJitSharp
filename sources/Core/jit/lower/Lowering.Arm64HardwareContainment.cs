// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private static bool IsValidConstForMovImm(GenTreeHWIntrinsic node)
    {
        var id = node.HWIntrinsicId;
        assert((id is NI_Vector_Create or NI_Vector_CreateScalar or NI_Vector_CreateScalarUnsafe or
            NI_AdvSimd_DuplicateToVector64 or NI_AdvSimd_DuplicateToVector128 or
            NI_AdvSimd_Arm64_DuplicateToVector64 or NI_AdvSimd_Arm64_DuplicateToVector128) &&
            (node.Operands.Length == 1));

        var operand = node.GetOp(1);
        if (operand.Oper.IsCnsIntOrI)
        {
            return Emitter.emitIns_valid_imm_for_movi(operand.AsIntCon().IconValue,
                node.SimdBaseType.EmitActualSize);
        }

        if (operand.Oper.IsCnsFltOrDbl)
        {
            assert(varTypeIsFloating(node.SimdBaseType));
            return Emitter.emitIns_valid_imm_for_fmov(operand.AsDblCon().DconVal);
        }

        return false;
    }

    private void ContainCheckHWIntrinsic(GenTreeHWIntrinsic node)
    {
        var id = node.HWIntrinsicId;
        var category = HWIntrinsicInfo.lookupCategory(id);
        var operandCount = node.Operands.Length;
        var hasImmediate = HWIntrinsicInfo.HasImmediateOperand(id);

        void ContainImmediate(int index)
        {
            var operand = node.GetOp(index);
            assert(varTypeIsIntegral(operand.Type));
            if (operand.Oper.IsCnsIntOrI)
            {
                MakeSrcContained(node, operand);
            }
        }

        if ((category is HW_Category_ShiftLeftByImmediate or HW_Category_ShiftRightByImmediate) ||
            ((category is HW_Category_SIMDByIndexedElement) && hasImmediate))
        {
            assert(operandCount is 2 or 3 or 4);
            if (operandCount is not (2 or 3 or 4))
            {
                throw new InvalidOperationException("Unexpected ARM64 immediate intrinsic operand count.");
            }

            ContainImmediate(operandCount);
            return;
        }

        if (!hasImmediate && !HWIntrinsicInfo.SupportsContainment(id))
        {
            return;
        }

        switch (id)
        {
            case NI_AdvSimd_DuplicateSelectedScalarToVector64:
            case NI_AdvSimd_DuplicateSelectedScalarToVector128:
            case NI_AdvSimd_Extract:
            case NI_AdvSimd_InsertScalar:
            case NI_AdvSimd_LoadAndInsertScalar:
            case NI_AdvSimd_LoadAndInsertScalarVector64x2:
            case NI_AdvSimd_LoadAndInsertScalarVector64x3:
            case NI_AdvSimd_LoadAndInsertScalarVector64x4:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
            case NI_AdvSimd_Arm64_DuplicateSelectedScalarToVector128:
            case NI_Sve_DuplicateSelectedScalarToVector:
            case NI_AdvSimd_Insert:
            {
                assert(hasImmediate);
                ContainImmediate(2);
                break;
            }

            case NI_AdvSimd_ExtractVector64:
            case NI_AdvSimd_ExtractVector128:
            case NI_AdvSimd_StoreSelectedScalar:
            case NI_AdvSimd_Arm64_StoreSelectedScalar:
            case NI_Sve_Prefetch16Bit:
            case NI_Sve_Prefetch32Bit:
            case NI_Sve_Prefetch64Bit:
            case NI_Sve_Prefetch8Bit:
            case NI_Sve_ExtractVector:
            case NI_Sve_AddRotateComplex:
            case NI_Sve_TrigonometricMultiplyAddCoefficient:
            case NI_Sve2_ShiftLeftAndInsert:
            case NI_Sve2_AddRotateComplex:
            case NI_Sve2_AddSaturateRotateComplex:
            case NI_Sha3_XorRotateRight:
            {
                assert(hasImmediate);
                ContainImmediate(3);
                break;
            }

            case NI_AdvSimd_Arm64_InsertSelectedScalar:
            {
                assert(hasImmediate);
                var first = node.GetOp(2);
                var second = node.GetOp(4);
                assert(first.Oper.IsCnsIntOrI && second.Oper.IsCnsIntOrI);
                MakeSrcContained(node, first);
                MakeSrcContained(node, second);
                break;
            }

            case NI_AdvSimd_CompareEqual:
            case NI_AdvSimd_Arm64_CompareEqual:
            case NI_AdvSimd_Arm64_CompareEqualScalar:
            {
                var left = node.GetOp(1);
                var right = node.GetOp(2);
                if (left.IsVectorZero)
                {
                    assert(HWIntrinsicInfo.IsCommutative(id));
                    MakeSrcContained(node, left);
                    node.SetOp(1, right);
                    node.SetOp(2, left);
                }
                else if (right.IsVectorZero)
                {
                    MakeSrcContained(node, right);
                }

                break;
            }

            case NI_AdvSimd_CompareGreaterThan:
            case NI_AdvSimd_CompareGreaterThanOrEqual:
            case NI_AdvSimd_Arm64_CompareGreaterThan:
            case NI_AdvSimd_Arm64_CompareGreaterThanOrEqual:
            case NI_AdvSimd_Arm64_CompareGreaterThanScalar:
            case NI_AdvSimd_Arm64_CompareGreaterThanOrEqualScalar:
            case NI_AdvSimd_CompareLessThan:
            case NI_AdvSimd_CompareLessThanOrEqual:
            case NI_AdvSimd_Arm64_CompareLessThan:
            case NI_AdvSimd_Arm64_CompareLessThanOrEqual:
            case NI_AdvSimd_Arm64_CompareLessThanScalar:
            case NI_AdvSimd_Arm64_CompareLessThanOrEqualScalar:
            {
                // Unsigned CMHI/CMHS have no immediate-zero form.
                var right = node.GetOp(2);
                if (right.IsVectorZero && !varTypeIsUnsigned(node.SimdBaseType))
                {
                    MakeSrcContained(node, right);
                }

                break;
            }

            case NI_Vector_CreateScalarUnsafe:
            case NI_AdvSimd_DuplicateToVector64:
            case NI_AdvSimd_DuplicateToVector128:
            case NI_AdvSimd_Arm64_DuplicateToVector64:
            case NI_AdvSimd_Arm64_DuplicateToVector128:
            {
                if (IsValidConstForMovImm(node))
                {
                    MakeSrcContained(node, node.GetOp(1));
                }

                break;
            }

            case NI_Vector_GetElement:
            {
                var vector = node.GetOp(1);
                var index = node.GetOp(2);
                assert(!IsContainableMemoryOp(vector) || !IsSafeToContainMem(node, vector));
                assert(index.Oper.IsConst);
                MakeSrcContained(node, index);
                break;
            }

            case NI_Sve_CreateTrueMaskByte:
            case NI_Sve_CreateTrueMaskDouble:
            case NI_Sve_CreateTrueMaskInt16:
            case NI_Sve_CreateTrueMaskInt32:
            case NI_Sve_CreateTrueMaskInt64:
            case NI_Sve_CreateTrueMaskSByte:
            case NI_Sve_CreateTrueMaskSingle:
            case NI_Sve_CreateTrueMaskUInt16:
            case NI_Sve_CreateTrueMaskUInt32:
            case NI_Sve_CreateTrueMaskUInt64:
            case NI_Sve_Count16BitElements:
            case NI_Sve_Count32BitElements:
            case NI_Sve_Count64BitElements:
            case NI_Sve_Count8BitElements:
            {
                assert(hasImmediate);
                ContainImmediate(1);
                break;
            }

            case NI_Sve_ConditionalSelect:
            case NI_Sve_ConditionalSelect_Predicates:
            {
                assert(operandCount == 3);
                var mask = node.GetOp(1);
                var trueValue = node.GetOp(2);
                var falseValue = node.GetOp(3);
                if (mask.IsMaskZero)
                {
                    MakeSrcContained(node, mask);
                    LABELEDDISPTREERANGE("Contained false mask op1 in ConditionalSelect", BlockRange(), mask);
                }

                if (trueValue.Oper.IsHWIntrinsic && ((trueValue.Flags & GTF_HW_EM_OP) == 0))
                {
                    var operation = trueValue.AsHWIntrinsic();
                    if (IsInvariantInRange(operation, node) && operation.IsEmbeddedMaskingCompatible())
                    {
                        var maskSize = node.SimdBaseType.Size;
                        var operationSize = operation.SimdBaseType.Size;
                        var contain = maskSize == operationSize;

                        if (!contain)
                        {
                            assert(operation.HWIntrinsicId is NI_Sve_ConvertToDouble or NI_Sve_ConvertToInt32 or
                                NI_Sve_ConvertToInt64 or NI_Sve_ConvertToSingle or NI_Sve_ConvertToUInt32 or
                                NI_Sve_ConvertToUInt64);
                            contain = maskSize == operation.AuxiliaryType.Size;
                        }

                        if (contain)
                        {
                            MakeSrcContained(node, operation);
                            operation.Flags |= GTF_HW_EM_OP;
                            LABELEDDISPTREERANGE("Contained op2 in ConditionalSelect", BlockRange(), node);
                        }
                    }

                    if (operation.HWIntrinsicId is NI_Sve_ShiftRightArithmeticForDivide)
                    {
                        assert(operation.Operands.Length == 2);
                        if (operation.GetOp(2).Oper.IsCnsIntOrI)
                        {
                            MakeSrcContained(operation, operation.GetOp(2));
                            LABELEDDISPTREERANGE("Contained ShiftRight in ConditionalSelect", BlockRange(), operation);
                        }
                    }
                }

                if (falseValue.IsZeroForSelect && trueValue.Oper.IsHWIntrinsic &&
                    ((trueValue.Flags & GTF_HW_EM_OP) != 0))
                {
                    switch (trueValue.AsHWIntrinsic().HWIntrinsicId)
                    {
                        case NI_Sve2_AddPairwise:
                        case NI_Sve2_MaxNumberPairwise:
                        case NI_Sve2_MaxPairwise:
                        case NI_Sve2_MinNumberPairwise:
                        case NI_Sve2_MinPairwise:
                        case NI_Sve2_ConvertToDoubleOdd:
                        case NI_Sve2_ConvertToSingleOdd:
                        case NI_Sve2_ConvertToSingleOddRoundToOdd:
                        {
                            // These instructions cannot mask via predication or MOVPRFX.
                            if (!mask.IsTrueMask(node.SimdBaseType))
                            {
                                break;
                            }

                            goto default;
                        }

                        default:
                        {
                            MakeSrcContained(node, falseValue);
                            LABELEDDISPTREERANGE("Contained false mask op3 in ConditionalSelect", BlockRange(), falseValue);
                            break;
                        }
                    }
                }

                break;
            }

            case NI_Sve_FusedMultiplyAddBySelectedScalar:
            case NI_Sve_FusedMultiplySubtractBySelectedScalar:
            case NI_Sve_MultiplyAddRotateComplex:
            case NI_Sve2_MultiplyAddRotateComplex:
            case NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplex:
            case NI_Sve2_DotProductRotateComplex:
            {
                assert(hasImmediate);
                ContainImmediate(4);
                break;
            }

            case NI_Sve_GatherPrefetch8Bit:
            case NI_Sve_GatherPrefetch16Bit:
            case NI_Sve_GatherPrefetch32Bit:
            case NI_Sve_GatherPrefetch64Bit:
            {
                assert(hasImmediate);
                ContainImmediate(varTypeIsSimd(node.GetOp(2).Type) ? 3 : 4);
                break;
            }

            case NI_Sve_SaturatingDecrementBy16BitElementCount:
            case NI_Sve_SaturatingDecrementBy32BitElementCount:
            case NI_Sve_SaturatingDecrementBy64BitElementCount:
            case NI_Sve_SaturatingDecrementBy8BitElementCount:
            case NI_Sve_SaturatingIncrementBy16BitElementCount:
            case NI_Sve_SaturatingIncrementBy32BitElementCount:
            case NI_Sve_SaturatingIncrementBy64BitElementCount:
            case NI_Sve_SaturatingIncrementBy8BitElementCount:
            case NI_Sve_SaturatingDecrementBy16BitElementCountScalar:
            case NI_Sve_SaturatingDecrementBy32BitElementCountScalar:
            case NI_Sve_SaturatingDecrementBy64BitElementCountScalar:
            case NI_Sve_SaturatingIncrementBy16BitElementCountScalar:
            case NI_Sve_SaturatingIncrementBy32BitElementCountScalar:
            case NI_Sve_SaturatingIncrementBy64BitElementCountScalar:
            {
                assert(hasImmediate);
                var first = node.GetOp(2);
                var second = node.GetOp(3);
                assert(varTypeIsIntegral(first.Type) && varTypeIsIntegral(second.Type));
                if (first.Oper.IsCnsIntOrI && second.Oper.IsCnsIntOrI)
                {
                    MakeSrcContained(node, first);
                    MakeSrcContained(node, second);
                }

                break;
            }

            case NI_Sve_MultiplyAddRotateComplexBySelectedScalar:
            case NI_Sve2_MultiplyAddRotateComplexBySelectedScalar:
            case NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar:
            case NI_Sve2_DotProductRotateComplexBySelectedIndex:
            {
                assert(hasImmediate);
                var first = node.GetOp(4);
                var second = node.GetOp(5);
                assert(varTypeIsIntegral(first.Type) && varTypeIsIntegral(second.Type));
                if (first.Oper.IsCnsIntOrI && second.Oper.IsCnsIntOrI)
                {
                    MakeSrcContained(node, first);
                    MakeSrcContained(node, second);
                }

                break;
            }

            default:
            {
                throw new InvalidOperationException($"Unexpected ARM64 containment intrinsic: {id}.");
            }
        }
    }
}
#endif
