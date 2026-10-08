// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64 && (DEBUG || LATE_DISASM)
using System;
using System.Diagnostics;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private const float PERFSCORE_LATENCY_22C = 22.0f;
    private const float PERFSCORE_THROUGHPUT_5X = 0.2f;
    private const float PERFSCORE_THROUGHPUT_6X = 1.0f / 6.0f;
    private const float PERFSCORE_THROUGHPUT_7C = 7.0f;

#if FEATURE_LOOP_ALIGN
    private static bool emitAlignIsPlacedAfterJmpArm64(instrDesc id)
    {
#if DEBUG
        return ((instrDescAlign)id).isPlacedAfterJmp;
#else
        throw new FatalJitException(CORJIT_SKIPPED,
            "Non-debug ARM64 alignment performance scoring requires the native debug-only placement metadata.");
#endif
    }
#endif

    // Latencies and throughput follow the Arm Cortex-A55 Software Optimization Guide:
    // https://static.docs.arm.com/epm128372/20/arm_cortex_a55_software_optimization_guide_v2.pdf
    internal insExecutionCharacteristics getInsExecutionCharacteristics(instrDesc id)
    {
        // Native leaves the result's memory-access kind at None; classification only seeds latency.
        var result = new insExecutionCharacteristics();
        var ins = id.idIns();
        var insFmt = id.idInsFmt();

        getMemoryOperation(id, out var memAccessKind, out var isLocalAccess);

        result.insThroughput = PERFSCORE_THROUGHPUT_ILLEGAL;
        result.insLatency = PERFSCORE_LATENCY_ILLEGAL;

        // Initialize insLatency based upon the instruction's memAccessKind and local access values
        //
        if (memAccessKind == PerfScoreMemoryAccessKind.Read)
        {
            result.insLatency = isLocalAccess ? PERFSCORE_LATENCY_RD_STACK : PERFSCORE_LATENCY_RD_GENERAL;
        }
        else if (memAccessKind == PerfScoreMemoryAccessKind.Write)
        {
            result.insLatency = isLocalAccess ? PERFSCORE_LATENCY_WR_STACK : PERFSCORE_LATENCY_WR_GENERAL;
        }
        else if (memAccessKind == PerfScoreMemoryAccessKind.ReadWrite)
        {
            result.insLatency = isLocalAccess ? PERFSCORE_LATENCY_RD_WR_STACK : PERFSCORE_LATENCY_RD_WR_GENERAL;
        }

        switch (insFmt)
        {
                //
                //  Branch Instructions
                //

            case IF_BI_0A:                                      // b, bl_local
            case IF_BI_0C:                                      // bl, b_tail
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C; // but is Dual Issue
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_BI_0B: // beq, bne, bge, blt, bgt, ble, ...
            case IF_BI_1A: // cbz, cbnz
            case IF_BI_1B: // tbz, tbnz
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_LARGEJMP: // bcc + b
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_BR_1B: // blr, br_tail
            {
                if (ins == INS_blr)
                {
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                    result.insLatency = PERFSCORE_LATENCY_1C;
                    break;
                }
                // otherwise we should have a br_tail instruction
                assert(ins == INS_br_tail);
                goto case IF_BR_0A;
            }
            case IF_BR_0A: // retaa, retab
            case IF_BR_1A: // ret, br
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            //
            //  Arithmetic and logical instructions
            //

            // ALU, basic
            case IF_DR_3A: // add, adds, adc, adcs, and, ands, bic, bics,
                           // eon, eor, orn, orr, sub, subs, sbc, sbcs
                           // asr, asrv, lsl, lslv, lsr, lsrv, ror, rorv
                           // sdiv, udiv, mul, smull, smulh, umull, umulh, mneg
            case IF_DR_2A: // cmp, cmn, tst
            {
                switch (ins)
                {
                    case INS_mul:
                    case INS_smull:
                    case INS_umull:
                    case INS_mneg:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_smulh:
                    case INS_umulh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_6C;
                        break;
                    }

                    case INS_sdiv:
                    case INS_udiv:
                    {
                        if (id.idOpSize() == EA_4BYTE)
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                            result.insLatency = PERFSCORE_LATENCY_12C;
                            break;
                        }
                        else
                        {
                            assert(id.idOpSize() == EA_8BYTE);
                            result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                            result.insLatency = PERFSCORE_LATENCY_20C;
                            break;
                        }
                    }

                    case INS_add:
                    case INS_adds:
                    case INS_adc:
                    case INS_adcs:
                    case INS_and:
                    case INS_ands:
                    case INS_bic:
                    case INS_bics:
                    case INS_eon:
                    case INS_eor:
                    case INS_orn:
                    case INS_orr:
                    case INS_sub:
                    case INS_subs:
                    case INS_sbc:
                    case INS_sbcs:
                    case INS_asr:
                    case INS_lsl:
                    case INS_lsr:
                    case INS_ror:
                    case INS_cmp:
                    case INS_cmn:
                    case INS_tst:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        break;
                    }

                    case INS_asrv:
                    case INS_lslv:
                    case INS_lsrv:
                    case INS_rorv:
                        // variable shift by register
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        break;
                    }

                    case INS_crc32b:
                    case INS_crc32h:
                    case INS_crc32cb:
                    case INS_crc32ch:
                    case INS_crc32x:
                    case INS_crc32cx:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_crc32w:
                    case INS_crc32cw:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        break;
                    }

                    case INS_smaddl:
                    case INS_smsubl:
                    case INS_smnegl:
                    case INS_umaddl:
                    case INS_umsubl:
                    case INS_umnegl:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            // ALU, basic immediate
            case IF_DI_1A: // cmp, cmn
            case IF_DI_1C: // tst
            case IF_DI_1D: // mov reg, imm(N,r,s)
            case IF_DI_1E: // adr, adrp
            case IF_DI_1F: // ccmp, ccmn
            case IF_DI_2A: // add, adds, suv, subs
            case IF_DI_2C: // and, ands, eor, orr
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_DR_2D: // cinc, cinv, cneg
            case IF_DR_2E: // mov, neg, mvn, negs
            case IF_DI_1B: // mov, movk, movn, movz
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_LARGEADR: // adrp + add
            case IF_LARGELDC: // adrp + ldr
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            // ALU, shift by immediate
            case IF_DR_3B: // add, adds, and, ands, bic, bics,
                           // eon, eor, orn, orr, sub, subs
            case IF_DR_2B: // cmp, cmn, tst
            case IF_DR_2F: // neg, negs, mvn
            case IF_DI_2B: // ror
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            // ALU, extend, scale
            case IF_DR_3C: // add, adc, and, bic, eon, eor, orn, orr, sub, sbc
            case IF_DR_2C: // cmp
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DV_2U: // sha
            {
                if (ins == INS_sha1h)
                {
                    result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                    result.insLatency = PERFSCORE_LATENCY_2C;
                }
                else
                {
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                    result.insLatency = PERFSCORE_LATENCY_2C;
                }
                break;
            }

            case IF_DV_2V:
            {
                if (ins == INS_sm4e)
                {
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                    result.insLatency = PERFSCORE_LATENCY_4C;
                }
                else
                {
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                    result.insLatency = PERFSCORE_LATENCY_2C;
                }
                break;
            }
            // ALU, Conditional select
            case IF_DR_1D: // cset, csetm
            case IF_DR_3D: // csel, csinc, csinv, csneg
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            // ALU, Conditional compare
            case IF_DR_2I: // ccmp , ccmn
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            // Multiply accumulate
            case IF_DR_4A: // madd, msub, smaddl, smsubl, umaddl, umsubl
            {
                if (id.idOpSize() == EA_4BYTE)
                {
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                    result.insLatency = PERFSCORE_LATENCY_3C;
                    break;
                }
                else
                {
                    assert(id.idOpSize() == EA_8BYTE);
                    result.insThroughput = PERFSCORE_THROUGHPUT_5C;
                    result.insLatency = PERFSCORE_LATENCY_3C;
                    break;
                }
            }

            // Miscellaneous Data Preocessing instructions
            case IF_DR_3E: // extr
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DR_2H: // sxtb, sxth, sxtw, uxtb, uxth, sha1h
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_DI_2D: // lsl, lsr, asr, sbfm, bfm, ubfm, sbfiz, bfi, ubfiz, sbfx, bfxil, ubfx
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DR_2G: // mov sp, cls, clz, rbit, rev16, rev32, rev
            {
                if (ins == INS_rbit)
                {
                    result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                    result.insLatency = PERFSCORE_LATENCY_2C;
                    break;
                }
                else
                {
                    result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                    result.insLatency = PERFSCORE_LATENCY_1C;
                    break;
                }
            }

                //
                //  Load/Store Instructions
                //

            case IF_LS_1A: // ldr, ldrsw (literal, pc relative immediate)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_LS_2A: // ldr, ldrsw, ldrb, ldrh, ldrsb, ldrsh, str, strb, strh (no immediate)
                           // ldar, ldarb, ldarh, ldapr, ldaprb, ldaprh, ldxr, ldxrb, ldxrh,
                           // ldaxr, ldaxrb, ldaxrh, stlr, stlrb, stlrh

            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                // ToDo: store release have 2/4 cycle latency
                break;
            }

            case IF_LS_2B: // ldr, ldrsw, ldrb, ldrh, ldrsb, ldrsh, str, strb, strh (scaled immediate)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_LS_2C: // ldr, ldrsw, ldrb, ldrh, ldrsb, ldrsh, str, strb, strh
                           // ldur, ldurb, ldurh, ldursb, ldursh, ldursw, stur, sturb, sturh
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_LS_3A: // ldr, ldrsw, ldrb, ldrh, ldrsb, ldrsh, str, strb strh (register extend, scale 2,4,8)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_LS_3B: // ldp, ldpsw, ldnp, stp, stnp  (load/store pair zero offset)
            case IF_LS_3C: // load/store pair with offset pre/post inc
            {
                if (memAccessKind == PerfScoreMemoryAccessKind.Read)
                {
                    // ldp, ldpsw, ldnp
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                    if (emitIGisInEpilog(emitCurIG) && (ins == INS_ldp))
                    {
                        // Reduce latency for ldp instructions in the epilog
                        //
                        result.insLatency = PERFSCORE_LATENCY_2C;
                    }
                    else if (id.idOpSize() == EA_8BYTE) // X-form
                    {
                        // the X-reg variant has an extra cycle of latency
                        // and two cycle throughput
                        result.insLatency += 1.0f;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                    }
                }
                else // store instructions
                {
                    // stp, stnp
                    assert(memAccessKind == PerfScoreMemoryAccessKind.Write);
                    result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                }
                break;
            }

            case IF_LS_3D: // stxr, stxrb, stxrh, stlxr, stlxrb, srlxrh
                // Store exclusive register, returning status
            {
                assert(emitInsIsStore(ins));
                // @ToDo - find out the actual latency
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = MathF.Max(PERFSCORE_LATENCY_4C, result.insLatency);
                break;
            }

            case IF_LS_3E: //  ARMv8.1 LSE Atomics
            {
                if (memAccessKind == PerfScoreMemoryAccessKind.Write)
                {
                    // staddb, staddlb, staddh, staddlh, stadd. staddl
                    result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                    result.insLatency = PERFSCORE_LATENCY_2C;
                }
                else
                {
                    assert(memAccessKind == PerfScoreMemoryAccessKind.ReadWrite);
                    result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                    result.insLatency = MathF.Max(PERFSCORE_LATENCY_3C, result.insLatency);
                }
                break;
            }

            case IF_LS_2D:
            case IF_LS_2E:
            case IF_LS_3F:
                // Load/Store multiple structures
                // Load single structure and replicate
            {
                switch (ins)
                {
                    case INS_ld1:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_3C;
                        }
                        else
                        {
                            // Q-form
                            assert(id.idOpSize() == EA_16BYTE);
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        break;
                    }

                    case INS_ld1_2regs:
                    case INS_ld2:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        else
                        {
                            // Q-form
                            assert(id.idOpSize() == EA_16BYTE);
                            result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                            result.insLatency = PERFSCORE_LATENCY_6C;
                        }
                        break;
                    }

                    case INS_ld1_3regs:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                            result.insLatency = PERFSCORE_LATENCY_5C;
                        }
                        else
                        {
                            // Q-form
                            assert(id.idOpSize() == EA_16BYTE);
                            result.insThroughput = PERFSCORE_THROUGHPUT_6C;
                            result.insLatency = PERFSCORE_LATENCY_8C;
                        }
                        break;
                    }

                    case INS_ld1_4regs:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                            result.insLatency = PERFSCORE_LATENCY_6C;
                        }
                        else
                        {
                            // Q-form
                            assert(id.idOpSize() == EA_16BYTE);
                            result.insThroughput = PERFSCORE_THROUGHPUT_8C;
                            result.insLatency = PERFSCORE_LATENCY_10C;
                        }
                        break;
                    }

                    case INS_ld3:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            if (optGetElemsize(id.idInsOpt()) == EA_4BYTE)
                            {
                                // S
                                result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                                result.insLatency = PERFSCORE_LATENCY_5C;
                            }
                            else
                            {
                                // B/H
                                result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                                result.insLatency = PERFSCORE_LATENCY_6C;
                            }
                        }
                        else
                        {
                            // Q-form
                            assert(id.idOpSize() == EA_16BYTE);
                            if ((optGetElemsize(id.idInsOpt()) == EA_4BYTE) ||
                                (optGetElemsize(id.idInsOpt()) == EA_8BYTE))
                            {
                                // S/D
                                result.insThroughput = PERFSCORE_THROUGHPUT_6C;
                                result.insLatency = PERFSCORE_LATENCY_8C;
                            }
                            else
                            {
                                // B/H
                                result.insThroughput = PERFSCORE_THROUGHPUT_7C;
                                result.insLatency = PERFSCORE_LATENCY_9C;
                            }
                        }
                        break;
                    }

                    case INS_ld4:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            if (optGetElemsize(id.idInsOpt()) == EA_4BYTE)
                            {
                                // S
                                result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                                result.insLatency = PERFSCORE_LATENCY_6C;
                            }
                            else
                            {
                                // B/H
                                result.insThroughput = PERFSCORE_THROUGHPUT_5C;
                                result.insLatency = PERFSCORE_LATENCY_7C;
                            }
                        }
                        else
                        {
                            // Q-form
                            assert(id.idOpSize() == EA_16BYTE);
                            if ((optGetElemsize(id.idInsOpt()) == EA_4BYTE) ||
                                (optGetElemsize(id.idInsOpt()) == EA_8BYTE))
                            {
                                // S/D
                                result.insThroughput = PERFSCORE_THROUGHPUT_8C;
                                result.insLatency = PERFSCORE_LATENCY_10C;
                            }
                            else
                            {
                                // B/H
                                result.insThroughput = PERFSCORE_THROUGHPUT_9C;
                                result.insLatency = PERFSCORE_LATENCY_11C;
                            }
                        }
                        break;
                    }

                    case INS_ld1r:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_ld2r:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        else
                        {
                            // B/H/S
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_3C;
                        }
                        break;
                    }

                    case INS_ld3r:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D
                            result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                            result.insLatency = PERFSCORE_LATENCY_5C;
                        }
                        else
                        {
                            // B/H/S
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        break;
                    }

                    case INS_ld4r:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D
                            result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                            result.insLatency = PERFSCORE_LATENCY_6C;
                        }
                        else
                        {
                            // B/H/S
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        break;
                    }

                    case INS_st1:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        break;
                    }

                    case INS_st1_2regs:
                    case INS_st2:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_1C;
                        }
                        else
                        {
                            // Q-form
                            assert(id.idOpSize() == EA_16BYTE);
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_2C;
                        }
                        break;
                    }

                    case INS_st1_3regs:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_2C;
                        }
                        else
                        {
                            // Q-form
                            assert(id.idOpSize() == EA_16BYTE);
                            result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                            result.insLatency = PERFSCORE_LATENCY_3C;
                        }
                        break;
                    }

                    case INS_st1_4regs:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_2C;
                        }
                        else
                        {
                            // Q-form
                            assert(id.idOpSize() == EA_16BYTE);
                            result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        break;
                    }

                    case INS_st3:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_st4:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                            result.insLatency = PERFSCORE_LATENCY_3C;
                        }
                        else
                        {
                            assert(id.idOpSize() == EA_16BYTE);
                            if (optGetElemsize(id.idInsOpt()) == EA_8BYTE)
                            {
                                // D
                                result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                                result.insLatency = PERFSCORE_LATENCY_4C;
                            }
                            else
                            {
                                // B/H/S
                                result.insThroughput = PERFSCORE_THROUGHPUT_5C;
                                result.insLatency = PERFSCORE_LATENCY_5C;
                            }
                        }
                        break;
                    }

                    default:
                    {
                        throw new UnreachableException();
                    }
                }
                break;
            }

            case IF_LS_2F:
            case IF_LS_2G:
            case IF_LS_3G:
                // Load/Store single structure
            {
                switch (ins)
                {
                    case INS_ld1:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_ld2:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        else
                        {
                            // B/H/S
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_3C;
                        }
                        break;
                    }

                    case INS_ld3:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D
                            result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                            result.insLatency = PERFSCORE_LATENCY_5C;
                        }
                        else
                        {
                            // B/H/S
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        break;
                    }

                    case INS_ld4:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D
                            result.insThroughput = PERFSCORE_THROUGHPUT_4C;
                            result.insLatency = PERFSCORE_LATENCY_6C;
                        }
                        else
                        {
                            // B/H/S
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        break;
                    }

                    case INS_st1:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        break;
                    }

                    case INS_st2:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D
                            result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                            result.insLatency = PERFSCORE_LATENCY_2C;
                        }
                        else
                        {
                            // B/H/S
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_1C;
                        }
                        break;
                    }

                    case INS_st3:
                    case INS_st4:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    default:
                    {
                        throw new UnreachableException();
                    }
                }
                break;
            }

            case IF_PC_0A: // autia1716, autiasp, autib1716, autibsp, autibz, autiaz, pacia1716, paciasp, pacib1716,
                           // pacibsp, pacibz, paciaz, xpaclri
            case IF_PC_1A: // autiza, autizb, paciza, pacizb, xpacd, xpaci
            case IF_PC_2A: // autia, autib, pacia, pacib
            {
                switch (ins)
                {
                    case INS_xpacd:
                    case INS_xpaci:
                    case INS_xpaclri:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    default:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_5C;
                        break;
                    }
                }
                break;
            }

            case IF_SN_0A: // nop, yield, align
            {
#if FEATURE_LOOP_ALIGN
                if (id.idIns() == INS_align)
                {
                    if ((id.idInsOpt() == INS_OPTS_NONE) || emitAlignIsPlacedAfterJmpArm64(id))
                    {
                        // Either we're not going to generate 'align' instruction, or the 'align'
                        // instruction is placed immediately after unconditional jmp.
                        // In both cases, don't count for PerfScore.

                        result.insThroughput = PERFSCORE_THROUGHPUT_ZERO;
                        result.insLatency = PERFSCORE_LATENCY_ZERO;
                        break;
                    }
                }
                else
#endif
                if (ins == INS_yield)
                {
                    // @ToDo - find out the actual latency, match x86/x64 for now
                    result.insThroughput = PERFSCORE_THROUGHPUT_140C;
                    result.insLatency = PERFSCORE_LATENCY_140C;
                }
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_ZERO;
                break;
            }

            case IF_SI_0B: // dmb, dsb, isb
                // @ToDo - find out the actual latency
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_10C;
                result.insLatency = PERFSCORE_LATENCY_10C;
                break;
            }

            case IF_DV_2J: // fcvt  Vd Vn
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_4C;
                break;
            }

            case IF_DV_2K: // fcmp  Vd Vn
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_DV_1A: // fmov - immediate (scalar)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_DV_1B: // fmov, orr, bic, movi, mvni  (immediate vector)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_DV_1C: // fcmp vn, #0.0
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_DV_2A: // fabs, fneg, fsqrt, fcvtXX, frintX, scvtf, ucvtf, fcmXX (vector)
            {
                switch (ins)
                {
                    case INS_fabs:
                    case INS_fneg:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = (id.idOpSize() == EA_8BYTE) ? PERFSCORE_LATENCY_2C : PERFSCORE_LATENCY_3C / 2;
                        break;
                    }

                    case INS_fsqrt:
                    {
                        if ((id.idInsOpt() == INS_OPTS_2S) || (id.idInsOpt() == INS_OPTS_4S))
                        {
                            // S-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                            result.insLatency = PERFSCORE_LATENCY_11C;
                        }
                        else
                        {
                            // D-form
                            assert(id.idInsOpt() == INS_OPTS_2D);
                            result.insThroughput = PERFSCORE_THROUGHPUT_6C;
                            result.insLatency = PERFSCORE_LATENCY_18C;
                        }
                        break;
                    }

                    case INS_fcvtas:
                    case INS_fcvtau:
                    case INS_fcvtms:
                    case INS_fcvtmu:
                    case INS_fcvtns:
                    case INS_fcvtnu:
                    case INS_fcvtps:
                    case INS_fcvtpu:
                    case INS_fcvtzs:
                    case INS_fcvtzu:
                    case INS_frinta:
                    case INS_frinti:
                    case INS_frintm:
                    case INS_frintn:
                    case INS_frintp:
                    case INS_frintx:
                    case INS_frintz:
                    case INS_scvtf:
                    case INS_ucvtf:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_fcmeq:
                    case INS_fcmge:
                    case INS_fcmgt:
                    case INS_fcmle:
                    case INS_fcmlt:
                    case INS_frecpe:
                    case INS_frsqrte:
                    case INS_urecpe:
                    case INS_ursqrte:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_fcvtl:
                    case INS_fcvtl2:
                    case INS_fcvtn:
                    case INS_fcvtn2:
                    case INS_fcvtxn:
                    case INS_fcvtxn2:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_DV_2G: // fmov, fabs, fneg, fsqrt, fcmXX, fcvtXX, frintX, scvtf, ucvtf (scalar)
            {
                switch (ins)
                {
                    case INS_fmov:
                        // FP move, vector register
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        break;
                    }

                    case INS_fabs:
                    case INS_fneg:

                    case INS_fcvtas:
                    case INS_fcvtau:
                    case INS_fcvtms:
                    case INS_fcvtmu:
                    case INS_fcvtns:
                    case INS_fcvtnu:
                    case INS_fcvtps:
                    case INS_fcvtpu:
                    case INS_fcvtzs:
                    case INS_fcvtzu:
                    case INS_scvtf:
                    case INS_ucvtf:

                    case INS_frinta:
                    case INS_frinti:
                    case INS_frintm:
                    case INS_frintn:
                    case INS_frintp:
                    case INS_frintx:
                    case INS_frintz:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_fcvtxn:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_fcmeq:
                    case INS_fcmge:
                    case INS_fcmgt:
                    case INS_fcmle:
                    case INS_fcmlt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_frecpe:
                    case INS_frecpx:
                    case INS_frsqrte:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_fsqrt:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_19C;
                            result.insLatency = PERFSCORE_LATENCY_22C;
                        }
                        else
                        {
                            // S-form or H-form
                            assert((id.idOpSize() == EA_4BYTE) || (id.idOpSize() == EA_2BYTE));
                            result.insThroughput = PERFSCORE_THROUGHPUT_9C;
                            result.insLatency = PERFSCORE_LATENCY_12C;
                        }
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_DV_2Q: // faddp, fmaxnmp, fmaxp, fminnmp, fminp (scalar)
            case IF_DV_2R: // fmaxnmv, fmaxv, fminnmv, fminv
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_4C;
                break;
            }

            case IF_DV_2S: // addp (scalar)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_DV_3B: // fadd, fsub, fdiv, fmul, fmulx, fmla, fmls, fmin, fminnm, fmax, fmaxnm, fabd, fcmXX
                           // faddp, fmaxnmp, fmaxp, fminnmp, fminp, addp (vector)
            {
                switch (ins)
                {
                    case INS_fmin:
                    case INS_fminnm:
                    case INS_fmax:
                    case INS_fmaxnm:
                    case INS_fabd:
                    case INS_fadd:
                    case INS_fsub:
                    case INS_fmul:
                    case INS_fmulx:
                    case INS_fmla:
                    case INS_fmls:
                    case INS_frecps:
                    case INS_frsqrts:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_faddp:
                    case INS_fmaxnmp:
                    case INS_fmaxp:
                    case INS_fminnmp:
                    case INS_fminp:
                    {
                        if (id.idOpSize() == EA_16BYTE)
                        {
                            // Q-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        else
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        break;
                    }

                    case INS_facge:
                    case INS_facgt:
                    case INS_fcmeq:
                    case INS_fcmge:
                    case INS_fcmgt:
                    case INS_fcmle:
                    case INS_fcmlt:
                    {
                        if (id.idOpSize() == EA_16BYTE)
                        {
                            // Q-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_2C;
                        }
                        else
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                            result.insLatency = PERFSCORE_LATENCY_2C;
                        }
                        break;
                    }

                    case INS_fdiv:
                    {
                        if ((id.idInsOpt() == INS_OPTS_2S) || (id.idInsOpt() == INS_OPTS_4S))
                        {
                            // S-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_10C;
                            result.insLatency = PERFSCORE_LATENCY_13C;
                        }
                        else
                        {
                            // D-form
                            assert(id.idInsOpt() == INS_OPTS_2D);
                            result.insThroughput = PERFSCORE_THROUGHPUT_10C;
                            result.insLatency = PERFSCORE_LATENCY_22C;
                        }
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_DV_3AI: // mul, mla, mls (vector by element)
            case IF_DV_3BI: // fmul, fmulx, fmla, fmls (vector by element)
            case IF_DV_3EI: // sqdmlal, sqdmlsl, sqdmulh, sqdmull (scalar by element)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_4C;
                break;
            }

            case IF_DV_4A: // fmadd, fmsub, fnmadd, fnsub (scalar)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_4C;
                break;
            }

            case IF_DV_4B: // eor3, bcax, sm3ss1
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DV_3D: // fadd, fsub, fdiv, fmul, fmulx, fmin, fminnm, fmax, fmaxnm, fabd, fcmXX (scalar)
            {
                switch (ins)
                {
                    case INS_fadd:
                    case INS_fsub:
                    case INS_fabd:
                    case INS_fmax:
                    case INS_fmaxnm:
                    case INS_fmin:
                    case INS_fminnm:
                    case INS_fmul:
                    case INS_fmulx:
                    case INS_fnmul:
                    case INS_frecps:
                    case INS_frsqrts:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_facge:
                    case INS_facgt:
                    case INS_fcmeq:
                    case INS_fcmge:
                    case INS_fcmgt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_fdiv:
                    {
                        if (id.idOpSize() == EA_8BYTE)
                        {
                            // D-form
                            result.insThroughput = PERFSCORE_THROUGHPUT_6C;
                            result.insLatency = PERFSCORE_LATENCY_15C;
                        }
                        else
                        {
                            // S-form or H-form
                            assert((id.idOpSize() == EA_4BYTE) || (id.idOpSize() == EA_2BYTE));
                            result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                            result.insLatency = PERFSCORE_LATENCY_10C;
                        }
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_DV_2H: // fmov, fcvtXX - to general
                // fmov : FP transfer to general register
                // fcvtaXX : FP convert from vector to general
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_DV_2I: // fmov, Xcvtf - from general
            {
                switch (ins)
                {
                    case INS_fmov:
                        // FP transfer from general register
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_scvtf:
                    case INS_ucvtf:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_5C;
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_DV_3C: // mov,and, bic, eor, mov,mvn, orn, bsl, bit, bif,
                           // tbl, tbx (vector)
            {
                switch (ins)
                {
                    case INS_tbl:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        break;
                    }
                    case INS_tbl_2regs:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3X;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }
                    case INS_tbl_3regs:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_4X;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }
                    case INS_tbl_4regs:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3X;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }
                    case INS_tbx:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3X;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }
                    case INS_tbx_2regs:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_4X;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }
                    case INS_tbx_3regs:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_5X;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }
                    case INS_tbx_4regs:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_6X;
                        result.insLatency = PERFSCORE_LATENCY_5C;
                        break;
                    }
                    default:
                        // All other instructions
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        break;
                    }
                }
                break;
            }

            case IF_DV_2E: // mov, dup (scalar)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DV_2F: // mov, ins (element)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DV_2B: // smov, umov - to general)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DV_2C: // mov, dup, ins - from general)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                if (ins == INS_dup)
                {
                    result.insLatency = PERFSCORE_LATENCY_3C;
                }
                else
                {
                    assert((ins == INS_ins) || (ins == INS_mov));
                    result.insLatency = PERFSCORE_LATENCY_2C;
                }
                break;
            }

            case IF_DV_2D: // dup (dvector)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DV_3A: // (vector)
                // add, sub, mul, mla, mls, cmeq, cmge, cmgt, cmhi, cmhs, ctst,
                // pmul, saba, uaba, sabd, uabd, umin, uminp, umax, umaxp, smin, sminp, smax, smaxp
            {
                switch (ins)
                {
                    case INS_add:
                    case INS_sub:
                    case INS_cmeq:
                    case INS_cmge:
                    case INS_cmgt:
                    case INS_cmhi:
                    case INS_cmhs:
                    case INS_shadd:
                    case INS_shsub:
                    case INS_srhadd:
                    case INS_srshl:
                    case INS_sshl:
                    case INS_smax:
                    case INS_smaxp:
                    case INS_smin:
                    case INS_sminp:
                    case INS_umax:
                    case INS_umaxp:
                    case INS_umin:
                    case INS_uminp:
                    case INS_uhadd:
                    case INS_uhsub:
                    case INS_urhadd:
                    case INS_urshl:
                    case INS_ushl:
                    case INS_uzp1:
                    case INS_uzp2:
                    case INS_zip1:
                    case INS_zip2:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_trn1:
                    case INS_trn2:
                    {
                        if (id.idInsOpt() == INS_OPTS_2D)
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        }
                        else
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        }

                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_addp:
                    case INS_cmtst:
                    case INS_pmul:
                    case INS_sabd:
                    case INS_sqadd:
                    case INS_sqsub:
                    case INS_uabd:
                    case INS_uqadd:
                    case INS_uqsub:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_mla:
                    case INS_mls:
                    case INS_mul:
                    case INS_sqdmulh:
                    case INS_sqrdmulh:
                    case INS_sqrshl:
                    case INS_sqshl:
                    case INS_uqrshl:
                    case INS_uqshl:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_saba:
                    case INS_uaba:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sdot:
                    case INS_udot:
                    {
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        if (id.idOpSize() == EA_16BYTE)
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        }
                        else
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        }
                        break;
                    }

                    case INS_addhn:
                    case INS_addhn2:
                    case INS_sabdl:
                    case INS_sabdl2:
                    case INS_saddl2:
                    case INS_saddl:
                    case INS_saddw:
                    case INS_saddw2:
                    case INS_ssubl:
                    case INS_ssubl2:
                    case INS_ssubw:
                    case INS_ssubw2:
                    case INS_subhn:
                    case INS_subhn2:
                    case INS_uabdl:
                    case INS_uabdl2:
                    case INS_uaddl:
                    case INS_uaddl2:
                    case INS_uaddw:
                    case INS_uaddw2:
                    case INS_usubl:
                    case INS_usubl2:
                    case INS_usubw:
                    case INS_usubw2:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_raddhn:
                    case INS_raddhn2:
                    case INS_rsubhn:
                    case INS_rsubhn2:
                    case INS_sabal:
                    case INS_sabal2:
                    case INS_uabal:
                    case INS_uabal2:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_smlal:
                    case INS_smlal2:
                    case INS_smlsl:
                    case INS_smlsl2:
                    case INS_smull:
                    case INS_smull2:
                    case INS_sqdmlal:
                    case INS_sqdmlal2:
                    case INS_sqdmlsl:
                    case INS_sqdmlsl2:
                    case INS_sqdmull:
                    case INS_sqdmull2:
                    case INS_sqrdmlah:
                    case INS_sqrdmlsh:
                    case INS_umlal:
                    case INS_umlal2:
                    case INS_umlsl:
                    case INS_umlsl2:
                    case INS_umull:
                    case INS_umull2:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_pmull:
                    case INS_pmull2:
                    {
                        if ((id.idInsOpt() == INS_OPTS_8B) || (id.idInsOpt() == INS_OPTS_16B))
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_3C;
                        }
                        else
                        {
                            // Crypto polynomial (64x64) multiply long
                            assert((id.idInsOpt() == INS_OPTS_1D) || (id.idInsOpt() == INS_OPTS_2D));
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_2C;
                        }
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_DV_3DI: // fmul, fmulx, fmla, fmls (scalar by element)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_4C;
                break;
            }

            case IF_DV_3E: // add, sub, cmeq, cmge, cmgt, cmhi, cmhs, ctst, (scalar)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DV_3G: // ext
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DV_3H: // sha512h, sha512h2, sha512su1, rax1, sm3partw1, sm3partw2, sm4ekey
            {
                switch (ins)
                {
                    case INS_sm4ekey:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }
                }
                break;
            }

            case IF_DV_3I: // xar
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DV_2L: // abs, neg, cmeq, cmge, cmgt, cmle, cmlt (scalar)
            case IF_DV_2M: // (vector)
                // abs, neg, mvn, not, cmeq, cmge, cmgt, cmle, cmlt,
                // addv, saddlv,  uaddlv, smaxv, sminv, umaxv, uminv
                // cls, clz, cnt, rbit, rev16, rev32, rev64,
                // xtn, xtn2, shll, shll2
            {
                switch (ins)
                {
                    case INS_abs:
                    case INS_sqneg:
                    case INS_suqadd:
                    case INS_usqadd:
                    {
                        if (id.idOpSize() == EA_16BYTE)
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        }
                        else
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        }

                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_addv:
                    case INS_saddlv:
                    case INS_uaddlv:
                    case INS_cls:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_sminv:
                    case INS_smaxv:
                    case INS_uminv:
                    case INS_umaxv:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_cmeq:
                    case INS_cmge:
                    case INS_cmgt:
                    case INS_cmle:
                    case INS_cmlt:

                    case INS_clz:
                    case INS_cnt:
                    case INS_rbit:
                    case INS_rev16:
                    case INS_rev32:
                    case INS_rev64:
                    case INS_xtn:
                    case INS_xtn2:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_mvn:
                    case INS_not:
                    case INS_neg:
                    case INS_shll:
                    case INS_shll2:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        break;
                    }

                    case INS_sqabs:
                    case INS_sqxtn:
                    case INS_sqxtn2:
                    case INS_sqxtun:
                    case INS_sqxtun2:
                    case INS_uqxtn:
                    case INS_uqxtn2:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_DV_2N: // sshr, ssra, srshr, srsra, shl, ushr, usra, urshr, ursra, sri, sli (shift by immediate -
                           // scalar)
            case IF_DV_2O: // sshr, ssra, srshr, srsra, shl, ushr, usra, urshr, ursra, sri, sli (shift by immediate -
                           // vector)
                           // sshll, sshll2, ushll, ushll2, shrn, shrn2, rshrn, rshrn2, sxrl, sxl2, uxtl, uxtl2
            {
                switch (ins)
                {
                    case INS_shl:
                    case INS_shrn:
                    case INS_shrn2:
                    case INS_sli:
                    case INS_sri:
                    case INS_sshr:
                    case INS_ushr:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_shll:
                    case INS_shll2:
                    case INS_sshll:
                    case INS_sshll2:
                    case INS_ushll:
                    case INS_ushll2:
                    case INS_sxtl:
                    case INS_sxtl2:
                    case INS_uxtl:
                    case INS_uxtl2:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_rshrn:
                    case INS_rshrn2:
                    case INS_srshr:
                    case INS_sqshrn:
                    case INS_sqshrn2:
                    case INS_ssra:
                    case INS_urshr:
                    case INS_uqshrn:
                    case INS_uqshrn2:
                    case INS_usra:
                    {
                        if (id.idOpSize() == EA_16BYTE)
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_3C;
                        }
                        else
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                            result.insLatency = PERFSCORE_LATENCY_3C;
                        }
                        break;
                    }

                    case INS_srsra:
                    case INS_ursra:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sqrshrn:
                    case INS_sqrshrn2:
                    case INS_sqrshrun:
                    case INS_sqrshrun2:
                    case INS_sqshrun:
                    case INS_sqshrun2:
                    case INS_sqshl:
                    case INS_sqshlu:
                    case INS_uqrshrn:
                    case INS_uqrshrn2:
                    case INS_uqshl:
                    {
                        if (id.idOpSize() == EA_16BYTE)
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        else
                        {
                            result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                            result.insLatency = PERFSCORE_LATENCY_4C;
                        }
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_DV_2P: // aese, aesd, aesmc, aesimc
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_DV_3F: // sha1c, sha1m, sha1p, sha1su0, sha256h, sha256h2, sha256su1 (vector)
            {
                switch (ins)
                {
                    case INS_sha1su0:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_sha256su0:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_sha1c:
                    case INS_sha1m:
                    case INS_sha1p:
                    case INS_sha256h:
                    case INS_sha256h2:
                    case INS_sha256su1:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SI_0A: // brk   imm16
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_SR_1A:
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_DV_2T: // addv, saddlv, smaxv, sminv, uaddlv, umaxv, uminv
            {
                switch (ins)
                {
                    case INS_addv:
                    case INS_saddlv:
                    case INS_uaddlv:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_smaxv:
                    case INS_sminv:
                    case INS_umaxv:
                    case INS_uminv:
                    case INS_sha256h2:
                    case INS_sha256su1:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sadalp:
                    case INS_uadalp:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_saddlp:
                    case INS_uaddlp:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    default:
                        // all other instructions
                    {
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            default:
                // fallback to SVE instructions
            {
                getInsSveExecutionCharacteristics(id, ref result);
                break;
            }
        }

        return result;
    }
}
#endif
