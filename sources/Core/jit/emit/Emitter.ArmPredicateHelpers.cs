// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public partial class Emitter
{
    public static bool isGeneralRegister(regNumber reg)
    {
        return reg <= REG_R15;
    }

    public static bool isFloatReg(regNumber reg)
    {
        return (uint)reg >= (uint)REG_F0 && (uint)reg <= (uint)REG_F31;
    }

    public static bool isDoubleReg(regNumber reg)
    {
        return isFloatReg(reg) && ((uint)reg % 2) == 0;
    }

    private static uint unsigned_abs(int value)
    {
        return value < 0 ? unchecked(0u - (uint)value) : (uint)value;
    }

    public static bool insSetsFlags(insFlags flags)
    {
        return flags != INS_FLAGS_NOT_SET;
    }

    public static bool insDoesNotSetFlags(insFlags flags)
    {
        return flags != INS_FLAGS_SET;
    }

    public static insFlags insMustSetFlags(insFlags flags)
    {
        return flags == INS_FLAGS_SET ? INS_FLAGS_SET : INS_FLAGS_NOT_SET;
    }

    public static insFlags insMustNotSetFlags(insFlags flags)
    {
        return flags == INS_FLAGS_NOT_SET ? INS_FLAGS_NOT_SET : INS_FLAGS_SET;
    }

    public static bool insOptsNone(insOpts opt)
    {
        return opt == INS_OPTS_NONE;
    }

    public static bool insOptAnyInc(insOpts opt)
    {
        return opt == INS_OPTS_LDST_PRE_DEC || opt == INS_OPTS_LDST_POST_INC;
    }

    public static bool insOptsPreDec(insOpts opt)
    {
        return opt == INS_OPTS_LDST_PRE_DEC;
    }

    public static bool insOptsPostInc(insOpts opt)
    {
        return opt == INS_OPTS_LDST_POST_INC;
    }

    public static bool insOptAnyShift(insOpts opt)
    {
        var value = (uint)opt;
        return value >= (uint)INS_OPTS_RRX && value <= (uint)INS_OPTS_ROR;
    }

    public static bool insOptsRRX(insOpts opt)
    {
        return opt == INS_OPTS_RRX;
    }

    public static bool insOptsLSL(insOpts opt)
    {
        return opt == INS_OPTS_LSL;
    }

    public static bool insOptsLSR(insOpts opt)
    {
        return opt == INS_OPTS_LSR;
    }

    public static bool insOptsASR(insOpts opt)
    {
        return opt == INS_OPTS_ASR;
    }

    public static bool insOptsROR(insOpts opt)
    {
        return opt == INS_OPTS_ROR;
    }

    public static uint getBitWidth(emitAttr size)
    {
        assert((int)size <= (int)EA_8BYTE);
        return unchecked((uint)size * (uint)BITS_PER_BYTE);
    }
}
#endif
