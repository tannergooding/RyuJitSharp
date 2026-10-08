// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LoopHoistingTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optHoistLoopCode")]
    private static extern PhaseStatus Hoist(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optPerformHoistExpr")]
    private static extern void HoistExpression(Compiler compiler, GenTree tree, BasicBlock block,
        FlowGraphNaturalLoop loop);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optCopyLoopMemoryDependence")]
    private static extern void CopyMemoryDependence(Compiler compiler, GenTree source, GenTree clone);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitHoistLimit")]
    private static extern ref int HoistLimit(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitNoHoist")]
    private static extern ref int NoHoist(ref JitConfigValues config);
#endif

    [Test]
    public static void NoLoopsDoesNotAccessSideEffectAnalysis()
    {
        WithCompiler(compiler => {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = entry;
            compiler._loops = FlowGraphNaturalLoops.Find(compiler.fgComputeDfs(false));
#if DEBUG
            compiler.verbose = true;
            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(Hoist(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING)));
            Assert.That(output, Does.Contain("No loops; no hoisting"));
#else
            Assert.That(Hoist(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
#endif
            Assert.That(compiler._loopSideEffects, Is.Null);
        });
    }

    [Test]
    public static void EmptyLoopPreservesGraphAndReportsNoHoisting()
    {
        WithLoop((compiler, preheader, header, latch) => {
            var first = preheader.FirstStmt;
            var traits = compiler._dfsTree!.PostOrderTraits();
            var empty = VarSetOps.MakeEmpty(compiler);
            compiler._loopSideEffects = [new LoopSideEffects
            {
                VarInOut = empty,
                VarUseDef = VarSetOps.MakeEmpty(compiler),
            }];
            Assert.That(traits, Is.Not.Null);

            Assert.That(Hoist(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(preheader.FirstStmt, Is.SameAs(first));
            Assert.That(header.FirstStmt, Is.Null);
            Assert.That(latch.FirstStmt, Is.Null);
            Assert.That(compiler.Metrics.HoistedExpressions, Is.Zero);
        });
    }

    [Test]
    public static void CopyingHoistedExpressionPreservesMemoryDependenceForEachOperand()
    {
        WithCompiler(compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var left = compiler.gtNewIconNode(TYP_INT, 3);
            var right = compiler.gtNewIconNode(TYP_INT, 4);
            var source = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, left, right);
            var clone = compiler.gtCloneExpr(source)!;
            var map = compiler.NodeToLoopMemoryBlockMap;
            map.Add(source, block);
            map.Add(right, block);

            CopyMemoryDependence(compiler, source, clone);

            Assert.That(map[clone], Is.SameAs(block));
            Assert.That(map[clone.AsOp().Op2], Is.SameAs(block));
            Assert.That(map.ContainsKey(clone.AsOp().Op1), Is.False);
        });
    }

    [Test]
    public static void InsertionMarksHoistedCloneForCseAndDoesNotReplaceOriginal()
    {
        WithLoop((compiler, preheader, header, unusedLatch) => {
            var tree = compiler.gtNewIconNode(TYP_INT, 73);
            var loop = compiler._loops!.GetLoopByIndex(0);

            HoistExpression(compiler, tree, header, loop);

            Assert.That(preheader.FirstStmt, Is.Not.Null);
            Assert.That(preheader.FirstStmt!.RootNode.Oper, Is.EqualTo(GT_COMMA));
            var clone = preheader.FirstStmt.RootNode.AsOp().Op1;
            Assert.That(clone, Is.Not.SameAs(tree));
            Assert.That(clone.Flags & GTF_MAKE_CSE, Is.EqualTo(GTF_MAKE_CSE));
            Assert.That(tree.Flags & GTF_MAKE_CSE, Is.EqualTo(GTF_EMPTY));
            Assert.That(compiler.Metrics.HoistedExpressions, Is.Zero);
        });
    }

    [Test]
    public static void InvariantExpressionInDefinitelyExecutedHeaderIsHoisted()
    {
        WithLoop((compiler, preheader, header, unusedLatch) => {
            var tree = AddInvariantCandidate(compiler, header);

#if DEBUG
            compiler.verbose = true;
            var status = PhaseStatus.MODIFIED_NOTHING;
            var output = CodeGenLifeTransitionTests.Capture(() => status = Hoist(compiler));
            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING), output);
            Assert.That(output, Does.Contain($"PostOrderVisit for [{tree.TreeId:D6}] MUL"));
#else
            Assert.That(Hoist(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
#endif
            Assert.That(compiler.Metrics.HoistedExpressions, Is.EqualTo(1));
            Assert.That(preheader.FirstStmt!.RootNode.AsOp().Op1.Flags & GTF_MAKE_CSE,
                Is.EqualTo(GTF_MAKE_CSE));
            Assert.That(header.FirstStmt!.RootNode, Is.SameAs(tree));
        });
    }

    [Test]
    public static void CheapInvariantExpressionIsNotHoistedUnderRegisterPressure()
    {
        WithLoop((compiler, preheader, header, unusedLatch) => {
            var tree = AddInvariantCandidate(compiler, header);
            tree.SetCosts(1, 1);
            compiler._loopSideEffects![0].ContainsCall = true;

            Assert.That(Hoist(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(preheader.FirstStmt, Is.Null);
            Assert.That(compiler.Metrics.HoistedExpressions, Is.Zero);
        });
    }

#if DEBUG
    [TestCase(1)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public static void NonzeroNoHoistConfigurationDisablesHoisting(int configuration)
    {
        WithLoop((compiler, preheader, header, unusedLatch) => {
            _ = AddInvariantCandidate(compiler, header);
            NoHoist(ref JitConfig) = configuration;

            Assert.That(Hoist(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(preheader.FirstStmt, Is.Null);
            Assert.That(compiler.Metrics.HoistedExpressions, Is.Zero);
        });
    }

    [Test]
    public static void DuplicateValueNumberReportsNativeHexDiagnostic()
    {
        WithLoop((compiler, preheader, header, unusedLatch) => {
            var first = AddInvariantCandidate(compiler, header);
            var duplicate = compiler.gtCloneExpr(first)!;
            compiler.fgInsertStmtAtEnd(header, compiler.fgNewStmtFromTree(duplicate));
            compiler.verbose = true;

            var status = PhaseStatus.MODIFIED_NOTHING;
            var output = CodeGenLifeTransitionTests.Capture(() => status = Hoist(compiler));

            Assert.That(first._vnPair.Liberal, Is.GreaterThan(9));
            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING), output);
            Assert.That(compiler.Metrics.HoistedExpressions, Is.EqualTo(1));
            Assert.That(preheader.FirstStmt!.NextStmt, Is.Null);
            Assert.That(output, Does.Contain(
                $"      [{duplicate.TreeId:D6}] ... already hoisted ${first._vnPair.Liberal:x} in L00"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CandidateRespectsHoistLimitAndTryRegion(bool differentTry)
    {
        WithLoop((compiler, preheader, header, unusedLatch) => {
            _ = AddInvariantCandidate(compiler, header);
            if (differentTry)
            {
                header.bbTryIndex = 1;
            }
            else
            {
                HoistLimit(ref JitConfig) = 0;
            }
            compiler.verbose = true;
            var status = PhaseStatus.MODIFIED_EVERYTHING;
            var output = CodeGenLifeTransitionTests.Capture(() => status = Hoist(compiler));

            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(preheader.FirstStmt, Is.Null);
            Assert.That(compiler.Metrics.HoistedExpressions, Is.Zero);
            Assert.That(output, Does.Contain(differentTry ? "eh region constraint" : "JitHoistLimit 0"));
        });
    }
#endif

    private static GenTreeOp AddInvariantCandidate(Compiler compiler, BasicBlock header)
    {
        compiler.vnStore = new ValueNumStore(compiler);
        var left = compiler.gtNewIconNode(TYP_INT, 3);
        var right = compiler.gtNewIconNode(TYP_INT, 4);
        var tree = compiler.gtNewBinaryNode(GT_MUL, TYP_INT, left, right);
        left._vnPair.SetBoth(compiler.vnStore.VNForIntCon(3));
        right._vnPair.SetBoth(compiler.vnStore.VNForIntCon(4));
        tree._vnPair.SetBoth(compiler.vnStore.VNForFunc(TYP_INT, VNF_InitVal,
            compiler.vnStore.VNForIntCon(0)));
        tree.SetCosts(20, 20);
        header.bbWeight = BB_UNITY_WEIGHT;
        compiler.fgInsertStmtAtEnd(header, compiler.fgNewStmtFromTree(tree));
        compiler._loopSideEffects = [new LoopSideEffects
        {
            VarInOut = VarSetOps.MakeEmpty(compiler),
            VarUseDef = VarSetOps.MakeEmpty(compiler),
        }];
        return tree;
    }

    private static void WithLoop(Action<Compiler, BasicBlock, BasicBlock, BasicBlock> action)
    {
        WithCompiler(compiler => {
            var preheader = BasicBlock.New(compiler, BBJ_ALWAYS);
            var header = BasicBlock.New(compiler, BBJ_ALWAYS);
            var latch = BasicBlock.New(compiler, BBJ_COND);
            var exit = BasicBlock.New(compiler, BBJ_RETURN);
            preheader.Next = header;
            header.Prev = preheader;
            header.Next = latch;
            latch.Prev = header;
            latch.Next = exit;
            exit.Prev = latch;
            compiler.fgFirstBB = preheader;
            compiler.fgLastBB = exit;
            compiler.fgPredsComputed = true;
            preheader.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(header, preheader));
            header.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(latch, header));
            latch.SetCond(compiler.fgAddRefPred(header, latch), compiler.fgAddRefPred(exit, latch));
            compiler._dfsTree = compiler.fgComputeDfs(false);
            compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            action(compiler, preheader, header, latch);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
#if DEBUG
        HoistLimit(ref JitConfig) = -1;
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
#if DEBUG
        compiler.info.compFullName = nameof(LoopHoistingTests);
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
            JitConfig = previousConfig;
        }
    }
}
