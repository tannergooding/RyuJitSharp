// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_SIMD
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class GenTreeVectorPredicateTests
{
    private static Compiler? s_previousCompiler;
#if DEBUG
    private static JitTls? s_jitTls;
#endif

    [SetUp]
    public static unsafe void SetUp()
    {
        s_previousCompiler = JitTls.Compiler;
#if DEBUG
        s_jitTls = new JitTls(null);
#endif
        JitTls.Compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
    }

    [TearDown]
    public static void TearDown()
    {
        JitTls.Compiler = s_previousCompiler;
#if DEBUG
        s_jitTls?.Dispose();
        s_jitTls = null;
#endif
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
#if TARGET_XARCH
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
#endif
    public static void FixedPredicatesIgnoreInactiveBytes(var_types type)
    {
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
    public static void SimdPredicateHelpersPreserveNaNAndSignedZeroSemantics()
    {
        var value = default(simd64_t);
        value.u32[0] = 0x80000000;
        value.u32[1] = 0;
        value.u32[2] = 0xFFC00001;
        value.u32[3] = 0x3F800000;
        var input = value.AsSpan<byte>();

        var negative = Globals.EvaluateSimdIsNegative(TYP_FLOAT, input, 16);
        var zero = Globals.EvaluateSimdIsZero(TYP_FLOAT, input, 16);
        var negativeZero = Globals.EvaluateSimdIsNegativeZero(TYP_FLOAT, input, 16);
        var positiveZero = Globals.EvaluateSimdIsPositiveZero(TYP_FLOAT, input, 16);

        Assert.That(negative.u32[0], Is.EqualTo(uint.MaxValue));
        Assert.That(negative.u32[1], Is.Zero);
        Assert.That(negative.u32[2], Is.EqualTo(uint.MaxValue));
        Assert.That(negative.u32[3], Is.Zero);

        Assert.That(zero.u32[0], Is.EqualTo(uint.MaxValue));
        Assert.That(zero.u32[1], Is.EqualTo(uint.MaxValue));
        Assert.That(zero.u32[2], Is.Zero);
        Assert.That(zero.u32[3], Is.Zero);

        Assert.That(negativeZero.u32[0], Is.EqualTo(uint.MaxValue));
        Assert.That(negativeZero.u32[1], Is.Zero);
        Assert.That(positiveZero.u32[0], Is.Zero);
        Assert.That(positiveZero.u32[1], Is.EqualTo(uint.MaxValue));

        Assert.That(Globals.GetSimdIntegralBaseType(TYP_FLOAT), Is.EqualTo(TYP_INT));
        Assert.That(Globals.GetSimdIntegralBaseType(TYP_DOUBLE), Is.EqualTo(TYP_LONG));
        Assert.That(Globals.GetSimdIntegralBaseType(TYP_UINT), Is.EqualTo(TYP_UINT));
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void SimdIsNaNReturnsPerLaneNotEqualMask(var_types baseType)
    {
        var value = default(simd64_t);
        if (baseType is TYP_FLOAT)
        {
            value.f32[0] = float.NaN;
            value.f32[1] = 0;
            value.f32[2] = 1;
            value.f32[3] = float.NaN;
            value.f32[4] = float.NaN;

            var result = Globals.EvaluateSimdIsNaN(baseType, value.AsSpan<byte>(), 16);
            Assert.That(result.u32[0], Is.EqualTo(uint.MaxValue));
            Assert.That(result.u32[1], Is.Zero);
            Assert.That(result.u32[2], Is.Zero);
            Assert.That(result.u32[3], Is.EqualTo(uint.MaxValue));
            Assert.That(result.u32[4], Is.Zero);
        }
        else
        {
            value.f64[0] = double.NaN;
            value.f64[1] = -0.0;
            value.f64[2] = double.NaN;

            var result = Globals.EvaluateSimdIsNaN(baseType, value.AsSpan<byte>(), 16);
            Assert.That(result.u64[0], Is.EqualTo(ulong.MaxValue));
            Assert.That(result.u64[1], Is.Zero);
            Assert.That(result.u64[2], Is.Zero);
        }
    }

    [Test]
    public static void SimdAllBitsSetPredicatesCompareFloatingPayloadsAsIntegers()
    {
        var value = default(simd64_t);
        value.u32[4] = uint.MaxValue;

        Assert.That(Globals.EvaluateSimdAnyWhereAllBitsSet(TYP_FLOAT, value.AsSpan<byte>(), 16), Is.False);

        value.u32[1] = uint.MaxValue;

        Assert.That(Globals.EvaluateSimdAnyWhereAllBitsSet(TYP_FLOAT, value.AsSpan<byte>(), 16), Is.True);
        Assert.That(Globals.EvaluateSimdAllWhereAllBitsSet(TYP_FLOAT, value.AsSpan<byte>(), 16), Is.False);

        value.u32[0] = uint.MaxValue;
        value.u32[2] = uint.MaxValue;
        value.u32[3] = uint.MaxValue;

        Assert.That(Globals.EvaluateSimdAllWhereAllBitsSet(TYP_FLOAT, value.AsSpan<byte>(), 16), Is.True);
        Assert.That(Globals.EvaluateSimdIsNaN(TYP_INT, value.AsSpan<byte>(), 16).IsZero, Is.True);
        Assert.That(Globals.EvaluateSimdIsNegative(TYP_UINT, value.AsSpan<byte>(), 16).IsZero, Is.True);
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

#if TARGET_ARM64
    [Test]
    public static void ScalablePredicatesReadScalableStorage()
    {
        var node = new GenTreeVecCon(TYP_SIMD_ARM64);

        Assert.That(node.IsZero, Is.True);
        Assert.That(node.IsAllBitsSet, Is.False);

        node.SimdScalableVal = simdscalable_t.AllBitsSet;

        Assert.That(node.IsZero, Is.False);
        Assert.That(node.IsAllBitsSet, Is.True);
    }
#endif
}
#endif
