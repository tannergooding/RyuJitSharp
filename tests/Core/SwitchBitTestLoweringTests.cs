// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SwitchBitTestLoweringTests
{
    [TestCase(4)]
    [TestCase(32)]
    [TestCase(33)]
    [TestCase(64)]
    public static void BitTablePreservesEveryCaseAndDeduplicatesCfgEdges(int bitCount)
    {
        var cases = Enumerable.Range(0, bitCount).Select(index => index % 3 == 0 ? 0 : 1).ToArray();
        cases[^1] = 0;
        WithSwitch(cases, 2, 0.25, false, (compiler, lowering, block, value, table, peeled) => {
            var targets = table.Select(edge => edge.DestinationBlock).ToArray();
            var successorCount = block.SwitchTargets.Succs.Length;
            value.Flags |= GTF_GLOB_REF;

            Assert.That(TryLowerSwitchToBitTest(lowering, table, table.Length, successorCount,
                block, value, peeled), Is.True);

            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
            var constant = value.Next ?? throw new InvalidOperationException();
            var bitTest = constant.Next ?? throw new InvalidOperationException();
            var jump = bitTest.Next ?? throw new InvalidOperationException();
            Assert.That(constant.Type, Is.EqualTo(bitCount <= 32 ? TYP_INT : TYP_LONG));
            Assert.That(bitTest.Oper, Is.EqualTo(GT_BT));
            Assert.That(bitTest.Flags & (GTF_SET_FLAGS | GTF_GLOB_REF), Is.EqualTo(GTF_SET_FLAGS | GTF_GLOB_REF));
            Assert.That(bitTest.AsOp().Op1, Is.SameAs(constant));
            Assert.That(bitTest.AsOp().Op2, Is.SameAs(value));
            Assert.That(jump.Oper, Is.EqualTo(GT_JCC));
            Assert.That(jump.AsCC().Condition.Code, Is.EqualTo(GenCondition.C));
            Assert.That(block.LastNode, Is.SameAs(jump));
            var bits = unchecked((ulong)constant.AsIntCon().IconValue);
            for (var index = 0; index < bitCount; index++)
            {
                var destination = (bits & (1UL << index)) != 0 ? block.TrueTarget : block.FalseTarget;
                Assert.That(destination, Is.SameAs(targets[index]), $"case {index}");
            }
            Assert.That(block.TrueEdge.DupCount, Is.EqualTo(1));
            Assert.That(block.FalseEdge.DupCount, Is.EqualTo(1));
            Assert.That(block.TrueTarget.bbRefs, Is.EqualTo(1));
            Assert.That(block.FalseTarget.bbRefs, Is.EqualTo(1));
            Assert.That(block.TrueEdge.Likelihood,
                Is.EqualTo((double)cases.Count(target => target == cases[0]) / bitCount).Within(1e-12));
            Assert.That(block.FalseEdge.Likelihood + block.TrueEdge.Likelihood, Is.EqualTo(1).Within(1e-12));
            Assert.That(compiler.fgPgoConsistent, Is.True);
        });
    }

    [Test]
    public static void HighOnesInvertTheTableAndExchangeConditionTargets()
    {
        var cases = Enumerable.Repeat(0, 64).ToArray();
        cases[1] = 1;
        WithSwitch(cases, 2, 0.25, false, (compiler, lowering, block, value, table, peeled) => {
            var initialOneTarget = table[0].DestinationBlock;
            var initialZeroTarget = table[1].DestinationBlock;
            Assert.That(TryLowerSwitchToBitTest(lowering, table, table.Length, 3, block, value, peeled), Is.True);
            Assert.That(value.Next?.AsIntCon().IconValue, Is.EqualTo((nint)2));
            Assert.That(value.Next?.Type, Is.EqualTo(TYP_LONG));
            Assert.That(block.TrueTarget, Is.SameAs(initialZeroTarget));
            Assert.That(block.FalseTarget, Is.SameAs(initialOneTarget));
            Assert.That(block.TrueEdge.Likelihood, Is.EqualTo(1.0 / 64));
            Assert.That(block.FalseEdge.Likelihood, Is.EqualTo(63.0 / 64));
        });
    }

    [TestCase(65, 2)]
    [TestCase(6, 3)]
    [TestCase(6, 4)]
    public static void UnsupportedTablesLeaveNodesAndEdgesUntouched(int bitCount, int distinctCases)
    {
        var cases = Enumerable.Range(0, bitCount).Select(index => index % distinctCases).ToArray();
        WithSwitch(cases, 0, 1.0 / (bitCount + 1), false, (compiler, lowering, block, value, table, peeled) => {
            var descriptor = block.SwitchTargets;
            var uniqueEdges = table.Distinct().ToArray();
            var counts = uniqueEdges.Select(edge => edge.DupCount).ToArray();
            var likelihoods = uniqueEdges.Select(edge => edge.Likelihood).ToArray();

            Assert.That(TryLowerSwitchToBitTest(lowering, table, table.Length, descriptor.Succs.Length,
                block, value, peeled), Is.False);

            Assert.That(block.Kind, Is.EqualTo(BBJ_SWITCH));
            Assert.That(block.SwitchTargets, Is.SameAs(descriptor));
            Assert.That(block.LastNode, Is.SameAs(value));
            Assert.That(value.Next, Is.Null);
            Assert.That(uniqueEdges.Select(edge => edge.DupCount), Is.EqualTo(counts));
            Assert.That(uniqueEdges.Select(edge => edge.Likelihood), Is.EqualTo(likelihoods));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SharedDefaultAndOtherPredecessorsContributeToRecomputedProfile(bool targetHasSuccessor)
    {
        WithSwitch([0, 1, 0, 1], 0, 0.2, true, (compiler, lowering, block, value, table, peeled) => {
            var target = table[0].DestinationBlock;
            if (targetHasSuccessor)
            {
                target.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(table[1].DestinationBlock, target));
                target.TargetEdge.Likelihood = 1;
                target.bbWeight = 0;
            }

            Assert.That(TryLowerSwitchToBitTest(lowering, table, table.Length, 2, block, value, peeled), Is.True);

            Assert.That(block.TrueTarget, Is.SameAs(target));
            Assert.That(target.bbRefs, Is.EqualTo(2));
            Assert.That(target.bbWeight, Is.EqualTo(60).Within(1e-10));
            // Native recomputes case0 before case1, leaving propagation of a
            // changed target's outgoing flow to a later profile repair.
            Assert.That(block.FalseTarget.bbWeight, Is.EqualTo(40).Within(1e-10));
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!targetHasSuccessor));
        });
    }

    [TestCase(1.0)]
    [TestCase(0.9999)]
    public static void UnreachableOrNearlyUnreachableSwitchUsesEvenConditionalLikelihoods(double defaultLikelihood)
    {
        WithSwitch([0, 0, 1], 2, defaultLikelihood, true, (compiler, lowering, block, value, table, peeled) => {
            Assert.That(TryLowerSwitchToBitTest(lowering, table, table.Length, 3, block, value, peeled), Is.True);
            Assert.That(block.TrueEdge.Likelihood, Is.EqualTo(0.5));
            Assert.That(block.FalseEdge.Likelihood, Is.EqualTo(0.5));
            Assert.That(block.TrueTarget.bbWeight, Is.EqualTo(block.bbWeight * 0.5));
            Assert.That(block.FalseTarget.bbWeight, Is.EqualTo(block.bbWeight * 0.5));
        });
    }

    private delegate void SwitchAction(Compiler compiler, Lowering lowering, BasicBlock block,
        GenTreeLclVar value, FlowEdge[] table, double peeledLikelihood);

    private static void WithSwitch(int[] cases, int defaultTarget, double defaultLikelihood, bool profile,
        SwitchAction action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 1;
        compiler.fgPredsComputed = true;
        compiler.fgPgoConsistent = true;
#if DEBUG
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var original = NewBlock(1);
            original.bbWeight = 100;
            var block = NewBlock(2);
            var targets = Enumerable.Range(0, int.Max(cases.Max(), defaultTarget) + 1)
                .Select(index => NewBlock(index + 3)).ToArray();
            var table = new FlowEdge[cases.Length + 1];
            var unique = new List<FlowEdge>();
            for (var index = 0; index < table.Length; index++)
            {
                var targetIndex = index == cases.Length ? defaultTarget : cases[index];
                var edge = compiler.fgAddRefPred(targets[targetIndex], block);
                table[index] = edge;
                if (!unique.Contains(edge))
                {
                    unique.Add(edge);
                }
            }
            foreach (var edge in unique)
            {
                var targetIndex = Array.IndexOf(targets, edge.DestinationBlock);
                edge.Likelihood = (cases.Count(target => target == targetIndex) *
                    ((1 - defaultLikelihood) / cases.Length)) + (targetIndex == defaultTarget ? defaultLikelihood : 0);
            }
            var descriptor = new BBswtDesc([.. unique], new int[table.Length], hasDefault: true);
            table.CopyTo(descriptor.Cases);
            block.SwitchTargets = descriptor;

            var defaultEdge = table[^1];
            var peeled = defaultEdge.Likelihood / defaultEdge.DupCount;
            compiler.fgRemoveRefPred(defaultEdge);
            defaultEdge.Likelihood -= peeled;
            var defaultBranch = compiler.fgAddRefPred(defaultEdge.DestinationBlock, original);
            defaultBranch.Likelihood = peeled;
            var switchBranch = compiler.fgAddRefPred(block, original);
            switchBranch.Likelihood = 1 - peeled;
            original.SetCond(defaultBranch, switchBranch);
            block.bbWeight = 100 * (1 - peeled);
            if (profile)
            {
                block.setBBProfileWeight(block.bbWeight);
                foreach (var target in targets)
                {
                    target.setBBProfileWeight(999);
                }
            }
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            block.InsertAtEnd(value);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            action(compiler, lowering, block, value, table, peeled);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static BasicBlock NewBlock(int id)
    {
        var block = new BasicBlock(null, null) { bbID = id, bbNum = id, Kind = BBJ_RETURN };
        block.MakeLir(null, null);

        return block;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryLowerSwitchToBitTest")]
    private static extern bool TryLowerSwitchToBitTest(Lowering lowering, ReadOnlySpan<FlowEdge> jumpTable,
        int jumpCount, int targetCount, BasicBlock switchBlock, GenTree switchValue, double defaultLikelihood);
}
