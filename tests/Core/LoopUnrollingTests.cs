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
internal static unsafe class LoopUnrollingTests
{
    [TestCase(0, 4, 1, GT_ADD, TYP_INT, GT_LT, false, true, 4u)]
    [TestCase(0, 4, 1, GT_ADD, TYP_INT, GT_LE, false, true, 5u)]
    [TestCase(0, 4, 1, GT_ADD, TYP_INT, GT_NE, false, true, 4u)]
    [TestCase(0, 0, 1, GT_ADD, TYP_INT, GT_NE, false, true, 0u)]
    [TestCase(0, 5, 2, GT_ADD, TYP_INT, GT_NE, false, false, 0u)]
    [TestCase(5, 0, 1, GT_SUB, TYP_INT, GT_GT, false, true, 5u)]
    [TestCase(5, 0, 1, GT_SUB, TYP_INT, GT_GE, false, true, 6u)]
    [TestCase(5, 0, 1, GT_ADD, TYP_INT, GT_GT, false, false, 0u)]
    [TestCase(0, 4, 0, GT_ADD, TYP_INT, GT_LT, false, false, 0u)]
    [TestCase(0, 256, 1, GT_ADD, TYP_UBYTE, GT_LT, false, false, 0u)]
    [TestCase(0, 254, 1, GT_ADD, TYP_UBYTE, GT_LT, false, true, 254u)]
    [TestCase(0, 128, 1, GT_ADD, TYP_BYTE, GT_LT, false, false, 0u)]
    [TestCase(-128, -129, 1, GT_SUB, TYP_BYTE, GT_GT, false, false, 0u)]
    [TestCase(0, -1, 1, GT_ADD, TYP_INT, GT_LT, true, true, uint.MaxValue)]
    [TestCase(0, int.MaxValue, 1, GT_ADD, TYP_INT, GT_LT, false, true, (uint)int.MaxValue)]
    [TestCase(int.MaxValue - 1, int.MaxValue, 2, GT_ADD, TYP_INT, GT_LT, false, false, 0u)]
    [TestCase(1, 5, 2, GT_MUL, TYP_INT, GT_LT, false, false, 0u)]
    public static void IterationCountPreservesNativeWidthAndOverflow(
        int initial, int limit, int stride, genTreeOps increment, var_types type,
        genTreeOps comparison, bool unsignedTest, bool succeeds, uint expectedCount)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var result = ComputeLoopRepetitions(compiler, initial, limit, stride,
            increment, type, comparison, unsignedTest, out var count);
        Assert.That(result, Is.EqualTo(succeeds));
        if (succeeds)
        {
            Assert.That(count, Is.EqualTo(expectedCount));
        }
    }

    [Test]
    public static void ScalarReplacementPreservesStoreAndReplacesNestedUses()
    {
        WithLoop((compiler, unusedLoop, unusedPreheader, header, unusedLatch, unusedExit) =>
        {
            var store = compiler.gtNewStoreLclVarNode(0,
                compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 1)));
            var statement = compiler.gtNewStmt(store);
            compiler.fgInsertStmtAtEnd(header, statement);
            ReplaceScalarUses(compiler, header, 0, 11);

            Assert.That(statement.RootNode, Is.SameAs(store));
            Assert.That(store.LclNum, Is.Zero);
            Assert.That(store.Data.AsOp().Op1.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(store.Data.AsOp().Op1.AsIntCon().IconValue, Is.EqualTo((nint)11));
        });
    }

    [TestCase(1)]
    [TestCase(4)]
    public static void FullyUnrollsConstantLoopInIterationOrder(int iterations)
    {
        WithLoop((compiler, loop, preheader, header, latch, exit) =>
        {
            AddCountedLoopBody(compiler, preheader, header, latch, iterations);

            Assert.That(loop.AnalyzeIteration(out _), Is.True);
            var changed = false;
            var result = TryUnrollLoop(compiler, loop, ref changed);
            Assert.That(result, Is.True);
            Assert.That(changed, Is.True);
            if (iterations == 0)
            {
                Assert.That(preheader.Target, Is.SameAs(exit));
                return;
            }

            var clonedHeader = preheader.Target;
            var seen = 0;
            while (seen < iterations)
            {
                Assert.That(clonedHeader, Is.Not.SameAs(header));
                var clonedStore = clonedHeader.FirstStmt!.RootNode.AsLclVarCommon();
                Assert.That(clonedStore.Data.AsIntCon().IconValue, Is.EqualTo((nint)seen));
                var clonedLatch = clonedHeader.Target;
                Assert.That(clonedLatch.Kind, Is.EqualTo(BBJ_ALWAYS));
                Assert.That(clonedLatch.LastStmt!.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                seen++;
                if (seen == iterations)
                {
                    Assert.That(clonedLatch.Target, Is.SameAs(exit));
                }
                else
                {
                    clonedHeader = clonedLatch.Target;
                }
            }
        });
    }

    [TestCase(0x1_0000_0001L, 0, 4, GT_LT)]
    [TestCase(0xFFFF_FFFFL, 5, 0, GT_GT)]
    public static void RejectsWideIncrementBeforeCloning(long rawIncrement, int initial, int limit, genTreeOps comparison)
    {
        WithLoop((compiler, loop, preheader, header, latch, unusedExit) =>
        {
            compiler.fgInsertStmtAtEnd(preheader, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, initial))));
            compiler.fgInsertStmtAtEnd(header, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewLclvNode(TYP_INT, 0))));
            var increment = compiler.gtNewStoreLclVarNode(0,
                compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, 0),
                    compiler.gtNewIconNode(TYP_INT, unchecked((nint)rawIncrement))));
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(increment));
            var test = compiler.gtNewBinaryNode(comparison, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, limit));
            compiler.fgInsertStmtAtEnd(latch,
                compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, test)));

            Assert.That(loop.AnalyzeIteration(out var info), Is.True);
            Assert.That(info.IterConst(), Is.EqualTo(unchecked((int)rawIncrement)));
            Assert.That(increment.Data.AsOp().Op2.AsIntCon().IconValue,
                Is.EqualTo(unchecked((nint)rawIncrement)));
            Assert.That(ComputeLoopRepetitions(compiler, initial, limit, info.IterConst(),
                info.IterOper(), info.IterOperType(), info.TestOper(), false, out var count), Is.True);
            compiler.opts.compJitUnrollLoopMaxIterationCount = checked((ushort)count);

            var changed = false;
            _ = Assert.Throws<FatalJitException>(() => TryUnrollLoop(compiler, loop, ref changed));
            Assert.That(changed, Is.False);
            Assert.That(preheader.Target, Is.SameAs(header));
            Assert.That(header.Next, Is.SameAs(latch));
        });
    }

    [Test]
    public static void PhaseDiscardsUnreachableOriginalAndRecomputesLoops()
    {
        WithLoop((compiler, loop, preheader, header, latch, unusedExit) =>
        {
            AddCountedLoopBody(compiler, preheader, header, latch, 4);
            compiler._loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));

            Assert.That(compiler.optUnrollLoops(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Metrics.LoopsUnrolled, Is.EqualTo(1));
            Assert.That(compiler._loops!.NumLoops, Is.Zero);
            foreach (var block in compiler.Blocks)
            {
                Assert.That(ReferenceEquals(block, header) || ReferenceEquals(block, latch), Is.False);
            }
        });
    }

    [Test]
    public static void UnrollingInnerLoopRecomputesOuterLoopOnNextPass()
    {
        WithLoop((compiler, unusedLoop, outerPreheader, outerHeader, outerLatch, unusedExit) =>
        {
            compiler.fgInsertStmtAtEnd(outerPreheader, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            var innerPreheader = compiler.fgNewBBafter(BBJ_ALWAYS, outerHeader, extendRegion: true);
            var innerHeader = compiler.fgNewBBafter(BBJ_ALWAYS, innerPreheader, extendRegion: true);
            var innerLatch = compiler.fgNewBBafter(BBJ_COND, innerHeader, extendRegion: true);

            compiler.fgRedirectEdge(ref outerHeader.TargetEdgeRef, innerPreheader);
            innerPreheader.SetKindAndTargetEdge(BBJ_ALWAYS, NewEdge(compiler, innerPreheader, innerHeader));
            innerHeader.SetKindAndTargetEdge(BBJ_ALWAYS, NewEdge(compiler, innerHeader, innerLatch));
            innerLatch.SetCond(NewEdge(compiler, innerLatch, innerHeader),
                NewEdge(compiler, innerLatch, outerLatch));

            compiler.fgInsertStmtAtEnd(innerPreheader, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 0))));
            var innerIncrement = compiler.gtNewStoreLclVarNode(1,
                compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 1)));
            compiler.fgInsertStmtAtEnd(innerLatch, compiler.gtNewStmt(innerIncrement));
            var innerComparison = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 4));
            compiler.fgInsertStmtAtEnd(innerLatch,
                compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, innerComparison)));

            var outerIncrement = compiler.gtNewStoreLclVarNode(0,
                compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 1)));
            compiler.fgInsertStmtAtEnd(outerLatch, compiler.gtNewStmt(outerIncrement));
            var outerComparison = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 2));
            compiler.fgInsertStmtAtEnd(outerLatch,
                compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, outerComparison)));

            compiler._loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));
            Assert.That(compiler._loops.NumLoops, Is.EqualTo(2));
            Assert.That(compiler.optUnrollLoops(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Metrics.LoopsUnrolled, Is.EqualTo(2));
            Assert.That(compiler._loops!.NumLoops, Is.Zero);
            var innerIncrements = 0;
            foreach (var block in compiler.Blocks)
            {
                foreach (var statement in block.Statements)
                {
                    var root = statement.RootNode;
                    if (root.Oper is GT_STORE_LCL_VAR &&
                        root.AsLclVarCommon().LclNum == 1 &&
                        root.AsLclVarCommon().Data.Oper is GT_ADD)
                    {
                        innerIncrements++;
                    }
                }
            }
            Assert.That(innerIncrements, Is.EqualTo(8));
        });
    }

    [Test]
    public static void UnrollPreservesOtherExitsInEveryIteration()
    {
        WithLoop((compiler, unusedLoop, preheader, header, latch, exit) =>
        {
            AddCountedLoopBody(compiler, preheader, header, latch, 3);
            var earlyTest = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, -1));
            compiler.fgInsertStmtAtEnd(header,
                compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, earlyTest)));
            header.SetCond(header.TargetEdge, NewEdge(compiler, header, exit));
            var loop = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false)).GetLoopByIndex(0);
            Assert.That(loop.ExitEdges.Length, Is.EqualTo(2));

            var changed = false;
            Assert.That(TryUnrollLoop(compiler, loop, ref changed), Is.True);
            var clone = preheader.Target;
            for (var iteration = 0; iteration < 3; iteration++)
            {
                Assert.That(clone.Kind, Is.EqualTo(BBJ_COND));
                Assert.That(clone.FalseTarget, Is.SameAs(exit));
                Assert.That(clone.FirstStmt!.RootNode.AsLclVarCommon().Data.AsIntCon().IconValue,
                    Is.EqualTo((nint)iteration));
                var clonedLatch = clone.TrueTarget;
                Assert.That(clonedLatch.Kind, Is.EqualTo(BBJ_ALWAYS));
                clone = clonedLatch.Target;
            }
            Assert.That(clone, Is.SameAs(exit));
        });
    }

    [Test]
    public static void ColdLoopAndSizeLimitDoNotDuplicateBlocks()
    {
        WithLoop((compiler, loop, preheader, header, latch, unusedExit) =>
        {
            AddCountedLoopBody(compiler, preheader, header, latch, 4);
            header.bbWeight = BB_ZERO_WEIGHT;
            var changed = false;

            Assert.That(TryUnrollLoop(compiler, loop, ref changed), Is.False);
            Assert.That(changed, Is.False);
            Assert.That(preheader.Target, Is.SameAs(header));
        });

        WithLoop((compiler, loop, preheader, header, latch, unusedExit) =>
        {
            AddCountedLoopBody(compiler, preheader, header, latch, 4);
            for (var i = 0; i < 400; i++)
            {
                compiler.fgInsertStmtAtEnd(header, compiler.gtNewStmt(
                    compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, i))));
            }

            var changed = false;
            Assert.That(TryUnrollLoop(compiler, loop, ref changed), Is.False);
            Assert.That(changed, Is.True);
            Assert.That(preheader.Target, Is.SameAs(header));
        });
    }

    [Test]
    public static void UpstreamBlendedModeAndExcessIterationCount()
    {
        WithLoop((compiler, unusedLoop, preheader, header, latch, unusedExit) =>
        {
            AddCountedLoopBody(compiler, preheader, header, latch, 4);
            compiler._loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));
            compiler.opts.compCodeOpt = Compiler.SMALL_CODE;

            Assert.That(compiler.compCodeOpt, Is.EqualTo(Compiler.BLENDED_CODE));
            Assert.That(compiler.optUnrollLoops(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Metrics.LoopsUnrolled, Is.EqualTo(1));
            Assert.That(preheader.Target, Is.Not.SameAs(header));
        });

        WithLoop((compiler, loop, preheader, header, latch, unusedExit) =>
        {
            AddCountedLoopBody(compiler, preheader, header, latch, 5);
            var changed = false;

            Assert.That(TryUnrollLoop(compiler, loop, ref changed), Is.False);
            Assert.That(changed, Is.False);
            Assert.That(preheader.Target, Is.SameAs(header));
        });
    }

    [Test]
    public static void DifferingEHRegionsPreventOrdinaryDuplication()
    {
        WithLoop((compiler, loop, preheader, header, latch, unusedExit) =>
        {
            AddCountedLoopBody(compiler, preheader, header, latch, 2);
            latch.TryIndex = 0;
            Assert.That(CanDuplicateLoop(compiler, loop, false, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("Loop not entirely within one EH region"));
        });
    }

    private static void AddCountedLoopBody(
        Compiler compiler, BasicBlock preheader, BasicBlock header, BasicBlock latch, int iterations)
    {
        compiler.fgInsertStmtAtEnd(preheader, compiler.gtNewStmt(
            compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
        compiler.fgInsertStmtAtEnd(header, compiler.gtNewStmt(
            compiler.gtNewStoreLclVarNode(1, compiler.gtNewLclvNode(TYP_INT, 0))));
        var increment = compiler.gtNewStoreLclVarNode(0,
            compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 1)));
        compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(increment));
        var comparison = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
            compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, iterations));
        compiler.fgInsertStmtAtEnd(latch,
            compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, comparison)));
    }

    private static void WithLoop(
        Action<Compiler, FlowGraphNaturalLoop, BasicBlock, BasicBlock, BasicBlock, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compJitUnrollLoopMaxIterationCount = 4;
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        compiler.info.compFullName = nameof(LoopUnrollingTests);
#endif
        JitTls.Compiler = compiler;
        try
        {
            compiler.lvaCount = 2;
            compiler.lvaTable = new LclVarDsc[2];
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[1].Type = TYP_INT;
            var preheader = BasicBlock.New(compiler, BBJ_ALWAYS);
            var header = BasicBlock.New(compiler, BBJ_ALWAYS);
            var latch = BasicBlock.New(compiler, BBJ_COND);
            var exit = BasicBlock.New(compiler, BBJ_RETURN);
            preheader.bbRefs = 1;
            header.bbRefs = 0;
            latch.bbRefs = 0;
            exit.bbRefs = 0;
            preheader.Next = header;
            header.Prev = preheader;
            header.Next = latch;
            latch.Prev = header;
            latch.Next = exit;
            exit.Prev = latch;
            compiler.fgFirstBB = preheader;
            compiler.fgLastBB = exit;
            _ = Jump(preheader, header);
            _ = Jump(header, latch);
            latch.SetCond(Connect(latch, header), Connect(latch, exit));
            compiler.fgPredsComputed = true;
            var loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));
            Assert.That(loops.NumLoops, Is.EqualTo(1));
            action(compiler, loops.GetLoopByIndex(0), preheader, header, latch, exit);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optComputeLoopRep")]
    private static extern bool ComputeLoopRepetitions(Compiler compiler, int initial, int limit, int increment,
        genTreeOps incrementOper, var_types incrementType, genTreeOps comparison, bool unsignedTest, out uint count);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optTryUnrollLoop")]
    private static extern bool TryUnrollLoop(Compiler compiler, FlowGraphNaturalLoop loop, ref bool changed);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optCanDuplicateLoop")]
    private static extern bool CanDuplicateLoop(
        Compiler compiler, FlowGraphNaturalLoop loop, bool withEH, out string reason);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optReplaceScalarUsesWithConst")]
    private static extern void ReplaceScalarUses(Compiler compiler, BasicBlock block, int local, nint value);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgComputeDfs")]
    private static extern FlowGraphDfsTree ComputeDfs(Compiler compiler, bool useProfile);

    private static FlowEdge Connect(BasicBlock source, BasicBlock target)
    {
        var edge = new FlowEdge(source, target, target.bbPreds) { Likelihood = 0.5 };
        edge.incrementDupCount();
        target.bbPreds = edge;
        target.bbRefs++;
        return edge;
    }

    private static FlowEdge NewEdge(Compiler compiler, BasicBlock source, BasicBlock target)
    {
        var edge = compiler.fgAddRefPred(target, source);
        edge.Likelihood = 0.5;
        return edge;
    }

    private static FlowEdge Jump(BasicBlock source, BasicBlock target)
    {
        var edge = Connect(source, target);
        source.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
        return edge;
    }
}
