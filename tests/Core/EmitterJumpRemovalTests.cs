// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
#if DEBUG
using System.IO;
using System.Text;
#endif
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;
using static RyuJitSharp.regNumber;
#if DEBUG
using static RyuJitSharp.Globals;
#endif

namespace RyuJitSharp.UnitTests;

internal static class EmitterJumpRemovalTests
{
    [Test]
    public static void NoCandidatesLeaveTheSavedGroupsAndJumpListUntouched()
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(INS_jmp, target);
            var jump = Last(emitter);
            var successor = emitter.emitAddInlineLabel();
            TotalSize(emitter) = 5;

            emitter.emitRemoveJumpToNextInst();

            Assert.That(JumpView.First(emitter), Is.SameAs(jump));
            Assert.That(JumpView.Last(emitter), Is.SameAs(jump));
            Assert.That(jump.idCodeSize(), Is.EqualTo(5u));
            Assert.That(source.igSize, Is.EqualTo(5));
            Assert.That(successor.igOffs, Is.EqualTo(5u));
            Assert.That(TotalSize(emitter), Is.EqualTo(5));
        });
    }

    [Test]
    public static void RemovesSeparatedHeadAndMiddleCandidatesWithoutSkippingKeptJumps()
    {
        WithEmitter((_, emitter) =>
        {
            var firstTarget = Label();
            var secondTarget = Label();
            var firstGroup = emitter.emitCurIG ?? throw new AssertionException("Missing first group.");
            emitter.emitIns_J(INS_jmp, firstTarget, isRemovableJmpCandidate: true);
            var removedHead = Last(emitter);
            var secondGroup = emitter.emitAddInlineLabel();
            firstTarget.bbEmitCookie = secondGroup;

            emitter.emitIns_J(INS_jne, Label());
            var keptMiddle = Last(emitter);
            var thirdGroup = emitter.emitAddInlineLabel();

            emitter.emitIns_J(INS_jmp, secondTarget, isRemovableJmpCandidate: true);
            var removedMiddle = Last(emitter);
            var fourthGroup = emitter.emitAddInlineLabel();
            secondTarget.bbEmitCookie = fourthGroup;

            emitter.emitIns_J(INS_jmp, Label());
            var keptTail = Last(emitter);
            var finalGroup = emitter.emitAddInlineLabel();
            TotalSize(emitter) = 21;

            emitter.emitRemoveJumpToNextInst();

            Assert.That(JumpView.First(emitter), Is.SameAs(keptMiddle));
            Assert.That(JumpView.Next(keptMiddle), Is.SameAs(keptTail));
            Assert.That(JumpView.Last(emitter), Is.SameAs(keptTail));
            Assert.That(JumpView.Next(keptTail), Is.Null);
            Assert.That(JumpView.Next(removedHead), Is.SameAs(keptMiddle));
            Assert.That(JumpView.Next(removedMiddle), Is.SameAs(keptTail));
            Assert.That(JumpView.Removable(removedHead), Is.True);
            Assert.That(JumpView.Removable(removedMiddle), Is.True);
            Assert.That(removedHead.idCodeSize(), Is.Zero);
            Assert.That(removedMiddle.idCodeSize(), Is.Zero);
            Assert.That(firstGroup.igData?[0], Is.SameAs(removedHead));
            Assert.That(thirdGroup.igData?[0], Is.SameAs(removedMiddle));
            Assert.That(firstGroup.igInsCnt, Is.EqualTo(1));
            Assert.That(thirdGroup.igInsCnt, Is.EqualTo(1));
            Assert.That(firstGroup.igSize, Is.Zero);
            Assert.That(secondGroup.igSize, Is.EqualTo(6));
            Assert.That(thirdGroup.igSize, Is.Zero);
            Assert.That(fourthGroup.igSize, Is.EqualTo(5));
            Assert.That(secondGroup.igOffs, Is.Zero);
            Assert.That(thirdGroup.igOffs, Is.EqualTo(6u));
            Assert.That(fourthGroup.igOffs, Is.EqualTo(6u));
            Assert.That(finalGroup.igOffs, Is.EqualTo(11u));
            Assert.That(TotalSize(emitter), Is.EqualTo(11));
            Assert.That(firstGroup.igFlags & InsGroupFlags.UpdatedInstructionSize,
                Is.Not.EqualTo(InsGroupFlags.None));
            Assert.That(thirdGroup.igFlags & InsGroupFlags.UpdatedInstructionSize,
                Is.Not.EqualTo(InsGroupFlags.None));
            Assert.That(secondGroup.igFlags & InsGroupFlags.UpdatedInstructionSize,
                Is.EqualTo(InsGroupFlags.None));
        });
    }

    [Test]
    public static void OffsetAdjustmentIncludesGroupsWithoutJumpsBeforeTheNextListedJump()
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            emitter.emitIns_J(INS_jmp, target, isRemovableJmpCandidate: true);
            var removed = Last(emitter);
            var intermediate = emitter.emitAddInlineLabel();
            target.bbEmitCookie = intermediate;
            emitter.emitIns_Nop(3);
            var next = emitter.emitAddInlineLabel();
            emitter.emitIns_J(INS_jne, Label());
            var kept = Last(emitter);
            var end = emitter.emitAddInlineLabel();
            TotalSize(emitter) = 14;

            emitter.emitRemoveJumpToNextInst();

            Assert.That(removed.idCodeSize(), Is.Zero);
            Assert.That(JumpView.First(emitter), Is.SameAs(kept));
            Assert.That(JumpView.Last(emitter), Is.SameAs(kept));
            Assert.That(intermediate.igOffs, Is.Zero);
            Assert.That(next.igOffs, Is.EqualTo(3u));
            Assert.That(end.igOffs, Is.EqualTo(9u));
            Assert.That(TotalSize(emitter), Is.EqualTo(9));
        });
    }

    [TestCase(false, 0u, 5, 5u)]
    [TestCase(true, 1u, 6, 6u)]
    public static void CallAdjacentJumpKeepsOneByteOnlyBeforeAnEpilog(
        bool epilog, uint jumpSize, int groupSize, uint targetOffset)
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(INS_call, Label());
            var call = Last(emitter);
            emitter.emitIns_J(INS_jmp, target, isRemovableJmpCandidate: true);
            var jump = Last(emitter);
            var successor = emitter.emitAddInlineLabel();
            target.bbEmitCookie = successor;
            if (epilog)
            {
                successor.igFlags |= InsGroupFlags.Epilog;
            }
            TotalSize(emitter) = 10;

            emitter.emitRemoveJumpToNextInst();

            Assert.That(JumpView.First(emitter), Is.SameAs(call));
            Assert.That(JumpView.Last(emitter), Is.SameAs(call));
            Assert.That(JumpView.Next(call), Is.Null);
            Assert.That(jump.idCodeSize(), Is.EqualTo(jumpSize));
            Assert.That(JumpView.AfterCall(jump), Is.EqualTo(epilog));
            Assert.That(JumpView.Offset(jump), Is.EqualTo(5u));
            Assert.That(source.igSize, Is.EqualTo((ushort)groupSize));
            Assert.That(source.igData?[1], Is.SameAs(jump));
            Assert.That(successor.igOffs, Is.EqualTo(targetOffset));
            Assert.That(TotalSize(emitter), Is.EqualTo((int)targetOffset));
        });
    }

    [Test]
    public static void SingleRemovedJumpClearsBothListEndpointsAndRetainsGcState()
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(INS_jmp, target, isRemovableJmpCandidate: true);
            var jump = Last(emitter);
            var successor = emitter.emitAddInlineLabel();
            target.bbEmitCookie = successor;
            source.igGCregs = SRBM_RAX;
            successor.igGCregs = SRBM_RCX;
            source.igFlags |= InsGroupFlags.NoGCInterrupt;
            var sourceGcRegs = source.igGCregs;
            var targetGcRegs = successor.igGCregs;
            var sourceGcFlags = source.igFlags & (InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs | InsGroupFlags.NoGCInterrupt);
            var targetGcFlags = successor.igFlags & (InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs | InsGroupFlags.NoGCInterrupt);
            TotalSize(emitter) = 5;

            emitter.emitRemoveJumpToNextInst();

            Assert.That(JumpView.First(emitter), Is.Null);
            Assert.That(JumpView.Last(emitter), Is.Null);
            Assert.That(jump.idCodeSize(), Is.Zero);
            Assert.That(source.igGCregs, Is.EqualTo(sourceGcRegs));
            Assert.That(successor.igGCregs, Is.EqualTo(targetGcRegs));
            Assert.That(source.igFlags & (InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs | InsGroupFlags.NoGCInterrupt), Is.EqualTo(sourceGcFlags));
            Assert.That(successor.igFlags & (InsGroupFlags.GCVars | InsGroupFlags.ByrefRegs | InsGroupFlags.NoGCInterrupt), Is.EqualTo(targetGcFlags));
            Assert.That(TotalSize(emitter), Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void KeptCandidatesLoseTheirCandidateMarkerButRetainCallAdjacency(bool missingGroupFlag)
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(INS_call, Label());
            emitter.emitIns_J(INS_jmp, target, isRemovableJmpCandidate: true);
            var jump = Last(emitter);
            var next = emitter.emitAddInlineLabel();
            if (!missingGroupFlag)
            {
                emitter.emitIns_Nop(1);
            }
            var later = emitter.emitAddInlineLabel();
            target.bbEmitCookie = missingGroupFlag ? next : later;
            if (missingGroupFlag)
            {
                source.igFlags &= ~InsGroupFlags.HasRemovableJump;
            }
            TotalSize(emitter) = missingGroupFlag ? 10 : 11;

            emitter.emitRemoveJumpToNextInst();

            Assert.That(JumpView.Removable(jump), Is.False);
            Assert.That(JumpView.AfterCall(jump), Is.True);
            Assert.That(JumpView.Last(emitter), Is.SameAs(jump));
            Assert.That(jump.idCodeSize(), Is.EqualTo(5u));
            Assert.That(source.igSize, Is.EqualTo(10));
            Assert.That(next.igOffs, Is.EqualTo(10u));
            Assert.That(later.igOffs, Is.EqualTo(missingGroupFlag ? 10u : 11u));
            Assert.That(TotalSize(emitter), Is.EqualTo(missingGroupFlag ? 10 : 11));
        });
    }

    [Test]
    public static void AdjacentHotColdGroupsFollowTheSameRemovalRuleWithoutChangingTheBoundary()
    {
        WithEmitter((compiler, emitter) =>
        {
            var target = Label();
            target.SetFlags(BBF_COLD);
            compiler.fgFirstColdBlock = target;
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(INS_jmp, target, isRemovableJmpCandidate: true);
            var jump = Last(emitter);
            var coldGroup = emitter.emitAddInlineLabel();
            target.bbEmitCookie = coldGroup;
            emitter.emitSetFirstColdIGCookie(coldGroup);
            TotalSize(emitter) = 5;

            emitter.emitRemoveJumpToNextInst();

            Assert.That(JumpView.KeepLong(jump), Is.True);
            Assert.That(JumpView.FirstCold(emitter), Is.SameAs(coldGroup));
            Assert.That(source.igSize, Is.Zero);
            Assert.That(coldGroup.igOffs, Is.Zero);
            Assert.That(TotalSize(emitter), Is.Zero);
        });
    }

    [Test]
    public static void UnresolvedCandidateTargetIsRejectedBeforeChangingItsDescriptor()
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.emitIns_J(INS_jmp, Label(), isRemovableJmpCandidate: true);
            var jump = Last(emitter);
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            _ = emitter.emitAddInlineLabel();
            TotalSize(emitter) = 5;

            var exception = Assert.Throws<FatalJitException>(() => emitter.emitRemoveJumpToNextInst());

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(JumpView.First(emitter), Is.SameAs(jump));
            Assert.That(jump.idCodeSize(), Is.EqualTo(5u));
            Assert.That(source.igSize, Is.EqualTo(5));
            Assert.That(TotalSize(emitter), Is.EqualTo(5));
        });
    }

#if DEBUG
    [Test]
    public static void VerboseRemovalDisplaysTheJumpListBeforeAndAfterAndChecksGroupOffsets()
    {
        WithEmitter((compiler, emitter) =>
        {
            var target = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(INS_jmp, target, isRemovableJmpCandidate: true);
            var jump = Last(emitter);
            var successor = emitter.emitAddInlineLabel();
            target.bbEmitCookie = successor;
            TotalSize(emitter) = 5;
            compiler.verbose = true;

            var output = Capture(() => emitter.emitRemoveJumpToNextInst());

            Assert.That(output, Is.EqualTo(NativeNewLines(
                "*************** In emitRemoveJumpToNextInst()\n" +
                "Emitter Jump List:\n" +
                $"IG{source.GetDisplayId():D2} IN0001 jmp[5] -> IG{successor.GetDisplayId():D2} ; removal candidate\n" +
                "  total jump count: 1\n" +
                $"IG{source.GetDisplayId():D2} IN0001 is the last instruction in the group and jumps to the next " +
                $"instruction group IG{successor.GetDisplayId():D2} {emitter.emitLabelString(successor)}, removing.\n" +
                $"Adjusted offset of IG{successor.GetDisplayId():D2} from 0005 to 0000\n" +
                "Emitter Jump List:\n" +
                "  total jump count: 0\n" +
                "emitRemoveJumpToNextInst removed 5 bytes of unconditional jumps\n")));
            Assert.That(JumpView.First(emitter), Is.Null);
            Assert.That(jump.idCodeSize(), Is.Zero);
            Assert.That(source.igSize, Is.Zero);
            Assert.That(TotalSize(emitter), Is.Zero);
        });
    }

    [Test]
    public static void JumpListDiagnosticKeepsNativePushNameAndBoundJumpPolicy()
    {
        WithEmitter((_, emitter) =>
        {
            var target = Label();
            emitter.emitIns_J(INS_push_hide, target);
            var push = Last(emitter);
            emitter.emitIns_J(INS_jmp, target);
            var bound = Last(emitter);
            var successor = emitter.emitAddInlineLabel();
            target.bbEmitCookie = successor;
            JumpView.Bind(bound);

            var output = Capture(emitter.emitDispJumpList);

            Assert.That(output, Is.EqualTo(NativeNewLines(
                "Emitter Jump List:\n" +
                $"IG{push.StorageGroup?.GetDisplayId():D2} IN0001 push[5] -> IG{successor.GetDisplayId():D2}\n" +
                $"IG{bound.StorageGroup?.GetDisplayId():D2} IN0002 jmp[5]\n" +
                "  total jump count: 2\n")));
        });
    }

    [Test]
    public static void VerboseKeptCandidatePrintsTheReasonAndDoesNotPrintAnAfterList()
    {
        WithEmitter((compiler, emitter) =>
        {
            var target = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(INS_jmp, target, isRemovableJmpCandidate: true);
            var jump = Last(emitter);
            var successor = emitter.emitAddInlineLabel();
            target.bbEmitCookie = successor;
            source.igFlags &= ~InsGroupFlags.HasRemovableJump;
            TotalSize(emitter) = 5;
            compiler.verbose = true;

            var output = Capture(emitter.emitRemoveJumpToNextInst);

            Assert.That(output, Is.EqualTo(NativeNewLines(
                "*************** In emitRemoveJumpToNextInst()\n" +
                "Emitter Jump List:\n" +
                $"IG{source.GetDisplayId():D2} IN0001 jmp[5] -> IG{successor.GetDisplayId():D2} ; removal candidate\n" +
                "  total jump count: 1\n" +
                $"IG{source.GetDisplayId():D2} IN0001 containing instruction group is not marked " +
                "with IGF_HAS_REMOVABLE_JMP, keeping.\n" +
                "emitRemoveJumpToNextInst removed no unconditional jumps\n")));
            Assert.That(JumpView.Removable(jump), Is.False);
            Assert.That(jump.idCodeSize(), Is.EqualTo(5u));
            Assert.That(successor.igOffs, Is.EqualTo(5u));
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
    {
        return LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");
    }

    private abstract class JumpView(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc? First(Emitter emitter) => FirstJump(emitter);
        public static instrDesc? Last(Emitter emitter) => LastJump(emitter);
        public static instrDesc? Next(instrDesc id) => ((instrDescJmp)id).idjNext;
        public static bool Removable(instrDesc id) => ((instrDescJmp)id).idjIsRemovableJmpCandidate;
        public static bool AfterCall(instrDesc id) => ((instrDescJmp)id).idjIsAfterCallBeforeEpilog;
        public static bool KeepLong(instrDesc id) => ((instrDescJmp)id).idjKeepLong;
        public static uint Offset(instrDesc id) => ((instrDescJmp)id).idjOffs;
        public static insGroup? FirstCold(Emitter emitter) => ColdGroup(emitter);
        public static void Bind(instrDesc id) => id.idSetIsBound();

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitJumpList")]
        private static extern ref instrDescJmp? FirstJump(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitJumpLast")]
        private static extern ref instrDescJmp? LastJump(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitFirstColdIG")]
        private static extern ref insGroup? ColdGroup(Emitter emitter);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitTotalCodeSize")]
    private static extern ref int TotalSize(Emitter emitter);

#if DEBUG
    private static string NativeNewLines(string output)
    {
        return output.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif
}
