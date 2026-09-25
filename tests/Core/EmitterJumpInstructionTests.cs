// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using JumpLists = RyuJitSharp.UnitTests.EmitterGroupBufferTests.DescriptorFactory;

namespace RyuJitSharp.UnitTests;

internal static class EmitterJumpInstructionTests
{
    [TestCase(EJ_NONE, INS_nop)]
    [TestCase(EJ_jmp, INS_jmp)]
    [TestCase(EJ_jo, INS_jo)]
    [TestCase(EJ_jno, INS_jno)]
    [TestCase(EJ_jb, INS_jb)]
    [TestCase(EJ_jae, INS_jae)]
    [TestCase(EJ_je, INS_je)]
    [TestCase(EJ_jne, INS_jne)]
    [TestCase(EJ_jbe, INS_jbe)]
    [TestCase(EJ_ja, INS_ja)]
    [TestCase(EJ_js, INS_js)]
    [TestCase(EJ_jns, INS_jns)]
    [TestCase(EJ_jp, INS_jp)]
    [TestCase(EJ_jnp, INS_jnp)]
    [TestCase(EJ_jl, INS_jl)]
    [TestCase(EJ_jge, INS_jge)]
    [TestCase(EJ_jle, INS_jle)]
    [TestCase(EJ_jg, INS_jg)]
    [TestCase(EJ_COUNT, INS_call)]
    public static void JumpKindsRetainTheCompleteNativeMappingIncludingTheCallSentinel(
        emitJumpKind kind, instruction expected)
    {
        Assert.That(Emitter.emitJumpKindToIns(kind), Is.EqualTo(expected));
    }

    [TestCase(INS_jmp, false, 5u)]
    [TestCase(INS_jmp, true, 5u)]
    [TestCase(INS_jo, false, 6u)]
    [TestCase(INS_jne, true, 6u)]
    [TestCase(INS_call, false, 5u)]
    [TestCase(INS_push, false, 5u)]
    [TestCase(INS_push_hide, false, 5u)]
    public static void ForwardLabelsRetainNativeMaximumSizesAndKeepShortMarker(
        instruction ins, bool keepShort, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            emitter.emitIns_J(ins, target, keepShort);
            var id = Last(emitter);

            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_LABEL));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(48));
            Assert.That(id.idIsSmallDsc(), Is.False);
            Assert.That(JumpView.Target(id), Is.SameAs(target));
            Assert.That(JumpView.Group(id), Is.SameAs(emitter.emitCurIG));
            Assert.That(JumpView.Offset(id), Is.Zero);
            Assert.That(JumpView.IsShort(id), Is.EqualTo(keepShort));
            Assert.That(JumpView.KeepLong(id), Is.False);
            Assert.That(JumpLists.PendingJump(emitter), Is.SameAs(id));
            Assert.That(JumpLists.NextJump(id), Is.Null);
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)size));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
        });
    }

    [TestCase(INS_jmp, 125, 0u, true)]
    [TestCase(INS_jmp, 126, 0u, true)]
    [TestCase(INS_jmp, 127, 0u, false)]
    [TestCase(INS_jo, 126, 0u, true)]
    [TestCase(INS_jne, 127, 0u, false)]
    [TestCase(INS_jmp, int.MaxValue, 2147483600u, true)]
    [TestCase(INS_jne, -4, 4294967280u, true)]
    [TestCase(INS_jmp, -2, 4294967291u, true)]
    public static void BackwardEstimatesPreserveSignedBoundariesAndUnsignedWraparound(
        instruction ins, int currentOffset, uint targetOffset, bool shortJump)
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            target.bbEmitCookie = new insGroup { igOffs = targetOffset };
            CodeOffset(emitter) = currentOffset;
            emitter.emitIns_J(ins, target, keepShort: false);

            var id = Last(emitter);
            Assert.That(JumpView.IsShort(id), Is.EqualTo(shortJump));
            Assert.That(id.idCodeSize(), Is.EqualTo(shortJump ? 2u : (ins == INS_jmp ? 5u : 6u)));
            Assert.That(CodeOffset(emitter), Is.EqualTo(currentOffset));
            Assert.That(JumpView.Target(id), Is.SameAs(target));
            Assert.That(target.bbEmitCookie.igOffs, Is.EqualTo(targetOffset));
        });
    }

    [Test]
    public static void BackwardEstimateIncludesInstructionsAlreadyInTheCurrentGroup()
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            target.bbEmitCookie = new insGroup { igOffs = 0 };
            CodeOffset(emitter) = 120;
            emitter.emitIns_Nop(7);
            emitter.emitIns_J(INS_jmp, target, keepShort: false);

            var id = Last(emitter);
            Assert.That(JumpView.Offset(id), Is.EqualTo(7u));
            Assert.That(JumpView.IsShort(id), Is.False);
            Assert.That(id.idCodeSize(), Is.EqualTo(5u));
            Assert.That(CurrentSize(emitter), Is.EqualTo(12));
        });
    }

    [Test]
    public static void HotColdSeparationDependsOnTheColdSectionAndRetainsTheRequestedMarker(
        [Values(false, true)] bool hasColdSection, [Values(false, true)] bool sourceCold,
        [Values(false, true)] bool targetCold, [Values(false, true)] bool keepShort)
    {
        WithEmitter((compiler, emitter) =>
        {
            var source = compiler.compCurBB ?? throw new AssertionException("Missing current block.");
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
            target.bbEmitCookie = new insGroup();
            var different = hasColdSection && (sourceCold != targetCold);

            emitter.emitIns_J(INS_jo, target, keepShort);

            var id = Last(emitter);
            Assert.That(compiler.fgInDifferentRegions(source, target), Is.EqualTo(different));
            Assert.That(JumpView.KeepLong(id), Is.EqualTo(different));
            Assert.That(JumpView.IsShort(id), Is.EqualTo(keepShort || !different));
            Assert.That(id.idCodeSize(), Is.EqualTo(different ? 6u : 2u));
        });
    }

    [Test]
    public static void PendingJumpsReverseIntoEmissionOrderWithoutReplacingDescriptorsOrTargets()
    {
        WithEmitter((compiler, emitter) =>
        {
            var target = Label();
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            emitter.emitIns_Nop(3);
            emitter.emitIns_J(INS_jo, target, keepShort: false);
            var first = Last(emitter);
            emitter.emitIns_J(INS_jmp, target, keepShort: false, isRemovableJmpCandidate: true);
            var second = Last(emitter);

            Assert.That(JumpLists.PendingJump(emitter), Is.SameAs(second));
            Assert.That(JumpLists.NextJump(second), Is.SameAs(first));
            Assert.That(JumpLists.NextJump(first), Is.Null);
            Assert.That(JumpView.Offset(first), Is.EqualTo(3u));
            Assert.That(JumpView.Offset(second), Is.EqualTo(9u));
            Assert.That(first.StorageIndex, Is.EqualTo(1));
            Assert.That(second.StorageIndex, Is.EqualTo(2));

            target.bbEmitCookie = emitter.emitAddInlineLabel();

            Assert.That(group.igSize, Is.EqualTo(14));
            Assert.That(group.igData, Has.Length.EqualTo(3));
            Assert.That(group.igData?[1], Is.SameAs(first));
            Assert.That(group.igData?[2], Is.SameAs(second));
            Assert.That(JumpLists.FirstJump(emitter), Is.SameAs(first));
            Assert.That(JumpLists.LastJump(emitter), Is.SameAs(second));
            Assert.That(JumpLists.NextJump(first), Is.SameAs(second));
            Assert.That(JumpLists.NextJump(second), Is.Null);
            Assert.That(JumpLists.PendingJump(emitter), Is.Null);
            Assert.That(JumpView.Target(first), Is.SameAs(target));
            Assert.That(JumpView.Target(second), Is.SameAs(target));
            Assert.That(JumpView.Group(second), Is.SameAs(group));
            Assert.That(target.bbEmitCookie.igOffs, Is.EqualTo(14u));

            emitter.emitIns_J(INS_jne, target, keepShort: false);
            var third = Last(emitter);
            _ = emitter.emitAddInlineLabel();
            Assert.That(JumpLists.NextJump(second), Is.SameAs(third));
            Assert.That(JumpLists.LastJump(emitter), Is.SameAs(third));
            Assert.That(JumpLists.NextJump(third), Is.Null);
            Assert.That(JumpView.Offset(third), Is.Zero);
            Assert.That(JumpView.IsShort(third), Is.True);
            Assert.That(ContainsRemovable(emitter), Is.True);
        });
    }

    [Test]
    public static void LabelPushRelocationIsDistinctFromCallAndBranchRelocation(
        [Values(INS_push, INS_push_hide, INS_call, INS_jmp)] instruction ins,
        [Values(false, true)] bool relocatable)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compReloc = relocatable;
            var depth = emitter.emitCntStackDepth;
            var maximum = emitter.emitMaxStackDepth;
            var target = Label();
            target.bbEmitCookie = emitter.emitCurIG;
            emitter.emitIns_J(ins, target, keepShort: false);

            var id = Last(emitter);
            Assert.That(id.idIsDspReloc(), Is.EqualTo(relocatable && (ins is INS_push or INS_push_hide)));
            Assert.That(id.idIsCnsReloc(), Is.False);
            Assert.That(id.idCodeSize(), Is.EqualTo(ins == INS_jmp ? 2u : 5u));
            Assert.That(emitter.emitCntStackDepth, Is.EqualTo(depth));
            Assert.That(emitter.emitMaxStackDepth, Is.EqualTo(maximum));
        });
    }

    [Test]
    public static void RemovableMetadataCapturesTheInstructionImmediatelyBeforeAllocation(
        [Values(false, true)] bool afterCall, [Values(false, true)] bool interveningInstruction,
        [Values(false, true)] bool removable)
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            if (afterCall)
            {
                emitter.emitIns_J(INS_call, target, keepShort: false);
            }
            else
            {
                emitter.emitIns_Nop(1);
            }
            if (interveningInstruction)
            {
                emitter.emitIns_Nop(1);
            }
            emitter.emitIns_J(INS_jmp, target, keepShort: false, isRemovableJmpCandidate: removable);

            var id = Last(emitter);
            Assert.That(JumpView.IsRemovable(id), Is.EqualTo(removable));
            Assert.That(JumpView.AfterCall(id), Is.EqualTo(removable && afterCall && !interveningInstruction));
            Assert.That(ContainsRemovable(emitter), Is.EqualTo(removable));
        });
    }

    [Test]
    public static void AllocationRolloverRetainsCallAdjacencyAndRecordsTheNewGroupOffset()
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            emitter.emitIns_J(INS_call, target, keepShort: false);
            var call = Last(emitter);
            var oldGroup = emitter.emitCurIG;
            Capacity(emitter) = Used(emitter) + 1;

            emitter.emitIns_J(INS_jmp, target, keepShort: false, isRemovableJmpCandidate: true);

            var jump = Last(emitter);
            Assert.That(emitter.emitCurIG, Is.Not.SameAs(oldGroup));
            Assert.That(JumpView.Group(call), Is.SameAs(oldGroup));
            Assert.That(JumpView.Group(jump), Is.SameAs(emitter.emitCurIG));
            Assert.That(JumpView.Offset(jump), Is.Zero);
            Assert.That(JumpView.AfterCall(jump), Is.True);
            Assert.That(JumpLists.FirstJump(emitter), Is.SameAs(call));
            Assert.That(JumpLists.PendingJump(emitter), Is.SameAs(jump));
            Assert.That(JumpLists.NextJump(jump), Is.Null);
            Assert.That(CodeOffset(emitter), Is.EqualTo(5));
            Assert.That(CurrentSize(emitter), Is.EqualTo(5));
            Assert.That(jump.StorageIndex, Is.Zero);
        });
    }

#if DEBUG
    [TestCase(INS_call, true, true)]
    [TestCase(INS_call, false, false)]
    [TestCase(INS_jmp, true, false)]
    public static void FinallyCallDiagnosticIsAddedToTheAllocatedMetadata(
        instruction ins, bool finallyBlock, bool expected)
    {
        WithEmitter((compiler, emitter) =>
        {
            var target = Label();
            if (finallyBlock)
            {
                compiler.fgSafeBasicBlockCreation = true;
                compiler.compCurBB = BasicBlock.New(compiler, BBJ_CALLFINALLY);
                compiler.compCurBB.TargetEdge = new FlowEdge(compiler.compCurBB, target, null);
                compiler.fgSafeBasicBlockCreation = false;
            }
            emitter.emitIns_J(ins, target, keepShort: false);

            var info = Last(emitter).idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");
            Assert.That(info.idFinallyCall, Is.EqualTo(expected));
            Assert.That(info.idNum, Is.EqualTo(1u));
            Assert.That(info.idSize, Is.EqualTo((nuint)48));
        });
    }

    [TestCase(INS_jmp)]
    [TestCase(INS_jo)]
    [TestCase(INS_call)]
    [TestCase(INS_push)]
    public static void D005RejectionPreservesInstructionBuffersAndJumpLists(instruction ins)
    {
        WithEmitter((compiler, emitter) =>
        {
            var target = Label();
            emitter.emitIns_J(INS_call, target, keepShort: false);
            var previous = Last(emitter);
            var used = Used(emitter);
            var size = CurrentSize(emitter);
            var count = CurrentCount(emitter);
            compiler.opts.dspCode = true;

            var exception = Assert.Throws<FatalJitException>(() =>
                emitter.emitIns_J(ins, target, keepShort: true, isRemovableJmpCandidate: true));

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(Last(emitter), Is.SameAs(previous));
            Assert.That(Used(emitter), Is.EqualTo(used));
            Assert.That(CurrentSize(emitter), Is.EqualTo(size));
            Assert.That(CurrentCount(emitter), Is.EqualTo(count));
            Assert.That(JumpLists.PendingJump(emitter), Is.SameAs(previous));
            Assert.That(JumpLists.NextJump(previous), Is.Null);
            Assert.That(ContainsRemovable(emitter), Is.False);
            Assert.That(target.bbEmitCookie, Is.Null);
        });
    }
#endif

    private static BasicBlock Label()
    {
        var block = new BasicBlock(null, null);
        block.SetFlags(BBF_HAS_LABEL);

        return block;
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX,
            (compiler, codeGen, _) => action(compiler, codeGen.Emitter));
    }

    private static Emitter.instrDesc Last(Emitter emitter)
        => LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");

    internal abstract class JumpView(CodeGen codeGen) : Emitter(codeGen)
    {
        public static BasicBlock? Target(instrDesc id) => ((instrDescJmp)id).idjTarget;
        public static insGroup? Group(instrDesc id) => ((instrDescJmp)id).idjIG;
        public static uint Offset(instrDesc id) => ((instrDescJmp)id).idjOffs;
        public static bool IsShort(instrDesc id) => ((instrDescJmp)id).idjShort;
        public static bool KeepLong(instrDesc id) => ((instrDescJmp)id).idjKeepLong;
        public static bool IsRemovable(instrDesc id) => ((instrDescJmp)id).idjIsRemovableJmpCandidate;
        public static bool AfterCall(instrDesc id) => ((instrDescJmp)id).idjIsAfterCallBeforeEpilog;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurCodeOffset")]
    private static extern ref int CodeOffset(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeEndp")]
    private static extern ref nuint Capacity(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitContainsRemovableJmpCandidates")]
    private static extern ref bool ContainsRemovable(Emitter emitter);
}
