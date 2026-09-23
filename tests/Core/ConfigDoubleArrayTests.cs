// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.IO;
using System.Runtime.CompilerServices;
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
    [TestCase(",, \t1, \r\n2, \v\f", new double[] { 1, 2 })]
    [TestCase("1.7976931348623157e308,2.2250738585072014e-308", new double[] { double.MaxValue, 2.2250738585072014e-308 })]
    [TestCase(null, new double[0])]
    [TestCase("", new double[0])]
    [TestCase(" ,\t", new double[0])]
    public static void ParsesNativeNumbersAndSeparators(string? text, double[] expected)
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
    [TestCase("nan", 0x7FFFFFFFFFFFFFFFUL)]
    [TestCase("nan(ind)", 0xFFF8000000000000UL)]
    [TestCase("nan(snan)", 0x7FF0000000000001UL)]
    public static void PreservesNativeSpecialValueBits(string text, ulong expected)
    {
        Assert.That(BitConverter.DoubleToUInt64Bits(Parse(text).GetData()[0]), Is.EqualTo(expected));
    }

    [TestCase("invalid")]
    [TestCase("1,invalid")]
    [TestCase("1e")]
    [TestCase("1e999")]
    [TestCase("1e-999")]
    public static void InvalidValuesFailExplicitly(string text)
    {
        _ = Assert.Throws<FormatException>(() => _ = Parse(text));
    }

    [TestCase("1,2", false)]
    [TestCase("1e999", true)]
    public static void ParsingIsIndependentOfStaleErrnoAndRestoresIt(string text, bool invalid)
    {
        var error = GetErrno(default);
        var original = *error;
        try
        {
            *error = 34;
            if (invalid)
            {
                _ = Assert.Throws<FormatException>(() => _ = Parse(text));
            }
            else
            {
                _ = Parse(text);
            }
            var restored = *error;
            Assert.That(restored, Is.EqualTo(34));
        }
        finally
        {
            *error = original;
        }
    }

    [TestCase(null, "<uninitialized config double array>\n")]
    [TestCase("", "<empty config double array>\n")]
    [TestCase("-0,1.25,inf,nan", "-0.000000 ,1.250000 ,inf ,nan ")]
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

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "GetErrno")]
    private static extern int* GetErrno(ConfigDoubleArray unused);
}
#endif
