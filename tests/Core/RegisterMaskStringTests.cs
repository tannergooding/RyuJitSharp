// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class RegisterMaskStringTests
{
    private static readonly Compiler s_context = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

#if REGMASK_BITS_8
    private const int FullWidth = 2;
#elif REGMASK_BITS_16
    private const int FullWidth = 4;
#elif REGMASK_BITS_32
    private const int FullWidth = 8;
#else
    private const int FullWidth = 16;
#endif

    [TestCase(0UL, "0")]
    [TestCase(1UL, "1")]
    [TestCase(0xABUL, "AB")]
#if REGMASK_BITS_32 || REGMASK_BITS_64
    [TestCase(0x12345678UL, "12345678")]
    [TestCase(0x80000000UL, "80000000")]
#endif
#if REGMASK_BITS_64
    [TestCase(0x8000000000000000UL, "8000000000000000")]
    [TestCase(ulong.MaxValue, "FFFFFFFFFFFFFFFF")]
#endif
    public static void FullMaskUsesUnsignedWidthAndUppercaseMinimumPadding(ulong bits, string digits)
    {
        var mask = new regMaskTP(unchecked((regMask)bits));
        var text = regMaskToString(mask, s_context);

        Assert.That(text, Is.EqualTo(digits.PadLeft(FullWidth, '0')));
        Assert.That(text.Length, Is.LessThan(24));
    }

#if TARGET_AMD64 || TARGET_ARM64
    [TestCase(0UL, "0000")]
    [TestCase(1UL, "0001")]
    [TestCase(0x8BUL, "008B")]
    [TestCase(0xF00FUL, "F00F")]
    [TestCase(0x8000000000000000UL, "0000")]
    [TestCase(0x10000UL, "10000")]
#if TARGET_AMD64
    [TestCase(0x80000000UL, "80000000")]
#else
    [TestCase(0x80000000UL, "0000")]
#endif
    public static void IntegerMaskFiltersFloatingBitsAndKeepsAllNativeGeneralRegisters(ulong bits, string expected)
    {
        var mask = new regMaskTP(unchecked((regMask)bits));

        Assert.That(regMaskIntToString(mask, s_context), Is.EqualTo(expected));
    }

#if HAS_MORE_THAN_64_REGISTERS
    [TestCase(1UL)]
    [TestCase(ulong.MaxValue)]
    public static void NativeFormattingIntentionallyIgnoresTheUpperBank(ulong upper)
    {
        var mask = new regMaskTP((regMask)0x8B, unchecked((regMask)upper));

        Assert.That(regMaskToString(mask, s_context), Is.EqualTo("000000000000008B"));
        Assert.That(regMaskIntToString(mask, s_context), Is.EqualTo("008B"));
    }
#endif
#endif

    [Test]
    public static void LaterFormattingDoesNotOverwriteAnEarlierResult()
    {
        var first = regMaskToString(new regMaskTP((regMask)1), s_context);
        var second = regMaskToString(new regMaskTP((regMask)2), s_context);

        Assert.That(first, Is.EqualTo("1".PadLeft(FullWidth, '0')));
        Assert.That(second, Is.EqualTo("2".PadLeft(FullWidth, '0')));
    }
}
#endif
