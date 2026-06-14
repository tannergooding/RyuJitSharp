// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct HWIntrinsicInfo
{
    public static byte GetMultiRegCount(NamedIntrinsic id)
    {
        assert(IsMultiReg(id));

        switch (id)
        {
#if TARGET_ARM64
            case NI_AdvSimd_Arm64_LoadPairScalarVector64:
            case NI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal:
            case NI_AdvSimd_Arm64_LoadPairVector64:
            case NI_AdvSimd_Arm64_LoadPairVector64NonTemporal:
            case NI_AdvSimd_Arm64_LoadPairVector128:
            case NI_AdvSimd_Arm64_LoadPairVector128NonTemporal:
            case NI_AdvSimd_Load2xVector64AndUnzip:
            case NI_AdvSimd_Arm64_Load2xVector128AndUnzip:
            case NI_AdvSimd_Load2xVector64:
            case NI_AdvSimd_Arm64_Load2xVector128:
            case NI_AdvSimd_LoadAndInsertScalarVector64x2:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
            case NI_AdvSimd_LoadAndReplicateToVector64x2:
            case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x2:
            case NI_Sve_Load2xVectorAndUnzip:
            {
                return 2;
            }

            case NI_AdvSimd_Load3xVector64AndUnzip:
            case NI_AdvSimd_Arm64_Load3xVector128AndUnzip:
            case NI_AdvSimd_Load3xVector64:
            case NI_AdvSimd_Arm64_Load3xVector128:
            case NI_AdvSimd_LoadAndInsertScalarVector64x3:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
            case NI_AdvSimd_LoadAndReplicateToVector64x3:
            case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x3:
            case NI_Sve_Load3xVectorAndUnzip:
            {
                return 3;
            }

            case NI_AdvSimd_Load4xVector64AndUnzip:
            case NI_AdvSimd_Arm64_Load4xVector128AndUnzip:
            case NI_AdvSimd_Load4xVector64:
            case NI_AdvSimd_Arm64_Load4xVector128:
            case NI_AdvSimd_LoadAndInsertScalarVector64x4:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
            case NI_AdvSimd_LoadAndReplicateToVector64x4:
            case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x4:
            case NI_Sve_Load4xVectorAndUnzip:
            {
                return 4;
            }
#endif

#if TARGET_XARCH
            case NI_X86Base_DivRem:
            case NI_X86Base_X64_BigMul:
            case NI_X86Base_X64_DivRem:
            {
                return 2;
            }
#endif

            default:
            {
                unreached();
                return 0;
            }
        }
    }

    public static bool HasSpecialSideEffect(NamedIntrinsic id)
    {
        // TODO: Port HWIntrinsicInfo.HasSpecialSideEffect
        return false;
    }

    public static bool IsMultiReg(NamedIntrinsic id)
    {
        // TODO: Port HWIntrinsicInfo.IsMultiReg
        return false;
    }

    public static HWIntrinsicCategory lookupCategory(NamedIntrinsic id)
    {
        assert(id is > NI_HW_INTRINSIC_START and < NI_HW_INTRINSIC_END);
        return s_categories[id - NI_HW_INTRINSIC_START];
    }

    public static HWIntrinsicFlag lookupFlags(NamedIntrinsic id)
    {
        assert(id is > NI_HW_INTRINSIC_START and < NI_HW_INTRINSIC_END);
        return s_flags[id - NI_HW_INTRINSIC_START];
    }

#if TARGET_XARCH
    public static byte lookupFltCost(NamedIntrinsic id)
    {
        assert(id is > NI_HW_INTRINSIC_START and < NI_HW_INTRINSIC_END);
        return s_fltCosts[id - NI_HW_INTRINSIC_START];
    }

    public static byte lookupIntCost(NamedIntrinsic id)
    {
        assert(id is > NI_HW_INTRINSIC_START and < NI_HW_INTRINSIC_END);
        return s_intCosts[id - NI_HW_INTRINSIC_START];
    }
#endif

#if DEBUG
    public static string lookupName(NamedIntrinsic id)
    {
        assert(id is > NI_HW_INTRINSIC_START and < NI_HW_INTRINSIC_END);
        return s_names[id - NI_HW_INTRINSIC_START];
    }
#endif

#if TARGET_XARCH
    public static bool MaybeMemoryLoad(NamedIntrinsic id)
    {
        var flags = lookupFlags(id);
        return (flags & HW_Flag_MaybeMemoryLoad) != 0;
    }

    public static bool MaybeMemoryStore(NamedIntrinsic id)
    {
        var flags = lookupFlags(id);
        return (flags & HW_Flag_MaybeMemoryStore) != 0;
    }
#endif
}
