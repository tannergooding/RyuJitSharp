// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class SelectNodeLoweringTests
{
    [Test]
    public static void SelectReplacementPreservesOwnerIdentityAndFlagsButClearsValueNumbers()
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewIconNode(TYP_INT, 7);
            var condition = new GenTreeOp(GT_LT, TYP_INT, left, right);
            condition._vnPair.SetBoth(456);
            var first = compiler.gtNewLclvNode(TYP_INT, 2);
            var second = compiler.gtNewLclvNode(TYP_INT, 3);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, first, second);
            select.Flags |= GTF_DONT_CSE;
            select._vnPair.SetBoth(123);
            var owner = new GenTreeUnOp(GT_RETURN, TYP_INT, select);
            var block = NewBlock(left, right, condition, first, second, select, owner);

            Assert.That(LowerSelect(NewLowering(compiler, block), select), Is.SameAs(owner));

            var replacement = owner.Op1.AsOpCC();
            Assert.That(replacement, Is.Not.SameAs(select));
            Assert.That(replacement.Oper, Is.EqualTo(GT_SELECTCC));
            Assert.That(replacement.Condition.Code, Is.EqualTo(GenCondition.SLT));
            Assert.That(replacement.Flags, Is.EqualTo(select.Flags));
            Assert.That(replacement._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(replacement.Op1, Is.SameAs(first));
            Assert.That(replacement.Op2, Is.SameAs(second));
            Assert.That(condition.Oper, Is.EqualTo(GT_CMP));
            Assert.That(condition.Type, Is.EqualTo(TYP_VOID));
            Assert.That(condition.Flags & GTF_SET_FLAGS, Is.EqualTo(GTF_SET_FLAGS));
            Assert.That(condition._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(second.Next, Is.SameAs(condition));
            Assert.That(condition.Next, Is.SameAs(replacement));
            Assert.That(select.Prev is null && select.Next is null, Is.True);
#if DEBUG
            Assert.That(replacement.TreeId, Is.EqualTo(select.TreeId));
#endif
        });
    }

    [TestCase(GT_TEST_NE, GT_TEST, GenCondition.NE)]
    [TestCase(GT_BITTEST_EQ, GT_BT, GenCondition.NC)]
    public static void BitTestConditionsBecomeTheirNativeFlagProducer(
        genTreeOps sourceOper, genTreeOps loweredOper, GenCondition.CodeKind code)
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewIconNode(TYP_INT, 2);
            var condition = new GenTreeOp(sourceOper, TYP_INT, left, right);
            var first = compiler.gtNewLclvNode(TYP_INT, 2);
            var second = compiler.gtNewLclvNode(TYP_INT, 3);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, first, second);
            var owner = new GenTreeUnOp(GT_RETURN, TYP_INT, select);
            var block = NewBlock(left, right, condition, first, second, select, owner);

            _ = LowerSelect(NewLowering(compiler, block), select);

            Assert.That(condition.Oper, Is.EqualTo(loweredOper));
            Assert.That(condition.Type, Is.EqualTo(TYP_VOID));
            Assert.That(owner.Op1.AsOpCC().Condition.Code, Is.EqualTo(code));
        });
    }

    [TestCase(GT_LT, GenCondition.FGT, true)]
    [TestCase(GT_EQ, GenCondition.FEQ, false)]
    public static void FloatingConditionsSwapOnlyWhenRequiredAndRetainCompoundChecks(
        genTreeOps oper, GenCondition.CodeKind code, bool swapped)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            compiler.lvaTable[1].Type = TYP_DOUBLE;
            var left = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var right = compiler.gtNewLclvNode(TYP_DOUBLE, 1);
            var condition = new GenTreeOp(oper, TYP_INT, left, right);
            var first = compiler.gtNewLclvNode(TYP_INT, 2);
            var second = compiler.gtNewLclvNode(TYP_INT, 3);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, first, second);
            var owner = new GenTreeUnOp(GT_RETURN, TYP_INT, select);
            var block = NewBlock(left, right, condition, first, second, select, owner);

            _ = LowerSelect(NewLowering(compiler, block), select);

            Assert.That(owner.Op1.AsOpCC().Condition.Code, Is.EqualTo(code));
            Assert.That(condition.Op1, Is.SameAs(swapped ? right : left));
            Assert.That(condition.Op2, Is.SameAs(swapped ? left : right));
            Assert.That(first.IsRegOptional && second.IsRegOptional, Is.EqualTo(swapped));
            Assert.That(left.Next, Is.SameAs(right));
        });
    }

    [TestCase(false, false, GenCondition.P)]
    [TestCase(true, false, GenCondition.FNEU)]
    [TestCase(false, true, GenCondition.FNEU)]
    public static void NanCheckOptimizationRequiresOptimizationAndStableLocalReads(
        bool minOpts, bool interveningDefinition, GenCondition.CodeKind code)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            var left = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var right = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var condition = new GenTreeOp(GT_NE, TYP_INT, left, right) {
                Flags = GTF_RELOP_NAN_UN,
            };
            var first = compiler.gtNewLclvNode(TYP_INT, 2);
            var second = compiler.gtNewLclvNode(TYP_INT, 3);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, first, second);
            var owner = new GenTreeUnOp(GT_RETURN, TYP_INT, select);
            var block = NewBlock(left);
            if (interveningDefinition)
            {
                var value = compiler.gtNewDconNode(TYP_DOUBLE, 1.0);
                block.InsertAtEnd(value);
                block.InsertAtEnd(compiler.gtNewStoreLclVarNode(0, value));
            }
            foreach (var node in new GenTree[] { right, condition, first, second, select, owner })
            {
                block.InsertAtEnd(node);
            }

            _ = LowerSelect(NewLowering(compiler, block), select);

            Assert.That(owner.Op1.AsOpCC().Condition.Code, Is.EqualTo(code));
            Assert.That(first.IsRegOptional, Is.EqualTo(code is GenCondition.P));
        }, minOpts);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void SelectRemainsWhenProducingFlagsOrWhenTheConditionCannotMove(
        bool producesFlags, bool interveningDefinition)
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewIconNode(TYP_INT, 0);
            var condition = new GenTreeOp(GT_EQ, TYP_INT, left, right);
            var first = compiler.gtNewLclvNode(TYP_INT, 2);
            var second = compiler.gtNewLclvNode(TYP_INT, 3);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, first, second);
            if (producesFlags)
            {
                select.Flags |= GTF_SET_FLAGS;
            }
            var owner = new GenTreeUnOp(GT_RETURN, TYP_INT, select);
            var block = NewBlock(left, right, condition);
            if (interveningDefinition)
            {
                var value = compiler.gtNewIconNode(TYP_INT, 7);
                block.InsertAtEnd(value);
                block.InsertAtEnd(compiler.gtNewStoreLclVarNode(0, value));
            }
            foreach (var node in new GenTree[] { first, second, select, owner })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerSelect(NewLowering(compiler, block), select), Is.SameAs(owner));
            Assert.That(owner.Op1, Is.SameAs(select));
            Assert.That(condition.Oper, Is.EqualTo(GT_EQ));
            Assert.That(condition.Type, Is.EqualTo(TYP_INT));
            Assert.That(first.IsRegOptional && second.IsRegOptional, Is.True);
        });
    }

    [TestCase(0, true)]
    [TestCase(2, true)]
    [TestCase(11, false)]
    public static void SetccMovesItsBoundedProducerChainAsOneRange(int chainLength, bool converted)
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewIconNode(TYP_INT, 0);
            var producer = new GenTreeOp(GT_CMP, TYP_VOID, left, right) {
                Flags = GTF_SET_FLAGS,
            };
            var block = NewBlock(left, right);
            var chain = new List<GenTreeCCMP>();
            for (var index = 0; index < chainLength; index++)
            {
                var chainLeft = compiler.gtNewLclvNode(TYP_INT, 1);
                var chainRight = compiler.gtNewIconNode(TYP_INT, index);
                block.InsertAtEnd(chainLeft);
                block.InsertAtEnd(chainRight);
                chain.Add(new GenTreeCCMP(TYP_VOID, new GenCondition(GenCondition.EQ), chainLeft, chainRight, default) {
                    Flags = GTF_SET_FLAGS,
                });
            }
            block.InsertAtEnd(producer);
            foreach (var node in chain)
            {
                block.InsertAtEnd(node);
            }
            var condition = compiler.gtNewCC(GT_SETCC, TYP_INT, new GenCondition(GenCondition.EQ));
            var first = compiler.gtNewLclvNode(TYP_INT, 2);
            var second = compiler.gtNewLclvNode(TYP_INT, 3);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, first, second);
            var owner = new GenTreeUnOp(GT_RETURN, TYP_INT, select);
            foreach (var node in new GenTree[] { condition, first, second, select, owner })
            {
                block.InsertAtEnd(node);
            }

            _ = LowerSelect(NewLowering(compiler, block), select);

            Assert.That(owner.Op1.Oper, Is.EqualTo(converted ? GT_SELECTCC : GT_SELECT));
            if (converted)
            {
                Assert.That(second.Next, Is.SameAs(producer));
                GenTree current = producer;
                foreach (var node in chain)
                {
                    Assert.That(current.Next, Is.SameAs(node));
                    current = node;
                }
                Assert.That(current.Next, Is.SameAs(owner.Op1));
                Assert.That(condition.Prev is null && condition.Next is null, Is.True);
                Assert.That(owner.Op1.AsOpCC().Condition.Code, Is.EqualTo(GenCondition.EQ));
            }
            else
            {
                Assert.That(chain[^1].Next, Is.SameAs(condition));
                Assert.That(condition.Next, Is.SameAs(first));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SingleFlagCheckModeRejectsCompoundConditionsBeforeMutatingLir(bool setcc)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            compiler.lvaTable[1].Type = TYP_DOUBLE;
            var left = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var right = compiler.gtNewLclvNode(TYP_DOUBLE, 1);
            var producer = new GenTreeOp(setcc ? GT_CMP : GT_EQ, setcc ? TYP_VOID : TYP_INT, left, right);
            GenTree condition = producer;
            var block = NewBlock(left, right, producer);
            if (setcc)
            {
                producer.Flags |= GTF_SET_FLAGS;
                condition = compiler.gtNewCC(GT_SETCC, TYP_INT, new GenCondition(GenCondition.FEQ));
                block.InsertAtEnd(condition);
            }
            var first = compiler.gtNewLclvNode(TYP_INT, 2);
            var second = compiler.gtNewLclvNode(TYP_INT, 3);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, first, second);
            foreach (var node in new GenTree[] { first, second, select })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(TryLowerConditionToFlagsNode(NewLowering(compiler, block), select, condition,
                out var code, false), Is.False);
            Assert.That(code.Code, Is.EqualTo(GenCondition.FEQ));
            Assert.That(condition.Type, Is.EqualTo(TYP_INT));
            Assert.That(condition.Next, Is.SameAs(first));
            Assert.That(select.Cond, Is.SameAs(condition));
        });
    }

    private static BasicBlock NewBlock(params GenTree[] nodes)
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }

        return block;
    }

    private static Lowering NewLowering(Compiler compiler, BasicBlock block)
    {
        var lowering = new Lowering(compiler, new LinearScan(compiler));
        LoweringBlock(lowering) = block;

        return lowering;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerSelect")]
    private static extern GenTree? LowerSelect(Lowering lowering, GenTreeConditional select);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryLowerConditionToFlagsNode")]
    private static extern bool TryLowerConditionToFlagsNode(Lowering lowering, GenTree parent, GenTree condition,
        out GenCondition code, bool allowMultipleFlagsChecks);

    private static void WithCompiler(Action<Compiler> action, bool minOpts = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.opts.compFlags = CLFLG_REGVAR;
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 4;
        for (var index = 0; index < compiler.lvaCount; index++)
        {
            compiler.lvaTable[index].Type = TYP_INT;
        }
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
