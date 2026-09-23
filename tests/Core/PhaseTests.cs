// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

#if DEBUG
[NonParallelizable]
internal static class PhaseTests
{
    [Test]
    public static unsafe void DisabledPhaseChecksSkipValidation()
    {
        var previousConfig = Globals.JitConfig;
        using var jitTls = new JitTls(null);
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.activePhaseChecks = PhaseChecks.CHECK_IR_RELAXED;
        JitTls.Compiler = compiler;

        try
        {
            Globals.JitConfig = default;
            Assert.That(Globals.JitConfig.JitEnablePhaseChecks, Is.Zero);
            var phase = new ProbePhase(compiler);
            Assert.DoesNotThrow(phase.Finish);
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    private sealed class ProbePhase(Compiler compiler) : Phase(compiler, Phases.PHASE_IMPORTATION)
    {
        public void Finish() => PostPhase(PhaseStatus.MODIFIED_EVERYTHING);

        protected override PhaseStatus DoPhase() => PhaseStatus.MODIFIED_EVERYTHING;
    }
}
#endif
