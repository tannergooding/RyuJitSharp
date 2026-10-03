// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterFrameReferenceTests
{
#if DEBUG
    [TestCase(-3, -16, false, "[TEMP_03-0x10]")]
    [TestCase(-3, 16, true, "[TEMP_03+0x10]")]
    [TestCase(-3, 0, false, "[TEMP_03]")]
    public static void ArmFrameReferencePreservesTemporaryAndDisplacementText(int variable, int displacement,
        bool assembly, string expected)
    {
        var emitter = CreateEmitter();

        Assert.That(Capture(() => DisplayFrameReference(emitter, variable, displacement, 0, assembly)),
            Is.EqualTo(expected));
    }
#else
    [Test]
    public static void ReleaseFrameReferenceDiagnosticDoesNotEmit()
    {
        var emitter = CreateEmitter();

        Assert.That(Capture(() => DisplayFrameReference(emitter, -3, -16, 0, false)), Is.Empty);
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispFrameRef")]
    private static extern void DisplayFrameReference(Emitter emitter, int variable, int displacement, uint ilOffset,
        bool assembly);

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler)
        {
            IsFramePointerUsed = false,
        }.Emitter;
        emitter.emitBegCG(compiler, default);
        return emitter;
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
}
#endif
