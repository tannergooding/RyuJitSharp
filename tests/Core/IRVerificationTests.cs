// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class IRVerificationTests
{
    [SetUp]
    public static void RequireDebugConfiguration()
    {
        Assume.That(TestConfiguration.IsDebug, "IR verification checks are Debug-only.");
    }

    [TestCase(GT_NOP)]
    [TestCase(GT_JTRUE)]
    [TestCase(GT_BOUNDS_CHECK)]
    public static void VoidOperatorsRejectOtherTypes(genTreeOps oper)
    {
        SsaLivenessTests.WithCompiler(0, compiler =>
            Assert.That(() => CheckType(compiler, new GenTree(oper, TYP_INT)), Throws.Exception));
    }

    [Test]
    public static void UnsupportedSmallTypeAndUnsignedTypeFail()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            Assert.That(() => CheckType(compiler, new GenTree(GT_NO_OP, TYP_BYTE)), Throws.Exception);
            Assert.That(() => CheckType(compiler, new GenTree(GT_NO_OP, TYP_UINT)), Throws.Exception);
            Assert.That(() => CheckType(compiler, new GenTreeLclVar(TYP_BYTE, 0)), Throws.Nothing);
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
                Assert.That(() => CheckFlagsAndTypes(compiler, tree, block), Throws.Exception));

            Assert.That(output, Does.Contain($"Missing flags on tree [{tree.TreeId:D6}]: "));
            Assert.That(compiler.Metrics.IRExtraFlags, Is.Zero);
        });
    }

    [Test]
    public static void ReverseOperationFlagsRequireSupportedOperandOrder()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var block = AddBlock(compiler);
            var addition = new GenTreeOp(GT_ADD, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1),
                compiler.gtNewIconNode(TYP_INT, 2));
            addition.Flags |= GTF_REVERSE_OPS;

            if (Configuration.TARGET_WASM)
            {
                Assert.That(() => CheckFlagsAndTypes(compiler, addition, block), Throws.Exception);
            }
            else
            {
                Assert.That(() => CheckFlagsAndTypes(compiler, addition, block), Throws.Nothing);
            }

            var comma = new GenTreeOp(GT_COMMA, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1),
                compiler.gtNewIconNode(TYP_INT, 2));
            comma.Flags |= GTF_REVERSE_OPS;
            Assert.That(() => CheckFlagsAndTypes(compiler, comma, block), Throws.Exception);

            var negation = new GenTreeUnOp(GT_NEG, TYP_INT, compiler.gtNewIconNode(TYP_INT, 1));
            negation.Flags |= GTF_REVERSE_OPS;
            Assert.That(() => CheckFlagsAndTypes(compiler, negation, block), Throws.Exception);
        });
    }

    [Test]
    public static void PhysicalRegisterFactoryPreservesTheSourceRegister()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var node = compiler.gtNewPhysRegNode(REG_SPBASE, TYP_I_IMPL);
            Assert.That(node.Oper, Is.EqualTo(GT_PHYSREG));
            Assert.That(node.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(node.SrcReg, Is.EqualTo(REG_SPBASE));

            if (Configuration.TARGET_ARM64)
            {
                Assert.That(genIsValidIntReg(REG_FFR), Is.False);
                var ffrNode = compiler.gtNewPhysRegNode(REG_FFR, TYP_MASK);
                Assert.That(ffrNode.SrcReg, Is.EqualTo(REG_FFR));
            }
            else
            {
                Assert.That(() => compiler.gtNewPhysRegNode(REG_FFR, TYP_MASK), Throws.Exception);
            }

            Assert.That(() => compiler.gtNewPhysRegNode(REG_NA, TYP_I_IMPL), Throws.Exception);
        });
    }

    [Test]
    public static void FieldListConstructorCreatesContainedStructNodes()
    {
        SsaLivenessTests.WithCompiler(0, _ => {
            var first = new GenTreeFieldList();
            var second = new GenTreeFieldList();

            Assert.That(first.Oper, Is.EqualTo(GT_FIELD_LIST));
            Assert.That(first.Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(first.IsContained, Is.True);
            Assert.That(first, Is.Not.SameAs(second));
        });
    }

    [Test]
    public static void ExtraFlagIsCountedOnlyInRelaxedMode()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var tree = compiler.gtNewIconNode(TYP_INT, 7);
            tree.Flags |= GTF_ASG | GTF_CALL;
            compiler.activePhaseChecks = PhaseChecks.CHECK_IR | PhaseChecks.CHECK_IR_RELAXED;

            Assert.That(() => CheckFlags(compiler, tree, tree.Flags & GTF_ALL_EFFECT, GTF_EMPTY),
                Throws.Nothing);
            Assert.That(compiler.Metrics.IRExtraFlags, Is.EqualTo(2));

            compiler.activePhaseChecks = PhaseChecks.CHECK_IR;
            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => CheckFlags(compiler, tree, tree.Flags & GTF_ALL_EFFECT, GTF_EMPTY),
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
            Assert.That(() => CheckStatements(compiler, block), Throws.Nothing);

            stmt.PrevStmt = null;
            Assert.That(() => CheckStatements(compiler, block), Throws.Exception);
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

            Assert.That(() => CheckStatements(compiler, block), Throws.Exception);
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
            TestConfiguration.EnableVerbose(compiler);

            var output = CodeGenLifeTransitionTests.Capture(() => CheckLinks(compiler));
            Assert.That(output, Does.Contain($"SSA checks completed successfully{Environment.NewLine}"));

            tree.Flags |= GTF_CALL;
            Assert.That(() => CheckLinks(compiler), Throws.Exception);
        });
    }

    [Test]
    public static void LargeGraphGuardPrecedesBlockAndSsaChecks()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            compiler.fgBBcount = 10001;
            TestConfiguration.SetExpensiveDebugCheckLevel(compiler, 0);

            Assert.That(() => CheckLinks(compiler), Throws.Nothing);
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

            Assert.That(() => CheckLinks(compiler), Throws.Exception);

            node.IsUnusedValue = true;
            Assert.That(() => CheckLinks(compiler), Throws.Nothing);
        });
    }

    private static BasicBlock AddBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        return block;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgDebugCheckType")]
    private static extern void CheckType(Compiler compiler, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgDebugCheckFlagsAndTypes")]
    private static extern void CheckFlagsAndTypes(Compiler compiler, GenTree tree, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgDebugCheckFlagsHelper")]
    private static extern void CheckFlags(Compiler compiler, GenTree tree, GenTreeFlags actualFlags,
        GenTreeFlags expectedFlags);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgDebugCheckStmtsList")]
    private static extern void CheckStatements(Compiler compiler, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgDebugCheckLinks")]
    private static extern void CheckLinks(Compiler compiler);

}
