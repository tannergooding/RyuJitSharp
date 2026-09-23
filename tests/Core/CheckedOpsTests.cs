// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Numerics;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class CheckedOpsTests
{
    private delegate bool TryOperation<T>(T left, T right, out T result);

    [TestCase(false)]
    [TestCase(true)]
    public static void AddInt(bool unsigned)
    {
        CheckArithmetic<int>(unsigned ? CheckedOps.TryAddUns : CheckedOps.TryAdd, static (left, right) => left + right, unsigned);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AddLong(bool unsigned)
    {
        CheckArithmetic<long>(unsigned ? CheckedOps.TryAddUns : CheckedOps.TryAdd, static (left, right) => left + right, unsigned);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SubtractInt(bool unsigned)
    {
        CheckArithmetic<int>(unsigned ? CheckedOps.TrySubUns : CheckedOps.TrySub, static (left, right) => left - right, unsigned);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SubtractLong(bool unsigned)
    {
        CheckArithmetic<long>(unsigned ? CheckedOps.TrySubUns : CheckedOps.TrySub, static (left, right) => left - right, unsigned);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultiplyInt(bool unsigned)
    {
        CheckArithmetic<int>(unsigned ? CheckedOps.TryMulUns : CheckedOps.TryMul, static (left, right) => left * right, unsigned);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultiplyLong(bool unsigned)
    {
        CheckArithmetic<long>(unsigned ? CheckedOps.TryMulUns : CheckedOps.TryMul, static (left, right) => left * right, unsigned);
    }

    [TestCase(0, 8, true, 0)]
    [TestCase(1, 8, true, 8)]
    [TestCase(8, 8, true, 8)]
    [TestCase(9, 8, true, 16)]
    [TestCase(-9, 8, true, -8)]
    [TestCase(int.MinValue, 8, true, int.MinValue)]
    [TestCase(int.MaxValue - 7, 8, true, int.MaxValue - 7)]
    [TestCase(int.MaxValue - 6, 8, false, int.MinValue)]
    [TestCase(int.MaxValue, 1, true, int.MaxValue)]
    public static void AlignInt(int value, int alignment, bool expectedSuccess, int expectedResult)
    {
        var success = CheckedOps.TryAlignUp(value, alignment, out var result);
        Assert.That(success, Is.EqualTo(expectedSuccess));
        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [TestCase(0L, 8L, true, 0L)]
    [TestCase(1L, 8L, true, 8L)]
    [TestCase(8L, 8L, true, 8L)]
    [TestCase(9L, 8L, true, 16L)]
    [TestCase(-9L, 8L, true, -8L)]
    [TestCase(long.MinValue, 8L, true, long.MinValue)]
    [TestCase(long.MaxValue - 7, 8L, true, long.MaxValue - 7)]
    [TestCase(long.MaxValue - 6, 8L, false, long.MinValue)]
    [TestCase(long.MaxValue, 1L, true, long.MaxValue)]
    public static void AlignLong(long value, long alignment, bool expectedSuccess, long expectedResult)
    {
        var success = CheckedOps.TryAlignUp(value, alignment, out var result);
        Assert.That(success, Is.EqualTo(expectedSuccess));
        Assert.That(result, Is.EqualTo(expectedResult));
    }

    private static void CheckArithmetic<T>(TryOperation<T> operation, Func<BigInteger, BigInteger, BigInteger> exactOperation, bool unsigned)
        where T : IBinaryInteger<T>, ISignedNumber<T>, IMinMaxValue<T>
    {
        var width = typeof(T) == typeof(int) ? 32 : 64;
        var squareRoot = T.CreateChecked(width == 32 ? 46340L : 3037000499L);
        T[] values = [
            T.MinValue, T.MinValue + T.One, T.MinValue / T.CreateChecked(2),
            -squareRoot - T.One, -squareRoot, -T.CreateChecked(2), -T.One,
            T.Zero, T.One, T.CreateChecked(2), squareRoot, squareRoot + T.One,
            T.One << (width / 2), T.MaxValue / T.CreateChecked(2), T.MaxValue - T.One, T.MaxValue,
        ];
        var modulus = BigInteger.One << width;
        var minimum = unsigned ? BigInteger.Zero : BigInteger.CreateChecked(T.MinValue);
        var maximum = unsigned ? modulus - 1 : BigInteger.CreateChecked(T.MaxValue);

        foreach (var left in values)
        {
            foreach (var right in values)
            {
                var exactLeft = BigInteger.CreateChecked(left);
                var exactRight = BigInteger.CreateChecked(right);

                if (unsigned)
                {
                    if (exactLeft.Sign < 0)
                    {
                        exactLeft += modulus;
                    }

                    if (exactRight.Sign < 0)
                    {
                        exactRight += modulus;
                    }
                }

                var expected = exactOperation(exactLeft, exactRight);
                var expectedSuccess = expected >= minimum && expected <= maximum;
                var success = operation(left, right, out var result);
                Assert.That(success, Is.EqualTo(expectedSuccess), $"Operands: {left}, {right}");
                Assert.That(result, Is.EqualTo(T.CreateTruncating(expected)), $"Operands: {left}, {right}");
            }
        }
    }
}
