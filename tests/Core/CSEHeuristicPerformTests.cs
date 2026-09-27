// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSEHeuristicPerformTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEattempt")]
    private static extern ref int Attempt(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSECandidateCount")]
    private static extern ref int CandidateCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEcount")]
    private static extern ref int PromotionCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "sortTab")]
    private static extern ref CSEdsc?[]? SortedCandidates(CSE_HeuristicCommon heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "aggressiveRefCnt")]
    private static extern ref double AggressiveCutoff(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "moderateRefCnt")]
    private static extern ref double ModerateCutoff(CSE_Heuristic heuristic);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitCSEHash")]
    private static extern ref int CseHash(ref JitConfigValues config);
#endif

    [Test]
    public static void BaseInitializationDispatchesToStandardHeuristic()
    {
        WithCompiler(compiler =>
        {
            compiler.lvaTable = [];
            CSE_HeuristicCommon heuristic = new CSE_Heuristic(compiler);

            heuristic.Initialize();

            var standard = (CSE_Heuristic)heuristic;
            Assert.Multiple(() =>
            {
                Assert.That(AggressiveCutoff(standard), Is.EqualTo(BB_UNITY_WEIGHT / 2));
                Assert.That(ModerateCutoff(standard), Is.EqualTo(BB_UNITY_WEIGHT));
            });
        });
    }

    [Test]
    public static void ConsiderCandidatesCountsNonviableAttemptsInNativeOrder()
    {
        WithCompiler(compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var descriptor = new CSEdsc(tree, compiler.gtNewStmt(tree), block) { csdIndex = 1 };
            var heuristic = new CSE_Heuristic(compiler);
            SortedCandidates(heuristic) = [descriptor, descriptor];
            CandidateCount(compiler) = 2;

            heuristic.ConsiderCandidates();

            Assert.Multiple(() =>
            {
                Assert.That(Attempt(compiler), Is.EqualTo(2));
                Assert.That(heuristic.MadeChanges(), Is.False);
            });
        });
    }

    [TestCase(1)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(64)]
    public static void PerformCseReplacesDefinitionAndUseWithSameSsaLocal(int index)
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            compiler.lvaTable = [];
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            var vn = store.VNForIntCon(42);
            var definition = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 20), compiler.gtNewIconNode(TYP_INT, 22));
            var use = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 21), compiler.gtNewIconNode(TYP_INT, 21));
            definition._vnPair.SetBoth(vn);
            use._vnPair.SetBoth(vn);
            definition._cseNum = (sbyte)-index;
            use._cseNum = (sbyte)index;
            var definitionStatement = compiler.gtNewStmt(definition);
            var useStatement = compiler.gtNewStmt(use);
            var descriptor = new CSEdsc(definition, definitionStatement, block) { csdIndex = index };
            descriptor.csdTreeList.tslNext = new treeStmtLst(use, useStatement, block);
            descriptor.csdTreeLast = descriptor.csdTreeList.tslNext;
            var heuristic = new CSE_Heuristic(compiler);
            var candidate = new CSE_Candidate(heuristic, descriptor);

            heuristic.PerformCSE(candidate);

            var replacement = definitionStatement.RootNode.AsOp();
            var def = replacement.Op1.AsLclVar();
            var defUse = replacement.Op2.AsLclVar();
            var rewrittenUse = useStatement.RootNode.AsLclVar();
            Assert.Multiple(() =>
            {
                Assert.That(replacement.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(def.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(def.Data, Is.SameAs(definition));
                Assert.That(def.LclNum, Is.EqualTo(defUse.LclNum));
                Assert.That(rewrittenUse.LclNum, Is.EqualTo(def.LclNum));
                Assert.That(def.SsaNum, Is.EqualTo(defUse.SsaNum));
                Assert.That(rewrittenUse.SsaNum, Is.EqualTo(def.SsaNum));
                Assert.That(defUse._vnPair.Liberal, Is.EqualTo(vn));
                Assert.That(rewrittenUse._vnPair.Conservative, Is.EqualTo(vn));
                Assert.That(def._cseNum, Is.EqualTo(-index));
                Assert.That(definition._cseNum, Is.Zero);
                Assert.That(use._cseNum, Is.Zero);
                Assert.That(compiler.lvaGetDesc(def.LclNum).lvIsCSE, Is.True);
                Assert.That(PromotionCount(compiler), Is.EqualTo(1));
#if DEBUG
                Assert.That(compiler.lvaGetDesc(def.LclNum).lvReason, Is.EqualTo(FMT_CSE(index)));
#endif
            });
        });
    }

    [TestCase(42, 43, 42, 0, 1)]
    [TestCase(300, 20, 20, 280, 0)]
    public static void SharedConstantsChooseNativeBaseAndRetainOccurrenceValues(
        int definitionValue, int useValue, int expectedBase, int expectedDefDelta, int expectedUseDelta)
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            compiler.lvaTable = [];
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            var definition = compiler.gtNewIconNode(TYP_INT, definitionValue);
            var use = compiler.gtNewIconNode(TYP_INT, useValue);
            definition._vnPair.SetBoth(store.VNForIntCon(definitionValue));
            use._vnPair.SetBoth(store.VNForIntCon(useValue));
            definition._cseNum = -1;
            use._cseNum = 1;
            var definitionStatement = compiler.gtNewStmt(definition);
            var useStatement = compiler.gtNewStmt(use);
            var descriptor = new CSEdsc(definition, definitionStatement, block)
            {
                csdIndex = 1,
                csdIsSharedConst = true,
            };
            descriptor.csdTreeList.tslNext = new treeStmtLst(use, useStatement, block);
            descriptor.csdTreeLast = descriptor.csdTreeList.tslNext;

            var heuristic = new CSE_Heuristic(compiler);
            heuristic.PerformCSE(new CSE_Candidate(heuristic, descriptor));

            var definitionReplacement = definitionStatement.RootNode.AsOp();
            var tempStore = definitionReplacement.Op1.AsLclVar();
            Assert.Multiple(() =>
            {
                Assert.That(descriptor.csdConstDefValue, Is.EqualTo((nint)expectedBase));
                Assert.That(descriptor.csdConstDefVN, Is.EqualTo(store.VNForIntCon(expectedBase)));
                Assert.That(tempStore.Data.AsIntCon().IconValue, Is.EqualTo((nint)expectedBase));
                Assert.That(definitionReplacement.EffectiveVal.Oper,
                    Is.EqualTo(expectedDefDelta == 0 ? GT_LCL_VAR : GT_ADD));
                Assert.That(useStatement.RootNode.Oper,
                    Is.EqualTo(expectedUseDelta == 0 ? GT_LCL_VAR : GT_ADD));
                Assert.That(definitionReplacement._vnPair.Liberal, Is.EqualTo(store.VNForIntCon(definitionValue)));
                Assert.That(useStatement.RootNode._vnPair.Liberal, Is.EqualTo(store.VNForIntCon(useValue)));
            });

            if (expectedDefDelta != 0)
            {
                Assert.That(definitionReplacement.EffectiveVal.AsOp().Op2.AsIntCon().IconValue,
                    Is.EqualTo((nint)expectedDefDelta));
            }

            if (expectedUseDelta != 0)
            {
                Assert.That(useStatement.RootNode.AsOp().Op2.AsIntCon().IconValue,
                    Is.EqualTo((nint)expectedUseDelta));
            }
        });
    }

    [Test]
    public static void RewritingUseRetainsNestedStoreBeforeCseLocal()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            compiler.lvaCount = 1;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            var vn = store.VNForIntCon(42);
            var definition = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 20), compiler.gtNewIconNode(TYP_INT, 22));
            var nestedStore = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 17));
            nestedStore._vnPair.SetBoth(store.VNForIntCon(17));
            var use = compiler.gtNewCommaNode(TYP_INT, nestedStore, compiler.gtNewIconNode(TYP_INT, 42));
            use.Flags |= GTF_ASG;
            definition._vnPair.SetBoth(vn);
            use._vnPair.SetBoth(vn);
            definition._cseNum = -1;
            use._cseNum = 1;
            var definitionStatement = compiler.gtNewStmt(definition);
            var useStatement = compiler.gtNewStmt(use);
            var descriptor = new CSEdsc(definition, definitionStatement, block) { csdIndex = 1 };
            descriptor.csdTreeList.tslNext = new treeStmtLst(use, useStatement, block);

            var heuristic = new CSE_Heuristic(compiler);
            heuristic.PerformCSE(new CSE_Candidate(heuristic, descriptor));

            var rewritten = useStatement.RootNode.AsOp();
            Assert.Multiple(() =>
            {
                Assert.That(rewritten.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(rewritten.Op1, Is.SameAs(nestedStore));
                Assert.That(rewritten.Op2.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(rewritten.Op2.AsLclVar().LclNum,
                    Is.EqualTo(definitionStatement.RootNode.AsOp().Op1.AsLclVar().LclNum));
                Assert.That(rewritten._vnPair.Liberal, Is.EqualTo(vn));
                Assert.That(use._cseNum, Is.Zero);
            });
        });
    }

    [Test]
    public static void RewritingSsaUsePropagatesCheckedBoundToReachingDefinition()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            compiler.lvaTable = [];
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            var reachingVN = store.VNForExpr(null, TYP_INT);
            var checkedBoundVN = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(checkedBoundVN);
            var definition = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2));
            var use = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 2), compiler.gtNewIconNode(TYP_INT, 1));
            definition._vnPair.SetBoth(reachingVN);
            use._vnPair = new ValueNumPair(reachingVN, checkedBoundVN);
            definition._cseNum = -1;
            use._cseNum = 1;
            var definitionStatement = compiler.gtNewStmt(definition);
            var useStatement = compiler.gtNewStmt(use);
            var descriptor = new CSEdsc(definition, definitionStatement, block) { csdIndex = 1 };
            descriptor.csdTreeList.tslNext = new treeStmtLst(use, useStatement, block);

            var heuristic = new CSE_Heuristic(compiler);
            heuristic.PerformCSE(new CSE_Candidate(heuristic, descriptor));

            var rewrittenUse = useStatement.RootNode.AsLclVar();
            Assert.Multiple(() =>
            {
                Assert.That(rewrittenUse._vnPair.Conservative, Is.EqualTo(reachingVN));
                Assert.That(store.IsVNCheckedBound(reachingVN), Is.True);
                Assert.That(rewrittenUse.SsaNum,
                    Is.EqualTo(definitionStatement.RootNode.AsOp().Op1.AsLclVar().SsaNum));
            });
        });
    }

    [Test]
    public static void ConsiderCandidatesPromotesViableDefinitionAndUse()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            compiler.lvaTable = [];
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            var vn = store.VNForExpr(null, TYP_INT);
            var definition = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2));
            var use = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 2), compiler.gtNewIconNode(TYP_INT, 1));
            definition.SetCosts(9, 9);
            definition._vnPair.SetBoth(vn);
            use._vnPair.SetBoth(vn);
            definition._cseNum = -1;
            use._cseNum = 1;
            var definitionStatement = compiler.gtNewStmt(definition);
            var useStatement = compiler.gtNewStmt(use);
            var descriptor = new CSEdsc(definition, definitionStatement, block)
            {
                csdIndex = 1,
                csdDefCount = 1,
                csdUseCount = 1,
                csdDefWtCnt = 100,
                csdUseWtCnt = 100,
                defExcSetPromise = store.VNForExpr(null, TYP_INT),
            };
            descriptor.csdTreeList.tslNext = new treeStmtLst(use, useStatement, block);
            var heuristic = new CSE_Heuristic(compiler);
            SortedCandidates(heuristic) = [descriptor];
            CandidateCount(compiler) = 1;

            heuristic.ConsiderCandidates();

            Assert.Multiple(() =>
            {
                Assert.That(Attempt(compiler), Is.EqualTo(1));
                Assert.That(heuristic.MadeChanges(), Is.True);
                Assert.That(PromotionCount(compiler), Is.EqualTo(1));
                Assert.That(definitionStatement.RootNode.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(useStatement.RootNode.Oper, Is.EqualTo(GT_LCL_VAR));
            });
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        compiler.info.compFullName = nameof(CSEHeuristicPerformTests);
        CseHash(ref JitConfig) = -1;
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previous;
        }
    }
}
