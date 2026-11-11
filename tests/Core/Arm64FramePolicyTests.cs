// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64FramePolicyTests
{
    [Test]
    public static void OrdinaryOptionOnlyChangesFpLrPlacement()
    {
        WithCodeGen(0, codeGen => {
            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.False);

            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(true);
            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.True);

            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(false);
            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.False);
        });
    }

    private static void WithCodeGen(int option, System.Action<CodeGen> action)
    {
        if (!Configuration.TARGET_ARM64)
        {
            Assert.Ignore("Frame-policy checks require the ARM64 target.");
        }

        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.SetMinOpts(false);
        compiler.opts.compJitSaveFpLrWithCalleeSavedRegisters = option;
        JitTls.Compiler = compiler;

        try
        {
            action(new CodeGen(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
