// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SysVX64FinalFrameTests
{
    [Test]
    public static void OnlyStackParametersHaveCallerAllocatedHomes()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = true;
            compiler.lvaTable[1].lvIsParam = true;
            compiler.info.compArgsCount = 2;
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.InRegister(REG_RDI, 0, 8)),
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.OnStack(24, 0, 8)),
            ];

            Assert.That(compiler.lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(0, out _), Is.False);
            Assert.That(compiler.lvaParamHasLocalStackSpace(0), Is.True);
            Assert.That(compiler.lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(1, out var offset), Is.True);
            Assert.That(offset, Is.EqualTo(24));
            Assert.That(compiler.lvaParamHasLocalStackSpace(1), Is.False);
        });
    }

    [TestCase(false, 0)]
    [TestCase(true, 8)]
    public static void EmptyCallFrameStillAlignsWhenRequired(bool hasCalls, int expectedSize)
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.opts.compNeedToAlignFrame = hasCalls;
            compiler.compCalleeRegsPushed = 0;
            compiler.compLclFrameSize = 0;
            codeGen.IsFramePointerUsed = false;

            compiler.lvaAlignFrame();

            Assert.That(compiler.compLclFrameSize, Is.EqualTo(expectedSize));
        });
    }

    [Test]
    public static void FrameEstimateAccountsForSysVCalleeSaves()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.PRE_REGALLOC_FRAME_LAYOUT;
            codeGen.ResetWritePhaseForFramePointerRequired();
            codeGen.IsFramePointerRequired = false;
            codeGen.IsFramePointerUsed = false;

            var estimate = compiler.lvaFrameSize(Compiler.TENTATIVE_FRAME_LAYOUT);

            Assert.That(compiler.compCalleeRegsPushed, Is.EqualTo(CNT_CALLEE_SAVED));
            Assert.That(estimate, Is.EqualTo((uint)compiler.compLclFrameSize + CALLEE_SAVED_REG_MAXSZ));
        });
    }

    [Test]
    public static void FinalLayoutHomesRegisterArgumentLocallyAndKeepsOutgoingAreaAtZero()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_LONG, lvIsParam = true, lvIsRegArg = true,
                    lvOnFrame = true, RegNum = REG_STK },
                new LclVarDsc { Type = TYP_LONG, lvIsParam = true,
                    lvOnFrame = true, RegNum = REG_STK },
                new LclVarDsc { Type = TYP_LONG, lvOnFrame = true, RegNum = REG_STK },
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(8),
                    lvOnFrame = true, RegNum = REG_STK },
            ];
            compiler.lvaCount = 4;
            compiler.lvaOutgoingArgSpaceVar = 3;
            compiler.lvaOutgoingArgSpaceSize.Value = 8;
            compiler.info.compArgsCount = 2;
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.InRegister(REG_RDI, 0, 8)),
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.OnStack(8, 0, 8)),
            ];
            compiler.compCalleeRegsPushed = 0;
            codeGen.IsFramePointerUsed = false;

            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);

            Assert.That(compiler.compLclFrameSize, Is.EqualTo(24));
            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(16));
            Assert.That(compiler.lvaTable[1].StackOffset, Is.EqualTo(40));
            Assert.That(compiler.lvaTable[2].StackOffset, Is.EqualTo(8));
            Assert.That(compiler.lvaTable[3].StackOffset, Is.Zero);
            Assert.That(compiler.lvaTable[3].lvFramePointerBased, Is.False);
        });
    }

    [Test]
    public static void FramePointerLayoutKeepsCallerStackArgumentsAboveLocalHomes()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = true;
            compiler.lvaTable[1].lvIsParam = true;
            compiler.lvaTable =
            [
                compiler.lvaTable[0],
                compiler.lvaTable[1],
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(0),
                    lvOnFrame = true, RegNum = REG_STK },
            ];
            compiler.lvaCount = 3;
            compiler.lvaOutgoingArgSpaceVar = 2;
            compiler.info.compArgsCount = 2;
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.InRegister(REG_RDI, 0, 8)),
                AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.OnStack(8, 0, 8)),
            ];
            compiler.compCalleeRegsPushed = 1;
            codeGen.IsFramePointerUsed = true;

            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);

            Assert.That(compiler.lvaTable[0].StackOffset, Is.LessThan(0));
            Assert.That(compiler.lvaTable[1].StackOffset, Is.GreaterThan(0));
            Assert.That(compiler.lvaTable[1].StackOffset - compiler.lvaTable[0].StackOffset,
                Is.GreaterThanOrEqualTo(16));
        });
    }

    [Test]
    public static void OSRLayoutReusesTierZeroFrameSlot()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.lvaTable[0].lvIsOSRLocal = true;
            var patchpointBytes = stackalloc byte[PatchpointInfo.ComputeSize(2)];
            var patchpoint = (PatchpointInfo*)patchpointBytes;
            patchpoint->Initialize(2, 64);
            patchpoint->SetOffsetAndExposure(0, -40, true);
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.info.compLocalsCount = 1;
            compiler.compCalleeRegsPushed = 0;
            codeGen.IsFramePointerUsed = true;

            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);

            Assert.That(compiler.compLclFrameSize, Is.Zero);
            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(32));
        });
    }

    [Test]
    public static void DependentPromotedParameterFieldUsesParentStackHome()
    {
        WithFrame((compiler, _) =>
        {
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(16),
                    lvIsParam = true, lvPromoted = true, lvDoNotEnregister = true, lvFieldCnt = 1,
                    lvFieldLclStart = 1, lvOnFrame = true, StackOffset = 24 },
                new LclVarDsc { Type = TYP_LONG, lvIsStructField = true, lvIsParam = true,
                    lvParentLcl = 0, lvFldOffset = 8, lvOnFrame = true },
            ];
            compiler.lvaCount = 2;

            compiler.lvaAssignFrameOffsetsToPromotedStructs();

            Assert.That(compiler.lvaTable[1].StackOffset, Is.EqualTo(32));
        });
    }

    [Test]
    public static void FinalizationCountsOnlySysVNonvolatileRegisters()
    {
        WithFrame((compiler, codeGen) =>
        {
            codeGen.IsFramePointerUsed = true;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_R12 | RBM_RDI);
            Allocator(compiler) = new LinearScan(compiler);

            codeGen.genFinalizeFrame();

            Assert.That(compiler.compCalleeRegsPushed, Is.EqualTo(2));
            Assert.That(compiler.compCalleeFPRegsSavedMask, Is.EqualTo((regMask)0));
            Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.FINAL_FRAME_LAYOUT));
        });
    }

    [Test]
    public static void ProfilerHookReservesR14AndR15BeforeFinalLayout()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.opts.compJitELTHookEnabled = true;
            codeGen.IsFramePointerUsed = true;
            Allocator(compiler) = new LinearScan(compiler);

            codeGen.genFinalizeFrame();

            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask() & (RBM_R14 | RBM_R15),
                Is.EqualTo(new regMaskTP(SRBM_R14 | SRBM_R15)));
            Assert.That(compiler.compCalleeRegsPushed, Is.EqualTo(2));
        });
    }

    [Test]
    public static void OSRPrologReconstructsTierZeroUnwindAtEntry()
    {
        WithOSRProlog(SRBM_RBP | SRBM_R12 | SRBM_RBX, (compiler, codeGen) =>
        {
            codeGen.genOSRHandleTier0CalleeSavedRegistersAndFrame();

            Assert.That(CurrentDescriptors(codeGen.Emitter), Is.Empty);
            var func = compiler.funCurrentFunc();
            var codes = func.unwindCodes ?? throw new AssertionException("Missing Tier0 unwind codes.");
            Assert.That(codes.AsSpan((int)func.unwindCodeSlot).ToArray(),
                Is.EqualTo((byte[])[0, 0x82, 0, 0x30, 0, 0xC0, 0, 0x50]));
        });
    }

    [Test]
    public static void OSRSavesNewCalleeSavedRegistersInTierZeroReservedSlots()
    {
        WithOSRProlog(SRBM_RBP | SRBM_RBX, (compiler, codeGen) =>
        {
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = false;
            compiler.compLclFrameSize = 32;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_R12 | RBM_R13 | RBM_RDI);

            codeGen.genOSRSaveRemainingCalleeSavedRegisters();

            var ids = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing OSR save descriptors.");
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_mov, INS_mov]));
            Assert.That(ids.Select(id => id.idReg1()), Is.EqualTo((regNumber[])[REG_R13, REG_R12]));
            Assert.That(ids.All(id => id.idAddr().iiaAddrMode.amBaseReg == REG_RSP), Is.True);
            Assert.That(ids.Select(id => Displacement(codeGen.Emitter, id)), Is.EqualTo((nint[])[112, 104]));
        });
    }

    [TestCase(0x12345678L, false)]
    [TestCase(0x100000000L, true)]
    public static void PrologStoresSysVSecurityCookieWithoutClobberingUnusedScratch(long cookie, bool usesScratch)
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            compiler.compNeedsGSSecurityCookie = true;
            compiler.lvaGSSecurityCookie = 0;
            compiler.lvaTable[0].Type = TYP_LONG;
            compiler.gsGlobalSecurityCookieVal = (nint)cookie;
            var zeroed = true;

            codeGen.genSetGSSecurityCookie(REG_R10, ref zeroed);

            var ids = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing cookie initialization descriptors.");
            Assert.That(ids, Has.Count.EqualTo(usesScratch ? 2 : 1));
            Assert.That(ids[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(zeroed, Is.EqualTo(!usesScratch));
        });
    }

    [Test]
    public static void OSRPrologKeepsTierZeroSecurityCookie()
    {
        WithOSRProlog(SRBM_RBP, (compiler, codeGen) =>
        {
            compiler.compNeedsGSSecurityCookie = true;
            compiler.info.compPatchpointInfo->SecurityCookieOffset = -24;
            var zeroed = true;

            codeGen.genSetGSSecurityCookie(REG_R10, ref zeroed);

            Assert.That(CurrentDescriptors(codeGen.Emitter), Is.Empty);
            Assert.That(zeroed, Is.True);
        });
    }

    private static void WithOSRProlog(regMask tier0Saves, Action<Compiler, CodeGen> action)
    {
        SysVX64FrameCodeGenTests.WithProlog((compiler, codeGen) =>
        {
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 96);
            patchpoint->CalleeSaveRegisters = (long)tier0Saves;
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.info.compLocalsCount = 1;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.compCalleeRegsPushed = 0;
            codeGen.IsFramePointerUsed = false;
            compiler.unwindBegProlog();
            action(compiler, codeGen);
        });
    }

    private static void WithFrame(Action<Compiler, CodeGen> action)
    {
        SysVX64EmitterCallTests.WithEmitter((compiler, _) =>
        {
            CORINFO_METHOD_INFO methodInfo = default;
            compiler.info.compMethodInfo = &methodInfo;
            compiler.info.compArgsCount = 0;
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_LONG, lvOnFrame = true, RegNum = REG_STK },
                new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(0),
                    lvOnFrame = true, RegNum = REG_STK },
            ];
            compiler.lvaCount = 2;
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
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
            compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
            compiler.lvaSecretStubArg = BAD_VAR_NUM;
            compiler.compCalleeFPRegsSavedMask = 0;
            compiler.compCalleeRegsPushed = 0;
            compiler.srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
            compiler.srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
            compiler.srbmMskCalleeTrash = SRBM_MSK_CALLEE_TRASH_INIT;
            compiler.lvaDoneFrameLayout = Compiler.TENTATIVE_FRAME_LAYOUT;
            compiler.fgFirstBB = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            var codeGen = compiler.codeGen as CodeGen ?? throw new AssertionException("Missing code generator.");
            LastIntRegister(compiler) = REG_R15;
            AllFloatRegisters(compiler) = SRBM_ALLFLOAT_INIT;
            codeGen.CopyRegisterInfo();
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.RegSet.rsClearRegsModified();
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "regIntLast")]
    private static extern ref regNumber LastIntRegister(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask AllFloatRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);
}
#endif
