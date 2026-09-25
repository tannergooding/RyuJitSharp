// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class SwitchLoweringTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void RangeDispatchRevisitsDefaultGuardAndLowersInsertedSwitchBlock(bool bitTest)
    {
        int[] cases = bitTest ? [0, 1, 0, 1] : [0, 1, 2, 0];
        var defaultTarget = bitTest ? 2 : 3;
        WithSwitch(cases, defaultTarget, defaultTarget, true, 0.2, (_, lowering, source, node, targets) => {
            lowering.LowerRange(source, new LIR.ReadOnlyRange(node.Op1, node));

            Assert.That(source.LastNode?.Oper, Is.EqualTo(GT_JCC));
            Assert.That(source.TrueTarget, Is.SameAs(targets[defaultTarget]));
            var bottom = source.FalseTarget;
            var first = bottom.FirstNode ?? throw new AssertionException("Switch expansion must insert an index read.");
            var last = bottom.LastNode ?? throw new AssertionException("Switch expansion must insert a branch.");
            lowering.LowerRange(bottom, new LIR.ReadOnlyRange(first, last));

            Assert.That(bottom.LastNode?.Oper, Is.EqualTo(bitTest ? GT_JCC : GT_SWITCH_TABLE));
            Assert.That(node.Prev, Is.Null);
            Assert.That(node.Next, Is.Null);
            Assert.That(bottom.Next, Is.SameAs(targets[defaultTarget]));
        });
    }

    [TestCase(0)]
    [TestCase(4)]
    public static void MinoptsSingleTargetRetainsIndexEvaluation(int caseCount)
    {
        WithSwitch(new int[caseCount], 0, 0, true, 0.2, (compiler, lowering, source, node, targets) => {
            var local = node.Op1;
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            var index = new GenTreeIndir(GT_IND, TYP_INT, address);
            source.InsertBefore(local, address, index);
            source.Remove(local);
            node.Op1 = index;

            var next = LowerSwitch(lowering, node);

            Assert.That(next?.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(next?.AsLclVar().Data, Is.SameAs(index));
            Assert.That(source.LastNode, Is.SameAs(next));
            Assert.That(index.Next, Is.SameAs(next));
            Assert.That(source.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(source.Target, Is.SameAs(targets[0]));
            Assert.That(source.TargetEdge.DupCount, Is.EqualTo(1));
            Assert.That(targets[0].bbRefs, Is.EqualTo(1));
            Assert.That(compiler.lvaTable[next!.AsLclVar().LclNum].Type, Is.EqualTo(TYP_INT));
            Assert.That(source.Next, Is.SameAs(targets[0]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RepeatedNonDefaultTargetNeedsOnlyTheUnsignedDefaultGuard(bool minopts)
    {
        WithSwitch([0, 0, 0], 1, 0, minopts, 0.25, (_, lowering, source, node, targets) => {
            var next = LowerSwitch(lowering, node);
            var bottom = AssertDefaultGuard(source, targets[1], 2, 0.25);

            Assert.That(next, Is.SameAs(source.FirstNode));
            Assert.That(bottom.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(bottom.IsEmpty, Is.True);
            Assert.That(bottom.Target, Is.SameAs(targets[0]));
            Assert.That(bottom.TargetEdge.DupCount, Is.EqualTo(1));
            Assert.That(targets[0].bbRefs, Is.EqualTo(1));
            Assert.That(bottom.bbWeight, Is.EqualTo(75));
            Assert.That(bottom.Next, Is.SameAs(targets[0]));
            Assert.That(node.Next, Is.Null);
            Assert.That(node.Prev, Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void JumpTableRetainsLastCaseAndScalesSharedOrUniqueDefault(bool sharedDefault, bool minopts)
    {
        int[] cases = [0, 1, 2, 0];
        var defaultTarget = sharedDefault ? 0 : 3;
        WithSwitch(cases, defaultTarget, defaultTarget, minopts, 0.2, (compiler, lowering, source, node, targets) => {
            var descriptor = source.SwitchTargets;
            var lastEdge = descriptor.Cases[^2];
            var defaultEdge = descriptor.DefaultCase;

            _ = LowerSwitch(lowering, node);
            var bottom = AssertDefaultGuard(source, targets[defaultTarget], 3, 0.2);

            Assert.That(bottom.Kind, Is.EqualTo(BBJ_SWITCH));
            Assert.That(bottom.SwitchTargets, Is.SameAs(descriptor));
            Assert.That(descriptor.HasDefaultCase, Is.False);
            Assert.That(descriptor.Cases.Length, Is.EqualTo(4));
            Assert.That(descriptor.Cases[^1], Is.SameAs(lastEdge));
            Assert.That(descriptor.Succs.Length, Is.EqualTo(3));
            for (var index = 0; index < cases.Length; index++)
            {
                Assert.That(descriptor.Cases[index].DestinationBlock, Is.SameAs(targets[cases[index]]));
            }
            foreach (var edge in descriptor.Succs)
            {
                Assert.That(edge.SourceBlock, Is.SameAs(bottom));
                Assert.That(edge.Likelihood, Is.EqualTo(edge.DupCount / 4.0).Within(1e-12));
            }
            Assert.That(defaultEdge.DupCount, Is.EqualTo(sharedDefault ? 2 : 0));
            var table = bottom.LastNode;
            Assert.That(table?.Oper, Is.EqualTo(GT_SWITCH_TABLE));
            Assert.That(table?.AsOp().Op2.Oper, Is.EqualTo(GT_JMPTABLE));
            Assert.That(table?.AsOp().Op1.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(table?.AsOp().Op1.Oper, Is.EqualTo(GT_CAST));
            Assert.That(table?.AsOp().Op1.AsCast().IsUnsigned, Is.True);
            Assert.That(bottom.Next, Is.SameAs(targets[defaultTarget]));
        });
    }

    [Test]
    public static void NonLocalIndexIsSpilledOnceBeforeGuardAndBitTest()
    {
        WithSwitch([0, 1, 0, 1], 2, 0, true, 0.2, (compiler, lowering, source, node, targets) => {
            var value = node.Op1;
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            var sum = new GenTreeOp(GT_ADD, TYP_INT, value, one);
            source.InsertBefore(node, one, sum);
            node.Op1 = sum;

            _ = LowerSwitch(lowering, node);
            var bottom = AssertDefaultGuard(source, targets[2], 3, 0.2);

            Assert.That(compiler.lvaCount, Is.EqualTo(2));
            var store = sum.Next;
            Assert.That(store?.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(store?.AsLclVar().Data, Is.SameAs(sum));
            Assert.That(store?.AsLclVar().LclNum, Is.EqualTo(1));
            Assert.That(bottom.FirstNode?.AsLclVarCommon().LclNum, Is.EqualTo(1));
            Assert.That(bottom.LastNode?.Oper, Is.EqualTo(GT_JCC));
            Assert.That(bottom.LastNode?.Prev?.Oper, Is.EqualTo(GT_BT));
            Assert.That(bottom.TrueTarget, Is.SameAs(targets[0]));
            Assert.That(bottom.FalseTarget, Is.SameAs(targets[1]));
            Assert.That(bottom.TrueEdge.Likelihood, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(bottom.FalseEdge.Likelihood, Is.EqualTo(0.5).Within(1e-12));
        });
    }

    [TestCase(1.0)]
    [TestCase(0.9999)]
    public static void LargeNearlyUnreachableTableRedistributesProbabilityPerCase(double defaultLikelihood)
    {
        var cases = Enumerable.Range(0, 65).Select(index => index % 2).ToArray();
        WithSwitch(cases, 2, 2, true, defaultLikelihood, (compiler, lowering, source, node, targets) => {
            _ = LowerSwitch(lowering, node);
            var bottom = AssertDefaultGuard(source, targets[2], 64, defaultLikelihood);

            Assert.That(bottom.LastNode?.Oper, Is.EqualTo(GT_SWITCH_TABLE));
            Assert.That(bottom.SwitchTargets.Cases.Length, Is.EqualTo(65));
            foreach (var edge in bottom.SwitchTargets.Succs)
            {
                Assert.That(edge.Likelihood, Is.EqualTo(edge.DupCount / 65.0).Within(1e-12));
                Assert.That(edge.DestinationBlock.bbWeight, Is.EqualTo(bottom.bbWeight * edge.Likelihood).Within(1e-12));
            }
        });
    }

#if DEBUG
    [TestCase(-1, false, 0.2)]
    [TestCase(1, false, 0.2)]
    [TestCase(0, true, 0.2)]
    [TestCase(1, false, 1.0)]
    [NonParallelizable]
    public static void StressExpansionPreservesCaseOrderFallthroughAndDuplicateEdges(int following, bool sharedDefault,
        double defaultLikelihood)
    {
        var previous = StressNames(ref JitConfig);
        fixed (byte* names = "STRESS_SWITCH_CMP_BR_EXPANSION\0"u8)
        {
            StressNames(ref JitConfig) = names;
            try
            {
                int[] cases = [0, 1, 0, 2];
                var defaultTarget = sharedDefault ? 0 : 3;
                WithSwitch(cases, defaultTarget, following, false, defaultLikelihood, (compiler, lowering, source, node, targets) => {
                    compiler.compAllowStress = true;
                    compiler.info.compMethodName = "Switch";
                    compiler.info.compFullName = "SwitchLoweringTests.Switch";
                    var followingBlock = source.Next;
                    _ = LowerSwitch(lowering, node);
                    Assert.That(compiler.compActiveStressModes[(int)Compiler.STRESS_SWITCH_CMP_BR_EXPANSION], Is.EqualTo(1));
                    var expectedComparisons = Enumerable.Range(0, cases.Length).Where(index => cases[index] != following).ToList();
                    if (following == -1)
                    {
                        expectedComparisons.RemoveAt(expectedComparisons.Count - 1);
                    }
                    var actualComparisons = new List<int>();
                    for (var block = source.Next; (block is not null) && (block != followingBlock); block = block.Next)
                    {
                        Assert.That(block.Kind, Is.AnyOf(BBJ_ALWAYS, BBJ_COND));
                        if (block.Kind is BBJ_COND)
                        {
                            var comparison = block.LastNode!.AsUnOp().Op1.AsOp();
                            Assert.That(comparison.Oper, Is.EqualTo(GT_EQ));
                            actualComparisons.Add((int)comparison.Op2.AsIntCon().IconValue);
                        }
                    }
                    Assert.That(actualComparisons, Is.EqualTo(expectedComparisons));
                    for (var index = -1; index <= cases.Length; index++)
                    {
                        var target = ExecuteBranches(source, index, targets);
                        Assert.That(target, Is.SameAs(targets[(index < 0) || (index >= cases.Length) ? defaultTarget : cases[index]]));
                    }
                    foreach (var target in targets.Where(target => target.bbRefs != 0))
                    {
                        Assert.That(target.bbWeight, Is.EqualTo(target.computeIncomingWeight()).Within(1e-10));
                    }
                });
            }
            finally
            {
                StressNames(ref JitConfig) = previous;
            }
        }
    }

    private static BasicBlock ExecuteBranches(BasicBlock source, int index, BasicBlock[] targets)
    {
        var block = source;
        for (var steps = 0; !targets.Contains(block); steps++)
        {
            Assert.That(steps, Is.LessThan(20));
            if (block.Kind is BBJ_ALWAYS)
            {
                block = block.Target;
                continue;
            }
            var comparison = block.LastNode!.AsUnOp().Op1.AsOp();
            var constant = (int)comparison.Op2.AsIntCon().IconValue;
            var takeTrue = comparison.Oper is GT_GT ? unchecked((uint)index) > (uint)constant : index == constant;
            block = takeTrue ? block.TrueTarget : block.FalseTarget;
        }

        return block;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitStressModeNames")]
    private static extern ref byte* StressNames(ref JitConfigValues config);
#endif

    private static BasicBlock AssertDefaultGuard(BasicBlock source, BasicBlock defaultTarget, int lastCase, double probability)
    {
        Assert.That(source.Kind, Is.EqualTo(BBJ_COND));
        Assert.That(source.TrueTarget, Is.SameAs(defaultTarget));
        Assert.That(source.TrueEdge.Likelihood, Is.EqualTo(probability).Within(1e-12));
        Assert.That(source.FalseEdge.Likelihood, Is.EqualTo(1 - probability).Within(1e-12));
        Assert.That(source.LastNode?.Oper, Is.EqualTo(GT_JTRUE));
        var comparison = source.LastNode!.AsUnOp().Op1.AsOp();
        Assert.That(comparison.Oper, Is.EqualTo(GT_GT));
        Assert.That(comparison.IsUnsigned, Is.True);
        Assert.That(comparison.Op2.AsIntCon().IconValue, Is.EqualTo((nint)lastCase));
        Assert.That(source.FalseTarget, Is.SameAs(source.Next));

        return source.FalseTarget;
    }

    private delegate void SwitchAction(Compiler compiler, Lowering lowering, BasicBlock source, GenTreeUnOp node, BasicBlock[] targets);

    private static void WithSwitch(int[] cases, int defaultTarget, int followingTarget, bool minopts, double defaultLikelihood,
        SwitchAction action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 1;
        compiler.compHndBBtab = [];
        compiler.compRationalIRForm = true;
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.fgPredsComputed = true;
        compiler.fgPgoConsistent = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var source = NewBlock(compiler, BBJ_SWITCH);
            var targetCount = int.Max(cases.Length == 0 ? 0 : cases.Max(), defaultTarget) + 1;
            var targets = Enumerable.Range(0, targetCount).Select(_ => NewBlock(compiler, BBJ_RETURN)).ToArray();
            var following = followingTarget < 0 ? NewBlock(compiler, BBJ_RETURN) : targets[followingTarget];
            source.Next = following;
            var last = following;
            foreach (var target in targets.Where(target => target != following))
            {
                last.Next = target;
                last = target;
            }
            compiler.fgFirstBB = source;
            compiler.fgLastBB = last;
            source.setBBProfileWeight(100);
            source.bbCodeOffs = 0;
            source.bbCodeOffsEnd = 10;
            var table = new FlowEdge[cases.Length + 1];
            var successors = new List<FlowEdge>();
            for (var index = 0; index < table.Length; index++)
            {
                var edge = compiler.fgAddRefPred(targets[index == cases.Length ? defaultTarget : cases[index]], source);
                table[index] = edge;
                if (!successors.Contains(edge))
                {
                    successors.Add(edge);
                }
            }
            foreach (var edge in successors)
            {
                var targetIndex = Array.IndexOf(targets, edge.DestinationBlock);
                edge.Likelihood = cases.Length == 0 ? 1 :
                    (cases.Count(target => target == targetIndex) * ((1 - defaultLikelihood) / cases.Length)) +
                    (targetIndex == defaultTarget ? defaultLikelihood : 0);
                edge.DestinationBlock.setBBProfileWeight(edge.Likelihood * source.bbWeight);
            }
            var descriptor = new BBswtDesc([.. successors], new int[table.Length], hasDefault: true);
            table.CopyTo(descriptor.Cases);
            source.SwitchTargets = descriptor;
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var node = new GenTreeUnOp(GT_SWITCH, TYP_VOID, value);
            source.InsertAtEnd(value);
            source.InsertAtEnd(node);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            CurrentBlock(lowering) = source;
            action(compiler, lowering, source, node, targets);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;

        return block;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? CurrentBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerSwitch")]
    private static extern GenTree? LowerSwitch(Lowering lowering, GenTree node);
}
