// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ConfigIntArrayTests
{
    [TestCase("1,2\t3", new[] { 1, 2, 3 })]
    [TestCase("2;-;3|4", new[] { 2, 0, -3, -4 })]
    [TestCase("-2147483648 2147483648 -", new[] { int.MinValue, int.MinValue, 0 })]
    [TestCase("2147483649 4294967295 4294967296", new[] { int.MinValue + 1, -1, 0 })]
    [TestCase("--1 +2 -3", new[] { 0, -1, -2, -3 })]
    [TestCase("abc-xyz9\n5", new[] { 0, -9, -5 })]
    [TestCase("000-0-0", new[] { 0, 0, 0 })]
    [TestCase("1-2,3", new[] { 1, -2, -3 })]
    [TestCase("1-,2", new[] { 1, 0, -2 })]
    [TestCase("", new int[0])]
    [TestCase("a+ \t", new int[0])]
    public static void ParsesNativeTokensAndWrappingArithmetic(string text, int[] expected)
    {
        var array = Parse(text);

        Assert.That(array.GetLength(), Is.EqualTo(expected.Length));
        Assert.That(array.GetData().ToArray(), Is.EqualTo(expected));
        array.EnsureInit(null);
        Assert.That(array.GetData().ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public static void StopsAtFirstNulAndInitializesOnlyOnce()
    {
        var initial = "1,2\0-3"u8.ToArray();
        var replacement = "4,5\0"u8.ToArray();
        ConfigIntArray array = default;
        fixed (byte* first = initial)
        fixed (byte* second = replacement)
        {
            array.EnsureInit(first);
            array.EnsureInit(second);
        }

        int[] expected = [1, 2];
        Assert.That(array.GetData().ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public static void MissingConfigurationFailsExplicitly()
    {
        ConfigIntArray array = default;
        _ = Assert.Throws<FatalJitException>(() => array.EnsureInit(null));
        Assert.That(array.GetLength(), Is.Zero);
    }

    [TestCase(null, "<uninitialized config int array>\n")]
    [TestCase("", "<empty config int array>\n")]
    [TestCase("1;-;2", "1, 0, -2")]
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

    private static ConfigIntArray Parse(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text + '\0');
        fixed (byte* p = bytes)
        {
            ConfigIntArray array = default;
            array.EnsureInit(p);

            return array;
        }
    }
}
#endif
