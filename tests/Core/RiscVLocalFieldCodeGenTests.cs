// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_RISCV64
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class RiscVLocalFieldCodeGenTests
{
    [Test]
    public static void LocalFieldLoadPreservesTheTargetStackRecordingBoundary()
    {
        RiscVCodeGenPortTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 0, 12) { RegNum = REG_A0 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForLclFld(tree));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Target local-stack instruction recording is not implemented."));
        });
    }
}
#endif
