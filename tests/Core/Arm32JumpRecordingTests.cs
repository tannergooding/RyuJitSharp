// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm32JumpRecordingTests
{
    [TestCase(INS_b, IF_T2_J2, ISZ_32BIT, 4u, false)]
    [TestCase(INS_bl, IF_T2_J2, ISZ_32BIT, 4u, true)]
    [TestCase(INS_beq, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_bne, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_bhs, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_blo, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_bmi, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_bpl, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_bvs, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_bvc, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_bhi, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_bls, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_bge, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_blt, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_bgt, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    [TestCase(INS_ble, IF_LARGEJMP, ISZ_48BIT, 6u, false)]
    public static void BranchesUseNativeFormatsAndRecordTheirTargets(
        instruction ins, Emitter.insFormat expectedFormat, Emitter.insSize expectedSize,
        uint expectedCodeSize, bool expectedKeepLong)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var emitter = codeGen.Emitter;
            var target = Label();
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            var initialGroupSize = GroupSize(emitter);

            var jump = RecordJump(emitter, ins, target);

            Assert.That(jump.idIns(), Is.EqualTo(ins));
            Assert.That(jump.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(jump.idInsSize(), Is.EqualTo(expectedSize));
            Assert.That(jump.idCodeSize(), Is.EqualTo(expectedCodeSize));
            Assert.That(jump.idjTarget, Is.SameAs(target));
            Assert.That(jump.idjTargetIG, Is.Null);
            Assert.That(jump.idjIG, Is.SameAs(group));
            Assert.That(jump.idjOffs, Is.EqualTo(unchecked((uint)initialGroupSize)));
            Assert.That(jump.idjShort, Is.False);
            Assert.That(jump.idjKeepLong, Is.EqualTo(expectedKeepLong));
            Assert.That(PendingJump(emitter), Is.SameAs(jump));
            Assert.That(jump.idjNext, Is.Null);
#if !DEBUG
            Assert.That(GroupSize(emitter), Is.EqualTo(initialGroupSize + (int)expectedCodeSize));
#endif
        });
    }

    [TestCase(INS_b, IF_T1_M)]
    [TestCase(INS_beq, IF_T1_K)]
    public static void ForcedShortBranchesSelectTheirNativeFormats(instruction ins, Emitter.insFormat expectedFormat)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var emitter = codeGen.Emitter;
            var jump = RecordJump(emitter, ins, Label(), keepShort: true);

            Assert.That(jump.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(jump.idInsSize(), Is.EqualTo(ISZ_16BIT));
            Assert.That(jump.idCodeSize(), Is.EqualTo(2u));
            Assert.That(jump.idjShort, Is.True);
            Assert.That(jump.idjKeepLong, Is.False);
#if !DEBUG
            Assert.That(GroupSize(emitter), Is.EqualTo(2));
#endif
        });
    }

    [Test]
    public static void BackwardBranchesSelectFormatsAtNativeDistanceBoundaries()
    {
        AssertBackwardBranch(INS_b, -JMP_DIST_SMALL_MAX_NEG - 4, IF_T1_M, ISZ_16BIT, 2u, isShort: true);
        AssertBackwardBranch(INS_b, -JMP_DIST_SMALL_MAX_NEG - 3, IF_T2_J2, ISZ_32BIT, 4u, isShort: false);
        AssertBackwardBranch(INS_beq, -JCC_DIST_SMALL_MAX_NEG - 4, IF_T1_K, ISZ_16BIT, 2u, isShort: true);
        AssertBackwardBranch(INS_beq, -JCC_DIST_SMALL_MAX_NEG - 3, IF_T2_J1, ISZ_32BIT, 4u, isShort: false);
        AssertBackwardBranch(INS_beq, -JCC_DIST_MEDIUM_MAX_NEG - 4, IF_T2_J1, ISZ_32BIT, 4u, isShort: false);
        AssertBackwardBranch(INS_beq, -JCC_DIST_MEDIUM_MAX_NEG - 3, IF_LARGEJMP, ISZ_48BIT, 6u, isShort: false);
    }

    [Test]
    public static void ShortSelectionLeavesLabelLoadsLongAndPreservesCompareBranches()
    {
        var labelLoad = new Emitter.instrDescJmp();
        labelLoad.idInsFmt(IF_T2_M1);
        SetShortJump(null, labelLoad);
        Assert.That(labelLoad.idInsFmt(), Is.EqualTo(IF_T2_M1));
        Assert.That(labelLoad.idjShort, Is.False);

        var compareBranch = new Emitter.instrDescJmp { idjShort = true };
        compareBranch.idInsFmt(IF_T1_I);
        SetShortJump(null, compareBranch);
        Assert.That(compareBranch.idInsFmt(), Is.EqualTo(IF_T1_I));
        Assert.That(compareBranch.idjShort, Is.True);
    }

    [Test]
    public static void CrossingTheHotColdBoundaryForcesTheLongBranchForm()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var target = Label();
            target.SetFlags(BBF_COLD);
            compiler.fgFirstColdBlock = target;
            var emitter = codeGen.Emitter;
            target.bbEmitCookie = emitter.emitCurIG;

            var jump = RecordJump(emitter, INS_beq, target);

            Assert.That(jump.idInsFmt(), Is.EqualTo(IF_LARGEJMP));
            Assert.That(jump.idjKeepLong, Is.True);
            Assert.That(jump.idjShort, Is.False);
        });
    }

    [TestCase(INS_cbz, REG_R0)]
    [TestCase(INS_cbnz, REG_R7)]
    public static void CompareAndBranchInstructionsUseShortRegisterBranchMetadata(
        instruction ins, regNumber reg)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var emitter = codeGen.Emitter;
            var target = Label();
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            var initialGroupSize = GroupSize(emitter);

            var jump = RecordConditionalRegisterBranch(emitter, ins, EA_4BYTE, target, reg);

            Assert.That(jump.idIns(), Is.EqualTo(ins));
            Assert.That(jump.idInsFmt(), Is.EqualTo(IF_T1_I));
            Assert.That(jump.idInsSize(), Is.EqualTo(ISZ_16BIT));
            Assert.That(jump.idReg1(), Is.EqualTo(reg));
            Assert.That(jump.idjTarget, Is.SameAs(target));
            Assert.That(jump.idjTargetIG, Is.Null);
            Assert.That(jump.idjIG, Is.SameAs(group));
            Assert.That(jump.idjOffs, Is.EqualTo(unchecked((uint)initialGroupSize)));
            Assert.That(jump.idjShort, Is.True);
            Assert.That(jump.idjKeepLong, Is.False);
            Assert.That(jump.idjNext, Is.Null);
            Assert.That(PendingJump(emitter), Is.SameAs(jump));
#if !DEBUG
            Assert.That(GroupSize(emitter), Is.EqualTo(initialGroupSize + 2));
#endif
        });
    }

#if DEBUG
    [Test]
    public static void LongAddressModeForcesLongBranches()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            compiler.opts.compLongAddress = true;
            var jump = RecordJump(codeGen.Emitter, INS_b, Label());

            Assert.That(jump.idjKeepLong, Is.True);
            Assert.That(jump.idjShort, Is.False);
        });
    }

    [Test]
    public static void FinallyCallBranchSetsItsDebugMarker()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = compiler.compCurBB = new BasicBlock(null, null);
            var target = Label();
            source.SetKindAndTargetEdge(BBJ_CALLFINALLY, new FlowEdge(source, target, null));

            var jump = RecordJump(codeGen.Emitter, INS_bl, target);
            var debugInfo = jump.idDebugOnlyInfo() ?? throw new AssertionException("Missing jump debug information.");
            Assert.That(debugInfo.idFinallyCall, Is.True);
        });
    }
#endif

    private static void AssertBackwardBranch(
        instruction ins, int codeOffset, Emitter.insFormat expectedFormat,
        Emitter.insSize expectedSize, uint expectedCodeSize, bool isShort)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var emitter = codeGen.Emitter;
            var target = Label();
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igOffs = 0;
            target.bbEmitCookie = group;
            CodeOffset(emitter) = codeOffset;

            var jump = RecordJump(emitter, ins, target);

            Assert.That(jump.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(jump.idInsSize(), Is.EqualTo(expectedSize));
            Assert.That(jump.idCodeSize(), Is.EqualTo(expectedCodeSize));
            Assert.That(jump.idjShort, Is.EqualTo(isShort));
            Assert.That(jump.idjKeepLong, Is.False);
        });
    }

    private static Emitter.instrDescJmp RecordJump(
        Emitter emitter, instruction ins, BasicBlock target, bool keepShort = false)
    {
#if DEBUG
        try
        {
            emitter.emitIns_J(ins, target, keepShort);
        }
        catch (FatalJitException failure) when (failure.Result == CorJitResult.CORJIT_SKIPPED)
        {
        }
#else
        emitter.emitIns_J(ins, target, keepShort);
#endif
        return LastInstruction(emitter) as Emitter.instrDescJmp
            ?? throw new AssertionException("No jump descriptor was recorded.");
    }

    private static Emitter.instrDescJmp RecordConditionalRegisterBranch(
        Emitter emitter, instruction ins, emitAttr attr, BasicBlock target, regNumber reg)
    {
#if DEBUG
        try
        {
            emitter.emitIns_J_R(ins, attr, target, reg);
        }
        catch (FatalJitException failure) when (failure.Result == CorJitResult.CORJIT_SKIPPED)
        {
        }
#else
        emitter.emitIns_J_R(ins, attr, target, reg);
#endif
        return LastInstruction(emitter) as Emitter.instrDescJmp
            ?? throw new AssertionException("No conditional register-branch descriptor was recorded.");
    }

    private static BasicBlock Label()
    {
        var block = new BasicBlock(null, null);
        block.SetFlags(BBF_HAS_LABEL);

        return block;
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitSetShortJump")]
    private static extern void SetShortJump(Emitter? emitter, Emitter.instrDescJmp jump);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurCodeOffset")]
    private static extern ref int CodeOffset(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGjmpList")]
    private static extern ref Emitter.instrDescJmp? PendingJump(Emitter emitter);
}
#endif
