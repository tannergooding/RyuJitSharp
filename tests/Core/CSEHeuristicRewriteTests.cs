// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSEHeuristicRewriteTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optUnmarkCSE")]
    private static extern bool Unmark(Compiler compiler, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optExtractSideEffectsForCSE")]
    private static extern GenTree? Extract(Compiler compiler, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEtab")]
    private static extern ref CSEdsc?[] Candidates(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSECandidateCount")]
    private static extern ref int CandidateCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEweight")]
    private static extern ref double CurrentWeight(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEunmarks")]
    private static extern ref int Unmarks(Compiler compiler);

    [TestCase(40, 0)]
    [TestCase(75, 25)]
    public static void UnmarkUseDecrementsCountAndSaturatesWeightedCount(double initialWeight, double expectedWeight)
    {
        WithCompiler(compiler =>
        {
            var tree = compiler.gtNewIconNode(TYP_INT, 42);
            var descriptor = MakeDescriptor(compiler, tree);
            descriptor.csdUseCount = 1;
            descriptor.csdUseWtCnt = initialWeight;
            CurrentWeight(compiler) = 50;
            tree._cseNum = 1;

            Assert.That(Unmark(compiler, tree), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(tree._cseNum, Is.Zero);
                Assert.That(descriptor.csdUseCount, Is.Zero);
                Assert.That(descriptor.csdUseWtCnt, Is.EqualTo(expectedWeight));
                Assert.That(Unmarks(compiler), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public static void ExtractPreservesStoresInExecutionOrderAndUnmarksDiscardedUse()
    {
        WithCompiler(compiler =>
        {
            SetLocalTypes(compiler, 2);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var firstValue = compiler.gtNewIconNode(TYP_INT, 1);
            var secondValue = compiler.gtNewIconNode(TYP_INT, 2);
            var use = compiler.gtNewIconNode(TYP_INT, 3);
            var firstStore = compiler.gtNewStoreLclVarNode(0, firstValue);
            var secondStore = compiler.gtNewStoreLclVarNode(1, secondValue);
            firstStore._vnPair.SetBoth(store.VNForIntCon(1));
            secondStore._vnPair.SetBoth(store.VNForIntCon(2));

            var descriptor = MakeDescriptor(compiler, use);
            descriptor.csdUseCount = 1;
            descriptor.csdUseWtCnt = 100;
            CurrentWeight(compiler) = 25;
            use._cseNum = 1;

            var right = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, use, secondStore);
            right.Flags |= GTF_ASG;
            var root = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, firstStore, right);
            root.Flags |= GTF_ASG;

            var extracted = Extract(compiler, root);

            Assert.That(extracted, Is.TypeOf<GenTreeOp>());
            Assert.Multiple(() =>
            {
                Assert.That(extracted!.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(extracted.AsOp().Op1, Is.SameAs(firstStore));
                Assert.That(extracted.AsOp().Op2, Is.SameAs(secondStore));
                Assert.That(extracted._vnPair, Is.EqualTo(secondStore._vnPair));
                Assert.That(use._cseNum, Is.Zero);
                Assert.That(descriptor.csdUseCount, Is.Zero);
                Assert.That(descriptor.csdUseWtCnt, Is.EqualTo(75));
            });
        });
    }

    [Test]
    public static void ExtractRetainsNestedCseUseWithinPreservedDef()
    {
        WithCompiler(compiler =>
        {
            SetLocalTypes(compiler, 1);
            var nestedUse = compiler.gtNewIconNode(TYP_INT, 1);
            var descriptor = MakeDescriptor(compiler, nestedUse);
            descriptor.csdUseCount = 1;
            descriptor.csdUseWtCnt = 100;
            nestedUse._cseNum = 1;
            var cseDef = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, nestedUse,
                compiler.gtNewIconNode(TYP_INT, 2));
            cseDef._cseNum = -1;

            var firstStore = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0));
            var root = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, firstStore, cseDef);
            root.Flags |= GTF_ASG;

            var extracted = Extract(compiler, root);

            Assert.Multiple(() =>
            {
                Assert.That(extracted!.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(extracted.AsOp().Op1, Is.SameAs(firstStore));
                Assert.That(extracted.AsOp().Op2, Is.SameAs(cseDef));
                Assert.That(cseDef._cseNum, Is.EqualTo(-1));
                Assert.That(nestedUse._cseNum, Is.EqualTo(1));
                Assert.That(descriptor.csdUseCount, Is.EqualTo(1));
                Assert.That(Unmarks(compiler), Is.Zero);
            });
        });
    }

    private static CSEdsc MakeDescriptor(Compiler compiler, GenTree tree)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        var descriptor = new CSEdsc(tree, compiler.gtNewStmt(tree), block) { csdIndex = 1 };
        Candidates(compiler) = [descriptor];
        CandidateCount(compiler) = 1;
        return descriptor;
    }

    private static void SetLocalTypes(Compiler compiler, int count)
    {
        compiler.lvaCount = count;
        compiler.lvaTable = new LclVarDsc[count];
        for (var index = 0; index < count; index++)
        {
            compiler.lvaTable[index].Type = TYP_INT;
        }
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
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
