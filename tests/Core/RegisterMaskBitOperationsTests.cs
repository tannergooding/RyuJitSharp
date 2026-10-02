// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RegisterMaskBitOperationsTests
{
    [TestCase(0UL, 0U)]
    [TestCase(1UL, 1U)]
    [TestCase(0x80000000UL, 1U)]
    [TestCase(0xFFFFFFFFUL, 32U)]
    [TestCase(0xAAAAAAAAUL, 16U)]
    [TestCase(0x80000001UL, 2U)]
#if REGMASK_BITS_64
    [TestCase(0x8000000000000000UL, 1U)]
    [TestCase(ulong.MaxValue, 64U)]
    [TestCase(0xAAAAAAAAAAAAAAAAUL, 32U)]
    [TestCase(0x8000000080000001UL, 3U)]
#endif
    public static void ScalarPopulationCountReinterpretsTheCompleteNativeUnsignedMask(ulong bits, uint expected)
    {
        var mask = unchecked((regMask)bits);

        Assert.That(PopCount(mask), Is.EqualTo(expected));
        Assert.That(PopCount(new regMaskTP(mask)), Is.EqualTo(expected));
    }

    [TestCase(1UL, 0U)]
    [TestCase(2UL, 1U)]
    [TestCase(0x80000000UL, 31U)]
    [TestCase(0x80000010UL, 4U)]
    [TestCase(0xFFFFFFFFUL, 0U)]
#if REGMASK_BITS_64
    [TestCase(0x8000000000000000UL, 63U)]
    [TestCase(0x8000000100000000UL, 32U)]
    [TestCase(0xFFFFFFFF00000000UL, 32U)]
#endif
    public static void ScalarScanFindsTheLeastSignificantSetBitWithoutSignedConversion(ulong bits, uint expected)
    {
        var mask = unchecked((regMask)bits);

        Assert.That(BitScanForward(mask), Is.EqualTo(expected));
        Assert.That(BitScanForward(new regMaskTP(mask)), Is.EqualTo(expected));
    }

    [Test]
    public static void AllBitsUseTheTargetBankWidth()
    {
        var mask = (regMask)(-1);
#if REGMASK_BITS_32
        const uint ExpectedCount = 32;
#else
        const uint ExpectedCount = 64;
#endif
        Assert.That(PopCount(mask), Is.EqualTo(ExpectedCount));
        Assert.That(BitScanForward(mask), Is.Zero);
    }

#if HAS_MORE_THAN_64_REGISTERS
    [TestCase(0UL, 0UL, 0U)]
    [TestCase(1UL, 1UL, 2U)]
    [TestCase(ulong.MaxValue, 0UL, 64U)]
    [TestCase(0UL, ulong.MaxValue, 64U)]
    [TestCase(ulong.MaxValue, ulong.MaxValue, 128U)]
    [TestCase(0xAAAAAAAAAAAAAAAAUL, 0x5555555555555555UL, 64U)]
    [TestCase(0x8000000000000000UL, 0x8000000000000000UL, 2U)]
    public static void PopulationCountIncludesBothBanksEvenWhenTheirBitPositionsOverlap(
        ulong lower, ulong upper, uint expected)
    {
        var mask = new regMaskTP(unchecked((regMask)lower), unchecked((regMask)upper));

        Assert.That(PopCount(mask), Is.EqualTo(expected));
    }

    [TestCase(1UL, ulong.MaxValue, 0U)]
    [TestCase(0x8000000000000000UL, 1UL, 63U)]
    [TestCase(0x8000000100000000UL, 1UL, 32U)]
    [TestCase(0UL, 1UL, 64U)]
    [TestCase(0UL, 2UL, 65U)]
    [TestCase(0UL, 0x8000000000000000UL, 127U)]
    [TestCase(0UL, 0x8000000100000000UL, 96U)]
    public static void ScanPrefersTheLowerBankAndOffsetsAnUpperOnlyResultBy64(
        ulong lower, ulong upper, uint expected)
    {
        var mask = new regMaskTP(unchecked((regMask)lower), unchecked((regMask)upper));

        Assert.That(BitScanForward(mask), Is.EqualTo(expected));
    }
#endif

#if DEBUG
    private static int s_assertions;
    private static string? s_lastAssertion;

    [TestCase(false)]
    [TestCase(true)]
    public static void ZeroScanRetainsTheNativeNonzeroAssertion(bool fullMask)
    {
        WithAssertionRecorder(() => {
            if (fullMask)
            {
                _ = BitScanForward(default(regMaskTP));
            }
            else
            {
                _ = BitScanForward(default(regMask));
            }

            Assert.That(s_assertions, Is.EqualTo(1));
            Assert.That(s_lastAssertion, Is.EqualTo("value != 0"));
        });
    }

    private static void WithAssertionRecorder(Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions = 0;
        s_lastAssertion = null;
        action();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;
        s_lastAssertion = Marshal.PtrToStringUTF8((nint)expression);
        return 0;
    }
#endif
}
