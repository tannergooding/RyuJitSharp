// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

#if DEBUG
[NonParallelizable]
internal static class ILDisplayTests
{
    [TestCase(7.5, 17, "7.5000000000000000")]
    [TestCase(2.5, 17, "2.5000000000000000")]
    [TestCase(0.0, 17, "0.0000000000000000")]
    [TestCase(1.0e17, 17, "1.0000000000000000e+17")]
    [TestCase(1.0e16, 17, "10000000000000000.")]
    [TestCase(0.0001, 17, "0.00010000000000000000")]
    [TestCase(1.0e-5, 17, "1.0000000000000001e-05")]
    [TestCase(7.5, 9, "7.50000000")]
    public static void GeneralFloatingFormatRetainsNativeSignificantZeroes(double value, int precision, string expected)
    {
        Assert.That(Globals.formatFloatWithTrailingZeros(value, precision), Is.EqualTo(expected));
    }

    [TestCase(-128, "FFFFFFFFFFFFFF80")]
    [TestCase(-2, "FFFFFFFFFFFFFFFE")]
    [TestCase(-1, "FFFFFFFFFFFFFFFF")]
    [TestCase(0, "0")]
    [TestCase(127, "7F")]
    public static void ShortInlineIntegerUsesNativeHexWidth(int operand, string expectedHex)
    {
        var text = DumpInstruction([0x1F, unchecked((byte)operand)]);
        Assert.That(text, Does.EndWith($" 0x{expectedHex}{Environment.NewLine}"));
    }

    [TestCase("20FEFFFFFF", "0xFFFFFFFFFFFFFFFE")]
    [TestCase("2000000080", "0xFFFFFFFF80000000")]
    [TestCase("20FFFFFF7F", "0x7FFFFFFF")]
    [TestCase("21F0DEBC9A78563412", "0x123456789ABCDEF0")]
    [TestCase("210000000000000080", "0x8000000000000000")]
    [TestCase("2252069E3F", "1.234568")]
    [TestCase("22FFFF7F7F", "340282346638528859811704183484516925440.000000")]
    [TestCase("2200000000", "0.000000")]
    [TestCase("2200000080", "-0.000000")]
    [TestCase("220000C07F", "nan")]
    [TestCase("220100807F", "nan")]
    [TestCase("23FB598C42CAC0F33F", "1.234568")]
    [TestCase("230000000000000080", "-0.000000")]
    [TestCase("23000000000000F07F", "inf")]
    [TestCase("23000000000000F0FF", "-inf")]
    [TestCase("23000000000000F87F", "nan")]
    [TestCase("23010000000000F87F", "nan")]
    [TestCase("23010000000000F8FF", "-nan")]
    public static void NumericOperandsMatchNativeFormatting(string instructionHex, string expectedOperand)
    {
        var previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var text = DumpInstruction(Convert.FromHexString(instructionHex));
            Assert.That(text, Does.EndWith($" {expectedOperand}{Environment.NewLine}"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestCase("220000C0FF", "-nan(ind)", "-nan")]
    [TestCase("23000000000000F8FF", "-nan(ind)", "-nan")]
    [TestCase("23010000000000F07F", "nan(snan)", "nan")]
    [TestCase("23010000000000F0FF", "-nan(snan)", "-nan")]
    public static void NaNOperandsUseHostSpelling(string instructionHex, string windowsOperand, string unixOperand)
    {
        var expectedOperand = OperatingSystem.IsWindows() ? windowsOperand : unixOperand;
        var text = DumpInstruction(Convert.FromHexString(instructionHex));
        Assert.That(text, Does.EndWith($" {expectedOperand}{Environment.NewLine}"));
    }

    [TestCase("4500000000", " 45 00 00 00 00    switch      ")]
    [TestCase("450200000001000000FEFFFFFF", " 45 02 00 00 00 01 00 00 00 fe ff ff ff switch      ")]
    public static void SwitchDisplaysTheEntireTable(string instructionHex, string expectedText)
    {
        var text = DumpInstruction(Convert.FromHexString(instructionHex));
        Assert.That(text, Is.EqualTo(expectedText + Environment.NewLine));
    }

    private static unsafe string DumpInstruction(ReadOnlySpan<byte> instruction)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        writer.AutoFlush = true;
        var previousWriter = Globals.s_jitstdout;

        try
        {
            Globals.s_jitstdout = writer;

            fixed (byte* code = instruction)
            {
                Assert.That(Globals.dumpSingleInstr(code, 0), Is.EqualTo(instruction.Length));
            }
        }
        finally
        {
            Globals.s_jitstdout = previousWriter;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
#endif
