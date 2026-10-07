// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.GenTreeBlk;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.BarrierKind;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class RiscVCodeGenPortTests
{
    private const string RiscVRecorderDebugBoundary = "Instruction sanity checking outside AMD64 is not ported.";

#if DEBUG
    [TestCase(TYP_INT, -2048, RiscVRecorderDebugBoundary, true)]
    [TestCase(TYP_INT, 2047, RiscVRecorderDebugBoundary, true)]
    [TestCase(TYP_BYREF, 42, RiscVRecorderDebugBoundary, true)]
    [TestCase(TYP_INT, -2049, "Instruction sanity checking outside AMD64 is not ported.", true)]
    [TestCase(TYP_LONG, 2048, "Instruction sanity checking outside AMD64 is not ported.", true)]
#else
    [TestCase(TYP_INT, -2048, null, true)]
    [TestCase(TYP_INT, 2047, null, true)]
    [TestCase(TYP_BYREF, 42, null, true)]
    [TestCase(TYP_INT, -2049, null, true)]
    [TestCase(TYP_LONG, 2048, null, true)]
#endif
    public static void IntegerConstantNodeDispatchPreservesRiscVImmediateRecordingBoundary(
        var_types type,
        long immediate,
        string? expectedBoundary,
        bool recordsDescriptor)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var constant = compiler.gtNewIconNode(type, unchecked((nint)immediate));
            constant.RegNum = REG_A0;

            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            FatalJitException? failure = null;
            try
            {
                codeGen.genCodeForTreeNode(constant);
            }
            catch (FatalJitException exception)
            {
                failure = exception;
            }

            if (expectedBoundary is null)
            {
                Assert.That(failure, Is.Null);
            }
            else
            {
                Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
                Assert.That(failure?.Message, Does.Contain(expectedBoundary));
            }

            Assert.That(instructionBuffer.Count,
                Is.EqualTo(initialInstructionCount + (recordsDescriptor ? 1 : 0)));
            if (!recordsDescriptor)
            {
                Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
                return;
            }

            var descriptor = instructionBuffer[^1];
            var instructionCount = Emitter.emitLoadImmediate(
                false, descriptor.idOpSize(), REG_NA, unchecked((nint)immediate));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_A0));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo((uint)(instructionCount * 4)));
            Assert.That(descriptor.idIns(), Is.Not.EqualTo(INS_invalid));
            Assert.That(CurrentInstructionGroupSize(emitter),
                Is.EqualTo(initialGroupSize + (failure is null ? instructionCount * 4 : 0)));
        });
    }

    [TestCase(INS_add, EA_8BYTE, REG_A0, REG_A1, REG_A2, INS_none, 4)]
    [TestCase(INS_add, EA_8BYTE, REG_A0, REG_R0, REG_A1, INS_c_mv, 2)]
    [TestCase(INS_add, EA_8BYTE, REG_A0, REG_A0, REG_A1, INS_c_add, 2)]
    [TestCase(INS_and, EA_8BYTE, REG_A0, REG_A0, REG_A1, INS_c_and, 2)]
    [TestCase(INS_fadd_s, EA_4BYTE, REG_FA0, REG_FA1, REG_FA2, INS_none, 4)]
    [TestCase(INS_sc_w, EA_4BYTE, REG_A0, REG_A1, REG_A2, INS_none, 4)]
    [TestCase(INS_lr_w, EA_4BYTE, REG_A0, REG_A1, REG_R0, INS_none, 4)]
    public static void ThreeRegisterInstructionRecordsItsDescriptor(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        regNumber reg3,
        instruction compressedIns,
        int codeSize)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            FatalJitException? failure = null;
            try
            {
                emitter.emitIns_R_R_R(ins, attr, reg1, reg2, reg3);
            }
            catch (FatalJitException exception)
            {
                failure = exception;
            }

#if DEBUG
            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Instruction sanity checking outside AMD64 is not ported."));
#else
            Assert.That(failure, Is.Null);
#endif

            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount + 1));
            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(reg2));
            Assert.That(descriptor.idReg3(), Is.EqualTo(reg3));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo((uint)codeSize));
            Assert.That(descriptor.idAddr().iiaInstrEncode,
                Is.EqualTo(ExpectedThreeRegisterEncoding(ins, reg1, reg2, reg3, compressedIns)));
            Assert.That(CurrentInstructionGroupSize(emitter),
                Is.EqualTo(initialGroupSize + (failure is null ? (int)codeSize : 0)));
        });
    }

    [TestCase(INS_add, 0x00000033u)]
    [TestCase(INS_fadd_s, 0x00000053u)]
    [TestCase(INS_sc_w, 0x1800202Fu)]
    [TestCase(INS_c_mv, 0x00008002u)]
    public static void InstructionCodeMatchesTheRiscVOpcodeTable(instruction ins, uint expectedCode)
    {
        Assert.That(Emitter.emitInsCode(ins), Is.EqualTo(expectedCode));
    }

    [TestCase(INS_mov, EA_8BYTE, REG_A0, REG_A1, 0x00058513u)]
    [TestCase(INS_sext_w, EA_4BYTE, REG_A0, REG_A1, 0x0005851Bu)]
    [TestCase(INS_not, EA_8BYTE, REG_A0, REG_A1, 0xFFF5C513u)]
    [TestCase(INS_clz, EA_8BYTE, REG_A0, REG_A1, 0x60059513u)]
    [TestCase(INS_rev8, EA_8BYTE, REG_A0, REG_A1, 0x6B85D513u)]
    [TestCase(INS_fmv_x_w, EA_4BYTE, REG_A0, REG_FA1, 0xE0058553u)]
    [TestCase(INS_fclass_d, EA_8BYTE, REG_A0, REG_FA1, 0xE2059553u)]
    [TestCase(INS_fcvt_wu_d, EA_4BYTE, REG_A0, REG_FA1, 0xC2159553u)]
    [TestCase(INS_fmv_d_x, EA_8BYTE, REG_FA0, REG_A1, 0xF2058553u)]
    [TestCase(INS_fcvt_s_w, EA_4BYTE, REG_FA0, REG_A1, 0xD005F553u)]
    [TestCase(INS_fcvt_d_w, EA_8BYTE, REG_FA0, REG_A1, 0xD2058553u)]
    [TestCase(INS_fcvt_s_d, EA_4BYTE, REG_FA0, REG_FA1, 0x4015F553u)]
    [TestCase(INS_fcvt_d_s, EA_8BYTE, REG_FA0, REG_FA1, 0x42058553u)]
    [TestCase(INS_fsqrt_s, EA_4BYTE, REG_FA0, REG_FA1, 0x5805F553u)]
    public static void RegisterRegisterInstructionRecordsItsDescriptor(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        uint expectedCode)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(() => emitter.emitIns_R_R(ins, attr, reg1, reg2));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(reg2));
            Assert.That(descriptor.idAddr().iiaInstrEncode, Is.EqualTo(expectedCode));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
        });
    }

    [Test]
    public static void UnsupportedRegisterRegisterInstructionStopsBeforeRecording()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_R(INS_nop, EA_8BYTE, REG_A0, REG_A1));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Illegal instruction within RISC-V two-register instruction recording."));
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount));
            Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
        });
    }

    [TestCase(INS_fence, EA_4BYTE, 0x33, 0x0330000Fu)]
    [TestCase(INS_fence, EA_4BYTE, 0x23, 0x0230000Fu)]
    [TestCase(INS_fence, EA_4BYTE, 0x31, 0x0310000Fu)]
    [TestCase(INS_j, EA_4BYTE, 8, 0x0080006Fu)]
    [TestCase(INS_j, EA_4BYTE, -4, 0xFFDFF06Fu)]
    [TestCase(INS_j, EA_4BYTE, -1048576, 0x8000006Fu)]
    [TestCase(INS_j, EA_4BYTE, 1048574, 0x7FFFF06Fu)]
    public static void ImmediateOnlyInstructionRecordsItsDescriptor(
        instruction ins,
        emitAttr attr,
        int immediate,
        uint expectedCode)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_I(ins, attr, immediate));

            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idAddr().iiaInstrEncode, Is.EqualTo(expectedCode));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
        });
    }

    [TestCase(BARRIER_FULL, 0x0330000Fu)]
    [TestCase(BARRIER_LOAD_ONLY, 0x0230000Fu)]
    [TestCase(BARRIER_STORE_ONLY, 0x0310000Fu)]
    public static void MemoryBarrierRecordsItsRiscVFence(BarrierKind barrierKind, uint expectedCode)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => codeGen.instGen_MemoryBarrier(barrierKind));

            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_fence));
            Assert.That(descriptor.idAddr().iiaInstrEncode, Is.EqualTo(expectedCode));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
        });
    }

    [TestCase(EJ_jmp, INS_j)]
    [TestCase(EJ_eq, INS_beq)]
    [TestCase(EJ_ne, INS_bne)]
    public static void JumpKindMapsToItsRiscVInstruction(emitJumpKind jumpKind, instruction expectedInstruction)
    {
        Assert.That(Emitter.emitJumpKindToIns(jumpKind), Is.EqualTo(expectedInstruction));
    }

    [TestCase(EJ_jmp, EJ_jmp)]
    [TestCase(EJ_eq, EJ_ne)]
    [TestCase(EJ_ne, EJ_eq)]
    public static void JumpKindMapsToItsRiscVReverse(emitJumpKind jumpKind, emitJumpKind expectedReverse)
    {
        Assert.That(Emitter.emitReverseJumpKind(jumpKind), Is.EqualTo(expectedReverse));
    }

    [Test]
    public static void JumpGenerationReachesTheRiscVEmitterBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var target = new BasicBlock(null, null);
            var failure = CaptureFatalJitException(
                () => codeGen.inst_JMP(EJ_jmp, target));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message, Does.Contain("Label jump instruction recording requires xarch."));
        });
    }

    [Test]
    public static void InstructionClassificationIdentifiesGCRegisterWrites()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            Assert.Multiple(() =>
            {
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_nop), Is.False);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_j), Is.False);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_lea), Is.True);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_sw), Is.False);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_fsd), Is.False);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_fence), Is.False);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_beq), Is.False);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_fld), Is.False);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_fmadd_s), Is.False);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_csrrw), Is.True);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_ecall), Is.False);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_feq_s), Is.True);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_fmv_x_w), Is.True);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_fcvt_w_s), Is.True);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_fadd_s), Is.False);
                Assert.That(emitter.emitInsMayWriteToGCReg(INS_add), Is.True);
            });
        });
    }

#if PROFILING_SUPPORTED
    [Test]
    public static void UnhookedProfilerCallbacksDoNotRecordInstructions()
    {
        WithProfilerCallbacks((compiler, codeGen) =>
        {
            var initRegZeroed = true;
            codeGen.genProfilingEnterCallback(REG_T0, ref initRegZeroed);
            codeGen.genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_LEAVE);

            Assert.That(CurrentInstructionBuffer(codeGen.Emitter), Is.Empty);
            Assert.That(initRegZeroed, Is.True);
            Assert.That(compiler.info.compProfilerCallback, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HookedProfilerEntryPreparesArgumentsBeforeTheCallBoundary(bool indirect)
    {
        WithProfilerCallbacks((compiler, codeGen) =>
        {
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHndIndirected = indirect;
            compiler.compProfilerMethHnd = (void*)0x12345678;
            var initRegZeroed = true;

            var failure = CaptureFatalJitException(
                () => codeGen.genProfilingEnterCallback(REG_T0, ref initRegZeroed));

            AssertProfilerCallbackBoundary(failure);
            var descriptors = CurrentInstructionBuffer(codeGen.Emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            Assert.That(descriptors.Any(descriptor => descriptor.idReg1() == REG_T0), Is.True);
#if !DEBUG
            Assert.That(descriptors.Any(descriptor => descriptor.idReg1() == REG_T1), Is.True);
#endif
            Assert.That(initRegZeroed, Is.True);
        });
    }

    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL)]
    public static void HookedProfilerLeavePreparesAndUntracksArgumentsBeforeTheCallBoundary(CorInfoHelpFunc helper)
    {
        WithProfilerCallbacks((compiler, codeGen) =>
        {
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHnd = (void*)0x12345678;
            codeGen.GCInfo.gcRegGCrefSetCur = new regMaskTP(SRBM_T0 | SRBM_T1);
            codeGen.GCInfo.gcRegByrefSetCur = new regMaskTP(SRBM_T0 | SRBM_T1);

            var failure = CaptureFatalJitException(() => codeGen.genProfilingLeaveCallback(helper));

            AssertProfilerCallbackBoundary(failure);
            Assert.That(compiler.info.compProfilerCallback, Is.True);
#if DEBUG
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(new regMaskTP(SRBM_T0 | SRBM_T1)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(new regMaskTP(SRBM_T0 | SRBM_T1)));
#else
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));
#endif
            var descriptors = CurrentInstructionBuffer(codeGen.Emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            Assert.That(descriptors.Any(descriptor => descriptor.idReg1() == REG_T0), Is.True);
#if !DEBUG
            Assert.That(descriptors.Any(descriptor => descriptor.idReg1() == REG_T1), Is.True);
#endif
        });
    }
#endif

#if !DEBUG
    [TestCase(-2048, true, INS_addi)]
    [TestCase(2047, true, INS_addi)]
    [TestCase(-2049, false, INS_add)]
    [TestCase(2048, false, INS_add)]
    public static void FrameInstructionWithConstantUsesTheSignedImmediateBoundary(
        int immediate,
        bool expectedImmediateFit,
        instruction expectedFinalInstruction)
    {
        WithCodeGen((_, codeGen) =>
        {
            var immediateFits = GenInstrWithConstant(
                codeGen, INS_addi, EA_PTRSIZE, REG_T0, REG_FP, immediate, REG_T2, false);

            Assert.That(immediateFits, Is.EqualTo(expectedImmediateFit));
            var descriptors = CurrentInstructionBuffer(codeGen.Emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(expectedFinalInstruction));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_T0));
            Assert.That(descriptors[^1].idCodeSize(), Is.EqualTo(4u));
        });
    }

    [Test]
    public static void LargeFrameMemoryOperandUsesTemporaryBaseRegister()
    {
        WithCodeGen((_, codeGen) =>
        {
            var immediateFits = GenInstrWithConstant(
                codeGen, INS_sd, EA_PTRSIZE, REG_A0, REG_SP, 2048, REG_T2, false);

            Assert.That(immediateFits, Is.False);
            var descriptors = CurrentInstructionBuffer(codeGen.Emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            Assert.That(descriptors[^2].idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptors[^2].idReg1(), Is.EqualTo(REG_T2));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_sd));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_A0));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(REG_T2));
        });
    }
#endif

    [Test]
    public static void UnsupportedJmpVarargsStopsBeforeEmittingCode()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compIsVarArgs = true;

            var failure = CaptureFatalJitException(() => codeGen.genJmpPlaceVarArgs());

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            var descriptors = CurrentInstructionBuffer(codeGen.Emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            Assert.That(descriptors, Is.Empty);
        });
    }

    [TestCase(EA_PTRSIZE, REG_A0, REG_A1, INS_mov, 0x00058513u, false)]
    [TestCase(EA_4BYTE, REG_A0, REG_A1, INS_sext_w, 0x0005851Bu, false)]
    [TestCase(EA_PTRSIZE, REG_A0, REG_FA1, INS_fmv_x_d, 0xE2058553u, false)]
    [TestCase(EA_4BYTE, REG_A0, REG_FA1, INS_fmv_x_w, 0xE0058553u, false)]
    [TestCase(EA_PTRSIZE, REG_FA0, REG_A1, INS_fmv_d_x, 0xF2058553u, false)]
    [TestCase(EA_4BYTE, REG_FA0, REG_A1, INS_fmv_w_x, 0xF0058553u, false)]
    [TestCase(EA_PTRSIZE, REG_FA0, REG_FA1, INS_fsgnj_d, 0x22B58553u, true)]
    [TestCase(EA_4BYTE, REG_FA0, REG_FA1, INS_fsgnj_s, 0x20B58553u, true)]
    public static void RegisterMoveRecordsTheExpectedTransfer(
        emitAttr attr,
        regNumber dstReg,
        regNumber srcReg,
        instruction expectedInstruction,
        uint expectedCode,
        bool hasThirdRegister)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_Mov(attr, dstReg, srcReg, canSkip: false));

            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(expectedInstruction));
            Assert.That(descriptor.idReg1(), Is.EqualTo(dstReg));
            Assert.That(descriptor.idReg2(), Is.EqualTo(srcReg));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
            Assert.That(descriptor.idAddr().iiaInstrEncode, Is.EqualTo(expectedCode));
            if (hasThirdRegister)
            {
                Assert.That(descriptor.idReg3(), Is.EqualTo(srcReg));
            }
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void RegisterMoveHonorsCanSkipForIdenticalRegisters(bool canSkip)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#if DEBUG
            var expectedBoundary = canSkip ? null : RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_Mov(EA_PTRSIZE, REG_A0, REG_A0, canSkip));

            if (canSkip)
            {
                Assert.That(failure, Is.Null);
                Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount));
                Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
                return;
            }

            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount + 1));
            Assert.That(instructionBuffer[^1].idIns(), Is.EqualTo(INS_mov));
            Assert.That(instructionBuffer[^1].idReg1(), Is.EqualTo(REG_A0));
            Assert.That(instructionBuffer[^1].idReg2(), Is.EqualTo(REG_A0));
        });
    }

    [TestCase(INS_addi, EA_8BYTE, REG_A0, REG_A1, 42, 0x02A58513u)]
    [TestCase(INS_addi, EA_8BYTE, REG_A0, REG_A1, -2048, 0x80058513u)]
    [TestCase(INS_addi, EA_8BYTE, REG_A0, REG_A1, 2047, 0x7FF58513u)]
    [TestCase(INS_addi, EA_8BYTE, REG_A0, REG_A1, -42, 0xFD658513u)]
    [TestCase(INS_addiw, EA_4BYTE, REG_A0, REG_A1, 42, 0x02A5851Bu)]
    [TestCase(INS_lw, EA_4BYTE, REG_A0, REG_A1, 42, 0x02A5A503u)]
    [TestCase(INS_flw, EA_4BYTE, REG_FA0, REG_A1, 42, 0x02A5A507u)]
    [TestCase(INS_jalr, EA_8BYTE, REG_A0, REG_A1, 42, 0x02A58567u)]
    [TestCase(INS_sw, EA_4BYTE, REG_A0, REG_A1, 42, 0x02A5A523u)]
    [TestCase(INS_sw, EA_4BYTE, REG_A0, REG_A1, -2048, 0x80A5A023u)]
    [TestCase(INS_sw, EA_4BYTE, REG_A0, REG_A1, 2047, 0x7EA5AFA3u)]
    [TestCase(INS_fsw, EA_4BYTE, REG_FA0, REG_A1, 42, 0x02A5A527u)]
    [TestCase(INS_csrrw, EA_8BYTE, REG_A0, REG_A1, 0xC00, 0xC0059573u)]
    [TestCase(INS_csrrw, EA_8BYTE, REG_A0, REG_A1, 0xFFF, 0xFFF59573u)]
    public static void TwoRegisterImmediateInstructionRecordsItsDescriptor(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        int immediate,
        uint expectedCode)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_R_I(ins, attr, reg1, reg2, (nint)immediate));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(reg2));
            Assert.That(descriptor.idSmallCns(), Is.EqualTo(immediate));
            Assert.That(descriptor.idAddr().iiaInstrEncode, Is.EqualTo(expectedCode));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EmbeddedDataRegisterInstructionRecordsRelocationDescriptor(bool hasRelocation)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var fieldHandle = Compiler.eeFindJitDataOffs(64);
            var attr = hasRelocation
                ? EA_SET_FLG(EA_PTRSIZE, EA_CNS_RELOC_FLG)
                : EA_PTRSIZE;
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_C(INS_addi, attr, REG_A0, REG_NA, fieldHandle));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_addi));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_A0));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_RC));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(8u));
            Assert.That(descriptor.idIsBound(), Is.True);
            Assert.That(descriptor.idIsCnsReloc(), Is.EqualTo(hasRelocation));
            Assert.That(descriptor.idIsDspReloc(), Is.False);
            Assert.That(descriptor.idAddr().iiaFieldHnd == fieldHandle, Is.True);
            Assert.That(CurrentInstructionGroupSize(emitter),
                Is.EqualTo(initialGroupSize + (failure is null ? 8 : 0)));
        });
    }

    [TestCase(INS_addi, REG_A0, REG_A0)]
    [TestCase(INS_ld, REG_A1, REG_A1)]
    [TestCase(INS_sd, REG_A0, REG_A1)]
    [TestCase(INS_fld, REG_FA0, REG_A1)]
    public static void RelocatableAddressInstructionRecordsTwoWordDescriptor(
        instruction ins,
        regNumber dataReg,
        regNumber addrReg)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            const nint address = 0x12345678;
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_AI(ins, EA_PTR_DSP_RELOC, dataReg, addrReg, address));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idReg1(), Is.EqualTo(dataReg));
            Assert.That(descriptor.idReg2(), Is.EqualTo(addrReg));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_RELOC));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(8u));
            Assert.That(descriptor.idIsDspReloc(), Is.True);
            Assert.That(descriptor.idIsCnsReloc(), Is.False);
            Assert.That((nint)descriptor.idAddr().iiaAddr, Is.EqualTo(address));
            Assert.That(CurrentInstructionGroupSize(emitter),
                Is.EqualTo(initialGroupSize + (failure is null ? 8 : 0)));
        });
    }

    [Test]
    public static void RegisterAddressInstructionPreservesUnportedBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = Assert.Throws<FatalJitException>(
                () => emitter.emitIns_R_AR(INS_lw, EA_4BYTE, REG_A0, REG_A1, 8));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 register-address instruction recording is not ported."));
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount));
            Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
        });
    }

    [TestCase(INS_csrrwi, REG_A0, 0, 0, 0x00005573u)]
    [TestCase(INS_csrrwi, REG_R0, 31, 0xFFF, 0xFFFFD073u)]
    [TestCase(INS_csrrsi, REG_A0, 31, 0xFFF, 0xFFFFE573u)]
    [TestCase(INS_csrrsi, REG_R0, 0, 0, 0x00006073u)]
    [TestCase(INS_csrrci, REG_A0, 0, 0xFFF, 0xFFF07573u)]
    [TestCase(INS_csrrci, REG_R0, 31, 0, 0x000FF073u)]
    public static void RegisterTwoImmediateInstructionRecordsItsDescriptor(
        instruction ins,
        regNumber reg,
        int imm1,
        int imm2,
        uint expectedCode)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_I_I(ins, EA_8BYTE, reg, imm1, imm2));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
            Assert.That(descriptor.idAddr().iiaInstrEncode, Is.EqualTo(expectedCode));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
        });
    }

    [Test]
    public static void UnsupportedRegisterTwoImmediateInstructionTerminatesWithoutRecording()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_I_I(INS_add, EA_8BYTE, REG_A0, 1, 0xC00));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount));
            Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
        });
    }

    [Test]
    public static void UnsupportedThreeRegisterImmediateInstructionTerminatesWithoutRecording()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_R_R_I(
                    INS_add, EA_8BYTE, REG_A0, REG_A1, REG_A2, 1));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Three-register-immediate instruction recording requires xarch."));
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount));
            Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
        });
    }

    [Test]
    public static void UnsupportedFourRegisterInstructionTerminatesWithoutRecording()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_R_R_R(
                    INS_add, EA_8BYTE, REG_A0, REG_A1, REG_A2, REG_A3));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Four-register instruction recording requires xarch."));
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount));
            Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
        });
    }

    private static uint ExpectedThreeRegisterEncoding(
        instruction ins,
        regNumber reg1,
        regNumber reg2,
        regNumber reg3,
        instruction compressedIns)
    {
        var code = Emitter.emitInsCode(compressedIns is INS_none ? ins : compressedIns);
        if (compressedIns is INS_c_mv or INS_c_add)
        {
            code |= (unchecked((uint)reg3) << 2) | (unchecked((uint)reg1) << 7);
        }
        else if (compressedIns is not INS_none)
        {
            code |= (Emitter.tryGetRvcRegisterNumber(reg3) << 2) |
                    (Emitter.tryGetRvcRegisterNumber(reg1) << 7);
        }
        else
        {
            code |= (unchecked((uint)reg1) & 0x1Fu) << 7;
            code |= (unchecked((uint)reg2) & 0x1Fu) << 15;
            code |= (unchecked((uint)reg3) & 0x1Fu) << 20;
            if (ins is INS_fadd_s or INS_fsub_s or INS_fmul_s or INS_fdiv_s or
                INS_fadd_d or INS_fsub_d or INS_fmul_d or INS_fdiv_d)
            {
                code |= 0x7u << 12;
            }
            else if (ins is INS_sc_w or INS_sc_d)
            {
                code |= 0b10u << 25;
            }
            else if (ins is INS_lr_w or INS_lr_d or INS_amoswap_w or INS_amoswap_d or
                     INS_amoadd_w or INS_amoadd_d or INS_amoxor_w or INS_amoxor_d or
                     INS_amoand_w or INS_amoand_d or INS_amoor_w or INS_amoor_d or
                     INS_amomin_w or INS_amomin_d or INS_amomax_w or INS_amomax_d or
                     INS_amominu_w or INS_amominu_d or INS_amomaxu_w or INS_amomaxu_d)
            {
                code |= 0b11u << 25;
            }
        }

        return code;
    }

    [Test]
    public static void RelocatableImmediateGenerationRecordsRiscVDescriptor()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => codeGen.instGen_Set_Reg_To_Imm(EA_HANDLE_CNS_RELOC, REG_A0, 42));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_addi));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_A0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_A0));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_RELOC));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(8u));
            Assert.That((nint)descriptor.idAddr().iiaAddr, Is.EqualTo((nint)42));
            Assert.That(CurrentInstructionGroupSize(emitter),
                Is.EqualTo(initialGroupSize + (failure is null ? 8 : 0)));
        });
    }

#if DEBUG
    [TestCase(TYP_FLOAT, 0UL, RiscVRecorderDebugBoundary)]
    [TestCase(TYP_DOUBLE, 0UL, RiscVRecorderDebugBoundary)]
    [TestCase(TYP_FLOAT, 0x7FFUL, RiscVRecorderDebugBoundary)]
    [TestCase(TYP_DOUBLE, 0x7FFUL, RiscVRecorderDebugBoundary)]
#else
    [TestCase(TYP_FLOAT, 0UL, null)]
    [TestCase(TYP_DOUBLE, 0UL, null)]
    [TestCase(TYP_FLOAT, 0x7FFUL, null)]
    [TestCase(TYP_DOUBLE, 0x7FFUL, null)]
#endif
    [TestCase(TYP_FLOAT, 0x12345000UL, "Target register-immediate recording is not implemented.")]
    [TestCase(TYP_DOUBLE, 0x12345000UL, "Target register-immediate recording is not implemented.")]
    [TestCase(TYP_FLOAT, 0x80000000UL, "Target register-immediate recording is not implemented.")]
    public static void InlineFloatingConstantNodeDispatchPreservesRiscVInstructionRecordingBoundary(
        var_types type,
        ulong bits,
        string? expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var value = type is TYP_FLOAT
                ? BitConverter.UInt32BitsToSingle(unchecked((uint)bits))
                : BitConverter.UInt64BitsToDouble(bits);
            var constant = compiler.gtNewDconNode(type, value);
            constant.RegNum = REG_FA0;
            if (bits != 0)
            {
                codeGen.InternalRegisters.Add(
                    constant, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));
            }

            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(constant));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [TestCase(TYP_FLOAT, 0x7F800001UL)]
    [TestCase(TYP_FLOAT, 0x12345001UL)]
    [TestCase(TYP_DOUBLE, 0x8000000000000000UL)]
    [TestCase(TYP_DOUBLE, 0x3FF0000000000001UL)]
    public static void PooledFloatingConstantNodeDispatchRecordsRiscVEmbeddedDataDescriptor(
        var_types type,
        ulong bits)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var value = type is TYP_FLOAT
                ? BitConverter.UInt32BitsToSingle(unchecked((uint)bits))
                : BitConverter.UInt64BitsToDouble(bits);
            var constant = compiler.gtNewDconNode(type, value);
            constant.RegNum = REG_FA0;

            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(constant));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(type is TYP_FLOAT ? INS_flw : INS_fld));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_FA0));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_RC));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(8u));
            Assert.That(descriptor.idIsBound(), Is.True);
            Assert.That(Compiler.eeIsJitDataOffs(descriptor.idAddr().iiaFieldHnd), Is.True);
            Assert.That(emitter.emitConsDsc.dsdList, Is.Not.Null);
            Assert.That(CurrentInstructionGroupSize(emitter),
                Is.EqualTo(initialGroupSize + (failure is null ? 8 : 0)));
        });
    }

    [TestCase(TYP_BYREF)]
    [TestCase(TYP_I_IMPL)]
    public static void LocalAddressPreservesTheStackInstructionRecordingBoundary(var_types type)
    {
        WithCodeGen((_, codeGen) =>
        {
            var localAddress = new GenTreeLclFld(GT_LCL_ADDR, type, 0, 24)
            {
                RegNum = REG_A0,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForLclAddr(localAddress));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Target local-stack instruction recording is not implemented."));
        });
    }

#if DEBUG
    [TestCase(TYP_INT, TYP_LONG, RiscVRecorderDebugBoundary)]
    [TestCase(TYP_LONG, TYP_INT, RiscVRecorderDebugBoundary)]
#else
    [TestCase(TYP_INT, TYP_LONG, "Target conditional-branch recording is not implemented.")]
    [TestCase(TYP_LONG, TYP_INT, "Target conditional-branch recording is not implemented.")]
#endif
    [TestCase(TYP_LONG, TYP_LONG, "Target conditional-branch recording is not implemented.")]
    public static void RangeCheckNodeDispatchPreservesRiscVInstructionRecordingBoundary(
        var_types indexType,
        var_types lengthType,
        string expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            compiler.opts.compDbgCode = true;

            var index = new GenTreePhysReg(REG_A0, indexType) { RegNum = REG_A0 };
            var length = new GenTreePhysReg(REG_A1, lengthType) { RegNum = REG_A1 };
            var boundsCheck = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            if ((indexType is TYP_INT) || (lengthType is TYP_INT))
            {
                codeGen.InternalRegisters.Add(
                    boundsCheck, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));
            }

            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(boundsCheck));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void NullCheckNodeDispatchPreservesRiscVIndirectLoadRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var address = new GenTreePhysReg(REG_A0, TYP_I_IMPL) { RegNum = REG_A0 };
            var nullCheck = new GenTreeIndir(GT_NULLCHECK, TYP_INT, address);
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(nullCheck));

            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void BinaryOperandRecordingRemainsUnsupportedOnRiscV()
    {
        WithCodeGen((_, codeGen) =>
        {
            var dst = new GenTreePhysReg(REG_A0, TYP_INT) { RegNum = REG_A0 };
            var src = new GenTreePhysReg(REG_A1, TYP_INT) { RegNum = REG_A1 };

            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.Emitter.emitInsBinary(INS_add, EA_4BYTE, dst, src));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message, Does.Contain("Binary operand recording requires xarch."));
        });
    }

#if DEBUG
    [TestCase(8, RiscVRecorderDebugBoundary)]
    [TestCase(0x12345, RiscVRecorderDebugBoundary)]
#else
    [TestCase(8, null)]
    [TestCase(0x12345, null)]
#endif
    public static void LeaNodeDispatchRecordsRiscVImmediateInstruction(int offset, string? expectedBoundary)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var baseAddress = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A0 };
            var lea = new GenTreeAddrMode(TYP_BYREF, baseAddress, null, 0, offset) { RegNum = REG_A1 };
            if (!Emitter.isValidSimm12(offset))
            {
                codeGen.InternalRegisters.Add(
                    lea, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));
            }

            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(lea));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void LeaNodeDispatchSkipsAZeroOffsetWhenTheBaseIsTheTarget()
    {
        WithCodeGen((_, codeGen) =>
        {
            var baseAddress = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A0 };
            var lea = new GenTreeAddrMode(TYP_BYREF, baseAddress, null, 0, 0) { RegNum = REG_A0 };

            Assert.DoesNotThrow(() => codeGen.genCodeForTreeNode(lea));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GSSecurityCookieInitializationSkipsWhenNotNeeded(bool initiallyZeroed)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.compNeedsGSSecurityCookie = false;
            var zeroed = initiallyZeroed;

            codeGen.genSetGSSecurityCookie(REG_A0, ref zeroed);

            Assert.That(codeGen.Emitter.emitCurIG?.igInsCnt, Is.Zero);
            Assert.That(zeroed, Is.EqualTo(initiallyZeroed));
        });
    }

    [Test]
    public static void GSSecurityCookieInitializationSkipsWhenOsrFrameAlreadyHasCookie()
    {
        WithProlog((compiler, codeGen) =>
        {
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 64);
            patchpoint->SecurityCookieOffset = -24;
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.compNeedsGSSecurityCookie = true;
            var zeroed = true;

            codeGen.genSetGSSecurityCookie(REG_A0, ref zeroed);

            Assert.That(codeGen.Emitter.emitCurIG?.igInsCnt, Is.Zero);
            Assert.That(zeroed, Is.True);
        });
    }

    [Test]
    public static void GSSecurityCookieInitializationRecordsImmediateInstruction()
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.compNeedsGSSecurityCookie = true;
            compiler.gsGlobalSecurityCookieVal = 1;
            var zeroed = true;

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = "Target local-stack store recording is not implemented.";
#endif
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var failure = CaptureFatalJitException(
                () => codeGen.genSetGSSecurityCookie(REG_A0, ref zeroed));

            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
            Assert.That(zeroed, Is.True);
        });
    }

    [Test]
    public static void GSSecurityCookieInitializationRecordsRelocatedLoadBeforeStoreBoundary()
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.opts.compReloc = true;
            compiler.compNeedsGSSecurityCookie = true;
            compiler.gsGlobalSecurityCookieAddr = (nint*)0x12345678;
            var zeroed = true;

            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var failure = CaptureFatalJitException(() =>
                codeGen.genSetGSSecurityCookie(REG_A0, ref zeroed));

#if DEBUG
            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message, Does.Contain(RiscVRecorderDebugBoundary));
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount + 1));
            Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
#else
            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Target local-stack store recording is not implemented."));
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount + 1));
            Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize + 8));
#endif
            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_ld));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_A0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_A0));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_RELOC));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(8u));
            Assert.That((nint)descriptor.idAddr().iiaAddr, Is.EqualTo((nint)compiler.gsGlobalSecurityCookieAddr));
            Assert.That(zeroed, Is.True);
        });
    }

    [Test]
    public static void SelectNodeDispatchPreservesRiscVThreeRegisterRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zicond);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zicond);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zicond);

            var condition = new GenTreePhysReg(REG_A0, TYP_INT) { RegNum = REG_A0 };
            var trueValue = new GenTreePhysReg(REG_A1, TYP_INT) { RegNum = REG_A1 };
            var falseValue = new GenTreePhysReg(REG_A2, TYP_INT) { RegNum = REG_A2 };
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, trueValue, falseValue)
            {
                RegNum = REG_A3,
            };
            codeGen.InternalRegisters.Add(select, regMaskTP.CreateFromRegNum(REG_A4, REG_A4.SingleTypeMask));

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(select));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

#if DEBUG
    [TestCase(GT_LSH, TYP_INT, true, false, RiscVRecorderDebugBoundary)]
    [TestCase(GT_RSH, TYP_LONG, false, false, RiscVRecorderDebugBoundary)]
    [TestCase(GT_ROR, TYP_INT, false, false, RiscVRecorderDebugBoundary)]
    [TestCase(GT_ROR, TYP_LONG, false, true, RiscVRecorderDebugBoundary)]
    [TestCase(GT_ROL, TYP_INT, true, true, RiscVRecorderDebugBoundary)]
#else
    [TestCase(GT_LSH, TYP_INT, true, false, null)]
    [TestCase(GT_RSH, TYP_LONG, false, false, null)]
    [TestCase(GT_ROR, TYP_INT, false, false, null)]
    [TestCase(GT_ROR, TYP_LONG, false, true, null)]
    [TestCase(GT_ROL, TYP_INT, true, true, null)]
#endif
    public static void ShiftNodeDispatchRecordsRiscVImmediateInstruction(
        genTreeOps oper,
        var_types type,
        bool useImmediate,
        bool useZbb,
        string? expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            if (useZbb)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zbb);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zbb);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zbb);
            }

            var value = new GenTreePhysReg(REG_A0, type) { RegNum = REG_A0 };
            GenTree shiftBy;
            if (useImmediate)
            {
                shiftBy = compiler.gtNewIconNode(TYP_INT, 5);
                shiftBy.IsContained = true;
            }
            else
            {
                shiftBy = new GenTreePhysReg(REG_A1, TYP_INT) { RegNum = REG_A1 };
            }

            var shift = new GenTreeOp(oper, type, value, shiftBy) { RegNum = REG_A2 };
            if ((oper is GT_ROR or GT_ROL) && !useZbb)
            {
                codeGen.InternalRegisters.Add(
                    shift, regMaskTP.CreateFromRegNum(REG_A3, REG_A3.SingleTypeMask));
            }

            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(shift));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [TestCase(GT_SH1ADD)]
    [TestCase(GT_SH2ADD)]
    [TestCase(GT_SH3ADD)]
    [TestCase(GT_SH1ADD_UW)]
    [TestCase(GT_SH2ADD_UW)]
    [TestCase(GT_SH3ADD_UW)]
    public static void ShxaddNodeDispatchPreservesRiscVInstructionRecordingBoundary(genTreeOps oper)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);

            var left = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var right = new GenTreePhysReg(REG_A1, TYP_LONG) { RegNum = REG_A1 };
            var tree = new GenTreeOp(oper, TYP_LONG, left, right) { RegNum = REG_A2 };

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void AddUwNodeDispatchPreservesRiscVInstructionRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);

            var left = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var right = new GenTreePhysReg(REG_A1, TYP_LONG) { RegNum = REG_A1 };
            var tree = new GenTreeOp(GT_ADD_UW, TYP_LONG, left, right) { RegNum = REG_A2 };

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void SlliUwNodeDispatchRecordsRiscVImmediateInstruction()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);

            var value = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var shiftBy = compiler.gtNewIconNode(TYP_INT, 7);
            shiftBy.IsContained = true;

            var tree = new GenTreeOp(GT_SLLI_UW, TYP_LONG, value, shiftBy) { RegNum = REG_A2 };

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [TestCase(8)]
    [TestCase(16)]
    public static void CopyBlockUnrollRecordsRiscVImmediateInstructions(int size)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var destination = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A0 };
            var sourceAddress = new GenTreePhysReg(REG_A1, TYP_BYREF) { RegNum = REG_A1 };
            var source = new GenTreeIndir(GT_IND, TYP_STRUCT, sourceAddress) { IsContained = true };
            var block = new GenTreeBlk(TYP_STRUCT, destination, source, new ClassLayout((uint)size))
            {
                _kind = BlkOpKindUnroll,
            };
            codeGen.InternalRegisters.Add(
                block, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForCpBlkUnroll(block));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void StoreBlkInitLoopDispatchRecordsRiscVInitialStore()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var destination = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A0 };
            var block = new GenTreeBlk(
                TYP_STRUCT,
                destination,
                new GenTreeIntCon(TYP_INT, 0),
                new ClassLayout((uint)TARGET_POINTER_SIZE))
            {
                _kind = BlkOpKindLoop,
            };

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForStoreBlk(block));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

#if DEBUG
    [TestCase(false, RiscVRecorderDebugBoundary)]
    [TestCase(true, RiscVRecorderDebugBoundary)]
#else
    [TestCase(false, null)]
    [TestCase(true, null)]
#endif
    public static void IntegerCastDispatchRecordsRiscVExtensionInstruction(
        bool useZba,
        string? expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            if (useZba)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);
            }

            var source = new GenTreePhysReg(REG_A0, TYP_INT) { RegNum = REG_A0 };
            var cast = new GenTreeCast(TYP_LONG, source, fromUnsigned: true, castType: TYP_ULONG)
            {
                RegNum = REG_A1,
            };

            var failure = CaptureFatalJitException(() => codeGen.genCodeForCast(cast));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [TestCase(GT_ADD, INS_addi, 17, false, 17)]
    [TestCase(GT_ADD, INS_addi, 17, true, 17)]
    [TestCase(GT_SUB, INS_sub, 17, false, -17)]
    public static void TernaryImmediateRecordingPreservesOperandAndSubtractionSelection(
        genTreeOps oper,
        instruction ins,
        long immediate,
        bool constantFirst,
        int expectedImmediate)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var register = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var constant = compiler.gtNewIconNode(TYP_LONG, unchecked((nint)immediate));
            constant.IsContained = true;

            GenTree src1 = constantFirst ? constant : register;
            GenTree src2 = constantFirst ? register : constant;
            var dst = new GenTreeOp(oper, TYP_LONG, src1, src2) { RegNum = REG_A1 };
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var result = REG_NA;

            var failure = CaptureFatalJitException(
                () => result = emitter.emitInsTernary(ins, EA_8BYTE, dst, src1, src2));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var emitted = instructionBuffer.GetRange(initialInstructionCount,
                instructionBuffer.Count - initialInstructionCount);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo([INS_addi]));
            Assert.That(emitted[0].idSmallCns(), Is.EqualTo(expectedImmediate));

            if (failure is null)
            {
                Assert.That(result, Is.EqualTo(REG_A1));
            }
        });
    }

    [TestCase(GT_ADD, INS_add)]
    [TestCase(GT_SUB, INS_sub)]
    [TestCase(GT_MUL, INS_mul)]
    public static void TernaryRegisterRecordingPreservesRiscVInstructionSelection(genTreeOps oper, instruction ins)
    {
        WithCodeGen((_, codeGen) =>
        {
            var src1 = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var src2 = new GenTreePhysReg(REG_A1, TYP_LONG) { RegNum = REG_A1 };
            var dst = new GenTreeOp(oper, TYP_LONG, src1, src2) { RegNum = REG_A2 };
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var result = REG_NA;

            var failure = CaptureFatalJitException(
                () => result = emitter.emitInsTernary(ins, EA_8BYTE, dst, src1, src2));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var emitted = instructionBuffer.GetRange(initialInstructionCount,
                instructionBuffer.Count - initialInstructionCount);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo([ins]));

            if (failure is null)
            {
                Assert.That(result, Is.EqualTo(REG_A2));
            }
        });
    }

    [Test]
    public static void TernaryFloatingPointRecordingUsesThreeRegisterInstruction()
    {
        WithCodeGen((_, codeGen) =>
        {
            var src1 = new GenTreePhysReg(REG_FA0, TYP_FLOAT) { RegNum = REG_FA0 };
            var src2 = new GenTreePhysReg(REG_FA1, TYP_FLOAT) { RegNum = REG_FA1 };
            var dst = new GenTreeOp(GT_ADD, TYP_FLOAT, src1, src2) { RegNum = REG_FA2 };
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var result = REG_NA;

            var failure = CaptureFatalJitException(
                () => result = emitter.emitInsTernary(INS_fadd_s, EA_4BYTE, dst, src1, src2));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var emitted = instructionBuffer.GetRange(initialInstructionCount,
                instructionBuffer.Count - initialInstructionCount);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo([INS_fadd_s]));

            if (failure is null)
            {
                Assert.That(result, Is.EqualTo(REG_FA2));
            }
        });
    }

    [Test]
    public static void TernaryUnsignedWideAddZeroExtendsIntOperands()
    {
        WithCodeGen((_, codeGen) =>
        {
            var src1 = new GenTreePhysReg(REG_A0, TYP_INT) { RegNum = REG_A0 };
            var src2 = new GenTreePhysReg(REG_A1, TYP_INT) { RegNum = REG_A1 };
            var dst = new GenTreeOp(GT_ADD, TYP_LONG, src1, src2)
            {
                RegNum = REG_A2,
                Flags = GTF_UNSIGNED,
            };
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = CaptureFatalJitException(
                () => emitter.emitInsTernary(INS_add, EA_8BYTE, dst, src1, src2));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var emitted = instructionBuffer.GetRange(initialInstructionCount,
                instructionBuffer.Count - initialInstructionCount);
#if DEBUG
            Assert.That(emitted[0].idIns(), Is.EqualTo(INS_slli));
#else
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo([INS_slli, INS_srli, INS_slli, INS_srli, INS_add]));
#endif
        });
    }

    [Test]
    public static void TernaryOverflowAddPreservesSignedOverflowCheckSequence()
    {
        WithCodeGen((_, codeGen) =>
        {
            var src1 = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var src2 = new GenTreePhysReg(REG_A1, TYP_LONG) { RegNum = REG_A1 };
            var dst = new GenTreeOp(GT_ADD, TYP_LONG, src1, src2)
            {
                RegNum = REG_A2,
                Flags = GTF_OVERFLOW,
            };
            codeGen.InternalRegisters.Add(dst,
                regMaskTP.CreateFromRegNum(REG_A3, REG_A3.SingleTypeMask) |
                regMaskTP.CreateFromRegNum(REG_A4, REG_A4.SingleTypeMask));

            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = CaptureFatalJitException(
                () => emitter.emitInsTernary(INS_add, EA_8BYTE, dst, src1, src2));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = "Target conditional-branch recording is not implemented.";
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var emitted = instructionBuffer.GetRange(initialInstructionCount,
                instructionBuffer.Count - initialInstructionCount);
#if DEBUG
            Assert.That(emitted[0].idIns(), Is.EqualTo(INS_add));
#else
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo([INS_add, INS_slt, INS_slti]));
#endif
        });
    }

    [Test]
    public static void TernaryBitwiseIntResultPreservesRegisterExtensionBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var src1 = new GenTreePhysReg(REG_A0, TYP_INT) { RegNum = REG_A0 };
            var src2 = new GenTreePhysReg(REG_A1, TYP_INT) { RegNum = REG_A1 };
            var dst = new GenTreeOp(GT_AND, TYP_INT, src1, src2) { RegNum = REG_A2 };
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = CaptureFatalJitException(
                () => emitter.emitInsTernary(INS_and, EA_4BYTE, dst, src1, src2));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var emitted = instructionBuffer.GetRange(initialInstructionCount,
                instructionBuffer.Count - initialInstructionCount);
#if DEBUG
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo([INS_and]));
#else
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo([INS_and, INS_sext_w]));
#endif
        });
    }

    [TestCase(TYP_LONG, EA_8BYTE, false, INS_mul)]
    [TestCase(TYP_INT, EA_4BYTE, true, INS_mulw)]
    public static void TernaryOverflowMultiplyPreservesHighPartAndThrowBoundary(
        var_types type,
        emitAttr attr,
        bool isUnsigned,
        instruction ins)
    {
        WithCodeGen((_, codeGen) =>
        {
            var src1 = new GenTreePhysReg(REG_A0, type) { RegNum = REG_A0 };
            var src2 = new GenTreePhysReg(REG_A1, type) { RegNum = REG_A1 };
            var dst = new GenTreeOp(GT_MUL, type, src1, src2)
            {
                RegNum = REG_A2,
                Flags = GTF_OVERFLOW | (isUnsigned ? GTF_UNSIGNED : GTF_EMPTY),
            };

            var internalRegisters = regMaskTP.CreateFromRegNum(REG_A3, REG_A3.SingleTypeMask);
            if (!isUnsigned)
            {
                internalRegisters |= regMaskTP.CreateFromRegNum(REG_A4, REG_A4.SingleTypeMask);
            }

            codeGen.InternalRegisters.Add(dst, internalRegisters);

            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var result = REG_NA;

            var failure = CaptureFatalJitException(
                () => result = emitter.emitInsTernary(ins, attr, dst, src1, src2));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = "Target conditional-branch recording is not implemented.";
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var emitted = instructionBuffer.GetRange(initialInstructionCount,
                instructionBuffer.Count - initialInstructionCount);
#if DEBUG
            Assert.That(emitted[0].idIns(), Is.EqualTo(isUnsigned ? INS_slli : INS_mulh));
#else
            instruction[] expectedInstructions = isUnsigned
                ? [INS_slli, INS_slli, INS_mulhu, INS_srai, INS_mulw]
                : [INS_mulh, INS_mul, INS_srai];
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo(expectedInstructions));
#endif
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
        });
    }

    [Test]
    public static void IntegerCastOverflowDispatchPreservesRiscVTwoRegisterRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var source = new GenTreePhysReg(REG_A0, TYP_INT) { RegNum = REG_A0 };
            var cast = new GenTreeCast(TYP_LONG, source, fromUnsigned: false, castType: TYP_ULONG)
            {
                RegNum = REG_A1,
                Flags = GTF_OVERFLOW,
            };
            codeGen.InternalRegisters.Add(
                cast, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));

            var failure = CaptureFatalJitException(() => codeGen.genCodeForCast(cast));
#if DEBUG
            const string expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string expectedBoundary = "Target conditional-branch recording is not implemented.";
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void IntegerCastCopyDispatchPreservesRiscVRegisterRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var source = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var cast = new GenTreeCast(TYP_LONG, source, fromUnsigned: false, castType: TYP_LONG)
            {
                RegNum = REG_A1,
            };

            var failure = CaptureFatalJitException(() => codeGen.genCodeForCast(cast));
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

#if DEBUG
    [TestCase(TYP_FLOAT, TYP_DOUBLE, RiscVRecorderDebugBoundary)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, RiscVRecorderDebugBoundary)]
#else
    [TestCase(TYP_FLOAT, TYP_DOUBLE, null)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, null)]
#endif
#if DEBUG
    [TestCase(TYP_FLOAT, TYP_FLOAT, RiscVRecorderDebugBoundary)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE, RiscVRecorderDebugBoundary)]
#else
    [TestCase(TYP_FLOAT, TYP_FLOAT, null)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE, null)]
#endif
    public static void FloatToFloatCastDispatchPreservesRiscVRecordingBoundary(
        var_types sourceType,
        var_types destinationType,
        string? expectedBoundary)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var source = new GenTreePhysReg(REG_FA0, sourceType) { RegNum = REG_FA0 };
            var cast = new GenTreeCast(destinationType, source, fromUnsigned: false, castType: destinationType)
            {
                RegNum = REG_FA1,
            };

            var failure = CaptureFatalJitException(() => codeGen.genCodeForCast(cast));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void FloatToFloatCastWithSameRegisterDoesNotRecordRiscVInstruction()
    {
        WithCodeGen((_, codeGen) =>
        {
            var source = new GenTreePhysReg(REG_FA0, TYP_FLOAT) { RegNum = REG_FA0 };
            var cast = new GenTreeCast(TYP_FLOAT, source, fromUnsigned: false, castType: TYP_FLOAT)
            {
                RegNum = REG_FA0,
            };

            Assert.DoesNotThrow(() => codeGen.genCodeForCast(cast));
        });
    }

    [Test]
    public static void CatchReturnDispatchPreservesRiscVBlockLabelRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var block = new BasicBlock(null, null);
            var target = new BasicBlock(null, null);
            block.SetKindAndTargetEdge(BBKinds.BBJ_EHCATCHRET, new FlowEdge(block, target, null));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genEmitEndBlock(block));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 block-relative address recording is not ported."));
        });
    }

    [Test]
    public static void CalleeSavedRestoreRecordsRiscVImmediateInstruction()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var restoreMask = regMaskTP.CreateFromRegNum(REG_S1, REG_S1.SingleTypeMask);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(
                () => RestoreCalleeSavedRegisters(codeGen, restoreMask, REG_FP, 16, reportUnwindData: false));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void OSRPrologRecordsRiscVImmediateInstruction()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT, uwi = new UnwindInfo() }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();
            compiler.funCurrentFunc().GetUnwindInfo().InitUnwindInfo(compiler, null, null);

            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 96);
            patchpoint->CalleeSaveRegisters = (long)(SRBM_S1 | SRBM_FP | SRBM_RA);
            compiler.info.compPatchpointInfo = patchpoint;

            compiler.unwindBegProlog();

            var failure = CaptureFatalJitException(
                () => codeGen.genOSRHandleTier0CalleeSavedRegistersAndFrame());
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [TestCase(NI_PRIMITIVE_SaturateToInt8)]
    [TestCase(NI_PRIMITIVE_SaturateToInt16)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt8)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt16)]
    public static void SaturationIntrinsicDispatchPreservesRiscVRegisterRecordingBoundary(
        NamedIntrinsic intrinsic)
    {
#if DEBUG
        const string expectedBoundary = RiscVRecorderDebugBoundary;
#else
        const string expectedBoundary = "Target conditional-branch recording is not implemented.";
#endif
        WithCodeGen((compiler, codeGen) => AssertIntrinsicBoundary(
            compiler, codeGen, intrinsic, TYP_INT, hasSecondOperand: false,
            expectedBoundary: expectedBoundary));
    }

#if DEBUG
    [TestCase(NI_System_Math_Abs, TYP_FLOAT, RiscVRecorderDebugBoundary)]
    [TestCase(NI_System_Math_Abs, TYP_DOUBLE, RiscVRecorderDebugBoundary)]
#else
    [TestCase(NI_System_Math_Abs, TYP_FLOAT, null)]
    [TestCase(NI_System_Math_Abs, TYP_DOUBLE, null)]
#endif
#if DEBUG
    [TestCase(NI_System_Math_Sqrt, TYP_FLOAT, RiscVRecorderDebugBoundary)]
    [TestCase(NI_System_Math_Sqrt, TYP_DOUBLE, RiscVRecorderDebugBoundary)]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, TYP_INT, RiscVRecorderDebugBoundary)]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, TYP_LONG, RiscVRecorderDebugBoundary)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, TYP_INT, RiscVRecorderDebugBoundary)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, TYP_LONG, RiscVRecorderDebugBoundary)]
    [TestCase(NI_PRIMITIVE_PopCount, TYP_INT, RiscVRecorderDebugBoundary)]
    [TestCase(NI_PRIMITIVE_PopCount, TYP_LONG, RiscVRecorderDebugBoundary)]
#else
    [TestCase(NI_System_Math_Sqrt, TYP_FLOAT, null)]
    [TestCase(NI_System_Math_Sqrt, TYP_DOUBLE, null)]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, TYP_INT, null)]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, TYP_LONG, null)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, TYP_INT, null)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, TYP_LONG, null)]
    [TestCase(NI_PRIMITIVE_PopCount, TYP_INT, null)]
    [TestCase(NI_PRIMITIVE_PopCount, TYP_LONG, null)]
#endif
    public static void UnaryIntrinsicDispatchPreservesRiscVRegisterRecordingBoundary(
        NamedIntrinsic intrinsic,
        var_types type,
        string? expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) => AssertIntrinsicBoundary(
            compiler, codeGen, intrinsic, type, hasSecondOperand: false,
            expectedBoundary: expectedBoundary));
    }

    [TestCase(NI_System_Math_MinNative, TYP_FLOAT)]
    [TestCase(NI_System_Math_MinNative, TYP_DOUBLE)]
    [TestCase(NI_System_Math_MaxNative, TYP_FLOAT)]
    [TestCase(NI_System_Math_MaxNative, TYP_DOUBLE)]
    [TestCase(NI_System_Math_Min, TYP_INT)]
    [TestCase(NI_System_Math_MinUnsigned, TYP_INT)]
    [TestCase(NI_System_Math_Max, TYP_INT)]
    [TestCase(NI_System_Math_MaxUnsigned, TYP_INT)]
    public static void BinaryIntrinsicDispatchPreservesRiscVThreeRegisterRecordingBoundary(
        NamedIntrinsic intrinsic,
        var_types type)
    {
#if DEBUG
        const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
        const string? expectedBoundary = null;
#endif
        WithCodeGen((compiler, codeGen) => AssertIntrinsicBoundary(
            compiler, codeGen, intrinsic, type, hasSecondOperand: true,
            expectedBoundary: expectedBoundary));
    }

    private static void AssertIntrinsicBoundary(
        Compiler compiler,
        CodeGen codeGen,
        NamedIntrinsic intrinsic,
        var_types type,
        bool hasSecondOperand,
        string? expectedBoundary)
    {
        var isFloating = type is TYP_FLOAT or TYP_DOUBLE;
        var sourceRegister = isFloating ? REG_FT0 : REG_A0;
        var secondSourceRegister = isFloating ? REG_FT1 : REG_A1;
        var targetRegister = isFloating ? REG_FT2 : REG_A2;
        var source = new GenTreePhysReg(sourceRegister, type) { RegNum = sourceRegister };
        GenTreeIntrinsic tree;
        if (hasSecondOperand)
        {
            var secondSource = new GenTreePhysReg(secondSourceRegister, type) { RegNum = secondSourceRegister };
            tree = new GenTreeIntrinsic(type, source, secondSource, intrinsic, null);
        }
        else
        {
            tree = new GenTreeIntrinsic(type, source, intrinsic, null);
        }

        tree.RegNum = targetRegister;

        if (intrinsic is NI_PRIMITIVE_SaturateToInt8 or NI_PRIMITIVE_SaturateToInt16
            or NI_PRIMITIVE_SaturateToUInt8 or NI_PRIMITIVE_SaturateToUInt16)
        {
            codeGen.InternalRegisters.Add(tree,
                regMaskTP.CreateFromRegNum(REG_A3, REG_A3.SingleTypeMask));
        }

        var emitter = codeGen.Emitter;
        var instructionBuffer = CurrentInstructionBuffer(emitter)
            ?? throw new AssertionException("Missing current instruction buffer.");
        var initialInstructionCount = instructionBuffer.Count;
        var initialGroupSize = CurrentInstructionGroupSize(emitter);
        var failure = CaptureFatalJitException(() => codeGen.genIntrinsic(tree));
        AssertRiscVInstructionBoundary(
            codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
    }

    private static FatalJitException? CaptureFatalJitException(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (FatalJitException exception)
        {
            return exception;
        }
    }

#if PROFILING_SUPPORTED
    private static void AssertProfilerCallbackBoundary(FatalJitException? failure)
    {
        Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
#if DEBUG
        Assert.That(failure?.Message, Does.Contain(RiscVRecorderDebugBoundary));
#else
        Assert.That(failure?.Message, Does.Contain("Target call instruction recording is not implemented."));
#endif
    }
#endif

    private static void AssertRiscVInstructionBoundary(
        CodeGen codeGen,
        FatalJitException? failure,
        string? expectedBoundary,
        int initialInstructionCount,
        int initialGroupSize)
    {
        if (expectedBoundary is null or RiscVRecorderDebugBoundary)
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            Assert.That(instructionBuffer.Count, Is.GreaterThan(initialInstructionCount));

            if (expectedBoundary is null)
            {
                Assert.That(failure, Is.Null);
                Assert.That(CurrentInstructionGroupSize(emitter), Is.GreaterThan(initialGroupSize));
            }
            else
            {
                Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
                Assert.That(failure?.Message, Does.Contain(expectedBoundary));
            }

            return;
        }

        Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        Assert.That(failure?.Message, Does.Contain(expectedBoundary));
    }

    internal static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );

            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static void WithProlog(Action<Compiler, CodeGen> action)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var group = codeGen.Emitter.emitCurIG
                ?? throw new AssertionException("Missing instruction group.");
            group.igFlags |= InsGroupFlags.Prolog;
            action(compiler, codeGen);
        });
    }

#if PROFILING_SUPPORTED
    private static void WithProfilerCallbacks(Action<Compiler, CodeGen> action)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.info.compMatchedVM = false;
            codeGen.GCInfo.gcVarPtrSetCur = [0];
            codeGen.GCInfo.gcRegGCrefSetCur = default;
            codeGen.GCInfo.gcRegByrefSetCur = default;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;
            compiler.compLclFrameSize = 64;
            compiler.compCalleeRegsPushed = 2;

            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);
#endif

#if !DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genInstrWithConstant")]
    private static extern bool GenInstrWithConstant(
        CodeGen codeGen,
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        nint immediate,
        regNumber tempReg,
        bool inUnwindRegion);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentInstructionBuffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentInstructionGroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genRestoreCalleeSavedRegistersHelp")]
    private static extern void RestoreCalleeSavedRegisters(CodeGen codeGen, regMaskTP mask, regNumber baseReg,
        int offset, bool reportUnwindData);
}
#endif
