// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Numerics;
#if DEBUG
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SharedBitShapeTests
{
    private static IEnumerable<TestCaseData> ScalarCases()
    {
        foreach (var testCase in NativeDefinedCases<sbyte>(8, signed: true))
        {
            yield return testCase;
        }

        foreach (var testCase in NativeDefinedCases<byte>(8, signed: false))
        {
            yield return testCase;
        }

        foreach (var testCase in NativeDefinedCases<short>(16, signed: true))
        {
            yield return testCase;
        }

        foreach (var testCase in NativeDefinedCases<ushort>(16, signed: false))
        {
            yield return testCase;
        }

        foreach (var testCase in NativeDefinedCases<int>(32, signed: true))
        {
            yield return testCase;
        }

        foreach (var testCase in NativeDefinedCases<uint>(32, signed: false))
        {
            yield return testCase;
        }

        foreach (var testCase in NativeDefinedCases<long>(64, signed: true))
        {
            yield return testCase;
        }

        foreach (var testCase in NativeDefinedCases<ulong>(64, signed: false))
        {
            yield return testCase;
        }

        foreach (var testCase in NativeDefinedCases<nint>(nint.Size * 8, signed: true))
        {
            yield return testCase;
        }

        foreach (var testCase in NativeDefinedCases<nuint>(nuint.Size * 8, signed: false))
        {
            yield return testCase;
        }
    }

    private static IEnumerable<TestCaseData> NativeDefinedCases<T>(int width, bool signed)
        where T : IBinaryInteger<T>
    {
        var allBits = ulong.MaxValue >> (64 - width);
        var highestPower = 1UL << (width - (signed ? 2 : 1));
        var maximum = signed ? allBits >> 1 : allBits;
        (ulong Value, ulong Lowest, bool AtMostOne, bool ExactlyOne)[] cases = [
            (0UL, 0UL, true, false),
            (1UL, 1UL, true, true),
            (3UL, 1UL, false, false),
            (6UL, 2UL, false, false),
            (highestPower, highestPower, true, true),
            (highestPower | 1UL, 1UL, false, false),
            (maximum, 1UL, false, false),
        ];

        foreach (var (value, lowest, atMostOne, exactlyOne) in cases)
        {
            yield return new TestCaseData(T.CreateTruncating(value), T.CreateTruncating(lowest), atMostOne, exactlyOne);
        }

        if (signed)
        {
            // Exclude the signed minimum: negation overflows at 32/64 bits, and the
            // promoted 8/16-bit result would require an out-of-range signed conversion.
            yield return new TestCaseData(T.CreateTruncating(allBits), T.One, false, false);
            yield return new TestCaseData(T.CreateTruncating(allBits - 1), T.CreateChecked(2), false, false);
            yield return new TestCaseData(T.CreateTruncating((1UL << (width - 1)) + 1), T.One, false, false);
            yield return new TestCaseData(
                T.CreateTruncating((1UL << (width - 1)) + 2), T.CreateChecked(2), false, false);
        }
        else
        {
            yield return new TestCaseData(T.CreateTruncating(allBits - 1), T.CreateChecked(2), false, false);
        }
    }

    [TestCaseSource(nameof(ScalarCases))]
    public static void ScalarShapesPreserveNativeDefinedArithmetic<T>(
        T value, T lowest, bool atMostOne, bool exactlyOne)
        where T : IBinaryInteger<T>
    {
        WithoutNativeAssertions(() => {
            Assert.That(genFindLowestBit(value), Is.EqualTo(lowest));
            Assert.That(genMaxOneBit(value), Is.EqualTo(atMostOne));
            Assert.That(genExactlyOneBit(value), Is.EqualTo(exactlyOne));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NarrowSignedMinimumPredicatesUsePromotedIntArithmetic(bool sixteenBits)
    {
        WithoutNativeAssertions(() => {
            if (sixteenBits)
            {
                Assert.That(genMaxOneBit(short.MinValue), Is.False);
                Assert.That(genExactlyOneBit(short.MinValue), Is.False);
            }
            else
            {
                Assert.That(genMaxOneBit(sbyte.MinValue), Is.False);
                Assert.That(genExactlyOneBit(sbyte.MinValue), Is.False);
            }
        });
    }

    [TestCase(0UL, 0UL, true, false)]
    [TestCase(1UL, 1UL, true, true)]
    [TestCase(2UL, 2UL, true, true)]
    [TestCase(3UL, 1UL, false, false)]
    [TestCase(6UL, 2UL, false, false)]
    [TestCase(0x80000000UL, 0x80000000UL, true, true)]
    [TestCase(0x80000001UL, 1UL, false, false)]
    [TestCase(0xFFFFFFFFUL, 1UL, false, false)]
#if REGMASK_BITS_64
    [TestCase(0x8000000000000000UL, 0x8000000000000000UL, true, true)]
    [TestCase(0x8000000080000000UL, 0x80000000UL, false, false)]
    [TestCase(ulong.MaxValue, 1UL, false, false)]
    [TestCase(ulong.MaxValue - 1, 2UL, false, false)]
#endif
    public static void LowerOnlyMasksPreserveTheCompleteUnsignedBank(
        ulong bits, ulong lowest, bool atMostOne, bool exactlyOne)
    {
        WithoutNativeAssertions(() => {
            var mask = new regMaskTP(unchecked((regMask)bits));
            var result = genFindLowestBit(mask);

            Assert.That(result.Lower, Is.EqualTo(unchecked((regMask)lowest)));
#if HAS_MORE_THAN_64_REGISTERS
            Assert.That(result.Upper, Is.EqualTo(SRBM_NONE));
#endif
            Assert.That(genMaxOneBit(mask), Is.EqualTo(atMostOne));
            Assert.That(genExactlyOneBit(mask), Is.EqualTo(exactlyOne));
        });
    }

#if HAS_MORE_THAN_64_REGISTERS
    [TestCase(0UL, 1UL, 0UL, 1UL, true, true)]
    [TestCase(0UL, 0x80000000UL, 0UL, 0x80000000UL, true, true)]
    [TestCase(0UL, 0x8000000000000000UL, 0UL, 0x8000000000000000UL, true, true)]
    [TestCase(0UL, 3UL, 0UL, 1UL, false, false)]
    [TestCase(0UL, ulong.MaxValue, 0UL, 1UL, false, false)]
    [TestCase(0UL, ulong.MaxValue - 1, 0UL, 2UL, false, false)]
    [TestCase(1UL, 1UL, 1UL, 0UL, false, false)]
    [TestCase(1UL, ulong.MaxValue, 1UL, 0UL, false, false)]
    [TestCase(0x8000000000000000UL, 1UL, 0x8000000000000000UL, 0UL, false, false)]
    [TestCase(0x80000000UL, 0x8000000000000000UL, 0x80000000UL, 0UL, false, false)]
    [TestCase(3UL, 0UL, 1UL, 0UL, false, false)]
    [TestCase(ulong.MaxValue, 0UL, 1UL, 0UL, false, false)]
    [TestCase(ulong.MaxValue, ulong.MaxValue, 1UL, 0UL, false, false)]
    public static void FullMasksPreferTheLowerBankButCountBothBanksForOneBit(
        ulong lower, ulong upper, ulong lowestLower, ulong lowestUpper, bool atMostOne, bool exactlyOne)
    {
        WithoutNativeAssertions(() => {
            var mask = new regMaskTP(unchecked((regMask)lower), unchecked((regMask)upper));
            var result = genFindLowestBit(mask);

            Assert.That(result.Lower, Is.EqualTo(unchecked((regMask)lowestLower)));
            Assert.That(result.Upper, Is.EqualTo(unchecked((regMask)lowestUpper)));
            Assert.That(genMaxOneBit(mask), Is.EqualTo(atMostOne));
            Assert.That(genExactlyOneBit(mask), Is.EqualTo(exactlyOne));
        });
    }
#endif

    private static IEnumerable<TestCaseData> Powers32()
    {
        for (var bit = 0; bit < 32; bit++)
        {
            yield return new TestCaseData(1U << bit, (uint)bit);
        }
    }

    private static IEnumerable<TestCaseData> Powers64()
    {
        for (var bit = 0; bit < 64; bit++)
        {
            yield return new TestCaseData(1UL << bit, (uint)bit);
        }
    }

    [TestCaseSource(nameof(Powers32))]
    public static void Log2ReturnsEveryUnsigned32BitPosition(uint value, uint expected)
    {
        WithoutNativeAssertions(() => Assert.That(genLog2(value), Is.EqualTo(expected)));
    }

    [TestCaseSource(nameof(Powers64))]
    public static void Log2ReturnsEveryUnsigned64BitPosition(ulong value, uint expected)
    {
        WithoutNativeAssertions(() => Assert.That(genLog2(value), Is.EqualTo(expected)));
    }

    private static void WithoutNativeAssertions(Action action)
    {
#if DEBUG
        WithAssertionRecorder(action);
        Assert.That(s_assertions, Is.Empty);
#else
        action();
#endif
    }

#if DEBUG
    private static readonly List<string?> s_assertions = [];
    private static readonly string[] s_exactlyOneAssertion = ["genExactlyOneBit(value)"];

    [TestCase(false)]
    [TestCase(true)]
    public static void ZeroLog2ReportsExactlyOneBeforeTheScanNonzeroAssertion(bool wide)
    {
        WithAssertionRecorder(() => {
            if (wide)
            {
                _ = genLog2(0UL);
            }
            else
            {
                _ = genLog2(0U);
            }
        });

        // A zero scan has no defined native numeric continuation. Only observe assertions.
        List<string> expected = ["genExactlyOneBit(value)", "value != 0"];

        if (wide && OperatingSystem.IsWindows() && !Environment.Is64BitProcess)
        {
            expected.Add("value != 0");
        }

        Assert.That(s_assertions, Is.EqualTo(expected));
    }

    [TestCase(3U, 0U)]
    [TestCase(0x80000001U, 0U)]
    [TestCase(uint.MaxValue - 1, 1U)]
    public static void MultiBitLog2ReportsOnlyTheExactlyOneAssertion32(uint value, uint expected)
    {
        WithAssertionRecorder(() => Assert.That(genLog2(value), Is.EqualTo(expected)));

        Assert.That(s_assertions, Is.EqualTo(s_exactlyOneAssertion));
    }

    [TestCase(3UL, 0U)]
    [TestCase(0x8000000000000001UL, 0U)]
    [TestCase(ulong.MaxValue - 1, 1U)]
    public static void MultiBitLog2ReportsOnlyTheExactlyOneAssertion64(ulong value, uint expected)
    {
        WithAssertionRecorder(() => Assert.That(genLog2(value), Is.EqualTo(expected)));

        Assert.That(s_assertions, Is.EqualTo(s_exactlyOneAssertion));
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
