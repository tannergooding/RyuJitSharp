// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS
namespace RyuJitSharp;

public sealed partial class GenTreeHWIntrinsic
{
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
#if !TARGET_WASM
        switch (id)
        {
#if TARGET_XARCH
            case NI_X86Base_And:
            case NI_AVX_And:
            case NI_AVX2_And:
            case NI_AVX512_And:
            case NI_AVX512_AndMask:
#elif TARGET_ARM64
            case NI_AdvSimd_And:
            case NI_Sve_And:
#endif
            {
                return GT_AND;
            }

#if TARGET_XARCH
            case NI_AVX512_NotMask:
#elif TARGET_ARM64
            case NI_AdvSimd_Not:
            case NI_Sve_Not:
#endif
            {
                return GT_NOT;
            }

#if TARGET_XARCH
            case NI_X86Base_Xor:
            case NI_AVX_Xor:
            case NI_AVX2_Xor:
            case NI_AVX512_Xor:
            case NI_AVX512_XorMask:
#elif TARGET_ARM64
            case NI_AdvSimd_Xor:
            case NI_Sve_Xor:
#endif
            {
                return GT_XOR;
            }

#if TARGET_XARCH
            case NI_X86Base_Or:
            case NI_AVX_Or:
            case NI_AVX2_Or:
            case NI_AVX512_Or:
            case NI_AVX512_OrMask:
#elif TARGET_ARM64
            case NI_AdvSimd_Or:
            case NI_Sve_Or:
#endif
            {
                return GT_OR;
            }

#if TARGET_XARCH
            case NI_X86Base_AndNot:
            case NI_AVX_AndNot:
            case NI_AVX2_AndNotVector:
            case NI_AVX512_AndNot:
            case NI_AVX512_AndNotMask:
#elif TARGET_ARM64
            case NI_AdvSimd_BitwiseClear:
            case NI_Sve_BitwiseClear:
#endif
            {
                return GT_AND_NOT;
            }

#if TARGET_XARCH
            case NI_X86Base_Add:
            case NI_AVX_Add:
            case NI_AVX2_Add:
            case NI_AVX512_Add:
#elif TARGET_ARM64
            case NI_AdvSimd_Add:
            case NI_AdvSimd_Arm64_Add:
            case NI_Sve_Add:
#endif
            {
                return GT_ADD;
            }

#if TARGET_XARCH
            case NI_X86Base_AddScalar:
            case NI_AVX512_AddScalar:
            {
                isScalar = true;
                return GT_ADD;
            }
#endif

#if TARGET_ARM64
            case NI_AdvSimd_AddScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_ADD;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_Divide:
            case NI_AVX_Divide:
            case NI_AVX512_Divide:
#elif TARGET_ARM64
            case NI_AdvSimd_Arm64_Divide:
            case NI_Sve_Divide:
#endif
            {
                return GT_DIV;
            }

#if TARGET_XARCH
            case NI_X86Base_DivideScalar:
            case NI_AVX512_DivideScalar:
            {
                isScalar = true;
                return GT_DIV;
            }
#endif

#if TARGET_ARM64
            case NI_AdvSimd_DivideScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_DIV;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_MultiplyLow:
            case NI_AVX_Multiply:
            case NI_AVX2_MultiplyLow:
            case NI_AVX512_MultiplyLow:
#elif TARGET_ARM64
            case NI_AdvSimd_Multiply:
            case NI_AdvSimd_Arm64_Multiply:
            case NI_Sve_Multiply:
#endif
            {
                return GT_MUL;
            }

#if TARGET_XARCH
            case NI_X86Base_Multiply:
            case NI_AVX512_Multiply:
            {
                if (varTypeIsFloating(simdBaseType))
                {
                    return GT_MUL;
                }
                return GT_NONE;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_MultiplyScalar:
            case NI_AVX512_MultiplyScalar:
            {
                isScalar = true;
                return GT_MUL;
            }
#endif

#if TARGET_ARM64
            case NI_AdvSimd_MultiplyScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_MUL;
            }
#endif

#if TARGET_ARM64
            case NI_AdvSimd_Negate:
            case NI_AdvSimd_Arm64_Negate:
            case NI_Sve_Negate:
            {
                return GT_NEG;
            }

            case NI_AdvSimd_NegateScalar:
            case NI_AdvSimd_Arm64_NegateScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_NEG;
            }
#endif

#if TARGET_XARCH
            case NI_AVX512_RotateLeft:
            case NI_AVX512_RotateLeftVariable:
            {
                return GT_ROL;
            }

            case NI_AVX512_RotateRight:
            case NI_AVX512_RotateRightVariable:
            {
                return GT_ROR;
            }
#endif // TARGET_XARCH

#if TARGET_XARCH
            case NI_X86Base_ShiftLeftLogical:
            case NI_AVX2_ShiftLeftLogical:
            case NI_AVX2_ShiftLeftLogicalVariable:
            case NI_AVX512_ShiftLeftLogical:
            case NI_AVX512_ShiftLeftLogicalVariable:
#elif TARGET_ARM64
            case NI_AdvSimd_ShiftLeftLogical:
            case NI_Sve_ShiftLeftLogical:
#endif
            {
                return GT_LSH;
            }

#if TARGET_ARM64
            case NI_AdvSimd_ShiftLeftLogicalScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_LSH;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_ShiftRightArithmetic:
            case NI_AVX2_ShiftRightArithmetic:
            case NI_AVX2_ShiftRightArithmeticVariable:
            case NI_AVX512_ShiftRightArithmetic:
            case NI_AVX512_ShiftRightArithmeticVariable:
#elif TARGET_ARM64
            case NI_AdvSimd_ShiftRightArithmetic:
            case NI_Sve_ShiftRightArithmetic:
#endif
            {
                return GT_RSH;
            }

#if TARGET_ARM64
            case NI_AdvSimd_ShiftRightArithmeticScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_RSH;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_ShiftRightLogical:
            case NI_AVX2_ShiftRightLogical:
            case NI_AVX2_ShiftRightLogicalVariable:
            case NI_AVX512_ShiftRightLogical:
            case NI_AVX512_ShiftRightLogicalVariable:
#elif TARGET_ARM64
            case NI_AdvSimd_ShiftRightLogical:
            case NI_Sve_ShiftRightLogical:
#endif
            {
                return GT_RSZ;
            }

#if TARGET_ARM64
            case NI_AdvSimd_ShiftRightLogicalScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_RSZ;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_Subtract:
            case NI_AVX_Subtract:
            case NI_AVX2_Subtract:
            case NI_AVX512_Subtract:
#elif TARGET_ARM64
            case NI_AdvSimd_Subtract:
            case NI_AdvSimd_Arm64_Subtract:
            case NI_Sve_Subtract:
#endif
            {
                return GT_SUB;
            }

#if TARGET_XARCH
            case NI_X86Base_SubtractScalar:
            case NI_AVX512_SubtractScalar:
            {
                isScalar = true;
                return GT_SUB;
            }
#endif

#if TARGET_ARM64
            case NI_AdvSimd_SubtractScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_SUB;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_CompareEqual:
            case NI_AVX_CompareEqual:
            case NI_AVX2_CompareEqual:
            case NI_AVX512_CompareEqualMask:
#elif TARGET_ARM64
            case NI_AdvSimd_CompareEqual:
            case NI_AdvSimd_Arm64_CompareEqual:
#endif
            {
                return GT_EQ;
            }

#if TARGET_XARCH
            case NI_X86Base_CompareScalarEqual:
            {
                isScalar = true;
                return GT_EQ;
            }
#endif

#if TARGET_ARM64
            case NI_AdvSimd_Arm64_CompareEqualScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_EQ;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_CompareGreaterThan:
            case NI_AVX_CompareGreaterThan:
            case NI_AVX2_CompareGreaterThan:
            case NI_AVX512_CompareGreaterThanMask:
#elif TARGET_ARM64
            case NI_AdvSimd_CompareGreaterThan:
            case NI_AdvSimd_Arm64_CompareGreaterThan:
#endif
            {
                return GT_GT;
            }

#if TARGET_XARCH
            case NI_X86Base_CompareScalarGreaterThan:
            {
                isScalar = true;
                return GT_GT;
            }
#endif

#if TARGET_ARM64
            case NI_AdvSimd_Arm64_CompareGreaterThanScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_GT;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_CompareGreaterThanOrEqual:
            case NI_AVX_CompareGreaterThanOrEqual:
            case NI_AVX512_CompareGreaterThanOrEqualMask:
#elif TARGET_ARM64
            case NI_AdvSimd_CompareGreaterThanOrEqual:
            case NI_AdvSimd_Arm64_CompareGreaterThanOrEqual:
#endif
            {
                return GT_GE;
            }

#if TARGET_XARCH
            case NI_X86Base_CompareScalarGreaterThanOrEqual:
            {
                isScalar = true;
                return GT_GE;
            }
#endif

#if TARGET_ARM64
            case NI_AdvSimd_Arm64_CompareGreaterThanOrEqualScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_GE;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_CompareLessThan:
            case NI_AVX_CompareLessThan:
            case NI_AVX2_CompareLessThan:
            case NI_AVX512_CompareLessThanMask:
#elif TARGET_ARM64
            case NI_AdvSimd_CompareLessThan:
            case NI_AdvSimd_Arm64_CompareLessThan:
#endif
            {
                return GT_LT;
            }

#if TARGET_XARCH
            case NI_X86Base_CompareScalarLessThan:
            {
                isScalar = true;
                return GT_LT;
            }
#endif

#if TARGET_ARM64
            case NI_AdvSimd_Arm64_CompareLessThanScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_LT;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_CompareLessThanOrEqual:
            case NI_AVX_CompareLessThanOrEqual:
            case NI_AVX512_CompareLessThanOrEqualMask:
#elif TARGET_ARM64
            case NI_AdvSimd_CompareLessThanOrEqual:
            case NI_AdvSimd_Arm64_CompareLessThanOrEqual:
#endif
            {
                return GT_LE;
            }

#if TARGET_XARCH
            case NI_X86Base_CompareScalarLessThanOrEqual:
            {
                isScalar = true;
                return GT_LE;
            }
#endif

#if TARGET_ARM64
            case NI_AdvSimd_Arm64_CompareLessThanOrEqualScalar:
            {
                if (simdBaseType.Size != 8)
                {
                    isScalar = true;
                }
                return GT_LE;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_CompareNotEqual:
            case NI_AVX_CompareNotEqual:
            case NI_AVX512_CompareNotEqualMask:
            {
                return GT_NE;
            }

            case NI_X86Base_CompareScalarNotEqual:
            {
                isScalar = true;
                return GT_NE;
            }
#endif

            default:
            {
                return GT_NONE;
            }
        }
#else
        return GT_NONE;
#endif
    }
}
#endif
