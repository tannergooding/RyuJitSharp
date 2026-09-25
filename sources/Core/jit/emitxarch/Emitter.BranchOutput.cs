// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private unsafe byte* emitOffsetToPtr(uint offset)
    {
        if (offset < emitTotalHotCodeSize)
        {
            return emitCodeBlock + offset;
        }

        assert(offset < (emitTotalHotCodeSize + emitTotalColdCodeSize));
        return emitColdCodeBlock + (offset - emitTotalHotCodeSize);
    }

    private bool emitJumpCrossHotColdBoundary(uint srcOffset, uint dstOffset)
    {
        if (emitTotalColdCodeSize == 0)
        {
            return false;
        }

        assert(srcOffset < (emitTotalHotCodeSize + emitTotalColdCodeSize));
        assert(dstOffset < (emitTotalHotCodeSize + emitTotalColdCodeSize));
        return (srcOffset < emitTotalHotCodeSize) != (dstOffset < emitTotalHotCodeSize);
    }
#endif

    public unsafe byte* emitOutputLJ(insGroup ig, byte* dst, instrDesc i)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Label output requires Windows AMD64.");
#else
        var id = i as instrDescJmp ?? throw new FatalJitException("Label output requires a jump descriptor.");
        var compiler = _compiler ?? throw new FatalJitException("Label output requires an active compiler.");
        var ins = id.idIns();
        var relAddr = true;
        bool jmp;
        long ssz;
        long lsz;

        assert(!IsSSEInstruction(ins));
        assert(!IsSimdVexOrEvexEncodableInstruction(ins));

        switch (ins)
        {
            case INS_jmp:
            {
                ssz = JMP_SIZE_SMALL;
                lsz = JMP_SIZE_LARGE;
                jmp = true;
                break;
            }

            case INS_call:
            {
                ssz = lsz = CALL_INST_SIZE;
                jmp = false;
                break;
            }

            case INS_push:
            case INS_push_hide:
            {
                ssz = lsz = 5;
                jmp = false;
                relAddr = false;
                break;
            }

            case INS_mov:
            case INS_lea:
            {
                ssz = lsz = id.idCodeSize();
                jmp = false;
                relAddr = false;
                break;
            }

            default:
            {
                ssz = JCC_SIZE_SMALL;
                lsz = JCC_SIZE_LARGE;
                jmp = true;
                break;
            }
        }

        var srcOffs = emitCurCodeOffs(dst);
        var srcAddr = emitOffsetToPtr(srcOffs);
        var targetIG = id.idjTargetIG
            ?? throw new FatalJitException("A label instruction requires a bound target group.");
        var dstOffs = targetIG.igOffs;
        var dstAddr = emitOffsetToPtr(dstOffs);
        if (!relAddr)
        {
            srcAddr = null;
        }

        var distVal = dstAddr - srcAddr;
        if (dstOffs <= srcOffs)
        {
            if (jmp && distVal - ssz >= JMP_DIST_SMALL_MAX_NEG)
            {
                emitSetShortJump(id);
            }
        }
        else
        {
            emitFwdJumps = true;
            if (!emitJumpCrossHotColdBoundary(srcOffs, dstOffs))
            {
                dstOffs = unchecked((uint)(dstOffs - emitOffsAdj));
                distVal -= emitOffsAdj;
            }

            id.idjOffs = dstOffs;
            if (id.idjOffs != dstOffs)
            {
                IMPL_LIMITATION("Method is too large");
            }

            if (jmp && distVal - ssz <= JMP_DIST_SMALL_MAX_POS)
            {
                emitSetShortJump(id);
            }
        }

        if (relAddr)
        {
            distVal -= id.idjShort ? ssz : lsz;
        }

        if (id.idjShort)
        {
            assert(!id.idjKeepLong);
            assert(!emitJumpCrossHotColdBoundary(srcOffs, dstOffs));
#pragma warning disable CA1508 // Retain native instruction-width and opcode-layout assertions.
            assert(JMP_SIZE_SMALL == JCC_SIZE_SMALL);
            assert(JMP_SIZE_SMALL == 2);
#pragma warning restore CA1508
            assert(jmp);

#if DEBUG
            var debugInfo = id.idDebugOnlyInfo();
            if (id.idCodeSize() != JMP_SIZE_SMALL &&
                (compiler.verbose || debugInfo?.idNum == unchecked((uint)INTERESTING_JUMP_NUM) ||
                 INTERESTING_JUMP_NUM == 0))
            {
                jitprintf($"; NOTE: size of jump [{id.StorageOffset:X}] mis-predicted by " +
                    $"{id.idCodeSize() - JMP_SIZE_SMALL} bytes\n");
            }
#endif
            dst += emitOutputByte(dst, unchecked((long)insCode(ins)));
            id.idjAddr = distVal > 0 ? dst : null;
            dst += emitOutputByte(dst, distVal);
        }
        else
        {
            ulong code;
            if (jmp)
            {
                var delta = (int)INS_l_jmp - (int)INS_jmp;
#pragma warning disable CA1508 // Retain native instruction-width and opcode-layout assertions.
                assert((int)INS_jo + delta == (int)INS_l_jo);
                assert((int)INS_jb + delta == (int)INS_l_jb);
                assert((int)INS_jae + delta == (int)INS_l_jae);
                assert((int)INS_je + delta == (int)INS_l_je);
                assert((int)INS_jne + delta == (int)INS_l_jne);
                assert((int)INS_jbe + delta == (int)INS_l_jbe);
                assert((int)INS_ja + delta == (int)INS_l_ja);
                assert((int)INS_js + delta == (int)INS_l_js);
                assert((int)INS_jns + delta == (int)INS_l_jns);
                assert((int)INS_jp + delta == (int)INS_l_jp);
                assert((int)INS_jnp + delta == (int)INS_l_jnp);
                assert((int)INS_jl + delta == (int)INS_l_jl);
                assert((int)INS_jge + delta == (int)INS_l_jge);
                assert((int)INS_jle + delta == (int)INS_l_jle);
                assert((int)INS_jg + delta == (int)INS_l_jg);
#pragma warning restore CA1508

                code = (ulong)insCode((instruction)((int)ins + delta));
            }
            else if (ins is INS_push or INS_push_hide)
            {
                assert(insCodeMI(INS_push) == 0x68);
                code = 0x68;
            }
            else if (ins == INS_mov)
            {
                var oldFormat = id.idInsFmt();
                var oldReloc = id.idIsDspReloc();
                try
                {
                    // The target is held in idjTargetIG, leaving the address union for the destination local.
                    id.idInsFmt(emitInsModeFormat(ins, IF_SRD_CNS));
                    id.idSetIsDspReloc(false);
                    dst = emitOutputSV(dst, id, (ulong)insCodeMI(ins), null);
                }
                finally
                {
                    id.idInsFmt(oldFormat);
                    id.idSetIsDspReloc(oldReloc);
                }
                code = 0xCC;
            }
            else if (ins == INS_lea)
            {
                var idAmd = new instrDescAmd();
                idAmd.idIns(ins);
                idAmd.idReg1(id.idReg1());
                idAmd.idOpSize(id.idOpSize());
                idAmd.idGCref(id.idGCref());
                idAmd.idDebugOnlyInfo(id.idDebugOnlyInfo());
                idAmd.idInsFmt(emitInsModeFormat(ins, IF_RRD_ARD));
                idAmd.idAddr().iiaAddrMode.amBaseReg = REG_NA;
                idAmd.idAddr().iiaAddrMode.amIndxReg = REG_NA;
                emitSetAmdDisp(idAmd, unchecked((nint)distVal));
                idAmd.idSetIsDspReloc(id.idIsDspReloc());
                assert(emitGetInsAmdAny(idAmd) == distVal);
                idAmd.idCodeSize(emitInsSizeAM(idAmd, insCodeRM(ins)));

                code = AddX86PrefixIfNeeded(id, (ulong)insCodeRM(ins), id.idOpSize());
                var regcode = insEncodeReg345(id, id.idReg1(), EA_PTRSIZE, &code);
                code |= (ulong)regcode << 8;
                dst = emitOutputAM(dst, idAmd, code, null);
                id.idjAddr = dstOffs > srcOffs ? unchecked(dst - sizeof(int)) : null;
                return dst;
            }
            else
            {
                code = 0xE8;
            }

            if (ins != INS_mov)
            {
                dst += emitOutputByte(dst, unchecked((long)code));
                if ((code & 0xFF00) != 0)
                {
                    dst += emitOutputByte(dst, unchecked((long)(code >> 8)));
                }
            }

            id.idjAddr = dstOffs > srcOffs ? dst : null;
            var crossJump = emitJumpCrossHotColdBoundary(srcOffs, dstOffs);
            int encodedDisplacement;
            if (compiler.opts.compReloc && (!relAddr || crossJump))
            {
                encodedDisplacement = 0;
            }
            else
            {
                assert(distVal >= int.MinValue && distVal <= int.MaxValue);
                encodedDisplacement = unchecked((int)distVal);
            }
            dst += emitOutputLong(dst, encodedDisplacement);

            if (compiler.opts.compReloc)
            {
                if (!relAddr)
                {
                    emitRecordRelocation(unchecked(dst - sizeof(int)), (void*)distVal, RELOC_DISP32);
                }
                else if (crossJump)
                {
                    assert(id.idjKeepLong);
                    emitRecordRelocation(unchecked(dst - sizeof(int)), unchecked(dst + distVal),
                        CorInfoReloc.RELATIVE32);
                }
            }
        }

        if (ins == INS_call && (emitThisGCrefRegs | emitThisByrefRegs) != SRBM_NONE)
        {
            emitGCregDeadUpdMask(new regMaskTP(emitThisGCrefRegs | emitThisByrefRegs), dst);
        }
        return dst;
#endif
    }

    private unsafe void emitGCregDeadUpdMask(regMaskTP regs, byte* addr)
    {
#if DEBUG
        assert(emitIssuing);
#endif
        if (emitIGisInEpilog(emitCurIG))
        {
            return;
        }

#if EMIT_GENERATE_GCINFO && HAS_FIXED_REGISTER_SET
        var gcrefRegs = new regMaskTP(emitThisGCrefRegs) & regs;
        assert(emitSyncThisObjReg == REG_NA ||
            (regMaskTP.CreateFromRegNum(emitSyncThisObjReg, emitSyncThisObjReg.SingleTypeMask) & regs).IsEmpty);

        if (gcrefRegs.IsNonEmpty)
        {
            assert((emitThisByrefRegs & (regMask)gcrefRegs) == SRBM_NONE);
            if (emitFullGCinfo)
            {
                emitGCregDeadSet(GCInfo.GCtype.GCT_GCREF, gcrefRegs, addr);
            }
            emitThisGCrefRegs &= ~(regMask)gcrefRegs;
        }

        var byrefRegs = new regMaskTP(emitThisByrefRegs) & regs;
        if (byrefRegs.IsNonEmpty)
        {
            assert((emitThisGCrefRegs & (regMask)byrefRegs) == SRBM_NONE);
            if (emitFullGCinfo)
            {
                emitGCregDeadSet(GCInfo.GCtype.GCT_BYREF, byrefRegs, addr);
            }
            emitThisByrefRegs &= ~(regMask)byrefRegs;
        }
#endif
    }
}
