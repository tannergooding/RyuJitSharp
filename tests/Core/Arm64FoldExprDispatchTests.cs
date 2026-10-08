// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64FoldExprDispatchTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void HardwareIntrinsicDispatchPreservesTier0Guard(bool tier0Disabled)
    {
        WithCompiler(compiler =>
        {
            var input = compiler.gtNewIconNode(TYP_INT, 42);
            var intrinsic = new GenTreeHWIntrinsic(TYP_INT, NI_ArmBase_LeadingZeroCount, TYP_INT, 0, input);

            if (tier0Disabled)
            {
                Assert.That(compiler.gtFoldExpr(intrinsic), Is.SameAs(intrinsic));
            }
            else
            {
                var result = compiler.gtFoldExpr(intrinsic);
                Assert.That(result, Is.SameAs(input));
                Assert.That(result.AsIntCon().IconValue, Is.EqualTo((nint)26));
            }
        }, tier0Disabled);
    }

    [TestCase(NI_ArmBase_ReverseElementBits, TYP_INT, -2147483648L)]
    [TestCase(NI_ArmBase_Arm64_ReverseElementBits, TYP_LONG, long.MinValue)]
    public static void ReverseBitsFoldsConstants(NamedIntrinsic intrinsicId, var_types type, long expected)
    {
        WithCompiler(compiler =>
        {
            var input = compiler.gtNewIconNode(type, 1);
            var intrinsic = new GenTreeHWIntrinsic(type, intrinsicId, type, 0, input);

            var result = compiler.gtFoldExpr(intrinsic);
            Assert.That(result, Is.SameAs(input));
            Assert.That(result.AsIntConCommon().IntegralValue, Is.EqualTo(expected));
        });
    }

    [TestCase(NI_ArmBase_ReverseElementBits, TYP_INT, 0x0123_4567UL, 0xE6A2_C480UL)]
    [TestCase(NI_ArmBase_Arm64_ReverseElementBits, TYP_LONG, 0x0123_4567_89AB_CDEFUL, 0xF7B3_D591_E6A2_C480UL)]
    public static void ReverseBitsPreservesInteriorPattern(NamedIntrinsic intrinsicId, var_types type,
        ulong inputValue, ulong expected)
    {
        WithCompiler(compiler =>
        {
            var input = compiler.gtNewIconNode(type, unchecked((nint)inputValue));
            var intrinsic = new GenTreeHWIntrinsic(type, intrinsicId, type, 0, input);

            var result = compiler.gtFoldExpr(intrinsic);
            Assert.That(result, Is.SameAs(input));
            Assert.That(result.AsIntConCommon().UnsignedIntegralValue, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void VectorToVector128ZeroesUpperHalf()
    {
        WithCompiler(compiler =>
        {
            var input = compiler.gtNewVconNode(TYP_SIMD8);
            input.SimdVal.u64[0] = 0xFEDC_BA98_7654_3210;
            input.SimdVal.u64[1] = ulong.MaxValue;
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_ToVector128, TYP_LONG, 16, input);

            var result = compiler.gtFoldExpr(intrinsic);
            Assert.That(result, Is.SameAs(input));
            Assert.That(result.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(result.AsVecCon().SimdVal.u64[0], Is.EqualTo(0xFEDC_BA98_7654_3210));
            Assert.That(result.AsVecCon().SimdVal.u64[1], Is.Zero);
        });
    }

    [Test]
    public static void SveTrueMaskSelectsFirstPredicate()
    {
        WithCompiler(compiler =>
        {
            var condition = compiler.gtNewSimdTrueMaskNode(TYP_INT);
            var first = compiler.gtNewMskConNode(simdmask_t.Zero);
            var second = compiler.gtNewMskConNode(simdmask_t.AllBitsSet(4));
            var intrinsic = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_ConditionalSelect_Predicates,
                TYP_INT, 16, condition, first, second);

            Assert.That(compiler.gtFoldExpr(intrinsic), Is.SameAs(first));
        });
    }

    [Test]
    public static void SvePredicateSelectCombinesConstantMasks()
    {
        WithCompiler(compiler =>
        {
            simdmask_t conditionBits = default;
            conditionBits.u64[0] = 1;
            var condition = compiler.gtNewMskConNode(conditionBits);
            var first = compiler.gtNewMskConNode(conditionBits);
            simdmask_t secondBits = default;
            secondBits.u64[0] = 16;
            var second = compiler.gtNewMskConNode(secondBits);
            var intrinsic = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_ConditionalSelect_Predicates,
                TYP_INT, 16, condition, first, second);

            var result = compiler.gtFoldExpr(intrinsic);
            Assert.That(result, Is.SameAs(first));
            Assert.That(result.AsMskCon().SimdMaskVal.u64[0], Is.EqualTo(17UL));
        });
    }

    [Test]
    public static void AdvSimdSelectWithAllBitsSetReturnsTrueOperand()
    {
        WithCompiler(compiler =>
        {
            var condition = compiler.gtNewVconNode(TYP_SIMD16);
            condition.SimdVal.v128[0] = simd16_t.AllBitsSet;
            condition.SimdVal.v128[1] = default;
            var whenTrue = new GenTreeLclVar(TYP_SIMD16, 0);
            var whenFalse = new GenTreeLclVar(TYP_SIMD16, 1);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_BitwiseSelect,
                TYP_INT, 16, condition, whenTrue, whenFalse);

            Assert.That(compiler.gtFoldExpr(intrinsic), Is.SameAs(whenTrue));
        });
    }

    [Test]
    public static void AdvSimdSelectWithZeroConditionReturnsFalseOperand()
    {
        WithCompiler(compiler =>
        {
            var condition = compiler.gtNewVconNode(TYP_SIMD16);
            condition.SimdVal.v128[0] = default;
            condition.SimdVal.v128[1] = simd16_t.AllBitsSet;
            var whenTrue = new GenTreeLclVar(TYP_SIMD16, 0);
            var whenFalse = new GenTreeLclVar(TYP_SIMD16, 1);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_BitwiseSelect,
                TYP_INT, 16, condition, whenTrue, whenFalse);

            Assert.That(compiler.gtFoldExpr(intrinsic), Is.SameAs(whenFalse));
        });
    }

    [Test]
    public static void ScalableMaskToVectorProducesRepeatedAllBits()
    {
        WithCompiler(compiler =>
        {
            var mask = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, true);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD, NI_Sve_ConvertMaskToVector,
                TYP_INT, 16, mask);

            var result = compiler.gtFoldExpr(intrinsic);
            Assert.That(result.Type, Is.EqualTo(TYP_SIMD));
            Assert.That(result.AsVecCon().SimdScalableVal.Kind, Is.EqualTo(SimdScalableKind.SimdScalableRepeated));
            Assert.That(result.AsVecCon().SimdScalableVal.Index.u64[0], Is.EqualTo((ulong)uint.MaxValue));
        });
    }

    [Test]
    public static void MaskVectorRoundTripUsesArm64SecondOperand()
    {
        WithCompiler(compiler =>
        {
            var predicate = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, true);
            var vector = new GenTreeLclVar(TYP_SIMD16, 0);
            var toMask = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_ConvertVectorToMask,
                TYP_INT, 16, predicate, vector);
            var toVector = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_ConvertMaskToVector,
                TYP_INT, 16, toMask);

            Assert.That(compiler.gtFoldExpr(toVector), Is.SameAs(vector));
        });
    }

    [Test]
    public static void MultiplyByScalarOneReturnsVector()
    {
        WithCompiler(compiler =>
        {
            var vector = new GenTreeLclVar(TYP_SIMD16, 0);
            var scalar = compiler.gtNewVconNode(TYP_SIMD8);
            scalar.SimdVal.i32[0] = 1;
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_MultiplyByScalar,
                TYP_INT, 16, vector, scalar);

            Assert.That(compiler.gtFoldExpr(intrinsic), Is.SameAs(vector));
        });
    }

    [TestCase(NI_Sve_And, NI_Sve_And_Predicates)]
    [TestCase(NI_Sve_BitwiseClear, NI_Sve_BitwiseClear_Predicates)]
    [TestCase(NI_Sve_Xor, NI_Sve_Xor_Predicates)]
    [TestCase(NI_Sve_Or, NI_Sve_Or_Predicates)]
    [TestCase(NI_Sve_ZipHigh, NI_Sve_ZipHigh_Predicates)]
    [TestCase(NI_Sve_ZipLow, NI_Sve_ZipLow_Predicates)]
    [TestCase(NI_Sve_UnzipOdd, NI_Sve_UnzipOdd_Predicates)]
    [TestCase(NI_Sve_UnzipEven, NI_Sve_UnzipEven_Predicates)]
    [TestCase(NI_Sve_TransposeEven, NI_Sve_TransposeEven_Predicates)]
    [TestCase(NI_Sve_TransposeOdd, NI_Sve_TransposeOdd_Predicates)]
    [TestCase(NI_Sve_ReverseElement, NI_Sve_ReverseElement_Predicates)]
    [TestCase(NI_Sve_ConditionalSelect, NI_Sve_ConditionalSelect_Predicates)]
    public static void MaskVariantMatchesNativeMapping(NamedIntrinsic intrinsic, NamedIntrinsic expected)
    {
        Assert.That(HWIntrinsicInfo.GetMaskVariant(intrinsic), Is.EqualTo(expected));
    }

    [Test]
    public static void SveAndConvertsVectorOperandsToPredicateOperands()
    {
        WithCompiler(compiler =>
        {
            var firstMask = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, true);
            var secondMask = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, false);
            var first = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, firstMask, TYP_INT, 16);
            var second = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, secondMask, TYP_INT, 16);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_And, TYP_INT, 16, first, second);

            var result = compiler.gtFoldExpr(intrinsic);
            Assert.That(result.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Sve_ConvertMaskToVector));
            Assert.That(result.AsHWIntrinsic().GetOp(1), Is.SameAs(intrinsic));
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(NI_Sve_And_Predicates));
            Assert.That(intrinsic.GetOp(1), Is.SameAs(firstMask));
            Assert.That(intrinsic.GetOp(2), Is.SameAs(secondMask));
        });
    }

    [Test]
    public static void SveAndWithoutConvertibleOperandsKeepsOriginal()
    {
        WithCompiler(compiler =>
        {
            var first = new GenTreeLclVar(TYP_SIMD16, 0);
            var second = new GenTreeLclVar(TYP_SIMD16, 1);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_And, TYP_INT, 16, first, second);

            Assert.That(compiler.gtFoldExpr(intrinsic), Is.SameAs(intrinsic));
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(NI_Sve_And));
        });
    }

    [Test]
    public static void SveConditionalSelectPreservesPredicateWhenRewritingVectors()
    {
        WithCompiler(compiler =>
        {
            var condition = new GenTreeLclVar(TYP_MASK, 0);
            var firstMask = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, true);
            var secondMask = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, false);
            var first = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, firstMask, TYP_INT, 16);
            var second = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, secondMask, TYP_INT, 16);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_ConditionalSelect,
                TYP_INT, 16, condition, first, second);

            var result = compiler.gtFoldExpr(intrinsic);
            Assert.That(result.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Sve_ConvertMaskToVector));
            Assert.That(result.AsHWIntrinsic().GetOp(1), Is.SameAs(intrinsic));
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(NI_Sve_ConditionalSelect_Predicates));
            Assert.That(intrinsic.GetOp(1), Is.SameAs(condition));
            Assert.That(intrinsic.GetOp(2), Is.SameAs(firstMask));
            Assert.That(intrinsic.GetOp(3), Is.SameAs(secondMask));
        });
    }

    [TestCase(TYP_BYTE)]
    [TestCase(TYP_UBYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_USHORT)]
    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_UINT)]
    [TestCase(TYP_DOUBLE)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_ULONG)]
    public static void WideShiftNarrowingSaturatesAndDuplicatesEachSourceWord(var_types baseType)
    {
        var elementSize = (int)baseType.Size;
        var maximum = elementSize == sizeof(ulong) ? ulong.MaxValue : (1UL << (elementSize * 8)) - 1;

        simd16_t input = default;
        input.u64[0] = maximum - 1;
        input.u64[1] = elementSize == sizeof(ulong) ? maximum : maximum + 1;
        var result = Compiler.NarrowAndDuplicateSimdLong(baseType, input);
        AssertNarrowedElements(result, elementSize, maximum - 1, maximum);

        input.u64[0] = 0;
        input.u64[1] = 1;
        result = Compiler.NarrowAndDuplicateSimdLong(baseType, input);
        AssertNarrowedElements(result, elementSize, 0, 1);

        simd8_t shortInput = default;
        shortInput.u64[0] = elementSize == sizeof(ulong) ? maximum : maximum + 1;
        var shortResult = Compiler.NarrowAndDuplicateSimdLong(baseType, shortInput);
        Assert.That(shortResult.u64[0], Is.EqualTo(ulong.MaxValue));

        shortInput.u64[0] = 0;
        shortResult = Compiler.NarrowAndDuplicateSimdLong(baseType, shortInput);
        Assert.That(shortResult.u64[0], Is.Zero);
    }

    [Test]
    public static void WideShiftNarrowingFoldsIntoVector()
    {
        WithCompiler(compiler =>
        {
            var value = compiler.gtNewVconNode(TYP_SIMD16);
            value.SimdVal.i32[0] = 1;
            var shifts = compiler.gtNewVconNode(TYP_SIMD16);
            shifts.SimdVal.u64[0] = 2;
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_ShiftLeftLogical,
                TYP_INT, 16, value, shifts)
            {
                AuxiliaryType = TYP_ULONG,
            };

            var result = compiler.gtFoldExpr(intrinsic);
            Assert.That(result, Is.SameAs(value));
            Assert.That(result.AsVecCon().SimdVal.i32[0], Is.EqualTo(4));
            Assert.That(result.AsVecCon().SimdVal.i32[1], Is.Zero);
        });
    }

    private static void AssertNarrowedElements(in simd16_t result, int elementSize,
        ulong expectedFirst, ulong expectedSecond)
    {
        var elementCount = 16 / elementSize;
        for (var index = 0; index < elementCount; index++)
        {
            var actual = elementSize switch
            {
                1 => result.u8[index],
                2 => result.u16[index],
                4 => result.u32[index],
                8 => result.u64[index],
                _ => throw new InvalidOperationException(),
            };
            Assert.That(actual, Is.EqualTo(index < elementCount / 2 ? expectedFirst : expectedSecond),
                $"element {index}, width {elementSize}");
        }
    }

    private static void WithCompiler(Action<Compiler> action, bool tier0Disabled = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        if (tier0Disabled)
        {
            flags.Set(JitFlags.JIT_FLAG_MIN_OPT);
        }

        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(tier0Disabled);
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
