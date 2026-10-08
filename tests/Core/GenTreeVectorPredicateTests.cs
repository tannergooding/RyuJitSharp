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
        var node = new GenTreeVecCon(TYP_SIMD);

        Assert.That(node.IsZero, Is.True);
        Assert.That(node.IsAllBitsSet, Is.False);

        node.SimdScalableVal = simdscalable_t.AllBitsSet;

        Assert.That(node.IsZero, Is.False);
        Assert.That(node.IsAllBitsSet, Is.True);
    }
#endif
}
#endif
