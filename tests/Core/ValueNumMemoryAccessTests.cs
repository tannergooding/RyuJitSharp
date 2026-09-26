// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.MemoryKind;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumMemoryAccessTests
{
    [Test]
    public static void ByrefLoadIncludesTypePointerAndMemoryState()
    {
        WithStore((compiler, store) =>
        {
            var pointer = store.VNForExpr(null, TYP_BYREF);
            var firstMemory = store.VNForExpr(null, TYP_HEAP);
            compiler.fgCurMemoryVN[(int)ByrefExposed] = firstMemory;

            var first = compiler.fgValueNumberByrefExposedLoad(TYP_INT, pointer);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(first, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_ByrefExposedLoad));
            Assert.That(app.GetArg(0), Is.EqualTo(store.VNForIntCon((int)TYP_INT)));
            Assert.That(app.GetArg(1), Is.EqualTo(pointer));
            Assert.That(app.GetArg(2), Is.EqualTo(firstMemory));
            Assert.That(compiler.fgValueNumberByrefExposedLoad(TYP_INT, pointer), Is.EqualTo(first));

            compiler.fgCurMemoryVN[(int)ByrefExposed] = store.VNForExpr(null, TYP_HEAP);
            Assert.That(compiler.fgValueNumberByrefExposedLoad(TYP_INT, pointer), Is.Not.EqualTo(first));
            Assert.That(compiler.fgValueNumberByrefExposedLoad(TYP_STRUCT, pointer),
                Is.Not.EqualTo(compiler.fgValueNumberByrefExposedLoad(TYP_STRUCT, pointer)));
        });
    }

    [Test]
    public static void SsaVarDefReadsWholeLocalValueWithoutRenumbering()
    {
        WithStore((compiler, store) =>
        {
            compiler.lvaTable = new LclVarDsc[1];
            compiler.lvaCount = 1;
            ref var local = ref compiler.lvaTable[0];
            local.Type = TYP_INT;
            var ssa = local.lvPerSsaData.AllocSsaNum();
            var value = store.VNForIntCon(42);
            local.GetPerSsaData(ssa)._vnPair.SetBoth(value);
            var tree = compiler.gtNewLclvNode(TYP_INT, 0);
            tree.SsaNum = ssa;

            compiler.fgValueNumberSsaVarDef(tree);

            Assert.That(tree._vnPair.Liberal, Is.EqualTo(value));
            Assert.That(tree._vnPair.Conservative, Is.EqualTo(value));
        });
    }

    [Test]
    public static void LocalStoreRecordsSsaValueAndVoidResult()
    {
        WithStore((compiler, store) =>
        {
            compiler.lvaTable = new LclVarDsc[1];
            compiler.lvaCount = 1;
            ref var local = ref compiler.lvaTable[0];
            local.Type = TYP_INT;
            var ssa = local.lvPerSsaData.AllocSsaNum();
            var data = compiler.gtNewIconNode(TYP_INT, 32);
            var value = store.VNForIntCon(32);
            data._vnPair.SetBoth(value);
            var assignment = compiler.gtNewStoreLclVarNode(0, data);
            assignment.SsaNum = ssa;

            compiler.fgValueNumberStore(assignment);

            Assert.That(local.GetPerSsaData(ssa)._vnPair.Liberal, Is.EqualTo(value));
            Assert.That(local.GetPerSsaData(ssa)._vnPair.Conservative, Is.EqualTo(value));
            Assert.That(assignment._vnPair.Liberal, Is.EqualTo(ValueNumStore.VNForVoid()));
        });
    }

    [Test]
    public static void ObjectHandleAndOffsetRequiresEqualValueNumbers()
    {
        WithStore((compiler, store) =>
        {
            var obj = store.VNForHandle(0x1000, GTF_ICON_OBJ_HDL);
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            address._vnPair.SetBoth(obj);

            Assert.That(compiler.GetObjectHandleAndOffset(address, out var offset, out var handle), Is.True);
            Assert.That(offset, Is.EqualTo((nint)0));
            Assert.That((nint)handle, Is.EqualTo((nint)0x1000));

            address._vnPair.Conservative = store.VNForIntCon(3);
            Assert.That(compiler.GetObjectHandleAndOffset(address, out _, out _), Is.False);
        });
    }

    [Test]
    public static void ArrayLoadSelectsElementFromCurrentHeap()
    {
        WithStore((compiler, store) =>
        {
            var heap = store.VNForExpr(null, TYP_HEAP);
            compiler.fgCurMemoryVN[(int)GcHeap] = heap;
            var encodedType = (nint)(((int)TYP_INT << 1) | 1);
            var typeVN = store.VNForHandle(encodedType, GTF_ICON_CLASS_HDL);
            var arrayVN = store.VNForExpr(null, TYP_REF);
            var indexVN = store.VNForIntCon(2);
            var offsetVN = store.VNForLongCon(0);
            var ptr = store.VNForFunc(TYP_BYREF, VNF_PtrToArrElem, typeVN, arrayVN, indexVN, offsetVN);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(ptr, ref app), Is.True);

            var arrayType = store.VNForMapSelect(ValueNumKind.VNK_Liberal, TYP_MEM, heap, typeVN);
            var array = store.VNForMapSelect(ValueNumKind.VNK_Liberal, TYP_MEM, arrayType, arrayVN);
            var element = store.VNForMapSelect(ValueNumKind.VNK_Liberal, TYP_INT, array, indexVN);
            var address = compiler.gtNewIconNode(TYP_BYREF, 0);
            var load = new GenTreeIndir(genTreeOps.GT_IND, TYP_INT, address);

            compiler.fgValueNumberArrayElemLoad(load, app);

            Assert.That(load._vnPair.Liberal, Is.EqualTo(element));
            Assert.That(load._vnPair.Conservative, Is.Not.EqualTo(element));
        });
    }

    [TestCase(0, true)]
    [TestCase(2, false)]
    public static void ArrayStoreUpdatesMapOrInvalidatesHeap(int offset, bool inBounds)
    {
        WithStore((compiler, store) =>
        {
            ArgumentNullException.ThrowIfNull(compiler.compCurBB);
            compiler.compCurBB.bbMemoryDef = (1 << (int)GcHeap) | (1 << (int)ByrefExposed);
            var oldHeap = store.VNForExpr(null, TYP_HEAP);
            compiler.fgCurMemoryVN[(int)GcHeap] = oldHeap;
            var typeVN = store.VNForHandle(((int)TYP_INT << 1) | 1, GTF_ICON_CLASS_HDL);
            var arrayVN = store.VNForExpr(null, TYP_REF);
            var indexVN = store.VNForIntCon(3);
            var ptr = store.VNForFunc(TYP_BYREF, VNF_PtrToArrElem, typeVN, arrayVN, indexVN,
                store.VNForLongCon(offset));
            var address = compiler.gtNewIconNode(TYP_BYREF, 0);
            address._vnPair.SetBoth(ptr);
            var data = compiler.gtNewIconNode(TYP_INT, 17);
            var dataVN = store.VNForIntCon(17);
            data._vnPair.SetBoth(dataVN);
            var assignment = compiler.gtNewStoreIndNode(TYP_INT, address, data);

            compiler.fgValueNumberStore(assignment);

            var updatedHeap = compiler.fgCurMemoryVN[(int)GcHeap];
            Assert.That(updatedHeap, Is.Not.EqualTo(oldHeap));
            Assert.That(assignment._vnPair.Liberal, Is.EqualTo(ValueNumStore.VNForVoid()));
            if (inBounds)
            {
                var atType = store.VNForMapSelect(ValueNumKind.VNK_Liberal, TYP_MEM, updatedHeap, typeVN);
                var atArray = store.VNForMapSelect(ValueNumKind.VNK_Liberal, TYP_MEM, atType, arrayVN);
                Assert.That(store.VNForMapSelect(ValueNumKind.VNK_Liberal, TYP_INT, atArray, indexVN),
                    Is.EqualTo(dataVN));
            }
            else
            {
                Assert.That(store.TypeOfVN(updatedHeap), Is.EqualTo(TYP_HEAP));
            }
        });
    }

    internal static void WithStore(Action<Compiler, ValueNumStore> action)
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
            var blocks = CreateLoop(compiler);
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
            compiler.compCurBB = blocks[0];
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static BasicBlock[] CreateLoop(Compiler compiler)
    {
        BasicBlock[] blocks =
        [
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
            BasicBlock.New(compiler, BBJ_RETURN),
        ];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgPredsComputed = true;
        blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[0]));
        blocks[1].SetCond(compiler.fgAddRefPred(blocks[2], blocks[1]),
            compiler.fgAddRefPred(blocks[3], blocks[1]));
        blocks[2].SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(blocks[1], blocks[2]));
        return blocks;
    }
}
