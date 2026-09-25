// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private static bool IsBitTestInstruction(instruction ins) =>
        ins is INS_bt or INS_bts or INS_btr or INS_btc;

    private static bool IsSSEInstruction(instruction ins) =>
        ins >= FIRST_SSE_INSTRUCTION && ins <= LAST_SSE_INSTRUCTION;

    private static bool IsCFCMOV(instruction ins) =>
        ins >= FIRST_CFCMOV_INSTRUCTION && ins <= LAST_CFCMOV_INSTRUCTION;

    private static bool IsApxConditionalInstruction(instruction ins) =>
        IsCCMP(ins) || IsCFCMOV(ins) || IsCTEST(ins);

    private bool IsDstDstSrcAVXInstruction(instruction ins) =>
        UseVexEncodings && (prefixFlags(ins) & INS_FLAGS_IsDstDstSrcAVXInstruction) != 0;

    private bool IsDstSrcSrcAVXInstruction(instruction ins) =>
        UseVexEncodings && (prefixFlags(ins) & INS_FLAGS_IsDstSrcSrcAVXInstruction) != 0;

    private static bool HasRegularWideImmediateForm(instruction ins) =>
        (prefixFlags(ins) & INS_FLAGS_HasSBit) != 0;

    private static regNumber inst3opImulReg(instruction ins)
    {
        assert(instrIs3opImul(ins));
        return (regNumber)(ins - INS_imul_AX);
    }

    private static regNumber getBmiRegNumber(instruction ins) => ins switch
    {
        INS_blsi => (regNumber)3,
        INS_blsmsk => (regNumber)2,
        INS_blsr => (regNumber)1,
        _ => REG_NA,
    };

    private static regNumber getSseShiftRegNumber(instruction ins) => ins switch
    {
        INS_psrldq => (regNumber)3,
        INS_pslldq => (regNumber)7,
        INS_psrld or INS_psrlw or INS_psrlq => (regNumber)2,
        INS_pslld or INS_psllw or INS_psllq => (regNumber)6,
        INS_psrad or INS_psraw or INS_vpsraq => (regNumber)4,
        INS_vprold or INS_vprolq => (regNumber)1,
        INS_vprord or INS_vprorq => (regNumber)0,
        _ => throw new System.InvalidOperationException("Invalid SIMD shift immediate instruction."),
    };

    private static bool insNeedsRRIb(instruction ins) => ins == INS_imul;

    private unsafe ulong insEncodeRRIb(instrDesc id, regNumber reg, emitAttr size)
    {
        assert(size == EA_4BYTE && insNeedsRRIb(id.idIns()));
        ulong code = 0x69C0;
        var regcode = insEncodeReg012(id, reg, size, &code);
        return code | regcode | ((ulong)regcode << 3);
    }

    private unsafe ulong insEncodeMIreg(instrDesc id, regNumber reg, emitAttr size, ulong code)
    {
        assert((code & 0xC000) == 0);
        code |= 0xC000;
        var regcode = insEncodeReg012(id, reg, size, &code);
        code |= (ulong)regcode << 8;
        return code;
    }

    private unsafe ulong insEncodeOpreg(instrDesc id, regNumber reg, emitAttr size)
    {
        var code = (ulong)insCodeRR(id.idIns());
        var regcode = insEncodeReg012(id, reg, size, &code);
        code |= regcode;
        return code;
    }

    private ulong AddX86PrefixIfNeeded(instrDesc id, ulong code, emitAttr size)
    {
        if (TakesEvexPrefix(id))
        {
            return AddEvexPrefix(id, code, size);
        }
        if (TakesVexPrefix(id.idIns()))
        {
            return AddVexPrefix(id.idIns(), code, size);
        }
        if (TakesRex2Prefix(id))
        {
            return AddRex2Prefix(id.idIns(), code);
        }
        return code;
    }

    private ulong AddX86PrefixIfNeededAndNotPresent(instrDesc id, ulong code, emitAttr size)
    {
        if (TakesEvexPrefix(id))
        {
            return hasEvexPrefix(code) ? code : AddEvexPrefix(id, code, size);
        }
        if (TakesVexPrefix(id.idIns()))
        {
            return hasVexPrefix(code) ? code : AddVexPrefix(id.idIns(), code, size);
        }
        if (TakesRex2Prefix(id))
        {
            return hasRex2Prefix(code) ? code : AddRex2Prefix(id.idIns(), code);
        }
        return code;
    }

    private ulong AddRex2Prefix(instruction ins, ulong code)
    {
        assert(IsRex2EncodableInstruction(ins));
        if (ins >= INS_imul_08 && ins <= INS_imul_15)
        {
            code &= 0xFFFFFFFF;
        }
        code |= 0xD50000000000UL;
        if (IsLegacyMap1(code))
        {
            code |= 0x008000000000UL;
        }
        return code;
    }

    private static uint GetCCFromCCMPOrCTEST(instruction ins)
    {
        assert(IsCCMP(ins) || IsCTEST(ins));
        return ins switch
        {
            INS_ccmpo or INS_ctesto => 0,
            INS_ccmpno or INS_ctestno => 1,
            INS_ccmpb or INS_ctestb => 2,
            INS_ccmpae or INS_ctestae => 3,
            INS_ccmpe or INS_cteste => 4,
            INS_ccmpne or INS_ctestne => 5,
            INS_ccmpbe or INS_ctestbe => 6,
            INS_ccmpa or INS_ctesta => 7,
            INS_ccmps or INS_ctests => 8,
            INS_ccmpns or INS_ctestns => 9,
            INS_ccmpt or INS_ctestt => 10,
            INS_ccmpf or INS_ctestf => 11,
            INS_ccmpl or INS_ctestl => 12,
            INS_ccmpge or INS_ctestge => 13,
            INS_ccmple or INS_ctestle => 14,
            INS_ccmpg or INS_ctestg => 15,
            _ => throw new System.InvalidOperationException("Invalid CCMP or CTEST condition."),
        };
    }

    private ulong AddEvexPrefix(instrDesc id, ulong code, emitAttr size)
    {
        var ins = id.idIns();
        assert(IsEvexEncodableInstruction(ins));
        if (instrIsExtendedReg3opImul(ins))
        {
            code &= 0xFFFFFFFF;
        }
        assert(!hasEvexPrefix(code));
        assert((code & 0xFFFFFFFF00000000UL) == 0);
        code |= 0x62F07C0800000000UL;

        if (IsApxExtendedEvexInstruction(ins))
        {
            var flags = prefixFlags(ins);
            if ((flags & (Encoding_VEX | Encoding_EVEX)) == 0)
            {
                code |= 0x4000000000000UL;
            }
            if (IsApxNddCompatibleInstruction(ins) && id.idIsEvexNdContextSet())
            {
                code |= 0x1000000000UL;
            }
            if (IsApxZuCompatibleInstruction(ins) && id.idIsEvexZuContextSet())
            {
                code |= 0x1000000000UL;
            }
            if (IsApxNfCompatibleInstruction(ins) && id.idIsEvexNfContextSet())
            {
                code |= 0x400000000UL;
            }
            if (size == EA_2BYTE)
            {
                code |= 0x10000000000UL;
            }
            if (IsCCMP(ins) || IsCTEST(ins))
            {
                code &= 0xFFFF87F0FFFFFFFFUL;
                code |= (ulong)GetCCFromCCMPOrCTEST(ins) << 32;
                code |= (ulong)id.idGetEvexDFV() << 43;
            }
            return code;
        }
        assert(!IsApxExtendedEvexInstruction(ins));
        if (size == EA_32BYTE)
        {
            code |= 0x0000002000000000UL;
        }
        else if (size == EA_64BYTE)
        {
            code |= 0x0000004000000000UL;
        }
        if (id.idIsEvexbContextSet())
        {
            if (!id.idHasMem())
            {
                switch (id.idGetEvexbContext())
                {
                    case 1:
                    {
                        code = (code & ~0x0000004000000000UL) | 0x0000002000000000UL;
                        break;
                    }

                    case 2:
                    {
                        code = (code & ~0x0000002000000000UL) | 0x0000004000000000UL;
                        break;
                    }

                    case 3:
                    {
                        code |= 0x0000006000000000UL;
                        break;
                    }

                    default:
                    {
                        throw new System.InvalidOperationException("Invalid EVEX embedded rounding mode.");
                    }
                }
                code |= 0x0000001000000000UL;
            }
            else if (HasEmbeddedBroadcast(id))
            {
                code |= 0x0000001000000000UL;
            }
        }

        var maskReg = REG_NA;
        switch (id.idInsFmt())
        {
            case IF_RWR_RRD_RRD_RRD:
            {
                assert(!id.idIsEvexAaaContextSet());
                maskReg = id.idReg4();
                break;
            }

            default:
            {
                if (!IsCCMP(ins) && !IsCTEST(ins))
                {
                    var aaa = id.idGetEvexAaaContext();
                    if (aaa != 0)
                    {
                        maskReg = (regNumber)((int)REG_K0 + (int)aaa);
                    }
                }
                break;
            }
        }
        if (isMaskReg(maskReg))
        {
            code |= (ulong)(maskReg - REG_K0) << 32;
            if (id.idIsEvexZContextSet())
            {
                code |= 0x0000008000000000UL;
            }
        }
        return code;
    }

    private ulong AddRexRPrefix(instrDesc id, ulong code)
    {
        if (hasEvexPrefix(code))
        {
            return code & 0xFF7FFFFFFFFFFFFFUL;
        }
        if (hasVexPrefix(code))
        {
            return code & 0xFF7FFFFFFFFFFFUL;
        }
        if (TakesRex2Prefix(id))
        {
            return code | 0xD50400000000UL;
        }
        return code | 0x4400000000UL;
    }

    private unsafe uint insEncodeReg345(instrDesc id, regNumber reg, emitAttr size, ulong* code)
    {
        assert(reg < REG_STK);
        if (IsExtendedReg(reg))
        {
            if (isHighSimdReg(reg))
            {
                *code &= 0xFFEFFFFFFFFFFFFFUL;
            }
            if (((uint)AbsRegNumber(reg) & 8) != 0)
            {
                *code = AddRexRPrefix(id, *code);
            }
            if (IsExtendedGPReg(reg))
            {
                assert(TakesRex2Prefix(id) || TakesEvexPrefix(id));
                if (hasRex2Prefix(*code))
                {
                    *code |= 0x004000000000UL;
                }
                else if (hasEvexPrefix(*code))
                {
                    *code &= 0xFFEFFFFFFFFFFFFFUL;
                }
            }
        }
        else if (EA_SIZE(size) == EA_1BYTE && reg > REG_RBX)
        {
            *code = hasRex2Prefix(*code) || hasEvexPrefix(*code) ? *code
                : AddRexPrefix(id.idIns(), *code);
        }
        return RegEncoding(reg) << 3;
    }

    private ulong insEncodeReg3456(instrDesc id, regNumber reg, emitAttr size, ulong code)
    {
        var ins = id.idIns();
        assert(reg < REG_STK);
        assert(IsSimdVexOrEvexEncodableInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        assert(hasVexPrefix(code) || hasEvexPrefix(code));
        ulong bits = RegEncoding(reg);
        if (IsExtendedReg(reg))
        {
            bits |= 8;
        }
        if (IsSimdVexOrEvexEncodableInstruction(ins))
        {
            if (TakesEvexPrefix(id) && hasEvexPrefix(code))
            {
                bits = (uint)AbsRegNumber(reg) & 0xF;
                if (isHighSimdReg(reg) || (isHighGPReg(reg) && IsBMIInstruction(ins)))
                {
                    code &= 0xFFFFFFF7FFFFFFFFUL;
                }
                return code ^ (bits << 43);
            }
            assert(hasVexPrefix(code));
            return code ^ (bits << 35);
        }
        assert(TakesEvexPrefix(id) && hasEvexPrefix(code));
        bits = (uint)AbsRegNumber(reg) & 0xF;
        if (isHighGPReg(reg))
        {
            code &= 0xFFFFFFF7FFFFFFFFUL;
        }
        return code ^ (bits << 43);
    }
#endif
}
