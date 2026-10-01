// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareConstructionClassificationCompletionTests
{
    [Test]
    public static void FixedIntegralBroadcastPreservesNativeTruncationAndEveryLane(
        [Values(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG)] var_types baseType,
        [Values(-1L, 0L, 0x180000001L)] long value)
    {
        WithCompiler(compiler =>
        {
            GenTree scalar = baseType.Size == 8
                ? compiler.gtNewLconNode(value)
                : compiler.gtNewIconNode(TYP_INT, unchecked((int)value));
            var result = compiler.gtNewSimdCreateBroadcastNode(TYP_SIMD16, scalar, baseType, 16).AsVecCon();
            var expected = unchecked((ulong)value) & (ulong.MaxValue >> (64 - (baseType.Size * 8)));
            var unsignedType = baseType.Size switch
            {
                1 => TYP_UBYTE,
                2 => TYP_USHORT,
                4 => TYP_UINT,
                _ => TYP_ULONG,
            };
            for (var index = 0; index < (16 / baseType.Size); index++)
            {
                Assert.That(result.GetIntegralVectorConstElement(index, unsignedType), Is.EqualTo(expected));
            }
            Assert.That(scalar.AsIntConCommon().IntegralValue,
                Is.EqualTo(baseType.Size == 8 ? value : unchecked((int)value)));
        });
    }

    [TestCase(TYP_FLOAT, 0x8000000000000000UL)]
    [TestCase(TYP_FLOAT, 0x7FF8000000001234UL)]
    [TestCase(TYP_FLOAT, 0x3FF8000000000000UL)]
    [TestCase(TYP_DOUBLE, 0x8000000000000000UL)]
    [TestCase(TYP_DOUBLE, 0x7FF8000000001234UL)]
    [TestCase(TYP_DOUBLE, 0x3FF8000000000000UL)]
    public static void FixedFloatingBroadcastPreservesConversionNaNPayloadAndSignedZero(
        var_types baseType, ulong bits)
    {
        WithCompiler(compiler =>
        {
            var value = BitConverter.Int64BitsToDouble(unchecked((long)bits));
            var scalar = compiler.gtNewDconNode(baseType, value);
            var result = compiler.gtNewSimdCreateBroadcastNode(TYP_SIMD16, scalar, baseType, 16).AsVecCon();
            if (baseType == TYP_FLOAT)
            {
                var expected = unchecked((uint)BitConverter.SingleToInt32Bits((float)value));
                for (var index = 0; index < 4; index++)
                {
                    Assert.That(result.SimdVal.u32[index], Is.EqualTo(expected));
                }
            }
            else
            {
                Assert.That(result.SimdVal.u64[0], Is.EqualTo(bits));
                Assert.That(result.SimdVal.u64[1], Is.EqualTo(bits));
            }
            Assert.That(BitConverter.DoubleToInt64Bits(scalar.DconVal), Is.EqualTo(unchecked((long)bits)));
        });
    }

    [TestCase((byte)8)]
    [TestCase((byte)12)]
    [TestCase((byte)16)]
#if TARGET_XARCH
    [TestCase((byte)32)]
    [TestCase((byte)64)]
#endif
    public static void FixedBroadcastUsesEveryLogicalLaneAndLeavesTheStorageTailZero(byte size)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var result = compiler.gtNewSimdCreateBroadcastNode(type, compiler.gtNewDconNode(TYP_FLOAT, 1.5),
                TYP_FLOAT, size).AsVecCon();
            Assert.That(result.Type, Is.EqualTo(type));
            for (var index = 0; index < (size / 4); index++)
            {
                Assert.That(result.SimdVal.u32[index], Is.EqualTo(0x3FC00000U));
            }
            for (var index = size / 4; index < result.SimdVal.AsSpan<uint>().Length; index++)
            {
                Assert.That(result.SimdVal.u32[index], Is.EqualTo(0U));
            }
        });
    }

    [Test]
    public static void NonconstantBroadcastUsesNativeCreateWithoutDuplicatingItsSource(
        [Values(TYP_INT, TYP_LONG, TYP_FLOAT, TYP_DOUBLE)] var_types baseType,
        [Values(false, true)] bool effectful)
    {
        WithCompiler(compiler =>
        {
            var value = Local(compiler, baseType);
            var effect = compiler.gtNewStoreLclVarNode(value.LclNum, Local(compiler, baseType));
            GenTree source = effectful
                ? compiler.gtNewBinaryNode(GT_COMMA, baseType, effect, value)
                : value;
            var result = compiler.gtNewSimdCreateBroadcastNode(TYP_SIMD16, source, baseType, 16).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_Vector_Create));
            Assert.That(result.SimdBaseType, Is.EqualTo(baseType));
            Assert.That(result.GetOp(1), Is.SameAs(source));
            Assert.That(CountReferences(result, value), Is.EqualTo(1));
            Assert.That(CountReferences(result, effect), Is.EqualTo(effectful ? 1 : 0));
            Assert.That(result.IsMemoryLoad(out var loadAddress), Is.False);
            Assert.That(result.IsMemoryStore(out var storeAddress), Is.False);
            Assert.That(loadAddress, Is.Null);
            Assert.That(storeAddress, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MetadataLoadReturnsItsAddressEvenWhenThePointerValueIsZero(bool zero)
    {
        WithCompiler(compiler =>
        {
            var address = Address(compiler, zero);
#if TARGET_XARCH
            var id = NI_X86Base_LoadAlignedVector128;
#elif TARGET_ARM64
            var id = NI_AdvSimd_LoadAndReplicateToVector128;
#elif TARGET_WASM
            var id = NI_PackedSimd_LoadScalarAndSplatVector128;
#else
#error Unsupported hardware classification test target
#endif
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, id, TYP_FLOAT, 16, address);
            AssertMemoryClassification(node, address, false);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MetadataStoreReturnsItsAddressAndPreservesMutationAndExceptionFlags(bool zero)
    {
        WithCompiler(compiler =>
        {
            var address = Address(compiler, zero);
            var value = Local(compiler, TYP_SIMD16);
#if TARGET_XARCH
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_VOID, NI_X86Base_StoreLow, TYP_FLOAT, 16,
                address, value);
#elif TARGET_ARM64
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_VOID, NI_AdvSimd_StoreSelectedScalar, TYP_FLOAT, 16,
                address, value, compiler.gtNewIconNode(TYP_INT, 0));
#elif TARGET_WASM
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_VOID, NI_PackedSimd_StoreSelectedScalar, TYP_FLOAT, 16,
                address, value, compiler.gtNewIconNode(TYP_INT, 0));
#else
#error Unsupported hardware classification test target
#endif
            AssertMemoryClassification(node, address, true);
        });
    }

    [Test]
    public static void PureArithmeticClearsBothOutAddressesAndDoesNotGainMemoryFlags()
    {
        WithCompiler(compiler =>
        {
#if TARGET_XARCH
            var id = NI_X86Base_Add;
#elif TARGET_ARM64
            var id = NI_AdvSimd_Add;
#elif TARGET_WASM
            var id = NI_PackedSimd_Add;
#else
#error Unsupported hardware classification test target
#endif
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, id, TYP_INT, 16,
                Local(compiler, TYP_SIMD16), Local(compiler, TYP_SIMD16));
            var loadAddress = (GenTree?)Address(compiler, false);
            var storeAddress = loadAddress;
            Assert.That(node.IsMemoryLoad(out loadAddress), Is.False);
            Assert.That(node.IsMemoryStore(out storeAddress), Is.False);
            Assert.That(loadAddress, Is.Null);
            Assert.That(storeAddress, Is.Null);
            Assert.That(node.IsMemoryLoadOrStore, Is.False);
            Assert.That(node.Flags & (GTF_ASG | GTF_GLOB_REF | GTF_EXCEPT), Is.EqualTo(GTF_EMPTY));
        });
    }

#if TARGET_XARCH
    [TestCase(NI_X86Base_LoadLow)]
    [TestCase(NI_X86Base_LoadHigh)]
    public static void XarchPartialLoadsSelectTheSecondOperand(NamedIntrinsic id)
    {
        WithCompiler(compiler =>
        {
            var address = Address(compiler, false);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, id, TYP_DOUBLE, 16,
                Local(compiler, TYP_SIMD16), address);
            AssertMemoryClassification(node, address, false);
        });
    }

    [Test]
    public static void XarchMaskMoveSelectsTheThirdOperand()
    {
        WithCompiler(compiler =>
        {
            var address = Address(compiler, false);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_VOID, NI_X86Base_MaskMove, TYP_UBYTE, 16,
                Local(compiler, TYP_SIMD16), Local(compiler, TYP_SIMD16), address);
            AssertMemoryClassification(node, address, true);
        });
    }

    [Test]
    public static void XarchPointerOverloadsDependOnAuxiliaryTypeNotTheOperandShape(
        [Values(NI_X86Base_ConvertToVector128Int16, NI_AVX2_BroadcastScalarToVector128)] NamedIntrinsic id,
        [Values(false, true)] bool pointer)
    {
        WithCompiler(compiler =>
        {
            GenTree operand = pointer ? Address(compiler, false) : Local(compiler, TYP_SIMD16);
            var baseType = id == NI_X86Base_ConvertToVector128Int16 ? TYP_BYTE : TYP_INT;
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, id, baseType, 16, operand);
            Assert.That(node.AuxiliaryType, Is.EqualTo(TYP_UNKNOWN));
            Assert.That(node.IsMemoryLoad(out var initialAddress), Is.False);
            Assert.That(initialAddress, Is.Null);
            node.AuxiliaryType = pointer ? TYP_U_IMPL : TYP_UNKNOWN;
            Assert.That(node.IsMemoryLoad(out var address), Is.EqualTo(pointer));
            Assert.That(address, pointer ? Is.SameAs(operand) : Is.Null);
            Assert.That(node.IsMemoryStore(out var storeAddress), Is.False);
            Assert.That(storeAddress, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void XarchGatherAddressPositionDependsOnItsMaskedForm(bool masked)
    {
        WithCompiler(compiler =>
        {
            var address = Address(compiler, false);
            var indices = Local(compiler, TYP_SIMD16);
            var scale = compiler.gtNewIconNode(TYP_INT, 4);
            var node = masked
                ? compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX2_GatherMaskVector128, TYP_INT, 16,
                    Local(compiler, TYP_SIMD16), address, indices, Local(compiler, TYP_SIMD16), scale)
                : compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX2_GatherVector128, TYP_INT, 16,
                    address, indices, scale);
            AssertMemoryClassification(node, address, false);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void XarchMultiplyNoFlagsHasStoreSemanticsOnlyForItsOutArgument(bool store)
    {
        WithCompiler(compiler =>
        {
            var address = Address(compiler, false);
            var left = compiler.gtNewIconNode(TYP_INT, 1);
            var right = compiler.gtNewIconNode(TYP_INT, 2);
            var node = store
                ? compiler.gtNewSimdHWIntrinsicNode(TYP_INT, NI_AVX2_MultiplyNoFlags, TYP_INT, 0, left, right, address)
                : compiler.gtNewSimdHWIntrinsicNode(TYP_INT, NI_AVX2_MultiplyNoFlags, TYP_INT, 0, left, right);
            Assert.That(node.IsMemoryStore(out var actual), Is.EqualTo(store));
            Assert.That(actual, store ? Is.SameAs(address) : Is.Null);
            Assert.That(node.IsMemoryLoad(out var loaded), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(node.Flags & (GTF_ASG | GTF_GLOB_REF | GTF_EXCEPT),
                Is.EqualTo(store ? GTF_ASG | GTF_GLOB_REF | GTF_EXCEPT : GTF_EMPTY));
        });
    }
#endif

#if TARGET_ARM64
    [Test]
    public static void Arm64NonconstantScalableBroadcastRetainsNativeCreate(
        [Values(TYP_INT, TYP_LONG, TYP_FLOAT, TYP_DOUBLE)] var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var value = Local(compiler, baseType);
            var result = compiler.gtNewSimdCreateBroadcastNode(TYP_SIMD, value, baseType, 0).AsHWIntrinsic();
            Assert.That(result.Type, Is.EqualTo(TYP_SIMD));
            Assert.That(result.HWIntrinsicId, Is.EqualTo(NI_Vector_Create));
            Assert.That(result.SimdBaseType, Is.EqualTo(baseType));
            Assert.That(result.GetOp(1), Is.SameAs(value));
        });
    }

    [Test]
    public static void Arm64ScalableIntegralBroadcastStoresOneMaskedRepeatedElement(
        [Values(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG)] var_types baseType,
        [Values(-1L, long.MinValue, 0x180000001L)] long value)
    {
        WithCompiler(compiler =>
        {
            GenTree scalar = baseType.Size == 8
                ? compiler.gtNewLconNode(value)
                : compiler.gtNewIconNode(TYP_INT, unchecked((int)value));
            var result = compiler.gtNewSimdCreateBroadcastNode(TYP_SIMD, scalar, baseType, 0).AsVecCon();
            ref var scalable = ref result.SimdScalableVal;
            var expected = unchecked((ulong)value) & (ulong.MaxValue >> (64 - (baseType.Size * 8)));
            Assert.That(result.Type, Is.EqualTo(TYP_SIMD));
            Assert.That(scalable.BaseType, Is.EqualTo(baseType));
            Assert.That(scalable.Kind, Is.EqualTo(SimdScalableKind.SimdScalableRepeated));
            Assert.That(scalable.Index.u64[0], Is.EqualTo(expected));
            Assert.That(scalable.Step.u64[0], Is.EqualTo(0UL));
        });
    }

    [TestCase(TYP_FLOAT, 0x8000000000000000UL)]
    [TestCase(TYP_FLOAT, 0x7FF8000000001234UL)]
    [TestCase(TYP_FLOAT, 0x3FF8000000000000UL)]
    [TestCase(TYP_DOUBLE, 0x8000000000000000UL)]
    [TestCase(TYP_DOUBLE, 0x7FF8000000001234UL)]
    [TestCase(TYP_DOUBLE, 0x3FF8000000000000UL)]
    public static void Arm64ScalableFloatingBroadcastPreservesRawRepeatedIndex(var_types baseType, ulong bits)
    {
        WithCompiler(compiler =>
        {
            var value = BitConverter.Int64BitsToDouble(unchecked((long)bits));
            var scalar = compiler.gtNewDconNode(baseType, value);
            var result = compiler.gtNewSimdCreateBroadcastNode(TYP_SIMD, scalar, baseType, 0).AsVecCon();
            ref var scalable = ref result.SimdScalableVal;
            var expected = baseType == TYP_FLOAT
                ? unchecked((uint)BitConverter.SingleToInt32Bits((float)value))
                : bits;
            Assert.That(scalable.Kind, Is.EqualTo(SimdScalableKind.SimdScalableRepeated));
            Assert.That(scalable.BaseType, Is.EqualTo(baseType));
            Assert.That(scalable.Index.u64[0], Is.EqualTo(expected));
            Assert.That(scalable.Step.u64[0], Is.EqualTo(0UL));
        });
    }

    [Test]
    public static void Arm64LaneLoadSelectsTheThirdOperand()
    {
        WithCompiler(compiler =>
        {
            var address = Address(compiler, false);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AdvSimd_LoadAndInsertScalar, TYP_INT, 16,
                Local(compiler, TYP_SIMD16), compiler.gtNewIconNode(TYP_INT, 0), address);
            AssertMemoryClassification(node, address, false);
        });
    }

    [TestCase(NI_Sve_GatherVector, false)]
    [TestCase(NI_Sve_GatherVector, true)]
    [TestCase(NI_Sve2_GatherVectorNonTemporal, false)]
    [TestCase(NI_Sve2_GatherVectorNonTemporal, true)]
    public static void Arm64GatherAcceptsNativeScalarOrVectorAddresses(NamedIntrinsic id, bool vectorAddress)
    {
        WithCompiler(compiler =>
        {
            GenTree address = vectorAddress ? Local(compiler, TYP_SIMD) : Address(compiler, false);
            var mask = Local(compiler, TYP_MASK);
            var node = vectorAddress
                ? compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD, id, TYP_LONG, 0, mask, address)
                : compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD, id, TYP_LONG, 0,
                    mask, address, Local(compiler, TYP_SIMD));
            AssertMemoryClassification(node, address, false);
        });
    }

    [TestCase(NI_Sve_Scatter, false)]
    [TestCase(NI_Sve_Scatter, true)]
    [TestCase(NI_Sve2_ScatterNonTemporal, false)]
    [TestCase(NI_Sve2_ScatterNonTemporal, true)]
    public static void Arm64ScatterAcceptsNativeScalarOrVectorAddresses(NamedIntrinsic id, bool vectorAddress)
    {
        WithCompiler(compiler =>
        {
            GenTree address = vectorAddress ? Local(compiler, TYP_SIMD) : Address(compiler, false);
            var mask = Local(compiler, TYP_MASK);
            var value = Local(compiler, TYP_SIMD);
            var node = vectorAddress
                ? compiler.gtNewSimdHWIntrinsicNode(TYP_VOID, id, TYP_LONG, 0, mask, address, value)
                : compiler.gtNewSimdHWIntrinsicNode(TYP_VOID, id, TYP_LONG, 0,
                    mask, address, Local(compiler, TYP_SIMD), value);
            AssertMemoryClassification(node, address, true);
        });
    }
#endif

#if TARGET_WASM
    [Test]
    public static void WasmLoweredLoadsUseTheFirstOperandAndNeverBecomePure(
        [Values(NI_PackedSimd_LoadScalarAndInsert, NI_PackedSimd_LoadScalarAndSplatVector128,
            NI_PackedSimd_LoadScalarVector128, NI_PackedSimd_LoadWideningVector128)] NamedIntrinsic id,
        [Values(false, true)] bool zero)
    {
        WithCompiler(compiler =>
        {
            Assert.That(HWIntrinsicInfo.IsInvalidNodeId(id), Is.False);
            var address = Address(compiler, zero);
            var node = id == NI_PackedSimd_LoadScalarAndInsert
                ? compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, id, TYP_INT, 16,
                    address, Local(compiler, TYP_SIMD16), compiler.gtNewIconNode(TYP_INT, 1))
                : compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, id, TYP_INT, 16, address);
            AssertMemoryClassification(node, address, false);
        });
    }
#endif

    private static void AssertMemoryClassification(GenTreeHWIntrinsic node, GenTree address, bool store)
    {
        var flags = node.Flags;
        Assert.That(node.IsMemoryLoad(out var loaded), Is.EqualTo(!store));
        Assert.That(node.IsMemoryStore(out var stored), Is.EqualTo(store));
        Assert.That(loaded, store ? Is.Null : Is.SameAs(address));
        Assert.That(stored, store ? Is.SameAs(address) : Is.Null);
        Assert.That(node.IsMemoryLoadOrStore, Is.True);
        Assert.That(node.Flags, Is.EqualTo(flags));
        Assert.That(flags & (GTF_GLOB_REF | GTF_EXCEPT), Is.EqualTo(GTF_GLOB_REF | GTF_EXCEPT));
        Assert.That(flags & GTF_ASG, Is.EqualTo(store ? GTF_ASG : GTF_EMPTY));
    }

    private static GenTreeIntCon Address(Compiler compiler, bool zero)
    {
        return compiler.gtNewIconNode(TYP_I_IMPL, zero ? 0 : 128);
    }

    private static int CountReferences(GenTree tree, GenTree value)
    {
        var count = ReferenceEquals(tree, value) ? 1 : 0;
        foreach (var operand in tree.Operands)
        {
            count += CountReferences(operand, value);
        }

        return count;
    }

    private static GenTreeLclVar Local(Compiler compiler, var_types type)
    {
        var index = compiler.lvaCount++;
        compiler.lvaTable[index] = new LclVarDsc { Type = type };

        return compiler.gtNewLclvNode(type, index);
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info();
        compiler.lvaTable = new LclVarDsc[128];
        compiler.lvaCount = 2;
        compiler.lvaTable[0] = new LclVarDsc { Type = TYP_SIMD16 };
        compiler.lvaTable[1] = new LclVarDsc { Type = TYP_INT };
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
#endif
