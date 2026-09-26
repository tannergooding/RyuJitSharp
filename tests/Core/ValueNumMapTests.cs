// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ValueNumKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumMapTests
{
    [Test]
    public static void SmallMemoryDependencySetPromotesWithFifthValueExactlyOnce()
    {
        var type = typeof(ValueNumStore).GetNestedType("SmallValueNumSet", BindingFlags.NonPublic)!;
        var set = Activator.CreateInstance(type, nonPublic: true)!;
        var add = type.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance)!;
        var toArray = type.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance)!;

        int[] values = [13, 11, 16, 12, 15, 14];
        foreach (var value in values)
        {
            Assert.That(add.Invoke(set, [value]), Is.EqualTo(true));
            Assert.That(add.Invoke(set, [value]), Is.EqualTo(false), $"Duplicate value {value}");
        }

        Assert.That((int[])toArray.Invoke(set, null)!, Is.EqualTo(values));
    }

    [Test]
    public static void PreciseStoresSelectEqualAndDisjointIndicesButRetainUnknownAliases()
    {
        WithStore((compiler, store, blocks) =>
        {
            compiler.compCurBB = blocks[0];
            var baseMap = store.VNForExpr(null, TYP_HEAP);
            var first = store.VNForIntCon(11);
            var second = store.VNForIntCon(12);
            var unknown = store.VNForExpr(null, TYP_INT);
            var value = store.VNForIntCon(42);
            var firstStore = store.VNForMapStore(baseMap, first, value);
            var secondStore = store.VNForMapStore(firstStore, second, store.VNForIntCon(13));

            Assert.That(store.VNForMapSelect(VNK_Liberal, TYP_INT, firstStore, first), Is.EqualTo(value));
            Assert.That(store.VNForMapSelect(VNK_Conservative, TYP_INT, secondStore, first),
                Is.EqualTo(value));

            var aliasing = store.VNForMapSelect(VNK_Liberal, TYP_INT, firstStore, unknown);
            AssertMapSelect(store, aliasing, firstStore, unknown);
            Assert.That(store.VNForMapSelect(VNK_Liberal, TYP_INT, firstStore, unknown),
                Is.EqualTo(aliasing));

            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(firstStore, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_MapStore));
            Assert.That(app.GetArg(3), Is.EqualTo(ValueNumStore.NoLoop));
        });
    }

    [Test]
    public static void PhysicalSelectorsPreserveUnsignedWidthsAndStoreAliasRules()
    {
        WithStore((compiler, store, _) =>
        {
            var selector = store.EncodePhysicalSelector(uint.MaxValue, 0x80000001);
            Assert.That(store.DecodePhysicalSelector(selector, out var size), Is.EqualTo(uint.MaxValue));
            Assert.That(size, Is.EqualTo(0x80000001u));

            var baseMap = store.VNForExpr(null, TYP_STRUCT);
            var value = store.VNForExpr(null, TYP_STRUCT);
            var map = store.VNForMapPhysicalStore(baseMap, 4, 8, value);
            Assert.That(store.VNForMapPhysicalSelect(VNK_Liberal, TYP_INT, map, 4, 8),
                Is.EqualTo(value));

            var nested = store.VNForMapPhysicalSelect(VNK_Liberal, TYP_INT, map, 6, 4);
            AssertMapSelect(store, nested, value, store.EncodePhysicalSelector(2, 4));

            var disjoint = store.VNForMapPhysicalSelect(VNK_Liberal, TYP_INT, map, 12, 4);
            AssertMapSelect(store, disjoint, baseMap, store.EncodePhysicalSelector(12, 4));

            var overlap = store.VNForMapPhysicalSelect(VNK_Liberal, TYP_INT, map, 10, 4);
            AssertMapSelect(store, overlap, map, store.EncodePhysicalSelector(10, 4));
            Assert.That(store.VNForMapPhysicalSelect(VNK_Conservative, TYP_INT, map, 10, 4),
                Is.EqualTo(overlap));
        });
    }

    [Test]
    public static void BitCastAndZeroObjectAreTraversedAsPhysicalMaps()
    {
        WithStore((compiler, store, _) =>
        {
            var map = store.VNForExpr(null, TYP_STRUCT);
            var bitCast = store.VNForFunc(TYP_INT, VNFunc.VNF_BitCast, map,
                store.VNForIntCon((int)TYP_INT));
            var selector = store.EncodePhysicalSelector(0, 4);
            Assert.That(store.VNForMapPhysicalSelect(VNK_Liberal, TYP_INT, bitCast, 0, 4),
                Is.EqualTo(store.VNForMapPhysicalSelect(VNK_Liberal, TYP_INT, map, 0, 4)));

            var layout = compiler.typGetBlkLayout(8);
            var zero = store.VNForZeroObj(layout);
            Assert.That(store.VNForMapPhysicalSelect(VNK_Liberal, TYP_INT, zero, 0, 4),
                Is.EqualTo(store.VNZeroForType(TYP_INT)));
            AssertMapSelect(store, store.VNForMapPhysicalSelect(VNK_Liberal, TYP_STRUCT, zero, 0, 4),
                zero, selector);
        });
    }

    [Test]
    public static void LocalPhiReadsRequestedSsaKindAndCachesOnlyAgreement()
    {
        WithStore((compiler, store, blocks) =>
        {
            compiler.lvaTable = new LclVarDsc[1];
            compiler.lvaCount = 1;
            ref var descriptor = ref compiler.lvaTable[0];
            descriptor.Type = TYP_HEAP;
            var first = descriptor.lvPerSsaData.AllocSsaNum();
            var second = descriptor.lvPerSsaData.AllocSsaNum();
            var index = store.VNForIntCon(3);
            var left = store.VNForExpr(null, TYP_HEAP);
            var right = store.VNForExpr(null, TYP_HEAP);
            var leftValue = store.VNForIntCon(11);
            var rightValue = store.VNForIntCon(12);
            compiler.compCurBB = blocks[0];
            descriptor.GetPerSsaData(first)._vnPair =
                new(store.VNForMapStore(left, index, leftValue),
                    store.VNForMapStore(left, index, rightValue));
            descriptor.GetPerSsaData(second)._vnPair =
                new(store.VNForMapStore(right, index, leftValue),
                    store.VNForMapStore(right, index, rightValue));
            var phi = store.VNForPhiDef(TYP_HEAP, 0, 3, [first, second]);
            var conservativePhi = store.VNForPhiDef(TYP_HEAP, 0, 4, [first, second]);

            Assert.That(store.VNForMapSelect(VNK_Liberal, TYP_INT, phi, index), Is.EqualTo(leftValue));
            Assert.That(store.VNForMapSelect(VNK_Conservative, TYP_INT, conservativePhi, index),
                Is.EqualTo(rightValue));

            var mismatch = store.VNForPhiDef(TYP_HEAP, 0, 5, [first, second]);
            descriptor.GetPerSsaData(second)._vnPair.Liberal =
                store.VNForMapStore(right, index, rightValue);
            AssertMapSelect(store, store.VNForMapSelect(VNK_Liberal, TYP_INT, mismatch, index),
                mismatch, index);
        });
    }

    [Test]
    public static void MemoryPhiHandlesCyclesWithoutMemoizingRecursiveAgreement()
    {
        WithStore((compiler, store, blocks) =>
        {
            var first = compiler.AllocMemorySsaNum();
            var second = compiler.AllocMemorySsaNum();
            Assert.That(compiler.GetMemoryPerSsaData(first)._vnPair.Liberal,
                Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(compiler.GetMemoryPerSsaData(second)._vnPair.Conservative,
                Is.EqualTo(ValueNumStore.NoVN));
            var index = store.VNForIntCon(3);
            var baseMap = store.VNForExpr(null, TYP_HEAP);
            compiler.compCurBB = blocks[0];
            var value = store.VNForIntCon(42);
            var stored = store.VNForMapStore(baseMap, index, value);
            var phi = store.VNForMemoryPhiDef(blocks[1], [first, second]);
            compiler.GetMemoryPerSsaData(first)._vnPair.SetBoth(phi);
            compiler.GetMemoryPerSsaData(second)._vnPair.SetBoth(stored);

            Assert.That(store.VNForMapSelect(VNK_Liberal, TYP_INT, phi, index), Is.EqualTo(value));

            var changed = store.VNForIntCon(43);
            compiler.GetMemoryPerSsaData(second)._vnPair.SetBoth(
                store.VNForMapStore(baseMap, index, changed));
            Assert.That(store.VNForMapSelect(VNK_Liberal, TYP_INT, phi, index), Is.EqualTo(changed));
        });
    }

    [Test]
    public static void BudgetExhaustionIsCachedBeforeConsideringPhiRecursion()
    {
        WithStore((compiler, store, blocks) =>
        {
            typeof(ValueNumStore).GetField("_mapSelectBudget",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(store, 0);
            var map = store.VNForExpr(null, TYP_HEAP);
            var index = store.VNForIntCon(1);
            var first = store.VNForMapSelect(VNK_Liberal, TYP_INT, map, index);
            Assert.That(store.VNForMapSelect(VNK_Conservative, TYP_INT, map, index), Is.EqualTo(first));
            Assert.That(store.LoopOfVN(first), Is.Null);
            var opaque = new VNFuncApp();
            Assert.That(store.GetVNFunc(first, ref opaque), Is.True);
            Assert.That(opaque.Func, Is.EqualTo(VNFunc.VNF_MemOpaque));
            Assert.That(opaque.GetArg(0), Is.EqualTo(ValueNumStore.UnknownLoop));
        });
    }

    [Test]
    public static void LoopMemoryDependenceRecordsClosestAncestorAndIgnoresOutsideDefinitions()
    {
        WithStore((compiler, store, blocks) =>
        {
            compiler.compCurBB = blocks[1];
            var tree = compiler.gtNewIconNode(TYP_INT, 0);
            compiler.compCurTree = tree;
            var index = store.VNForIntCon(1);
            var map = store.VNForExpr(blocks[1], TYP_HEAP);
            var value = store.VNForIntCon(42);
            var stored = store.VNForMapStore(map, index, value);
            Assert.That(store.VNForMapSelect(VNK_Liberal, TYP_INT, stored, index), Is.EqualTo(value));
            Assert.That(compiler.NodeToLoopMemoryBlockMap[tree], Is.SameAs(blocks[1]));

            var outside = store.VNForExpr(blocks[0], TYP_HEAP);
            var otherTree = compiler.gtNewIconNode(TYP_INT, 1);
            compiler.compCurTree = otherTree;
            compiler.optRecordLoopMemoryDependence(otherTree, blocks[1], outside);
            Assert.That(compiler.NodeToLoopMemoryBlockMap.ContainsKey(otherTree), Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void FloatingConstantDumpsUseUcrtSpecialValuesAndSignedZero()
    {
        WithStore((compiler, store, _) =>
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            var previous = Globals.s_jitstdout;
            Globals.s_jitstdout = writer;
            try
            {
                (int Bits, string Text)[] floats =
                [
                    (0x7f800000, "inf"), (unchecked((int)0xff800000), "-inf"),
                    (0x00000000, "0.000000"), (unchecked((int)0x80000000), "-0.000000"),
                    (0x7fc00001, "nan"), (unchecked((int)0xffc00001), "-nan"),
                    (unchecked((int)0xffc00000), "-nan(ind)"),
                    (0x7f800001, "nan"), (unchecked((int)0xff800001), "-nan"),
                ];
                foreach (var (bits, text) in floats)
                {
                    compiler.vnPrint(store.VNForFloatCon(BitConverter.Int32BitsToSingle(bits)), 1);
                    writer.Flush();
                    Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                        Does.EndWith($" {{FltCns[{text}]}}"), $"float bits: {bits:x8}");
                    stream.SetLength(0);
                }

                (long Bits, string Text)[] doubles =
                [
                    (0x7ff0000000000000, "inf"), (unchecked((long)0xfff0000000000000), "-inf"),
                    (0x0000000000000000, "0.000000"), (unchecked((long)0x8000000000000000), "-0.000000"),
                    (0x7ff8000000000001, "nan"), (unchecked((long)0xfff8000000000001), "-nan"),
                    (unchecked((long)0xfff8000000000000), "-nan(ind)"),
                    (0x7ff0000000000001, "nan(snan)"),
                    (unchecked((long)0xfff0000000000001), "-nan(snan)"),
                ];
                foreach (var (bits, text) in doubles)
                {
                    compiler.vnPrint(store.VNForDoubleCon(BitConverter.Int64BitsToDouble(bits)), 1);
                    writer.Flush();
                    Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                        Does.EndWith($" {{DblCns[{text}]}}"), $"double bits: {bits:x16}");
                    stream.SetLength(0);
                }
            }
            finally
            {
                Globals.s_jitstdout = previous;
            }
        });
    }

    [Test]
    public static void NativeMapAndPhiDiagnosticShapesArePrintedByVnPrint()
    {
        WithStore((compiler, store, blocks) =>
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
            var previous = Globals.s_jitstdout;
            Globals.s_jitstdout = writer;
            try
            {
                compiler.compCurBB = blocks[0];
                var heap = store.VNForExpr(null, TYP_HEAP);
                var index = store.VNForIntCon(4);
                var value = store.VNForIntCon(5);
                var stored = store.VNForMapStore(heap, index, value);
                var selected = store.VNForMapSelect(VNK_Liberal, TYP_INT, heap, index);
                var physical = store.VNForMapPhysicalStore(store.VNForExpr(null, TYP_STRUCT),
                    2, 4, value);
                var phi = store.VNForMemoryPhiDef(blocks[1], [1, 2]);
                var layout = compiler.typGetBlkLayout(8);
                var zero = store.VNForZeroObj(layout);
                var bitCast = store.VNForFunc(TYP_INT, VNFunc.VNF_BitCast,
                    store.VNForExpr(null, TYP_STRUCT), store.VNForIntCon((int)TYP_INT));
                var cast = store.VNForFunc(TYP_LONG, VNFunc.VNF_Cast,
                    store.VNForExpr(null, TYP_INT),
                    store.VNForCastOper(TYP_LONG, srcIsUnsigned: false));
                var add = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_ADD,
                    store.VNForExpr(null, TYP_INT), index);
                var pointer = store.VNForFuncNoFolding(TYP_BYREF, VNFunc.VNF_PtrToLoc,
                    index, value);
                var exception = store.VNForExpr(null, TYP_REF);
                var exceptions = store.VNExcSetUnion(store.VNExcSetSingleton(exception),
                    store.VNExcSetSingleton(store.VNForExpr(null, TYP_REF)));
                var withException = store.VNWithExc(value, exceptions);
                var floatConstant = store.VNForFloatCon(1.25f);
                var doubleConstant = store.VNForDoubleCon(-2.5);
                var simdConstant = store.VNForSimd8Con(simd8_t.Zero);

                compiler.vnPrint(ValueNumStore.NoVN, 1);
                compiler.vnPrint(stored, 1);
                compiler.vnPrint(selected, 1);
                compiler.vnPrint(physical, 1);
                compiler.vnPrint(phi, 1);
                compiler.vnPrint(zero, 1);
                compiler.vnPrint(bitCast, 1);
                compiler.vnPrint(cast, 1);
                compiler.vnPrint(add, 1);
                compiler.vnPrint(pointer, 1);
                compiler.vnPrint(withException, 1);
                store.vnDumpExc(compiler, exceptions);
                store.vnDumpExc(compiler, ValueNumStore.VNForEmptyExcSet());
                compiler.vnPrint(floatConstant, 1);
                compiler.vnPrint(doubleConstant, 1);
                compiler.vnPrint(simdConstant, 1);
                writer.Flush();

                var actual = Encoding.UTF8.GetString(stream.ToArray());
                Assert.That(actual, Does.Contain("$VN.No"));
                Assert.That(actual, Does.Contain($"${heap:x}[${index:x} := ${value:x}]"));
                Assert.That(actual, Does.Contain($"${heap:x}[${index:x}]"));
                Assert.That(actual, Does.Contain("[2:5] := "));
                Assert.That(actual, Does.Contain($"MemoryPhiDef({FMT_BB(blocks[1].bbNum)}, m:1, m:2)"));
                Assert.That(actual, Does.Contain("ZeroObj("));
                Assert.That(actual, Does.Contain($": {layout.ClassName})"));
                Assert.That(actual, Does.Contain("BitCast<int <- struct>("));
                Assert.That(actual, Does.Contain("long <- int"));
                Assert.That(actual, Does.Contain("{ADD("));
                Assert.That(actual, Does.Contain("PtrToLoc("));
                Assert.That(actual, Does.Contain($"norm=${value:x}"));
                Assert.That(actual, Does.Contain($", exc=${exceptions:x}("));
                Assert.That(actual, Does.Contain("EmptyExcSet"));
                Assert.That(actual, Does.Contain("FltCns[1.250000]"));
                Assert.That(actual, Does.Contain("DblCns[-2.500000]"));
                Assert.That(actual, Does.Contain("Simd8Cns[0x00000000, 0x00000000]"));
            }
            finally
            {
                Globals.s_jitstdout = previous;
            }
        });
    }
#endif

    private static void AssertMapSelect(ValueNumStore store, int result, int map, int index)
    {
        var app = new VNFuncApp();
        Assert.That(store.GetVNFunc(result, ref app), Is.True);
        Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_MapSelect));
        Assert.That(app.GetArg(0), Is.EqualTo(map));
        Assert.That(app.GetArg(1), Is.EqualTo(index));
    }

    private static void WithStore(Action<Compiler, ValueNumStore, BasicBlock[]> action)
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
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store, blocks);
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
