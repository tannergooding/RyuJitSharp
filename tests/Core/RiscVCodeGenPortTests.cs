// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class RiscVCodeGenPortTests
{
    [TestCase(TYP_BYREF)]
    [TestCase(TYP_I_IMPL)]
    public static void LocalAddressPreservesTheStackInstructionRecordingBoundary(var_types type)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var localAddress = new GenTreeLclFld(GT_LCL_ADDR, type, 0, 24)
            {
                RegNum = REG_A0,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForLclAddr(localAddress));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Target local-stack instruction recording is not implemented."));
        });
    }
}
#endif
