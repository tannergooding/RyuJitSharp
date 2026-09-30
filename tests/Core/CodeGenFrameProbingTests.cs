// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenFrameProbingTests
{
    [TestCase(8191, 8191, 1)]
    [TestCase(8192, 0, 2)]
    [TestCase(8193, 1, 2)]
    [TestCase(16384, 0, 3)]
    public static void ConstantProbesUseTheEePageSizeAndTouchExactPageBoundaries(
        int allocation, int lastTouch, int probes)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.eeInfo.osPageSize = 8192;
            var finalOffset = codeGen.genStackPointerConstantAdjustmentLoopWithProbe(-allocation, true);
            var instructions = Descriptors(codeGen);

            Assert.That(finalOffset, Is.EqualTo((nint)lastTouch));
            Assert.That(instructions.Count(id => id.idIns() == INS_test), Is.EqualTo(probes));
            Assert.That(instructions.Where(id => id.idIns() == INS_sub)
                .Sum(id => (long)InstructionConstant(codeGen.Emitter, id)), Is.EqualTo(allocation));
            Assert.That(instructions[0].idIns(), Is.EqualTo(INS_test));
            Assert.That(instructions[^1].idIns(), Is.EqualTo(lastTouch == 0 ? INS_test : INS_sub));
        });
    }

    [Test]
    public static void ExactlyOnePageRestorationProbesBeforeSubtracting()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.eeInfo.osPageSize = 4096;
            var lastTouch = codeGen.genStackPointerConstantAdjustmentLoopWithProbe(-4096, true);
            var instructions = Descriptors(codeGen);

            Assert.That(lastTouch, Is.EqualTo((nint)0));
            Assert.That(instructions.Select(id => id.idIns()), Is.EqualTo(
                (instruction[])[INS_test, INS_sub, INS_test]));
            Assert.That(instructions[0].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_SPBASE));
            Assert.That(instructions[^1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_SPBASE));
        });
    }
}
