// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ConfigDoubleArrayTests
{
    [TestCase("1,2\t3", new double[] { 1, 2, 3 })]
    [TestCase("1-2+.5", new double[] { 1, -2, 0.5 })]
    [TestCase("0x1.8p+1,0X1p-2", new double[] { 3, 0.25 })]
    [TestCase("-0x1.8p+1+0X1p-2", new double[] { -3, 0.25 })]
    [TestCase("0x1p-1074,5e-324", new double[] { double.Epsilon, double.Epsilon })]
    [TestCase("1e999,1e-999", new double[] { double.PositiveInfinity, 0 })]
    [TestCase(",, \t1, \r\n2, \v\f", new double[] { 1, 2 })]
    [TestCase("1.7976931348623157e308,2.2250738585072014e-308", new double[] { double.MaxValue, 2.2250738585072014e-308 })]
    [TestCase(null, new double[0])]
    [TestCase("", new double[0])]
    [TestCase(" ,\t", new double[0])]
    public static void ParsesNumbersAndSeparators(string? text, double[] expected)
    {
        var array = Parse(text);
        Assert.That(array.GetLength(), Is.EqualTo(expected.Length));
        Assert.That(array.GetData().ToArray(), Is.EqualTo(expected));
        array.EnsureInit(null);
        Assert.That(array.GetData().ToArray(), Is.EqualTo(expected));
    }

    [TestCase("-0", 0x8000000000000000UL)]
    [TestCase("inf", 0x7FF0000000000000UL)]
    [TestCase("-INFINITY", 0xFFF0000000000000UL)]
    public static void PreservesNativeSpecialValueBits(string text, ulong expected)
    {
        Assert.That(BitConverter.DoubleToUInt64Bits(Parse(text).GetData()[0]), Is.EqualTo(expected));
    }

    [TestCase("invalid")]
    [TestCase("1,invalid")]
    [TestCase("1e")]
    [TestCase("0x1")]
    [TestCase("0x1p")]
    [TestCase("nan(ind)")]
    [TestCase("nan(snan)")]
    public static void InvalidValuesFailExplicitly(string text)
    {
        _ = Assert.Throws<FormatException>(() => _ = Parse(text));
    }

    [TestCase("nan")]
    [TestCase("+NaN")]
    [TestCase("-NaN")]
    public static void ParsesNaNWithoutPreservingCrtPayloads(string text)
    {
        Assert.That(double.IsNaN(Parse(text).GetData()[0]), Is.True);
    }

    [Test]
    public static void ParsingUsesInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            double[] expected = [1.25, 0.75];
            Assert.That(Parse("1.25,0x1.8p-1").GetData().ToArray(), Is.EqualTo(expected));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestCase(null, "<uninitialized config double array>\n")]
    [TestCase("", "<empty config double array>\n")]
    [TestCase("-0,1.25,inf", "-0.000000 ,1.250000 ,inf ")]
    public static void DumpPreservesNativeLayout(string? text, string expected)
    {
        using var tls = new JitTls(null);
        var array = text is null ? default : Parse(text);
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;

        try
        {
            s_jitstdout = writer;
            array.Dump();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
            Is.EqualTo(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
    }

    private static ConfigDoubleArray Parse(string? text)
    {
        var bytes = text is null ? null : Encoding.UTF8.GetBytes(text + '\0');

        fixed (byte* p = bytes)
        {
            ConfigDoubleArray array = default;
            array.EnsureInit(p);

            return array;
        }
    }
}
#endif
