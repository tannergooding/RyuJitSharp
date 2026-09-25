// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    internal enum PerfScoreMemoryAccessKind : uint
    {
        None,
        Read,
        Write,
        ReadWrite,
    }

#if DEBUG || LATE_DISASM
    private const float PERFSCORE_THROUGHPUT_ILLEGAL = -1024.0f;
    private const float PERFSCORE_THROUGHPUT_ZERO = 0.0f;
    private const float PERFSCORE_THROUGHPUT_1P5X = 2.0f / 3.0f;
    private const float PERFSCORE_THROUGHPUT_4X = 0.25f;
    private const float PERFSCORE_THROUGHPUT_3X = 1.0f / 3.0f;
    private const float PERFSCORE_THROUGHPUT_2X = 0.5f;
    private const float PERFSCORE_THROUGHPUT_1C = 1.0f;
    private const float PERFSCORE_THROUGHPUT_2C = 2.0f;
    private const float PERFSCORE_THROUGHPUT_3C = 3.0f;
    private const float PERFSCORE_THROUGHPUT_4C = 4.0f;
    private const float PERFSCORE_THROUGHPUT_4P5C = 4.5f;
    private const float PERFSCORE_THROUGHPUT_5C = 5.0f;
    private const float PERFSCORE_THROUGHPUT_6C = 6.0f;
    private const float PERFSCORE_THROUGHPUT_8C = 8.0f;
    private const float PERFSCORE_THROUGHPUT_9C = 9.0f;
    private const float PERFSCORE_THROUGHPUT_10C = 10.0f;
    private const float PERFSCORE_THROUGHPUT_12C = 12.0f;
    private const float PERFSCORE_THROUGHPUT_13C = 13.0f;
    private const float PERFSCORE_THROUGHPUT_16C = 16.0f;
    private const float PERFSCORE_THROUGHPUT_18C = 18.0f;
    private const float PERFSCORE_THROUGHPUT_19C = 19.0f;
    private const float PERFSCORE_THROUGHPUT_25C = 25.0f;
    private const float PERFSCORE_THROUGHPUT_32C = 32.0f;
    private const float PERFSCORE_THROUGHPUT_33C = 33.0f;
    private const float PERFSCORE_THROUGHPUT_36C = 36.0f;
    private const float PERFSCORE_THROUGHPUT_50C = 50.0f;
    private const float PERFSCORE_THROUGHPUT_52C = 52.0f;
    private const float PERFSCORE_THROUGHPUT_57C = 57.0f;
    private const float PERFSCORE_THROUGHPUT_140C = 140.0f;

    private const float PERFSCORE_LATENCY_ILLEGAL = -1024.0f;
    private const float PERFSCORE_LATENCY_ZERO = 0.0f;
    private const float PERFSCORE_LATENCY_1C = 1.0f;
    private const float PERFSCORE_LATENCY_2C = 2.0f;
    private const float PERFSCORE_LATENCY_3C = 3.0f;
    private const float PERFSCORE_LATENCY_4C = 4.0f;
    private const float PERFSCORE_LATENCY_5C = 5.0f;
    private const float PERFSCORE_LATENCY_6C = 6.0f;
    private const float PERFSCORE_LATENCY_7C = 7.0f;
    private const float PERFSCORE_LATENCY_8C = 8.0f;
    private const float PERFSCORE_LATENCY_9C = 9.0f;
    private const float PERFSCORE_LATENCY_10C = 10.0f;
    private const float PERFSCORE_LATENCY_11C = 11.0f;
    private const float PERFSCORE_LATENCY_12C = 12.0f;
    private const float PERFSCORE_LATENCY_13C = 13.0f;
    private const float PERFSCORE_LATENCY_14C = 14.0f;
    private const float PERFSCORE_LATENCY_15C = 15.0f;
    private const float PERFSCORE_LATENCY_16C = 16.0f;
    private const float PERFSCORE_LATENCY_17C = 17.0f;
    private const float PERFSCORE_LATENCY_18C = 18.0f;
    private const float PERFSCORE_LATENCY_20C = 20.0f;
    private const float PERFSCORE_LATENCY_23C = 23.0f;
    private const float PERFSCORE_LATENCY_26C = 26.0f;
    private const float PERFSCORE_LATENCY_28C = 28.0f;
    private const float PERFSCORE_LATENCY_31C = 31.0f;
    private const float PERFSCORE_LATENCY_33C = 33.0f;
    private const float PERFSCORE_LATENCY_41C = 41.0f;
    private const float PERFSCORE_LATENCY_45C = 45.0f;
    private const float PERFSCORE_LATENCY_62C = 62.0f;
    private const float PERFSCORE_LATENCY_69C = 69.0f;
    private const float PERFSCORE_LATENCY_105C = 105.0f;
    private const float PERFSCORE_LATENCY_140C = 140.0f;
    private const float PERFSCORE_LATENCY_400C = 400.0f;

    private const float PERFSCORE_LATENCY_BRANCH_DIRECT = 1.0f;
    private const float PERFSCORE_LATENCY_BRANCH_COND = 2.0f;
    private const float PERFSCORE_LATENCY_BRANCH_INDIRECT = 2.0f;
    private const float PERFSCORE_THROUGHPUT_RD = PERFSCORE_THROUGHPUT_2X;
    private const float PERFSCORE_THROUGHPUT_WR = PERFSCORE_THROUGHPUT_1C;
    private const float PERFSCORE_THROUGHPUT_RW = PERFSCORE_THROUGHPUT_1C;
    private const float PERFSCORE_LATENCY_RD_STACK = PERFSCORE_LATENCY_2C;
    private const float PERFSCORE_LATENCY_WR_STACK = PERFSCORE_LATENCY_2C;
    private const float PERFSCORE_LATENCY_RD_WR_STACK = PERFSCORE_LATENCY_5C;
    private const float PERFSCORE_LATENCY_RD_CONST_ADDR = PERFSCORE_LATENCY_2C;
    private const float PERFSCORE_LATENCY_WR_CONST_ADDR = PERFSCORE_LATENCY_2C;
    private const float PERFSCORE_LATENCY_RD_WR_CONST_ADDR = PERFSCORE_LATENCY_5C;
    private const float PERFSCORE_LATENCY_RD_GENERAL = PERFSCORE_LATENCY_3C;
    private const float PERFSCORE_LATENCY_WR_GENERAL = PERFSCORE_LATENCY_3C;
    private const float PERFSCORE_LATENCY_RD_WR_GENERAL = PERFSCORE_LATENCY_6C;

    internal struct insExecutionCharacteristics
    {
        public float insThroughput;
        public float insLatency;
        public PerfScoreMemoryAccessKind insMemoryAccessKind;
    }

    internal float insEvaluateExecutionCost(instrDesc id)
    {
        var result = getInsExecutionCharacteristics(id);
        var throughput = result.insThroughput;
        var latency = result.insLatency;
        var memAccessKind = result.insMemoryAccessKind;

        assert(throughput >= 0.0f);
        assert(latency >= 0.0f);

        if (memAccessKind is PerfScoreMemoryAccessKind.Write or PerfScoreMemoryAccessKind.ReadWrite)
        {
            // A store is assumed not to be read back for the next WR_GENERAL cycles.
            latency = MathF.Max(0.0f, latency - PERFSCORE_LATENCY_WR_GENERAL);
        }
        else if (latency >= 1.0f)
        {
            // Speculation typically eliminates one cycle of non-store latency.
            latency -= 1.0f;
        }

        return MathF.Max(throughput, latency);
    }

    private void perfScoreUnhandledInstruction(instrDesc id, ref insExecutionCharacteristics result)
    {
#if DEBUG
        jitprintf($"PerfScore: unhandled instruction: {codeGen.genInsDisplayName(id)}, format {emitIfName(id.idInsFmt())}");
        assert(false, "PerfScore: unhandled instruction");
#endif
        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
        result.insLatency = PERFSCORE_LATENCY_1C;
    }
#endif
}
