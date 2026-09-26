// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LoopCloningModelTests
{
    [Test]
    public static void CandidateVariantsKeepTheirOriginalNodesAndDimensions()
    {
        WithCompiler(compiler => {
            var array = new GenTreeLclVar(TYP_REF, 0);
            var indices = new GenTree[] { new GenTreeLclVar(TYP_INT, 1), new GenTreeLclVar(TYP_INT, 2) };
            var element = new GenTreeArrElem(TYP_INT, array, 4, indices);
            var multidimensional = new LcMdArrayOptInfo(element, 1);
            var descriptor = multidimensional.GetArrIndexForDim();
            Assert.Multiple(() => {
                Assert.That(multidimensional.Type, Is.EqualTo(LcOptInfo.OptType.LcMdArray));
                Assert.That(multidimensional.GetArrIndexForDim(), Is.SameAs(descriptor));
                Assert.That(descriptor.Rank, Is.EqualTo(2));
                Assert.That(descriptor.IndLcls, Has.Count.EqualTo(1));
                Assert.That(descriptor.IndLcls[0], Is.EqualTo(1));
                Assert.That(descriptor.ArrLcl, Is.Zero);
            });

            var statement = new Statement(new GenTreeIntCon(TYP_INT, 0), 1);
            var block = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
            var bounds = new ArrIndex();
            var span = new SpanIndex();
            var indir = new GenTreeIndir(GT_IND, TYP_I_IMPL, array);
            var candidates = new LcOptInfo[]
            {
                new LcJaggedArrayOptInfo(bounds, 1, statement),
                new LcSpanOptInfo(span, statement),
                new LcTypeTestOptInfo(block, statement, indir, 0, (CORINFO_CLASS_STRUCT_*)1),
                new LcMethodAddrTestOptInfo(block, statement, indir, 0, (void*)2, true
#if DEBUG
                , (CORINFO_METHOD_STRUCT_*)3
#endif
                ),
            };
            Assert.That(candidates, Has.Length.EqualTo(4));
            Assert.That(((LcTypeTestOptInfo)candidates[2]).MethodTableIndir, Is.SameAs(indir));
            Assert.That(((LcMethodAddrTestOptInfo)candidates[3]).DelegateAddressIndir, Is.SameAs(indir));
        });
    }

    [Test]
    public static void CandidateDescriptorsSnapshotValuesAndRetainNodeReferences()
    {
        WithCompiler(compiler => {
            var bounds = new GenTreeIntCon(TYP_INT, 0);
            var statement = new Statement(bounds, 1);
            var block = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
            var array = new ArrIndex { ArrLcl = 2, ArrType = TYP_REF, Rank = 1, UseBlock = block };
            array.IndLcls.Add(3);
            array.BndsChks.Add(bounds);
            var span = new SpanIndex { LenLcl = 4, IndLcl = 3, BndsChk = bounds, UseBlock = block };
            var jaggedCandidate = new LcJaggedArrayOptInfo(array, 1, statement);
            var spanCandidate = new LcSpanOptInfo(span, statement);

            array.ArrLcl = 9;
            array.ArrType = TYP_BYREF;
            array.Rank = 2;
            array.UseBlock = null;
            span.LenLcl = 9;
            span.IndLcl = 7;
            span.BndsChk = null;
            span.UseBlock = null;

            Assert.Multiple(() => {
                Assert.That(jaggedCandidate.ArrIndex, Is.Not.SameAs(array));
                Assert.That(jaggedCandidate.ArrIndex.ArrLcl, Is.EqualTo(2));
                Assert.That(jaggedCandidate.ArrIndex.ArrType, Is.EqualTo(TYP_REF));
                Assert.That(jaggedCandidate.ArrIndex.Rank, Is.EqualTo(1));
                Assert.That(jaggedCandidate.ArrIndex.IndLcls[0], Is.EqualTo(3));
                Assert.That(jaggedCandidate.ArrIndex.BndsChks[0], Is.SameAs(bounds));
                Assert.That(jaggedCandidate.ArrIndex.UseBlock, Is.SameAs(block));
                Assert.That(jaggedCandidate.Stmt, Is.SameAs(statement));
                Assert.That(spanCandidate.SpanIndex, Is.Not.SameAs(span));
                Assert.That(spanCandidate.SpanIndex.LenLcl, Is.EqualTo(4));
                Assert.That(spanCandidate.SpanIndex.IndLcl, Is.EqualTo(3));
                Assert.That(spanCandidate.SpanIndex.BndsChk, Is.SameAs(bounds));
                Assert.That(spanCandidate.SpanIndex.UseBlock, Is.SameAs(block));
                Assert.That(spanCandidate.Stmt, Is.SameAs(statement));
            });

            var replacement = new GenTreeIntCon(TYP_INT, 1);
            array.IndLcls[0] = 5;
            array.BndsChks[0] = replacement;
            Assert.That(jaggedCandidate.ArrIndex.IndLcls[0], Is.EqualTo(5));
            Assert.That(jaggedCandidate.ArrIndex.BndsChks[0], Is.SameAs(replacement));
        });
    }

    [Test]
    public static void SymbolicIdentifiersCompareByNativeVariantAndArrayPrefix()
    {
        var first = new ArrIndex { ArrLcl = 0, ArrType = TYP_REF, Rank = 2 };
        first.IndLcls.AddRange([1, 2]);
        var second = new ArrIndex { ArrLcl = 0, ArrType = TYP_REF, Rank = 3 };
        second.IndLcls.AddRange([1, 2, 3]);
        var one = new LC_Array(LC_Array.ArrType.Jagged, first, 1, LC_Array.OperType.ArrLen);
        var two = new LC_Array(LC_Array.ArrType.Jagged, second, 1, LC_Array.OperType.ArrLen);
        Assert.That(one.Matches(two), Is.True);
        two.Dim = 2;
        Assert.That(one.Matches(two), Is.False);
        two.Dim = 1;
        two.Oper = LC_Array.OperType.None;
        Assert.That(one.Matches(two), Is.False);

        Assert.That(LC_Ident.CreateArrAccess(one, 1).Matches(LC_Ident.CreateArrAccess(one)), Is.False);
        Assert.That(LC_Ident.CreateVar(1, TYP_INT, 2).Matches(LC_Ident.CreateVar(1, TYP_INT, 2)), Is.True);
        Assert.That(LC_Ident.CreateVar(1, TYP_INT).Matches(LC_Ident.CreateVar(1, TYP_LONG)), Is.False);
        Assert.That(LC_Ident.CreateIndirOfLocal(1, 8, TYP_REF)
            .Matches(LC_Ident.CreateIndirOfLocal(1, 0, TYP_REF)), Is.False);
        Assert.That(LC_Ident.CreateNull(TYP_REF).Matches(LC_Ident.CreateNull(TYP_BYREF)), Is.True);
        Assert.That(LC_Ident.CreateClassHandle((CORINFO_CLASS_STRUCT_*)1)
            .Matches(LC_Ident.CreateClassHandle((CORINFO_CLASS_STRUCT_*)2)), Is.False);
        Assert.That(LC_Ident.CreateMethodAddr((void*)4
#if DEBUG
            , null
#endif
            ).Matches(LC_Ident.CreateIndirMethodAddrSlot((void*)4
#if DEBUG
            , null
#endif
            )), Is.False);
        var spanA = new LC_Span(new SpanIndex { LenLcl = 2, IndLcl = 3 });
        var spanB = new LC_Span(new SpanIndex { LenLcl = 2, IndLcl = 4 });
        Assert.That(LC_Ident.CreateSpanAccess(spanA).Matches(LC_Ident.CreateSpanAccess(spanB)), Is.False);
    }

    [TestCase(GT_EQ, true)]
    [TestCase(GT_GE, true)]
    [TestCase(GT_LE, true)]
    [TestCase(GT_NE, false)]
    [TestCase(GT_GT, false)]
    [TestCase(GT_LT, false)]
    public static void StaticConditionEvaluationRequiresIdenticalExpressions(genTreeOps op, bool expected)
    {
        var left = new LC_Expr(LC_Ident.CreateVar(1, TYP_INT));
        var self = new LC_Condition(op, left, left);
        Assert.That(self.Evaluates(out var result), Is.True);
        Assert.That(result, Is.EqualTo(expected));
        var distinct = new LC_Condition(op, left, new LC_Expr(LC_Ident.CreateVar(2, TYP_INT)));
        Assert.That(distinct.Evaluates(out _), Is.False);
    }

    [Test]
    public static void CombiningConditionsMatchesSameOrReversedOrderedComparisons()
    {
        var left = new LC_Expr(LC_Ident.CreateVar(1, TYP_INT));
        var right = new LC_Expr(LC_Ident.CreateVar(2, TYP_INT));
        var less = new LC_Condition(GT_LT, left, right, asUnsigned: true);
        Assert.That(less.Combines(new LC_Condition(GT_LT, left, right), out var same), Is.True);
        Assert.That(same.CompareUnsigned, Is.True);
        Assert.That(less.Combines(new LC_Condition(GT_GE, right, left), out var reversed), Is.True);
        Assert.That(reversed.Oper, Is.EqualTo(GT_LT));
        Assert.That(less.Combines(new LC_Condition(GT_GT, right, left), out _), Is.False);
        var equal = new LC_Condition(GT_EQ, left, right);
        Assert.That(equal.Combines(new LC_Condition(GT_EQ, right, left), out _), Is.False);
    }

    [Test]
    public static void ContextRetainsPerLoopArraysAndRemovesOnlyOptimizedConditions()
    {
        WithCompiler(compiler => {
            var context = new LoopCloneContext(2);
            var first = new LC_Expr(LC_Ident.CreateVar(1, TYP_INT));
            var second = new LC_Expr(LC_Ident.CreateVar(2, TYP_INT));
            var conditions = context.EnsureConditions(1);
            conditions.Add(new LC_Condition(GT_LT, first, second));
            conditions.Add(new LC_Condition(GT_GE, second, first));
            conditions.Add(new LC_Condition(GT_EQ, first, first));
            context.EvaluateConditions(1, out var allTrue, out var anyFalse);
            Assert.That(allTrue, Is.False);
            Assert.That(anyFalse, Is.False);
            context.OptimizeConditions(1);
            Assert.That(conditions, Has.Count.EqualTo(1));
            Assert.That(conditions[0].Oper, Is.EqualTo(GT_LT));

            var levels = context.EnsureBlockConditions(1, 3);
            levels[1].Add(new LC_Condition(GT_EQ, first, first));
            levels[2].Add(new LC_Condition(GT_NE, first, second));
            Assert.That(context.HasBlockConditions(1), Is.True);
            context.OptimizeBlockConditions(1);
            Assert.That(levels[1], Is.Empty);
            Assert.That(levels[2], Has.Count.EqualTo(1));
            Assert.That(context.HasBlockConditions(1), Is.True);

            var iteration = new NaturalLoopIterInfo { IterVar = 3 };
            context.SetLoopIterInfo(1, iteration);
            context.CancelLoopOptInfo(1);
            Assert.That(context.GetConditions(1), Is.Null);
            Assert.That(context.GetLoopIterInfo(1), Is.SameAs(iteration));
            Assert.That(context.HasBlockConditions(1), Is.True);
        });
    }

    [Test]
    public static void DereferenceLevelsPreserveNullCheckBeforeUnsignedBoundsAndNestedNullChecks()
    {
        var index = new ArrIndex { ArrLcl = 0, ArrType = TYP_REF, Rank = 2 };
        index.IndLcls.AddRange([1, 2]);
        var array = new LC_Array(LC_Array.ArrType.Jagged, index, LC_Array.OperType.None);
        var root = new LC_ArrayDeref(array, 0);
        root.EnsureChildren();
        var inner = new LC_ArrayDeref(array, 1);
        root.Children!.Add(inner);
        inner.EnsureChildren();
        inner.Children!.Add(new LC_ArrayDeref(array, 2));

        List<List<LC_Condition>> levels = [[], [], [], [], []];
        root.DeriveLevelConditions(levels);
        Assert.That(levels.TrueForAll(level => level.Count == 1), Is.True);
        Assert.That(levels[0][0].Oper, Is.EqualTo(GT_NE));
        Assert.That(levels[1][0].CompareUnsigned, Is.True);
        Assert.That(levels[1][0].Op2.Ident.ArrAccess.GetDimRank(), Is.Zero);
        Assert.That(levels[2][0].Op1.Ident.ArrAccess.GetDimRank(), Is.EqualTo(1));
        Assert.That(levels[3][0].Op2.Ident.ArrAccess.GetDimRank(), Is.EqualTo(1));
        Assert.That(levels[4][0].Op1.Ident.ArrAccess.GetDimRank(), Is.EqualTo(2));
        Assert.That(LC_ArrayDeref.Find(root.Children, 1), Is.SameAs(inner));
        Assert.That(LC_ArrayDeref.Find(root.Children, 2), Is.Null);
    }

    [Test]
    public static void GuardExpressionMaterializationRetainsInversionUnsignedAndHandleKinds()
    {
        WithCompiler(compiler => {
            var block = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
            var left = new LC_Expr(LC_Ident.CreateConst(1));
            var right = new LC_Expr(LC_Ident.CreateConst(2));
            var comparison = new LC_Condition(GT_LT, left, right, asUnsigned: true)
                .ToGenTree(compiler, block, invert: true);
            Assert.That(comparison.Oper, Is.EqualTo(GT_GE));
            Assert.That((comparison.Flags & GTF_UNSIGNED) != 0, Is.True);

            var classHandle = LC_Ident.CreateClassHandle((CORINFO_CLASS_STRUCT_*)1).ToGenTree(compiler, block);
            Assert.That((classHandle.Flags & GTF_ICON_CLASS_HDL) != 0, Is.True);
            var slot = LC_Ident.CreateIndirMethodAddrSlot((void*)2
#if DEBUG
                , (CORINFO_METHOD_STRUCT_*)3
#endif
                ).ToGenTree(compiler, block);
            Assert.That(slot.Oper, Is.EqualTo(GT_IND));
            Assert.That(slot.Flags & (GTF_IND_NONFAULTING | GTF_IND_INVARIANT),
                Is.EqualTo(GTF_IND_NONFAULTING | GTF_IND_INVARIANT));
        });
    }

    [Test]
    public static void IdentifierMaterializationUsesOriginalOperandTypesAndOffsets()
    {
        WithCompiler(compiler => {
            compiler.lvaCount = 3;
            compiler.lvaTable = new LclVarDsc[3];
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.lvaTable[1].Type = TYP_INT;
            compiler.lvaTable[2].Type = TYP_INT;
            var block = (BasicBlock)RuntimeHelpers.GetUninitializedObject(typeof(BasicBlock));
            var array = new LC_Array(LC_Array.ArrType.Jagged,
                new ArrIndex { ArrLcl = 0, ArrType = TYP_REF, Rank = 0 },
                LC_Array.OperType.ArrLen);
            var length = LC_Ident.CreateArrAccess(array, offset: 1).ToGenTree(compiler, block);
            Assert.That(length.Oper, Is.EqualTo(GT_ADD));
            Assert.That(length.AsOp().Op1.Oper, Is.EqualTo(GT_ARR_LENGTH));
            Assert.That(length.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)1));
            Assert.That(length.AsOp().Op1.AsArrLen().ArrRef.AsLclVar().LclNum, Is.Zero);

            var span = new LC_Span(new SpanIndex { LenLcl = 1, IndLcl = 2 });
            Assert.That(LC_Ident.CreateSpanAccess(span).ToGenTree(compiler, block)
                .AsLclVar().LclNum, Is.EqualTo(1));
            var offset = LC_Ident.CreateVar(2, TYP_INT, -2).ToGenTree(compiler, block);
            Assert.That(offset.Oper, Is.EqualTo(GT_ADD));
            Assert.That(offset.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)(-2)));
            Assert.That(LC_Ident.CreateIndirOfLocal(0, 0, TYP_REF).ToGenTree(compiler, block)
                .Oper, Is.EqualTo(GT_IND));
        });
    }

    [Test]
    public static void ConditionBlocksShortCircuitToSlowPathWithChainLikelihood()
    {
        WithCompiler(compiler => {
            compiler.compHndBBtab = [];
            compiler.info = new Compiler.Info();
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
            compiler.info.compFullName = nameof(LoopCloningModelTests);
#endif
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var fast = BasicBlock.New(compiler, BBJ_ALWAYS);
            var slow = BasicBlock.New(compiler, BBJ_ALWAYS);
            entry.Next = fast;
            fast.Prev = entry;
            fast.Next = slow;
            slow.Prev = fast;
            entry.bbRefs = 1;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = slow;
            compiler.fgPredsComputed = true;

            var left = new LC_Expr(LC_Ident.CreateConst(1));
            var right = new LC_Expr(LC_Ident.CreateConst(2));
            List<LC_Condition> conditions =
            [
                new(GT_LT, left, right),
                new(GT_NE, left, right),
            ];
            var last = new LoopCloneContext(1).CondToStmtInBlock(
                compiler, conditions, slow, entry, totalCondsInChain: 2);
            var first = entry.Next!;
            var perBlock = Math.Sqrt(LoopCloneContext.FastPathWeightScaleFactor);
            Assert.That(first.Next, Is.SameAs(last));
            Assert.That(last.Next, Is.SameAs(fast));
            Assert.That(first.TrueTarget, Is.SameAs(slow));
            Assert.That(last.TrueTarget, Is.SameAs(slow));
            Assert.That(first.FalseTarget, Is.SameAs(last));
            Assert.That(first.TrueEdge.Likelihood, Is.EqualTo(1 - perBlock).Within(1e-12));
            Assert.That(first.FalseEdge.Likelihood, Is.EqualTo(perBlock).Within(1e-12));
            Assert.That(last.TrueEdge.Likelihood, Is.EqualTo(1 - perBlock).Within(1e-12));
            Assert.That(first.FirstStmt!.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(GT_GE));
            Assert.That(last.FirstStmt!.RootNode.AsUnOp().Op1.Oper, Is.EqualTo(GT_EQ));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
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
