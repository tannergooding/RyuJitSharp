// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public GenTree gtNewSimdCeilNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.SIMDCeilingRequiresHardwareIntrinsics);
            return default!;
        }

        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(varTypeIsFloating(simdBaseType));

        NamedIntrinsic intrinsic;

        if (Configuration.TARGET_XARCH)
        {
            if (simdSize == 64)
            {
                return gtNewSimdHWIntrinsicNode(type, NI_AVX512_RoundScale, simdBaseType, simdSize,
                    op1, gtNewIconNode(TYP_INT, (int)FloatRoundingMode.ToPositiveInfinity));
            }

            intrinsic = simdSize == 32 ? NI_AVX_Ceiling : NI_X86Base_Ceiling;
        }
        else if (Configuration.TARGET_ARM64)
        {
            if (simdBaseType == TYP_DOUBLE)
            {
                intrinsic = simdSize == 8 ? NI_AdvSimd_CeilingScalar : NI_AdvSimd_Arm64_Ceiling;
            }
            else
            {
                intrinsic = NI_AdvSimd_Ceiling;
            }
        }
        else if (Configuration.TARGET_WASM)
        {
            intrinsic = NI_PackedSimd_Ceiling;
        }
        else
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.GtNewSimdCeilNodeRequiresItsTargetSpecificImplementation);
            return default!;
        }

        assert(intrinsic != NI_Illegal);
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
    }

    public GenTree gtNewSimdFloorNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.SIMDFloorRequiresHardwareIntrinsics);
            return default!;
        }

        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(varTypeIsFloating(simdBaseType));

        NamedIntrinsic intrinsic;

        if (Configuration.TARGET_XARCH)
        {
            if (simdSize == 64)
            {
                return gtNewSimdHWIntrinsicNode(type, NI_AVX512_RoundScale, simdBaseType, simdSize,
                    op1, gtNewIconNode(TYP_INT, (int)FloatRoundingMode.ToNegativeInfinity));
            }

            intrinsic = simdSize == 32 ? NI_AVX_Floor : NI_X86Base_Floor;
        }
        else if (Configuration.TARGET_ARM64)
        {
            if (simdBaseType == TYP_DOUBLE)
            {
                intrinsic = simdSize == 8 ? NI_AdvSimd_FloorScalar : NI_AdvSimd_Arm64_Floor;
            }
            else
            {
                intrinsic = NI_AdvSimd_Floor;
            }
        }
        else if (Configuration.TARGET_WASM)
        {
            intrinsic = NI_PackedSimd_Floor;
        }
        else
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.GtNewSimdFloorNodeRequiresItsTargetSpecificImplementation);
            return default!;
        }

        assert(intrinsic != NI_Illegal);
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
    }

    public GenTree gtNewSimdRoundNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.SIMDRoundingRequiresHardwareIntrinsics);
            return default!;
        }

        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(varTypeIsFloating(simdBaseType));

        if (Configuration.TARGET_XARCH)
        {
            if (simdSize == 64)
            {
                return gtNewSimdHWIntrinsicNode(type, NI_AVX512_RoundScale, simdBaseType, simdSize,
                    op1, gtNewIconNode(TYP_INT, (int)FloatRoundingMode.ToNearestInteger));
            }

            var intrinsic = simdSize == 32 ? NI_AVX_RoundToNearestInteger : NI_X86Base_RoundToNearestInteger;
            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
        }
        else if (Configuration.TARGET_ARM64)
        {
            NamedIntrinsic intrinsic;
            if (simdBaseType == TYP_DOUBLE)
            {
                intrinsic = simdSize == 8 ? NI_AdvSimd_RoundToNearestScalar : NI_AdvSimd_Arm64_RoundToNearest;
            }
            else
            {
                intrinsic = NI_AdvSimd_RoundToNearest;
            }
            assert(intrinsic != NI_Illegal);

            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
        }
        else if (Configuration.TARGET_WASM)
        {
            return gtNewSimdHWIntrinsicNode(type, NI_PackedSimd_RoundToNearest, simdBaseType, simdSize, op1);
        }

        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.GtNewSimdRoundNodeRequiresItsTargetSpecificImplementation);
        return default!;
    }

    public GenTree gtNewSimdLoadNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.SIMDLoadsRequireHardwareIntrinsics);
            return default!;
        }

        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && varTypeIsArithmetic(simdBaseType));
        return gtNewLoadValueNode(type, op1);
    }

    public GenTree gtNewSimdLoadAlignedNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.AlignedSIMDLoadsRequireHardwareIntrinsics);
            return default!;
        }

        if (Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
        {
            // ARM64/Wasm has no aligned loads. Alignment is validated with optimizations disabled,
            // so only skip the intrinsic handling when optimizations are enabled.
            assert(opts.OptimizationEnabled);

            return gtNewSimdLoadNode(type, op1, simdBaseType, simdSize);
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.GtNewSimdLoadAlignedNodeRequiresItsTargetSpecificImplementation
    );
            return default!;
        }

        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && varTypeIsArithmetic(simdBaseType));

        var intrinsic = simdSize switch {
            64 => NI_AVX512_LoadAlignedVector512,
            32 => NI_AVX_LoadAlignedVector256,
            _ => NI_X86Base_LoadAlignedVector128,
        };
        return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1);
    }

    public GenTree gtNewSimdLoadNonTemporalNode(var_types type, GenTree op1, var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.NonTemporalSIMDLoadsRequireHardwareIntrinsics
    );
            return default!;
        }

        if (Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
        {
            // ARM64/Wasm has no aligned loads. Alignment is validated with optimizations disabled,
            // so only skip the intrinsic handling when optimizations are enabled.
            assert(opts.OptimizationEnabled);

            return gtNewSimdLoadNode(type, op1, simdBaseType, simdSize);
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.GtNewSimdLoadNonTemporalNodeRequiresItsTargetSpecificImplementation
    );
            return default!;
        }

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
    }

    public GenTree gtNewSimdStoreNode(GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize,
        bool reverseOps = false)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.SIMDStoresRequireHardwareIntrinsics);
            return default!;
        }

        assert(op1 is not null && op2 is not null);
        assert(varTypeIsArithmetic(simdBaseType));
        assert(varTypeIsSimd(op2.Type) && GetSimdTypeForSize(simdSize) == op2.Type);
        if (reverseOps)
        {
            gtPrepareOperandsForReordering(ref op2, ref op1);
        }
        return gtNewStoreValueNode(op2.Type, op1, op2);
    }

    public GenTree gtNewSimdStoreAlignedNode(GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize,
        bool reverseOps = false)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.AlignedSIMDStoresRequireHardwareIntrinsics);
            return default!;
        }

        if (Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
        {
            // ARM64/Wasm has no aligned stores. Alignment is validated with optimizations disabled,
            // so only skip the intrinsic handling when optimizations are enabled.
            assert(opts.OptimizationEnabled);

            return gtNewSimdStoreNode(op1, op2, simdBaseType, simdSize, reverseOps);
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.GtNewSimdStoreAlignedNodeRequiresItsTargetSpecificImplementation
    );
            return default!;
        }

        assert(op1 is not null && op2 is not null);
        assert(varTypeIsSimd(op2.Type) && GetSimdTypeForSize(simdSize) == op2.Type);
        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsic = simdSize switch {
            32 => NI_AVX_StoreAligned,
            64 => NI_AVX512_StoreAligned,
            _ => NI_X86Base_StoreAligned,
        };
        if (reverseOps)
        {
            gtPrepareOperandsForReordering(ref op2, ref op1);
        }
        return gtNewSimdHWIntrinsicNode(TYP_VOID, intrinsic, simdBaseType, simdSize, op1, op2);
    }

    public GenTree gtNewSimdStoreNonTemporalNode(GenTree op1, GenTree op2, var_types simdBaseType, byte simdSize,
        bool reverseOps = false)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.NonTemporalSIMDStoresRequireHardwareIntrinsics
    );
            return default!;
        }

        if (Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
        {
            // ARM64/Wasm has no aligned stores. Alignment is validated with optimizations disabled,
            // so only skip the intrinsic handling when optimizations are enabled.
            assert(opts.OptimizationEnabled);

            return gtNewSimdStoreNode(op1, op2, simdBaseType, simdSize, reverseOps);
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.GtNewSimdStoreNonTemporalNodeRequiresItsTargetSpecificImplementation
    );
            return default!;
        }

        assert(op1 is not null && op2 is not null);
        assert(varTypeIsSimd(op2.Type) && GetSimdTypeForSize(simdSize) == op2.Type);
        assert(varTypeIsArithmetic(simdBaseType));

        var intrinsic = simdSize switch {
            64 => NI_AVX512_StoreAlignedNonTemporal,
            32 => NI_AVX_StoreAlignedNonTemporal,
            _ => NI_X86Base_StoreAlignedNonTemporal,
        };
        if (reverseOps)
        {
            gtPrepareOperandsForReordering(ref op2, ref op1);
        }
        return gtNewSimdHWIntrinsicNode(TYP_VOID, intrinsic, simdBaseType, simdSize, op1, op2);
    }

    public GenTree gtNewSimdDotProdNode(var_types type, GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.SIMDDotProductsRequireHardwareIntrinsics);
            return default!;
        }

        var simdType = GetSimdTypeForSize(simdSize);
        assert(varTypeIsSimd(simdType) && varTypeIsSimd(type));
        assert(op1 is not null && op1.Type == simdType);
        assert(op2 is not null && op2.Type == simdType);
        if (Configuration.TARGET_XARCH)
        {
            assert(!varTypeIsByte(simdBaseType) && !varTypeIsLong(simdBaseType));
            assert(simdSize != 64);
            assert(simdSize != 32 || varTypeIsFloating(simdBaseType) ||
                compIsaSupportedDebugOnly(InstructionSet_AVX2));
        }
        else if (Configuration.TARGET_ARM64)
        {
            assert(!varTypeIsLong(simdBaseType));
        }
        return gtNewSimdHWIntrinsicNode(type, NI_Vector_Dot, simdBaseType, simdSize, op1, op2);
    }

    public GenTree gtNewSimdFmaNode(var_types type, GenTree op1, GenTree op2, GenTree op3,
        var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.SIMDFusedMultiplyAddRequiresHardwareIntrinsics
    );
            return default!;
        }

        assert(varTypeIsSimd(type) && GetSimdTypeForSize(simdSize) == type);
        assert(op1 is not null && op1.Type == type);
        assert(op2 is not null && op2.Type == type);
        assert(op3 is not null && op3.Type == type);
        assert(varTypeIsFloating(simdBaseType));
        if (Configuration.TARGET_XARCH)
        {
            var intrinsic = simdSize == 64 ? NI_AVX512_FusedMultiplyAdd : NI_AVX2_MultiplyAdd;
            if (simdSize != 64)
            {
                assert(compIsaSupportedDebugOnly(InstructionSet_AVX2));
            }

            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2, op3);
        }
        else if (Configuration.TARGET_ARM64)
        {
            NamedIntrinsic intrinsic;
            if (simdBaseType == TYP_DOUBLE)
            {
                intrinsic = simdSize == 8 ? NI_AdvSimd_FusedMultiplyAddScalar : NI_AdvSimd_Arm64_FusedMultiplyAdd;
            }
            else
            {
                intrinsic = NI_AdvSimd_FusedMultiplyAdd;
            }

            // AdvSimd.FusedMultiplyAdd expects (addend, left, right), while the APIs take (left, right, addend).
            // We expect op1 and op2 to have already been spilled.
            (op1, op3) = (op3, op1);
            assert(intrinsic != NI_Illegal);

            return gtNewSimdHWIntrinsicNode(type, intrinsic, simdBaseType, simdSize, op1, op2, op3);
        }
        else if (Configuration.TARGET_WASM)
        {
            // Wasm has no single rounding FMA, and this should be guarded against already in import.
            unreached();
            FatalJitException.ThrowFatalJitException(CORJIT_RECOVERABLEERROR, FatalJitErrorCode.UnreachableWasmSIMDFusedMultiplyAdd);
            return default!;
        }

        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.GtNewSimdFmaNodeRequiresItsTargetSpecificImplementation);
        return default!;
    }

    public GenTree gtNewSimdCmpOpAnyNode(genTreeOps op, var_types type, GenTree op1, GenTree op2,
        var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.SIMDComparisonsRequireHardwareIntrinsics);
            return default!;
        }

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
                assert(!Configuration.TARGET_XARCH || simdSize != 32 ||
                    compIsaSupportedDebugOnly(InstructionSet_AVX2));
                op1 = gtNewSimdCmpOpNode(op, simdType, op1, op2, simdBaseType, simdSize);
                op2 = gtNewZeroConNode(simdType);
                simdBaseType = simdBaseType switch {
                    TYP_FLOAT => TYP_INT,
                    TYP_DOUBLE => TYP_LONG,
                    _ => simdBaseType,
                };
                break;
            }

            case GT_NE:
            {
                assert(!Configuration.TARGET_XARCH || simdSize != 32 || varTypeIsFloating(simdBaseType) ||
                    compIsaSupportedDebugOnly(InstructionSet_AVX2));
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

    public GenTree gtNewSimdTernaryLogicNode(var_types type, GenTree op1, GenTree op2, GenTree op3,
        GenTree op4, var_types simdBaseType, byte simdSize)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.SIMDTernaryLogicRequiresXarchHardwareIntrinsics
    );
            return default!;
        }

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
}
