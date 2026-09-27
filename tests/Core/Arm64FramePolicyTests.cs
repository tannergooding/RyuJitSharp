// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
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
            Assert.That(codeGen.genForceFuncletFrameType5, Is.False);

            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(true);
            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.True);
            Assert.That(codeGen.genForceFuncletFrameType5, Is.False);

            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(false);
            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.False);
            Assert.That(codeGen.genForceFuncletFrameType5, Is.False);
        });
    }

    [Test]
    public static void OptionThreeForcesFrameTypeFiveOnlyAfterEnablingAndStaysForced()
    {
        WithCodeGen(3, codeGen => {
            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(false);
            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.False);
            Assert.That(codeGen.genForceFuncletFrameType5, Is.False);

            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(true);
            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.True);
            Assert.That(codeGen.genForceFuncletFrameType5, Is.True);

            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(false);
            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.False);
            Assert.That(codeGen.genForceFuncletFrameType5, Is.True);

            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(true);
            Assert.That(codeGen.IsSaveFpLrWithAllCalleeSavedRegisters, Is.True);
            Assert.That(codeGen.genForceFuncletFrameType5, Is.True);
        });
    }

    private static void WithCodeGen(int option, System.Action<CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
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
#endif
