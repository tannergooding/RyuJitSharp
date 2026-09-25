// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.PerfScoreMemoryAccessKind;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if TARGET_AMD64
internal static class EmitterExecutionCostTests
{
    [TestCase(IF_RRD, IF_NONE)]
    [TestCase(IF_RWR_MRD, IF_MRD)]
    [TestCase(IF_MWR_RRD, IF_MWR)]
    [TestCase(IF_MRW_RRD, IF_MRW)]
    [TestCase(IF_RWR_SRD, IF_SRD)]
    [TestCase(IF_SWR_RRD, IF_SWR)]
    [TestCase(IF_SRW_CNS, IF_SRW)]
    [TestCase(IF_RWR_ARD, IF_ARD)]
    [TestCase(IF_AWR_RRD, IF_AWR)]
    [TestCase(IF_ARW_CNS, IF_ARW)]
    public static void MemoryFormatFollowsNativeSchedulingMetadata(Emitter.insFormat format,
        Emitter.insFormat expected)
    {
        var id = View.Basic(INS_mov, EA_8BYTE, format);
        Assert.That(Emitter.ExtractMemoryFormat(format), Is.EqualTo(expected));
        Assert.That(Emitter.getMemoryOperation(id), Is.EqualTo(expected));

        id.idIns(INS_lea);
        Assert.That(Emitter.getMemoryOperation(id), Is.EqualTo(IF_NONE));
    }

#if DEBUG || LATE_DISASM
    [TestCase(INS_add, EA_8BYTE, IF_RRW_RRD, 0.25f, 1.0f, None, 0.25f)]
    [TestCase(INS_mov, EA_8BYTE, IF_RWR_RRD, 0.25f, 0.0f, None, 0.25f)]
    [TestCase(INS_add, EA_8BYTE, IF_RWR_SRD, 0.5f, 2.0f, Read, 1.0f)]
    [TestCase(INS_add, EA_8BYTE, IF_RRW_ARD, 0.5f, 3.0f, Read, 2.0f)]
    [TestCase(INS_mov, EA_8BYTE, IF_AWR_RRD, 1.0f, 3.0f, Write, 1.0f)]
    [TestCase(INS_add, EA_8BYTE, IF_ARW_RRD, 1.0f, 6.0f, ReadWrite, 3.0f)]
    [TestCase(INS_addps, EA_16BYTE, IF_RWR_RRD_ARD, 0.5f, 7.0f, Read, 6.0f)]
    [TestCase(INS_addps, EA_32BYTE, IF_RWR_RRD_ARD, 0.5f, 8.0f, Read, 7.0f)]
    [TestCase(INS_addps, EA_64BYTE, IF_RWR_RRD_ARD, 0.5f, 9.0f, Read, 8.0f)]
    [TestCase(INS_div, EA_4BYTE, IF_RRD, 6.0f, 26.0f, None, 25.0f)]
    [TestCase(INS_div, EA_8BYTE, IF_RRD, 52.0f, 62.0f, None, 61.0f)]
    [TestCase(INS_idiv, EA_8BYTE, IF_RRD, 57.0f, 69.0f, None, 68.0f)]
    [TestCase(INS_vdivsh, EA_2BYTE, IF_RWR_RRD_RRD, 4.0f, 14.0f, None, 13.0f)]
    [TestCase(INS_vpconflictd, EA_16BYTE, IF_RWR_RRD, 6.0f, 12.0f, None, 11.0f)]
    [TestCase(INS_vpconflictd, EA_64BYTE, IF_RWR_RRD, 19.0f, 26.0f, None, 25.0f)]
    [TestCase(INS_vgatherdps, EA_16BYTE, IF_RWR_RRD_ARD, 2.0f, 14.0f, Read, 13.0f)]
    [TestCase(INS_vpbroadcastd, EA_64BYTE, IF_RWR_ARD, 0.5f, 8.0f, Read, 7.0f)]
    public static void CharacteristicsAndCostPreserveWidthAndMemoryPenalties(instruction ins,
        emitAttr size, Emitter.insFormat format, float throughput, float latency,
        Emitter.PerfScoreMemoryAccessKind memoryKind, float cost)
    {
        WithEmitter(emitter =>
        {
            var id = View.Basic(ins, size, format);
            var result = emitter.getInsExecutionCharacteristics(id);

            Assert.That(result.insThroughput, Is.EqualTo(throughput));
            Assert.That(result.insLatency, Is.EqualTo(latency));
            Assert.That(result.insMemoryAccessKind, Is.EqualTo(memoryKind));
            Assert.That(emitter.insEvaluateExecutionCost(id), Is.EqualTo(cost));
        });
    }

    [Test]
    public static void DirectAndIndirectCallsUseDifferentThroughputs()
    {
        WithEmitter(emitter =>
        {
            var direct = View.Basic(INS_call, EA_8BYTE, IF_METHOD);
            var indirect = View.Basic(INS_call, EA_8BYTE, IF_METHPTR);
            var memory = View.Basic(INS_call, EA_8BYTE, IF_ARD);

            Assert.That(emitter.insEvaluateExecutionCost(direct), Is.EqualTo(1.0f));
            Assert.That(emitter.insEvaluateExecutionCost(indirect), Is.EqualTo(3.0f));
            Assert.That(emitter.insEvaluateExecutionCost(memory), Is.EqualTo(3.0f));
            Assert.That(emitter.getInsExecutionCharacteristics(memory).insMemoryAccessKind, Is.EqualTo(Read));
        });
    }

    [Test]
    public static void PseudoInstructionsAndRemovableJumpDoNotAccumulateCost()
    {
        WithEmitter(emitter =>
        {
            var nop = View.Basic(INS_nop, EA_1BYTE, IF_NONE);
            var data16 = View.Basic(INS_data16, EA_1BYTE, IF_NONE);
            var conditionalBranch = View.Basic(INS_je, EA_4BYTE, IF_LABEL);
            var removed = View.Jump(removable: true);
            var branch = View.Jump(removable: false);

            Assert.That(emitter.insEvaluateExecutionCost(nop), Is.EqualTo(0.25f));
            Assert.That(emitter.insEvaluateExecutionCost(data16), Is.EqualTo(0.25f));
            Assert.That(emitter.insEvaluateExecutionCost(conditionalBranch), Is.EqualTo(1.0f));
            Assert.That(emitter.insEvaluateExecutionCost(removed), Is.Zero);
            Assert.That(emitter.insEvaluateExecutionCost(branch), Is.EqualTo(2.0f));

#if FEATURE_LOOP_ALIGN
            var zeroAlign = View.Align(0, false);
            var emittedAlign = View.Align(8, false);
            Assert.That(emitter.insEvaluateExecutionCost(zeroAlign), Is.Zero);
#if DEBUG
            var placedAfterJump = View.Align(8, true);
            Assert.That(emitter.insEvaluateExecutionCost(placedAfterJump), Is.Zero);
#endif
            Assert.That(emitter.insEvaluateExecutionCost(emittedAlign), Is.EqualTo(0.25f));
#endif
        });
    }

    [Test]
    public static void LeaAddressComplexityChangesThroughputAndLatencyWithoutMemoryAccess()
    {
        WithEmitter(emitter =>
        {
            var id = View.AddressLea(REG_RAX, REG_RCX, 0);
            var simple = emitter.getInsExecutionCharacteristics(id);
            Assert.That(simple.insThroughput, Is.EqualTo(0.5f));
            Assert.That(simple.insLatency, Is.EqualTo(1.0f));
            Assert.That(simple.insMemoryAccessKind, Is.EqualTo(None));

            View.SetDisplacement(id, 16);
            var complex = emitter.getInsExecutionCharacteristics(id);
            Assert.That(complex.insThroughput, Is.EqualTo(1.0f));
            Assert.That(complex.insLatency, Is.EqualTo(1.0f));

            View.SetBase(id, REG_RBP);
            Assert.That(emitter.getInsExecutionCharacteristics(id).insLatency, Is.EqualTo(3.0f));
        });
    }

    [Test]
    public static void FloatRegisterMovqKeepsNativeSinglePrecisionReciprocalThroughput()
    {
        WithEmitter(emitter =>
        {
            var id = View.Basic(INS_movq, EA_8BYTE, IF_RWR_RRD);
            id.idReg1(REG_XMM1);
            id.idReg2(REG_XMM2);

            var result = emitter.getInsExecutionCharacteristics(id);
            Assert.That(result.insThroughput, Is.EqualTo(1.0f / 3.0f));
            Assert.That(result.insLatency, Is.EqualTo(1.0f));
            Assert.That(emitter.insEvaluateExecutionCost(id), Is.EqualTo(1.0f / 3.0f));
        });
    }

    private static void WithEmitter(System.Action<Emitter> test)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (_, codeGen, _) =>
            test(codeGen.Emitter));
    }
#endif

    private abstract class View(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Basic(instruction ins, emitAttr size, insFormat format)
        {
            var id = new instrDescBasic();
            id.idIns(ins);
            id.idOpSize(size);
            id.idInsFmt(format);
            return id;
        }

        public static instrDesc AddressLea(regNumber baseReg, regNumber indexReg, nint displacement)
        {
            var id = new instrDescAmd();
            id.idIns(INS_lea);
            id.idOpSize(EA_8BYTE);
            id.idInsFmt(IF_RWR_ARD);
            id.idAddr().iiaAddrMode.amBaseReg = baseReg;
            id.idAddr().iiaAddrMode.amIndxReg = indexReg;
            id.idAddr().iiaAddrMode.amDisp = unchecked((int)displacement);
            return id;
        }

        public static void SetDisplacement(instrDesc id, int displacement)
            => id.idAddr().iiaAddrMode.amDisp = displacement;

        public static void SetBase(instrDesc id, regNumber reg)
            => id.idAddr().iiaAddrMode.amBaseReg = reg;

        public static instrDesc Jump(bool removable)
        {
            var id = new instrDescJmp { idjIsRemovableJmpCandidate = removable };
            id.idIns(INS_jmp);
            id.idInsFmt(IF_LABEL);
            id.idCodeSize(removable ? 0u : 2u);
            return id;
        }

#if FEATURE_LOOP_ALIGN
        public static instrDesc Align(uint padding, bool placedAfterJump)
        {
            var id = new instrDescAlign();
            id.idIns(INS_align);
            id.idInsFmt(IF_NONE);
            id.idCodeSize(padding);
#if DEBUG
            id.isPlacedAfterJmp = placedAfterJump;
#endif
            return id;
        }
#endif
    }
}
#endif
