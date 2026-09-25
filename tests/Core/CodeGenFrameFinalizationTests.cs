// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenFrameFinalizationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void FinalizationSeparatesIntegerPushesFromFloatingSaveSlots(bool framePointer)
    {
        WithFrame((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = framePointer;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_RSI | RBM_XMM6);

            codeGen.genFinalizeFrame();

            Assert.That(compiler.compCalleeRegsPushed, Is.EqualTo(2));
            Assert.That(compiler.compCalleeFPRegsSavedMask, Is.EqualTo(SRBM_XMM6));
            Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.FINAL_FRAME_LAYOUT));
            Assert.That(compiler.lvaTable[1].StackOffset, Is.Zero);
            Assert.That(compiler.lvaTable[1].lvFramePointerBased, Is.False);
        });
    }

    [Test]
    public static void EntryLocationsAreRestoredBeforeInitializationIsCounted()
    {
        WithFrame((compiler, codeGen) =>
        {
            ref var local = ref compiler.lvaTable[0];
            local.Type = TYP_REF;
            local.lvTracked = true;
            local.lvLRACandidate = true;
            assert(compiler.fgFirstBB is not null);
            compiler.fgFirstBB.bbLiveIn = VarSetOps.MakeSingleton(compiler, 0);
            var calls = 0;
            Allocator(compiler) = new EntryAllocator(block =>
            {
                Assert.That(block, Is.SameAs(compiler.fgFirstBB));
                Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.TENTATIVE_FRAME_LAYOUT));
                compiler.lvaTable[0].RegNum = REG_RBX;
                calls++;
            });
            codeGen.RegSet.rsSetRegsModified(RBM_RBX);

            codeGen.genFinalizeFrame();

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(local.lvMustInit, Is.True);
            Assert.That(codeGen.InitStkLclCnt, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SpecialFramesMarkRequiredCalleeSavedRegistersBeforeLayout(bool pinvoke)
    {
        WithFrame((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = true;
            compiler.opts.compDbgEnC = !pinvoke;
            compiler.info.compUnmanagedCallCountWithGCTransition = pinvoke ? 1 : 0;

            codeGen.genFinalizeFrame();

            var expected = pinvoke ? SRBM_INT_CALLEE_SAVED & ~SRBM_FPBASE : SRBM_ENC_CALLEE_SAVED;
            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(), Is.EqualTo(new regMaskTP(expected)));
            Assert.That(compiler.compCalleeRegsPushed, Is.EqualTo(System.Numerics.BitOperations.PopCount((ulong)expected)));
            Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.FINAL_FRAME_LAYOUT));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ExhaustedHomingBanksAcquireAnUnreservedCalleeSaveBeforeLayout(bool floating)
    {
        WithFrame((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = false;
            var live = floating ? new regMaskTP(SRBM_FLT_CALLEE_TRASH_INIT) : new regMaskTP(SRBM_INT_CALLEE_TRASH_INIT);
            codeGen.CalleeRegArgMaskLiveIn = live;
            codeGen.RegSet.rsMaskResvd = floating ? RBM_XMM6 : RBM_RBX;

            codeGen.genFinalizeFrame();

            var expected = floating ? RBM_XMM7 : RBM_RBP;
            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(), Is.EqualTo(expected));
            Assert.That(compiler.compCalleeRegsPushed, Is.EqualTo(floating ? 0 : 1));
            Assert.That(compiler.compCalleeFPRegsSavedMask, Is.EqualTo(floating ? SRBM_XMM7 : SRBM_NONE));
        });
    }

    private static void WithFrame(Action<Compiler, CodeGen> action)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.TENTATIVE_FRAME_LAYOUT;
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
            compiler.lvaStubArgumentVar = BAD_VAR_NUM;
            compiler.srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
            compiler.srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
            compiler.srbmMskCalleeTrash = SRBM_MSK_CALLEE_TRASH_INIT;
            AllFloatRegisters(compiler) = SRBM_ALLFLOAT_INIT;
            codeGen.CopyRegisterInfo();
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.RegSet.rsClearRegsModified();
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            Allocator(compiler) = new LinearScan(compiler);
            action(compiler, codeGen);
        });
    }

    internal sealed class EntryAllocator(Action<BasicBlock> record) : IRegAlloc
    {
        public PhaseStatus DoRegisterAllocation() => throw new NotSupportedException();

        public bool IsContainableMemoryOp(GenTree node) => throw new NotSupportedException();

        public bool IsRegCandidate(in LclVarDsc local) => throw new NotSupportedException();

        public bool WillEnregisterLocalVars() => throw new NotSupportedException();

        public void recordVarLocationsAtStartOfBB(BasicBlock block) => record(block);

#if TRACK_LSRA_STATS
        public void dumpLsraStatsCsv(System.IO.StreamWriter writer) => throw new NotSupportedException();

        public void dumpLsraStatsSummary(System.IO.StreamWriter writer) => throw new NotSupportedException();
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask AllFloatRegisters(Compiler compiler);
}
