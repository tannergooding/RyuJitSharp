// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class SsaDiagnosticsTests
{
    [Test]
    public static void SummaryFollowsLocalAndDefinitionOrder()
    {
        SsaLivenessTests.WithCompiler(3, compiler => {
            var first = AddBlock(compiler);
            var second = AddBlock(compiler);
            compiler.lvaTable[0].lvInSsa = true;
            compiler.lvaTable[2].lvInSsa = true;

            ref var definitions = ref compiler.lvaTable[2].lvPerSsaData;
            var firstNumber = definitions.AllocSsaNum();
            definitions.GetSsaDef(firstNumber) = new LclSsaVarDsc(first);
            definitions.GetSsaDef(firstNumber).AddPhiUse(second);
            var secondNumber = definitions.AllocSsaNum();
            definitions.GetSsaDef(secondNumber).AddUse(first);

            var text = CodeGenLifeTransitionTests.Capture(compiler.DumpSsaSummary);

            Assert.That(text, Is.EqualTo(
                $"V00: in SSA but no defs{Environment.NewLine}" +
                $"V02.{firstNumber}: defined in BB{first.bbNum:D2} 1 uses (global), has phi uses{Environment.NewLine}" +
                $"V02.{secondNumber}: defined in BB00 1 uses (global){Environment.NewLine}"));
        });
    }

    [Test]
    public static void MissingAnnotationsReturnWithoutGraph()
    {
        SsaLivenessTests.WithCompiler(0, compiler => {
            var text = CodeGenLifeTransitionTests.Capture(compiler.JitTestCheckSSA);
            Assert.That(text, Is.Empty);
            Assert.That(CodeGenLifeTransitionTests.Capture(compiler.JitTestCheckVN), Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ValueNumberConstraintsOptionallyIgnoreExceptions(bool normalize)
    {
        SsaLivenessTests.WithCompiler(2, compiler =>
        {
            var first = AddLocal(compiler, 0, 3);
            var second = AddLocal(compiler, 1, 4);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var value = store.VNForIntCon(17);
            var exceptions = store.VNExcSetSingleton(store.VNForExpr(null, TYP_REF));
            first._vnPair = new ValueNumPair(store.VNWithExc(value, exceptions), store.VNForIntCon(1));
            second._vnPair = new ValueNumPair(value, store.VNForIntCon(2));
            var label = normalize ? TestLabel.TL_VNNorm : TestLabel.TL_VN;
            compiler.NodeTestData.Add(first, new TestLabelAndNum { _tl = label, _num = 10 });
            compiler.NodeTestData.Add(second, new TestLabelAndNum { _tl = label, _num = normalize ? 10 : 11 });
            compiler.verbose = true;

            var text = CodeGenLifeTransitionTests.Capture(compiler.JitTestCheckVN);

            Assert.That(text, Does.Contain("Jit Testing: Value numbering."));
            Assert.That(text.Contains("Already in hash tables.", StringComparison.Ordinal), Is.EqualTo(normalize));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ConflictingValueNumberConstraintsFail(bool sameLabel)
    {
        SsaLivenessTests.WithCompiler(2, compiler =>
        {
            var first = AddLocal(compiler, 0, 3);
            var second = AddLocal(compiler, 1, 4);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            first._vnPair.SetBoth(store.VNForIntCon(17));
            second._vnPair.SetBoth(store.VNForIntCon(sameLabel ? 18 : 17));
            compiler.NodeTestData.Add(first, new TestLabelAndNum { _tl = TestLabel.TL_VN, _num = 10 });
            compiler.NodeTestData.Add(second, new TestLabelAndNum { _tl = TestLabel.TL_VN, _num = sameLabel ? 10 : 11 });

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.JitTestCheckVN(), Throws.Exception));

            Assert.That(text, Does.Contain(sameLabel
                ? "previously bound to a different value number"
                : "associated with a different value number class"));
        });
    }

    [Test]
    public static void UnreachableValueNumberAnnotationFailsBeforeComparison()
    {
        SsaLivenessTests.WithCompiler(1, compiler =>
        {
            var node = new GenTreeLclVar(TYP_INT, 0);
            compiler.NodeTestData.Add(node, new TestLabelAndNum { _tl = TestLabel.TL_VN });
            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.JitTestCheckVN(), Throws.Exception));
            Assert.That(text, Does.Contain("has become unreachable at the time the constraint is tested."));
        });
    }

    [Test]
    public static void NameConstraintsCompareValuesRatherThanNodeIdentity()
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var first = AddLocal(compiler, 0, 3);
            var second = AddLocal(compiler, 0, 3);
            var third = AddLocal(compiler, 1, 4);
            compiler.NodeTestData.Add(first, new TestLabelAndNum { _tl = TestLabel.TL_SsaName, _num = 10 });
            compiler.NodeTestData.Add(second, new TestLabelAndNum { _tl = TestLabel.TL_SsaName, _num = 10 });
            compiler.NodeTestData.Add(third, new TestLabelAndNum { _tl = TestLabel.TL_VN, _num = 10 });
            compiler.verbose = true;

            var text = CodeGenLifeTransitionTests.Capture(compiler.JitTestCheckSSA);

            Assert.That(text, Does.Contain("Jit Testing: SSA names."));
            Assert.That(text, Does.Contain("      added to hash tables."));
            Assert.That(text, Does.Contain("      Already in hash tables."));
            Assert.That(text, Does.Not.Contain($"Node: [{third.TreeId:D6}]"));
        });
    }

    [Test]
    public static void WideLabelsRemainDistinctButPrintTheirTruncatedValues()
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var wideLabel = unchecked((nint)((1L << 32) + 7));
            var first = AddLocal(compiler, 0, 3);
            var second = AddLocal(compiler, 1, 4);
            compiler.NodeTestData.Add(first, new TestLabelAndNum { _tl = TestLabel.TL_SsaName, _num = 7 });
            compiler.NodeTestData.Add(second, new TestLabelAndNum { _tl = TestLabel.TL_SsaName, _num = wideLabel });
            compiler.verbose = true;

            var text = CodeGenLifeTransitionTests.Capture(compiler.JitTestCheckSSA);

            Assert.That(text.Split(" -- SSA name class 7.", StringSplitOptions.None).Length - 1, Is.EqualTo(2));
            Assert.That(text, Does.Not.Contain("Already in hash tables."));
            Assert.That(text, Does.Not.Contain(wideLabel.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void WideLabelsTruncateInFailureDiagnostics(bool sameLabel)
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var wideLabel = unchecked((nint)((1L << 32) + 7));
            var first = AddLocal(compiler, 0, 3);
            var second = AddLocal(compiler, sameLabel ? 1 : 0, sameLabel ? 4 : 3);
            compiler.NodeTestData.Add(first, new TestLabelAndNum { _tl = TestLabel.TL_SsaName, _num = wideLabel });
            compiler.NodeTestData.Add(second, new TestLabelAndNum { _tl = TestLabel.TL_SsaName, _num = sameLabel ? wideLabel : 8 });

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.JitTestCheckSSA(), Throws.Exception));

            Assert.That(text, Does.Contain($"was declared in SSA name class {(sameLabel ? 7 : 8)},"));
            Assert.That(text, Does.Contain(sameLabel
                ? "previously bound to a different SSA name"
                : "associated with a different name class: 7."));
            Assert.That(text, Does.Not.Contain(wideLabel.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        });
    }

    [TestCase(false, "different name class")]
    [TestCase(true, "different SSA name")]
    public static void ConflictingConstraintsFailWithNativeDiagnostic(bool sameLabel, string reason)
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            var first = AddLocal(compiler, 0, 3);
            var second = AddLocal(compiler, sameLabel ? 1 : 0, sameLabel ? 4 : 3);
            compiler.NodeTestData.Add(first, new TestLabelAndNum { _tl = TestLabel.TL_SsaName, _num = 10 });
            compiler.NodeTestData.Add(second, new TestLabelAndNum { _tl = TestLabel.TL_SsaName, _num = sameLabel ? 10 : 11 });

            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.JitTestCheckSSA(), Throws.Exception));

            Assert.That(text, Does.Contain(reason));
        });
    }

    [Test]
    public static void UnreachableAndNonLocalAnnotationsFailBeforeComparingNames()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var unreachable = new GenTreeLclVar(TYP_INT, 0);
            compiler.NodeTestData.Add(unreachable, new TestLabelAndNum { _tl = TestLabel.TL_SsaName });
            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.JitTestCheckSSA(), Throws.Exception));
            Assert.That(text, Does.Contain("has become unreachable at the time the constraint is tested."));
        });

        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var nonLocal = compiler.gtNewIconNode(TYP_INT, 1);
            compiler.NodeTestData.Add(nonLocal, new TestLabelAndNum { _tl = TestLabel.TL_SsaName });
            var text = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(() => compiler.JitTestCheckSSA(), Throws.Exception));
            Assert.That(text, Does.Contain("SSAName constraint put on non-lcl-var expression"));
            Assert.That(text, Does.Contain("(of type int)."));
        });
    }

    private static BasicBlock AddBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        if (compiler.fgLastBB is BasicBlock previous)
        {
            previous.Next = block;
            block.Prev = previous;
        }
        else
        {
            compiler.fgFirstBB = block;
        }
        compiler.fgLastBB = block;
        return block;
    }

    private static GenTreeLclVar AddLocal(Compiler compiler, int local, int ssa)
    {
        var block = compiler.fgFirstBB ?? AddBlock(compiler);
        var node = new GenTreeLclVar(TYP_INT, local) { SsaNum = ssa };
        var statement = new Statement(node, 0);
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);
        return node;
    }
}
#endif
