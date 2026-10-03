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
    public static void LeadingZeroIsAValidUpperBoundWhenFollowedByOtherBounds()
    {
        var histogram = new Histogram([0, 1, 2, 0]);
        uint[] values = [0, 1, 2, 3];

        foreach (var value in values)
        {
            histogram.record(value);
        }

        Assert.That(Capture(histogram), Is.EqualTo(
            "     <=          0 ===>       1 count ( 25% of total)\n" +
            "      1 ..       1 ===>       1 count ( 50% of total)\n" +
            "      2 ..       2 ===>       1 count ( 75% of total)\n" +
            "      >          2 ===>       1 count (100% of total)\n"));
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

#if CALL_ARG_STATS || COUNT_BASIC_BLOCKS || EMITTER_STATS || MEASURE_NODE_SIZE || MEASURE_MEM_ALLOC
    [TestCase(0L, "0\n")]
    [TestCase(-1L, "-1\n")]
    [TestCase(long.MinValue, "-9223372036854775808\n")]
    [TestCase(long.MaxValue, "9223372036854775807\n")]
    public static void ScalarCounterPreservesSigned64BitOutput(long value, string expected)
    {
        Assert.That(Capture(new Counter(value)), Is.EqualTo(expected));
    }

    [Test]
    public static void ScalarCounterDefaultsToZeroAndRemainsMutable()
    {
        var counter = new Counter();
        Assert.That(counter.Value, Is.Zero);
        counter.Value = -17;

        Assert.That(Capture(counter), Is.EqualTo("-17\n"));
    }

    [Test]
    public static void NodeCountsSortByUnsignedCountThenOpcodeAndOmitZeroRows()
    {
        var counts = new NodeCounts();
        counts.record(genTreeOps.GT_MUL);
        counts.record(genTreeOps.GT_ADD);
        counts.record(genTreeOps.GT_CNS_INT);
        counts.record(genTreeOps.GT_ADD);
        counts.record(genTreeOps.GT_MUL);
        NodeStorage(counts)[(int)genTreeOps.GT_SUB] = int.MinValue;

        Assert.That(Capture(counts), Is.EqualTo(
            "SUB".PadRight(20) + " : 2147483648\n" +
            "ADD".PadRight(20) + " :       2\n" +
            "MUL".PadRight(20) + " :       2\n" +
            "CNS_INT".PadRight(20) + " :       1\n"));
    }

    [Test]
    public static void NodeCountsUpdateAtomically()
    {
        var counts = new NodeCounts();
        _ = Parallel.For(0, 4, _ => {
            for (var i = 0; i < 1024; i++)
            {
                counts.record(genTreeOps.GT_ADD);
            }
        });

        Assert.That(Capture(counts), Is.EqualTo("ADD".PadRight(20) + " :    4096\n"));
    }

    [TestCase(int.MaxValue, 0x80000000U)]
    [TestCase(-1, 0U)]
    public static void NodeCountsReinterpretWrappedSignedStorage(int initial, uint expected)
    {
        var counts = new NodeCounts();
        NodeStorage(counts)[(int)genTreeOps.GT_ADD] = initial;
        counts.record(genTreeOps.GT_ADD);

        Assert.That(unchecked((uint)NodeStorage(counts)[(int)genTreeOps.GT_ADD]), Is.EqualTo(expected));
        Assert.That(Capture(counts), Is.EqualTo(expected == 0 ? "" : "ADD".PadRight(20) + $" : {expected,7}\n"));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_counts")]
    private static extern ref int[] NodeStorage(NodeCounts counts);
#endif

    internal static string Capture(Dumpable dumpable)
    {
        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        dumpable.dump(writer);
        writer.Flush();

        return Encoding.UTF8.GetString(output.ToArray());
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_counts")]
    internal static extern ref InlineArrayHistogramMaxSizeCount<uint> Counts(Histogram histogram);
}
