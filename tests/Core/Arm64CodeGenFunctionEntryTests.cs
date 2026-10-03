// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64CodeGenFunctionEntryTests
{
    [Test]
    public static void FunctionEntryDispatchRetainsTheInstructionGroupRecordingBoundary()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTree(GT_FTN_ENTRY, TYP_I_IMPL)
            {
                RegNum = REG_R3,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree)) ??
                throw new AssertionException("The unported ARM64 instruction-group recording dependency did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("Instruction-group address recording requires xarch."));
            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
        });
    }
}
#endif
