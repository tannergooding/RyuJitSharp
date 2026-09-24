// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
#if FEATURE_HW_INTRINSICS
    public static bool isImmOp(NamedIntrinsic intrinsic, GenTree operand)
    {
#if TARGET_XARCH
        if (lookupCategory(intrinsic) is not HW_Category_IMM)
        {
            return false;
        }
        if ((lookupFlags(intrinsic) & HW_Flag_MaybeIMM) == 0)
        {
            return true;
        }
#else
        if (!HasImmediateOperand(intrinsic))
        {
            return false;
        }
#endif
        return operand.Type.ActualType is TYP_INT;
    }

#if TARGET_XARCH
    public static bool HasFullRangeImm(NamedIntrinsic intrinsic) => (lookupFlags(intrinsic) & HW_Flag_FullRangeIMM) != 0;

    public static bool isAVX2GatherIntrinsic(NamedIntrinsic intrinsic)
        => intrinsic is NI_AVX2_GatherVector128 or NI_AVX2_GatherVector256 or NI_AVX2_GatherMaskVector128 or NI_AVX2_GatherMaskVector256;

    public static int lookupImmUpperBound(NamedIntrinsic intrinsic)
    {
        if ((lookupFlags(intrinsic) & HW_Flag_EmbRoundingCompatible) != 0)
        {
            // Jump-table fallbacks compute entries 0..11; the managed fallback
            // rejects 0..7 before the embedded-rounding instruction is reached.
            return 11;
        }
        assert(lookupCategory(intrinsic) is HW_Category_IMM);
        switch (intrinsic)
        {
            case NI_AVX_Compare:
            case NI_AVX_CompareScalar:
            case NI_AVX512_Compare:
            case NI_AVX512_CompareMask:
            case NI_AVX512_CompareScalarMask:
            case NI_AVX10v2_MinMaxScalar:
            case NI_AVX10v2_MinMax:
            {
                assert(!HasFullRangeImm(intrinsic));
                return 31;
            }
            case NI_AVX2_GatherVector128:
            case NI_AVX2_GatherVector256:
            case NI_AVX2_GatherMaskVector128:
            case NI_AVX2_GatherMaskVector256:
            {
                assert(!HasFullRangeImm(intrinsic));
                return 8;
            }
            case NI_AVX512_GetMantissa:
            case NI_AVX512_GetMantissaScalar:
            case NI_AVX512_Range:
            case NI_AVX512_RangeScalar:
            {
                assert(!HasFullRangeImm(intrinsic));
                return 15;
            }
            default:
            {
                assert(HasFullRangeImm(intrinsic));
                return 255;
            }
        }
    }
#endif
#endif
}
