// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if MEASURE_NOWAY
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class NowayStatisticsTests
{
    [TestCase(0, true, false)]
    [TestCase(2, true, false)]
    [TestCase(1, false, false)]
    [TestCase(1, true, true)]
    public static void RecordingRequiresExactOptionAndCompiler(int option, bool hasCompiler, bool expected)
    {
        var text = Run(_ => {
            RecordNowayAssertGlobal("a", 7, "condition");
            Compiler.DisplayNowayAssertMap();
        }, option, hasCompiler);

        Assert.That(text, Is.EqualTo(expected
            ? "\nnoway_assert counts:\ncount, file, line, text\n1, a, 7, \"condition\"\n"
            : ""));
    }

    [Test]
    public static void IdentityUsesFileAndLineAndRetainsFirstCondition()
    {
        var text = Run(compiler => {
            compiler.RecordNowayAssert("a", 7, "first");
            compiler.RecordNowayAssert("a", 7, "second");
            compiler.RecordNowayAssert("b", 7, "different file");
            Compiler.DisplayNowayAssertMap();
        });

        Assert.That(text, Does.EndWith("2, a, 7, \"first\"\n1, b, 7, \"different file\"\n"));
    }

    [Test]
    public static void SuccessfulAssertionsRetainTheirOriginalLocations()
    {
        var text = Run(_ => {
            noway_assert(true, "first", filePath: "a", lineNumber: 7);
            noway_assert(true, "second", filePath: "b", lineNumber: 8);
            Compiler.DisplayNowayAssertMap();
        });

        Assert.That(text, Does.Contain("1, a, 7, \"first\"\n"));
        Assert.That(text, Does.Contain("1, b, 8, \"second\"\n"));
    }

    [TestCase(6, new int[] { 46, 37, 28, 19, 10, 1 })]
    [TestCase(7, new int[] { 19, 46, 1, 28, 55, 10, 37 })]
    public static void EqualCountsRetainNativeChainsAndGrowthOrder(int count, int[] expectedLines)
    {
        var text = Run(compiler => {
            for (var i = 0; i < count; i++)
            {
                compiler.RecordNowayAssert("a", 1 + (9 * i), "same");
            }

            Compiler.DisplayNowayAssertMap();
        });
        var expected = new StringBuilder("\nnoway_assert counts:\ncount, file, line, text\n");

        foreach (var line in expectedLines)
        {
            _ = expected.Append(CultureInfo.InvariantCulture, $"1, a, {line}, \"same\"\n");
        }

        Assert.That(text, Is.EqualTo(expected.ToString()));
    }

    [Test]
    public static void LargeReportUsesDescendingNativeSort()
    {
        var text = Run(compiler => {
            for (var i = 1; i <= 12; i++)
            {
                for (var j = 0; j < i; j++)
                {
                    compiler.RecordNowayAssert("a", i, "condition");
                }
            }

            Compiler.DisplayNowayAssertMap();
        });
        var expected = new StringBuilder("\nnoway_assert counts:\ncount, file, line, text\n");

        for (var i = 12; i != 0; i--)
        {
            _ = expected.Append(CultureInfo.InvariantCulture, $"{i}, a, {i}, \"condition\"\n");
        }

        Assert.That(text, Is.EqualTo(expected.ToString()));
    }

    [Test]
    public static void ExistingCountDoesNotGrowTheMapAtCapacity()
    {
        var text = Run(compiler => {
            for (var i = 0; i < 6; i++)
            {
                compiler.RecordNowayAssert("a", 1 + (9 * i), "same");
            }

            compiler.RecordNowayAssert("a", 1, "ignored");
            Compiler.DisplayNowayAssertMap();
        });

        Assert.That(text, Does.EndWith(
            "2, a, 1, \"same\"\n1, a, 46, \"same\"\n1, a, 37, \"same\"\n" +
            "1, a, 28, \"same\"\n1, a, 19, \"same\"\n1, a, 10, \"same\"\n"));
    }

    [Test]
    public static void FileHashUsesSignedUtf8Bytes()
    {
        _ = Run(compiler => {
            compiler.RecordNowayAssert("\u00e9", 7, "condition");
            var map = Map(null) ?? throw new InvalidOperationException("Expected recorded assertion.");

            Assert.That(map.Snapshot()[0].Location.Hash, Is.EqualTo(0xFFFFFF73U));
        });
    }

    [Test]
    public static void CountIncrementWrapsAtHostWidth()
    {
        var text = Run(compiler => {
            compiler.RecordNowayAssert("a", -1, "condition");
            var map = Map(null) ?? throw new InvalidOperationException("Expected recorded assertion.");
            map.Snapshot()[0].Location.Count = nuint.MaxValue;
            compiler.RecordNowayAssert("a", -1, "ignored");
            Compiler.DisplayNowayAssertMap();
        });

        Assert.That(text, Does.EndWith("0, a, 4294967295, \"condition\"\n"));
    }

    [Test]
    public static void ShutdownReportsOccurrencesBeforeTheNoMethodsReturn()
    {
        var text = Run(compiler => {
            compiler.RecordNowayAssert("a", 7, "condition");
#if DEBUG
            var previous = Compiler.genMethodCnt;
            Compiler.genMethodCnt = 0;

            try
            {
#endif
                Compiler.compShutdown();
#if DEBUG
            }
            finally
            {
                Compiler.genMethodCnt = previous;
            }
#endif
        });

        Assert.That(text, Does.StartWith("\nnoway_assert counts:\ncount, file, line, text\n1, a, 7, \"condition\"\n"));
    }

    [Test]
    public static void DescendingComparisonUsesSignedHostCounts()
    {
        var text = Run(compiler => {
            compiler.RecordNowayAssert("a", 1, "large");
            var map = Map(null) ?? throw new InvalidOperationException("Expected recorded assertion.");
            map.Snapshot()[0].Location.Count = nuint.MaxValue;
            compiler.RecordNowayAssert("a", 2, "small");
            Compiler.DisplayNowayAssertMap();
        });

        Assert.That(text, Does.EndWith($"1, a, 2, \"small\"\n{nuint.MaxValue}, a, 1, \"large\"\n"));
    }

    [Test]
    public static void FileReportsAppendWithoutHeaders()
    {
        var path = Path.GetTempFileName();

        try
        {
            File.WriteAllText(path, "existing\n");
            var stdout = Run(compiler => {
                compiler.RecordNowayAssert("a", 7, "condition");

                fixed (byte* fileName = Encoding.UTF8.GetBytes(path + '\0'))
                {
                    FileOption(ref JitConfig) = fileName;
                    Compiler.DisplayNowayAssertMap();
                    Compiler.DisplayNowayAssertMap();
                }
            });

            Assert.That(stdout, Is.Empty);
            Assert.That(File.ReadAllText(path), Is.EqualTo(
                "existing\n1, a, 7, \"condition\"\r\n1, a, 7, \"condition\"\r\n"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FileOpenFailureIsReportedOnlyWhenThereIsData(bool record)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "assertions.csv");
        var text = Run(compiler => {
            if (record)
            {
                compiler.RecordNowayAssert("a", 1, "condition");
            }

            fixed (byte* fileName = Encoding.UTF8.GetBytes(path + '\0'))
            {
                FileOption(ref JitConfig) = fileName;
                Compiler.DisplayNowayAssertMap();
            }
        });

        Assert.That(text, Is.EqualTo(record ? $"Failed to open JitMeasureNowayAssertFile \"{path}\"\n" : ""));
    }

    private static string Run(Action<Compiler> action, int option = 1, bool hasCompiler = true)
    {
        var savedConfig = JitConfig;
        var savedMap = Map(null);
        var savedOutput = s_jitstdout;
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var savedCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        using var output = new MemoryStream();
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        JitConfig = new JitConfigValues();
        Option(ref JitConfig) = option;
        Map(null) = null;
        s_jitstdout = writer;
        JitTls.Compiler = hasCompiler ? compiler : null;

        try
        {
            action(compiler);
            writer.Flush();

            return Encoding.UTF8.GetString(output.ToArray());
        }
        finally
        {
            JitTls.Compiler = savedCompiler;
            s_jitstdout = savedOutput;
            Map(null) = savedMap;
            JitConfig = savedConfig;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_nowayAssertMap")]
    private static extern ref Compiler.NowayAssertMap? Map(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMeasureNowayAssert")]
    private static extern ref int Option(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMeasureNowayAssertFile")]
    private static extern ref byte* FileOption(ref JitConfigValues config);
}
#endif
