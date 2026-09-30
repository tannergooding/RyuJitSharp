// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_X86
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenXarchFrameClosureTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void X86FuncletCaptureDoesNotRequireFinalFrameLayout(bool hasFunclet)
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            compiler.compHndBBtabCount = hasFunclet ? (ushort)1 : (ushort)0;
            var group = emitter.emitCurIG;

            codeGen.genCaptureFuncletPrologEpilogInfo();

            Assert.That(emitter.emitCurIG, Is.SameAs(group));
        });
    }
}
#endif
