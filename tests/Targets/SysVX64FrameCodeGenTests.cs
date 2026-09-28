// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if UNIX_AMD64_ABI
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class SysVX64FrameCodeGenTests
{
    [TestCase(nameof(Compiler.unwindBegProlog))]
    [TestCase(nameof(Compiler.unwindEndProlog))]
    [TestCase(nameof(Compiler.unwindBegEpilog))]
    [TestCase(nameof(Compiler.unwindEndEpilog))]
    [TestCase(nameof(Compiler.unwindPush))]
    [TestCase(nameof(Compiler.unwindPush2))]
    [TestCase(nameof(Compiler.unwindAllocStack))]
    [TestCase(nameof(Compiler.unwindSetFrameReg))]
    [TestCase(nameof(Compiler.unwindSaveReg))]
    [TestCase(nameof(Compiler.unwindReserve))]
    [TestCase(nameof(Compiler.unwindEmit))]
    public static unsafe void NativeAotCfiUnwindRejectsBeforeRecordingOrPublication(string method)
    {
        WithProlog((compiler, _) =>
        {
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            Action<Compiler> record = method switch
            {
                nameof(Compiler.unwindBegProlog) => c => c.unwindBegProlog(),
                nameof(Compiler.unwindEndProlog) => c => c.unwindEndProlog(),
                nameof(Compiler.unwindBegEpilog) => c => c.unwindBegEpilog(),
                nameof(Compiler.unwindEndEpilog) => c => c.unwindEndEpilog(),
                nameof(Compiler.unwindPush) => c => c.unwindPush(REG_RBX),
                nameof(Compiler.unwindPush2) => c => c.unwindPush2(REG_RBX, REG_R12),
                nameof(Compiler.unwindAllocStack) => c => c.unwindAllocStack(16),
                nameof(Compiler.unwindSetFrameReg) => c => c.unwindSetFrameReg(REG_RBP, 0),
                nameof(Compiler.unwindSaveReg) => c => c.unwindSaveReg(REG_RBX, 0),
                nameof(Compiler.unwindReserve) => c => c.unwindReserve(),
                nameof(Compiler.unwindEmit) => c => c.unwindEmit(null, null),
                _ => throw new ArgumentOutOfRangeException(nameof(method)),
            };

            var exception = Assert.Throws<FatalJitException>(() => record(compiler));
            Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(exception?.Message, Does.Contain("CFI"));
            Assert.That(compiler.compGeneratingUnwindProlog, Is.False);
            Assert.That(compiler.compGeneratingUnwindEpilog, Is.False);
            Assert.That(compiler.funCurrentFunc().unwindCodes, Is.Null);
            Assert.That(compiler.funCurrentFunc().unwindCodeSlot, Is.Zero);
        });
    }

    [TestCase(0u, false)]
    [TestCase(256u, true)]
    public static void FramePointerRecordsSysVUnwindOffset(uint offset, bool extended)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.unwindBegProlog();
            codeGen.genEstablishFramePointer((int)offset, reportUnwindData: true);
            compiler.unwindEndProlog();

            var func = compiler.funCurrentFunc();
            var codes = func.unwindCodes ?? throw new AssertionException("Missing unwind codes.");
            var recorded = codes.AsSpan((int)func.unwindCodeSlot);
            Assert.That(func.unwindHeader.FrameRegister, Is.EqualTo((byte)REG_RBP));
            Assert.That(func.unwindHeader.FrameOffset, Is.EqualTo(extended ? 15 : 0));
            Assert.That(recorded[1] & 0x0f, Is.EqualTo(extended ? 11 : 3));
            if (extended)
            {
                Assert.That(BitConverter.ToUInt32(recorded.Slice(2, 4)), Is.EqualTo(16u));
            }
            Assert.That(Descriptors(codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[offset == 0 ? INS_mov : INS_lea]));
        });
    }

    [Test]
    public static void UnwindReservationIncludesSysVLargeFrameOffsetExtension()
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.unwindBegProlog();
            codeGen.genEstablishFramePointer(256, reportUnwindData: true);
            compiler.unwindEndProlog();
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags &= ~InsGroupFlags.Prolog;
            compiler.info.compMatchedVM = false;

            compiler.unwindReserve();

            var func = compiler.funCurrentFunc();
            var codes = func.unwindCodes ?? throw new AssertionException("Missing unwind codes.");
            var header = codes.AsSpan((int)func.unwindCodeSlot, 4);
            Assert.That(header[0] & 7, Is.EqualTo(1));
            Assert.That(header[2], Is.EqualTo(3));
            Assert.That(header[3] & 15, Is.EqualTo((byte)REG_RBP));
            Assert.That(header[3] >> 4, Is.EqualTo(15));
            Assert.That(codes[func.unwindCodeSlot + 5] & 15, Is.EqualTo(11));
        });
    }

    [Test]
    public static void PrologAndEpilogSaveOnlySysVNonvolatileIntegersInReverseOrder()
    {
        WithProlog((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = false;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_R12 | RBM_RDI);
            compiler.compCalleeRegsPushed = 2;
            Assert.That(codeGen.RegSet.rsGetModifiedIntCalleeSavedRegsMask(),
                Is.EqualTo(new regMaskTP(SRBM_RBX | SRBM_R12)));
            compiler.unwindBegProlog();
            var zeroed = false;
            codeGen.genPushCalleeSavedRegisters(REG_RAX, ref zeroed);
            compiler.unwindEndProlog();

            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags &= ~InsGroupFlags.Prolog;
            group.igFlags |= InsGroupFlags.Epilog;
            codeGen.genPopCalleeSavedRegisters();

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_push, INS_push, INS_pop, INS_pop]));
            Assert.That(ids.Select(id => id.idReg1()),
                Is.EqualTo((regNumber[])[REG_R12, REG_RBX, REG_RBX, REG_R12]));
            Assert.That(compiler.funCurrentFunc().unwindCodes, Is.Not.Null);
        });
    }

    [TestCase(8u, INS_push, false)]
    [TestCase(64u, INS_sub, false)]
    [TestCase(4088u, INS_sub, false)]
    public static void LocalFrameAllocationRecordsStackAndProbesNearPageBoundary(
        uint size, instruction expected, bool probes)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.osPageSize = 4096;
            compiler.unwindBegProlog();
            var zeroed = false;
            codeGen.genAllocLclFrame(size, REG_RAX, ref zeroed, default);
            compiler.unwindEndProlog();

            var ids = Descriptors(codeGen);
            Assert.That(ids[0].idIns(), Is.EqualTo(expected));
            Assert.That(ids.Any(id => id.idIns() == INS_test), Is.EqualTo(probes));
            Assert.That(compiler.funCurrentFunc().unwindCodes, Is.Not.Null);
        });
    }

    [Test]
    public static void PageSizedFrameUsesStackProbeHelperBeforeInstallingStackPointer()
    {
        WithProlog((compiler, codeGen) =>
        {
            using var callbacks = new SysVX64HelperCodeGenTests.HelperCallbacks(compiler, indirect: false);
            compiler.eeInfo.osPageSize = 4096;
            compiler.unwindBegProlog();
            var zeroed = true;

            codeGen.genAllocLclFrame(4096, REG_RAX, ref zeroed, default);
            compiler.unwindEndProlog();

            Assert.That(SysVX64HelperCodeGenTests.HelperCallbacks.Assertions, Is.Zero);
            Assert.That(SysVX64HelperCodeGenTests.HelperCallbacks.Helper,
                Is.EqualTo(CorInfoHelpFunc.CORINFO_HELP_STACK_PROBE));
            Assert.That(Descriptors(codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_lea, INS_call, INS_mov]));
            Assert.That(zeroed, Is.False);
            Assert.That(compiler.funCurrentFunc().unwindCodes, Is.Not.Null);
        });
    }

    [Test]
    public static void VectorFrameInitializationUsesSysVNonargumentXmm8()
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.info.compInitMem = true;
            codeGen.IsFramePointerUsed = true;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = var_types.TYP_STRUCT, Layout = new ClassLayout(32),
                lvOnFrame = true, lvFramePointerBased = true, StackOffset = -32, RegNum = REG_STK,
            };
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags &= ~InsGroupFlags.Prolog;
            codeGen.genCheckUseBlockInit();
            Assert.That(codeGen.UseBlockInit, Is.True);
            group.igFlags |= InsGroupFlags.Prolog;
            var zeroed = false;

            codeGen.genZeroInitFrame(-16, -32, REG_RAX, ref zeroed);

            var xor = Descriptors(codeGen).Single(id => id.idIns() == INS_xorps);
            Assert.That(xor.idReg1(), Is.EqualTo(REG_XMM8));
        });
    }

    [Test]
    public static void SmallSysVFrameInitializationClearsStackLocalWithoutBlockInit()
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.info.compInitMem = true;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = var_types.TYP_LONG,
                lvOnFrame = true, lvFramePointerBased = true, StackOffset = -32, RegNum = REG_STK,
            };
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags &= ~InsGroupFlags.Prolog;
            codeGen.genCheckUseBlockInit();
            Assert.That(codeGen.UseBlockInit, Is.False);
            group.igFlags |= InsGroupFlags.Prolog;
            var zeroed = false;

            codeGen.genZeroInitFrame(-24, -32, REG_RAX, ref zeroed);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_xor, INS_mov]));
            Assert.That(zeroed, Is.True);
        });
    }

    [Test]
    public static void RootEpilogRestoresSysVCalleeSavesAndFramePointer()
    {
        WithProlog((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = true;
            compiler.compLclFrameSize = 32;
            compiler.compCalleeFPRegsSavedMask = 0;
            Assert.That(new regMaskTP(compiler.compCalleeFPRegsSavedMask).IsEmpty, Is.True);
            compiler.compCalleeRegsPushed = 2;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_R12);
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags &= ~InsGroupFlags.Prolog;
            group.igFlags |= InsGroupFlags.Epilog;

            codeGen.genFnEpilog(new BasicBlock(null, null));

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_add, INS_pop, INS_pop, INS_pop, INS_ret]));
            Assert.That(Descriptors(codeGen).Where(id => id.idIns() == INS_pop).Select(id => id.idReg1()),
                Is.EqualTo((regNumber[])[REG_RBX, REG_R12, REG_RBP]));
        });
    }

    [Test]
    public static unsafe void OsrEpilogRestoresSysVAdditionalSavesBeforeTierZeroSaves()
    {
        WithProlog((compiler, codeGen) =>
        {
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 96);
            patchpoint->CalleeSaveRegisters = (long)(SRBM_RBX | SRBM_RBP);
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.compLclFrameSize = 32;
            compiler.compCalleeFPRegsSavedMask = 0;
            compiler.compCalleeRegsPushed = 0;
            codeGen.IsFramePointerUsed = true;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_R12);
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags &= ~InsGroupFlags.Prolog;
            group.igFlags |= InsGroupFlags.Epilog;

            codeGen.genFnEpilog(new BasicBlock(null, null));

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo((instruction[])[INS_add, INS_pop, INS_pop, INS_pop, INS_ret]));
            Assert.That(ids.Where(id => id.idIns() == INS_pop).Select(id => id.idReg1()),
                Is.EqualTo((regNumber[])[REG_R12, REG_RBX, REG_RBP]));
        });
    }

    [TestCase(0, 8, INS_push)]
    [TestCase(8, 8, INS_push)]
    [TestCase(16, 24, INS_sub)]
    public static void FuncletAllocatesSysVOutgoingSpaceWithoutWindowsShadowSlots(
        int outgoing, int adjustment, instruction allocation)
    {
        WithProlog((compiler, codeGen) =>
        {
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            var entry = compiler.compCurBB ?? throw new AssertionException("Missing entry block.");
            var handler = new BasicBlock(null, null) { bbEmitCookie = group, HndIndex = 0 };
            entry.Next = handler;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = handler;
            compiler.fgFirstFuncletBB = handler;
            compiler.compCurBB = handler;
            compiler.compHndBBtab = [new EHblkDsc { ebdHndBeg = handler, ebdHndLast = handler }];
            compiler.compHndBBtabCount = 1;
            compiler.compFuncInfos =
            [
                new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT },
                new FuncInfoDsc { funKind = FuncKind.FUNC_HANDLER, funEHIndex = 0 },
            ];
            compiler.compFuncInfoCount = 2;
            compiler.funSetCurrentFunc(1);
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = outgoing;
            codeGen.IsFramePointerUsed = true;
            group.igFlags &= ~InsGroupFlags.Prolog;
            group.igFlags |= InsGroupFlags.FuncletProlog;

            codeGen.genCaptureFuncletPrologEpilogInfo();
            codeGen.genFuncletProlog(handler);
            group.igFlags &= ~InsGroupFlags.FuncletProlog;
            group.igFlags |= InsGroupFlags.FuncletEpilog;
            codeGen.genFuncletEpilog(handler);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()),
                Is.EqualTo((instruction[])[allocation, INS_add, INS_ret]));
            Assert.That(InstructionConstant(codeGen.Emitter, ids[1]), Is.EqualTo((nint)adjustment));
            Assert.That(compiler.compGeneratingUnwindProlog, Is.False);
            Assert.That(compiler.funCurrentFunc().unwindCodes, Is.Not.Null);
        });
    }

    internal static void WithProlog(Action<Compiler, CodeGen> action, bool minopts = true)
    {
        SysVX64EmitterCallTests.WithEmitter((compiler, emitter) =>
        {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.osPageSize = 4096;
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
            var codeGen = compiler.codeGen as CodeGen ?? throw new AssertionException("Missing code generator.");
            LastIntRegister(compiler) = REG_R15;
            codeGen.CopyRegisterInfo();
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags |= InsGroupFlags.Prolog | InsGroupFlags.OutOfOrderHead;
            action(compiler, codeGen);
        }, minopts);
    }

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        return CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing instruction descriptors.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "regIntLast")]
    private static extern ref regNumber LastIntRegister(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint InstructionConstant(Emitter emitter, Emitter.instrDesc descriptor);
}
#endif
