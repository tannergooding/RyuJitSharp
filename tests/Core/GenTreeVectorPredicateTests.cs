// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class GenTreeVectorPredicateTests
{
    private static Compiler? s_previousCompiler;
    private static IDisposable? s_jitTls;
    private static bool s_tlsInitialized;

    [SetUp]
    public static unsafe void SetUp()
    {
        Assume.That(Configuration.FEATURE_SIMD, "This fixture requires SIMD support.");
        s_previousCompiler = JitTls.Compiler;
        s_jitTls = TestConfiguration.CreateJitTls();
        s_tlsInitialized = true;
        JitTls.Compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
    }

    [TearDown]
    public static void TearDown()
    {
        if (!s_tlsInitialized)
        {
            return;
        }

        JitTls.Compiler = s_previousCompiler;
        s_jitTls?.Dispose();
        s_jitTls = null;
        s_tlsInitialized = false;
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void FixedPredicatesIgnoreInactiveBytes(var_types type)
    {
        if (type is TYP_SIMD32 or TYP_SIMD64)
        {
            Assume.That(Configuration.TARGET_XARCH, "The vector width requires an x86-family target.");
        }

        var node = new GenTreeVecCon(type);
        node.SimdVal.AsSpan<byte>().Fill(byte.MaxValue);
        node.SimdVal.AsSpan<byte>()[..type.Size].Clear();

        Assert.That(node.IsZero, Is.True);
        Assert.That(node.IsAllBitsSet, Is.False);

        node.SimdVal.AsSpan<byte>().Clear();
        node.SimdVal.AsSpan<byte>()[..type.Size].Fill(byte.MaxValue);

        Assert.That(node.IsZero, Is.False);
        Assert.That(node.IsAllBitsSet, Is.True);
    }

    [Test]
    public static void SimdConstantsPreserveNaNAndSignedZeroSemantics()
    {
        var node = new GenTreeVecCon(TYP_SIMD16);
        node.SimdVal.u32[0] = 0x80000000;
        node.SimdVal.u32[1] = 0;
        node.SimdVal.u32[2] = 0xFFC00001;
        node.SimdVal.u32[3] = 0x3F800000;

        Assert.That(node.ContainsNaN(TYP_FLOAT), Is.True);
        Assert.That(node.ContainsNegativeZero(TYP_FLOAT), Is.True);
        Assert.That(node.ContainsPositiveZero(TYP_FLOAT), Is.True);

        var negativeZeroMask = node.GetFloatingZeroMask(TYP_FLOAT, true);
        var positiveZeroMask = node.GetFloatingZeroMask(TYP_FLOAT, false);
        Assert.That(negativeZeroMask.u32[0], Is.EqualTo(uint.MaxValue));
        Assert.That(negativeZeroMask.u32[1], Is.Zero);
        Assert.That(positiveZeroMask.u32[0], Is.Zero);
        Assert.That(positiveZeroMask.u32[1], Is.EqualTo(uint.MaxValue));
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void SimdConstantsIdentifyNaNsAcrossElements(var_types baseType)
    {
        var node = new GenTreeVecCon(TYP_SIMD16);
        if (baseType is TYP_FLOAT)
        {
            node.SimdVal.f32[0] = float.NaN;
            node.SimdVal.f32[1] = 0;
            node.SimdVal.f32[2] = 1;
            node.SimdVal.f32[3] = float.NaN;

            Assert.That(node.ContainsNaN(baseType), Is.True);
            Assert.That(node.IsNaN(baseType), Is.False);

            node.SimdVal.f32[1] = float.NaN;
            node.SimdVal.f32[2] = float.NaN;
            Assert.That(node.IsNaN(baseType), Is.True);
        }
        else
        {
            node.SimdVal.f64[0] = double.NaN;
            node.SimdVal.f64[1] = -0.0;

            Assert.That(node.ContainsNaN(baseType), Is.True);
            Assert.That(node.IsNaN(baseType), Is.False);

            node.SimdVal.f64[1] = double.NaN;
            Assert.That(node.IsNaN(baseType), Is.True);
        }
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_STRUCT)]
    public static void InvalidTypesTerminateBothPredicates(var_types type)
    {
        var node = new GenTreeVecCon(TYP_SIMD16) { Type = type };

        var zeroFailure = Assert.Throws<FatalJitException>(() => Assert.That(node.IsZero, Is.True));
        var allBitsSetFailure = Assert.Throws<FatalJitException>(() => Assert.That(node.IsAllBitsSet, Is.True));

        Assert.That(zeroFailure?.Result, Is.EqualTo(CORJIT_IMPLLIMITATION));
        Assert.That(allBitsSetFailure?.Result, Is.EqualTo(CORJIT_IMPLLIMITATION));
    }

    [Test]
    public static void ScalablePredicatesReadScalableStorage()
    {
        Assume.That(Configuration.TARGET_ARM64, "This case requires the ARM64 target.");
        var node = new GenTreeVecCon(TYP_SIMD_ARM64);

        Assert.That(node.IsZero, Is.True);
        Assert.That(node.IsAllBitsSet, Is.False);

        node.SimdScalableVal = simdscalable_t.AllBitsSet;

        Assert.That(node.IsZero, Is.False);
        Assert.That(node.IsAllBitsSet, Is.True);
    }
}
