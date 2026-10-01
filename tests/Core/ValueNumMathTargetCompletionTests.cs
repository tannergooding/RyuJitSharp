// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using ValueNum = System.Int32;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;
using static RyuJitSharp.VNFunc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumMathTargetCompletionTests
{
    [TestCase(NI_System_Math_Abs, VNF_Abs)]
    [TestCase(NI_System_Math_Acos, VNF_Acos)]
    [TestCase(NI_System_Math_Acosh, VNF_Acosh)]
    [TestCase(NI_System_Math_Asin, VNF_Asin)]
    [TestCase(NI_System_Math_Asinh, VNF_Asinh)]
    [TestCase(NI_System_Math_Atan, VNF_Atan)]
    [TestCase(NI_System_Math_Atanh, VNF_Atanh)]
    [TestCase(NI_System_Math_Cbrt, VNF_Cbrt)]
    [TestCase(NI_System_Math_Ceiling, VNF_Ceiling)]
    [TestCase(NI_System_Math_Cos, VNF_Cos)]
    [TestCase(NI_System_Math_Cosh, VNF_Cosh)]
    [TestCase(NI_System_Math_Exp, VNF_Exp)]
    [TestCase(NI_System_Math_Floor, VNF_Floor)]
    [TestCase(NI_System_Math_ILogB, VNF_ILogB)]
    [TestCase(NI_System_Math_Log, VNF_Log)]
    [TestCase(NI_System_Math_Log2, VNF_Log2)]
    [TestCase(NI_System_Math_Log10, VNF_Log10)]
    [TestCase(NI_System_Math_Round, VNF_RoundDouble)]
    [TestCase(NI_System_Math_Sin, VNF_Sin)]
    [TestCase(NI_System_Math_Sinh, VNF_Sinh)]
    [TestCase(NI_System_Math_Sqrt, VNF_Sqrt)]
    [TestCase(NI_System_Math_Tan, VNF_Tan)]
    [TestCase(NI_System_Math_Tanh, VNF_Tanh)]
    [TestCase(NI_System_Math_Truncate, VNF_Truncate)]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, VNF_LeadingZeroCount)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, VNF_TrailingZeroCount)]
    [TestCase(NI_PRIMITIVE_PopCount, VNF_PopCount)]
    public static void EveryUnarySymbolicMappingRetainsItsTypeOperandAndInterning(
        NamedIntrinsic intrinsic, VNFunc expected)
    {
        WithStore(store =>
        {
            var counting = intrinsic is NI_PRIMITIVE_LeadingZeroCount
                or NI_PRIMITIVE_TrailingZeroCount or NI_PRIMITIVE_PopCount;
            var_types[] sourceTypes = counting ? [TYP_INT, TYP_LONG] : [TYP_FLOAT, TYP_DOUBLE];
            foreach (var sourceType in sourceTypes)
            {
                var argument = store.VNForExpr(null, sourceType);
                var resultType = intrinsic == NI_System_Math_ILogB ? TYP_INT : sourceType;
                var func = intrinsic == NI_System_Math_Round && sourceType == TYP_FLOAT ? VNF_RoundSingle : expected;
                var result = store.EvalMathFuncUnary(resultType, intrinsic, argument);

                AssertUnary(store, result, resultType, func, argument);
                if (intrinsic == NI_System_Math_Round)
                {
                    var integer = store.EvalMathFuncUnary(TYP_INT, intrinsic, argument);

                    AssertUnary(store, integer, TYP_INT, VNF_RoundInt32, argument);
                }
            }
        });
    }

    [TestCase(NI_PRIMITIVE_LeadingZeroCount, 0L, 32, 64)]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, 1L, 31, 63)]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, -1L, 0, 0)]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, long.MinValue, 32, 0)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, 0L, 32, 64)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, 8L, 3, 3)]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, -1L, 0, 0)]
    [TestCase(NI_PRIMITIVE_PopCount, 0L, 0, 0)]
    [TestCase(NI_PRIMITIVE_PopCount, -1L, 32, 64)]
    [TestCase(NI_PRIMITIVE_PopCount, long.MinValue, 0, 1)]
    public static void BitCountingRetainsUnsignedInputWidthAndBothNativeResultTypes(
        NamedIntrinsic intrinsic, long input, int narrow, int wide)
    {
        WithStore(store =>
        {
            foreach (var sourceType in new[] { TYP_INT, TYP_LONG })
            {
                var argument = sourceType == TYP_LONG
                    ? store.VNForLongCon(input)
                    : store.VNForIntCon(unchecked((int)input));
                var expected = sourceType == TYP_LONG ? wide : narrow;
                foreach (var resultType in new[] { TYP_INT, TYP_LONG })
                {
                    var result = store.EvalMathFuncUnary(resultType, intrinsic, argument);

                    Assert.That(store.TypeOfVN(result), Is.EqualTo(resultType));
                    Assert.That(store.GetConstantInt64(result), Is.EqualTo((long)expected));
                }
            }
        });
    }

    [TestCase(0.5, 0UL)]
    [TestCase(-0.5, 0x8000000000000000UL)]
    [TestCase(1.5, 0x4000000000000000UL)]
    [TestCase(2.5, 0x4000000000000000UL)]
    [TestCase(-2.5, 0xC000000000000000UL)]
    [TestCase(4503599627370496.0, 0x4330000000000000UL)]
    [TestCase(double.PositiveInfinity, 0x7FF0000000000000UL)]
    public static void DoubleRoundRetainsEvenMidpointsSignedZeroAndIntegerBoundary(double input, ulong bits)
    {
        WithStore(store =>
        {
            var result = store.EvalMathFuncUnary(TYP_DOUBLE, NI_System_Math_Round, store.VNForDoubleCon(input));

            Assert.That(BitConverter.DoubleToUInt64Bits(store.GetConstantDouble(result)), Is.EqualTo(bits));
        });
    }

    [TestCase(0.5f, 0U)]
    [TestCase(-0.5f, 0x80000000U)]
    [TestCase(1.5f, 0x40000000U)]
    [TestCase(2.5f, 0x40000000U)]
    [TestCase(-2.5f, 0xC0000000U)]
    [TestCase(8388608.0f, 0x4B000000U)]
    [TestCase(float.PositiveInfinity, 0x7F800000U)]
    public static void SingleRoundRetainsEvenMidpointsSignedZeroAndIntegerBoundary(float input, uint bits)
    {
        WithStore(store =>
        {
            var result = store.EvalMathFuncUnary(TYP_FLOAT, NI_System_Math_Round, store.VNForFloatCon(input));

            Assert.That(BitConverter.SingleToUInt32Bits(store.GetConstantSingle(result)), Is.EqualTo(bits));
        });
    }

    [TestCase(0.5, 0)]
    [TestCase(-0.5, 0)]
    [TestCase(1.5, 2)]
    [TestCase(2.5, 2)]
    [TestCase(-2.5, -2)]
    [TestCase(2147483520.0, 2147483520)]
    [TestCase(-2147483648.0, int.MinValue)]
    public static void IntegerRoundRetainsItsNativeValidDomainConversion(double input, int expected)
    {
        WithStore(store =>
        {
            var fromDouble = store.EvalMathFuncUnary(TYP_INT, NI_System_Math_Round, store.VNForDoubleCon(input));
            var fromSingle = store.EvalMathFuncUnary(TYP_INT, NI_System_Math_Round, store.VNForFloatCon((float)input));

            Assert.That(store.GetConstantInt32(fromDouble), Is.EqualTo(expected));
            Assert.That(store.GetConstantInt32(fromSingle), Is.EqualTo(expected));
        });
    }

    [TestCase(false, 0.0, int.MinValue)]
    [TestCase(true, 0.0, int.MinValue)]
    [TestCase(false, double.NaN, int.MaxValue)]
    [TestCase(true, double.NaN, int.MaxValue)]
    [TestCase(false, double.PositiveInfinity, int.MaxValue)]
    [TestCase(true, double.PositiveInfinity, int.MaxValue)]
    [TestCase(false, double.Epsilon, -1074)]
    [TestCase(true, (double)float.Epsilon, -149)]
    [TestCase(false, 1.0, 0)]
    [TestCase(true, 1.0, 0)]
    public static void ILogBRetainsNativeIntegerSentinelsAndSubnormalExponents(bool single, double input, int expected)
    {
        WithStore(store =>
        {
            var argument = single ? store.VNForFloatCon((float)input) : store.VNForDoubleCon(input);
            var result = store.EvalMathFuncUnary(TYP_INT, NI_System_Math_ILogB, argument);

            Assert.That(store.TypeOfVN(result), Is.EqualTo(TYP_INT));
            Assert.That(store.GetConstantInt32(result), Is.EqualTo(expected));
        });
    }

    [TestCase(NI_System_Math_Sin, VNF_Sin)]
    [TestCase(NI_System_Math_Acos, VNF_Acos)]
    [TestCase(NI_System_Math_ILogB, VNF_ILogB)]
    public static void AotRetainsCallImplementedUnaryFunctions(NamedIntrinsic intrinsic, VNFunc func)
    {
        WithStore(store =>
        {
            foreach (var sourceType in new[] { TYP_FLOAT, TYP_DOUBLE })
            {
                var argument = sourceType == TYP_FLOAT ? store.VNForFloatCon(0.5f) : store.VNForDoubleCon(0.5);
                var type = intrinsic == NI_System_Math_ILogB ? TYP_INT : sourceType;
                var result = store.EvalMathFuncUnary(type, intrinsic, argument);

                AssertUnary(store, result, type, func, argument);
            }
        }, aot: true);
    }

    [Test]
    public static void AotSquareRootUsesTheExistingTargetIntrinsicPredicate()
    {
        WithStore(store =>
        {
            var argument = store.VNForDoubleCon(4);
            var result = store.EvalMathFuncUnary(TYP_DOUBLE, NI_System_Math_Sqrt, argument);
#if TARGET_XARCH || TARGET_ARM || TARGET_ARM64 || TARGET_RISCV64 || TARGET_WASM
            Assert.That(store.GetConstantDouble(result), Is.EqualTo(2.0));
#else
            AssertUnary(store, result, TYP_DOUBLE, VNF_Sqrt, argument);
#endif
        }, aot: true);
    }

    [TestCase(NI_System_Math_Atan2, VNF_Atan2)]
    [TestCase(NI_System_Math_Pow, VNF_Pow)]
    public static void AotBinaryCallsAndNonconstantInputsRetainOrderedArguments(NamedIntrinsic intrinsic, VNFunc func)
    {
        WithStore(store =>
        {
            var left = store.VNForDoubleCon(2);
            var right = store.VNForDoubleCon(3);
            var result = store.EvalMathFuncBinary(TYP_DOUBLE, intrinsic, left, right);

            AssertBinary(store, result, TYP_DOUBLE, func, left, right);
        }, aot: true);
        WithStore(store =>
        {
            var left = store.VNForExpr(null, TYP_DOUBLE);
            var right = store.VNForExpr(null, TYP_DOUBLE);
            var result = store.EvalMathFuncBinary(TYP_DOUBLE, intrinsic, left, right);

            AssertBinary(store, result, TYP_DOUBLE, func, left, right);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BinaryFoldsRetainSignedZeroAndExactPower(bool single)
    {
        WithStore(store =>
        {
            var type = single ? TYP_FLOAT : TYP_DOUBLE;
            var negativeZero = single ? store.VNForFloatCon(-0.0f) : store.VNForDoubleCon(-0.0);
            var one = single ? store.VNForFloatCon(1) : store.VNForDoubleCon(1);
            var zero = store.EvalMathFuncBinary(type, NI_System_Math_Atan2, negativeZero, one);
            var first = single ? store.VNForFloatCon(2) : store.VNForDoubleCon(2);
            var second = single ? store.VNForFloatCon(11) : store.VNForDoubleCon(11);
            var power = store.EvalMathFuncBinary(type, NI_System_Math_Pow, first, second);

            Assert.That(zero, Is.EqualTo(negativeZero));
            Assert.That(single ? store.GetConstantSingle(power) : store.GetConstantDouble(power), Is.EqualTo(2048.0));
        });
    }

#if TARGET_ARM || TARGET_ARM64
    [TestCase(false)]
    [TestCase(true)]
    public static void ArmPowerOneRetainsSubnormalSignedZeroAndNaNPayloadIdentity(bool single)
    {
        WithStore(store =>
        {
            var type = single ? TYP_FLOAT : TYP_DOUBLE;
            var one = single ? store.VNForFloatCon(1) : store.VNForDoubleCon(1);
            ValueNum[] values = single
                ? [store.VNForFloatCon(float.Epsilon), store.VNForFloatCon(-float.Epsilon),
                    store.VNForFloatCon(-0.0f), store.VNForFloatCon(BitConverter.UInt32BitsToSingle(0x7F812345))]
                : [store.VNForDoubleCon(double.Epsilon), store.VNForDoubleCon(-double.Epsilon),
                    store.VNForDoubleCon(-0.0), store.VNForDoubleCon(BitConverter.UInt64BitsToDouble(0x7FF0123456789ABC))];
            foreach (var value in values)
            {
                var result = store.EvalMathFuncBinary(type, NI_System_Math_Pow, value, one);

                Assert.That(result, Is.EqualTo(value));
            }
        });
    }
#endif

#if TARGET_RISCV64 || TARGET_WASM
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void NativeMinMaxRetainsTargetNaNPolicySignedZeroAndPayloads(bool single, bool maximum)
    {
        WithStore(store =>
        {
            var type = single ? TYP_FLOAT : TYP_DOUBLE;
            var negativeZero = single ? store.VNForFloatCon(-0.0f) : store.VNForDoubleCon(-0.0);
            var positiveZero = store.VNZeroForType(type);
            var firstNaN = single
                ? store.VNForFloatCon(BitConverter.UInt32BitsToSingle(0x7FC12345))
                : store.VNForDoubleCon(BitConverter.UInt64BitsToDouble(0x7FF8123456789ABC));
            var secondNaN = single
                ? store.VNForFloatCon(BitConverter.UInt32BitsToSingle(0xFFC54321))
                : store.VNForDoubleCon(BitConverter.UInt64BitsToDouble(0xFFF876543210ABCD));
            var number = single ? store.VNForFloatCon(3) : store.VNForDoubleCon(3);
#if TARGET_WASM
            var leftNaNExpected = firstNaN;
            var rightNaNExpected = secondNaN;
#else
            var leftNaNExpected = number;
            var rightNaNExpected = number;
#endif
            var cases = new[]
            {
                (Left: negativeZero, Right: positiveZero, Expected: maximum ? positiveZero : negativeZero),
                (Left: positiveZero, Right: negativeZero, Expected: maximum ? positiveZero : negativeZero),
                (Left: firstNaN, Right: number, Expected: leftNaNExpected),
                (Left: number, Right: secondNaN, Expected: rightNaNExpected),
                (Left: firstNaN, Right: secondNaN, Expected: firstNaN),
            };
            var intrinsic = maximum ? NI_System_Math_MaxNative : NI_System_Math_MinNative;
            foreach (var item in cases)
            {
                var result = store.EvalMathFuncBinary(type, intrinsic, item.Left, item.Right);

                Assert.That(result, Is.EqualTo(item.Expected));
            }
        }, aot: true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NativeMinMaxSymbolicFunctionsRetainTheTargetPolicy(bool maximum)
    {
        WithStore(store =>
        {
            var left = store.VNForExpr(null, TYP_DOUBLE);
            var right = store.VNForExpr(null, TYP_DOUBLE);
            var intrinsic = maximum ? NI_System_Math_MaxNative : NI_System_Math_MinNative;
#if TARGET_WASM
            var func = maximum ? VNF_Max : VNF_Min;
#else
            var func = maximum ? VNF_MaxNumber : VNF_MinNumber;
#endif
            var result = store.EvalMathFuncBinary(TYP_DOUBLE, intrinsic, left, right);

            AssertBinary(store, result, TYP_DOUBLE, func, left, right);
        });
    }
#endif

#if TARGET_RISCV64
    [TestCase(NI_System_Math_Min, false, -1L, 1L, -1L)]
    [TestCase(NI_System_Math_Max, false, -1L, 1L, 1L)]
    [TestCase(NI_System_Math_MinUnsigned, false, -1L, 1L, 1L)]
    [TestCase(NI_System_Math_MaxUnsigned, false, -1L, 1L, -1L)]
    [TestCase(NI_System_Math_Min, true, long.MinValue, 1L, long.MinValue)]
    [TestCase(NI_System_Math_Max, true, long.MinValue, 1L, 1L)]
    [TestCase(NI_System_Math_MinUnsigned, true, long.MinValue, 1L, 1L)]
    [TestCase(NI_System_Math_MaxUnsigned, true, long.MinValue, 1L, long.MinValue)]
    public static void RiscVIntegerMinMaxRetainsSignedUnsignedWidthsAndInterning(
        NamedIntrinsic intrinsic, bool wide, long first, long second, long expected)
    {
        WithStore(store =>
        {
            var type = wide ? TYP_LONG : TYP_INT;
            var left = wide ? store.VNForLongCon(first) : store.VNForIntCon((int)first);
            var right = wide ? store.VNForLongCon(second) : store.VNForIntCon((int)second);
            var result = store.EvalMathFuncBinary(type, intrinsic, left, right);
            var expectedVN = wide ? store.VNForLongCon(expected) : store.VNForIntCon((int)expected);

            Assert.That(result, Is.EqualTo(expectedVN));
        });
    }

    [TestCase(NI_System_Math_Min, VNF_MinInt)]
    [TestCase(NI_System_Math_Max, VNF_MaxInt)]
    [TestCase(NI_System_Math_MinUnsigned, VNF_MinInt_UN)]
    [TestCase(NI_System_Math_MaxUnsigned, VNF_MaxInt_UN)]
    public static void RiscVIntegerMinMaxSymbolicFunctionsRetainSignedness(NamedIntrinsic intrinsic, VNFunc func)
    {
        WithStore(store =>
        {
            var left = store.VNForExpr(null, TYP_LONG);
            var right = store.VNForExpr(null, TYP_LONG);
            var result = store.EvalMathFuncBinary(TYP_LONG, intrinsic, left, right);

            AssertBinary(store, result, TYP_LONG, func, left, right);
        });
    }
#endif

    private static void AssertUnary(ValueNumStore store, ValueNum result, var_types type, VNFunc func, ValueNum argument)
    {
        var app = new VNFuncApp();
        Assert.That(store.GetVNFunc(result, ref app), Is.True);
        Assert.That(store.TypeOfVN(result), Is.EqualTo(type));
        Assert.That(app.Func, Is.EqualTo(func));
        Assert.That(app.GetArg(0), Is.EqualTo(argument));
        Assert.That(result, Is.EqualTo(store.VNForFunc(type, func, argument)));
    }

    private static void AssertBinary(ValueNumStore store, ValueNum result, var_types type, VNFunc func,
        ValueNum left, ValueNum right)
    {
        var app = new VNFuncApp();
        Assert.That(store.GetVNFunc(result, ref app), Is.True);
        Assert.That(store.TypeOfVN(result), Is.EqualTo(type));
        Assert.That(app.Func, Is.EqualTo(func));
        Assert.That(app.GetArg(0), Is.EqualTo(left));
        Assert.That(app.GetArg(1), Is.EqualTo(right));
        Assert.That(result, Is.EqualTo(store.VNForFunc(type, func, left, right)));
    }

    private static void WithStore(Action<ValueNumStore> action, bool aot = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        if (aot)
        {
            flags.Set(JitFlags.JIT_FLAG_AOT);
        }
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            action(new ValueNumStore(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
