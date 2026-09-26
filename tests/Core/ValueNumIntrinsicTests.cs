// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumIntrinsicTests
{
    [Test]
    public static void MathAndBitCountsCarryOrderedOperandExceptions()
    {
        WithCompiler((compiler, store) => {
            var left = compiler.gtNewDconNode(TYP_FLOAT, 2);
            var right = compiler.gtNewDconNode(TYP_FLOAT, 3);
            var leftExc = store.VNExcSetSingleton(
                store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc, store.VNForIntCon(1)));
            var rightExc = store.VNExcSetSingleton(
                store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc, store.VNForIntCon(2)));
            left._vnPair.SetBoth(store.VNWithExc(store.VNForFloatCon(2), leftExc));
            right._vnPair.SetBoth(store.VNWithExc(store.VNForFloatCon(3), rightExc));

            var binary = new GenTreeIntrinsic(TYP_FLOAT, left, right, NI_System_Math_Pow, null);
            compiler.fgValueNumberIntrinsic(binary);
            store.VNPUnpackExc(binary._vnPair, out var value, out var exceptions);
            Assert.That(store.GetConstantSingle(value.Liberal), Is.EqualTo(8));
            Assert.That(exceptions.Liberal, Is.EqualTo(store.VNExcSetUnion(leftExc, rightExc)));
            Assert.That(exceptions.Conservative, Is.EqualTo(exceptions.Liberal));

            var bitOperand = compiler.gtNewIconNode(TYP_INT, 0);
            bitOperand._vnPair.SetBoth(store.VNWithExc(store.VNForIntCon(0), leftExc));
            var unary = new GenTreeIntrinsic(TYP_INT, bitOperand, NI_PRIMITIVE_LeadingZeroCount, null);
            compiler.fgValueNumberIntrinsic(unary);
            Assert.That(store.VNNormalValue(unary._vnPair.Liberal),
                Is.EqualTo(store.VNForIntCon(32)));
            Assert.That(store.VNExceptionSet(unary._vnPair.Liberal), Is.EqualTo(leftExc));
        });
    }

    [Test]
    public static void SaturatingIntrinsicAndLog2UseDistinctNativeVNContracts()
    {
        WithCompiler((compiler, store) => {
            var operand = compiler.gtNewIconNode(TYP_INT, 0);
            var value = store.VNForExpr(compiler.compCurBB, TYP_INT);
            operand._vnPair.SetBoth(value);
            var saturate = new GenTreeIntrinsic(TYP_INT, operand, NI_PRIMITIVE_SaturateToUInt8, null);
            compiler.fgValueNumberIntrinsic(saturate);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(saturate._vnPair.Liberal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_SaturateToUInt8));
            Assert.That(app.GetArg(0), Is.EqualTo(value));

            var log2 = new GenTreeIntrinsic(TYP_INT, operand, NI_PRIMITIVE_Log2, null);
            compiler.fgValueNumberIntrinsic(log2);
            Assert.That(log2._vnPair.Liberal, Is.Not.EqualTo(saturate._vnPair.Liberal));
            Assert.That(log2._vnPair.Liberal, Is.Not.EqualTo(value));
            Assert.That(store.TypeOfVN(log2._vnPair.Liberal), Is.EqualTo(TYP_INT));
        });
    }

    [Test]
    public static void BitCastAndCastPreserveSourceExceptionPairs()
    {
        WithCompiler((compiler, store) => {
            var operand = compiler.gtNewIconNode(TYP_INT, 0);
            var exception = store.VNExcSetSingleton(
                store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc, ValueNumStore.VNForNull()));
            operand._vnPair.SetBoth(store.VNWithExc(store.VNForIntCon(0x3F800000), exception));
            var bitCast = new GenTreeUnOp(genTreeOps.GT_BITCAST, TYP_FLOAT, operand);
            compiler.fgValueNumberBitCast(bitCast);
            Assert.That(store.GetConstantSingle(store.VNNormalValue(bitCast._vnPair.Liberal)),
                Is.EqualTo(1.0f));
            Assert.That(store.VNExceptionSet(bitCast._vnPair.Liberal), Is.EqualTo(exception));

            var cast = new GenTreeCast(TYP_LONG, operand, fromUnsigned: false, castType: TYP_LONG);
            compiler.fgValueNumberCastTree(cast);
            Assert.That(store.GetConstantInt64(store.VNNormalValue(cast._vnPair.Liberal)),
                Is.EqualTo(0x3F800000));
            Assert.That(store.VNExceptionSet(cast._vnPair.Liberal), Is.EqualTo(exception));
        });
    }

    [Test]
    public static void ArrayAddressEncodesSignedElementTypeAndKeepsAddressExceptions()
    {
        WithCompiler((compiler, store) => {
            var array = new GenTreeLclVar(TYP_REF, 0);
            var arrayVN = store.VNForExpr(compiler.compCurBB, TYP_REF);
            array._vnPair.SetBoth(arrayVN);
            var offset = compiler.gtNewIconNode(TYP_I_IMPL, 16);
            offset._vnPair.SetBoth(store.VNForIntPtrCon(16));
            var address = new GenTreeOp(genTreeOps.GT_ADD, TYP_BYREF, array, offset);
            var exception = store.VNExcSetSingleton(
                store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc, arrayVN));
            address._vnPair.SetBoth(store.VNWithExc(store.VNForExpr(compiler.compCurBB, TYP_BYREF),
                exception));
            var node = new GenTreeArrAddr(address, TYP_UINT, null, 16);
            compiler.fgValueNumberArrIndexAddr(node);

            store.VNPUnpackExc(node._vnPair, out var normal, out var exceptions);
            Assert.That(normal.BothEqual(), Is.True);
            Assert.That(exceptions.Liberal, Is.EqualTo(exception));
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(normal.Liberal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_PtrToArrElem));
            Assert.That(store.GetHandleFlags(app.GetArg(0)), Is.EqualTo(GTF_ICON_CLASS_HDL));
            Assert.That(store.ConstantValue<nint>(app.GetArg(0)), Is.EqualTo(
                (nint)((((int)TYP_INT) << 1) | 1)));
            Assert.That(app.GetArg(1), Is.EqualTo(arrayVN));
            Assert.That(app.GetArg(2), Is.EqualTo(store.VNForPtrSizeIntCon(0)));
            Assert.That(app.GetArg(3), Is.EqualTo(store.VNForIntPtrCon(0)));
        });
    }

    [Test]
    public static void UnparseableArrayAddressIsUniqueWithoutLosingExceptions()
    {
        WithCompiler((compiler, store) => {
            var address = compiler.gtNewIconNode(TYP_BYREF, 0);
            var exception = store.VNExcSetSingleton(
                store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc, ValueNumStore.VNForNull()));
            address._vnPair.SetBoth(store.VNWithExc(store.VNForExpr(compiler.compCurBB, TYP_BYREF),
                exception));
            var node = new GenTreeArrAddr(address, TYP_INT, null, 16);
            compiler.fgValueNumberArrIndexAddr(node);
            var first = node._vnPair.Liberal;
            compiler.fgValueNumberArrIndexAddr(node);
            Assert.That(node._vnPair.Liberal, Is.Not.EqualTo(first));
            Assert.That(store.VNExceptionSet(node._vnPair.Liberal), Is.EqualTo(exception));
        });
    }

    [Test]
    public static void HardwareIntrinsicIncludesResultTypeAfterOperands()
    {
        WithCompiler((compiler, store) => {
            var left = new GenTreeVecCon(TYP_SIMD16);
            var right = new GenTreeVecCon(TYP_SIMD16);
            var leftVN = store.VNForExpr(compiler.compCurBB, TYP_SIMD16);
            var rightVN = store.VNForExpr(compiler.compCurBB, TYP_SIMD16);
            left._vnPair.SetBoth(leftVN);
            right._vnPair.SetBoth(rightVN);
            var add = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_Add, TYP_INT, 16, left, right);
            compiler.fgValueNumberHWIntrinsic(add);
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(add._vnPair.Liberal, ref app), Is.True);
            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_HWI_X86Base_Add));
            Assert.That(app.GetArg(0), Is.EqualTo(leftVN));
            Assert.That(app.GetArg(1), Is.EqualTo(rightVN));
            var type = new VNFuncApp();
            Assert.That(store.GetVNFunc(app.GetArg(2), ref type), Is.True);
            Assert.That(type.Func, Is.EqualTo(VNFunc.VNF_SimdType));
            Assert.That(store.GetConstantInt32(type.GetArg(0)), Is.EqualTo(16));
            Assert.That(store.GetConstantInt32(type.GetArg(1)), Is.EqualTo((int)TYP_INT));
        });
    }

    [Test]
    public static void HardwareStoreMutatesHeapBeforeNumberingOperands()
    {
        WithCompiler((compiler, store) => {
            compiler.byrefStatesMatchGcHeapStates = true;
            var heap = store.VNForExpr(compiler.compCurBB, TYP_HEAP);
            compiler.fgSetCurrentMemoryVN(MemoryKind.GcHeap, heap);
            var address = new GenTreeLclVar(TYP_BYREF, 0);
            address._vnPair.SetBoth(store.VNForExpr(compiler.compCurBB, TYP_BYREF));
            var vector = new GenTreeVecCon(TYP_SIMD16);
            vector._vnPair.SetBoth(store.VNForExpr(compiler.compCurBB, TYP_SIMD16));
            var storeNode = new GenTreeHWIntrinsic(TYP_VOID, NI_X86Base_StoreAligned, TYP_INT, 16,
                address, vector);

            compiler.fgValueNumberHWIntrinsic(storeNode);

            var currentHeap = compiler.fgCurMemoryVN[(int)MemoryKind.GcHeap];
            Assert.That(currentHeap, Is.Not.EqualTo(heap));
            Assert.That(compiler.fgCurMemoryVN[(int)MemoryKind.ByrefExposed], Is.EqualTo(currentHeap));
            var intrinsic = new VNFuncApp();
            Assert.That(store.GetVNFunc(store.VNNormalValue(storeNode._vnPair.Liberal), ref intrinsic), Is.True);
            var load = new VNFuncApp();
            Assert.That(store.GetVNFunc(intrinsic.GetArg(0), ref load), Is.True);
            Assert.That(load.Func, Is.EqualTo(VNFunc.VNF_ByrefExposedLoad));
            Assert.That(load.GetArg(2), Is.EqualTo(currentHeap));
        });
    }

    private static void WithCompiler(Action<Compiler, ValueNumStore> action)
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
            compiler.compCurBB.bbMemoryDef = (1 << (int)MemoryKind.GcHeap) |
                (1 << (int)MemoryKind.ByrefExposed);
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
