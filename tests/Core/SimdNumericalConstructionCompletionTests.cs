// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;
using static RyuJitSharp.ManagedInstructionSet;
using static RyuJitSharp.SimdScalableKind;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SimdNumericalConstructionCompletionTests
{
    [SetUp]
    public static void RequireSupportedTarget()
    {
        Assume.That(Configuration.FEATURE_HW_INTRINSICS &&
            (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 || Configuration.TARGET_WASM),
            "This fixture requires a supported hardware-intrinsic target.");
    }

    [TestCase(TYP_BYTE)]
    [TestCase(TYP_UBYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_USHORT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_UINT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_ULONG)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void OneVectorConstantUsesMatchingElementType(var_types baseType)
    {
        WithCompiler(compiler =>
        {
            var value = compiler.gtNewOneConNode(TYP_SIMD16, baseType).AsVecCon();
            var elementCount = 16 / baseType.Size;

            for (var index = 0; index < elementCount; index++)
            {
                if (baseType is TYP_FLOAT or TYP_DOUBLE)
                {
                    Assert.That(value.GetElementFloating(baseType, index), Is.EqualTo(1.0));
                }
                else
                {
                    Assert.That(value.GetElementIntegral(baseType, index), Is.EqualTo(1L));
                }
            }
        });
    }

    [Test]
    public static void RoundingPreservesSourceBitsAndSelectsNativeControls(
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types baseType,
        [Values((byte)8, (byte)16, (byte)32, (byte)64)] byte size,
        [Values(false, true)] bool negativeZero)
    {
        Assume.That(Configuration.TARGET_XARCH
            ? size is 16 or 32 or 64
            : Configuration.TARGET_ARM64
                ? size is 8 or 16
                : size == 16,
            "This test case uses a vector size supported by the target.");

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
                if (Configuration.TARGET_XARCH && size == 64)
                {
                    Assert.That((int)intrinsic.GetOp(2).AsIntCon().IconValue, Is.EqualTo((int)mode));
                }
                Assert.That(baseType == TYP_FLOAT ? value.SimdVal.u32[0] : value.SimdVal.u64[0],
                    Is.EqualTo(bits));
            }
        });
    }

    [TestCase((byte)16, TYP_SIMD16, NI_X86Base_Max, NI_X86Base_Min)]
    [TestCase((byte)32, TYP_SIMD32, NI_AVX2_Max, NI_AVX2_Min)]
    [TestCase((byte)64, TYP_SIMD64, NI_AVX512_Max, NI_AVX512_Min)]
    public static void UnsignedMagnitudeUsesNativeMinMax(
        byte size, var_types simdType, NamedIntrinsic expectedMax, NamedIntrinsic expectedMin)
    {
        Assume.That(Configuration.TARGET_XARCH, "This test requires the xarch target.");

        WithCompiler(compiler =>
        {
            var max = compiler.gtNewSimdMinMaxNode(
                simdType, new GenTreeLclVar(simdType, 0), new GenTreeLclVar(simdType, 1),
                TYP_UBYTE, size, isMax: true, isMagnitude: true, isNumber: false);
            var min = compiler.gtNewSimdMinMaxNode(
                simdType, new GenTreeLclVar(simdType, 0), new GenTreeLclVar(simdType, 1),
                TYP_UBYTE, size, isMax: false, isMagnitude: true, isNumber: false);

            Assert.That(max.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(expectedMax));
            Assert.That(min.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(expectedMin));
        });
    }

    [Test]
    public static void FmaPreservesNativeOperandOrder(
        [Values(TYP_FLOAT, TYP_DOUBLE)] var_types baseType,
        [Values((byte)8, (byte)16, (byte)32, (byte)64)] byte size)
    {
        Assume.That(Configuration.TARGET_XARCH
            ? size is 16 or 32 or 64
            : Configuration.TARGET_ARM64
                ? size is 8 or 16
                : size == 16,
            "This test case uses a vector size supported by the target.");

        WithCompiler(compiler =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var left = compiler.gtNewVconNode(type);
            var right = compiler.gtNewVconNode(type);
            var addend = compiler.gtNewVconNode(type);
            if (Configuration.TARGET_WASM)
            {
                _ = Assert.Throws<FatalJitException>(
                    () => compiler.gtNewSimdFmaNode(type, left, right, addend, baseType, size));
                return;
            }

            var result = compiler.gtNewSimdFmaNode(type, left, right, addend, baseType, size).AsHWIntrinsic();
            NamedIntrinsic expected;
            if (Configuration.TARGET_XARCH)
            {
                expected = size == 64 ? NI_AVX512_FusedMultiplyAdd : NI_AVX2_MultiplyAdd;
                Assert.That(result.GetOp(1), Is.SameAs(left));
                Assert.That(result.GetOp(3), Is.SameAs(addend));
            }
            else
            {
                expected = (baseType, size) switch
                {
                    (TYP_DOUBLE, 8) => NI_AdvSimd_FusedMultiplyAddScalar,
                    (TYP_DOUBLE, 16) => NI_AdvSimd_Arm64_FusedMultiplyAdd,
                    _ => NI_AdvSimd_FusedMultiplyAdd,
                };
                Assert.That(result.GetOp(1), Is.SameAs(addend));
                Assert.That(result.GetOp(3), Is.SameAs(left));
            }

            Assert.That(result.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(result.GetOp(2), Is.SameAs(right));
            Assert.That(result.Type, Is.EqualTo(type));
            Assert.That(result.SimdBaseType, Is.EqualTo(baseType));
            Assert.That(result.SimdSize, Is.EqualTo(size));
        });
    }

    [Test]
    public static void ConversionPreservesFloatingSourceMetadataAndDelegation(
        [Values(TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG)] var_types target,
        [Values((byte)8, (byte)16)] byte size,
        [Values(false, true)] bool native)
    {
        Assume.That(Configuration.TARGET_ARM64 || Configuration.TARGET_WASM,
            "This test requires the ARM64 or WebAssembly target.");
        Assume.That(Configuration.TARGET_ARM64
                ? size is 8 or 16
                : size == 16 && target is TYP_INT or TYP_UINT,
            "This test case uses a conversion supported by the target.");

        WithCompiler(compiler =>
        {
            var source = target is TYP_LONG or TYP_ULONG ? TYP_DOUBLE : TYP_FLOAT;
            var type = Compiler.GetSimdTypeForSize(size);
            var value = compiler.gtNewVconNode(type);
            value.SimdVal.u64[0] = 0x8000_0000_7FC0_1234UL;
            var result = native
                ? compiler.gtNewSimdCvtNativeNode(type, value, target, source, size)
                : compiler.gtNewSimdCvtNode(type, value, target, source, size);
            NamedIntrinsic expected;
            if (Configuration.TARGET_ARM64)
            {
                expected = (target, size) switch
                {
                    (TYP_INT, _) => NI_AdvSimd_ConvertToInt32RoundToZero,
                    (TYP_UINT, _) => NI_AdvSimd_ConvertToUInt32RoundToZero,
                    (TYP_LONG, 8) => NI_AdvSimd_Arm64_ConvertToInt64RoundToZeroScalar,
                    (TYP_LONG, 16) => NI_AdvSimd_Arm64_ConvertToInt64RoundToZero,
                    (TYP_ULONG, 8) => NI_AdvSimd_Arm64_ConvertToUInt64RoundToZeroScalar,
                    _ => NI_AdvSimd_Arm64_ConvertToUInt64RoundToZero,
                };
            }
            else
            {
                expected = target == TYP_INT ? NI_PackedSimd_ConvertToInt32Saturate
                    : NI_PackedSimd_ConvertToUInt32Saturate;
            }

            var intrinsic = result.AsHWIntrinsic();
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(intrinsic.Type, Is.EqualTo(type));
            Assert.That(intrinsic.SimdBaseType, Is.EqualTo(source));
            Assert.That(intrinsic.SimdSize, Is.EqualTo(size));
            Assert.That(intrinsic.GetOp(1), Is.SameAs(value));
            Assert.That(value.SimdVal.u64[0], Is.EqualTo(0x8000_0000_7FC0_1234UL));
        });
    }

    [Test]
    public static void UnsupportedWasmLongConversionsTerminate(
        [Values(TYP_LONG, TYP_ULONG)] var_types target,
        [Values(false, true)] bool native)
    {
        Assume.That(Configuration.TARGET_WASM, "This test requires the WebAssembly target.");

        WithCompiler(compiler =>
        {
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            _ = Assert.Throws<FatalJitException>(() => _ = native
                ? compiler.gtNewSimdCvtNativeNode(TYP_SIMD16, value, target, TYP_DOUBLE, 16)
                : compiler.gtNewSimdCvtNode(TYP_SIMD16, value, target, TYP_DOUBLE, 16));
        });
    }

    [TestCase(SimdScalableRepeated)]
    [TestCase(SimdScalableSequence)]
    [TestCase(SimdScalableScalar)]
    public static void ScalableWrapperPreservesStoredRepresentation(SimdScalableKind kind)
    {
        Assume.That(Configuration.TARGET_ARM64, "This test requires the ARM64 target.");

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
        Assume.That(Configuration.TARGET_ARM64, "This test requires the ARM64 target.");

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
        Assume.That(Configuration.TARGET_ARM64, "This test requires the ARM64 target.");

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

    private static NamedIntrinsic ExpectedRounding(FloatRoundingMode mode, var_types baseType, byte size)
    {
        if (Configuration.TARGET_XARCH)
        {
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
        }

        if (Configuration.TARGET_ARM64)
        {
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
        }

        return mode switch
        {
            FloatRoundingMode.ToPositiveInfinity => NI_PackedSimd_Ceiling,
            FloatRoundingMode.ToNegativeInfinity => NI_PackedSimd_Floor,
            _ => NI_PackedSimd_RoundToNearest,
        };
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
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
            if (Configuration.TARGET_XARCH)
            {
                foreach (var instructionSet in new[] { InstructionSet_AVX2, InstructionSet_AVX512 })
                {
                    compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
                    compiler.opts.compSupportsISAReported.AddInstructionSet(instructionSet);
                    compiler.opts.compSupportsISAExactly.AddInstructionSet(instructionSet);
                }
            }
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
