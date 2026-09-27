// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64MemoryLoweringTests
{
    private static bool s_immutable;

    [TestCase(TYP_FLOAT, false, false, 0.0)]
    [TestCase(TYP_DOUBLE, false, true, 0.0)]
    [TestCase(TYP_FLOAT, false, true, 1.25)]
    [TestCase(TYP_DOUBLE, false, true, 1.25)]
    [TestCase(TYP_FLOAT, true, false, 1.25)]
    [TestCase(TYP_DOUBLE, true, false, 1.25)]
    [TestCase(TYP_FLOAT, true, true, 1.25)]
    [TestCase(TYP_DOUBLE, true, true, 1.25)]
    public static void FloatingStoresPreserveNativeRetypingAndVolatileBitcasts(
        var_types type, bool isVolatile, bool optimized, double value)
    {
        WithLowering(optimized, (compiler, lowering, block) => {
            var address = new GenTreeIntCon(TYP_LONG, 0x1000);
            var data = compiler.gtNewDconNode(type, value);
            var store = new GenTreeStoreInd(type, address, data);
            store.Flags |= isVolatile ? GTF_IND_VOLATILE : GTF_EMPTY;
            var next = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            Append(block, address, data, store, next);

            Assert.That(LowerStoreIndirCommon(lowering, store), Is.SameAs(next));
            var integerType = type is TYP_FLOAT ? TYP_INT : TYP_LONG;
            var retyped = isVolatile || value == 0.0;
            Assert.That(store.Type, Is.EqualTo(retyped ? integerType : type));
            Assert.That(store.IsVolatile, Is.EqualTo(isVolatile));
            if (isVolatile && !optimized)
            {
                Assert.That(store.Data.Oper, Is.EqualTo(GT_BITCAST));
                Assert.That(store.Data.AsUnOp().Op1, Is.SameAs(data));
                Assert.That(data.Next, Is.SameAs(store.Data));
            }
            else if (retyped)
            {
                var expected = type is TYP_FLOAT
                    ? BitConverter.SingleToInt32Bits((float)value)
                    : BitConverter.DoubleToInt64Bits(value);
                Assert.That((long)store.Data.AsIntCon().IconValue, Is.EqualTo(expected));
                Assert.That(store.Data.IsContained, Is.EqualTo(expected == 0));
            }
            else
            {
                Assert.That(store.Data, Is.SameAs(data));
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void OrdinaryNegativeZeroStoresStayFloatingPoint(var_types type)
    {
        WithLowering(true, (compiler, lowering, block) => {
            var address = new GenTreeIntCon(TYP_LONG, 0x1000);
            var data = compiler.gtNewDconNode(type, BitConverter.Int64BitsToDouble(long.MinValue));
            var store = new GenTreeStoreInd(type, address, data);
            Append(block, address, data, store);

            _ = LowerStoreIndirCommon(lowering, store);

            Assert.That(store.Type, Is.EqualTo(type));
            Assert.That(store.Data, Is.SameAs(data));
            Assert.That(BitConverter.DoubleToInt64Bits(data.DconVal), Is.EqualTo(long.MinValue));
        });
    }

    [TestCase(TYP_FLOAT, false, false)]
    [TestCase(TYP_DOUBLE, false, true)]
    [TestCase(TYP_FLOAT, true, true)]
    [TestCase(TYP_DOUBLE, true, false)]
    public static void VolatileLoadsReplaceTheirUseAndRetainNativeBitcastFolding(
        var_types type, bool optimized, bool rcpc2)
    {
        WithLowering(optimized, (compiler, lowering, block) => {
            compiler.lvaTable[1].Type = type;
            if (rcpc2)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Rcpc2);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Rcpc2);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Rcpc2);
            }

            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            var offset = new GenTreeIntCon(TYP_LONG, 8);
            var address = new GenTreeOp(GT_ADD, TYP_LONG, local, offset);
            address.Flags |= GTF_ORDER_SIDEEFF;
            var load = new GenTreeIndir(GT_IND, type, address);
            load.Flags |= GTF_IND_VOLATILE | GTF_GLOB_REF;
            var consumer = compiler.gtNewStoreLclVarNode(1, load);
            Append(block, local, offset, address, load, consumer);

            var loweredNext = LowerIndir(lowering, load);
            var bitcast = loweredNext as GenTreeUnOp ?? throw new InvalidOperationException("Missing volatile-load bitcast.");
            Assert.That(bitcast.Oper, Is.EqualTo(GT_BITCAST));
            Assert.That(bitcast.Op1, Is.SameAs(load));
            Assert.That(consumer.Data, Is.SameAs(bitcast));
            Assert.That(load.Type, Is.EqualTo(type is TYP_FLOAT ? TYP_INT : TYP_LONG));
            Assert.That(load.Addr.Oper, Is.EqualTo(rcpc2 ? GT_LEA : GT_ADD));

            Assert.That(LowerBitCast(lowering, bitcast), Is.SameAs(consumer));
            Assert.That(consumer.Data, Is.SameAs(optimized ? load : bitcast));
            Assert.That(load.Type, Is.EqualTo(optimized ? type : type is TYP_FLOAT ? TYP_INT : TYP_LONG));
            Assert.That(load.IsVolatile, Is.True);
        });
    }

    [TestCase(TYP_DOUBLE, 3, TYP_LONG, 8)]
    [TestCase(TYP_SIMD16, 4, TYP_BYTE, 1)]
    public static void UnusedLoadsChooseProbeWidthBeforeFormingAddressModes(
        var_types type, int shiftBy, var_types probeType, int scale)
    {
        WithLowering(false, (compiler, lowering, block) => {
            compiler.lvaTable[1].Type = TYP_LONG;
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            var index = compiler.gtNewLclvNode(TYP_LONG, 1);
            var amount = new GenTreeIntCon(TYP_INT, shiftBy);
            var shift = new GenTreeOp(GT_LSH, TYP_LONG, index, amount);
            var address = new GenTreeOp(GT_ADD, TYP_LONG, local, shift);
            var load = new GenTreeIndir(GT_IND, type, address) { IsUnusedValue = true };
            var next = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            Append(block, local, index, amount, shift, address, load, next);

            Assert.That(LowerIndir(lowering, load), Is.SameAs(next));

            var probe = next.Prev ?? throw new InvalidOperationException("Missing lowered probe.");
            Assert.That(probe.Oper, Is.EqualTo(GT_NULLCHECK));
            Assert.That(probe.Type, Is.EqualTo(probeType));
            Assert.That(probe.AsIndir().Addr.AsAddrMode().Scale, Is.EqualTo(scale));
            Assert.That(probe.AsIndir().Addr.IsContained, Is.True);
            Assert.That(load.Next, Is.Null);
            Assert.That(probe.IsUnusedValue, Is.False);
#if DEBUG
            Assert.That(probe.TreeId, Is.EqualTo(load.TreeId));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PublishingObjectHandlesUsesReleaseOnlyForMutableObjects(bool immutable)
    {
        WithLowering(true, (compiler, lowering, block) => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.isObjectImmutable = &IsImmutable;
            var ee = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &ee;
            s_immutable = immutable;
            var address = new GenTreeIntCon(TYP_LONG, 0x1000);
            var data = new GenTreeIntCon(TYP_REF, 42) { Flags = GTF_ICON_OBJ_HDL };
            var store = new GenTreeStoreInd(TYP_REF, address, data);
            Append(block, address, data, store);

            _ = LowerStoreIndirCommon(lowering, store);

            Assert.That(store.IsVolatile, Is.EqualTo(!immutable));
            Assert.That(store.Data, Is.SameAs(data));
        });
    }

    [Test]
    public static void WriteBarrierStoresReturnBeforeOrdinaryAddressContainment()
    {
        WithLowering(true, (compiler, lowering, block) => {
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            var offset = new GenTreeIntCon(TYP_LONG, 8);
            var address = new GenTreeOp(GT_ADD, TYP_LONG, local, offset);
            var value = compiler.gtNewLclvNode(TYP_REF, 2);
            var store = new GenTreeStoreInd(TYP_REF, address, value) { Flags = GTF_IND_TGT_HEAP };
            var next = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            Append(block, local, offset, address, value, store, next);

            Assert.That(LowerStoreIndirCommon(lowering, store), Is.SameAs(next));
            Assert.That(store.Addr.Oper, Is.EqualTo(GT_LEA));
            Assert.That(store.Addr.IsContained, Is.False);
            Assert.That(value.IsContained, Is.False);
        });
    }

    [TestCase(0x1000, false, true)]
    [TestCase(0x1008, false, true)]
    [TestCase(0x1004, false, false)]
    [TestCase(0x1004, true, true)]
    public static void LongStoreCoalescingPreservesArm64AtomicGranules(int address, bool allowNonAtomic, bool merged)
    {
        WithLowering(true, (compiler, lowering, block) => {
            var first = ConstantStore(block, TYP_LONG, address, new GenTreeIntCon(TYP_LONG, 1));
            var second = ConstantStore(block, TYP_LONG, address + 8, new GenTreeIntCon(TYP_LONG, 2));
            if (allowNonAtomic)
            {
                first.Flags |= GTF_IND_ALLOW_NON_ATOMIC;
                second.Flags |= GTF_IND_ALLOW_NON_ATOMIC;
            }

            _ = LowerStoreIndirCommon(lowering, first);
            _ = LowerStoreIndirCommon(lowering, second);

            Assert.That(second.Type, Is.EqualTo(merged ? TYP_SIMD16 : TYP_LONG));
            Assert.That(first.Next is null, Is.EqualTo(merged));
            if (merged)
            {
                Assert.That(second.Data.AsVecCon().SimdVal.u64[0], Is.EqualTo(1));
                Assert.That(second.Data.AsVecCon().SimdVal.u64[1], Is.EqualTo(2));
                Assert.That(second.Addr.AsIntCon().IconValue, Is.EqualTo((nint)address));
            }
        });
    }

    [Test]
    public static void EqualSimd16ConstantsAreReusedWithoutWidening()
    {
        WithLowering(true, (compiler, lowering, block) => {
            var firstValue = compiler.gtNewVconNode(TYP_SIMD16);
            firstValue.SimdVal.u64[0] = 0x1122334455667788;
            var secondValue = compiler.gtNewVconNode(TYP_SIMD16);
            secondValue.SimdVal = firstValue.SimdVal;
            var first = ConstantStore(block, TYP_SIMD16, 0x2000, firstValue);
            var second = ConstantStore(block, TYP_SIMD16, 0x2010, secondValue);

            _ = LowerStoreIndirCommon(lowering, first);
            _ = LowerStoreIndirCommon(lowering, second);

            Assert.That(first.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(second.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(first.Data.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(second.Data.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(first.Data.AsLclVar().LclNum, Is.EqualTo(second.Data.AsLclVar().LclNum));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void LoadLoweringRunsPairSchedulingAndReturnsTheOriginalSuccessor()
    {
        WithLowering(true, (compiler, lowering, block) => {
            var firstAddress = compiler.gtNewLclvNode(TYP_LONG, 0);
            var first = new GenTreeIndir(GT_IND, TYP_LONG, firstAddress);
            first.Flags |= GTF_GLOB_REF | GTF_IND_NONFAULTING;
            var noise = new GenTreeIntCon(TYP_INT, 42) { IsUnusedValue = true };
            var secondBase = compiler.gtNewLclvNode(TYP_LONG, 0);
            var offset = new GenTreeIntCon(TYP_LONG, 8);
            var secondAddress = new GenTreeOp(GT_ADD, TYP_LONG, secondBase, offset);
            var second = new GenTreeIndir(GT_IND, TYP_LONG, secondAddress);
            second.Flags |= GTF_GLOB_REF | GTF_IND_NONFAULTING;
            var consumer = new GenTreeOp(GT_ADD, TYP_LONG, first, second) { IsUnusedValue = true };
            Append(block, firstAddress, first, noise, secondBase, offset, secondAddress, second, consumer);

            Assert.That(LowerIndir(lowering, first), Is.SameAs(noise));
            Assert.That(LowerIndir(lowering, second), Is.SameAs(consumer));
            Assert.That(second.Next, Is.SameAs(noise));
            Assert.That(second.Addr.Oper, Is.EqualTo(GT_LEA));
            Assert.That(second.Addr.IsContained, Is.True);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    private static GenTreeStoreInd ConstantStore(BasicBlock block, var_types type, int address, GenTree value)
    {
        var addr = new GenTreeIntCon(TYP_LONG, address);
        var store = new GenTreeStoreInd(type, addr, value);
        Append(block, addr, value, store);
        return store;
    }

    private static void Append(BasicBlock block, params GenTree[] nodes)
    {
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsImmutable(ICorJitInfo* self, CORINFO_OBJECT_STRUCT_* obj) => s_immutable ? (byte)1 : (byte)0;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerStoreIndirCommon")]
    private static extern GenTree? LowerStoreIndirCommon(Lowering lowering, GenTreeStoreInd store);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerIndir")]
    private static extern GenTree? LowerIndir(Lowering lowering, GenTreeIndir indir);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerBitCast")]
    private static extern GenTree? LowerBitCast(Lowering lowering, GenTreeUnOp bitcast);

    private static void WithLowering(bool optimized, Action<Compiler, Lowering, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(!optimized);
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.compHndBBtab = [];
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = TYP_LONG;
        compiler.lvaTable[1].Type = TYP_FLOAT;
        compiler.lvaTable[2].Type = TYP_REF;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(Arm64MemoryLoweringTests);
#endif
        JitTls.Compiler = compiler;

        try
        {
            compiler.codeGen = new CodeGen(compiler);
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            block.bbRefs = 1;
            block.MakeLir(null, null);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
