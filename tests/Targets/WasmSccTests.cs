// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.Target.UnitTests;

[NonParallelizable]
internal static unsafe class WasmSccTests
{
    [Test]
    public static void PhaseEntrypointsStopAtTheB508SuccessorOrderingBoundary()
    {
        WithCompiler(compiler =>
        {
            JitFlags flags = default;
            compiler.opts.jitFlags = &flags;

            var controlFlowFailure = Assert.Throws<FatalJitException>(() => compiler.fgWasmControlFlow());
            Assert.That(controlFlowFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));

            var transformFailure = Assert.Throws<FatalJitException>(() => compiler.fgWasmTransformSccs());
            Assert.That(transformFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
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
