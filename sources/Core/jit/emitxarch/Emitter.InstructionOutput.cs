// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe nuint emitOutputInstr(insGroup ig, instrDesc id, byte** dp)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction output requires Windows AMD64.");
#else
        assert(_compiler is not null);
        assert(emitCurIG is not null);
#if DEBUG
        assert(emitIssuing);
        var dspOffs = _compiler.opts.dspGCtbls;
#else
        const bool dspOffs = false;
#endif
        var dst = *dp;
        var sz = INSTR_DESC_SIZE;
        var ins = id.idIns();
        var insFmt = id.idInsFmt();
        byte callInstrSize = 0;
        var convertedJmpToNop = false;
        var size = id.idOpSize();
        assert(ins != INS_imul || size >= EA_4BYTE);
        assert(!instrIs3opImul(ins) || size >= EA_4BYTE);

        VARSET_TP GCvars = [];
        regMaskTP gcrefRegs;
        regMaskTP byrefRegs;
        ulong code;
        uint regcode;
        int args;
        CnsVal cnsVal = default;
        byte* addr;

        switch (insFmt)
        {
            case IF_NONE:
            {
#if FEATURE_LOOP_ALIGN
                if (ins == INS_align)
                {
                    sz = DescriptorSizes.Align;
                    if (ig.endsWithAlignInstr())
                    {
                        dst = emitOutputAlign(ig, id, dst);
                    }
                    else
                    {
                        assert(id.idCodeSize() == 0);
                    }
                    break;
                }
#endif
                if (ins == INS_nop)
                {
                    dst = emitOutputNOP(dst, id.idCodeSize());
                    break;
                }
                if (ins == INS_data16)
                {
                    dst = emitOutputData16(dst);
                    sz = emitSizeOfInsDsc_NONE(id);
                    // TLS linker relaxation may replace this sequence with one that trashes RAX.
                    emitGCregDeadUpd(REG_RAX, dst);
                    break;
                }
                if (ins == INS_cdq)
                {
                    emitGCregDeadUpd(REG_EDX, dst);
                }
                assert(id.idGCref() == GCT_NONE);
                code = insCodeMR(ins);
                code = AddX86PrefixIfNeeded(id, code, EA_4BYTE);
                if ((ins is INS_cdq or INS_cwde) && TakesRexWPrefix(id))
                {
                    code = AddRexWPrefix(id, code);
                }
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                if ((code & 0xFF000000) != 0)
                {
                    dst += emitOutputWord(dst, (long)(code >> 16));
                    code &= 0xFFFF;
                    dst += emitOutputWord(dst, (long)code);
                }
                else if ((code & 0x00FF0000) != 0)
                {
                    dst += emitOutputByte(dst, (long)(code >> 16));
                    code &= 0xFFFF;
                    dst += emitOutputWord(dst, (long)code);
                }
                else if ((code & 0xFF00) != 0)
                {
                    dst += emitOutputWord(dst, (long)code);
                }
                else
                {
                    dst += emitOutputByte(dst, (long)code);
                }
                break;
            }

            case IF_CNS:
            {
                dst = emitOutputIV(dst, id);
                sz = emitSizeOfInsDsc_CNS(id);
                break;
            }

            case IF_LABEL:
            {
                var jmp = (instrDescJmp)id;
                assert(id.idGCref() == GCT_NONE);
                if (!jmp.idjIsRemovableJmpCandidate)
                {
                    assert(id.idIsBound());
                    dst = emitOutputLJ(ig, dst, id);
                }
                else if (jmp.idjIsAfterCallBeforeEpilog)
                {
                    assert(id.idCodeSize() == 1);
                    dst = emitOutputNOP(dst, 1);
                    convertedJmpToNop = true;
                }
                else
                {
                    assert(jmp.idjIsRemovableJmpCandidate);
                    assert(emitJmpInstHasNoCode(id));
                }
                sz = DescriptorSizes.Jump;
                break;
            }

            case IF_RWR_LABEL:
            case IF_SWR_LABEL:
            {
                assert(id.idGCref() == GCT_NONE);
                assert(id.idIsBound() || emitJmpInstHasNoCode(id));
                assert(!((instrDescJmp)id).idjIsRemovableJmpCandidate);
                dst = emitOutputLJ(ig, dst, id);
                sz = id.idInsFmt() == IF_SWR_LABEL ? DescriptorSizes.Label : DescriptorSizes.Jump;
                break;
            }

            case IF_METHOD:
            case IF_METHPTR:
            {
                args = emitGetInsCDinfo(id);
                if (id.idIsLargeCall())
                {
                    var idCall = (instrDescCGCA)id;
                    gcrefRegs = idCall.idcGcrefRegs;
                    byrefRegs = idCall.idcByrefRegs;
                    VarSetOps.Assign(_compiler, ref GCvars, idCall.idcGCvars);
                    sz = idCall.NativeLogicalSize;
                }
                else
                {
                    assert(!id.idIsLargeDsp());
                    assert(!id.idIsLargeCns());
                    gcrefRegs = new regMaskTP((regMask)emitDecodeCallGCregs(id));
                    byrefRegs = default;
                    VarSetOps.AssignNoCopy(_compiler, ref GCvars, VarSetOps.MakeEmpty(_compiler));
                    sz = INSTR_DESC_SIZE;
                }
                addr = (byte*)id.idAddr().iiaAddr;
                assert(addr != null);
                if (insFmt == IF_METHPTR)
                {
                    assert(ins is INS_call or INS_tail_i_jmp);
                    code = AddX86PrefixIfNeeded(id, insCodeMR(ins), size);
                    if (id.idIsDspReloc())
                    {
                        dst += emitOutputWord(dst, (long)(code | 0x0500));
                        dst += emitOutputLong(dst, 0);
                        emitRecordRelocation(dst - sizeof(int), addr, RELOC_DISP32);
                    }
                    else
                    {
                        noway_assert(!_compiler.opts.compReloc);
                        noway_assert(codeGen.genAddrRelocTypeHint((nuint)addr) != CorInfoReloc.RELATIVE32);
                        noway_assert(unchecked((int)(nint)addr) == (nint)addr);
                        dst += emitOutputWord(dst, (long)(code | 0x0400));
                        dst += emitOutputByte(dst, 0x25);
                        dst += emitOutputLong(dst, unchecked((int)(nint)addr));
                    }
                    goto DONE_CALL;
                }

                assert(insFmt == IF_METHOD);
                if (ins == INS_l_jmp)
                {
                    dst += emitOutputByte(dst, (long)insCode(ins));
                }
                else
                {
                    assert(ins == INS_call);
                    var callCode = (ulong)insCodeMI(ins);
                    if (_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && id.idIsTlsGD())
                    {
                        callCode = (callCode << 8) | 0x48;
                        dst += emitOutputWord(dst, (long)callCode);
                    }
                    else
                    {
                        dst += emitOutputByte(dst, (long)callCode);
                    }
                }
                assert(id.idIsDspReloc());
                dst += emitOutputLong(dst, 0);
                if (id.idIsDspReloc())
                {
                    emitRecordRelocation(dst - sizeof(int), addr, CorInfoReloc.RELATIVE32);
                }
                goto DONE_CALL;
            }

            case IF_RRD:
            case IF_RWR:
            case IF_RRW:
            {
                dst = emitOutputR(dst, id);
                sz = SMALL_IDSC_SIZE;
                break;
            }

            case IF_RRW_SHF:
            {
                code = AddX86PrefixIfNeeded(id, insCodeMR(ins), size);
                code = insEncodeMRreg(id, id.idReg1(), size, code);
                if (size != EA_1BYTE)
                {
                    code |= 1;
                }
                if (TakesRexWPrefix(id))
                {
                    code = AddRexWPrefix(id, code);
                }
                if (size == EA_2BYTE && !TakesEvexPrefix(id))
                {
                    dst += emitOutputByte(dst, 0x66);
                }
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                dst += emitOutputWord(dst, (long)code);
                dst += emitOutputByte(dst, emitGetInsSC(id));
                sz = emitSizeOfInsDsc_CNS(id);
                assert(id.idGCref() == GCT_NONE);
                emitGCregDeadUpd(id.idReg1(), dst);
                break;
            }

            case IF_RWR_RRD_SHF:
            {
                assert(IsApxExtendedEvexInstruction(ins));
                code = AddX86PrefixIfNeeded(id, insCodeMR(ins), size);
                code = insEncodeMRreg(id, id.idReg2(), size, code);
                code = insEncodeReg3456(id, id.idReg1(), size, code);
                if (size != EA_1BYTE)
                {
                    code |= 1;
                }
                if (TakesRexWPrefix(id))
                {
                    code = AddRexWPrefix(id, code);
                }
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                dst += emitOutputWord(dst, (long)code);
                dst += emitOutputByte(dst, emitGetInsSC(id));
                sz = emitSizeOfInsDsc_CNS(id);
                assert(id.idGCref() == GCT_NONE);
                emitGCregDeadUpd(id.idReg1(), dst);
                break;
            }

            case IF_RRD_RRD:
            case IF_RWR_RRD:
            case IF_RRW_RRD:
            case IF_RRW_RRW:
            {
                dst = emitOutputRR(dst, id);
                sz = SMALL_IDSC_SIZE;
                break;
            }

            case IF_RRD_CNS:
            case IF_RWR_CNS:
            case IF_RRW_CNS:
            {
                dst = emitOutputRI(dst, id);
                sz = emitSizeOfInsDsc_CNS(id);
                break;
            }

            case IF_RRD_RRD_RRD:
            case IF_RWR_RRD_RRD:
            case IF_RRW_RRD_RRD:
            case IF_RWR_RWR_RRD:
            {
                dst = emitOutputRRR(dst, id);
                sz = INSTR_DESC_SIZE;
                break;
            }

            case IF_RWR_RRD_RRD_CNS:
            {
                dst = emitOutputRRR(dst, id);
                dst += emitOutputByte(dst, emitGetInsSC(id));
                sz = emitSizeOfInsDsc_CNS(id);
                break;
            }

            case IF_RWR_RRD_RRD_RRD:
            {
                assert(IsSimdVexOrEvexEncodableInstruction(ins));
                var op4Reg = id.idReg4();
                if (isMaskReg(op4Reg))
                {
                    assert(IsAvx512OnlyInstruction(ins));
                    assert(EncodedBySSE38orSSE3A(ins));
                    dst = emitOutputRRR(dst, id);
                    sz = INSTR_DESC_SIZE;
                    break;
                }
                var val = (encodeRegAsIval(op4Reg) - XMMBASE) << 4;
                dst = emitOutputRRR(dst, id);
                dst += emitOutputByte(dst, val);
                sz = INSTR_DESC_SIZE;
                break;
            }

            case IF_RRD_RRD_CNS:
            case IF_RWR_RRD_CNS:
            case IF_RRW_RRD_CNS:
            {
                assert(id.idGCref() == GCT_NONE);
                regNumber mReg;
                regNumber rReg;
                if (IsApxExtendedEvexInstruction(ins))
                {
                    assert(hasCodeMI(ins));
                    code = AddX86PrefixIfNeeded(id, insCodeMI(ins), size);
                    code = insEncodeReg3456(id, id.idReg1(), size, code);
                    mReg = id.idReg2();
                    code = insEncodeMIreg(id, mReg, size, code);
                    var val = emitGetInsSC(id);
                    var valInByte = ImmCanUseSByteEncoding(ins, val);
                    switch (size)
                    {
                        case EA_1BYTE:
                        {
                            break;
                        }
                        case EA_2BYTE:
                        {
                            code |= EXTENDED_EVEX_PP_BITS;
                            goto case EA_4BYTE;
                        }
                        case EA_4BYTE:
                        {
                            code |= 1;
                            break;
                        }
                        case EA_8BYTE:
                        {
                            code = AddRexWPrefix(id, code);
                            code |= 1;
                            break;
                        }
                        default:
                        {
                            throw new FatalJitException("Unexpected APX immediate operand size.");
                        }
                    }
                    dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                    if (valInByte && size > EA_1BYTE)
                    {
                        code |= 2;
                        dst += emitOutputWord(dst, (long)code);
                        dst += emitOutputByte(dst, val);
                    }
                    else
                    {
                        dst += emitOutputWord(dst, (long)code);
                        switch (size)
                        {
                            case EA_1BYTE:
                            {
                                dst += emitOutputByte(dst, val);
                                break;
                            }
                            case EA_2BYTE:
                            {
                                dst += emitOutputWord(dst, val);
                                break;
                            }
                            case EA_4BYTE:
                            case EA_8BYTE:
                            {
                                dst += emitOutputLong(dst, val);
                                break;
                            }
                        }
                        if (id.idIsCnsReloc())
                        {
                            emitRecordRelocation(dst - sizeof(int), (void*)val, RELOC_DISP32);
                            assert(size == EA_4BYTE);
                        }
                    }
                    sz = emitSizeOfInsDsc_CNS(id);
                    if (!emitInsCanOnlyWriteSSE2OrAVXReg(id))
                    {
                        emitGCregDeadUpd(id.idReg1(), dst);
                    }
                    if (id.idInsFmt() == IF_RWR_RRD_CNS)
                    {
                        assert(!instrIs3opImul(ins));
                        emitGCregDeadUpd(id.idReg1(), dst);
                    }
                    else
                    {
#if DEBUG
                        emitDispIns(id, false, false, false);
#endif
                        throw new FatalJitException("Unexpected APX immediate instruction format.");
                    }
                    break;
                }
                else if (hasCodeMR(ins))
                {
                    code = AddX86PrefixIfNeeded(id, insCodeMR(ins), size);
                    if (size == EA_2BYTE && TakesRex2Prefix(id))
                    {
                        dst += emitOutputByte(dst, 0x66);
                    }
                    code = insEncodeMRreg(id, code);
                    mReg = id.idReg1();
                    rReg = id.idReg2();
                }
                else if (hasCodeMI(ins))
                {
                    code = AddX86PrefixIfNeeded(id, insCodeMI(ins), size);
                    assert((code & 0xC000) == 0);
                    code |= 0xC000;
                    mReg = id.idReg2();
                    rReg = getSseShiftRegNumber(ins);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, insCodeRM(ins), size);
                    code = insEncodeRMreg(id, code);
                    mReg = id.idReg2();
                    rReg = id.idReg1();
                }
                assert((code & 0x00FF0000) != 0);
                if (TakesRexWPrefix(id))
                {
                    code = AddRexWPrefix(id, code);
                }
                if (TakesSimdPrefix(id))
                {
                    if (IsDstDstSrcAVXInstruction(ins))
                    {
                        // The two-register form uses the destination as the first source.
                        code = insEncodeReg3456(id, id.idReg1(), size, code);
                    }
                    else if (IsDstSrcSrcAVXInstruction(ins))
                    {
                        code = insEncodeReg3456(id, id.idReg2(), size, code);
                    }
                }
                regcode = insEncodeReg345(id, rReg, size, &code) | insEncodeReg012(id, mReg, size, &code);
                dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                if ((code & 0xFF000000) != 0)
                {
                    dst += emitOutputWord(dst, (long)(code >> 16));
                    code &= 0xFFFF;
                    if (Is4ByteSSEInstruction(ins))
                    {
                        dst += emitOutputByte(dst, (long)code);
                        code &= 0xFF00;
                    }
                }
                else if ((code & 0x00FF0000) != 0)
                {
                    dst += emitOutputByte(dst, (long)(code >> 16));
                    code &= 0xFFFF;
                }
                if ((code & 0xFF00) == 0xC000)
                {
                    dst += emitOutputWord(dst, (long)(code | (regcode << 8)));
                }
                else if ((code & 0xFF) == 0)
                {
                    assert(IsSimdVexOrEvexEncodableInstruction(ins) || Is4ByteSSEInstruction(ins));
                    dst += emitOutputByte(dst, (long)((code >> 8) & 0xFF));
                    dst += emitOutputByte(dst, 0xC0 | regcode);
                }
                else
                {
                    dst += emitOutputWord(dst, (long)code);
                    dst += emitOutputByte(dst, 0xC0 | regcode);
                }
                dst += emitOutputByte(dst, emitGetInsSC(id));
                sz = emitSizeOfInsDsc_CNS(id);
                if (!emitInsCanOnlyWriteSSE2OrAVXReg(id))
                {
                    emitGCregDeadUpd(id.idReg1(), dst);
                }
                break;
            }

            case IF_ARD:
            case IF_AWR:
            case IF_ARW:
            {
                dst = emitCodeWithInstructionSize(dst, emitOutputAM(dst, id, insCodeMR(ins), null), &callInstrSize);
                if (ins == INS_call)
                {
                    goto IND_CALL;
                }
                sz = id.idInsFmt() == IF_ARD ? emitSizeOfInsDsc_SPEC(id) : emitSizeOfInsDsc_AMD(id);
                break;
            }

            case IF_RRD_ARD_CNS:
            case IF_RWR_ARD_CNS:
            case IF_RRW_ARD_CNS:
            {
                assert(IsSimdInstruction(ins));
                emitGetInsAmdCns(id, ref cnsVal);
                if (hasCodeMI(ins))
                {
                    assert(TakesEvexPrefix(id));
                    assert(!EncodedBySSE38orSSE3A(ins));
                    code = AddX86PrefixIfNeeded(id, insCodeMI(ins), size);
                    regcode = insEncodeReg345(id, getSseShiftRegNumber(ins), size, &code);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, insCodeRM(ins), size);
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code);
                }
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputAM(dst, id, code, &cnsVal);
                }
                else
                {
                    dst = emitOutputAM(dst, id, code | (regcode << 8), &cnsVal);
                }
                sz = emitSizeOfInsDsc_AMD(id);
                break;
            }

            case IF_ARD_RRD_CNS:
            case IF_AWR_RRD_CNS:
            case IF_ARW_RRD_CNS:
            {
                assert(IsSimdInstruction(ins));
                emitGetInsAmdCns(id, ref cnsVal);
                dst = emitOutputAM(dst, id, insCodeMR(ins), &cnsVal);
                sz = emitSizeOfInsDsc_AMD(id);
                break;
            }

            case IF_RRD_ARD:
            case IF_RWR_ARD:
            case IF_RRW_ARD:
            case IF_RRD_RRD_ARD:
            case IF_RWR_RRD_ARD:
            case IF_RRW_RRD_ARD:
            case IF_RWR_RWR_ARD:
            {
                code = insCodeRM(ins);
                if (IsApxNddCompatibleInstruction(ins) && id.idIsEvexNdContextSet())
                {
                    // Native APX output canonicalizes this format before encoding its NDD operands.
                    id.idInsFmt(IF_RWR_RRD_ARD);
                    code = AddX86PrefixIfNeeded(id, code, size);
                    code = insEncodeReg3456(id, id.idReg1(), size, code);
                    regcode = insEncodeReg345(id, id.idReg2(), size, &code) << 8;
                    dst = emitOutputAM(dst, id, code | regcode, null);
                    sz = emitSizeOfInsDsc_AMD(id);
                    break;
                }
                if (EncodedBySSE38orSSE3A(ins) || ins == INS_crc32)
                {
                    dst = emitOutputAM(dst, id, code, null);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, code, size);
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    dst = emitOutputAM(dst, id, code | regcode, null);
                }
                sz = emitSizeOfInsDsc_AMD(id);
                break;
            }

            case IF_RRD_ARD_RRD:
            case IF_RWR_ARD_RRD:
            case IF_RRW_ARD_RRD:
            {
                assert(IsAVX2GatherInstruction(ins));
                dst = emitOutputAM(dst, id, insCodeRM(ins), null);
                sz = emitSizeOfInsDsc_AMD(id);
                break;
            }

            case IF_RWR_RRD_ARD_CNS:
            case IF_RWR_RRD_ARD_RRD:
            {
                assert(IsSimdInstruction(ins));
                code = insCodeRM(ins);
                emitGetInsAmdCns(id, ref cnsVal);
                if (insFmt == IF_RWR_RRD_ARD_RRD)
                {
                    var op3Reg = decodeRegFromIval(cnsVal.cnsVal);
                    if (isMaskReg(op3Reg))
                    {
                        assert(IsAvx512OnlyInstruction(ins));
                        assert(EncodedBySSE38orSSE3A(ins));
                        dst = emitOutputAM(dst, id, code, null);
                        sz = emitSizeOfInsDsc_AMD(id);
                        break;
                    }
                    assert(isLowSimdReg(op3Reg));
                    cnsVal.cnsVal = unchecked((sbyte)((cnsVal.cnsVal - XMMBASE) << 4));
                }
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputAM(dst, id, code, &cnsVal);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, code, size);
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    dst = emitOutputAM(dst, id, code | regcode, &cnsVal);
                }
                sz = emitSizeOfInsDsc_AMD(id);
                break;
            }

            case IF_ARD_RRD:
            case IF_AWR_RRD:
            case IF_ARW_RRD:
            case IF_ARW_RRW:
            {
                code = insCodeMR(ins);
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputAM(dst, id, code, null);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, code, size);
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    dst = emitOutputAM(dst, id, code | regcode, null);
                }
                sz = emitSizeOfInsDsc_AMD(id);
                break;
            }

            case IF_AWR_RRD_RRD:
            {
                code = AddX86PrefixIfNeeded(id, insCodeMR(ins), size);
                dst = emitOutputAM(dst, id, code, null);
                sz = emitSizeOfInsDsc_AMD(id);
                break;
            }

            case IF_ARD_CNS:
            case IF_AWR_CNS:
            case IF_ARW_CNS:
            {
                emitGetInsAmdCns(id, ref cnsVal);
                dst = emitOutputAM(dst, id, insCodeMI(ins), &cnsVal);
                sz = emitSizeOfInsDsc_AMD(id);
                break;
            }

            case IF_ARW_SHF:
            {
                emitGetInsAmdCns(id, ref cnsVal);
                dst = emitOutputAM(dst, id, insCodeMR(ins), &cnsVal);
                sz = emitSizeOfInsDsc_AMD(id);
                break;
            }

            case IF_SRD:
            case IF_SWR:
            case IF_SRW:
            {
                assert(ins != INS_pop_hide);
                if (ins == INS_pop)
                {
                    dst = emitOutputSV(dst, id, insCodeMR(ins), null);
                    break;
                }
                dst = emitCodeWithInstructionSize(dst, emitOutputSV(dst, id, insCodeMR(ins), null), &callInstrSize);
                if (ins == INS_call)
                {
                    goto IND_CALL;
                }
                break;
            }

            case IF_SRD_CNS:
            case IF_SWR_CNS:
            case IF_SRW_CNS:
            {
                emitGetInsCns(id, ref cnsVal);
                dst = emitOutputSV(dst, id, insCodeMI(ins), &cnsVal);
                sz = emitSizeOfInsDsc_CNS(id);
                break;
            }

            case IF_SRW_SHF:
            {
                emitGetInsCns(id, ref cnsVal);
                dst = emitOutputSV(dst, id, insCodeMR(ins), &cnsVal);
                sz = emitSizeOfInsDsc_CNS(id);
                break;
            }

            case IF_SRD_RRD_CNS:
            case IF_SWR_RRD_CNS:
            case IF_SRW_RRD_CNS:
            {
                assert(IsSimdInstruction(ins) || ins is INS_shld or INS_shrd);
                emitGetInsAmdCns(id, ref cnsVal);
                dst = emitOutputSV(dst, id, insCodeMR(ins), &cnsVal);
                sz = emitSizeOfInsDsc_CNS(id);
                break;
            }

            case IF_RRD_SRD_CNS:
            case IF_RWR_SRD_CNS:
            case IF_RRW_SRD_CNS:
            {
                assert(IsSimdInstruction(ins));
                emitGetInsCns(id, ref cnsVal);
                if (hasCodeMI(ins))
                {
                    assert(TakesEvexPrefix(id));
                    assert(!EncodedBySSE38orSSE3A(ins));
                    code = AddX86PrefixIfNeeded(id, insCodeMI(ins), size);
                    regcode = insEncodeReg345(id, getSseShiftRegNumber(ins), size, &code);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, insCodeRM(ins), size);
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code);
                }
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputSV(dst, id, code, &cnsVal);
                }
                else
                {
                    if (IsDstDstSrcAVXInstruction(ins))
                    {
                        code = insEncodeReg3456(id, id.idReg1(), size, code);
                    }
                    dst = emitOutputSV(dst, id, code | (regcode << 8), &cnsVal);
                }
                sz = emitSizeOfInsDsc_CNS(id);
                break;
            }

            case IF_RRD_SRD:
            case IF_RWR_SRD:
            case IF_RRW_SRD:
            {
                code = insCodeRM(ins);
                if (EncodedBySSE38orSSE3A(ins) || ins == INS_crc32)
                {
                    dst = emitOutputSV(dst, id, code, null);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, code, size);
                    if (IsDstDstSrcAVXInstruction(ins))
                    {
                        code = insEncodeReg3456(id, id.idReg1(), size, code);
                    }
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    dst = emitOutputSV(dst, id, code | regcode, null);
                }
                sz = INSTR_DESC_SIZE;
                break;
            }

            case IF_RRD_RRD_SRD:
            case IF_RWR_RRD_SRD:
            case IF_RRW_RRD_SRD:
            case IF_RWR_RWR_SRD:
            {
                assert(IsSimdVexOrEvexEncodableInstruction(ins) || IsApxExtendedEvexInstruction(ins));
                if (IsApxNddEncodableInstruction(ins) && id.idIsEvexNdContextSet())
                {
                    code = AddX86PrefixIfNeeded(id, insCodeRM(ins), size);
                    code = insEncodeReg3456(id, id.idReg1(), size, code);
                    regcode = insEncodeReg345(id, id.idReg2(), size, &code) << 8;
                    dst = emitOutputSV(dst, id, code | regcode, null);
                    sz = INSTR_DESC_SIZE;
                    break;
                }
                code = AddX86PrefixIfNeeded(id, insCodeRM(ins), size);
                code = insEncodeReg3456(id, id.idReg2(), size, code);
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputSV(dst, id, code, null);
                }
                else
                {
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    dst = emitOutputSV(dst, id, code | regcode, null);
                }
                sz = INSTR_DESC_SIZE;
                break;
            }

            case IF_RWR_RRD_SRD_CNS:
            case IF_RWR_RRD_SRD_RRD:
            {
                assert(IsSimdVexOrEvexEncodableInstruction(ins));
                code = AddX86PrefixIfNeeded(id, insCodeRM(ins), size);
                code = insEncodeReg3456(id, id.idReg2(), size, code);
                emitGetInsCns(id, ref cnsVal);
                if (insFmt == IF_RWR_RRD_SRD_RRD)
                {
                    var op3Reg = decodeRegFromIval(cnsVal.cnsVal);
                    if (isMaskReg(op3Reg))
                    {
                        assert(IsAvx512OnlyInstruction(ins));
                        assert(EncodedBySSE38orSSE3A(ins));
                        dst = emitOutputSV(dst, id, code, null);
                        sz = emitSizeOfInsDsc_CNS(id);
                        break;
                    }
                    assert(isLowSimdReg(op3Reg));
                    cnsVal.cnsVal = unchecked((sbyte)((cnsVal.cnsVal - XMMBASE) << 4));
                }
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputSV(dst, id, code, &cnsVal);
                }
                else
                {
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    dst = emitOutputSV(dst, id, code | regcode, &cnsVal);
                }
                sz = emitSizeOfInsDsc_CNS(id);
                break;
            }

            case IF_SRD_RRD:
            case IF_SWR_RRD:
            case IF_SRW_RRD:
            case IF_SRW_RRW:
            {
                code = insCodeMR(ins);
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputSV(dst, id, code, null);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, code, size);
                    if (IsDstDstSrcAVXInstruction(ins))
                    {
                        code = insEncodeReg3456(id, id.idReg1(), size, code);
                    }
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    dst = emitOutputSV(dst, id, code | regcode, null);
                }
                sz = INSTR_DESC_SIZE;
                break;
            }

            case IF_RRD_SRD_RRD:
            case IF_RWR_SRD_RRD:
            case IF_RRW_SRD_RRD:
            {
                assert(IsAVX2GatherInstruction(ins));
                throw new FatalJitException("A gather cannot use a stack instruction format.");
            }

            case IF_SWR_RRD_RRD:
            {
                throw new FatalJitException("Unexpected three-register stack-store format.");
            }

            case IF_MRD:
            case IF_MRW:
            case IF_MWR:
            {
                noway_assert(ins != INS_call);
                dst = emitOutputCV(dst, id, insCodeMR(ins) | 0x0500, null);
                sz = id.idInsFmt() == IF_MRD ? emitSizeOfInsDsc_SPEC(id) : emitSizeOfInsDsc_DSP(id);
                break;
            }

            case IF_MRD_OFF:
            {
                dst = emitOutputCV(dst, id, insCodeMI(ins), null);
                sz = INSTR_DESC_SIZE;
                break;
            }

            case IF_RRD_MRD_CNS:
            case IF_RWR_MRD_CNS:
            case IF_RRW_MRD_CNS:
            {
                assert(IsSimdInstruction(ins));
                emitGetInsDcmCns(id, ref cnsVal);
                if (hasCodeMI(ins))
                {
                    assert(TakesEvexPrefix(id));
                    assert(!EncodedBySSE38orSSE3A(ins));
                    code = AddX86PrefixIfNeeded(id, insCodeMI(ins), size);
                    regcode = insEncodeReg345(id, getSseShiftRegNumber(ins), size, &code);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, insCodeRM(ins), size);
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code);
                }
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputCV(dst, id, code, &cnsVal);
                }
                else
                {
                    if (IsDstDstSrcAVXInstruction(ins))
                    {
                        code = insEncodeReg3456(id, id.idReg1(), size, code);
                    }
                    dst = emitOutputCV(dst, id, code | (regcode << 8) | 0x0500, &cnsVal);
                }
                sz = emitSizeOfInsDsc_DSP(id);
                break;
            }

            case IF_MRD_RRD_CNS:
            case IF_MWR_RRD_CNS:
            case IF_MRW_RRD_CNS:
            {
                assert(ins is INS_vextractf32x4 or INS_vextractf32x8 or INS_vextractf64x2 or INS_vextractf64x4
                    or INS_vextracti32x4 or INS_vextracti32x8 or INS_vextracti64x2 or INS_vextracti64x4 or INS_vcvtps2ph);
                assert(UseSimdEncoding());
                emitGetInsDcmCns(id, ref cnsVal);
                dst = emitOutputCV(dst, id, insCodeMR(ins), &cnsVal);
                sz = emitSizeOfInsDsc_DSP(id);
                break;
            }

            case IF_RRD_MRD:
            case IF_RWR_MRD:
            case IF_RRW_MRD:
            {
                code = insCodeRM(ins);
                if (EncodedBySSE38orSSE3A(ins) || ins == INS_crc32)
                {
                    dst = emitOutputCV(dst, id, code, null);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, code, size);
                    if (IsDstDstSrcAVXInstruction(ins))
                    {
                        code = insEncodeReg3456(id, id.idReg1(), size, code);
                    }
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    var fldh = id.idAddr().iiaFieldHnd;
                    dst = emitOutputCV(dst, id, code | regcode | (fldh == FLD_GLOBAL_GS ? 0x0400UL : 0x0500UL), null);
                }
                sz = emitSizeOfInsDsc_DSP(id);
                break;
            }

            case IF_RRD_RRD_MRD:
            case IF_RWR_RRD_MRD:
            case IF_RRW_RRD_MRD:
            case IF_RWR_RWR_MRD:
            {
                assert(IsSimdVexOrEvexEncodableInstruction(ins));
                code = AddX86PrefixIfNeeded(id, insCodeRM(ins), size);
                code = insEncodeReg3456(id, id.idReg2(), size, code);
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputCV(dst, id, code, null);
                }
                else
                {
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    dst = emitOutputCV(dst, id, code | regcode | 0x0500, null);
                }
                sz = emitSizeOfInsDsc_DSP(id);
                break;
            }

            case IF_RWR_RRD_MRD_CNS:
            case IF_RWR_RRD_MRD_RRD:
            {
                assert(IsSimdVexOrEvexEncodableInstruction(ins));
                code = AddX86PrefixIfNeeded(id, insCodeRM(ins), size);
                code = insEncodeReg3456(id, id.idReg2(), size, code);
                emitGetInsCns(id, ref cnsVal);
                if (insFmt == IF_RWR_RRD_MRD_RRD)
                {
                    var op3Reg = decodeRegFromIval(cnsVal.cnsVal);
                    if (isMaskReg(op3Reg))
                    {
                        assert(IsAvx512OnlyInstruction(ins));
                        assert(EncodedBySSE38orSSE3A(ins));
                        dst = emitOutputCV(dst, id, code, null);
                        sz = emitSizeOfInsDsc_DSP(id);
                        break;
                    }
                    assert(isLowSimdReg(op3Reg));
                    cnsVal.cnsVal = unchecked((sbyte)((cnsVal.cnsVal - XMMBASE) << 4));
                }
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputCV(dst, id, code, &cnsVal);
                }
                else
                {
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    dst = emitOutputCV(dst, id, code | regcode | 0x0500, &cnsVal);
                }
                sz = emitSizeOfInsDsc_DSP(id);
                break;
            }

            case IF_RWR_MRD_OFF:
            {
                code = AddX86PrefixIfNeeded(id, insCode(ins), size);
                if (IsDstDstSrcAVXInstruction(ins))
                {
                    code = insEncodeReg3456(id, id.idReg1(), size, code);
                }
                regcode = insEncodeReg012(id, id.idReg1(), size, &code);
                dst = emitOutputCV(dst, id, code | 0x30 | regcode, null);
                sz = emitSizeOfInsDsc_DSP(id);
                break;
            }

            case IF_MRD_RRD:
            case IF_MWR_RRD:
            case IF_MRW_RRD:
            case IF_MRW_RRW:
            {
                code = insCodeMR(ins);
                if (EncodedBySSE38orSSE3A(ins))
                {
                    dst = emitOutputCV(dst, id, code, null);
                }
                else
                {
                    code = AddX86PrefixIfNeeded(id, code, size);
                    if (IsDstDstSrcAVXInstruction(ins))
                    {
                        code = insEncodeReg3456(id, id.idReg1(), size, code);
                    }
                    regcode = insEncodeReg345(id, id.idReg1(), size, &code) << 8;
                    dst = emitOutputCV(dst, id, code | regcode | 0x0500, null);
                }
                sz = emitSizeOfInsDsc_DSP(id);
                break;
            }

            case IF_MRD_CNS:
            case IF_MWR_CNS:
            case IF_MRW_CNS:
            {
                emitGetInsDcmCns(id, ref cnsVal);
                dst = emitOutputCV(dst, id, insCodeMI(ins) | 0x0500, &cnsVal);
                sz = emitSizeOfInsDsc_DSP(id);
                break;
            }

            case IF_MRW_SHF:
            {
                emitGetInsDcmCns(id, ref cnsVal);
                dst = emitOutputCV(dst, id, insCodeMR(ins) | 0x0500, &cnsVal);
                sz = emitSizeOfInsDsc_DSP(id);
                break;
            }

            case IF_RRD_MRD_RRD:
            case IF_RWR_MRD_RRD:
            case IF_RRW_MRD_RRD:
            {
                assert(IsAVX2GatherInstruction(ins));
                throw new FatalJitException("A gather cannot use a static-memory instruction format.");
            }

            case IF_MWR_RRD_RRD:
            {
                throw new FatalJitException("Unexpected three-register static-store format.");
            }

            default:
            {
#if DEBUG
                jitprintf($"unexpected format {emitIfName(id.idInsFmt())}\n");
#endif
                throw new FatalJitException("Unknown instruction-output format.");
            }
        }
        goto DONE_OUTPUT;

    IND_CALL:
        args = unchecked((int)emitGetInsCIargs(id));
        if (id.idIsLargeCall())
        {
            var idCall = (instrDescCGCA)id;
            gcrefRegs = idCall.idcGcrefRegs;
            byrefRegs = idCall.idcByrefRegs;
            VarSetOps.Assign(_compiler, ref GCvars, idCall.idcGCvars);
            sz = idCall.NativeLogicalSize;
        }
        else
        {
            assert(!id.idIsLargeDsp());
            assert(!id.idIsLargeCns());
            gcrefRegs = new regMaskTP((regMask)emitDecodeCallGCregs(id));
            byrefRegs = default;
            VarSetOps.AssignNoCopy(_compiler, ref GCvars, VarSetOps.MakeEmpty(_compiler));
            sz = INSTR_DESC_SIZE;
        }

    DONE_CALL:
        var recCall = !id.idIsNoGC();
        assert((nuint)(dst - *dp) <= byte.MaxValue);
        callInstrSize = checked((byte)(dst - *dp));

        // Stack variables cannot be used by the call. Kill them at the call's start,
        // before return-register changes, including at non-returning THROW boundaries.
        emitUpdateLiveGCvars(GCvars, *dp);
#if DEBUG
        if (_compiler.verbose || _compiler.opts.disasmWithGC)
        {
            emitDispGCVarDelta();
        }
#endif
        if (id.idGCref() == GCT_GCREF)
        {
            gcrefRegs |= new regMaskTP(SRBM_EAX);
        }
        else if (id.idGCref() == GCT_BYREF)
        {
            byrefRegs |= new regMaskTP(SRBM_EAX);
        }
        if (id.idIsLargeCall() && ((instrDescCGCA)id).hasAsyncContinuationRet())
        {
            gcrefRegs |= new regMaskTP(REG_ASYNC_CONTINUATION_RET.SingleTypeMask);
        }
        if (gcrefRegs != new regMaskTP(emitThisGCrefRegs))
        {
            emitUpdateLiveGCregs(GCT_GCREF, gcrefRegs, dst);
        }
        if (byrefRegs != new regMaskTP(emitThisByrefRegs))
        {
            emitUpdateLiveGCregs(GCT_BYREF, byrefRegs, dst);
        }
        if (recCall || args != 0)
        {
            assert(callInstrSize != 0);
            if (args >= 0)
            {
                emitStackPop(dst, true, callInstrSize, (uint)args);
            }
            else
            {
                emitStackKillArgs(dst, unchecked((uint)-args), callInstrSize);
            }
        }
        if (!emitFullGCinfo && recCall)
        {
            assert(callInstrSize != 0);
            emitRecordGCcall(dst, callInstrSize);
        }
#if DEBUG
        if (ins == INS_call && !id.idIsTlsGD())
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            var callSig = debugInfo.idCallSig?.Value ?? default;
            emitRecordCallSite(emitCurCodeOffs(*dp), debugInfo.idCallSig is null ? null : &callSig,
                (CORINFO_METHOD_HANDLE)debugInfo.idMemCookie);
        }
#endif

    DONE_OUTPUT:
        assert((ins == INS_jmp && id.idIns() == INS_nop) || sz == emitSizeOfInsDsc(id));
        assert(emitCurStackLvl >= 0);
        assert(*dp != dst || emitInstHasNoCode(id));
#if DEBUG
        var display = _compiler.opts.disAsm || _compiler.verbose;
#else
        var display = _compiler.opts.disAsm;
#endif
        if (display && (!emitJmpInstHasNoCode(id) || convertedJmpToNop))
        {
            if (convertedJmpToNop)
            {
                id.idIns(INS_nop);
                id.idInsFmt(IF_NONE);
                try
                {
                    emitDispIns(id, false, dspOffs, true, emitCurCodeOffs(*dp), *dp, (nuint)(dst - *dp), ig);
                }
                finally
                {
                    id.idIns(ins);
                    id.idInsFmt(insFmt);
                }
            }
            else
            {
                emitDispIns(id, false, dspOffs, true, emitCurCodeOffs(*dp), *dp, (nuint)(dst - *dp), ig);
            }
        }
#if FEATURE_LOOP_ALIGN
        if (emitLastAlignedIg is not null && (emitCurIG == emitLastAlignedIg || emitCurIG.IsBefore(emitLastAlignedIg)))
        {
            var diff = (int)id.idCodeSize() - checked((int)(dst - *dp));
            assert(diff >= 0);
            if (diff != 0)
            {
#if DEBUG
                assert(id.idIns() != INS_align);
                JITDUMP($"Added over-estimation compensation: {diff}\n");
                if (_compiler.opts.disAsm)
                {
                    emitDispInsAddr(dst);
                    jitprintf($"\t\t  ;; NOP compensation instructions of {diff} bytes.\n");
                }
#endif
                dst = emitOutputNOP(dst, (nuint)diff);
            }
            assert(id.idCodeSize() == (uint)(dst - *dp));
        }
#endif
#if DEBUG
        if (_compiler.compDebugBreak)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            if (JitConfig.JitEmitPrintRefRegs != 0)
            {
                jitprintf($"Before emitOutputInstr for id->idDebugOnlyInfo()->idNum=0x{debugInfo.idNum:x2}\n");
                fixed (regMask* gcRegs = &emitThisGCrefRegs)
                {
                    jitprintf($"  emitThisGCrefRegs(0x{FMT_PTR((void*)dspPtr(gcRegs))})=");
                    printRegMaskInt(new regMaskTP(emitThisGCrefRegs));
                    emitDispRegSet(new regMaskTP(emitThisGCrefRegs));
                    jitprintf("\n");
                }
                fixed (regMask* byrefs = &emitThisByrefRegs)
                {
                    jitprintf($"  emitThisByrefRegs(0x{FMT_PTR((void*)dspPtr(byrefs))})=");
                    printRegMaskInt(new regMaskTP(emitThisByrefRegs));
                    emitDispRegSet(new regMaskTP(emitThisByrefRegs));
                    jitprintf("\n");
                }
            }
            if (unchecked((uint)JitConfig.JitBreakEmitOutputInstr) == debugInfo.idNum)
            {
                assert(false, "JitBreakEmitOutputInstr reached");
            }
        }
#endif
        *dp = dst;
#if DEBUG
        if (ins is INS_mulEAX or INS_imulEAX)
        {
            assert(((SRBM_EAX | SRBM_EDX) & (emitThisGCrefRegs | emitThisByrefRegs)) == SRBM_NONE);
        }
        if (instrIs3opImul(ins))
        {
            var mask = inst3opImulReg(ins).SingleTypeMask;
            assert((mask & (emitThisGCrefRegs | emitThisByrefRegs)) == SRBM_NONE);
        }
        if (_compiler.verbose || _compiler.opts.disasmWithGC)
        {
            emitDispGCInfoDelta();
        }
#endif

        return (nuint)sz;
#endif
    }
}
