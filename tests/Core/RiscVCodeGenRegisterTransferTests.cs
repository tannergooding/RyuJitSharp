// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_RISCV64
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class RiscVCodeGenRegisterTransferTests
{
    [TestCase(REG_A0, REG_A1, TYP_INT)]
    [TestCase(REG_FT0, REG_FT1, TYP_FLOAT)]
    [TestCase(REG_FT0, REG_A0, TYP_FLOAT)]
    [TestCase(REG_FT0, REG_FT1, TYP_DOUBLE)]
    [TestCase(REG_FT0, REG_A0, TYP_DOUBLE)]
    public static void RegisterArgumentMoveReachesRiscVInstructionRecording(
        regNumber sourceReg, regNumber targetReg, var_types type)
    {
        RiscVCodeGenPortTests.WithCodeGen((_, codeGen) =>
        {
            var operand = new GenTreePhysReg(sourceReg, type) { RegNum = sourceReg };
            var tree = new GenTreeUnOp(GT_PUTARG_REG, type, operand) { RegNum = targetReg };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genPutArgReg(tree));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message, Does.Contain("Register move recording requires xarch."));
        });
    }

    [Test]
    public static void PhysicalRegisterMoveReachesRiscVInstructionRecording()
    {
        RiscVCodeGenPortTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A1 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForPhysReg(tree));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message, Does.Contain("Register move recording requires xarch."));
        });
    }
}
#endif
