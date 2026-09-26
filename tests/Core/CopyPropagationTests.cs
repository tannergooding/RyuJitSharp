// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
using LclNumToLiveDefsMap = System.Collections.Generic.Dictionary<int, System.Collections.Generic.Stack<RyuJitSharp.Compiler.CopyPropSsaDef>>;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CopyPropagationTests
{
    [Test]
    public static void PushedDefinitionReferencesLiveSsaDescriptorAfterGrowth()
    {
        WithCompiler((compiler, store) =>
        {
            var node = NewUse(compiler, 1, store.VNForIntCon(4));
            var map = new LclNumToLiveDefsMap();
            compiler.optCopyPropPushDef(node, 1, node.SsaNum, map);
            var entry = map[1].Peek();
            var later = compiler.lvaTable[1].lvPerSsaData.AllocSsaNum();
            _ = compiler.lvaTable[1].lvPerSsaData.AllocSsaNum();
            var updated = store.VNForIntCon(9);
            compiler.lvaTable[1].GetPerSsaData(node.SsaNum)._vnPair.SetBoth(updated);

            Assert.That(entry.GetSsaDef()._vnPair.Conservative, Is.EqualTo(updated));
            Assert.That(entry.SsaNum, Is.EqualTo(node.SsaNum));
            Assert.That(later, Is.GreaterThan(entry.SsaNum));
#if DEBUG
            Assert.That(entry.GetDefNode(), Is.SameAs(node));
#endif
        });
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public static void MatchingLiveDefinitionsReplaceTheOriginalUseAndRecordSsaUse(bool live, bool replaced)
    {
        WithCompiler((compiler, store) =>
        {
            var vn = store.VNForIntCon(42);
            var old = NewUse(compiler, 0, vn);
            var source = NewUse(compiler, 1, vn);
            var block = new BasicBlock(null, null);
            var statement = compiler.gtNewStmt(old);
            var map = new LclNumToLiveDefsMap();
            compiler.optCopyPropPushDef(source, 1, source.SsaNum, map);
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            if (live)
            {
                VarSetOps.AddElemD(compiler, compiler.compCurLife, 1);
            }

            Assert.That(compiler.optCopyProp(block, statement, old, 0, map), Is.EqualTo(replaced));
            Assert.That(old.LclNum, Is.EqualTo(replaced ? 1 : 0));
            Assert.That(old.SsaNum, Is.EqualTo(replaced ? source.SsaNum : SsaConfig.FIRST_SSA_NUM));
            Assert.That(compiler.lvaTable[1].GetPerSsaData(source.SsaNum).NumUses,
                Is.EqualTo(replaced ? 1 : 0));
        });
    }

    [TestCase("different-vn")]
    [TestCase("different-type")]
    [TestCase("enregistration")]
    [TestCase("async")]
    [TestCase("exception-hint")]
    public static void ReplacementRequiresNativeEligibility(string exclusion)
    {
        WithCompiler((compiler, store) =>
        {
            var vn = store.VNForIntCon(7);
            var old = NewUse(compiler, 0, vn);
            var source = NewUse(compiler, 1, exclusion == "different-vn" ? store.VNForIntCon(8) : vn);
            var block = new BasicBlock(null, null);
            var map = new LclNumToLiveDefsMap();
            compiler.optCopyPropPushDef(source, 1, source.SsaNum, map);
            compiler.compCurLife = VarSetOps.MakeSingleton(compiler, 1);

            switch (exclusion)
            {
                case "different-type":
                {
                    compiler.lvaTable[1].Type = TYP_LONG;
                    break;
                }
                case "enregistration":
                {
                    compiler.lvaTable[1].lvDoNotEnregister = true;
                    break;
                }
                case "async":
                {
                    compiler.lvaTable[0].lvOnlyUsedOnSynchronousPath = true;
                    break;
                }
                case "exception-hint":
                {
                    compiler.lvaTable[1].lvHasExceptionalUsesHint = true;
                    break;
                }
            }

            Assert.That(compiler.optCopyProp(block, compiler.gtNewStmt(old), old, 0, map), Is.False);
            Assert.That(old.LclNum, Is.Zero);
            Assert.That(compiler.lvaTable[1].GetPerSsaData(source.SsaNum).NumUses, Is.Zero);
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void SmallSourceTypeUsesActualTypeUnlessNormalizedOnLoad(bool parameter, bool replaces)
    {
        WithCompiler((compiler, store) =>
        {
            var vn = store.VNForIntCon(7);
            var old = NewUse(compiler, 0, vn);
            var source = NewUse(compiler, 1, vn);
            compiler.lvaTable[1].Type = TYP_BYTE;
            compiler.lvaTable[1].lvIsParam = parameter;
            var map = new LclNumToLiveDefsMap();
            compiler.optCopyPropPushDef(source, 1, source.SsaNum, map);
            compiler.compCurLife = VarSetOps.MakeSingleton(compiler, 1);

            Assert.That(compiler.optCopyProp(new BasicBlock(null, null), compiler.gtNewStmt(old),
                old, 0, map), Is.EqualTo(replaces));
            Assert.That(old.LclNum, Is.EqualTo(replaces ? 1 : 0));
        });
    }

    [Test]
    public static void BlockProcessesDefinitionsInEvaluationOrderThenPopsExactlyThoseDefinitions()
    {
        WithCompiler((compiler, store) =>
        {
            var vn = store.VNForIntCon(5);
            var source = NewDef(compiler, 1, vn);
            var old = NewUse(compiler, 0, vn);
            var block = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            AddStatement(compiler, block, source);
            AddStatement(compiler, block, old);
            var map = new LclNumToLiveDefsMap();

            Assert.That(compiler.optBlockCopyProp(block, map), Is.True);
            Assert.That(old.LclNum, Is.EqualTo(1));
            Assert.That(map[1].Peek().SsaNum, Is.EqualTo(source.SsaNum));
            compiler.optBlockCopyPropPopStacks(block, map);
            Assert.That(map, Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HandlerSkipsSubstitutionButStillTracksDefinitions(bool fault)
    {
        WithCompiler((compiler, store) =>
        {
            var vn = store.VNForIntCon(5);
            var source = NewDef(compiler, 1, vn);
            var old = NewUse(compiler, 0, vn);
            var block = new BasicBlock(null, null)
            {
                CatchType = fault ? bbCatchType.BBCT_FAULT : bbCatchType.BBCT_FINALLY,
                bbLiveIn = VarSetOps.MakeEmpty(compiler),
            };
            AddStatement(compiler, block, source);
            AddStatement(compiler, block, old);
            var map = new LclNumToLiveDefsMap();

            Assert.That(compiler.optBlockCopyProp(block, map), Is.False);
            Assert.That(old.LclNum, Is.Zero);
            Assert.That(map.ContainsKey(1), Is.True);
            compiler.optBlockCopyPropPopStacks(block, map);
            Assert.That(map, Is.Empty);
        });
    }

    [Test]
    public static void ShadowedParameterIsNotAddedAsCandidate()
    {
        WithCompiler((compiler, store) =>
        {
            compiler.lvaTable[1].lvIsParam = true;
            compiler.gsShadowVarInfo =
            [
                new Compiler.ShadowParamVarInfo(),
                new Compiler.ShadowParamVarInfo { ShadowCopy = 0 },
                new Compiler.ShadowParamVarInfo(),
            ];
            var source = NewUse(compiler, 1, store.VNForIntCon(8));
            var map = new LclNumToLiveDefsMap();

            compiler.optCopyPropPushDef(source, 1, source.SsaNum, map);
            Assert.That(map, Is.Empty);
        });
    }

    [Test]
    public static void ReservedSsaDefinitionDoesNotEnterCopyStack()
    {
        WithCompiler((compiler, _) =>
        {
            var node = compiler.gtNewLclvNode(TYP_INT, 1);
            var map = new LclNumToLiveDefsMap();
            compiler.optCopyPropPushDef(node, 1, SsaConfig.RESERVED_SSA_NUM, map);
            Assert.That(map, Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PhaseRespectsSsaGateAndPropagatesThroughDominatorWalk(bool ssaBuilt)
    {
        WithCompiler((compiler, store) =>
        {
            var vn = store.VNForIntCon(5);
            var source = NewDef(compiler, 1, vn);
            var old = NewUse(compiler, 0, vn);
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            block.bbLiveIn = VarSetOps.MakeEmpty(compiler);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            AddStatement(compiler, block, source);
            AddStatement(compiler, block, old);
            compiler._domTree = FlowGraphDominatorTree.Build(compiler.fgComputeDfs());
            compiler.fgSsaPassesCompleted = ssaBuilt ? 1 : 0;

            Assert.That(compiler.optVnCopyProp(), Is.EqualTo(ssaBuilt
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(old.LclNum, Is.EqualTo(ssaBuilt ? 1 : 0));
            Assert.That(VarSetOps.MaybeUninit(compiler.compCurLife), Is.True);
        });
    }

    [Test]
    public static void DefinitionsInOneDominatorSiblingDoNotReachTheOther()
    {
        WithCompiler((compiler, store) =>
        {
            var vn = store.VNForIntCon(5);
            var root = BasicBlock.New(compiler, BBKinds.BBJ_COND);
            var first = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var second = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            root.Next = first;
            first.Next = second;
            compiler.fgFirstBB = root;
            compiler.fgLastBB = second;
            var firstEdge = new FlowEdge(root, first, first.bbPreds);
            var secondEdge = new FlowEdge(root, second, second.bbPreds);
            first.bbPreds = firstEdge;
            second.bbPreds = secondEdge;
            root.SetCond(firstEdge, secondEdge);

            root.bbLiveIn = VarSetOps.MakeEmpty(compiler);
            first.bbLiveIn = VarSetOps.MakeEmpty(compiler);
            second.bbLiveIn = VarSetOps.MakeEmpty(compiler);
            var source = NewDef(compiler, 1, vn);
            var firstUse = NewUse(compiler, 0, vn);
            var secondUse = NewUse(compiler, 0, vn);
            AddStatement(compiler, first, source);
            AddStatement(compiler, first, firstUse);
            AddStatement(compiler, second, secondUse);
            compiler._domTree = FlowGraphDominatorTree.Build(compiler.fgComputeDfs());
            compiler.fgSsaPassesCompleted = 1;

            Assert.That(compiler.optVnCopyProp(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(firstUse.LclNum, Is.EqualTo(1));
            Assert.That(secondUse.LclNum, Is.Zero);
        });
    }

    [Test]
    public static void InitialParameterUseRemainsAvailableAfterBlockExit()
    {
        WithCompiler((compiler, store) =>
        {
            compiler.lvaTable[1].lvIsParam = true;
            var vn = store.VNForIntCon(5);
            var parameter = NewUse(compiler, 1, vn);
            var old = NewUse(compiler, 0, vn);
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            block.bbLiveIn = VarSetOps.MakeSingleton(compiler, 1);
            AddStatement(compiler, block, parameter);
            AddStatement(compiler, block, old);
            compiler._domTree = FlowGraphDominatorTree.Build(compiler.fgComputeDfs());
            compiler.fgSsaPassesCompleted = 1;

            Assert.That(compiler.optVnCopyProp(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(old.LclNum, Is.EqualTo(1));
            Assert.That(old.SsaNum, Is.EqualTo(parameter.SsaNum));
        });
    }

    [Test]
    public static void NestedDefinitionsRestoreOuterCandidateAfterBlockExit()
    {
        WithCompiler((compiler, store) =>
        {
            var vn = store.VNForIntCon(5);
            var outerDef = NewDef(compiler, 1, vn);
            var innerDef = NewDef(compiler, 1, vn);
            var outer = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            var inner = new BasicBlock(null, null) { bbLiveIn = VarSetOps.MakeEmpty(compiler) };
            AddStatement(compiler, outer, outerDef);
            AddStatement(compiler, inner, innerDef);
            var map = new LclNumToLiveDefsMap();

            Assert.That(compiler.optBlockCopyProp(outer, map), Is.False);
            Assert.That(map[1].Peek().SsaNum, Is.EqualTo(outerDef.SsaNum));
            Assert.That(compiler.optBlockCopyProp(inner, map), Is.False);
            Assert.That(map[1].Peek().SsaNum, Is.EqualTo(innerDef.SsaNum));
            compiler.optBlockCopyPropPopStacks(inner, map);
            Assert.That(map[1].Peek().SsaNum, Is.EqualTo(outerDef.SsaNum));
            compiler.optBlockCopyPropPopStacks(outer, map);
            Assert.That(map, Is.Empty);
        });
    }

    [Test]
    public static void EqualCandidatesFollowNativeBucketOrderRatherThanInsertionOrder()
    {
        WithCompiler((compiler, store) =>
        {
            var vn = store.VNForIntCon(5);
            var old = NewUse(compiler, 0, vn);
            var second = NewUse(compiler, 2, vn);
            var first = NewUse(compiler, 1, vn);
            var map = new LclNumToLiveDefsMap();
            compiler.optCopyPropPushDef(second, 2, second.SsaNum, map);
            compiler.optCopyPropPushDef(first, 1, first.SsaNum, map);
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            VarSetOps.AddElemD(compiler, compiler.compCurLife, 1);
            VarSetOps.AddElemD(compiler, compiler.compCurLife, 2);

            Assert.That(compiler.optCopyProp(new BasicBlock(null, null), compiler.gtNewStmt(old),
                old, 0, map), Is.True);
            Assert.That(old.LclNum, Is.EqualTo(1));
            Assert.That(old.SsaNum, Is.EqualTo(first.SsaNum));
        });
    }

    [TestCase(false, 10)]
    [TestCase(true, 1)]
    public static void CandidateOrderRetainsNativeCollisionsAndGrowth(bool grow, int expectedLocal)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.lvaTable = new LclVarDsc[11];
            compiler.lvaCount = 11;
            compiler.lvaTrackedCount = 11;
            for (var local = 0; local < compiler.lvaCount; local++)
            {
                compiler.lvaTable[local] = new LclVarDsc { Type = TYP_INT, lvTracked = true, _varIndex = (ushort)local };
            }

            var vn = store.VNForIntCon(5);
            var old = NewUse(compiler, 0, vn);
            var map = new LclNumToLiveDefsMap();
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            int[] candidates = grow ? [7, 6, 5, 4, 3, 2, 1] : [1, 10];
            foreach (var local in candidates)
            {
                var source = NewUse(compiler, local, vn);
                compiler.optCopyPropPushDef(source, local, source.SsaNum, map);
                VarSetOps.AddElemD(compiler, compiler.compCurLife, local);
            }

            Assert.That(compiler.optCopyProp(new BasicBlock(null, null), compiler.gtNewStmt(old),
                old, 0, map), Is.True);
            Assert.That(old.LclNum, Is.EqualTo(expectedLocal));
        });
    }

    private static GenTreeLclVar NewUse(Compiler compiler, int local, int vn)
    {
        ref var descriptor = ref compiler.lvaTable[local];
        var ssa = descriptor.lvPerSsaData.AllocSsaNum();
        descriptor.GetPerSsaData(ssa)._vnPair.SetBoth(vn);
        var node = compiler.gtNewLclvNode(TYP_INT, local);
        node.SsaNum = ssa;
        node._vnPair.SetBoth(vn);
        return node;
    }

    private static GenTreeLclVar NewDef(Compiler compiler, int local, int vn)
    {
        var node = NewUse(compiler, local, vn);
        var data = compiler.gtNewIconNode(TYP_INT, 5);
        var store = compiler.gtNewStoreLclVarNode(local, data);
        store.SsaNum = node.SsaNum;
        return store;
    }

    private static void AddStatement(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = compiler.gtNewStmt(root);
        compiler.fgSetStmtSeq(statement);
        compiler.fgInsertStmtAtEnd(block, statement);
    }

    private static void WithCompiler(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compThisArg = BAD_VAR_NUM;
        compiler.lvaTable =
        [
            new LclVarDsc { Type = TYP_INT, lvTracked = true, _varIndex = 0 },
            new LclVarDsc { Type = TYP_INT, lvTracked = true, _varIndex = 1 },
            new LclVarDsc { Type = TYP_INT, lvTracked = true, _varIndex = 2 },
        ];
        compiler.lvaCount = 3;
        compiler.lvaTrackedCount = 3;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.info.compFullName = nameof(CopyPropagationTests);
        compiler.fgSafeBasicBlockCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
