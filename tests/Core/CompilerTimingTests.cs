// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class CompilerTimingTests
{
    [Test]
    public static void TimingHooksPreserveOtherCompilerState()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.fgSsaPassesCompleted = 3;

        RecordInlining(compiler);
        RecordCompilation(compiler);

        Assert.That(compiler.fgSsaPassesCompleted, Is.EqualTo(3));
#if !DEBUG
        Assert.That(typeof(Compiler).GetField("_compCycles", BindingFlags.NonPublic | BindingFlags.Instance), Is.Null);
        Assert.That(typeof(Compiler).GetField("_compCyclesAtEndOfInlining", BindingFlags.NonPublic | BindingFlags.Instance), Is.Null);
#endif
    }

#if DEBUG
    [Test]
    public static void InliningRecordsAndReplacesTheCurrentCounter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        for (var iteration = 0; iteration < 2; iteration++)
        {
            InliningCycles(compiler) = -1;
            var before = Stopwatch.GetTimestamp();

            RecordInlining(compiler);

            var after = Stopwatch.GetTimestamp();
            Assert.That(InliningCycles(compiler), Is.InRange(before, after));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(60)]
    public static void CompilationRecordsIntegerMicroseconds(int elapsedSeconds)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var elapsedTicks = elapsedSeconds * Stopwatch.Frequency;
        var start = Stopwatch.GetTimestamp() - elapsedTicks;
        InliningCycles(compiler) = start;
        CompilationCycles(compiler) = -1;

        RecordCompilation(compiler);

        var after = Stopwatch.GetTimestamp();
        var minimum = elapsedTicks * 1000000 / Stopwatch.Frequency;
        var maximum = (after - start) * 1000000 / Stopwatch.Frequency;
        Assert.That(CompilationCycles(compiler), Is.InRange(minimum, maximum));
        Assert.That(InliningCycles(compiler), Is.EqualTo(start));
    }

    [Test]
    public static void NonpositiveIntervalClearsPreviousElapsedTime()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        InliningCycles(compiler) = long.MaxValue;
        CompilationCycles(compiler) = 91;

        RecordCompilation(compiler);

        Assert.That(CompilationCycles(compiler), Is.Zero);
        Assert.That(InliningCycles(compiler), Is.EqualTo(long.MaxValue));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_compCyclesAtEndOfInlining")]
    private static extern ref long InliningCycles(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_compCycles")]
    private static extern ref long CompilationCycles(Compiler compiler);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RecordStateAtEndOfInlining")]
    private static extern void RecordInlining(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RecordStateAtEndOfCompilation")]
    private static extern void RecordCompilation(Compiler compiler);
}
