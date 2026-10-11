// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class FeatureConfigurationTests
{
    [Test]
    public static void MaskedHardwareIntrinsicsMatchTheTargetArchitecture()
    {
        Assert.That(Configuration.FEATURE_MASKED_HW_INTRINSICS,
            Is.EqualTo(Configuration.TARGET_XARCH || Configuration.TARGET_ARM64));
    }

    [Test]
    public static void MaskTypeAndOperatorMetadataAreAvailableOnEveryTarget()
    {
        var maskOper = genTreeOps.GT_CNS_MSK;
        Assert.That(Globals.varTypeIsMask(var_types.TYP_MASK), Is.EqualTo(Configuration.FEATURE_MASKED_HW_INTRINSICS));
        Assert.That(maskOper.IsCnsMsk, Is.EqualTo(Configuration.FEATURE_MASKED_HW_INTRINSICS));
        Assert.That(maskOper.IsConst, Is.EqualTo(Configuration.FEATURE_MASKED_HW_INTRINSICS));
        Assert.That((int)VNFunc.VNF_CNS_MSK, Is.EqualTo((int)genTreeOps.GT_CNS_MSK));
    }

    [Test]
    public static void MaskConstantConstructionRequiresMaskedHardwareIntrinsics()
    {
        WithCompiler(compiler =>
        {
            if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                _ = new GenTreeMskCon(default);
            }
            else
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() => new GenTreeMskCon(default));
            }
        });
    }

    [Test]
    public static void MaskValueNumberingMatchesTheConfiguredFeature()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() => store.VNZeroForType(var_types.TYP_MASK));
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                {
                    simdmask_t result = default;
                    var input = default(simdmask_t);
                    Globals.EvaluateUnaryMask(genTreeOps.GT_NOT, false, var_types.TYP_BYTE, 64, ref result, input);
                });
                return;
            }

            var value = simdmask_t.AllBitsSet(3);
            var vn = store.VNForSimdMaskCon(value);

            Assert.That(store.TypeOfVN(vn), Is.EqualTo(var_types.TYP_MASK));
            Assert.That(store.GetConstantSimdMask(vn), Is.EqualTo(value));
            Assert.That(store.VNForSimdMaskCon(value), Is.EqualTo(vn));
            Assert.That(store.GetConstantSimdMaskValue(vn).IsScalable, Is.False);

            simdmask_t result = default;
            var allBits = simdmask_t.AllBitsSet(64);
            Globals.EvaluateUnaryMask(genTreeOps.GT_NOT, false, var_types.TYP_BYTE, 64, ref result, allBits);
            Assert.That(result.RawBits, Is.Zero);
        });
    }

    [Test]
    public static void SimdConstantStorageMatchesTheConfiguredTarget()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            if (!Configuration.FEATURE_SIMD)
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() => store.VNForSimd8Con(simd8_t.Zero));
                _ = Assert.Throws<PlatformNotSupportedException>(() => store.VNForSimdType(16, TYP_INT));
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                    store.VNBroadcastForSimdType(TYP_SIMD16, TYP_INT, store.VNForIntCon(0)));

                var input = new byte[16];
                var result = new byte[16];
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                    Globals.EvaluateUnarySimd(genTreeOps.GT_NEG, false, TYP_INT, result, input));
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                    Globals.EvaluateBinarySimd(genTreeOps.GT_ADD, false, TYP_INT, result, input, input, input.Length));
                return;
            }

            var fixedVector = simd8_t.AllBitsSet;
            var fixedVectorVN = store.VNForSimd8Con(fixedVector);
            Assert.That(store.GetConstantSimd8(fixedVectorVN), Is.EqualTo(fixedVector));

            if (Configuration.TARGET_XARCH)
            {
                var wideVector = simd64_t.AllBitsSet;
                var wideVectorVN = store.VNForSimd64Con(wideVector);
                Assert.That(store.GetConstantSimd64(wideVectorVN), Is.EqualTo(wideVector));
            }
            else
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                    store.VNForSimd64Con(simd64_t.AllBitsSet));
            }

            if (Configuration.TARGET_ARM64)
            {
                var scalableVector = simdscalable_t.AllBitsSet;
                var scalableVectorVN = store.VNForSimdScalableCon(scalableVector);
                Assert.That(store.GetConstantSimdScalable(scalableVectorVN), Is.EqualTo(scalableVector));
            }
            else
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                    store.VNForSimdScalableCon(simdscalable_t.AllBitsSet));
            }
        });
    }

    [Test]
    public static void NativeVariableTypeOrdinalsRemainTargetSpecific()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            if (!Configuration.FEATURE_SIMD)
            {
                Assert.That(var_typesExtensions.NativeCount, Is.EqualTo(16));
                AssertNativeOrdinalRoundTrips(store, TYP_INT, TYP_UNKNOWN, TYP_COUNT);
                return;
            }

            if (Configuration.TARGET_XARCH)
            {
                Assert.That(var_typesExtensions.NativeCount, Is.EqualTo(22));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_SIMD32), Is.EqualTo(18));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_SIMD64), Is.EqualTo(19));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_MASK), Is.EqualTo(20));
                AssertNativeOrdinalRoundTrips(store, TYP_INT, TYP_SIMD8, TYP_SIMD12, TYP_SIMD16,
                    TYP_SIMD32, TYP_SIMD64, TYP_MASK, TYP_UNKNOWN, TYP_COUNT);
            }
            else if (Configuration.TARGET_ARM64)
            {
                Assert.That(var_typesExtensions.NativeCount, Is.EqualTo(21));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_SIMD_ARM64), Is.EqualTo(18));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_MASK), Is.EqualTo(19));
                AssertNativeOrdinalRoundTrips(store, TYP_INT, TYP_SIMD8, TYP_SIMD12, TYP_SIMD16,
                    TYP_SIMD_ARM64, TYP_MASK, TYP_UNKNOWN, TYP_COUNT);
            }
            else
            {
                Assert.That(var_typesExtensions.NativeCount, Is.EqualTo(20));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_MASK), Is.EqualTo(18));
                AssertNativeOrdinalRoundTrips(store, TYP_INT, TYP_SIMD8, TYP_SIMD12, TYP_SIMD16,
                    TYP_MASK, TYP_UNKNOWN, TYP_COUNT);
            }

            var structEncoding = store.EncodeBitCastType(TYP_STRUCT, new ValueSize(3));
            Assert.That(store.GetConstantInt32(structEncoding), Is.EqualTo(var_typesExtensions.NativeCount + 3));
            Assert.That(store.DecodeBitCastType(structEncoding, out var size), Is.EqualTo(TYP_STRUCT));
            Assert.That(size, Is.EqualTo(3U));

            var simdTypeVN = store.VNForSimdType(16, TYP_INT, TYP_MASK);
            var simdTypeApp = new VNFuncApp();
            Assert.That(store.GetVNFunc(simdTypeVN, ref simdTypeApp), Is.True);
            var encodedTypes = store.GetConstantInt32(simdTypeApp.GetArg(1));
            Assert.That(encodedTypes & 0xFF, Is.EqualTo(var_typesExtensions.ToNativeOrdinal(TYP_INT)));
            Assert.That((encodedTypes >> 8) - 1, Is.EqualTo(var_typesExtensions.ToNativeOrdinal(TYP_MASK)));
        });
    }

    private static void AssertNativeOrdinalRoundTrips(ValueNumStore store, params var_types[] types)
    {
        foreach (var type in types)
        {
            var ordinal = var_typesExtensions.ToNativeOrdinal(type);
            Assert.That(var_typesExtensions.FromNativeOrdinal(ordinal), Is.EqualTo(type));

            var castVN = store.VNForCastOper(type, false);
            store.GetCastOperFromVN(castVN, out var castType, out var isUnsigned);
            Assert.That(castType, Is.EqualTo(type));
            Assert.That(isUnsigned, Is.False);
        }
    }

    private static unsafe void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compCodeOpt = Compiler.BLENDED_CODE;
#if DEBUG
        compiler.info.compFullName = nameof(FeatureConfigurationTests);
#endif
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previous;
        }
    }
}
