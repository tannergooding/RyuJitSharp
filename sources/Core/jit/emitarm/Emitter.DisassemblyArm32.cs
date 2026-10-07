// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Emitter.insSize;
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
}
#endif
