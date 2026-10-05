// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64CodeGenJumpCompareTests
{
    [TestCase(GenCondition.CodeKind.EQ)]
    [TestCase(GenCondition.CodeKind.NE)]
    public static void CompareJumpRecordsAConditionalRegisterBranch(GenCondition.CodeKind condition)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = CreateJump(compiler, GT_JCMP, condition, 0);

            codeGen.genCodeForTreeNode(tree);

            var descriptor = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter).Single();
            var branch = (Emitter.instrDescJmp)descriptor;
            var currentBlock = compiler.compCurBB ?? throw new AssertionException("Missing current block.");
            Assert.That(descriptor.idIns(), Is.EqualTo(condition is GenCondition.CodeKind.EQ ? INS_cbz : INS_cbnz));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R1));
            Assert.That(branch.idjTarget, Is.SameAs(currentBlock.TrueTarget));
        });
    }

    [TestCase(GenCondition.CodeKind.EQ, 1L, INS_tbz, 0)]
    [TestCase(GenCondition.CodeKind.NE, 8L, INS_tbnz, 3)]
    [TestCase(GenCondition.CodeKind.NE, 0x80000000L, INS_tbnz, 31)]
    public static void BitTestJumpRecordsTheSelectedBitAndImmediateBranch(
        GenCondition.CodeKind condition, long mask, instruction expectedInstruction, int expectedBitIndex)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = CreateJump(compiler, GT_JTEST, condition, (nint)mask);

            codeGen.genCodeForTreeNode(tree);

            var descriptor = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter).Single();
            var branch = (Emitter.instrDescJmp)descriptor;
            var currentBlock = compiler.compCurBB ?? throw new AssertionException("Missing current block.");
            Assert.That(descriptor.idIns(), Is.EqualTo(expectedInstruction));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R1));
            Assert.That(descriptor.idSmallCns(), Is.EqualTo(expectedBitIndex));
            Assert.That(branch.idjTarget, Is.SameAs(currentBlock.TrueTarget));
        });
    }

    private static GenTreeOpCC CreateJump(
        Compiler compiler, genTreeOps oper, GenCondition.CodeKind condition, nint immediate)
    {
        var trueTarget = new BasicBlock(null, null);
        trueTarget.SetFlags(BBF_HAS_LABEL);
        var falseTarget = new BasicBlock(null, null);
        falseTarget.SetFlags(BBF_HAS_LABEL);
        var block = new BasicBlock(null, null);
        block.SetCond(new FlowEdge(block, trueTarget, null), new FlowEdge(block, falseTarget, null));
        block.Next = falseTarget;
        compiler.compCurBB = block;

        var value = compiler.gtNewIconNode(TYP_INT, 7);
        value.RegNum = REG_R1;
        var constant = compiler.gtNewIconNode(TYP_INT, immediate);
        constant.IsContained = true;

        return new GenTreeOpCC(oper, TYP_VOID, new GenCondition(condition), value, constant);
    }
}
#endif
