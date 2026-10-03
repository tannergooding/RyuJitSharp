// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64CodeGenAsyncResumeInfoTests
{
    [Test]
    public static void AsyncResumeInfoDispatchRetainsTheSharedRecordingBoundary()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreeVal(GT_ASYNC_RESUME_INFO, TYP_I_IMPL, 0)
            {
                RegNum = REG_R3,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree)) ??
                throw new AssertionException("The unported async-resume recording dependency did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message,
                Is.EqualTo("Instruction recording outside AMD64 is not ported."));
            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
        });
    }

    [Test]
    public static void RecordAsyncResumeDispatchRetainsTheSharedRecordingBoundary()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreeVal(GT_RECORD_ASYNC_RESUME, TYP_VOID, 0);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree)) ??
                throw new AssertionException("The unported async-resume recording dependency did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message,
                Is.EqualTo("Instruction recording outside AMD64 is not ported."));
            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
        });
    }
}
#endif
