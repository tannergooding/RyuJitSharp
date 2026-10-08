// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using static RyuJitSharp.SpecialCodeKind;

namespace RyuJitSharp.Target.UnitTests;

[NonParallelizable]
internal static unsafe class WasmSccTests
{
    private static readonly int[] s_initialAddCodeOrder = [19, 10, 1, 4, 3, 2];
    private static readonly int[] s_growthAddCodeOrder = [3, 1, 4, 10, 19, 2, 5];
    private static readonly int[] s_rekeyAddCodeOrder = [3, 1, 4, 19, 2, 5, 11];

    [Test]
    public static void PhaseEntrypointsRunOnAcyclicControlFlow()
    {
        WithCompiler(compiler =>
        {
            var entry = NewBlock(compiler, BBJ_ALWAYS);
            var exit = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, entry, exit);
            Connect(compiler, entry, exit);
            compiler.fgGlobalMorphDone = true;
            compiler.fgFuncletsCreated = true;
            compiler.compFuncInfoCount = 1;

            Assert.That(compiler.fgWasmTransformSccs(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgWasmControlFlow(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.fgFirstBB, Is.SameAs(entry));
            Assert.That(compiler.fgLastBB, Is.SameAs(exit));
            Assert.That(entry.Next, Is.SameAs(exit));
            var blockMap = compiler.fgIndexToBlockMap
                ?? throw new AssertionException("Wasm control flow did not publish its block map.");
            Assert.That(blockMap, Has.Length.EqualTo(3));
            Assert.That(blockMap[0], Is.SameAs(entry));
            Assert.That(blockMap[1], Is.SameAs(exit));
            Assert.That(blockMap[2].bbPreorderNum, Is.EqualTo(2));
        });
    }

    [Test]
    public static void TransformPhaseRewritesAnIrreducibleLoop()
    {
        WithCompiler(compiler =>
        {
            var entry = NewBlock(compiler, BBJ_COND);
            var firstPred = NewBlock(compiler, BBJ_ALWAYS);
            var secondPred = NewBlock(compiler, BBJ_ALWAYS);
            var first = NewBlock(compiler, BBJ_ALWAYS);
            var second = NewBlock(compiler, BBJ_ALWAYS);
            Link(compiler, entry, firstPred, secondPred, first, second);

            var firstEntry = compiler.fgAddRefPred(firstPred, entry);
            var secondEntry = compiler.fgAddRefPred(secondPred, entry);
            firstEntry.Likelihood = 0.5;
            secondEntry.Likelihood = 0.5;
            entry.SetCond(firstEntry, secondEntry);
            Connect(compiler, firstPred, first);
            Connect(compiler, secondPred, second);
            Connect(compiler, first, second);
            Connect(compiler, second, first);
            compiler.fgGlobalMorphDone = true;
            compiler.fgFuncletsCreated = true;
            compiler.compFuncInfoCount = 1;

            Assert.That(compiler.fgWasmTransformSccs(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Blocks.Any(block => block.Kind is BBJ_SWITCH), Is.True);
        });
    }

    [Test]
    public static void ControlFlowPhaseBuildsNestedIntervalsForSwitchTargets()
    {
        WithCompiler(compiler =>
        {
            var entry = NewBlock(compiler, BBJ_SWITCH);
            var first = NewBlock(compiler, BBJ_RETURN);
            var second = NewBlock(compiler, BBJ_RETURN);
            var third = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, entry, first, second, third);
            var firstEdge = compiler.fgAddRefPred(first, entry);
            var secondEdge = compiler.fgAddRefPred(second, entry);
            var thirdEdge = compiler.fgAddRefPred(third, entry);
            entry.SwitchTargets = new BBswtDesc([firstEdge, secondEdge, thirdEdge], [0, 1, 2],
                hasDefault: true, dominantCase: 0);
            compiler.fgHasSwitch = true;
            compiler.fgGlobalMorphDone = true;
            compiler.fgFuncletsCreated = true;
            compiler.compFuncInfoCount = 1;

            Assert.That(compiler.fgWasmControlFlow(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            BasicBlock[] expectedBlocks = [entry, third, second, first];
            Assert.That(compiler.Blocks.ToArray(), Is.EqualTo(expectedBlocks));
            var intervals = compiler.fgWasmIntervals
                ?? throw new AssertionException("Wasm control flow did not publish its intervals.");
            uint[] expectedEnds = [3, 2, 1];
            Assert.That(intervals.Select(interval => interval.End()), Is.EqualTo(expectedEnds));
        });
    }

    [Test]
    public static void ControlFlowPhaseReversesConditionalBranchForFallthrough()
    {
        WithCompiler(compiler =>
        {
            var entry = NewBlock(compiler, BBJ_COND);
            var trueBlock = NewBlock(compiler, BBJ_RETURN);
            var falseBlock = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, entry, trueBlock, falseBlock);

            var trueEdge = compiler.fgAddRefPred(trueBlock, entry);
            var falseEdge = compiler.fgAddRefPred(falseBlock, entry);
            trueEdge.Likelihood = 0.5;
            falseEdge.Likelihood = 0.5;
            entry.SetCond(trueEdge, falseEdge);

            var left = compiler.gtNewIconNode(TYP_INT, 7);
            var right = compiler.gtNewIconNode(TYP_INT, 3);
            var comparison = new GenTreeOp(GT_NE, TYP_INT, left, right);
            var jump = new GenTreeUnOp(GT_JTRUE, TYP_VOID, comparison);
            entry.InsertAtEnd(left);
            entry.InsertAtEnd(right);
            entry.InsertAtEnd(comparison);
            entry.InsertAtEnd(jump);
            compiler.fgGlobalMorphDone = true;
            compiler.fgFuncletsCreated = true;
            compiler.compFuncInfoCount = 1;

            Assert.That(compiler.fgWasmControlFlow(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(entry.TrueTarget, Is.SameAs(falseBlock));
            Assert.That(entry.FalseTarget, Is.SameAs(trueBlock));
            Assert.That(entry.LastLIRNode?.AsUnOp().Op1.AsOp().Oper, Is.EqualTo(GT_EQ));
        });
    }

    [Test]
    public static void AddCodeDscMapIterationPreservesNativeBucketsAcrossGrowthAndRekey()
    {
        var map = new AddCodeDscMap();
        Compiler.AddCodeDsc[] descriptors = [
            NewAddCodeDsc(1),
            NewAddCodeDsc(10),
            NewAddCodeDsc(19),
            NewAddCodeDsc(2),
            NewAddCodeDsc(3),
            NewAddCodeDsc(4),
        ];

        foreach (var descriptor in descriptors)
        {
            map.Add(new Compiler.AddCodeDscKey(descriptor), descriptor);
        }

        Assert.That(map.Keys.Select(key => key.Data), Is.EqualTo(s_initialAddCodeOrder));

        var growthDescriptor = NewAddCodeDsc(5);
        map.Add(new Compiler.AddCodeDscKey(growthDescriptor), growthDescriptor);
        Assert.That(map.Keys.Select(key => key.Data), Is.EqualTo(s_growthAddCodeOrder));

        var rekeyedDescriptor = descriptors[1];
        var oldKey = new Compiler.AddCodeDscKey(rekeyedDescriptor);
        Assert.That(map.Remove(oldKey), Is.True);
        rekeyedDescriptor.acdTryIndex = 11;
        map.Add(new Compiler.AddCodeDscKey(rekeyedDescriptor), rekeyedDescriptor);

        Assert.That(map.Keys.Select(key => key.Data), Is.EqualTo(s_rekeyAddCodeOrder));
        Assert.That(map.Values.Select(value => (int)value.acdTryIndex), Is.EqualTo(s_rekeyAddCodeOrder));

        static Compiler.AddCodeDsc NewAddCodeDsc(ushort tryIndex)
        {
            return new Compiler.AddCodeDsc
            {
                acdKind = SCK_RNGCHK_FAIL,
                acdKeyDsg = Compiler.AcdKeyDesignator.KD_TRY,
                acdTryIndex = tryIndex,
            };
        }
    }

    [Test]
    public static void WasmSuccessorsVisitUsedThrowHelpersInNativeMapOrder()
    {
        WithCompiler(compiler =>
        {
            compiler.fgFuncletsCreated = true;
            compiler.compFuncInfoCount = 1;
            var entry = NewBlock(compiler, BBJ_ALWAYS);
            var normal = NewBlock(compiler, BBJ_RETURN);
            BasicBlock[] helperBlocks = [
                NewBlock(compiler, BBJ_RETURN),
                NewBlock(compiler, BBJ_RETURN),
                NewBlock(compiler, BBJ_RETURN),
                NewBlock(compiler, BBJ_RETURN),
                NewBlock(compiler, BBJ_RETURN),
                NewBlock(compiler, BBJ_RETURN),
                NewBlock(compiler, BBJ_RETURN),
            ];
            compiler.fgFirstBB = entry;
            Connect(compiler, entry, normal);

            SpecialCodeKind[] kinds = [
                SCK_RNGCHK_FAIL,
                SCK_DIV_BY_ZERO,
                SCK_ARITH_EXCPN,
                SCK_ARG_EXCPN,
                SCK_ARG_RNG_EXCPN,
                SCK_FAIL_FAST,
                SCK_NULL_CHECK,
            ];
            var map = new AddCodeDscMap();

            for (var index = kinds.Length - 1; index >= 0; index--)
            {
                var descriptor = new Compiler.AddCodeDsc {
                    acdDstBlk = helperBlocks[index],
                    acdKind = kinds[index],
                    acdKeyDsg = Compiler.AcdKeyDesignator.KD_NONE,
                    acdUsed = true,
                };
                map.Add(new Compiler.AddCodeDscKey(descriptor), descriptor);
            }

            var mapField = typeof(Compiler).GetField("fgAddCodeDscMap", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("fgAddCodeDscMap was not found.");
            mapField.SetValue(compiler, map);

            var visited = new List<BasicBlock>();
            var result = FgWasm.VisitWasmSuccs(compiler, entry, successor => {
                visited.Add(successor);
                return BasicBlockVisit.Continue;
            });

            BasicBlock[] expected = [
                helperBlocks[0],
                helperBlocks[1],
                helperBlocks[2],
                helperBlocks[3],
                helperBlocks[4],
                helperBlocks[5],
                helperBlocks[6],
                normal,
            ];
            Assert.That(result, Is.EqualTo(BasicBlockVisit.Continue));
            Assert.That(visited, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void EntryDiscoveryCountsEachHeaderOnceAndIgnoresExceptionalReturns()
    {
        WithCompiler(compiler =>
        {
            var firstPred = NewBlock(compiler, BBJ_ALWAYS);
            var secondPred = NewBlock(compiler, BBJ_ALWAYS);
            var catchRet = NewBlock(compiler, BBJ_EHCATCHRET);
            var filterRet = NewBlock(compiler, BBJ_EHFILTERRET);
            var faultRet = NewBlock(compiler, BBJ_EHFAULTRET);
            var first = NewBlock(compiler, BBJ_ALWAYS);
            var second = NewBlock(compiler, BBJ_ALWAYS);
            Link(compiler, firstPred, secondPred, catchRet, filterRet, faultRet, first, second);
            Connect(compiler, firstPred, first);
            Connect(compiler, secondPred, first);
            Connect(compiler, catchRet, second);
            Connect(compiler, filterRet, second);
            _ = compiler.fgAddRefPred(second, faultRet);
            Connect(compiler, first, second);
            Connect(compiler, second, first);
            first.bbWeight = 13;
            second.bbWeight = 29;

            var fgWasm = WithPostorder(compiler, second, first, faultRet, filterRet, catchRet, secondPred, firstPred);
            var scc = new Scc(fgWasm, first);
            scc.Add(second);
            scc.ComputeEntries();

            Assert.That(scc.NumBlocks(), Is.EqualTo(2u));
            Assert.That(scc.NumEntries(), Is.EqualTo(1u));
            Assert.That(scc.TotalEntryWeight(), Is.EqualTo(13));
            Assert.That(scc.IsIrr(), Is.False);
            Assert.That(scc.NumIrr(), Is.Zero);
            Assert.That(scc.EnclosingTryIndex(), Is.Zero);
            Assert.That(scc.EnclosingHndIndex(), Is.Zero);
            Assert.That(scc.TryHeader(), Is.Null);
            Assert.That(BitVecOps.IsMember(fgWasm.GetTraits(), scc.InternalBlocks(), second.bbPostorderNum), Is.True);
            Assert.That(BitVecOps.IsMember(fgWasm.GetTraits(), scc.InternalBlocks(), first.bbPostorderNum), Is.False);
        });
    }

    [TestCase(30.0, 70.0)]
    [TestCase(0.0, 0.0)]
    [TestCase(5.0, 0.0)]
    public static void AllEntryComponentDispatchPreservesPostorderIndicesAndWeights(double firstWeight, double secondWeight)
    {
        WithCompiler(compiler =>
        {
            var firstPred = NewBlock(compiler, BBJ_ALWAYS);
            var secondPred = NewBlock(compiler, BBJ_ALWAYS);
            var first = NewBlock(compiler, BBJ_ALWAYS);
            var second = NewBlock(compiler, BBJ_ALWAYS);
            Link(compiler, firstPred, secondPred, first, second);
            Connect(compiler, firstPred, first);
            Connect(compiler, secondPred, second);
            Connect(compiler, first, second);
            Connect(compiler, second, first);
            first.bbWeight = firstWeight;
            second.bbWeight = secondWeight;

            // Supplied postorder isolates the helper contract; no Wasm successor traversal is exercised.
            var fgWasm = WithPostorder(compiler, second, first, secondPred, firstPred);
            var sccs = new ArrayStack<Scc>();
            fgWasm.WasmFindSccs(sccs);

            Assert.That(sccs.Height(), Is.EqualTo(1));
            var scc = sccs.Bottom();
            Assert.That(scc.NumEntries(), Is.EqualTo(2u));
            Assert.That(scc.NumIrr(), Is.EqualTo(1u));
            Assert.That(BitVecOps.Count(fgWasm.GetTraits(), scc.InternalBlocks()), Is.EqualTo(nint.Zero));
            Assert.That(fgWasm.WasmTransformSccs(sccs), Is.True);

            var dispatch = compiler.Blocks.Single(block => block.Kind is BBJ_SWITCH);
            var targets = dispatch.SwitchTargets;
            Assert.That(targets.HasDefaultCase, Is.True);
            Assert.That(targets.Cases.Length, Is.EqualTo(2));
            Assert.That(targets.Cases[0].DestinationBlock, Is.SameAs(second));
            Assert.That(targets.Cases[1].DestinationBlock, Is.SameAs(first));
            Assert.That(dispatch.bbWeight, Is.EqualTo(firstWeight + secondWeight));
            Assert.That(dispatch.LastLIRNode?.Oper, Is.EqualTo(GT_SWITCH));

            var firstLikelihood = firstWeight + secondWeight > 0
                ? secondWeight / (firstWeight + secondWeight)
                : 0.5;
            Assert.That(targets.Cases[0].Likelihood, Is.EqualTo(firstLikelihood));
            Assert.That(targets.Cases[1].Likelihood, Is.EqualTo(1.0 - firstLikelihood));
            Assert.That(first.bbPostorderNum, Is.EqualTo(1));
            Assert.That(second.bbPostorderNum, Is.Zero);

            AssertControlStore(firstPred, dispatch, 1);
            AssertControlStore(secondPred, dispatch, 0);
            AssertControlStore(first, dispatch, 0);
            AssertControlStore(second, dispatch, 1);
            Assert.That(firstPred.LastLIRNode?.AsLclVar().LclNum, Is.EqualTo(secondPred.LastLIRNode?.AsLclVar().LclNum));
            BasicBlock[] expectedPredecessors = [dispatch];
            Assert.That(first.PredBlocks.ToArray(), Is.EqualTo(expectedPredecessors));
            Assert.That(second.PredBlocks.ToArray(), Is.EqualTo(expectedPredecessors));
        });
    }

    [Test]
    public static void ReverseWalkMarksSingletonsAndDoesNotWalkCatchReturns()
    {
        WithCompiler(compiler =>
        {
            var catchRet = NewBlock(compiler, BBJ_EHCATCHRET);
            var target = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, catchRet, target);
            Connect(compiler, catchRet, target);
            var fgWasm = WithPostorder(compiler, target, catchRet);
            var subset = BitVecOps.MakeFull(fgWasm.GetTraits());
            var sccs = new ArrayStack<Scc>();
            var map = new Dictionary<BasicBlock, Scc?>();

            fgWasm.AssignBlockToScc(target, target, subset, sccs, map);

            Assert.That(map.ContainsKey(target), Is.True);
            Assert.That(map[target], Is.Null);
            Assert.That(map.ContainsKey(catchRet), Is.False);
            Assert.That(sccs.Empty(), Is.True);
            fgWasm.AssignBlockToScc(target, target, subset, sccs, map);
            Assert.That(map.Count, Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EntryDiscoveryFindsCommonTryRegionInEitherPostorder(bool innerFirst)
    {
        WithCompiler(compiler =>
        {
            var firstPred = NewBlock(compiler, BBJ_ALWAYS);
            var secondPred = NewBlock(compiler, BBJ_ALWAYS);
            var outerHeader = NewBlock(compiler, BBJ_ALWAYS);
            var innerHeader = NewBlock(compiler, BBJ_ALWAYS);
            var outerBody = NewBlock(compiler, BBJ_ALWAYS);
            Link(compiler, firstPred, secondPred, outerHeader, innerHeader, outerBody);
            Connect(compiler, firstPred, innerHeader);
            Connect(compiler, secondPred, outerBody);
            Connect(compiler, outerHeader, innerHeader);
            Connect(compiler, innerHeader, outerBody);
            Connect(compiler, outerBody, innerHeader);
            innerHeader.TryIndex = 0;
            outerHeader.TryIndex = 1;
            outerBody.TryIndex = 1;
            innerHeader.bbWeight = 11;
            outerBody.bbWeight = 17;
            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = innerHeader,
                    ebdTryLast = innerHeader,
                    ebdEnclosingTryIndex = 1,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
                new EHblkDsc
                {
                    ebdTryBeg = outerHeader,
                    ebdTryLast = outerBody,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 2;

            var fgWasm = innerFirst
                ? WithPostorder(compiler, innerHeader, outerBody, outerHeader, secondPred, firstPred)
                : WithPostorder(compiler, outerBody, innerHeader, outerHeader, secondPred, firstPred);
            var scc = new Scc(fgWasm, innerHeader);
            scc.Add(outerBody);
            scc.ComputeEntries();

            Assert.That(scc.NumEntries(), Is.EqualTo(2u));
            Assert.That(scc.EnclosingTryIndex(), Is.EqualTo(2));
            Assert.That(scc.EnclosingHndIndex(), Is.Zero);
            Assert.That(scc.TryHeader(), Is.SameAs(innerHeader));
            Assert.That(scc.TotalEntryWeight(), Is.EqualTo(28));
        });
    }

    private static void AssertControlStore(BasicBlock block, BasicBlock dispatch, int index)
    {
        Assert.That(block.Target, Is.SameAs(dispatch));
        var store = block.LastLIRNode ?? throw new AssertionException("Missing SCC control-variable assignment.");
        Assert.That(store.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
        Assert.That(store.AsLclVar().Data.IsIntegralConst(index), Is.True);
    }

    private static FgWasm WithPostorder(Compiler compiler, params BasicBlock[] postorder)
    {
        for (var i = 0; i < postorder.Length; i++)
        {
            postorder[i].bbPostorderNum = i;
        }

        var fgWasm = new FgWasm(compiler);
        fgWasm.SetDfsAndTraits(new FlowGraphDfsTree(
            compiler, postorder, postorder.Length, hasCycle: true, profileAware: true, forWasm: true));

        return fgWasm;
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.MakeLir(null, null);

        return block;
    }

    private static void Connect(Compiler compiler, BasicBlock source, BasicBlock destination)
    {
        var edge = compiler.fgAddRefPred(destination, source);
        edge.Likelihood = 1.0;
        source.SetKindAndTargetEdge(source.Kind, edge);
    }

    private static void Link(Compiler compiler, params BasicBlock[] blocks)
    {
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];

        for (var i = 0; i < blocks.Length - 1; i++)
        {
            blocks[i].Next = blocks[i + 1];
            blocks[i + 1].Prev = blocks[i];
        }
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
#endif
        var previous = JitTls.Compiler;
#if DEBUG
        var previousConfig = JitConfig;
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

        try
        {
            JitFlags flags = default;
            flags.Set(JitFlags.JIT_FLAG_ALT_JIT);
            compiler.opts.jitFlags = &flags;
            compiler.opts.SetMinOpts(false);
#if DEBUG
            compiler.info.compFullName = nameof(WasmSccTests);
#endif
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.fgPredsComputed = true;
            compiler.fgImportDone = true;
            compiler.compHndBBtab = [];
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
            JitConfig = new JitConfigValues();
            AltJitSkipOnAssert(ref JitConfig) = 1;
#endif
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
            compiler.lvaCount = 1;
            s_assertions.Clear();
            action(compiler);
            Assert.That(s_assertions, Is.Empty);
        }
        finally
        {
            JitTls.Compiler = previous;
#if DEBUG
            JitConfig = previousConfig;
#endif
        }
    }

    private static readonly List<string> s_assertions = [];

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression) ?? "");

        return 0;
    }

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitSkipOnAssert")]
    private static extern ref int AltJitSkipOnAssert(ref JitConfigValues config);
#endif
}
#endif
