// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RedundantBranchTests
{
    private static readonly MethodInfo s_implies = typeof(Compiler).GetMethod(
        "IsCmp2ImpliedByCmp1", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(nameof(Compiler), "IsCmp2ImpliedByCmp1");

    private static readonly MethodInfo s_reachable = typeof(Compiler).GetMethod(
        "optReachableWithBudget", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingMethodException(nameof(Compiler), "optReachableWithBudget");

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicFields)]
    private static readonly Type s_threadInfo = typeof(Compiler).GetNestedType(
        "JumpThreadInfo", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(nameof(Compiler), "JumpThreadInfo");

    private static readonly MethodInfo s_threadCore = typeof(Compiler).GetMethod(
        "optJumpThreadCore", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingMethodException(nameof(Compiler), "optJumpThreadCore");

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicFields)]
    private static readonly Type s_relopInfo = typeof(Compiler).GetNestedType(
        "RelopImplicationInfo", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(nameof(Compiler), "RelopImplicationInfo");

    private static readonly MethodInfo s_relopImplies = typeof(Compiler).GetMethod(
        "optRelopImpliesRelop", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingMethodException(nameof(Compiler), "optRelopImpliesRelop");

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "optRboBlockHasSideEffects")]
    private static extern bool BlockHasSideEffects(Compiler? _, BasicBlock block);

    [TestCase(false)]
    [TestCase(true)]
    public static void PhaseFoldsConstantBranchAndInvalidatesAnalysis(bool taken)
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var trueTarget = BasicBlock.New(compiler, BBJ_RETURN);
            var falseTarget = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = trueTarget;
            trueTarget.Next = falseTarget;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = falseTarget;
            compiler.fgPredsComputed = true;
            entry.bbRefs = 1;
            trueTarget.bbRefs = 0;
            falseTarget.bbRefs = 0;
            entry.SetCond(compiler.fgAddRefPred(trueTarget, entry),
                compiler.fgAddRefPred(falseTarget, entry));
            trueTarget.SetFlags(BasicBlockFlags.BBF_STALE_PREDICATE);
            falseTarget.SetFlags(BasicBlockFlags.BBF_STALE_PREDICATE);
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var left = compiler.gtNewIconNode(var_types.TYP_INT, 1);
            var right = compiler.gtNewIconNode(var_types.TYP_INT, taken ? 1 : 0);
            left._vnPair.SetBoth(store.VNForIntCon(1));
            right._vnPair.SetBoth(store.VNForIntCon(taken ? 1 : 0));
            var comparison = compiler.gtNewBinaryNode(GT_EQ, var_types.TYP_INT, left, right);
            comparison.Flags |= GenTreeFlags.GTF_RELOP_JMP_USED;
            comparison._vnPair.SetBoth(store.VNForIntCon(taken ? 1 : 0));
            var statement = compiler.gtNewStmt(
                compiler.gtNewUnaryNode(GT_JTRUE, var_types.TYP_VOID, comparison));
            compiler.fgInsertStmtAtEnd(entry, statement);
            compiler.fgSetStmtSeq(statement);
            compiler._domTree = FlowGraphDominatorTree.Build(compiler.fgComputeDfs());
            compiler.fgSsaValid = true;

            Assert.That(compiler.optRedundantBranches(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(entry.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(entry.Target, Is.SameAs(taken ? trueTarget : falseTarget));
            Assert.That((taken ? falseTarget : trueTarget).CountOfInEdges, Is.Zero);
            var rewrittenCondition = statement.RootNode.AsUnOp().Op1;
            Assert.That(rewrittenCondition._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(rewrittenCondition._vnPair.Conservative, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(trueTarget.HasFlag(BasicBlockFlags.BBF_STALE_PREDICATE), Is.False);
            Assert.That(falseTarget.HasFlag(BasicBlockFlags.BBF_STALE_PREDICATE), Is.False);
            Assert.That(compiler._domTree, Is.Null);
            Assert.That(compiler.fgSsaValid, Is.False);
        });
    }

    [Test]
    public static void UnchangedPhaseStillInvalidatesAnalysis()
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = entry;
            entry.bbRefs = 1;
            compiler._domTree = FlowGraphDominatorTree.Build(compiler.fgComputeDfs());
            compiler.fgSsaValid = true;

            Assert.That(compiler.optRedundantBranches(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler._domTree, Is.Null);
            Assert.That(compiler.fgSsaValid, Is.False);
        });
    }

    [TestCase(GT_GE, 100L, GT_LE, 10L, "AlwaysFalse")]
    [TestCase(GT_GE, 100L, GT_GE, 10L, "AlwaysTrue")]
    [TestCase(GT_GT, 100L, GT_GT, 100L, "AlwaysTrue")]
    [TestCase(GT_EQ, 100L, GT_NE, 100L, "AlwaysFalse")]
    [TestCase(GT_EQ, 100L, GT_NE, 101L, "AlwaysTrue")]
    [TestCase(GT_NE, 100L, GT_NE, 100L, "AlwaysTrue")]
    [TestCase(GT_NE, 100L, GT_NE, 101L, "Unknown")]
    [TestCase(GT_GT, 100L, GT_NE, 10L, "AlwaysTrue")]
    [TestCase(GT_GT, 100L, GT_NE, 101L, "Unknown")]
    [TestCase(GT_GE, 10L, GT_GE, 100L, "Unknown")]
    [TestCase(GT_LT, long.MinValue, GT_GE, 0L, "Unknown")]
    [TestCase(GT_GT, long.MaxValue, GT_LE, 0L, "Unknown")]
    public static void IntegralCompareImplicationPreservesNativeBounds(
        genTreeOps first, long firstBound, genTreeOps second, long secondBound, string expected)
    {
        var result = s_implies.Invoke(null,
            [first, (nint)firstBound, second, (nint)secondBound]);
        Assert.That(result?.ToString(), Is.EqualTo(expected));
    }

    [TestCase(VNFunc.VNF_LT_UN, false)]
    [TestCase(VNFunc.VNF_LE_UN, false)]
    [TestCase(VNFunc.VNF_GE_UN, false)]
    [TestCase(VNFunc.VNF_GT_UN, false)]
    [TestCase(VNFunc.VNF_LT_UN, true)]
    [TestCase(VNFunc.VNF_GT_UN, true)]
    public static void ExtendedValueNumberFunctionsDoNotOverflowImplication(VNFunc func, bool wrapped)
    {
        WithCompiler(compiler => {
#if DEBUG
            compiler.info.compFullName = nameof(RedundantBranchTests);
#endif
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var left = store.VNForExpr(null, var_types.TYP_INT);
            var right = store.VNForExpr(null, var_types.TYP_INT);
            var other = store.VNForExpr(null, var_types.TYP_INT);
            var zero = store.VNForIntCon(0);
            var dom = store.VNForFunc(var_types.TYP_INT, func, left, right);
            if (wrapped)
            {
                dom = store.VNForFuncNoFolding(var_types.TYP_INT, VNFunc.VNF_EQ, dom, zero);
            }
            var tree = store.VNForFunc(var_types.TYP_INT, VNFunc.VNF_EQ, other, zero);
            var info = Activator.CreateInstance(s_relopInfo)
                ?? throw new InvalidOperationException("Could not construct implication state.");
            (s_relopInfo.GetField("DomCmpNormVN") ?? throw new MissingFieldException("DomCmpNormVN")).SetValue(info, dom);
            (s_relopInfo.GetField("TreeNormVN") ?? throw new MissingFieldException("TreeNormVN")).SetValue(info, tree);
            object[] arguments = [info];

            Assert.That((int)func, Is.GreaterThan(byte.MaxValue));
            _ = s_relopImplies.Invoke(compiler, arguments);
            Assert.That((s_relopInfo.GetField("CanInfer") ?? throw new MissingFieldException("CanInfer")).GetValue(arguments[0]),
                Is.EqualTo(false));
        });
    }

    [TestCase(8, false, "Reachable")]
    [TestCase(8, true, "Unreachable")]
    [TestCase(1, false, "BudgetExceeded")]
    public static void ReachabilityHonorsExcludedBlockAndEdgeBudget(
        int budgetValue, bool excludeIntermediate, string expected)
    {
        WithCompiler(compiler => {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var intermediate = BasicBlock.New(compiler, BBJ_RETURN);
            var target = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = intermediate;
            intermediate.Next = target;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = target;
            compiler.fgPredsComputed = true;
            entry.bbRefs = 1;
            intermediate.bbRefs = 0;
            target.bbRefs = 0;
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(intermediate, entry));
            intermediate.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, intermediate));

            var budget = new[] { budgetValue };
            var result = s_reachable.Invoke(compiler,
                [entry, target, excludeIntermediate ? intermediate : null, budget]);
            Assert.That(result?.ToString(), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void ThreadingRedirectsClassifiedPredecessorsToTheirRespectiveSuccessors()
    {
        WithCompiler(compiler => {
            var truePred = BasicBlock.New(compiler, BBJ_RETURN);
            var falsePred = BasicBlock.New(compiler, BBJ_RETURN);
            var test = BasicBlock.New(compiler, BBJ_RETURN);
            var trueTarget = BasicBlock.New(compiler, BBJ_RETURN);
            var falseTarget = BasicBlock.New(compiler, BBJ_RETURN);
            truePred.Next = falsePred;
            falsePred.Next = test;
            test.Next = trueTarget;
            trueTarget.Next = falseTarget;
            compiler.fgFirstBB = truePred;
            compiler.fgLastBB = falseTarget;
            compiler.fgPredsComputed = true;
            truePred.bbRefs = 1;
            falsePred.bbRefs = 0;
            test.bbRefs = 0;
            trueTarget.bbRefs = 0;
            falseTarget.bbRefs = 0;
            truePred.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(test, truePred));
            falsePred.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(test, falsePred));
            test.SetCond(compiler.fgAddRefPred(trueTarget, test),
                compiler.fgAddRefPred(falseTarget, test));

            var info = Activator.CreateInstance(s_threadInfo, [test])
                ?? throw new InvalidOperationException("Could not construct jump-thread classification.");
            (s_threadInfo.GetField("NumPreds") ?? throw new MissingFieldException("NumPreds")).SetValue(info, 2);
            (s_threadInfo.GetField("NumTruePreds") ?? throw new MissingFieldException("NumTruePreds")).SetValue(info, 1);
            (s_threadInfo.GetField("NumFalsePreds") ?? throw new MissingFieldException("NumFalsePreds")).SetValue(info, 1);
            var truePreds = s_threadInfo.GetField("TruePreds")?.GetValue(info) as HashSet<BasicBlock>
                ?? throw new MissingFieldException("JumpThreadInfo.TruePreds");
            _ = truePreds.Add(truePred);
            compiler.vnStore = new ValueNumStore(compiler);

            Assert.That(s_threadCore.Invoke(compiler, [info]), Is.EqualTo(true));
            Assert.Multiple(() => {
                Assert.That(truePred.Target, Is.SameAs(trueTarget));
                Assert.That(falsePred.Target, Is.SameAs(falseTarget));
                Assert.That(test.CountOfInEdges, Is.Zero);
                Assert.That(compiler.fgModified, Is.True);
            });
        });
    }

    [Test]
    public static void SideEffectFreeBlockTraversalInspectsLirNodes()
    {
        WithCompiler(compiler => {
            var empty = BasicBlock.New(compiler, BBJ_RETURN);
            empty.MakeLir(null, null);
            Assert.That(BlockHasSideEffects(null, empty), Is.False);

            var check = compiler.gtNewNullCheck(compiler.gtNewIconNode(var_types.TYP_REF, 0));
            var checkedBlock = BasicBlock.New(compiler, BBJ_RETURN);
            checkedBlock.MakeLir(check, check);
            Assert.That(BlockHasSideEffects(null, checkedBlock), Is.True);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
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
