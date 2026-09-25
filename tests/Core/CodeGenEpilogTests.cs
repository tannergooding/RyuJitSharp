// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenEpilogTests
{
    [Test]
    public static void StandardPopsRestoreRegistersInNativeAscendingOrder()
    {
        WithEpilog((_, codeGen) =>
        {
            codeGen.IsFramePointerUsed = false;
            var count = codeGen.genPopCalleeSavedRegistersFromMask(
                RBM_RBX | RBM_RBP | RBM_RSI | RBM_RDI | RBM_R12 | RBM_R13 | RBM_R14 | RBM_R15);

            Assert.That(count, Is.EqualTo(8u));
            Assert.That(Descriptors(codeGen).Select(id => id.idReg1()),
                Is.EqualTo((regNumber[])[REG_RBX, REG_RBP, REG_RSI, REG_RDI, REG_R12, REG_R13, REG_R14, REG_R15]));
            Assert.That(Descriptors(codeGen).All(id => id.idIns() == INS_pop), Is.True);
        });
    }

    [TestCase(false, 4, 2)]
    [TestCase(true, 3, 3)]
    public static void ApxPopsUseFirstOddRegisterThenPairsAndFinalAlignmentPop(
        bool framePointer, int expectedInstructions, int expectedPairCount)
    {
        WithEpilog((_, codeGen) =>
        {
            codeGen.IsFramePointerUsed = framePointer;
            codeGen.Emitter.UsePromotedEvexEncodings = true;
            var count = codeGen.genPopCalleeSavedRegistersFromMaskAPX(
                RBM_RBX | RBM_RSI | RBM_RDI | RBM_R12 | RBM_R13 | RBM_R14);

            var ids = Descriptors(codeGen);
            Assert.That(count, Is.EqualTo(6u));
            Assert.That(ids, Has.Count.EqualTo(expectedInstructions));
            Assert.That(ids.Count(id => id.idIns() == INS_pop2), Is.EqualTo(expectedPairCount));
            Assert.That(ids[^1].idIns(), Is.EqualTo(framePointer ? INS_pop2 : INS_pop));
            if (!framePointer)
            {
                Assert.That(ids[^1].idReg1(), Is.EqualTo(REG_RBX));
            }
        });
    }

    [TestCase(0, 8)]
    [TestCase(32, 40)]
    [TestCase(40, 40)]
    public static void FuncletCaptureAlignsReturnAddressAndOutgoingSpace(int outgoing, int delta)
    {
        WithEpilog((compiler, codeGen) =>
        {
            compiler.compHndBBtabCount = 1;
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = outgoing;
            codeGen.genCaptureFuncletPrologEpilogInfo();

            codeGen.genFuncletEpilog(new BasicBlock(null, null));

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_add, INS_ret]));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[0]), Is.EqualTo((nint)delta));
        });
    }

    [Test]
    public static void RootEpilogDeallocatesFrameBeforePopsAndReturn()
    {
        WithEpilog((compiler, codeGen) =>
        {
            compiler.compCalleeFPRegsSavedMask = 0;
            compiler.compLclFrameSize = 32;
            compiler.compCalleeRegsPushed = 1;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX);
            var block = new BasicBlock(null, null);

            codeGen.genFnEpilog(block);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_add, INS_pop, INS_pop, INS_ret]));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[0]), Is.EqualTo((nint)32));
            Assert.That(ids[1].idReg1(), Is.EqualTo(REG_RBX));
            Assert.That(ids[2].idReg1(), Is.EqualTo(REG_RBP));
        });
    }

    [Test]
    public static void OsrEpilogRestoresAdditionalSavesBeforeTier0SavesAndFramePointer()
    {
        WithEpilog((compiler, codeGen) =>
        {
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 96);
            patchpoint->CalleeSaveRegisters = (long)(SRBM_RBX | SRBM_RBP);
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.compCalleeRegsPushed = 0;
            compiler.compLclFrameSize = 32;
            compiler.compCalleeFPRegsSavedMask = 0;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_R12);

            codeGen.genFnEpilog(new BasicBlock(null, null));

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_add, INS_pop, INS_pop, INS_pop, INS_ret]));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[0]), Is.EqualTo((nint)120));
            Assert.That(ids.Skip(1).Take(3).Select(id => id.idReg1()),
                Is.EqualTo((regNumber[])[REG_R12, REG_RBX, REG_RBP]));
        });
    }

    [TestCase(false, 32)]
    [TestCase(true, 64)]
    public static void LocallocAndEnCUseReportedFramePointerLeaBeforeCalleePops(bool enc, int leaDisplacement)
    {
        WithEpilog((compiler, codeGen) =>
        {
            compiler.compLocallocUsed = !enc;
            compiler.opts.compDbgEnC = enc;
            compiler.compLclFrameSize = 64;
            compiler.compCalleeFPRegsSavedMask = 0;
            compiler.compCalleeRegsPushed = 1;
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = 32;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX);

            codeGen.genFnEpilog(new BasicBlock(null, null));

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_lea, INS_pop, INS_pop, INS_ret]));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RSP));
            Assert.That(ids[0].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RBP));
            Assert.That(Displacement(codeGen.Emitter, ids[0]), Is.EqualTo((nint)leaDisplacement));
        });
    }

    [Test]
    public static void FuncletPrologRecordsOutgoingFrameAllocationForUnwind()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var prolog = emitter.emitGetFirstPrologIG();
            emitter.emitCurIG = prolog;
            prolog.igFlags |= InsGroupFlags.FuncletProlog;
            var block = new BasicBlock(null, null) { bbEmitCookie = prolog, HndIndex = 0 };
            compiler.compHndBBtab = [new EHblkDsc { ebdHndBeg = block, ebdHndLast = block }];
            compiler.compHndBBtabCount = 1;
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_HANDLER, funEHIndex = 0 }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.osPageSize = 4096;
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = 32;
            codeGen.genCaptureFuncletPrologEpilogInfo();

            codeGen.genFuncletProlog(block);

            var func = compiler.funCurrentFunc();
            Assert.That(compiler.compGeneratingUnwindProlog, Is.False);
            Assert.That(func.startLoc?.GetIG(), Is.SameAs(prolog));
            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_sub]));
            Assert.That(InstructionConstant(emitter, Descriptors(codeGen).Single()), Is.EqualTo((nint)40));
            byte[] expected = [(byte)Descriptors(codeGen).Single().idCodeSize(), 0x42];
            var storage = func.unwindCodes ?? throw new AssertionException("Unwind codes were not initialized.");
            Assert.That(storage.AsSpan((int)func.unwindCodeSlot).ToArray(), Is.EqualTo(expected));
        });
    }

    private static void WithEpilog(Action<Compiler, CodeGen> action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.compCalleeFPRegsSavedMask = 0;
            LastIntegerRegister(compiler) = REG_R15;
            codeGen.CopyRegisterInfo();
            var emitter = codeGen.Emitter;
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags |= InsGroupFlags.Epilog | InsGroupFlags.OutOfOrderHead;
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "regIntLast")]
    private static extern ref regNumber LastIntegerRegister(Compiler compiler);
}
