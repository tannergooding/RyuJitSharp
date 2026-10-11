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
#if TARGET_XARCH
        assert(reg.IsIntReg);
        var ins = InstructionOrdinalMaps.GetInstructionFromNativeOrdinal(
            InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) + (int)reg);
        check3opImulValues();
        assert(instrIs3opImul(ins));

        return ins;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Three-operand IMUL selection requires x86 architecture support.");
#endif
    }

    [Conditional("DEBUG")]
    private static void check3opImulValues()
    {
#if TARGET_X86
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_EAX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_BX) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_EBX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_CX) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_ECX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_DX) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_EDX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_BP) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_EBP);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_SI) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_ESI);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_DI) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_EDI);
#elif TARGET_AMD64
#pragma warning disable CA1508 // Retain the native encoding-layout assertions.
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_RAX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_BX) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_RBX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_CX) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_RCX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_DX) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_RDX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_BP) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_RBP);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_SI) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_RSI);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_DI) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_RDI);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_08) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R8);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_09) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R9);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_10) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R10);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_11) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R11);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_12) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R12);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_13) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R13);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_14) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R14);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_15) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R15);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_16) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R16);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_17) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R17);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_18) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R18);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_19) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R19);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_20) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R20);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_21) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R21);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_22) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R22);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_23) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R23);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_24) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R24);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_25) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R25);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_26) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R26);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_27) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R27);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_28) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R28);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_29) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R29);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_30) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R30);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_31) -
               InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) == (int)REG_R31);
#pragma warning restore CA1508
#endif
    }
}
