// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class EmitterLoopAlignmentRecordingTests
{
    [TestCase(16, false, 1, 15)]
    [TestCase(16, true, 1, 15)]
    [TestCase(32, false, 3, 31)]
    [TestCase(32, true, 1, 15)]
    [TestCase(64, false, 5, 63)]
    [TestCase(64, true, 1, 15)]
    [TestCase(128, false, 9, 127)]
    [TestCase(256, false, 18, 255)]
    public static void RecordingPreservesAdaptiveAndFixedPadding(
        int boundary, bool adaptive, int count, int padding)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compJitAlignLoopBoundary = (ushort)boundary;
            compiler.opts.compJitAlignLoopAdaptive = adaptive;
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing current group.");
            Assert.That(emitter.emitEndsWithAlignInstr(), Is.False);
            Align(emitter, true);

            Assert.That(emitter.emitCurIG, Is.SameAs(group));
            Assert.That(emitter.emitEndsWithAlignInstr(), Is.True);
            Assert.That(Count(emitter), Is.EqualTo(count));
            Assert.That(CodeSize(emitter), Is.EqualTo(padding));
            var descriptors = Instructions(emitter);
            var total = 0u;
            for (var i = 0; i < count; i++)
            {
                var id = descriptors[i];
                Assert.That(id.idIns(), Is.EqualTo(INS_align));
                Assert.That(id.idCodeSize(), Is.EqualTo((uint)Math.Min(15, padding - (15 * i))));
                Assert.That(Access.Group(id), Is.SameAs(group));
                Assert.That(Access.Predecessor(id), Is.SameAs(i == 0 ? group : null));
                Assert.That(Access.Next(id), Is.SameAs(i == 0 ? null : descriptors[i - 1]));
#if DEBUG
                Assert.That(Access.AfterJump(id), Is.True);
                Assert.That(id.NativeLogicalSize, Is.EqualTo(48));
#else
                Assert.That(id.NativeLogicalSize, Is.EqualTo(40));
#endif
                total += id.idCodeSize();
            }
            Assert.That(total, Is.EqualTo((uint)padding));
            Assert.That(Access.LastGroup(emitter), Is.SameAs(descriptors[0]));
            Assert.That(Access.Pending(emitter), Is.SameAs(descriptors[count - 1]));
            Assert.That(Used(emitter), Is.EqualTo((nuint)(count * FullAlignSize)));
        });
    }

    [TestCase(-1, true)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    public static void CapacityCheckUsesLogicalDescriptorsAndDebugPrefixes(int extraBytes, bool movesGroup)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compJitAlignLoopBoundary = 32;
            compiler.opts.compJitAlignLoopAdaptive = false;
            emitter.emitIns_Nop(1);
            var original = emitter.emitCurIG ?? throw new AssertionException("Missing current group.");
            Capacity(emitter) = Used(emitter) + (nuint)((3 * FullAlignSize) + extraBytes);
            Align(emitter);

            Assert.That(emitter.emitCurIG == original, Is.EqualTo(!movesGroup));
            Assert.That(original.endsWithAlignInstr(), Is.EqualTo(!movesGroup));
            Assert.That(emitter.emitEndsWithAlignInstr(), Is.True);
            Assert.That(Count(emitter), Is.EqualTo(movesGroup ? 3 : 4));
            var first = Access.LastGroup(emitter) ?? throw new AssertionException("Missing alignment group.");
            Assert.That(Access.Group(first), Is.SameAs(emitter.emitCurIG));
            for (var id = Access.Pending(emitter); id is not null; id = Access.Next(id))
            {
                Assert.That(Access.Group(id), Is.SameAs(emitter.emitCurIG));
            }
        });
    }

    [Test]
    public static void SavingAndConnectingPreserveDescriptorIdentityAndLoopHeader()
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compJitAlignLoopBoundary = 32;
            Align(emitter);
            var alignGroup = emitter.emitCurIG ?? throw new AssertionException("Missing current group.");
            var first = Instructions(emitter)[0];
            NextGroup(emitter, false);

            Assert.That(Access.Saved(emitter), Is.SameAs(first));
            Assert.That(Access.Pending(emitter), Is.Null);
            var saved = alignGroup.igData ?? throw new AssertionException("Missing saved descriptors.");
            Assert.That(saved[0], Is.SameAs(first));
            var intervening = emitter.emitCurIG ?? throw new AssertionException("Missing intervening group.");
            emitter.emitIns_Nop(1);
            emitter.emitConnectAlignInstrWithCurIG();

            Assert.That(Access.Predecessor(first), Is.SameAs(intervening));
            Assert.That(Access.Header(first), Is.SameAs(emitter.emitCurIG));
            Assert.That(Access.Group(first), Is.SameAs(alignGroup));
            Assert.That(emitter.emitEndsWithAlignInstr(), Is.False);
            Assert.That(intervening.igSize, Is.EqualTo(1));
            Assert.That(Access.Next(first), Is.SameAs(saved[1]));
            Assert.That(Access.Next(saved[1]), Is.SameAs(saved[2]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ForwardTargetsDoNotUpdateLoopHistory(bool hasCookie)
    {
        WithEmitter((_, emitter) =>
        {
            var groups = Groups(4);
            emitter.emitCurIG = groups[1];
            var top = LoopTop(hasCookie ? groups[3] : null);
            Assert.That(emitter.emitSetLoopBackEdge(top), Is.False);
            Assert.That(groups[1].igLoopBackEdge, Is.Null);
            Assert.That(LastLoopStart(emitter), Is.Null);
            Assert.That(LastLoopEnd(emitter), Is.Null);
        });
    }

    [TestCase(1, 3, 4, 6)]
    [TestCase(1, 1, 3, 3)]
    public static void DisjointAndSingleGroupLoopsSetBackEdges(int firstStart, int firstEnd, int nextStart, int nextEnd)
    {
        WithEmitter((_, emitter) =>
        {
            var groups = Groups(8);
            emitter.emitCurIG = groups[firstEnd];
            Assert.That(emitter.emitSetLoopBackEdge(LoopTop(groups[firstStart])), Is.True);
            Assert.That(groups[firstEnd].igLoopBackEdge, Is.SameAs(groups[firstStart]));
            emitter.emitCurIG = groups[nextEnd];
            Assert.That(emitter.emitSetLoopBackEdge(LoopTop(groups[nextStart])), Is.True);
            Assert.That(groups[nextEnd].igLoopBackEdge, Is.SameAs(groups[nextStart]));
            Assert.That(LastLoopStart(emitter), Is.SameAs(groups[nextStart]));
            Assert.That(LastLoopEnd(emitter), Is.SameAs(groups[nextEnd]));
        });
    }

    [TestCase(1, 3, 1, 5, false, false)]
    [TestCase(2, 4, 1, 6, true, false)]
    [TestCase(1, 6, 2, 4, false, true)]
    [TestCase(1, 4, 3, 6, true, true)]
    [TestCase(3, 6, 1, 4, true, true)]
    [TestCase(1, 3, 3, 5, true, true)]
    public static void OverlappingLoopsRemoveOnlyNativeSelectedAlignments(
        int lastStart, int lastEnd, int currentStart, int currentEnd, bool removeCurrent, bool removeLast)
    {
        WithEmitter((_, emitter) =>
        {
            var groups = Groups(8);
            var lastAlign = Access.Install(emitter, groups[lastStart - 1], groups[lastStart], 3);
            var currentAlign = lastStart == currentStart ? lastAlign
                : Access.Install(emitter, groups[currentStart - 1], groups[currentStart], 3);
            LastLoopStart(emitter) = groups[lastStart];
            LastLoopEnd(emitter) = groups[lastEnd];
            groups[lastEnd].igLoopBackEdge = groups[lastStart];
            emitter.emitCurIG = groups[currentEnd];

            Assert.That(emitter.emitSetLoopBackEdge(LoopTop(groups[currentStart])), Is.False);
            var currentGroup = Access.Group(currentAlign) ?? throw new AssertionException("Missing alignment group.");
            var lastGroup = Access.Group(lastAlign) ?? throw new AssertionException("Missing alignment group.");
            Assert.That(currentGroup.hadAlignInstr(), Is.EqualTo(removeCurrent));
            Assert.That(lastGroup.hadAlignInstr(), Is.EqualTo(removeLast));
            Assert.That(currentGroup.endsWithAlignInstr(), Is.EqualTo(!removeCurrent));
            Assert.That(lastGroup.endsWithAlignInstr(), Is.EqualTo(!removeLast));
            Assert.That(groups[lastEnd].igLoopBackEdge, Is.SameAs(groups[lastStart]));
            Assert.That(LastLoopStart(emitter), Is.SameAs(groups[lastStart]));
            Assert.That(LastLoopEnd(emitter), Is.SameAs(groups[lastEnd]));
        });
    }

    [TestCase(InsGroupFlags.Prolog)]
    [TestCase(InsGroupFlags.Epilog)]
    [TestCase(InsGroupFlags.FuncletProlog)]
    [TestCase(InsGroupFlags.FuncletEpilog)]
    public static void LayoutOrderingIncludesLateAllocatedExtensions(InsGroupFlags region)
    {
        var groups = Groups(5);
        groups[0].igFlags = region | InsGroupFlags.OutOfOrderHead;
        groups[1].igFlags = region;
        groups[2].igFlags = region;
        groups[1].InitializeNum(50);
        groups[2].InitializeNum(51);
        for (var i = 0; i < groups.Length; i++)
        {
            for (var j = 0; j < groups.Length; j++)
            {
                Assert.That(groups[i].IsBefore(groups[j]), Is.EqualTo(i < j));
                Assert.That(groups[i].IsAfter(groups[j]), Is.EqualTo(i > j));
            }
        }
    }

    [Test]
    public static void LayoutOrderingHandlesConsecutiveAndTerminalOutOfOrderRegions()
    {
        var groups = Groups(5);
        groups[0].igFlags = InsGroupFlags.Prolog | InsGroupFlags.OutOfOrderHead;
        groups[1].igFlags = InsGroupFlags.Prolog;
        groups[2].igFlags = InsGroupFlags.FuncletProlog | InsGroupFlags.OutOfOrderHead;
        groups[3].igFlags = InsGroupFlags.FuncletProlog;
        groups[4].igFlags = InsGroupFlags.FuncletProlog;
        groups[1].InitializeNum(50);
        groups[3].InitializeNum(51);
        groups[4].InitializeNum(52);
        for (var i = 0; i < groups.Length; i++)
        {
            for (var j = 0; j < groups.Length; j++)
            {
                Assert.That(groups[i].IsBefore(groups[j]), Is.EqualTo(i < j));
            }
        }
    }

#if DEBUG
    [Test]
    public static void UnsupportedDisassemblyFailsBeforeAlignmentFlagsOrAllocation()
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.dspCode = true;
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing current group.");
            var flags = group.igFlags;
            var used = Used(emitter);
            var exception = Assert.Throws<FatalJitException>(() => emitter.emitLoopAlignment(false));
            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(group.igFlags, Is.EqualTo(flags));
            Assert.That(Used(emitter), Is.EqualTo(used));
            Assert.That(Count(emitter), Is.Zero);
            Assert.That(Access.Pending(emitter), Is.Null);
        });
    }
#endif

    private static int FullAlignSize =>
#if DEBUG
        48 + IntPtr.Size;
#else
        40;
#endif

    private static void Align(Emitter emitter, bool afterJump = false)
    {
        emitter.emitLoopAlignment(
#if DEBUG
            afterJump
#endif
            );
    }

    private static BasicBlock LoopTop(insGroup? group)
    {
        var block = new BasicBlock(null, null) { bbEmitCookie = group };
        block.SetFlags(BBF_LOOP_ALIGN);
        return block;
    }

    private static insGroup[] Groups(int count)
    {
        var groups = new insGroup[count];
        for (var i = 0; i < count; i++)
        {
            groups[i] = new insGroup();
            groups[i].InitializeNum((uint)(i + 1));
            if (i != 0)
            {
                groups[i - 1].igNext = groups[i];
                groups[i].igPrev = groups[i - 1];
            }
        }
        return groups;
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX,
            (compiler, codeGen, _) => action(compiler, codeGen.Emitter));
    }

    private static List<Emitter.instrDesc> Instructions(Emitter emitter) =>
        Buffer(emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Exposes protected descriptor types for tests.")]
    private sealed class Access : Emitter
    {
        private Access(CodeGen codeGen) : base(codeGen)
        {
        }

        public static instrDesc? Pending(Emitter emitter) => PendingList(emitter);
        public static instrDesc? Saved(Emitter emitter) => SavedList(emitter);
        public static instrDesc? LastGroup(Emitter emitter) => LastGroupField(emitter);
        public static instrDesc? Next(instrDesc id) => ((instrDescAlign)id).idaNext;
        public static insGroup? Group(instrDesc id) => ((instrDescAlign)id).idaIG;
        public static insGroup? Predecessor(instrDesc id) => ((instrDescAlign)id).idaLoopHeadPredIG;
        public static insGroup? Header(instrDesc id) => ((instrDescAlign)id).loopHeadIG();
#if DEBUG
        public static bool AfterJump(instrDesc id) => ((instrDescAlign)id).isPlacedAfterJmp;
#endif

        public static instrDesc Install(Emitter emitter, insGroup containing, insGroup header, int count)
        {
            containing.igFlags |= InsGroupFlags.HasAlign;
            var first = new instrDescAlign { idaIG = containing, idaLoopHeadPredIG = header.igPrev };
            first.idIns(INS_align);
            var last = first;
            for (var i = 1; i < count; i++)
            {
                var next = new instrDescAlign { idaIG = containing };
                next.idIns(INS_align);
                last.idaNext = next;
                last = next;
            }
            last.idaNext = SavedList(emitter);
            SavedList(emitter) = first;

            return first;
        }

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGAlignList")]
        private static extern ref instrDescAlign? PendingList(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitAlignList")]
        private static extern ref instrDescAlign? SavedList(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitAlignLastGroup")]
        private static extern ref instrDescAlign? LastGroupField(Emitter emitter);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNxtIG")]
    private static extern void NextGroup(Emitter emitter, bool extend);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? Buffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeEndp")]
    private static extern ref nuint Capacity(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int Count(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CodeSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastLoopStart")]
    private static extern ref insGroup? LastLoopStart(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastLoopEnd")]
    private static extern ref insGroup? LastLoopEnd(Emitter emitter);
}
