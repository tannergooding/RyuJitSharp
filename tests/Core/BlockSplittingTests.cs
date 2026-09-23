// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.bbCatchType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class BlockSplittingTests
{
    private static IEnumerable<TestCaseData> SplitCases()
    {
        BBKinds[] kinds = [
            BBJ_ALWAYS, BBJ_CALLFINALLY, BBJ_CALLFINALLYRET, BBJ_EHCATCHRET,
            BBJ_EHFILTERRET, BBJ_LEAVE, BBJ_COND, BBJ_SWITCH, BBJ_EHFINALLYRET,
            BBJ_RETURN, BBJ_THROW, BBJ_EHFAULTRET
        ];

        foreach (var kind in kinds)
        {
            yield return new TestCaseData(kind, false);
            yield return new TestCaseData(kind, true);
        }
    }

    [TestCaseSource(nameof(SplitCases))]
    public static void TransfersControlFlowWithoutReplacingEdges(BBKinds kind, bool atBeginning)
    {
        WithCompiler(compiler => {
            var source = NewBlock(compiler, kind);
            var other = NewBlock(compiler, BBJ_COND);
            var firstTarget = NewBlock(compiler, BBJ_RETURN);
            var secondTarget = NewBlock(compiler, BBJ_RETURN);
            source.Next = other;
            other.Next = firstTarget;
            firstTarget.Next = secondTarget;
            compiler.fgFirstBB = source;
            compiler.fgLastBB = secondTarget;

            BBJumpTable? descriptor = null;

            if (source.HasTarget)
            {
                source.SetKindAndTargetEdge(kind, compiler.fgAddRefPred(firstTarget, source));
            }
            else if (kind is BBJ_COND or BBJ_SWITCH or BBJ_EHFINALLYRET)
            {
                var first = compiler.fgAddRefPred(firstTarget, source);
                var second = compiler.fgAddRefPred(secondTarget, source);
                first.Likelihood = 0.25;
                second.Likelihood = 0.75;

                if (kind is BBJ_COND)
                {
                    source.SetCond(first, second);
                }
                else if (kind is BBJ_SWITCH)
                {
                    var targets = new BBswtDesc([first, second], [0, 1, 0], hasDefault: true, dominantCase: 1);
                    targets.Cases[0] = first;
                    targets.Cases[1] = second;
                    targets.Cases[2] = compiler.fgAddRefPred(firstTarget, source);
                    source.SwitchTargets = targets;
                    descriptor = targets;
                }
                else
                {
                    descriptor = new BBJumpTable([first, second]);
                    source.SetEhf(descriptor);
                }
            }

            other.SetCond(compiler.fgAddRefPred(firstTarget, other), compiler.fgAddRefPred(secondTarget, other));
            other.TrueEdge.Likelihood = 0.4;
            other.FalseEdge.Likelihood = 0.6;
            var successors = source.Succs.Edges.ToArray();
            var firstRefs = firstTarget.bbRefs;
            var secondRefs = secondTarget.bbRefs;
            var likelihoods = Array.ConvertAll(successors, edge => edge.Likelihood);
            var duplicates = Array.ConvertAll(successors, edge => edge.DupCount);
            source.setBBProfileWeight(42);
            source.SetFlags(BBF_INTERNAL | BBF_COLD | BBF_KEEP_BBJ_ALWAYS | BBF_OSR_PATCHPOINT |
                BBF_BACKWARD_JUMP_TARGET | BBF_LOOP_ALIGN | BBF_GC_SAFE_POINT | BBF_HAS_JMP | BBF_RETLESS_CALL);
            var originalFlags = source.FlagsRaw;
            var count = compiler.fgBBcount;

            var split = atBeginning ? compiler.fgSplitBlockAtBeginning(source) : compiler.fgSplitBlockAtEnd(source);
            var removed = BBF_KEEP_BBJ_ALWAYS | BBF_OSR_PATCHPOINT | BBF_BACKWARD_JUMP_TARGET | BBF_LOOP_ALIGN;
            var sourceRemoved = BBF_HAS_JMP | BBF_RETLESS_CALL;

            if (atBeginning)
            {
                sourceRemoved |= BBF_GC_SAFE_POINT;
            }
            else
            {
                removed |= BBF_GC_SAFE_POINT;
            }

            Assert.Multiple(() => {
                Assert.That(split.Kind, Is.EqualTo(kind));
                Assert.That(split.Succs.Edges.ToArray(), Is.EqualTo(successors));
                Assert.That(split.FlagsRaw, Is.EqualTo(originalFlags & ~removed));
                Assert.That(source.FlagsRaw, Is.EqualTo(originalFlags & ~sourceRemoved));
                Assert.That(split.bbWeight, Is.EqualTo(42));
                Assert.That(split.bbRefs, Is.EqualTo(1));
                Assert.That(source.Kind, Is.EqualTo(BBJ_ALWAYS));
                Assert.That(source.Target, Is.SameAs(split));
                Assert.That(source.TargetEdge.Likelihood, Is.EqualTo(1));
                Assert.That(split.bbPreds, Is.SameAs(source.TargetEdge));
                Assert.That(source.Next, Is.SameAs(split));
                Assert.That(split.Prev, Is.SameAs(source));
                Assert.That(split.Next, Is.SameAs(other));
                Assert.That(other.Prev, Is.SameAs(split));
                Assert.That(compiler.fgLastBB, Is.SameAs(secondTarget));
                Assert.That(compiler.fgBBcount, Is.EqualTo(count + 1));
                Assert.That(firstTarget.bbRefs, Is.EqualTo(firstRefs));
                Assert.That(secondTarget.bbRefs, Is.EqualTo(secondRefs));
            });

            for (var i = 0; i < successors.Length; i++)
            {
                var edge = successors[i];
                var firstPred = edge.DestinationBlock.bbPreds ?? throw new InvalidOperationException("Missing predecessor.");
                Assert.Multiple(() => {
                    Assert.That(edge.SourceBlock, Is.SameAs(split));
                    Assert.That(edge.Likelihood, Is.EqualTo(likelihoods[i]));
                    Assert.That(edge.DupCount, Is.EqualTo(duplicates[i]));
                    Assert.That(firstPred.SourceBlock, Is.SameAs(other));
                    Assert.That(firstPred.NextPredEdge, Is.SameAs(edge));
                    Assert.That(edge.NextPredEdge, Is.Null);
                });
            }

            if (kind is BBJ_SWITCH)
            {
                Assert.Multiple(() => {
                    Assert.That(split.SwitchTargets, Is.SameAs(descriptor));
                    Assert.That(split.SwitchTargets.Cases[0], Is.SameAs(successors[0]));
                    Assert.That(split.SwitchTargets.DefaultCase, Is.SameAs(successors[0]));
                    Assert.That(split.SwitchTargets.DominantCase, Is.EqualTo(1));
                });
            }
            else if (kind is BBJ_EHFINALLYRET)
            {
                Assert.That(split.EhfTargets, Is.SameAs(descriptor));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MovesDuplicateConditionalSelfEdgesOnce(bool atBeginning)
    {
        WithCompiler(compiler => {
            var source = NewBlock(compiler, BBJ_COND);
            compiler.fgFirstBB = source;
            compiler.fgLastBB = source;
            var edge = compiler.fgAddRefPred(source, source);
            source.SetCond(edge, compiler.fgAddRefPred(source, source));
            edge.Likelihood = 1;

            var split = atBeginning ? compiler.fgSplitBlockAtBeginning(source) : compiler.fgSplitBlockAtEnd(source);

            Assert.Multiple(() => {
                Assert.That(split.TrueEdge, Is.SameAs(edge));
                Assert.That(split.FalseEdge, Is.SameAs(edge));
                Assert.That(split.Succs.Edges.Length, Is.EqualTo(1));
                Assert.That(edge.SourceBlock, Is.SameAs(split));
                Assert.That(edge.DestinationBlock, Is.SameAs(source));
                Assert.That(edge.DupCount, Is.EqualTo(2));
                Assert.That(source.bbRefs, Is.EqualTo(2));
                Assert.That(source.bbPreds, Is.SameAs(edge));
                Assert.That(edge.NextPredEdge, Is.Null);
                Assert.That(compiler.fgLastBB, Is.SameAs(split));
                Assert.That(split.Next, Is.Null);
            });
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void PreservesCodeOwnershipAndILRange(bool lir, bool atBeginning)
    {
        WithCompiler(compiler => {
            compiler.compRationalIRForm = lir;
            var source = NewBlock(compiler, BBJ_RETURN);
            compiler.fgFirstBB = source;
            compiler.fgLastBB = source;
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var last = compiler.gtNewIconNode(TYP_INT, 2);
            Statement? firstStmt = null;
            Statement? lastStmt = null;

            if (lir)
            {
                first.Next = last;
                last.Prev = first;
                source.FirstLIRNode = first;
                source.LastLIRNode = last;
            }
            else
            {
                firstStmt = compiler.gtNewStmt(first);
                lastStmt = compiler.gtNewStmt(last);
                compiler.fgInsertStmtAtEnd(source, firstStmt);
                compiler.fgInsertStmtAtEnd(source, lastStmt);
            }

            source.bbCodeOffs = 10;
            source.bbCodeOffsEnd = 20;
            source.SetFlags(BBF_GC_SAFE_POINT);

            var split = atBeginning ? compiler.fgSplitBlockAtBeginning(source) : compiler.fgSplitBlockAtEnd(source);
            var codeBlock = atBeginning ? split : source;
            var emptyBlock = atBeginning ? source : split;

            Assert.Multiple(() => {
                Assert.That(codeBlock.IsLIR, Is.EqualTo(lir));
                Assert.That(emptyBlock.IsLIR, Is.EqualTo(lir));
                Assert.That(codeBlock.bbCodeOffs, Is.EqualTo(10));
                Assert.That(codeBlock.bbCodeOffsEnd, Is.EqualTo(20));
                Assert.That(emptyBlock.bbCodeOffs, Is.EqualTo(BAD_IL_OFFSET));
                Assert.That(emptyBlock.bbCodeOffsEnd, Is.EqualTo(BAD_IL_OFFSET));
                Assert.That(codeBlock.HasFlag(BBF_GC_SAFE_POINT), Is.True);
                Assert.That(emptyBlock.HasFlag(BBF_GC_SAFE_POINT), Is.False);
                Assert.That(emptyBlock.FirstStmt, Is.Null);
                Assert.That(emptyBlock.FirstLIRNode, Is.Null);
                Assert.That(emptyBlock.LastLIRNode, Is.Null);
            });

            if (lir)
            {
                Assert.Multiple(() => {
                    Assert.That(codeBlock.FirstLIRNode, Is.SameAs(first));
                    Assert.That(codeBlock.LastLIRNode, Is.SameAs(last));
                    Assert.That(first.Next, Is.SameAs(last));
                    Assert.That(last.Prev, Is.SameAs(first));
                });
            }
            else
            {
                if ((firstStmt is null) || (lastStmt is null))
                {
                    throw new InvalidOperationException("Statement fixture was not initialized.");
                }

                Assert.Multiple(() => {
                    Assert.That(codeBlock.FirstStmt, Is.SameAs(firstStmt));
                    Assert.That(firstStmt.NextStmt, Is.SameAs(lastStmt));
                    Assert.That(firstStmt.PrevStmt, Is.SameAs(lastStmt));
                    Assert.That(lastStmt.PrevStmt, Is.SameAs(firstStmt));
                    Assert.That(lastStmt.NextStmt, Is.Null);
                });
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ExtendsAllEnclosingRegionsWithoutMovingTheirBeginnings(bool atBeginning)
    {
        WithCompiler(compiler => {
            var outerTry = NewBlock(compiler, BBJ_RETURN);
            var source = NewBlock(compiler, BBJ_RETURN);
            var innerHandler = NewBlock(compiler, BBJ_EHFAULTRET);
            outerTry.Next = source;
            source.Next = innerHandler;
            compiler.fgFirstBB = outerTry;
            compiler.fgLastBB = innerHandler;
            source.TryIndex = 0;
            source.HndIndex = 1;
            source.CatchType = BBCT_FAULT;
            compiler.compHndBBtab = [
                new EHblkDsc { ebdTryBeg = source, ebdTryLast = source, ebdHndBeg = innerHandler, ebdHndLast = innerHandler },
                new EHblkDsc { ebdTryBeg = outerTry, ebdTryLast = outerTry, ebdHndBeg = source, ebdHndLast = source }
            ];
            compiler.compHndBBtabCount = 2;

            var split = atBeginning ? compiler.fgSplitBlockAtBeginning(source) : compiler.fgSplitBlockAtEnd(source);

            Assert.Multiple(() => {
                Assert.That(split.TryIndex, Is.EqualTo(0));
                Assert.That(split.HndIndex, Is.EqualTo(1));
                Assert.That(split.CatchType, Is.EqualTo(BBCT_NONE));
                Assert.That(source.CatchType, Is.EqualTo(BBCT_FAULT));
                Assert.That(compiler.compHndBBtab[0].ebdTryBeg, Is.SameAs(source));
                Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(split));
                Assert.That(compiler.compHndBBtab[1].ebdHndBeg, Is.SameAs(source));
                Assert.That(compiler.compHndBBtab[1].ebdHndLast, Is.SameAs(split));
                Assert.That(compiler.compHndBBtab[0].ebdHndLast, Is.SameAs(innerHandler));
                Assert.That(compiler.compHndBBtab[1].ebdTryLast, Is.SameAs(outerTry));
            });
        });
    }

    [Test]
    public static void ReordersAnExistingEdgeBeforeEarlierPredecessors()
    {
        WithCompiler(compiler => {
            var replacement = NewBlock(compiler, BBJ_ALWAYS);
            var middle = NewBlock(compiler, BBJ_ALWAYS);
            var oldSource = NewBlock(compiler, BBJ_ALWAYS);
            var target = NewBlock(compiler, BBJ_RETURN);
            var middleEdge = compiler.fgAddRefPred(target, middle);
            var edge = compiler.fgAddRefPred(target, oldSource);
            edge.Likelihood = 0.5;
            edge.isHeuristicBased = true;
            compiler.fgReplacePred(edge, replacement);

            Assert.Multiple(() => {
                Assert.That(target.bbPreds, Is.SameAs(edge));
                Assert.That(edge.NextPredEdge, Is.SameAs(middleEdge));
                Assert.That(middleEdge.NextPredEdge, Is.Null);
                Assert.That(edge.SourceBlock, Is.SameAs(replacement));
                Assert.That(target.bbRefs, Is.EqualTo(2));
                Assert.That(edge.DupCount, Is.EqualTo(1));
                Assert.That(edge.Likelihood, Is.EqualTo(0.5));
                Assert.That(edge.isHeuristicBased, Is.True);
            });
        });
    }

    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 0)]
    [TestCase(3, 0)]
    [TestCase(3, 1)]
    [TestCase(3, 2)]
    public static void EnumeratesEachEHClauseByReferenceAndStops(int count, int start)
    {
        WithCompiler(compiler => {
            compiler.compHndBBtab = new EHblkDsc[count];
            compiler.compHndBBtabCount = (ushort)count;
            var clauses = start == 0 ? new EHClauses(compiler) : new EHClauses(compiler, (ushort)start);
            var iterator = clauses.GetEnumerator();
            var marker = NewBlock(compiler, BBJ_RETURN);

            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = start; i < count; i++)
                {
                    Assert.That(iterator.MoveNext(), Is.True);
                    Assert.That(Unsafe.AreSame(ref iterator.Current, ref compiler.compHndBBtab[i]), Is.True);
                    iterator.Current.ebdTryBeg = marker;
                    Assert.That(compiler.compHndBBtab[i].ebdTryBeg, Is.SameAs(marker));
                }

                Assert.That(iterator.MoveNext(), Is.False);
                Assert.That(iterator.MoveNext(), Is.False);
                iterator.Reset();
            }
        });
    }

    [TestCase(0, BAD_IL_OFFSET, 20)]
    [TestCase(1, BAD_IL_OFFSET, 20)]
    [TestCase(2, 10, 10)]
    [TestCase(3, 20, BAD_IL_OFFSET)]
    public static void StatementSplitsPreserveListsAndUnsignedRootOffsets(int scenario, int end, int start)
    {
        WithCompiler(compiler => {
            var block = NewBlock(compiler, BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = block;
            block.bbCodeOffs = 2;
            block.bbCodeOffsEnd = 20;
            Statement? splitAfter = null;

            if (scenario != 3)
            {
                splitAfter = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 1));
                compiler.fgInsertStmtAtEnd(block, splitAfter);
            }

            Statement? suffix = null;
            Statement? last = null;

            if (scenario is 1 or 2)
            {
                suffix = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 2));
                compiler.fgInsertStmtAtEnd(block, suffix);
                last = suffix;

                if (scenario == 2)
                {
                    var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
                    var root = new InlineContext(strategy) { _ilSize = 11 };
                    var child = new InlineContext(strategy) { _parent = root, _location = new ILLocation(10, 0), _ilSize = 124 };
#if DEBUG
                    root._ilInstsSet = new BitArray(11, true);
                    child._ilInstsSet = new BitArray(124, true);
#endif
                    last = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 3), new DebugInfo(child, new ILLocation(123, 0)));
                    compiler.fgInsertStmtAtEnd(block, last);
                }
            }

            var bottom = compiler.fgSplitBlockAfterStatement(block, splitAfter);

            Assert.Multiple(() => {
                Assert.That(block.bbCodeOffs, Is.EqualTo(2));
                Assert.That(block.bbCodeOffsEnd, Is.EqualTo(end));
                Assert.That(bottom.bbCodeOffs, Is.EqualTo(start));
                Assert.That(bottom.bbCodeOffsEnd, Is.EqualTo(scenario == 3 ? BAD_IL_OFFSET : 20));
                Assert.That(block.FirstStmt, Is.SameAs(splitAfter));
                Assert.That(splitAfter?.PrevStmt, Is.SameAs(splitAfter));
                Assert.That(splitAfter?.NextStmt, Is.Null);
                Assert.That(bottom.FirstStmt, Is.SameAs(suffix));
                Assert.That(suffix?.PrevStmt, Is.SameAs(last));
                Assert.That(last?.NextStmt, Is.Null);
                Assert.That(block.Target, Is.SameAs(bottom));
                Assert.That(bottom.Kind, Is.EqualTo(BBJ_RETURN));
                Assert.That(compiler.fgFindBlockILOffset(bottom), Is.EqualTo(scenario == 2 ? 10 : BAD_IL_OFFSET));
            });
        });
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;
        return block;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
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
