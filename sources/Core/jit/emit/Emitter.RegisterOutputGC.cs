// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    private GCInfo.GCtype emitRegGCtype(regNumber reg)
    {
#if DEBUG
        assert(emitIssuing);
#endif

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
#if TARGET_X86
                if (emitSyncThisObjReg != REG_NA && emitIGisInProlog(emitCurIG) && reg2 == REG_ECX)
#else
                if (emitSyncThisObjReg != REG_NA && emitIGisInProlog(emitCurIG) && reg2 == REG_ARG_0)
#endif
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

#endif

#if TARGET_XARCH
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
