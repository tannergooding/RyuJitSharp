// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Globalization;

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool strictArmAsm = true;

    private static readonly string[] s_arm64ConditionNames =
    [
        "eq", "ne", "hs", "lo", "mi", "pl", "vs", "vc",
        "hi", "ls", "ge", "lt", "gt", "le", "AL", "NV",
    ];

    private static readonly string[] s_arm64FlagNames =
    [
        "0", "v", "c", "cv", "z", "zv", "zc", "zcv",
        "n", "nv", "nc", "ncv", "nz", "nzv", "nzc", "nzcv",
    ];

    private static readonly string[] s_arm64BarrierNames =
    [
        "#0", "oshld", "oshst", "osh", "#4", "nshld", "nshst", "nsh",
        "#8", "ishld", "ishst", "ish", "#12", "ld", "st", "sy",
    ];

    private static void emitDispComma()
    {
        jitprintf(", ");
    }

    private void emitDispInst(instruction ins)
    {
        var insstr = codeGen.genInsName(ins);
        var len = insstr.Length;
        jitprintf(insstr);

        // Add at least one space, even when the mnemonic is eight characters or longer.
        do
        {
            jitprintf(" ");
            len++;
        }
        while (len < 8);
    }

    private void emitDispImm(long imm, bool addComma, bool alwaysHex = false, bool isAddrOffset = false)
    {
        assert(_compiler is not null);
        if (isAddrOffset)
        {
            alwaysHex = true;
        }
        else if (imm == 0)
        {
            // Non-offset values of zero are never displayed as hex.
            alwaysHex = false;
        }

        if (strictArmAsm)
        {
            jitprintf("#");
        }

        // Partial pointer words are diffable whenever significant bits extend beyond the low byte.
        if (_compiler.opts.disDiffable)
        {
            var top56bits = imm >> 8;
            if ((top56bits != 0) && (top56bits != -1))
            {
                imm = 0xD1FFAB1E;
            }
        }

        if (!alwaysHex && (imm > -1000) && (imm < 1000))
        {
            jitprintf(unchecked((int)imm).ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            const long HighWordMask = unchecked((long)0xFFFFFFFF00000000UL);
            if ((imm < 0) && ((imm & HighWordMask) == HighWordMask))
            {
                jitprintf("-");
                imm = unchecked(-imm);
            }

            if ((imm & HighWordMask) != 0)
            {
                var bits = unchecked((ulong)imm);
                jitprintf(isAddrOffset ? $"0x{bits:X}" : $"0x{bits:x}");
            }
            else
            {
                jitprintf($"0x{unchecked((uint)imm):X2}");
            }
        }

        if (addComma)
        {
            emitDispComma();
        }
    }

    private static void emitDispElementIndex(nint imm, bool addComma)
    {
        jitprintf($"[{imm.ToString(CultureInfo.InvariantCulture)}]");
        if (addComma)
        {
            emitDispComma();
        }
    }

    private static void emitDispFloatZero()
    {
        if (strictArmAsm)
        {
            jitprintf("#");
        }
        jitprintf("0.0");
    }

    private static void emitDispFloatImm(nint imm8)
    {
        assert((0 <= imm8) && (imm8 <= 0xFF));
        if (strictArmAsm)
        {
            jitprintf("#");
        }

        floatImm8 fpImm = new() { immFPIVal = unchecked((uint)imm8) };
        var result = emitDecodeFloatImm8(fpImm);
        jitprintf(result.ToString("F4", CultureInfo.InvariantCulture));
    }

    private static void emitDispSmallFloatImm(nint imm, instruction ins)
    {
        if (strictArmAsm)
        {
            jitprintf("#");
        }
        jitprintf(emitDecodeSmallFloatImm(imm, ins).ToString("F4", CultureInfo.InvariantCulture));
    }

    private void emitDispImmOptsLSL(nint imm, bool hasShift, uint shiftAmount)
    {
        if (!strictArmAsm && hasShift)
        {
            imm <<= (int)shiftAmount;
        }
        emitDispImm(imm, false);
        if (strictArmAsm && hasShift)
        {
            jitprintf($", LSL #{shiftAmount}");
        }
    }

    private static void emitDispCond(insCond cond)
    {
        var imm = (uint)cond;
        assert(imm < (uint)s_arm64ConditionNames.Length);
        jitprintf(s_arm64ConditionNames[imm]);
    }

    private static void emitDispFlags(insCFlags flags)
    {
        var imm = (uint)flags;
        assert(imm < (uint)s_arm64FlagNames.Length);
        jitprintf(s_arm64FlagNames[imm]);
    }

    private static void emitDispBarrier(insBarrier barrier)
    {
        var imm = (uint)barrier;
        assert(imm < (uint)s_arm64BarrierNames.Length);
        jitprintf(s_arm64BarrierNames[imm]);
    }

    private static void emitDispShiftOpts(insOpts opt)
    {
        switch (opt)
        {
            case INS_OPTS_LSL:
            {
                jitprintf(" LSL ");
                break;
            }
            case INS_OPTS_LSR:
            {
                jitprintf(" LSR ");
                break;
            }
            case INS_OPTS_ASR:
            {
                jitprintf(" ASR ");
                break;
            }
            case INS_OPTS_ROR:
            {
                jitprintf(" ROR ");
                break;
            }
            case INS_OPTS_MSL:
            {
                jitprintf(" MSL ");
                break;
            }
            default:
            {
                assert(false, "Bad value");
                break;
            }
        }
    }

    private static void emitDispExtendOpts(insOpts opt)
    {
        switch (opt)
        {
            case INS_OPTS_UXTB:
            {
                jitprintf("UXTB");
                break;
            }
            case INS_OPTS_UXTH:
            {
                jitprintf("UXTH");
                break;
            }
            case INS_OPTS_UXTW:
            {
                jitprintf("UXTW");
                break;
            }
            case INS_OPTS_UXTX:
            {
                jitprintf("UXTX");
                break;
            }
            case INS_OPTS_SXTB:
            {
                jitprintf("SXTB");
                break;
            }
            case INS_OPTS_SXTH:
            {
                jitprintf("SXTH");
                break;
            }
            case INS_OPTS_SXTW:
            {
                jitprintf("SXTW");
                break;
            }
            case INS_OPTS_SXTX:
            {
                jitprintf("SXTX");
                break;
            }
            default:
            {
                assert(false, "Bad value");
                break;
            }
        }
    }

    private void emitDispReg(regNumber reg, emitAttr attr, bool addComma)
    {
        var size = EA_SIZE(attr);
        jitprintf(emitRegName(reg, size));
        if (addComma)
        {
            emitDispComma();
        }
    }

    private void emitDispVectorReg(regNumber reg, insOpts opt, bool addComma)
    {
        assert(isVectorRegister(reg));
        jitprintf(emitVectorRegName(reg));
        emitDispArrangement(opt);
        if (addComma)
        {
            emitDispComma();
        }
    }

    private void emitDispVectorRegIndex(regNumber reg, emitAttr elemsize, nint index, bool addComma)
    {
        assert(isVectorRegister(reg));
        jitprintf(emitVectorRegName(reg));
        emitDispElemsize(elemsize);
        jitprintf($"[{unchecked((int)index).ToString(CultureInfo.InvariantCulture)}]");
        if (addComma)
        {
            emitDispComma();
        }
    }

    private void emitDispVectorRegList(regNumber firstReg, uint listSize, insOpts opt, bool addComma)
    {
        assert(isVectorRegister(firstReg));
        var currReg = firstReg;
        jitprintf("{");
        for (uint i = 0; i < listSize; i++)
        {
            var notLastRegister = i != listSize - 1;
            emitDispVectorReg(currReg, opt, notLastRegister);
            currReg = currReg == REG_V31 ? REG_V0 : REG_NEXT(currReg);
        }
        jitprintf("}");

        if (addComma)
        {
            emitDispComma();
        }
    }

    private void emitDispVectorElemList(regNumber firstReg, uint listSize, emitAttr elemsize, uint index, bool addComma)
    {
        assert(isVectorRegister(firstReg));
        var currReg = firstReg;
        jitprintf("{");
        for (uint i = 0; i < listSize; i++)
        {
            jitprintf(emitVectorRegName(currReg));
            emitDispElemsize(elemsize);
            var notLastRegister = i != listSize - 1;
            if (notLastRegister)
            {
                emitDispComma();
            }
            currReg = currReg == REG_V31 ? REG_V0 : REG_NEXT(currReg);
        }
        jitprintf("}");
        jitprintf($"[{unchecked((int)index).ToString(CultureInfo.InvariantCulture)}]");

        if (addComma)
        {
            emitDispComma();
        }
    }

    private static void emitDispArrangement(insOpts opt)
    {
        var str = "???";
        switch (opt)
        {
            case INS_OPTS_8B:
            {
                str = "8b";
                break;
            }
            case INS_OPTS_16B:
            {
                str = "16b";
                break;
            }
            case INS_OPTS_SCALABLE_B:
            {
                str = "b";
                break;
            }
            case INS_OPTS_4H:
            {
                str = "4h";
                break;
            }
            case INS_OPTS_8H:
            {
                str = "8h";
                break;
            }
            case INS_OPTS_SCALABLE_H:
            {
                str = "h";
                break;
            }
            case INS_OPTS_2S:
            {
                str = "2s";
                break;
            }
            case INS_OPTS_4S:
            {
                str = "4s";
                break;
            }
            case INS_OPTS_SCALABLE_S:
            case INS_OPTS_SCALABLE_S_UXTW:
            case INS_OPTS_SCALABLE_S_SXTW:
            {
                str = "s";
                break;
            }
            case INS_OPTS_1D:
            {
                str = "1d";
                break;
            }
            case INS_OPTS_2D:
            {
                str = "2d";
                break;
            }
            case INS_OPTS_SCALABLE_D:
            case INS_OPTS_SCALABLE_D_UXTW:
            case INS_OPTS_SCALABLE_D_SXTW:
            {
                str = "d";
                break;
            }
            case INS_OPTS_SCALABLE_Q:
            {
                str = "q";
                break;
            }
            default:
            {
                assert(false, "Invalid insOpt");
                break;
            }
        }
        jitprintf(".");
        jitprintf(str);
    }

    private static void emitDispElemsize(emitAttr elemsize)
    {
        var str = "???";
        switch (elemsize)
        {
            case EA_1BYTE:
            {
                str = ".b";
                break;
            }
            case EA_2BYTE:
            {
                str = ".h";
                break;
            }
            case EA_4BYTE:
            {
                str = ".s";
                break;
            }
            case EA_8BYTE:
            {
                str = ".d";
                break;
            }
            default:
            {
                assert(false, "invalid elemsize");
                break;
            }
        }
        jitprintf(str);
    }

    private void emitDispShiftedReg(regNumber reg, insOpts opt, nint imm, emitAttr attr)
    {
        var size = EA_SIZE(attr);
        assert((imm & 0x003F) == imm);
        assert(((imm & 0x0020) == 0) || (size == EA_8BYTE));
        jitprintf(emitRegName(reg, size));

        if (imm > 0)
        {
            if (strictArmAsm)
            {
                emitDispComma();
            }
            emitDispShiftOpts(opt);
            emitDispImm(imm, false);
        }
    }

    private void emitDispExtendReg(regNumber reg, insOpts opt, nint imm)
    {
        assert((imm >= 0) && (imm <= 4));
        assert(insOptsNone(opt) || insOptsAnyExtend(opt) || (opt == INS_OPTS_LSL));

        // The extend option, not the instruction size, determines the register width.
        var size = (insOptsNone(opt) || insOptsLSL(opt) || insOpts64BitExtend(opt)) ? EA_8BYTE : EA_4BYTE;
        if (strictArmAsm)
        {
            if (insOptsNone(opt) || (insOptsLSL(opt) && imm == 0))
            {
                emitDispReg(reg, size, false);
            }
            else
            {
                emitDispReg(reg, size, true);
                if (insOptsLSL(opt))
                {
                    jitprintf("LSL");
                }
                else
                {
                    emitDispExtendOpts(opt);
                }

                if (imm > 0)
                {
                    jitprintf(" ");
                    emitDispImm(imm, false);
                }
            }
        }
        else
        {
            if (insOptsNone(opt))
            {
                emitDispReg(reg, size, false);
            }
            else if (opt != INS_OPTS_LSL)
            {
                emitDispExtendOpts(opt);
                jitprintf("(");
                emitDispReg(reg, size, false);
                jitprintf(")");
            }

            if (imm > 0)
            {
                jitprintf("*");
                emitDispImm((nint)1 << (int)imm, false);
            }
        }
    }

    private void emitDispAddrRI(regNumber reg, insOpts opt, nint imm)
    {
        reg = encodingZRtoSP(reg);
        if (strictArmAsm)
        {
            jitprintf("[");
            emitDispReg(reg, EA_8BYTE, false);
            if (!insOptsPostIndex(opt) && (imm != 0))
            {
                emitDispComma();
                emitDispImm(imm, false, true, true);
            }
            jitprintf("]");

            if (insOptsPreIndex(opt))
            {
                jitprintf("!");
            }
            else if (insOptsPostIndex(opt))
            {
                emitDispComma();
                emitDispImm(imm, false, true, true);
            }
        }
        else
        {
            jitprintf("[");
            var operStr = "++";
            if (imm < 0)
            {
                operStr = "--";
                imm = unchecked(-imm);
            }

            if (insOptsPreIndex(opt))
            {
                jitprintf(operStr);
            }
            emitDispReg(reg, EA_8BYTE, false);
            if (insOptsPostIndex(opt))
            {
                jitprintf(operStr);
            }

            if (insOptsIndexed(opt))
            {
                emitDispComma();
            }
            else
            {
                jitprintf($"{operStr[1]}");
            }
            emitDispImm(imm, false, true, true);
            jitprintf("]");
        }
    }

    private void emitDispAddrRRExt(regNumber reg1, regNumber reg2, insOpts opt, bool isScaled, emitAttr size)
    {
        reg1 = encodingZRtoSP(reg1);
        uint scale = 0;
        if (isScaled)
        {
            scale = NaturalScale_helper(size);
        }

        jitprintf("[");
        if (strictArmAsm)
        {
            emitDispReg(reg1, EA_8BYTE, true);
            emitDispExtendReg(reg2, opt, (nint)scale);
        }
        else
        {
            emitDispReg(reg1, EA_8BYTE, false);
            jitprintf("+");
            emitDispExtendReg(reg2, opt, (nint)scale);
        }
        jitprintf("]");
    }
}
#endif
