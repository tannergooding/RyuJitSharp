// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_LOOP_ALIGN && TARGET_AMD64
using System;
#if DEBUG
using System.IO;
using System.Text;
#endif
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
#if DEBUG
using static RyuJitSharp.Globals;
#endif

namespace RyuJitSharp.UnitTests;

internal static class EmitterLoopAlignmentAdjustmentTests
{
    [Test]
    public static void NoAlignmentsLeaveCodeSizeAndGroupsUnchanged()
    {
        WithEmitter((_, emitter) =>
        {
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing group.");
            var flags = group.igFlags;
            TotalSize(emitter) = 37;

            emitter.emitLoopAlignAdjustments();

            Assert.That(TotalSize(emitter), Is.EqualTo(37));
            Assert.That(group.igFlags, Is.EqualTo(flags));
            Assert.That(LastAligned(emitter), Is.Null);
        });
    }

    [TestCase(true, 32, 15, 5, 28, 11, 16, 44)]
    [TestCase(false, 32, 31, 4, 30, 28, 32, 62)]
    [TestCase(true, 32, 15, 0, 28, 0, 0, 28)]
    [TestCase(true, 32, 15, 5, 10, 0, 5, 15)]
    [TestCase(false, 16, 15, 1, 16, 15, 16, 32)]
    [TestCase(false, 64, 63, 3, 63, 61, 64, 127)]
    public static void AdjustmentKeepsDescriptorWidthsAndGroupState(
        bool adaptive, int boundary, int estimated, int lead, int loopSize,
        int actual, int containingSize, int totalSize)
    {
        WithEmitter((compiler, emitter) =>
        {
            Configure(compiler, adaptive, boundary);
            EmitNops(emitter, lead);
            var containing = emitter.emitCurIG ?? throw new AssertionException("Missing containing group.");
            Align(emitter);
            var header = emitter.emitAddInlineLabel();
            var firstAlign = Access.First(emitter) ?? throw new AssertionException("Missing alignment descriptor.");
            EmitNops(emitter, loopSize);
            header.igLoopBackEdge = header;
            var end = emitter.emitAddInlineLabel();
            TotalSize(emitter) = lead + estimated + loopSize;
            var headerFlags = header.igFlags;
            var backEdge = header.igLoopBackEdge;

            emitter.emitLoopAlignAdjustments();

            Assert.That(containing.igSize, Is.EqualTo((ushort)containingSize));
            Assert.That(header.igOffs, Is.EqualTo((uint)containingSize));
            Assert.That(header.igSize, Is.EqualTo((ushort)loopSize));
            Assert.That(end.igOffs, Is.EqualTo((uint)totalSize));
            Assert.That(TotalSize(emitter), Is.EqualTo(totalSize));
            Assert.That(header.igFlags, Is.EqualTo(headerFlags));
            Assert.That(header.igLoopBackEdge, Is.SameAs(backEdge));
            Assert.That(Access.First(emitter), Is.SameAs(firstAlign));
            Assert.That(Access.Header(firstAlign), Is.SameAs(header));
            Assert.That(LastAligned(emitter), Is.SameAs(actual == 0 ? null : containing));

            if (actual == estimated)
            {
                Assert.That(containing.igFlags & InsGroupFlags.UpdatedInstructionSize,
                    Is.EqualTo(InsGroupFlags.None));
            }
            else
            {
                Assert.That(containing.igFlags & InsGroupFlags.UpdatedInstructionSize,
                    Is.EqualTo(InsGroupFlags.UpdatedInstructionSize));
            }
            Assert.That(containing.endsWithAlignInstr(), Is.EqualTo(actual != 0));
            Assert.That(containing.hadAlignInstr(), Is.EqualTo(actual == 0));

            var remaining = (uint)actual;
            for (var align = firstAlign;
                (align is not null) && ReferenceEquals(Access.Group(align), containing);
                align = Access.Next(align))
            {
                var expected = adaptive ? remaining : Math.Min(remaining, 15u);
                Assert.That(align.idCodeSize(), Is.EqualTo(expected));
                remaining -= expected;
            }
            Assert.That(remaining, Is.Zero);
        });
    }

    [Test]
    public static void AlreadyRejectedAlignmentClearsPaddingWithoutLosingTheBackedge()
    {
        WithEmitter((compiler, emitter) =>
        {
            Configure(compiler, adaptive: false, boundary: 32);
            emitter.emitIns_Nop(4);
            var containing = emitter.emitCurIG ?? throw new AssertionException("Missing containing group.");
            Align(emitter);
            var header = emitter.emitAddInlineLabel();
            var align = Access.First(emitter) ?? throw new AssertionException("Missing alignment descriptor.");
            emitter.emitIns_Nop(14);
            header.igLoopBackEdge = header;
            var end = emitter.emitAddInlineLabel();
            TotalSize(emitter) = 49;
            Access.RemoveFlags(align);

            emitter.emitLoopAlignAdjustments();

            Assert.That(containing.igSize, Is.EqualTo((ushort)4));
            Assert.That(containing.hadAlignInstr(), Is.True);
            Assert.That(containing.endsWithAlignInstr(), Is.False);
            Assert.That(header.igOffs, Is.EqualTo(4u));
            Assert.That(end.igOffs, Is.EqualTo(18u));
            Assert.That(header.igLoopBackEdge, Is.SameAs(header));
            Assert.That(LastAligned(emitter), Is.Null);
            Assert.That(TotalSize(emitter), Is.EqualTo(18));
            for (var current = align;
                (current is not null) && ReferenceEquals(Access.Group(current), containing);
                current = Access.Next(current))
            {
                Assert.That(current.idCodeSize(), Is.Zero);
            }
        });
    }

    [Test]
    public static void LoopExceedingTheConfiguredSizeRemovesAllReservedPadding()
    {
        WithEmitter((compiler, emitter) =>
        {
            Configure(compiler, adaptive: false, boundary: 32);
            compiler.opts.compJitAlignLoopMaxCodeSize = 20;
            emitter.emitIns_Nop(1);
            var containing = emitter.emitCurIG ?? throw new AssertionException("Missing containing group.");
            Align(emitter);
            var header = emitter.emitAddInlineLabel();
            EmitNops(emitter, 28);
            header.igLoopBackEdge = header;
            var end = emitter.emitAddInlineLabel();
            TotalSize(emitter) = 60;

            emitter.emitLoopAlignAdjustments();

            Assert.That(containing.igSize, Is.EqualTo((ushort)1));
            Assert.That(containing.hadAlignInstr(), Is.True);
            Assert.That(header.igOffs, Is.EqualTo(1u));
            Assert.That(end.igOffs, Is.EqualTo(29u));
            Assert.That(TotalSize(emitter), Is.EqualTo(29));
            Assert.That(LastAligned(emitter), Is.Null);
        });
    }

    [Test]
    public static void HotColdBoundaryKeepsItsGroupIdentityAndAdjustedOffset()
    {
        WithEmitter((compiler, emitter) =>
        {
            Configure(compiler, adaptive: true, boundary: 32);
            emitter.emitIns_Nop(5);
            Align(emitter);
            var coldHeader = emitter.emitAddInlineLabel();
            emitter.emitSetFirstColdIGCookie(coldHeader);
            EmitNops(emitter, 28);
            coldHeader.igLoopBackEdge = coldHeader;
            var end = emitter.emitAddInlineLabel();
            TotalSize(emitter) = 48;

            emitter.emitLoopAlignAdjustments();

            Assert.That(FirstCold(emitter), Is.SameAs(coldHeader));
            Assert.That(coldHeader.igOffs, Is.EqualTo(16u));
            Assert.That(end.igOffs, Is.EqualTo(44u));
            Assert.That(coldHeader.igLoopBackEdge, Is.SameAs(coldHeader));
        });
    }

    [Test]
    public static void MultipleAlignmentsPropagateCumulativeChangesThroughIntermediateGroups()
    {
        WithEmitter((compiler, emitter) =>
        {
            Configure(compiler, adaptive: true, boundary: 32);
            emitter.emitIns_Nop(5);
            var firstContaining = emitter.emitCurIG ?? throw new AssertionException("Missing first group.");
            Align(emitter);
            var firstHeader = emitter.emitAddInlineLabel();
            var firstAlign = Access.First(emitter) ?? throw new AssertionException("Missing first alignment.");
            EmitNops(emitter, 28);
            firstHeader.igLoopBackEdge = firstHeader;

            var secondContaining = emitter.emitAddInlineLabel();
            emitter.emitIns_Nop(8);
            Align(emitter);
            var secondHeader = emitter.emitAddInlineLabel();
            var secondAlign = Access.Next(firstAlign) ?? throw new AssertionException("Missing second alignment.");
            EmitNops(emitter, 28);
            secondHeader.igLoopBackEdge = secondHeader;
            var end = emitter.emitAddInlineLabel();
            TotalSize(emitter) = 99;

            emitter.emitLoopAlignAdjustments();

            Assert.That(firstAlign.idCodeSize(), Is.EqualTo(11u));
            Assert.That(secondAlign.idCodeSize(), Is.EqualTo(12u));
            Assert.That(firstContaining.igSize, Is.EqualTo((ushort)16));
            Assert.That(firstHeader.igOffs, Is.EqualTo(16u));
            Assert.That(secondContaining.igOffs, Is.EqualTo(44u));
            Assert.That(secondContaining.igSize, Is.EqualTo((ushort)20));
            Assert.That(secondHeader.igOffs, Is.EqualTo(64u));
            Assert.That(end.igOffs, Is.EqualTo(92u));
            Assert.That(TotalSize(emitter), Is.EqualTo(92));
            Assert.That(firstHeader.igLoopBackEdge, Is.SameAs(firstHeader));
            Assert.That(secondHeader.igLoopBackEdge, Is.SameAs(secondHeader));
            Assert.That(LastAligned(emitter), Is.SameAs(secondContaining));
        });
    }

#if DEBUG
    [TestCase(false, 0, 3, 32)]
    [TestCase(true, 29, 32, 61)]
    public static void JccErratumUsesTheNativeDebugOnlyOffsetAdjustment(
        bool forJcc, int actual, int headerOffset, int finalSize)
    {
        WithEmitter((compiler, emitter) =>
        {
            Configure(compiler, adaptive: false, boundary: 32);
            compiler.opts.compJitAlignLoopForJcc = forJcc;
            emitter.emitIns_Nop(3);
            var containing = emitter.emitCurIG ?? throw new AssertionException("Missing containing group.");
            Align(emitter);
            var header = emitter.emitAddInlineLabel();
            EmitNops(emitter, 29);
            header.igLoopBackEdge = header;
            var end = emitter.emitAddInlineLabel();
            TotalSize(emitter) = 63;

            emitter.emitLoopAlignAdjustments();

            Assert.That(containing.igSize, Is.EqualTo((ushort)headerOffset));
            Assert.That(header.igOffs, Is.EqualTo((uint)headerOffset));
            Assert.That(end.igOffs, Is.EqualTo((uint)finalSize));
            var padding = 0u;
            var first = Access.First(emitter) ?? throw new AssertionException("Missing alignment descriptor.");
            for (var align = first; (align is not null) && ReferenceEquals(Access.Group(align), containing);
                align = Access.Next(align))
            {
                padding += align.idCodeSize();
            }
            Assert.That(padding, Is.EqualTo((uint)actual));
            Assert.That(TotalSize(emitter), Is.EqualTo(finalSize));
        });
    }

    [Test]
    public static void VerboseAdjustmentReportsNativePaddingAndGroupOffsets()
    {
        WithEmitter((compiler, emitter) =>
        {
            Configure(compiler, adaptive: true, boundary: 32);
            emitter.emitIns_Nop(5);
            var containing = emitter.emitCurIG ?? throw new AssertionException("Missing containing group.");
            Align(emitter);
            var header = emitter.emitAddInlineLabel();
            EmitNops(emitter, 28);
            header.igLoopBackEdge = header;
            var end = emitter.emitAddInlineLabel();
            TotalSize(emitter) = 48;
            compiler.verbose = true;

            var output = Capture(emitter.emitLoopAlignAdjustments);

            Assert.That(output, Does.Contain("*************** In emitLoopAlignAdjustments()" + Environment.NewLine));
            Assert.That(output, Does.Contain("compJitAlignLoopAdaptive       = true" + Environment.NewLine));
            Assert.That(output, Does.Contain("compJitAlignLoopBoundary       = 32" + Environment.NewLine));
            Assert.That(output, Does.Contain($"  Adjusting 'align' instruction in IG{containing.GetDisplayId():D2} " +
                $"that is targeted for IG{header.GetDisplayId():D2} " + Environment.NewLine));
            Assert.That(output, Does.Contain($"loopSize of {emitter.emitLabelString(header)} = 28 bytes." +
                Environment.NewLine));
            Assert.That(output, Does.Contain($"Adjusted alignment for {emitter.emitLabelString(header)} " +
                "from 15 to 11." + Environment.NewLine));
            Assert.That(output, Does.Contain($"Adjusted size of {emitter.emitLabelString(containing)} " +
                "from 20 to 16." + Environment.NewLine));
            Assert.That(output, Does.Contain($"Recording last aligned IG: {emitter.emitLabelString(containing)}" +
                Environment.NewLine));
            Assert.That(output, Does.Contain($"Adjusted offset of {emitter.emitLabelString(end)} " +
                "from 0030 to 002C" + Environment.NewLine));
        });
    }
#endif

    private static void Configure(Compiler compiler, bool adaptive, int boundary)
    {
        compiler.opts.compJitAlignLoopAdaptive = adaptive;
        compiler.opts.compJitAlignLoopBoundary = (ushort)boundary;
        compiler.opts.compJitAlignPaddingLimit = (ushort)(adaptive ? 15 : boundary - 1);
        compiler.opts.compJitAlignLoopMaxCodeSize = 128;
    }

    private static void EmitNops(Emitter emitter, int count)
    {
        while (count > 0)
        {
            var size = Math.Min(count, 15);
            emitter.emitIns_Nop((uint)size);
            count -= size;
        }
    }

    private static void Align(Emitter emitter)
    {
        emitter.emitLoopAlignment(
#if DEBUG
            false
#endif
            );
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX,
            (compiler, codeGen, _) => action(compiler, codeGen.Emitter));
    }

    private abstract class Access(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc? First(Emitter emitter) => AlignList(emitter);
        public static instrDesc? Next(instrDesc descriptor) => ((instrDescAlign)descriptor).idaNext;
        public static insGroup? Group(instrDesc descriptor) => ((instrDescAlign)descriptor).idaIG;
        public static insGroup? Header(instrDesc descriptor) => ((instrDescAlign)descriptor).loopHeadIG();
        public static void RemoveFlags(instrDesc descriptor) => ((instrDescAlign)descriptor).removeAlignFlags();

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitAlignList")]
        private static extern ref instrDescAlign? AlignList(Emitter emitter);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitTotalCodeSize")]
    private static extern ref int TotalSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastAlignedIg")]
    private static extern ref insGroup? LastAligned(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitFirstColdIG")]
    private static extern ref insGroup? FirstCold(Emitter emitter);

#if DEBUG
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
#endif
