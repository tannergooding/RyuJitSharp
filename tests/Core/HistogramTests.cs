// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class HistogramTests
{
    [Test]
    public static void EmptyHistogramUsesNativeMessage()
    {
        Assert.That(Capture(new Histogram([1, 2, 0])), Is.EqualTo("  (no data recorded)\n"));
    }

    [Test]
    public static void BucketsIncludeUpperBoundsAndPreserveCumulativePercentages()
    {
        var histogram = new Histogram([1, 2, 5, 0]);
        uint[] values = [0, 1, 2, 3, 5, 6];

        foreach (var value in values)
        {
            histogram.record(value);
        }

        Assert.That(Capture(histogram), Is.EqualTo(
            "     <=          1 ===>       2 count ( 33% of total)\n" +
            "      2 ..       2 ===>       1 count ( 50% of total)\n" +
            "      3 ..       5 ===>       2 count ( 83% of total)\n" +
            "      >          5 ===>       1 count (100% of total)\n"));
    }

    [Test]
    public static void EmptyOverflowBucketIsNotPrinted()
    {
        var histogram = new Histogram([1, 2, 0]);
        histogram.record(1);

        Assert.That(Capture(histogram), Is.EqualTo(
            "     <=          1 ===>       1 count (100% of total)\n" +
            "      2 ..       2 ===>       0 count (100% of total)\n"));
    }

    [Test]
    public static void BucketLimitReservesTheLastCounterForOverflow()
    {
        var bounds = new uint[65];

        for (var i = 0; i < 64; i++)
        {
            bounds[i] = (uint)(i + 1);
        }

        var histogram = new Histogram(bounds);
        histogram.record(63);
        histogram.record(64);
        histogram.record(uint.MaxValue);

        Assert.That(Counts(histogram)[62], Is.EqualTo(1U));
        Assert.That(Counts(histogram)[63], Is.EqualTo(2U));
        Assert.That(Capture(histogram), Does.EndWith("      >         63 ===>       2 count (100% of total)\n"));
    }

    [Test]
    public static void RecordingIsAtomicAcrossWorkers()
    {
        var histogram = new Histogram([1, 0]);
        _ = Parallel.For(0, 4, _ => {
            for (var i = 0; i < 1024; i++)
            {
                histogram.record(1);
            }
        });

        Assert.That(Counts(histogram)[0], Is.EqualTo(4096U));
    }

    [Test]
    public static void CounterWrapPreservesNativeUnsignedArithmetic()
    {
        var histogram = new Histogram([1, 0]);
        Counts(histogram)[0] = uint.MaxValue;
        histogram.record(1);

        Assert.That(Capture(histogram), Is.EqualTo("  (no data recorded)\n"));
    }

    internal static string Capture(Histogram histogram)
    {
        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        histogram.dump(writer);
        writer.Flush();

        return Encoding.UTF8.GetString(output.ToArray());
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_counts")]
    internal static extern ref InlineArrayHistogramMaxSizeCount<uint> Counts(Histogram histogram);
}
