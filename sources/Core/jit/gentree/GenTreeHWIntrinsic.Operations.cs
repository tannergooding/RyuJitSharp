// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class GenTreeHWIntrinsic
{
    public bool ShouldConstantProp(GenTree operand, GenTreeVecCon constant)
    {
        var id = HWIntrinsicId;
        assert(HWIntrinsicInfo.CanBenefitFromConstantProp(id));
        var baseType = SimdBaseType;
        switch (id)
        {
            case NI_Vector_op_Equality:
            case NI_Vector_op_Inequality:
            case NI_AVX512_CompareEqualMask when Configuration.TARGET_XARCH:
            case NI_AVX512_CompareNotEqualMask when Configuration.TARGET_XARCH:
            {
                // Floating equality must treat positive and negative zero alike.
                return constant.IsZero && !varTypeIsFloating(baseType);
            }

            case NI_AdvSimd_CompareEqual when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareEqual when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareEqualScalar when Configuration.TARGET_ARM64:
            {
                return constant.IsZero;
            }

            case NI_AdvSimd_CompareGreaterThan when Configuration.TARGET_ARM64:
            case NI_AdvSimd_CompareGreaterThanOrEqual when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareGreaterThan when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareGreaterThanOrEqual when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareGreaterThanScalar when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareGreaterThanOrEqualScalar when Configuration.TARGET_ARM64:
            {
                return constant.IsZero && !varTypeIsUnsigned(baseType);
            }

            case NI_X86Base_Insert when Configuration.TARGET_XARCH:
            {
                return (baseType is TYP_FLOAT) && constant.IsZero;
            }

            case NI_Vector_Shuffle:
            case NI_Vector_ShuffleNative:
            case NI_Vector_ShuffleNativeFallback:
            {
                // Constant indices can avoid lowering the shuffle to a user call.
                assert(Operands.Length == 2);
                return IsUserCall && (operand == GetOp(2));
            }

            case NI_X86Base_Xor when Configuration.TARGET_XARCH:
            case NI_AVX_Xor when Configuration.TARGET_XARCH:
            case NI_AVX2_Xor when Configuration.TARGET_XARCH:
            case NI_AVX512_Xor when Configuration.TARGET_XARCH:
            {
                assert(Operands.Length == 2);
                return constant.IsVectorAllBitsSet;
            }
            default:
            {
                return false;
            }
        }
    }

    public static bool OperIsBitwiseHWIntrinsic(genTreeOps oper)
    {
        return oper is GT_AND or GT_AND_NOT or GT_NOT or GT_OR or GT_OR_NOT or GT_XOR or GT_XOR_NOT;
    }

    public bool OperIsBitwiseHWIntrinsic() => OperIsBitwiseHWIntrinsic(GetOperForHWIntrinsicId(out _));

    public genTreeOps GetOperForHWIntrinsicId(out bool isScalar, bool getEffectiveOp = false)
    {
        var simdBaseType = SimdBaseType;
        var oper = GetOperForHWIntrinsicId(HWIntrinsicId, simdBaseType, out isScalar);

        if (getEffectiveOp)
        {
            if (oper == GT_SUB)
            {
                var op1 = GetOp(1);
                if (varTypeIsIntegral(simdBaseType))
                {
                    if (op1.IsVectorZero)
                    {
                        oper = GT_NEG;
                    }
                    else if (isScalar && op1.Oper.IsCnsVec && op1.AsVecCon().IsScalarZero(simdBaseType))
                    {
                        oper = GT_NEG;
                    }
                }
            }
            else if (oper == GT_XOR)
            {
                var op2 = GetOp(2);
                if (op2.IsVectorAllBitsSet)
                {
                    oper = GT_NOT;
                }
                else if (varTypeIsFloating(simdBaseType) && op2.IsVectorNegativeZero(simdBaseType))
                {
                    oper = GT_NEG;
                }
            }
        }

        return oper;
    }

    public static genTreeOps GetOperForHWIntrinsicId(NamedIntrinsic id, var_types simdBaseType, out bool isScalar)
    {
        isScalar = false;
        if (Configuration.TARGET_WASM)
        {
            return GT_NONE;
        }

        switch (id)
        {
            case NI_X86Base_And when Configuration.TARGET_XARCH:
            case NI_AVX_And when Configuration.TARGET_XARCH:
            case NI_AVX2_And when Configuration.TARGET_XARCH:
            case NI_AVX512_And when Configuration.TARGET_XARCH:
            case NI_AVX512_AndMask when Configuration.TARGET_XARCH:
            case NI_AdvSimd_And when Configuration.TARGET_ARM64:
            case NI_Sve_And when Configuration.TARGET_ARM64:
            {
                return GT_AND;
            }

            case NI_AVX512_NotMask when Configuration.TARGET_XARCH:
            case NI_AdvSimd_Not when Configuration.TARGET_ARM64:
            case NI_Sve_Not when Configuration.TARGET_ARM64:
            {
                return GT_NOT;
            }

            case NI_X86Base_Xor when Configuration.TARGET_XARCH:
            case NI_AVX_Xor when Configuration.TARGET_XARCH:
            case NI_AVX2_Xor when Configuration.TARGET_XARCH:
            case NI_AVX512_Xor when Configuration.TARGET_XARCH:
            case NI_AVX512_XorMask when Configuration.TARGET_XARCH:
            case NI_AdvSimd_Xor when Configuration.TARGET_ARM64:
            case NI_Sve_Xor when Configuration.TARGET_ARM64:
            {
                return GT_XOR;
            }

            case NI_X86Base_Or when Configuration.TARGET_XARCH:
            case NI_AVX_Or when Configuration.TARGET_XARCH:
            case NI_AVX2_Or when Configuration.TARGET_XARCH:
            case NI_AVX512_Or when Configuration.TARGET_XARCH:
            case NI_AVX512_OrMask when Configuration.TARGET_XARCH:
            case NI_AdvSimd_Or when Configuration.TARGET_ARM64:
            case NI_Sve_Or when Configuration.TARGET_ARM64:
            {
                return GT_OR;
            }

            case NI_X86Base_AndNot when Configuration.TARGET_XARCH:
            case NI_AVX_AndNot when Configuration.TARGET_XARCH:
            case NI_AVX2_AndNotVector when Configuration.TARGET_XARCH:
            case NI_AVX512_AndNot when Configuration.TARGET_XARCH:
            case NI_AVX512_AndNotMask when Configuration.TARGET_XARCH:
            case NI_AdvSimd_BitwiseClear when Configuration.TARGET_ARM64:
            case NI_Sve_BitwiseClear when Configuration.TARGET_ARM64:
            {
                return GT_AND_NOT;
            }

            case NI_X86Base_Add when Configuration.TARGET_XARCH:
            case NI_AVX_Add when Configuration.TARGET_XARCH:
            case NI_AVX2_Add when Configuration.TARGET_XARCH:
            case NI_AVX512_Add when Configuration.TARGET_XARCH:
            case NI_AdvSimd_Add when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_Add when Configuration.TARGET_ARM64:
            case NI_Sve_Add when Configuration.TARGET_ARM64:
            {
                return GT_ADD;
            }

            case NI_X86Base_AddScalar when Configuration.TARGET_XARCH:
            case NI_AVX512_AddScalar when Configuration.TARGET_XARCH:
            {
                isScalar = true;
                return GT_ADD;
            }

            case NI_AdvSimd_AddScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_ADD;
            }

            case NI_X86Base_Divide when Configuration.TARGET_XARCH:
            case NI_AVX_Divide when Configuration.TARGET_XARCH:
            case NI_AVX512_Divide when Configuration.TARGET_XARCH:
            case NI_AdvSimd_Arm64_Divide when Configuration.TARGET_ARM64:
            case NI_Sve_Divide when Configuration.TARGET_ARM64:
            {
                return GT_DIV;
            }

            case NI_X86Base_DivideScalar when Configuration.TARGET_XARCH:
            case NI_AVX512_DivideScalar when Configuration.TARGET_XARCH:
            {
                isScalar = true;
                return GT_DIV;
            }

            case NI_AdvSimd_DivideScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_DIV;
            }

            case NI_X86Base_MultiplyLow when Configuration.TARGET_XARCH:
            case NI_AVX_Multiply when Configuration.TARGET_XARCH:
            case NI_AVX2_MultiplyLow when Configuration.TARGET_XARCH:
            case NI_AVX512_MultiplyLow when Configuration.TARGET_XARCH:
            case NI_AdvSimd_Multiply when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_Multiply when Configuration.TARGET_ARM64:
            case NI_Sve_Multiply when Configuration.TARGET_ARM64:
            {
                return GT_MUL;
            }

            case NI_X86Base_Multiply when Configuration.TARGET_XARCH:
            case NI_AVX512_Multiply when Configuration.TARGET_XARCH:
            {
                if (varTypeIsFloating(simdBaseType))
                {
                    return GT_MUL;
                }
                return GT_NONE;
            }

            case NI_X86Base_MultiplyScalar when Configuration.TARGET_XARCH:
            case NI_AVX512_MultiplyScalar when Configuration.TARGET_XARCH:
            {
                isScalar = true;
                return GT_MUL;
            }

            case NI_AdvSimd_MultiplyScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_MUL;
            }

            case NI_AdvSimd_Negate when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_Negate when Configuration.TARGET_ARM64:
            case NI_Sve_Negate when Configuration.TARGET_ARM64:
            {
                return GT_NEG;
            }

            case NI_AdvSimd_NegateScalar when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_NegateScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_NEG;
            }

            case NI_AVX512_RotateLeft when Configuration.TARGET_XARCH:
            case NI_AVX512_RotateLeftVariable when Configuration.TARGET_XARCH:
            {
                return GT_ROL;
            }

            case NI_AVX512_RotateRight when Configuration.TARGET_XARCH:
            case NI_AVX512_RotateRightVariable when Configuration.TARGET_XARCH:
            {
                return GT_ROR;
            }

            case NI_X86Base_ShiftLeftLogical when Configuration.TARGET_XARCH:
            case NI_AVX2_ShiftLeftLogical when Configuration.TARGET_XARCH:
            case NI_AVX2_ShiftLeftLogicalVariable when Configuration.TARGET_XARCH:
            case NI_AVX512_ShiftLeftLogical when Configuration.TARGET_XARCH:
            case NI_AVX512_ShiftLeftLogicalVariable when Configuration.TARGET_XARCH:
            case NI_AdvSimd_ShiftLeftLogical when Configuration.TARGET_ARM64:
            case NI_Sve_ShiftLeftLogical when Configuration.TARGET_ARM64:
            {
                return GT_LSH;
            }

            case NI_AdvSimd_ShiftLeftLogicalScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_LSH;
            }

            case NI_X86Base_ShiftRightArithmetic when Configuration.TARGET_XARCH:
            case NI_AVX2_ShiftRightArithmetic when Configuration.TARGET_XARCH:
            case NI_AVX2_ShiftRightArithmeticVariable when Configuration.TARGET_XARCH:
            case NI_AVX512_ShiftRightArithmetic when Configuration.TARGET_XARCH:
            case NI_AVX512_ShiftRightArithmeticVariable when Configuration.TARGET_XARCH:
            case NI_AdvSimd_ShiftRightArithmetic when Configuration.TARGET_ARM64:
            case NI_Sve_ShiftRightArithmetic when Configuration.TARGET_ARM64:
            {
                return GT_RSH;
            }

            case NI_AdvSimd_ShiftRightArithmeticScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_RSH;
            }

            case NI_X86Base_ShiftRightLogical when Configuration.TARGET_XARCH:
            case NI_AVX2_ShiftRightLogical when Configuration.TARGET_XARCH:
            case NI_AVX2_ShiftRightLogicalVariable when Configuration.TARGET_XARCH:
            case NI_AVX512_ShiftRightLogical when Configuration.TARGET_XARCH:
            case NI_AVX512_ShiftRightLogicalVariable when Configuration.TARGET_XARCH:
            case NI_AdvSimd_ShiftRightLogical when Configuration.TARGET_ARM64:
            case NI_Sve_ShiftRightLogical when Configuration.TARGET_ARM64:
            {
                return GT_RSZ;
            }

            case NI_AdvSimd_ShiftRightLogicalScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_RSZ;
            }

            case NI_X86Base_Subtract when Configuration.TARGET_XARCH:
            case NI_AVX_Subtract when Configuration.TARGET_XARCH:
            case NI_AVX2_Subtract when Configuration.TARGET_XARCH:
            case NI_AVX512_Subtract when Configuration.TARGET_XARCH:
            case NI_AdvSimd_Subtract when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_Subtract when Configuration.TARGET_ARM64:
            case NI_Sve_Subtract when Configuration.TARGET_ARM64:
            {
                return GT_SUB;
            }

            case NI_X86Base_SubtractScalar when Configuration.TARGET_XARCH:
            case NI_AVX512_SubtractScalar when Configuration.TARGET_XARCH:
            {
                isScalar = true;
                return GT_SUB;
            }

            case NI_AdvSimd_SubtractScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_SUB;
            }

            case NI_X86Base_CompareEqual when Configuration.TARGET_XARCH:
            case NI_AVX_CompareEqual when Configuration.TARGET_XARCH:
            case NI_AVX2_CompareEqual when Configuration.TARGET_XARCH:
            case NI_AVX512_CompareEqualMask when Configuration.TARGET_XARCH:
            case NI_AdvSimd_CompareEqual when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareEqual when Configuration.TARGET_ARM64:
            {
                return GT_EQ;
            }

            case NI_X86Base_CompareScalarEqual when Configuration.TARGET_XARCH:
            {
                isScalar = true;
                return GT_EQ;
            }

            case NI_AdvSimd_Arm64_CompareEqualScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_EQ;
            }

            case NI_X86Base_CompareGreaterThan when Configuration.TARGET_XARCH:
            case NI_AVX_CompareGreaterThan when Configuration.TARGET_XARCH:
            case NI_AVX2_CompareGreaterThan when Configuration.TARGET_XARCH:
            case NI_AVX512_CompareGreaterThanMask when Configuration.TARGET_XARCH:
            case NI_AdvSimd_CompareGreaterThan when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareGreaterThan when Configuration.TARGET_ARM64:
            {
                return GT_GT;
            }

            case NI_X86Base_CompareScalarGreaterThan when Configuration.TARGET_XARCH:
            {
                isScalar = true;
                return GT_GT;
            }

            case NI_AdvSimd_Arm64_CompareGreaterThanScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_GT;
            }

            case NI_X86Base_CompareGreaterThanOrEqual when Configuration.TARGET_XARCH:
            case NI_AVX_CompareGreaterThanOrEqual when Configuration.TARGET_XARCH:
            case NI_AVX512_CompareGreaterThanOrEqualMask when Configuration.TARGET_XARCH:
            case NI_AdvSimd_CompareGreaterThanOrEqual when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareGreaterThanOrEqual when Configuration.TARGET_ARM64:
            {
                return GT_GE;
            }

            case NI_X86Base_CompareScalarGreaterThanOrEqual when Configuration.TARGET_XARCH:
            {
                isScalar = true;
                return GT_GE;
            }

            case NI_AdvSimd_Arm64_CompareGreaterThanOrEqualScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_GE;
            }

            case NI_X86Base_CompareLessThan when Configuration.TARGET_XARCH:
            case NI_AVX_CompareLessThan when Configuration.TARGET_XARCH:
            case NI_AVX2_CompareLessThan when Configuration.TARGET_XARCH:
            case NI_AVX512_CompareLessThanMask when Configuration.TARGET_XARCH:
            case NI_AdvSimd_CompareLessThan when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareLessThan when Configuration.TARGET_ARM64:
            {
                return GT_LT;
            }

            case NI_X86Base_CompareScalarLessThan when Configuration.TARGET_XARCH:
            {
                isScalar = true;
                return GT_LT;
            }

            case NI_AdvSimd_Arm64_CompareLessThanScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_LT;
            }

            case NI_X86Base_CompareLessThanOrEqual when Configuration.TARGET_XARCH:
            case NI_AVX_CompareLessThanOrEqual when Configuration.TARGET_XARCH:
            case NI_AVX512_CompareLessThanOrEqualMask when Configuration.TARGET_XARCH:
            case NI_AdvSimd_CompareLessThanOrEqual when Configuration.TARGET_ARM64:
            case NI_AdvSimd_Arm64_CompareLessThanOrEqual when Configuration.TARGET_ARM64:
            {
                return GT_LE;
            }

            case NI_X86Base_CompareScalarLessThanOrEqual when Configuration.TARGET_XARCH:
            {
                isScalar = true;
                return GT_LE;
            }

            case NI_AdvSimd_Arm64_CompareLessThanOrEqualScalar when Configuration.TARGET_ARM64:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_LE;
            }

            case NI_X86Base_CompareNotEqual when Configuration.TARGET_XARCH:
            case NI_AVX_CompareNotEqual when Configuration.TARGET_XARCH:
            case NI_AVX512_CompareNotEqualMask when Configuration.TARGET_XARCH:
            {
                return GT_NE;
            }

            case NI_X86Base_CompareScalarNotEqual when Configuration.TARGET_XARCH:
            {
                isScalar = true;
                return GT_NE;
            }

            default:
            {
                return GT_NONE;
            }
        }
    }
}
