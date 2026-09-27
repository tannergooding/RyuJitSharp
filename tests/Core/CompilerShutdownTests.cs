// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerShutdownTests
{
    private static readonly string[] s_csvConfigurations = ["", "first.csv", "second.csv"];

    [Test]
    public static void TimerTerminationUpdatesTheCallersAggregate()
    {
        WithState(() => {
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var timer = new JitTimer(21);
            var summary = new CompTimeSummaryInfo();

            timer.Terminate(compiler, ref summary, includePhases: true);

            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            summary.Print(writer);
            writer.Flush();
            Assert.That(Encoding.UTF8.GetString(output.ToArray()), Does.Contain("Compiled 1 methods."));
        });
    }

    [Test]
    public static void CsvConfigurationPreservesNullEmptyAndReloadedValues()
    {
        WithState(() => {
            Assert.That(Compiler.JitTimeLogCsv, Is.Null);
            foreach (var expected in s_csvConfigurations)
            {
                fixed (byte* value = Encoding.UTF8.GetBytes(expected + "\0"))
                {
                    CsvConfig(ref JitConfig) = value;
                    Assert.That(Compiler.JitTimeLogCsv, Is.EqualTo(expected));
                }
            }
            CsvConfig(ref JitConfig) = null;
            Assert.That(Compiler.JitTimeLogCsv, Is.Null);
        });
    }

    [Test]
    public static void ShutdownDropsAssemblyListsWithoutResettingInitialization()
    {
        WithState(() => {
            fixed (byte* name = "Example\0"u8)
            {
                AltAssemblies(null) = new AssemblyNamesList2(name);
                AltInitialized(null) = true;
#if DEBUG
                DumpAssemblies(null) = new AssemblyNamesList2(name);
                DumpInitialized(null) = true;
#endif
            }

            Compiler.compShutdown();

            Assert.That(AltAssemblies(null), Is.Null);
            Assert.That(AltInitialized(null), Is.True);
#if DEBUG
            Assert.That(DumpAssemblies(null), Is.Null);
            Assert.That(DumpInitialized(null), Is.True);
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ShutdownClosesCsvEvenWhenSummaryCannotBeOpened(bool canOpenSummary)
    {
        WithState(() => {
            var path = canOpenSummary ? Path.GetTempFileName() : Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "summary.txt");
            using var csvStream = new MemoryStream();
            using var csv = new StreamWriter(csvStream);
            CsvFile(null) = csv;
            var info = new CompTimeInfo(21) { _totalCycles = (ulong)Stopwatch.Frequency };
            CompTimeSummaryInfo.s_compTimeSummary.AddInfo(in info, includePhases: true);
            try
            {
                if (canOpenSummary)
                {
                    File.WriteAllText(path, "existing\n");
                }

                fixed (byte* name = Encoding.UTF8.GetBytes(path + "\0"))
                {
                    TimeLogFile(null) = (nint)name;
                    Compiler.compShutdown();
                }

                Assert.That(csvStream.CanWrite, Is.False);
                if (canOpenSummary)
                {
                    Assert.That(File.ReadAllText(path), Does.StartWith("existing\nJIT Compilation time report:"));
                    Assert.That(File.ReadAllText(path), Does.Contain("Compiled 1 methods."));
                }
                else
                {
                    Assert.That(File.Exists(path), Is.False);
                }
            }
            finally
            {
                if (canOpenSummary)
                {
                    File.Delete(path);
                }
            }
        });
    }

    [Test]
    public static void PublicShutdownFinishesBeforeDisposingOutputAndIsIdempotent()
    {
        WithState(() => {
            var initialized = typeof(CILJit).GetField("s_isJitInitialized", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing JIT initialization state.");
            var previous = initialized.GetValue(null);
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: false);
            s_jitstdout = writer;
            initialized.SetValue(null, true);
#if DEBUG
            LoopStats(ref JitConfig) = 1;
#endif
            try
            {
                var jit = new CILJit();
                jit.ProcessShutdownWork(null);
                Assert.That(initialized.GetValue(null), Is.False);
#if DEBUG
                Assert.That(Encoding.UTF8.GetString(output.ToArray()), Does.Contain("Loop hoisting stats\n"));
#endif
                var length = output.ToArray().Length;
                jit.ProcessShutdownWork(null);
                Assert.That(output.ToArray().Length, Is.EqualTo(length));
                _ = Assert.Throws<ObjectDisposedException>(() => writer.Write("closed"));
            }
            finally
            {
                initialized.SetValue(null, previous);
            }
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void XmlFooterPrecedesZeroMethodGuardAndFallsBackToStdout(bool canOpenXml)
    {
        WithState(() => {
            var path = canOpenXml ? Path.GetTempFileName() : Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "inlines.xml");
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            using var csvStream = new MemoryStream();
            using var csv = new StreamWriter(csvStream);
            s_jitstdout = writer;
            CsvFile(null) = csv;
            Compiler.genMethodCnt = 0;
            XmlHeader(null) = true;
            LoopStats(ref JitConfig) = 1;
            try
            {
                fixed (byte* name = Encoding.UTF8.GetBytes(path + "\0"))
                {
                    XmlFile(ref JitConfig) = name;
                    Compiler.compShutdown();
                }
                writer.Flush();

                Assert.That(XmlHeader(null), Is.False);
                Assert.That(csvStream.CanWrite, Is.True);
                var text = Encoding.UTF8.GetString(output.ToArray());
                if (canOpenXml)
                {
                    Assert.That(text, Is.Empty);
                    Assert.That(File.ReadAllText(path), Is.EqualTo("</Methods>\r\n</InlineForest>\r\n"));
                }
                else
                {
                    Assert.That(text, Is.EqualTo("</Methods>\n</InlineForest>\n"));
                }
            }
            finally
            {
                if (canOpenXml)
                {
                    File.Delete(path);
                }
            }
        });
    }

    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(0, 1)]
    [TestCase(1, 1)]
    public static void ShutdownSelectsAggregateReportsInNativeOrder(int loops, int locals)
    {
        WithState(() => {
            LoopStats(ref JitConfig) = loops;
            EnregStats(ref JitConfig) = locals;
            XmlHeader(null) = true;
            using var output = new MemoryStream();
            using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
            s_jitstdout = writer;

            Compiler.compShutdown();
            writer.Flush();

            var text = Encoding.UTF8.GetString(output.ToArray());
            Assert.That(text.Contains("Loop hoisting stats", StringComparison.Ordinal), Is.EqualTo(loops != 0));
            Assert.That(text.Contains("Locals enregistration statistics", StringComparison.Ordinal), Is.EqualTo(locals != 0));
            Assert.That(XmlHeader(null), Is.True);
            Assert.That(text, Does.Not.Contain("</InlineForest>"));
            if ((loops != 0) && (locals != 0))
            {
                Assert.That(text.IndexOf("Loop hoisting", StringComparison.Ordinal),
                    Is.LessThan(text.IndexOf("Locals enregistration", StringComparison.Ordinal)));
            }
        });
    }
#endif

    private static void WithState(Action action)
    {
        var config = JitConfig;
        var stdout = s_jitstdout;
        var csv = CsvFile(null);
        var timeFile = TimeLogFile(null);
        var summary = CompTimeSummaryInfo.s_compTimeSummary;
        var alt = AltAssemblies(null);
        var altInitialized = AltInitialized(null);
#if DEBUG
        var dump = DumpAssemblies(null);
        var dumpInitialized = DumpInitialized(null);
        var methodCount = Compiler.genMethodCnt;
        var xmlHeader = XmlHeader(null);
        var enreg = Compiler.s_enregisterStats;
        Compiler.genMethodCnt = 1;
        XmlHeader(null) = false;
        Compiler.s_enregisterStats = default;
#endif
        JitConfig = new JitConfigValues();
        CsvFile(null) = null;
        TimeLogFile(null) = 0;
        CompTimeSummaryInfo.s_compTimeSummary = default;
        try
        {
            action();
        }
        finally
        {
            JitConfig = config;
            s_jitstdout = stdout;
            CsvFile(null) = csv;
            TimeLogFile(null) = timeFile;
            CompTimeSummaryInfo.s_compTimeSummary = summary;
            AltAssemblies(null) = alt;
            AltInitialized(null) = altInitialized;
#if DEBUG
            DumpAssemblies(null) = dump;
            DumpInitialized(null) = dumpInitialized;
            Compiler.genMethodCnt = methodCount;
            XmlHeader(null) = xmlHeader;
            Compiler.s_enregisterStats = enreg;
#endif
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_csvFile")]
    private static extern ref StreamWriter? CsvFile(JitTimer? timer);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "compJitTimeLogFilename")]
    private static extern ref nint TimeLogFile(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_pAltJitExcludeAssembliesList")]
    private static extern ref AssemblyNamesList2? AltAssemblies(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_pAltJitExcludeAssembliesListInitialized")]
    private static extern ref bool AltInitialized(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitTimeLogCsv")]
    private static extern ref byte* CsvConfig(ref JitConfigValues config);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_pJitDisasmIncludeAssembliesList")]
    private static extern ref AssemblyNamesList2? DumpAssemblies(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_pJitDisasmIncludeAssembliesListInitialized")]
    private static extern ref bool DumpInitialized(Compiler? compiler);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_HasDumpedXmlHeader")]
    private static extern ref bool XmlHeader(InlineStrategy? strategy);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitInlineDumpXmlFile")]
    private static extern ref byte* XmlFile(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_displayLoopHoistStats")]
    private static extern ref int LoopStats(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEnregStats")]
    private static extern ref int EnregStats(ref JitConfigValues config);
#endif
}
