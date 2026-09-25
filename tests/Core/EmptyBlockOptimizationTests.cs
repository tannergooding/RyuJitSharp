// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmptyBlockOptimizationTests
{
    [Test]
    public static void LeavesNonBranchEmptyBlocksUnchanged(
        [Values(BBJ_THROW, BBJ_CALLFINALLY, BBJ_CALLFINALLYRET, BBJ_RETURN,
            BBJ_EHCATCHRET, BBJ_EHFINALLYRET, BBJ_EHFAULTRET, BBJ_EHFILTERRET)] BBKinds kind,
        [Values(false, true)] bool lir)
    {
        WithCompiler(lir, compiler => {
            var (prefix, block, target) = CreateGraph(compiler);
            block.Kind = kind;
            var count = compiler.fgBBcount;

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.False);
            Assert.That(compiler.fgBBcount, Is.EqualTo(count));
            Assert.That(prefix.Next, Is.SameAs(block));
            Assert.That(block.Next, Is.SameAs(target));
            Assert.That(block.Kind, Is.EqualTo(kind));
            Assert.That(block.HasFlag(BBF_REMOVED), Is.False);
            Assert.That(compiler.compCurBB, Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void KeepsSelfLoopsIncludingTheOnlyBlock(bool lir, bool singleton)
    {
        WithCompiler(lir, compiler => {
            var block = NewBlock(compiler);
            if (singleton)
            {
                LinkBlocks(compiler, block);
            }
            else
            {
                var prefix = NewBlock(compiler);
                LinkBlocks(compiler, prefix, block);
            }

            Jump(compiler, block, block);
            var refs = block.bbRefs;

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.False);
            Assert.That(block.Target, Is.SameAs(block));
            Assert.That(block.bbRefs, Is.EqualTo(refs));
            Assert.That(block.HasFlag(BBF_REMOVED), Is.False);
        });
    }

    [TestCase("nonadjacent")]
    [TestCase("target-try")]
    [TestCase("target-other-pred")]
    [TestCase("debug-user-code")]
    public static void KeepsInitBlocksWhenTheirTargetCannotReplaceThem(string reason)
    {
        WithCompiler(false, compiler => {
            var block = NewBlock(compiler);
            var target = NewBlock(compiler);
            var other = NewBlock(compiler);
            LinkBlocks(compiler, block, target, other);
            Jump(compiler, block, target);

            switch (reason)
            {
                case "nonadjacent":
                {
                    compiler.fgRemoveRefPred(block.TargetEdge);
                    Jump(compiler, block, other);
                    break;
                }

                case "target-try":
                {
                    target.TryIndex = 0;
                    break;
                }

                case "target-other-pred":
                {
                    Jump(compiler, other, target);
                    break;
                }

                case "debug-user-code":
                {
                    compiler.opts.compDbgCode = true;
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown init-block refusal.");
                }
            }

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.False);
            Assert.That(compiler.fgFirstBB, Is.SameAs(block));
            Assert.That(compiler.fgBBcount, Is.EqualTo(3));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemovingInitBlockTransfersEntryReferenceAndIlRange(bool lir)
    {
        WithCompiler(lir, compiler => {
            var block = NewBlock(compiler);
            var target = NewBlock(compiler);
            LinkBlocks(compiler, block, target);
            Jump(compiler, block, target);
            block.bbCodeOffs = 0;
            block.bbCodeOffsEnd = 10;
            target.bbCodeOffs = 10;
            target.bbCodeOffsEnd = 20;

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.True);
            Assert.That(compiler.fgFirstBB, Is.SameAs(target));
            Assert.That(target.Prev, Is.Null);
            Assert.That(target.bbRefs, Is.EqualTo(1));
            Assert.That(target.bbPreds, Is.Null);
            Assert.That(target.bbCodeOffs, Is.Zero);
            Assert.That(target.bbCodeOffsEnd, Is.EqualTo(20));
            Assert.That(block.bbRefs, Is.Zero);
            Assert.That(block.HasFlag(BBF_REMOVED), Is.True);
            Assert.That(compiler.fgBBcount, Is.EqualTo(1));
            Assert.That(compiler.compCurBB, Is.SameAs(block));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ProtectsEntryBlockOnlyForOsr(bool osr, bool isEntry)
    {
        WithCompiler(false, compiler => {
            var (prefix, block, _) = CreateGraph(compiler);
            compiler.fgEntryBB = isEntry ? block : prefix;
            if (osr)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            }

            var keep = osr && isEntry;
            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.EqualTo(!keep));
            Assert.That(block.HasFlag(BBF_REMOVED), Is.EqualTo(!keep));
        });
    }

    [TestCase("empty")]
    [TestCase("nop")]
    [TestCase("phi")]
    [TestCase("phi-nop")]
    [TestCase("lir-empty")]
    [TestCase("lir-offsets")]
    public static void RemovesAllNativeEmptyRepresentationsAndRedirectsPredecessors(string body)
    {
        var lir = body.StartsWith("lir", StringComparison.Ordinal);
        WithCompiler(lir, compiler => {
            var (prefix, block, target) = CreateGraph(compiler);
            if (body is "phi" or "phi-nop")
            {
                var phi = new GenTreeLclVar(TYP_INT, 0, new GenTreePhi(TYP_INT));
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(phi));
            }

            if (body is "nop" or "phi-nop")
            {
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(new GenTree(GT_NOP, TYP_VOID)));
            }

            if (body is "lir-offsets")
            {
                block.InsertAtEnd(new GenTreeILOffset(default));
                block.InsertAtEnd(new GenTreeILOffset(default));
            }

            var other = NewBlock(compiler);
            target.Next = other;
            compiler.fgLastBB = other;
            Jump(compiler, other, block);
            prefix.TargetEdge.Likelihood = 1.0;
            block.SetFlags(BBF_ASYNC_RESUMPTION);
            block.setBBProfileWeight(30);
            target.setBBProfileWeight(40);
            compiler.fgPgoConsistent = true;

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.True);
            Assert.That(prefix.Target, Is.SameAs(target));
            Assert.That(other.Target, Is.SameAs(target));
            Assert.That(target.bbRefs, Is.EqualTo(2));
            Assert.That(block.bbPreds, Is.Null);
            Assert.That(block.bbRefs, Is.Zero);
            Assert.That(prefix.HasFlag(BBF_ASYNC_RESUMPTION), Is.True);
            Assert.That(other.HasFlag(BBF_ASYNC_RESUMPTION), Is.True);
            Assert.That(prefix.TargetEdge.Likelihood, Is.EqualTo(1.0));
            Assert.That(block.bbWeight, Is.EqualTo(30));
            Assert.That(target.bbWeight, Is.EqualTo(40));
            Assert.That(compiler.fgPgoConsistent, Is.True);
            Assert.That(compiler.compCurBB, Is.SameAs(block));
            Assert.That(prefix.Next, Is.SameAs(target));
            Assert.That(target.Prev, Is.SameAs(prefix));
        });
    }

    [Test]
    public static void CatchReturnAcrossEhRegionsGainsARealNopWithoutRemovingExistingContents(
        [Values(NodeThreading.None, NodeThreading.AllLocals, NodeThreading.AllTrees, NodeThreading.LIR)] NodeThreading threading,
        [Values(false, true)] bool handler)
    {
        WithCompiler(threading is NodeThreading.LIR, compiler => {
            compiler.fgNodeThreading = threading;
            var (prefix, block, target) = CreateGraph(compiler);
            prefix.Kind = BBJ_EHCATCHRET;
            if (handler)
            {
                block.HndIndex = 0;
            }
            else
            {
                block.TryIndex = 0;
            }
            GenTree previous;
            Statement? previousStatement = null;
            if (block.IsLIR)
            {
                previous = new GenTreeILOffset(default);
                block.InsertAtEnd(previous);
                compiler.codeGen = new CodeGen(compiler);
                CurrentLowering(compiler) = new Lowering(compiler, new LinearScan(compiler));
            }
            else
            {
                previous = new GenTree(GT_NOP, TYP_VOID);
                previousStatement = compiler.gtNewStmt(previous);
                compiler.fgInsertStmtAtEnd(block, previousStatement);
            }

            var count = compiler.fgBBcount;
            var edge = block.TargetEdge;

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.True);
            Assert.That(compiler.fgBBcount, Is.EqualTo(count));
            Assert.That(block.HasFlag(BBF_REMOVED), Is.False);
            Assert.That(prefix.Target, Is.SameAs(block));
            Assert.That(block.Target, Is.SameAs(target));
            Assert.That(block.TargetEdge, Is.SameAs(edge));
            Assert.That(handler ? block.HndIndex : block.TryIndex, Is.Zero);
            Assert.That(compiler.compCurBB, Is.Null);
            Assert.That(block.isEmpty(), Is.False);

            GenTree nop;
            if (block.IsLIR)
            {
                nop = block.LastNode ?? throw new AssertionException("Missing LIR no-op.");
                Assert.That(block.FirstNode, Is.SameAs(previous));
                Assert.That(previous.Next, Is.SameAs(nop));
                Assert.That(nop.Prev, Is.SameAs(previous));
                Assert.That(nop.Next, Is.Null);
            }
            else
            {
                var statement = block.LastStmt ?? throw new AssertionException("Missing no-op statement.");
                var priorStatement = previousStatement ?? throw new AssertionException("Missing original statement.");
                nop = statement.RootNode;
                Assert.That(block.FirstStmt, Is.SameAs(priorStatement));
                Assert.That(priorStatement.NextStmt, Is.SameAs(statement));
                Assert.That(statement.PrevStmt, Is.SameAs(priorStatement));
                Assert.That(statement.NextStmt, Is.Null);
                if (threading is NodeThreading.AllTrees)
                {
                    Assert.That(statement.TreeListBegin, Is.SameAs(nop));
                }
                else
                {
                    Assert.That(statement.TreeListBegin, Is.Null);
                }

                Assert.That(nop.CostEx, Is.EqualTo(1));
                Assert.That(nop.CostSz, Is.EqualTo(1));
            }

            Assert.That(nop.Oper, Is.EqualTo(GT_NO_OP));
            Assert.That(nop.Type, Is.EqualTo(TYP_VOID));
#if DEBUG
            Assert.That(nop.TreeId, Is.Not.EqualTo(previous.TreeId));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemovalCollapsesADegenerateConditionalPredecessor(bool lir)
    {
        WithCompiler(lir, compiler => {
            var (prefix, block, target) = CreateGraph(compiler);
            var falseEdge = compiler.fgAddRefPred(target, prefix);
            prefix.SetCond(prefix.TargetEdge, falseEdge);
            prefix.TrueEdge.Likelihood = 0.25;
            prefix.FalseEdge.Likelihood = 0.75;
            var value = compiler.gtNewIconNode(TYP_INT, 1);
            var branch = new GenTreeUnOp(GT_JTRUE, TYP_VOID, value);
            if (lir)
            {
                prefix.InsertAtEnd(value);
                prefix.InsertAtEnd(branch);
            }
            else
            {
                compiler.fgInsertStmtAtEnd(prefix, compiler.gtNewStmt(branch));
            }

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.True);
            Assert.That(prefix.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(prefix.Target, Is.SameAs(target));
            Assert.That(prefix.isEmpty(), Is.True);
            Assert.That(target.bbRefs, Is.EqualTo(1));
            Assert.That(target.bbPreds, Is.SameAs(prefix.TargetEdge));
            Assert.That(prefix.TargetEdge.DupCount, Is.EqualTo(1));
            Assert.That(prefix.TargetEdge.NextPredEdge, Is.Null);
            Assert.That(block.bbRefs, Is.Zero);
            Assert.That(block.bbPreds, Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void EhBoundaryAloneOrCatchReturnAloneDoesNotPreventRemoval(bool catchReturn, bool differentRegion)
    {
        WithCompiler(false, compiler => {
            var (prefix, block, target) = CreateGraph(compiler);
            if (catchReturn)
            {
                prefix.Kind = BBJ_EHCATCHRET;
            }

            if (differentRegion)
            {
                target.TryIndex = 0;
            }

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.True);
            Assert.That(prefix.Target, Is.SameAs(target));
            Assert.That(block.HasFlag(BBF_REMOVED), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemovingRegionEndUpdatesTheEhTable(bool handler)
    {
        WithCompiler(false, compiler => {
            var (prefix, block, target) = CreateGraph(compiler);
            if (handler)
            {
                prefix.HndIndex = 0;
                block.HndIndex = 0;
            }
            else
            {
                prefix.TryIndex = 0;
                block.TryIndex = 0;
            }

            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdTryBeg = prefix,
                    ebdTryLast = block,
                    ebdHndBeg = handler ? prefix : target,
                    ebdHndLast = handler ? block : target
                }
            ];
            compiler.compHndBBtabCount = 1;

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.True);
            Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(prefix));
            Assert.That(compiler.compHndBBtab[0].ebdHndLast, Is.SameAs(handler ? prefix : target));
            Assert.That(prefix.Next, Is.SameAs(target));
        });
    }

    [TestCase("next-unprofiled", false)]
    [TestCase("next-internal", false)]
    [TestCase("profile-disabled", true)]
    [TestCase("block-unprofiled", true)]
    [TestCase("block-internal", true)]
    [TestCase("earlier-user-block", true)]
    [TestCase("next-profiled-user-block", true)]
    public static void PreservesTheFirstProfiledUserBlockOnlyWhenRequired(string scenario, bool removed)
    {
        WithCompiler(false, compiler => {
            var (prefix, block, target) = CreateGraph(compiler);
            prefix.SetFlags(BBF_INTERNAL);
            compiler.fgPgoHaveWeights = true;
            block.setBBProfileWeight(10);

            switch (scenario)
            {
                case "next-unprofiled":
                {
                    break;
                }

                case "next-internal":
                {
                    target.setBBProfileWeight(10);
                    target.SetFlags(BBF_INTERNAL);
                    break;
                }

                case "profile-disabled":
                {
                    compiler.fgPgoHaveWeights = false;
                    break;
                }

                case "block-unprofiled":
                {
                    block.RemoveFlags(BBF_PROF_WEIGHT);
                    break;
                }

                case "block-internal":
                {
                    block.SetFlags(BBF_INTERNAL);
                    break;
                }

                case "earlier-user-block":
                {
                    prefix.RemoveFlags(BBF_INTERNAL);
                    break;
                }

                case "next-profiled-user-block":
                {
                    target.setBBProfileWeight(10);
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown profile case.");
                }
            }

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.EqualTo(removed));
            Assert.That(block.HasFlag(BBF_REMOVED), Is.EqualTo(removed));
            Assert.That(compiler.fgPgoConsistent, Is.True);
        });
    }

    [Test]
    public static void KeepsTheLastBlockWhenItIsTheFirstProfiledUserBlock()
    {
        WithCompiler(false, compiler => {
            var target = NewBlock(compiler);
            var block = NewBlock(compiler);
            LinkBlocks(compiler, target, block);
            target.SetFlags(BBF_INTERNAL);
            Jump(compiler, block, target);
            compiler.fgPgoHaveWeights = true;
            block.setBBProfileWeight(10);

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.False);
            Assert.That(compiler.fgLastBB, Is.SameAs(block));
            Assert.That(block.Next, Is.Null);
            Assert.That(block.HasFlag(BBF_REMOVED), Is.False);
        });
    }

    [Test]
    public static void ProfileGuardUsesLexicalNextRatherThanTheJumpTarget()
    {
        WithCompiler(false, compiler => {
            var (prefix, block, next) = CreateGraph(compiler);
            var target = NewBlock(compiler);
            next.Next = target;
            compiler.fgLastBB = target;
            compiler.fgRemoveRefPred(block.TargetEdge);
            Jump(compiler, block, target);
            prefix.SetFlags(BBF_INTERNAL);
            compiler.fgPgoHaveWeights = true;
            block.setBBProfileWeight(10);
            target.setBBProfileWeight(10);

            Assert.That(compiler.fgOptimizeEmptyBlock(block), Is.False);
            Assert.That(block.Target, Is.SameAs(target));
            Assert.That(block.Next, Is.SameAs(next));
            Assert.That(block.HasFlag(BBF_REMOVED), Is.False);
        });
    }

    private static (BasicBlock Prefix, BasicBlock Block, BasicBlock Target) CreateGraph(Compiler compiler)
    {
        var prefix = NewBlock(compiler);
        var block = NewBlock(compiler);
        var target = NewBlock(compiler);
        LinkBlocks(compiler, prefix, block, target);
        Jump(compiler, prefix, block);
        Jump(compiler, block, target);

        return (prefix, block, target);
    }

    private static BasicBlock NewBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        block.bbRefs = 0;

        return block;
    }

    private static void Jump(Compiler compiler, BasicBlock source, BasicBlock target)
    {
        source.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, source));
    }

    private static void LinkBlocks(Compiler compiler, params BasicBlock[] blocks)
    {
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        for (var i = 0; i < blocks.Length - 1; i++)
        {
            blocks[i].Next = blocks[i + 1];
        }

        blocks[0].bbRefs = 1;
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
        compiler.fgPgoConsistent = true;
        compiler.compRationalIRForm = lir;
        compiler.fgNodeThreading = lir ? NodeThreading.LIR : NodeThreading.None;
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pLowering")]
    private static extern ref Lowering? CurrentLowering(Compiler compiler);
}
