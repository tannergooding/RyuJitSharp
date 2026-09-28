// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using BitOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64OptimizerTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "largeFrame")]
    private static extern ref bool LargeFrame(CSE_Heuristic heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "aggressiveRefCnt")]
    private static extern ref double AggressiveCutoff(CSE_Heuristic heuristic);

    [TestCase(TYP_INT, false, true)]
    [TestCase(TYP_FLOAT, false, true)]
    [TestCase(TYP_INT, true, false)]
    public static void SmallCodeCseUsesArm64StackCosts(var_types type, bool largeFrame, bool expectedPromotion)
    {
        WithCompiler(compiler =>
        {
            compiler.compHndBBtab = [];
            compiler.lvaCount = 1;
            compiler.lvaTable = [new LclVarDsc { Type = type }];
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var heuristic = new CSE_Heuristic(compiler);
            heuristic.Initialize();
            // Compiler.compCodeOpt currently fixes BLENDED_CODE, so exercise the size heuristic directly.
            var optimizationKind = typeof(CSE_Heuristic).BaseType?.GetField("codeOptKind",
                BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException();
            optimizationKind.SetValue(heuristic, Compiler.SMALL_CODE);
            AggressiveCutoff(heuristic) = 100;
            LargeFrame(heuristic) = largeFrame;
            var tree = compiler.gtNewLclvNode(type, 0);
            tree.SetCosts(3, 3);
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var descriptor = new CSEdsc(tree, compiler.gtNewStmt(tree), block)
            {
                csdIndex = 1,
                csdDefCount = 1,
                csdUseCount = 1,
                csdDefWtCnt = 1,
                csdUseWtCnt = 1,
            };
            var candidate = new CSE_Candidate(heuristic, descriptor);
            candidate.InitializeCounts();

            Assert.That(heuristic.PromotionCheck(candidate), Is.EqualTo(expectedPromotion));
        });
    }

    [TestCase(512, false)]
    [TestCase(513, true)]
    public static void CseFrameThresholdUsesArm64Displacement(int localCount, bool expectedLarge)
    {
        WithCompiler(compiler =>
        {
            compiler.lvaOutgoingArgSpaceVar = localCount;
            compiler.lvaCount = localCount + 1;
            compiler.lvaTable = new LclVarDsc[localCount + 1];
            for (var index = 0; index < localCount; index++)
            {
                ref var local = ref compiler.lvaTable[index];
                local.Type = TYP_LONG;
                local.lvDoNotEnregister = true;
                local.setLvRefCnt(1);
            }

            var heuristic = new CSE_Heuristic(compiler);
            heuristic.Initialize();
            Assert.That(LargeFrame(heuristic), Is.EqualTo(expectedLarge));
        });
    }

    [TestCase(VNF_HWI_ArmBase_LeadingZeroCount, TYP_INT, 32)]
    [TestCase(VNF_HWI_ArmBase_Arm64_LeadingZeroCount, TYP_LONG, 64)]
    [TestCase(VNF_HWI_ArmBase_Arm64_LeadingSignCount, TYP_LONG, 64)]
    public static void Arm64CountIntrinsicsHaveOperandWidthBounds(VNFunc function, var_types type, int maximum)
    {
        WithCompiler(compiler =>
        {
            compiler.lvaCount = 1;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
            compiler.optAssertionInit(isLocalProp: false);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var operand = store.VNForExpr(null, type);
            var intrinsicType = store.VNForFuncNoFolding(TYP_REF, VNF_SimdType,
                store.VNForIntCon(0), store.VNForIntCon((int)type));
            var result = store.VNForFuncNoFolding(TYP_INT, function, operand, intrinsicType);
            var assertions = BitOps.MakeEmpty(compiler.apTraits ?? throw new InvalidOperationException());
            var range = RangeCheck.GetRangeFromAssertions(compiler, result, assertions);

            Assert.That(range.IsConstantRange(), Is.True);
            Assert.That(range.LowerLimit.Constant, Is.Zero);
            Assert.That(range.UpperLimit.Constant, Is.EqualTo(maximum));
        });
    }

    [TestCase(0, 0, 0)]
    [TestCase(0, 1, 1)]
    [TestCase(1, 0, 1)]
    [TestCase(0, 2, 2)]
    [TestCase(2, 0, 2)]
    [TestCase(1, 2, 2)]
    [TestCase(2, 1, 2)]
    public static void LatestStatementSelectsLaterInBlock(int firstIndex, int secondIndex, int expectedIndex)
    {
        WithCompiler(compiler =>
        {
            compiler.compHndBBtab = [];
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var block = BasicBlock.New(compiler, BBJ_ALWAYS);
            Statement[] statements =
            [
                compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 0)),
                compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 1)),
                compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 2)),
            ];
            foreach (var statement in statements)
            {
                compiler.fgInsertStmtAtEnd(block, statement);
            }

            Assert.That(compiler.gtLatestStatement(statements[firstIndex], statements[secondIndex]),
                Is.SameAs(statements[expectedIndex]));
        });
    }

    [Test]
    public static void StrengthReductionRespectsAddressScalingAndPostUsePlacement()
    {
        WithCompiler(compiler =>
        {
            compiler.compHndBBtab = [];
            compiler.fgNodeThreading = NodeThreading.AllTrees;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            BasicBlock[] blocks =
            [
                BasicBlock.New(compiler, BBJ_RETURN),
                BasicBlock.New(compiler, BBJ_RETURN),
                BasicBlock.New(compiler, BBJ_RETURN),
                BasicBlock.New(compiler, BBJ_RETURN),
                BasicBlock.New(compiler, BBJ_RETURN),
            ];
            for (var i = 0; i < blocks.Length; i++)
            {
                blocks[i].bbRefs = i == 0 ? 1 : 0;
                if (i > 0)
                {
                    blocks[i - 1].Next = blocks[i];
                }
            }

            compiler.fgFirstBB = blocks[0];
            compiler.fgLastBB = blocks[^1];
            compiler.fgPredsComputed = true;
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[0]));
            blocks[1].SetCond(compiler.fgAddRefPred(blocks[2], blocks[1]),
                compiler.fgAddRefPred(blocks[4], blocks[1]));
            blocks[2].SetCond(compiler.fgAddRefPred(blocks[2], blocks[2]),
                compiler.fgAddRefPred(blocks[3], blocks[2]));
            blocks[3].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[3]));

            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._domTree = FlowGraphDominatorTree.Build(compiler._dfsTree);
            var loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            var loop = loops.GetLoopByHeader(blocks[1]) ?? throw new InvalidOperationException();
            var evolution = new ScalarEvolutionContext(compiler);
            evolution.ResetForLoop(loop);

            var contextType = typeof(Compiler).GetNestedType("StrengthReductionContext", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            var cursorType = typeof(Compiler).GetNestedType("CursorInfo", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            var context = Activator.CreateInstance(contextType, BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic, null, [compiler, evolution, loop, new PerLoopInfo(loops)], null)
                ?? throw new InvalidOperationException();
            var cursors = (IList)(Activator.CreateInstance(typeof(System.Collections.Generic.List<>)
                .MakeGenericType(cursorType)) ?? throw new InvalidOperationException());
            var constructor = cursorType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic |
                BindingFlags.Public)[0];
            var first = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 1));
            var last = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_INT, 2));
            compiler.fgInsertStmtAtEnd(blocks[3], first);
            compiler.fgInsertStmtAtEnd(blocks[3], last);
            _ = cursors.Add(constructor.Invoke([blocks[3], last, last.RootNode, null]));
            _ = cursors.Add(constructor.Invoke([blocks[3], first, first.RootNode, null]));

            var method = contextType.GetMethod("FindUpdateInsertionPoint", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            object?[] arguments = [cursors, null];
            var insertionBlock = method.Invoke(context, arguments);

            Assert.That(insertionBlock, Is.SameAs(blocks[3]));
            Assert.That(arguments[1], Is.SameAs(last));

            cursors.Clear();
            _ = cursors.Add(constructor.Invoke([blocks[3], first, first.RootNode, null]));
            _ = cursors.Add(constructor.Invoke([blocks[3], first, first.RootNode, null]));
            _ = cursors.Add(constructor.Invoke([blocks[3], last, last.RootNode, null]));
            arguments = [cursors, null];
            Assert.That(method.Invoke(context, arguments), Is.SameAs(blocks[3]));
            Assert.That(arguments[1], Is.SameAs(last));

            var zero = evolution.NewConstant(TYP_INT, 0);
            var largeStep = evolution.NewAddRec(zero, evolution.NewConstant(TYP_INT, 8));
            var smallStep = evolution.NewAddRec(zero, evolution.NewConstant(TYP_INT, 4));
            var address = compiler.gtNewBinaryNode(GT_LSH, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 3));
            address.Flags |= GTF_ADDRMODE_NO_CSE;
            cursors.Clear();
            _ = cursors.Add(constructor.Invoke([blocks[3], first, address, largeStep]));
            _ = cursors.Add(constructor.Invoke([blocks[3], last, last.RootNode, smallStep]));
            var check = contextType.GetMethod("CheckAdvancedCursors", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            arguments = [cursors, null];
            Assert.That(check.Invoke(context, arguments), Is.EqualTo(false));
            Assert.That(arguments[1], Is.Null);

            address.Flags &= ~GTF_ADDRMODE_NO_CSE;
            arguments = [cursors, null];
            Assert.That(check.Invoke(context, arguments), Is.EqualTo(true));
            Assert.That(arguments[1], Is.Not.Null);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
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
#endif
