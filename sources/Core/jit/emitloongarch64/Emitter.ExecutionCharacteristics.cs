// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 && (DEBUG || LATE_DISASM)
using static RyuJitSharp.Emitter.insDisasmFmt;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;

namespace RyuJitSharp;

public partial class Emitter
{
    private const float PERFSCORE_THROUGHPUT_7C = 7.0f;
    private const float PERFSCORE_THROUGHPUT_9X = 1.0f / 9.0f;
    private const float PERFSCORE_LATENCY_22C = 22.0f;

    internal insExecutionCharacteristics getInsExecutionCharacteristicsLoongArch64(instrDesc id)
    {
        var ins = id.idIns();
        assert(ins != INS_invalid);

        var result = new insExecutionCharacteristics
        {
            insThroughput = PERFSCORE_THROUGHPUT_ILLEGAL,
            insLatency = PERFSCORE_LATENCY_ILLEGAL,
            insMemoryAccessKind = PerfScoreMemoryAccessKind.None,
        };

        var combinedInstructionCount = unchecked((int)(id.idCodeSize() / 4));
        assert(combinedInstructionCount >= 1);
        if (combinedInstructionCount > 1)
        {
            if (id.idInsOpt() == INS_OPTS_RC)
            {
                assert(combinedInstructionCount is 2 or 3);
                if (id.idInsIs(INS_b, INS_bl))
                {
                    result.insLatency = combinedInstructionCount == 2
                        ? PERFSCORE_LATENCY_2C
                        : PERFSCORE_LATENCY_3C;
                    result.insThroughput = combinedInstructionCount == 2
                        ? PERFSCORE_THROUGHPUT_6C
                        : PERFSCORE_THROUGHPUT_9C;
                }
                else
                {
                    result.insMemoryAccessKind = PerfScoreMemoryAccessKind.Read;
                    result.insThroughput = combinedInstructionCount == 2
                        ? PERFSCORE_THROUGHPUT_4C
                        : PERFSCORE_THROUGHPUT_7C;

                    if ((INS_ld_b <= ins) && (ins <= INS_ld_wu))
                    {
                        result.insLatency = combinedInstructionCount == 2
                            ? PERFSCORE_LATENCY_5C
                            : PERFSCORE_LATENCY_6C;
                    }
                    else if (id.idInsIs(INS_fld_s, INS_fld_d))
                    {
                        result.insLatency = combinedInstructionCount == 2
                            ? PERFSCORE_LATENCY_6C
                            : PERFSCORE_LATENCY_7C;
                    }
#if FEATURE_SIMD
                    else if (id.idInsIs(INS_vld, INS_xvld))
                    {
                        result.insLatency = combinedInstructionCount == 2
                            ? PERFSCORE_LATENCY_6C
                            : PERFSCORE_LATENCY_7C;
                    }
#endif
                    else
                    {
                        assert(false, "perfscore: unexpected load instruction with INS_OPTS_RC.");
                    }
                }
            }
            else if (id.idInsOpt() == INS_OPTS_RL)
            {
                assert(combinedInstructionCount is 2 or 3);
                result.insLatency = combinedInstructionCount == 2
                    ? PERFSCORE_LATENCY_2C
                    : PERFSCORE_LATENCY_3C;
                result.insThroughput = combinedInstructionCount == 2
                    ? PERFSCORE_THROUGHPUT_6C
                    : PERFSCORE_THROUGHPUT_9C;
            }
            else if (id.idInsOpt() == INS_OPTS_JIRL)
            {
                assert(combinedInstructionCount is 2 or 3);
                result.insLatency = combinedInstructionCount == 2
                    ? PERFSCORE_LATENCY_2C
                    : PERFSCORE_LATENCY_3C;
                result.insThroughput = combinedInstructionCount == 2 ? 5.5f : PERFSCORE_THROUGHPUT_5C;
            }
            else if (id.idInsOpt() == INS_OPTS_I)
            {
                result.insLatency = PERFSCORE_LATENCY_1C * combinedInstructionCount;
                result.insThroughput = combinedInstructionCount < 3
                    ? PERFSCORE_THROUGHPUT_4C * combinedInstructionCount
                    : (PERFSCORE_THROUGHPUT_4C * (combinedInstructionCount - 1)) + PERFSCORE_THROUGHPUT_1C;
            }
            else if (id.idInsOpt() == INS_OPTS_C)
            {
                assert(combinedInstructionCount is 2 or 4);
                result.insLatency = combinedInstructionCount == 2
                    ? PERFSCORE_LATENCY_2C
                    : PERFSCORE_LATENCY_4C;
                result.insThroughput = combinedInstructionCount == 2 ? 3.5f : 10.5f;
            }
            else if (id.idInsOpt() == INS_OPTS_RELOC)
            {
                result.insLatency = id.idIsCnsReloc() ? PERFSCORE_LATENCY_2C : PERFSCORE_LATENCY_5C;
                result.insThroughput = id.idIsCnsReloc()
                    ? PERFSCORE_THROUGHPUT_6C
                    : PERFSCORE_THROUGHPUT_4C;
                result.insMemoryAccessKind = id.idIsCnsReloc()
                    ? PerfScoreMemoryAccessKind.None
                    : PerfScoreMemoryAccessKind.Read;
            }
            else
            {
                perfScoreUnhandledInstruction(id, ref result);
            }

            return result;
        }

        assert(ins != INS_lea);
        if (emitInsIsLoadOrStore(ins))
        {
            if (emitInsIsLoad(ins))
            {
                result.insMemoryAccessKind = emitInsIsStore(ins)
                    ? PerfScoreMemoryAccessKind.ReadWrite
                    : PerfScoreMemoryAccessKind.Read;
            }
            else
            {
                assert(emitInsIsStore(ins));
                result.insMemoryAccessKind = PerfScoreMemoryAccessKind.Write;
            }
        }

        switch (emitGetInsFmt(ins))
        {
            case DF_G_ALIAS:
            case DF_G_2R5IU:
            case DF_G_2R6IU:
            case DF_G_2R16I:
            case DF_G_2R12IU:
            case DF_G_2R5IW:
            case DF_G_2R6ID:
            case DF_G_3R2IU:
            case DF_G_3RX:
            {
                result.insLatency = PERFSCORE_LATENCY_1C;
                result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                break;
            }
            case DF_G_B2:
            case DF_G_B1:
            case DF_F_B1:
            {
                result.insLatency = PERFSCORE_LATENCY_1C;
                result.insThroughput = (PERFSCORE_THROUGHPUT_1C + PERFSCORE_THROUGHPUT_2C) / 2;
                break;
            }
            case DF_G_B0:
            {
                result.insLatency = PERFSCORE_LATENCY_1C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }
            case DF_G_15I:
            {
                switch (ins)
                {
                    case INS_dbar:
                    case INS_ibar:
                    {
                        result.insLatency = PERFSCORE_LATENCY_ZERO;
                        result.insThroughput = ins == INS_dbar ? 0.09f : 0.06f;
                        break;
                    }
                    case INS_break:
                    {
                        result.insLatency = PERFSCORE_LATENCY_ZERO;
                        result.insThroughput = PERFSCORE_LATENCY_ZERO;
                        break;
                    }
                    default:
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }

                break;
            }
            case DF_G_R20I:
            {
                result.insLatency = PERFSCORE_LATENCY_1C;
                switch (ins)
                {
                    case INS_lu12i_w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                        break;
                    }
                    case INS_lu32i_d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }
                    case INS_pcaddi:
                    case INS_pcaddu12i:
                    case INS_pcalau12i:
                    case INS_pcaddu18i:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        break;
                    }
                    default:
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }

                break;
            }
            case DF_G_2R:
            {
                if ((INS_ext_w_b <= ins) && (ins <= INS_bitrev_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else
                {
                    assert((INS_rdtimel_w <= ins) && (ins <= INS_cpucfg));
                    result.insLatency = PERFSCORE_LATENCY_4C;
                    result.insThroughput = ins == INS_cpucfg
                        ? PERFSCORE_THROUGHPUT_1C
                        : (0.5f + 1.0f) / 2;
                }

                break;
            }
            case DF_G_2R12I:
            {
                if ((INS_ld_b <= ins) && (ins <= INS_ld_wu))
                {
                    result.insLatency = PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if ((INS_addi_w <= ins) && (ins <= INS_sltui))
                {
                    result.insLatency = PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if ((INS_st_b <= ins) && (ins <= INS_st_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_ZERO;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert(ins == INS_preld);
                    result.insLatency = PERFSCORE_LATENCY_ZERO;
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                }

                break;
            }
            case DF_G_2R14I:
            {
                switch (ins)
                {
                    case INS_ldptr_w:
                    case INS_ldptr_d:
                    {
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        break;
                    }
                    case INS_ll_w:
                    case INS_ll_d:
                    {
                        result.insLatency = PERFSCORE_LATENCY_9C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_9X;
                        break;
                    }
                    case INS_stptr_w:
                    case INS_stptr_d:
                    {
                        result.insLatency = PERFSCORE_LATENCY_ZERO;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        break;
                    }
                    case INS_sc_w:
                    case INS_sc_d:
                    {
                        result.insLatency = PERFSCORE_LATENCY_15C;
                        result.insThroughput = 0.0667f;
                        break;
                    }
                    default:
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }

                break;
            }
            case DF_G_3R:
            {
                if (((INS_add_w <= ins) && (ins <= INS_orn)) || ((INS_sll_w <= ins) && (ins <= INS_sltu)))
                {
                    result.insLatency = PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if (((INS_mul_w <= ins) && (ins <= INS_mulw_d_wu)) ||
                         ((INS_ldx_b <= ins) && (ins <= INS_ldx_wu)))
                {
                    result.insLatency = PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if ((INS_crc_w_b_w <= ins) && (ins <= INS_crcc_w_d_w))
                {
                    result.insLatency = PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if ((INS_ldgt_b <= ins) && (ins <= INS_ldle_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                }
                else if ((INS_stx_b <= ins) && (ins <= INS_preldx))
                {
                    result.insLatency = PERFSCORE_LATENCY_ZERO;
                    result.insThroughput = ins <= INS_stx_d
                        ? PERFSCORE_THROUGHPUT_2C
                        : PERFSCORE_THROUGHPUT_1C;
                }
                else if ((INS_amcas_b <= ins) && (ins <= INS_ammin_du))
                {
                    result.insLatency = PERFSCORE_LATENCY_13C;
                    result.insThroughput = 0.0769f;
                }
                else if ((INS_amcas_db_b <= ins) && (ins <= INS_ammin_db_du))
                {
                    result.insLatency = PERFSCORE_LATENCY_16C;
                    result.insThroughput = 0.0625f;
                }
                else
                {
                    assert((INS_div_w <= ins) && (ins <= INS_mod_du));
                    if ((INS_div_w <= ins) && (ins <= INS_mod_wu))
                    {
                        result.insLatency = (ins <= INS_mod_w)
                            ? (4.0f + 19.0f) / 2
                            : (PERFSCORE_LATENCY_4C + PERFSCORE_LATENCY_20C) / 2;
                        result.insThroughput = (0.08f + 0.25f) / 2;
                    }
                    else
                    {
                        result.insLatency = ins <= INS_mod_d ? (4.0f + 32.0f) / 2 : (4.0f + 33.0f) / 2;
                        result.insThroughput = (0.05f + 0.25f) / 2;
                    }
                }

                break;
            }
            case DF_F_GR:
            case DF_F_RG:
            case DF_F_CG:
            case DF_F_GC:
            {
                result.insLatency = ins == INS_movgr2frh_w
                    ? PERFSCORE_LATENCY_3C
                    : PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }
            case DF_F_RG12I:
            {
                result.insLatency = id.idInsIs(INS_fld_s, INS_fld_d)
                    ? PERFSCORE_LATENCY_5C
                    : PERFSCORE_LATENCY_ZERO;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }
            case DF_F_FG:
            {
                result.insLatency = PERFSCORE_LATENCY_15C;
                result.insThroughput = 0.0667f;
                break;
            }
            case DF_F_GF:
            {
                result.insLatency = PERFSCORE_LATENCY_7C;
                result.insThroughput = 0.14f;
                break;
            }
            case DF_F_CR:
            case DF_F_RC:
            case DF_F_C2R:
            case DF_F_3RX3:
            {
                result.insLatency = PERFSCORE_LATENCY_1C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }
            case DF_F_2R:
            {
                if (id.idInsIs(INS_fabs_s, INS_fabs_d, INS_fneg_s, INS_fneg_d, INS_fmov_s, INS_fmov_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if (ins == INS_fsqrt_s)
                {
                    result.insLatency = (PERFSCORE_LATENCY_8C + PERFSCORE_LATENCY_22C) / 2;
                    result.insThroughput = (0.2f + 0.667f) / 2;
                }
                else if (ins == INS_fsqrt_d)
                {
                    result.insLatency = (8.0f + 36.0f) / 2;
                    result.insThroughput = (0.12f + 0.667f) / 2;
                }
                else if (ins == INS_frsqrt_s)
                {
                    result.insLatency = (11.0f + 33.0f) / 2;
                    result.insThroughput = (0.129f + 0.445f) / 2;
                }
                else if (ins == INS_frsqrt_d)
                {
                    result.insLatency = (11.0f + 54.0f) / 2;
                    result.insThroughput = (0.077f + 0.445f) / 2;
                }
                else if (id.idInsIs(INS_frsqrte_s, INS_frsqrte_d, INS_frecipe_s, INS_frecipe_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_5C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if (ins == INS_frecip_s)
                {
                    result.insLatency = (PERFSCORE_LATENCY_8C + PERFSCORE_LATENCY_16C) / 2;
                    result.insThroughput = (0.29f + 0.667f) / 2;
                }
                else if (ins == INS_frecip_d)
                {
                    result.insLatency = (PERFSCORE_LATENCY_8C + PERFSCORE_LATENCY_23C) / 2;
                    result.insThroughput = (0.19f + 0.667f) / 2;
                }
                else if (id.idInsIs(INS_flogb_s, INS_flogb_d) ||
                         ((INS_ffint_s_w <= ins) && (ins <= INS_frint_d)))
                {
                    result.insLatency = PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else
                {
                    assert((INS_fclass_s <= ins) && (ins <= INS_fcvt_d_s));
                    result.insLatency = PERFSCORE_LATENCY_2C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }

                break;
            }
            case DF_F_R2G:
            {
                if ((INS_fldx_s <= ins) && (ins <= INS_fstx_d))
                {
                    result.insLatency = ins <= INS_fldx_d
                        ? PERFSCORE_LATENCY_5C
                        : PERFSCORE_LATENCY_ZERO;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert((INS_fldgt_s <= ins) && (ins <= INS_fstle_d));
                    result.insLatency = ins <= INS_fldle_d
                        ? PERFSCORE_LATENCY_5C
                        : PERFSCORE_LATENCY_ZERO;
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                }

                break;
            }
            case DF_F_3R:
            {
                if ((INS_fadd_s <= ins) && (ins <= INS_fsub_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_3C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if ((INS_fmul_s <= ins) && (ins <= INS_fmul_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_5C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if ((INS_fdiv_s <= ins) && (ins <= INS_fdiv_d))
                {
                    result.insLatency = ins == INS_fdiv_s
                        ? (PERFSCORE_LATENCY_6C + PERFSCORE_LATENCY_13C) / 2
                        : (6.0f + 19.0f) / 2;
                    result.insThroughput = ins == INS_fdiv_s
                        ? (0.28f + 0.667f) / 2
                        : (0.2f + 0.667f) / 2;
                }
                else if ((INS_fmax_s <= ins) && (ins <= INS_fmina_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_2C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if ((INS_fscaleb_s <= ins) && (ins <= INS_fscaleb_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else
                {
                    assert(ins is INS_fcopysign_s or INS_fcopysign_d);
                    result.insLatency = PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }

                break;
            }
            case DF_F_4R:
            {
                result.insLatency = PERFSCORE_LATENCY_5C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }
#if FEATURE_SIMD
            case DF_S_4R:
            case DF_A_4R:
            {
                result.insLatency = id.idInsIs(INS_vbitsel_v, INS_vshuf_b, INS_xvbitsel_v, INS_xvshuf_b)
                    ? PERFSCORE_LATENCY_1C
                    : PERFSCORE_LATENCY_5C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }
            case DF_S_3R:
            {
                if (id.idInsIs(INS_vldx, INS_vstx, INS_vfmul_s, INS_vfmul_d) ||
                    ((INS_vffint_s_l <= ins) && (ins <= INS_vftintrne_w_d)))
                {
                    result.insLatency = ins == INS_vstx ? PERFSCORE_LATENCY_ZERO : PERFSCORE_LATENCY_5C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if (((INS_vfcmp_caf_s <= ins) && (ins <= INS_vfcmp_sune_d)) ||
                         ((INS_vfadd_s <= ins) && (ins <= INS_vfsub_d)) ||
                         ((INS_vfmax_s <= ins) && (ins <= INS_vfmina_d)))
                {
                    result.insLatency = id.idInsIs(INS_vfadd_s, INS_vfadd_d, INS_vfsub_s, INS_vfsub_d)
                        ? PERFSCORE_LATENCY_3C
                        : PERFSCORE_LATENCY_2C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if (((INS_vseq_b <= ins) && (ins <= INS_vsub_d)) ||
                         ((INS_vsadd_b <= ins) && (ins <= INS_vssub_du)) ||
                         ((INS_vavg_b <= ins) && (ins <= INS_vmin_du)) ||
                         ((INS_vsll_b <= ins) && (ins <= INS_vrotr_d)) ||
                         ((INS_vpackev_b <= ins) && (ins <= INS_vpickod_d)) ||
                         ((INS_vand_v <= ins) && (ins <= INS_vorn_v)))
                {
                    result.insLatency = id.idInsIs(
                        INS_vsle_d, INS_vsle_du, INS_vslt_d, INS_vslt_du, INS_vavg_d, INS_vavg_du, INS_vavgr_d,
                        INS_vavgr_du, INS_vmax_d, INS_vmin_d, INS_vmax_du, INS_vmin_du)
                            ? PERFSCORE_LATENCY_2C
                            : PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if (((INS_vaddwev_h_b <= ins) && (ins <= INS_vaddwod_q_du_d)) ||
                         ((INS_vhaddw_h_b <= ins) && (ins <= INS_vabsd_du)) ||
                         ((INS_vsrlr_b <= ins) && (ins <= INS_vsran_w_d)) ||
                         ((INS_vbitclr_b <= ins) && (ins <= INS_vbitrev_d)) ||
                         ((INS_vfrstp_b <= ins) && (ins <= INS_vsub_q)) ||
                         ((INS_vfcvt_h_s <= ins) && (ins <= INS_vfcvt_s_d)))
                {
                    result.insLatency = id.idInsIs(
                        INS_vaddwev_q_d, INS_vsubwev_q_d, INS_vaddwod_q_d, INS_vsubwod_q_d, INS_vaddwev_q_du,
                        INS_vsubwev_q_du, INS_vaddwod_q_du, INS_vsubwod_q_du, INS_vaddwev_q_du_d, INS_vaddwod_q_du_d,
                        INS_vhaddw_q_d, INS_vhsubw_q_d, INS_vhaddw_qu_du, INS_vhsubw_qu_du, INS_vfcvt_h_s, INS_vfcvt_s_d,
                        INS_vadda_b, INS_vadda_h, INS_vadda_w, INS_vadda_d, INS_vsrlr_b, INS_vsrlr_h, INS_vsrlr_w,
                        INS_vsrlr_d, INS_vsrar_b, INS_vsrar_h, INS_vsrar_w, INS_vsrar_d, INS_vadd_q, INS_vsub_q)
                            ? PERFSCORE_LATENCY_3C
                            : PERFSCORE_LATENCY_2C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if (((INS_vmul_b <= ins) && (ins <= INS_vmaddwod_q_du_d)) ||
                         ((INS_vsrlrn_b_h <= ins) && (ins <= INS_vssrarn_wu_d)))
                {
                    result.insLatency = id.idInsIs(
                        INS_vmulwev_q_d, INS_vmulwod_q_d, INS_vmulwev_q_du, INS_vmulwod_q_du, INS_vmulwev_q_du_d,
                        INS_vmulwod_q_du_d, INS_vmaddwev_q_d, INS_vmaddwod_q_d, INS_vmaddwev_q_du, INS_vmaddwod_q_du,
                        INS_vmaddwev_q_du_d, INS_vmaddwod_q_du_d)
                            ? PERFSCORE_LATENCY_7C
                            : PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if (((INS_vsigncov_b <= ins) && (ins <= INS_vsigncov_d)) ||
                         ((INS_vshuf_h <= ins) && (ins <= INS_vshuf_d)))
                {
                    result.insLatency = PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert(((INS_vdiv_b <= ins) && (ins <= INS_vmod_du)) || id.idInsIs(INS_vfdiv_s, INS_vfdiv_d));
                    if (id.idInsIs(INS_vdiv_b, INS_vmod_b, INS_vdiv_bu, INS_vmod_bu))
                    {
                        result.insLatency = id.idInsIs(INS_vdiv_b, INS_vmod_b)
                            ? (24.0f + 52.0f) / 2
                            : (25.0f + 52.0f) / 2;
                        result.insThroughput = id.idInsIs(INS_vdiv_b, INS_vmod_b)
                            ? (0.03f + 0.074f) / 2
                            : (0.03f + 0.07f) / 2;
                    }
                    else if (id.idInsIs(INS_vdiv_h, INS_vmod_h, INS_vdiv_hu, INS_vmod_hu))
                    {
                        result.insLatency = (14.0f + 35.0f) / 2;
                        result.insThroughput = (0.05f + 0.13f) / 2;
                    }
                    else if (id.idInsIs(INS_vdiv_w, INS_vmod_w, INS_vdiv_wu, INS_vmod_wu))
                    {
                        result.insLatency = (9.0f + 26.0f) / 2;
                        result.insThroughput = id.idInsIs(INS_vdiv_w, INS_vmod_w)
                            ? (0.07f + 0.22f) / 2
                            : (0.065f + 0.22f) / 2;
                    }
                    else if (id.idInsIs(INS_vdiv_d, INS_vmod_d, INS_vdiv_du, INS_vmod_du))
                    {
                        result.insLatency = (6.0f + 22.0f) / 2;
                        result.insThroughput = (0.08f + 0.33f) / 2;
                    }
                    else if (id.idInsIs(INS_vfdiv_s))
                    {
                        result.insLatency = (9.0f + 23.0f) / 2;
                        result.insThroughput = (0.08f + 0.22f) / 2;
                    }
                    else if (id.idInsIs(INS_vfdiv_d))
                    {
                        result.insLatency = (6.0f + 19.0f) / 2;
                        result.insThroughput = (0.095f + 0.33f) / 2;
                    }
                }

                break;
            }
            case DF_S_RG:
            case DF_S_GRX:
            case DF_S_2RG:
            case DF_A_RG:
            case DF_A_2RG:
            {
                result.insLatency = id.idInsIs(
                    INS_vreplgr2vr_w, INS_vreplgr2vr_d, INS_xvreplgr2vr_w, INS_xvreplgr2vr_d)
                        ? PERFSCORE_LATENCY_2C
                        : PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }
            case DF_S_2R:
            {
                if (((INS_vclo_b <= ins) && (ins <= INS_vmsknz_b)) ||
                    ((INS_vexth_h_b <= ins) && (ins <= INS_vextl_qu_du)))
                {
                    result.insLatency = ((INS_vclo_b <= ins) && (ins <= INS_vpcnt_d))
                        ? PERFSCORE_LATENCY_2C
                        : PERFSCORE_LATENCY_1C;
                    result.insThroughput = id.idInsIs(INS_vpcnt_b, INS_vpcnt_h, INS_vpcnt_w, INS_vpcnt_d)
                        ? PERFSCORE_THROUGHPUT_2C
                        : PERFSCORE_THROUGHPUT_4C;
                }
                else if ((INS_vflogb_s <= ins) && (ins <= INS_vfclass_d))
                {
                    result.insLatency = id.idInsIs(INS_vfclass_s, INS_vfclass_d)
                        ? PERFSCORE_LATENCY_2C
                        : PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if ((INS_vfrint_s <= ins) && (ins <= INS_vffint_d_lu))
                {
                    result.insLatency = id.idInsIs(INS_vfcvtl_s_h, INS_vfcvth_s_h, INS_vfcvtl_d_s, INS_vfcvth_d_s)
                        ? PERFSCORE_LATENCY_3C
                        : PERFSCORE_LATENCY_4C;
                    result.insThroughput = id.idInsIs(INS_vffint_s_w, INS_vffint_s_wu, INS_vffint_d_l, INS_vffint_d_lu)
                        ? PERFSCORE_THROUGHPUT_4C
                        : PERFSCORE_THROUGHPUT_2C;
                }
                else if ((INS_vftint_w_s <= ins) && (ins <= INS_vftintrz_lu_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if (((INS_vftintl_l_s <= ins) && (ins <= INS_vftintrneh_l_s)) ||
                         id.idInsIs(INS_vffintl_d_w, INS_vffinth_d_w) ||
                         ((INS_vfrecipe_s <= ins) && (ins <= INS_vfrsqrte_d)))
                {
                    result.insLatency = PERFSCORE_LATENCY_5C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert((INS_vfsqrt_s <= ins) && (ins <= INS_vfrsqrt_d));
                    if (id.idInsIs(INS_vfsqrt_s, INS_vfsqrt_d))
                    {
                        result.insLatency = id.idInsIs(INS_vfsqrt_s) ? (11.0f + 39.0f) / 2 : (8.0f + 36.0f) / 2;
                        result.insThroughput = id.idInsIs(INS_vfsqrt_s)
                            ? (0.054f + 0.222f) / 2
                            : (0.059f + 0.333f) / 2;
                    }
                    else if (id.idInsIs(INS_vfrecip_s, INS_vfrecip_d))
                    {
                        result.insLatency = id.idInsIs(INS_vfrecip_s) ? (11.0f + 27.0f) / 2 : (8.0f + 23.0f) / 2;
                        result.insThroughput = id.idInsIs(INS_vfrecip_s)
                            ? (0.08f + 0.222f) / 2
                            : (0.095f + 0.333f) / 2;
                    }
                    else if (id.idInsIs(INS_vfrsqrt_s, INS_vfrsqrt_d))
                    {
                        result.insLatency = id.idInsIs(INS_vfrsqrt_s) ? (17.0f + 61.0f) / 2 : (11.0f + 54.0f) / 2;
                        result.insThroughput = id.idInsIs(INS_vfrsqrt_s)
                            ? (0.034f + 0.133f) / 2
                            : (0.038f + 0.222f) / 2;
                    }
                }

                break;
            }
            case DF_S_2RX:
            case DF_S_R13IU:
            case DF_S_2R8IU:
            case DF_S_2R5I:
            {
                result.insLatency = id.idInsIs(INS_vslei_d, INS_vslti_d, INS_vmaxi_d, INS_vmini_d)
                    ? PERFSCORE_LATENCY_2C
                    : PERFSCORE_LATENCY_1C;
                result.insThroughput = id.idInsIs(INS_vbitseli_b)
                    ? PERFSCORE_THROUGHPUT_2C
                    : PERFSCORE_THROUGHPUT_4C;
                break;
            }
            case DF_S_CR:
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }
            case DF_S_RGX:
            {
                if (id.idInsIs(INS_vinsgr2vr_d, INS_vinsgr2vr_w, INS_vinsgr2vr_h, INS_vinsgr2vr_b))
                {
                    result.insLatency = PERFSCORE_LATENCY_3C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                }
                else
                {
                    assert(id.idInsIs(INS_vldrepl_d, INS_vldrepl_w, INS_vldrepl_h, INS_vldrepl_b, INS_vld, INS_vst));
                    result.insLatency = id.idInsIs(INS_vst) ? PERFSCORE_LATENCY_ZERO : PERFSCORE_LATENCY_5C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }

                break;
            }
            case DF_S_2R3IU:
            {
                if ((INS_vsllwil_h_b <= ins) && (ins <= INS_vsat_bu))
                {
                    result.insLatency = PERFSCORE_LATENCY_2C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert(id.idInsIs(
                        INS_vslli_b, INS_vsrli_b, INS_vsrai_b, INS_vsrlri_b, INS_vsrari_b, INS_vrotri_b, INS_vreplvei_h));
                    result.insLatency = id.idInsIs(INS_vsrlri_b, INS_vsrari_b)
                        ? PERFSCORE_LATENCY_3C
                        : PERFSCORE_LATENCY_1C;
                    result.insThroughput = id.idInsIs(INS_vsrlri_b, INS_vsrari_b)
                        ? PERFSCORE_THROUGHPUT_2C
                        : PERFSCORE_THROUGHPUT_4C;
                }

                break;
            }
            case DF_S_2R4IU:
            {
                if ((INS_vsllwil_w_h <= ins) && (ins <= INS_vsat_hu))
                {
                    result.insLatency = ((INS_vsrlni_b_h <= ins) && (ins <= INS_vssrarni_bu_h))
                        ? PERFSCORE_LATENCY_4C
                        : PERFSCORE_LATENCY_2C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert(id.idInsIs(
                        INS_vslli_h, INS_vsrli_h, INS_vsrai_h, INS_vsrlri_h, INS_vsrari_h, INS_vrotri_h, INS_vreplvei_b));
                    result.insLatency = id.idInsIs(INS_vsrlri_h, INS_vsrari_h)
                        ? PERFSCORE_LATENCY_3C
                        : PERFSCORE_LATENCY_1C;
                    result.insThroughput = id.idInsIs(INS_vsrlri_h, INS_vsrari_h)
                        ? PERFSCORE_THROUGHPUT_2C
                        : PERFSCORE_THROUGHPUT_4C;
                }

                break;
            }
            case DF_S_2R5IU:
            {
                if (((INS_vslei_bu <= ins) && (ins <= INS_vrotri_w)) ||
                    ((INS_vmaxi_bu <= ins) && (ins <= INS_vmini_du)))
                {
                    if (id.idInsIs(INS_vsrlri_w, INS_vsrari_w))
                    {
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                    }
                    else
                    {
                        result.insLatency = id.idInsIs(INS_vslei_du, INS_vslti_du, INS_vmaxi_du, INS_vmini_du)
                            ? PERFSCORE_LATENCY_2C
                            : PERFSCORE_LATENCY_1C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                    }
                }
                else
                {
                    result.insLatency = id.idInsIs(
                        INS_vsllwil_d_w, INS_vsllwil_du_wu, INS_vbitclri_w, INS_vbitseti_w, INS_vbitrevi_w,
                        INS_vfrstpi_b, INS_vfrstpi_h, INS_vsat_w, INS_vsat_wu)
                            ? PERFSCORE_LATENCY_2C
                            : PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }

                break;
            }
            case DF_S_2R6IU:
            {
                if ((INS_vsrlni_w_d <= ins) && (ins <= INS_vsat_du))
                {
                    result.insLatency = id.idInsIs(
                        INS_vbitclri_d, INS_vbitseti_d, INS_vbitrevi_d, INS_vsat_d, INS_vsat_du)
                            ? PERFSCORE_LATENCY_2C
                            : PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert((INS_vslli_d <= ins) && (ins <= INS_vsrari_d));
                    result.insLatency = id.idInsIs(INS_vsrlri_d, INS_vsrari_d)
                        ? PERFSCORE_LATENCY_3C
                        : PERFSCORE_LATENCY_1C;
                    result.insThroughput = id.idInsIs(INS_vsrlri_d, INS_vsrari_d)
                        ? PERFSCORE_THROUGHPUT_2C
                        : PERFSCORE_THROUGHPUT_4C;
                }

                break;
            }
            case DF_S_2R7IU:
            case DF_A_2R7IU:
            case DF_A_CR:
            {
                result.insLatency = PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }
            case DF_A_3R:
            {
                if ((((INS_xvfcmp_caf_s <= ins) && (ins <= INS_xvfcmp_sune_d)) ||
                     ((INS_xvfadd_s <= ins) && (ins <= INS_xvfsub_d)) ||
                     ((INS_xvfmax_s <= ins) && (ins <= INS_xvfmina_d)) || (ins == INS_xvperm_w)))
                {
                    result.insLatency = id.idInsIs(
                        INS_xvfadd_s, INS_xvfadd_d, INS_xvfsub_s, INS_xvfsub_d, INS_xvperm_w)
                            ? PERFSCORE_LATENCY_3C
                            : PERFSCORE_LATENCY_2C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if (((INS_xvseq_b <= ins) && (ins <= INS_xvsub_d)) ||
                         ((INS_xvsadd_b <= ins) && (ins <= INS_xvssub_du)) ||
                         ((INS_xvavg_b <= ins) && (ins <= INS_xvmin_du)) ||
                         ((INS_xvsll_b <= ins) && (ins <= INS_xvrotr_d)) ||
                         ((INS_xvpackev_b <= ins) && (ins <= INS_xvorn_v)) ||
                         ((INS_xvsigncov_b <= ins) && (ins <= INS_xvsigncov_d)) ||
                         ((INS_xvshuf_h <= ins) && (ins <= INS_xvshuf_d)))
                {
                    result.insLatency = id.idInsIs(
                        INS_xvsle_d, INS_xvsle_du, INS_xvslt_d, INS_xvslt_du, INS_xvavg_d, INS_xvavg_du,
                        INS_xvavgr_d, INS_xvavgr_du, INS_xvmax_d, INS_xvmin_d, INS_xvmax_du, INS_xvmin_du)
                            ? PERFSCORE_LATENCY_2C
                            : PERFSCORE_LATENCY_1C;
                    result.insThroughput = id.idInsIs(
                        INS_xvsigncov_b, INS_xvsigncov_h, INS_xvsigncov_w, INS_xvsigncov_d,
                        INS_xvshuf_h, INS_xvshuf_w, INS_xvshuf_d)
                            ? PERFSCORE_THROUGHPUT_2C
                            : PERFSCORE_THROUGHPUT_4C;
                }
                else if (((INS_xvaddwev_h_b <= ins) && (ins <= INS_xvaddwod_q_du_d)) ||
                         ((INS_xvhaddw_h_b <= ins) && (ins <= INS_xvabsd_du)) ||
                         ((INS_xvsrlr_b <= ins) && (ins <= INS_xvsran_w_d)) ||
                         ((INS_xvbitclr_b <= ins) && (ins <= INS_xvbitrev_d)) ||
                         ((INS_xvfrstp_b <= ins) && (ins <= INS_xvsub_q)) ||
                         ((INS_xvfcvt_h_s <= ins) && (ins <= INS_xvfcvt_s_d)))
                {
                    result.insLatency = id.idInsIs(
                        INS_xvaddwev_q_d, INS_xvsubwev_q_d, INS_xvaddwod_q_d, INS_xvsubwod_q_d,
                        INS_xvaddwev_q_du, INS_xvsubwev_q_du, INS_xvaddwod_q_du, INS_xvsubwod_q_du,
                        INS_xvaddwev_q_du_d, INS_xvaddwod_q_du_d, INS_xvhaddw_q_d, INS_xvhsubw_q_d,
                        INS_xvhaddw_qu_du, INS_xvhsubw_qu_du, INS_xvadda_b, INS_xvadda_h, INS_xvadda_w,
                        INS_xvadda_d, INS_xvsrlr_b, INS_xvsrlr_h, INS_xvsrlr_w, INS_xvsrlr_d, INS_xvsrar_b,
                        INS_xvsrar_h, INS_xvsrar_w, INS_xvsrar_d, INS_xvadd_q, INS_xvsub_q, INS_xvfcvt_h_s,
                        INS_xvfcvt_s_d)
                            ? PERFSCORE_LATENCY_3C
                            : PERFSCORE_LATENCY_2C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if (((INS_xvmul_b <= ins) && (ins <= INS_xvmaddwod_q_du_d)) ||
                         ((INS_xvsrlrn_b_h <= ins) && (ins <= INS_xvssrarn_wu_d)))
                {
                    result.insLatency = id.idInsIs(
                        INS_xvmulwev_q_d, INS_xvmulwod_q_d, INS_xvmulwev_q_du, INS_xvmulwod_q_du,
                        INS_xvmulwev_q_du_d, INS_xvmulwod_q_du_d, INS_xvmaddwev_q_d, INS_xvmaddwod_q_d,
                        INS_xvmaddwev_q_du, INS_xvmaddwod_q_du, INS_xvmaddwev_q_du_d, INS_xvmaddwod_q_du_d)
                            ? PERFSCORE_LATENCY_7C
                            : PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if (((INS_xvfmul_s <= ins) && (ins <= INS_xvfmul_d)) ||
                         ((INS_xvffint_s_l <= ins) && (ins <= INS_xvftintrne_w_d)) ||
                         (ins == INS_xvldx) || (ins == INS_xvstx))
                {
                    result.insLatency = id.idInsIs(INS_xvstx)
                        ? PERFSCORE_LATENCY_ZERO
                        : PERFSCORE_LATENCY_5C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert(((INS_xvdiv_b <= ins) && (ins <= INS_xvmod_du)) ||
                           id.idInsIs(INS_xvfdiv_s, INS_xvfdiv_d));
                    if (id.idInsIs(INS_xvdiv_b, INS_xvmod_b, INS_xvdiv_bu, INS_xvmod_bu))
                    {
                        result.insLatency = (24.5f + 52.0f) / 2;
                        result.insThroughput = (0.03f + 0.074f) / 2;
                    }
                    else if (id.idInsIs(INS_xvdiv_h, INS_xvmod_h, INS_xvdiv_hu, INS_xvmod_hu))
                    {
                        result.insLatency = (14.0f + 35.0f) / 2;
                        result.insThroughput = (0.05f + 0.133f) / 2;
                    }
                    else if (id.idInsIs(INS_xvdiv_w, INS_xvmod_w, INS_xvdiv_wu, INS_xvmod_wu))
                    {
                        result.insLatency = (9.0f + 26.0f) / 2;
                        result.insThroughput = (0.065f + 0.22f) / 2;
                    }
                    else if (id.idInsIs(INS_xvdiv_d, INS_xvmod_d, INS_xvdiv_du, INS_xvmod_du))
                    {
                        result.insLatency = (6.6f + 22.0f) / 2;
                        result.insThroughput = (0.08f + 0.33f) / 2;
                    }
                    else
                    {
                        result.insLatency = id.idInsIs(INS_xvfdiv_s)
                            ? (8.6f + 22.4f) / 2
                            : (6.0f + 19.0f) / 2;
                        result.insThroughput = id.idInsIs(INS_xvfdiv_s)
                            ? (0.08f + 0.22f) / 2
                            : (0.095f + 0.33f) / 2;
                    }
                }

                break;
            }
            case DF_A_2R:
            {
                if ((INS_xvclo_b <= ins) && (ins <= INS_xvmsknz_b))
                {
                    result.insLatency = ((INS_xvclo_b <= ins) && (ins <= INS_xvpcnt_d))
                        ? PERFSCORE_LATENCY_2C
                        : PERFSCORE_LATENCY_1C;
                    result.insThroughput = id.idInsIs(INS_xvpcnt_b, INS_xvpcnt_h, INS_xvpcnt_w, INS_xvpcnt_d)
                        ? PERFSCORE_THROUGHPUT_2C
                        : PERFSCORE_THROUGHPUT_4C;
                }
                else if ((INS_xvflogb_s <= ins) && (ins <= INS_xvfclass_d))
                {
                    result.insLatency = id.idInsIs(INS_xvfclass_s, INS_xvfclass_d)
                        ? PERFSCORE_LATENCY_2C
                        : PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if ((INS_xvfrint_s <= ins) && (ins <= INS_xvfcvth_d_s))
                {
                    result.insLatency = id.idInsIs(INS_xvfcvtl_s_h, INS_xvfcvth_s_h, INS_xvfcvtl_d_s, INS_xvfcvth_d_s)
                        ? PERFSCORE_LATENCY_3C
                        : PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if (((INS_xvfrecipe_s <= ins) && (ins <= INS_xvfrsqrte_d)) ||
                         ((INS_xvffint_s_w <= ins) && (ins <= INS_xvftintrneh_l_s)))
                {
                    var isFiveCycle =
                        id.idInsIs(INS_xvffintl_d_w, INS_xvffinth_d_w) ||
                        ((INS_xvftintl_l_s <= ins) && (ins <= INS_xvftintrneh_l_s)) ||
                        ((INS_xvfrecipe_s <= ins) && (ins <= INS_xvfrsqrte_d));
                    result.insLatency = isFiveCycle ? PERFSCORE_LATENCY_5C : PERFSCORE_LATENCY_4C;
                    result.insThroughput = isFiveCycle
                        ? PERFSCORE_THROUGHPUT_2C
                        : PERFSCORE_THROUGHPUT_4C;
                }
                else if ((INS_xvexth_h_b <= ins) && (ins <= INS_xvextl_qu_du))
                {
                    result.insLatency = ((INS_vext2xv_h_b <= ins) && (ins <= INS_xvreplve0_q))
                        ? PERFSCORE_LATENCY_3C
                        : PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if (id.idInsIs(INS_xvfsqrt_s, INS_xvfsqrt_d))
                {
                    result.insLatency = id.idInsIs(INS_xvfsqrt_s)
                        ? (11.0f + 39.0f) / 2
                        : (8.0f + 36.0f) / 2;
                    result.insThroughput = id.idInsIs(INS_xvfsqrt_s)
                        ? (0.054f + 0.222f) / 2
                        : (0.059f + 0.333f) / 2;
                }
                else if (id.idInsIs(INS_xvfrecip_s, INS_xvfrecip_d))
                {
                    result.insLatency = id.idInsIs(INS_xvfrecip_s)
                        ? (11.0f + 27.0f) / 2
                        : (8.0f + 23.0f) / 2;
                    result.insThroughput = id.idInsIs(INS_xvfrecip_s)
                        ? (0.08f + 0.222f) / 2
                        : (0.095f + 0.333f) / 2;
                }
                else
                {
                    assert(id.idInsIs(INS_xvfrsqrt_s, INS_xvfrsqrt_d));
                    result.insLatency = id.idInsIs(INS_xvfrsqrt_s)
                        ? (17.0f + 61.0f) / 2
                        : (11.0f + 54.0f) / 2;
                    result.insThroughput = id.idInsIs(INS_xvfrsqrt_s)
                        ? (0.034f + 0.133f) / 2
                        : (0.038f + 0.222f) / 2;
                }

                break;
            }
            case DF_A_RGX:
            {
                if ((INS_xvldrepl_d <= ins) && (ins <= INS_xvst))
                {
                    result.insLatency = id.idInsIs(INS_xvst)
                        ? PERFSCORE_LATENCY_ZERO
                        : PERFSCORE_LATENCY_5C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert(id.idInsIs(INS_xvinsgr2vr_d, INS_xvinsgr2vr_w));
                    result.insLatency = (PERFSCORE_LATENCY_3C + PERFSCORE_LATENCY_5C) / 2;
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                }

                break;
            }
            case DF_A_GRX:
            {
                result.insLatency = PERFSCORE_LATENCY_5C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }
            case DF_A_2RX:
            {
                if (id.idInsIs(INS_xvinsve0_d))
                {
                    result.insLatency = (PERFSCORE_LATENCY_1C + PERFSCORE_LATENCY_3C) / 2;
                }
                else
                {
                    assert(id.idInsIs(INS_xvrepl128vei_w, INS_xvrepl128vei_d, INS_xvpickve_d));
                    result.insLatency = id.idInsIs(INS_xvpickve_d)
                        ? PERFSCORE_LATENCY_3C
                        : PERFSCORE_LATENCY_1C;
                }

                result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                break;
            }
            case DF_A_2R3IU:
            {
                if ((INS_xvsrlni_b_h <= ins) && (ins <= INS_xvssrarni_bu_h))
                {
                    result.insLatency = PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if (id.idInsIs(
                    INS_xvslli_b, INS_xvsrli_b, INS_xvsrai_b, INS_xvrotri_b, INS_xvrepl128vei_h))
                {
                    result.insLatency = PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else if (id.idInsIs(INS_xvinsve0_w))
                {
                    result.insLatency = (PERFSCORE_LATENCY_1C + PERFSCORE_LATENCY_3C) / 2;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }
                else
                {
                    assert(id.idInsIs(
                        INS_xvsrlri_b, INS_xvsrari_b, INS_xvsllwil_h_b, INS_xvsllwil_hu_bu, INS_xvpickve_w,
                        INS_xvbitclri_b, INS_xvbitseti_b, INS_xvbitrevi_b, INS_xvsat_b, INS_xvsat_bu));
                    result.insLatency = id.idInsIs(INS_xvsrlri_b, INS_xvsrari_b, INS_xvpickve_w)
                        ? PERFSCORE_LATENCY_3C
                        : PERFSCORE_LATENCY_2C;
                    result.insThroughput = id.idInsIs(INS_xvpickve_w)
                        ? PERFSCORE_THROUGHPUT_4C
                        : PERFSCORE_THROUGHPUT_2C;
                }

                break;
            }
            case DF_A_2R4IU:
            case DF_A_2R5I:
            {
                if ((INS_xvsrlri_h <= ins) && (ins <= INS_xvsat_hu))
                {
                    result.insLatency = id.idInsIs(INS_xvsrlri_h, INS_xvsrari_h)
                        ? PERFSCORE_LATENCY_3C
                        : PERFSCORE_LATENCY_2C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert(((INS_xvslli_h <= ins) && (ins <= INS_xvrepl128vei_b)) ||
                           ((INS_xvseqi_b <= ins) && (ins <= INS_xvmini_d)));
                    result.insLatency = id.idInsIs(INS_xvslei_d, INS_xvslti_d, INS_xvmaxi_d, INS_xvmini_d)
                        ? PERFSCORE_LATENCY_2C
                        : PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }

                break;
            }
            case DF_A_2R5IU:
            case DF_A_2R6IU:
            {
                if (id.idInsIs(INS_xvsrlri_w, INS_xvsrari_w, INS_xvsrlri_d, INS_xvsrari_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_3C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if (id.idInsIs(INS_xvfrstpi_b, INS_xvfrstpi_h, INS_xvsllwil_d_w, INS_xvsllwil_du_wu) ||
                         ((INS_xvbitclri_w <= ins) && (ins <= INS_xvsat_wu)) ||
                         ((INS_xvbitclri_d <= ins) && (ins <= INS_xvsat_du)))
                {
                    result.insLatency = PERFSCORE_LATENCY_2C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else if (((INS_xvsrlni_h_w <= ins) && (ins <= INS_xvssrarni_hu_w)) ||
                         ((INS_xvsrlni_w_d <= ins) && (ins <= INS_xvssrarni_wu_d)))
                {
                    result.insLatency = PERFSCORE_LATENCY_4C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert(((INS_xvslei_bu <= ins) && (ins <= INS_xvmini_du)) ||
                           ((INS_xvslli_w <= ins) && (ins <= INS_xvbsrl_v)) ||
                           ((INS_xvslli_d <= ins) && (ins <= INS_xvrotri_d)) ||
                           (ins == INS_xvrotri_w));
                    result.insLatency = id.idInsIs(INS_xvslei_du, INS_xvslti_du, INS_xvmaxi_du, INS_xvmini_du)
                        ? PERFSCORE_LATENCY_2C
                        : PERFSCORE_LATENCY_1C;
                    result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                }

                break;
            }
            case DF_A_2R8IU:
            case DF_A_R13IU:
            {
                result.insLatency = id.idInsIs(INS_xvpermi_d, INS_xvpermi_q)
                    ? PERFSCORE_LATENCY_3C
                    : PERFSCORE_LATENCY_1C;
                result.insThroughput = id.idInsIs(INS_xvbitseli_b)
                    ? PERFSCORE_THROUGHPUT_2C
                    : PERFSCORE_THROUGHPUT_4C;
                break;
            }
            case DF_S_2R8IX:
            case DF_A_RG8IX:
            {
                result.insLatency = PERFSCORE_LATENCY_ZERO;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }
#endif
            default:
            {
                perfScoreUnhandledInstruction(id, ref result);
                break;
            }
        }

        return result;
    }
}
#endif
