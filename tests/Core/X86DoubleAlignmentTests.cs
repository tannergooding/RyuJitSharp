// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && DOUBLE_ALIGN
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class X86DoubleAlignmentTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void CodeGenAndCompilerShareDoubleAlignmentState(bool framePointer)
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, _) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            ICodeGen contract = codeGen;
            codeGen.IsFramePointerUsed = framePointer;

            Assert.That(contract.IsDoubleAligned, Is.False);
            Assert.That(compiler.genDoubleAlign, Is.False);

            contract.IsDoubleAligned = true;
            Assert.That(codeGen.IsDoubleAligned, Is.True);
            Assert.That(compiler.genDoubleAlign, Is.True);
            Assert.That(codeGen.IsFramePointerUsed, Is.EqualTo(framePointer));

            contract.IsDoubleAligned = false;
            Assert.That(codeGen.IsDoubleAligned, Is.False);
            Assert.That(compiler.genDoubleAlign, Is.False);
        });
    }

    [Test]
    public static void EmptyFloatingSavesAndUnneededAvxClearingDoNotRequireAmd64Recording()
    {
        X86EmitterStaticOutputTests.WithEmitter((compiler, emitter) =>
        {
            var codeGen = compiler.codeGen as CodeGen
                ?? throw new AssertionException("Missing x86 code generator.");
            compiler.compCalleeFPRegsSavedMask = 0;
            emitter.ContainsCallNeedingVzeroupper = false;
            emitter.Contains256BitOrMoreAvxInstruction = false;

            Assert.DoesNotThrow(() =>
            {
                codeGen.genPreserveCalleeSavedFltRegs();
                codeGen.genRestoreCalleeSavedFltRegs();
                codeGen.genClearAvxStateInProlog();
                codeGen.genClearAvxStateInEpilog();
            });
        });
    }
}
#endif
