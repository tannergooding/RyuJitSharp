// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.CorInfoOptions;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CompilerFrameHelperCompletionTests
{
#if TARGET_ARM
    [TestCase(REG_R0, 4, SRBM_R0, true, SRBM_R0)]
    [TestCase(REG_F0, 4, SRBM_R0, false, SRBM_F0)]
    [TestCase(REG_F0, 8, SRBM_R0 | SRBM_R1, false, SRBM_F0 | SRBM_F1)]
    [TestCase(REG_F0, 8, SRBM_F0, true, SRBM_F0 | SRBM_F1)]
    [TestCase(REG_F0, 8, SRBM_F1, true, SRBM_F0 | SRBM_F1)]
    public static void ArmPrespillMasksUseFullRegisterPositionsAndRetainBothDoubleHalves(
        regNumber register, int size, regMask prespill, bool expected, regMask expectedFullMask)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, _) =>
        {
            compiler.info.compArgsCount = 1;
            compiler.lvaTable[0].Type = register == REG_R0 ? TYP_INT : (size == 8 ? TYP_DOUBLE : TYP_FLOAT);
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = true;
            var segment = AbiPassingSegment.InRegister(register, 0, size);
            compiler.lvaParameterPassingInfo = [AbiPassingInformation.FromSegment(compiler, false, segment)];

            Assert.That((regMask)((ulong)segment.RegisterMask << segment.RegisterMaskBase),
                Is.EqualTo(expectedFullMask));
            Assert.That(compiler.lvaIsPreSpilled(0, new regMaskTP(prespill)), Is.EqualTo(expected));
        });
    }
#endif

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void TempOrderingKeepsTheArmExceptionAndFramePointerPolicy(bool reorder, bool framePointer)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, codeGen) =>
        {
            compiler.compGSReorderStackLayout = reorder;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = framePointer;

#if TARGET_ARM
            Assert.That(compiler.lvaTempsHaveLargerOffsetThanVars(), Is.False);
#else
            Assert.That(compiler.lvaTempsHaveLargerOffsetThanVars(), Is.EqualTo(!reorder || framePointer));
#endif
        });
    }

    [TestCase(0, false)]
    [TestCase(5, false)]
    [TestCase(15, true)]
    public static void AsyncHeadersAllocateOnlyPresentLocalsInNativeOrder(int presence, bool saveContexts)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, _) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_I_IMPL, lvOnFrame = true, RegNum = REG_STK, StackOffset = -123 },
                new LclVarDsc { Type = TYP_REF, lvOnFrame = true, RegNum = REG_STK, StackOffset = -123 },
                new LclVarDsc { Type = TYP_REF, lvOnFrame = true, RegNum = REG_STK, StackOffset = -123 },
                new LclVarDsc { Type = TYP_REF, lvOnFrame = true, RegNum = REG_STK, StackOffset = -123 },
            ];
            compiler.lvaCount = 4;
            compiler.lvaResumedIndicator = ((presence & 1) != 0) ? 0 : BAD_VAR_NUM;
            compiler.lvaAsyncThreadObjectVar = ((presence & 2) != 0) ? 1 : BAD_VAR_NUM;
            compiler.lvaAsyncExecutionContextVar = ((presence & 4) != 0) ? 2 : BAD_VAR_NUM;
            compiler.lvaAsyncSynchronizationContextVar = ((presence & 8) != 0) ? 3 : BAD_VAR_NUM;
            compiler.info.compMethodInfo->options &= ~CORINFO_ASYNC_SAVE_CONTEXTS;
            if (saveContexts)
            {
                compiler.info.compMethodInfo->options |= CORINFO_ASYNC_SAVE_CONTEXTS;
            }

            var initialOffset = -2 * TARGET_POINTER_SIZE;
            var actualOffset = compiler.lvaAllocAsyncContexts(initialOffset);
            var expectedOffset = initialOffset;
            for (var i = 0; i < 4; i++)
            {
                if ((presence & (1 << i)) != 0)
                {
                    expectedOffset -= TARGET_POINTER_SIZE;
                    Assert.That(compiler.lvaTable[i].StackOffset, Is.EqualTo(expectedOffset));
                }
                else
                {
                    Assert.That(compiler.lvaTable[i].StackOffset, Is.EqualTo(-123));
                }
            }

            Assert.That(actualOffset, Is.EqualTo(expectedOffset));
            Assert.That(compiler.compLclFrameSize, Is.EqualTo(initialOffset - expectedOffset));
        });
    }

#if TARGET_AMD64
    [TestCase(false, 0, 0)]
    [TestCase(false, 24, 24)]
    [TestCase(true, -80, 0)]
    [TestCase(true, -8, 72)]
    public static void StackAndInitialStackGettersConvertTheLocalHome(bool framePointerBased, int offset, int expected)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 64;
            compiler.lvaTable[0].StackOffset = offset;
            compiler.lvaTable[0].lvFramePointerBased = framePointerBased;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;

            Assert.That(codeGen.genSPtoFPdelta, Is.EqualTo(80));
            Assert.That(compiler.lvaGetSPRelativeOffset(0), Is.EqualTo(expected));
            Assert.That(compiler.lvaGetInitialSPRelativeOffset(0), Is.EqualTo(expected));
        });
    }

    [TestCase(false, 0u, 0)]
    [TestCase(false, 2147483648u, int.MinValue)]
    [TestCase(false, uint.MaxValue, -1)]
    [TestCase(true, 4294967216u, 0)]
    [TestCase(true, uint.MaxValue, 79)]
    public static void InitialStackConversionRetainsTheNativeUnsignedOffsetBits(
        bool framePointerBased, uint offset, int expected)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 64;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;

            Assert.That(compiler.lvaToInitialSPRelativeOffset(offset, framePointerBased), Is.EqualTo(expected));
        });
    }
#endif

#if TARGET_AMD64 || TARGET_X86
    [TestCase(false)]
    [TestCase(true)]
    public static void CallerStackConversionDistinguishesRootAndOsrFrames(bool framePointerBased)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 64;
            compiler.lvaTable[0].StackOffset = 24;
            compiler.lvaTable[0].lvFramePointerBased = framePointerBased;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;
#if TARGET_AMD64
            var expected = framePointerBased ? 8 : -72;
            const int rootAdjustment = 104;
#else
            var expected = framePointerBased ? 16 : -56;
            const int rootAdjustment = 96;
#endif

            Assert.That(compiler.lvaToCallerSPRelativeOffset(24, framePointerBased), Is.EqualTo(expected));
            Assert.That(compiler.lvaGetCallerSPRelativeOffset(0), Is.EqualTo(expected));

            PatchpointInfo patchpoint = default;
            patchpoint.Initialize(0, 96);
            compiler.info.compPatchpointInfo = &patchpoint;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);

            Assert.That(compiler.lvaToCallerSPRelativeOffset(24, framePointerBased, forRootFrame: false),
                Is.EqualTo(expected));
            Assert.That(compiler.lvaToCallerSPRelativeOffset(24, framePointerBased),
                Is.EqualTo(expected - rootAdjustment));
            Assert.That(compiler.lvaGetCallerSPRelativeOffset(0), Is.EqualTo(expected - rootAdjustment));
        });
    }
#endif

#if TARGET_AMD64
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void FrameEstimateReservesNativeCalleeSavesBeforeTentativeLayout(
        bool framePointer, bool floatingPointUsed)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, codeGen) =>
        {
            compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
            compiler.compFloatingPointUsed = floatingPointUsed;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = framePointer;

            var size = compiler.lvaFrameSize(Compiler.TENTATIVE_FRAME_LAYOUT);

            Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.TENTATIVE_FRAME_LAYOUT));
            Assert.That(compiler.compCalleeRegsPushed, Is.EqualTo(CNT_CALLEE_SAVED - (framePointer ? 1 : 0)));
            Assert.That(compiler.compCalleeFPRegsSavedMask,
                Is.EqualTo(floatingPointUsed ? SRBM_FLT_CALLEE_SAVED : SRBM_NONE));
            Assert.That(size, Is.EqualTo((uint)compiler.compLclFrameSize + CALLEE_SAVED_REG_MAXSZ));
        });
    }
#endif
}
