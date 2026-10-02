// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Numerics;
#if DEBUG
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class ScalarQueryTests
{
    [Test]
    public static void IntegralQueriesCoverEveryNativeWidth()
    {
        CheckIntegral<sbyte>(8);
        CheckIntegral<byte>(8);
        CheckIntegral<short>(16);
        CheckIntegral<ushort>(16);
        CheckIntegral<int>(32);
        CheckIntegral<uint>(32);
        CheckIntegral<long>(64);
        CheckIntegral<ulong>(64);
        CheckIntegral<nint>(nint.Size * 8);
        CheckIntegral<nuint>(nuint.Size * 8);
    }

    private static void CheckIntegral<T>(int width)
        where T : IBinaryInteger<T>, IMinMaxValue<T>
    {
        Assert.That(isPow2(T.Zero), Is.False);
        Assert.That(isPow2(T.MaxValue), Is.False);
        Assert.That(isPow2(T.MinValue), Is.False);
        Assert.That(isPow2(T.One | (T.One << 1)), Is.False);
        Assert.That(signum(T.Zero), Is.Zero);
        Assert.That(signum(T.One), Is.EqualTo(1));
        Assert.That(signum(T.MaxValue), Is.EqualTo(1));
        Assert.That(signum(T.MinValue), Is.EqualTo(T.IsNegative(T.MinValue) ? -1 : 0));

        for (var position = 0; position < width; position++)
        {
            var value = T.One << position;
            var positive = !T.IsNegative(value);

            Assert.That(isPow2(value), Is.EqualTo(positive), $"{typeof(T)} bit {position}");
            Assert.That(signum(value), Is.EqualTo(positive ? 1 : -1));
        }
    }

    [TestCase(0x00000000U, 0)]
    [TestCase(0x00000001U, 1)]
    [TestCase(0x00800000U, 1)]
    [TestCase(0x3F800000U, 1)]
    [TestCase(0x7F7FFFFFU, 1)]
    [TestCase(0x7F800000U, 1)]
    [TestCase(0x7FC12345U, 0)]
    [TestCase(0x7F812345U, 0)]
    public static void SingleSignumUsesOrderedComparison(uint bits, int expected)
    {
        Assert.That(signum(UInt32BitsToSingle(bits)), Is.EqualTo(expected));
        Assert.That(signum(UInt32BitsToSingle(bits | 0x80000000U)), Is.EqualTo(-expected));
    }

    [TestCase(0x0000000000000000UL, 0)]
    [TestCase(0x0000000000000001UL, 1)]
    [TestCase(0x0010000000000000UL, 1)]
    [TestCase(0x3FF0000000000000UL, 1)]
    [TestCase(0x7FEFFFFFFFFFFFFFUL, 1)]
    [TestCase(0x7FF0000000000000UL, 1)]
    [TestCase(0x7FF8123456789ABCUL, 0)]
    [TestCase(0x7FF0123456789ABCUL, 0)]
    public static void DoubleSignumUsesOrderedComparison(ulong bits, int expected)
    {
        Assert.That(signum(UInt64BitsToDouble(bits)), Is.EqualTo(expected));
        Assert.That(signum(UInt64BitsToDouble(bits | 0x8000000000000000UL)), Is.EqualTo(-expected));
    }

#if DEBUG
    [TestCase(2U, 32U)]
    [TestCase(3U, 21U)]
    [TestCase(4U, 16U)]
    [TestCase(5U, 14U)]
    [TestCase(6U, 13U)]
    [TestCase(7U, 12U)]
    [TestCase(8U, 11U)]
    [TestCase(9U, 11U)]
    [TestCase(10U, 10U)]
    [TestCase(11U, 10U)]
    [TestCase(12U, 9U)]
    [TestCase(13U, 9U)]
    [TestCase(14U, 9U)]
    [TestCase(15U, 9U)]
    [TestCase(16U, 8U)]
    public static void UnsignedDigitsCoverEveryBaseAndPowerBoundary(uint radix, uint maximumDigits)
    {
        Assert.That(CountDigits(0U, radix), Is.EqualTo(1U));
        Assert.That(CountDigits(uint.MaxValue, radix), Is.EqualTo(maximumDigits));
        var digits = 1U;

        for (ulong power = radix; power <= uint.MaxValue; power *= radix)
        {
            Assert.That(CountDigits((uint)power - 1, radix), Is.EqualTo(digits));
            Assert.That(CountDigits((uint)power, radix), Is.EqualTo(digits + 1));
            Assert.That(CountDigits((uint)power + 1, radix), Is.EqualTo(digits + 1));
            digits++;
        }
    }

    [TestCase(0U, 1U)]
    [TestCase(9U, 1U)]
    [TestCase(10U, 2U)]
    [TestCase(0x80000000U, 10U)]
    [TestCase(uint.MaxValue, 10U)]
    public static void DefaultUnsignedDigitsUseDecimal(uint value, uint expected)
    {
        Assert.That(CountDigits(value), Is.EqualTo(expected));
    }

    [TestCase(1U)]
    [TestCase(17U)]
    [NonParallelizable]
    public static unsafe void InvalidBaseRetainsNativeAssertionWithSafeZeroContinuation(uint radix)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertionCount = 0;
        s_assertion = null;
        _ = CountDigits(0U, radix);

        Assert.That(s_assertionCount, Is.EqualTo(1));
        Assert.That(s_assertion, Is.EqualTo("2 <= base && base <= 16"));
    }

    private static int s_assertionCount;
    private static string? s_assertion;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertionCount++;
        s_assertion = Marshal.PtrToStringUTF8((nint)expression);
        return 0;
    }
#endif
}
