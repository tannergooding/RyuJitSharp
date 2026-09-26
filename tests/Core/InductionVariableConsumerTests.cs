// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class InductionVariableConsumerTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optCanAndShouldChangeExitTest")]
    private static extern bool CanChangeExitTest(Compiler compiler, GenTree condition, bool dump);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optIsUpdateOfIVWithoutSideEffects")]
    private static extern bool IsRemovableIVUpdate(Compiler compiler, GenTree tree, int lclNum);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optVisitBoundingExitingCondBlocks")]
    private static extern void VisitBoundingExits(Compiler compiler,
        FlowGraphNaturalLoop loop, Action<BasicBlock> visitor);

    [Test]
    public static void PhaseSkipsMethodsWithoutNaturalLoops()
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.info.compFullName = nameof(PhaseSkipsMethodsWithoutNaturalLoops);
#endif
        JitTls.Compiler = compiler;
        try
        {
            Assert.That(compiler.optInductionVariables(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler._dfsTree, Is.Null);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void LoopOccurrencesVisitChildrenThenReverseTreeAndStatementOrder()
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
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 1;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;

        try
        {
            var blocks = CreateNestedLoop(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);
            var loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            var outer = loops.GetLoopByHeader(blocks[1])!;
            var inner = loops.GetLoopByHeader(blocks[2])!;
            var bounding = new List<BasicBlock>();
            VisitBoundingExits(compiler, outer, bounding.Add);
            BasicBlock[] expectedBounding = [blocks[1]];
            Assert.That(bounding, Is.EqualTo(expectedBounding));
            bounding.Clear();
            VisitBoundingExits(compiler, inner, bounding.Add);
            expectedBounding = [blocks[2]];
            Assert.That(bounding, Is.EqualTo(expectedBounding));

            Assert.That(outer.MayExecuteBlockMultipleTimesPerIteration(blocks[2]), Is.True);
            Assert.That(outer.MayExecuteBlockMultipleTimesPerIteration(blocks[3]), Is.False);
            Assert.That(inner.MayExecuteBlockMultipleTimesPerIteration(blocks[2]), Is.False);
            Assert.That(outer.IsPostDominatedOnLoopIteration(blocks[2], blocks[3]), Is.True);
            Assert.That(outer.IsPostDominatedOnLoopIteration(blocks[3], blocks[2]), Is.False);

            var outerUse = AddLocalStatement(compiler, blocks[1]);
            var firstInnerUse = AddLocalStatement(compiler, blocks[2], twoUses: true);
            var lastInnerUse = AddLocalStatement(compiler, blocks[2]);
            var info = new PerLoopInfo(loops);

            var occurrences = new List<Statement>();
            Assert.That(info.VisitOccurrences(outer, 0, (_, statement, _) => {
                occurrences.Add(statement);
                return true;
            }), Is.True);
            Statement[] expectedOccurrences = [
                lastInnerUse, firstInnerUse, firstInnerUse, outerUse,
            ];
            Assert.That(occurrences, Is.EqualTo(expectedOccurrences));
            Assert.That(info.HasAnyOccurrences(inner, 0), Is.True);
            Assert.That(info.HasAnyOccurrences(outer, 1), Is.False);

            var statements = new List<Statement>();
            Assert.That(info.VisitStatementsWithOccurrences(outer, 0, (_, statement) => {
                statements.Add(statement);
                return true;
            }), Is.True);
            Statement[] expectedStatements = [lastInnerUse, firstInnerUse, outerUse];
            Assert.That(statements, Is.EqualTo(expectedStatements));

            compiler.fgRemoveStmt(blocks[2], lastInnerUse);
            info.Invalidate(inner);
            occurrences.Clear();
            _ = info.VisitOccurrences(outer, 0, (_, statement, _) => {
                occurrences.Add(statement);
                return true;
            });
            expectedOccurrences = [firstInnerUse, firstInnerUse, outerUse];
            Assert.That(occurrences, Is.EqualTo(expectedOccurrences));

            var condition = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 3));
            Assert.That(CanChangeExitTest(compiler, condition, false), Is.True);
            condition.Op2 = compiler.gtNewIconNode(TYP_INT, 0);
            Assert.That(CanChangeExitTest(compiler, condition, false), Is.False);
            condition.Op2 = compiler.gtNewIconNode(TYP_INT, 3);
            condition.Flags |= GTF_SIDE_EFFECT;
            Assert.That(CanChangeExitTest(compiler, condition, false), Is.False);

            var update = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1));
            Assert.That(IsRemovableIVUpdate(compiler, update, 0), Is.True);
            Assert.That(IsRemovableIVUpdate(compiler, update, 1), Is.False);
            update.Data.Flags |= GTF_SIDE_EFFECT;
            Assert.That(IsRemovableIVUpdate(compiler, update, 0), Is.False);

            Assert.That(info.HasSuspensionPoint(outer), Is.False);
            var call = new GenTreeCall(TYP_VOID) { _callType = CT_USER_FUNC };
            call.SetIsAsync(default);
            var callStmt = compiler.gtNewStmt(call);
            compiler.fgSetStmtSeq(callStmt);
            compiler.fgInsertStmtAtEnd(blocks[2], callStmt);
            flags.Set(JitFlags.JIT_FLAG_ASYNC);
            info.Invalidate(outer);
            Assert.That(info.HasSuspensionPoint(inner), Is.True);
            Assert.That(info.HasSuspensionPoint(outer), Is.True);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(TYP_INT, 1L, 8L, GT_LSH)]
    [TestCase(TYP_INT, -1L, 8L, GT_MUL)]
    [TestCase(TYP_INT, 1L, int.MinValue, GT_MUL)]
    [TestCase(TYP_LONG, 1L, 8L, GT_LSH)]
    [TestCase(TYP_LONG, -1L, 8L, GT_MUL)]
    [TestCase(TYP_LONG, 1L, long.MinValue, GT_MUL)]
    public static void RephrasingUsesShiftOnlyForPositivePowerOfTwoScales(
        var_types type, long sourceStep, long targetStep, genTreeOps expectedOperation)
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
        compiler.fgNodeThreading = NodeThreading.AllTrees;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            var blocks = CreateNestedLoop(compiler);
            var dfsTree = compiler.fgComputeDfs();
            var loops = FlowGraphNaturalLoops.Find(dfsTree);
            var loop = loops.GetLoopByHeader(blocks[2])!;
            var scevContext = new ScalarEvolutionContext(compiler);
            scevContext.ResetForLoop(loop);
            var zero = scevContext.NewConstant(type, 0);
            var source = scevContext.NewAddRec(zero, scevContext.NewConstant(type, sourceStep));
            var target = scevContext.NewAddRec(zero, scevContext.NewConstant(type, targetStep));
            var sourceTree = type is TYP_INT
                ? compiler.gtNewIconNode(TYP_INT, 1)
                : compiler.gtNewLconNode(1);

            var contextType = typeof(Compiler).GetNestedType("StrengthReductionContext", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Strength reduction context is missing.");
            var constructor = contextType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [typeof(Compiler), typeof(ScalarEvolutionContext),
                    typeof(FlowGraphNaturalLoop), typeof(PerLoopInfo)], null)
                ?? throw new InvalidOperationException("Strength reduction constructor is missing.");
            var context = constructor.Invoke([compiler, scevContext, loop, new PerLoopInfo(loops)]);
            var rephrase = contextType.GetMethod("RephraseIV", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("IV rephrasing is missing.");
            var result = (GenTree)rephrase.Invoke(context, [target, source, sourceTree])!;

            Assert.That(result.Oper, Is.EqualTo(expectedOperation));
            Assert.That(result.Type, Is.EqualTo(type));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static Statement AddLocalStatement(Compiler compiler, BasicBlock block, bool twoUses = false)
    {
        var first = compiler.gtNewLclvNode(TYP_INT, 0);
        GenTree tree = twoUses ? compiler.gtNewBinaryNode(GT_ADD, TYP_INT, first,
            compiler.gtNewLclvNode(TYP_INT, 0)) : first;
        var stmt = compiler.gtNewStmt(tree);
        compiler.gtSetStmtInfo(stmt);
        compiler.fgSetStmtSeq(stmt);
        compiler.fgInsertStmtAtEnd(block, stmt);
        return stmt;
    }

    private static BasicBlock[] CreateNestedLoop(Compiler compiler)
    {
        BasicBlock[] blocks = [
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
        ];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i].bbRefs = i == 0 ? 1 : 0;
            if (i > 0)
            {
                blocks[i - 1].Next = blocks[i];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgPredsComputed = true;
        blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[0]));
        blocks[1].SetCond(compiler.fgAddRefPred(blocks[2], blocks[1]),
            compiler.fgAddRefPred(blocks[4], blocks[1]));
        blocks[2].SetCond(compiler.fgAddRefPred(blocks[2], blocks[2]),
            compiler.fgAddRefPred(blocks[3], blocks[2]));
        blocks[3].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[3]));
        return blocks;
    }
}
