// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CompilerFinalFrameLayoutTests
{
    [TestCase(Compiler.TENTATIVE_FRAME_LAYOUT, -20, -35, 15)]
    [TestCase(Compiler.FINAL_FRAME_LAYOUT, -20, -32, 12)]
    [TestCase(Compiler.FINAL_FRAME_LAYOUT, -24, -32, 8)]
    public static void LocalAlignmentKeepsTentativeOffsetsConservative(
        Compiler.FrameLayoutState state, int initialOffset, int expectedOffset, int expectedSize)
    {
        WithFrame((compiler, _) =>
        {
            compiler.lvaDoneFrameLayout = state;
            var offset = compiler.lvaAllocLocalAndSetVirtualOffset(0, 8, initialOffset);
            Assert.That(offset, Is.EqualTo(expectedOffset));
            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(expectedOffset));
            Assert.That(compiler.compLclFrameSize, Is.EqualTo(expectedSize));
        });
    }

    [TestCase(false, Compiler.FINAL_FRAME_LAYOUT, 0, 0)]
    [TestCase(false, Compiler.TENTATIVE_FRAME_LAYOUT, 0, 16)]
    [TestCase(true, Compiler.FINAL_FRAME_LAYOUT, 8, 16)]
    [TestCase(true, Compiler.FINAL_FRAME_LAYOUT, 16, 16)]
    public static void FrameAlignmentIncludesRegisterParityAndTentativePadding(
        bool framePointer, Compiler.FrameLayoutState state, int initialSize, int expectedSize)
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = state;
            compiler.compCalleeRegsPushed = 0;
            compiler.compLclFrameSize = initialSize;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = framePointer;

            compiler.lvaAlignFrame();

            Assert.That(compiler.compLclFrameSize, Is.EqualTo(expectedSize));
        });
    }

    [Test]
    public static void VirtualLocalsIncludeSavedRegistersAndSkipOutgoingArgumentHome()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.compCalleeRegsPushed = 0;
            compiler.lvaTable[0].Type = TYP_LONG;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;

            compiler.lvaAssignVirtualFrameOffsetsToLocals();

            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(-24));
            Assert.That(compiler.compLclFrameSize, Is.EqualTo(8));
        });
    }

    [Test]
    public static void FinalLayoutFixesLocalOffsetsAndPinsOutgoingAreaToStackPointer()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.TENTATIVE_FRAME_LAYOUT;
            compiler.compCalleeRegsPushed = 0;
            compiler.lvaTable[0].Type = TYP_LONG;
            compiler.lvaTable[1] = new LclVarDsc
            {
                Type = TYP_STRUCT,
                lvOnFrame = true,
                RegNum = REG_STK,
                Layout = new ClassLayout(32),
            };
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = 32;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;

            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);

            Assert.That(compiler.compLclFrameSize, Is.EqualTo(48));
            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(-8));
            Assert.That(compiler.lvaTable[1].StackOffset, Is.Zero);
            Assert.That(compiler.lvaTable[1].lvFramePointerBased, Is.False);
        });
    }

    [Test]
    public static void TentativeTempsReserveTheNativeUpperBound()
    {
        WithFrame((compiler, _) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.TENTATIVE_FRAME_LAYOUT;
            var size = compiler.lvaMaxSpillTempSize;

            var offset = compiler.lvaAllocateTemps(-16, mustDoubleAlign: false);

            Assert.That(offset, Is.EqualTo(-16 - size));
            Assert.That(compiler.compLclFrameSize, Is.EqualTo(size));
        });
    }

    [Test]
    public static void AsyncFrameHeaderPrecedesOrdinaryLocals()
    {
        WithFrame((compiler, _) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.lvaResumedIndicator = 0;
            compiler.lvaAsyncThreadObjectVar = 1;
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            compiler.lvaTable[1].Type = TYP_REF;

            var offset = compiler.lvaAllocAsyncContexts(-16);

            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(-24));
            Assert.That(compiler.lvaTable[1].StackOffset, Is.EqualTo(-32));
            Assert.That(offset, Is.EqualTo(-32));
            Assert.That(compiler.compLclFrameSize, Is.EqualTo(16));
        });
    }

    [Test]
    public static void OSRLocalOffsetsUseTheRootFrameSlot()
    {
        WithFrame((compiler, _) =>
        {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.lvaTable[0].lvIsOSRLocal = true;
            var patchpointBytes = stackalloc byte[PatchpointInfo.ComputeSize(2)];
            var patchpoint = (PatchpointInfo*)patchpointBytes;
            patchpoint->Initialize(2, 64);
            patchpoint->SetOffsetAndExposure(0, -40, true);
            patchpoint->MonitorAcquiredOffset = -24;
            patchpoint->AsyncThreadOffset = -32;
            compiler.info.compPatchpointInfo = patchpoint;

            Assert.That(compiler.lvaOSRLocalTier0FrameOffset(0), Is.EqualTo(-40));
            compiler.lvaMonAcquired = 0;
            Assert.That(compiler.lvaOSRLocalTier0FrameOffset(0), Is.EqualTo(-24));
            compiler.lvaMonAcquired = BAD_VAR_NUM;
            compiler.lvaAsyncThreadObjectVar = 0;
            Assert.That(compiler.lvaOSRLocalTier0FrameOffset(0), Is.EqualTo(-32));
        });
    }

    [Test]
    public static void OSRLayoutReusesTheOriginalFrameWithoutAllocatingAnotherLocal()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.lvaDoneFrameLayout = Compiler.TENTATIVE_FRAME_LAYOUT;
            compiler.compCalleeRegsPushed = 0;
            compiler.lvaTable[0].Type = TYP_LONG;
            compiler.lvaTable[0].lvIsOSRLocal = true;
            var patchpointBytes = stackalloc byte[PatchpointInfo.ComputeSize(2)];
            var patchpoint = (PatchpointInfo*)patchpointBytes;
            patchpoint->Initialize(2, 64);
            patchpoint->SetOffsetAndExposure(0, -40, true);
            compiler.info.compPatchpointInfo = patchpoint;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;

            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);

            Assert.That(compiler.compLclFrameSize, Is.Zero);
            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(32));
        });
    }

    internal static void WithFrame(Action<Compiler, CodeGen> action)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, lvOnFrame = true, RegNum = REG_STK },
                                 new LclVarDsc { Type = TYP_STRUCT, lvOnFrame = true, RegNum = REG_STK,
                                                 Layout = new ClassLayout(0) }];
            compiler.lvaCount = 2;
            compiler.lvaOutgoingArgSpaceVar = 1;
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = 0;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            compiler.lvaMonAcquired = BAD_VAR_NUM;
            compiler.lvaResumedIndicator = BAD_VAR_NUM;
            compiler.lvaAsyncThreadObjectVar = BAD_VAR_NUM;
            compiler.lvaAsyncExecutionContextVar = BAD_VAR_NUM;
            compiler.lvaAsyncSynchronizationContextVar = BAD_VAR_NUM;
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.compCalleeFPRegsSavedMask = 0;
            compiler.compLclFrameSize = 0;
            action(compiler, codeGen);
        });
    }
}
