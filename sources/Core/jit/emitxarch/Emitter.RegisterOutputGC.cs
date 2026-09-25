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
    private unsafe uint emitCurCodeOffs(byte* dst)
    {
        nuint distance;
        if (dst >= emitCodeBlock && dst <= emitCodeBlock + emitTotalHotCodeSize)
        {
            distance = (nuint)(dst - emitCodeBlock);
        }
        else
        {
            assert(emitFirstColdIG is not null && emitColdCodeBlock != null);
            assert(dst >= emitColdCodeBlock && dst <= emitColdCodeBlock + emitTotalColdCodeSize);
            distance = (nuint)(dst - emitColdCodeBlock + emitTotalHotCodeSize);
        }
        noway_assert(distance <= uint.MaxValue);
        return (uint)distance;
    }

    private unsafe void emitGCregLiveSet(GCInfo.GCtype gcType, regMaskTP mask, byte* dst, bool isThis)
    {
#if DEBUG
        assert(emitIssuing);
#endif
        assert(gcType != GCT_NONE);
        assert(!isThis || (_compiler ?? throw new System.InvalidOperationException("Emitter is not initialized."))
            .lvaKeepAliveAndReportThis());
        assert(emitFullGCinfo);
        assert((new regMaskTP(emitThisGCrefRegs | emitThisByrefRegs) & mask).IsEmpty);
        var descriptor = gcInfo.gcRegPtrAllocDsc();
        descriptor.rpdGCtype = gcType;
        descriptor.rpdOffs = emitCurCodeOffs(dst);
        descriptor.rpdArg = false;
        descriptor.rpdCall = false;
        descriptor.rpdIsThis = isThis;
        descriptor.rpdCompiler.rpdAdd = (regMask)mask;
        descriptor.rpdCompiler.rpdDel = SRBM_NONE;
    }

    private unsafe void emitGCregDeadSet(GCInfo.GCtype gcType, regMaskTP mask, byte* dst)
    {
#if DEBUG
        assert(emitIssuing);
#endif
        assert(gcType != GCT_NONE && emitFullGCinfo);
        assert((new regMaskTP(emitThisGCrefRegs | emitThisByrefRegs) & mask).IsNonEmpty);
        var descriptor = gcInfo.gcRegPtrAllocDsc();
        descriptor.rpdGCtype = gcType;
        descriptor.rpdOffs = emitCurCodeOffs(dst);
        descriptor.rpdCall = false;
        descriptor.rpdIsThis = false;
        descriptor.rpdArg = false;
        descriptor.rpdCompiler.rpdAdd = SRBM_NONE;
        descriptor.rpdCompiler.rpdDel = (regMask)mask;
    }

    private unsafe void emitGCregDeadUpd(regNumber reg, byte* dst)
    {
#if DEBUG
        assert(emitIssuing);
#endif
#if EMIT_GENERATE_GCINFO && HAS_FIXED_REGISTER_SET
        if (emitIGisInEpilog(emitCurIG))
        {
            return;
        }
        var mask = reg.SingleTypeMask;
        if ((emitThisGCrefRegs & mask) != SRBM_NONE)
        {
            assert((emitThisByrefRegs & mask) == SRBM_NONE);
            if (emitFullGCinfo)
            {
                emitGCregDeadSet(GCT_GCREF, new regMaskTP(mask), dst);
            }
            emitThisGCrefRegs &= ~mask;
        }
        else if ((emitThisByrefRegs & mask) != SRBM_NONE)
        {
            if (emitFullGCinfo)
            {
                emitGCregDeadSet(GCT_BYREF, new regMaskTP(mask), dst);
            }
            emitThisByrefRegs &= ~mask;
        }
#endif
    }

    private unsafe void emitGCregLiveUpd(GCInfo.GCtype gcType, regNumber reg, byte* dst)
    {
#if DEBUG
        assert(emitIssuing);
#endif
#if EMIT_GENERATE_GCINFO && HAS_FIXED_REGISTER_SET
        if (emitIGisInEpilog(emitCurIG))
        {
            return;
        }
        assert(gcType is GCT_GCREF or GCT_BYREF);
        var mask = reg.SingleTypeMask;
        var live = gcType == GCT_GCREF ? emitThisGCrefRegs : emitThisByrefRegs;
        var other = gcType == GCT_GCREF ? emitThisByrefRegs : emitThisGCrefRegs;
        if ((live & mask) == SRBM_NONE)
        {
            if ((other & mask) != SRBM_NONE)
            {
                emitGCregDeadUpd(reg, dst);
            }
            if (emitFullGCinfo)
            {
                emitGCregLiveSet(gcType, new regMaskTP(mask), dst, reg == emitSyncThisObjReg);
            }
            if (gcType == GCT_GCREF)
            {
                emitThisGCrefRegs |= mask;
            }
            else
            {
                emitThisByrefRegs |= mask;
            }
        }
        assert((emitThisGCrefRegs & emitThisByrefRegs) == SRBM_NONE);
#endif
    }

    private GCInfo.GCtype emitRegGCtype(regNumber reg)
    {
        var mask = reg.SingleTypeMask;
        if ((emitThisGCrefRegs & mask) != SRBM_NONE)
        {
            return GCT_GCREF;
        }
        if ((emitThisByrefRegs & mask) != SRBM_NONE)
        {
            return GCT_BYREF;
        }
        return GCT_NONE;
    }

    private unsafe void emitHandleGCrefRegs(byte* dst, instrDesc id)
    {
        var reg1 = id.idReg1();
        var reg2 = id.idReg2();
        switch (id.idInsFmt())
        {
            case IF_RRD_RRD:
            {
                break;
            }

            case IF_RWR_RRD:
            {
                if (emitSyncThisObjReg != REG_NA && emitIGisInProlog(emitCurIG) && reg2 == REG_ARG_0)
                {
                    var compiler = _compiler ?? throw new System.InvalidOperationException("Emitter is not initialized.");
                    assert(compiler.lvaIsOriginalThisArg(0));
                    assert(compiler.lvaTable[0].lvRegister);
                    assert(compiler.lvaTable[0].RegNum == reg1);
                    if (emitFullGCinfo)
                    {
                        emitGCregLiveSet(id.idGCref(), new regMaskTP(reg1.SingleTypeMask), dst, true);
                        break;
                    }
                }
                emitGCregLiveUpd(id.idGCref(), reg1, dst);
                break;
            }

            case IF_RRW_RRD:
            case IF_RWR_RRD_RRD:
            {
                var targetReg = reg1;
                if (id.idInsFmt() == IF_RWR_RRD_RRD)
                {
                    reg1 = id.idReg2();
                    reg2 = id.idReg3();
                }
                switch (id.idIns())
                {
                    case INS_xor:
                    {
                        assert(reg1 == reg2);
                        emitGCregLiveUpd(id.idGCref(), targetReg, dst);
                        break;
                    }

                    case INS_or:
                    case INS_and:
                    {
                        emitGCregDeadUpd(targetReg, dst);
                        break;
                    }

                    case INS_add:
                    case INS_sub:
                    case INS_sub_hide:
                    {
                        assert(id.idGCref() == GCT_BYREF);
                        emitGCregLiveUpd(GCT_BYREF, targetReg, dst);
                        break;
                    }

                    default:
                    {
#if DEBUG
                        emitDispIns(id, false, false, false);
#endif
                        assert(false, "unexpected GC register update instruction");
                        break;
                    }
                }
                break;
            }

            case IF_RRW_RRW:
            {
                assert(id.idIns() == INS_xchg);
                var gc1 = emitRegGCtype(reg1);
                var gc2 = emitRegGCtype(reg2);
                if (gc1 != gc2)
                {
                    if (gc1 != GCT_NONE)
                    {
                        emitGCregDeadUpd(reg1, dst);
                    }
                    if (gc2 != GCT_NONE)
                    {
                        emitGCregDeadUpd(reg2, dst);
                    }
                    if (gc1 != GCT_NONE)
                    {
                        emitGCregLiveUpd(gc1, reg2, dst);
                    }
                    if (gc2 != GCT_NONE)
                    {
                        emitGCregLiveUpd(gc2, reg1, dst);
                    }
                }
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

    private static bool emitInsCanOnlyWriteSSE2OrAVXReg(instrDesc id)
    {
        var ins = id.idIns();
        if (!IsSimdInstruction(ins))
        {
            return false;
        }
        switch (ins)
        {
            case INS_andn:
            case INS_bextr:
            case INS_blsi:
            case INS_blsmsk:
            case INS_blsr:
            case INS_bzhi:
            case INS_cvttsd2si32:
            case INS_cvttsd2si64:
            case INS_cvttss2si32:
            case INS_cvttss2si64:
            case INS_cvtsd2si32:
            case INS_cvtsd2si64:
            case INS_cvtss2si32:
            case INS_cvtss2si64:
            case INS_extractps:
            case INS_movd32:
            case INS_movd64:
            case INS_movmskpd:
            case INS_movmskps:
            case INS_mulx:
            case INS_pdep:
            case INS_pext:
            case INS_pmovmskb:
            case INS_pextrb:
            case INS_pextrd:
            case INS_pextrq:
            case INS_pextrw:
            case INS_rorx:
            case INS_shlx:
            case INS_sarx:
            case INS_shrx:
            case INS_vcvtsd2usi32:
            case INS_vcvtsd2usi64:
            case INS_vcvtss2usi32:
            case INS_vcvtss2usi64:
            case INS_vcvttsd2usi32:
            case INS_vcvttsd2usi64:
            case INS_vcvttss2usi32:
            case INS_vcvttss2usi64:
            {
                return false;
            }

            case INS_kmovb_gpr:
            case INS_kmovw_gpr:
            case INS_kmovd_gpr:
            case INS_kmovq_gpr:
            {
                return !id.idReg1().IsIntReg;
            }

            default:
            {
                return true;
            }
        }
    }
#endif
}
