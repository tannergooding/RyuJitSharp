// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumMemoryTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4)]
    [TestCase(65)]
    public static void MemoryPhiCopiesOrderedInputsAndAllocatesDistinctDefinitions(int count)
    {
        WithStore((compiler, store) => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var args = new int[count];
            for (var i = 0; i < count; i++)
            {
                args[i] = ((count - i) % 3) + 1;
            }

            var expected = (int[])args.Clone();
            var numbers = new HashSet<int>();
            for (var i = 0; i < 70; i++)
            {
                Assert.That(numbers.Add(store.VNForMemoryPhiDef(block, args)), Is.True);
            }
            Array.Fill(args, 1234);

            foreach (var number in numbers)
            {
                VNMemoryPhiDef definition = default;
                Assert.That(store.GetMemoryPhiDef(number, ref definition), Is.True);
                Assert.That(store.TypeOfVN(number), Is.EqualTo(TYP_HEAP));
                Assert.That(definition.Block, Is.SameAs(block));
                Assert.That(definition.SsaArgs.ToArray(), Is.EqualTo(expected));
                Assert.That(store.IsPhiDef(number), Is.False);
                VNPhiDef localPhi = default;
                Assert.That(store.GetPhiDef(number, ref localPhi), Is.False);
                VNFuncApp app = default;
                Assert.That(store.GetVNFunc(number, ref app), Is.False);
            }
        });
    }

    [Test]
    public static void FailedQueriesPreserveTheDestination()
    {
        WithStore((compiler, store) => {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            int[] args = [4, 2];
            var definition = new VNMemoryPhiDef(block, args);
            int[] numbers = [
                ValueNumStore.NoVN,
                store.VNForIntCon(1),
                store.VNForPhiDef(TYP_INT, 0, 2, [1]),
                store.VNForExpr(null, TYP_HEAP),
            ];
            foreach (var number in numbers)
            {
                Assert.That(store.GetMemoryPhiDef(number, ref definition), Is.False);
                Assert.That(definition.Block, Is.SameAs(block));
                Assert.That(definition.SsaArgs.Span.SequenceEqual([4, 2]), Is.True);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MemoryProvenanceUsesTheOwningNaturalLoop(bool inLoop)
    {
        WithStore((compiler, store) => {
            var blocks = CreateLoop(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
            Assert.That(compiler._loops.NumLoops, Is.EqualTo(1));
            var expected = inLoop ? compiler._loops.GetLoopByIndex(0) : null;
            var block = blocks[inLoop ? 1 : 0];
            var opaque = store.VNForExpr(block, TYP_HEAP);
            var phi = store.VNForMemoryPhiDef(block, [1, 2]);
            var mapStore = store.VNForFunc(TYP_HEAP, VNFunc.VNF_MapStore,
                opaque, store.VNForIntCon(0), store.VNForIntCon(42),
                expected?.Index ?? ValueNumStore.NoLoop);

            Assert.That(store.LoopOfVN(opaque), Is.SameAs(expected));
            Assert.That(store.LoopOfVN(phi), Is.SameAs(expected));
            Assert.That(store.LoopOfVN(mapStore), Is.SameAs(expected));
        });
    }

    [Test]
    public static void UnknownAndNonMemoryValuesNeedNoLoopState()
    {
        WithStore((compiler, store) => {
            Assert.That(store.LoopOfVN(ValueNumStore.NoVN), Is.Null);
            Assert.That(store.LoopOfVN(store.VNForIntCon(1)), Is.Null);
            Assert.That(store.LoopOfVN(store.VNForExpr(null, TYP_HEAP)), Is.Null);
            Assert.That(store.LoopOfVN(store.VNForPhiDef(TYP_INT, 0, 2, [1])), Is.Null);
            Assert.That(TYP_MEM, Is.EqualTo(TYP_UNDEF));
            Assert.That(TYP_HEAP, Is.EqualTo(TYP_UNKNOWN));
        });
    }

    private static BasicBlock[] CreateLoop(Compiler compiler)
    {
        BasicBlock[] blocks = [
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
        blocks[1].SetCond(compiler.fgAddRefPred(blocks[2], blocks[1]), compiler.fgAddRefPred(blocks[3], blocks[1]));
        blocks[2].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[2]));
        return blocks;
    }

    private static void WithStore(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            action(compiler, new ValueNumStore(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
