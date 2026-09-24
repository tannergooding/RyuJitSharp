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

        emitter.emitCurIG = new insGroup { igFlags = flags };

        Assert.That(emitter.emitGeneratingPrologOrFuncletProlog(), Is.EqualTo(expectedProlog));
        Assert.That(emitter.emitGeneratingEpilogOrFuncletEpilog(), Is.EqualTo(expectedEpilog));
    }
}
