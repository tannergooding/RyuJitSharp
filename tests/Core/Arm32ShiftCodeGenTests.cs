// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm32ShiftCodeGenTests
{
    [TestCase(GT_LSH, INS_lsl)]
    [TestCase(GT_RSH, INS_asr)]
    [TestCase(GT_RSZ, INS_lsr)]
    [TestCase(GT_ROR, INS_ror)]
    public static void VariableShiftsUseThreeRegisterInstruction(
        genTreeOps oper, instruction expected)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var operand = new GenTreePhysReg(REG_R2, TYP_INT) { RegNum = REG_R2 };
            var shiftBy = new GenTreePhysReg(REG_R4, TYP_INT) { RegNum = REG_R4 };
            var tree = new GenTreeOp(oper, TYP_INT, operand, shiftBy) { RegNum = REG_R3 };

#if DEBUG
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForShift(tree));
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
#else
            codeGen.genCodeForShift(tree);
            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R2));
            Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_R4));
#endif
        });
    }

    [TestCase(GT_LSH, INS_lsl, -1, 31)]
    [TestCase(GT_LSH, INS_mov, 32, 0)]
    [TestCase(GT_LSH, INS_lsl, 33, 1)]
    [TestCase(GT_RSH, INS_asr, -1, 31)]
    [TestCase(GT_RSZ, INS_mov, 32, 0)]
    [TestCase(GT_ROR, INS_ror, 33, 1)]
    public static void ImmediateCountsAreMaskedToTheRegisterWidth(
        genTreeOps oper, instruction expected, int count, int expectedCount)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var operand = new GenTreePhysReg(REG_R2, TYP_INT) { RegNum = REG_R2 };
            var shiftBy = compiler.gtNewIconNode(TYP_INT, count);
            shiftBy.IsContained = true;
            var tree = new GenTreeOp(oper, TYP_INT, operand, shiftBy) { RegNum = REG_R3 };

#if DEBUG
            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForShift(tree));
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
#else
            codeGen.genCodeForShift(tree);
            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing descriptor buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expected));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R3));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R2));
            Assert.That(codeGen.Emitter.emitGetInsSC(descriptors[0]), Is.EqualTo((nint)expectedCount));
#endif
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif
