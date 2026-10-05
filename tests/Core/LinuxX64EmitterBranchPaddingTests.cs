// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_AMD64 && UNIX_AMD64_ABI
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinuxX64EmitterBranchPaddingTests
{
    [Test]
    public static void LocalBackwardJumpEncodesItsNegativeDisplacement()
    {
        WithEmitter((_, emitter) =>
        {
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            var descriptor = Descriptor.Jump(group);
            var buffer = stackalloc byte[16];
            new Span<byte>(buffer, 16).Fill(0xA5);
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 16;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputLJ(group, buffer, descriptor);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString("EBFE")));
            Assert.That(buffer[2], Is.EqualTo(0xA5));
            Assert.That(Descriptor.IsShort(descriptor), Is.True);
        });
    }

    [Test]
    public static void AlignedPaddingUsesRecordedSize()
    {
        WithEmitter((compiler, emitter) =>
        {
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            group.igFlags |= InsGroupFlags.HasAlign;
            var codeGen = compiler.codeGen ?? throw new AssertionException("Missing code generator.");
            codeGen.ShouldAlignLoops = true;
            compiler.opts.compJitAlignLoopBoundary = 16;
            var descriptor = Descriptor.Align(group, 7);
            var buffer = stackalloc byte[16];
            new Span<byte>(buffer, 16).Fill(0xA5);
            var count = compiler.Metrics.LoopsAligned;

            var end = emitter.emitOutputAlign(group, descriptor, buffer);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString("0F1F8000000000")));
            Assert.That(compiler.Metrics.LoopsAligned, Is.EqualTo(count + 1));
        });
    }

    private static void WithEmitter(Action<Compiler, Emitter> test)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            var emitter = codeGen.Emitter;
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            emitter.emitBegFN(true
#if DEBUG
                , false
#endif
                );
            test(compiler, emitter);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private abstract class Descriptor(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDescJmp Jump(insGroup group)
        {
            var descriptor = new instrDescJmp();
            descriptor.idIns(INS_jmp);
            descriptor.idInsFmt(IF_LABEL);
            descriptor.idCodeSize(5);
            descriptor.idjTargetIG = group;
            return descriptor;
        }

        public static bool IsShort(instrDesc descriptor) => ((instrDescJmp)descriptor).idjShort;

        public static instrDesc Align(insGroup group, uint padding)
        {
            var descriptor = new instrDescAlign();
            descriptor.idIns(INS_align);
            descriptor.idCodeSize(padding);
            descriptor.idaIG = group;
#if DEBUG
            descriptor.isPlacedAfterJmp = true;
#endif
            return descriptor;
        }
    }
}
#endif
