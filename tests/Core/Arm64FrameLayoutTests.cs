// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64FrameLayoutTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void FrameSelectionLaysOutLocalsBeforeReservingRegisters(bool minOpts, bool scalable)
    {
        WithFrame((compiler, codeGen) => {
            compiler.compFloatingPointUsed = scalable;
            if (scalable)
            {
                compiler.lvaTable[0].Type = TYP_SIMD;
            }

            var allocator = new LinearScan(compiler);
            codeGen.resetFramePointerUsedWritePhase();
            SetFrameType(allocator);
            Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.REGALLOC_FRAME_LAYOUT));
            Assert.That(codeGen.IsFramePointerUsed, Is.True);
            Assert.That(compiler.compCalleeRegsPushed,
                Is.EqualTo(CNT_CALLEE_SAVED + 1 + (scalable ? CNT_CALLEE_SAVED_FLOAT : 0)));
            Assert.That(compiler.compUsesUnknownSizeFrame, Is.EqualTo(scalable));
            Assert.That(compiler.compLocallocUsed, Is.EqualTo(scalable));

            var reserved = SRBM_FPBASE | SRBM_OPT_RSVD | (scalable ? SRBM_UNKBASE : SRBM_NONE);
            Assert.That(NewInterval(allocator, TYP_INT).registerPreferences & reserved, Is.EqualTo(SRBM_NONE));
            Assert.That(codeGen.RegSet.rsMaskResvd.IntRegSet & SRBM_OPT_RSVD, Is.EqualTo(SRBM_OPT_RSVD));
            Assert.That(codeGen.RegSet.rsMaskResvd.IntRegSet & SRBM_UNKBASE,
                Is.EqualTo(scalable ? SRBM_UNKBASE : SRBM_NONE));

            var offset = scalable ? compiler.lvaTable[0].UnknownSizeFrameIndex : compiler.lvaTable[0].StackOffset;
            codeGen.resetFramePointerUsedWritePhase();
            SetFrameType(allocator);
            Assert.That(scalable ? compiler.lvaTable[0].UnknownSizeFrameIndex : compiler.lvaTable[0].StackOffset,
                Is.EqualTo(offset));
            Assert.That(compiler.unkSizeFrame.nVector, Is.EqualTo(scalable ? 1u : 0u));
        }, minOpts);
    }

    [TestCase(232, true, true)]
    [TestCase(240, true, false)]
    [TestCase(240, false, true)]
    public static void AppleNativeAotUsesCanonicalFramesAtTheNativeSizeBoundary(
        int localSize, bool framePointerRequired, bool canonicalEligible)
    {
        WithFrame((compiler, codeGen) => {
            compiler.opts.compJitSaveFpLrWithCalleeSavedRegisters = 0;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            compiler.lvaTable[0] = NewLocal(TYP_STRUCT);
            compiler.lvaTable[0].Layout = new ClassLayout(localSize);
            codeGen.IsFramePointerRequired = framePointerRequired;

            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);

            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters,
                Is.EqualTo(TargetOS.IsApplePlatform && canonicalEligible));
        });
    }

    [TestCase(1, 24, -8)]
    [TestCase(2, -8, -24)]
    [TestCase(3, -8, -24)]
    public static void FinalLayoutRelocatesFpLrWithoutOverlappingArguments(
        int placementOption, int localOffset, int callerSpOffset)
    {
        WithFrame((compiler, codeGen) => {
            compiler.opts.compJitSaveFpLrWithCalleeSavedRegisters = placementOption;
            compiler.lvaTable[1] = NewLocal(TYP_STRUCT);
            compiler.lvaTable[1].Layout = new ClassLayout(32);
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = 32;

            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);

            Assert.That(compiler.compLclFrameSize, Is.EqualTo(48));
            Assert.That(codeGen.genTotalFrameSize, Is.EqualTo(64));
            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(localOffset));
            Assert.That(compiler.lvaGetCallerSPRelativeOffset(0), Is.EqualTo(callerSpOffset));
            Assert.That(compiler.lvaTable[1].StackOffset, Is.Zero);
            Assert.That(compiler.lvaTable[1].lvFramePointerBased, Is.False);
        });
    }

    [TestCase(REG_R0, -64)]
    [TestCase(REG_R7, -8)]
    public static void VarargsRegistersUseTheIncomingHomeArea(regNumber register, int callerSpOffset)
    {
        WithFrame((compiler, codeGen) => {
            compiler.info.compIsVarArgs = true;
            compiler.info.compArgsCount = 1;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = true;
            compiler.lvaParameterPassingInfo =
                [AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.InRegister(register, 0, 8))];

            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);

            Assert.That(compiler.compLclFrameSize, Is.Zero);
            Assert.That(codeGen.genTotalFrameSize, Is.EqualTo(80));
            Assert.That(compiler.lvaGetCallerSPRelativeOffset(0), Is.EqualTo(callerSpOffset));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PrologStackVariableOffsetUsesFramePointerOrTotalFrameSize(bool framePointer)
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.compLclFrameSize = 40;
            codeGen.IsFramePointerUsed = framePointer;

            var local = new LclVarDsc { Type = TYP_LONG, StackOffset = 128 };
            var getStackOffset = typeof(CodeGen).GetMethod("psiGetVarStackOffset", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertionException("Missing prolog stack-offset helper.");
            var actual = (int)(getStackOffset.Invoke(codeGen, [local]) ??
                throw new AssertionException("Missing prolog stack offset result."));
            var expected = framePointer
                ? local.StackOffset - REGSIZE_BYTES
                : local.StackOffset - ((2 * REGSIZE_BYTES) + 40);

            Assert.That(actual, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void SplitVarargsStructReusesTheRegisterAndStackBoundary()
    {
        WithFrame((compiler, _) => {
            compiler.info.compIsVarArgs = true;
            compiler.info.compArgsCount = 1;
            compiler.lvaParameterStackSize = 8;
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(16);
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegments(compiler,
                    AbiPassingSegment.InRegister(REG_R7, 0, 8), AbiPassingSegment.OnStack(0, 8, 8)),
            ];

            Assert.That(compiler.lvaParamHasLocalStackSpace(0), Is.False);
            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);
            Assert.That(compiler.compLclFrameSize, Is.Zero);
            Assert.That(compiler.lvaGetCallerSPRelativeOffset(0), Is.EqualTo(-8));
        });
    }

    [TestCase(1, -72)]
    [TestCase(2, -88)]
    public static void OsrRelocationLeavesOriginalSlotsAboveTheFrameBoundary(int placementOption, int newSlotOffset)
    {
        WithFrame((compiler, _) => {
            compiler.opts.compJitSaveFpLrWithCalleeSavedRegisters = placementOption;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.lvaTable =
                [compiler.lvaTable[0], NewLocal(TYP_LONG), compiler.lvaTable[1]];
            compiler.lvaCount = 3;
            compiler.info.compLocalsCount = 1;
            compiler.lvaOutgoingArgSpaceVar = 2;
            compiler.lvaTable[0].lvIsOSRLocal = true;
            var bytes = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)bytes;
            patchpoint->Initialize(1, 64);
            patchpoint->SetOffsetAndExposure(0, -40, true);
            compiler.info.compPatchpointInfo = patchpoint;

            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);

            Assert.That(compiler.lvaGetCallerSPRelativeOffset(0), Is.EqualTo(-40));
            Assert.That(compiler.lvaGetCallerSPRelativeOffset(1), Is.EqualTo(newSlotOffset));
        });
    }

    [TestCase(0, 16)]
    [TestCase(7, 64)]
    [TestCase(8, 80)]
    public static void ScalableVectorsAndFixedMasksKeepSeparateFrameStorage(int maskLocals, int fixedFrameSize)
    {
        WithFrame((compiler, codeGen) => {
            var outgoing = compiler.lvaTable[1];
            compiler.lvaTable = new LclVarDsc[maskLocals + 2];
            compiler.lvaTable[0] = NewLocal(TYP_SIMD);
            for (var index = 0; index < maskLocals; index++)
            {
                compiler.lvaTable[index + 1] = NewLocal(TYP_MASK);
            }

            compiler.lvaTable[^1] = outgoing;
            compiler.lvaCount = compiler.lvaTable.Length;
            compiler.lvaOutgoingArgSpaceVar = compiler.lvaCount - 1;
            codeGen.RegSet.tmpBeginPreAllocateTemps();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_SIMD, 1);
            codeGen.RegSet.tmpPreAllocateTemps(TYP_MASK, 1);

            compiler.lvaAssignFrameOffsets(Compiler.FINAL_FRAME_LAYOUT);

            Assert.That(compiler.compUsesUnknownSizeFrame, Is.True);
            Assert.That(compiler.compLocallocUsed, Is.True);
            Assert.That(compiler.unkSizeFrame.nVector, Is.EqualTo(2u));
            Assert.That(compiler.unkSizeFrame.nMask, Is.Zero);
            Assert.That(compiler.unkSizeFrame.FrameSizeInVectors(), Is.EqualTo(2u));
            Assert.That(compiler.compLclFrameSize, Is.EqualTo(fixedFrameSize));
            Assert.That(compiler.unkSizeFrame.GetAddressingOffset(in compiler.lvaTable[0]),
                Is.EqualTo(-1));

            var temps = 0;
            for (var temp = codeGen.RegSet.tmpListBeg(); temp is not null; temp = codeGen.RegSet.tmpListNxt(temp))
            {
                if (temp.tdTempType is TYP_MASK)
                {
                    Assert.That(temp.tdTempOffs, Is.GreaterThanOrEqualTo(16));
                }
                else
                {
                    Assert.That(compiler.unkSizeFrame.GetAddressingOffset(temp), Is.EqualTo(-2));
                }
                temps++;
            }

            Assert.That(temps, Is.EqualTo(2));
        });
    }

    [Test]
    public static void MasksKeepThePinnedNativeExactValueSize()
    {
        var size = ValueSize.FromJitType(TYP_MASK);
        Assert.That(size.IsExact, Is.True);
        Assert.That(size.ExactSize, Is.EqualTo(8));
        Assert.That(varTypeHasUnknownSize(TYP_MASK), Is.False);
        Assert.That(ValueSize.FromJitType(TYP_SIMD).IsExact, Is.False);
    }

    [TestCase(1, 1)]
    [TestCase(8, 1)]
    [TestCase(9, 2)]
    public static void UnknownFrameBookkeepingPadsTheMaskBlock(int maskCount, int maskVectors)
    {
        Compiler.UnknownSizeFrame frame = default;
        for (var index = 0; index < maskCount; index++)
        {
            Assert.That(frame.AllocMask(), Is.EqualTo((uint)index));
        }

        var vectorIndex = frame.AllocVector();
        frame.FinalizeLayout();
        Assert.That(frame.MaskBlockSizeInVectors(), Is.EqualTo((uint)maskVectors));
        Assert.That(frame.FrameSizeInVectors(), Is.EqualTo((uint)maskVectors + 1));
        Assert.That(frame.GetOffset((uint)maskCount - 1, isMask: true), Is.EqualTo(-maskCount));
        Assert.That(frame.GetOffset(vectorIndex), Is.EqualTo(-maskVectors - 1));
    }

    [Test]
    public static void PrologParameterScopeUsesArm64StackLocation()
    {
        WithFrame((compiler, codeGen) =>
        {
            compiler.opts.compDbgInfo = true;
            compiler.info.compArgsCount = 1;
            compiler.info.compLocalsCount = 1;
            compiler.lvaCount = 1;
            compiler.lvaTable = [new() {
                Type = TYP_LONG,
                lvIsParam = true,
                lvOnFrame = true,
                lvFramePointerBased = true,
                StackOffset = 16,
                RegNum = REG_STK,
            }];
            compiler.lvaParameterPassingInfo = [AbiPassingInformation.FromSegment(compiler, false,
                AbiPassingSegment.OnStack(0, 0, 8))];
            compiler.info.compVarScopes = [
                new() { vsdVarNum = 0, vsdLVnum = 0, vsdLifeBeg = 0, vsdLifeEnd = 10 },
            ];
            compiler.info.compVarScopesCount = 1;
            compiler.compInitScopeLists();
            codeGen.initializeVariableLiveKeeper();
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaTrackedToVarNum = [0];
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            codeGen.Emitter.emitBegProlog();

            codeGen.psiBegProlog();

            var range = codeGen.getVariableLiveKeeper().getLiveRangesForVarForProlog(0)[0];
            Assert.That(range.m_VarLocation.vlIsOnStack(REG_SPBASE, 8), Is.True);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setFrameType")]
    private static extern void SetFrameType(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types type);

    private static LclVarDsc NewLocal(var_types type)
    {
        return new LclVarDsc { Type = type, lvOnFrame = true, lvFramePointerBased = true, RegNum = REG_STK };
    }

    private static void WithFrame(Action<Compiler, CodeGen> action, bool minOpts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        CORINFO_METHOD_INFO methodInfo = default;
        JitFlags flags = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.opts.compJitSaveFpLrWithCalleeSavedRegisters = 1;
#if DEBUG
        compiler.info.compFullName = nameof(Arm64FrameLayoutTests);
#endif
        compiler.lvaTable = [NewLocal(TYP_LONG), NewLocal(TYP_STRUCT)];
        compiler.lvaTable[1].Layout = new ClassLayout(0);
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
        compiler.lvaSecretStubArg = BAD_VAR_NUM;

        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            compiler.compCalleeRegsPushed = 2;
            codeGen.IsFramePointerRequired = false;
            codeGen.IsFrameRequired = false;
            codeGen.IsFramePointerUsed = true;
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
