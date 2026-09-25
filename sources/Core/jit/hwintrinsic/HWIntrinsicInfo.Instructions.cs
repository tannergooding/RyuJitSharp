// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly partial struct HWIntrinsicInfo
{
#if FEATURE_HW_INTRINSICS
    public static instruction lookupIns(NamedIntrinsic id, var_types type, Compiler? compiler)
    {
        if ((type < TYP_BYTE) || (type > TYP_DOUBLE))
        {
            assert(false, "Unexpected type");
            return INS_invalid;
        }

        var instructionCount = (int)TYP_DOUBLE - (int)TYP_BYTE + 1;
        var ins = s_instructions[(GetTableIndex(id) * instructionCount) + (int)type - (int)TYP_BYTE];
#if TARGET_X86
        if (ins is INS_movd64)
        {
            ins = INS_movd32;
        }
#endif
#if TARGET_XARCH
        if (compiler is not null)
        {
            var evexIns = ins switch {
                INS_movdqa32 when varTypeIsLong(type) => INS_vmovdqa64,
                INS_movdqu32 when varTypeIsLong(type) => INS_vmovdqu64,
                INS_pandd when varTypeIsLong(type) => INS_vpandq,
                INS_pandnd when varTypeIsLong(type) => INS_vpandnq,
                INS_pord when varTypeIsLong(type) => INS_vporq,
                INS_pxord when varTypeIsLong(type) => INS_vpxorq,
                INS_vbroadcastf32x4 when type is TYP_DOUBLE => INS_vbroadcastf64x2,
                INS_vbroadcasti32x4 when varTypeIsLong(type) => INS_vbroadcasti64x2,
                INS_vextractf32x4 when type is TYP_DOUBLE => INS_vextractf64x2,
                INS_vextractf32x4 when varTypeIsInt(type) => INS_vextracti32x4,
                INS_vextractf32x4 when varTypeIsLong(type) => INS_vextracti64x2,
                INS_vextracti32x4 when varTypeIsLong(type) => INS_vextracti64x2,
                INS_vinsertf32x4 when type is TYP_DOUBLE => INS_vinsertf64x2,
                INS_vinsertf32x4 when varTypeIsInt(type) => INS_vinserti32x4,
                INS_vinsertf32x4 when varTypeIsLong(type) => INS_vinserti64x2,
                INS_vinserti32x4 when varTypeIsLong(type) => INS_vinserti64x2,
                _ => ins,
            };
            if ((evexIns != ins) && compiler.canUseEvexEncoding())
            {
                ins = evexIns;
            }
        }
#endif
        return ins;
    }

    public static bool HasEvexSemantics(NamedIntrinsic id)
    {
#if TARGET_XARCH
        return (lookupFlags(id) & HW_Flag_NoEvexSemantics) == 0;
#else
        return false;
#endif
    }

    public static bool HasRMWSemantics(NamedIntrinsic id)
    {
#if TARGET_XARCH
        return (lookupFlags(id) & HW_Flag_NoRMWSemantics) == 0;
#elif TARGET_ARM64
        return (lookupFlags(id) & HW_Flag_HasRMWSemantics) != 0;
#else
        return false;
#endif
    }

#if TARGET_XARCH
    public static bool IsRmwIntrinsic(NamedIntrinsic id) => (lookupFlags(id) & HW_Flag_RmwIntrinsic) != 0;

    public static bool genIsTableDrivenHWIntrinsic(NamedIntrinsic id, HWIntrinsicCategory category)
    {
        return (category is not HW_Category_Special and not HW_Category_Scalar and not HW_Category_Helper) &&
            ((lookupFlags(id) & HW_Flag_SpecialCodeGen) == 0);
    }
#endif
#endif
}
