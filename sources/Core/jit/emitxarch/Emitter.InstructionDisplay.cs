// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using System;
using System.Globalization;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitDispReloc(nint value)
    {
        var compiler = _compiler ?? throw new FatalJitException("Instruction display requires an active compiler.");
        jitprintf(compiler.opts.disAsm && compiler.opts.disDiffable
            ? "(reloc)"
            : $"(reloc 0x{unchecked((nuint)compiler.dspOffset(value)):x})");
    }

    public void emitDispAddrMode(instrDesc id)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "x86 address-mode display is not implemented.");
#else
        var compiler = _compiler ?? throw new FatalJitException("Address-mode display requires an active compiler.");
        var ins = id.idIns();
        var disp = ins is INS_call or INS_tail_i_jmp ? emitGetInsCIdisp(id) : emitGetInsAmdAny(id);
        var address = id.idAddr().iiaAddrMode;
        var separated = false;
        var frameRef = false;

        jitprintf("[");
        if (address.amBaseReg != REG_NA)
        {
            jitprintf(emitRegName(address.amBaseReg));
            separated = true;
            frameRef = address.amBaseReg == REG_ESP
                || (codeGen.IsFramePointerUsed && address.amBaseReg == REG_EBP);
        }

        if (address.amIndxReg != REG_NA)
        {
            var scale = (uint)emitDecodeScale(address.amScale);
            if (separated)
            {
                jitprintf("+");
            }
            if (scale > 1u)
            {
                jitprintf($"{scale}*");
            }
            jitprintf(emitRegName(address.amIndxReg));
            separated = true;
        }

        if (id.idIsDspReloc() && (ins != INS_i_jmp))
        {
            if (separated)
            {
                jitprintf("+");
            }
            emitDispReloc(disp);
        }
        else if (!frameRef && compiler.opts.disDiffable && unchecked((nuint)((disp >> 20) + 1)) > 1)
        {
            if (separated)
            {
                jitprintf("+");
            }
            jitprintf("D1FFAB1EH");
        }
        else if (disp > 0)
        {
            if (separated)
            {
                jitprintf("+");
            }
            var width = frameRef || disp < 1000 ? 2 : disp <= 0xFFFF ? 4 : 8;
            jitprintf($"0x{unchecked((nuint)disp).ToString($"X{width}", CultureInfo.InvariantCulture)}");
        }
        else if (disp < 0)
        {
            var width = frameRef || disp > -1000 ? 2 : disp >= -0xFFFF ? 4 : 8;
            if (!frameRef && disp < -0xFFFFFF)
            {
                if (separated)
                {
                    jitprintf("+");
                }
                jitprintf($"0x{unchecked((uint)disp):X8}");
            }
            else
            {
                jitprintf($"-0x{unchecked((nuint)(-disp)).ToString($"X{width}", CultureInfo.InvariantCulture)}");
            }
        }
        else if (!separated)
        {
            jitprintf("0x0000");
        }

        jitprintf("]");
#endif
    }

    public void emitDispShift(instruction ins, int count = 0)
    {
        switch (ins)
        {
            case INS_rcl_1:
            case INS_rcr_1:
            case INS_rol_1:
            case INS_ror_1:
            case INS_shl_1:
            case INS_shr_1:
            case INS_sar_1:
            {
                jitprintf(", 1");
                break;
            }

            case INS_rcl:
            case INS_rcr:
            case INS_rol:
            case INS_ror:
            case INS_shl:
            case INS_shr:
            case INS_sar:
            {
                jitprintf(", cl");
                break;
            }

            case INS_rcl_N:
            case INS_rcr_N:
            case INS_rol_N:
            case INS_ror_N:
            case INS_shl_N:
            case INS_shr_N:
            case INS_sar_N:
            {
                jitprintf($", {count}");
                break;
            }
        }
    }

    public unsafe void emitDispInsHex(instrDesc id, byte* code, nuint size)
    {
        var compiler = _compiler ?? throw new FatalJitException("Instruction display requires an active compiler.");
        if (!compiler.opts.disCodeBytes || compiler.opts.disDiffable)
        {
            return;
        }

        jitprintf(" ");
        for (nuint i = 0; i < size; i++)
        {
            jitprintf($"{code[i]:X2}");
        }
        if (size < 10)
        {
            jitprintf(new string(' ', checked((int)(2 * (10 - size)))));
        }
    }

    public void emitDispEmbBroadcastCount(instrDesc id)
    {
        if (!IsEvexEncodableInstruction(id.idIns()) || !HasEmbeddedBroadcast(id))
        {
            return;
        }
        if (IsApxExtendedEvexInstruction(id.idIns()) && !IsSimdInstruction(id.idIns()))
        {
            return;
        }

        var baseSize = GetInputSizeInBytes(id);
        var vectorSize = (nint)EA_SIZE_IN_BYTES(emitGetMemOpSize(id, ignoreEmbeddedBroadcast: true));
        jitprintf($" {{1to{vectorSize / baseSize}}}");
    }

    public void emitDispEmbRounding(instrDesc id)
    {
        if (!id.idIsEvexbContextSet() || IsApxExtendedEvexInstruction(id.idIns()))
        {
            return;
        }
        assert(!id.idHasMem());
        jitprintf(id.idGetEvexbContext() switch
        {
            1 => " {rd-sae}",
            2 => " {ru-sae}",
            3 => " {rz-sae}",
            _ => throw new FatalJitException("Invalid EVEX rounding mode."),
        });
    }

    public void emitDispEmbMasking(instrDesc id)
    {
        if (!IsEvexEncodableInstruction(id.idIns()) || !IsSimdInstruction(id.idIns()))
        {
            return;
        }
        var mask = (regNumber)(id.idGetEvexAaaContext() + (uint)REG_K0);
        if (mask != REG_K0)
        {
            emitDispMask(id, mask);
        }
    }

    public void emitDispConstant(instrDesc id, bool skipComma = false)
    {
        var ins = id.idIns();
        if (emitInstHasPseudoName(ins) && ins is not
            (INS_roundpd or INS_roundps or INS_roundsd or INS_roundss))
        {
            return;
        }

        CnsVal constant = default;
        if (id.idHasMemGen())
        {
            emitGetInsDcmCns(id, ref constant);
        }
        else if (id.idHasMemAdr())
        {
            _ = emitGetInsAmdCns(id, ref constant);
        }
        else
        {
            emitGetInsCns(id, ref constant);
        }

        if (id.idInsFmt() is insFormat.IF_RRW_SHF or insFormat.IF_RWR_RRD_SHF or
            insFormat.IF_MRW_SHF or insFormat.IF_SRW_SHF or insFormat.IF_ARW_SHF)
        {
            emitDispShift(ins, unchecked((byte)constant.cnsVal));
            return;
        }
        if (!skipComma)
        {
            jitprintf(", ");
        }
        if (constant.cnsReloc)
        {
            emitDispReloc(constant.cnsVal);
            return;
        }

        var compiler = _compiler ?? throw new FatalJitException("Instruction display requires an active compiler.");
        var val = constant.cnsVal;
        if (compiler.opts.disDiffable && ((val >> 18) is not 0 and not -1))
        {
            val = unchecked((nint)0xD1FFAB1E);
        }
        if (val > -1000 && val < 1000)
        {
            jitprintf($"{val}");
        }
        else if (val > 0 || val < -0xFFFFFF)
        {
            jitprintf($"0x{unchecked((nuint)val):X}");
        }
        else
        {
            jitprintf($"-0x{unchecked((nuint)(-val)):X}");
        }

        var debug = id.idDebugOnlyInfo();
        if (debug is not null)
        {
            emitDispCommentForHandle(constant.cnsVal, debug.idMemCookie, debug.idFlags);
        }
    }

    private void emitDispMask(instrDesc id, regNumber reg)
    {
        jitprintf($" {{{emitRegName(reg)}}}");
        if (id.idIsEvexZContextSet())
        {
            jitprintf("{z}");
        }
    }

    private static bool emitInstHasPseudoName(instruction ins) =>
        (prefixFlags(ins) & insFlags.INS_FLAGS_HasPseudoName) != 0;
}
#endif
