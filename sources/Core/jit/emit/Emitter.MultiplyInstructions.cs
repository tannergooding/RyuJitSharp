// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public partial class Emitter
{
    public static instruction inst3opImulForReg(regNumber reg)
    {
#if TARGET_AMD64
        assert(reg.IsIntReg);
        var ins = INS_imul_AX + (int)reg;
        check3opImulValues();
        assert(instrIs3opImul(ins));

        return ins;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Three-operand IMUL selection requires AMD64.");
#endif
    }

    [Conditional("DEBUG")]
    private static void check3opImulValues()
    {
#if TARGET_AMD64
#pragma warning disable CA1508 // Retain the native encoding-layout assertions.
        assert((int)INS_imul_AX - (int)INS_imul_AX == (int)REG_RAX);
        assert((int)INS_imul_BX - (int)INS_imul_AX == (int)REG_RBX);
        assert((int)INS_imul_CX - (int)INS_imul_AX == (int)REG_RCX);
        assert((int)INS_imul_DX - (int)INS_imul_AX == (int)REG_RDX);
        assert((int)INS_imul_BP - (int)INS_imul_AX == (int)REG_RBP);
        assert((int)INS_imul_SI - (int)INS_imul_AX == (int)REG_RSI);
        assert((int)INS_imul_DI - (int)INS_imul_AX == (int)REG_RDI);
        assert((int)INS_imul_08 - (int)INS_imul_AX == (int)REG_R8);
        assert((int)INS_imul_09 - (int)INS_imul_AX == (int)REG_R9);
        assert((int)INS_imul_10 - (int)INS_imul_AX == (int)REG_R10);
        assert((int)INS_imul_11 - (int)INS_imul_AX == (int)REG_R11);
        assert((int)INS_imul_12 - (int)INS_imul_AX == (int)REG_R12);
        assert((int)INS_imul_13 - (int)INS_imul_AX == (int)REG_R13);
        assert((int)INS_imul_14 - (int)INS_imul_AX == (int)REG_R14);
        assert((int)INS_imul_15 - (int)INS_imul_AX == (int)REG_R15);
        assert((int)INS_imul_16 - (int)INS_imul_AX == (int)REG_R16);
        assert((int)INS_imul_17 - (int)INS_imul_AX == (int)REG_R17);
        assert((int)INS_imul_18 - (int)INS_imul_AX == (int)REG_R18);
        assert((int)INS_imul_19 - (int)INS_imul_AX == (int)REG_R19);
        assert((int)INS_imul_20 - (int)INS_imul_AX == (int)REG_R20);
        assert((int)INS_imul_21 - (int)INS_imul_AX == (int)REG_R21);
        assert((int)INS_imul_22 - (int)INS_imul_AX == (int)REG_R22);
        assert((int)INS_imul_23 - (int)INS_imul_AX == (int)REG_R23);
        assert((int)INS_imul_24 - (int)INS_imul_AX == (int)REG_R24);
        assert((int)INS_imul_25 - (int)INS_imul_AX == (int)REG_R25);
        assert((int)INS_imul_26 - (int)INS_imul_AX == (int)REG_R26);
        assert((int)INS_imul_27 - (int)INS_imul_AX == (int)REG_R27);
        assert((int)INS_imul_28 - (int)INS_imul_AX == (int)REG_R28);
        assert((int)INS_imul_29 - (int)INS_imul_AX == (int)REG_R29);
        assert((int)INS_imul_30 - (int)INS_imul_AX == (int)REG_R30);
        assert((int)INS_imul_31 - (int)INS_imul_AX == (int)REG_R31);
#pragma warning restore CA1508
#endif
    }
}
