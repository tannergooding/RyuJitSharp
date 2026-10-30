// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        for (var position = 0; position < width; position++)
        {
            var value = T.One << position;
            var positive = !T.IsNegative(value);

            Assert.That(isPow2(value), Is.EqualTo(positive), $"{typeof(T)} bit {position}");
        }
    }

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
        Assume.That(TestConfiguration.IsDebug);
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
        Assume.That(TestConfiguration.IsDebug);
        Assert.That(CountDigits(value), Is.EqualTo(expected));
    }

    [TestCase(1U)]
    [TestCase(17U)]
    [NonParallelizable]
    public static unsafe void InvalidBaseRetainsNativeAssertionWithSafeZeroContinuation(uint radix)
    {
        Assume.That(TestConfiguration.IsDebug);
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
}
