// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class NaturalLoopIterationTests
{
    [TestCase(GT_ADD, 1, GT_LT, 5, true)]
    [TestCase(GT_SUB, 1, GT_GT, -5, true)]
    [TestCase(GT_ADD, 2, GT_NE, 5, false)]
    [TestCase(GT_ADD, 1, GT_NE, 5, true)]
    [TestCase(GT_MUL, 2, GT_LT, 5, false)]
    public static void RecognizesNativeIncrementsAndDirections(
        genTreeOps update, int stride, genTreeOps testOper, int limit, bool directional)
    {
        WithLoop((compiler, loop, preheader, unusedHeader, latch) =>
        {
            var init = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0));
            compiler.fgInsertStmtAtEnd(preheader, compiler.gtNewStmt(init));
            var increment = NewIncrement(compiler, update, stride);
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(increment));
            AddTest(compiler, latch, testOper, compiler.gtNewIconNode(TYP_INT, limit));

            Assert.That(compiler.optIsLoopIncrTree(increment), Is.Zero);
            var recognized = loop.AnalyzeIteration(out var info);
            Assert.That(recognized, Is.True);
            if (!recognized)
            {
                return;
            }
            Assert.That(info.TestBlock, Is.SameAs(latch));
            Assert.That(info.IterTree, Is.SameAs(increment));
            Assert.That(info.HasConstInit, Is.True);
            Assert.That(info.ConstInitValue, Is.Zero);
            Assert.That(info.ConstLimit(), Is.EqualTo(limit));
            Assert.That(info.IsIncreasingLoop() || info.IsDecreasingLoop(), Is.EqualTo(directional));
        });
    }

    [Test]
    public static void AcceptsSymbolicInvariantLimitOnlyWithEntryProofOrCallerGuard()
    {
        WithLoop((compiler, loop, unusedPreheader, header, latch) =>
        {
            var increment = NewIncrement(compiler, GT_ADD, 1);
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(increment));
            AddTest(compiler, latch, GT_LT, compiler.gtNewLclvNode(TYP_INT, 1));

            Assert.That(loop.AnalyzeIteration(out _), Is.False);
            Assert.That(loop.AnalyzeIteration(out var info, allowMissingBaseCase: true), Is.True);
            Assert.That(info.HasInvariantLocalLimit, Is.True);
            Assert.That(info.VarLimit(), Is.EqualTo(1));
            Assert.That(info.NeedsZeroTripGuard, Is.True);
            compiler.fgInsertStmtAtEnd(header,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 10))));
            Assert.That(loop.AnalyzeIteration(out _, allowMissingBaseCase: true), Is.False);
        });
    }

    [Test]
    public static void InterveningReadRejectsLateIncrementAndFindsEarlierCandidate()
    {
        WithLoop((compiler, loop, preheader, unusedHeader, latch) =>
        {
            compiler.fgInsertStmtAtEnd(preheader,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            var first = NewIncrement(compiler, GT_ADD, 1);
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(first));
            compiler.fgInsertStmtAtEnd(latch,
                compiler.gtNewStmt(compiler.gtNewLclvNode(TYP_INT, 0)));
            var second = NewIncrement(compiler, GT_ADD, 1);
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(second));
            AddTest(compiler, latch, GT_LT, compiler.gtNewIconNode(TYP_INT, 5));

            Assert.That(compiler.optExtractTestIncr(latch, out _, out var selected), Is.True);
            Assert.That(selected, Is.SameAs(second));
            Assert.That(loop.AnalyzeIteration(out _), Is.False);
        });
    }

    [Test]
    public static void CandidateMustUpdateItsOwnLocalWithIntConstant()
    {
        WithLoop((compiler, unusedLoop, unusedPreheader, unusedHeader, unusedLatch) =>
        {
            var changedLocal = compiler.gtNewStoreLclVarNode(0,
                compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 1)));
            var variableStep = compiler.gtNewStoreLclVarNode(0,
                compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewLclvNode(TYP_INT, 1)));
            Assert.That(compiler.optIsLoopIncrTree(changedLocal), Is.EqualTo(BAD_VAR_NUM));
            Assert.That(compiler.optIsLoopIncrTree(variableStep), Is.EqualTo(BAD_VAR_NUM));
        });
    }

    [Test]
    public static void EquivalentGuardProvesEntryButMismatchedGuardDoesNot()
    {
        foreach (var guardOper in new[] { GT_LT, GT_LE })
        {
            WithLoop((compiler, loop, unusedPreheader, unusedHeader, latch) =>
            {
                compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(NewIncrement(compiler, GT_ADD, 1)));
                AddTest(compiler, latch, GT_LT, compiler.gtNewLclvNode(TYP_INT, 1));

                Assert.That(loop.AnalyzeIteration(out var info), Is.EqualTo(guardOper is GT_LT));
                if (guardOper is GT_LT)
                {
                    Assert.That(info.NeedsZeroTripGuard, Is.False);
                }
            }, guardOper);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RecognizesInvariantArrayAndOffsetLimits(bool offset)
    {
        WithLoop((compiler, loop, preheader, unusedHeader, latch) =>
        {
            compiler.lvaTable[1].Type = TYP_REF;
            compiler.fgInsertStmtAtEnd(preheader,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(NewIncrement(compiler, GT_ADD, 1)));
            GenTree limit = new GenTreeArrLen(TYP_INT, compiler.gtNewLclvNode(TYP_REF, 1), 0);
            if (offset)
            {
                limit = compiler.gtNewBinaryNode(GT_SUB, TYP_INT, limit, compiler.gtNewIconNode(TYP_INT, 4));
            }
            AddTest(compiler, latch, GT_LT, limit);

            Assert.That(loop.AnalyzeIteration(out _), Is.False);
            Assert.That(loop.AnalyzeIteration(out var info, allowMissingBaseCase: true), Is.True);
            Assert.That(info.HasArrayLengthLimit, Is.True);
            Assert.That(info.LimitVar, Is.EqualTo(1));
            Assert.That(info.LimitOffset, Is.EqualTo(offset ? -4 : 0));
            Assert.That(info.LimitBase().Oper, Is.EqualTo(GT_ARR_LENGTH));
        });
    }

    [Test]
    public static void TemporaryComparisonStillFindsTheIncrement()
    {
        WithLoop((compiler, loop, preheader, unusedHeader, latch) =>
        {
            compiler.fgInsertStmtAtEnd(preheader,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            var increment = NewIncrement(compiler, GT_ADD, 1);
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(increment));
            var comparison = compiler.gtNewBinaryNode(GT_LT, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 7));
            compiler.fgInsertStmtAtEnd(latch,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(1, comparison)));
            var tempNotZero = compiler.gtNewBinaryNode(GT_NE, TYP_INT,
                compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 0));
            compiler.fgInsertStmtAtEnd(latch,
                compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, tempNotZero)));

            Assert.That(loop.AnalyzeIteration(out var info), Is.True);
            Assert.That(info.TestTree, Is.SameAs(comparison));
            Assert.That(info.IterTree, Is.SameAs(increment));
        });
    }

    [Test]
    public static void RejectsTooManyStatementsBetweenIncrementAndTest()
    {
        WithLoop((compiler, loop, unusedPreheader, unusedHeader, latch) =>
        {
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(NewIncrement(compiler, GT_ADD, 1)));
            for (var i = 0; i < 101; i++)
            {
                compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, i)));
            }
            AddTest(compiler, latch, GT_LT, compiler.gtNewIconNode(TYP_INT, 5));
            Assert.That(compiler.optExtractTestIncr(latch, out _, out _), Is.False);
            Assert.That(loop.AnalyzeIteration(out _), Is.False);
        });
    }

    [Test]
    public static void UnsignedReversedExitTestUsesNormalizedRelationAndBaseCase()
    {
        WithLoop((compiler, loop, preheader, unusedHeader, latch) =>
        {
            compiler.fgInsertStmtAtEnd(preheader,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, -1))));
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(NewIncrement(compiler, GT_SUB, 1)));
            var compare = compiler.gtNewBinaryNode(GT_GE, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 0), compiler.gtNewLclvNode(TYP_INT, 0));
            compare.Flags |= GTF_UNSIGNED;
            compiler.fgInsertStmtAtEnd(latch,
                compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, compare)));

            Assert.That(loop.AnalyzeIteration(out _), Is.False);
            Assert.That(loop.AnalyzeIteration(out var info, allowMissingBaseCase: true), Is.True);
            Assert.That(info.TestOper(), Is.EqualTo(GT_LE));
            Assert.That(info.NeedsZeroTripGuard, Is.True);
        });
    }

    [Test]
    public static void ExtraneousDefinitionAndAddressExposureRejectIterator()
    {
        WithLoop((compiler, loop, preheader, header, latch) =>
        {
            compiler.fgInsertStmtAtEnd(preheader,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            compiler.fgInsertStmtAtEnd(header,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 2))));
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(NewIncrement(compiler, GT_ADD, 1)));
            AddTest(compiler, latch, GT_LT, compiler.gtNewIconNode(TYP_INT, 5));
            Assert.That(loop.AnalyzeIteration(out _), Is.False);
        });

        WithLoop((compiler, loop, preheader, unusedHeader, latch) =>
        {
            compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.ESCAPE_ADDRESS);
            compiler.fgInsertStmtAtEnd(preheader,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(NewIncrement(compiler, GT_ADD, 1)));
            AddTest(compiler, latch, GT_LT, compiler.gtNewIconNode(TYP_INT, 5));
            Assert.That(loop.AnalyzeIteration(out _), Is.False);
        });
    }

    [Test]
    public static void SkipsUnrelatedExitAndUsesLatchExit()
    {
        WithLoop((compiler, loop, preheader, unusedHeader, latch) =>
        {
            compiler.fgInsertStmtAtEnd(preheader,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(NewIncrement(compiler, GT_ADD, 1)));
            AddTest(compiler, latch, GT_LT, compiler.gtNewIconNode(TYP_INT, 5));

            Assert.That(loop.ExitEdges.Length, Is.EqualTo(2));
            Assert.That(loop.AnalyzeIteration(out var info), Is.True);
            Assert.That(info.TestBlock, Is.SameAs(latch));
        }, headerExit: true);
    }

    [Test]
    public static void InterveningThrowIsUnsafeOnlyWithinTry()
    {
        foreach (var inTry in new[] { false, true })
        {
            WithLoop((compiler, loop, preheader, unusedHeader, latch) =>
            {
                compiler.fgInsertStmtAtEnd(preheader,
                    compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
                compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(NewIncrement(compiler, GT_ADD, 1)));
                var mayThrow = compiler.gtNewIconNode(TYP_INT, 1);
                mayThrow.Flags |= GTF_EXCEPT;
                compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(mayThrow));
                AddTest(compiler, latch, GT_LT, compiler.gtNewIconNode(TYP_INT, 5));
                if (inTry)
                {
                    latch.TryIndex = 0;
                }

                Assert.That(loop.AnalyzeIteration(out _), Is.EqualTo(!inTry));
            });
        }
    }

    [Test]
    public static void PromotedFieldCannotBeIterationVariable()
    {
        WithLoop((compiler, loop, preheader, unusedHeader, latch) =>
        {
            compiler.lvaTable[0].lvIsStructField = true;
            compiler.fgInsertStmtAtEnd(preheader,
                compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0))));
            compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(NewIncrement(compiler, GT_ADD, 1)));
            AddTest(compiler, latch, GT_LT, compiler.gtNewIconNode(TYP_INT, 5));
            Assert.That(loop.AnalyzeIteration(out _), Is.False);
        });
    }

    private static GenTreeLclVar NewIncrement(Compiler compiler, genTreeOps oper, int stride)
        => compiler.gtNewStoreLclVarNode(0, compiler.gtNewBinaryNode(oper, TYP_INT,
            compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, stride)));

    private static void AddTest(Compiler compiler, BasicBlock latch, genTreeOps oper, GenTree limit)
    {
        var compare = compiler.gtNewBinaryNode(oper, TYP_INT, compiler.gtNewLclvNode(TYP_INT, 0), limit);
        compiler.fgInsertStmtAtEnd(latch, compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, compare)));
    }

    private static void WithLoop(Action<Compiler, FlowGraphNaturalLoop, BasicBlock, BasicBlock, BasicBlock> action,
        genTreeOps? guardOper = null, bool headerExit = false)
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
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(NaturalLoopIterationTests);
#endif
        JitTls.Compiler = compiler;
        try
        {
            compiler.lvaCount = 2;
            compiler.lvaTable = new LclVarDsc[2];
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[1].Type = TYP_INT;
            var preheader = BasicBlock.New(compiler, BBJ_ALWAYS);
            var header = BasicBlock.New(compiler, headerExit ? BBJ_COND : BBJ_ALWAYS);
            var latch = BasicBlock.New(compiler, BBJ_COND);
            var exit = BasicBlock.New(compiler, BBJ_RETURN);
            preheader.bbRefs = guardOper is null ? 1 : 0;
            preheader.Next = header;
            header.Prev = preheader;
            header.Next = latch;
            latch.Prev = header;
            latch.Next = exit;
            exit.Prev = latch;
            compiler.fgFirstBB = preheader;
            compiler.fgLastBB = exit;
            _ = Jump(preheader, header);
            if (headerExit)
            {
                AddTest(compiler, header, GT_LT, compiler.gtNewIconNode(TYP_INT, 3));
                header.SetCond(Connect(header, latch), Connect(header, exit));
            }
            else
            {
                _ = Jump(header, latch);
            }
            latch.SetCond(Connect(latch, header), Connect(latch, exit));
            if (guardOper is not null)
            {
                var guard = BasicBlock.New(compiler, BBJ_COND);
                guard.bbRefs = 1;
                guard.Next = preheader;
                preheader.Prev = guard;
                compiler.fgFirstBB = guard;
                AddTest(compiler, guard, guardOper.Value, compiler.gtNewLclvNode(TYP_INT, 1));
                guard.SetCond(Connect(guard, preheader), Connect(guard, exit));
            }
            compiler.fgPredsComputed = true;
            var loops = FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));
            Assert.That(loops.NumLoops, Is.EqualTo(1));
            action(compiler, loops.GetLoopByIndex(0), preheader, header, latch);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgComputeDfs")]
    private static extern FlowGraphDfsTree ComputeDfs(Compiler compiler, bool useProfile);

    private static FlowEdge Connect(BasicBlock source, BasicBlock target)
    {
        var edge = new FlowEdge(source, target, target.bbPreds) { Likelihood = 0.5 };
        target.bbPreds = edge;
        target.bbRefs++;
        return edge;
    }

    private static FlowEdge Jump(BasicBlock source, BasicBlock target)
    {
        var edge = Connect(source, target);
        source.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
        return edge;
    }
}
