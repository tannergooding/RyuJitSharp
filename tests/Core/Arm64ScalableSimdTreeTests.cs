// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64ScalableSimdTreeTests
{
    [SetUp]
    public static void RequireArm64ScalableSimd()
    {
        Assume.That(
            Configuration.TARGET_ARM64 && Configuration.FEATURE_HW_INTRINSICS && Configuration.FEATURE_SIMD,
            "This fixture requires ARM64 hardware-intrinsic and SIMD support.");
    }

    [TestCase(GT_ADD, NI_Sve_Add)]
    [TestCase(GT_AND, NI_Sve_And)]
    [TestCase(GT_OR, NI_Sve_Or)]
    [TestCase(GT_XOR, NI_Sve_Xor)]
    [TestCase(GT_SUB, NI_Sve_Subtract)]
    [TestCase(GT_LSH, NI_Sve_ShiftLeftLogical)]
    [TestCase(GT_RSH, NI_Sve_ShiftRightArithmetic)]
    [TestCase(GT_RSZ, NI_Sve_ShiftRightLogical)]
    [TestCase(GT_MUL, NI_Sve_Multiply)]
    [TestCase(GT_DIV, NI_Sve_Divide)]
    public static void BinaryOperationLookupUsesSveIds(genTreeOps operation, NamedIntrinsic expected)
    {
        WithCompiler(compiler => {
            var first = new GenTreeLclVar(TYP_SIMD_ARM64, 0);
            var second = new GenTreeLclVar(TYP_SIMD_ARM64, 1);

            var actual = compiler.GetHWIntrinsicIdForBinOp(
                operation, first, second, TYP_INT, SIZE_UNKNOWN, isScalar: false);

            Assert.That(actual, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void AndNotRemainsDecomposedForScalableVectors()
    {
        WithCompiler(compiler => {
            var first = new GenTreeLclVar(TYP_SIMD_ARM64, 0);
            var second = new GenTreeLclVar(TYP_SIMD_ARM64, 1);

            Assert.That(
                compiler.GetHWIntrinsicIdForBinOp(GT_AND_NOT, first, second, TYP_INT, SIZE_UNKNOWN, isScalar: false),
                Is.EqualTo(NI_Illegal));
        });
    }

    [TestCase(GT_EQ, NI_Sve_CompareEqual)]
    [TestCase(GT_GE, NI_Sve_CompareGreaterThanOrEqual)]
    [TestCase(GT_GT, NI_Sve_CompareGreaterThan)]
    [TestCase(GT_LE, NI_Sve_CompareLessThanOrEqual)]
    [TestCase(GT_LT, NI_Sve_CompareLessThan)]
    [TestCase(GT_NE, NI_Sve_CompareNotEqualTo)]
    public static void ComparisonLookupUsesSveIds(genTreeOps operation, NamedIntrinsic expected)
    {
        WithCompiler(compiler => {
            var first = new GenTreeLclVar(TYP_SIMD_ARM64, 0);
            var second = new GenTreeLclVar(TYP_SIMD_ARM64, 1);
            var lookupType = compiler.GetLookupTypeForCmpOp(
                operation, TYP_SIMD_ARM64, TYP_INT, SIZE_UNKNOWN);

            Assert.That(
                lookupType,
                Is.EqualTo(Configuration.FEATURE_MASKED_HW_INTRINSICS ? TYP_MASK : TYP_SIMD_ARM64));
            Assert.That(
                compiler.GetHWIntrinsicIdForCmpOp(
                    operation, lookupType, first, second, TYP_INT, SIZE_UNKNOWN, isScalar: false),
                Is.EqualTo(expected));
        });
    }

    [Test]
    public static void ScalableUnaryBuildersUseSveAndIntegerBitwiseTypes()
    {
        WithCompiler(compiler => {
            var vector = new GenTreeLclVar(TYP_SIMD_ARM64, 0);

            var absolute = compiler.gtNewSimdAbsNode(TYP_SIMD_ARM64, vector, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();
            var negate = compiler.gtNewSimdUnOpNode(GT_NEG, TYP_SIMD_ARM64, vector, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();
            var bitwiseNot = compiler.gtNewSimdUnOpNode(GT_NOT, TYP_SIMD_ARM64, vector, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();

            Assert.Multiple(() => {
                Assert.That(absolute.HWIntrinsicId, Is.EqualTo(NI_Sve_Abs));
                Assert.That(negate.HWIntrinsicId, Is.EqualTo(NI_Sve_Negate));
                Assert.That(bitwiseNot.HWIntrinsicId, Is.EqualTo(NI_Sve_Not));
                Assert.That(bitwiseNot.SimdBaseType, Is.EqualTo(TYP_UINT));
            });
        });
    }

    [Test]
    public static void ScalableRightShiftKeepsPositiveVectorCountsAndElementType()
    {
        WithCompiler(compiler => {
            var vector = new GenTreeLclVar(TYP_SIMD_ARM64, 0);
            var count = new GenTreeLclVar(TYP_INT, 1);
            var shift = compiler.gtNewSimdBinOpNode(
                GT_RSH, TYP_SIMD_ARM64, vector, count, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();
            var broadcast = shift.GetOp(2).AsHWIntrinsic();

            Assert.Multiple(() => {
                Assert.That(shift.HWIntrinsicId, Is.EqualTo(NI_Sve_ShiftRightArithmetic));
                Assert.That(shift.SimdBaseType, Is.EqualTo(TYP_INT));
                Assert.That(shift.AuxiliaryType, Is.EqualTo(TYP_INT));
                Assert.That(broadcast.HWIntrinsicId, Is.EqualTo(NI_Vector_Create));
                Assert.That(broadcast.GetOp(1).Oper, Is.EqualTo(GT_AND));
                Assert.That(broadcast.GetOp(1).AsOp().Op1, Is.SameAs(count));
            });
        });
    }

    [Test]
    public static void ScalableRepeatedConstantsPreserveFloatingPointBits()
    {
        WithCompiler(compiler => {
            var floatBits = unchecked((int)0x7FC0_1234);
            var doubleBits = unchecked((long)0x7FF8_0000_0000_1234);
            var single = compiler.gtNewScalableRepeatedConstant(BitConverter.Int32BitsToSingle(floatBits));
            var dbl = compiler.gtNewScalableRepeatedConstant(BitConverter.Int64BitsToDouble(doubleBits));

            Assert.Multiple(() => {
                Assert.That(single.SimdScalableVal.Kind, Is.EqualTo(SimdScalableKind.SimdScalableRepeated));
                Assert.That(single.SimdScalableVal.BaseType, Is.EqualTo(TYP_FLOAT));
                Assert.That(BitConverter.SingleToInt32Bits(single.SimdScalableVal.Index.f32[0]), Is.EqualTo(floatBits));
                Assert.That(dbl.SimdScalableVal.Kind, Is.EqualTo(SimdScalableKind.SimdScalableRepeated));
                Assert.That(dbl.SimdScalableVal.BaseType, Is.EqualTo(TYP_DOUBLE));
                Assert.That(BitConverter.DoubleToInt64Bits(dbl.SimdScalableVal.Index.f64[0]), Is.EqualTo(doubleBits));
            });
        });
    }

    [Test]
    public static void ScalableRoundingAndSquareRootUseSveIds()
    {
        WithCompiler(compiler => {
            var vector = new GenTreeLclVar(TYP_SIMD_ARM64, 0);

            var ceiling = compiler.gtNewSimdCeilNode(TYP_SIMD_ARM64, vector, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();
            var floor = compiler.gtNewSimdFloorNode(TYP_SIMD_ARM64, vector, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();
            var round = compiler.gtNewSimdRoundNode(TYP_SIMD_ARM64, vector, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();
            var truncate = compiler.gtNewSimdTruncNode(TYP_SIMD_ARM64, vector, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();
            var sqrt = compiler.gtNewSimdSqrtNode(TYP_SIMD_ARM64, vector, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();

            Assert.Multiple(() => {
                Assert.That(ceiling.HWIntrinsicId, Is.EqualTo(NI_Sve_RoundToPositiveInfinity));
                Assert.That(floor.HWIntrinsicId, Is.EqualTo(NI_Sve_RoundToNegativeInfinity));
                Assert.That(round.HWIntrinsicId, Is.EqualTo(NI_Sve_RoundToNearest));
                Assert.That(truncate.HWIntrinsicId, Is.EqualTo(NI_Sve_RoundToZero));
                Assert.That(sqrt.HWIntrinsicId, Is.EqualTo(NI_Sve_Sqrt));
            });
        });
    }

    [Test]
    public static void ScalableConversionsAndFusedMultiplyAddUseSveIds()
    {
        WithCompiler(compiler => {
            var first = new GenTreeLclVar(TYP_SIMD_ARM64, 0);
            var second = new GenTreeLclVar(TYP_SIMD_ARM64, 1);
            var addend = new GenTreeLclVar(TYP_SIMD_ARM64, 2);

            var intConversion = compiler.gtNewSimdCvtNativeNode(
                TYP_SIMD_ARM64, first, TYP_INT, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();
            var longConversion = compiler.gtNewSimdCvtNativeNode(
                TYP_SIMD_ARM64, first, TYP_LONG, TYP_DOUBLE, SIZE_UNKNOWN).AsHWIntrinsic();
            var fma = compiler.gtNewSimdFmaNode(
                TYP_SIMD_ARM64, first, second, addend, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();

            Assert.Multiple(() => {
                Assert.That(intConversion.HWIntrinsicId, Is.EqualTo(NI_Sve_ConvertToInt32));
                Assert.That(longConversion.HWIntrinsicId, Is.EqualTo(NI_Sve_ConvertToInt64));
                Assert.That(fma.HWIntrinsicId, Is.EqualTo(NI_Sve_FusedMultiplyAdd));
                Assert.That(fma.GetOp(1), Is.SameAs(addend));
                Assert.That(fma.GetOp(3), Is.SameAs(first));
            });
        });
    }

    [Test]
    public static void ScalableRearrangementAndReductionUseSveIds()
    {
        WithCompiler(compiler => {
            var first = new GenTreeLclVar(TYP_SIMD_ARM64, 0);
            var second = new GenTreeLclVar(TYP_SIMD_ARM64, 1);

            var narrow = compiler.gtNewSimdNarrowNode(
                TYP_SIMD_ARM64, first, second, TYP_INT, SIZE_UNKNOWN).AsHWIntrinsic();
            var floatNarrow = compiler.gtNewSimdNarrowNode(
                TYP_SIMD_ARM64, first, second, TYP_FLOAT, SIZE_UNKNOWN).AsHWIntrinsic();
            var zip = compiler.gtNewSimdZipNode(
                TYP_SIMD_ARM64, first, second, TYP_INT, SIZE_UNKNOWN, upper: true).AsHWIntrinsic();
            var unzip = compiler.gtNewSimdUnzipNode(
                TYP_SIMD_ARM64, first, second, TYP_INT, SIZE_UNKNOWN, odd: true).AsHWIntrinsic();
            var reverse = compiler.gtNewSimdReverseNode(
                TYP_SIMD_ARM64, first, TYP_INT, SIZE_UNKNOWN).AsHWIntrinsic();
            var widenLower = compiler.gtNewSimdWidenLowerNode(
                TYP_SIMD_ARM64, first, TYP_INT, SIZE_UNKNOWN).AsHWIntrinsic();
            var widenUpper = compiler.gtNewSimdWidenUpperNode(
                TYP_SIMD_ARM64, first, TYP_INT, SIZE_UNKNOWN).AsHWIntrinsic();
            var sum = compiler.gtNewSimdSumNode(TYP_INT, first, TYP_INT, SIZE_UNKNOWN).AsHWIntrinsic();
            var indices = new GenTreeLclVar(TYP_SIMD_ARM64, 3);

            Assert.Multiple(() => {
                Assert.That(narrow.HWIntrinsicId, Is.EqualTo(NI_Sve_UnzipEven));
                Assert.That(floatNarrow.HWIntrinsicId, Is.EqualTo(NI_Sve_UnzipEven));
                Assert.That(floatNarrow.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Sve_ConvertToSingle));
                Assert.That(zip.HWIntrinsicId, Is.EqualTo(NI_Sve_ZipHigh));
                Assert.That(unzip.HWIntrinsicId, Is.EqualTo(NI_Sve_UnzipOdd));
                Assert.That(reverse.HWIntrinsicId, Is.EqualTo(NI_Sve_ReverseElement));
                Assert.That(widenLower.HWIntrinsicId, Is.EqualTo(NI_Sve_SignExtendWideningLower));
                Assert.That(widenUpper.HWIntrinsicId, Is.EqualTo(NI_Sve_SignExtendWideningUpper));
                Assert.That(sum.HWIntrinsicId, Is.EqualTo(NI_Vector_ToScalar));
                Assert.That(sum.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Sve_AddAcross));
                Assert.That(compiler.IsValidForShuffle(indices, SIZE_UNKNOWN, TYP_INT, out var canBecomeValid, false), Is.False);
                Assert.That(canBecomeValid, Is.False);
            });
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            if (TestConfiguration.IsDebug)
            {
                TestConfiguration.SetScalableVectorConfiguration(enabled: true);
            }
            action(compiler);
        });
    }
}
