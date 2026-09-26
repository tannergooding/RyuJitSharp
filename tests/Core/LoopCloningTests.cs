// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LoopCloningTests
{
    [TestCase(true, 400, 0)]
    [TestCase(false, 400, 0)]
    [TestCase(true, 0, 0)]
    [TestCase(true, 400, 100)]
    public static void SpanCandidateKeepsFastOriginalAndBoundsCheckedSlowClone(
        bool usePhase, int sizeLimit, int minimumRatio)
    {
        WithLoop((compiler, preheader, header, latch, exit) => {
            compiler.fgInsertStmtAtEnd(preheader, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            var check = new GenTreeBoundsChk(compiler.gtNewLclvNode(TYP_INT, 0),
                compiler.gtNewLclvNode(TYP_INT, 3), SpecialCodeKind.SCK_RNGCHK_FAIL);
            var comma = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT,
                check, compiler.gtNewIconNode(TYP_INT, 1));
            compiler.fgInsertStmtAtEnd(header, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(2, comma)));
            AddCountedLatch(compiler, latch, 4);
            compiler.optMethodFlags |= OMF_HAS_ARRAYREF;

            var loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));
            compiler._loops = loops;
            Assert.That(loops.GetLoopByIndex(0).AnalyzeIteration(out var iter, true), Is.True);
            Assert.That(iter.NeedsZeroTripGuard, Is.False);
#if DEBUG
            Assert.That(JitConfig.JitCloneLoops, Is.EqualTo(1));
#endif
            var configuration = JitConfig;
            SetConfig(ref configuration, "_jitCloneLoopsSizeLimit", sizeLimit);
            SetConfig(ref configuration, "_jitCloneLoopsMinPerCallRatio", minimumRatio);
            JitConfig = configuration;
            if (usePhase)
            {
                Assert.That(compiler.optCloneLoops(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
                if (sizeLimit == 0 || minimumRatio == 100)
                {
                    Assert.That(compiler.Metrics.LoopsCloned, Is.Zero);
                    Assert.That(comma.AsOp().Op1.Oper, Is.EqualTo(GT_BOUNDS_CHECK));
                    Assert.That(preheader.Target, Is.SameAs(header));
                    Assert.That(compiler.Metrics.LoopsRejectedForInsufficientBenefit,
                        Is.EqualTo(minimumRatio == 100 ? 1 : 0));
                    return;
                }
            }
            else
            {
                var context = new LoopCloneContext(loops.NumLoops);
                Assert.That(ObtainCandidates(compiler, context), Is.True);
                var loop = loops.GetLoopByIndex(0);
                Assert.That(context.GetLoopOptInfo(loop.Index), Has.Count.EqualTo(1));
                Assert.That(context.GetLoopOptInfo(loop.Index)![0], Is.TypeOf<LcSpanOptInfo>());
                Assert.That(DeriveConditions(compiler, loop, context), Is.True);
                Assert.That(context.GetConditions(loop.Index), Has.Count.EqualTo(1));
                Assert.That(context.GetConditions(loop.Index)![0].Oper, Is.EqualTo(GT_LE));
                Assert.That(context.GetConditions(loop.Index)![0].Op2.Ident.SpanAccess.SpanIndex.LenLcl,
                    Is.EqualTo(3));
                Assert.That(ComputeDerefConditions(compiler, loop, context), Is.True);
                CloneLoop(compiler, loop, context);
            }

            Assert.That(compiler.Metrics.LoopsCloned, Is.EqualTo(usePhase ? 1 : 0));
            Assert.That(comma.AsOp().Op1.Oper, Is.EqualTo(GT_NOP));
            var fastPreheader = header.Prev!;
            Assert.That(fastPreheader.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(fastPreheader.Target, Is.SameAs(header));
            var choice = fastPreheader.Prev!;
            Assert.That(choice.Kind, Is.EqualTo(BBJ_COND));
            var slowPreheader = choice.TrueTarget;
            Assert.That(slowPreheader.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(slowPreheader.Target, Is.Not.SameAs(header));
            Assert.That(slowPreheader.Target.FirstStmt!.RootNode.AsLclVarCommon().Data
                .AsOp().Op1.AsBoundsChk().Oper, Is.EqualTo(GT_BOUNDS_CHECK));
            Assert.That(preheader.Target, Is.SameAs(choice));
            Assert.That(choice.FalseTarget, Is.SameAs(fastPreheader));
            Assert.That(exit.Kind, Is.EqualTo(BBJ_RETURN));
        });
    }

    [Test]
    public static void NoCandidateRetainsTheNativeConservativePhaseStatus()
    {
        WithLoop((compiler, preheader, unusedHeader, latch, unusedExit) => {
            compiler.fgInsertStmtAtEnd(preheader, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            AddCountedLatch(compiler, latch, 4);
            compiler._loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));
#if DEBUG
            compiler.verbose = true;
            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(compiler.optCloneLoops(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING)));
            Assert.That(output, Does.Contain("*************** In optCloneLoops()"));
            Assert.That(output, Does.Contain("Considering loop L00 to clone for optimizations."));
            Assert.That(output, Does.Contain("Not checking loop L00 -- no array bounds or type tests in this method"));
            Assert.That(output, Does.Contain("  No clonable loops"));
#else
            Assert.That(compiler.optCloneLoops(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
#endif
            Assert.That(compiler.Metrics.LoopsCloned, Is.Zero);
        });
    }

    [Test]
    public static void SlowPreheaderExtendsMatchingExtentsPastDifferentTryClauses()
    {
        WithLoop((compiler, preheader, header, latch, exit) => {
            var slowPreheader = BasicBlock.New(compiler, BBJ_ALWAYS);
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = header, ebdTryLast = latch,
                    ebdHndBeg = exit, ebdHndLast = latch,
                    ebdEnclosingTryIndex = 1,
                },
                new EHblkDsc
                {
                    ebdTryBeg = header, ebdTryLast = latch,
                    ebdHndBeg = exit, ebdHndLast = exit,
                    ebdEnclosingTryIndex = 3,
                },
                new EHblkDsc
                {
                    ebdTryBeg = preheader, ebdTryLast = preheader,
                    ebdHndBeg = exit, ebdHndLast = exit,
                    ebdEnclosingTryIndex = 3,
                },
                new EHblkDsc
                {
                    ebdTryBeg = preheader, ebdTryLast = latch,
                    ebdHndBeg = header, ebdHndLast = latch,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 4;
            Assert.That(compiler.compHndBBtab[1].ebdEnclosingTryIndex, Is.EqualTo(3));
            Assert.That(EHblkDsc.ebdIsSameTry(compiler.compHndBBtab[1],
                compiler.compHndBBtab[2]), Is.False);

            ExtendEnclosingEHRegions(compiler, 2, latch, slowPreheader);

            Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(latch));
            Assert.That(compiler.compHndBBtab[0].ebdHndLast, Is.SameAs(latch));
            Assert.That(compiler.compHndBBtab[1].ebdTryLast, Is.SameAs(slowPreheader));
            Assert.That(compiler.compHndBBtab[1].ebdHndLast, Is.SameAs(exit));
            Assert.That(compiler.compHndBBtab[2].ebdTryLast, Is.SameAs(preheader));
            Assert.That(compiler.compHndBBtab[3].ebdTryLast, Is.SameAs(slowPreheader));
            Assert.That(compiler.compHndBBtab[3].ebdHndLast, Is.SameAs(slowPreheader));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void ArrayCandidateNeedsNullGuardBeforeArrayLengthGuard(bool usePhase)
    {
        WithLoop((compiler, preheader, header, latch, unusedExit) => {
            compiler.lvaTable[1].Type = TYP_REF;
            compiler.fgInsertStmtAtEnd(preheader, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            var arrLen = compiler.gtNewArrLen(TYP_INT, compiler.gtNewLclvNode(TYP_REF, 1),
                OFFSETOF__CORINFO_Array__length);
            var check = new GenTreeBoundsChk(compiler.gtNewLclvNode(TYP_INT, 0),
                arrLen, SpecialCodeKind.SCK_RNGCHK_FAIL) { InxType = TYP_INT };
            var comma = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT,
                check, compiler.gtNewIconNode(TYP_INT, 1));
            compiler.fgInsertStmtAtEnd(header, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(2, comma)));
            AddCountedLatch(compiler, latch, 4);
            compiler.optMethodFlags |= OMF_HAS_ARRAYREF;
            compiler._loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));

            var context = new LoopCloneContext(compiler._loops.NumLoops);
            Assert.That(ObtainCandidates(compiler, context), Is.True);
            var loop = compiler._loops.GetLoopByIndex(0);
            Assert.That(context.GetLoopOptInfo(loop.Index)![0], Is.TypeOf<LcJaggedArrayOptInfo>());
            Assert.That(DeriveConditions(compiler, loop, context), Is.True);
            Assert.That(ComputeDerefConditions(compiler, loop, context), Is.True);
            var deref = context.GetBlockConditions(loop.Index);
            Assert.That(deref, Has.Count.EqualTo(1));
            Assert.That(deref[0][0].Oper, Is.EqualTo(GT_NE));
            Assert.That(context.GetConditions(loop.Index)![0].Oper, Is.EqualTo(GT_LE));
            if (usePhase)
            {
                Assert.That(compiler.optCloneLoops(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
                Assert.That(compiler.Metrics.LoopsCloned, Is.EqualTo(1));
            }
            else
            {
                CloneLoop(compiler, loop, context);
            }
            Assert.That(comma.AsOp().Op1.Oper, Is.EqualTo(GT_NOP));
            Assert.That(preheader.Next!.FirstStmt!.RootNode.AsUnOp().Op1.Oper,
                Is.EqualTo(GT_EQ));
        });
    }

#if DEBUG
    [Test]
    public static void SpanCandidateAndConditionDumpsFollowNativeOrder()
    {
        WithLoop((compiler, preheader, header, latch, unusedExit) => {
            compiler.fgInsertStmtAtEnd(preheader, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            var check = new GenTreeBoundsChk(compiler.gtNewLclvNode(TYP_INT, 0),
                compiler.gtNewLclvNode(TYP_INT, 3), SpecialCodeKind.SCK_RNGCHK_FAIL);
            compiler.fgInsertStmtAtEnd(header, compiler.gtNewStmt(
                compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, check, compiler.gtNewIconNode(TYP_INT, 1))));
            AddCountedLatch(compiler, latch, 4);
            compiler.optMethodFlags |= OMF_HAS_ARRAYREF;
            compiler._loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));
            compiler.verbose = true;
            var context = new LoopCloneContext(compiler._loops.NumLoops);
            var loop = compiler._loops.GetLoopByIndex(0);

            var output = CodeGenLifeTransitionTests.Capture(() => {
                Assert.That(ObtainCandidates(compiler, context), Is.True);
                Assert.That(DeriveConditions(compiler, loop, context), Is.True);
                Assert.That(ComputeDerefConditions(compiler, loop, context), Is.True);
            });

            var candidate = output.IndexOf("Considering loop L00 to clone for optimizations.",
                StringComparison.Ordinal);
            var identified = output.IndexOf("Checking loop L00 for optimization candidates (array bounds)",
                StringComparison.Ordinal);
            var deriving = output.IndexOf("Deriving cloning conditions for L00", StringComparison.Ordinal);
            var conditions = output.IndexOf("Conditions: ", StringComparison.Ordinal);
            var derefs = output.IndexOf("No array deref conditions", StringComparison.Ordinal);
            Assert.That(candidate, Is.GreaterThanOrEqualTo(0));
            Assert.That(identified, Is.GreaterThan(candidate));
            Assert.That(deriving, Is.GreaterThan(identified));
            Assert.That(conditions, Is.GreaterThan(deriving));
            Assert.That(derefs, Is.GreaterThan(conditions));
        });
    }
#endif

    [Test]
    public static void UnknownEntryForNotEqualLoopRequiresOrderedEntryAndWrapGuards()
    {
        WithLoop((compiler, unusedPreheader, header, latch, unusedExit) => {
            var check = new GenTreeBoundsChk(compiler.gtNewLclvNode(TYP_INT, 0),
                compiler.gtNewLclvNode(TYP_INT, 3), SpecialCodeKind.SCK_RNGCHK_FAIL);
            compiler.fgInsertStmtAtEnd(header, compiler.gtNewStmt(
                compiler.gtNewBinaryNode(GT_COMMA, TYP_INT,
                    check, compiler.gtNewIconNode(TYP_INT, 0))));
            AddCountedLatch(compiler, latch, 4, GT_NE);
            compiler.optMethodFlags |= OMF_HAS_ARRAYREF;
            compiler._loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));
            var loop = compiler._loops.GetLoopByIndex(0);
            Assert.That(loop.AnalyzeIteration(out var iter, allowMissingBaseCase: true), Is.True);
            Assert.That(iter.NeedsZeroTripGuard, Is.True);

            var context = new LoopCloneContext(compiler._loops.NumLoops);
            Assert.That(ObtainCandidates(compiler, context), Is.True);
            Assert.That(DeriveConditions(compiler, loop, context), Is.True);
            var conditions = context.GetConditions(loop.Index)!;
            Assert.That(conditions[0].Oper, Is.EqualTo(GT_LT));
            Assert.That(conditions[0].Op1.Ident.LclNum, Is.Zero);
            Assert.That(conditions[^1].Oper, Is.EqualTo(GT_LE));
            Assert.That(conditions[^1].Op1.Ident.LclNum, Is.Zero);
        });
    }

    [TestCase(1, int.MaxValue, true)]
    [TestCase(2, int.MaxValue, false)]
    [TestCase(2, int.MaxValue - 1, true)]
    public static void SpanStrideOverflowRejectsUnsafeConstantLimit(
        int stride, int limit, bool expected)
    {
        WithLoop((compiler, preheader, header, latch, unusedExit) => {
            compiler.fgInsertStmtAtEnd(preheader, compiler.gtNewStmt(
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            var check = new GenTreeBoundsChk(compiler.gtNewLclvNode(TYP_INT, 0),
                compiler.gtNewLclvNode(TYP_INT, 3), SpecialCodeKind.SCK_RNGCHK_FAIL);
            var comma = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT,
                check, compiler.gtNewIconNode(TYP_INT, 0));
            compiler.fgInsertStmtAtEnd(header, compiler.gtNewStmt(comma));
            AddCountedLatch(compiler, latch, limit, stride: stride);
            compiler.optMethodFlags |= OMF_HAS_ARRAYREF;
            compiler._loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));
            var context = new LoopCloneContext(compiler._loops.NumLoops);
            Assert.That(ObtainCandidates(compiler, context), Is.True);
            var loop = compiler._loops.GetLoopByIndex(0);
            Assert.That(context.GetLoopOptInfo(loop.Index), Has.Count.EqualTo(1));
#if DEBUG
            compiler.verbose = true;
            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(DeriveConditions(compiler, loop, context), Is.EqualTo(expected)));
            if (!expected)
            {
                Assert.That(output, Does.Contain("exceeds overflow bound"));
            }
#else
            Assert.That(DeriveConditions(compiler, loop, context), Is.EqualTo(expected));
#endif
        });
    }

    [Test]
    public static void ProfiledGdvCandidateRequiresFrequentlyExecutedBiasedGuard()
    {
        WithLoop((compiler, unusedPreheader, header, latch, exit) => {
            header.setBBProfileWeight(100);
            latch.setBBProfileWeight(60);
            exit.setBBProfileWeight(2);
            var loop = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false)).GetLoopByIndex(0);
            compiler.compCurBB = latch;
            Assert.Multiple(() => {
                Assert.That(loop.Header, Is.SameAs(header));
                Assert.That(header.hasProfileWeight, Is.True);
                Assert.That(latch.hasProfileWeight, Is.True);
                Assert.That(exit.hasProfileWeight, Is.True);
                Assert.That(latch.TrueTarget, Is.SameAs(header));
            });
            var equality = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 1));
            Assert.That(GdvProfitable(compiler, equality, loop), Is.True);
            exit.setBBProfileWeight(10);
            Assert.That(GdvProfitable(compiler, equality, loop), Is.False);
            exit.setBBProfileWeight(0);
            latch.setBBProfileWeight(5);
            Assert.That(GdvProfitable(compiler, equality, loop), Is.False);
        });
    }

    [Test]
    public static void GdvClassGuardClonesWithoutCountedIterationAndPreservesFaultingSlowPath()
    {
        WithLoop((compiler, preheader, header, latch, exit) => {
            compiler.lvaTable[1].Type = TYP_REF;
            compiler.MethodHasGuardedDevirtualization = true;
            header.setBBProfileWeight(100);
            latch.setBBProfileWeight(60);
            exit.setBBProfileWeight(2);
            var indir = compiler.gtNewIndir(TYP_I_IMPL, compiler.gtNewLclvNode(TYP_REF, 1));
            var handle = compiler.gtNewIconHandleNode(123, GTF_ICON_CLASS_HDL);
            var test = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, indir, handle);
            compiler.fgInsertStmtAtEnd(latch,
                compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, test)));
            compiler._loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));
            var loop = compiler._loops.GetLoopByIndex(0);
            Assert.That(loop.AnalyzeIteration(out _, allowMissingBaseCase: true), Is.False);

            var context = new LoopCloneContext(compiler._loops.NumLoops);
            Assert.That(ObtainCandidates(compiler, context), Is.True);
            Assert.That(context.GetLoopOptInfo(loop.Index), Has.Count.EqualTo(1));
            Assert.That(context.GetLoopOptInfo(loop.Index)![0], Is.TypeOf<LcTypeTestOptInfo>());
            Assert.That(DeriveConditions(compiler, loop, context), Is.True);
            Assert.That(ComputeDerefConditions(compiler, loop, context), Is.True);
            Assert.That(context.GetBlockConditions(loop.Index)[0][0].Oper, Is.EqualTo(GT_NE));
            Assert.That(compiler.optCloneLoops(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Metrics.LoopsCloned, Is.EqualTo(1));
            Assert.That(indir.HasOrderingSideEffect, Is.True);
            Assert.That(indir.Flags & GTF_EXCEPT, Is.EqualTo(GTF_EMPTY));
            var choice = preheader.Target;
            Assert.That(choice.TrueTarget, Is.Not.SameAs(header));
            var slow = choice.TrueTarget;
            while (slow.Kind is BBJ_ALWAYS)
            {
                slow = slow.Target;
            }
            var slowIndir = slow.LastStmt!.RootNode.AsUnOp().Op1.AsOp().Op1.AsIndir();
            Assert.That(slowIndir.Flags & GTF_EXCEPT, Is.EqualTo(GTF_EXCEPT));
        });
    }

    [Test]
    public static void ReconstructingJaggedArrayStopsAfterNonReferenceElement()
    {
        WithLoop((compiler, unusedPreheader, unusedHeader, unusedLatch, unusedExit) => {
            var firstCheck = new GenTreeBoundsChk(compiler.gtNewLclvNode(TYP_INT, 0),
                compiler.gtNewArrLen(TYP_INT, compiler.gtNewLclvNode(TYP_REF, 1),
                    OFFSETOF__CORINFO_Array__length),
                SpecialCodeKind.SCK_RNGCHK_FAIL) { InxType = TYP_INT };
            var first = compiler.gtNewBinaryNode(GT_COMMA, TYP_REF,
                firstCheck, compiler.gtNewLclvNode(TYP_REF, 1));
            var store = compiler.gtNewStoreLclVarNode(2, first);
            var secondCheck = new GenTreeBoundsChk(compiler.gtNewLclvNode(TYP_INT, 0),
                compiler.gtNewArrLen(TYP_INT, compiler.gtNewLclvNode(TYP_REF, 2),
                    OFFSETOF__CORINFO_Array__length),
                SpecialCodeKind.SCK_RNGCHK_FAIL) { InxType = TYP_INT };
            var second = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT,
                secondCheck, compiler.gtNewIconNode(TYP_INT, 0));
            var chain = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, store, second);
            Assert.That(ReconstructArray(compiler, first, new ArrIndex()), Is.True);
            Assert.That(ReconstructArray(compiler, chain, new ArrIndex()), Is.False);
        });
    }

    private static void AddCountedLatch(Compiler compiler, BasicBlock latch, int limit,
        genTreeOps comparisonOper = GT_LT, int stride = 1)
    {
        var increment = compiler.gtNewStoreLclVarNode(0,
            compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, stride)));
        compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(increment));
        var comparison = compiler.gtNewBinaryNode(comparisonOper, TYP_INT,
            compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, limit));
        compiler.fgInsertStmtAtEnd(latch,
            compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, comparison)));
    }

    private static void WithLoop(Action<Compiler, BasicBlock, BasicBlock, BasicBlock, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var config = new JitConfigValues();
#if DEBUG
        SetConfig(ref config, "_jitCloneLoops", 1);
        SetConfig(ref config, "_jitCloneLoopsWithEH", 1);
        SetConfig(ref config, "_jitCloneLoopsWithGdvTests", 1);
#endif
        SetConfig(ref config, "_jitCloneLoopsSizeLimit", 400);
        JitConfig = config;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        compiler.info.compFullName = nameof(LoopCloningTests);
#endif
        JitTls.Compiler = compiler;
        try
        {
            compiler.lvaCount = 4;
            compiler.lvaTable = new LclVarDsc[4];
            for (var index = 0; index < compiler.lvaCount; index++)
            {
                compiler.lvaTable[index].Type = TYP_INT;
            }
            var preheader = BasicBlock.New(compiler, BBJ_ALWAYS);
            var header = BasicBlock.New(compiler, BBJ_ALWAYS);
            var latch = BasicBlock.New(compiler, BBJ_COND);
            var exit = BasicBlock.New(compiler, BBJ_RETURN);
            preheader.bbRefs = 1;
            preheader.Next = header;
            header.Prev = preheader;
            header.Next = latch;
            latch.Prev = header;
            latch.Next = exit;
            exit.Prev = latch;
            compiler.fgFirstBB = preheader;
            compiler.fgLastBB = exit;
            compiler.fgPredsComputed = true;
            preheader.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(compiler, preheader, header));
            header.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(compiler, header, latch));
            latch.SetCond(Connect(compiler, latch, header), Connect(compiler, latch, exit));
            action(compiler, preheader, header, latch, exit);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }

    private static void SetConfig(ref JitConfigValues config, string field, int value)
    {
        object boxed = config;
        typeof(JitConfigValues).GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(boxed, value);
        config = (JitConfigValues)boxed;
    }

    private static FlowEdge Connect(Compiler compiler, BasicBlock source, BasicBlock target)
    {
        var edge = compiler.fgAddRefPred(target, source);
        edge.Likelihood = 0.5;
        return edge;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgComputeDfs")]
    private static extern FlowGraphDfsTree ComputeDfs(Compiler compiler, bool useProfile);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optObtainLoopCloningOpts")]
    private static extern bool ObtainCandidates(Compiler compiler, LoopCloneContext context);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optDeriveLoopCloningConditions")]
    private static extern bool DeriveConditions(Compiler compiler, FlowGraphNaturalLoop loop, LoopCloneContext context);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optComputeDerefConditions")]
    private static extern bool ComputeDerefConditions(Compiler compiler, FlowGraphNaturalLoop loop, LoopCloneContext context);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optCloneLoop")]
    private static extern void CloneLoop(Compiler compiler, FlowGraphNaturalLoop loop, LoopCloneContext context);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optExtendEnclosingEHRegions")]
    private static extern void ExtendEnclosingEHRegions(Compiler compiler, ushort enclosingRegion,
        BasicBlock beforeSlowPreheader, BasicBlock slowPreheader);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optReconstructArrIndex")]
    private static extern bool ReconstructArray(Compiler compiler, GenTree tree, ArrIndex index);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optCheckLoopCloningGDVTestProfitable")]
    private static extern bool GdvProfitable(Compiler compiler, GenTreeOp guard, FlowGraphNaturalLoop loop);
}
