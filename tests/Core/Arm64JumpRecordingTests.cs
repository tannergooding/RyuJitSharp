// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64JumpRecordingTests
{
    [Test]
    public static void GeneratedJumpMappingPreservesEveryNativeOrdinal()
    {
        ReadOnlySpan<instruction> expected =
        [
            INS_nop, INS_b, INS_beq, INS_bne, INS_bhs, INS_blo, INS_bmi, INS_bpl,
            INS_bvs, INS_bvc, INS_bhi, INS_bls, INS_bge, INS_blt, INS_bgt, INS_ble,
        ];
        Assert.That(expected.Length, Is.EqualTo((int)EJ_COUNT));
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.That(Emitter.emitJumpKindToIns((emitJumpKind)index), Is.EqualTo(expected[index]));
        }
    }

    [TestCase(INS_cbz, IF_LARGEJMP, IF_BI_1A)]
    [TestCase(INS_cbnz, IF_LARGEJMP, IF_BI_1A)]
    [TestCase(INS_tbz, IF_LARGEJMP, IF_BI_1B)]
    [TestCase(INS_tbnz, IF_LARGEJMP, IF_BI_1B)]
    [TestCase(INS_bne, IF_LARGEJMP, IF_BI_0B)]
    [TestCase(INS_adr, IF_LARGEADR, IF_DI_1E)]
    [TestCase(INS_adr, IF_DI_1E, IF_DI_1E)]
    [TestCase(INS_ldr, IF_LARGELDC, IF_LS_1A)]
    [TestCase(INS_ldr, IF_LS_1A, IF_LS_1A)]
    public static void ShortSelectionPreservesAllFormatsAndTheKeepLongVeto(
        instruction ins, Emitter.insFormat initial, Emitter.insFormat expected)
    {
        JumpView.CheckShortSelection(ins, initial, expected, keepLong: false);
        JumpView.CheckShortSelection(ins, initial, initial, keepLong: true);
    }

    [Test]
    public static void NormalBranchesRetainNativeFormatsAndDoNotEstimateBackwardDistances(
        [Values(INS_b, INS_bl_local, INS_beq, INS_bne, INS_bhs, INS_blo, INS_bmi, INS_bpl,
            INS_bvs, INS_bvc, INS_bhi, INS_bls, INS_bge, INS_blt, INS_bgt, INS_ble)] instruction ins,
        [Values(false, true)] bool hasCookie)
    {
        var (_, _, emitter) = CreateEmitter();
        var format = ins is INS_b or INS_bl_local ? IF_BI_0A : IF_LARGEJMP;
        var size = ins is INS_b or INS_bl_local ? 4u : 8u;
        var target = Label();
        if (hasCookie)
        {
            target.bbEmitCookie = emitter.emitCurIG;
        }

        emitter.emitIns(INS_nop);
        emitter.emitIns_J(ins, target);
        var id = Last(emitter);

        JumpView.Check(id, target, emitter.emitCurIG, 4u, keepShort: false, keepLong: false);
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(format));
        Assert.That(id.idCodeSize(), Is.EqualTo(size));
        Assert.That(id.NativeLogicalSize, Is.EqualTo(48));
        Assert.That(id.idIsSmallDsc(), Is.False);
        Assert.That(id.idIsBound(), Is.False);
        Assert.That(JumpView.Pending(emitter), Is.SameAs(id));
        Assert.That(JumpView.Next(id), Is.Null);
        Assert.That(GroupSize(emitter), Is.EqualTo(4 + (int)size));
        Assert.That(GroupCount(emitter), Is.EqualTo(2));
    }

    [Test]
    public static void ForcedShortConditionalBranchesSelectTheSingleInstructionForm(
        [Values(INS_beq, INS_bne, INS_bhs, INS_blo, INS_bmi, INS_bpl, INS_bvs, INS_bvc,
            INS_bhi, INS_bls, INS_bge, INS_blt, INS_bgt, INS_ble)] instruction ins,
        [Values(false, true)] bool useShortJumpEntryPoint)
    {
        var (compiler, _, emitter) = CreateEmitter();
        var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
        group.igFlags |= InsGroupFlags.FuncletProlog;
        var target = Label();
        target.bbEmitCookie = group;
        target.SetFlags(BBF_COLD);
        compiler.fgFirstColdBlock = target;

        if (useShortJumpEntryPoint)
        {
            emitter.emitIns_ShortJ(ins, target);
        }
        else
        {
            emitter.emitIns_J(ins, target, keepShort: true);
        }

        var id = Last(emitter);
        JumpView.Check(id, target, group, 0u, keepShort: true, keepLong: false);
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_BI_0B));
        Assert.That(id.idCodeSize(), Is.EqualTo(4u));
        Assert.That(JumpView.Pending(emitter), Is.SameAs(id));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
        Assert.That(GroupCount(emitter), Is.EqualTo(1));
    }

    [TestCase(InsGroupFlags.Prolog)]
    [TestCase(InsGroupFlags.Epilog)]
    [TestCase(InsGroupFlags.FuncletProlog)]
    [TestCase(InsGroupFlags.FuncletEpilog)]
    public static void ShortJumpEntryPointAcceptsEveryNativeOutOfOrderRegion(InsGroupFlags flags)
    {
        var (_, _, emitter) = CreateEmitter();
        var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
        group.igFlags |= flags;
        var target = Label();
        target.bbEmitCookie = group;

        emitter.emitIns_ShortJ(INS_bne, target);

        var id = Last(emitter);
        JumpView.Check(id, target, group, 0u, keepShort: true, keepLong: false);
        Assert.That(id.idInsFmt(), Is.EqualTo(IF_BI_0B));
        Assert.That(GroupSize(emitter), Is.EqualTo(4));
    }

    [Test]
    public static void NormalHotColdPolicyDependsOnThePresenceOfAColdSection(
        [Values(INS_b, INS_beq)] instruction ins, [Values(false, true)] bool hasColdSection,
        [Values(false, true)] bool sourceCold, [Values(false, true)] bool targetCold)
    {
        var (compiler, _, emitter) = CreateEmitter();
        var source = compiler.compCurBB ?? throw new AssertionException("Missing source block.");
        var target = Label();
        if (sourceCold)
        {
            source.SetFlags(BBF_COLD);
        }
        if (targetCold)
        {
            target.SetFlags(BBF_COLD);
        }
        compiler.fgFirstColdBlock = hasColdSection ? Label() : null;

        emitter.emitIns_J(ins, target);

        var id = Last(emitter);
        JumpView.Check(id, target, emitter.emitCurIG, 0u, keepShort: false,
            keepLong: hasColdSection && (sourceCold != targetCold));
        Assert.That(id.idInsFmt(), Is.EqualTo(ins == INS_b ? IF_BI_0A : IF_LARGEJMP));
        Assert.That(id.idCodeSize(), Is.EqualTo(ins == INS_b ? 4u : 8u));
    }

    [Test]
    public static void PendingJumpListRetainsReverseRecordingOrderAndOffsets()
    {
        var (_, _, emitter) = CreateEmitter();
        var firstTarget = Label();
        var secondTarget = Label();
        emitter.emitIns(INS_nop);
        emitter.emitIns_J(INS_beq, firstTarget);
        var first = Last(emitter);
        emitter.emitIns_J(INS_b, secondTarget);
        var second = Last(emitter);

        JumpView.Check(first, firstTarget, emitter.emitCurIG, 4u, keepShort: false, keepLong: false);
        JumpView.Check(second, secondTarget, emitter.emitCurIG, 12u, keepShort: false, keepLong: false);
        Assert.That(JumpView.Pending(emitter), Is.SameAs(second));
        Assert.That(JumpView.Next(second), Is.SameAs(first));
        Assert.That(JumpView.Next(first), Is.Null);
        Assert.That(GroupSize(emitter), Is.EqualTo(16));
        Assert.That(GroupCount(emitter), Is.EqualTo(3));
        Assert.That(second.StorageIndex, Is.EqualTo(first.StorageIndex + 1));
    }

    [Test]
    public static void XarchRemovableArgumentDoesNotIntroduceArm64Metadata(
        [Values(false, true)] bool removable)
    {
        var (_, _, emitter) = CreateEmitter();
        emitter.emitIns_J(INS_b, Label(), isRemovableJmpCandidate: removable);

        Assert.That(JumpView.IsRemovable(Last(emitter)), Is.False);
        Assert.That(ContainsRemovable(emitter), Is.False);
    }

    [TestCase(EJ_jmp, INS_b)]
    [TestCase(EJ_eq, INS_beq)]
    [TestCase(EJ_ne, INS_bne)]
    [TestCase(EJ_vc, INS_bvc)]
    [TestCase(EJ_lo, INS_blo)]
    public static void CodegenJumpUsesTheArm64InstructionMapping(emitJumpKind kind, instruction expected)
    {
        var (_, codeGen, emitter) = CreateEmitter();
        var target = Label();
        codeGen.inst_JMP(kind, target);

        var id = Last(emitter);
        Assert.That(id.idIns(), Is.EqualTo(expected));
        JumpView.Check(id, target, emitter.emitCurIG, 0u, keepShort: false, keepLong: false);
    }

#if DEBUG
    [Test]
    public static void ForcedShortOverridesDebugLongAddressAndRegionPolicy(
        [Values(false, true)] bool forceLong, [Values(false, true)] bool differentRegion,
        [Values(false, true)] bool keepShort)
    {
        var (compiler, _, emitter) = CreateEmitter();
        var target = Label();
        compiler.opts.compLongAddress = forceLong;
        if (differentRegion)
        {
            target.SetFlags(BBF_COLD);
            compiler.fgFirstColdBlock = target;
        }

        emitter.emitIns_J(INS_bne, target, keepShort);

        var id = Last(emitter);
        JumpView.Check(id, target, emitter.emitCurIG, 0u, keepShort,
            keepLong: !keepShort && (forceLong || differentRegion));
        Assert.That(id.idInsFmt(), Is.EqualTo(keepShort ? IF_BI_0B : IF_LARGEJMP));
        Assert.That(id.idCodeSize(), Is.EqualTo(keepShort ? 4u : 8u));
    }

    [TestCase(INS_bl_local, true, true)]
    [TestCase(INS_bl_local, false, false)]
    [TestCase(INS_b, true, false)]
    public static void FinallyCallMarkerIsSpecificToTheLocalCallInACallFinallyBlock(
        instruction ins, bool finallyBlock, bool expected)
    {
        var (compiler, _, emitter) = CreateEmitter();
        var target = Label();
        if (finallyBlock)
        {
            var source = compiler.compCurBB ?? throw new AssertionException("Missing source block.");
            using var tls = new JitTls(null);
            var previous = JitTls.Compiler;
            try
            {
                JitTls.Compiler = compiler;
                source.SetKindAndTargetEdge(BBJ_CALLFINALLY, new FlowEdge(source, target, null));
            }
            finally
            {
                JitTls.Compiler = previous;
            }
        }

        emitter.emitIns_J(ins, target);

        var info = Last(emitter).idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");
        Assert.That(info.idFinallyCall, Is.EqualTo(expected));
        Assert.That(info.idNum, Is.EqualTo(1u));
        Assert.That(info.idSize, Is.EqualTo((nuint)48));
    }
#endif

    private static BasicBlock Label()
    {
        var target = new BasicBlock(null, null);
        target.SetFlags(BBF_HAS_LABEL);

        return target;
    }

    private static (Compiler Compiler, CodeGen CodeGen, Emitter Emitter) CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.compCurBB = new BasicBlock(null, null);
        var codeGen = new CodeGen(compiler);
        var emitter = codeGen.Emitter;
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );

        return (compiler, codeGen, emitter);
    }

    private static Emitter.instrDesc Last(Emitter emitter)
        => LastInstruction(emitter) ?? throw new AssertionException("No branch was recorded.");

    private abstract class JumpView(CodeGen codeGen) : Emitter(codeGen)
    {
        public static void CheckShortSelection(instruction ins, insFormat initial, insFormat expected, bool keepLong)
        {
            var id = new instrDescJmp { idjKeepLong = keepLong };
            id.idIns(ins);
            id.idInsFmt(initial);
            SetShortJump(null, id);

            Assert.That(id.idInsFmt(), Is.EqualTo(expected));
            Assert.That(id.idjShort, Is.EqualTo(!keepLong));
            Assert.That(id.idjKeepLong, Is.EqualTo(keepLong));
        }

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitSetShortJump")]
        private static extern void SetShortJump(Emitter? emitter, instrDescJmp id);

        public static void Check(instrDesc id, BasicBlock target, insGroup? group, uint offset,
            bool keepShort, bool keepLong)
        {
            var jump = (instrDescJmp)id;
            Assert.That(jump.idjTarget, Is.SameAs(target));
            Assert.That(jump.idjTargetIG, Is.Null);
            Assert.That(jump.idjIG, Is.SameAs(group));
            Assert.That(jump.idjOffs, Is.EqualTo(offset));
            Assert.That(jump.idjShort, Is.EqualTo(keepShort));
            Assert.That(jump.idjKeepLong, Is.EqualTo(keepLong));
        }

        public static instrDesc? Next(instrDesc id) => ((instrDescJmp)id).idjNext;
        public static bool IsRemovable(instrDesc id) => ((instrDescJmp)id).idjIsRemovableJmpCandidate;
        public static instrDesc? Pending(Emitter emitter) => PendingJump(emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGjmpList")]
        private static extern ref instrDescJmp? PendingJump(Emitter emitter);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int GroupCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitContainsRemovableJmpCandidates")]
    private static extern ref bool ContainsRemovable(Emitter emitter);
}
#endif
