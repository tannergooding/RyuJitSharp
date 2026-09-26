// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ScalarEvolutionTests
{
    [TestCase(TYP_INT, int.MaxValue, 1, int.MinValue)]
    [TestCase(TYP_INT, -1, 2, 1)]
    [TestCase(TYP_LONG, long.MaxValue, 1, long.MinValue)]
    [TestCase(TYP_LONG, -1, 2, 1)]
    public static void FoldingPreservesNativeWidths(var_types type, long left, long right, long expected)
    {
        WithLoop((compiler, context, _, _) => {
            var result = context.Simplify(context.NewBinop(ScevOper.Add,
                context.NewConstant(type, left), context.NewConstant(type, right)));
            Assert.That(((ScevConstant)result).Value, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void ExtensionsPreserveNativeSignedBitPattern()
    {
        WithLoop((compiler, context, _, _) => {
            foreach (var oper in new[] { ScevOper.SignExtend, ScevOper.ZeroExtend })
            {
                var extension = context.NewExtension(oper, TYP_LONG, context.NewConstant(TYP_INT, -1));
                Assert.That(((ScevConstant)context.Simplify(extension)).Value, Is.EqualTo(-1));
            }
        });
    }

    [Test]
    public static void RecurrenceDistributionRequiresAnUnsignedBound()
    {
        WithLoop((compiler, context, _, _) => {
            var recurrence = context.NewAddRec(context.NewConstant(TYP_INT, 0), context.NewConstant(TYP_INT, 1));
            var extension = context.NewExtension(ScevOper.ZeroExtend, TYP_LONG, recurrence);
            Assert.That(context.Simplify(extension), Is.TypeOf<ScevUnop>());
            var bounds = new SimplificationAssumptions([context.NewConstant(TYP_INT, 10)]);
            var distributed = (ScevAddRec)context.Simplify(extension, bounds);
            Assert.That(((ScevConstant)distributed.Start).Value, Is.Zero);
            Assert.That(((ScevConstant)distributed.Step).Value, Is.EqualTo(1));
            Assert.That(context.Simplify(context.NewExtension(ScevOper.SignExtend, TYP_LONG, recurrence), bounds),
                Is.TypeOf<ScevUnop>());
        });
    }

    [Test]
    public static void EqualityVisitAndPeelingPreserveStructure()
    {
        WithLoop((compiler, context, _, _) => {
            var local = new ScevLocal(TYP_INT, 2, 3);
            var expression = context.NewBinop(ScevOper.Add,
                context.NewConstant(TYP_INT, 4),
                context.NewBinop(ScevOper.Add, local, context.NewConstant(TYP_INT, 5)));
            var equivalent = context.NewBinop(ScevOper.Add, context.NewConstant(TYP_INT, 4),
                context.NewBinop(ScevOper.Add, new ScevLocal(TYP_INT, 2, 3), context.NewConstant(TYP_INT, 5)));
            Assert.That(Scev.Equals(expression, equivalent), Is.True);
            Assert.That(expression.IsInvariant(), Is.True);
            Assert.That(expression.PeelAdditions(out var offset), Is.SameAs(local));
            Assert.That(offset, Is.EqualTo(9));
            Assert.That(expression.Visit(node => node is ScevLocal ? ScevVisit.Abort : ScevVisit.Continue),
                Is.EqualTo(ScevVisit.Abort));
            Assert.That(context.NewAddRec(local, context.NewConstant(TYP_INT, 1)).IsInvariant(), Is.False);
        });
    }

    [Test]
    public static void AnalysisCachesResultsPerLoopAndLimitsDepth()
    {
        WithLoop((compiler, context, header, _) => {
            var tree = compiler.gtNewIconNode(TYP_INT, 42);
            var first = context.Analyze(header, tree);
            Assert.That(first, Is.TypeOf<ScevConstant>());
            Assert.That(context.Analyze(header, tree), Is.SameAs(first));
            context.ResetForLoop(compiler._loops!.GetLoopByIndex(0));
            Assert.That(context.Analyze(header, tree), Is.Not.SameAs(first));

            GenTree deep = compiler.gtNewIconNode(TYP_INT, 0);
            for (var i = 0; i < 65; i++)
            {
                deep = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, compiler.gtNewIconNode(TYP_INT, i), deep);
            }

            Assert.That(context.Analyze(header, deep), Is.Null);
            Assert.That(context.Analyze(header, deep), Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PhiAnalysisRecognizesDirectAndRecursiveRecurrences(bool indirect)
    {
        WithLoop((compiler, context, header, latch) => {
            var preheader = header.Prev ?? throw new InvalidOperationException("Expected a loop preheader.");
            ref var descriptor = ref compiler.lvaTable[0];
            descriptor.lvInSsa = true;
            var enter = descriptor.lvPerSsaData.AllocSsaNum();
            var current = descriptor.lvPerSsaData.AllocSsaNum();
            var backedge = descriptor.lvPerSsaData.AllocSsaNum();
            var initial = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0));
            initial.SsaNum = enter;
            descriptor.GetPerSsaData(enter) = new LclSsaVarDsc(preheader, initial);

            var phi = new GenTreePhi(TYP_INT)
            {
                FirstUse = new GenTreePhi.Use(new GenTreePhiArg(TYP_INT, 0, enter, preheader))
                {
                    Next = new GenTreePhi.Use(new GenTreePhiArg(TYP_INT, 0, backedge, latch)),
                },
            };
            var phiStore = compiler.gtNewStoreLclVarNode(0, phi);
            phiStore.SsaNum = current;
            descriptor.GetPerSsaData(current) = new LclSsaVarDsc(header, phiStore);

            var currentUse = compiler.gtNewLclvNode(TYP_INT, 0);
            currentUse.SsaNum = current;
            var step = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                currentUse, compiler.gtNewIconNode(TYP_INT, 1));
            GenTree data = step;
            if (indirect)
            {
                ref var temporary = ref compiler.lvaTable[1];
                temporary.lvInSsa = true;
                var tempNum = temporary.lvPerSsaData.AllocSsaNum();
                var tempStore = compiler.gtNewStoreLclVarNode(1, step);
                tempStore.SsaNum = tempNum;
                temporary.GetPerSsaData(tempNum) = new LclSsaVarDsc(latch, tempStore);
                data = compiler.gtNewLclvNode(TYP_INT, 1);
                data.AsLclVarCommon().SsaNum = tempNum;
            }

            var stepStore = compiler.gtNewStoreLclVarNode(0, data);
            stepStore.SsaNum = backedge;
            descriptor.GetPerSsaData(backedge) = new LclSsaVarDsc(latch, stepStore);

            var headerUse = compiler.gtNewLclvNode(TYP_INT, 0);
            headerUse.SsaNum = current;
            var recurrence = context.Analyze(header, headerUse);
            Assert.That(recurrence, Is.TypeOf<ScevAddRec>());
            Assert.That(((ScevLocal)((ScevAddRec)recurrence!).Start).SsaNum, Is.EqualTo(enter));
            Assert.That(((ScevConstant)((ScevAddRec)recurrence).Step).Value, Is.EqualTo(1));

            var compare = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                headerUse, compiler.gtNewIconNode(TYP_INT, 5));
            compiler.fgInsertStmtAtEnd(latch,
                compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, compare)));
            var count = context.ComputeExitNotTakenCount(latch);
            Assert.That(count, Is.TypeOf<ScevConstant>());
            Assert.That(((ScevConstant)count!).Value, Is.EqualTo(5));
        });
    }

    [Test]
    public static void MaterializationPreservesValueNumbersAndRejectsRecurrences()
    {
        WithLoop((compiler, context, _, _) => {
            var seven = context.NewConstant(TYP_INT, 7);
            var expr = context.NewBinop(ScevOper.Add, seven, context.NewConstant(TYP_INT, 2));
            var tree = context.Materialize(expr);
            Assert.That(tree, Is.Not.Null);
            Assert.That(tree!.Oper, Is.EqualTo(GT_ADD));
            Assert.That(tree._vnPair.Liberal, Is.EqualTo(context.MaterializeVN(expr).Liberal));
            Assert.That(context.EvaluateRelop(compiler.vnStore!.VNForIntCon(1)), Is.EqualTo(RelopEvaluationResult.True));
            Assert.That(context.EvaluateRelop(compiler.vnStore.VNForIntCon(0)), Is.EqualTo(RelopEvaluationResult.False));

            var recurrence = context.NewAddRec(seven, context.NewConstant(TYP_INT, 1));
#if DEBUG
            var previousId = compiler.compGenTreeID;
#endif
            Assert.That(context.Materialize(context.NewBinop(ScevOper.Add, seven, recurrence)), Is.Null);
#if DEBUG
            Assert.That(compiler.compGenTreeID, Is.EqualTo(previousId));
#endif
            Assert.That(context.MaterializeVN(recurrence).Liberal, Is.EqualTo(ValueNumStore.NoVN));
        });
    }

    private static void WithLoop(Action<Compiler, ScalarEvolutionContext, BasicBlock, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
        compiler.lvaCount = 2;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
#if DEBUG
        compiler.info.compFullName = nameof(ScalarEvolutionTests);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
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
            compiler.vnStore = new ValueNumStore(compiler);
            var context = new ScalarEvolutionContext(compiler);
            context.ResetForLoop(compiler._loops.GetLoopByIndex(0));
            action(compiler, context, header, latch);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
