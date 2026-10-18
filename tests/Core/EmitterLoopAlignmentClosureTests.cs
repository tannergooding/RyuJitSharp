// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterLoopAlignmentClosureTests
{
    [Test]
    public static void EmptyAlignmentListIsTheNativeEarlyReturn()
    {
        Assume.That(!Configuration.TARGET_AMD64);

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new View(compiler);
        View.Total(emitter) = 37;

        emitter.emitLoopAlignAdjustments();

        Assert.That(View.Total(emitter), Is.EqualTo(37));
    }

    [TestCase(0u, 0u)]
    [TestCase(28u, 4u)]
    [TestCase(uint.MaxValue - 3, 4u)]
    public static void FixedPaddingUsesTheNativeSizedOffset(uint lowOffset, uint expected)
    {
        Assume.That(!Configuration.TARGET_AMD64);

        using var tls = TestConfiguration.CreateJitTls();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        var previousCompiler = JitTls.Compiler;
        compiler.opts.compJitAlignLoopBoundary = 32;
        compiler.opts.compJitAlignLoopMaxCodeSize = 128;
        try
        {
            JitTls.Compiler = compiler;
            var emitter = new View(compiler);
            var header = new insGroup { igSize = 32 };
            header.igLoopBackEdge = header;
            var offset = (nuint)lowOffset;
            if (nuint.Size > sizeof(uint))
            {
                offset |= (nuint)1 << 32;
            }

            Assert.That(emitter.Padding(header, offset), Is.EqualTo(expected));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [Test]
    public static void ArmAlignmentDescriptorCarriesTheNativeFormatAndPaddingOption()
    {
        Assume.That(Configuration.TARGET_ARM64);

        using var tls = TestConfiguration.CreateJitTls();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        var previousCompiler = JitTls.Compiler;
        try
        {
            JitTls.Compiler = compiler;
            var emitter = new View(compiler);
            var id = emitter.NewAlignment();

            Assert.That(id.idIns(), Is.EqualTo(instruction.INS_align));
            Assert.That(id.idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_ARM64_SN_0A));
            Assert.That(id.idInsOpt(), Is.EqualTo(insOpts.INS_OPTS_ALIGN));
            Assert.That(id.idCodeSize(), Is.EqualTo(4u));
            id.idInsOpt(insOpts.INS_OPTS_NONE);
            Assert.That(id.idCodeSize(), Is.Zero);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [TestCase(4u, (ushort)28, 0u)]
    [TestCase(16u, (ushort)28, 16u)]
    [TestCase(20u, (ushort)28, 12u)]
    [TestCase(24u, (ushort)64, 8u)]
    [TestCase(28u, (ushort)128, 0u)]
    public static void ArmAdaptivePaddingKeepsTheThirtyTwoByteBoundary(
        uint offset, ushort loopSize, uint expected)
    {
        Assume.That(Configuration.TARGET_ARM64);

        using var tls = TestConfiguration.CreateJitTls();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        var previousCompiler = JitTls.Compiler;
        compiler.opts.compJitAlignLoopAdaptive = true;
        compiler.opts.compJitAlignLoopBoundary = 32;
        try
        {
            JitTls.Compiler = compiler;
            var emitter = new View(compiler);
            var header = new insGroup { igSize = loopSize };
            header.igLoopBackEdge = header;

            Assert.That(emitter.Padding(header, offset), Is.EqualTo(expected));
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    [ThreadStatic]
    private static MemoryStream? s_assertionOutput;

    [Test]
    public static void MismatchContextIsBufferedUntilItsDescriptorAssertionHasRun()
    {
        Assume.That(TestConfiguration.IsDebug && Configuration.TARGET_AMD64);

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        var context = new AssertionContext
        {
            JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            FirstPosition = -1,
            LastPosition = -1,
        };
        using var tls = new JitTls(&context.JitInfo);
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;
        compiler.opts.compJitAlignPaddingLimit = 15;
        var emitter = new View(compiler);
        var header = new insGroup { igSize = 4 };
        var group = new insGroup { igSize = 15, igFlags = InsGroupFlags.HasAlign };
        var otherHeader = new insGroup();
        var predecessor = new insGroup { igNext = header };
        header.InitializeNum(1);
        group.InitializeNum(2);
        otherHeader.InitializeNum(3);
        predecessor.InitializeNum(4);
        header.igNext = group;
        group.igLoopBackEdge = otherHeader;
        View.InstallMismatchedLastAlign(group, otherHeader, predecessor);

        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previousWriter = s_jitstdout;
        var previousOutput = s_assertionOutput;
        uint size;
        try
        {
            s_jitstdout = writer;
            s_assertionOutput = stream;
            size = emitter.LoopSize(header, group, predecessor);
            writer.Flush();
        }
        finally
        {
            s_assertionOutput = previousOutput;
            s_jitstdout = previousWriter;
        }

        var expected = "\n\nMismatch in align instruction.\n" +
            "Containing IG: IG02\nloopHeadPredIG: IG04\nloopHeadIG: IG01\n" +
            "igInLoop: IG02\nigInLoop->igLoopBackEdge: IG03\n" +
            "igInLoop has align instruction for : IG01\nLoop:\n\tIG01\n\tIG02\n" +
            "Did not find IG with back edge to IG01\n";
        Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
            Is.EqualTo(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
        Assert.That(context.Assertions, Is.EqualTo(2));
        Assert.That(context.FirstPosition, Is.Zero);
        Assert.That(context.LastPosition, Is.EqualTo(stream.Length));
        Assert.That(size, Is.EqualTo(4u));
    }

    private struct AssertionContext
    {
        public ICorJitInfo JitInfo;
        public int Assertions;
        public long FirstPosition;
        public long LastPosition;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        var context = (AssertionContext*)self;
        context->Assertions++;
        if (s_assertionOutput is MemoryStream stream)
        {
            if (context->Assertions == 1)
            {
                context->FirstPosition = stream.Position;
            }
            context->LastPosition = stream.Position;
        }
        return 0;
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Used by target-specific alignment tests.")]
    private sealed class View : Emitter
    {
        public View(Compiler compiler) : base(new CodeGen(compiler))
        {
            _compiler = compiler;
        }

        public uint Padding(insGroup header, nuint offset)
        {
            return Calculate(this, header, offset, false, header, header);
        }

        public instrDesc NewAlignment()
        {
            emitCurIG = new insGroup();
            Buffer(this) = [];
            BufferEnd(this) = 65536;
            return AllocateAlignment(this);
        }

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
        private static extern ref List<instrDesc>? Buffer(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeEndp")]
        private static extern ref nuint BufferEnd(Emitter emitter);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAlign")]
        private static extern instrDescAlign AllocateAlignment(Emitter emitter);

        public uint LoopSize(insGroup header, insGroup containing, insGroup predecessor)
        {
            return CalculateLoopSize(this, header, 128, false, containing, predecessor);
        }

        public static void InstallMismatchedLastAlign(insGroup group, insGroup wrongGroup, insGroup predecessor)
        {
            group.igLastIns = new instrDescAlign { idaIG = wrongGroup, idaLoopHeadPredIG = predecessor };
        }
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getLoopSize")]
        private static extern uint CalculateLoopSize(Emitter emitter, insGroup header, uint maximum,
            bool adjusted, insGroup containing, insGroup predecessor);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitCalculatePaddingForLoopAlignment")]
        private static extern uint Calculate(Emitter emitter, insGroup header, nuint offset,
            bool adjusted, insGroup containing, insGroup predecessor);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitTotalCodeSize")]
        public static extern ref int Total(Emitter emitter);
    }
}
