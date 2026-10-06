// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64ShiftCodeGenTests
{
    [TestCase(GT_LSH, INS_lsl)]
    [TestCase(GT_RSH, INS_asr)]
    [TestCase(GT_RSZ, INS_lsr)]
    [TestCase(GT_ROR, INS_ror)]
    public static void VariableShiftsUseThreeRegisterInstruction(
        genTreeOps oper, instruction expected)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var operand = new GenTreePhysReg(REG_R1, TYP_INT) { RegNum = REG_R1 };
            var shiftBy = new GenTreePhysReg(REG_R3, TYP_INT) { RegNum = REG_R3 };
            var tree = new GenTreeOp(oper, TYP_INT, operand, shiftBy) { RegNum = REG_R2 };

            codeGen.genCodeForShift(tree);

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R2));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_R3));
        });
    }

    [TestCase(TYP_INT, -1, 31, EA_4BYTE)]
    [TestCase(TYP_INT, 32, 0, EA_4BYTE)]
    [TestCase(TYP_INT, 33, 1, EA_4BYTE)]
    [TestCase(TYP_LONG, -1, 63, EA_8BYTE)]
    [TestCase(TYP_LONG, 64, 0, EA_8BYTE)]
    [TestCase(TYP_LONG, 65, 1, EA_8BYTE)]
    public static void ImmediateCountsAreMaskedToTheRegisterWidth(
        var_types type, int count, int expectedCount, emitAttr expectedSize)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var operand = new GenTreePhysReg(REG_R1, type) { RegNum = REG_R1 };
            var shiftBy = compiler.gtNewIconNode(TYP_INT, count);
            shiftBy.IsContained = true;
            var tree = new GenTreeOp(GT_LSH, type, operand, shiftBy) { RegNum = REG_R2 };

            codeGen.genCodeForShift(tree);

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_lsl));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(expectedSize));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R2));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptors[0]), Is.EqualTo((nint)expectedCount));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
