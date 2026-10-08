// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_RISCV64
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
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
            var descriptors = RiscVCodeGenPortTests.CurrentInstructionBuffer(codeGen.Emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialCount = descriptors.Count;
            var initialSize = RiscVCodeGenPortTests.CurrentInstructionGroupSize(codeGen.Emitter);
            var operand = new GenTreePhysReg(sourceReg, type) { RegNum = sourceReg };
            var tree = new GenTreeUnOp(GT_PUTARG_REG, type, operand) { RegNum = targetReg };

            Assert.DoesNotThrow(() => codeGen.genPutArgReg(tree));

            Assert.That(descriptors.Count, Is.EqualTo(initialCount + 1));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(targetReg));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(sourceReg));
            Assert.That(descriptors[^1].idCodeSize(), Is.GreaterThan(0u));
            Assert.That(RiscVCodeGenPortTests.CurrentInstructionGroupSize(codeGen.Emitter), Is.GreaterThan(initialSize));
        });
    }

    [Test]
    public static void PhysicalRegisterMoveReachesRiscVInstructionRecording()
    {
        RiscVCodeGenPortTests.WithCodeGen((_, codeGen) =>
        {
            var descriptors = RiscVCodeGenPortTests.CurrentInstructionBuffer(codeGen.Emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialCount = descriptors.Count;
            var initialSize = RiscVCodeGenPortTests.CurrentInstructionGroupSize(codeGen.Emitter);
            var tree = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A1 };

            Assert.DoesNotThrow(() => codeGen.genCodeForPhysReg(tree));

            Assert.That(descriptors.Count, Is.EqualTo(initialCount + 1));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_A1));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(REG_A0));
            Assert.That(descriptors[^1].idCodeSize(), Is.GreaterThan(0u));
            Assert.That(RiscVCodeGenPortTests.CurrentInstructionGroupSize(codeGen.Emitter), Is.GreaterThan(initialSize));
        });
    }
}
#endif
