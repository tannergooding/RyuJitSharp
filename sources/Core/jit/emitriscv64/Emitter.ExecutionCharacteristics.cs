// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64 && (DEBUG || LATE_DISASM)
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    internal insExecutionCharacteristics getInsExecutionCharacteristicsRiscV64(instrDesc id)
    {
        var result = new insExecutionCharacteristics
        {
            insThroughput = PERFSCORE_LATENCY_1C,
            insLatency = PERFSCORE_THROUGHPUT_1C,
            insMemoryAccessKind = PerfScoreMemoryAccessKind.None,
        };

        var codeSize = id.idCodeSize();
        assert((codeSize >= 2) && ((codeSize % 2) == 0));
        var immediateBuildingCost = ((codeSize / sizeof(uint)) - 1) * PERFSCORE_LATENCY_1C;
        var ins = id.idIns();
        assert(ins != INS_invalid);
        if ((ins == INS_lea) || (id.idInsOpt() == INS_OPTS_I))
        {
            result.insLatency += immediateBuildingCost;
            result.insThroughput += immediateBuildingCost;
            return result;
        }

        var opcode = GetMajorOpcode(emitInsCode(ins));
        switch (opcode)
        {
            case MajorOpcode.OpImm:
            case MajorOpcode.OpImm32:
            case MajorOpcode.Lui:
            case MajorOpcode.Auipc:
            {
                result.insLatency = PERFSCORE_LATENCY_1C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                break;
            }
            case MajorOpcode.Op:
            case MajorOpcode.Op32:
            case MajorOpcode.JrJalrMvAdd:
            case MajorOpcode.MiscAlu:
            {
                if (id.idInsIs(INS_mul, INS_mulh, INS_mulhu, INS_mulhsu, INS_mulw))
                {
                    result.insLatency = PERFSCORE_LATENCY_3C;
                }
                else if (id.idInsIs(INS_div, INS_divu, INS_rem, INS_remu))
                {
                    result.insLatency = result.insThroughput = (6.0f + 68.0f) / 2;
                }
                else if (id.idInsIs(INS_divw, INS_divuw, INS_remw, INS_remuw))
                {
                    result.insLatency = result.insThroughput = (6.0f + 36.0f) / 2;
                }
                else
                {
                    result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                }
                break;
            }
            case MajorOpcode.MAdd:
            case MajorOpcode.MSub:
            case MajorOpcode.NmAdd:
            case MajorOpcode.NmSub:
            case MajorOpcode.OpFp:
            {
                if (id.idInsIs(INS_fadd_s, INS_fsub_s, INS_fmul_s, INS_fmadd_s, INS_fmsub_s,
                    INS_fnmadd_s, INS_fnmsub_s))
                {
                    result.insLatency = PERFSCORE_LATENCY_5C;
                }
                else if (id.idInsIs(INS_fadd_d, INS_fsub_d, INS_fmul_d, INS_fmadd_d, INS_fmsub_d,
                    INS_fnmadd_d, INS_fnmsub_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_7C;
                }
                else if (id.idInsIs(INS_fdiv_s))
                {
                    result.insLatency = (9.0f + 36.0f) / 2;
                    result.insThroughput = (8.0f + 33.0f) / 2;
                }
                else if (id.idInsIs(INS_fsqrt_s))
                {
                    result.insLatency = (9.0f + 28.0f) / 2;
                    result.insThroughput = (8.0f + 33.0f) / 2;
                }
                else if (id.idInsIs(INS_fdiv_d))
                {
                    result.insLatency = (9.0f + 58.0f) / 2;
                    result.insThroughput = (8.0f + 58.0f) / 2;
                }
                else if (id.idInsIs(INS_fsqrt_d))
                {
                    result.insLatency = (9.0f + 57.0f) / 2;
                    result.insThroughput = (8.0f + 58.0f) / 2;
                }
                else if (id.idInsIs(INS_feq_s, INS_fle_s, INS_flt_s, INS_fclass_s, INS_feq_d, INS_fle_d,
                    INS_flt_d, INS_fclass_d, INS_fcvt_w_s, INS_fcvt_l_s, INS_fcvt_s_l, INS_fcvt_wu_s,
                    INS_fcvt_lu_s, INS_fcvt_s_lu, INS_fcvt_w_d, INS_fcvt_l_d, INS_fcvt_wu_d, INS_fcvt_lu_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_4C;
                }
                else if (id.idInsIs(INS_fcvt_d_l, INS_fcvt_d_lu, INS_fmv_d_x))
                {
                    result.insLatency = PERFSCORE_LATENCY_6C;
                }
                else if (id.idInsIs(INS_fmv_x_w, INS_fmv_x_d))
                {
                    result.insLatency = PERFSCORE_LATENCY_1C;
                }
                else
                {
                    result.insLatency = PERFSCORE_LATENCY_2C;
                }
                break;
            }
            case MajorOpcode.Amo:
            {
                result.insLatency = result.insThroughput = PERFSCORE_LATENCY_5C;
                result.insMemoryAccessKind = PerfScoreMemoryAccessKind.ReadWrite;
                break;
            }
            case MajorOpcode.Branch:
            {
                result.insLatency = result.insThroughput =
                    immediateBuildingCost + (PERFSCORE_LATENCY_1C + PERFSCORE_LATENCY_6C) / 2;
                break;
            }
            case MajorOpcode.Jalr:
            {
                result.insLatency = result.insThroughput =
                    immediateBuildingCost + (PERFSCORE_LATENCY_1C + PERFSCORE_LATENCY_5C) / 2;
                break;
            }
            case MajorOpcode.Jal:
            {
                result.insLatency = result.insThroughput =
                    immediateBuildingCost + (PERFSCORE_LATENCY_1C + PERFSCORE_LATENCY_2C) / 2;
                break;
            }
            case MajorOpcode.System:
            {
                var code = id.idAddr().iiaGetInstrEncode();
                var funct3 = (code >> 12) & 0b111;
                if (funct3 != 0)
                {
                    var isCsrrw = (funct3 & 0b11) == 0b01;
                    var isZero = ((code >> 15) & 0b11111) == 0;
                    var isWrite = isCsrrw || !isZero;
                    result.insLatency = isWrite ? PERFSCORE_LATENCY_7C : PERFSCORE_LATENCY_1C;
                }
                break;
            }
            case MajorOpcode.Load:
            case MajorOpcode.Store:
            case MajorOpcode.LoadFp:
            case MajorOpcode.StoreFp:
            {
                var isLoad = (opcode == MajorOpcode.Load) || (opcode == MajorOpcode.LoadFp);
                result.insLatency = isLoad ? PERFSCORE_LATENCY_2C : PERFSCORE_LATENCY_4C;
                if (isLoad)
                {
                    var log2Size = (emitInsCode(ins) >> 12) & 0b11;
                    if (log2Size < 2)
                    {
                        result.insLatency += PERFSCORE_LATENCY_1C;
                    }
                }

                var baseReg = id.idReg2();
                if ((baseReg != REG_SP) && (baseReg != REG_FP))
                {
                    result.insLatency += PERFSCORE_LATENCY_1C;
                }
                result.insThroughput += immediateBuildingCost;
                result.insMemoryAccessKind = isLoad
                    ? PerfScoreMemoryAccessKind.Read
                    : PerfScoreMemoryAccessKind.Write;
                break;
            }
            case MajorOpcode.MiscMem:
            {
                result.insLatency = PERFSCORE_LATENCY_5C;
                result.insThroughput = PERFSCORE_THROUGHPUT_5C;
                break;
            }
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
