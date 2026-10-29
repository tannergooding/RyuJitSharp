// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class EmitterStatisticFormattingTests
{
    [TestCase(1, "12.5")]
    [TestCase(2, "12.50")]
    [TestCase(3, "12.500")]
    public static void FormatsSupportedPrecisions(int precision, string expected)
    {
        using var stream = new MemoryStream();
        using var output = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        WriteEmitterStatistic(null, output, 12.5, 8, precision);
        output.Flush();

        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected.PadLeft(8)));
    }

    [TestCase(0)]
    [TestCase(4)]
    public static void RejectsUnsupportedPrecision(int precision)
    {
        using var output = new StreamWriter(Stream.Null);

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => WriteEmitterStatistic(null, output, 12.5, 8, precision));
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "WriteEmitterStatistic")]
    private static extern void WriteEmitterStatistic(
        Emitter? emitter,
        StreamWriter output,
        double value,
        int width,
        int precision);
}
