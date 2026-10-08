// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public partial class Emitter
{
    private static readonly string[] s_armConditionNames =
    [
        "eq", "ne", "hs", "lo", "mi", "pl", "vs", "vc",
        "hi", "ls", "ge", "lt", "gt", "le", "AL", "NV",
    ];

    private void emitDispImm(int imm, bool addComma, bool alwaysHex = false, bool isAddrOffset = false)
    {
        assert(_compiler is not null);

        if (!alwaysHex && (imm > -1000) && (imm < 1000))
        {
            jitprintf($"{imm}");
        }
        else if ((imm > 0) || (imm == unchecked(-imm)) ||
            (_compiler.opts.disDiffable && (imm == unchecked((int)0xD1FFAB1E))))
        {
            if (isAddrOffset)
            {
                jitprintf($"0x{imm:X2}");
            }
            else
            {
                jitprintf($"0x{imm:x2}");
            }
        }
        else
        {
            var magnitude = unchecked(-imm);
            if (isAddrOffset)
            {
                jitprintf($"-0x{magnitude:X2}");
            }
            else
            {
                jitprintf($"-0x{magnitude:x2}");
            }
        }

        if (addComma)
        {
            jitprintf(", ");
        }
    }

    private void emitDispCond(int condition)
    {
        assert((condition >= 0) && ((uint)condition < (uint)s_armConditionNames.Length));
        jitprintf(s_armConditionNames[condition]);
    }

    private unsafe void emitDispReloc(byte* address)
    {
        assert(_compiler is not null);
        jitprintf($"0x{FMT_PTR((void*)dspPtr(address))}");
    }

    private void emitDispRegRange(regNumber reg, int length, emitAttr attr)
    {
        jitprintf("{");
        emitDispReg(reg, attr, false);
        if (length > 1)
        {
            jitprintf("-");
            emitDispReg((regNumber)((int)reg + length - 1), attr, false);
        }
        jitprintf("}");
    }

    private void emitDispRegmask(int mask, bool encodedPC_LR)
    {
        var printedOne = false;
        bool hasPC;
        bool hasLR;

        if (encodedPC_LR)
        {
            hasPC = (mask & 2) != 0;
            hasLR = (mask & 1) != 0;
            mask >>= 2;
        }
        else
        {
            hasPC = (mask & (int)SRBM_PC) != 0;
            hasLR = (mask & (int)SRBM_LR) != 0;
            mask &= ~((int)SRBM_PC | (int)SRBM_LR);
        }

        var reg = REG_R0;
        uint bit = 1;
        var remaining = unchecked((uint)mask);

        jitprintf("{");
        while (remaining != 0)
        {
            if ((bit & remaining) != 0)
            {
                if (printedOne)
                {
                    jitprintf(",");
                }

                jitprintf(emitRegName(reg));
                printedOne = true;
                remaining -= bit;
            }

            reg = (regNumber)((int)reg + 1);
            bit <<= 1;
        }

        if (hasLR)
        {
            if (printedOne)
            {
                jitprintf(",");
            }

            jitprintf(emitRegName(REG_LR));
            printedOne = true;
        }

        if (hasPC)
        {
            if (printedOne)
            {
                jitprintf(",");
            }

            jitprintf(emitRegName(REG_PC));
        }

        jitprintf("}");
    }

    private void emitDispShiftOpts(insOpts options)
    {
        if (options == INS_OPTS_LSL)
        {
            jitprintf(" LSL ");
        }
        else if (options == INS_OPTS_LSR)
        {
            jitprintf(" LSR ");
        }
        else if (options == INS_OPTS_ASR)
        {
            jitprintf(" ASR ");
        }
        else if (options == INS_OPTS_ROR)
        {
            jitprintf(" ROR ");
        }
        else if (options == INS_OPTS_RRX)
        {
            jitprintf(" RRX ");
        }
    }

    private void emitDispReg(regNumber reg, emitAttr attr, bool addComma)
    {
        if (isFloatReg(reg))
        {
            if (attr == EA_8BYTE)
            {
                var regIndex = ((int)reg - (int)REG_F0) >> 1;
                if (regIndex < 10)
                {
                    jitprintf($"d{regIndex}");
                }
                else
                {
                    assert(regIndex < 100);
                    jitprintf($"d{regIndex / 10}{regIndex % 10}");
                }
            }
            else
            {
                var floatName = emitFloatRegName(reg, attr);
                jitprintf($"s{floatName[1..]}");
            }
        }
        else
        {
            jitprintf(emitRegName(reg, attr));
        }

        if (addComma)
        {
            jitprintf(", ");
        }
    }

    private void emitDispAddrR(regNumber reg, emitAttr attr)
    {
        jitprintf("[");
        emitDispReg(reg, attr, false);
        jitprintf("]");
        emitDispGC(attr);
    }

    private void emitDispAddrRI(regNumber reg, int immediate, emitAttr attr)
    {
        jitprintf("[");
        emitDispReg(reg, attr, false);
        if (immediate != 0)
        {
            if (immediate >= 0)
            {
                jitprintf("+");
            }

            emitDispImm(immediate, false, true, true);
        }

        jitprintf("]");
        emitDispGC(attr);
    }

    private void emitDispAddrRR(regNumber baseReg, regNumber indexReg, emitAttr attr)
    {
        jitprintf("[");
        emitDispReg(baseReg, attr, false);
        jitprintf("+");
        emitDispReg(indexReg, attr, false);
        jitprintf("]");
        emitDispGC(attr);
    }

    private void emitDispAddrRRI(regNumber baseReg, regNumber indexReg, int shift, emitAttr attr)
    {
        jitprintf("[");
        emitDispReg(baseReg, attr, false);
        jitprintf("+");
        if (shift > 0)
        {
            emitDispImm(1 << shift, false);
            jitprintf("*");
        }

        emitDispReg(indexReg, attr, false);
        jitprintf("]");
        emitDispGC(attr);
    }

    private void emitDispAddrPUW(regNumber reg, int immediate, insOpts options, emitAttr attr)
    {
        jitprintf("[");
        emitDispReg(reg, attr, false);
        if (insOptAnyInc(options))
        {
            jitprintf("!");
        }

        if (immediate != 0)
        {
            if (immediate >= 0)
            {
                jitprintf("+");
            }

            emitDispImm(immediate, false, true, true);
        }

        jitprintf("]");
        emitDispGC(attr);
    }

    private void emitDispGC(emitAttr attr)
    {
        // Native ARM disassembly has the corresponding GC annotations disabled.
    }

    private static bool insAlwaysSetFlags(instruction ins)
    {
        return ins is INS_cmp or INS_cmn or INS_teq or INS_tst;
    }

    private void emitDispInst(instruction ins, insFlags flags)
    {
        var instructionName = codeGen.genInsName(ins);
        var length = instructionName.Length;

        jitprintf(instructionName);
        if (insSetsFlags(flags) && !insAlwaysSetFlags(ins))
        {
            jitprintf("s");
            length++;
        }

        do
        {
            jitprintf(" ");
            length++;
        } while (length < 8);
    }

    public unsafe void emitDispInsHex(instrDesc id, byte* code, nuint size)
    {
        var compiler = _compiler ?? throw new FatalJitException("Instruction display requires an active compiler.");
        if (!compiler.opts.disCodeBytes || compiler.opts.disDiffable)
        {
            return;
        }

        if (size == 2)
        {
            var firstHalfword = *(ushort*)code;
            jitprintf($"  {firstHalfword:X4}     ");
        }
        else if (size == 4)
        {
            var firstHalfword = *(ushort*)code;
            var secondHalfword = *(ushort*)(code + 2);
            jitprintf($"  {firstHalfword:X4} {secondHalfword:X4}");
        }
        else
        {
            assert(size == 0);

            switch (emitInsSize(id.idInsFmt()))
            {
                case ISZ_16BIT:
                {
                    jitprintf("  2B");
                    break;
                }

                case ISZ_32BIT:
                {
                    jitprintf("  4B");
                    break;
                }

                case ISZ_48BIT:
                {
                    jitprintf("  6B");
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }
    }

    public unsafe void emitDispIns(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset = 0, byte* code = null, nuint size = 0, insGroup? ig = null)
    {
        if ((id.idInsFmt() == IF_LARGEJMP) && id.idIsBound())
        {
            emitDispLargeJmp(id, isNew, doffs, asmfm, offset, code, size, ig);
        }
        else
        {
            emitDispInsHelp(id, isNew, doffs, asmfm, offset, code, size, ig);
        }
    }

    private unsafe void emitDispLargeJmp(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset, byte* code, nuint size, insGroup? ig)
    {
        // Keep the real descriptor intact; these displayed instructions are synthesized for a pseudo-op.
        var conditionalJump = new instrDescJmp();
        conditionalJump.idIns(emitJumpKindToIns(emitReverseJumpKind(emitInsToJumpKind(id.idIns()))));
        conditionalJump.idInsFmt(IF_T1_K);
        conditionalJump.idInsSize(emitInsSize(IF_T1_K));
        conditionalJump.idjShort = true;
        conditionalJump.idAddr().iiaSetInstrCount(1);
        conditionalJump.idDebugOnlyInfo(id.idDebugOnlyInfo());

        var conditionalSize = code == null ? 0u : 2u;
        emitDispInsHelp(conditionalJump, false, doffs, asmfm, offset, code, conditionalSize, null);

        code += conditionalSize;
        offset = unchecked(offset + 2);

        var unconditionalJump = new instrDescJmp();
        unconditionalJump.idIns(INS_b);
        unconditionalJump.idInsFmt(IF_T2_J2);
        unconditionalJump.idInsSize(emitInsSize(IF_T2_J2));
        unconditionalJump.idjShort = false;
        if (id.idIsBound())
        {
            unconditionalJump.idSetIsBound();
            unconditionalJump.idjTargetIG = ((instrDescJmp)id).idjTargetIG;
        }
        else
        {
            unconditionalJump.idjTarget = ((instrDescJmp)id).idjTarget;
        }
        unconditionalJump.idDebugOnlyInfo(id.idDebugOnlyInfo());

        var branchSize = code == null ? 0u : 4u;
        emitDispInsHelp(unconditionalJump, isNew, doffs, asmfm, offset, code, branchSize, ig);
    }

    private unsafe void emitDispInsHelp(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset, byte* code, nuint size, insGroup? ig)
    {
        var compiler = _compiler ?? throw new FatalJitException("Instruction display requires an active compiler.");
#if DEBUG
        if (compiler.verbose)
        {
            var debugInfo = id.idDebugOnlyInfo()
                ?? throw new FatalJitException("Instruction display requires descriptor debug information.");
            jitprintf($"IN{debugInfo.idNum:x4}: ");
        }
#endif

        if (code == null)
        {
            size = 0;
        }
        if (!isNew && !asmfm && (size != 0))
        {
            doffs = true;
        }

        emitDispInsAddr(code);
        emitDispInsOffs(offset, doffs);

        byte* codeRW = null;
        if (code != null)
        {
            assert(((code >= emitCodeBlock) && (code < emitCodeBlock + emitTotalHotCodeSize))
                || ((code >= emitColdCodeBlock) && (code < emitColdCodeBlock + emitTotalColdCodeSize)));
            codeRW = unchecked(code + writeableOffset);
        }

        emitDispInsHex(id, codeRW, size);
        jitprintf("      ");

        var ins = id.idIns();
        var format = id.idInsFmt();
        emitDispInst(ins, id.idInsFlags());
        assert(!isNew || ((nuint)emitSizeOfInsDsc(id) == emitCurIGfreeNext - id.StorageOffset));

        var attr = id.idGCref() switch
        {
            GCT_GCREF => EA_GCREF,
            GCT_BYREF => EA_BYREF,
            _ => id.idOpSize(),
        };
        var immediate = 0;
        string? methodName;

        switch (format)
        {
            case IF_T1_A:
            case IF_T2_A:
            {
                break;
            }

            case IF_T1_L0:
            case IF_T2_B:
            {
                emitDispImm(unchecked((int)emitGetInsSC(id)), false);
                break;
            }

            case IF_T1_B:
            {
                emitDispCond(unchecked((int)emitGetInsSC(id)));
                break;
            }

            case IF_T1_L1:
            case IF_T2_I1:
            {
                emitDispRegmask(unchecked((int)emitGetInsSC(id)), true);
                break;
            }

            case IF_T2_E2:
            {
                if (ins == INS_vmrs)
                {
                    if (id.idReg1() != REG_R15)
                    {
                        emitDispReg(id.idReg1(), attr, true);
                        jitprintf("FPSCR");
                    }
                    else
                    {
                        jitprintf("APSR, FPSCR");
                    }
                }
                else
                {
                    emitDispReg(id.idReg1(), attr, false);
                }
                break;
            }

            case IF_T1_D1:
            {
                emitDispReg(id.idReg1(), attr, false);
                break;
            }

            case IF_T1_D2:
            {
                emitDispReg(id.idReg3(), attr, false);
                var debugInfo = id.idDebugOnlyInfo()
                    ?? throw new FatalJitException("Instruction display requires descriptor debug information.");
                if (debugInfo.idMemCookie != 0)
                {
                    methodName = compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)debugInfo.idMemCookie);
                    jitprintf($"\t\t// {methodName}");
                }
                break;
            }

            case IF_T1_F:
            {
                emitDispReg(REG_SP, attr, true);
                emitDispImm(unchecked((int)emitGetInsSC(id)), false);
                break;
            }

            case IF_T1_J0:
            case IF_T2_L1:
            case IF_T2_L2:
            {
                emitDispReg(id.idReg1(), attr, true);
                immediate = unchecked((int)emitGetInsSC(id));
                emitDispImm(immediate, false);
                break;
            }

            case IF_T2_N:
            {
                emitDispReg(id.idReg1(), attr, true);
                immediate = unchecked((int)emitGetInsSC(id));
                if (compiler.opts.disDiffable)
                {
                    immediate = 0xD1FF;
                }
                emitDispImm(immediate, false, true);
                break;
            }

            case IF_T2_N3:
            {
                emitDispReg(id.idReg1(), attr, true);
                jitprintf(ins == INS_movw ? "LOW RELOC " : "HIGH RELOC ");
                emitDispReloc(emitGetInsRelocValue(id));
                break;
            }

            case IF_T2_N2:
            {
                emitDispReg(id.idReg1(), attr, true);
                immediate = unchecked((int)emitGetInsSC(id));
                dataSection? labelTable = null;
                for (var section = emitConsDsc.dsdList; section is not null; section = section.dsNext)
                {
                    if ((section.dsType == dataSection.sectionType.blockAbsoluteAddr)
                        && (section.dsOffset == unchecked((uint)immediate)))
                    {
                        labelTable = section;
                        break;
                    }
                }

                if (id.idIsDspReloc())
                {
                    jitprintf("reloc ");
                }

                if (labelTable is not null)
                {
                    jitprintf($"{(ins == INS_movw ? "LOW" : "HIGH")} ADDRESS J_M{compiler.compMethodID:D3}_DS{immediate:D2}");
                }
                else
                {
                    jitprintf($"{(ins == INS_movw ? "LOW" : "HIGH")} ADDRESS RWD{unchecked((uint)immediate):D2}");
                }

                if ((labelTable is not null) && (ins == INS_movt))
                {
                    var blocks = labelTable.Blocks;
                    assert(blocks.Length > 0);
                    var isBound = emitCodeGetCookie(blocks[0]
                        ?? throw new FatalJitException("An ARM32 label table contains a null block.")) is not null;
                    if (isBound)
                    {
                        jitprintf($"\n\n    J_M{compiler.compMethodID:D3}_DS{immediate:D2} LABEL   DWORD");
                        var blockCount = labelTable.dsSize / TARGET_POINTER_SIZE;
                        for (uint index = 0; index < blockCount; index++)
                        {
                            var block = blocks[index]
                                ?? throw new FatalJitException("An ARM32 label table contains a null block.");
                            var label = emitCodeGetCookie(block)
                                ?? throw new FatalJitException("A bound ARM32 label table contains an unbound block.");
                            jitprintf($"\n            DD      {emitLabelString(label)}");
                        }
                    }
                }
                break;
            }

            case IF_T2_H2:
            case IF_T2_K2:
            {
                emitDispAddrRI(id.idReg1(), unchecked((int)emitGetInsSC(id)), attr);
                break;
            }

            case IF_T2_K3:
            {
                emitDispAddrRI(REG_PC, unchecked((int)emitGetInsSC(id)), attr);
                break;
            }

            case IF_T1_J1:
            case IF_T2_I0:
            {
                emitDispReg(id.idReg1(), attr, false);
                jitprintf("!, ");
                emitDispRegmask(unchecked((int)emitGetInsSC(id)), false);
                break;
            }

            case IF_T1_D0:
            case IF_T1_E:
            case IF_T2_C3:
            case IF_T2_C9:
            case IF_T2_C10:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispReg(id.idReg2(), attr, false);
                if ((format == IF_T1_E) && (ins == INS_rsb))
                {
                    jitprintf(", 0");
                }
                break;
            }

            case IF_T2_E1:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispAddrR(id.idReg2(), attr);
                break;
            }

            case IF_T2_D1:
            {
                emitDispReg(id.idReg1(), attr, true);
                immediate = unchecked((int)emitGetInsSC(id));
                var leastSignificantBit = (immediate >> 5) & 0x1f;
                var mostSignificantBit = immediate & 0x1f;
                emitDispImm(leastSignificantBit, true);
                emitDispImm(mostSignificantBit + 1 - leastSignificantBit, false);
                break;
            }

            case IF_T1_C:
            case IF_T1_G:
            case IF_T2_C2:
            case IF_T2_H1:
            case IF_T2_K1:
            case IF_T2_L0:
            case IF_T2_M0:
            {
                emitDispReg(id.idReg1(), attr, true);
                immediate = unchecked((int)emitGetInsSC(id));
                if (emitInsIsLoadOrStore(ins))
                {
                    emitDispAddrRI(id.idReg2(), immediate, attr);
                }
                else
                {
                    emitDispReg(id.idReg2(), attr, true);
                    emitDispImm(immediate, false);
                }
                break;
            }

            case IF_T1_J2:
            {
                emitDispReg(id.idReg1(), attr, true);
                immediate = unchecked((int)emitGetInsSC(id));
                if (emitInsIsLoadOrStore(ins))
                {
                    emitDispAddrRI(REG_SP, immediate, attr);
                }
                else
                {
                    emitDispReg(REG_SP, attr, true);
                    emitDispImm(immediate, false);
                }
                break;
            }

            case IF_T2_K4:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispAddrRI(REG_PC, unchecked((int)emitGetInsSC(id)), attr);
                break;
            }

            case IF_T2_C1:
            case IF_T2_C8:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispReg(id.idReg2(), attr, false);
                immediate = unchecked((int)emitGetInsSC(id));
                if (id.idInsOpt() == INS_OPTS_RRX)
                {
                    emitDispShiftOpts(id.idInsOpt());
                    assert(immediate == 1);
                }
                else if (immediate > 0)
                {
                    emitDispShiftOpts(id.idInsOpt());
                    emitDispImm(immediate, false);
                }
                break;
            }

            case IF_T2_C6:
            {
                immediate = unchecked((int)emitGetInsSC(id));
                emitDispReg(id.idReg1(), attr, true);
                emitDispReg(id.idReg2(), attr, immediate != 0);
                if (immediate != 0)
                {
                    emitDispImm(immediate, false);
                }
                break;
            }

            case IF_T2_C7:
            {
                emitDispAddrRRI(id.idReg1(), id.idReg2(), unchecked((int)emitGetInsSC(id)), attr);
                break;
            }

            case IF_T2_H0:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispAddrPUW(id.idReg2(), unchecked((int)emitGetInsSC(id)), id.idInsOpt(), attr);
                break;
            }

            case IF_T1_H:
            {
                emitDispReg(id.idReg1(), attr, true);
                if (emitInsIsLoadOrStore(ins))
                {
                    emitDispAddrRR(id.idReg2(), id.idReg3(), attr);
                }
                else
                {
                    emitDispReg(id.idReg2(), attr, true);
                    emitDispReg(id.idReg3(), attr, false);
                }
                break;
            }

            case IF_T2_C4:
            case IF_T2_C5:
            case IF_T2_VFP3:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispReg(id.idReg2(), attr, true);
                emitDispReg(id.idReg3(), attr, false);
                break;
            }

            case IF_T2_VFP2:
            {
                switch (ins)
                {
                    case INS_vcvt_d2i:
                    case INS_vcvt_d2u:
                    case INS_vcvt_d2f:
                    {
                        emitDispReg(id.idReg1(), EA_4BYTE, true);
                        emitDispReg(id.idReg2(), EA_8BYTE, false);
                        break;
                    }

                    case INS_vcvt_i2d:
                    case INS_vcvt_u2d:
                    case INS_vcvt_f2d:
                    {
                        emitDispReg(id.idReg1(), EA_8BYTE, true);
                        emitDispReg(id.idReg2(), EA_4BYTE, false);
                        break;
                    }

                    default:
                    {
                        emitDispReg(id.idReg1(), attr, true);
                        emitDispReg(id.idReg2(), attr, false);
                        break;
                    }
                }
                break;
            }

            case IF_T2_VLDST:
            {
                immediate = unchecked((int)emitGetInsSC(id));
                switch (ins)
                {
                    case INS_vldr:
                    case INS_vstr:
                    {
                        emitDispReg(id.idReg1(), attr, true);
                        emitDispAddrPUW(id.idReg2(), immediate, id.idInsOpt(), attr);
                        break;
                    }

                    case INS_vldm:
                    case INS_vstm:
                    {
                        emitDispReg(id.idReg2(), attr, false);
                        if (insOptAnyInc(id.idInsOpt()))
                        {
                            jitprintf("!");
                        }
                        jitprintf(", ");
                        emitDispRegRange(id.idReg1(), System.Math.Abs(immediate) >> 2, attr);
                        break;
                    }

                    case INS_vpush:
                    case INS_vpop:
                    {
                        emitDispRegRange(id.idReg1(), System.Math.Abs(immediate) >> 2, attr);
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            case IF_T2_VMOVD:
            {
                switch (ins)
                {
                    case INS_vmov_i2d:
                    {
                        emitDispReg(id.idReg1(), attr, true);
                        emitDispReg(id.idReg2(), EA_4BYTE, true);
                        emitDispReg(id.idReg3(), EA_4BYTE, false);
                        break;
                    }

                    case INS_vmov_d2i:
                    {
                        emitDispReg(id.idReg1(), EA_4BYTE, true);
                        emitDispReg(id.idReg2(), EA_4BYTE, true);
                        emitDispReg(id.idReg3(), attr, false);
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            case IF_T2_VMOVS:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispReg(id.idReg2(), attr, false);
                break;
            }

            case IF_T2_G1:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispAddrRR(id.idReg2(), id.idReg3(), attr);
                break;
            }

            case IF_T2_D0:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispReg(id.idReg2(), attr, true);
                immediate = unchecked((int)emitGetInsSC(id));
                if (ins == INS_bfi)
                {
                    var leastSignificantBit = (immediate >> 5) & 0x1f;
                    var mostSignificantBit = immediate & 0x1f;
                    emitDispImm(leastSignificantBit, true);
                    emitDispImm(mostSignificantBit + 1 - leastSignificantBit, false);
                }
                else if (ins == INS_ssat)
                {
                    emitDispImm((immediate & 0x1f) + 1, false);
                }
                else if (ins == INS_usat)
                {
                    emitDispImm(immediate & 0x1f, false);
                }
                else
                {
                    var leastSignificantBit = (immediate >> 5) & 0x1f;
                    emitDispImm(leastSignificantBit, true);
                    emitDispImm((immediate & 0x1f) + 1, false);
                }
                break;
            }

            case IF_T2_C0:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispReg(id.idReg2(), attr, true);
                emitDispReg(id.idReg3(), attr, false);
                immediate = unchecked((int)emitGetInsSC(id));
                if (id.idInsOpt() == INS_OPTS_RRX)
                {
                    emitDispShiftOpts(id.idInsOpt());
                    assert(immediate == 1);
                }
                else if (immediate > 0)
                {
                    emitDispShiftOpts(id.idInsOpt());
                    emitDispImm(immediate, false);
                }
                break;
            }

            case IF_T2_E0:
            {
                emitDispReg(id.idReg1(), attr, true);
                if (id.idIsLclVar())
                {
                    emitDispAddrRRI(id.idReg2(), codeGen.rsGetRsvdReg(), 0, attr);
                }
                else
                {
                    emitDispAddrRRI(id.idReg2(), id.idReg3(), unchecked((int)emitGetInsSC(id)), attr);
                }
                break;
            }

            case IF_T2_G0:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispReg(id.idReg2(), attr, true);
                emitDispAddrPUW(id.idReg3(), unchecked((int)emitGetInsSC(id)), id.idInsOpt(), attr);
                break;
            }

            case IF_T2_F1:
            case IF_T2_F2:
            {
                emitDispReg(id.idReg1(), attr, true);
                emitDispReg(id.idReg2(), attr, true);
                emitDispReg(id.idReg3(), attr, true);
                emitDispReg(id.idReg4(), attr, false);
                break;
            }

            case IF_T1_J3:
            case IF_T2_M1:
            {
                emitDispReg(id.idReg1(), attr, true);
                if (id.idIsBound())
                {
                    var jump = id as instrDescJmp
                        ?? throw new FatalJitException("A bound ARM32 label instruction has no jump descriptor.");
                    var target = jump.idjTargetIG
                        ?? throw new FatalJitException("A bound ARM32 label instruction has no instruction group.");
                    emitPrintLabel(target);
                }
                else
                {
                    var jump = id as instrDescJmp
                        ?? throw new FatalJitException("An unbound ARM32 label instruction has no jump descriptor.");
                    var target = jump.idjTarget
                        ?? throw new FatalJitException("An unbound ARM32 label instruction has no basic block.");
                    jitprintf($"L_M{compiler.compMethodID:D3}_{FMT_BB(target.bbNum)}");
                }
                break;
            }

            case IF_T1_I:
            {
                emitDispReg(id.idReg1(), attr, true);
                goto case IF_T1_K;
            }

            case IF_T1_K:
            case IF_T1_M:
            {
                assert(((instrDescJmp)id).idjShort);
                jitprintf("SHORT ");
                goto case IF_T2_N1;
            }

            case IF_T2_N1:
            {
                if (format == IF_T2_N1)
                {
                    emitDispReg(id.idReg1(), attr, true);
                    jitprintf(ins == INS_movw ? "LOW ADDRESS " : "HIGH ADDRESS ");
                }
                goto case IF_T2_J1;
            }

            case IF_T2_J1:
            case IF_T2_J2:
            case IF_LARGEJMP:
            {
                if (id.idAddr().iiaHasInstrCount())
                {
                    var instructionCount = id.idAddr().iiaGetInstrCount();
                    if (ig is null)
                    {
                        jitprintf($"pc{(instructionCount >= 0 ? "+" : "")}{instructionCount} instructions");
                    }
                    else
                    {
                        var instructionNumber = emitFindInsNum(ig, id);
                        var sourceOffset = unchecked(ig.igOffs + emitFindOffset(ig, instructionNumber + 1));
                        var targetOffset = unchecked(ig.igOffs + emitFindOffset(
                            ig, unchecked(instructionNumber + 1 + (uint)instructionCount)));
                        var relativeOffset = unchecked((int)(emitOffsetToPtr(targetOffset) - emitOffsetToPtr(sourceOffset)));
                        jitprintf($"pc{(relativeOffset >= 0 ? "+" : "")}{relativeOffset} ({instructionCount} instructions)");
                    }
                }
                else if (id.idIsBound())
                {
                    var jump = id as instrDescJmp
                        ?? throw new FatalJitException("A bound ARM32 branch has no jump descriptor.");
                    var target = jump.idjTargetIG
                        ?? throw new FatalJitException("A bound ARM32 branch has no instruction group.");
                    emitPrintLabel(target);
                }
                else
                {
                    var jump = id as instrDescJmp
                        ?? throw new FatalJitException("An unbound ARM32 branch has no jump descriptor.");
                    var target = jump.idjTarget
                        ?? throw new FatalJitException("An unbound ARM32 branch has no basic block.");
                    jitprintf($"L_M{compiler.compMethodID:D3}_{FMT_BB(target.bbNum)}");
                }
                break;
            }

            case IF_T2_J3:
            {
                var debugInfo = id.idDebugOnlyInfo()
                    ?? throw new FatalJitException("Instruction display requires descriptor debug information.");
                methodName = compiler.eeGetMethodFullName((CORINFO_METHOD_HANDLE)debugInfo.idMemCookie);
                jitprintf(methodName);
                break;
            }

            default:
            {
                jitprintf($"unexpected format {emitIfName(format)}");
                assert(false, "!\"unexpectedFormat\"");
                break;
            }
        }

        var trailingDebugInfo = id.idDebugOnlyInfo();
        if ((trailingDebugInfo is not null) && (trailingDebugInfo.idVarRefOffs != 0))
        {
            jitprintf("\t// ");
            emitDispFrameRef(id.idAddr().iiaLclVar.lvaVarNum(), unchecked((int)id.idAddr().iiaLclVar.lvaOffset()),
                trailingDebugInfo.idVarRefOffs, asmfm);
        }

        jitprintf("\n");
    }
}
#endif
