// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm32LocalHeapAndFrameSetupTests
{
    [Test]
    public static void FramePointerSetupUsesAddAndRecordsUnwindPadding()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            compiler.unwindBegProlog();
            var unwindInfo = compiler.funCurrentFunc().GetUnwindInfo();
            var initialLocation = unwindInfo.GetCurrentEmitterLocation()
                ?? throw new AssertionException("Unwind prolog location was not captured.");

            codeGen.genEstablishFramePointer(16, reportUnwindData: true);

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_add));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_FPBASE));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_SPBASE));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)16));

            var currentLocation = unwindInfo.GetCurrentEmitterLocation()
                ?? throw new AssertionException("Frame-pointer setup did not record unwind padding.");
            Assert.That(initialLocation.IsCurrentLocation(codeGen.Emitter), Is.False);
            Assert.That(currentLocation.IsCurrentLocation(codeGen.Emitter), Is.True);
            compiler.unwindEndProlog();
        });
    }

    [Test]
    public static void SmallLocalFrameUsesImmediateStackAdjustment()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            compiler.unwindBegProlog();
            var zeroed = true;

            codeGen.genAllocLclFrame(16, REG_R0, ref zeroed, RBM_NONE);

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_sub));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_SPBASE));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)16));
            Assert.That(zeroed, Is.True);
            compiler.unwindEndProlog();
        });
    }

    [Test]
    public static void LargeLocalFrameProbesBeforeMovingTheStackPointer()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.info.compMatchedVM = false;
            codeGen.GCInfo.gcVarPtrSetCur = [0];
            codeGen.GCInfo.gcRegGCrefSetCur = default;
            codeGen.GCInfo.gcRegByrefSetCur = default;
            codeGen.Emitter.emitBegProlog();
            compiler.unwindBegProlog();
            var zeroed = true;

            codeGen.genAllocLclFrame(4096, REG_R0, ref zeroed, RBM_NONE);

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_SPBASE));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_STACK_PROBE_HELPER_ARG));
            Assert.That(zeroed, Is.True);
            compiler.unwindEndProlog();
        });
    }

    [TestCase(-16, 36, 4, 1, 0)]
    [TestCase(-16, 40, 1, 0, 1)]
    [TestCase(-16, 44, 1, 1, 1)]
    [TestCase(-8192, 32, 4, 0, 0)]
    public static void BlockInitializationUsesTheArm32StoreWidthAndLoopThreshold(
        int low, int size, int pairStores, int scalarStores, int loopBranches)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            codeGen.Emitter.emitBegProlog();
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;
            SetUseBlockInit(codeGen, true);
            var initRegZeroed = false;

            codeGen.genZeroInitFrameUsingBlockInit(low + size, low, REG_R0, ref initRegZeroed);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Count(static descriptor => descriptor.idIns() == INS_stm),
                Is.EqualTo(pairStores));
            Assert.That(descriptors.Count(static descriptor => descriptor.idIns() == INS_str),
                Is.EqualTo(scalarStores));
            Assert.That(descriptors.Count(static descriptor => descriptor.idIns() == INS_bhi),
                Is.EqualTo(loopBranches));
            Assert.That(initRegZeroed, Is.True);
        }, captureAssertions: true, minOpts: false);
    }

    [Test]
    public static void ConstantLocalHeapAlignmentWrapsAtArm32TargetWidth()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compLocallocUsed = true;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;
            var size = compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)uint.MaxValue));
            size.IsContained = true;
            var tree = new GenTreeUnOp(GT_LCLHEAP, TYP_I_IMPL, size) { RegNum = REG_R0 };

            codeGen.genLclHeap(tree);

            var descriptor = LastInstruction(codeGen.Emitter);
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_SPBASE));
            Assert.That(descriptor.StorageIndex, Is.EqualTo(1));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? Last(Emitter emitter);

    private static Emitter.instrDesc LastInstruction(Emitter emitter)
    {
        return Last(emitter) ?? throw new AssertionException("Expected an emitted instruction.");
    }

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
    {
        return CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_UseBlockInit")]
    private static extern void SetUseBlockInit(CodeGen codeGen, bool value);
}
#endif
