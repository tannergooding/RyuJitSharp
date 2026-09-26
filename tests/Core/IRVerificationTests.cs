// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class IRVerificationTests
{
    [TestCase(GT_NOP)]
    [TestCase(GT_JTRUE)]
    [TestCase(GT_BOUNDS_CHECK)]
    public static void VoidOperatorsRejectOtherTypes(genTreeOps oper)
    {
        SsaLivenessTests.WithCompiler(0, compiler =>
            Assert.That(() => compiler.fgDebugCheckType(new GenTree(oper, TYP_INT)), Throws.Exception));
    }

    [Test]
    public static void UnsupportedSmallTypeAndUnsignedTypeFail()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            Assert.That(() => compiler.fgDebugCheckType(new GenTree(GT_NO_OP, TYP_BYTE)), Throws.Exception);
            Assert.That(() => compiler.fgDebugCheckType(new GenTree(GT_NO_OP, TYP_UINT)), Throws.Exception);
            Assert.That(() => compiler.fgDebugCheckType(new GenTreeLclVar(TYP_BYTE, 0)), Throws.Nothing);
        });
    }

    [Test]
    public static void MissingFlagIsFatalEvenWhenRelaxed()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            compiler.activePhaseChecks = PhaseChecks.CHECK_IR | PhaseChecks.CHECK_IR_RELAXED;
            var block = AddBlock(compiler);
            var tree = new GenTree(GT_CATCH_ARG, TYP_REF);
            tree.Flags &= ~GTF_ORDER_SIDEEFF;

            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckFlagsAndTypes(tree, block), Throws.Exception));

            Assert.That(output, Does.Contain($"Missing flags on tree [{tree.TreeId:D6}]: "));
            Assert.That(compiler.Metrics.IRExtraFlags, Is.Zero);
        });
    }

    [Test]
    public static void ExtraFlagIsCountedOnlyInRelaxedMode()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var tree = compiler.gtNewIconNode(TYP_INT, 7);
            tree.Flags |= GTF_ASG | GTF_CALL;
            compiler.activePhaseChecks = PhaseChecks.CHECK_IR | PhaseChecks.CHECK_IR_RELAXED;

            Assert.That(() => compiler.fgDebugCheckFlagsHelper(tree, tree.Flags & GTF_ALL_EFFECT, GTF_EMPTY),
                Throws.Nothing);
            Assert.That(compiler.Metrics.IRExtraFlags, Is.EqualTo(2));

            compiler.activePhaseChecks = PhaseChecks.CHECK_IR;
            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.fgDebugCheckFlagsHelper(tree, tree.Flags & GTF_ALL_EFFECT, GTF_EMPTY),
                    Throws.Exception));

            Assert.That(output, Does.Contain($"Extra flags on tree [{tree.TreeId:D6}]: "));
            Assert.That(compiler.Metrics.IRExtraFlags, Is.EqualTo(2));
        });
    }

    [Test]
    public static void StatementLinksRejectBrokenCircularPredecessor()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var block = AddBlock(compiler);
            compiler.fgNodeThreading = NodeThreading.None;
            var stmt = new Statement(new GenTree(GT_NO_OP, TYP_VOID), 0);
            compiler.fgInsertStmtAtEnd(block, stmt);
            Assert.That(() => compiler.fgDebugCheckStmtsList(block), Throws.Nothing);

            stmt.PrevStmt = null;
            Assert.That(() => compiler.fgDebugCheckStmtsList(block), Throws.Exception);
        });
    }

    [Test]
    public static void InvalidReturnPlacementIsRejected()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var block = AddBlock(compiler);
            var stmt = new Statement(new GenTreeUnOp(GT_RETURN, TYP_VOID, null), 0);
            compiler.fgInsertStmtAtEnd(block, stmt);
            compiler.fgInsertStmtAtEnd(block, new Statement(new GenTree(GT_NO_OP, TYP_VOID), 1));

            Assert.That(() => compiler.fgDebugCheckStmtsList(block), Throws.Exception);
        });
    }

    [Test]
    public static void LinkDriverRunsStatementChecksAndSsaPostcheck()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var block = AddBlock(compiler);
            compiler.fgNodeThreading = NodeThreading.None;
            var tree = new GenTree(GT_NO_OP, TYP_VOID);
            compiler.fgInsertStmtAtEnd(block, new Statement(tree, 0));
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler.fgSsaPassesCompleted = 1;
            compiler.fgSsaValid = true;
            compiler.verbose = true;

            var output = CodeGenLifeTransitionTests.Capture(compiler.fgDebugCheckLinks);
            Assert.That(output, Does.Contain($"SSA checks completed successfully{Environment.NewLine}"));

            tree.Flags |= GTF_CALL;
            Assert.That(() => compiler.fgDebugCheckLinks(), Throws.Exception);
        });
    }

    [Test]
    public static void LargeGraphGuardPrecedesBlockAndSsaChecks()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            compiler.fgBBcount = 10001;
            compiler.expensiveDebugCheckLevel = 0;

            Assert.That(() => compiler.fgDebugCheckLinks(), Throws.Nothing);
        });
    }

    [Test]
    public static void LinkDriverUsesExistingLirVerifier()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var block = AddBlock(compiler);
            block.MakeLir(null, null);
            var node = compiler.gtNewIconNode(TYP_INT, 9);
            block.InsertAtEnd(node);
            compiler.activePhaseChecks = PhaseChecks.CHECK_IR | PhaseChecks.CHECK_LIR_UNUSED_VALUES;

            Assert.That(() => compiler.fgDebugCheckLinks(), Throws.Exception);

            node.IsUnusedValue = true;
            Assert.That(() => compiler.fgDebugCheckLinks(), Throws.Nothing);
        });
    }

    private static BasicBlock AddBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        return block;
    }
}
#endif
