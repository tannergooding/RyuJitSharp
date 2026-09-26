// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ValueNumBinaryFoldingTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "VNEvalCanFoldBinaryFunc")]
    private static extern bool CanFold(ValueNumStore store, var_types type, VNFunc func, int left, int right);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "VNEvalShouldFold")]
    private static extern bool ShouldFold(ValueNumStore store, var_types type, VNFunc func, int left, int right);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EvalFuncForConstantArgs")]
    private static extern int Evaluate(ValueNumStore store, var_types type, VNFunc func, int left, int right);

    private static int Fold(ValueNumStore store, var_types type, VNFunc func, int left, int right)
    {
        Assert.That(CanFold(store, type, func, left, right), Is.True);
        Assert.That(ShouldFold(store, type, func, left, right), Is.True);
        return Evaluate(store, type, func, left, right);
    }

    [TestCase(VNFunc.VNF_ADD, false)]
    [TestCase(VNFunc.VNF_ADD_OVF, false)]
    [TestCase(VNFunc.VNF_GT_UN, false)]
    [TestCase(VNFunc.VNF_EQ, true)]
    [TestCase(VNFunc.VNF_NE, true)]
    [TestCase(VNFunc.VNF_Cast, true)]
    [TestCase(VNFunc.VNF_CastOvf, true)]
    [TestCase(VNFunc.VNF_BitCast, false)]
    public static void RelocatableHandlesExcludeOnlyNativePermittedOperations(VNFunc func, bool comparisonOrCastAllowed)
    {
        WithStore((store, compiler) => {
            compiler.opts.compReloc = true;
            var handle = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            var right = func is VNFunc.VNF_Cast or VNFunc.VNF_CastOvf
                ? store.VNForCastOper(TYP_LONG, false) : store.VNForIntCon(1);
            var type = func is VNFunc.VNF_Cast or VNFunc.VNF_CastOvf ? TYP_I_IMPL : TYP_INT;
            Assert.That(CanFold(store, type, func, handle, right), Is.EqualTo(comparisonOrCastAllowed));

            if (func is VNFunc.VNF_Cast or VNFunc.VNF_CastOvf)
            {
                Assert.That(CanFold(store, TYP_INT, func, handle, right), Is.False);
            }
        });
    }

    [Test]
    public static void EligibilityRejectsNonconstantUnsupportedMixedAndByrefResults()
    {
        WithStore((store, _) => {
            var integer = store.VNForIntCon(1);
            var floating = store.VNForFloatCon(1.0f);
            var symbolic = store.VNForExpr(null, TYP_INT);
            var castInfo = store.VNForCastOper(TYP_INT, false);
            Assert.That(CanFold(store, TYP_INT, VNFunc.VNF_ADD, integer, symbolic), Is.False);
            Assert.That(CanFold(store, TYP_FLOAT, VNFunc.VNF_ADD, integer, floating), Is.False);
            Assert.That(CanFold(store, TYP_INT, VNFunc.VNF_Cast, floating, castInfo), Is.True);
            Assert.That(CanFold(store, TYP_INT, VNFunc.VNF_BitCast, floating, castInfo), Is.True);
            Assert.That(CanFold(store, TYP_REF, VNFunc.VNF_BitCast, floating, castInfo), Is.False);
            Assert.That(CanFold(store, TYP_BYREF, VNFunc.VNF_ADD, integer, integer), Is.False);
            Assert.That(CanFold(store, TYP_INT, VNFunc.VNF_NEG, integer, integer), Is.False);
            Assert.That(CanFold(store, TYP_INT, VNFunc.VNF_MemOpaque, integer, integer), Is.False);
        });
    }

    [TestCase(VNFunc.VNF_DIV, int.MinValue, -1, false)]
    [TestCase(VNFunc.VNF_MOD, int.MinValue, -1, false)]
    [TestCase(VNFunc.VNF_DIV, 1, 0, false)]
    [TestCase(VNFunc.VNF_UDIV, 1, 0, false)]
    [TestCase(VNFunc.VNF_UMOD, 1, 0, false)]
    [TestCase(VNFunc.VNF_DIV, int.MinValue, 1, true)]
    [TestCase(VNFunc.VNF_UDIV, int.MinValue, -1, true)]
    [TestCase(VNFunc.VNF_MOD, -7, 3, true)]
    [TestCase(VNFunc.VNF_ADD_OVF, int.MaxValue, 1, false)]
    [TestCase(VNFunc.VNF_SUB_OVF, int.MinValue, 1, false)]
    [TestCase(VNFunc.VNF_MUL_OVF, int.MaxValue, 2, false)]
    [TestCase(VNFunc.VNF_ADD_UN_OVF, -1, 1, false)]
    [TestCase(VNFunc.VNF_SUB_UN_OVF, 0, 1, false)]
    [TestCase(VNFunc.VNF_MUL_UN_OVF, -1, 2, false)]
    [TestCase(VNFunc.VNF_ADD_UN_OVF, -2, 1, true)]
    public static void IntegerSafetyExcludesTrapsAndOverflow(VNFunc func, int left, int right, bool safe)
    {
        WithStore((store, _) => {
            var lhs = store.VNForIntCon(left);
            var rhs = store.VNForIntCon(right);
            Assert.That(CanFold(store, TYP_INT, func, lhs, rhs), Is.True);
            Assert.That(ShouldFold(store, TYP_INT, func, lhs, rhs), Is.EqualTo(safe));
        });
    }

    [TestCase(VNFunc.VNF_DIV, long.MinValue, -1L, false)]
    [TestCase(VNFunc.VNF_MOD, long.MinValue, -1L, false)]
    [TestCase(VNFunc.VNF_DIV, long.MinValue, 1L, true)]
    [TestCase(VNFunc.VNF_DIV, 1L, 0L, false)]
    [TestCase(VNFunc.VNF_ADD_OVF, long.MaxValue, 1L, false)]
    [TestCase(VNFunc.VNF_SUB_UN_OVF, 0L, 1L, false)]
    [TestCase(VNFunc.VNF_MUL_UN_OVF, -1L, 2L, false)]
    [TestCase(VNFunc.VNF_MUL_OVF, 3037000499L, 3037000499L, true)]
    public static void LongSafetyUsesFullNativeWidth(VNFunc func, long left, long right, bool safe)
    {
        WithStore((store, _) => {
            var lhs = store.VNForLongCon(left);
            var rhs = store.VNForLongCon(right);
            Assert.That(ShouldFold(store, TYP_LONG, func, lhs, rhs), Is.EqualTo(safe));
            if (func is VNFunc.VNF_DIV or VNFunc.VNF_MOD)
            {
                Assert.That(ShouldFold(store, TYP_INT, func, lhs, rhs), Is.False);
            }
        });
    }

    [Test]
    public static void FloatingDivisionDoesNotUseIntegerTrapGuard()
    {
#if TARGET_XARCH
        const long expectedNaN = unchecked((long)0xFFF8000000000000);
#else
        const long expectedNaN = 0x7FF8000000000000;
#endif
        WithStore((store, _) => {
            var zero = store.VNForDoubleCon(0.0);
            var result = Fold(store, TYP_DOUBLE, VNFunc.VNF_DIV, zero, zero);
            Assert.That(BitConverter.DoubleToInt64Bits(store.GetConstantDouble(result)),
                Is.EqualTo(expectedNaN));
        });
    }

    [TestCase(TYP_UBYTE, -0.5, false, true)]
    [TestCase(TYP_UINT, 4294967295.0, false, true)]
    [TestCase(TYP_UINT, 4294967296.0, false, false)]
    [TestCase(TYP_INT, double.NaN, false, false)]
    [TestCase(TYP_INT, double.PositiveInfinity, false, false)]
    [TestCase(TYP_LONG, -9223372036854775808.0, false, true)]
    [TestCase(TYP_LONG, 9223372036854775808.0, false, false)]
    public static void FloatingCastSafetyUsesNativeCheckedOpsBounds(var_types castType, double value,
        bool sourceUnsigned, bool expected)
    {
        WithStore((store, _) => {
            var source = store.VNForDoubleCon(value);
            var info = store.VNForCastOper(castType, sourceUnsigned);
            var resultType = castType is TYP_LONG or TYP_ULONG ? TYP_LONG : TYP_INT;
            Assert.That(ShouldFold(store, resultType, VNFunc.VNF_Cast, source, info), Is.EqualTo(expected));
            Assert.That(ShouldFold(store, resultType, VNFunc.VNF_CastOvf, source, info), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void CastSafetyDistinguishesSignedAndUnsignedSource()
    {
        WithStore((store, _) => {
            var source = store.VNForIntCon(-1);
            var signedInfo = store.VNForCastOper(TYP_UBYTE, false);
            var unsignedInfo = store.VNForCastOper(TYP_UBYTE, true);
            Assert.That(ShouldFold(store, TYP_INT, VNFunc.VNF_CastOvf, source, signedInfo), Is.False);
            Assert.That(ShouldFold(store, TYP_INT, VNFunc.VNF_CastOvf, source, unsignedInfo), Is.False);
            Assert.That(ShouldFold(store, TYP_INT, VNFunc.VNF_Cast, source, signedInfo), Is.True);

            var unsigned32 = store.VNForCastOper(TYP_UINT, true);
            Assert.That(ShouldFold(store, TYP_INT, VNFunc.VNF_CastOvf, source, unsigned32), Is.True);
            Assert.That(store.GetConstantInt32(Fold(store, TYP_INT, VNFunc.VNF_CastOvf, source, unsigned32)),
                Is.EqualTo(-1));
        });
    }

    [TestCase(VNFunc.VNF_ADD, int.MaxValue, 1, int.MinValue)]
    [TestCase(VNFunc.VNF_UDIV, -1, 2, int.MaxValue)]
    [TestCase(VNFunc.VNF_ROL, 0x12345678, 8, 0x34567812)]
    [TestCase(VNFunc.VNF_LT_UN, -1, 1, 0)]
    [TestCase(VNFunc.VNF_GT, -1, 1, 0)]
    [TestCase(VNFunc.VNF_GT_UN, -1, 1, 1)]
    [TestCase(VNFunc.VNF_MUL_OVF, 46340, 46340, 2147395600)]
    public static void IntegerEvaluationUsesScalarPrimitives(VNFunc func, int left, int right, int expected)
    {
        WithStore((store, _) => {
            var result = Fold(store, TYP_INT, func, store.VNForIntCon(left), store.VNForIntCon(right));
            Assert.That(store.TypeOfVN(result), Is.EqualTo(TYP_INT));
            Assert.That(store.GetConstantInt32(result), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void LongAndMixedReferenceOperandsKeepNativeResultTypes()
    {
        WithStore((store, _) => {
            var left = store.VNForLongCon(long.MinValue);
            var right = store.VNForLongCon(1);
            var result = Fold(store, TYP_LONG, VNFunc.VNF_SUB, left, right);
            Assert.That(store.TypeOfVN(result), Is.EqualTo(TYP_LONG));
            Assert.That(store.GetConstantInt64(result), Is.EqualTo(long.MaxValue));

            var byref = store.VNForByrefCon(0x1000);
            var zero = ValueNumStore.VNForNull();
            var sameType = Fold(store, TYP_INT, VNFunc.VNF_EQ, byref, store.VNForByrefCon(0x1000));
            var mixed = Fold(store, TYP_INT, VNFunc.VNF_NE, byref, zero);
            Assert.That(store.GetConstantInt32(sameType), Is.EqualTo(1));
            Assert.That(store.GetConstantInt32(mixed), Is.EqualTo(1));
            Assert.That(store.GetConstantInt32(Fold(store, TYP_INT, VNFunc.VNF_OR, zero, byref)),
                Is.EqualTo(0x1000));
        });
    }

    [Test]
    public static void FloatResultsKeepSignedZeroAndPayloadAndComparisonsAreInts()
    {
        WithStore((store, _) => {
            var negativeZero = store.VNForFloatCon(BitConverter.Int32BitsToSingle(int.MinValue));
            var positiveZero = store.VNForFloatCon(0);
            var result = Fold(store, TYP_FLOAT, VNFunc.VNF_SUB, negativeZero, positiveZero);
            Assert.That(BitConverter.SingleToInt32Bits(store.GetConstantSingle(result)), Is.EqualTo(int.MinValue));

            var payload = store.VNForDoubleCon(BitConverter.Int64BitsToDouble(0x7FF8000000001234));
            var added = Fold(store, TYP_DOUBLE, VNFunc.VNF_ADD, payload, store.VNForDoubleCon(1));
            Assert.That(BitConverter.DoubleToInt64Bits(store.GetConstantDouble(added)), Is.EqualTo(0x7FF8000000001234));
            var unordered = Fold(store, TYP_INT, VNFunc.VNF_LT_UN, payload, store.VNForDoubleCon(1));
            Assert.That(store.GetConstantInt32(unordered), Is.EqualTo(1));
        });
    }

    [Test]
    public static void CastEvaluationPreservesSourceInterpretationAndNarrowing()
    {
        WithStore((store, _) => {
            var source = store.VNForIntCon(int.MinValue);
            var signed = store.VNForCastOper(TYP_LONG, false);
            var unsigned = store.VNForCastOper(TYP_LONG, true);
            Assert.That(store.GetConstantInt64(Fold(store, TYP_LONG, VNFunc.VNF_Cast, source, signed)),
                Is.EqualTo((long)int.MinValue));
            Assert.That(store.GetConstantInt64(Fold(store, TYP_LONG, VNFunc.VNF_Cast, source, unsigned)),
                Is.EqualTo(2147483648L));

            var narrow = store.VNForCastOper(TYP_UBYTE, false);
            var byteValue = Fold(store, TYP_UBYTE, VNFunc.VNF_Cast, store.VNForIntCon(-1), narrow);
            Assert.That(store.TypeOfVN(byteValue), Is.EqualTo(TYP_INT));
            Assert.That(store.GetConstantInt32(byteValue), Is.EqualTo(255));

            var fromLong = store.VNForLongCon(-1);
            var floatInfo = store.VNForCastOper(TYP_FLOAT, true);
            var result = Fold(store, TYP_FLOAT, VNFunc.VNF_Cast, fromLong, floatInfo);
            Assert.That(BitConverter.SingleToInt32Bits(store.GetConstantSingle(result)), Is.EqualTo(0x5F800000));
        });
    }

    [Test]
    public static void FloatingAndBitCastsPreserveNegativeZeroAndBitPatterns()
    {
        WithStore((store, _) => {
            var negativeZero = store.VNForDoubleCon(BitConverter.Int64BitsToDouble(long.MinValue));
            var toFloat = store.VNForCastOper(TYP_FLOAT, false);
            var narrowed = Fold(store, TYP_FLOAT, VNFunc.VNF_Cast, negativeZero, toFloat);
            Assert.That(BitConverter.SingleToInt32Bits(store.GetConstantSingle(narrowed)), Is.EqualTo(int.MinValue));

            var payload = store.VNForLongCon(0x7FF8000000001234L);
            var bitcast = Fold(store, TYP_DOUBLE, VNFunc.VNF_BitCast, payload, store.VNForIntCon(0));
            Assert.That(BitConverter.DoubleToInt64Bits(store.GetConstantDouble(bitcast)),
                Is.EqualTo(0x7FF8000000001234L));
            var back = Fold(store, TYP_LONG, VNFunc.VNF_BitCast, bitcast, store.VNForIntCon(0));
            Assert.That(store.GetConstantInt64(back), Is.EqualTo(0x7FF8000000001234L));

            var byteBits = Fold(store, TYP_BYTE, VNFunc.VNF_BitCast, store.VNForIntCon(255),
                store.VNForIntCon(0));
            Assert.That(store.GetConstantInt32(byteBits), Is.EqualTo(-1));

#if FEATURE_SIMD
            var vector = simd8_t.Zero;
            vector.u64[0] = 0x7FF8000000005678;
            var vectorVN = store.VNForSimd8Con(vector);
            var vectorBitcast = Fold(store, TYP_LONG, VNFunc.VNF_BitCast, vectorVN, store.VNForIntCon(0));
            Assert.That(store.GetConstantInt64(vectorBitcast), Is.EqualTo(0x7FF8000000005678L));
#endif
        });
    }

    private static void WithStore(Action<ValueNumStore, Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            action(new ValueNumStore(compiler), compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
