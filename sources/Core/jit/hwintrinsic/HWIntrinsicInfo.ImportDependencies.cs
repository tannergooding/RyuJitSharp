// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
    public static bool HasScalarInputVariant(NamedIntrinsic id)
    {
        if (!Configuration.TARGET_ARM64)
        {
            throw new PlatformNotSupportedException("Scalar-input intrinsic variants are only available on ARM64.");
        }

        var flags = lookupFlags(id);

        return (flags & HW_Flag_HasScalarInputVariant) != 0;
    }

    public static bool BaseTypeFromValueTupleArg(NamedIntrinsic id)
    {
        if (!Configuration.TARGET_ARM64)
        {
            throw new PlatformNotSupportedException("Tuple-derived intrinsic base types are only available on ARM64.");
        }

        var flags = lookupFlags(id);

        return (flags & HW_Flag_BaseTypeFromValueTupleArg) != 0;
    }

    public static NamedIntrinsic GetScalarInputVariant(NamedIntrinsic id)
    {
        if (!Configuration.TARGET_ARM64)
        {
            throw new PlatformNotSupportedException("Scalar-input intrinsic variants are only available on ARM64.");
        }

        assert(HasScalarInputVariant(id));

        return id switch
        {
            NI_Sve_ConditionalExtractAfterLastActiveElement => NI_Sve_ConditionalExtractAfterLastActiveElementScalar,
            NI_Sve_ConditionalExtractLastActiveElement => NI_Sve_ConditionalExtractLastActiveElementScalar,
            NI_Sve_SaturatingDecrementBy16BitElementCount => NI_Sve_SaturatingDecrementBy16BitElementCountScalar,
            NI_Sve_SaturatingDecrementBy32BitElementCount => NI_Sve_SaturatingDecrementBy32BitElementCountScalar,
            NI_Sve_SaturatingDecrementBy64BitElementCount => NI_Sve_SaturatingDecrementBy64BitElementCountScalar,
            NI_Sve_SaturatingIncrementBy16BitElementCount => NI_Sve_SaturatingIncrementBy16BitElementCountScalar,
            NI_Sve_SaturatingIncrementBy32BitElementCount => NI_Sve_SaturatingIncrementBy32BitElementCountScalar,
            NI_Sve_SaturatingIncrementBy64BitElementCount => NI_Sve_SaturatingIncrementBy64BitElementCountScalar,
            _ => throw new FatalJitException("Unexpected ARM64 scalar-input variant intrinsic."),
        };
    }

    public static int lookupImmUpperBound(NamedIntrinsic intrinsic, int simdSize, var_types simdBaseType)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.TARGET_WASM)
        {
            throw new PlatformNotSupportedException("Packed SIMD immediate bounds are only available on WebAssembly.");
        }

        switch (intrinsic)
        {
            case NI_PackedSimd_ExtractScalar:
            case NI_PackedSimd_ReplaceScalar:
            case NI_PackedSimd_LoadScalarAndInsert:
            case NI_PackedSimd_StoreSelectedScalar:
            {
                return Compiler.getSIMDVectorLength(unchecked((uint)simdSize), simdBaseType) - 1;
            }

            default:
            {
                unreached();
                return 0;
            }
        }
    }

    public static void GetImmOpsPositions(NamedIntrinsic id, out int first, out int second)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.TARGET_WASM)
        {
            throw new PlatformNotSupportedException("Packed SIMD immediate operands are only available on WebAssembly.");
        }

        first = -1;
        second = -1;

        switch (id)
        {
            case NI_PackedSimd_ExtractScalar:
            case NI_PackedSimd_ReplaceScalar:
            {
                first = 2;
                break;
            }

            case NI_PackedSimd_StoreSelectedScalar:
            case NI_PackedSimd_LoadScalarAndInsert:
            {
                first = 3;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }
}
