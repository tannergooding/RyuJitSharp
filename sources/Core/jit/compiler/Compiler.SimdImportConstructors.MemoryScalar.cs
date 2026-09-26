// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS
    public GenTree gtNewSimdCeilNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(varTypeIsFloating(simdBaseType));

#if TARGET_XARCH
        if (simdSize == 64)
        {
            return gtNewSimdHWIntrinsicNode(type, NI_AVX512_RoundScale, simdBaseType, simdSize,
                op1, gtNewIconNode(TYP_INT, (int)FloatRoundingMode.ToPositiveInfinity));
        }

        var intrinsic = simdSize == 32 ? NI_AVX_Ceiling : NI_X86Base_Ceiling;
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
#else
        throw new FatalJitException("gtNewSimdCeilNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdFloorNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(varTypeIsFloating(simdBaseType));

#if TARGET_XARCH
        if (simdSize == 64)
        {
            return gtNewSimdHWIntrinsicNode(type, NI_AVX512_RoundScale, simdBaseType, simdSize,
                op1, gtNewIconNode(TYP_INT, (int)FloatRoundingMode.ToNegativeInfinity));
        }

        var intrinsic = simdSize == 32 ? NI_AVX_Floor : NI_X86Base_Floor;
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
#else
        throw new FatalJitException("gtNewSimdFloorNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdRoundNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(varTypeIsFloating(simdBaseType));

#if TARGET_XARCH
        if (simdSize == 64)
        {
            return gtNewSimdHWIntrinsicNode(type, NI_AVX512_RoundScale, simdBaseType, simdSize,
                op1, gtNewIconNode(TYP_INT, (int)FloatRoundingMode.ToNearestInteger));
        }

        var intrinsic = simdSize == 32 ? NI_AVX_RoundToNearestInteger : NI_X86Base_RoundToNearestInteger;
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
#else
        throw new FatalJitException("gtNewSimdRoundNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdLoadNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && varTypeIsArithmetic(simdBaseType));
        return gtNewLoadValueNode(type, op1);
    }

    public GenTree gtNewSimdLoadAlignedNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
#if TARGET_XARCH
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && varTypeIsArithmetic(simdBaseType));

        var intrinsic = simdSize switch
        {
            64 => NI_AVX512_LoadAlignedVector512,
            32 => NI_AVX_LoadAlignedVector256,
            _ => NI_X86Base_LoadAlignedVector128,
        };
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
#else
        throw new FatalJitException("gtNewSimdLoadAlignedNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdLoadNonTemporalNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
#if TARGET_XARCH
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && varTypeIsArithmetic(simdBaseType));

        NamedIntrinsic intrinsic;
        var isNonTemporal = false;
        if (simdSize == 32)
        {
            if (compOpportunisticallyDependsOn(InstructionSet_AVX2))
            {
                intrinsic = NI_AVX2_LoadAlignedVector256NonTemporal;
                isNonTemporal = true;
            }
            else
            {
                intrinsic = NI_AVX_LoadAlignedVector256;
            }
        }
        else if (simdSize == 64)
        {
            intrinsic = NI_AVX512_LoadAlignedVector512NonTemporal;
            isNonTemporal = true;
        }
        else
        {
            intrinsic = NI_X86Base_LoadAlignedVector128NonTemporal;
            isNonTemporal = true;
        }

        if (isNonTemporal)
        {
            if (simdBaseType == TYP_FLOAT)
            {
                simdBaseType = TYP_INT;
            }
            else if (simdBaseType == TYP_DOUBLE)
            {
                simdBaseType = TYP_LONG;
            }
        }
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
#else
        throw new FatalJitException("gtNewSimdLoadNonTemporalNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdStoreNode(GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize)
    {
        assert(op1 is not null && op2 is not null);
        assert(varTypeIsArithmetic(simdBaseType));
        assert(varTypeIsSimd(op2.Type) && GetSimdTypeForSize(simdSize) == op2.Type);
        return gtNewStoreValueNode(op2.Type, op1, op2);
    }

    public GenTree gtNewSimdStoreAlignedNode(GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize)
    {
#if TARGET_XARCH
        assert(op1 is not null && op2 is not null);
        assert(varTypeIsSimd(op2.Type) && GetSimdTypeForSize(simdSize) == op2.Type);
        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsic = simdSize switch
        {
            32 => NI_AVX_StoreAligned,
            64 => NI_AVX512_StoreAligned,
            _ => NI_X86Base_StoreAligned,
        };
        return gtNewSimdHWIntrinsicNode(TYP_VOID, intrinsic, simdBaseType, simdSize, op1, op2);
#else
        throw new FatalJitException("gtNewSimdStoreAlignedNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdStoreNonTemporalNode(GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize)
    {
#if TARGET_XARCH
        assert(op1 is not null && op2 is not null);
        assert(varTypeIsSimd(op2.Type) && GetSimdTypeForSize(simdSize) == op2.Type);
        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsic = simdSize switch
        {
            64 => NI_AVX512_StoreAlignedNonTemporal,
            32 => NI_AVX_StoreAlignedNonTemporal,
            _ => NI_X86Base_StoreAlignedNonTemporal,
        };
        return gtNewSimdHWIntrinsicNode(TYP_VOID, intrinsic, simdBaseType, simdSize, op1, op2);
#else
        throw new FatalJitException("gtNewSimdStoreNonTemporalNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdDotProdNode(var_types type, GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize)
    {
        var simdType = GetSimdTypeForSize(simdSize);
        assert(varTypeIsSimd(simdType) && varTypeIsSimd(type));
        assert(op1 is not null && op1.Type == simdType);
        assert(op2 is not null && op2.Type == simdType);
#if TARGET_XARCH
        assert(!varTypeIsByte(simdBaseType) && !varTypeIsLong(simdBaseType));
        assert(simdSize != 64);
        assert(simdSize != 32 || varTypeIsFloating(simdBaseType) ||
            compIsaSupportedDebugOnly(InstructionSet_AVX2));
#endif
        return gtNewSimdHWIntrinsicNode(type, NI_Vector_Dot, simdBaseType, simdSize, op1, op2);
    }

    public GenTree gtNewSimdFmaNode(var_types type, GenTree op1, GenTree op2, GenTree op3,
        var_types simdBaseType, byte simdSize)
    {
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(op2 is not null && op2.Type == type);
        assert(op3 is not null && op3.Type == type);
        assert(varTypeIsFloating(simdBaseType));
#if TARGET_XARCH
        var intrinsic = simdSize == 64 ? NI_AVX512_FusedMultiplyAdd : NI_AVX2_MultiplyAdd;
        if (simdSize != 64)
        {
            assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
        }
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2, op3);
#else
        throw new FatalJitException("gtNewSimdFmaNode requires its target-specific implementation.");
#endif
    }

    public GenTree gtNewSimdCmpOpAnyNode(genTreeOps op, var_types type, GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize)
    {
        assert(type == TYP_INT);
        var simdType = GetSimdTypeForSize(simdSize);
        assert(varTypeIsSimd(simdType));
        assert(op1 is not null && op1.Type == simdType);
        assert(op2 is not null && op2.Type == simdType);
        assert(varTypeIsArithmetic(simdBaseType));

        switch (op)
        {
            case GT_EQ:
            case GT_GE:
            case GT_GT:
            case GT_LE:
            case GT_LT:
            {
#if TARGET_XARCH
                assert(simdSize != 32 || compIsaSupportedDebugOnly(InstructionSet_AVX2));
#endif
                op1 = gtNewSimdCmpOpNode(op, simdType, op1, op2, simdBaseType, simdSize);
                op2 = gtNewZeroConNode(simdType);
                simdBaseType = simdBaseType switch
                {
                    TYP_FLOAT => TYP_INT,
                    TYP_DOUBLE => TYP_LONG,
                    _ => simdBaseType,
                };
                break;
            }

            case GT_NE:
            {
#if TARGET_XARCH
                assert(simdSize != 32 || varTypeIsFloating(simdBaseType) ||
                    compIsaSupportedDebugOnly(InstructionSet_AVX2));
#endif
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
        return gtNewSimdHWIntrinsicNode(type, NI_Vector_op_Inequality, simdBaseType, simdSize, op1, op2);
    }

#if TARGET_XARCH
    public GenTree gtNewSimdTernaryLogicNode(var_types type, GenTree op1, GenTree op2, GenTree op3,
        GenTree op4, var_types simdBaseType, byte simdSize)
    {
        assert(compIsaSupportedDebugOnly(InstructionSet_AVX512));
        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(op2 is not null && op2.Type == type);
        assert(op3 is not null && op3.Type == type);
        assert(op4 is not null && op4.Type.ActualType == TYP_INT);
        assert(varTypeIsArithmetic(simdBaseType));
        return gtNewSimdHWIntrinsicNode(type, NI_AVX512_TernaryLogic, simdBaseType, simdSize,
            op1, op2, op3, op4);
    }
#endif
#endif
}
