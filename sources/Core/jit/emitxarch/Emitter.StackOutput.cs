// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe byte* emitOutputSV(byte* dst, instrDesc id, ulong code, CnsVal* addc)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-variable byte output requires Windows AMD64.");
#else
        assert(id.idHasMemStk());

        var ins = id.idIns();
        var size = id.idOpSize();
        var opsz = EA_SIZE_IN_BYTES(size);
        assert(ins != INS_imul || id.idReg1() == REG_EAX || size is EA_4BYTE or EA_8BYTE);

        if (addc is not null && size > EA_1BYTE)
        {
            var cval = addc->cnsVal;
            if (ImmCanUseSByteEncoding(ins, cval) && !addc->cnsReloc)
            {
                if (id.idInsFmt() is not (IF_SRW_SHF or IF_RRW_SRD_CNS or IF_RWR_RRD_SRD_CNS)
                    && !IsSimdInstruction(ins))
                {
                    code |= 2;
                }
                opsz = 1;
            }
        }
        code = AddX86PrefixIfNeededAndNotPresent(id, code, size);
        if (TakesRexWPrefix(id))
        {
            code = AddRexWPrefix(id, code);
        }

        if (EncodedBySSE38orSSE3A(ins) || ins == INS_crc32)
        {
            if (ins == INS_crc32 && size > EA_1BYTE)
            {
                code |= 0x0100;
                if (size == EA_2BYTE)
                {
                    dst += emitOutputByte(dst, 0x66);
                }
            }

            var reg345 = IsBMIInstruction(ins) ? getBmiRegNumber(ins) : REG_NA;
            if (reg345 == REG_NA)
            {
                reg345 = id.idReg1();
            }
            else
            {
                code = insEncodeReg3456(id, id.idReg1(), size, code);
            }

            var regcode = insEncodeReg345(id, reg345, size, &code);
            dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);

            if (UseSimdEncoding() && ins != INS_crc32)
            {
                assert((code & 0xFF) == 0);
                dst += emitOutputByte(dst, unchecked((long)(code >> 8)));
            }
            else
            {
                dst += emitOutputWord(dst, unchecked((long)(code >> 16)));
                dst += emitOutputWord(dst, unchecked((long)code));
            }

            code = regcode;
        }
        else if ((code & 0xFF000000) != 0)
        {
            if (size == EA_2BYTE)
            {
                assert(ins == INS_movbe);
                dst += emitOutputByte(dst, 0x66);
            }

            dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
            if ((code & 0xFF000000) != 0)
            {
                dst += emitOutputWord(dst, unchecked((long)(code >> 16)));
            }
            code &= 0xFFFF;
        }
        else if ((code & 0x00FF0000) != 0)
        {
            if (size == EA_2BYTE && ins == INS_cmpxchg)
            {
                dst += emitOutputByte(dst, 0x66);
            }
            assert(ins != INS_bt);

            dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
            if ((code & 0x00FF0000) != 0)
            {
                dst += emitOutputByte(dst, unchecked((long)(code >> 16)));
                code &= 0xFFFF;
            }

            if (size != EA_1BYTE && HasRegularWideForm(ins))
            {
                code |= 1;
            }
        }
        else if (CodeGen.instIsFP(ins))
        {
            assert(size is EA_4BYTE or EA_8BYTE);
            if (size == EA_8BYTE)
            {
                code += 4;
            }
        }
        else if (!IsSSEInstruction(ins) && !IsSimdVexOrEvexEncodableInstruction(ins))
        {
            switch (size)
            {
                case EA_1BYTE:
                {
                    assert(ins is not (INS_lzcnt_apx or INS_tzcnt_apx or INS_popcnt_apx));
                    break;
                }

                case EA_2BYTE:
                {
                    if (!TakesEvexPrefix(id))
                    {
                        dst += emitOutputByte(dst, 0x66);
                    }
                    else
                    {
                        code |= EXTENDED_EVEX_PP_BITS;
                    }
                    code |= 1;
                    break;
                }

                case EA_4BYTE:
                {
                    code |= 1;
                    break;
                }

                case EA_8BYTE:
                {
                    if (TakesEvexPrefix(id))
                    {
                        assert(hasEvexPrefix(code));
                        code = AddRexWPrefix(id, code);
                    }
                    if (ins is not (INS_lzcnt_apx or INS_tzcnt_apx or INS_popcnt_apx))
                    {
                        code |= 1;
                    }
                    break;
                }

                default:
                {
                    throw new FatalJitException("Unexpected stack operand size.");
                }
            }

            if (ins >= INS_imul_08 && ins <= INS_imul_31)
            {
                _ = insEncodeReg345(id, inst3opImulReg(ins), size, &code);
            }
        }

        dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);

        var compiler = _compiler ?? throw new FatalJitException("An initialized compiler is required for stack output.");
        var varNum = id.idAddr().iiaLclVar.lvaVarNum();
        var adr = compiler.lvaFrameAddress(varNum, out var ebpBased);
        var dsp = unchecked(adr + (int)id.idAddr().iiaLclVar.lvaOffset());
        bool dspInByte;

        if (IsEvexEncodableInstruction(ins))
        {
            if (HasCompressedDisplacement(id))
            {
                var isCompressed = TryEvexCompressDisp8Byte(id, dsp, out var compressedDsp, out dspInByte);
                assert(isCompressed && dspInByte);
                dsp = unchecked((int)compressedDsp);
            }
            else if (TakesEvexPrefix(id) && !IsBMIInstruction(ins)
                && !(IsApxExtendedEvexInstruction(ins) && !IsSimdInstruction(ins)))
            {
                dspInByte = false;
            }
            else
            {
                dspInByte = unchecked((sbyte)dsp) == dsp;
            }
        }
        else
        {
            dspInByte = unchecked((sbyte)dsp) == dsp;
        }

        var dspIsZero = dsp == 0;
        assert(!id.idIsDspReloc());

        if (ebpBased)
        {
            if (EncodedBySSE38orSSE3A(ins) || ins == INS_crc32)
            {
                dst += emitOutputByte(dst, unchecked((long)(code | (dspInByte ? 0x45u : 0x85u))));
            }
            else
            {
                dst += emitOutputWord(dst, unchecked((long)(code | (dspInByte ? 0x4500u : 0x8500u))));
            }

            if (dspInByte)
            {
                dst += emitOutputByte(dst, dsp);
            }
            else
            {
                dst += emitOutputLong(dst, dsp);
            }
        }
        else
        {
#if !FEATURE_FIXED_OUT_ARGS
            dsp = unchecked(dsp + emitCurStackLvl);
            if (IsEvexEncodableInstruction(ins))
            {
                assert(!HasCompressedDisplacement(id));
                if (!TakesEvexPrefix(id))
                {
                    dspInByte = unchecked((sbyte)dsp) == dsp;
                }
            }
            else
            {
                dspInByte = unchecked((sbyte)dsp) == dsp;
            }
            dspIsZero = dsp == 0;
#endif

            if (EncodedBySSE38orSSE3A(ins) || ins == INS_crc32)
            {
                dst += emitOutputByte(dst, unchecked((long)(code | (dspIsZero ? 0x04u : dspInByte ? 0x44u : 0x84u))));
            }
            else
            {
                dst += emitOutputWord(dst, unchecked((long)(code | (dspIsZero ? 0x0400u : dspInByte ? 0x4400u : 0x8400u))));
            }
            dst += emitOutputByte(dst, 0x24);

            if (!dspIsZero)
            {
                if (dspInByte)
                {
                    dst += emitOutputByte(dst, dsp);
                }
                else
                {
                    dst += emitOutputLong(dst, dsp);
                }
            }
        }

        if (addc is not null)
        {
            var cval = addc->cnsVal;
            noway_assert(opsz < 8 || ((int)cval == cval && !addc->cnsReloc));

            switch (opsz)
            {
                case 0:
                case 4:
                case 8:
                {
                    dst += emitOutputLong(dst, (long)cval);
                    break;
                }

                case 2:
                {
                    dst += emitOutputWord(dst, (long)cval);
                    break;
                }

                case 1:
                {
                    dst += emitOutputByte(dst, (long)cval);
                    break;
                }

                default:
                {
                    throw new FatalJitException("Unexpected stack immediate size.");
                }
            }

            if (addc->cnsReloc)
            {
                emitRecordRelocation(dst - sizeof(int), (void*)cval, RELOC_DISP32);
                assert(opsz == 4);
            }
        }

        if (id.idGCref() != GCT_NONE)
        {
            adr = unchecked(adr + (int)(id.idAddr().iiaLclVar.lvaOffset() & ~(uint)(TARGET_POINTER_SIZE - 1)));
            switch (id.idInsFmt())
            {
                case IF_SRD:
                case IF_SRD_CNS:
                case IF_SWR_CNS:
                case IF_SRD_RRD:
                case IF_RRD_SRD:
                case IF_SRW_CNS:
                case IF_SRW_RRD:
                case IF_SRW_RRW:
                case IF_SRW:
                {
                    break;
                }

                case IF_SWR:
                case IF_SWR_RRD:
                {
                    emitGCvarLiveUpd(adr, varNum, id.idGCref(), dst
#if DEBUG
                        , unchecked((uint)varNum)
#endif
                        );
                    break;
                }

                case IF_RWR_SRD:
                {
                    emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
                    break;
                }

                case IF_RRW_SRD:
                {
                    assert(id.idGCref() == GCT_BYREF && (ins is INS_add or INS_sub or INS_sub_hide));
                    emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
                    break;
                }

                case IF_RWR_RRD_SRD:
                {
                    assert(id.idGCref() == GCT_BYREF && (ins is INS_add or INS_sub or INS_sub_hide));
                    assert(id.idIsEvexNdContextSet());
                    emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
                    break;
                }

                default:
                {
                    throw new FatalJitException(
                        $"Unexpected GC reference stack instruction format {id.idInsFmt()} for {ins}.");
                }
            }
        }
        else if (!emitInsCanOnlyWriteSSE2OrAVXReg(id))
        {
            switch (id.idInsFmt())
            {
                case IF_RWR_SRD:
                case IF_RRW_SRD:
                case IF_RWR_RRD_SRD:
                case IF_RRW_RRD_SRD:
                {
                    emitGCregDeadUpd(id.idReg1(), dst);
                    break;
                }

                case IF_RWR_RWR_SRD:
                {
                    emitGCregDeadUpd(id.idReg1(), dst);
                    emitGCregDeadUpd(id.idReg2(), dst);
                    break;
                }
            }

            if (ins is INS_mulEAX or INS_imulEAX)
            {
                emitGCregDeadUpd(REG_EAX, dst);
                emitGCregDeadUpd(REG_EDX, dst);
            }

            if (instrIs3opImul(ins))
            {
                emitGCregDeadUpd(inst3opImulReg(ins), dst);
            }
        }

        return dst;
#endif
    }
}
#endif
