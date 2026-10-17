// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class RegisterMaskStringTests
{
    private static readonly Compiler s_context = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

    private static int FullWidth => Configuration.REGMASK_BITS_32 ? 8 : 16;

    [TestCase(0UL, "0")]
    [TestCase(1UL, "1")]
    [TestCase(0xABUL, "AB")]
    [TestCase(0x12345678UL, "12345678")]
    [TestCase(0x80000000UL, "80000000")]
    [TestCase(0x8000000000000000UL, "8000000000000000")]
    [TestCase(ulong.MaxValue, "FFFFFFFFFFFFFFFF")]
    public static void FullMaskUsesUnsignedWidthAndUppercaseMinimumPadding(ulong bits, string digits)
    {
        if ((bits > uint.MaxValue) && !Configuration.REGMASK_BITS_64)
        {
            Assert.Ignore("The mask value requires a 64-bit register mask.");
        }

        var mask = new regMaskTP(unchecked((regMask)bits));
        var text = regMaskToString(mask, s_context);

        Assert.That(text, Is.EqualTo(digits.PadLeft(FullWidth, '0')));
        Assert.That(text.Length, Is.LessThan(24));
    }

    [TestCase(0UL, "0000")]
    [TestCase(1UL, "0001")]
    [TestCase(0x8BUL, "008B")]
    [TestCase(0xF00FUL, "F00F")]
    [TestCase(0x8000000000000000UL, "0000")]
    [TestCase(0x10000UL, "10000")]
    [TestCase(0x80000000UL, "80000000")]
    [TestCase(0x80000000UL, "0000")]
    public static void IntegerMaskFiltersFloatingBitsAndKeepsAllNativeGeneralRegisters(ulong bits, string expected)
    {
        if (!Configuration.TARGET_AMD64 && !Configuration.TARGET_ARM64)
        {
            Assert.Ignore("Integer register-mask formatting requires AMD64 or ARM64.");
        }

        if ((bits == 0x80000000UL) &&
            ((Configuration.TARGET_AMD64 && (expected == "0000")) ||
             (Configuration.TARGET_ARM64 && (expected == "80000000"))))
        {
            Assert.Ignore("The high-bit expectation is specific to the active target.");
        }

        var mask = new regMaskTP(unchecked((regMask)bits));

        Assert.That(regMaskIntToString(mask, s_context), Is.EqualTo(expected));
    }

    [TestCase(1UL)]
    [TestCase(ulong.MaxValue)]
    public static void NativeFormattingIntentionallyIgnoresTheUpperBank(ulong upper)
    {
        if (!Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            Assert.Ignore("Upper-bank formatting requires a target with more than 64 registers.");
        }

        var mask = new regMaskTP((regMask)0x8B, unchecked((regMask)upper));

        Assert.That(regMaskToString(mask, s_context), Is.EqualTo("000000000000008B"));
        Assert.That(regMaskIntToString(mask, s_context), Is.EqualTo("008B"));
    }

    [Test]
    public static void LaterFormattingDoesNotOverwriteAnEarlierResult()
    {
        var first = regMaskToString(new regMaskTP((regMask)1), s_context);
        var second = regMaskToString(new regMaskTP((regMask)2), s_context);

        Assert.That(first, Is.EqualTo("1".PadLeft(FullWidth, '0')));
        Assert.That(second, Is.EqualTo("2".PadLeft(FullWidth, '0')));
    }
}
