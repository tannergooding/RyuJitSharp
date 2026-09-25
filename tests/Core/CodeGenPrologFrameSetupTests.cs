// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenPrologFrameSetupTests
{
    [Test]
    public static void VarargsHomesAllIncomingIntegerRegistersAboveTheReturnAddress()
    {
        WithProlog((_, codeGen) =>
        {
            codeGen.Emitter.spillIntArgRegsToShadowSlots();

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idReg1()), Is.EqualTo((regNumber[])[REG_RCX, REG_RDX, REG_R8, REG_R9]));
            Assert.That(ids.Select(id => Displacement(codeGen.Emitter, id)), Is.EqualTo((nint[])[8, 16, 24, 32]));
            Assert.That(ids.All(id => id.idIns() == INS_mov && id.idOpSize() == EA_8BYTE
                && id.idAddr().iiaAddrMode.amBaseReg == REG_RSP
                && id.idAddr().iiaAddrMode.amIndxReg == REG_NA), Is.True);
        });
    }

    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(32, false)]
    [TestCase(32, true)]
    [TestCase(240, true)]
    public static void FramePointerSetupUsesMoveOrLeaAndOptionalUnwind(int delta, bool report)
    {
        WithProlog((_, codeGen) =>
        {
            codeGen.genEstablishFramePointer(delta, report);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(delta == 0 ? INS_mov : INS_lea));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RBP));
            if (delta == 0)
            {
                Assert.That(id.idReg2(), Is.EqualTo(REG_RSP));
            }
            else
            {
                Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RSP));
                Assert.That(Displacement(codeGen.Emitter, id), Is.EqualTo((nint)delta));
            }
        });
    }

    [TestCase(0u)]
    [TestCase(8u)]
    [TestCase(16u)]
    [TestCase(32u)]
    [TestCase(4088u)]
    public static void SmallFramesPreserveScratchAndUseTheNativeSinglePushOptimization(uint size)
    {
        WithProlog((_, codeGen) =>
        {
            var zeroed = true;
            codeGen.genAllocLclFrame(size, REG_RAX, ref zeroed, RBM_NONE);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(size == 0 ? 0 : 1));
            Assert.That(zeroed, Is.True);
            if (size != 0)
            {
                Assert.That(ids[0].idIns(), Is.EqualTo(size == 8 ? INS_push : INS_sub));
                Assert.That(ids[0].idReg1(), Is.EqualTo(size == 8 ? REG_RAX : REG_RSP));
                if (size != 8)
                {
                    Assert.That(InstructionConstant(codeGen.Emitter, ids[0]), Is.EqualTo((nint)size));
                }
            }
        });
    }

    [TestCase(4096u, REG_RAX, false)]
    [TestCase(8192u, REG_R11, false)]
    [TestCase(8192u, REG_R10, true)]
    public static void LargeFramesProbeBeforeMovingSpAndInvalidateOnlyNativeScratchRegisters(
        uint size, regNumber scratch, bool expectedZero)
    {
        WithProlog((_, codeGen) =>
        {
            var zeroed = true;
            var first = codeGen.Emitter.emitCurIG;
            codeGen.genAllocLclFrame(size, scratch, ref zeroed, RBM_RCX | RBM_RDX);

            var ids = CodeGenLocalHeapTests.AllDescriptors(first, codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_lea, INS_call, INS_mov]));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_STACK_PROBE_HELPER_ARG));
            Assert.That(Displacement(codeGen.Emitter, ids[0]), Is.EqualTo(-(nint)size));
            Assert.That(ids[^1].idReg1(), Is.EqualTo(REG_RSP));
            Assert.That(ids[^1].idReg2(), Is.EqualTo(REG_STACK_PROBE_HELPER_ARG));
            Assert.That(zeroed, Is.EqualTo(expectedZero));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IntegerPushesDescendAndExcludeTheSeparatelySavedFramePointer(bool framePointer)
    {
        WithProlog((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = framePointer;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_RBP | RBM_R12 | RBM_XMM6);
            compiler.compCalleeRegsPushed = framePointer ? 2 : 3;
            var zeroed = true;

            codeGen.genPushCalleeSavedRegisters(REG_RAX, ref zeroed);

            Assert.That(Descriptors(codeGen).Select(id => id.idReg1()), Is.EqualTo(framePointer
                ? (regNumber[])[REG_R12, REG_RBX] : [REG_R12, REG_RBP, REG_RBX]));
            Assert.That(Descriptors(codeGen).All(id => id.idIns() == INS_push), Is.True);
            Assert.That(zeroed, Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ApxPairsKeepAlignmentAndNativeRegisterOrder(bool framePointer, bool includesRbp)
    {
        WithProlog((_, codeGen) =>
        {
            codeGen.IsFramePointerUsed = framePointer;
            codeGen.Emitter.UsePromotedEvexEncodings = true;
            var mask = RBM_RBX | RBM_R12 | RBM_R13;
            if (includesRbp)
            {
                mask |= RBM_RBP;
            }

            codeGen.genPushCalleeSavedRegistersFromMaskAPX(mask);

            var ids = Descriptors(codeGen);
            var registers = ids.SelectMany(id => id.idIns() == INS_push2
                ? new[] { id.idReg1(), id.idReg2() } : [id.idReg1()]).ToArray();
            var expected = framePointer
                ? (includesRbp ? (regNumber[])[REG_R13, REG_R12, REG_RBP, REG_RBX] : [REG_R13, REG_R12, REG_RBX])
                : (includesRbp ? (regNumber[])[REG_RBP, REG_R13, REG_R12, REG_RBX] : [REG_RBX, REG_R13, REG_R12]);
            Assert.That(registers, Is.EqualTo(expected));
            Assert.That(ids.Count(id => id.idIns() == INS_push2), Is.EqualTo(framePointer && includesRbp ? 2 : 1));
        });
    }

    [TestCase(false, 0, 88, 64)]
    [TestCase(true, 0, 80, 64)]
    [TestCase(true, 1, 88, 64)]
    public static void FloatingSavesUseDescendingAlignedOffsetsWithoutWideningToYmm(
        bool framePointer, int integerSaves, int frameSize, int expectedOffset)
    {
        WithProlog((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = framePointer;
            compiler.compCalleeRegsPushed = integerSaves;
            compiler.compLclFrameSize = frameSize;
            compiler.compCalleeFPRegsSavedMask = SRBM_XMM6 | SRBM_XMM8;

            codeGen.genPreserveCalleeSavedFltRegs();

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idReg1()), Is.EqualTo((regNumber[])[REG_XMM6, REG_XMM8]));
            Assert.That(ids.All(id => id.idOpSize() == EA_16BYTE), Is.True);
            Assert.That(ids.Select(id => Displacement(codeGen.Emitter, id)),
                Is.EqualTo((nint[])[expectedOffset, expectedOffset - 16]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FloatingRestoreUsesFramePointerOnlyForLocalloc(bool localloc)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.compLocallocUsed = localloc;
            compiler.compLclFrameSize = 80;
            compiler.compCalleeRegsPushed = 0;
            compiler.compCalleeFPRegsSavedMask = SRBM_XMM6;
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = 32;

            codeGen.genRestoreCalleeSavedFltRegs();

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(localloc ? REG_RBP : REG_RSP));
            Assert.That(Displacement(codeGen.Emitter, id), Is.EqualTo((nint)(localloc ? 32 : 64)));
        });
    }

    [TestCase(false, false, 0, 0)]
    [TestCase(true, false, 1, 0)]
    [TestCase(false, true, 0, 1)]
    [TestCase(true, true, 0, 1)]
    public static void AvxClearingDistinguishesCallsFromWideInstructions(bool call, bool wide, int prolog, int epilog)
    {
        WithProlog((_, codeGen) =>
        {
            codeGen.Emitter.ContainsCallNeedingVzeroupper = call;
            codeGen.Emitter.Contains256BitOrMoreAvxInstruction = wide;
            codeGen.Emitter.UseVexEncodings = true;

            codeGen.genClearAvxStateInProlog();
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(prolog));
            codeGen.genClearAvxStateInEpilog();
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(prolog + epilog));
            Assert.That(Descriptors(codeGen).All(id => id.idIns() == INS_vzeroupper), Is.True);
        });
    }

    private static void WithProlog(Action<Compiler, CodeGen> action)
    {
        UnwindPrologRecordingTests.WithProlog((compiler, codeGen) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.osPageSize = 4096;
            LastIntegerRegister(compiler) = REG_R15;
            codeGen.CopyRegisterInfo();
            codeGen.resetFramePointerUsedWritePhase();
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "regIntLast")]
    private static extern ref regNumber LastIntegerRegister(Compiler compiler);
}
