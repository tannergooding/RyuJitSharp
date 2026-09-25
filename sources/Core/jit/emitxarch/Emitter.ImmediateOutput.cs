// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private const CorInfoReloc RELOC_DISP32 = CorInfoReloc.RELATIVE32;
#else
    private const CorInfoReloc RELOC_DISP32 = CorInfoReloc.DIRECT;
#endif

    public unsafe byte* emitOutputRI(byte* dst, instrDesc id)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register-immediate output requires AMD64.");
#else
        var size = id.idOpSize();
        var ins = id.idIns();
        var reg = id.idReg1();
        var val = emitGetInsCns(id);
        var valInByte = ImmCanUseSByteEncoding(ins, val);
        assert(!id.idHasReg2());
        assert(ins != INS_bt);
        if (id.idIsCnsReloc())
        {
            valInByte = false;
        }
        noway_assert(emitVerifyEncodable(ins, size, reg));

        ulong code;
        if (IsSimdInstruction(ins))
        {
            assert(id.idGCref() == GCT_NONE);
            assert(valInByte);
            var regOpcode = getSseShiftRegNumber(ins);
            code = insEncodeMIreg(id, reg, size, AddX86PrefixIfNeeded(id, (ulong)insCodeMI(ins), size));
            assert((code & 0x00FF0000) != 0);
            if (TakesSimdPrefix(id))
            {
                code = insEncodeReg3456(id, reg, size, code);
            }
            var regcode = (insEncodeReg345(id, regOpcode, size, &code) |
                insEncodeReg012(id, reg, size, &code)) << 8;

            dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
            if ((code & 0xFF000000) != 0)
            {
                dst += emitOutputWord(dst, unchecked((long)(code >> 16)));
            }
            else if ((code & 0xFF0000) != 0)
            {
                dst += emitOutputByte(dst, unchecked((long)(code >> 16)));
            }
            dst += emitOutputWord(dst, unchecked((long)(code | regcode)));
            dst += emitOutputByte(dst, (long)val);
            return dst;
        }

        if (ins == INS_mov)
        {
            code = (ulong)insCodeACC(ins);
            assert(code < 0x100);
            assert(!TakesVexPrefix(ins));
            code = AddX86PrefixIfNeededAndNotPresent(id, code, size) | 8;
            var regcode = insEncodeReg012(id, reg, size, &code);
            code |= regcode;
            if (TakesRexWPrefix(id))
            {
                code = AddRexWPrefix(id, code);
            }
            dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
            dst += emitOutputByte(dst, unchecked((long)code));
            if (size == EA_4BYTE)
            {
                dst += emitOutputLong(dst, (long)val);
            }
            else
            {
                assert(size == EA_PTRSIZE);
                dst += emitOutputSizeT(dst, (long)val);
            }
            if (id.idIsCnsReloc())
            {
                assert(_compiler is not null);
                if (_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI) && id.idAddr().iiaSecRel)
                {
                    emitRecordRelocation(dst - EA_SIZE_IN_BYTES(size), (void*)val, CorInfoReloc.AMD64_WIN_SECREL);
                }
                else
                {
                    emitRecordRelocation(dst - EA_SIZE_IN_BYTES(size), (void*)val, CorInfoReloc.DIRECT);
                }
            }
        }
        else
        {
            bool useSigned;
            bool useACC;
            if (reg == REG_EAX && !instrIs3opImul(ins))
            {
                if (size == EA_1BYTE || ins == INS_test)
                {
                    useSigned = false;
                    useACC = true;
                }
                else
                {
                    useSigned = valInByte;
                    useACC = !valInByte;
                }
                if (TakesEvexPrefix(id))
                {
                    useACC = false;
                }
            }
            else
            {
                useACC = false;
                useSigned = valInByte;
            }
            if (!HasRegularWideImmediateForm(ins))
            {
                useSigned = false;
            }

            if (useACC)
            {
                assert(!useSigned);
                code = (ulong)insCodeACC(ins);
                if (ins != INS_test)
                {
                    code = AddX86PrefixIfNeeded(id, code, size);
                }
            }
            else
            {
                assert(!useSigned || valInByte);
                if (valInByte && useSigned && insNeedsRRIb(ins))
                {
                    code = insEncodeRRIb(id, reg, size);
                }
                else
                {
                    code = insEncodeMIreg(id, reg, size,
                        AddX86PrefixIfNeeded(id, (ulong)insCodeMI(ins), size));
                    if (ins >= INS_imul_08 && ins <= INS_imul_31)
                    {
                        _ = insEncodeReg345(id, inst3opImulReg(ins), size, &code);
                    }
                }
            }

            switch (size)
            {
                case EA_1BYTE:
                {
                    break;
                }

                case EA_2BYTE:
                {
                    if (!TakesEvexPrefix(id))
                    {
                        dst += emitOutputByte(dst, 0x66);
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
                    code = AddRexWPrefix(id, code) | 1;
                    break;
                }

                default:
                {
                    assert(false, "unexpected size");
                    break;
                }
            }

            dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
            if (useSigned && size > EA_1BYTE)
            {
                code |= 2;
                dst += emitOutputWord(dst, unchecked((long)code));
                dst += emitOutputByte(dst, (long)val);
            }
            else
            {
                dst += useACC ? emitOutputByte(dst, unchecked((long)code))
                    : emitOutputWord(dst, unchecked((long)code));
                switch (size)
                {
                    case EA_1BYTE:
                    {
                        dst += emitOutputByte(dst, (long)val);
                        break;
                    }

                    case EA_2BYTE:
                    {
                        dst += emitOutputWord(dst, (long)val);
                        break;
                    }

                    case EA_4BYTE:
                    case EA_8BYTE:
                    {
                        dst += emitOutputLong(dst, (long)val);
                        break;
                    }
                }
                if (id.idIsCnsReloc())
                {
                    emitRecordRelocation(dst - sizeof(int), (void*)val, RELOC_DISP32);
                    assert(size == EA_4BYTE);
                }
            }
        }

        if (id.idGCref() != GCT_NONE)
        {
            switch (id.idInsFmt())
            {
                case IF_RRD_CNS:
                {
                    break;
                }

                case IF_RWR_CNS:
                {
                    emitGCregLiveUpd(id.idGCref(), reg, dst);
                    break;
                }

                case IF_RRW_CNS:
                {
                    assert(id.idGCref() == GCT_BYREF);
#if DEBUG
                    var regMask = reg.SingleTypeMask;
                    if ((emitThisGCrefRegs & regMask) != SRBM_NONE)
                    {
                        assert(ins == INS_add);
                    }
                    if ((emitThisByrefRegs & regMask) != SRBM_NONE)
                    {
                        assert(ins is INS_add or INS_sub or INS_sub_hide);
                    }
#endif
                    emitGCregLiveUpd(GCT_BYREF, reg, dst);
                    break;
                }

                default:
                {
#if DEBUG
                    emitDispIns(id, false, false, false);
#endif
                    assert(false, "unexpected GC ref instruction format");
                    break;
                }
            }
            assert(!instrIs3opImul(ins));
            assert(ins is not (INS_mulEAX or INS_imulEAX));
        }
        else
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

                case IF_RRW_CNS:
                case IF_RWR_CNS:
                {
                    assert(!instrIs3opImul(ins));
                    emitGCregDeadUpd(reg, dst);
                    break;
                }

                default:
                {
#if DEBUG
                    emitDispIns(id, false, false, false);
#endif
                    assert(false, "unexpected GC ref instruction format");
                    break;
                }
            }
        }
        return dst;
#endif
    }

    public unsafe byte* emitOutputIV(byte* dst, instrDesc id)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Immediate-only output requires AMD64.");
#else
        var ins = id.idIns();
        var size = id.idOpSize();
        var val = emitGetInsCns(id);
        var valInByte = unchecked((sbyte)val) == val;
        assert(!IsSSEInstruction(ins));
        assert(!IsSimdVexOrEvexEncodableInstruction(ins));
        noway_assert(size < EA_8BYTE || (unchecked((int)val) == val && !id.idIsCnsReloc()));
        if (id.idIsCnsReloc())
        {
            valInByte = false;
            assert(ins is INS_push or INS_push_hide);
        }

        ulong code;
        switch (ins)
        {
            case INS_jge:
            {
                assert(val >= -128 && val <= 127);
                dst += emitOutputByte(dst, (long)insCode(ins));
                dst += emitOutputByte(dst, (long)val);
                break;
            }

            case INS_loop:
            {
                assert(val >= -128 && val <= 127);
                dst += emitOutputByte(dst, (long)insCodeMI(ins));
                dst += emitOutputByte(dst, (long)val);
                break;
            }

            case INS_ret:
            {
                assert(val != 0);
                dst += emitOutputByte(dst, (long)insCodeMI(ins));
                dst += emitOutputWord(dst, (long)val);
                break;
            }

            case INS_push:
            case INS_push_hide:
            {
                code = (ulong)insCodeMI(ins);
                if (TakesRex2Prefix(id))
                {
                    code = AddRex2Prefix(ins, code);
                }
                if (valInByte)
                {
                    if (TakesRex2Prefix(id))
                    {
                        dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                    }
                    dst += emitOutputByte(dst, unchecked((long)(code | 2)));
                    dst += emitOutputByte(dst, (long)val);
                }
                else
                {
                    if (TakesRexWPrefix(id) || TakesRex2Prefix(id))
                    {
                        code = AddRexWPrefix(id, code);
                        dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
                    }
                    dst += emitOutputByte(dst, unchecked((long)code));
                    dst += emitOutputLong(dst, (long)val);
                    if (id.idIsCnsReloc())
                    {
                        emitRecordRelocation(dst - sizeof(int), (void*)val, RELOC_DISP32);
                    }
                }
                break;
            }

            default:
            {
                assert(false, "unexpected instruction");
                break;
            }
        }
        assert(ins == INS_push || id.idGCref() == GCT_NONE);
        return dst;
#endif
    }
}
