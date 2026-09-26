// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class ValueNumBinaryEvaluationTests
{
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "EvalOp")]
    private static extern T EvalOpAccessor<T>(ValueNumStore? _, VNFunc func, T left, T right)
        where T : unmanaged, INumber<T>;

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "EvalComparison")]
    private static extern int ComparisonAccessor<T>(ValueNumStore? _, VNFunc func, T left, T right)
        where T : unmanaged, INumber<T>;

    private static T EvalOp<T>(VNFunc func, T left, T right) where T : unmanaged, INumber<T>
        => EvalOpAccessor<T>(null, func, left, right);

    private static int Compare<T>(VNFunc func, T left, T right) where T : unmanaged, INumber<T>
        => ComparisonAccessor<T>(null, func, left, right);

    [TestCase(VNFunc.VNF_ADD, int.MaxValue, 1, int.MinValue)]
    [TestCase(VNFunc.VNF_SUB, int.MinValue, 1, int.MaxValue)]
    [TestCase(VNFunc.VNF_MUL, 65536, 65536, 0)]
    [TestCase(VNFunc.VNF_AND, -1, 0x12345678, 0x12345678)]
    [TestCase(VNFunc.VNF_OR, int.MinValue, 1, int.MinValue + 1)]
    [TestCase(VNFunc.VNF_XOR, -1, int.MinValue, int.MaxValue)]
    [TestCase(VNFunc.VNF_DIV, -7, 3, -2)]
    [TestCase(VNFunc.VNF_MOD, -7, 3, -1)]
    [TestCase(VNFunc.VNF_UDIV, -1, 2, int.MaxValue)]
    [TestCase(VNFunc.VNF_UMOD, -1, 2, 1)]
    [TestCase(VNFunc.VNF_LSH, 1, 31, int.MinValue)]
    [TestCase(VNFunc.VNF_RSH, int.MinValue, 31, -1)]
    [TestCase(VNFunc.VNF_RSZ, int.MinValue, 31, 1)]
    [TestCase(VNFunc.VNF_ROL, 0x12345678, 8, 0x34567812)]
    [TestCase(VNFunc.VNF_ROR, 0x12345678, 8, 0x78123456)]
    [TestCase(VNFunc.VNF_ADD_OVF, int.MaxValue - 1, 1, int.MaxValue)]
    [TestCase(VNFunc.VNF_ADD_UN_OVF, -2, 1, -1)]
    [TestCase(VNFunc.VNF_SUB_OVF, int.MinValue + 1, 1, int.MinValue)]
    [TestCase(VNFunc.VNF_SUB_UN_OVF, -1, 1, -2)]
    [TestCase(VNFunc.VNF_MUL_OVF, 46340, 46340, 2147395600)]
    [TestCase(VNFunc.VNF_MUL_UN_OVF, 65535, 65535, -131071)]
    public static void IntegerOperations(VNFunc func, int left, int right, int expected)
    {
        Assert.That(EvalOp(func, left, right), Is.EqualTo(expected));
    }

    [TestCase(VNFunc.VNF_ADD, long.MaxValue, 1L, long.MinValue)]
    [TestCase(VNFunc.VNF_SUB, long.MinValue, 1L, long.MaxValue)]
    [TestCase(VNFunc.VNF_MUL, 1L << 32, 1L << 32, 0L)]
    [TestCase(VNFunc.VNF_DIV, long.MinValue, 2L, long.MinValue / 2)]
    [TestCase(VNFunc.VNF_MOD, -7L, 3L, -1L)]
    [TestCase(VNFunc.VNF_UDIV, -1L, 2L, long.MaxValue)]
    [TestCase(VNFunc.VNF_UMOD, -1L, 2L, 1L)]
    [TestCase(VNFunc.VNF_LSH, 1L, 63L, long.MinValue)]
    [TestCase(VNFunc.VNF_LSH, 1L, 64L, 1L)]
    [TestCase(VNFunc.VNF_LSH, 1L, 65L, 2L)]
    [TestCase(VNFunc.VNF_RSH, long.MinValue, 63L, -1L)]
    [TestCase(VNFunc.VNF_RSH, long.MinValue, 64L, long.MinValue)]
    [TestCase(VNFunc.VNF_RSZ, long.MinValue, 63L, 1L)]
    [TestCase(VNFunc.VNF_ROL, 0x123456789ABCDEF0L, 8L, 0x3456789ABCDEF012L)]
    [TestCase(VNFunc.VNF_ROR, 0x123456789ABCDEF0L, 8L, unchecked((long)0xF0123456789ABCDE))]
    [TestCase(VNFunc.VNF_ADD_OVF, long.MaxValue - 1, 1L, long.MaxValue)]
    [TestCase(VNFunc.VNF_SUB_UN_OVF, -1L, 1L, -2L)]
    [TestCase(VNFunc.VNF_MUL_OVF, 3037000499L, 3037000499L, 9223372030926249001L)]
    public static void LongOperations(VNFunc func, long left, long right, long expected)
    {
        Assert.That(EvalOp(func, left, right), Is.EqualTo(expected));
    }

#if WINDOWS_AMD64_ABI
    // The 32-bit out-of-range cases record pinned-source expressions compiled with
    // MSVC /O2 on Windows x64, not a portable C++ guarantee for overshifts.
    [TestCase(VNFunc.VNF_LSH, 0, 0x12345678)]
    [TestCase(VNFunc.VNF_LSH, 31, 0)]
    [TestCase(VNFunc.VNF_LSH, 32, 0x12345678)]
    [TestCase(VNFunc.VNF_LSH, 33, 0x2468ACF0)]
    [TestCase(VNFunc.VNF_LSH, 63, 0)]
    [TestCase(VNFunc.VNF_LSH, 64, 0x12345678)]
    [TestCase(VNFunc.VNF_LSH, 65, 0x2468ACF0)]
    [TestCase(VNFunc.VNF_ROL, 0, 0x12345678)]
    [TestCase(VNFunc.VNF_ROL, 31, 0x091A2B3C)]
    [TestCase(VNFunc.VNF_ROL, 32, 0x12345678)]
    [TestCase(VNFunc.VNF_ROL, 33, 0x2468ACF0)]
    [TestCase(VNFunc.VNF_ROL, 63, 0x091A2B3C)]
    [TestCase(VNFunc.VNF_ROL, 64, 0x12345678)]
    [TestCase(VNFunc.VNF_ROL, 65, 0x2468ACF0)]
    [TestCase(VNFunc.VNF_ROL, -1, 0x091A2B3C)]
    [TestCase(VNFunc.VNF_ROR, 0, 0x12345678)]
    [TestCase(VNFunc.VNF_ROR, 32, 0x12345678)]
    [TestCase(VNFunc.VNF_ROR, -1, 0x2468ACF0)]
    public static void WindowsX64IntCountBoundaries(VNFunc func, int count, int expected)
    {
        Assert.That(EvalOp(func, 0x12345678, count), Is.EqualTo(expected));
    }

    [TestCase(VNFunc.VNF_RSH, 0, int.MinValue)]
    [TestCase(VNFunc.VNF_RSH, 31, -1)]
    [TestCase(VNFunc.VNF_RSH, 32, int.MinValue)]
    [TestCase(VNFunc.VNF_RSH, 33, -1073741824)]
    [TestCase(VNFunc.VNF_RSH, 63, -1)]
    [TestCase(VNFunc.VNF_RSH, 64, int.MinValue)]
    [TestCase(VNFunc.VNF_RSH, 65, -1073741824)]
    [TestCase(VNFunc.VNF_RSZ, 0, int.MinValue)]
    [TestCase(VNFunc.VNF_RSZ, 31, 1)]
    [TestCase(VNFunc.VNF_RSZ, 32, int.MinValue)]
    [TestCase(VNFunc.VNF_RSZ, 33, 1073741824)]
    [TestCase(VNFunc.VNF_RSZ, 63, 1)]
    [TestCase(VNFunc.VNF_RSZ, 64, int.MinValue)]
    [TestCase(VNFunc.VNF_RSZ, 65, 1073741824)]
    public static void WindowsX64IntRightShiftBoundaries(VNFunc func, int count, int expected)
    {
        Assert.That(EvalOp(func, int.MinValue, count), Is.EqualTo(expected));
    }

    [TestCase(VNFunc.VNF_LSH, 0L, 0x123456789ABCDEF0L)]
    [TestCase(VNFunc.VNF_LSH, 31L, 0x4D5E6F7800000000L)]
    [TestCase(VNFunc.VNF_LSH, 32L, unchecked((long)0x9ABCDEF000000000))]
    [TestCase(VNFunc.VNF_LSH, 33L, 0x3579BDE000000000L)]
    [TestCase(VNFunc.VNF_LSH, 63L, 0L)]
    [TestCase(VNFunc.VNF_LSH, 64L, 0x123456789ABCDEF0L)]
    [TestCase(VNFunc.VNF_LSH, 65L, 0x2468ACF13579BDE0L)]
    [TestCase(VNFunc.VNF_LSH, -1L, 0L)]
    [TestCase(VNFunc.VNF_ROL, 0L, 0x123456789ABCDEF0L)]
    [TestCase(VNFunc.VNF_ROL, 31L, 0x4D5E6F78091A2B3CL)]
    [TestCase(VNFunc.VNF_ROL, 32L, unchecked((long)0x9ABCDEF012345678))]
    [TestCase(VNFunc.VNF_ROL, 33L, 0x3579BDE02468ACF1L)]
    [TestCase(VNFunc.VNF_ROL, 63L, 0x091A2B3C4D5E6F78L)]
    [TestCase(VNFunc.VNF_ROL, 64L, 0x123456789ABCDEF0L)]
    [TestCase(VNFunc.VNF_ROL, 65L, 0x2468ACF13579BDE0L)]
    [TestCase(VNFunc.VNF_ROL, -1L, 0x091A2B3C4D5E6F78L)]
    [TestCase(VNFunc.VNF_ROR, 0L, 0x123456789ABCDEF0L)]
    [TestCase(VNFunc.VNF_ROR, 64L, 0x123456789ABCDEF0L)]
    [TestCase(VNFunc.VNF_ROR, -1L, 0x2468ACF13579BDE0L)]
    public static void WindowsX64LongCountBoundaries(VNFunc func, long count, long expected)
    {
        Assert.That(EvalOp(func, 0x123456789ABCDEF0L, count), Is.EqualTo(expected));
    }

    [TestCase(VNFunc.VNF_RSH, 0L, long.MinValue)]
    [TestCase(VNFunc.VNF_RSH, 31L, -4294967296L)]
    [TestCase(VNFunc.VNF_RSH, 32L, -2147483648L)]
    [TestCase(VNFunc.VNF_RSH, 33L, -1073741824L)]
    [TestCase(VNFunc.VNF_RSH, 63L, -1L)]
    [TestCase(VNFunc.VNF_RSH, 64L, long.MinValue)]
    [TestCase(VNFunc.VNF_RSH, 65L, -4611686018427387904L)]
    [TestCase(VNFunc.VNF_RSH, -1L, -1L)]
    [TestCase(VNFunc.VNF_RSZ, 0L, long.MinValue)]
    [TestCase(VNFunc.VNF_RSZ, 31L, 4294967296L)]
    [TestCase(VNFunc.VNF_RSZ, 32L, 2147483648L)]
    [TestCase(VNFunc.VNF_RSZ, 33L, 1073741824L)]
    [TestCase(VNFunc.VNF_RSZ, 63L, 1L)]
    [TestCase(VNFunc.VNF_RSZ, 64L, long.MinValue)]
    [TestCase(VNFunc.VNF_RSZ, 65L, 4611686018427387904L)]
    [TestCase(VNFunc.VNF_RSZ, -1L, 1L)]
    public static void WindowsX64LongRightShiftBoundaries(VNFunc func, long count, long expected)
    {
        Assert.That(EvalOp(func, long.MinValue, count), Is.EqualTo(expected));
    }
#endif

    [TestCase(VNFunc.VNF_GT, -1, 1, 0)]
    [TestCase(VNFunc.VNF_GT_UN, -1, 1, 1)]
    [TestCase(VNFunc.VNF_GE_UN, -1, -1, 1)]
    [TestCase(VNFunc.VNF_LT_UN, -1, 1, 0)]
    [TestCase(VNFunc.VNF_LE_UN, 0, -1, 1)]
    [TestCase(VNFunc.VNF_LT, -1, 1, 1)]
    [TestCase(VNFunc.VNF_EQ, -1, -1, 1)]
    [TestCase(VNFunc.VNF_NE, -1, -1, 0)]
    public static void IntegerComparisons(VNFunc func, int left, int right, int expected)
    {
        Assert.That(Compare(func, left, right), Is.EqualTo(expected));
        Assert.That(Compare(func, (long)left, (long)right), Is.EqualTo(expected));
    }

    [Test]
    public static void NativeSizeComparisonUsesUnsignedOrdering()
    {
        Assert.That(Compare(VNFunc.VNF_LT, nuint.MaxValue, (nuint)1), Is.EqualTo(0));
        Assert.That(Compare(VNFunc.VNF_GT_UN, nuint.MaxValue, (nuint)1), Is.EqualTo(1));
        Assert.That(EvalOp(VNFunc.VNF_OR, nuint.MaxValue, (nuint)0), Is.EqualTo(nuint.MaxValue));
        Assert.That(Compare(VNFunc.VNF_GT_UN, long.MinValue, long.MaxValue), Is.EqualTo(1));
        Assert.That(Compare(VNFunc.VNF_GT, long.MinValue, long.MaxValue), Is.EqualTo(0));
    }

    [Test]
    public static void NativePreconditionsUseOnlyNonzeroNonoverflowingDivisors()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EvalOp(VNFunc.VNF_DIV, int.MinValue, 1), Is.EqualTo(int.MinValue));
            Assert.That(EvalOp(VNFunc.VNF_MOD, int.MinValue, 3), Is.EqualTo(-2));
            Assert.That(EvalOp(VNFunc.VNF_UDIV, int.MinValue, -1), Is.EqualTo(0));
            Assert.That(CheckedOps.TryAdd(int.MaxValue, 1, out _), Is.False);
            Assert.That(CheckedOps.TryMul(int.MaxValue, 2, out _), Is.False);
        });
    }

    [TestCase(VNFunc.VNF_ADD, 0x80000000U, 0x00000000U, 0x00000000U)]
    [TestCase(VNFunc.VNF_SUB, 0x80000000U, 0x00000000U, 0x80000000U)]
    [TestCase(VNFunc.VNF_MUL, 0x80000000U, 0x3F800000U, 0x80000000U)]
    [TestCase(VNFunc.VNF_DIV, 0x80000000U, 0x3F800000U, 0x80000000U)]
    [TestCase(VNFunc.VNF_MOD, 0x80000000U, 0x3F800000U, 0x80000000U)]
    [TestCase(VNFunc.VNF_MOD, 0x3F800000U, 0x7F800000U, 0x3F800000U)]
    [TestCase(VNFunc.VNF_DIV, 0x3F800000U, 0x00000000U, 0x7F800000U)]
    public static void FloatOperationsPreserveSignedZeroAndInfinity(VNFunc func, uint leftBits, uint rightBits, uint expectedBits)
    {
        var left = BitConverter.Int32BitsToSingle(unchecked((int)leftBits));
        var right = BitConverter.Int32BitsToSingle(unchecked((int)rightBits));
        Assert.That(unchecked((uint)BitConverter.SingleToInt32Bits(EvalOp(func, left, right))), Is.EqualTo(expectedBits));
    }

    [TestCase(VNFunc.VNF_ADD, 0x8000000000000000UL, 0UL, 0UL)]
    [TestCase(VNFunc.VNF_SUB, 0x8000000000000000UL, 0UL, 0x8000000000000000UL)]
    [TestCase(VNFunc.VNF_MUL, 0x8000000000000000UL, 0x3FF0000000000000UL, 0x8000000000000000UL)]
    [TestCase(VNFunc.VNF_DIV, 0x3FF0000000000000UL, 0UL, 0x7FF0000000000000UL)]
    [TestCase(VNFunc.VNF_MOD, 0x3FF0000000000000UL, 0x7FF0000000000000UL, 0x3FF0000000000000UL)]
    public static void DoubleOperationsPreserveSignedZeroAndInfinity(VNFunc func, ulong leftBits, ulong rightBits, ulong expectedBits)
    {
        var left = BitConverter.Int64BitsToDouble(unchecked((long)leftBits));
        var right = BitConverter.Int64BitsToDouble(unchecked((long)rightBits));
        Assert.That(unchecked((ulong)BitConverter.DoubleToInt64Bits(EvalOp(func, left, right))), Is.EqualTo(expectedBits));
    }

#if WINDOWS_AMD64_ABI
    // Pinned valuenum.cpp FpAdd/FpSub/FpMul/FpDiv on Windows x64 use native
    // arithmetic, including operand NaN payloads and the invalid-operation NaN sign.
    [TestCase(VNFunc.VNF_ADD, 0x7FC01234U, 0x3F800000U, 0x7FC01234U,
        0x7FF8000000001234UL, 0x3FF0000000000000UL, 0x7FF8000000001234UL)]
    [TestCase(VNFunc.VNF_SUB, 0x7FC01234U, 0x3F800000U, 0x7FC01234U,
        0x7FF8000000001234UL, 0x3FF0000000000000UL, 0x7FF8000000001234UL)]
    [TestCase(VNFunc.VNF_MUL, 0x7FC01234U, 0x3F800000U, 0x7FC01234U,
        0x7FF8000000001234UL, 0x3FF0000000000000UL, 0x7FF8000000001234UL)]
    [TestCase(VNFunc.VNF_DIV, 0x7FC01234U, 0x3F800000U, 0x7FC01234U,
        0x7FF8000000001234UL, 0x3FF0000000000000UL, 0x7FF8000000001234UL)]
    [TestCase(VNFunc.VNF_ADD, 0xFFC05678U, 0x7FC01234U, 0xFFC05678U,
        0xFFF8000000005678UL, 0x7FF8000000001234UL, 0xFFF8000000005678UL)]
    [TestCase(VNFunc.VNF_SUB, 0xFFC05678U, 0x7FC01234U, 0xFFC05678U,
        0xFFF8000000005678UL, 0x7FF8000000001234UL, 0xFFF8000000005678UL)]
    [TestCase(VNFunc.VNF_MUL, 0xFFC05678U, 0x7FC01234U, 0xFFC05678U,
        0xFFF8000000005678UL, 0x7FF8000000001234UL, 0xFFF8000000005678UL)]
    [TestCase(VNFunc.VNF_DIV, 0xFFC05678U, 0x7FC01234U, 0xFFC05678U,
        0xFFF8000000005678UL, 0x7FF8000000001234UL, 0xFFF8000000005678UL)]
    [TestCase(VNFunc.VNF_ADD, 0x7F800000U, 0xFF800000U, 0xFFC00000U,
        0x7FF0000000000000UL, 0xFFF0000000000000UL, 0xFFF8000000000000UL)]
    [TestCase(VNFunc.VNF_MUL, 0x00000000U, 0x7F800000U, 0xFFC00000U,
        0x0000000000000000UL, 0x7FF0000000000000UL, 0xFFF8000000000000UL)]
    [TestCase(VNFunc.VNF_DIV, 0x00000000U, 0x00000000U, 0xFFC00000U,
        0x0000000000000000UL, 0x0000000000000000UL, 0xFFF8000000000000UL)]
    public static void WindowsX64FloatingArithmeticNaNBits(VNFunc func, uint leftFloatBits, uint rightFloatBits,
        uint expectedFloatBits, ulong leftDoubleBits, ulong rightDoubleBits, ulong expectedDoubleBits)
    {
        var leftFloat = BitConverter.Int32BitsToSingle(unchecked((int)leftFloatBits));
        var rightFloat = BitConverter.Int32BitsToSingle(unchecked((int)rightFloatBits));
        var leftDouble = BitConverter.Int64BitsToDouble(unchecked((long)leftDoubleBits));
        var rightDouble = BitConverter.Int64BitsToDouble(unchecked((long)rightDoubleBits));

        Assert.That(unchecked((uint)BitConverter.SingleToInt32Bits(EvalOp(func, leftFloat, rightFloat))),
            Is.EqualTo(expectedFloatBits));
        Assert.That(unchecked((ulong)BitConverter.DoubleToInt64Bits(EvalOp(func, leftDouble, rightDouble))),
            Is.EqualTo(expectedDoubleBits));
    }
#endif

    [TestCase(VNFunc.VNF_EQ, 0)]
    [TestCase(VNFunc.VNF_NE, 1)]
    [TestCase(VNFunc.VNF_GT, 0)]
    [TestCase(VNFunc.VNF_GE, 0)]
    [TestCase(VNFunc.VNF_LT, 0)]
    [TestCase(VNFunc.VNF_LE, 0)]
    [TestCase(VNFunc.VNF_GT_UN, 1)]
    [TestCase(VNFunc.VNF_GE_UN, 1)]
    [TestCase(VNFunc.VNF_LT_UN, 1)]
    [TestCase(VNFunc.VNF_LE_UN, 1)]
    public static void NaNComparisons(VNFunc func, int expected)
    {
        var single = BitConverter.Int32BitsToSingle(unchecked((int)0x7FC01234));
        var dbl = BitConverter.Int64BitsToDouble(unchecked((long)0x7FF8000000001234));
        Assert.That(Compare(func, single, 1.0f), Is.EqualTo(expected));
        Assert.That(Compare(func, 1.0f, single), Is.EqualTo(expected));
        Assert.That(Compare(func, dbl, 1.0), Is.EqualTo(expected));
        Assert.That(Compare(func, 1.0, dbl), Is.EqualTo(expected));
    }

    [Test]
    public static void FloatingComparisonsTreatSignedZerosAsEqual()
    {
        Assert.That(Compare(VNFunc.VNF_EQ, -0.0f, +0.0f), Is.EqualTo(1));
        Assert.That(Compare(VNFunc.VNF_GE_UN, -0.0, +0.0), Is.EqualTo(1));
        Assert.That(Compare(VNFunc.VNF_LT, double.NegativeInfinity, double.PositiveInfinity), Is.EqualTo(1));
    }

    [Test]
    public static void RemainderCanonicalizesNonfiniteDividends()
    {
#if TARGET_XARCH
        const int floatNaN = unchecked((int)0xFFC00000);
        const long doubleNaN = unchecked((long)0xFFF8000000000000);
#else
        const int floatNaN = 0x7FC00000;
        const long doubleNaN = 0x7FF8000000000000;
#endif
        var payloadFloat = BitConverter.Int32BitsToSingle(unchecked((int)0x7FC01234));
        var payloadDouble = BitConverter.Int64BitsToDouble(unchecked((long)0x7FF8000000001234));
        Assert.That(BitConverter.SingleToInt32Bits(EvalOp(VNFunc.VNF_MOD, payloadFloat, 2.0f)), Is.EqualTo(floatNaN));
        Assert.That(BitConverter.SingleToInt32Bits(EvalOp(VNFunc.VNF_MOD, 1.0f, 0.0f)), Is.EqualTo(floatNaN));
        Assert.That(BitConverter.DoubleToInt64Bits(EvalOp(VNFunc.VNF_MOD, payloadDouble, 2.0)), Is.EqualTo(doubleNaN));
        Assert.That(BitConverter.DoubleToInt64Bits(EvalOp(VNFunc.VNF_MOD, double.PositiveInfinity, 2.0)), Is.EqualTo(doubleNaN));
    }
}
