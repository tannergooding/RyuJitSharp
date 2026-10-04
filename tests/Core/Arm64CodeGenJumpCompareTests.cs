// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class Arm64CodeGenJumpCompareTests
{
    [TestCase(GenCondition.CodeKind.EQ)]
    [TestCase(GenCondition.CodeKind.NE)]
    public static void CompareJumpUsesTheUnportedRegisterBranchRecorder(GenCondition.CodeKind condition)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = CreateJump(compiler, GT_JCMP, condition, 0);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree)) ??
                throw new AssertionException("The unported ARM64 register-branch dependency did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("ARM64 emitIns_J_R recording is not ported."));
            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
        });
    }

    [TestCase(GenCondition.CodeKind.EQ, 1L, INS_tbz, 0)]
    [TestCase(GenCondition.CodeKind.NE, 8L, INS_tbnz, 3)]
    [TestCase(GenCondition.CodeKind.NE, 0x80000000L, INS_tbnz, 31)]
    public static void BitTestJumpPreservesTheSelectedBitAndUnportedImmediateBranch(
        GenCondition.CodeKind condition, long mask, instruction expectedInstruction, int expectedBitIndex)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = CreateJump(compiler, GT_JTEST, condition, (nint)mask);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(tree)) ??
                throw new AssertionException("The unported ARM64 immediate-branch dependency did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo(
                $"ARM64 emitIns_J_R_I recording is not ported ({expectedInstruction}, {expectedBitIndex})."));
            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
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
        compiler.compCurBB = block;

        var value = compiler.gtNewIconNode(TYP_INT, 7);
        value.RegNum = REG_R1;
        var constant = compiler.gtNewIconNode(TYP_INT, immediate);
        constant.IsContained = true;

        return new GenTreeOpCC(oper, TYP_VOID, new GenCondition(condition), value, constant);
    }
}
#endif
