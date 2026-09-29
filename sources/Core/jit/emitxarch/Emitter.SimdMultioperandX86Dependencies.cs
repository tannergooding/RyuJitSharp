// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    public bool UseSimdEncoding()
    {
        return UseVexEncodings || UseEvexEncodings;
    }

#if FEATURE_HW_INTRINSICS
    public static bool Is3OpRmwInstruction(instruction ins)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "x86 three-operand RMW instruction classification is not ported.");
    }

    private static bool isAvx512Blendv(instruction ins)
    {
        return ins is INS_vblendmps or INS_vblendmpd or INS_vpblendmb or INS_vpblendmd or INS_vpblendmq or INS_vpblendmw;
    }

    private static bool isAvxBlendv(instruction ins)
    {
        return ins is INS_vblendvps or INS_vblendvpd or INS_vpblendvb;
    }

    private static bool isSse41Blendv(instruction ins)
    {
        return ins is INS_blendvps or INS_blendvpd or INS_pblendvb;
    }
#endif
}
#endif
