// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.SimdScalableKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64SimdMetadataTests
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
    public static void VectorQueriesOnlyExamineActiveBytes(var_types type)
    {
        var node = new GenTreeVecCon(type);
        ref var value = ref node.SimdVal;
        value.u64[0] = ulong.MaxValue;
        value.u64[1] = ulong.MaxValue;

        Assert.That(node.IsAllBitsSet, Is.True);
        Assert.That(node.IsZero, Is.False);

        value.u64[0] = 0;
        if (type is TYP_SIMD12 or TYP_SIMD16)
        {
            value.u32[2] = 0;
        }
        if (type is TYP_SIMD16)
        {
            value.u32[3] = 0;
        }

        Assert.That(node.IsZero, Is.True);
        Assert.That(node.IsAllBitsSet, Is.False);
    }

    [Test]
    public static void Simd16QueriesIncludeBothHalves()
    {
        var node = new GenTreeVecCon(TYP_SIMD16);
        ref var value = ref node.SimdVal;
        value.u64[0] = ulong.MaxValue;

        Assert.That(node.IsAllBitsSet, Is.False);
        Assert.That(node.IsZero, Is.False);

        value.u64[0] = 0;
        value.u64[1] = 1;

        Assert.That(node.IsZero, Is.False);
    }

    [Test]
    public static void Simd12IgnoresInactiveFourthWord()
    {
        var node = new GenTreeVecCon(TYP_SIMD12);
        ref var value = ref node.SimdVal;
        value.u32[3] = uint.MaxValue;

        Assert.That(node.IsZero, Is.True);

        value.u64[0] = ulong.MaxValue;
        value.u32[2] = uint.MaxValue;

        Assert.That(node.IsAllBitsSet, Is.True);
    }

    [Test]
    public static void ScalableVectorQueriesDoNotUseFixedSimd16Bits()
    {
        var node = new GenTreeVecCon(TYP_SIMD);

        Assert.That(node.IsZero, Is.True);
        Assert.That(node.IsAllBitsSet, Is.False);
        node.SimdScalableVal = simdscalable_t.AllBitsSet;
        Assert.That(node.IsZero, Is.False);
        Assert.That(node.IsAllBitsSet, Is.True);
        _ = Assert.Throws<FatalJitException>(() => _ = node.SimdVal);
    }

    [TestCase(TYP_BYTE, 0xFFUL)]
    [TestCase(TYP_SHORT, 0xFFFFUL)]
    [TestCase(TYP_INT, 0xFFFF_FFFFUL)]
    [TestCase(TYP_ULONG, ulong.MaxValue)]
    public static void ScalableConstructionMasksBothOperandsAndClonePreservesThePayload(var_types baseType, ulong mask)
    {
        var compiler = JitTls.Compiler ?? throw new AssertionException("The fixture compiler is not initialized.");
        var node = compiler.gtNewSimdVconNode(
            TYP_SIMD, baseType, SimdScalableKind.SimdScalableSequence, ulong.MaxValue, ulong.MaxValue - 1);
        var clone = compiler.gtCloneCnsVec(node);

        Assert.That(node.SimdScalableVal.Index.u64[0], Is.EqualTo(mask));
        Assert.That(node.SimdScalableVal.Step.u64[0], Is.EqualTo(mask - 1));
        Assert.That(GenTreeVecCon.Equals(node, clone), Is.True);
        clone.SimdScalableVal.Step.u64[0] = 0;
        Assert.That(GenTreeVecCon.Equals(node, clone), Is.False);
        Assert.That(node.SimdScalableVal.Step.u64[0], Is.EqualTo(mask - 1));
    }

    [Test]
    public static void ScalableConstructionFromValueUsesTheScalarOperands()
    {
        var compiler = JitTls.Compiler ?? throw new AssertionException("The fixture compiler is not initialized.");
        var value = new simdscalable_t
        {
            BaseType = TYP_BYTE,
            Kind = SimdScalableKind.SimdScalableSequence,
        };
        value.Index.u64[0] = 0x1FF;
        value.Step.u64[0] = 0x100;

        var node = compiler.gtNewSimdVconNode(TYP_SIMD, in value);

        Assert.That(node.SimdScalableVal.BaseType, Is.EqualTo(TYP_BYTE));
        Assert.That(node.SimdScalableVal.Kind, Is.EqualTo(SimdScalableKind.SimdScalableSequence));
        Assert.That(node.SimdScalableVal.Index.u64[0], Is.EqualTo(0xFFUL));
        Assert.That(node.SimdScalableVal.Step.u64[0], Is.Zero);
    }

    [TestCase((byte)0)]
    [TestCase((byte)0x3C)]
    [TestCase(byte.MaxValue)]
    public static void ScalableConstantFactoriesUseRepeatedBytePatterns(byte pattern)
    {
        var compiler = JitTls.Compiler ?? throw new AssertionException("The fixture compiler is not initialized.");
        var constant = compiler.gtNewConWithPattern(TYP_SIMD, pattern)
            ?? throw new AssertionException("The scalable pattern factory returned no constant.");
        var node = constant.AsVecCon();

        Assert.That(node.SimdScalableVal.BaseType, Is.EqualTo(TYP_BYTE));
        Assert.That(node.SimdScalableVal.Kind, Is.EqualTo(SimdScalableKind.SimdScalableRepeated));
        Assert.That(node.SimdScalableVal.Index.u64[0], Is.EqualTo(pattern));
        Assert.That(node.SimdScalableVal.Step.u64[0], Is.Zero);
        Assert.That(compiler.gtNewZeroConNode(TYP_SIMD).AsVecCon().IsZero, Is.True);
        Assert.That(compiler.gtNewAllBitsSetConNode(TYP_SIMD).AsVecCon().IsAllBitsSet, Is.True);
    }

    [TestCase(nameof(GenTreeVecCon.EvaluateBinaryInPlace))]
    [TestCase(nameof(GenTreeVecCon.EvaluateBroadcastInPlace))]
    [TestCase(nameof(GenTreeVecCon.GetElementIntegral))]
    [TestCase(nameof(GenTreeVecCon.GetElementFloating))]
    [TestCase(nameof(GenTreeVecCon.SetElementIntegral))]
    [TestCase(nameof(GenTreeVecCon.SetElementFloating))]
    [TestCase(nameof(GenTreeVecCon.IsBroadcast))]
    [TestCase(nameof(GenTreeVecCon.IsNaN))]
    [TestCase(nameof(GenTreeVecCon.IsNegativeZero))]
    [TestCase(nameof(GenTreeVecCon.ContainsNaN))]
    [TestCase(nameof(GenTreeVecCon.ContainsPositiveZero))]
    [TestCase(nameof(GenTreeVecCon.ContainsNegativeZero))]
    [TestCase(nameof(GenTreeVecCon.GetFloatingZeroMask))]
    public static void UnportedScalableConsumersFailBeforeReadingFixedWidthStorage(string name)
    {
        var node = new GenTreeVecCon(TYP_SIMD);
        TestDelegate operation = name switch {
            nameof(GenTreeVecCon.EvaluateBinaryInPlace) => () => node.EvaluateBinaryInPlace(genTreeOps.GT_ADD, false, TYP_FLOAT, node),
            nameof(GenTreeVecCon.EvaluateBroadcastInPlace) => () => node.EvaluateBroadcastInPlace(TYP_FLOAT, 1.0),
            nameof(GenTreeVecCon.GetElementIntegral) => () => _ = node.GetElementIntegral(TYP_INT, 0),
            nameof(GenTreeVecCon.GetElementFloating) => () => _ = node.GetElementFloating(TYP_FLOAT, 0),
            nameof(GenTreeVecCon.SetElementIntegral) => () => node.SetElementIntegral(TYP_INT, 0, 1),
            nameof(GenTreeVecCon.SetElementFloating) => () => node.SetElementFloating(TYP_FLOAT, 0, 1.0),
            nameof(GenTreeVecCon.IsBroadcast) => () => _ = node.IsBroadcast(TYP_FLOAT),
            nameof(GenTreeVecCon.IsNaN) => () => _ = node.IsNaN(TYP_FLOAT),
            nameof(GenTreeVecCon.IsNegativeZero) => () => _ = node.IsNegativeZero(TYP_FLOAT),
            nameof(GenTreeVecCon.ContainsNaN) => () => _ = node.ContainsNaN(TYP_FLOAT),
            nameof(GenTreeVecCon.ContainsPositiveZero) => () => _ = node.ContainsPositiveZero(TYP_FLOAT),
            nameof(GenTreeVecCon.ContainsNegativeZero) => () => _ = node.ContainsNegativeZero(TYP_FLOAT),
            nameof(GenTreeVecCon.GetFloatingZeroMask) => () => _ = node.GetFloatingZeroMask(TYP_FLOAT, false),
            _ => throw new AssertionException("Unknown scalable consumer."),
        };

        _ = Assert.Throws<FatalJitException>(operation);
    }

    [TestCase(TYP_BYTE, SimdScalableKind.SimdScalableRepeated, 0xFFUL, 0UL, 3, 0xFFUL)]
    [TestCase(TYP_BYTE, SimdScalableKind.SimdScalableSequence, 0xFFUL, 1UL, 1, 0x100UL)]
    [TestCase(TYP_SHORT, SimdScalableKind.SimdScalableSequence, 0xFFFFUL, 1UL, 2, 0x10001UL)]
    [TestCase(TYP_INT, SimdScalableKind.SimdScalableSequence, 0xFFFF_FFFFUL, 1UL, 2, 0x1_0000_0001UL)]
    [TestCase(TYP_LONG, SimdScalableKind.SimdScalableSequence, ulong.MaxValue, 1UL, 2, 1UL)]
    [TestCase(TYP_ULONG, SimdScalableKind.SimdScalableSequence, 1UL, ulong.MaxValue, 2, ulong.MaxValue)]
    [TestCase(TYP_FLOAT, SimdScalableKind.SimdScalableRepeated, 0x8000_0000UL, 0UL, 1, 0x8000_0000UL)]
    [TestCase(TYP_DOUBLE, SimdScalableKind.SimdScalableRepeated, 0x7FF8_0000_0000_1234UL, 0UL, 1, 0x7FF8_0000_0000_1234UL)]
    [TestCase(TYP_INT, SimdScalableKind.SimdScalableScalar, 0xFFFF_FFFFUL, 0UL, 0, 0xFFFF_FFFFUL)]
    [TestCase(TYP_INT, SimdScalableKind.SimdScalableScalar, 0xFFFF_FFFFUL, 0UL, 1, 0UL)]
    public static void IntegralScalableElementLookupPreservesNativeRaw64BitArithmetic(
        var_types baseType, SimdScalableKind kind, ulong indexValue, ulong step, int elementIndex, ulong expected)
    {
        var compiler = JitTls.Compiler ?? throw new AssertionException("The fixture compiler is not initialized.");
        var node = compiler.gtNewSimdVconNode(TYP_SIMD, baseType, kind, indexValue, step);

        Assert.That(node.GetIntegralVectorConstElement(elementIndex, baseType), Is.EqualTo(expected));
    }

    [Test]
    public static void FixedIntegralVectorLookupStillSignExtendsSignedElements()
    {
        var node = new GenTreeVecCon(TYP_SIMD16);
        node.SimdVal.u64[0] = ulong.MaxValue;

        Assert.That(node.GetIntegralVectorConstElement(0, TYP_BYTE), Is.EqualTo(ulong.MaxValue));
        Assert.That(node.GetIntegralVectorConstElement(0, TYP_SHORT), Is.EqualTo(ulong.MaxValue));
        Assert.That(node.GetIntegralVectorConstElement(0, TYP_INT), Is.EqualTo(ulong.MaxValue));
        Assert.That(node.GetIntegralVectorConstElement(0, TYP_UBYTE), Is.EqualTo(0xFFUL));
        Assert.That(node.GetIntegralVectorConstElement(0, TYP_USHORT), Is.EqualTo(0xFFFFUL));
        Assert.That(node.GetIntegralVectorConstElement(0, TYP_UINT), Is.EqualTo(0xFFFF_FFFFUL));
    }

    [TestCase(GT_NEG, false, TYP_BYTE, SimdScalableRepeated, 0x80UL, 0UL, SimdScalableRepeated, 0x80UL, 0UL)]
    [TestCase(GT_NOT, false, TYP_UBYTE, SimdScalableRepeated, 0xA5UL, 0UL, SimdScalableRepeated, 0x5AUL, 0UL)]
    [TestCase(GT_NEG, false, TYP_SHORT, SimdScalableRepeated, 0x8000UL, 0UL, SimdScalableRepeated, 0x8000UL, 0UL)]
    [TestCase(GT_NOT, false, TYP_USHORT, SimdScalableRepeated, 0x1234UL, 0UL, SimdScalableRepeated, 0xEDCBUL, 0UL)]
    [TestCase(GT_NEG, false, TYP_INT, SimdScalableSequence, 1UL, 2UL, SimdScalableSequence, 0xFFFF_FFFFUL, 0xFFFF_FFFEUL)]
    [TestCase(GT_NOT, false, TYP_UINT, SimdScalableSequence, 1UL, 2UL, SimdScalableSequence, 0xFFFF_FFFEUL, 0xFFFF_FFFEUL)]
    [TestCase(GT_NEG, false, TYP_LONG, SimdScalableSequence, 1UL, 2UL, SimdScalableSequence, ulong.MaxValue, ulong.MaxValue - 1)]
    [TestCase(GT_LZCNT, false, TYP_ULONG, SimdScalableRepeated, 1UL, 0UL, SimdScalableRepeated, 63UL, 0UL)]
    [TestCase(GT_LZCNT, false, TYP_INT, SimdScalableSequence, 0x10UL, 0UL, SimdScalableRepeated, 27UL, 0UL)]
    [TestCase(GT_LZCNT, false, TYP_INT, SimdScalableSequence, 0UL, 0UL, SimdScalableRepeated, 32UL, 0UL)]
    [TestCase(GT_NEG, false, TYP_INT, SimdScalableScalar, 1UL, 0UL, SimdScalableScalar, 0xFFFF_FFFFUL, 0UL)]
    [TestCase(GT_NOT, false, TYP_INT, SimdScalableScalar, 0UL, 0UL, SimdScalableRepeated, 0xFFFF_FFFFUL, 0UL)]
    [TestCase(GT_NEG, true, TYP_FLOAT, SimdScalableRepeated, 0x3F80_0000UL, 0UL, SimdScalableScalar, 0xBF80_0000UL, 0UL)]
    [TestCase(GT_NEG, false, TYP_FLOAT, SimdScalableRepeated, 0UL, 0UL, SimdScalableRepeated, 0x8000_0000UL, 0UL)]
    [TestCase(GT_NEG, false, TYP_FLOAT, SimdScalableSequence, 0x3F80_0000UL, 0x8000_0000UL,
        SimdScalableSequence, 0xBF80_0000UL, 0UL)]
    [TestCase(GT_NOT, false, TYP_FLOAT, SimdScalableRepeated, 0x7FC0_1234UL, 0UL, SimdScalableRepeated, 0x803F_EDCBUL, 0UL)]
    [TestCase(GT_LZCNT, false, TYP_FLOAT, SimdScalableRepeated, 0UL, 0UL, SimdScalableRepeated, 32UL, 0UL)]
    [TestCase(GT_NEG, false, TYP_DOUBLE, SimdScalableRepeated, 0UL, 0UL,
        SimdScalableRepeated, 0x8000_0000_0000_0000UL, 0UL)]
    public static void ScalableUnaryFoldingPreservesRepresentableKindsAndExactBits(
        genTreeOps oper, bool scalar, var_types baseType, SimdScalableKind kind, ulong index, ulong step,
        SimdScalableKind expectedKind, ulong expectedIndex, ulong expectedStep)
    {
        var compiler = JitTls.Compiler ?? throw new AssertionException("The fixture compiler is not initialized.");
        var node = compiler.gtNewSimdVconNode(TYP_SIMD, baseType, kind, index, step);

        Assert.That(node.TryEvaluateUnaryInPlace(oper, scalar, baseType), Is.True);
        Assert.That(node.SimdScalableVal.BaseType, Is.EqualTo(baseType));
        Assert.That(node.SimdScalableVal.Kind, Is.EqualTo(expectedKind));
        Assert.That(node.SimdScalableVal.Index.u64[0], Is.EqualTo(expectedIndex));
        Assert.That(node.SimdScalableVal.Step.u64[0], Is.EqualTo(expectedStep));
    }

    [TestCase(GT_LZCNT, TYP_BYTE, SimdScalableRepeated, 1UL, 0UL)]
    [TestCase(GT_LZCNT, TYP_UBYTE, SimdScalableRepeated, 1UL, 0UL)]
    [TestCase(GT_LZCNT, TYP_SHORT, SimdScalableRepeated, 1UL, 0UL)]
    [TestCase(GT_LZCNT, TYP_USHORT, SimdScalableRepeated, 1UL, 0UL)]
    [TestCase(GT_LZCNT, TYP_INT, SimdScalableSequence, 1UL, 1UL)]
    [TestCase(GT_NOT, TYP_FLOAT, SimdScalableSequence, 0x3F80_0000UL, 0x3F80_0000UL)]
    [TestCase(GT_NEG, TYP_FLOAT, SimdScalableScalar, 0x3F80_0000UL, 0UL)]
    [TestCase(GT_NEG, TYP_DOUBLE, SimdScalableScalar, 0x3FF0_0000_0000_0000UL, 0UL)]
    [TestCase(GT_NOT, TYP_INT, SimdScalableScalar, 1UL, 0UL)]
    [TestCase(GT_LZCNT, TYP_INT, SimdScalableScalar, 1UL, 0UL)]
    public static void UnrepresentableScalableUnaryResultsLeaveTheTreeUnchanged(
        genTreeOps oper, var_types baseType, SimdScalableKind kind, ulong index, ulong step)
    {
        var compiler = JitTls.Compiler ?? throw new AssertionException("The fixture compiler is not initialized.");
        var node = compiler.gtNewSimdVconNode(TYP_SIMD, baseType, kind, index, step);
        var original = node.SimdScalableVal;

        Assert.That(node.TryEvaluateUnaryInPlace(oper, false, baseType), Is.False);
        Assert.That(node.SimdScalableVal, Is.EqualTo(original));
    }

#if DEBUG
    [TestCase(TYP_BYTE, SimdScalableKind.SimdScalableRepeated, 0xFFUL, 0UL, "0xff, 0xff, 0xff")]
    [TestCase(TYP_UBYTE, SimdScalableKind.SimdScalableSequence, 0xFFUL, 1UL, "0xff, 0x00, 0x01")]
    [TestCase(TYP_SHORT, SimdScalableKind.SimdScalableSequence, 0xFFFFUL, 1UL, "0xffff, 0x0000, 0x0001")]
    [TestCase(TYP_USHORT, SimdScalableKind.SimdScalableScalar, 1UL, 0UL, "0x0001, 0x0000, 0x0000")]
    [TestCase(TYP_INT, SimdScalableKind.SimdScalableSequence, 0xFFFF_FFFFUL, 1UL, "0xffffffff, 0x00000000, 0x00000001")]
    [TestCase(TYP_UINT, SimdScalableKind.SimdScalableScalar, 1UL, 0UL, "0x00000001, 0x00000000, 0x00000000")]
    [TestCase(TYP_LONG, SimdScalableKind.SimdScalableSequence, ulong.MaxValue, 1UL,
        "0xffffffffffffffff, 0x0000000000000000, 0x0000000000000001")]
    [TestCase(TYP_LONG, SimdScalableKind.SimdScalableSequence, 1UL, ulong.MaxValue,
        "0x0000000000000001, 0x0000000000000000, 0xffffffffffffffff")]
    [TestCase(TYP_ULONG, SimdScalableKind.SimdScalableScalar, 1UL, 0UL,
        "0x0000000000000001, 0x0000000000000000, 0x0000000000000000")]
    [TestCase(TYP_FLOAT, SimdScalableKind.SimdScalableSequence, 0x3F80_0000UL, 0x3F00_0000UL,
        "1.00000000, 1.50000000, 2.00000000")]
    [TestCase(TYP_FLOAT, SimdScalableKind.SimdScalableSequence, 0x4B80_0000UL, 0x3F80_0000UL,
        "16777216.0, 16777216.0, 16777218.0")]
    [TestCase(TYP_FLOAT, SimdScalableKind.SimdScalableScalar, 0x8000_0000UL, 0UL,
        "-0.00000000, 0.00000000, 0.00000000")]
    [TestCase(TYP_FLOAT, SimdScalableKind.SimdScalableRepeated, 0x7FC0_1234UL, 0UL, "nan, nan, nan")]
    [TestCase(TYP_DOUBLE, SimdScalableKind.SimdScalableSequence, 0x3FF0_0000_0000_0000UL, 0x3FE0_0000_0000_0000UL,
        "1.0000000000000000, 1.5000000000000000, 2.0000000000000000")]
    [TestCase(TYP_DOUBLE, SimdScalableKind.SimdScalableScalar, 0x8000_0000_0000_0000UL, 0UL,
        "-0.0000000000000000, 0.0000000000000000, 0.0000000000000000")]
    [TestCase(TYP_DOUBLE, SimdScalableKind.SimdScalableRepeated, 0x7FF8_0000_0000_1234UL, 0UL, "nan, nan, nan")]
    public static unsafe void ScalableConstantDumpsPreserveNativeElementArithmeticAndFormatting(
        var_types baseType, SimdScalableKind kind, ulong index, ulong step, string elements)
    {
        var compiler = JitTls.Compiler ?? throw new AssertionException("The fixture compiler is not initialized.");
        var node = compiler.gtNewSimdVconNode(TYP_SIMD, baseType, kind, index, step);
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previousWriter = Globals.s_jitstdout;

        try
        {
            Globals.s_jitstdout = writer;
            compiler.gtDispConst(node);
            writer.Flush();
        }
        finally
        {
            Globals.s_jitstdout = previousWriter;
        }

        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo($"{baseType.Name,-6} <{elements}...>"));
    }

    [Test]
    public static unsafe void ScalableHashCombinesBothOperandWordsInNativeOrder()
    {
        var compiler = JitTls.Compiler ?? throw new AssertionException("The fixture compiler is not initialized.");
        var node = compiler.gtNewSimdVconNode(
            TYP_SIMD, TYP_ULONG, SimdScalableKind.SimdScalableSequence, 0x1234_5678_90AB_CDEF, 0xFEDC_BA09_8765_4321);
        uint payloadHash = 0;
        foreach (var word in new uint[] { (uint)SimdScalableKind.SimdScalableSequence, (uint)TYP_ULONG,
            0x90AB_CDEF, 0x1234_5678, 0x8765_4321, 0xFEDC_BA09 })
        {
            payloadHash = unchecked((payloadHash + (payloadHash / 2)) ^ word);
        }
        var operatorHash = (uint)genTreeOps.GT_CNS_VEC;
        var expectedHash = unchecked((operatorHash + (operatorHash / 2)) ^ payloadHash);

        Assert.That(compiler.gtHashValue(node), Is.EqualTo(expectedHash));
    }
#endif

    [TestCase(TYP_BYTE, 0xffffUL)]
    [TestCase(TYP_UBYTE, 0xffffUL)]
    [TestCase(TYP_SHORT, 0x5555UL)]
    [TestCase(TYP_USHORT, 0x5555UL)]
    [TestCase(TYP_INT, 0x1111UL)]
    [TestCase(TYP_UINT, 0x1111UL)]
    [TestCase(TYP_FLOAT, 0x1111UL)]
    [TestCase(TYP_LONG, 0x0101UL)]
    [TestCase(TYP_ULONG, 0x0101UL)]
    [TestCase(TYP_DOUBLE, 0x0101UL)]
    public static void TrueMaskUsesBaseTypeElementStride(var_types baseType, ulong rawBits)
    {
        simdmask_t mask = default;
        mask.u64[0] = rawBits;
        var node = new GenTreeMskCon(mask);

        Assert.That(node.IsTrue(baseType), Is.True);
        Assert.That(EvaluateSimdMaskToPattern<simd16_t>(baseType, mask),
            Is.EqualTo(SveMaskPattern.SveMaskPatternAll));
    }

    [Test]
    public static void MaskPatternRejectsGapsAndNonUnitLaneValues()
    {
        simdmask_t mask = default;
        mask.u64[0] = 0x0011;
        Assert.That(EvaluateSimdMaskToPattern<simd16_t>(TYP_INT, mask),
            Is.EqualTo(SveMaskPattern.SveMaskPatternVectorCount2));

        mask.u64[0] = 0x0101;
        Assert.That(EvaluateSimdMaskToPattern<simd16_t>(TYP_INT, mask),
            Is.EqualTo(SveMaskPattern.SveMaskPatternNone));

        mask.u64[0] = 0x0003;
        Assert.That(EvaluateSimdMaskToPattern<simd16_t>(TYP_SHORT, mask),
            Is.EqualTo(SveMaskPattern.SveMaskPatternNone));

        mask.u64[0] = 0xffff;
        Assert.That(new GenTreeMskCon(mask).IsTrue(TYP_SHORT), Is.False);
    }

    [Test]
    public static void MaskPatternOnlyReadsActiveLanesAndNativePrefixEncodings()
    {
        simdmask_t mask = default;
        mask.u64[0] = 0x1111 | (1UL << 48);
        Assert.That(EvaluateSimdMaskToPattern<simd16_t>(TYP_INT, mask),
            Is.EqualTo(SveMaskPattern.SveMaskPatternAll));

        mask.u64[0] = 0xff;
        Assert.That(EvaluateSimdMaskToPattern<simd16_t>(TYP_BYTE, mask),
            Is.EqualTo(SveMaskPattern.SveMaskPatternVectorCount8));

        mask.u64[0] = 0x1ff;
        Assert.That(EvaluateSimdMaskToPattern<simd16_t>(TYP_BYTE, mask),
            Is.EqualTo(SveMaskPattern.SveMaskPatternNone));

        mask.u64[0] = 0;
        Assert.That(EvaluateSimdMaskToPattern<simd16_t>(TYP_BYTE, mask),
            Is.EqualTo(SveMaskPattern.SveMaskPatternNone));
    }

    [Test]
    public static void ConsecutiveIntrinsicRegistersDeriveFromTheFirstRegister()
    {
        var address = new GenTreeIntCon(TYP_I_IMPL, 0);
        var node = new GenTreeHWIntrinsic(TYP_STRUCT, NI_AdvSimd_Arm64_Load4xVector128, TYP_INT, 16, address);

        Assert.That(node.IsMultiRegNode, Is.True);
        node.SetRegNumByIdx(REG_V4, 0);
        node.SetRegNumByIdx(REG_V7, 3);

        Assert.That(node.GetRegNumByIdx(0), Is.EqualTo(REG_V4));
        Assert.That(node.GetRegNumByIdx(1), Is.EqualTo(REG_V5));
        Assert.That(node.GetRegNumByIdx(2), Is.EqualTo(REG_V6));
        Assert.That(node.GetRegNumByIdx(3), Is.EqualTo(REG_V7));
    }

    [Test]
    public static void OtherRegisterIsStoredForNonConsecutiveIntrinsics()
    {
        var scalar = new GenTreeIntCon(TYP_INT, 7);
        var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_INT, 16, scalar);

        node.SetRegNumByIdx(REG_V0, 0);
        node.SetRegNumByIdx(REG_V9, 1);

        Assert.That(node.GetRegNumByIdx(0), Is.EqualTo(REG_V0));
        Assert.That(node.GetRegNumByIdx(1), Is.EqualTo(REG_V9));
    }
}
#endif
