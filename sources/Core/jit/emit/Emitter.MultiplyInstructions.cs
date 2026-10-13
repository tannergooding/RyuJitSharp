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
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Three-operand IMUL selection requires x86 architecture support.");
        }

        assert(reg.IsIntReg);
        var ins = InstructionOrdinalMaps.GetInstructionFromNativeOrdinal(
            InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) + (int)reg);
        check3opImulValues();
        assert(IsThreeOperandImulInstruction(ins));

        return ins;
    }

    [Conditional("DEBUG")]
    private static void check3opImulValues()
    {
        var imulAxOrdinal = InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX);
        if (Configuration.TARGET_X86)
        {
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) - imulAxOrdinal == (int)REG_X86_EAX);
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_BX) - imulAxOrdinal == (int)REG_X86_EBX);
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_CX) - imulAxOrdinal == (int)REG_X86_ECX);
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_DX) - imulAxOrdinal == (int)REG_X86_EDX);
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_BP) - imulAxOrdinal == (int)REG_X86_EBP);
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_SI) - imulAxOrdinal == (int)REG_X86_ESI);
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_DI) - imulAxOrdinal == (int)REG_X86_EDI);
            return;
        }

        if (!Configuration.TARGET_AMD64)
        {
            throw new FatalJitException("Three-operand IMUL register layout is not available for this target.");
        }

#pragma warning disable CA1508 // Retain the native encoding-layout assertions.
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_AX) - imulAxOrdinal == (int)REG_X64_RAX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_BX) - imulAxOrdinal == (int)REG_X64_RBX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_CX) - imulAxOrdinal == (int)REG_X64_RCX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_DX) - imulAxOrdinal == (int)REG_X64_RDX);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_BP) - imulAxOrdinal == (int)REG_X64_RBP);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_SI) - imulAxOrdinal == (int)REG_X64_RSI);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_DI) - imulAxOrdinal == (int)REG_X64_RDI);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_08) - imulAxOrdinal == (int)REG_X64_R8);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_09) - imulAxOrdinal == (int)REG_X64_R9);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_10) - imulAxOrdinal == (int)REG_X64_R10);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_11) - imulAxOrdinal == (int)REG_X64_R11);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_12) - imulAxOrdinal == (int)REG_X64_R12);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_13) - imulAxOrdinal == (int)REG_X64_R13);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_14) - imulAxOrdinal == (int)REG_X64_R14);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_15) - imulAxOrdinal == (int)REG_X64_R15);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_16) - imulAxOrdinal == (int)REG_X64_R16);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_17) - imulAxOrdinal == (int)REG_X64_R17);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_18) - imulAxOrdinal == (int)REG_X64_R18);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_19) - imulAxOrdinal == (int)REG_X64_R19);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_20) - imulAxOrdinal == (int)REG_X64_R20);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_21) - imulAxOrdinal == (int)REG_X64_R21);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_22) - imulAxOrdinal == (int)REG_X64_R22);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_23) - imulAxOrdinal == (int)REG_X64_R23);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_24) - imulAxOrdinal == (int)REG_X64_R24);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_25) - imulAxOrdinal == (int)REG_X64_R25);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_26) - imulAxOrdinal == (int)REG_X64_R26);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_27) - imulAxOrdinal == (int)REG_X64_R27);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_28) - imulAxOrdinal == (int)REG_X64_R28);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_29) - imulAxOrdinal == (int)REG_X64_R29);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_30) - imulAxOrdinal == (int)REG_X64_R30);
        assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_imul_31) - imulAxOrdinal == (int)REG_X64_R31);
#pragma warning restore CA1508
    }
}
