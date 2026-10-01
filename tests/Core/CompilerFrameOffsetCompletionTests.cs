// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG && TARGET_AMD64
using System;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
#if TARGET_ARM
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerFrameOffsetCompletionTests
{
    [TestCase(Target.ArgOrder.ARG_ORDER_R2L, 8, 0, 8)]
    [TestCase(Target.ArgOrder.ARG_ORDER_R2L, 8, 4, 4)]
    [TestCase(Target.ArgOrder.ARG_ORDER_R2L, 0, 8, -8)]
    [TestCase(Target.ArgOrder.ARG_ORDER_L2R, 8, 0, 24)]
    public static void CallerStackSegmentsKeepNativeRelativeOffsetsAndPromotedFieldHomes(
        Target.ArgOrder order, int stackOffset, int segmentOffset, int expectedOffset)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, _) =>
        {
            compiler.info.compArgsCount = 1;
            compiler.info.compArgOrder = order;
            compiler.lvaParameterStackSize = 32;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT,
                Layout = new ClassLayout(16),
                lvIsParam = true,
                lvPromoted = true,
                lvFieldCnt = 1,
                lvFieldLclStart = 1,
            };
            compiler.lvaTable[1] = new LclVarDsc
            {
                Type = TYP_INT,
                lvIsParam = true,
                lvIsStructField = true,
                lvParentLcl = 0,
                lvFldOffset = 4,
            };
            compiler.lvaParameterPassingInfo =
            [
                AbiPassingInformation.FromSegment(compiler, false,
                    AbiPassingSegment.OnStack(stackOffset, segmentOffset, 4)),
            ];

            Assert.That(compiler.lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(0, out var offset),
                Is.True);
            Assert.That(offset, Is.EqualTo(expectedOffset));
            compiler.lvaAssignVirtualFrameOffsetsToArgs();
            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(expectedOffset));
            Assert.That(compiler.lvaTable[1].StackOffset, Is.EqualTo(expectedOffset + 4));
        });
    }

#if DEBUG
    [Test]
    public static void InvalidVerboseLayoutStateTerminatesAfterUnknownWithoutANewline()
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, _) =>
        {
            var previousConfig = JitConfig;
            JitConfig = default;
            try
            {
                compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
                compiler.compLclFrameSize = 23;
                compiler.lvaTable[0].StackOffset = -123;
                compiler.verbose = true;
                var invalidState = (Compiler.FrameLayoutState)((int)Compiler.FINAL_FRAME_LAYOUT + 1);

                var text = CodeGenLifeTransitionTests.Capture(() =>
                {
                    var error = Assert.Throws<FatalJitException>(() => compiler.lvaAssignFrameOffsets(invalidState));
                    Assert.That(error, Has.Property(nameof(FatalJitException.Result))
                        .EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
                });

                Assert.That(text, Is.EqualTo("*************** In lvaAssignFrameOffsets(UNKNOWN)"));
                Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(invalidState));
                Assert.That(compiler.compLclFrameSize, Is.EqualTo(23));
                Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(-123));
            }
            finally
            {
                JitConfig = previousConfig;
            }
        });
    }
#endif

#if DEBUG && TARGET_AMD64
    [TestCase(0)]
    [TestCase(8)]
    public static void OsrPromotedFieldDiagnosticsRetainParentAndFieldOffsetIdentity(byte fieldOffset)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, codeGen) =>
        {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.compCalleeRegsPushed = 0;
            compiler.info.compLocalsCount = 1;
            compiler.lvaTable =
            [
                new LclVarDsc
                {
                    Type = TYP_STRUCT,
                    Layout = new ClassLayout(16),
                    lvIsOSRLocal = true,
                    lvPromoted = true,
                    lvFieldCnt = 1,
                    lvFieldLclStart = 1,
                },
                new LclVarDsc
                {
                    Type = TYP_LONG,
                    lvIsOSRLocal = true,
                    lvIsStructField = true,
                    lvParentLcl = 0,
                    lvFldOffset = fieldOffset,
                    lvOnFrame = true,
                },
                new LclVarDsc
                {
                    Type = TYP_STRUCT,
                    Layout = new ClassLayout(0),
                },
            ];
            compiler.lvaCount = 3;
            compiler.lvaOutgoingArgSpaceVar = 2;
            var bytes = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)bytes;
            patchpoint->Initialize(1, 64);
            patchpoint->SetOffsetAndExposure(0, -40, false);
            compiler.info.compPatchpointInfo = patchpoint;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;
            compiler.verbose = true;

            var text = CodeGenLifeTransitionTests.Capture(compiler.lvaAssignVirtualFrameOffsetsToLocals);
            var offset = -48 + fieldOffset;

            Assert.That(text, Does.Contain(
                "---OSR--- V01 (promoted field of V00; on tier0 frame) tier0 FP-rel offset -40 " +
                $"frame offset -8 field offset {fieldOffset} new virt offset {offset}{Environment.NewLine}"));
            Assert.That(compiler.lvaTable[1].StackOffset, Is.EqualTo(offset));
            Assert.That(compiler.compLclFrameSize, Is.Zero);
        });
    }
#endif

#if TARGET_X86 || TARGET_ARM
    [TestCase(Compiler.TENTATIVE_FRAME_LAYOUT)]
    [TestCase(Compiler.FINAL_FRAME_LAYOUT)]
    public static void RawThirtyTwoBitSlotAssignmentDoesNotAddEightBytePadding(Compiler.FrameLayoutState state)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, _) =>
        {
            compiler.lvaDoneFrameLayout = state;
            compiler.lvaTable[0].Type = TYP_LONG;

            var offset = compiler.lvaAllocLocalAndSetVirtualOffset(0, 8, -20);

            Assert.That(offset, Is.EqualTo(-28));
            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(-28));
            Assert.That(compiler.compLclFrameSize, Is.EqualTo(8));
        });
    }
#endif

#if TARGET_ARM
    [TestCase(REG_R0, -12, 0, false)]
    [TestCase(REG_R2, -8, 4, false)]
    [TestCase(REG_R3, -4, 8, true)]
    public static void ArmPrespillOffsetsIncludeAlignmentButStackHomeSelectionDoesNot(
        regNumber register, int callerOffset, int virtualOffset, bool requiresLocalHome)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, codeGen) =>
        {
            compiler.info.compArgsCount = 1;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = true;
            compiler.lvaParameterPassingInfo =
                [AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.InRegister(register, 0, 4))];
            codeGen.RegSet.rsMaskPreSpillRegArg = new(SRBM_R0 | SRBM_R2);
            codeGen.RegSet.rsMaskPreSpillAlign = new(SRBM_R3);

            Assert.That(codeGen.RegSet.rsMaskPreSpillRegs(false).IntRegSet, Is.EqualTo(SRBM_R0 | SRBM_R2));
            Assert.That(codeGen.RegSet.rsMaskPreSpillRegs(true).IntRegSet,
                Is.EqualTo(SRBM_R0 | SRBM_R2 | SRBM_R3));
            Assert.That(compiler.lvaGetRelativeOffsetToCallerAllocatedSpaceForParameter(0, out var offset), Is.True);
            Assert.That(offset, Is.EqualTo(callerOffset));
            compiler.lvaAssignVirtualFrameOffsetsToArgs();
            Assert.That(compiler.lvaTable[0].StackOffset, Is.EqualTo(virtualOffset));
            Assert.That(compiler.lvaParamHasLocalStackSpace(0), Is.EqualTo(requiresLocalHome));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ArmPrespillClassificationUsesTheParentAbiForPromotedFields(bool field)
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, _) =>
        {
            compiler.info.compArgsCount = 1;
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(4);
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].lvIsRegArg = true;
            compiler.lvaTable[0].lvPromoted = true;
            compiler.lvaTable[0].lvFieldCnt = 1;
            compiler.lvaTable[0].lvFieldLclStart = 1;
            compiler.lvaTable[1].Type = TYP_INT;
            compiler.lvaTable[1].lvIsParam = true;
            compiler.lvaTable[1].lvIsRegArg = true;
            compiler.lvaTable[1].lvIsStructField = true;
            compiler.lvaTable[1].lvParentLcl = 0;
            compiler.lvaParameterPassingInfo =
                [AbiPassingInformation.FromSegment(compiler, false, AbiPassingSegment.InRegister(REG_R2, 0, 4))];

            Assert.That(compiler.lvaIsPreSpilled(field ? 1 : 0, new regMaskTP(SRBM_R2)), Is.True);
            Assert.That(compiler.lvaIsPreSpilled(field ? 1 : 0, new regMaskTP(SRBM_R0)), Is.False);
        });
    }
#endif
}
