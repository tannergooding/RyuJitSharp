// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && (TARGET_XARCH || TARGET_ARM64)
using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using ValueNum = System.Int32;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumHardwareDispatchCompletionTests
{
    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(3, true)]
    public static void PreciseAritiesRetainBothValueKindsAndOperandExceptions(int count, bool split)
    {
        WithCompiler((compiler, store) =>
        {
            var operands = new GenTree[count];
            var expectedExceptions = ValueNumStore.VNPForEmptyExcSet();
            for (var index = 0; index < count; index++)
            {
                operands[index] = Operand(store, TYP_SIMD16, split);
                expectedExceptions = store.VNPExcSetUnion(expectedExceptions,
                    store.VNPExceptionSet(operands[index]._vnPair));
            }
            var id = count switch
            {
                1 => NI_Vector_ToScalar,
                2 => AddId,
                _ => TernaryId,
            };
            var baseType = count == 3 ? TYP_FLOAT : TYP_INT;
            var resultType = count == 1 ? TYP_INT : TYP_SIMD16;
            var tree = new GenTreeHWIntrinsic(resultType, id, baseType, 16, operands);
            var typeVN = store.VNForSimdType(16, baseType);
            var func = Function(id);
            compiler.fgValueNumberHWIntrinsic(tree);
            store.VNPUnpackExc(tree._vnPair, out var normal, out var exceptions);
            var expectedLiberal = Evaluate(false);
            var expectedConservative = split ? Evaluate(true) : expectedLiberal;

            Assert.That(normal.Liberal, Is.EqualTo(expectedLiberal));
            Assert.That(normal.Conservative, Is.EqualTo(expectedConservative));
            Assert.That(exceptions, Is.EqualTo(expectedExceptions));

            ValueNum Evaluate(bool conservative)
            {
                var first = store.VNNormalValue(conservative
                    ? operands[0]._vnPair.Conservative : operands[0]._vnPair.Liberal);
                if (count == 1)
                {
                    return store.EvalHWIntrinsicFunUnary(tree, func, first, typeVN);
                }
                var second = store.VNNormalValue(conservative
                    ? operands[1]._vnPair.Conservative : operands[1]._vnPair.Liberal);
                if (count == 2)
                {
                    return store.EvalHWIntrinsicFunBinary(tree, func, first, second, typeVN);
                }
                var third = store.VNNormalValue(conservative
                    ? operands[2]._vnPair.Conservative : operands[2]._vnPair.Liberal);

                return store.EvalHWIntrinsicFunTernary(tree, func, first, second, third, typeVN);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FourOperandFallbackIsFreshAndUnionsAllOperandExceptionKinds(bool split)
    {
        WithCompiler((compiler, store) =>
        {
#if TARGET_XARCH
            var id = NI_AVX512_TernaryLogic;
            var_types[] types = [TYP_SIMD16, TYP_SIMD16, TYP_SIMD16, TYP_INT];
#else
            var id = NI_AdvSimd_Arm64_InsertSelectedScalar;
            var_types[] types = [TYP_SIMD16, TYP_INT, TYP_SIMD16, TYP_INT];
#endif
            var operands = new GenTree[4];
            var expected = ValueNumStore.VNPForEmptyExcSet();
            for (var index = 0; index < operands.Length; index++)
            {
                operands[index] = Operand(store, types[index], split);
                expected = store.VNPExcSetUnion(expected, store.VNPExceptionSet(operands[index]._vnPair));
            }
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, id, TYP_INT, 16, operands);
            var heap = compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap];
            compiler.fgValueNumberHWIntrinsic(tree);
            store.VNPUnpackExc(tree._vnPair, out var first, out var exceptions);
            compiler.fgValueNumberHWIntrinsic(tree);
            var second = store.VNPNormalPair(tree._vnPair);

            Assert.That(first.BothEqual(), Is.True);
            Assert.That(second.BothEqual(), Is.True);
            Assert.That(second.Liberal, Is.Not.EqualTo(first.Liberal));
            Assert.That(exceptions, Is.EqualTo(expected));
            Assert.That(store.VNPExceptionSet(tree._vnPair), Is.EqualTo(expected));
            Assert.That(compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap], Is.EqualTo(heap));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void OnlyTheSimdDisableBitTwoRequestsOpaqueValueNumbers(int bits)
    {
        WithCompiler((compiler, store) =>
        {
            DisableBits(ref JitConfig) = bits;
            var left = Operand(store, TYP_SIMD16, false);
            var right = Operand(store, TYP_SIMD16, false);
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, AddId, TYP_INT, 16, left, right);
            var expectedExceptions = store.VNPExcSetUnion(
                store.VNPExceptionSet(left._vnPair), store.VNPExceptionSet(right._vnPair));
            compiler.fgValueNumberHWIntrinsic(tree);
            var first = store.VNNormalValue(tree._vnPair.Liberal);
            compiler.fgValueNumberHWIntrinsic(tree);
            var second = store.VNNormalValue(tree._vnPair.Liberal);

            Assert.That(second == first, Is.EqualTo((bits & 2) == 0));
            Assert.That(store.VNPExceptionSet(tree._vnPair), Is.EqualTo(expectedExceptions));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MemoryLoadsUseCurrentByrefHeapAndFreshConservativeReads(bool split)
    {
        WithCompiler((compiler, store) =>
        {
            var address = Operand(store, TYP_BYREF, split);
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, LoadId, TYP_INT, 16, address);
            var heap = compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap];
            var byref = compiler.fgCurMemoryVN[(int)MemoryKind.ByrefExposed];
            compiler.fgValueNumberHWIntrinsic(tree);
            store.VNPUnpackExc(tree._vnPair, out var first, out var exceptions);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(first.Liberal, ref app), Is.True);
            var loadVN = app.GetArg(0);
            Assert.That(store.GetVNFunc(loadVN, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_ByrefExposedLoad));
            Assert.That(app.GetArg(1), Is.EqualTo(store.VNNormalValue(address._vnPair.Liberal)));
            Assert.That(app.GetArg(2), Is.EqualTo(byref));
            var expectedExceptions = store.VNPExcSetUnion(
                store.VNPExceptionSet(address._vnPair), compiler.fgValueNumberIndirNullCheckExceptions(address));
            Assert.That(exceptions, Is.EqualTo(expectedExceptions));

            compiler.fgValueNumberHWIntrinsic(tree);
            var repeated = store.VNPNormalPair(tree._vnPair);
            Assert.That(repeated.Liberal, Is.EqualTo(first.Liberal));
            Assert.That(repeated.Conservative, Is.Not.EqualTo(first.Conservative));
            compiler.fgSetCurrentMemoryVN(MemoryKind.ByrefExposed, store.VNForExpr(compiler.compCurBB, TYP_HEAP));
            compiler.fgValueNumberHWIntrinsic(tree);
            Assert.That(store.VNNormalValue(tree._vnPair.Liberal), Is.Not.EqualTo(first.Liberal));
            Assert.That(compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap], Is.EqualTo(heap));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StoresMutateHeapBeforeNumberingTheirAddress(bool sharedHeapStates)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.byrefStatesMatchGcHeapStates = sharedHeapStates;
            var oldHeap = compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap];
            var oldByref = compiler.fgCurMemoryVN[(int)MemoryKind.ByrefExposed];
            var address = Operand(store, TYP_BYREF, true);
            var value = Operand(store, TYP_SIMD16, true);
            var tree = new GenTreeHWIntrinsic(TYP_VOID, StoreId, TYP_INT, 16, address, value);
            compiler.fgValueNumberHWIntrinsic(tree);
            var heap = compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap];
            var byref = compiler.fgCurMemoryVN[(int)MemoryKind.ByrefExposed];
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(store.VNNormalValue(tree._vnPair.Liberal), ref app), Is.True);
            Assert.That(store.GetVNFunc(app.GetArg(0), ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_ByrefExposedLoad));
            Assert.That(app.GetArg(2), Is.EqualTo(byref));
            Assert.That(heap, Is.Not.EqualTo(oldHeap));
            Assert.That(byref, Is.Not.EqualTo(oldByref));
            Assert.That(heap == byref, Is.EqualTo(sharedHeapStates));
            var expected = store.VNPExcSetUnion(store.VNPExceptionSet(address._vnPair),
                store.VNPExceptionSet(value._vnPair));
            expected = store.VNPExcSetUnion(expected, compiler.fgValueNumberIndirNullCheckExceptions(address));
            Assert.That(store.VNPExceptionSet(tree._vnPair), Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MaskConversionsAreUniqueWithoutDiscardingOperandExceptions(bool constant)
    {
        WithCompiler((compiler, store) =>
        {
            var operand = Operand(store, TYP_MASK, true);
            if (constant)
            {
                var values = new ValueNumPair(store.VNForSimdMaskCon(simdmask_t.Zero),
                    store.VNForSimdMaskCon(simdmask_t.AllBitsSet(16)));
                operand._vnPair = store.VNPWithExc(values, store.VNPExceptionSet(operand._vnPair));
            }
            var expected = store.VNPExceptionSet(operand._vnPair);
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, MaskConversionId, TYP_INT, 16, operand);
            compiler.fgValueNumberHWIntrinsic(tree);
            var first = store.VNPNormalPair(tree._vnPair);
            compiler.fgValueNumberHWIntrinsic(tree);
            var second = store.VNPNormalPair(tree._vnPair);
            var app = new VNFuncApp();

            Assert.That(first.BothEqual(), Is.True);
            Assert.That(second.BothEqual(), Is.True);
            Assert.That(second.Liberal, Is.Not.EqualTo(first.Liberal));
            Assert.That(store.GetVNFunc(first.Liberal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_MemOpaque));
            Assert.That(app.Arity, Is.EqualTo(1));
            Assert.That(app.GetArg(0), Is.EqualTo(ValueNumStore.NoLoop));
            Assert.That(store.GetVNFunc(second.Liberal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNF_MemOpaque));
            Assert.That(app.Arity, Is.EqualTo(1));
            Assert.That(app.GetArg(0), Is.EqualTo(ValueNumStore.NoLoop));
            Assert.That(store.VNPExceptionSet(tree._vnPair), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void NonbarrierSpecialSideEffectsKeepHeapStateAndUseOpaqueDispatch()
    {
        WithCompiler((compiler, store) =>
        {
#if TARGET_XARCH
            var id = NI_X86Base_Pause;
#else
            var id = NI_ArmBase_Yield;
#endif
            var tree = new GenTreeHWIntrinsic(TYP_VOID, id, TYP_INT, 0);
            var heap = compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap];
            var byref = compiler.fgCurMemoryVN[(int)MemoryKind.ByrefExposed];
            compiler.fgValueNumberHWIntrinsic(tree);

            Assert.That(store.VNPNormalPair(tree._vnPair).BothDefined(), Is.True);
            Assert.That(store.VNPExceptionSet(tree._vnPair), Is.EqualTo(ValueNumStore.VNPForEmptyExcSet()));
            Assert.That(compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap], Is.EqualTo(heap));
            Assert.That(compiler.fgCurMemoryVN[(int)MemoryKind.ByrefExposed], Is.EqualTo(byref));
        });
    }

#if TARGET_XARCH
    [TestCase(NI_X86Base_LoadFence, false)]
    [TestCase(NI_X86Base_LoadFence, true)]
    [TestCase(NI_X86Base_MemoryFence, false)]
    [TestCase(NI_X86Base_MemoryFence, true)]
    [TestCase(NI_X86Base_StoreFence, false)]
    [TestCase(NI_X86Base_StoreFence, true)]
    public static void EveryBarrierMutatesHeapBeforeOpaqueDispatch(NamedIntrinsic id, bool sharedHeapStates)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.byrefStatesMatchGcHeapStates = sharedHeapStates;
            var heap = compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap];
            var byref = compiler.fgCurMemoryVN[(int)MemoryKind.ByrefExposed];
            var tree = new GenTreeHWIntrinsic(TYP_VOID, id, TYP_INT, 0);
            compiler.fgValueNumberHWIntrinsic(tree);
            var newHeap = compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap];
            var newByref = compiler.fgCurMemoryVN[(int)MemoryKind.ByrefExposed];

            Assert.That(newHeap, Is.Not.EqualTo(heap));
            Assert.That(newByref, Is.Not.EqualTo(byref));
            Assert.That(newHeap == newByref, Is.EqualTo(sharedHeapStates));
            Assert.That(store.VNPExceptionSet(tree._vnPair), Is.EqualTo(ValueNumStore.VNPForEmptyExcSet()));
        });
    }

    [TestCase(NI_X86Base_MaskMove)]
    [TestCase(NI_AVX_MaskStore)]
    [TestCase(NI_AVX2_MaskStore)]
    [TestCase(NI_AVX_MaskLoad)]
    [TestCase(NI_AVX2_MaskLoad)]
    [TestCase(NI_AVX2_GatherVector128)]
    [TestCase(NI_AVX2_GatherVector256)]
    [TestCase(NI_AVX2_GatherMaskVector128)]
    [TestCase(NI_AVX2_GatherMaskVector256)]
    public static void ImpreciseEffectiveAddressesUseFreshNullExceptionsNotTheBaseAddress(NamedIntrinsic id)
    {
        WithCompiler((compiler, store) =>
        {
            var tree = EffectiveAddressTree(store, id);
            var address = (tree.IsMemoryLoad(out var loadAddress) ? loadAddress : StoreAddress(tree))
                ?? throw new InvalidOperationException("Expected a memory intrinsic address.");
            var expectedOperands = ValueNumStore.VNPForEmptyExcSet();
            foreach (var operand in tree.Operands)
            {
                expectedOperands = store.VNPExcSetUnion(expectedOperands, store.VNPExceptionSet(operand._vnPair));
            }
            compiler.fgValueNumberHWIntrinsic(tree);
            var first = store.VNPExceptionSet(tree._vnPair);
            compiler.fgValueNumberHWIntrinsic(tree);
            var second = store.VNPExceptionSet(tree._vnPair);

            Assert.That(first.Liberal, Is.Not.EqualTo(second.Liberal));
            Assert.That(store.VNExcIsSubset(first.Liberal, expectedOperands.Liberal), Is.True);
            Assert.That(store.VNExcIsSubset(first.Conservative, expectedOperands.Conservative), Is.True);
            Assert.That(store.VNExcIsSubset(first.Liberal,
                compiler.fgValueNumberIndirNullCheckExceptions(address).Liberal), Is.False);
        });
    }

    [Test]
    public static void GatherAuxiliaryIndexTypeRemainsPartOfTheDispatchFunction()
    {
        WithCompiler((compiler, store) =>
        {
            var first = EffectiveAddressTree(store, NI_AVX2_GatherVector128);
            first.AuxiliaryType = TYP_INT;
            var second = new GenTreeHWIntrinsic(TYP_SIMD16, first.HWIntrinsicId, first.SimdBaseType, 16,
                first.Operands.ToArray())
            {
                AuxiliaryType = TYP_LONG,
            };
            compiler.fgValueNumberHWIntrinsic(first);
            compiler.fgValueNumberHWIntrinsic(second);
            var firstApp = new VNFuncApp();
            var secondApp = new VNFuncApp();
            Assert.That(store.GetVNFunc(store.VNNormalValue(first._vnPair.Liberal), ref firstApp), Is.True);
            Assert.That(store.GetVNFunc(store.VNNormalValue(second._vnPair.Liberal), ref secondApp), Is.True);
            Assert.That(firstApp.GetArg(3), Is.EqualTo(store.VNForSimdType(16, TYP_INT, TYP_INT)));
            Assert.That(secondApp.GetArg(3), Is.EqualTo(store.VNForSimdType(16, TYP_INT, TYP_LONG)));
            Assert.That(store.VNNormalValue(first._vnPair.Liberal),
                Is.Not.EqualTo(store.VNNormalValue(second._vnPair.Liberal)));
        });
    }

    private static GenTree StoreAddress(GenTreeHWIntrinsic tree)
    {
        Assert.That(tree.IsMemoryStore(out var address), Is.True);

        return address ?? throw new InvalidOperationException("Expected a store address.");
    }

    private static GenTreeHWIntrinsic EffectiveAddressTree(ValueNumStore store, NamedIntrinsic id)
    {
        var wide = id is NI_AVX2_GatherVector256 or NI_AVX2_GatherMaskVector256;
        var type = wide ? TYP_SIMD32 : TYP_SIMD16;
        var gatherMask = id is NI_AVX2_GatherMaskVector128 or NI_AVX2_GatherMaskVector256;
        var_types[] types = id switch
        {
            NI_X86Base_MaskMove => [type, type, TYP_BYREF],
            NI_AVX_MaskStore or NI_AVX2_MaskStore => [TYP_BYREF, type, type],
            NI_AVX_MaskLoad or NI_AVX2_MaskLoad => [TYP_BYREF, type],
            _ when gatherMask => [type, TYP_BYREF, type, type, TYP_INT],
            _ => [TYP_BYREF, type, TYP_INT],
        };
        var operands = new GenTree[types.Length];
        for (var index = 0; index < operands.Length; index++)
        {
            operands[index] = Operand(store, types[index], true, 4);
        }
        var baseType = id is NI_AVX_MaskLoad or NI_AVX_MaskStore ? TYP_FLOAT
            : id == NI_X86Base_MaskMove ? TYP_BYTE : TYP_INT;
        var resultType = id is NI_X86Base_MaskMove or NI_AVX_MaskStore or NI_AVX2_MaskStore ? TYP_VOID : type;

        return new GenTreeHWIntrinsic(resultType, id, baseType, (byte)type.Size, operands);
    }
#endif

#if TARGET_ARM64
    [TestCase(NI_Sve_CreateFalseMaskByte)]
    [TestCase(NI_Sve_ConversionTrueMask)]
    public static void ValidZeroOperandMasksEncodeTheirResultType(NamedIntrinsic id)
    {
        WithCompiler((compiler, store) =>
        {
            var tree = new GenTreeHWIntrinsic(TYP_MASK, id, TYP_BYTE, 16);
            compiler.fgValueNumberHWIntrinsic(tree);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(tree._vnPair.Liberal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(Function(id)));
            Assert.That(app.GetArg(0), Is.EqualTo(store.VNForSimdType(16, TYP_BYTE)));
            Assert.That(store.VNPNormalPair(tree._vnPair).BothEqual(), Is.True);
            Assert.That(store.VNPExceptionSet(tree._vnPair), Is.EqualTo(ValueNumStore.VNPForEmptyExcSet()));
        });
    }
#endif

#if DEBUG
    [Test]
    public static void TypeDiagnosticUsesTheNativeValueNumberPrefixAndLevel()
    {
        WithCompiler((compiler, store) =>
        {
            var tree = new GenTreeHWIntrinsic(TYP_SIMD16, AddId, TYP_INT, 16,
                Operand(store, TYP_SIMD16, false), Operand(store, TYP_SIMD16, false));
            var type = store.VNForSimdType(16, TYP_INT);
            var expected = Capture(() => compiler.vnPrint(type, 1));
            compiler.verbose = true;
            var actual = Capture(() => compiler.fgValueNumberHWIntrinsic(tree));

            Assert.That(actual, Is.EqualTo($"    simdTypeVN is {expected}\n"));
        });
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
        var previous = s_jitstdout;
        s_jitstdout = writer;
        try
        {
            action();
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        finally
        {
            s_jitstdout = previous;
        }
    }
#endif

    private static NamedIntrinsic AddId =>
#if TARGET_XARCH
        NI_X86Base_Add;
#else
        NI_AdvSimd_Add;
#endif

    private static NamedIntrinsic TernaryId =>
#if TARGET_XARCH
        NI_AVX2_MultiplyAdd;
#else
        NI_AdvSimd_FusedMultiplyAdd;
#endif

    private static NamedIntrinsic LoadId =>
#if TARGET_XARCH
        NI_X86Base_LoadAlignedVector128;
#else
        NI_AdvSimd_LoadAndReplicateToVector128;
#endif

    private static NamedIntrinsic StoreId =>
#if TARGET_XARCH
        NI_X86Base_StoreAligned;
#else
        NI_AdvSimd_Store;
#endif

    private static NamedIntrinsic MaskConversionId =>
#if TARGET_XARCH
        NI_AVX512_ConvertMaskToVector;
#else
        NI_Sve_ConvertMaskToVector;
#endif

    private static VNFunc Function(NamedIntrinsic id) =>
        (VNFunc)((int)VNF_HWI_FIRST + (int)id - (int)NI_HW_INTRINSIC_START - 1);

    private static GenTree Operand(ValueNumStore store, var_types type, bool split, int immediate = 0)
    {
        GenTree operand = type == TYP_INT
            ? new GenTreeIntCon(TYP_INT, immediate)
            : new GenTreeLclVar(type, 0);
        var liberal = type == TYP_INT ? store.VNForIntCon(immediate) : store.VNForExpr(null, type);
        var conservative = split && type != TYP_INT ? store.VNForExpr(null, type) : liberal;
        var firstException = store.VNExcSetSingleton(
            store.VNForFunc(TYP_REF, VNF_NullPtrExc, store.VNForExpr(null, TYP_BYREF)));
        var secondException = split ? store.VNExcSetSingleton(
            store.VNForFunc(TYP_REF, VNF_NullPtrExc, store.VNForExpr(null, TYP_BYREF))) : firstException;
        operand._vnPair = store.VNPWithExc(new(liberal, conservative), new(firstException, secondException));

        return operand;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitDisableSimdVN")]
    private static extern ref int DisableBits(ref JitConfigValues config);

    private static void WithCompiler(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        DisableBits(ref JitConfig) = 0;
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
            compiler.compCurBB.bbMemoryDef = (1 << (int)MemoryKind.GcHeap) | (1 << (int)MemoryKind.ByrefExposed);
            compiler.compCurBB.bbRefs = 1;
            compiler.fgFirstBB = compiler.compCurBB;
            compiler.fgLastBB = compiler.compCurBB;
            compiler.fgPredsComputed = true;
            compiler._dfsTree = compiler.fgComputeDfs();
            compiler._loops = FlowGraphNaturalLoops.Find(compiler._dfsTree);
            compiler._blockToLoop = BlockToNaturalLoopMap.Build(compiler._loops);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            compiler.fgSetCurrentMemoryVN(MemoryKind.GcHeap, store.VNForExpr(compiler.compCurBB, TYP_HEAP));
            compiler.fgSetCurrentMemoryVN(MemoryKind.ByrefExposed, store.VNForExpr(compiler.compCurBB, TYP_HEAP));
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
#endif
