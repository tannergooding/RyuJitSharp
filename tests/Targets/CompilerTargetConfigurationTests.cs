using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CompilerTargetConfigurationTests
{
    [Test]
    public static void LongConstantFactoryUsesTheTargetWidthRepresentation()
    {
        WithCompiler(compiler =>
        {
            const long value = 0x1234567887654321L;
            var constant = compiler.gtNewLconNode(value);

            Assert.That(constant.Type, Is.EqualTo(TYP_LONG));
            Assert.That(constant.LngValue, Is.EqualTo(value));

            if (Configuration.TARGET_64BIT)
            {
                Assert.That(constant, Is.TypeOf<GenTreeIntCon>());
            }
            else
            {
                Assert.That(constant, Is.TypeOf<GenTreeLngCon>());
            }
        });
    }

    [Test]
    public static void NullCheckTypeUsesTheTargetArchitecture()
    {
        WithCompiler(compiler =>
        {
            var type = compiler.gtTypeForNullCheck(compiler.gtNewLconNode(0));

            Assert.That(type, Is.EqualTo(Configuration.TARGET_XARCH ? TYP_INT : TYP_LONG));
        });
    }

    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void OneConstantFactorySupportsWideXarchVectors(var_types type)
    {
        Assume.That(Configuration.TARGET_XARCH && Configuration.FEATURE_SIMD,
            "Wide fixed-size SIMD constants are supported only on xarch.");

        WithCompiler(compiler =>
        {
            var constant = compiler.gtNewOneConNode(type, TYP_INT).AsVecCon();

            Assert.That(constant.Type, Is.EqualTo(type));
            Assert.That(constant.IsBroadcast(TYP_INT), Is.True);
        });
    }

    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void SimdPatternConstantsUseConfiguredTargetStorage(var_types type)
    {
        if (!Configuration.FEATURE_SIMD ||
            ((type is TYP_SIMD32 or TYP_SIMD64) && !Configuration.TARGET_XARCH))
        {
            Assert.Ignore("This vector type is unsupported on the configured target.");
        }

        WithCompiler(compiler =>
        {
            var constant = compiler.gtNewConWithPattern(type, 0xA5).AsVecCon();
            Assert.That(constant.SimdVal.AsSpan<byte>()[..SimdStorageSize].ToArray(),
                Is.All.EqualTo((byte)0xA5));
            Assert.That(constant.SimdVal.AsSpan<byte>()[SimdStorageSize..].ToArray(),
                Is.All.EqualTo((byte)0));
        });
    }

    [Test]
    public static void SimdAbsoluteValueUsesTheTargetIntrinsic()
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.FEATURE_SIMD ||
            !(Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 || Configuration.TARGET_WASM))
        {
            Assert.Ignore("SIMD absolute value intrinsics are unsupported on this target.");
        }

        var method = typeof(Compiler).GetMethod("gtNewSimdAbsNode", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new AssertionException("The SIMD absolute value helper is unavailable.");

        WithCompiler(compiler =>
        {
            var operand = compiler.gtNewVconNode(TYP_SIMD16);
            var result = method.Invoke(compiler, [TYP_SIMD16, operand, TYP_INT, (byte)16]) as GenTreeHWIntrinsic
                ?? throw new AssertionException("The SIMD absolute value helper did not return an intrinsic.");
            var expectedIntrinsic = Configuration.TARGET_XARCH
                ? NI_X86Base_Abs
                : Configuration.TARGET_ARM64 ? NI_AdvSimd_Abs : NI_PackedSimd_Abs;

            Assert.That(result.HWIntrinsicId, Is.EqualTo(expectedIntrinsic));
        });
    }

    [Test]
    public static void SimdSqrtComparisonAndSelectUseTargetIntrinsics()
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.FEATURE_SIMD ||
            !(Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 || Configuration.TARGET_WASM))
        {
            Assert.Ignore("SIMD hardware intrinsics are unsupported on this target.");
        }

        WithCompiler(compiler =>
        {
            var operand = compiler.gtNewVconNode(TYP_SIMD16);
            var sqrt = compiler.gtNewSimdSqrtNode(TYP_SIMD16, operand, TYP_FLOAT, 16).AsHWIntrinsic();
            var expectedSqrt = Configuration.TARGET_XARCH
                ? NI_X86Base_Sqrt
                : Configuration.TARGET_ARM64 ? NI_AdvSimd_Arm64_Sqrt : NI_PackedSimd_Sqrt;
            Assert.That(sqrt.HWIntrinsicId, Is.EqualTo(expectedSqrt));
            Assert.That(sqrt.GetOp(1), Is.SameAs(operand));

            var left = compiler.gtNewVconNode(TYP_SIMD16);
            var right = compiler.gtNewVconNode(TYP_SIMD16);
            var comparison = compiler.gtNewSimdCmpOpNode(GT_EQ, TYP_SIMD16, left, right, TYP_FLOAT, 16)
                .AsHWIntrinsic();
            var expectedComparison = Configuration.TARGET_XARCH
                ? NI_X86Base_CompareEqual
                : Configuration.TARGET_ARM64 ? NI_AdvSimd_CompareEqual : NI_PackedSimd_CompareEqual;
            Assert.That(comparison.HWIntrinsicId, Is.EqualTo(expectedComparison));
            Assert.That(comparison.GetOp(1), Is.SameAs(left));
            Assert.That(comparison.GetOp(2), Is.SameAs(right));

            var reduction = compiler.gtNewSimdCmpOpAllNode(GT_EQ, TYP_INT, left, right, TYP_FLOAT, 16)
                .AsHWIntrinsic();
            Assert.That(reduction.HWIntrinsicId, Is.EqualTo(NI_Vector_op_Equality));

            var selectMask = compiler.gtNewVconNode(TYP_SIMD16);
            var select = compiler.gtNewSimdCndSelNode(TYP_SIMD16, selectMask, left, right, TYP_INT, 16)
                .AsHWIntrinsic();
            var expectedSelect = Configuration.TARGET_ARM64
                ? NI_AdvSimd_BitwiseSelect
                : NI_Vector_ConditionalSelect;
            Assert.That(select.HWIntrinsicId, Is.EqualTo(expectedSelect));
        });
    }

    [Test]
    public static void MaskConversionsUseTheTargetIntrinsic()
    {
        if (!Configuration.FEATURE_MASKED_HW_INTRINSICS ||
            !(Configuration.TARGET_XARCH || Configuration.TARGET_ARM64))
        {
            Assert.Ignore("Masked hardware intrinsics are unsupported on this target.");
        }

        WithCompiler(compiler =>
        {
            var mask = new GenTreeMskCon(default);
            var expanded = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, mask, TYP_INT, 16).AsHWIntrinsic();
            var expectedExpansion = Configuration.TARGET_XARCH
                ? NI_AVX512_ConvertMaskToVector
                : NI_Sve_ConvertMaskToVector;
            Assert.That(expanded.HWIntrinsicId, Is.EqualTo(expectedExpansion));

            var vector = compiler.gtNewVconNode(TYP_SIMD16);
            var converted = compiler.gtNewSimdCvtVectorToMaskNode(TYP_MASK, vector, TYP_INT, 16).AsHWIntrinsic();
            var expectedConversion = Configuration.TARGET_XARCH
                ? NI_AVX512_ConvertVectorToMask
                : NI_Sve_ConvertVectorToMask;
            Assert.That(converted.HWIntrinsicId, Is.EqualTo(expectedConversion));
            if (Configuration.TARGET_ARM64)
            {
                Assert.That(converted.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Sve_ConversionTrueMask));
            }
        });
    }

    [Test]
    public static void SimdLowerAndUpperPreserveTargetSupportBoundaries()
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.FEATURE_SIMD ||
            !(Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 || Configuration.TARGET_WASM))
        {
            Assert.Ignore("SIMD hardware intrinsics are unsupported on this target.");
        }

        WithCompiler(compiler =>
        {
            if (Configuration.TARGET_WASM)
            {
                var input = compiler.gtNewVconNode(TYP_SIMD16);
                var lowerFailure = Assert.Throws<FatalJitException>(() =>
                {
                    _ = compiler.gtNewSimdGetLowerNode(TYP_SIMD8, input, TYP_INT, 16);
                });
                Assert.That(lowerFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));

                var upperFailure = Assert.Throws<FatalJitException>(() =>
                {
                    _ = compiler.gtNewSimdGetUpperNode(TYP_SIMD8, input, TYP_INT, 16);
                });
                Assert.That(upperFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
                return;
            }

            var size = Configuration.TARGET_XARCH ? (byte)32 : (byte)16;
            var inputType = Configuration.TARGET_XARCH ? TYP_SIMD32 : TYP_SIMD16;
            var resultType = Configuration.TARGET_XARCH ? TYP_SIMD16 : TYP_SIMD8;
            var source = compiler.gtNewVconNode(inputType);
            var lower = compiler.gtNewSimdGetLowerNode(resultType, source, TYP_INT, size).AsHWIntrinsic();
            var upper = compiler.gtNewSimdGetUpperNode(resultType, source, TYP_INT, size).AsHWIntrinsic();

            Assert.That(lower.HWIntrinsicId, Is.EqualTo(NI_Vector_GetLower));
            Assert.That(upper.HWIntrinsicId, Is.EqualTo(NI_Vector_GetUpper));
            Assert.That(lower.Type, Is.EqualTo(resultType));
            Assert.That(upper.Type, Is.EqualTo(resultType));
        });
    }

    [Test]
    public static void SimdConstructionFailsExplicitlyWhenHardwareIntrinsicsAreUnavailable()
    {
        if (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 || Configuration.TARGET_WASM)
        {
            Assert.Ignore("SIMD construction is supported on this target.");
        }

        WithCompiler(compiler =>
        {
            var left = new GenTreeLclVar(TYP_SIMD16, 0);
            var right = new GenTreeLclVar(TYP_SIMD16, 1);
            var failure = Assert.Throws<FatalJitException>(() =>
                compiler.gtNewSimdNarrowNode(TYP_SIMD16, left, right, TYP_INT, 16));
            Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_IMPLLIMITATION));

            Assert.That(compiler.IsValidForShuffle(right, 16, TYP_INT, out var canBecomeValid, false), Is.False);
            Assert.That(canBecomeValid, Is.False);
            var constantShuffleFailure = Assert.Throws<FatalJitException>(() =>
                compiler.gtNewSimdShuffleNode(TYP_SIMD16, left, right, TYP_INT, 16, false));
            Assert.That(constantShuffleFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_IMPLLIMITATION));

            var variableShuffleFailure = Assert.Throws<FatalJitException>(() =>
                compiler.gtNewSimdShuffleVariableNode(TYP_SIMD16, left, right, TYP_INT, 16, false));
            Assert.That(variableShuffleFailure?.Result, Is.EqualTo(CorJitResult.CORJIT_IMPLLIMITATION));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SimdMinMaxUsesTheTargetIntrinsicOrFailsExplicitly(bool isMax)
    {
        if (!Configuration.FEATURE_HW_INTRINSICS || !Configuration.FEATURE_SIMD)
        {
            Assert.Ignore("SIMD hardware intrinsics are unsupported on this target.");
        }

        WithCompiler(compiler =>
        {
            var op1 = new GenTreeLclVar(TYP_SIMD16, 0);
            var op2 = new GenTreeLclVar(TYP_SIMD16, 1);

            if (!(Configuration.TARGET_XARCH || Configuration.TARGET_ARM64 || Configuration.TARGET_WASM))
            {
                var failure = Assert.Throws<FatalJitException>(() =>
                {
                    _ = compiler.gtNewSimdMinMaxNativeNode(TYP_SIMD16, op1, op2, TYP_INT, 16, isMax);
                });
                Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_IMPLLIMITATION));
                return;
            }

            var expectedIntrinsic = Configuration.TARGET_XARCH
                ? (isMax ? NI_X86Base_Max : NI_X86Base_Min)
                : Configuration.TARGET_ARM64
                    ? (isMax ? NI_AdvSimd_Max : NI_AdvSimd_Min)
                    : (isMax ? NI_PackedSimd_Max : NI_PackedSimd_Min);

            var nativeResult = compiler.gtNewSimdMinMaxNativeNode(TYP_SIMD16, op1, op2, TYP_INT, 16, isMax)
                .AsHWIntrinsic();
            Assert.That(nativeResult.HWIntrinsicId, Is.EqualTo(expectedIntrinsic));

            var result = compiler.gtNewSimdMinMaxNode(
                TYP_SIMD16, new GenTreeLclVar(TYP_SIMD16, 0), new GenTreeLclVar(TYP_SIMD16, 1),
                TYP_INT, 16, isMax, isMagnitude: false, isNumber: false).AsHWIntrinsic();
            Assert.That(result.HWIntrinsicId, Is.EqualTo(expectedIntrinsic));

            var expectedFloatIntrinsic = Configuration.TARGET_XARCH
                ? (isMax ? NI_X86Base_Max : NI_X86Base_Min)
                : Configuration.TARGET_ARM64
                    ? (isMax ? NI_AdvSimd_Max : NI_AdvSimd_Min)
                    : (isMax ? NI_PackedSimd_PseudoMax : NI_PackedSimd_PseudoMin);
            var floatNativeResult = compiler.gtNewSimdMinMaxNativeNode(
                TYP_SIMD16, new GenTreeLclVar(TYP_SIMD16, 2), new GenTreeLclVar(TYP_SIMD16, 3),
                TYP_FLOAT, 16, isMax).AsHWIntrinsic();
            Assert.That(floatNativeResult.HWIntrinsicId, Is.EqualTo(expectedFloatIntrinsic));

            var floatResult = compiler.gtNewSimdMinMaxNode(
                TYP_SIMD16, compiler.gtNewVconNode(TYP_SIMD16), compiler.gtNewVconNode(TYP_SIMD16),
                TYP_FLOAT, 16, isMax, isMagnitude: false, isNumber: false);
            Assert.That(floatResult.Type, Is.EqualTo(TYP_SIMD16));
            if (Configuration.TARGET_ARM64)
            {
                Assert.That(floatResult.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(expectedFloatIntrinsic));
            }
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif

        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
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
