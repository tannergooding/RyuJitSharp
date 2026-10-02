// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class PerfCounterTests
{
    [Test]
    public static void StartCapturesTheClockAndMillisecondFrequency()
    {
        var counter = new PerfCounter();
        var before = Stopwatch.GetTimestamp();
        var started = counter.Start();
        var after = Stopwatch.GetTimestamp();

        Assert.That(started, Is.True);
        Assert.That(Begin(counter), Is.InRange(before, after));
        Assert.That(Frequency(counter), Is.EqualTo((double)Stopwatch.Frequency / 1000.0));
    }

    [Test]
    public static void RestartReplacesBothValues()
    {
        var counter = new PerfCounter();
        Assert.That(counter.Start(), Is.True);
        Begin(counter) = long.MinValue;
        Frequency(counter) = -1;

        var before = Stopwatch.GetTimestamp();
        var started = counter.Start();
        var after = Stopwatch.GetTimestamp();

        Assert.That(started, Is.True);
        Assert.That(Begin(counter), Is.InRange(before, after));
        Assert.That(Frequency(counter), Is.EqualTo((double)Stopwatch.Frequency / 1000.0));
    }

    [Test]
    public static void CountersKeepIndependentStartValues()
    {
        var first = new PerfCounter();
        var second = new PerfCounter();
        Assert.That(first.Start(), Is.True);
        var start = Begin(first);
        var frequency = Frequency(first);

        Assert.That(second.Start(), Is.True);
        Assert.That(second.Start(), Is.True);
        Assert.That(Begin(first), Is.EqualTo(start));
        Assert.That(Frequency(first), Is.EqualTo(frequency));
    }

    [TestCase(0L)]
    [TestCase(1L)]
    [TestCase(-1L)]
    [TestCase(9007199254740993L)]
    [TestCase(-9007199254740993L)]
    public static void ElapsedMillisecondsRetainIntegerSubtractionBeforeConversion(long offset)
    {
        var counter = new PerfCounter();
        Assert.That(counter.Start(), Is.True);
        var start = Stopwatch.GetTimestamp() - offset;
        Begin(counter) = start;
        var frequency = Frequency(counter);

        var before = Stopwatch.GetTimestamp();
        var elapsed = counter.ElapsedTime();
        var after = Stopwatch.GetTimestamp();

        Assert.That(elapsed, Is.InRange((double)(before - start) / frequency,
            (double)(after - start) / frequency));
        Assert.That(Begin(counter), Is.EqualTo(start));
        Assert.That(Frequency(counter), Is.EqualTo(frequency));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_beg")]
    private static extern ref long Begin(PerfCounter counter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_freq")]
    private static extern ref double Frequency(PerfCounter counter);
}
