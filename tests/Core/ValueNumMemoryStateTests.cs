// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.MemoryKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumMemoryStateTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void HeapStorePreservesSharedStateAndRecordsOnlyMappedLiberalDefinition(bool shared, bool mapped)
    {
        WithStore((compiler, store) =>
        {
            compiler.byrefStatesMatchGcHeapStates = shared;
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var ssa = compiler.AllocMemorySsaNum();
            var conservative = store.VNForIntCon(17);
            compiler.GetMemoryPerSsaData(ssa)._vnPair.Conservative = conservative;
            if (mapped)
            {
                compiler.GetMemorySsaMap(GcHeap)[tree] = ssa;
            }

            var heap = store.VNForExpr(compiler.compCurBB, TYP_HEAP);
            compiler.recordGcHeapStore(tree, heap, "test store");

            Assert.That(compiler.fgCurMemoryVN[(int)GcHeap], Is.EqualTo(heap));
            var byref = compiler.fgCurMemoryVN[(int)ByrefExposed];
            Assert.That(store.TypeOfVN(byref), Is.EqualTo(TYP_HEAP));
            Assert.That(byref == heap, Is.EqualTo(shared));
            Assert.That(compiler.GetMemoryPerSsaData(ssa)._vnPair.Liberal,
                Is.EqualTo(mapped ? heap : ValueNumStore.NoVN));
            Assert.That(compiler.GetMemoryPerSsaData(ssa)._vnPair.Conservative, Is.EqualTo(conservative));
            Assert.That(compiler.GetMemorySsaMap(GcHeap) == compiler.GetMemorySsaMap(ByrefExposed),
                Is.EqualTo(shared));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SeparateAddressExposedMutationPreservesHeapAndHeapSsa(bool fresh)
    {
        WithStore((compiler, store) =>
        {
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var heapSsa = compiler.AllocMemorySsaNum();
            var byrefSsa = compiler.AllocMemorySsaNum();
            compiler.GetMemorySsaMap(GcHeap)[tree] = heapSsa;
            compiler.GetMemorySsaMap(ByrefExposed)[tree] = byrefSsa;
            var heap = store.VNForExpr(compiler.compCurBB, TYP_HEAP);
            var byref = store.VNForExpr(compiler.compCurBB, TYP_HEAP);
            compiler.fgSetCurrentMemoryVN(GcHeap, heap);
            compiler.fgSetCurrentMemoryVN(ByrefExposed, byref);
            compiler.GetMemoryPerSsaData(heapSsa)._vnPair.SetBoth(heap);

            if (fresh)
            {
                compiler.fgMutateAddressExposedLocal(tree, "test mutation");
                Assert.That(compiler.fgCurMemoryVN[(int)ByrefExposed], Is.Not.EqualTo(byref));
            }
            else
            {
                compiler.recordAddressExposedLocalStore(tree, byref, "test store");
                Assert.That(compiler.fgCurMemoryVN[(int)ByrefExposed], Is.EqualTo(byref));
            }

            Assert.That(compiler.fgCurMemoryVN[(int)GcHeap], Is.EqualTo(heap));
            Assert.That(compiler.GetMemoryPerSsaData(heapSsa)._vnPair.Liberal, Is.EqualTo(heap));
            Assert.That(compiler.GetMemoryPerSsaData(byrefSsa)._vnPair.Liberal,
                Is.EqualTo(compiler.fgCurMemoryVN[(int)ByrefExposed]));
            Assert.That(compiler.GetMemoryPerSsaData(byrefSsa)._vnPair.Conservative,
                Is.EqualTo(ValueNumStore.NoVN));
        });
    }

    [Test]
    public static void HeapMutationsAllocateFreshSharedValues()
    {
        WithStore((compiler, store) =>
        {
            compiler.byrefStatesMatchGcHeapStates = true;
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            compiler.fgMutateGcHeap(tree, "first mutation");
            var first = compiler.fgCurMemoryVN[(int)GcHeap];
            compiler.fgMutateGcHeap(tree, "second mutation");
            var second = compiler.fgCurMemoryVN[(int)GcHeap];
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(store.TypeOfVN(second), Is.EqualTo(TYP_HEAP));
            Assert.That(compiler.fgCurMemoryVN[(int)ByrefExposed], Is.EqualTo(second));
        });
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
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            compiler.compCurBB = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.compCurBB.bbMemoryDef = (1 << (int)GcHeap) | (1 << (int)ByrefExposed);
            compiler.compCurBB.bbRefs = 1;
            compiler.fgFirstBB = compiler.compCurBB;
            compiler.fgLastBB = compiler.compCurBB;
            compiler.fgPredsComputed = true;
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
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
