// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.bbCatchType;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class FlowGraphCompactionTests
{
    [TestCase("not-always")]
    [TestCase("keep-always")]
    [TestCase("self")]
    [TestCase("first-target")]
    [TestCase("entry-target")]
    [TestCase("osr-target")]
    [TestCase("nonempty-multiple-preds")]
    [TestCase("handler-multiple-preds")]
    [TestCase("protected-target")]
    [TestCase("different-try")]
    [TestCase("different-handler")]
    public static void RejectsIneligibleCompactionsWithoutMutation(string reason)
    {
        WithCompiler(false, compiler => {
            var (prefix, block, target, _) = CreateGraph(compiler);

            switch (reason)
            {
                case "not-always":
                {
                    block.Kind = BBJ_RETURN;
                    break;
                }

                case "keep-always":
                {
                    block.SetFlags(BBF_KEEP_BBJ_ALWAYS);
                    break;
                }

                case "self":
                {
                    compiler.fgRemoveRefPred(block.TargetEdge);
                    block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(block, block));
                    break;
                }

                case "first-target":
                {
                    compiler.fgRemoveRefPred(block.TargetEdge);
                    block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(prefix, block));
                    break;
                }

                case "entry-target":
                {
                    compiler.fgEntryBB = target;
                    break;
                }

                case "osr-target":
                {
                    compiler.fgOSREntryBB = target;
                    break;
                }

                case "nonempty-multiple-preds":
                case "handler-multiple-preds":
                {
                    prefix.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, prefix));
                    if (reason == "nonempty-multiple-preds")
                    {
                        _ = AppendStatement(compiler, block, false, 1);
                    }
                    else
                    {
                        block.CatchType = BBCT_FAULT;
                    }

                    break;
                }

                case "protected-target":
                {
                    target.SetFlags(BBF_DONT_REMOVE);
                    break;
                }

                case "different-try":
                {
                    target.TryIndex = 0;
                    break;
                }

                case "different-handler":
                {
                    target.HndIndex = 0;
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown compaction refusal.");
                }
            }

            var refs = target.bbRefs;
            var count = compiler.fgBBcount;
            Assert.That(compiler.fgCanCompactBlock(block), Is.False);
            Assert.That(target.bbRefs, Is.EqualTo(refs));
            Assert.That(compiler.fgBBcount, Is.EqualTo(count));
            Assert.That(block.Next, Is.SameAs(target));
            Assert.That(target.HasFlag(BBF_REMOVED), Is.False);
        });
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public static void KeepsInitBlockInternalForDebugCode(bool debugCode, bool internalTarget, bool expected)
    {
        WithCompiler(false, compiler => {
            var (prefix, block, target, tail) = CreateGraph(compiler);
            compiler.fgFirstBB = block;
            block.Prev = null;
            prefix.Next = null;
            tail.Next = prefix;
            compiler.fgLastBB = prefix;
            compiler.opts.compDbgCode = debugCode;
            if (internalTarget)
            {
                target.SetFlags(BBF_INTERNAL);
            }

            Assert.That(compiler.fgCanCompactInitBlock(), Is.EqualTo(expected));
            Assert.That(compiler.fgCanCompactBlock(block), Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RejectsInitTargetWithTryRegionOrOtherPredecessor(bool otherPredecessor)
    {
        WithCompiler(false, compiler => {
            var (prefix, block, target, tail) = CreateGraph(compiler);
            compiler.fgFirstBB = block;
            block.Prev = null;
            prefix.Next = null;
            tail.Next = prefix;
            compiler.fgLastBB = prefix;
            if (otherPredecessor)
            {
                prefix.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, prefix));
            }
            else
            {
                target.TryIndex = 0;
                block.TryIndex = 0;
            }

            Assert.That(compiler.fgCanCompactInitBlock(), Is.False);
            Assert.That(compiler.fgCanCompactBlock(block), Is.False);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void CompactsCallFinallyPairsOnlyWhenAdjacent(bool adjacent, bool lir)
    {
        WithCompiler(lir, compiler => {
            var (prefix, block, target, tail) = CreateGraph(compiler);
            target.SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(prefix, target));
            tail.SetKindAndTargetEdge(BBJ_CALLFINALLYRET, compiler.fgAddRefPred(prefix, tail));
            if (lir)
            {
                tail.InsertAtEnd(new GenTreeILOffset(default));
            }

            if (!adjacent)
            {
                var intervening = NewBlock(compiler, BBJ_RETURN);
                block.Next = intervening;
                intervening.Next = target;
            }

            Assert.That(compiler.fgCanCompactBlock(block), Is.EqualTo(adjacent));
            if (adjacent)
            {
                var edge = target.TargetEdge;
                compiler.fgCompactBlock(block);
                Assert.That(block.TargetEdge, Is.SameAs(edge));
                Assert.That(block.Next, Is.SameAs(tail));
                Assert.That(block.isBBCallFinallyPair, Is.True);
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void UsesNativeModeSpecificEmptinessForMultiplePredecessors(bool lir, bool executable)
    {
        WithCompiler(lir, compiler => {
            var (prefix, block, target, _) = CreateGraph(compiler);
            prefix.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, prefix));
            if (lir)
            {
                block.InsertAtEnd(new GenTreeILOffset(default));
                if (executable)
                {
                    // Unlike a statement NOP, a LIR NOP is not ignored by native isEmpty.
                    block.InsertAtEnd(new GenTree(GT_NOP, TYP_VOID));
                }
            }
            else
            {
                _ = AppendStatement(compiler, block, true, 0);
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(new GenTree(GT_NOP, TYP_VOID)));
                if (executable)
                {
                    _ = AppendStatement(compiler, block, false, 1);
                }
            }

            Assert.That(compiler.fgCanCompactBlock(block), Is.EqualTo(!executable));
            if (!executable)
            {
                compiler.fgCompactBlock(block);
                Assert.That(prefix.Target, Is.SameAs(block));
                Assert.That(block.Kind, Is.EqualTo(BBJ_RETURN));
                Assert.That(lir ? block.FirstNode is not null : block.FirstStmt is not null, Is.True);
            }
        });
    }

    [TestCase(0, 0, 0, 0)]
    [TestCase(0, 0, 2, 2)]
    [TestCase(0, 2, 0, 0)]
    [TestCase(0, 2, 0, 2)]
    [TestCase(0, 2, 2, 0)]
    [TestCase(0, 2, 2, 2)]
    [TestCase(2, 0, 0, 2)]
    [TestCase(2, 0, 2, 2)]
    [TestCase(2, 2, 0, 2)]
    [TestCase(2, 2, 2, 2)]
    public static void SplicesPhiPrefixesBeforeOrdinaryStatements(
        int blockPhis, int blockStatements, int targetPhis, int targetStatements)
    {
        WithCompiler(false, compiler => {
            var (_, block, target, _) = CreateGraph(compiler);
            var expected = new List<Statement>();
            var blockBody = new List<Statement>();
            for (var i = 0; i < blockPhis; i++)
            {
                expected.Add(AppendStatement(compiler, block, true, i));
            }

            for (var i = 0; i < blockStatements; i++)
            {
                blockBody.Add(AppendStatement(compiler, block, false, i));
            }

            for (var i = 0; i < targetPhis; i++)
            {
                expected.Add(AppendStatement(compiler, target, true, i));
            }

            expected.AddRange(blockBody);
            for (var i = 0; i < targetStatements; i++)
            {
                expected.Add(AppendStatement(compiler, target, false, i));
            }

            compiler.fgCompactBlock(block);
            AssertStatementLinks(block, expected);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PreservesNativePhiOnlyTargetSpliceWhenBothBlocksHavePhis(bool blockHasOrdinaryStatement)
    {
        WithCompiler(false, compiler => {
            var (_, block, target, _) = CreateGraph(compiler);
            var first = AppendStatement(compiler, block, true, 0);
            var expected = new List<Statement> { first };
            if (blockHasOrdinaryStatement)
            {
                expected.Add(AppendStatement(compiler, block, false, 1));
            }

            _ = AppendStatement(compiler, target, true, 2);
            _ = AppendStatement(compiler, target, true, 3);
            compiler.fgCompactBlock(block);

            // Native reads targetFirst->Prev after overwriting it with blkLastPhi.
            // When target has only phis, it consequently retains only block's list.
            AssertStatementLinks(block, expected);
            Assert.That(target.FirstStmt, Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void TransfersLirRangesAndClearsTheRemovedOwner(bool blockHasNodes, bool targetHasNodes)
    {
        WithCompiler(true, compiler => {
            var (_, block, target, _) = CreateGraph(compiler);
            var expected = new List<GenTree>();
            if (blockHasNodes)
            {
                var node = compiler.gtNewIconNode(TYP_INT, 1);
                block.InsertAtEnd(node);
                expected.Add(node);
            }

            if (targetHasNodes)
            {
                var first = compiler.gtNewIconNode(TYP_INT, 2);
                var last = compiler.gtNewIconNode(TYP_INT, 3);
                target.InsertAtEnd(first);
                target.InsertAtEnd(last);
                expected.Add(first);
                expected.Add(last);
            }

            compiler.fgCompactBlock(block);
            Assert.That(block.ToArray(), Is.EqualTo(expected));
            Assert.That(target.FirstNode, Is.Null);
            Assert.That(target.LastNode, Is.Null);
            Assert.That(block.FirstNode, Is.SameAs(expected.FirstOrDefault()));
            Assert.That(block.LastNode, Is.SameAs(expected.LastOrDefault()));

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.That(expected[i].Prev, Is.SameAs(i == 0 ? null : expected[i - 1]));
                Assert.That(expected[i].Next, Is.SameAs(i + 1 == expected.Count ? null : expected[i + 1]));
            }
        });
    }

    [TestCase(BBJ_ALWAYS, false)]
    [TestCase(BBJ_ALWAYS, true)]
    [TestCase(BBJ_CALLFINALLY, false)]
    [TestCase(BBJ_CALLFINALLY, true)]
    [TestCase(BBJ_EHCATCHRET, false)]
    [TestCase(BBJ_EHCATCHRET, true)]
    [TestCase(BBJ_EHFILTERRET, false)]
    [TestCase(BBJ_EHFILTERRET, true)]
    [TestCase(BBJ_COND, false)]
    [TestCase(BBJ_COND, true)]
    [TestCase(BBJ_SWITCH, false)]
    [TestCase(BBJ_SWITCH, true)]
    [TestCase(BBJ_EHFINALLYRET, false)]
    [TestCase(BBJ_EHFINALLYRET, true)]
    [TestCase(BBJ_EHFAULTRET, false)]
    [TestCase(BBJ_EHFAULTRET, true)]
    [TestCase(BBJ_THROW, false)]
    [TestCase(BBJ_THROW, true)]
    [TestCase(BBJ_RETURN, false)]
    [TestCase(BBJ_RETURN, true)]
    public static void TransfersEverySupportedOutgoingKindWithoutReplacingEdges(BBKinds kind, bool lir)
    {
        WithCompiler(lir, compiler => {
            var (prefix, block, target, firstSuccessor) = CreateGraph(compiler);
            var secondSuccessor = NewBlock(compiler, BBJ_RETURN);
            firstSuccessor.Next = secondSuccessor;
            compiler.fgLastBB = secondSuccessor;
            var outgoing = new List<FlowEdge>();
            BBJumpTable? descriptor = null;

            if (kind is BBJ_ALWAYS or BBJ_CALLFINALLY or BBJ_EHCATCHRET or BBJ_EHFILTERRET)
            {
                var edge = compiler.fgAddRefPred(firstSuccessor, target);
                edge.Likelihood = 1;
                outgoing.Add(edge);
                target.SetKindAndTargetEdge(kind, edge);
                if (kind is BBJ_CALLFINALLY)
                {
                    target.SetFlags(BBF_RETLESS_CALL);
                }
            }
            else if (kind is BBJ_COND or BBJ_SWITCH or BBJ_EHFINALLYRET)
            {
                var first = compiler.fgAddRefPred(firstSuccessor, target);
                var second = compiler.fgAddRefPred(secondSuccessor, target);
                first.Likelihood = 0.25;
                second.Likelihood = 0.75;
                outgoing.Add(first);
                outgoing.Add(second);

                if (kind is BBJ_COND)
                {
                    target.SetCond(first, second);
                }
                else if (kind is BBJ_SWITCH)
                {
                    var targets = new BBswtDesc([first, second], [0, 1, 0], hasDefault: true);
                    targets.Cases[0] = first;
                    targets.Cases[1] = second;
                    targets.Cases[2] = compiler.fgAddRefPred(firstSuccessor, target);
                    target.SwitchTargets = targets;
                    descriptor = targets;
                }
                else
                {
                    descriptor = new BBJumpTable([first, second]);
                    target.SetEhf(descriptor);
                }
            }
            else
            {
                target.Kind = kind;
            }

            prefix.SetCond(compiler.fgAddRefPred(firstSuccessor, prefix), compiler.fgAddRefPred(secondSuccessor, prefix));
            var refs = outgoing.Select(edge => edge.DestinationBlock.bbRefs).ToArray();
            var duplicates = outgoing.Select(edge => edge.DupCount).ToArray();
            var likelihoods = outgoing.Select(edge => edge.Likelihood).ToArray();
            var count = compiler.fgBBcount;

            Assert.That(compiler.fgCanCompactBlock(block), Is.True);
            compiler.fgCompactBlock(block);

            Assert.That(block.Kind, Is.EqualTo(kind));
            Assert.That(block.Next, Is.SameAs(firstSuccessor));
            Assert.That(firstSuccessor.Prev, Is.SameAs(block));
            Assert.That(compiler.fgBBcount, Is.EqualTo(count - 1));
            Assert.That(target.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(target.bbRefs, Is.Zero);
            Assert.That(target.bbPreds, Is.Null);
            Assert.That(compiler.fgModified, Is.True);

            for (var i = 0; i < outgoing.Count; i++)
            {
                var edge = outgoing[i];
                Assert.That(edge.SourceBlock, Is.SameAs(block));
                Assert.That(edge.DestinationBlock.bbRefs, Is.EqualTo(refs[i]));
                Assert.That(edge.DupCount, Is.EqualTo(duplicates[i]));
                Assert.That(edge.Likelihood, Is.EqualTo(likelihoods[i]));
                Assert.That(edge.DestinationBlock.PredEdges.Last(), Is.SameAs(edge));
            }

            if (kind is BBJ_SWITCH)
            {
                Assert.That(block.SwitchTargets, Is.SameAs(descriptor));
                Assert.That(block.SwitchTargets.Cases[0], Is.SameAs(block.SwitchTargets.Cases[2]));
            }
            else if (kind is BBJ_EHFINALLYRET)
            {
                Assert.That(block.EhfTargets, Is.SameAs(descriptor));
            }
            else if (kind is BBJ_COND)
            {
                Assert.That(block.TrueEdge, Is.SameAs(outgoing[0]));
                Assert.That(block.FalseEdge, Is.SameAs(outgoing[1]));
            }
            else if (block.HasTarget)
            {
                Assert.That(block.TargetEdge, Is.SameAs(outgoing[0]));
                Assert.That(block.HasFlag(BBF_RETLESS_CALL), Is.EqualTo(kind is BBJ_CALLFINALLY));
            }
        });
    }

    [Test]
    public static void TransfersAConditionalWithOneSharedEdgeOnlyOnce()
    {
        WithCompiler(false, compiler => {
            var (_, block, target, tail) = CreateGraph(compiler);
            var edge = compiler.fgAddRefPred(tail, target);
            target.SetCond(edge, compiler.fgAddRefPred(tail, target));
            compiler.fgCompactBlock(block);
            Assert.That(block.TrueEdge, Is.SameAs(edge));
            Assert.That(block.FalseEdge, Is.SameAs(edge));
            Assert.That(edge.SourceBlock, Is.SameAs(block));
            Assert.That(edge.DupCount, Is.EqualTo(2));
            Assert.That(tail.bbRefs, Is.EqualTo(2));
            Assert.That(tail.bbPreds, Is.SameAs(edge));
            Assert.That(edge.NextPredEdge, Is.Null);
        });
    }

    [TestCase(false, 30, true)]
    [TestCase(true, 30, true)]
    [TestCase(true, 40, false)]
    public static void RetargetsOtherPredecessorsAndRechecksProfileFlow(bool profileWeight, int targetWeight, bool consistent)
    {
        WithCompiler(false, compiler => {
            var (prefix, block, target, tail) = CreateGraph(compiler);
            prefix.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(block, prefix));
            prefix.bbWeight = 10;
            prefix.TargetEdge.Likelihood = 1;
            tail.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, tail));
            tail.bbWeight = 20;
            tail.TargetEdge.Likelihood = 1;
            var redirected = tail.TargetEdge;
            target.bbWeight = targetWeight;
            if (profileWeight)
            {
                // Preserve the source's profile flag even if the target does not have it.
                block.setBBProfileWeight(10);
            }

            compiler.fgPgoConsistent = true;
            Assert.That(compiler.fgCanCompactBlock(block), Is.True);
            compiler.fgCompactBlock(block);

            Assert.That(tail.Target, Is.SameAs(block));
            Assert.That(tail.TargetEdge, Is.SameAs(redirected));
            Assert.That(block.PredEdges.ToArray(), Is.EqualTo([prefix.TargetEdge, redirected]));
            Assert.That(block.bbRefs, Is.EqualTo(2));
            Assert.That(block.bbWeight, Is.EqualTo(targetWeight));
            Assert.That(block.hasProfileWeight, Is.EqualTo(profileWeight));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(consistent));
        });
    }

    [Test]
    public static void RedirectsDuplicateSwitchPredecessorsWithoutLosingLikelihood()
    {
        WithCompiler(false, compiler => {
            var (prefix, block, target, _) = CreateGraph(compiler);
            var oldEdge = compiler.fgAddRefPred(target, prefix);
            var retainedEdge = compiler.fgAddRefPred(block, prefix);
            var switches = new BBswtDesc([oldEdge, retainedEdge], [0, 0, 1], hasDefault: true);
            switches.Cases[0] = oldEdge;
            switches.Cases[1] = compiler.fgAddRefPred(target, prefix);
            switches.Cases[2] = retainedEdge;
            oldEdge.Likelihood = 0.75;
            retainedEdge.Likelihood = 0.25;
            prefix.SwitchTargets = switches;

            compiler.fgCompactBlock(block);

            Assert.That(switches.Succs.ToArray(), Is.EqualTo([retainedEdge]));
            Assert.That(switches.Cases.ToArray(), Is.EqualTo([retainedEdge, retainedEdge, retainedEdge]));
            Assert.That(retainedEdge.DupCount, Is.EqualTo(3));
            Assert.That(retainedEdge.Likelihood, Is.EqualTo(1));
            Assert.That(block.bbRefs, Is.EqualTo(3));
            Assert.That(block.bbPreds, Is.SameAs(retainedEdge));
        });
    }

    [TestCase(BAD_IL_OFFSET, BAD_IL_OFFSET, 2, 20, 2, 20)]
    [TestCase(4, 10, BAD_IL_OFFSET, BAD_IL_OFFSET, 4, 10)]
    [TestCase(4, 10, 2, 20, 2, 20)]
    [TestCase(2, 20, 4, 10, 2, 20)]
    [TestCase(int.MinValue, int.MinValue + 10, 2, 20, 2, int.MinValue + 10)]
    [TestCase(2, 20, int.MinValue, int.MinValue + 10, 2, int.MinValue + 10)]
    [TestCase(BAD_IL_OFFSET, BAD_IL_OFFSET, BAD_IL_OFFSET, BAD_IL_OFFSET, BAD_IL_OFFSET, BAD_IL_OFFSET)]
    public static void CombinesOffsetsFlagsWeightAndLiveOut(
        int start, int end, int targetStart, int targetEnd, int expectedStart, int expectedEnd)
    {
        WithCompiler(false, compiler => {
            var (_, block, target, _) = CreateGraph(compiler);
            compiler.lvaTrackedCount = 3;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            block.bbCodeOffs = start;
            block.bbCodeOffsEnd = end;
            target.bbCodeOffs = targetStart;
            target.bbCodeOffsEnd = targetEnd;
            block.SetFlags(BBF_INTERNAL | BBF_GC_SAFE_POINT);
            target.SetFlags(BBF_ASYNC_RESUMPTION | BBF_NEEDS_GCPOLL | BBF_BACKWARD_JUMP);
            target.setBBProfileWeight(42);
            block.bbLiveOut = [1];
            target.bbLiveOut = [2];

            compiler.fgCompactBlock(block);

            Assert.That(block.bbCodeOffs, Is.EqualTo(expectedStart));
            Assert.That(block.bbCodeOffsEnd, Is.EqualTo(expectedEnd));
            Assert.That(block.HasFlag(BBF_INTERNAL), Is.False);
            var expectedFlags = BBF_IMPORTED | BBF_GC_SAFE_POINT | BBF_NEEDS_GCPOLL |
                BBF_BACKWARD_JUMP | BBF_ASYNC_RESUMPTION | BBF_PROF_WEIGHT;
            Assert.That(block.FlagsRaw & expectedFlags, Is.EqualTo(expectedFlags));
            Assert.That(block.bbWeight, Is.EqualTo(42));
            Assert.That(block.bbLiveOut, Is.EqualTo(new nint[] { 2 }));
            Assert.That(block.bbLiveOut, Is.Not.SameAs(target.bbLiveOut));
        });
    }

    [Test]
    public static void PreservesUninitializedLiveOutAndUpdatesTheLastBlock()
    {
        WithCompiler(false, compiler => {
            var (_, block, target, _) = CreateGraph(compiler);
            target.Next = null;
            compiler.fgLastBB = target;
            block.bbLiveOut = [1];
            target.bbLiveOut = [];
            compiler.fgCompactBlock(block);
            Assert.That(block.bbLiveOut, Is.Empty);
            Assert.That(compiler.fgLastBB, Is.SameAs(block));
            Assert.That(block.Next, Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void MovesAllEnclosingEhEndpointsToThePreviousBlock(bool handler, bool adjacent)
    {
        WithCompiler(false, compiler => {
            var (prefix, block, target, tail) = CreateGraph(compiler);
            var newEnd = block;
            if (!adjacent)
            {
                newEnd = NewBlock(compiler, BBJ_RETURN);
                block.Next = newEnd;
                newEnd.Next = target;
            }

            if (handler)
            {
                block.HndIndex = 0;
                newEnd.HndIndex = 0;
                target.HndIndex = 0;
                block.CatchType = BBCT_FAULT;
            }
            else
            {
                block.TryIndex = 0;
                newEnd.TryIndex = 0;
                target.TryIndex = 0;
            }

            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdTryBeg = handler ? prefix : block,
                    ebdTryLast = handler ? prefix : target,
                    ebdHndBeg = handler ? block : tail,
                    ebdHndLast = handler ? target : tail
                },
                new EHblkDsc {
                    ebdTryBeg = prefix,
                    ebdTryLast = target,
                    ebdHndBeg = tail,
                    ebdHndLast = tail
                }
            ];
            compiler.compHndBBtabCount = 2;

            compiler.fgCompactBlock(block);

            var firstEnd = handler ? compiler.compHndBBtab[0].ebdHndLast : compiler.compHndBBtab[0].ebdTryLast;
            Assert.That(firstEnd, Is.SameAs(newEnd));
            Assert.That(compiler.compHndBBtab[1].ebdTryLast, Is.SameAs(newEnd));
            Assert.That(compiler.compHndBBtab[1].ebdTryBeg, Is.SameAs(prefix));
            Assert.That(compiler.compHndBBtab[1].ebdHndLast, Is.SameAs(tail));
            Assert.That(block.Next, Is.SameAs(adjacent ? tail : newEnd));
            Assert.That(newEnd.Next, Is.SameAs(tail));
        });
    }

    private static void AssertStatementLinks(BasicBlock block, List<Statement> expected)
    {
        var statement = block.FirstStmt;
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.That(statement, Is.SameAs(expected[i]));
            Assert.That(expected[i].PrevStmt, Is.SameAs(i == 0 ? expected[^1] : expected[i - 1]));
            statement = expected[i].NextStmt;
        }

        Assert.That(statement, Is.Null);
        Assert.That(block.LastStmt, Is.SameAs(expected.LastOrDefault()));
    }

    private static Statement AppendStatement(Compiler compiler, BasicBlock block, bool phi, int value)
    {
        GenTree tree = phi
            ? new GenTreeLclVar(TYP_INT, value, new GenTreePhi(TYP_INT))
            : compiler.gtNewIconNode(TYP_INT, value);
        var statement = compiler.gtNewStmt(tree);
        compiler.fgInsertStmtAtEnd(block, statement);

        return statement;
    }

    private static (BasicBlock Prefix, BasicBlock Block, BasicBlock Target, BasicBlock Tail) CreateGraph(Compiler compiler)
    {
        var prefix = NewBlock(compiler, BBJ_RETURN);
        var block = NewBlock(compiler, BBJ_ALWAYS);
        var target = NewBlock(compiler, BBJ_RETURN);
        var tail = NewBlock(compiler, BBJ_RETURN);
        prefix.Next = block;
        block.Next = target;
        target.Next = tail;
        compiler.fgFirstBB = prefix;
        compiler.fgLastBB = tail;
        block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, block));

        return (prefix, block, target, tail);
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;

        return block;
    }

    private static void WithCompiler(bool lir, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        compiler.compRationalIRForm = lir;
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
