// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenConditionalTests
{
    [TestCase(false, REG_RAX)]
    [TestCase(false, REG_RCX)]
    [TestCase(false, REG_RDX)]
    [TestCase(true, REG_RDX)]
    public static void SelectionsRetainRegisterConflictsAndBooleanConsumption(bool boolean, regNumber target)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var trueVal = Register(compiler, TYP_INT, REG_RAX);
            var falseVal = Register(compiler, TYP_INT, REG_RCX);
            GenTreeOp tree = boolean
                ? new GenTreeConditional(GT_SELECT, TYP_INT, Register(compiler, TYP_INT, REG_R8), trueVal, falseVal)
                : new GenTreeOpCC(GT_SELECTCC, TYP_INT, new GenCondition(GenCondition.CodeKind.NE), trueVal, falseVal);
            tree.RegNum = target;

            codeGen.genCodeForSelect(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(target == REG_RAX ? INS_cmove : INS_cmovne));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(target));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(target == REG_RAX ? REG_RCX : REG_RAX));
            Assert.That(descriptors.Count(id => id.idIns() == INS_mov), Is.EqualTo(target == REG_RDX ? 1 : 0));
            if (boolean)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_test));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R8));
            }
        });
    }

    [TestCase(GenCondition.CodeKind.FEQ, INS_cmovnp, INS_cmovne)]
    [TestCase(GenCondition.CodeKind.FNEU, INS_cmovp, INS_cmovne)]
    public static void FloatingSelectionsPreserveBothFlagConditions(
        GenCondition.CodeKind condition, instruction first, instruction second)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOpCC(GT_SELECTCC, TYP_INT, new GenCondition(condition),
                Register(compiler, TYP_INT, REG_RAX), Register(compiler, TYP_INT, REG_RCX))
            {
                RegNum = REG_RDX,
            };

            codeGen.genCodeForSelect(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_mov, first, second]));
            Assert.That(descriptors[^1].idReg2(),
                Is.EqualTo(condition == GenCondition.CodeKind.FEQ ? REG_RCX : REG_RAX));
        });
    }

    [Test]
    public static void ContainedAddressConflictsSwapBeforeOverwritingTheBase()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = new GenTreeAddrMode(TYP_BYREF, Register(compiler, TYP_BYREF, REG_RDX),
                Register(compiler, TYP_I_IMPL, REG_R8), 4, 8) { IsContained = true };
            var memory = new GenTreeIndir(GT_IND, TYP_INT, address) { IsContained = true };
            var tree = new GenTreeOpCC(GT_SELECTCC, TYP_INT, new GenCondition(GenCondition.CodeKind.NE),
                memory, Register(compiler, TYP_INT, REG_RCX)) { RegNum = REG_RDX };
            Assert.That(memory.ContainedRegMask, Is.EqualTo(RBM_RDX | RBM_R8));

            codeGen.genCodeForSelect(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors.Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_mov, INS_cmove]));
            Assert.That(descriptors[0].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RDX));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_RCX));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void BooleanBranchesKeepColdBoundaryJumps(bool nextIsFalse, bool coldBoundary)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var (whenTrue, whenFalse) = SetConditionalBlock(compiler, nextIsFalse, coldBoundary);
            var tree = new GenTreeUnOp(GT_JTRUE, TYP_VOID, Register(compiler, TYP_INT, REG_RAX));

            codeGen.genCodeForJTrue(tree);

            var descriptors = Descriptors(codeGen);
            var fallsThrough = nextIsFalse && !coldBoundary;
            Assert.That(descriptors.Select(id => id.idIns()), Is.EqualTo(fallsThrough
                ? (instruction[])[INS_test, INS_jne]
                : [INS_test, INS_jne, INS_jmp]));
            Assert.That(EmitterJumpInstructionTests.JumpView.Target(descriptors[1]), Is.SameAs(whenTrue));
            if (!fallsThrough)
            {
                Assert.That(EmitterJumpInstructionTests.JumpView.Target(descriptors[^1]), Is.SameAs(whenFalse));
            }
        });
    }

    [TestCase(GenCondition.CodeKind.NE, false)]
    [TestCase(GenCondition.CodeKind.FNEU, false)]
    [TestCase(GenCondition.CodeKind.FEQ, true)]
    public static void FlagBranchesRetainNativeOrAndSequences(GenCondition.CodeKind condition, bool createsLabel)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var (whenTrue, _) = SetConditionalBlock(compiler, nextIsFalse: true, coldBoundary: false);
            var firstGroup = codeGen.Emitter.emitCurIG;
            assert(firstGroup is not null);

            codeGen.genCodeForJcc(new GenTreeCC(GT_JCC, TYP_VOID, new GenCondition(condition)));

            var descriptors = createsLabel
                ? firstGroup.igData ?? throw new AssertionException("Missing condition group.")
                : [.. Descriptors(codeGen)];
            instruction[] expected = condition switch
            {
                GenCondition.CodeKind.NE => [INS_jne],
                GenCondition.CodeKind.FNEU => [INS_jp, INS_jne],
                _ => [INS_jp, INS_je],
            };
            Assert.That(descriptors.Select(id => id.idIns()), Is.EqualTo(expected));
            Assert.That(EmitterJumpInstructionTests.JumpView.Target(descriptors[^1]), Is.SameAs(whenTrue));
            if (createsLabel)
            {
                var next = EmitterJumpInstructionTests.JumpView.Target(descriptors[0]);
                Assert.That(next, Is.Not.SameAs(whenTrue));
                assert(next is not null);
                Assert.That(next.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
            }
        });
    }

    [Test]
    public static void SetccNodesProduceTheirRegisterResult()
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var tree = new GenTreeCC(GT_SETCC, TYP_INT, new GenCondition(GenCondition.CodeKind.ULT))
            {
                RegNum = REG_RAX,
            };
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);

            codeGen.genCodeForSetcc(tree);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_setb, INS_movzx]));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
        });
    }

    [TestCase(EJ_NONE, INS_none)]
    [TestCase(EJ_jo, INS_cmovo)]
    [TestCase(EJ_jno, INS_cmovno)]
    [TestCase(EJ_jp, INS_cmovp)]
    [TestCase(EJ_jle, INS_cmovle)]
    public static void CmovMappingIncludesOverflowAndParity(emitJumpKind condition, instruction expected)
    {
        Assert.That(CodeGen.JumpKindToCmov(condition), Is.EqualTo(expected));
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsSelectionInstructions()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeConditional(GT_SELECT, TYP_INT,
                Register(compiler, TYP_INT, REG_R8), Register(compiler, TYP_INT, REG_RAX),
                Register(compiler, TYP_INT, REG_RCX)) { RegNum = REG_RDX };
            compiler.opts.dspCode = true;
            var diagnostic = InstructionRecordingTestSupport.Capture(() => codeGen.genCodeForSelect(tree));
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(3));
            Assert.That(diagnostic, Does.Contain("cmov"));
        });
    }
#endif

    private static (BasicBlock WhenTrue, BasicBlock WhenFalse) SetConditionalBlock(
        Compiler compiler, bool nextIsFalse, bool coldBoundary)
    {
        var block = compiler.compCurBB;
        assert(block is not null);
        var whenTrue = new BasicBlock(null, null);
        var whenFalse = new BasicBlock(null, null);
        whenTrue.SetFlags(BBF_HAS_LABEL);
        whenFalse.SetFlags(BBF_HAS_LABEL);
        block.SetCond(new FlowEdge(block, whenTrue, null), new FlowEdge(block, whenFalse, null));
        block.Next = nextIsFalse ? whenFalse : whenTrue;
        compiler.fgFirstColdBlock = coldBoundary ? whenFalse : null;

        return (whenTrue, whenFalse);
    }
}
