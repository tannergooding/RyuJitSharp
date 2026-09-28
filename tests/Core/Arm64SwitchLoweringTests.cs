// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
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

[NonParallelizable]
internal static unsafe class Arm64SwitchLoweringTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void SwitchUsesArm64BitTestOrJumpTable(bool bitTest)
    {
        int[] cases = bitTest ? [0, 1, 0, 1] : [0, 1, 2, 0];
        WithSwitch(cases, bitTest ? 2 : 3, false, (compiler, lowering, source, node, targets) => {
            _ = LowerSwitch(lowering, node);

            Assert.That(source.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(source.LastNode?.Oper, Is.EqualTo(GT_JTRUE));
            Assert.That(source.TrueTarget, Is.SameAs(targets[bitTest ? 2 : 3]));
            var bottom = source.FalseTarget;
            Assert.That(bottom.LastNode?.Oper, Is.EqualTo(bitTest ? GT_JTRUE : GT_SWITCH_TABLE));
            if (bitTest)
            {
                var comparison = bottom.LastNode!.AsUnOp().Op1.AsOp();
                Assert.That(comparison.Oper, Is.EqualTo(GT_EQ));
                Assert.That(comparison.Op1.Oper, Is.EqualTo(GT_AND));
                Assert.That(comparison.Op1.AsOp().Op1.Oper, Is.EqualTo(GT_RSZ));
                Assert.That(comparison.Op1.AsOp().Op1.AsOp().Op1.AsIntCon().IconValue, Is.EqualTo((nint)5));
                Assert.That(bottom.TrueTarget, Is.SameAs(targets[0]));
                Assert.That(bottom.FalseTarget, Is.SameAs(targets[1]));
            }
            else
            {
                Assert.That(bottom.LastNode!.AsOp().Op1.Type, Is.EqualTo(TYP_I_IMPL));
                Assert.That(bottom.SwitchTargets.HasDefaultCase, Is.False);
            }

            lowering.LowerRange(bottom, new LIR.ReadOnlyRange(bottom.FirstNode!, bottom.LastNode!));
            Assert.That(bottom.LastNode?.Oper, Is.EqualTo(bitTest ? GT_JTEST : GT_SWITCH_TABLE));
            Assert.That(node.Next, Is.Null);
            Assert.That(node.Prev, Is.Null);
        });
    }

    [Test]
    public static void SixtyFourCaseBitTestRetainsArm64WideTable()
    {
        var cases = Enumerable.Range(0, 64).Select(index => index % 2).ToArray();
        WithSwitch(cases, 2, false, (compiler, lowering, source, node, targets) => {
            _ = LowerSwitch(lowering, node);
            var bottom = source.FalseTarget;
            var comparison = bottom.LastNode!.AsUnOp().Op1.AsOp();
            var shift = comparison.Op1.AsOp().Op1.AsOp();
            Assert.That(shift.Oper, Is.EqualTo(GT_RSZ));
            Assert.That(shift.Type, Is.EqualTo(TYP_LONG));
            Assert.That(unchecked((ulong)shift.Op1.AsIntCon().IconValue), Is.EqualTo(0x5555555555555555UL));
            Assert.That(bottom.TrueTarget, Is.SameAs(targets[0]));
            Assert.That(bottom.FalseTarget, Is.SameAs(targets[1]));
        });
    }

    [Test]
    public static void SingleTargetPreservesIndexEvaluation()
    {
        WithSwitch([0, 0, 0], 0, true, (_, lowering, source, node, targets) => {
            var result = LowerSwitch(lowering, node);
            Assert.That(result?.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(source.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(source.Target, Is.SameAs(targets[0]));
            Assert.That(source.TargetEdge.DupCount, Is.EqualTo(1));
            Assert.That(result?.AsLclVar().Data, Is.Not.Null);
        });
    }

    private delegate void SwitchAction(Compiler compiler, Lowering lowering, BasicBlock source,
        GenTreeUnOp node, BasicBlock[] targets);

    private static void WithSwitch(int[] cases, int defaultTarget, bool minopts, SwitchAction action)
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
        compiler.info.compFullName = nameof(Arm64SwitchLoweringTests);
#endif
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var source = BasicBlock.New(compiler, BBJ_SWITCH);
            var targets = Enumerable.Range(0, int.Max(cases.Max(), defaultTarget) + 1)
                .Select(_ => BasicBlock.New(compiler, BBJ_RETURN)).ToArray();
            source.Next = targets[defaultTarget];
            var last = source.Next;
            foreach (var target in targets.Where(target => target != last))
            {
                last.Next = target;
                last = target;
            }
            compiler.fgFirstBB = source;
            compiler.fgLastBB = last;
            source.setBBProfileWeight(100);
            source.bbCodeOffsEnd = 10;

            var table = new FlowEdge[cases.Length + 1];
            var successors = new List<FlowEdge>();
            for (var index = 0; index < table.Length; index++)
            {
                var target = targets[index == cases.Length ? defaultTarget : cases[index]];
                var edge = compiler.fgAddRefPred(target, source);
                table[index] = edge;
                if (!successors.Contains(edge))
                {
                    successors.Add(edge);
                }
            }
            foreach (var edge in successors)
            {
                var target = Array.IndexOf(targets, edge.DestinationBlock);
                edge.Likelihood = (cases.Count(value => value == target) * (0.8 / cases.Length)) +
                    (target == defaultTarget ? 0.2 : 0);
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? CurrentBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerSwitch")]
    private static extern GenTree? LowerSwitch(Lowering lowering, GenTree node);
}
#endif
