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
    public unsafe byte* emitOutputCV(byte* dst, instrDesc id, ulong code, CnsVal* addc)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Static-variable byte output requires Windows AMD64.");
#else
        assert(id.idHasMemGen());

        var size = id.idOpSize();
        var opsz = EA_SIZE_IN_BYTES(size);
        var ins = id.idIns();
        var fldh = id.idAddr().iiaFieldHnd;
        var offs = emitGetInsDsp(id);

        if (fldh == FLD_GLOBAL_FS)
        {
            dst += emitOutputByte(dst, 0x64);
        }
        else if (fldh == FLD_GLOBAL_GS)
        {
            dst += emitOutputByte(dst, 0x65);
        }

        code = AddX86PrefixIfNeededAndNotPresent(id, code, size);
        if (TakesRexWPrefix(id))
        {
            code = AddRexWPrefix(id, code);
        }

        if (addc is not null && size > EA_1BYTE)
        {
            var cval = addc->cnsVal;
            if (ImmCanUseSByteEncoding(ins, cval) && !addc->cnsReloc)
            {
                if (id.idInsFmt() != IF_MRW_SHF && !IsSimdInstruction(ins))
                {
                    code |= 2;
                }
                opsz = 1;
            }
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
            if (UseVexEncodings && ins != INS_crc32)
            {
                assert((code & 0xFF) == 0);
                dst += emitOutputByte(dst, unchecked((long)(code >> 8)));
            }
            else
            {
                dst += emitOutputWord(dst, unchecked((long)(code >> 16)));
                dst += emitOutputWord(dst, unchecked((long)code));
            }

            dst += emitOutputByte(dst, regcode | 0x05);
            code = 0;
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
        else
        {
            switch (size)
            {
                case EA_1BYTE:
                {
                    break;
                }

                case EA_2BYTE:
                {
                    dst += emitOutputByte(dst, 0x66);
                    code |= 1;
                    break;
                }

                case EA_4BYTE:
                case EA_8BYTE:
                {
                    code |= 1;
                    break;
                }

                default:
                {
                    throw new FatalJitException("Unexpected static operand size.");
                }
            }
        }

        dst += emitOutputRexOrSimdPrefixIfNeeded(ins, dst, ref code);
        if (code != 0)
        {
            if (id.idInsFmt() is IF_MRD_OFF or IF_RWR_MRD_OFF)
            {
                dst += emitOutputByte(dst, unchecked((long)code));
            }
            else
            {
                dst += emitOutputWord(dst, unchecked((long)code));
            }
        }

        if (fldh == FLD_GLOBAL_GS)
        {
            dst += emitOutputByte(dst, 0x25);
        }

        var dataOffset = Compiler.eeGetJitDataOffs(fldh);
        byte* addr;
        if (dataOffset >= 0)
        {
            addr = emitDataOffsetToPtr((uint)dataOffset);

#if DEBUG
            if (emitChkAlign && ins != INS_lea)
            {
                var byteSize = EA_SIZE_IN_BYTES(emitGetMemOpSize(id, false));
                var compiler = _compiler ?? throw new FatalJitException("An initialized compiler is required for static output.");
                var alignment = compiler.compCodeOpt == Compiler.SMALL_CODE ? 4 : byteSize;
                assert(((nuint)addr % (uint)alignment) == 0);
            }
#endif
        }
        else
        {
            assert(jitStaticFldIsGlobAddr(fldh));
            addr = null;
        }

        var target = unchecked(addr + (long)offs);
        var addlDelta = 0;
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
                    throw new FatalJitException("Unexpected static immediate size.");
                }
            }
        }

        if (id.idIsDspReloc())
        {
            dst += emitOutputLong(dst, 0);
        }
        else
        {
            dst += emitOutputLong(dst, (long)(nint)target);
        }

        if (id.idIsDspReloc())
        {
            emitRecordRelocation(dst - sizeof(int), target, RELOC_DISP32, addlDelta);
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
                    throw new FatalJitException("Unexpected static immediate size.");
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
            switch (id.idInsFmt())
            {
                case IF_MRD:
                case IF_MRW:
                case IF_MWR:
                case IF_RRD_MRD:
                case IF_MRD_RRD:
                case IF_MWR_RRD:
                case IF_MRW_RRD:
                case IF_MRW_RRW:
                case IF_MRD_CNS:
                case IF_MWR_CNS:
                case IF_MRW_CNS:
                case IF_MRW_SHF:
                {
                    break;
                }

                case IF_RWR_MRD:
                {
                    emitGCregLiveUpd(id.idGCref(), id.idReg1(), dst);
                    break;
                }

                case IF_RRW_MRD:
                {
                    assert(id.idGCref() == GCT_BYREF);
                    assert(ins is INS_add or INS_sub or INS_sub_hide);
                    emitGCregLiveUpd(GCT_BYREF, id.idReg1(), dst);
                    break;
                }

                default:
                {
#if DEBUG
                    emitDispIns(id, false, false, false);
#endif
                    throw new FatalJitException(
                        $"Unexpected GC reference static instruction format {id.idInsFmt()} for {ins}.");
                }
            }
        }
        else if (!emitInsCanOnlyWriteSSE2OrAVXReg(id))
        {
            switch (id.idInsFmt())
            {
                case IF_RWR_MRD:
                case IF_RRW_MRD:
                case IF_RWR_RRD_MRD:
                case IF_RRW_RRD_MRD:
                {
                    emitGCregDeadUpd(id.idReg1(), dst);
                    break;
                }

                case IF_RWR_RWR_MRD:
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
