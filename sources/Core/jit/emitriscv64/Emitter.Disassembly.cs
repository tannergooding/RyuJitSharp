// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
#if DEBUG
    public string emitRegNameRiscV64(regNumber reg, emitAttr size, bool varName)
    {
        assert((uint)reg < (uint)REG_COUNT);
        return RiscVRegisterName((uint)reg);
    }
#endif

    private static string RiscVRegisterName(uint reg)
    {
        assert(reg < (uint)REG_COUNT);
        return ((regNumber)reg).Name;
    }

    private bool emitDispBranchInstrTypeRiscV64(uint opcode2, bool isZeroReg, out bool printSecondReg)
    {
        printSecondReg = true;
        switch (opcode2)
        {
            case 0:
            {
                jitprintf(isZeroReg ? "beqz" : "beq ");
                printSecondReg = !isZeroReg;
                break;
            }
            case 1:
            {
                jitprintf(isZeroReg ? "bnez" : "bne ");
                printSecondReg = !isZeroReg;
                break;
            }
            case 4:
            {
                jitprintf("blt ");
                break;
            }
            case 5:
            {
                jitprintf("bge ");
                break;
            }
            case 6:
            {
                jitprintf("bltu");
                break;
            }
            case 7:
            {
                jitprintf("bgeu");
                break;
            }
            default:
            {
                return false;
            }
        }

        return true;
    }

    private void emitDispBranchLabelRiscV64(instrDesc id)
    {
        if (id.idIsBound())
        {
            var target = id.RiscVIGlabel
                ?? throw new InvalidOperationException("A bound RISC-V branch requires an instruction group.");
            emitPrintLabel(target);
            return;
        }

        var block = id.RiscVBBlabel
            ?? throw new InvalidOperationException("An unbound RISC-V branch requires a basic block.");
        var compiler = _compiler ?? throw new InvalidOperationException("RISC-V disassembly requires an active compiler.");
        jitprintf($"L_M{compiler.compMethodID:D3}_{FMT_BB(block.bbNum)}");
    }

    private bool emitDispBranchRiscV64(
        uint opcode2, uint rs1, uint rs2, instrDesc id, insGroup? ig, bool printOffsetPlaceholder)
    {
        if (!emitDispBranchInstrTypeRiscV64(opcode2, rs2 == (uint)REG_ZERO, out var printSecondReg))
        {
            return false;
        }

        jitprintf($"           {RiscVRegisterName(rs1)}, ");
        if (printSecondReg)
        {
            jitprintf($"{RiscVRegisterName(rs2)}, ");
        }
        emitDispBranchLabelRiscV64(id);
        jitprintf("\n");

        return true;
    }

    private void emitDispIllegalInstructionRiscV64(uint code)
    {
        jitprintf($"RISCV64 illegal instruction: 0x{code:X8}\n");
        assert(false, "RISCV64 illegal instruction");
    }

    private void emitDispImmediateRiscV64(nint immediate, bool newLine = true, uint regBase = (uint)REG_ZERO)
    {
        var compiler = _compiler ?? throw new InvalidOperationException("RISC-V disassembly requires an active compiler.");
        if (compiler.opts.disDiffable && (regBase != (uint)REG_FP) && (regBase != (uint)REG_SP))
        {
            jitprintf("0xD1FFAB1E");
        }
        else
        {
            jitprintf($"{immediate}");
        }

        if (newLine)
        {
            jitprintf("\n");
        }
    }

    private unsafe void emitDispInsNameRiscV64(uint code, instrDesc? id)
    {
        emitDispInsNameRiscV64(code, null, false, 0, id, null);
    }

    private unsafe void emitDispInsNameRiscV64(
        uint code, byte* addr, bool doffs, uint insOffset, instrDesc? id, insGroup? ig)
    {
        var compiler = _compiler ?? throw new InvalidOperationException("RISC-V disassembly requires an active compiler.");
        emitDispInsAddr(addr - writeableOffset);
        emitDispInsOffs(insOffset, doffs);

        if (compiler.opts.disCodeBytes && !compiler.opts.disDiffable)
        {
            var digitCount = Is32BitInstruction((ushort)code) ? 8 : 4;
            jitprintf($"  {code.ToString($"X{digitCount}", CultureInfo.InvariantCulture),-8}    ");
        }

        jitprintf("      ");
        var willPrintLoadImmediate = (addr != null) && (id?.idInsOpt() == INS_OPTS_I) && !compiler.opts.disDiffable;

        switch (GetMajorOpcode(code))
        {
            case MajorOpcode.Lui:
            case MajorOpcode.Auipc:
            {
                var rd = (code >> 7) & 0x1f;
                var immediate = unchecked((int)(code & 0xfffff000)) >> 12;
                var mnemonic = GetMajorOpcode(code) == MajorOpcode.Lui ? "lui" : "auipc";
                jitprintf($"{mnemonic,-15}{RiscVRegisterName(rd)}, ");
                if ((addr == null) && (immediate == 0))
                {
                    jitprintf("??\n");
                }
                else
                {
                    emitDispImmediateRiscV64(immediate, !willPrintLoadImmediate);
                }

                return;
            }
            case MajorOpcode.OpImm:
            {
                emitDispOpImmRiscV64(code, willPrintLoadImmediate);
                return;
            }
            case MajorOpcode.OpImm32:
            {
                emitDispOpImm32RiscV64(code, willPrintLoadImmediate);
                return;
            }
            case MajorOpcode.Op:
            {
                if (emitDispOpRiscV64(code))
                {
                    return;
                }

                break;
            }
            case MajorOpcode.Op32:
            {
                if (emitDispOp32RiscV64(code))
                {
                    return;
                }

                break;
            }
            case MajorOpcode.Store:
            {
                var funct3 = (code >> 12) & 0x7;
                if (funct3 >= 4)
                {
                    emitDispIllegalInstructionRiscV64(code);
                    return;
                }

                var rs1 = (code >> 15) & 0x1f;
                var rs2 = (code >> 20) & 0x1f;
                var immediate = unchecked((int)((((code >> 25) & 0x7f) << 5) | ((code >> 7) & 0x1f)));
                immediate = (immediate << 20) >> 20;
                var width = "bhwd"[(int)funct3];
                jitprintf($"s{width,-14}{RiscVRegisterName(rs2)}, ");
                emitDispImmediateRiscV64(immediate, false, rs1);
                jitprintf($"({RiscVRegisterName(rs1)})\n");
                return;
            }
            case MajorOpcode.Branch:
            {
                var funct3 = (code >> 12) & 0x7;
                var rs1 = (code >> 15) & 0x1f;
                var rs2 = (code >> 20) & 0x1f;
                if (!emitDispBranchRiscV64(funct3, rs1, rs2, id
                    ?? throw new InvalidOperationException("RISC-V branch disassembly requires an instruction descriptor."),
                    ig, addr == null))
                {
                    emitDispIllegalInstructionRiscV64(code);
                }

                return;
            }
            case MajorOpcode.Load:
            {
                var funct3 = (code >> 12) & 0x7;
                var rs1 = (code >> 15) & 0x1f;
                var rd = (code >> 7) & 0x1f;
                var immediate = unchecked((int)code) >> 20;
                var width = "bhwd"[(int)(funct3 & 0b011)];
                var isUnsigned = (funct3 & 0b100) != 0;
                if ((width == 'd') && isUnsigned)
                {
                    emitDispIllegalInstructionRiscV64(code);
                    return;
                }

                var mnemonic = $"l{width}{(isUnsigned ? 'u' : ' ')}";
                jitprintf($"{mnemonic,-15}{RiscVRegisterName(rd)}, ");
                emitDispImmediateRiscV64(immediate, false, rs1);
                jitprintf($"({RiscVRegisterName(rs1)})\n");
                return;
            }
            case MajorOpcode.Jalr:
            {
                emitDispJalrRiscV64(code, id, addr == null);
                return;
            }
            case MajorOpcode.Jal:
            {
                emitDispJalRiscV64(code, id, addr == null);
                return;
            }
            case MajorOpcode.MiscMem:
            {
                var predecessor = (code >> 24) & 0xf;
                var successor = (code >> 20) & 0xf;
                EmitRiscVInstruction("fence", $"{predecessor}, {successor}");
                return;
            }
            case MajorOpcode.System:
            {
                if (emitDispSystemRiscV64(code))
                {
                    return;
                }

                throw new FatalJitException(CORJIT_SKIPPED, "Unsupported RISC-V system instruction disassembly.");
            }
            case MajorOpcode.OpFp:
            {
                emitDispOpFpRiscV64(code);
                return;
            }
            case MajorOpcode.StoreFp:
            case MajorOpcode.LoadFp:
            {
                emitDispFpLoadStoreRiscV64(code, GetMajorOpcode(code) == MajorOpcode.StoreFp);
                return;
            }
            case MajorOpcode.Amo:
            {
                emitDispAmoRiscV64(code);
                return;
            }
            case MajorOpcode.JrJalrMvAdd:
            {
                emitDispCompressedMoveRiscV64(code, compiler.opts.disCodeBytes);
                return;
            }
            case MajorOpcode.MiscAlu:
            {
                emitDispCompressedAluRiscV64(code, compiler.opts.disCodeBytes);
                return;
            }
            default:
            {
                jitprintf($"CODE: 0x{code:X}\n");
                jitprintf($"MajorOpcode: {(int)GetMajorOpcode(code)}\n");
                NO_WAY("illegal ins within emitDisInsName!");
                return;
            }
        }

        emitDispIllegalInstructionRiscV64(code);
    }

    private void EmitRiscVInstruction(string mnemonic, string operands)
    {
        jitprintf($"{mnemonic,-15}{operands}\n");
    }

    private void emitDispOpImmRiscV64(uint code, bool willPrintLoadImmediate)
    {
        var funct3 = (code >> 12) & 0x7;
        var rd = (code >> 7) & 0x1f;
        var rs1 = (code >> 15) & 0x1f;
        var immediate = unchecked((int)code) >> 20;
        var hasImmediate = true;
        string mnemonic;

        switch (funct3)
        {
            case 0:
            {
                if (code == emitInsCode(INS_nop))
                {
                    jitprintf("nop\n");
                    return;
                }

                mnemonic = immediate != 0 ? "addi" : "mv";
                hasImmediate = immediate != 0;
                break;
            }
            case 1:
            {
                var funct6 = (unchecked((uint)immediate) >> 6) & 0x3f;
                var shift = unchecked((uint)immediate) & 0x3f;
                if (funct6 == 0b011000)
                {
                    mnemonic = shift switch
                    {
                        0 => "clz",
                        1 => "ctz",
                        2 => "cpop",
                        4 => "sext.b",
                        5 => "sext.h",
                        _ => string.Empty,
                    };
                    if (mnemonic.Length == 0)
                    {
                        emitDispIllegalInstructionRiscV64(code);
                        return;
                    }
                    hasImmediate = false;
                }
                else
                {
                    mnemonic = funct6 switch
                    {
                        0b000000 => "slli",
                        0b001010 => "bseti",
                        0b010010 => "bclri",
                        0b011010 => "binvi",
                        _ => string.Empty,
                    };
                    if (mnemonic.Length == 0)
                    {
                        emitDispIllegalInstructionRiscV64(code);
                        return;
                    }
                    immediate = (int)shift;
                }
                break;
            }
            case 2:
            {
                mnemonic = "slti";
                break;
            }
            case 3:
            {
                mnemonic = "sltiu";
                break;
            }
            case 4:
            {
                mnemonic = immediate == -1 ? "not" : "xori";
                hasImmediate = immediate != -1;
                break;
            }
            case 5:
            {
                var funct6 = (unchecked((uint)immediate) >> 6) & 0x3f;
                var shift = unchecked((uint)immediate) & 0x3f;
                if (funct6 == 0b011010)
                {
                    if (shift != 0b111000)
                    {
                        emitDispIllegalInstructionRiscV64(code);
                        return;
                    }
                    mnemonic = "rev8";
                    hasImmediate = false;
                }
                else
                {
                    mnemonic = funct6 switch
                    {
                        0b000000 => "srli",
                        0b010000 => "srai",
                        0b011000 => "rori",
                        0b010010 => "bexti",
                        _ => string.Empty,
                    };
                    if (mnemonic.Length == 0)
                    {
                        emitDispIllegalInstructionRiscV64(code);
                        return;
                    }
                    immediate = (int)shift;
                }
                break;
            }
            case 6:
            {
                mnemonic = "ori";
                break;
            }
            case 7:
            {
                mnemonic = "andi";
                break;
            }
            default:
            {
                emitDispIllegalInstructionRiscV64(code);
                return;
            }
        }

        jitprintf($"{mnemonic,-15}{RiscVRegisterName(rd)}, {RiscVRegisterName(rs1)}");
        if (hasImmediate)
        {
            jitprintf(", ");
            if (funct3 == 0)
            {
                emitDispImmediateRiscV64(immediate, false, rs1);
            }
            else
            {
                jitprintf($"{immediate}");
            }
        }
        if (!willPrintLoadImmediate)
        {
            jitprintf("\n");
        }
    }

    private void emitDispOpImm32RiscV64(uint code, bool willPrintLoadImmediate)
    {
        var funct3 = (code >> 12) & 0x7;
        var rd = (code >> 7) & 0x1f;
        var rs1 = (code >> 15) & 0x1f;
        var immediate = unchecked((int)code) >> 20;
        switch (funct3)
        {
            case 0:
            {
                if (immediate == 0)
                {
                    EmitRiscVInstruction("sext.w", $"{RiscVRegisterName(rd)}, {RiscVRegisterName(rs1)}");
                }
                else
                {
                    jitprintf($"{"addiw",-15}{RiscVRegisterName(rd)}, {RiscVRegisterName(rs1)}, ");
                    emitDispImmediateRiscV64(immediate, !willPrintLoadImmediate);
                }
                return;
            }
            case 1:
            {
                var funct7 = (unchecked((uint)immediate) >> 5) & 0x7f;
                var funct6 = (unchecked((uint)immediate) >> 6) & 0x3f;
                var shift = unchecked((uint)immediate) & 0x1f;
                if (funct7 == 0)
                {
                    EmitRiscVInstruction("slliw", $"{RiscVRegisterName(rd)}, {RiscVRegisterName(rs1)}, {shift}");
                }
                else if (funct6 == 0b000010)
                {
                    var shift6 = unchecked((uint)immediate) & 0x3f;
                    EmitRiscVInstruction("slli.uw", $"{RiscVRegisterName(rd)}, {RiscVRegisterName(rs1)}, {shift6}");
                }
                else if (funct7 == 0b0110000)
                {
                    var mnemonic = shift switch
                    {
                        0 => "clzw",
                        1 => "ctzw",
                        2 => "cpopw",
                        _ => string.Empty,
                    };
                    if (mnemonic.Length == 0)
                    {
                        emitDispIllegalInstructionRiscV64(code);
                        return;
                    }
                    EmitRiscVInstruction(mnemonic, $"{RiscVRegisterName(rd)}, {RiscVRegisterName(rs1)}");
                }
                else
                {
                    emitDispIllegalInstructionRiscV64(code);
                }
                return;
            }
            case 5:
            {
                var funct7 = (unchecked((uint)immediate) >> 5) & 0x7f;
                var shift = unchecked((uint)immediate) & 0x1f;
                var mnemonic = funct7 switch
                {
                    0b0000000 => "srliw",
                    0b0100000 => "sraiw",
                    0b0110000 => "roriw",
                    _ => string.Empty,
                };
                if (mnemonic.Length == 0)
                {
                    emitDispIllegalInstructionRiscV64(code);
                    return;
                }
                EmitRiscVInstruction(mnemonic, $"{RiscVRegisterName(rd)}, {RiscVRegisterName(rs1)}, {shift}");
                return;
            }
            default:
            {
                emitDispIllegalInstructionRiscV64(code);
                return;
            }
        }
    }

    private bool emitDispOpRiscV64(uint code)
    {
        var funct7 = (code >> 25) & 0x7f;
        var funct3 = (code >> 12) & 0x7;
        var rd = (code >> 7) & 0x1f;
        var rs1 = (code >> 15) & 0x1f;
        var rs2 = (code >> 20) & 0x1f;

        var mnemonic = funct7 switch
        {
            0b0000000 => funct3 switch
            {
                0 => "add", 1 => "sll", 2 => "slt", 3 => "sltu",
                4 => "xor", 5 => "srl", 6 => "or", 7 => "and", _ => string.Empty,
            },
            0b0100000 => funct3 switch
            {
                0 => "sub", 4 => "xnor", 5 => "sra", 6 => "orn", 7 => "andn", _ => string.Empty,
            },
            0b0000001 => funct3 switch
            {
                0 => "mul", 1 => "mulh", 2 => "mulhsu", 3 => "mulhu",
                4 => "div", 5 => "divu", 6 => "rem", 7 => "remu", _ => string.Empty,
            },
            0b0010000 => funct3 switch
            {
                2 => "sh1add", 4 => "sh2add", 6 => "sh3add", _ => string.Empty,
            },
            0b0110000 => funct3 switch
            {
                1 => "rol", 5 => "ror", _ => string.Empty,
            },
            0b0000101 => funct3 switch
            {
                4 => "min", 5 => "minu", 6 => "max", 7 => "maxu", _ => string.Empty,
            },
            0b0010100 => funct3 == 1 ? "bset" : string.Empty,
            0b0100100 => funct3 switch
            {
                1 => "bclr", 5 => "bext", _ => string.Empty,
            },
            0b0110100 => funct3 == 1 ? "binv" : string.Empty,
            0b0000111 => funct3 switch
            {
                5 => "czero.eqz", 7 => "czero.nez", _ => string.Empty,
            },
            _ => string.Empty,
        };

        if (mnemonic.Length == 0 || ((funct7 == 0b0000101) && (funct3 < 4)))
        {
            emitDispIllegalInstructionRiscV64(code);
            return true;
        }

        if (funct7 == 0b0000101)
        {
            mnemonic = funct3 switch
            {
                4 => "min ",
                6 => "max ",
                _ => mnemonic,
            };
        }
        EmitRiscVInstruction(mnemonic, $"{RiscVRegisterName(rd)}, {RiscVRegisterName(rs1)}, {RiscVRegisterName(rs2)}");
        return true;
    }

    private bool emitDispOp32RiscV64(uint code)
    {
        var funct7 = (code >> 25) & 0x7f;
        var funct3 = (code >> 12) & 0x7;
        var rd = (code >> 7) & 0x1f;
        var rs1 = (code >> 15) & 0x1f;
        var rs2 = (code >> 20) & 0x1f;

        var mnemonic = funct7 switch
        {
            0b0000000 => funct3 switch
            {
                0 => "addw", 1 => "sllw", 5 => "srlw", _ => string.Empty,
            },
            0b0100000 => funct3 switch
            {
                0 => "subw", 5 => "sraw", _ => string.Empty,
            },
            0b0000001 => funct3 switch
            {
                0 => "mulw", 4 => "divw", 5 => "divuw", 6 => "remw", 7 => "remuw", _ => string.Empty,
            },
            0b0010000 => funct3 switch
            {
                2 => "sh1add.uw", 4 => "sh2add.uw", 6 => "sh3add.uw", _ => string.Empty,
            },
            0b0110000 => funct3 switch
            {
                1 => "rolw", 5 => "rorw", _ => string.Empty,
            },
            0b0000100 => funct3 switch
            {
                0 => rs2 == (uint)REG_ZERO ? "zext.w" : "add.uw",
                4 => rs2 == (uint)REG_ZERO ? "zext.h" : string.Empty,
                _ => string.Empty,
            },
            _ => string.Empty,
        };

        if ((mnemonic.Length == 0) || ((funct7 == 0b0000100) && (funct3 == 4) && (rs2 != (uint)REG_ZERO)))
        {
            emitDispIllegalInstructionRiscV64(code);
            return true;
        }

        var operands = mnemonic.StartsWith("zext", StringComparison.Ordinal)
            ? $"{RiscVRegisterName(rd)}, {RiscVRegisterName(rs1)}"
            : $"{RiscVRegisterName(rd)}, {RiscVRegisterName(rs1)}, {RiscVRegisterName(rs2)}";
        EmitRiscVInstruction(mnemonic, operands);
        return true;
    }

    private void emitDispJalrRiscV64(uint code, instrDesc? id, bool noAddress)
    {
        var rs1 = (code >> 15) & 0x1f;
        var rd = (code >> 7) & 0x1f;
        var immediate = unchecked((int)code) >> 20;
        var callHasRelocOffset = noAddress && (id?.idInsOpt() == INS_OPTS_C) && (id?.idIsCallRegPtr() == false);

        if ((immediate == 0) && (rs1 == (uint)REG_RA) && (rd == (uint)REG_ZERO))
        {
            jitprintf("ret");
            return;
        }

        if ((immediate == 0) && ((rd == (uint)REG_RA) || (rd == (uint)REG_ZERO)) && !callHasRelocOffset)
        {
            var mnemonic = rd == (uint)REG_RA ? "jalr" : "jr";
            jitprintf($"{mnemonic,-15}{RiscVRegisterName(rs1)}");
        }
        else
        {
            jitprintf($"{"jalr",-15}{RiscVRegisterName(rd)}, ");
            if ((immediate == 0) && callHasRelocOffset)
            {
                jitprintf("??");
            }
            else
            {
                emitDispImmediateRiscV64(immediate, false);
            }
            jitprintf($"({RiscVRegisterName(rs1)})");
        }

        EmitRiscVCallMethodName(id);
        jitprintf("\n");
    }

    private void emitDispJalRiscV64(uint code, instrDesc? id, bool noAddress)
    {
        var rd = (code >> 7) & 0x1f;
        var immediate = unchecked((int)(
            (((code >> 31) & 0x1) << 20) |
            (((code >> 12) & 0xff) << 12) |
            (((code >> 20) & 0x1) << 11) |
            (((code >> 21) & 0x3ff) << 1)));
        immediate = (immediate << 11) >> 11;

        if ((rd == (uint)REG_ZERO) || (rd == (uint)REG_RA))
        {
            var mnemonic = rd == (uint)REG_RA ? "jal" : "j";
            jitprintf($"{mnemonic,-15}");
            if (id?.idIsBound() == true)
            {
                var target = id.RiscVIGlabel
                    ?? throw new InvalidOperationException("A bound RISC-V jump requires an instruction group.");
                emitPrintLabel(target);
            }
            else if (noAddress && (immediate == 0))
            {
                jitprintf("pc+??");
            }
            else
            {
                jitprintf("pc+");
                emitDispImmediateRiscV64(immediate / sizeof(uint));
                jitprintf(" instructions");
            }
        }
        else
        {
            jitprintf($"{"jal",-15}{RiscVRegisterName(rd)}, ");
            if (noAddress && (immediate == 0))
            {
                jitprintf("??");
            }
            else
            {
                emitDispImmediateRiscV64(immediate, false);
            }
        }

        EmitRiscVCallMethodName(id);
        jitprintf("\n");
    }

    private unsafe void EmitRiscVCallMethodName(instrDesc? id)
    {
        var debugInfo = id?.idDebugOnlyInfo();
        if ((debugInfo is null) || (debugInfo.idMemCookie == 0))
        {
            return;
        }

        var compiler = _compiler ?? throw new InvalidOperationException("RISC-V disassembly requires an active compiler.");
        var methodName = compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)debugInfo.idMemCookie);
        jitprintf($"\t\t// {methodName}");
    }

    private bool emitDispSystemRiscV64(uint code)
    {
        var funct3 = (code >> 12) & 0x7;
        if (funct3 != 0)
        {
            var rd = (code >> 7) & 0x1f;
            var csr = code >> 20;
            if (funct3 <= 3)
            {
                var rs1 = (code >> 15) & 0x1f;
                var mnemonic = funct3 switch
                {
                    1 => "csrrw",
                    2 => "csrrs",
                    3 => "csrrc",
                    _ => string.Empty,
                };
                if (mnemonic.Length == 0)
                {
                    emitDispIllegalInstructionRiscV64(code);
                    return false;
                }
                EmitRiscVInstruction(mnemonic, $"{RiscVRegisterName(rd)}, {csr}, {RiscVRegisterName(rs1)}");
                return true;
            }

            var immediate = (code >> 15) & 0x1f;
            var immediateMnemonic = funct3 switch
            {
                5 => "csrrwi",
                6 => "csrrsi",
                7 => "csrrci",
                _ => string.Empty,
            };
            if (immediateMnemonic.Length == 0)
            {
                emitDispIllegalInstructionRiscV64(code);
                return false;
            }
            EmitRiscVInstruction(immediateMnemonic, $"{RiscVRegisterName(rd)}, {csr}, {immediate}");
            return true;
        }

        if (code == emitInsCode(INS_ebreak))
        {
            jitprintf("ebreak\n");
            return true;
        }
        if (code == emitInsCode(INS_ecall))
        {
            jitprintf("ecall\n");
            return true;
        }

        emitDispIllegalInstructionRiscV64(code);
        return false;
    }

    private void emitDispOpFpRiscV64(uint code)
    {
        var funct5 = (code >> 27) & 0x1f;
        var format = (code >> 25) & 0x3;
        var funct3 = (code >> 12) & 0x7;
        var rs2Field = (code >> 20) & 0x1f;
        if (format > 1)
        {
            emitDispIllegalInstructionRiscV64(code);
            return;
        }

        var type = format == 0 ? 's' : 'd';
        var fd = RiscVRegisterName(((code >> 7) & 0x1f) | (uint)REG_FT0);
        var fs1 = RiscVRegisterName(((code >> 15) & 0x1f) | (uint)REG_FT0);
        var fs2 = RiscVRegisterName(((code >> 20) & 0x1f) | (uint)REG_FT0);
        var xd = RiscVRegisterName((code >> 7) & 0x1f);
        var xs1 = RiscVRegisterName((code >> 15) & 0x1f);
        switch (funct5)
        {
            case 0b00000:
            {
                EmitRiscVInstruction($"fadd.{type}", $"{fd}, {fs1}, {fs2}");
                return;
            }
            case 0b00001:
            {
                EmitRiscVInstruction($"fsub.{type}", $"{fd}, {fs1}, {fs2}");
                return;
            }
            case 0b00010:
            {
                EmitRiscVInstruction($"fmul.{type}", $"{fd}, {fs1}, {fs2}");
                return;
            }
            case 0b00011:
            {
                EmitRiscVInstruction($"fdiv.{type}", $"{fd}, {fs1}, {fs2}");
                return;
            }
            case 0b01011:
            {
                if (rs2Field != 0)
                {
                    emitDispIllegalInstructionRiscV64(code);
                    return;
                }
                EmitRiscVInstruction($"fsqrt.{type}", $"{fd}, {fs1}");
                return;
            }
            case 0b00100:
            {
                if (funct3 > 2)
                {
                    emitDispIllegalInstructionRiscV64(code);
                    return;
                }

                if (fs1 != fs2)
                {
                    var suffix = new[] { ".s", "n.s", "x.s" }[funct3];
                    EmitRiscVInstruction($"fsgnj{suffix}", $"{fd}, {fs1}, {fs2}");
                }
                else
                {
                    var prefix = (type, funct3) switch
                    {
                        ('s', 0) => "fmv.s",
                        ('s', 1) => "fneg.s",
                        ('s', 2) => "fabs.s",
                        ('d', 0) => "fmv.d",
                        ('d', 1) => "fneg.d",
                        _ => "fabs.d",
                    };
                    EmitRiscVInstruction(prefix, $"{fd}, {fs1}");
                }
                return;
            }
            case 0b00101:
            {
                var mnemonic = funct3 switch
                {
                    0 => $"fmin.{type}",
                    1 => $"fmax.{type}",
                    _ => string.Empty,
                };
                if (mnemonic.Length == 0)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Unsupported RISC-V floating-point min/max instruction.");
                }
                EmitRiscVInstruction(mnemonic, $"{fd}, {fs1}, {fs2}");
                return;
            }
            case 0b01000:
            {
                if (rs2Field > 1)
                {
                    emitDispIllegalInstructionRiscV64(code);
                    return;
                }
                EmitRiscVInstruction($"fcvt.{type}.{(rs2Field == 0 ? 's' : 'd')}", $"{fd}, {fs1}");
                return;
            }
            case 0b11000:
            {
                var mnemonic = rs2Field switch
                {
                    0 => $"fcvt.w.{type}",
                    1 => $"fcvt.wu.{type}",
                    2 => $"fcvt.l.{type}",
                    3 => $"fcvt.lu.{type}",
                    _ => string.Empty,
                };
                if (mnemonic.Length == 0)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Unsupported RISC-V floating-point conversion instruction.");
                }
                EmitRiscVInstruction(mnemonic, $"{xd}, {fs1}");
                return;
            }
            case 0b11100:
            {
                if (rs2Field != 0)
                {
                    emitDispIllegalInstructionRiscV64(code);
                    return;
                }
                var mnemonic = funct3 switch
                {
                    0 => $"fmv.x.{(type == 's' ? 'w' : type)}",
                    1 => $"fclass.{type}",
                    _ => string.Empty,
                };
                if (mnemonic.Length == 0)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Unsupported RISC-V floating-point register operation.");
                }
                EmitRiscVInstruction(mnemonic, $"{xd}, {fs1}");
                return;
            }
            case 0b10100:
            {
                var mnemonic = funct3 switch
                {
                    0 => $"fle.{type}",
                    1 => $"flt.{type}",
                    2 => $"feq.{type}",
                    _ => string.Empty,
                };
                if (mnemonic.Length == 0)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Unsupported RISC-V floating-point compare instruction.");
                }
                EmitRiscVInstruction(mnemonic, $"{xd}, {fs1}, {fs2}");
                return;
            }
            case 0b11010:
            {
                var mnemonic = rs2Field switch
                {
                    0 => $"fcvt.{type}.w",
                    1 => $"fcvt.{type}.wu",
                    2 => $"fcvt.{type}.l",
                    3 => $"fcvt.{type}.lu",
                    _ => string.Empty,
                };
                if (mnemonic.Length == 0)
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Unsupported RISC-V floating-point conversion instruction.");
                }
                EmitRiscVInstruction(mnemonic, $"{fd}, {xs1}");
                return;
            }
            case 0b11110:
            {
                if ((rs2Field != 0) || (funct3 != 0))
                {
                    emitDispIllegalInstructionRiscV64(code);
                    return;
                }
                EmitRiscVInstruction($"fmv.{(type == 's' ? 'w' : type)}.x", $"{fd}, {xs1}");
                return;
            }
            default:
            {
                throw new FatalJitException(CORJIT_SKIPPED, "Unsupported RISC-V floating-point disassembly opcode.");
            }
        }
    }

    private void emitDispFpLoadStoreRiscV64(uint code, bool isStore)
    {
        var funct3 = (code >> 12) & 0x7;
        if (funct3 is not 2 and not 3)
        {
            emitDispIllegalInstructionRiscV64(code);
            return;
        }

        var rs1 = (code >> 15) & 0x1f;
        var register = (code >> (isStore ? 20 : 7)) & 0x1f;
        var immediate = isStore
            ? unchecked((int)((((code >> 25) & 0x7f) << 5) | ((code >> 7) & 0x1f)))
            : unchecked((int)code) >> 20;
        if (isStore)
        {
            immediate = (immediate << 20) >> 20;
        }
        var width = funct3 == 2 ? 'w' : 'd';
        var prefix = isStore ? "fs" : "fl";
        var reg = RiscVRegisterName(register | (uint)REG_FT0);
        jitprintf($"{prefix}{width,-13}{reg}, ");
        emitDispImmediateRiscV64(immediate, false, rs1);
        jitprintf($"({RiscVRegisterName(rs1)})\n");
    }

    private void emitDispAmoRiscV64(uint code)
    {
        var funct5 = (code >> 27) & 0x1f;
        var mnemonic = funct5 switch
        {
            0b00010 => "lr",
            0b00011 => "sc",
            0b00001 => "amoswap",
            0b00000 => "amoadd",
            0b00100 => "amoxor",
            0b01100 => "amoand",
            0b01000 => "amoor",
            0b10000 => "amomin",
            0b10100 => "amomax",
            0b11000 => "amominu",
            0b11100 => "amomaxu",
            _ => string.Empty,
        };
        if (mnemonic.Length == 0)
        {
            emitDispIllegalInstructionRiscV64(code);
            return;
        }

        var width = ((code >> 12) & 0x7) switch
        {
            2 => 'w',
            3 => 'd',
            _ => '?',
        };
        if (width == '?')
        {
            emitDispIllegalInstructionRiscV64(code);
            return;
        }

        var aq = (code & (1u << 25)) != 0 ? "aq" : string.Empty;
        var rl = (code & (1u << 26)) != 0 ? "rl" : string.Empty;
        var operation = $"{mnemonic}.{width}.{aq}{rl}";
        var rd = (code >> 7) & 0x1f;
        var rs1 = (code >> 15) & 0x1f;
        var rs2 = (code >> 20) & 0x1f;
        if (funct5 == 0b00010)
        {
            if (rs2 != (uint)REG_ZERO)
            {
                emitDispIllegalInstructionRiscV64(code);
                return;
            }
            EmitRiscVInstruction(operation, $"{RiscVRegisterName(rd)}, ({RiscVRegisterName(rs1)})");
        }
        else
        {
            EmitRiscVInstruction(operation,
                $"{RiscVRegisterName(rd)}, {RiscVRegisterName(rs2)}, ({RiscVRegisterName(rs1)})");
        }
    }

    private void emitDispCompressedMoveRiscV64(uint code, bool includeCompressedName)
    {
        var funct4 = (code >> 12) & 0xf;
        var rdRs1 = (code >> 7) & 0x1f;
        var rs2 = (code >> 2) & 0x1f;
        if ((rdRs1 == (uint)REG_ZERO) || (rs2 == (uint)REG_ZERO) || (funct4 is not 0b1000 and not 0b1001))
        {
            emitDispIllegalInstructionRiscV64(code);
            return;
        }

        if (funct4 == 0b1001)
        {
            if (includeCompressedName)
            {
                EmitRiscVInstruction("c.add", $"{RiscVRegisterName(rdRs1)}, {RiscVRegisterName(rs2)}");
            }
            else
            {
                EmitRiscVInstruction("add", $"{RiscVRegisterName(rdRs1)}, {RiscVRegisterName(rdRs1)}, {RiscVRegisterName(rs2)}");
            }
        }
        else
        {
            var mnemonic = includeCompressedName ? "c.mv" : "mv";
            EmitRiscVInstruction(mnemonic, $"{RiscVRegisterName(rdRs1)}, {RiscVRegisterName(rs2)}");
        }
    }

    private void emitDispCompressedAluRiscV64(uint code, bool includeCompressedName)
    {
        var funct6 = (code >> 10) & 0x3f;
        var funct2 = (code >> 5) & 0x3;
        var rdRs1 = (uint)getRegNumberFromRvcReg((code >> 7) & 0x7);
        var rs2 = (uint)getRegNumberFromRvcReg((code >> 2) & 0x7);
        var mnemonic = (funct6, funct2) switch
        {
            (0b100011, 0b00) => "sub",
            (0b100011, 0b01) => "xor",
            (0b100011, 0b10) => "or",
            (0b100011, 0b11) => "and",
            (0b100111, 0b00) => "subw",
            (0b100111, 0b01) => "addw",
            _ => string.Empty,
        };
        if (mnemonic.Length == 0)
        {
            emitDispIllegalInstructionRiscV64(code);
            return;
        }

        if (includeCompressedName)
        {
            mnemonic = $"c.{mnemonic}";
        }
        else
        {
            EmitRiscVInstruction(mnemonic,
                $"{RiscVRegisterName(rdRs1)}, {RiscVRegisterName(rdRs1)}, {RiscVRegisterName(rs2)}");
            return;
        }
        EmitRiscVInstruction(mnemonic, $"{RiscVRegisterName(rdRs1)}, {RiscVRegisterName(rs2)}");
    }

    private void emitDispInsInstrNumRiscV64(instrDesc id)
    {
#if DEBUG
        var compiler = _compiler ?? throw new InvalidOperationException("RISC-V disassembly requires an active compiler.");
        if (!compiler.verbose)
        {
            return;
        }

        var debugInfo = id.idDebugOnlyInfo()
            ?? throw new InvalidOperationException("RISC-V instruction disassembly requires debug descriptor info.");
        jitprintf($"IN{debugInfo.idNum:x4}: ");
#endif
    }

    private unsafe void emitDispIns_OptsRelocRiscV64(instrDesc id)
    {
        var formatter = new InstructionFormatter(this, id);
        EmitLogic_OptsReloc(formatter, id);
    }

    private unsafe void emitDispIns_OptsRcRiscV64(instrDesc id)
    {
        var formatter = new InstructionFormatter(this, id);
        EmitLogic_OptsRc(formatter, id, 0);
    }

    private unsafe void emitDispIns_OptsRlRiscV64(instrDesc id)
    {
        var formatter = new InstructionFormatter(this, id);
        EmitLogic_OptsRl(formatter, id, 0);
    }

    private unsafe void emitDispIns_OptsJumpRiscV64(instrDesc id)
    {
        var jump = (instrDescJmp)id;
        var formatter = new InstructionFormatter(this, id);
        EmitLogic_OptsJump(formatter, jump, 0);
    }

    private unsafe void emitDispIns_OptsCRiscV64(instrDesc id)
    {
        var formatter = new InstructionFormatter(this, id);
        EmitLogic_OptsC(formatter, id);
    }

    private unsafe void emitDispIns_OptsIRiscV64(instrDesc id)
    {
        var loadImmediate = (instrDescLoadImm)id;
        var formatter = new InstructionFormatter(this, id);
        EmitLogic_OptsI(formatter, loadImmediate);
    }

    private unsafe void emitDispInsRiscV64(
        instrDesc id, bool isNew, bool doffs, bool asmfm, uint offset, byte* code, nuint size, insGroup? ig)
    {
        if (code == null)
        {
            switch (id.idInsOpt())
            {
                case INS_OPTS_JUMP:
                {
                    emitDispIns_OptsJumpRiscV64(id);
                    break;
                }
                case INS_OPTS_C:
                {
                    emitDispIns_OptsCRiscV64(id);
                    break;
                }
                case INS_OPTS_I:
                {
                    emitDispIns_OptsIRiscV64(id);
                    break;
                }
                case INS_OPTS_RC:
                {
                    emitDispIns_OptsRcRiscV64(id);
                    break;
                }
                case INS_OPTS_RL:
                {
                    emitDispIns_OptsRlRiscV64(id);
                    break;
                }
                case INS_OPTS_RELOC:
                {
                    emitDispIns_OptsRelocRiscV64(id);
                    break;
                }
                default:
                {
                    emitDispInsNameRiscV64(id.idAddr().iiaGetInstrEncode(), id);
                    break;
                }
            }

            return;
        }

        emitDispInsInstrNumRiscV64(id);
        var compiler = _compiler ?? throw new InvalidOperationException("RISC-V disassembly requires an active compiler.");
        var printLoadImmediate = (id.idInsOpt() == INS_OPTS_I) && !compiler.opts.disDiffable;
        var instructionBytes = code + writeableOffset;
        nuint instructionSize;
        for (nuint i = 0; i < size; i += instructionSize, instructionBytes += instructionSize, offset += (uint)instructionSize)
        {
            var word = Unsafe.ReadUnaligned<ushort>(instructionBytes);
            uint instruction = word;
            instructionSize = sizeof(ushort);
            if (Is32BitInstruction(word))
            {
                instruction |= (uint)Unsafe.ReadUnaligned<ushort>(instructionBytes + sizeof(ushort)) << 16;
                instructionSize = sizeof(uint);
            }
#if DEBUG
            if (compiler.verbose && (i != 0))
            {
                jitprintf("        ");
            }
#endif
            emitDispInsNameRiscV64(instruction, instructionBytes, doffs, offset, id, ig);
            if (printLoadImmediate && ((i + instructionSize) < size))
            {
                jitprintf("\n");
            }
        }

        if (printLoadImmediate)
        {
            var loadImmediate = (instrDescLoadImm)id;
            jitprintf($"\t\t;; load imm: hex=0x{loadImmediate.idcCnsVal:X16} dec={loadImmediate.idcCnsVal}\n");
        }
    }
}
#endif
