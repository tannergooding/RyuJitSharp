// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class FlowGraphUpdateTests
{
#if DEBUG
    [Test]
    public static void DumpsReuseTheirBlockOrderAfterGraphShrinks()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var first = Block(compiler);
            var last = Block(compiler);
            Link(compiler, first, last);
            Jump(compiler, first, last);
            compiler.fgDispBasicBlocks();
            Assert.That(compiler.fgBBOrder, Is.EqualTo<BasicBlock[]>([first, last]));

            Assert.That(compiler.fgUpdateFlowGraphPhase(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            compiler.fgDispBasicBlocks();
            Assert.That(compiler.fgBBOrder, Is.EqualTo<BasicBlock[]>([first]));
        });
    }
#endif

    [TestCase(false)]
    [TestCase(true)]
    public static void CompactsTheEntireChainAndReportsAnIdempotentPhase(bool lir)
    {
        FlowGraphCleanupTests.WithCompiler(lir ? NodeThreading.LIR : NodeThreading.None, compiler => {
            compiler.compRationalIRForm = lir;
            var first = Block(compiler);
            var middle = Block(compiler);
            var last = Block(compiler);
            Link(compiler, first, middle, last);
            Jump(compiler, first, middle);
            Jump(compiler, middle, last);
            var one = new GenTree(GT_NO_OP, TYP_VOID);
            var two = new GenTree(GT_NO_OP, TYP_VOID);
            Append(compiler, first, one);
            Append(compiler, middle, two);

            Assert.That(compiler.fgUpdateFlowGraphPhase(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.fgBBcount, Is.EqualTo(1));
            Assert.That(compiler.fgLastBB, Is.SameAs(first));
            Assert.That(first.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(middle.HasFlag(BBF_REMOVED) && last.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(lir ? first.FirstNode : first.FirstStmt?.RootNode, Is.SameAs(one));
            Assert.That(lir ? first.LastNode : first.LastStmt?.RootNode, Is.SameAs(two));
            Assert.That(compiler.fgUpdateFlowGraphPhase(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ReversesBranchesAndPreservesLikelihoodsAndEhEnds(bool lir, bool eh)
    {
        FlowGraphCleanupTests.WithCompiler(lir ? NodeThreading.LIR : NodeThreading.None, compiler => {
            compiler.compRationalIRForm = lir;
            var block = Block(compiler);
            var bypass = Block(compiler);
            var taken = Block(compiler, protect: true);
            var exit = Block(compiler, protect: true);
            Link(compiler, block, bypass, taken, exit);
            var test = Conditional(compiler, block, taken, bypass);
            Jump(compiler, bypass, exit);
            bypass.SetFlags(BBF_ASYNC_RESUMPTION);
            if (lir)
            {
                bypass.InsertAtEnd(new GenTreeILOffset(default));
            }

            if (eh)
            {
                block.TryIndex = 0;
                bypass.TryIndex = 0;
                block.SetFlags(BBF_DONT_REMOVE);
                compiler.compHndBBtab = [
                    new EHblkDsc { ebdTryBeg = block, ebdTryLast = bypass, ebdHndBeg = exit, ebdHndLast = exit }
                ];
                compiler.compHndBBtabCount = 1;
            }

            Assert.That(compiler.fgUpdateFlowGraphPhase(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(block.TrueTarget, Is.SameAs(exit));
            Assert.That(block.FalseTarget, Is.SameAs(taken));
            Assert.That(block.Next, Is.SameAs(taken));
            Assert.That(taken.Prev, Is.SameAs(block));
            Assert.That(block.TrueEdge.Likelihood, Is.EqualTo(0.75));
            Assert.That(block.FalseEdge.Likelihood, Is.EqualTo(0.25));
            Assert.That(block.HasFlag(BBF_ASYNC_RESUMPTION), Is.True);
            Assert.That(bypass.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(bypass.bbRefs, Is.Zero);
            Assert.That(exit.bbRefs, Is.EqualTo(1));
            if (lir)
            {
                Assert.That(test.AsCC().Condition.Code, Is.EqualTo(GenCondition.NE));
            }
            else
            {
                Assert.That(test.AsUnOp().Op1.Oper, Is.EqualTo(GT_NE));
            }

            if (eh)
            {
                Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(block));
            }
            Assert.That(compiler.fgUpdateFlowGraphPhase(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RelocatesJoinFreeTargetAndUpdatesRegionEnd(bool eh)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = Block(compiler, protect: true);
            var bypass = Block(compiler);
            var barrier = Block(compiler, protect: true);
            var taken = Block(compiler, protect: true);
            var exit = Block(compiler, protect: true);
            Link(compiler, block, bypass, barrier, taken, exit);
            _ = Conditional(compiler, block, taken, bypass);
            Jump(compiler, bypass, exit);
            Jump(compiler, barrier, exit);
            Append(compiler, barrier, new GenTree(GT_NO_OP, TYP_VOID));
            if (eh)
            {
                foreach (var item in new[] { block, bypass, barrier, taken })
                {
                    item.TryIndex = 0;
                }
                compiler.compHndBBtab = [
                    new EHblkDsc { ebdTryBeg = block, ebdTryLast = taken, ebdHndBeg = exit, ebdHndLast = exit }
                ];
                compiler.compHndBBtabCount = 1;
            }

            Assert.That(compiler.fgUpdateFlowGraphPhase(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.Next, Is.SameAs(taken));
            Assert.That(taken.Next, Is.SameAs(barrier));
            Assert.That(barrier.Next, Is.SameAs(exit));
            Assert.That(block.TrueTarget, Is.SameAs(exit));
            Assert.That(block.FalseTarget, Is.SameAs(taken));
            Assert.That(exit.bbRefs, Is.EqualTo(2));
            if (eh)
            {
                Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(barrier));
            }
            Assert.That(compiler.fgUpdateFlowGraphPhase(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }

    [TestCase(BBJ_RETURN, false)]
    [TestCase(BBJ_ALWAYS, false)]
    [TestCase(BBJ_COND, false)]
    [TestCase(BBJ_RETURN, true)]
    [TestCase(BBJ_ALWAYS, true)]
    [TestCase(BBJ_COND, true)]
    public static void RemovesUnreachableBlocksAndSelfLoopsUnlessProtected(BBKinds kind, bool protect)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, protect: true);
            var dead = Block(compiler, protect);
            var exit = Block(compiler, protect: true);
            Link(compiler, entry, dead, exit);
            if (kind is BBJ_ALWAYS)
            {
                Jump(compiler, dead, dead);
            }
            else if (kind is BBJ_COND)
            {
                _ = Conditional(compiler, dead, dead, exit);
            }

            Assert.That(compiler.fgUpdateFlowGraph(doTailDuplication: false, isPhase: true), Is.EqualTo(!protect));
            Assert.That(dead.HasFlag(BBF_REMOVED), Is.EqualTo(!protect));
            Assert.That(entry.Next, Is.SameAs(protect ? dead : exit));
            Assert.That(compiler.fgBBcount, Is.EqualTo(protect ? 3 : 2));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void InvalidatesOsrProfileOnlyWhenGraphChanges(bool osr, bool change)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            if (osr)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            }
            var entry = Block(compiler, protect: true);
            var dead = Block(compiler, protect: !change);
            Link(compiler, entry, dead);

            Assert.That(compiler.fgUpdateFlowGraph(isPhase: true), Is.EqualTo(change));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!osr || !change));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OptionalTailDuplicationNormalizesReturns(bool enabled)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            compiler.info.compRetType = TYP_UBYTE;
            var entry = Block(compiler);
            Link(compiler, entry);
            var comparison = Comparison(compiler, 0);
            Append(compiler, entry, new GenTreeUnOp(GT_RETURN, TYP_INT, comparison));

            Assert.That(compiler.fgUpdateFlowGraph(enabled, isPhase: true), Is.EqualTo(enabled));
            Assert.That(entry.Kind, Is.EqualTo(enabled ? BBJ_COND : BBJ_RETURN));
            Assert.That(compiler.fgBBcount, Is.EqualTo(enabled ? 3 : 1));
        });
    }

    [Test]
    [CancelAfter(10000)]
    public static void TailDuplicationTerminatesForAConditionalCycle()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var entry = Block(compiler, protect: true);
            var first = Block(compiler, protect: true);
            var second = Block(compiler, protect: true);
            var exit = Block(compiler, protect: true);
            Link(compiler, entry, first, second, exit);
            Append(compiler, entry, compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1)));
            Jump(compiler, entry, first);
            _ = Conditional(compiler, first, exit, second, value: 0);
            _ = Conditional(compiler, second, exit, first, value: 2);

            Assert.That(compiler.fgUpdateFlowGraph(doTailDuplication: true, isPhase: true), Is.True);
            Assert.That(entry.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(entry.Target, Is.SameAs(first));
            Assert.That(entry.Statements.Count(), Is.EqualTo(1));
            Assert.That(first.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(second.Kind, Is.EqualTo(BBJ_COND));
        });
    }

    [Test]
    public static void EhEndQueriesUseTheMostNestedDescriptor()
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.None, compiler => {
            var block = Block(compiler);
            var outerEnd = Block(compiler);
            compiler.compHndBBtab = [
                new EHblkDsc { ebdTryLast = block, ebdHndLast = block },
                new EHblkDsc { ebdTryLast = outerEnd, ebdHndLast = outerEnd }
            ];
            compiler.compHndBBtabCount = 2;
            Assert.That(compiler.ehIsBlockEHLast(block), Is.False);
            block.TryIndex = 0;
            block.HndIndex = 0;
            Assert.That(Unsafe.AreSame(ref compiler.ehIsBlockTryLast(block), ref compiler.compHndBBtab[0]), Is.True);
            Assert.That(Unsafe.AreSame(ref compiler.ehIsBlockHndLast(block), ref compiler.compHndBBtab[0]), Is.True);
            block.TryIndex = 1;
            block.HndIndex = 1;
            Assert.That(Unsafe.IsNullRef(in compiler.ehIsBlockTryLast(block)), Is.True);
            Assert.That(Unsafe.IsNullRef(in compiler.ehIsBlockHndLast(block)), Is.True);
        });
    }

    private static BasicBlock Block(Compiler compiler, bool protect = false)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        block.bbRefs = 0;
        if (protect)
        {
            block.SetFlags(BBF_DONT_REMOVE);
        }

        return block;
    }

    private static void Link(Compiler compiler, params BasicBlock[] blocks)
    {
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        blocks[0].bbRefs++;
        for (var i = 1; i < blocks.Length; i++)
        {
            blocks[i - 1].Next = blocks[i];
        }
    }

    private static void Jump(Compiler compiler, BasicBlock block, BasicBlock target)
    {
        block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, block));
    }

    private static GenTree Conditional(Compiler compiler, BasicBlock block, BasicBlock taken, BasicBlock other, int value = 0)
    {
        block.SetCond(compiler.fgAddRefPred(taken, block), compiler.fgAddRefPred(other, block));
        block.TrueEdge.Likelihood = 0.25;
        block.FalseEdge.Likelihood = 0.75;
        GenTree test = block.IsLIR
            ? new GenTreeCC(GT_JCC, TYP_VOID, new GenCondition(GenCondition.EQ))
            : new GenTreeUnOp(GT_JTRUE, TYP_VOID, Comparison(compiler, value));
        Append(compiler, block, test);

        return test;
    }

    private static GenTreeOp Comparison(Compiler compiler, int value)
    {
        var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
            compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, value));
        comparison.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;

        return comparison;
    }

    private static void Append(Compiler compiler, BasicBlock block, GenTree tree)
    {
        if (block.IsLIR)
        {
            block.InsertAtEnd(tree);
        }
        else
        {
            compiler.fgInsertStmtAtEnd(block, compiler.fgNewStmtFromTree(tree));
        }
    }
}
