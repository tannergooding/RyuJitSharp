// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ConditionalBranchLoweringTests
{
    [TestCase(GT_LT, TYP_INT, GT_CMP, GenCondition.SLT, false)]
    [TestCase(GT_GE, TYP_LONG, GT_CMP, GenCondition.SGE, false)]
    [TestCase(GT_EQ, TYP_DOUBLE, GT_CMP, GenCondition.FEQ, false)]
    [TestCase(GT_LT, TYP_DOUBLE, GT_CMP, GenCondition.FGT, true)]
    [TestCase(GT_TEST_EQ, TYP_INT, GT_TEST, GenCondition.EQ, false)]
    [TestCase(GT_BITTEST_EQ, TYP_INT, GT_BT, GenCondition.NC, false)]
    public static void ComparisonBecomesFlagProducerAndBranchPreservesIdentity(
        genTreeOps oper, var_types type, genTreeOps loweredOper, GenCondition.CodeKind code, bool swapped)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[1].Type = type;
            var left = compiler.gtNewLclvNode(type, 0);
            var right = compiler.gtNewLclvNode(type, 1);
            var condition = new GenTreeOp(oper, TYP_INT, left, right);
            var branch = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
            branch.Flags |= GTF_DONT_CSE;
            branch._vnPair.SetBoth(123);
            var block = NewBlock(left, right, condition, branch);

            Assert.That(LowerJTrue(NewLowering(compiler, block), branch), Is.Null);

            var replacement = block.LastNode as GenTreeCC
                ?? throw new AssertionException("JTRUE was not replaced with JCC.");
            Assert.That(replacement.Oper, Is.EqualTo(GT_JCC));
            Assert.That(replacement.Type, Is.EqualTo(TYP_VOID));
            Assert.That(replacement.Condition.Code, Is.EqualTo(code));
            Assert.That(replacement.Flags, Is.EqualTo(branch.Flags));
            Assert.That(replacement._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(replacement._vnPair.Conservative, Is.EqualTo(ValueNumStore.NoVN));
#if DEBUG
            Assert.That(replacement.TreeId, Is.EqualTo(branch.TreeId));
#endif
            Assert.That(condition.Oper, Is.EqualTo(loweredOper));
            Assert.That(condition.Type, Is.EqualTo(TYP_VOID));
            Assert.That(condition.Flags & GTF_SET_FLAGS, Is.EqualTo(GTF_SET_FLAGS));
            Assert.That(condition.Op1, Is.SameAs(swapped ? right : left));
            Assert.That(condition.Op2, Is.SameAs(swapped ? left : right));
            Assert.That(condition.Next, Is.SameAs(replacement));
            Assert.That(branch.Prev is null && branch.Next is null, Is.True);
        });
    }

    [Test]
    public static void ExistingFlagProducerIsReusedWithoutMaterializingBoolean()
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewIconNode(TYP_INT, 7);
            var flags = new GenTreeOp(GT_CMP, TYP_VOID, left, right) {
                Flags = GTF_SET_FLAGS,
            };
            var condition = new GenTreeCC(GT_SETCC, TYP_INT, new GenCondition(GenCondition.NE));
            var branch = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
            var block = NewBlock(left, right, flags, condition, branch);

            Assert.That(LowerJTrue(NewLowering(compiler, block), branch), Is.Null);

            Assert.That(flags.Next, Is.SameAs(block.LastNode));
            Assert.That(flags.Next?.AsCC().Condition.Code, Is.EqualTo(GenCondition.NE));
            Assert.That(condition.Prev is null && condition.Next is null, Is.True);
            Assert.That(branch.Prev is null && branch.Next is null, Is.True);
        });
    }

    [Test]
    public static void NonFlagConditionRetainsOriginalBranch()
    {
        WithCompiler(compiler => {
            var condition = compiler.gtNewLclvNode(TYP_INT, 0);
            var branch = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
            branch._vnPair.SetBoth(123);
            var block = NewBlock(condition, branch);

            Assert.That(LowerJTrue(NewLowering(compiler, block), branch), Is.Null);

            Assert.That(block.LastNode, Is.SameAs(branch));
            Assert.That(condition.Next, Is.SameAs(branch));
            Assert.That(branch.Op1, Is.SameAs(condition));
            Assert.That(branch.Oper, Is.EqualTo(GT_JTRUE));
            Assert.That(branch._vnPair.Liberal, Is.EqualTo(123u));
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerJTrue")]
    private static extern GenTree? LowerJTrue(Lowering lowering, GenTreeUnOp branch);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_INT;
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
