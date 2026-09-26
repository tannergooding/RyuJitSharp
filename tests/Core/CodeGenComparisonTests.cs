// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenComparisonTests
{
    [TestCase(GT_LT, false, INS_setl)]
    [TestCase(GT_LT, true, INS_setb)]
    [TestCase(GT_GE, false, INS_setge)]
    [TestCase(GT_EQ, false, INS_sete)]
    public static void IntegralComparisonsPreserveSignednessAndBooleanWidening(
        genTreeOps oper, bool unsigned, instruction set)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeOp(oper, TYP_INT,
                Register(compiler, TYP_INT, REG_RAX), Register(compiler, TYP_INT, REG_RCX))
            {
                RegNum = REG_RDX,
                Flags = unsigned ? GTF_UNSIGNED : GTF_EMPTY,
            };
            codeGen.genConsumeOperands(tree);

            codeGen.genCodeForCompare(tree);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_cmp, set, INS_movzx]));
            Assert.That(Descriptors(codeGen)[2].idOpSize(), Is.EqualTo(EA_1BYTE));
        });
    }

    [TestCase(GT_TEST_EQ, TYP_LONG, TYP_LONG, 255, EA_1BYTE, INS_test, INS_sete)]
    [TestCase(GT_TEST_NE, TYP_INT, TYP_INT, 256, EA_4BYTE, INS_test, INS_setne)]
    [TestCase(GT_BITTEST_NE, TYP_LONG, TYP_INT, 63, EA_8BYTE, INS_bt, INS_setb)]
    public static void BitTestsPreserveMaskNarrowingAndIndependentIndexWidth(
        genTreeOps oper, var_types valueType, var_types indexType, int constant,
        emitAttr expectedSize, instruction compare, instruction set)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var mask = compiler.gtNewIconNode(indexType, constant);
            mask.IsContained = oper != GT_BITTEST_NE;
            if (!mask.IsContained)
            {
                mask.RegNum = REG_RCX;
            }
            var tree = new GenTreeOp(oper, TYP_INT, Register(compiler, valueType, REG_RAX), mask)
            {
                RegNum = REG_RDX,
            };
            codeGen.genConsumeOperands(tree);

            codeGen.genCodeForCompare(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors.Select(id => id.idIns()), Is.EqualTo((instruction[])[compare, set, INS_movzx]));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(expectedSize));
        });
    }

    [TestCase(GT_LT, TYP_INT, true, false)]
    [TestCase(GT_GE, TYP_LONG, true, false)]
    [TestCase(GT_LT, TYP_LONG, false, false)]
    [TestCase(GT_GE, TYP_INT, true, true)]
    public static void SignComparisonsOnlyUseShiftsInTheNativeMode(
        genTreeOps oper, var_types type, bool optimize, bool unsigned)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.canUseAllOpts = optimize;
            var zero = compiler.gtNewIconNode(type, 0);
            zero.IsContained = true;
            var tree = new GenTreeOp(oper, TYP_INT, Register(compiler, type, REG_RAX), zero)
            {
                RegNum = REG_RCX,
                Flags = unsigned ? GTF_UNSIGNED : GTF_EMPTY,
            };
            codeGen.genConsumeOperands(tree);

            codeGen.genCodeForCompare(tree);

            var descriptors = Descriptors(codeGen);
            if (optimize && !unsigned)
            {
                instruction[] expected = oper == GT_LT
                    ? [INS_mov, INS_shr_N]
                    : [INS_mov, INS_not, INS_shr_N];
                Assert.That(descriptors.Select(id => id.idIns()), Is.EqualTo(expected));
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[^1]),
                    Is.EqualTo((nint)((type.Size * 8) - 1)));
            }
            else
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_test));
                Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_movzx));
            }
        });
    }

    [TestCase(TYP_DOUBLE, GT_LT, false, false, INS_seta, true)]
    [TestCase(TYP_DOUBLE, GT_NE, true, true, INS_setp, false)]
    [TestCase(TYP_FLOAT, GT_NE, false, false, INS_setne, false)]
    public static void FloatingConditionsPreserveSwapAndSameRegisterNaNChecks(
        var_types type, genTreeOps oper, bool unordered, bool sameRegister, instruction set, bool swapped)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var first = compiler.gtNewDconNode(type, 1);
            first.RegNum = REG_XMM0;
            var second = compiler.gtNewDconNode(type, 2);
            second.RegNum = sameRegister ? REG_XMM0 : REG_XMM1;
            var tree = new GenTreeOp(oper, TYP_INT, first, second)
            {
                RegNum = REG_RAX,
                Flags = unordered ? GTF_RELOP_NAN_UN : GTF_EMPTY,
            };
            codeGen.genConsumeOperands(tree);

            codeGen.genCodeForCompare(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors.Select(id => id.idIns()),
                Is.EqualTo((instruction[])[type == TYP_FLOAT ? INS_ucomiss : INS_ucomisd, set, INS_movzx]));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(swapped ? REG_XMM1 : REG_XMM0));
        });
    }

    [TestCase(TYP_INT, false, INS_sete, 2)]
    [TestCase(TYP_INT, true, INS_sete_apx, 1)]
    [TestCase(TYP_UBYTE, true, INS_sete, 1)]
    [NonParallelizable]
    public static void BooleanResultsUseApxZeroUpperOnlyWhenWideningIsNeeded(
        var_types type, bool zeroUpper, instruction set, int count)
    {
        var saved = ApxZu(ref JitConfig);
        ApxZu(ref JitConfig) = zeroUpper ? 1 : 0;
        try
        {
            CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
            {
                foreach (var isa in new[] { InstructionSet_APX, InstructionSet_AVX512 })
                {
                    compiler.opts.compSupportsISA.AddInstructionSet(isa);
                    compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
                    compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
                }
                codeGen.Emitter.UseVexEncodings = true;
                codeGen.Emitter.UseEvexEncodings = true;
                codeGen.Emitter.UsePromotedEvexEncodings = true;

                codeGen.inst_SETCC(new GenCondition(GenCondition.CodeKind.EQ), type, REG_RAX);

                Assert.That(Descriptors(codeGen), Has.Count.EqualTo(count));
                Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(set));
            });
        }
        finally
        {
            ApxZu(ref JitConfig) = saved;
        }
    }

    [TestCase(GenCondition.CodeKind.FEQ, INS_setnp, INS_jp, INS_sete)]
    [TestCase(GenCondition.CodeKind.FNEU, INS_setp, INS_jp, INS_setne)]
    public static void CompoundConditionsKeepShortCircuitLabelsAndFinalWidening(
        GenCondition.CodeKind condition, instruction firstSet, instruction jump, instruction secondSet)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var firstGroup = codeGen.Emitter.emitCurIG;
            assert(firstGroup is not null);
            assert(compiler.compCurBB is not null);
            compiler.compCurBB.SetFlags(BasicBlockFlags.BBF_COLD);
            var blockCount = compiler.fgBBcount;

            codeGen.inst_SETCC(new GenCondition(condition), TYP_INT, REG_RAX);

            var saved = firstGroup.igData ?? throw new AssertionException("Missing completed condition group.");
            Assert.That(saved.Select(id => id.idIns()), Is.EqualTo((instruction[])[firstSet, jump, secondSet]));
            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_movzx]));
            var target = EmitterJumpInstructionTests.JumpView.Target(saved[1]);
            Assert.That(target, Is.Not.Null);
            assert(target is not null);
            Assert.That(target.HasFlag(BasicBlockFlags.BBF_COLD | BasicBlockFlags.BBF_HAS_LABEL), Is.True);
            Assert.That(target.bbEmitCookie, Is.SameAs(codeGen.Emitter.emitCurIG));
            Assert.That(compiler.fgBBcount, Is.EqualTo(blockCount + 1));
        });
    }

    [TestCase(GT_JCC)]
    [TestCase(GT_SETCC)]
    [TestCase(GT_SELECTCC)]
    public static void FlagReuseUpdatesTheOwningConsumerCondition(genTreeOps consumerOper)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.canUseAllOpts = true;
            var value = Register(compiler, TYP_INT, REG_RAX);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            zero.IsContained = true;
            var tree = new GenTreeOp(GT_CMP, TYP_VOID, value, zero) { Flags = GTF_SET_FLAGS };
            var condition = new GenCondition(GenCondition.CodeKind.SLT);
            GenTree consumer = consumerOper == GT_SELECTCC
                ? new GenTreeOpCC(GT_SELECTCC, TYP_INT, condition,
                    Register(compiler, TYP_INT, REG_RCX), Register(compiler, TYP_INT, REG_RDX))
                : new GenTreeCC(consumerOper, consumerOper == GT_SETCC ? TYP_INT : TYP_VOID, condition);
            var resolution = new GenTreeCopyOrReload(GT_COPY, TYP_INT, value);
            tree.Next = resolution;
            resolution.Next = consumer;
            codeGen.Emitter.emitIns_R_R(INS_add, EA_4BYTE, REG_RAX, REG_RCX);

            codeGen.genCodeForCompare(tree);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(consumerOper == GT_SELECTCC ? consumer.AsOpCC().Condition.Code : consumer.AsCC().Condition.Code,
                Is.EqualTo(GenCondition.CodeKind.S));
        });
    }

    [Test]
    public static void UnknownInterveningNodesPreventFlagReuse()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.canUseAllOpts = true;
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            zero.IsContained = true;
            var tree = new GenTreeOp(GT_CMP, TYP_VOID, Register(compiler, TYP_INT, REG_RAX), zero)
            {
                Flags = GTF_SET_FLAGS,
                Next = compiler.gtNewIconNode(TYP_INT, 1),
            };
            codeGen.Emitter.emitIns_R_R(INS_add, EA_4BYTE, REG_RAX, REG_RCX);

            codeGen.genCodeForCompare(tree);

            Assert.That(Descriptors(codeGen).Select(id => id.idIns()), Is.EqualTo((instruction[])[INS_add, INS_test]));
        });
    }

    [TestCase(EJ_NONE, EJ_NONE)]
    [TestCase(EJ_jmp, EJ_jmp)]
    [TestCase(EJ_jo, EJ_jno)]
    [TestCase(EJ_jbe, EJ_ja)]
    [TestCase(EJ_jp, EJ_jnp)]
    [TestCase(EJ_jle, EJ_jg)]
    public static void GeneratedReverseConditionsRetainNativePairs(emitJumpKind condition, emitJumpKind reverse)
    {
        Assert.That(Emitter.emitReverseJumpKind(condition), Is.EqualTo(reverse));
        Assert.That(Emitter.emitReverseJumpKind(reverse), Is.EqualTo(condition));
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsConditionInstructions()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.dspCode = true;
            var diagnostic = InstructionRecordingTestSupport.Capture(() =>
                codeGen.inst_SETCC(new GenCondition(GenCondition.CodeKind.FEQ), TYP_INT, REG_RAX));
            Assert.That(Descriptors(codeGen), Is.Not.Empty);
            Assert.That(diagnostic, Does.Contain("set"));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableApxZU")]
    private static extern ref int ApxZu(ref JitConfigValues config);
}
