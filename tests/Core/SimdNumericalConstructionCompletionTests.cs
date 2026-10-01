// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && (TARGET_XARCH || TARGET_ARM64 || TARGET_WASM)
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;
#if TARGET_XARCH
using static RyuJitSharp.CORINFO_InstructionSet;
#elif TARGET_ARM64
using static RyuJitSharp.SimdScalableKind;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SimdNumericalConstructionCompletionTests
{
    [Test]
    public static void RoundingPreservesSourceBitsAndSelectsNativeControls(
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types baseType,
#if TARGET_XARCH
        [Values((byte)16, (byte)32, (byte)64)] byte size,
#elif TARGET_ARM64
        [Values((byte)8, (byte)16)] byte size,
#else
        [Values((byte)16)] byte size,
#endif
        [Values(false, true)] bool negativeZero)
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var value = compiler.gtNewVconNode(type);
            var bits = (baseType, negativeZero) switch
            {
                (TYP_FLOAT, true) => 0x8000_0000UL,
                (TYP_FLOAT, false) => 0x7FC0_1234UL,
                (TYP_DOUBLE, true) => 0x8000_0000_0000_0000UL,
                _ => 0x7FF8_0000_0000_1234UL,
            };
            if (baseType == TYP_FLOAT)
            {
                value.SimdVal.u32[0] = (uint)bits;
            }
            else
            {
                value.SimdVal.u64[0] = bits;
            }

            foreach (var mode in new[] {
                FloatRoundingMode.ToPositiveInfinity,
                FloatRoundingMode.ToNegativeInfinity,
                FloatRoundingMode.ToNearestInteger,
            })
            {
                var result = mode switch
                {
                    FloatRoundingMode.ToPositiveInfinity => compiler.gtNewSimdCeilNode(type, value, baseType, size),
                    FloatRoundingMode.ToNegativeInfinity => compiler.gtNewSimdFloorNode(type, value, baseType, size),
                    _ => compiler.gtNewSimdRoundNode(type, value, baseType, size),
                };
                var intrinsic = result.AsHWIntrinsic();
                Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(ExpectedRounding(mode, baseType, size)));
                Assert.That(intrinsic.Type, Is.EqualTo(type));
                Assert.That(intrinsic.SimdBaseType, Is.EqualTo(baseType));
                Assert.That(intrinsic.SimdSize, Is.EqualTo(size));
                Assert.That(intrinsic.GetOp(1), Is.SameAs(value));
#if TARGET_XARCH
                if (size == 64)
                {
                    Assert.That((int)intrinsic.GetOp(2).AsIntCon().IconValue, Is.EqualTo((int)mode));
                }
#endif
                Assert.That(baseType == TYP_FLOAT ? value.SimdVal.u32[0] : value.SimdVal.u64[0],
                    Is.EqualTo(bits));
            }
        });
    }

    [Test]
    public static void FmaPreservesNativeOperandOrder(
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types baseType,
#if TARGET_XARCH
        [Values((byte)16, (byte)32, (byte)64)] byte size)
#elif TARGET_ARM64
        [Values((byte)8, (byte)16)] byte size)
#else
        [Values((byte)16)] byte size)
#endif
    {
        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var left = compiler.gtNewVconNode(type);
            var right = compiler.gtNewVconNode(type);
            var addend = compiler.gtNewVconNode(type);
#if TARGET_WASM
            _ = Assert.Throws<FatalJitException>(
                () => compiler.gtNewSimdFmaNode(type, left, right, addend, baseType, size));
#else
            var result = compiler.gtNewSimdFmaNode(type, left, right, addend, baseType, size).AsHWIntrinsic();
#if TARGET_XARCH
            var expected = size == 64 ? NI_AVX512_FusedMultiplyAdd : NI_AVX2_MultiplyAdd;
            Assert.That(result.GetOp(1), Is.SameAs(left));
            Assert.That(result.GetOp(3), Is.SameAs(addend));
#else
            var expected = (baseType, size) switch
            {
                (TYP_DOUBLE, 8) => NI_AdvSimd_FusedMultiplyAddScalar,
                (TYP_DOUBLE, 16) => NI_AdvSimd_Arm64_FusedMultiplyAdd,
                _ => NI_AdvSimd_FusedMultiplyAdd,
            };
            Assert.That(result.GetOp(1), Is.SameAs(addend));
            Assert.That(result.GetOp(3), Is.SameAs(left));
#endif
            Assert.That(result.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(result.GetOp(2), Is.SameAs(right));
            Assert.That(result.Type, Is.EqualTo(type));
            Assert.That(result.SimdBaseType, Is.EqualTo(baseType));
            Assert.That(result.SimdSize, Is.EqualTo(size));
#endif
        });
    }

#if TARGET_ARM64 || TARGET_WASM
    [Test]
    public static void ConversionPreservesFloatingSourceMetadataAndDelegation(
#if TARGET_ARM64
        [Values(TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG)] var_types target,
        [Values((byte)8, (byte)16)] byte size,
#else
        [Values(TYP_INT, TYP_UINT)] var_types target,
        [Values((byte)16)] byte size,
#endif
        [Values(false, true)] bool native)
    {
        WithCompiler(compiler =>
        {
            var source = target is TYP_LONG or TYP_ULONG ? TYP_DOUBLE : TYP_FLOAT;
            var type = Compiler.GetSimdTypeForSize(size);
            var value = compiler.gtNewVconNode(type);
            value.SimdVal.u64[0] = 0x8000_0000_7FC0_1234UL;
            var result = native
                ? compiler.gtNewSimdCvtNativeNode(type, value, target, source, size)
                : compiler.gtNewSimdCvtNode(type, value, target, source, size);
#if TARGET_ARM64
            var expected = (target, size) switch
            {
                (TYP_INT, _) => NI_AdvSimd_ConvertToInt32RoundToZero,
                (TYP_UINT, _) => NI_AdvSimd_ConvertToUInt32RoundToZero,
                (TYP_LONG, 8) => NI_AdvSimd_Arm64_ConvertToInt64RoundToZeroScalar,
                (TYP_LONG, 16) => NI_AdvSimd_Arm64_ConvertToInt64RoundToZero,
                (TYP_ULONG, 8) => NI_AdvSimd_Arm64_ConvertToUInt64RoundToZeroScalar,
                _ => NI_AdvSimd_Arm64_ConvertToUInt64RoundToZero,
            };
#else
            var expected = target == TYP_INT ? NI_PackedSimd_ConvertToInt32Saturate
                : NI_PackedSimd_ConvertToUInt32Saturate;
#endif
            var intrinsic = result.AsHWIntrinsic();
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(intrinsic.Type, Is.EqualTo(type));
            Assert.That(intrinsic.SimdBaseType, Is.EqualTo(source));
            Assert.That(intrinsic.SimdSize, Is.EqualTo(size));
            Assert.That(intrinsic.GetOp(1), Is.SameAs(value));
            Assert.That(value.SimdVal.u64[0], Is.EqualTo(0x8000_0000_7FC0_1234UL));
        });
    }
#endif

#if TARGET_WASM
    [Test]
    public static void UnsupportedWasmLongConversionsTerminate(
        [Values(TYP_LONG, TYP_ULONG)] var_types target,
        [Values(false, true)] bool native)
    {
        WithCompiler(compiler =>
        {
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            _ = Assert.Throws<FatalJitException>(() => _ = native
                ? compiler.gtNewSimdCvtNativeNode(TYP_SIMD16, value, target, TYP_DOUBLE, 16)
                : compiler.gtNewSimdCvtNode(TYP_SIMD16, value, target, TYP_DOUBLE, 16));
        });
    }
#endif

#if TARGET_ARM64
    [TestCase(SimdScalableRepeated)]
    [TestCase(SimdScalableSequence)]
    [TestCase(SimdScalableScalar)]
    public static void ScalableWrapperPreservesStoredRepresentation(SimdScalableKind kind)
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var value = new simdscalable_t { BaseType = TYP_UINT, Kind = kind };
            value.Index.u64[0] = 0x8000_0001;
            value.Step.u64[0] = kind == SimdScalableSequence ? 3UL : 0UL;
            var operand = store.VNForSimdScalableCon(value);
            var result = store.GetConstantSimdScalable(TYP_DOUBLE, operand);

            Assert.That(result.BaseType, Is.EqualTo(value.BaseType));
            Assert.That(result.Kind, Is.EqualTo(kind));
            Assert.That(result.Index.u64[0], Is.EqualTo(value.Index.u64[0]));
            Assert.That(result.Step.u64[0], Is.EqualTo(value.Step.u64[0]));
            Assert.That(result, Is.EqualTo(store.GetConstantSimdScalable(operand)));
        });
    }

    [Test]
    public static void ScalableWrapperBroadcastsIntegralRawBits(
        [Values(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG)]
        var_types baseType,
        [Values(-1L, 0L, 0x1_8000_0001L)] long value)
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var operand = baseType.Size == 8 ? store.VNForLongCon(value)
                : store.VNForIntCon(unchecked((int)value));
            var expected = unchecked((ulong)value);
            if (baseType.Size != 8)
            {
                expected &= (1UL << (baseType.Size * 8)) - 1;
            }
            var result = store.GetConstantSimdScalable(baseType, operand);

            Assert.That(result.BaseType, Is.EqualTo(baseType));
            Assert.That(result.Kind, Is.EqualTo(SimdScalableRepeated));
            Assert.That(result.Index.u64[0], Is.EqualTo(expected));
            Assert.That(result.Step.u64[0], Is.Zero);
        });
    }

    [TestCase(TYP_FLOAT, 0x8000_0000UL)]
    [TestCase(TYP_FLOAT, 0x7FC0_1234UL)]
    [TestCase(TYP_FLOAT, 0x3FC0_0000UL)]
    [TestCase(TYP_DOUBLE, 0x8000_0000_0000_0000UL)]
    [TestCase(TYP_DOUBLE, 0x7FF8_0000_0000_1234UL)]
    [TestCase(TYP_DOUBLE, 0x3FF8_0000_0000_0000UL)]
    public static void ScalableWrapperBroadcastsFloatingRawBits(var_types baseType, ulong bits)
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var operand = baseType == TYP_FLOAT
                ? store.VNForFloatCon(BitConverter.Int32BitsToSingle(unchecked((int)bits)))
                : store.VNForDoubleCon(BitConverter.Int64BitsToDouble(unchecked((long)bits)));
            var result = store.GetConstantSimdScalable(baseType, operand);

            Assert.That(result.BaseType, Is.EqualTo(baseType));
            Assert.That(result.Kind, Is.EqualTo(SimdScalableRepeated));
            Assert.That(result.Index.u64[0], Is.EqualTo(bits));
            Assert.That(result.Step.u64[0], Is.Zero);
        });
    }
#endif

    private static NamedIntrinsic ExpectedRounding(FloatRoundingMode mode, var_types baseType, byte size)
    {
#if TARGET_XARCH
        if (size == 64)
        {
            return NI_AVX512_RoundScale;
        }

        return (mode, size) switch
        {
            (FloatRoundingMode.ToPositiveInfinity, 32) => NI_AVX_Ceiling,
            (FloatRoundingMode.ToNegativeInfinity, 32) => NI_AVX_Floor,
            (FloatRoundingMode.ToNearestInteger, 32) => NI_AVX_RoundToNearestInteger,
            (FloatRoundingMode.ToPositiveInfinity, _) => NI_X86Base_Ceiling,
            (FloatRoundingMode.ToNegativeInfinity, _) => NI_X86Base_Floor,
            _ => NI_X86Base_RoundToNearestInteger,
        };
#elif TARGET_ARM64
        return (mode, baseType, size) switch
        {
            (FloatRoundingMode.ToPositiveInfinity, TYP_DOUBLE, 8) => NI_AdvSimd_CeilingScalar,
            (FloatRoundingMode.ToPositiveInfinity, TYP_DOUBLE, 16) => NI_AdvSimd_Arm64_Ceiling,
            (FloatRoundingMode.ToPositiveInfinity, _, _) => NI_AdvSimd_Ceiling,
            (FloatRoundingMode.ToNegativeInfinity, TYP_DOUBLE, 8) => NI_AdvSimd_FloorScalar,
            (FloatRoundingMode.ToNegativeInfinity, TYP_DOUBLE, 16) => NI_AdvSimd_Arm64_Floor,
            (FloatRoundingMode.ToNegativeInfinity, _, _) => NI_AdvSimd_Floor,
            (FloatRoundingMode.ToNearestInteger, TYP_DOUBLE, 8) => NI_AdvSimd_RoundToNearestScalar,
            (FloatRoundingMode.ToNearestInteger, TYP_DOUBLE, 16) => NI_AdvSimd_Arm64_RoundToNearest,
            _ => NI_AdvSimd_RoundToNearest,
        };
#else
        return mode switch
        {
            FloatRoundingMode.ToPositiveInfinity => NI_PackedSimd_Ceiling,
            FloatRoundingMode.ToNegativeInfinity => NI_PackedSimd_Floor,
            _ => NI_PackedSimd_RoundToNearest,
        };
#endif
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
        JitTls.Compiler = compiler;
        try
        {
#if TARGET_XARCH
            foreach (var instructionSet in new[] { InstructionSet_AVX2, InstructionSet_AVX512 })
            {
                compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
                compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
            }
#endif
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
