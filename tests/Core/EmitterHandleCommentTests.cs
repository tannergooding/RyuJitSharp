// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterHandleCommentTests
{
#if TARGET_XARCH
    private const string Prefix = "      ;";
#elif TARGET_WASM
    private const string Prefix = "      ;;";
#else
    private const string Prefix = "      //";
#endif

    [TestCase(GTF_ICON_STR_HDL, "string handle")]
    [TestCase(GTF_ICON_CONST_PTR, "const ptr")]
    [TestCase(GTF_ICON_GLOBAL_PTR, "global ptr")]
    [TestCase(GTF_ICON_STATIC_HDL, "static handle")]
    [TestCase(GTF_ICON_FTN_ADDR, "function address")]
    [TestCase(GTF_ICON_TOKEN_HDL, "token handle")]
#if !DEBUG
    [TestCase(GTF_ICON_OBJ_HDL, "frozen object handle")]
#endif
    public static void SimpleHandleDescriptionsUseTheTargetCommentPrefix(GenTreeFlags flags, string description)
    {
        var emitter = CreateEmitter();
        var output = Capture(() => EmitHandleComment(emitter, 1, 0, flags));

        Assert.That(output, Is.EqualTo($"{Prefix} {description}"));
    }

    [Test]
    public static void StaticAddressCookieUsesTheTargetCommentPrefix()
    {
        var emitter = CreateEmitter();
        var output = Capture(() => EmitHandleComment(emitter, 0, 1, GTF_ICON_STATIC_ADDR_PTR));

        Assert.That(output, Is.EqualTo($"{Prefix} static base addr cell"));
    }

    [Test]
    public static void MissingHandleAndUnknownKindsProduceNoComment()
    {
        var emitter = CreateEmitter();
        var output = Capture(() => EmitHandleComment(emitter, 0, 0, GTF_ICON_STR_HDL));
        Assert.That(output, Is.Empty);

        output = Capture(() => EmitHandleComment(emitter, 1, 0, default));
        Assert.That(output, Is.Empty);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitDispCommentForHandle")]
    private static extern void EmitHandleComment(Emitter emitter, nint handle, nint cookie, GenTreeFlags flags);

    private static Emitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
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
