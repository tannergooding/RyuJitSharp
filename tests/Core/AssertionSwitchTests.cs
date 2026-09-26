// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.var_types;
using optAssertionKind = RyuJitSharp.Compiler.optAssertionKind;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AssertionSwitchTests
{
    [Test]
    public static void PeelOffsetsI32PreservesArgumentOrderWrappingAndStoppedBase()
    {
        WithCompiler((compiler, store) => {
            var baseVN = store.VNForExpr(null, TYP_INT);
            var first = store.VNForFuncNoFolding(TYP_INT, VNF_ADD,
                store.VNForIntCon(int.MaxValue), baseVN);
            var second = store.VNForFuncNoFolding(TYP_INT, VNF_ADD,
                first, store.VNForIntCon(1));
            var vn = second;
            store.PeelOffsetsI32(ref vn, out var offset);
            Assert.That(vn, Is.EqualTo(baseVN));
            Assert.That(offset, Is.EqualTo(int.MinValue));

            var twoVariables = store.VNForFuncNoFolding(TYP_INT, VNF_ADD,
                baseVN, store.VNForExpr(null, TYP_INT));
            vn = twoVariables;
            store.PeelOffsetsI32(ref vn, out offset);
            Assert.That(vn, Is.EqualTo(twoVariables));
            Assert.That(offset, Is.Zero);

            var longOperand = store.VNForFuncNoFolding(TYP_LONG, VNF_ADD,
                store.VNForLongCon(3), store.VNForExpr(null, TYP_LONG));
            vn = longOperand;
            store.PeelOffsetsI32(ref vn, out offset);
            Assert.That(vn, Is.EqualTo(longOperand));
            Assert.That(offset, Is.Zero);

            var handle = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            var handleAddition = store.VNForFuncNoFolding(TYP_I_IMPL, VNF_ADD,
                baseVN, handle);
            vn = handleAddition;
            store.PeelOffsetsI32(ref vn, out offset);
            Assert.That(vn, Is.EqualTo(handleAddition));
            Assert.That(offset, Is.Zero);

            var outer = store.VNForFuncNoFolding(TYP_INT, VNF_ADD,
                twoVariables, store.VNForIntCon(5));
            vn = outer;
            store.PeelOffsetsI32(ref vn, out offset);
            Assert.That(vn, Is.EqualTo(twoVariables));
            Assert.That(offset, Is.EqualTo(5));
        });
    }

    [TestCase(false, OAK_GE_UN)]
    [TestCase(true, OAK_GE)]
    public static void UniqueCasesCreateOrderedEqualitiesAndCorrectDefaultBound(
        bool nonnegative, optAssertionKind defaultKind)
    {
        WithCompiler((compiler, store) => {
            var valueVN = nonnegative
                ? store.VNForFunc(TYP_INT, VNF_ARR_LENGTH, store.VNForExpr(null, TYP_REF))
                : store.VNForExpr(null, TYP_INT);
            var (block, targets) = CreateSwitch(compiler, valueVN, [0, 1, 2], true);
            Assert.That(compiler.optCreateJumpTableImpliedAssertions(block), Is.True);
            Assert.That(compiler.AssertionCount, Is.EqualTo(3));

            for (var index = 0; index < targets.Length; index++)
            {
                var first = targets[index].FirstStmt;
                Assert.That(first, Is.Not.Null);
                Assert.That(first!.RootNode.Oper, Is.EqualTo(GT_NOP));
                Assert.That(first.RootNode.AssertionInfo.AssertionIndex, Is.EqualTo(index + 1));
                Assert.That(first.NextStmt, Is.Null);
                var assertion = compiler.optGetAssertion((ushort)(index + 1));
                Assert.That(assertion.Kind, Is.EqualTo(index == 2 ? defaultKind : OAK_EQUAL));
                Assert.That(assertion.Op1.VN, Is.EqualTo(valueVN));
                Assert.That(assertion.Op2.IntConstant, Is.EqualTo((nint)index));
            }
        });
    }

    [Test]
    public static void SharedTargetsAndOtherPredecessorsNeverReceiveCaseAssertions()
    {
        WithCompiler((compiler, store) => {
            var valueVN = store.VNForExpr(null, TYP_INT);
            var (block, targets) = CreateSwitch(compiler, valueVN, [0, 0, 1, 2, 2], true);
            var otherPredecessor = BasicBlock.New(compiler, BBJ_ALWAYS);
            compiler.fgLastBB!.Next = otherPredecessor;
            compiler.fgLastBB = otherPredecessor;
            otherPredecessor.SetKindAndTargetEdge(BBJ_ALWAYS,
                compiler.fgAddRefPred(targets[1], otherPredecessor));

            Assert.That(compiler.optCreateJumpTableImpliedAssertions(block), Is.False);
            Assert.That(compiler.AssertionCount, Is.Zero);
            Assert.That(targets[0].FirstStmt, Is.Null);
            Assert.That(targets[1].FirstStmt, Is.Null);
            Assert.That(targets[2].FirstStmt, Is.Null);
            Assert.That(block.SwitchTargets.Cases[0].DupCount, Is.EqualTo(2));
            Assert.That(block.SwitchTargets.Cases[3].DupCount, Is.EqualTo(2));
        });
    }

    [Test]
    public static void OffsetCasesPreserveTargetOrderAndSkipDefault()
    {
        WithCompiler((compiler, store) => {
            var baseVN = store.VNForExpr(null, TYP_INT);
            var operandVN = store.VNForFuncNoFolding(TYP_INT, VNF_ADD,
                baseVN, store.VNForIntCon(-2));
            var (block, targets) = CreateSwitch(compiler, operandVN, [0, 1, 2], true);
            Assert.That(compiler.optCreateJumpTableImpliedAssertions(block), Is.True);
            Assert.That(compiler.AssertionCount, Is.EqualTo(2));
            for (var index = 0; index < 2; index++)
            {
                var assertion = compiler.optGetAssertion((ushort)(index + 1));
                Assert.That(assertion.Kind, Is.EqualTo(OAK_EQUAL));
                Assert.That(assertion.Op1.VN, Is.EqualTo(baseVN));
                Assert.That(assertion.Op2.IntConstant, Is.EqualTo((nint)(index + 2)));
                Assert.That(targets[index].FirstStmt!.RootNode.AssertionInfo.AssertionIndex,
                    Is.EqualTo(index + 1));
            }

            Assert.That(targets[2].FirstStmt, Is.Null);
        });
    }

    [Test]
    public static void OverflowingCaseOffsetsAndUnavailableValueNumbersDoNotMutateTargets()
    {
        WithCompiler((compiler, store) => {
            var baseVN = store.VNForExpr(null, TYP_INT);
            var operandVN = store.VNForFuncNoFolding(TYP_INT, VNF_ADD,
                baseVN, store.VNForIntCon(int.MinValue));
            var (block, targets) = CreateSwitch(compiler, operandVN, [0, 1, 2], true);
            Assert.That(compiler.optCreateJumpTableImpliedAssertions(block), Is.False);
            Assert.That(compiler.AssertionCount, Is.Zero);
            Assert.That(targets, Has.All.Property(nameof(BasicBlock.FirstStmt)).Null);

            block.LastStmt!.RootNode.AsUnOp().Op1._vnPair.SetBoth(ValueNumStore.NoVN);
            Assert.That(compiler.optCreateJumpTableImpliedAssertions(block), Is.False);
            Assert.That(targets, Has.All.Property(nameof(BasicBlock.FirstStmt)).Null);

            block.LastStmt.RootNode.AsUnOp().Op1._vnPair.SetBoth(store.VNForExpr(null, TYP_LONG));
            Assert.That(compiler.optCreateJumpTableImpliedAssertions(block), Is.False);
            Assert.That(targets, Has.All.Property(nameof(BasicBlock.FirstStmt)).Null);
        });
    }

    [Test]
    public static void NonDefaultSwitchDoesNotInventADefaultAssertion()
    {
        WithCompiler((compiler, store) => {
            var (block, targets) = CreateSwitch(compiler, store.VNForExpr(null, TYP_INT), [0, 1], false);
            Assert.That(compiler.optCreateJumpTableImpliedAssertions(block), Is.True);
            Assert.That(compiler.AssertionCount, Is.EqualTo(2));
            Assert.That(compiler.optGetAssertion(2).Kind, Is.EqualTo(OAK_EQUAL));
            Assert.That(targets[1].FirstStmt!.RootNode.AssertionInfo.AssertionIndex, Is.EqualTo(2));
        });
    }

    [Test]
    public static void DefaultCaseRequiresPositiveIndexAndNonconstantOperand()
    {
        WithCompiler((compiler, store) => {
            var (onlyDefault, zeroTarget) = CreateSwitch(
                compiler, store.VNForExpr(null, TYP_INT), [0], true);
            Assert.That(compiler.optCreateJumpTableImpliedAssertions(onlyDefault), Is.False);
            Assert.That(zeroTarget[0].FirstStmt, Is.Null);

            var (constantSwitch, targets) = CreateSwitch(
                compiler, store.VNForIntCon(1), [0, 1], true);
            Assert.That(compiler.optCreateJumpTableImpliedAssertions(constantSwitch), Is.True);
            Assert.That(compiler.AssertionCount, Is.EqualTo(1));
            Assert.That(compiler.optGetAssertion(1).Kind, Is.EqualTo(OAK_EQUAL));
            Assert.That(targets[0].FirstStmt!.RootNode.AssertionInfo.AssertionIndex, Is.EqualTo(1));
            Assert.That(targets[1].FirstStmt, Is.Null);
        });
    }

    private static (BasicBlock Block, BasicBlock[] Targets) CreateSwitch(
        Compiler compiler, int valueVN, int[] cases, bool hasDefault)
    {
        var block = BasicBlock.New(compiler, BBJ_SWITCH);
        var targets = new BasicBlock[cases.Length];
        for (var index = 0; index < targets.Length; index++)
        {
            targets[index] = BasicBlock.New(compiler, BBJ_RETURN);
        }

        var previous = block;
        foreach (var target in targets)
        {
            previous.Next = target;
            previous = target;
        }

        compiler.fgFirstBB = block;
        compiler.fgLastBB = previous;
        var edges = new FlowEdge[cases.Length];
        var unique = new List<FlowEdge>();
        for (var index = 0; index < cases.Length; index++)
        {
            var edge = compiler.fgAddRefPred(targets[cases[index]], block);
            edges[index] = edge;
            if (!unique.Contains(edge))
            {
                unique.Add(edge);
            }
        }

        var descriptor = new BBswtDesc([.. unique], new int[cases.Length], hasDefault);
        edges.CopyTo(descriptor.Cases);
        block.SwitchTargets = descriptor;
        var value = compiler.gtNewLclvNode(TYP_INT, 0);
        value._vnPair.SetBoth(valueVN);
        var statement = compiler.gtNewStmt(new GenTreeUnOp(GT_SWITCH, TYP_VOID, value));
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);
        return (block, targets);
    }

    private static void WithCompiler(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 1;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.fgPredsComputed = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            compiler.optAssertionInit(isLocalProp: false);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
