// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe byte* emitOutputR(byte* dst, instrDesc id)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register output requires AMD64.");
#else
        var ins = id.idIns();
        var reg = id.idReg1();
        var size = id.idOpSize();
        assert(!id.idHasReg2());
        assert(!IsSSEInstruction(ins));
        assert(!IsSimdVexOrEvexEncodableInstruction(ins));

        ulong code;
        switch (ins)
        {
            case INS_inc:
            case INS_dec:
            {
                ins = (instruction)(ins + 1);
                if (size == EA_2BYTE && !TakesEvexPrefix(id))
                {
                    dst += emitOutputByte(dst, 0x66);
                }

                code = (ulong)insCodeRR(ins);
                if (size != EA_1BYTE)
                {
                    code |= 1;
                }
                code = AddX86PrefixIfNeeded(id, code, size);
                if (TakesRexWPrefix(id))
                {
                    code = AddRexWPrefix(id, code);
                }
                var regcode = insEncodeReg012(id, reg, size, &code);
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                dst += emitOutputWord(dst, unchecked((long)(code | ((ulong)regcode << 8))));
                break;
            }

            case INS_pop:
            case INS_pop_hide:
            case INS_push:
            case INS_push_hide:
            {
                assert(size == EA_PTRSIZE);
                code = insEncodeOpreg(id, reg, size);
                if (TakesRex2Prefix(id))
                {
                    code = AddRex2Prefix(ins, code);
                    if (id.idIsApxPpxContextSet())
                    {
                        code = AddRexWPrefix(id, code);
                    }
                }
                assert(!TakesSimdPrefix(id));
                assert(!TakesRexWPrefix(id));
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                dst += emitOutputByte(dst, unchecked((long)code));
                break;
            }

            case INS_bswap:
            {
                assert(size >= EA_4BYTE && size <= EA_PTRSIZE);
                code = (ulong)insCodeRR(ins);
                if (TakesRex2Prefix(id))
                {
                    code = AddRex2Prefix(ins, code);
                }
                if (TakesRexWPrefix(id))
                {
                    code = AddRexWPrefix(id, code);
                }
                var regcode = insEncodeReg012(id, reg, size, &code);
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                dst += TakesRex2Prefix(id)
                    ? emitOutputByte(dst, unchecked((long)(code | regcode)))
                    : emitOutputWord(dst, unchecked((long)(code | ((ulong)regcode << 8))));
                break;
            }

            case INS_seto:
            case INS_setno:
            case INS_setb:
            case INS_setae:
            case INS_sete:
            case INS_setne:
            case INS_setbe:
            case INS_seta:
            case INS_sets:
            case INS_setns:
            case INS_setp:
            case INS_setnp:
            case INS_setl:
            case INS_setge:
            case INS_setle:
            case INS_setg:
            {
                assert(id.idGCref() == GCT_NONE);
                assert(size == EA_1BYTE);
                code = (ulong)insCodeMR(ins);
                if (TakesRex2Prefix(id))
                {
                    code = AddRex2Prefix(ins, code);
                }
                code = insEncodeMRreg(id, reg, EA_1BYTE, code);
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                if (!TakesRex2Prefix(id))
                {
                    assert((code & 0x00FF0000) != 0);
                    dst += emitOutputByte(dst, unchecked((long)(code >> 16)));
                }
                dst += emitOutputWord(dst, unchecked((long)(code & 0xFFFF)));
                break;
            }

            case INS_seto_apx:
            case INS_setno_apx:
            case INS_setb_apx:
            case INS_setae_apx:
            case INS_sete_apx:
            case INS_setne_apx:
            case INS_setbe_apx:
            case INS_seta_apx:
            case INS_sets_apx:
            case INS_setns_apx:
            case INS_setp_apx:
            case INS_setnp_apx:
            case INS_setl_apx:
            case INS_setge_apx:
            case INS_setle_apx:
            case INS_setg_apx:
            {
                assert(TakesEvexPrefix(id));
                assert(size == EA_1BYTE);
                code = AddEvexPrefix(id, (ulong)insCodeMR(ins), size);
                code = insEncodeMRreg(id, reg, EA_1BYTE, code);
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                dst += emitOutputWord(dst, unchecked((long)(code & 0xFFFF)));
                break;
            }

            default:
            {
                if (ins is INS_mulEAX or INS_imulEAX)
                {
                    emitGCregDeadUpd(REG_EAX, dst);
                    emitGCregDeadUpd(REG_EDX, dst);
                }
                assert(id.idGCref() == GCT_NONE);
                code = (ulong)insCodeMR(ins);
                code = AddX86PrefixIfNeeded(id, code, size);
                code = insEncodeMRreg(id, reg, size, code);
                if (size != EA_1BYTE)
                {
                    code |= 1;
                    if (size == EA_2BYTE && !TakesEvexPrefix(id))
                    {
                        dst += emitOutputByte(dst, 0x66);
                    }
                }
                if (TakesRexWPrefix(id))
                {
                    code = AddRexWPrefix(id, code);
                }
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                dst += emitOutputWord(dst, unchecked((long)code));
                break;
            }
        }

        switch (id.idInsFmt())
        {
            case IF_RRD:
            {
                break;
            }

            case IF_RWR:
            {
                if (id.idGCref() != GCT_NONE)
                {
                    emitGCregLiveUpd(id.idGCref(), reg, dst);
                }
                else
                {
                    emitGCregDeadUpd(reg, dst);
                }
                break;
            }

            case IF_RRW:
            {
                if (id.idGCref() != GCT_NONE)
                {
                    assert(ins is INS_inc or INS_dec or INS_inc_l or INS_dec_l);
                    assert(id.idGCref() == GCT_BYREF);
                    emitGCregLiveUpd(GCT_BYREF, reg, dst);
                }
                else
                {
                    assert((emitThisGCrefRegs & reg.SingleTypeMask) == SRBM_NONE);
                }
                break;
            }

            default:
            {
#if DEBUG
                emitDispIns(id, false, false, false);
#endif
                assert(false, "unexpected instruction format");
                break;
            }
        }

        return dst;
#endif
    }

    public unsafe byte* emitOutputRR(byte* dst, instrDesc id)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register-pair output requires AMD64.");
#else
        var ins = id.idIns();
        var reg1 = id.idReg1();
        var reg2 = id.idReg2();
        var size = id.idOpSize();
        assert(!id.idHasReg3());
        var isInsCodeMR = false;
        ulong code;

        if (IsSimdInstruction(ins))
        {
            assert((ins is not (INS_movd32 or INS_movd64)) || (reg1.IsFltReg != reg2.IsFltReg));
            if (ins is INS_kmovb_gpr or INS_kmovw_gpr or INS_kmovd_gpr or INS_kmovq_gpr)
            {
                assert(!(reg1.IsIntReg && reg2.IsIntReg));
                code = (ulong)insCodeRM(ins);
                if (reg1.IsIntReg)
                {
                    code |= 1;
                }
            }
            else
            {
                isInsCodeMR = ins is INS_vcompresspd or INS_vcompressps or INS_vpcompressb or
                    INS_vpcompressd or INS_vpcompressq or INS_vpcompressw ||
                    (ins is INS_movd32 or INS_movd64 && !reg1.IsFltReg);
                code = isInsCodeMR ? (ulong)insCodeMR(ins) : (ulong)insCodeRM(ins);
            }
            code = AddX86PrefixIfNeeded(id, code, size);
            code = insEncodeRMreg(id, code);
            if (TakesRexWPrefix(id))
            {
                code = AddRexWPrefix(id, code);
            }
        }
        else if (ins is INS_movsx or INS_movzx || insIsCMOV(ins))
        {
            assert(hasCodeRM(ins) && !hasCodeMI(ins) && !hasCodeMR(ins));
            code = AddX86PrefixIfNeeded(id, (ulong)insCodeRM(ins), size);
            code = insEncodeRMreg(id, code) | (size == EA_2BYTE ? 1UL : 0);
            assert(size < EA_4BYTE || insIsCMOV(ins));
            if (size == EA_8BYTE || ins == INS_movsx)
            {
                code = AddRexWPrefix(id, code);
            }
        }
        else if (ins == INS_movsxd)
        {
            assert(hasCodeRM(ins) && !hasCodeMI(ins) && !hasCodeMR(ins));
            code = AddX86PrefixIfNeeded(id, (ulong)insCodeRM(ins), size);
            code = AddRexWPrefix(id, insEncodeRMreg(id, code));
        }
        else if (ins is INS_bsf or INS_bsr or INS_crc32 or INS_lzcnt or INS_popcnt or INS_tzcnt or
            INS_lzcnt_apx or INS_tzcnt_apx or INS_popcnt_apx or INS_crc32_apx)
        {
            assert(hasCodeRM(ins) && !hasCodeMI(ins) && !hasCodeMR(ins));
            code = AddX86PrefixIfNeeded(id, (ulong)insCodeRM(ins), size);
            code = insEncodeRMreg(id, code);
            if (ins == INS_crc32 && size > EA_1BYTE)
            {
                code |= 0x0100;
            }
            if (ins == INS_crc32_apx && size > EA_1BYTE)
            {
                code |= 1;
            }
            if (size == EA_2BYTE)
            {
                if (!TakesEvexPrefix(id))
                {
                    assert(ins == INS_crc32);
                    dst += emitOutputByte(dst, 0x66);
                }
                else
                {
                    code |= 0x10000000000UL;
                }
            }
            else if (size == EA_8BYTE)
            {
                code = AddRexWPrefix(id, code);
            }
        }
        else if (ins is INS_push2 or INS_pop2)
        {
            assert(size == EA_PTRSIZE && TakesEvexPrefix(id));
            code = insEncodeRMreg(id, AddX86PrefixIfNeeded(id, (ulong)insCodeMR(ins), size));
            assert(id.idIsApxPpxContextSet());
            code = AddRexWPrefix(id, code);
        }
        else
        {
            assert(!TakesSimdPrefix(id) || TakesEvexPrefix(id));
            code = insEncodeRMreg(id, AddX86PrefixIfNeeded(id, (ulong)insCodeMR(ins), size));
            isInsCodeMR = IsBitTestInstruction(ins);
            if (ins != INS_test && !IsShiftInstruction(ins) && !IsCFCMOV(ins) && !IsCTEST(ins))
            {
                code |= 2;
            }
            switch (size)
            {
                case EA_1BYTE:
                {
                    noway_assert(reg1.IsIntReg);
                    noway_assert(reg2.IsIntReg);
                    break;
                }

                case EA_2BYTE:
                {
                    if (TakesEvexPrefix(id))
                    {
                        assert(hasEvexPrefix(code));
                        assert((code & 0x10000000000UL) != 0);
                    }
                    else
                    {
                        dst += emitOutputByte(dst, 0x66);
                    }
                    if (!IsCFCMOV(ins))
                    {
                        code |= 1;
                    }
                    break;
                }

                case EA_4BYTE:
                {
                    if (TakesEvexPrefix(id))
                    {
                        assert(hasEvexPrefix(code));
                        assert((code & 0x10000000000UL) == 0);
                    }
                    if (!IsCFCMOV(ins))
                    {
                        code |= 1;
                    }
                    break;
                }

                case EA_8BYTE:
                {
                    if (ins != INS_xor || reg1 != reg2)
                    {
                        code = AddRexWPrefix(id, code);
                    }
                    else
                    {
                        id.idOpSize(EA_4BYTE);
                    }
                    if (!IsCFCMOV(ins))
                    {
                        code |= 1;
                    }
                    break;
                }

                default:
                {
                    assert(false, "unexpected size");
                    break;
                }
            }
        }

        var regFor012Bits = reg2;
        var regFor345Bits = IsBMIInstruction(ins) ? getBmiRegNumber(ins) : REG_NA;
        if (regFor345Bits == REG_NA)
        {
            regFor345Bits = reg1;
        }
        if (isInsCodeMR)
        {
            (regFor012Bits, regFor345Bits) = (regFor345Bits, regFor012Bits);
        }

        uint regCode;
        if (!IsApxNddEncodableInstruction(ins) || !id.idIsEvexNdContextSet())
        {
            regCode = insEncodeReg345(id, regFor345Bits, size, &code);
            regCode |= insEncodeReg012(id, regFor012Bits, size, &code);
        }
        else
        {
            code = insEncodeReg3456(id, reg1, size, code);
            regCode = insEncodeReg012(id, reg2, size, &code);
        }

        if (TakesSimdPrefix(id) && !IsApxConditionalInstruction(ins))
        {
            if (IsDstDstSrcAVXInstruction(ins))
            {
                code = insEncodeReg3456(id, reg1, size, code);
            }
            else if (IsDstSrcSrcAVXInstruction(ins))
            {
                code = insEncodeReg3456(id, reg2, size, code);
            }
        }

        dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
        if ((code & 0xFF000000) != 0)
        {
            dst += emitOutputWord(dst, unchecked((long)(code >> 16)));
            code &= 0xFFFF;
            if (Is4ByteSSEInstruction(ins))
            {
                dst += emitOutputByte(dst, unchecked((long)code));
                code &= 0xFF00;
            }
        }
        else if ((code & 0x00FF0000) != 0)
        {
            dst += emitOutputByte(dst, unchecked((long)(code >> 16)));
            code &= 0xFFFF;
        }

        if ((code & 0xFF00) == 0xC000)
        {
            dst += emitOutputWord(dst, unchecked((long)(code | ((ulong)regCode << 8))));
        }
        else if ((code & 0xFF) == 0)
        {
            assert(IsSimdVexOrEvexEncodableInstruction(ins) || Is4ByteSSEInstruction(ins));
            dst += emitOutputByte(dst, unchecked((long)((code >> 8) & 0xFF)));
            dst += emitOutputByte(dst, 0xC0 | regCode);
        }
        else if (IsApxNddEncodableInstruction(ins) && id.idIsEvexNdContextSet())
        {
            dst += emitOutputByte(dst, unchecked((long)(code & 0xFF)));
            dst += emitOutputByte(dst, unchecked((long)(0xC0 | regCode | (code >> 8))));
        }
        else
        {
            dst += emitOutputWord(dst, unchecked((long)code));
            dst += emitOutputByte(dst, 0xC0 | regCode);
        }

        if (id.idGCref() != GCT_NONE)
        {
            emitHandleGCrefRegs(dst, id);
        }
        else if (!emitInsCanOnlyWriteSSE2OrAVXReg(id))
        {
            switch (id.idInsFmt())
            {
                case IF_RRD_CNS:
                {
                    assert(ins is not (INS_mulEAX or INS_imulEAX));
                    if (instrIs3opImul(ins))
                    {
                        emitGCregDeadUpd(inst3opImulReg(ins), dst);
                    }
                    break;
                }

                case IF_RWR_RRD:
                case IF_RRW_RRD:
                {
                    emitGCregDeadUpd(reg1, dst);
                    break;
                }
            }
        }
        return dst;
#endif
    }

    public unsafe byte* emitOutputRRR(byte* dst, instrDesc id)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Three-register output requires AMD64.");
#else
        var ins = id.idIns();
        assert(IsSimdVexOrEvexEncodableInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins) || isAvxBlendv(ins) || isAvx512Blendv(ins) ||
            IsKInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        var targetReg = id.idReg1();
        var src1 = id.idReg2();
        var src2 = id.idReg3();
        var size = id.idOpSize();

        var code = AddX86PrefixIfNeeded(id, (ulong)insCodeRM(ins), size);
        if (IsApxExtendedEvexInstruction(ins) && !IsBMIInstruction(ins))
        {
            (src1, targetReg) = (targetReg, src1);
            switch (size)
            {
                case EA_1BYTE:
                {
                    noway_assert(src1.IsIntReg);
                    noway_assert(src2.IsIntReg);
                    noway_assert(targetReg.IsIntReg);
                    break;
                }

                case EA_2BYTE:
                case EA_4BYTE:
                {
                    if (!insIsCMOV(ins) && !IsCFCMOV(ins))
                    {
                        code |= 1;
                    }
                    break;
                }

                case EA_8BYTE:
                {
                    code = AddRexWPrefix(id, code);
                    if (!insIsCMOV(ins) && !IsCFCMOV(ins))
                    {
                        code |= 1;
                    }
                    break;
                }

                default:
                {
                    assert(false, "unexpected size");
                    break;
                }
            }
        }
        code = insEncodeRMreg(id, code);
        if (TakesRexWPrefix(id))
        {
            code = AddRexWPrefix(id, code);
        }
        var regCode = insEncodeReg345(id, targetReg, size, &code);
        regCode |= insEncodeReg012(id, src2, size, &code);
        code = insEncodeReg3456(id, src1, size, code);

        dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
        if ((code & 0xFF000000) != 0)
        {
            dst += emitOutputWord(dst, unchecked((long)(code >> 16)));
            code &= 0xFFFF;
        }
        else if ((code & 0x00FF0000) != 0)
        {
            dst += emitOutputByte(dst, unchecked((long)(code >> 16)));
            code &= 0xFFFF;
        }
        if ((code & 0xFF00) == 0xC000)
        {
            dst += emitOutputWord(dst, unchecked((long)(code | ((ulong)regCode << 8))));
        }
        else if ((code & 0xFF) == 0)
        {
            assert(IsSimdVexOrEvexEncodableInstruction(ins));
            dst += emitOutputByte(dst, unchecked((long)((code >> 8) & 0xFF)));
            dst += emitOutputByte(dst, 0xC0 | regCode);
        }
        else
        {
            dst += emitOutputWord(dst, unchecked((long)code));
            dst += emitOutputByte(dst, 0xC0 | regCode);
        }

        if (id.idGCref() != GCT_NONE)
        {
            emitHandleGCrefRegs(dst, id);
        }
        if (!emitInsCanOnlyWriteSSE2OrAVXReg(id))
        {
            switch (id.idInsFmt())
            {
                case IF_RWR_RRD_RRD:
                case IF_RRW_RRD_RRD:
                case IF_RWR_RRD_RRD_CNS:
                case IF_RWR_RRD_RRD_RRD:
                {
                    emitGCregDeadUpd(id.idReg1(), dst);
                    break;
                }

                case IF_RWR_RWR_RRD:
                {
                    emitGCregDeadUpd(id.idReg1(), dst);
                    emitGCregDeadUpd(id.idReg2(), dst);
                    break;
                }
            }
        }
        return dst;
#endif
    }
}
