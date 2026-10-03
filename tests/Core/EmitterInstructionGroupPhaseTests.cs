// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class EmitterInstructionGroupPhaseTests
{
    [TestCase(InsGroupFlags.None, false, false)]
    [TestCase(InsGroupFlags.Prolog, true, false)]
    [TestCase(InsGroupFlags.Epilog, false, true)]
    [TestCase(InsGroupFlags.FuncletProlog, true, false)]
    [TestCase(InsGroupFlags.FuncletEpilog, false, true)]
    [TestCase(InsGroupFlags.NoGCInterrupt | InsGroupFlags.Placeholder, false, false)]
    [TestCase(InsGroupFlags.Prolog | InsGroupFlags.FuncletEpilog, true, true)]
    public static void PhaseQueriesReadCurrentInstructionGroupFlags(
        InsGroupFlags flags, bool expectedProlog, bool expectedEpilog)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;

        Assert.That(emitter.emitGeneratingPrologOrFuncletProlog(), Is.False);
        Assert.That(emitter.emitGeneratingEpilogOrFuncletEpilog(), Is.False);
        Assert.That(IsInProlog(emitter, null), Is.False);
        Assert.That(IsInEpilog(emitter, null), Is.False);
        Assert.That(IsInFuncletProlog(emitter, null), Is.False);
        Assert.That(IsInFuncletEpilog(emitter, null), Is.False);

        emitter.emitCurIG = new insGroup { igFlags = flags };

        Assert.That(emitter.emitGeneratingPrologOrFuncletProlog(), Is.EqualTo(expectedProlog));
        Assert.That(emitter.emitGeneratingEpilogOrFuncletEpilog(), Is.EqualTo(expectedEpilog));
        Assert.That(IsInProlog(emitter, emitter.emitCurIG), Is.EqualTo((flags & InsGroupFlags.Prolog) != 0));
        Assert.That(IsInEpilog(emitter, emitter.emitCurIG), Is.EqualTo((flags & InsGroupFlags.Epilog) != 0));
        Assert.That(IsInFuncletProlog(emitter, emitter.emitCurIG),
            Is.EqualTo((flags & InsGroupFlags.FuncletProlog) != 0));
        Assert.That(IsInFuncletEpilog(emitter, emitter.emitCurIG),
            Is.EqualTo((flags & InsGroupFlags.FuncletEpilog) != 0));
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitIGisInProlog")]
    private static extern bool IsInProlog(Emitter emitter, insGroup? group);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitIGisInEpilog")]
    private static extern bool IsInEpilog(Emitter emitter, insGroup? group);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitIGisInFuncletProlog")]
    private static extern bool IsInFuncletProlog(Emitter emitter, insGroup? group);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitIGisInFuncletEpilog")]
    private static extern bool IsInFuncletEpilog(Emitter emitter, insGroup? group);
}
