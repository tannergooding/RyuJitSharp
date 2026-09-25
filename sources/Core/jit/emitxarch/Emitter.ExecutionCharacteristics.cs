// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if (DEBUG || LATE_DISASM) && TARGET_AMD64
    internal insExecutionCharacteristics getInsExecutionCharacteristics(instrDesc id)
    {
        var ins = id.idIns();
        var insFmt = id.idInsFmt();
        var opSize = id.idOpSize();
        var memFmt = getMemoryOperation(id);
        var memThroughput = PERFSCORE_THROUGHPUT_ILLEGAL;
        var memLatency = PERFSCORE_LATENCY_ILLEGAL;
        var memAccessKind = PerfScoreMemoryAccessKind.None;

        switch (memFmt)
        {
            case IF_SRD:
            {
                memThroughput = PERFSCORE_THROUGHPUT_RD;
                memLatency = PERFSCORE_LATENCY_RD_STACK;
                memAccessKind = PerfScoreMemoryAccessKind.Read;
                break;
            }

            case IF_SWR:
            {
                memThroughput = PERFSCORE_THROUGHPUT_WR;
                memLatency = PERFSCORE_LATENCY_WR_STACK;
                memAccessKind = PerfScoreMemoryAccessKind.Write;
                break;
            }

            case IF_SRW:
            {
                memThroughput = PERFSCORE_THROUGHPUT_RW;
                memLatency = PERFSCORE_LATENCY_RD_WR_STACK;
                memAccessKind = PerfScoreMemoryAccessKind.ReadWrite;
                break;
            }

            case IF_MRD:
            {
                memThroughput = PERFSCORE_THROUGHPUT_RD;
                memLatency = PERFSCORE_LATENCY_RD_CONST_ADDR;
                memAccessKind = PerfScoreMemoryAccessKind.Read;
                break;
            }

            case IF_MWR:
            {
                memThroughput = PERFSCORE_THROUGHPUT_WR;
                memLatency = PERFSCORE_LATENCY_WR_CONST_ADDR;
                memAccessKind = PerfScoreMemoryAccessKind.Write;
                break;
            }

            case IF_MRW:
            {
                memThroughput = PERFSCORE_THROUGHPUT_RW;
                memLatency = PERFSCORE_LATENCY_RD_WR_CONST_ADDR;
                memAccessKind = PerfScoreMemoryAccessKind.ReadWrite;
                break;
            }

            case IF_ARD:
            {
                memThroughput = PERFSCORE_THROUGHPUT_RD;
                memLatency = PERFSCORE_LATENCY_RD_GENERAL;
                memAccessKind = PerfScoreMemoryAccessKind.Read;
                break;
            }

            case IF_AWR:
            {
                memThroughput = PERFSCORE_THROUGHPUT_WR;
                memLatency = PERFSCORE_LATENCY_WR_GENERAL;
                memAccessKind = PerfScoreMemoryAccessKind.Write;
                break;
            }

            case IF_ARW:
            {
                memThroughput = PERFSCORE_THROUGHPUT_RW;
                memLatency = PERFSCORE_LATENCY_RD_WR_GENERAL;
                memAccessKind = PerfScoreMemoryAccessKind.ReadWrite;
                break;
            }

            case IF_NONE:
            {
                memThroughput = PERFSCORE_THROUGHPUT_ZERO;
                memLatency = PERFSCORE_LATENCY_ZERO;
                break;
            }

            default:
            {
                assert(false, "Unhandled insFmt for switch (memFmt)");
                memThroughput = PERFSCORE_THROUGHPUT_ZERO;
                memLatency = PERFSCORE_LATENCY_ZERO;
                break;
            }
        }

        var insThroughput = PERFSCORE_THROUGHPUT_ILLEGAL;
        var insLatency = PERFSCORE_LATENCY_ILLEGAL;

        switch (ins)
        {
            case INS_align:
            {
                assert(memFmt == IF_NONE);

#if FEATURE_LOOP_ALIGN
#if DEBUG
                var skipAlign = id.idCodeSize() == 0 || ((instrDescAlign)id).isPlacedAfterJmp;
#else
                var skipAlign = id.idCodeSize() == 0;
                if (!skipAlign)
                {
                    throw new FatalJitException(CORJIT_SKIPPED,
                        "Non-debug alignment performance scoring requires the native debug-only placement metadata.");
                }
#endif
                if (skipAlign)
                {
                    // Either we're not going to generate 'align' instruction, or the 'align'
                    // instruction is placed immediately after unconditional jmp.
                    // In both cases, don't count for PerfScore.

                    insThroughput = PERFSCORE_THROUGHPUT_ZERO;
                    insLatency = PERFSCORE_LATENCY_ZERO;
                    break;
                }
#endif

                insThroughput = PERFSCORE_THROUGHPUT_4X;
                insLatency = PERFSCORE_LATENCY_ZERO;
                break;
            }

            case INS_lea:
            {
                // uops.info
                insThroughput = PERFSCORE_THROUGHPUT_2X; // one or two components
                insLatency = PERFSCORE_LATENCY_1C;

                if (insFmt == IF_RWR_LABEL)
                {
                    // RIP relative addressing
                    //
                    // - throughput is only 1 per cycle
                    //
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                }
                else if (insFmt != IF_RWR_SRD)
                {
                    if (id.idAddr().iiaAddrMode.amIndxReg != REG_NA)
                    {
                        var baseReg = id.idAddr().iiaAddrMode.amBaseReg;
                        if (baseReg != REG_NA)
                        {
                            var dsp = emitGetInsAmdAny(id);

                            if ((dsp != 0) || baseRegisterRequiresDisplacement(baseReg))
                            {
                                // three components
                                //
                                // - throughput is only 1 per cycle
                                //
                                insThroughput = PERFSCORE_THROUGHPUT_1C;

                                if (baseRegisterRequiresDisplacement(baseReg) || id.idIsDspReloc())
                                {
                                    // Increased Latency for these cases
                                    //  - see https://reviews.llvm.org/D32277
                                    //
                                    insLatency = PERFSCORE_LATENCY_3C;
                                }
                            }
                        }
                    }
                }
                break;
            }

            case INS_div:
            {
                // The integer divide instructions have long latencies
                if (opSize == EA_8BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_52C;
                    insLatency = PERFSCORE_LATENCY_62C;
                }
                else
                {
                    assert(opSize == EA_4BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_6C;
                    insLatency = PERFSCORE_LATENCY_26C;
                }
                break;
            }

            case INS_idiv:
            {
                // The integer divide instructions have long latenies
                if (opSize == EA_8BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_57C;
                    insLatency = PERFSCORE_LATENCY_69C;
                }
                else
                {
                    assert(opSize == EA_4BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_6C;
                    insLatency = PERFSCORE_LATENCY_26C;
                }
                break;
            }

            case INS_shld:
            case INS_shrd:
            {
                insLatency = PERFSCORE_LATENCY_3C;

                if (insFmt == IF_RRW_RRD_CNS)
                {
                    // ins   reg, reg, cns
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                }
                else
                {
                    assert(memAccessKind == PerfScoreMemoryAccessKind.Write); // _SHF form never emitted
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                break;
            }

            case INS_jmp:
            {
                if (emitInstHasNoCode(id))
                {
                    // a removed jmp to the next instruction
                    insThroughput = PERFSCORE_THROUGHPUT_ZERO;
                    insLatency = PERFSCORE_LATENCY_ZERO;
                }
                else
                {
                    // branch to a constant address
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_BRANCH_DIRECT;
                }
                break;
            }

            case INS_call:
            {
                // uops.info
                insLatency = PERFSCORE_LATENCY_ZERO;

                switch (insFmt)
                {
                    case IF_LABEL:
                    {
                        insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }

                    case IF_METHOD:
                    {
                        insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }

                    case IF_METHPTR:
                    {
                        insThroughput = PERFSCORE_THROUGHPUT_3C;
                        break;
                    }

                    case IF_SRD:
                    case IF_ARD:
                    case IF_MRD:
                    {
                        insThroughput = PERFSCORE_THROUGHPUT_3C;
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
                break;
            }

            case INS_ret:
            {
                insLatency = PERFSCORE_LATENCY_ZERO;

                if (insFmt == IF_CNS)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                }
                else
                {
                    assert(insFmt == IF_NONE);
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                }
                break;
            }

            case INS_xchg:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                insLatency = (memFmt == IF_NONE) ? PERFSCORE_LATENCY_1C : PERFSCORE_LATENCY_23C;
                break;
            }

            case INS_movd32:
            case INS_movd64:
            case INS_movq:
            case INS_vmovw:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                insLatency = PERFSCORE_LATENCY_3C;

                if (memAccessKind == PerfScoreMemoryAccessKind.Read)
                {
                    // The reads have twice the throughput of the register to register variants
                    insThroughput = PERFSCORE_THROUGHPUT_2X;
                }
                else if (id.idReg1().IsFltReg && id.idReg2().IsFltReg)
                {
                    // movq   xmm, xmm
                    insThroughput = PERFSCORE_THROUGHPUT_3X;
                    insLatency = PERFSCORE_LATENCY_1C;
                }
                break;
            }

            case INS_movss:
            case INS_movsd_simd:
            case INS_movddup:
            case INS_vmovsh:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                insLatency = PERFSCORE_LATENCY_1C;

                if (memAccessKind == PerfScoreMemoryAccessKind.Read)
                {
                    // The reads have twice the throughput of the register to register variants
                    insThroughput = PERFSCORE_THROUGHPUT_2X;
                }
                break;
            }

            case INS_vaddsh:
            case INS_vsubsh:
            case INS_vmulsh:
            case INS_vfmadd213sh:
            case INS_vmaxsh:
            case INS_vminsh:
            case INS_vcvtsh2ss:
            {
                insLatency = PERFSCORE_LATENCY_4C;
                insThroughput = PERFSCORE_THROUGHPUT_2X;
                break;
            }

            case INS_vdivsh:
            {
                insLatency = PERFSCORE_LATENCY_14C;
                insThroughput = PERFSCORE_THROUGHPUT_4C;
                break;
            }

            case INS_vsqrtsh:
            {
                insLatency = PERFSCORE_LATENCY_14C;
                insThroughput = PERFSCORE_THROUGHPUT_4P5C;
                break;
            }

            case INS_vrsqrtsh:
            case INS_vcomish:
            case INS_vucomish:
            case INS_vrcpsh:
            {
                insLatency = PERFSCORE_LATENCY_4C;
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case INS_vrndscalesh:
            {
                insLatency = PERFSCORE_LATENCY_8C;
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case INS_vcvtss2sh:
            {
                insLatency = PERFSCORE_LATENCY_6C;
                insThroughput = PERFSCORE_THROUGHPUT_1P5X;
                break;
            }

            case INS_vcvtsd2sh:
            {
                insLatency = PERFSCORE_LATENCY_7C;
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case INS_vcvtsh2sd:
            {
                insLatency = PERFSCORE_LATENCY_10C;
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case INS_vcvtsi2sh32:
            case INS_vcvtsi2sh64:
            case INS_vcvtsh2si32:
            case INS_vcvtsh2si64:
            case INS_vcvtusi2sh32:
            case INS_vcvtusi2sh64:
            case INS_vcvtsh2usi32:
            case INS_vcvtsh2usi64:
            case INS_vcvttsh2si32:
            case INS_vcvttsh2si64:
            case INS_vcvttsh2usi32:
            case INS_vcvttsh2usi64:
            {
                insLatency = PERFSCORE_LATENCY_7C;
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case INS_vpmovdb:
            case INS_vpmovdw:
            case INS_vpmovqb:
            case INS_vpmovqd:
            case INS_vpmovqw:
            case INS_vpmovsdb:
            case INS_vpmovsdw:
            case INS_vpmovsqb:
            case INS_vpmovsqd:
            case INS_vpmovsqw:
            case INS_vpmovswb:
            case INS_vpmovusdb:
            case INS_vpmovusdw:
            case INS_vpmovusqb:
            case INS_vpmovusqd:
            case INS_vpmovusqw:
            case INS_vpmovuswb:
            case INS_vpmovwb:
            {
                insThroughput = PERFSCORE_THROUGHPUT_2C;
                insLatency = (opSize >= EA_32BYTE) ? PERFSCORE_LATENCY_4C : PERFSCORE_LATENCY_2C;
                break;
            }

            case INS_vrcp14pd:
            case INS_vrcp14ps:
            case INS_vrcp14sd:
            case INS_vrcp14ss:
            case INS_vrsqrt14pd:
            case INS_vrsqrt14sd:
            case INS_vrsqrt14ps:
            case INS_vrsqrt14ss:
            {
                if (opSize == EA_64BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_8C;
                }
                else
                {
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                    insLatency = PERFSCORE_LATENCY_4C;
                }
                break;
            }

            case INS_vpconflictd:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_6C;
                    insLatency = PERFSCORE_LATENCY_12C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_10C;
                    insLatency = PERFSCORE_LATENCY_16C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);

                    insThroughput = PERFSCORE_THROUGHPUT_19C;
                    insLatency = PERFSCORE_LATENCY_26C;
                }
                break;
            }

            case INS_vpconflictq:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_4C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_6C;
                    insLatency = PERFSCORE_LATENCY_12C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);

                    insThroughput = PERFSCORE_THROUGHPUT_10C;
                    insLatency = PERFSCORE_LATENCY_16C;
                }
                break;
            }

            case INS_cvttss2si32:
            case INS_cvttss2si64:
            case INS_cvtss2si32:
            case INS_cvtss2si64:
            case INS_vcvtss2usi32:
            case INS_vcvtss2usi64:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                insLatency = (opSize == EA_8BYTE) ? PERFSCORE_LATENCY_8C : PERFSCORE_LATENCY_7C;
                break;
            }

            case INS_pslld:
            case INS_psllw:
            case INS_psllq:
            case INS_psrlw:
            case INS_psrld:
            case INS_psrlq:
            case INS_psrad:
            case INS_psraw:
            case INS_vpsraq:
            {
                if (insFmt == IF_RWR_CNS)
                {
                    insLatency = PERFSCORE_LATENCY_1C;
                    insThroughput = PERFSCORE_THROUGHPUT_2X;
                }
                else
                {
                    insLatency = (opSize >= EA_32BYTE) ? PERFSCORE_LATENCY_4C : PERFSCORE_LATENCY_2C;
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                }
                break;
            }

            case INS_vblendmps:
            case INS_vblendmpd:
            case INS_vpblendmd:
            case INS_vpblendmq:
            {
                if (opSize >= EA_64BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2X;
                }
                else
                {
                    insThroughput = PERFSCORE_THROUGHPUT_3X;
                }
                insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case INS_vpblendmb:
            case INS_vpblendmw:
            {
                if (opSize >= EA_64BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2X;
                }
                else
                {
                    insThroughput = PERFSCORE_THROUGHPUT_3X;
                }
                insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case INS_bswap:
            {
                insThroughput = PERFSCORE_THROUGHPUT_2X;
                insLatency = (opSize == EA_8BYTE) ? PERFSCORE_LATENCY_2C : PERFSCORE_LATENCY_1C;
                break;
            }

            case INS_pmovmskb:
            case INS_movmskpd:
            case INS_movmskps:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;

                if (opSize >= EA_32BYTE)
                {
                    insLatency = (ins == INS_pmovmskb) ? PERFSCORE_LATENCY_4C : PERFSCORE_LATENCY_5C;
                }
                else
                {
                    insLatency = PERFSCORE_LATENCY_3C;
                }
                break;
            }

            case INS_pmovsxbw:
            case INS_pmovsxbd:
            case INS_pmovsxbq:
            case INS_pmovsxwd:
            case INS_pmovsxwq:
            case INS_pmovsxdq:
            case INS_pmovzxbw:
            case INS_pmovzxbd:
            case INS_pmovzxbq:
            case INS_pmovzxwd:
            case INS_pmovzxwq:
            case INS_pmovzxdq:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                insLatency = (opSize >= EA_32BYTE) ? PERFSCORE_LATENCY_3C : PERFSCORE_LATENCY_1C;
                break;
            }

            case INS_ptest:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                insLatency = (opSize >= EA_32BYTE) ? PERFSCORE_LATENCY_6C : PERFSCORE_LATENCY_4C;
                break;
            }

            case INS_cvtsd2ss:
            case INS_cvtps2pd:
            case INS_cvtpd2dq:
            case INS_cvtdq2pd:
            case INS_cvtpd2ps:
            case INS_cvttpd2dq:
            case INS_vcvtpd2udq:
            case INS_vcvtph2ps:
            case INS_vcvtps2ph:
            case INS_vcvtps2qq:
            case INS_vcvtps2uqq:
            case INS_vcvtqq2ps:
            case INS_vcvttpd2udq:
            case INS_vcvttps2qq:
            case INS_vcvttps2uqq:
            case INS_vcvtudq2pd:
            case INS_vcvtuqq2ps:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                insLatency = (opSize >= EA_32BYTE) ? PERFSCORE_LATENCY_7C : PERFSCORE_LATENCY_5C;
                break;
            }

            case INS_vtestps:
            case INS_vtestpd:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                insLatency = (opSize >= EA_32BYTE) ? PERFSCORE_LATENCY_5C : PERFSCORE_LATENCY_3C;
                break;
            }

            case INS_vpbroadcastb:
            case INS_vpbroadcastw:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                insLatency = (opSize >= EA_32BYTE) ? PERFSCORE_LATENCY_3C : PERFSCORE_LATENCY_1C;
                break;
            }

            case INS_vpbroadcastd:
            case INS_vpbroadcastq:
            case INS_vbroadcasti32x4:
            case INS_vbroadcastf32x4:
            case INS_vbroadcastf64x2:
            case INS_vbroadcasti64x2:
            case INS_vbroadcastf64x4:
            case INS_vbroadcasti64x4:
            case INS_vbroadcastf32x2:
            case INS_vbroadcasti32x2:
            case INS_vbroadcastf32x8:
            case INS_vbroadcasti32x8:
            case INS_vbroadcastss:
            case INS_vbroadcastsd:
            {
                insLatency = (opSize >= EA_32BYTE) ? PERFSCORE_LATENCY_3C : PERFSCORE_LATENCY_1C;

                if (memAccessKind == PerfScoreMemoryAccessKind.None)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                }
                else
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2X;
                }
                break;
            }

            case INS_pinsrb:
            case INS_pinsrw:
            case INS_pinsrd:
            case INS_pinsrq:
            {
                if (memAccessKind == PerfScoreMemoryAccessKind.None)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_4C;
                }
                else
                {
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                    insLatency = PERFSCORE_LATENCY_3C;
                }
                break;
            }

            case INS_vpgatherdd:
            case INS_vgatherdps:
            case INS_vpgatherdd_msk:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_11C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_4C;
                    insLatency = PERFSCORE_LATENCY_13C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_8C;
                    insLatency = PERFSCORE_LATENCY_17C;
                }
                break;
            }

            case INS_vpgatherdq:
            case INS_vpgatherqd:
            case INS_vpgatherqq:
            case INS_vgatherdpd:
            case INS_vgatherqps:
            case INS_vgatherqpd:
            case INS_vgatherdpd_msk:
            case INS_vgatherdps_msk:
            case INS_vgatherqpd_msk:
            case INS_vgatherqps_msk:
            case INS_vpgatherdq_msk:
            case INS_vpgatherqd_msk:
            case INS_vpgatherqq_msk:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                    insLatency = PERFSCORE_LATENCY_9C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_11C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_4C;
                    insLatency = PERFSCORE_LATENCY_13C;
                }
                break;
            }

            case INS_vpscatterdd_msk:
            case INS_vscatterdps_msk:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_4C;
                    insLatency = PERFSCORE_LATENCY_7C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_8C;
                    insLatency = PERFSCORE_LATENCY_9C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_16C;
                    insLatency = PERFSCORE_LATENCY_13C;
                }
                break;
            }

            case INS_vpscatterdq_msk:
            case INS_vpscatterqd_msk:
            case INS_vpscatterqq_msk:
            case INS_vscatterdpd_msk:
            case INS_vscatterqpd_msk:
            case INS_vscatterqps_msk:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_5C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_4C;
                    insLatency = PERFSCORE_LATENCY_7C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_8C;
                    insLatency = PERFSCORE_LATENCY_9C;
                }
                break;
            }

            case INS_vpshldd:
            case INS_vpshldq:
            case INS_vpshldvd:
            case INS_vpshldvq:
            case INS_vpshldvw:
            case INS_vpshldw:
            case INS_vpshrdd:
            case INS_vpshrdq:
            case INS_vpshrdvd:
            case INS_vpshrdvq:
            case INS_vpshrdvw:
            case INS_vpshrdw:
            {
                insThroughput = (opSize >= EA_64BYTE) ? PERFSCORE_THROUGHPUT_1C : PERFSCORE_THROUGHPUT_2X;
                insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case INS_vcvtdq2ph:
            case INS_vcvtudq2ph:
            {
                if (opSize >= EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_9C;
                }
                else
                {
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                    insLatency = PERFSCORE_LATENCY_7C;
                }
                break;
            }

            case INS_vcvtne2ps2bf16:
            case INS_vcvtneps2bf16:
            {
                insThroughput = (opSize >= EA_64BYTE) ? PERFSCORE_THROUGHPUT_2C : PERFSCORE_THROUGHPUT_1C;
                insLatency = PERFSCORE_LATENCY_8C;
                break;
            }

            case INS_vcvtph2psx:
            case INS_vcvtps2phx:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;
                insLatency = (opSize >= EA_32BYTE) ? PERFSCORE_LATENCY_8C : PERFSCORE_LATENCY_6C;
                break;
            }

            case INS_vcvtph2qq:
            case INS_vcvtph2uqq:
            case INS_vcvttph2qq:
            case INS_vcvttph2uqq:
            {
                insThroughput = (opSize >= EA_64BYTE) ? PERFSCORE_THROUGHPUT_2C : PERFSCORE_THROUGHPUT_1C;
                insLatency = PERFSCORE_LATENCY_10C;
                break;
            }

            case INS_vcvtqq2ph:
            case INS_vcvtuqq2ph:
            {
                if (opSize >= EA_64BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_10C;
                }
                else
                {
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                    insLatency = PERFSCORE_LATENCY_8C;
                }
                break;
            }

            case INS_divpd:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_4C;
                    insLatency = PERFSCORE_LATENCY_13C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_8C;
                    insLatency = PERFSCORE_LATENCY_13C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_16C;
                    insLatency = PERFSCORE_LATENCY_23C;
                }
                break;
            }

            case INS_divps:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_3C;
                    insLatency = PERFSCORE_LATENCY_11C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_5C;
                    insLatency = PERFSCORE_LATENCY_11C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_10C;
                    insLatency = PERFSCORE_LATENCY_18C;
                }
                break;
            }

            case INS_vdivph:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_8C;
                    insLatency = PERFSCORE_LATENCY_31C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_16C;
                    insLatency = PERFSCORE_LATENCY_31C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_32C;
                    insLatency = PERFSCORE_LATENCY_41C;
                }
                break;
            }

            case INS_vrcpph:
            {
                if (opSize >= EA_64BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_6C;
                }
                else
                {
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                    insLatency = PERFSCORE_LATENCY_4C;
                }
                break;
            }

            case INS_vrsqrtph:
            {
                if (opSize >= EA_64BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_2C;
                    insLatency = PERFSCORE_LATENCY_7C;
                }
                else
                {
                    insThroughput = PERFSCORE_THROUGHPUT_1C;
                    insLatency = PERFSCORE_LATENCY_5C;
                }
                break;
            }

            case INS_sqrtpd:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_4C;
                    insLatency = PERFSCORE_LATENCY_16C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_8C;
                    insLatency = PERFSCORE_LATENCY_16C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_16C;
                    insLatency = PERFSCORE_LATENCY_28C;
                }
                break;
            }

            case INS_sqrtps:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_3C;
                    insLatency = PERFSCORE_LATENCY_12C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_6C;
                    insLatency = PERFSCORE_LATENCY_12C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_12C;
                    insLatency = PERFSCORE_LATENCY_20C;
                }
                break;
            }

            case INS_vsqrtph:
            {
                if (opSize == EA_16BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_9C;
                    insLatency = PERFSCORE_LATENCY_33C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insThroughput = PERFSCORE_THROUGHPUT_18C;
                    insLatency = PERFSCORE_LATENCY_33C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insThroughput = PERFSCORE_THROUGHPUT_36C;
                    insLatency = PERFSCORE_LATENCY_45C;
                }
                break;
            }

            case INS_vp2intersectd:
            case INS_vp2intersectq:
            {
                insThroughput = PERFSCORE_THROUGHPUT_1C;

                if (opSize == EA_16BYTE)
                {
                    insLatency = PERFSCORE_LATENCY_3C;
                }
                else if (opSize == EA_32BYTE)
                {
                    insLatency = PERFSCORE_LATENCY_4C;
                }
                else
                {
                    assert(opSize == EA_64BYTE);
                    insLatency = PERFSCORE_LATENCY_6C;
                }
                break;
            }

            default:
            {
                assert((uint)ins < (uint)insThroughputInfos.Length);
                assert((uint)ins < (uint)insLatencyInfos.Length);

                insThroughput = insThroughputInfos[(int)ins];
                insLatency = insLatencyInfos[(int)ins];
                break;
            }
        }

        var result = new insExecutionCharacteristics();
#pragma warning disable CA1508 // Retain the native memory-cost sentinel assertions.
        assert(memThroughput != PERFSCORE_THROUGHPUT_ILLEGAL);
        assert(memLatency != PERFSCORE_LATENCY_ILLEGAL);
#pragma warning restore CA1508

        if (insLatency == PERFSCORE_LATENCY_ILLEGAL || insThroughput == PERFSCORE_THROUGHPUT_ILLEGAL)
        {
            perfScoreUnhandledInstruction(id, ref result);
            result.insMemoryAccessKind = memAccessKind;
            return result;
        }

        if (memAccessKind != PerfScoreMemoryAccessKind.None)
        {
            if (IsSimdInstruction(ins))
            {
                // SIMD and floating-point indirections are more expensive as vector width grows.
                if (opSize >= EA_64BYTE)
                {
                    memLatency += PERFSCORE_LATENCY_2C;
                }
                else if (opSize >= EA_32BYTE)
                {
                    memLatency += PERFSCORE_LATENCY_1C;
                }
            }
            else if (insLatency < memLatency)
            {
                // The memory access amortizes most general-purpose instruction costs.
                insLatency = PERFSCORE_LATENCY_ZERO;
            }
        }

        // Reciprocal throughput is limited by the higher cost; memory and operation latencies add.
        result.insThroughput = MathF.Max(insThroughput, memThroughput);
        result.insLatency = insLatency + memLatency;
        result.insMemoryAccessKind = memAccessKind;
        return result;
    }
#elif DEBUG || LATE_DISASM
    internal insExecutionCharacteristics getInsExecutionCharacteristics(instrDesc id)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Execution characteristics outside AMD64 are not implemented.");
    }
#endif
}
