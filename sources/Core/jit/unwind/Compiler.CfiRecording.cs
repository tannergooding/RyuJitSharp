// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_CFI_SUPPORT
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private const short DWARF_REG_ILLEGAL = -1;
    private const byte CFI_ADJUST_CFA_OFFSET = 0;
    private const byte CFI_DEF_CFA_REGISTER = 1;
    private const byte CFI_REL_OFFSET = 2;
    private const byte CFI_NEGATE_RA_STATE = 4;

#if TARGET_AMD64
    private static regMaskTP CfiCalleeSavedMask =>
        new(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED, SRBM_MSK_CALLEE_SAVED);
#else
    private static regMaskTP CfiCalleeSavedMask =>
        new(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED);
#endif

    private static void createCfiCode(in FuncInfoDsc func, uint codeOffset, byte cfiOpcode, short dwarfReg, int offset = 0)
    {
        noway_assert(unchecked((byte)codeOffset) == codeOffset);
        var cfiEntry = new CFI_CODE(unchecked((byte)codeOffset), cfiOpcode, dwarfReg, offset);
        noway_assert(func.cfiCodes is not null);
        func.cfiCodes.Add(cfiEntry);
    }

    private void unwindPushPopCFI(regNumber reg)
    {
        assert(UnwindEmitter().emitGeneratingPrologOrFuncletProlog());

        ref var func = ref funCurrentFunc();
        var cbProlog = unwindGetCurrentOffset(in func);
        var relOffsetMask = CfiCalleeSavedMask;

#if UNIX_AMD64_ABI && ETW_EBP_FRAMED
        // ETW removes RBP from the callee-save mask, but its frame-register push still needs unwind info.
        relOffsetMask |= new regMaskTP(SRBM_FPBASE);
#endif
#if TARGET_ARM
        relOffsetMask |= new regMaskTP(regMask.SRBM_R11 | regMask.SRBM_LR | regMask.SRBM_PC);
        createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL,
            reg >= REG_FP_FIRST ? 2 * REGSIZE_BYTES : REGSIZE_BYTES);
#else
        assert(reg < REG_FP_FIRST);
        createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL, REGSIZE_BYTES);
#endif
        if ((bool)(relOffsetMask & genRegMask(reg)))
        {
            createCfiCode(in func, cbProlog, CFI_REL_OFFSET, mapRegNumToDwarfReg(reg));
        }
    }

    private void unwindPush2Pop2CFI(regNumber reg1, regNumber reg2)
    {
        // Upstream uses two separate records until the OS supports push2/pop2 unwind codes.
        unwindPushPopCFI(reg1);
        unwindPushPopCFI(reg2);
    }

    private void unwindBegPrologCFI()
    {
        assert(UnwindEmitter().emitGeneratingPrologOrFuncletProlog());
        ref var func = ref funCurrentFunc();

        unwindGetFuncLocations(in func, true, out func.startLoc, out func.endLoc);
        if (fgFirstColdBlock is not null)
        {
            unwindGetFuncLocations(in func, false, out func.coldStartLoc, out func.coldEndLoc);
        }

        func.cfiCodes = [];
    }

    private void unwindPushPopMaskCFI(regMaskTP regMask, bool isFloat)
    {
#if TARGET_ARM
        var regNum = isFloat ? REG_FP_LAST - 1 : REG_INT_LAST;
        var regBit = isFloat ? genRegMask(regNum) | genRegMask(regNum + 1) : genRegMask(regNum);
#else
        var regNum = isFloat ? REG_FP_LAST : REG_INT_LAST;
        var regBit = genRegMask(regNum);
#endif

        while (!regMask.IsEmpty && !regBit.IsEmpty)
        {
            if ((bool)(regBit & regMask))
            {
                unwindPushPopCFI(regNum);
                regMask &= ~regBit;
            }

#if TARGET_ARM
            // LLVM DWARF names D0-D15, so pair the emitter's S0-S31 registers.
            regBit >>= isFloat ? 2 : 1;
            regNum = unchecked((regNumber)((int)regNum - (isFloat ? 2 : 1)));
#else
            regBit >>= 1;
            regNum = unchecked(regNum - 1);
#endif
        }
    }

    private void unwindAllocStackCFI(uint size)
    {
        assert(UnwindEmitter().emitGeneratingPrologOrFuncletProlog());
        ref var func = ref funCurrentFunc();
        uint cbProlog = 0;

        if (UnwindEmitter().emitGeneratingPrologOrFuncletProlog())
        {
            cbProlog = unwindGetCurrentOffset(in func);
        }

        createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL, unchecked((int)size));
    }

    private void unwindSetFrameRegCFI(regNumber reg, uint offset)
    {
        assert(UnwindEmitter().emitGeneratingPrologOrFuncletProlog());
        ref var func = ref funCurrentFunc();
        var cbProlog = unwindGetCurrentOffset(in func);

        createCfiCode(in func, cbProlog, CFI_DEF_CFA_REGISTER, mapRegNumToDwarfReg(reg));
        if (offset != 0)
        {
            // Preserve the old CFA address when switching from RSP to RBP = RSP + offset.
            var adjust = unchecked(-(int)offset);
            createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL, adjust);
        }
    }

#if TARGET_LOONGARCH64
    private static short mapRegNumToDwarfReg(regNumber reg)
    {
        // LoongArch register numbers are the DWARF encodings for R0-R31 and F0-F31.
        if ((byte)reg <= (byte)REG_F31)
        {
            return (short)reg;
        }

        NYI("CFI codes");
        return DWARF_REG_ILLEGAL;
    }
#elif TARGET_RISCV64
    private static short mapRegNumToDwarfReg(regNumber reg)
    {
        // RISC-V integer and floating-point register ordinals are their DWARF numbers.
        if ((reg >= REG_INT_FIRST) && (reg <= REG_FP_LAST))
        {
            return (short)reg;
        }

        NYI("CFI codes");
        return DWARF_REG_ILLEGAL;
    }
#elif TARGET_ARM
    private static short mapRegNumToDwarfReg(regNumber reg)
    {
        return reg switch
        {
            REG_R0 => 0,
            REG_R1 => 1,
            REG_R2 => 2,
            REG_R3 => 3,
            REG_R4 => 4,
            REG_R5 => 5,
            REG_R6 => 6,
            REG_R7 => 7,
            REG_R8 => 8,
            REG_R9 => 9,
            REG_R10 => 10,
            REG_R11 => 11,
            REG_R12 => 12,
            REG_R13 => 13,
            REG_R14 => 14,
            REG_R15 => 15,
            REG_F0 => 256,
            REG_F2 => 257,
            REG_F4 => 258,
            REG_F6 => 259,
            REG_F8 => 260,
            REG_F10 => 261,
            REG_F12 => 262,
            REG_F14 => 263,
            REG_F16 => 264,
            REG_F18 => 265,
            REG_F20 => 266,
            REG_F22 => 267,
            REG_F24 => 268,
            REG_F26 => 269,
            REG_F28 => 270,
            REG_F30 => 271,
            _ => throw new FatalJitException(CORJIT_IMPLLIMITATION, "Unexpected ARM DWARF register."),
        };
    }
#elif !TARGET_ARM64 && (!TARGET_AMD64 || !UNIX_AMD64_ABI)
    private static short mapRegNumToDwarfReg(regNumber reg)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "The target DWARF register mapping is not ported.");
    }
#endif
}
#endif
