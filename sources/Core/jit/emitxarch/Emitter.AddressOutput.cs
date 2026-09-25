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
    public unsafe byte* emitOutputAM(byte* dst, instrDesc id, ulong code, CnsVal* addc)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Address-mode byte output requires AMD64.");
#else
        assert(_compiler is not null);
        assert(id.idHasMemAdr());
        nint dsp;
        bool dspInByte;
        bool dspIsZero;
        var ins = id.idIns();
        var size = id.idOpSize();
        var opsz = EA_SIZE_IN_BYTES(size);
        var reg = id.idAddr().iiaAddrMode.amBaseReg;
        var rgx = id.idAddr().iiaAddrMode.amIndxReg;

        if (ins is INS_call or INS_tail_i_jmp)
        {
            code = AddX86PrefixIfNeeded(id, code, size);
            if (ins == INS_tail_i_jmp)
            {
                // The unwinder recognizes indirect tail jumps as epilog instructions only with REX.W.
                code = AddRexWPrefix(id, code);
            }
            #endif
            if (id.idIsCallRegPtr())
            {
                code = insEncodeMRreg(id, reg, EA_PTRSIZE, code);
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                dst += emitOutputWord(dst, unchecked((long)code));
                goto DONE;
            }

            dsp = emitGetInsCIdisp(id);
            if (IsExtendedReg(reg, EA_PTRSIZE))
            {
                _ = insEncodeReg012(id, reg, EA_PTRSIZE, &code);
                reg = (regNumber)RegEncoding(reg);
            }
            if (IsExtendedReg(rgx, EA_PTRSIZE))
            {
                _ = insEncodeRegSIB(id, rgx, &code);
                rgx = (regNumber)RegEncoding(rgx);
            }
            dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
            goto GOT_DSP;
        }

        // Integer immediates can select the sign-extended byte opcode; SIMD control bytes cannot.
        if ((addc != null) && (size > EA_1BYTE))
        {
            if (ImmCanUseSByteEncoding(ins, addc->cnsVal) && !addc->cnsReloc)
            {
                if ((id.idInsFmt() != IF_ARW_SHF) && !IsSimdInstruction(ins))
                {
                    code |= 2;
                }
                opsz = 1;
            }
        }

        code = AddX86PrefixIfNeededAndNotPresent(id, code, size);
        if (TakesSimdPrefix(id))
        {
            if (IsDstDstSrcAVXInstruction(ins))
            {
                regNumber src1;
                switch (id.idInsFmt())
                {
                    case IF_RRD_RRD_ARD:
                    case IF_RWR_RRD_ARD:
                    case IF_RRW_RRD_ARD:
                    case IF_RWR_RWR_ARD:
                    case IF_RRD_ARD_RRD:
                    case IF_RWR_ARD_RRD:
                    case IF_RRW_ARD_RRD:
                    case IF_RWR_RRD_ARD_CNS:
                    case IF_RWR_RRD_ARD_RRD:
                    {
                        src1 = id.idReg2();
                        break;
                    }

                    case IF_RRD_ARD:
                    case IF_RWR_ARD:
                    case IF_RRW_ARD:
                    case IF_AWR_RRD_RRD:
                    case IF_RRD_ARD_CNS:
                    case IF_RWR_ARD_CNS:
                    case IF_RRW_ARD_CNS:
                    {
                        src1 = id.idReg1();
                        break;
                    }

                    default:
                    {
                        assert(false, "Unhandled insFmt in emitOutputAM");
                        src1 = id.idReg1();
                        break;
                    }
                }
                code = insEncodeReg3456(id, src1, size, code);
            }
            else if (IsDstSrcSrcAVXInstruction(ins) && id.idHasReg2())
            {
                code = insEncodeReg3456(id, id.idReg2(), size, code);
            }
        }

        if (TakesRexWPrefix(id))
        {
            code = AddRexWPrefix(id, code);
        }
        if (IsExtendedReg(reg, EA_PTRSIZE))
        {
            _ = insEncodeReg012(id, reg, EA_PTRSIZE, &code);
            reg = (regNumber)RegEncoding(reg);
        }
        if (IsExtendedReg(rgx, EA_PTRSIZE))
        {
            _ = insEncodeRegSIB(id, rgx, &code);
            rgx = (regNumber)RegEncoding(rgx);
        }

        if (EncodedBySSE38orSSE3A(ins) || (ins == INS_crc32))
        {
            if ((ins == INS_crc32) && (size > EA_1BYTE))
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
                if ((id.idInsFmt() == IF_AWR_RRD_RRD) && (ins != INS_extractps))
                {
                    reg345 = id.idReg2();
                }
                else
                {
                    reg345 = id.idReg1();
                }
            }
            var regcode = insEncodeReg345(id, reg345, size, &code);
            dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
            if (UseSimdEncoding() && (ins != INS_crc32))
            {
                assert((code & 0xFF) == 0);
                dst += emitOutputByte(dst, (long)((code >> 8) & 0xFF));
            }
            else
            {
                dst += emitOutputWord(dst, unchecked((long)(code >> 16)));
                dst += emitOutputWord(dst, (long)(code & 0xFFFF));
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
                code &= 0xFFFF;
            }
        }
        else if ((code & 0x00FF0000) != 0)
        {
            if ((size == EA_2BYTE) && (ins == INS_cmpxchg))
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
            if ((size != EA_1BYTE) && HasRegularWideForm(ins))
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
                    break;
                }

                case EA_2BYTE:
                {
                    if (TakesEvexPrefix(id))
                    {
                        assert(IsApxExtendedEvexInstruction(ins));
                        assert(hasEvexPrefix(code));
                        assert((code & EXTENDED_EVEX_PP_BITS) != 0);
                    }
                    else
                    {
                        dst += emitOutputByte(dst, 0x66);
                    }
                    if (IsCFCMOV(ins))
                    {
                        break;
                    }
                    goto case EA_4BYTE;
                }

                case EA_4BYTE:
                case EA_8BYTE:
                {
                    if (ins != INS_movbe_apx)
                    {
                        code |= 1;
                    }
                    break;
                }

                default:
                {
                    NO_WAY("unexpected size");
                    break;
                }
            }
            if ((ins >= INS_imul_08) && (ins <= INS_imul_31))
            {
                _ = insEncodeReg345(id, inst3opImulReg(ins), size, &code);
            }
        }

        dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
        dsp = emitGetInsAmdAny(id);

    GOT_DSP:
        if (id.idIsDspReloc())
        {
            dspInByte = false;
            dspIsZero = false;
        }
        else if (IsEvexEncodableInstruction(ins))
        {
            if (HasCompressedDisplacement(id))
            {
                var isCompressed = TryEvexCompressDisp8Byte(id, dsp, out var compressedDsp, out dspInByte);
                assert(isCompressed && dspInByte);
                dsp = compressedDsp;
            }
            else if (TakesEvexPrefix(id) && !IsBMIInstruction(ins)
                && !(IsApxExtendedEvexInstruction(ins) && !IsSimdInstruction(ins)))
            {
                assert(!(TryEvexCompressDisp8Byte(id, dsp, out _, out dspInByte) && hasTupleTypeInfo(ins)));
                dspInByte = false;
            }
            else
            {
                dspInByte = unchecked((sbyte)dsp) == dsp;
            }
            dspIsZero = dsp == 0;
        }
        else
        {
            dspInByte = unchecked((sbyte)dsp) == dsp;
            dspIsZero = dsp == 0;
        }

        if (rgx == REG_NA)
        {
            switch (reg)
            {
                case REG_NA:
                {
                    if (id.idIsDspReloc())
                    {
                        if (EncodedBySSE38orSSE3A(ins) || (ins == INS_crc32))
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x05));
                        }
                        else
                        {
                            dst += emitOutputWord(dst, (long)(code | 0x0500));
                        }
                        var addlDelta = 0;
                        if (addc != null)
                        {
                            // RIP follows the immediate, so the displacement relocation includes its width.
                            noway_assert((opsz < 8) || ((unchecked((int)addc->cnsVal) == addc->cnsVal) && !addc->cnsReloc));
                            switch (opsz)
                            {
                                case 0:
                                case 4:
                                case 8:
                                {
                                    addlDelta = -4;
                                    break;
                                }

                                case 2:
                                {
                                    addlDelta = -2;
                                    break;
                                }

                                case 1:
                                {
                                    addlDelta = -1;
                                    break;
                                }

                                default:
                                {
                                    unreached();
                                    break;
                                }
                            }
                        }
                        dst += emitOutputLong(dst, 0);
                        if (!IsSimdInstruction(ins) && id.idIsTlsGD())
                        {
                            addlDelta = -4;
                            emitRecordRelocation(dst - sizeof(int), (void*)dsp, CorInfoReloc.AMD64_LIN_TLSGD, addlDelta);
                        }
                        else
                        {
                            emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32, addlDelta);
                        }
                    }
                    else
                    {
                        noway_assert(!_compiler.opts.compReloc);
                        noway_assert(codeGen.genAddrRelocTypeHint((nuint)dsp) != CorInfoReloc.RELATIVE32);
                        noway_assert(unchecked((int)dsp) == dsp);
                        if (EncodedBySSE38orSSE3A(ins) || (ins == INS_crc32))
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x04));
                        }
                        else
                        {
                            dst += emitOutputWord(dst, (long)(code | 0x0400));
                        }
                        dst += emitOutputByte(dst, 0x25);
                        dst += emitOutputLong(dst, dsp);
                    }
                    break;
                }

                case REG_EBP:
                {
                    if (EncodedBySSE38orSSE3A(ins) || (ins == INS_crc32))
                    {
                        if (dspInByte)
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x45));
                            dst += emitOutputByte(dst, dsp);
                        }
                        else
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x85));
                            dst += emitOutputLong(dst, dsp);
                            if (id.idIsDspReloc())
                            {
                                emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                            }
                        }
                    }
                    else if (dspInByte)
                    {
                        dst += emitOutputWord(dst, (long)(code | 0x4500));
                        dst += emitOutputByte(dst, dsp);
                    }
                    else
                    {
                        dst += emitOutputWord(dst, (long)(code | 0x8500));
                        dst += emitOutputLong(dst, dsp);
                        if (id.idIsDspReloc())
                        {
                            emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                        }
                    }
                    break;
                }

                case REG_ESP:
                {
                    if (EncodedBySSE38orSSE3A(ins) || (ins == INS_crc32))
                    {
                        if (dspIsZero)
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x04));
                            dst += emitOutputByte(dst, 0x24);
                        }
                        else if (dspInByte)
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x44));
                            dst += emitOutputByte(dst, 0x24);
                            dst += emitOutputByte(dst, dsp);
                        }
                        else
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x84));
                            dst += emitOutputByte(dst, 0x24);
                            dst += emitOutputLong(dst, dsp);
                            if (id.idIsDspReloc())
                            {
                                emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                            }
                        }
                    }
                    else if (dspIsZero)
                    {
                        dst += emitOutputWord(dst, (long)(code | 0x0400));
                        dst += emitOutputByte(dst, 0x24);
                    }
                    else if (dspInByte)
                    {
                        dst += emitOutputWord(dst, (long)(code | 0x4400));
                        dst += emitOutputByte(dst, 0x24);
                        dst += emitOutputByte(dst, dsp);
                    }
                    else
                    {
                        dst += emitOutputWord(dst, (long)(code | 0x8400));
                        dst += emitOutputByte(dst, 0x24);
                        dst += emitOutputLong(dst, dsp);
                        if (id.idIsDspReloc())
                        {
                            emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                        }
                    }
                    break;
                }

                default:
                {
                    if (EncodedBySSE38orSSE3A(ins) || (ins == INS_crc32))
                    {
                        code |= insEncodeReg012(id, reg, EA_PTRSIZE, null);
                        if (dspIsZero)
                        {
                            dst += emitOutputByte(dst, (long)code);
                        }
                        else if (dspInByte)
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x40));
                            dst += emitOutputByte(dst, dsp);
                        }
                        else
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x80));
                            dst += emitOutputLong(dst, dsp);
                            if (id.idIsDspReloc())
                            {
                                emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                            }
                        }
                    }
                    else
                    {
                        code |= insEncodeReg012(id, reg, EA_PTRSIZE, null) << 8;
                        if (dspIsZero)
                        {
                            dst += emitOutputWord(dst, (long)code);
                        }
                        else if (dspInByte)
                        {
                            dst += emitOutputWord(dst, (long)(code | 0x4000));
                            dst += emitOutputByte(dst, dsp);
                        }
                        else
                        {
                            dst += emitOutputWord(dst, (long)(code | 0x8000));
                            dst += emitOutputLong(dst, dsp);
                            if (id.idIsDspReloc())
                            {
                                emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                            }
                        }
                    }
                    break;
                }
            }
        }
        else
        {
            uint regByte;
            var mul = (uint)emitDecodeScale(id.idAddr().iiaAddrMode.amScale);
            if (mul > 1)
            {
                if (reg != REG_NA)
                {
                    regByte = insEncodeReg012(id, reg, EA_PTRSIZE, null)
                        | insEncodeReg345(id, rgx, EA_PTRSIZE, null) | insSSval(mul);
                    if (EncodedBySSE38orSSE3A(ins) || (ins == INS_crc32))
                    {
                        if (dspIsZero && (reg != REG_EBP))
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x04));
                            dst += emitOutputByte(dst, regByte);
                        }
                        else if (dspInByte)
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x44));
                            dst += emitOutputByte(dst, regByte);
                            dst += emitOutputByte(dst, dsp);
                        }
                        else
                        {
                            dst += emitOutputByte(dst, (long)(code | 0x84));
                            dst += emitOutputByte(dst, regByte);
                            dst += emitOutputLong(dst, dsp);
                            if (id.idIsDspReloc())
                            {
                                emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                            }
                        }
                    }
                    else if (dspIsZero && (reg != REG_EBP))
                    {
                        dst += emitOutputWord(dst, (long)(code | 0x0400));
                        dst += emitOutputByte(dst, regByte);
                    }
                    else if (dspInByte)
                    {
                        dst += emitOutputWord(dst, (long)(code | 0x4400));
                        dst += emitOutputByte(dst, regByte);
                        dst += emitOutputByte(dst, dsp);
                    }
                    else
                    {
                        dst += emitOutputWord(dst, (long)(code | 0x8400));
                        dst += emitOutputByte(dst, regByte);
                        dst += emitOutputLong(dst, dsp);
                        if (id.idIsDspReloc())
                        {
                            emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                        }
                    }
                }
                else
                {
                    regByte = insEncodeReg012(id, REG_EBP, EA_PTRSIZE, null)
                        | insEncodeReg345(id, rgx, EA_PTRSIZE, null) | insSSval(mul);
                    if (EncodedBySSE38orSSE3A(ins) || (ins == INS_crc32))
                    {
                        dst += emitOutputByte(dst, (long)(code | 0x04));
                    }
                    else
                    {
                        dst += emitOutputWord(dst, (long)(code | 0x0400));
                    }
                    dst += emitOutputByte(dst, regByte);
                    if (ins == INS_i_jmp)
                    {
                        dsp = (nint)emitDataOffsetToPtr(unchecked((uint)dsp));
                    }
                    dst += emitOutputLong(dst, dsp);
                    if (id.idIsDspReloc())
                    {
                        emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                    }
                }
            }
            else
            {
                regByte = insEncodeReg012(id, reg, EA_PTRSIZE, null) | insEncodeReg345(id, rgx, EA_PTRSIZE, null);
                if (EncodedBySSE38orSSE3A(ins) || (ins == INS_crc32))
                {
                    if (dspIsZero && (reg != REG_EBP))
                    {
                        dst += emitOutputByte(dst, (long)(code | 0x04));
                        dst += emitOutputByte(dst, regByte);
                    }
                    else if (dspInByte)
                    {
                        dst += emitOutputByte(dst, (long)(code | 0x44));
                        dst += emitOutputByte(dst, regByte);
                        dst += emitOutputByte(dst, dsp);
                    }
                    else
                    {
                        dst += emitOutputByte(dst, (long)(code | 0x84));
                        dst += emitOutputByte(dst, regByte);
                        dst += emitOutputLong(dst, dsp);
                        if (id.idIsDspReloc())
                        {
                            emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                        }
                    }
                }
                else if (dspIsZero && (reg != REG_EBP))
                {
                    dst += emitOutputWord(dst, (long)(code | 0x0400));
                    dst += emitOutputByte(dst, regByte);
                }
                else if (dspInByte)
                {
                    dst += emitOutputWord(dst, (long)(code | 0x4400));
                    dst += emitOutputByte(dst, regByte);
                    dst += emitOutputByte(dst, dsp);
                }
                else
                {
                    dst += emitOutputWord(dst, (long)(code | 0x8400));
                    dst += emitOutputByte(dst, regByte);
                    dst += emitOutputLong(dst, dsp);
                    if (id.idIsDspReloc())
                    {
                        emitRecordRelocation(dst - sizeof(int), (void*)dsp, RELOC_DISP32);
                    }
                }
            }
        }

        if (addc != null)
        {
            var cval = addc->cnsVal;
            noway_assert((opsz < 8) || ((unchecked((int)cval) == cval) && !addc->cnsReloc));
            switch (opsz)
            {
                case 0:
                case 4:
                case 8:
                {
                    dst += emitOutputLong(dst, cval);
                    break;
                }

                case 2:
                {
                    dst += emitOutputWord(dst, cval);
                    break;
                }

                case 1:
                {
                    dst += emitOutputByte(dst, cval);
                    break;
                }

                default:
                {
                    assert(false, "unexpected operand size");
                    break;
                }
            }
            if (addc->cnsReloc)
            {
                emitRecordRelocation(dst - sizeof(int), (void*)cval, RELOC_DISP32);
                assert(opsz == 4);
            }
        }

    DONE:
        if (id.idGCref() != GCT_NONE)
        {
            switch (id.idInsFmt())
            {
                case IF_ARD:
                case IF_AWR:
                case IF_ARW:
                case IF_RRD_ARD:
                case IF_ARD_RRD:
                case IF_AWR_RRD:
                case IF_AWR_RRD_RRD:
                case IF_ARD_CNS:
                case IF_AWR_CNS:
                {
                    break;
                }

                case IF_RWR_ARD:
                {
                    emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
                    break;
                }

                case IF_RRW_ARD:
                {
                    assert(((id.idGCref() == GCT_BYREF) && ((ins is INS_add or INS_sub or INS_sub_hide) || insIsCMOV(ins)))
                        || ((id.idGCref() == GCT_GCREF) && insIsCMOV(ins)));
                    emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
                    break;
                }

                case IF_RWR_RRD_ARD:
                {
                    assert(((id.idGCref() == GCT_BYREF) && ((ins is INS_add or INS_sub or INS_sub_hide) || insIsCMOV(ins)))
                        || ((id.idGCref() == GCT_GCREF) && insIsCMOV(ins)));
                    assert(id.idIsEvexNdContextSet());
                    emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
                    break;
                }

                case IF_ARW_RRD:
                case IF_ARW_RRW:
                case IF_ARW_CNS:
                case IF_ARW_SHF:
                {
                    if (id.idGCref() == GCT_BYREF)
                    {
                        assert(ins is INS_add or INS_sub or INS_sub_hide);
                    }
                    else
                    {
                        assert((id.idGCref() == GCT_GCREF) && (ins is INS_cmpxchg or INS_xchg));
                        emitGCregLiveUpd(id.idGCref(), ins == INS_cmpxchg ? REG_EAX : id.idReg1(), dst);
                    }
                    break;
                }

                default:
                {
                    assert(false, "unexpected GC ref instruction format");
                    break;
                }
            }
            assert(!instrIs3opImul(ins));
            assert(ins is not (INS_mulEAX or INS_imulEAX));
        }
        else if (!emitInsCanOnlyWriteSSE2OrAVXReg(id))
        {
            switch (id.idInsFmt())
            {
                case IF_RWR_ARD:
                case IF_RRW_ARD:
                case IF_RWR_ARD_CNS:
                case IF_RRW_ARD_CNS:
                case IF_RWR_ARD_RRD:
                case IF_RRW_ARD_RRD:
                case IF_RWR_RRD_ARD:
                case IF_RRW_RRD_ARD:
                case IF_RWR_RRD_ARD_CNS:
                case IF_RWR_RRD_ARD_RRD:
                {
                    emitGCregDeadUpd(id.idReg1(), dst);
                    break;
                }

                case IF_RWR_RWR_ARD:
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
