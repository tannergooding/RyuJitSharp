// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insCond;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64CodeGenPortTests
{
    [TestCase(GT_SELECTCC, INS_csel)]
    [TestCase(GT_SELECT_INVCC, INS_csinv)]
    [TestCase(GT_SELECT_NEGCC, INS_csneg)]
    [TestCase(GT_SELECT_INCCC, INS_csinc)]
    public static void ConditionalSelectVariantsRetainTheirInstruction(genTreeOps oper, instruction expected)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOpCC(oper, TYP_INT, new GenCondition(GenCondition.CodeKind.NE),
                Register(compiler, REG_R0), Register(compiler, REG_R1))
            {
                RegNum = REG_R2,
            };

            codeGen.genCodeForSelect(tree);

            var descriptor = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That((insCond)(uint)descriptor.idSmallCns(), Is.EqualTo(INS_COND_NE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R2));
        });
    }

    [TestCase(GenCondition.CodeKind.FNE, INS_COND_GT, INS_COND_LO, REG_R2, REG_R1)]
    [TestCase(GenCondition.CodeKind.FEQU, INS_COND_EQ, INS_COND_VS, REG_R0, REG_R2)]
    public static void CompoundFloatingConditionsRetainTheSecondSelect(
        GenCondition.CodeKind condition, insCond firstCondition, insCond secondCondition,
        regNumber secondSource1, regNumber secondSource2)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOpCC(GT_SELECTCC, TYP_INT, new GenCondition(condition),
                Register(compiler, REG_R0), Register(compiler, REG_R1))
            {
                RegNum = REG_R2,
            };

            codeGen.genCodeForSelect(tree);

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Select(descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_csel, INS_csel]));
            Assert.That((insCond)(uint)descriptors[0].idSmallCns(), Is.EqualTo(firstCondition));
            Assert.That((insCond)(uint)descriptors[1].idSmallCns(), Is.EqualTo(secondCondition));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(secondSource1));
            Assert.That(descriptors[1].idReg3(), Is.EqualTo(secondSource2));
        });
    }

    private static GenTreeIntCon Register(Compiler compiler, regNumber reg)
    {
        var node = compiler.gtNewIconNode(TYP_INT, 7);
        node.RegNum = reg;

        return node;
    }
}
#endif
