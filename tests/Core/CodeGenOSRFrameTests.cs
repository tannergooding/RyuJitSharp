// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenOSRFrameTests
{
    [Test]
    public static void Tier0PushesAndFrameAllocationAreReconstructedAtOffsetZero()
    {
        WithOSRFrame(SRBM_EBP | SRBM_R12 | SRBM_EBX, (compiler, codeGen) =>
        {
            codeGen.genOSRHandleTier0CalleeSavedRegistersAndFrame();

            Assert.That(CodeGenShiftTests.Descriptors(codeGen), Is.Empty);
            byte[] expected = [0, 0x82, 0, 0x30, 0, 0xC0, 0, 0x50];
            Assert.That(Codes(in compiler.funCurrentFunc()), Is.EqualTo(expected));
        });
    }

    [TestCase(false, 112)]
    [TestCase(true, 120)]
    public static void AdditionalSavesUseReservedTier0SlotsInDescendingRegisterOrder(
        bool framePointer, int firstOffset)
    {
        WithOSRFrame(SRBM_EBP | SRBM_EBX, (compiler, codeGen) =>
        {
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = framePointer;
            compiler.compLclFrameSize = 32;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_R12 | RBM_R13 | RBM_XMM6);

            codeGen.genOSRSaveRemainingCalleeSavedRegisters();

            var ids = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idReg1()), Is.EqualTo((regNumber[])[REG_R13, REG_R12]));
            Assert.That(ids.All(id => id.idIns() == INS_mov
                && id.idAddr().iiaAddrMode.amBaseReg == REG_RSP), Is.True);
            Assert.That(ids.Select(id => Displacement(codeGen.Emitter, id)),
                Is.EqualTo((nint[])[firstOffset, firstOffset - REGSIZE_BYTES]));

            var codes = Codes(in compiler.funCurrentFunc());
            Assert.That(codes, Has.Length.EqualTo(8));
            Assert.That(codes[1..4], Is.EqualTo((byte[])[0xC4, (byte)((firstOffset - 8) / 8), 0]));
            Assert.That(codes[5..8], Is.EqualTo((byte[])[0xD4, (byte)(firstOffset / 8), 0]));
        });
    }

    [Test]
    public static void AlreadySavedRegistersDoNotGenerateAnotherSave()
    {
        WithOSRFrame(SRBM_EBP | SRBM_R12, (compiler, codeGen) =>
        {
            codeGen.RegSet.rsSetRegsModified(RBM_R12);

            codeGen.genOSRSaveRemainingCalleeSavedRegisters();

            Assert.That(CodeGenShiftTests.Descriptors(codeGen), Is.Empty);
            Assert.That(Codes(in compiler.funCurrentFunc()), Is.Empty);
        });
    }

    private static void WithOSRFrame(regMask tier0Saves, Action<Compiler, CodeGen> action)
    {
        UnwindPrologRecordingTests.WithProlog((compiler, codeGen) =>
        {
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 96);
            patchpoint->CalleeSaveRegisters = (long)tier0Saves;
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.info.compLocalsCount = 1;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.compCalleeRegsPushed = 0;
            LastIntegerRegister(compiler) = REG_R15;
            codeGen.CopyRegisterInfo();

            action(compiler, codeGen);
        });
    }

    private static byte[] Codes(in FuncInfoDsc func)
    {
        var storage = func.unwindCodes ?? throw new AssertionException("Missing OSR unwind records.");
        return storage.AsSpan((int)func.unwindCodeSlot).ToArray();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "regIntLast")]
    private static extern ref regNumber LastIntegerRegister(Compiler compiler);
}
