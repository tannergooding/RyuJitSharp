// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regNumber;
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

        _ = Assert.Throws<FatalJitException>(() => _ = node.IsZero);
        _ = Assert.Throws<FatalJitException>(() => _ = node.IsAllBitsSet);
    }

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
