// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BitPositionTests
{
    [TestCaseSource(nameof(OneHot32Cases))]
    [TestCase(3U, 0U, 1U)]
    [TestCase(0x7FFFFFFFU, 0U, 30U)]
    [TestCase(0x80000001U, 0U, 31U)]
    [TestCase(uint.MaxValue, 0U, 31U)]
    [TestCase(0xAAAAAAAAU, 1U, 31U)]
    [TestCase(0x55555555U, 0U, 30U)]
    [TestCase(0x40000008U, 3U, 30U)]
    [TestCase(0xFFFFFFFEU, 1U, 31U)]
    public static void Positions32DistinguishTheLowestAndHighestSetBits(uint value, uint lowest, uint highest)
    {
        Assert.That(BitScanForward(value), Is.EqualTo(lowest));
        Assert.That(TrailingZeroCount(value), Is.EqualTo(lowest));
        Assert.That(LeadingZeroCount(value), Is.EqualTo(31U - highest));
        Assert.That(Log2(value), Is.EqualTo(highest));
    }

    [TestCaseSource(nameof(OneHot64Cases))]
    [TestCase(3UL, 0U, 1U)]
    [TestCase(0x7FFFFFFFUL, 0U, 30U)]
    [TestCase(0x80000001UL, 0U, 31U)]
    [TestCase(0xFFFFFFFFUL, 0U, 31U)]
    [TestCase(0x100000001UL, 0U, 32U)]
    [TestCase(0x180000000UL, 31U, 32U)]
    [TestCase(0xFFFFFFFF00000000UL, 32U, 63U)]
    [TestCase(0x7FFFFFFFFFFFFFFFUL, 0U, 62U)]
    [TestCase(0x8000000000000001UL, 0U, 63U)]
    [TestCase(ulong.MaxValue, 0U, 63U)]
    [TestCase(0xAAAAAAAAAAAAAAAAUL, 1U, 63U)]
    [TestCase(0x5555555555555555UL, 0U, 62U)]
    public static void Positions64DistinguishTheLowestAndHighestSetBits(ulong value, uint lowest, uint highest)
    {
        Assert.That(BitScanForward(value), Is.EqualTo(lowest));
        Assert.That(TrailingZeroCount(value), Is.EqualTo(lowest));
        Assert.That(LeadingZeroCount(value), Is.EqualTo(63U - highest));
        Assert.That(Log2(value), Is.EqualTo(highest));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TotalFunctionsAtZeroReturnNativeResultsWithoutAsserting(bool wide)
    {
#if DEBUG
        WithAssertionRecorder(() => AssertTotalZero(wide));
        Assert.That(s_assertions, Is.Empty);
#else
        AssertTotalZero(wide);
#endif
    }

    private static void AssertTotalZero(bool wide)
    {
        if (wide)
        {
            Assert.That(LeadingZeroCount(0UL), Is.EqualTo(64U));
            Assert.That(TrailingZeroCount(0UL), Is.EqualTo(64U));
            Assert.That(Log2(0UL), Is.EqualTo(0U));
        }
        else
        {
            Assert.That(LeadingZeroCount(0U), Is.EqualTo(32U));
            Assert.That(TrailingZeroCount(0U), Is.EqualTo(32U));
            Assert.That(Log2(0U), Is.EqualTo(0U));
        }
    }

    private static IEnumerable<TestCaseData> OneHot32Cases()
    {
        for (var bit = 0; bit < 32; bit++)
        {
            yield return new TestCaseData(1U << bit, (uint)bit, (uint)bit);
        }
    }

    private static IEnumerable<TestCaseData> OneHot64Cases()
    {
        for (var bit = 0; bit < 64; bit++)
        {
            yield return new TestCaseData(1UL << bit, (uint)bit, (uint)bit);
        }
    }

#if DEBUG
    private static readonly List<string?> s_assertions = [];

    [TestCase(false)]
    [TestCase(true)]
    public static void ZeroScanRetainsTheNativeAssertionCountAndText(bool wide)
    {
        WithAssertionRecorder(() => {
            if (wide)
            {
                _ = BitScanForward(0UL);
            }
            else
            {
                _ = BitScanForward(0U);
            }

            var expectedCount = wide && OperatingSystem.IsWindows() && !Environment.Is64BitProcess ? 2 : 1;
            Assert.That(s_assertions, Has.Count.EqualTo(expectedCount));
            Assert.That(s_assertions, Is.All.EqualTo("value != 0"));
        });
    }

    private static void WithAssertionRecorder(Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();
        action();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));
        return 0;
    }
#endif
}
