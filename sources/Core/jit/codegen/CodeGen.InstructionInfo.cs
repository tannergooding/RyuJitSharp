// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_XARCH
    public static bool instIsFP(instruction ins)
    {
        assert((uint)ins < (uint)instInfo.Length);
        return (instInfo[(int)ins] & INS_FLAGS_X87Instr) != 0;
    }

    public static int instKMaskBaseSize(instruction ins)
    {
        assert((uint)ins < (uint)s_kMaskBaseSizes.Length);
        return s_kMaskBaseSizes[(int)ins];
    }

    public static bool instIsEmbeddedMaskingCompatible(instruction ins)
    {
        return (ins is not INS_invalid) && (instKMaskBaseSize(ins) != 0);
    }

    public bool IsEmbeddedBroadcastEnabled(instruction ins, GenTree operand)
    {
#if FEATURE_HW_INTRINSICS
        return Emitter.UseEvexEncodings && instIsEmbeddedBroadcastCompatible(ins) &&
            operand.IsContained && operand.Oper.IsHWIntrinsic &&
            operand.AsHWIntrinsic().IsBroadcastScalar;
#else
        return false;
#endif
    }
#endif
}
