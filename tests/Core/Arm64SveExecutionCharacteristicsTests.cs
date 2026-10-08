// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && (DEBUG || LATE_DISASM)
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64SveExecutionCharacteristicsTests
{
    [TestCase(IF_SVE_AA_3A, INS_sve_add, 2.0f, 0.5f)]
    [TestCase(IF_SVE_AA_3A, INS_sve_mul, 4.0f, 1.0f)]
    [TestCase(IF_SVE_AA_3A, INS_sve_asr, 2.0f, 1.0f)]
    [TestCase(IF_SVE_AA_3A, INS_sve_mov, 1.0f, 1.0f)]
    [TestCase(IF_SVE_AC_3A, INS_sve_sdiv, 12.0f, 11.0f)]
    [TestCase(IF_SVE_AM_2A, INS_sve_lsl, 2.0f, 1.0f)]
    [TestCase(IF_SVE_AM_2A, INS_sve_sqshl, 4.0f, 1.0f)]
    [TestCase(IF_SVE_AQ_3A, INS_sve_abs, 2.0f, 0.5f)]
    [TestCase(IF_SVE_AQ_3A, INS_sve_sxtw, 2.0f, 1.0f)]
    [TestCase(IF_SVE_AR_4A, INS_sve_mla, 5.0f, 0.5f)]
    [TestCase(IF_SVE_GY_3B, INS_sve_fdot, 1.0f, 1.0f)]
    [TestCase(IF_SVE_GY_3B, INS_sve_bfdot, 4.0f, 2.0f)]
    [TestCase(IF_SVE_AV_3A, INS_sve_eor3, 2.0f, 1.0f)]
    [TestCase(IF_SVE_AV_3A, INS_sve_bsl, 2.0f, 2.0f)]
    [TestCase(IF_SVE_AT_3A, INS_sve_bext, 6.0f, 0.5f)]
    [TestCase(IF_SVE_AT_3A, INS_sve_frecps, 4.0f, 2.0f)]
    [TestCase(IF_SVE_AT_3A, INS_sve_mul, 5.0f, 0.5f)]
    [TestCase(IF_SVE_AT_3A, INS_sve_add, 2.0f, 2.0f)]
    [TestCase(IF_SVE_FL_3A, INS_sve_smullb, 4.0f, 1.0f)]
    [TestCase(IF_SVE_FL_3A, INS_sve_pmullb, 2.0f, 1.0f)]
    [TestCase(IF_SVE_BA_3A, INS_sve_index, 8.0f, 0.5f)]
    [TestCase(IF_SVE_CE_2A, INS_sve_mov, 140.0f, 140.0f)]
    [TestCase(IF_SVE_CZ_4A, INS_sve_and, 2.0f, 2.0f)]
    [TestCase(IF_SVE_CZ_4A, INS_sve_ands, 2.0f, 1.0f)]
    [TestCase(IF_SVE_CZ_4A, INS_sve_nor, 1.0f, 1.0f)]
    [TestCase(IF_SVE_CZ_4A, INS_sve_movs, 1.0f, 3.0f)]
    [TestCase(IF_SVE_DA_4A, INS_sve_brkpa, 2.0f, 1.0f)]
    [TestCase(IF_SVE_DA_4A, INS_sve_brkpas, 3.0f, 1.0f)]
    [TestCase(IF_SVE_DG_2A, INS_sve_rdffrs, 4.0f, 0.5f)]
    [TestCase(IF_SVE_HL_3A, INS_sve_fdiv, 15.0f, 14.0f)]
    [TestCase(IF_SVE_HL_3A, INS_sve_famax, 20.0f, 25.0f)]
    [TestCase(IF_SVE_HR_3A, INS_sve_fsqrt, 14.0f, 16.0f)]
    [TestCase(IF_SVE_HS_3A, INS_sve_scvtf, 6.0f, 0.25f)]
    [TestCase(IF_SVE_AG_3A, INS_sve_andqv, 20.0f, 25.0f)]
    [TestCase(IF_SVE_IS_3A, INS_sve_ld3w, 10.0f, 3.0f)]
    [TestCase(IF_SVE_IT_4A, INS_sve_ld3w, 10.0f, 0.5f)]
    [TestCase(IF_SVE_JO_3A, INS_sve_st4w, 11.0f, 9.0f)]
    [TestCase(IF_SVE_JC_4A, INS_sve_st4w, 11.0f, 1.0f / 9.0f)]
    [TestCase(IF_SVE_JC_4A, INS_sve_st3w, 7.0f, 0.5f)]
    [TestCase(IF_SVE_HX_3A_B, INS_sve_ld1w, 9.0f, 1.0f)]
    [TestCase(IF_SVE_JI_3A_A, INS_sve_st1w, 2.0f, 1.0f)]
    [TestCase(IF_SVE_IO_3A, INS_sve_ld1rqw, 6.0f, 3.0f)]
    [TestCase(IF_SVE_IO_3A, INS_sve_ld1row, 1.0f, 1.0f)]
    [TestCase(IF_SVE_GG_3A, INS_sve_luti2, 1.0f, 1.0f)]
    [TestCase(IF_SVE_CB_2A, INS_sve_mov, 2.0f, 2.0f)]
    [TestCase(IF_SVE_CB_2A, INS_sve_dup, 3.0f, 1.0f)]
    public static void ScoresPreserveNativeInstructionDispatchAndPlaceholderValues(
        Emitter.insFormat format, instruction ins, float latency, float throughput)
    {
        Arm64SveDisassemblyTests.WithEmitter(emitter =>
        {
            var id = Arm64SveDisassemblyTests.View.Descriptor(format, ins, INS_OPTS_SCALABLE_S,
                REG_V0, REG_P7, REG_V1, REG_V31);
            var score = new Emitter.insExecutionCharacteristics
            {
                insLatency = -1024.0f,
                insThroughput = -1024.0f,
                insMemoryAccessKind = Emitter.PerfScoreMemoryAccessKind.ReadWrite,
            };
            var (output, assertions) = Arm64SveDisassemblyTests.Capture(() => Score(emitter, id, ref score));

            Assert.That(score.insLatency, Is.EqualTo(latency));
            Assert.That(score.insThroughput, Is.EqualTo(throughput));
            Assert.That(score.insMemoryAccessKind, Is.EqualTo(Emitter.PerfScoreMemoryAccessKind.ReadWrite));
            Assert.That(output, Is.Empty);
            Assert.That(assertions, Is.Empty);
        });
    }

    [TestCase(IF_SVE_AM_2A)]
    [TestCase(IF_SVE_HL_3A)]
    [TestCase(IF_SVE_HH_2A)]
    [TestCase(IF_EN5A)]
    public static void UnhandledInstructionsUseTheSharedDebugDiagnosticAndReleaseScore(Emitter.insFormat format)
    {
        Arm64SveDisassemblyTests.WithEmitter(emitter =>
        {
            var id = Arm64SveDisassemblyTests.View.Descriptor(format, INS_sve_add, INS_OPTS_SCALABLE_S,
                REG_V0, REG_P7, REG_V1, REG_V31);
            var score = new Emitter.insExecutionCharacteristics
            {
                insMemoryAccessKind = Emitter.PerfScoreMemoryAccessKind.Write,
            };
            var (output, assertions) = Arm64SveDisassemblyTests.Capture(() => Score(emitter, id, ref score));

            Assert.That(score.insLatency, Is.EqualTo(1.0f));
            Assert.That(score.insThroughput, Is.EqualTo(1.0f));
            Assert.That(score.insMemoryAccessKind, Is.EqualTo(Emitter.PerfScoreMemoryAccessKind.Write));
#if DEBUG
            Assert.That(output, Does.StartWith("PerfScore: unhandled instruction: "));
            Assert.That(output, Does.EndWith($", format {Emitter.emitIfName(format)}"));
            Assert.That(assertions, Is.EqualTo<string[]>(["PerfScore: unhandled instruction"]));
#else
            Assert.That(output, Is.Empty);
            Assert.That(assertions, Is.Empty);
#endif
        });
    }

    [TestCase(IF_SVE_AA_3A, INS_sve_add, 2.0f, 0.5f)]
    [TestCase(IF_SVE_IJ_3A, INS_sve_ld1w, 9.0f, 1.0f)]
    [TestCase(IF_SVE_JN_3A, INS_sve_st1w, 2.0f, 1.0f)]
    public static void SharedScoringDispatchReachesSveWithoutChangingTheMemoryAccessField(
        Emitter.insFormat format, instruction ins, float latency, float throughput)
    {
        Arm64SveDisassemblyTests.WithEmitter(emitter =>
        {
            var id = Arm64SveDisassemblyTests.View.Descriptor(format, ins, INS_OPTS_SCALABLE_S,
                REG_V0, REG_P7, REG_R2, REG_R3, EA_SCALABLE);
            var score = emitter.getInsExecutionCharacteristics(id);

            Assert.That(score.insLatency, Is.EqualTo(latency));
            Assert.That(score.insThroughput, Is.EqualTo(throughput));
            Assert.That(score.insMemoryAccessKind, Is.EqualTo(Emitter.PerfScoreMemoryAccessKind.None));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getInsSveExecutionCharacteristics")]
    private static extern void Score(Emitter emitter, Emitter.instrDesc id,
        ref Emitter.insExecutionCharacteristics result);
}
#endif
