// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static uint unsigned_abs(int value)
    {
        RequireArm32Target();
        return value < 0 ? unchecked(0u - (uint)value) : (uint)value;
    }

    private static bool offsetFitsInVectorMem(int disp)
    {
        RequireArm32Target();
        var imm = unsigned_abs(disp);
        return (imm & 0x03fc) == imm;
    }

    public static bool insSetsFlags(insFlags flags)
    {
        RequireArm32Target();
        return flags != INS_FLAGS_NOT_SET;
    }

    public static bool insDoesNotSetFlags(insFlags flags)
    {
        RequireArm32Target();
        return flags != INS_FLAGS_SET;
    }

    public static bool insOptAnyInc(insOpts opt)
    {
        RequireArm32Target();
        return opt == INS_OPTS_LDST_PRE_DEC || opt == INS_OPTS_LDST_POST_INC;
    }
}
