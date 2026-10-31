// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private const short DWARF_REG_ILLEGAL = -1;
    private const byte CFI_ADJUST_CFA_OFFSET = 0;
    private const byte CFI_DEF_CFA_REGISTER = 1;
    private const byte CFI_REL_OFFSET = 2;
    private const byte CFI_NEGATE_RA_STATE = 4;

    // AMD64 has no callee-saved mask registers, so the upper bank is empty on every target.
    private static regMaskTP CfiCalleeSavedMask =>
        GetCfiRegisterPolicy().CalleeSavedMask;

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

        var (CalleeSavedMask, FirstFloatRegister, _, _, RegisterSize) = GetCfiRegisterPolicy();
        var relOffsetMask = CalleeSavedMask;

        if (Configuration.UNIX_AMD64_ABI && Configuration.ETW_EBP_FRAMED)
        {
            // ETW removes RBP from the callee-save mask, but its frame-register push still needs unwind info.
            relOffsetMask |= new regMaskTP(regMask.SRBM_X64_RBP);
        }

        if (Configuration.TARGET_ARM)
        {
            relOffsetMask |= new regMaskTP(regMask.SRBM_ARM32_R11 | regMask.SRBM_ARM32_LR | regMask.SRBM_ARM32_PC);
            createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL,
                reg >= FirstFloatRegister ? 2 * RegisterSize : RegisterSize);
        }
        else
        {
            assert(reg < FirstFloatRegister);
            createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL, RegisterSize);
        }

        if ((bool)(relOffsetMask & genRegMask(reg)))
        {
            createCfiCode(in func, cbProlog, CFI_REL_OFFSET, MapCfiRegNumToDwarfReg(reg));
        }
    }

    private void unwindPush2Pop2CFI(regNumber reg1, regNumber reg2)
    {
        assert(UnwindEmitter().emitGeneratingPrologOrFuncletProlog());

        ref var func = ref funCurrentFunc();
        var cbProlog = unwindGetCurrentOffset(in func);
        var (calleeSavedMask, _, _, _, registerSize) = GetCfiRegisterPolicy();
        var relOffsetMask = calleeSavedMask;

        if (Configuration.UNIX_AMD64_ABI && Configuration.ETW_EBP_FRAMED)
        {
            relOffsetMask |= new regMaskTP(regMask.SRBM_X64_RBP);
        }

        createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL, 2 * registerSize);

        if ((bool)(relOffsetMask & genRegMask(reg1)))
        {
            createCfiCode(in func, cbProlog, CFI_REL_OFFSET, MapCfiRegNumToDwarfReg(reg1), registerSize);
        }

        if ((bool)(relOffsetMask & genRegMask(reg2)))
        {
            createCfiCode(in func, cbProlog, CFI_REL_OFFSET, MapCfiRegNumToDwarfReg(reg2));
        }
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
        // LLVM DWARF names D0-D15, so pair the ARM emitter's S0-S31 registers.
        var (_, _, LastFloatRegister, LastIntegerRegister, _) = GetCfiRegisterPolicy();
        var pairFloatRegisters = Configuration.TARGET_ARM && isFloat;
        var regNum = isFloat
            ? (pairFloatRegisters ? LastFloatRegister - 1 : LastFloatRegister)
            : LastIntegerRegister;
        var regBit = pairFloatRegisters ? genRegMask(regNum) | genRegMask(regNum + 1) : genRegMask(regNum);

        while (!regMask.IsEmpty && !regBit.IsEmpty)
        {
            if ((bool)(regBit & regMask))
            {
                unwindPushPopCFI(regNum);
                regMask &= ~regBit;
            }

            regBit >>= pairFloatRegisters ? 2 : 1;
            regNum = unchecked(regNum - (pairFloatRegisters ? 2u : 1u));
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

        createCfiCode(in func, cbProlog, CFI_DEF_CFA_REGISTER, MapCfiRegNumToDwarfReg(reg));
        if (offset != 0)
        {
            // Preserve the old CFA address when switching from RSP to RBP = RSP + offset.
            var adjust = unchecked(-(int)offset);
            createCfiCode(in func, cbProlog, CFI_ADJUST_CFA_OFFSET, DWARF_REG_ILLEGAL, adjust);
        }
    }

    private static short MapCfiRegNumToDwarfReg(regNumber reg)
    {
        if (Configuration.TARGET_ARM || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            return MapPortableCfiRegNumToDwarfReg(reg);
        }

        if (Configuration.TARGET_ARM64)
        {
            return MapArm64CfiRegNumToDwarfReg(reg);
        }

        if (Configuration.UNIX_AMD64_ABI)
        {
            return MapAmd64CfiRegNumToDwarfReg(reg);
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.TargetDWARFRegisterMappingIsNotPorted);
        return default!;
    }

    private static short MapPortableCfiRegNumToDwarfReg(regNumber reg)
    {
        if (Configuration.TARGET_LOONGARCH64)
        {
            // LoongArch register numbers are the DWARF encodings for R0-R31 and F0-F31.
            if ((byte)reg <= (byte)REG_LOONGARCH64_F31)
            {
                return (short)reg;
            }

            NYI("CFI codes");
            return DWARF_REG_ILLEGAL;
        }

        if (Configuration.TARGET_RISCV64)
        {
            // RISC-V integer and floating-point register ordinals are their DWARF numbers.
            if ((reg >= REG_RISCV64_R0) && (reg <= REG_RISCV64_FT11))
            {
                return (short)reg;
            }

            NYI("CFI codes");
            return DWARF_REG_ILLEGAL;
        }

        if (Configuration.TARGET_ARM)
        {
            switch (reg)
            {
                case REG_ARM32_R0:
                {
                    return 0;
                }

                case REG_ARM32_R1:
                {
                    return 1;
                }

                case REG_ARM32_R2:
                {
                    return 2;
                }

                case REG_ARM32_R3:
                {
                    return 3;
                }

                case REG_ARM32_R4:
                {
                    return 4;
                }

                case REG_ARM32_R5:
                {
                    return 5;
                }

                case REG_ARM32_R6:
                {
                    return 6;
                }

                case REG_ARM32_R7:
                {
                    return 7;
                }

                case REG_ARM32_R8:
                {
                    return 8;
                }

                case REG_ARM32_R9:
                {
                    return 9;
                }

                case REG_ARM32_R10:
                {
                    return 10;
                }

                case REG_ARM32_R11:
                {
                    return 11;
                }

                case REG_ARM32_R12:
                {
                    return 12;
                }

                case REG_ARM32_SP:
                {
                    return 13;
                }

                case REG_ARM32_LR:
                {
                    return 14;
                }

                case REG_ARM32_PC:
                {
                    return 15;
                }

                case REG_ARM32_F0:
                {
                    return 256;
                }

                case REG_ARM32_F2:
                {
                    return 257;
                }

                case REG_ARM32_F4:
                {
                    return 258;
                }

                case REG_ARM32_F6:
                {
                    return 259;
                }

                case REG_ARM32_F8:
                {
                    return 260;
                }

                case REG_ARM32_F10:
                {
                    return 261;
                }

                case REG_ARM32_F12:
                {
                    return 262;
                }

                case REG_ARM32_F14:
                {
                    return 263;
                }

                case REG_ARM32_F16:
                {
                    return 264;
                }

                case REG_ARM32_F18:
                {
                    return 265;
                }

                case REG_ARM32_F20:
                {
                    return 266;
                }

                case REG_ARM32_F22:
                {
                    return 267;
                }

                case REG_ARM32_F24:
                {
                    return 268;
                }

                case REG_ARM32_F26:
                {
                    return 269;
                }

                case REG_ARM32_F28:
                {
                    return 270;
                }

                case REG_ARM32_F30:
                {
                    return 271;
                }

                default:
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_IMPLLIMITATION, FatalJitErrorCode.UnexpectedARMDWARFRegister);
                    return default!;
                }
            }
        }

        FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.TargetDWARFRegisterMappingIsNotPorted);
        return default!;
    }

    private static short MapAmd64CfiRegNumToDwarfReg(regNumber reg)
    {
        var dwarfReg = DWARF_REG_ILLEGAL;

        switch (reg)
        {
            case REG_X64_RAX:
            {
                dwarfReg = 0;
                break;
            }

            case REG_X64_RCX:
            {
                dwarfReg = 2;
                break;
            }

            case REG_X64_RDX:
            {
                dwarfReg = 1;
                break;
            }

            case REG_X64_RBX:
            {
                dwarfReg = 3;
                break;
            }

            case REG_X64_RSP:
            {
                dwarfReg = 7;
                break;
            }

            case REG_X64_RBP:
            {
                dwarfReg = 6;
                break;
            }

            case REG_X64_RSI:
            {
                dwarfReg = 4;
                break;
            }

            case REG_X64_RDI:
            {
                dwarfReg = 5;
                break;
            }

            case >= REG_X64_R8 and <= REG_X64_R15:
            {
                dwarfReg = (short)reg;
                break;
            }

            case >= REG_X64_R16 and <= REG_X64_R31:
            {
                dwarfReg = (short)(unchecked((int)reg) + 114);
                break;
            }

            default:
            {
                noway_assert(false, "!\"unexpected REG_NUM\"");
                break;
            }
        }

        return dwarfReg;
    }

    private static short MapArm64CfiRegNumToDwarfReg(regNumber reg)
    {
        short dwarfReg = reg switch {
            REG_ARM64_R0 => 0,
            REG_ARM64_R1 => 1,
            REG_ARM64_R2 => 2,
            REG_ARM64_R3 => 3,
            REG_ARM64_R4 => 4,
            REG_ARM64_R5 => 5,
            REG_ARM64_R6 => 6,
            REG_ARM64_R7 => 7,
            REG_ARM64_R8 => 8,
            REG_ARM64_R9 => 9,
            REG_ARM64_R10 => 10,
            REG_ARM64_R11 => 11,
            REG_ARM64_R12 => 12,
            REG_ARM64_R13 => 13,
            REG_ARM64_R14 => 14,
            REG_ARM64_R15 => 15,
            REG_ARM64_R16 => 16,
            REG_ARM64_R17 => 17,
            REG_ARM64_R18 => 18,
            REG_ARM64_R19 => 19,
            REG_ARM64_R20 => 20,
            REG_ARM64_R21 => 21,
            REG_ARM64_R22 => 22,
            REG_ARM64_R23 => 23,
            REG_ARM64_R24 => 24,
            REG_ARM64_R25 => 25,
            REG_ARM64_R26 => 26,
            REG_ARM64_R27 => 27,
            REG_ARM64_R28 => 28,
            REG_ARM64_R29 => 29,
            REG_ARM64_R30 => 30,
            REG_ARM64_SP => 31,
            REG_ARM64_V0 => 64,
            REG_ARM64_V1 => 65,
            REG_ARM64_V2 => 66,
            REG_ARM64_V3 => 67,
            REG_ARM64_V4 => 68,
            REG_ARM64_V5 => 69,
            REG_ARM64_V6 => 70,
            REG_ARM64_V7 => 71,
            REG_ARM64_V8 => 72,
            REG_ARM64_V9 => 73,
            REG_ARM64_V10 => 74,
            REG_ARM64_V11 => 75,
            REG_ARM64_V12 => 76,
            REG_ARM64_V13 => 77,
            REG_ARM64_V14 => 78,
            REG_ARM64_V15 => 79,
            REG_ARM64_V16 => 80,
            REG_ARM64_V17 => 81,
            REG_ARM64_V18 => 82,
            REG_ARM64_V19 => 83,
            REG_ARM64_V20 => 84,
            REG_ARM64_V21 => 85,
            REG_ARM64_V22 => 86,
            REG_ARM64_V23 => 87,
            REG_ARM64_V24 => 88,
            REG_ARM64_V25 => 89,
            REG_ARM64_V26 => 90,
            REG_ARM64_V27 => 91,
            REG_ARM64_V28 => 92,
            REG_ARM64_V29 => 93,
            REG_ARM64_V30 => 94,
            REG_ARM64_V31 => 95,
            _ => DWARF_REG_ILLEGAL,
        };

        if (dwarfReg == DWARF_REG_ILLEGAL)
        {
            NYI("CFI codes");
        }

        return dwarfReg;
    }
}
