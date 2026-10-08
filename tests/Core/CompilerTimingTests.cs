// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
#if FEATURE_JIT_METHOD_PERF && MEASURE_CLRAPI_CALLS
using static RyuJitSharp.API_ICorJitInfo_Names;
#endif
using static RyuJitSharp.Globals;
using static RyuJitSharp.Phases;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerTimingTests
{
#if FEATURE_JIT_METHOD_PERF
#if MEASURE_CLRAPI_CALLS
    private static int _isIntrinsicCallCount;

    [Test]
    public static void ClrCountersUseNativeWidthsAndIndependentInlineStorage()
    {
        var first = new CompTimeInfo(0);
        first._allClrApiCalls = uint.MaxValue;
        first._allClrApiCycles = ulong.MaxValue;
        first._perClrApiCalls[(int)API_isIntrinsic] = uint.MaxValue;
        first._perClrApiCycles[(int)API_isIntrinsic] = ulong.MaxValue;
        first._maxClrApiCycles[(int)API_isIntrinsic] = uint.MaxValue;
        first._clrInvokesByPhase[(int)PHASE_IMPORTATION] = ulong.MaxValue;
        first._clrCyclesByPhase[(int)PHASE_IMPORTATION] = ulong.MaxValue;
        var second = new CompTimeInfo(0);
        second._allClrApiCalls = 2;
        second._allClrApiCycles = 2;
        second._perClrApiCalls[(int)API_isIntrinsic] = 2;
        second._perClrApiCycles[(int)API_isIntrinsic] = 2;
        second._maxClrApiCycles[(int)API_isIntrinsic] = 2;
        second._clrInvokesByPhase[(int)PHASE_IMPORTATION] = 2;
        second._clrCyclesByPhase[(int)PHASE_IMPORTATION] = 2;

        var copy = first;
        copy._perClrApiCalls[(int)API_isIntrinsic] = 0;
        Assert.That(first._perClrApiCalls[(int)API_isIntrinsic], Is.EqualTo(uint.MaxValue));

        CompTimeSummaryInfo summary = default;
        summary.AddInfo(first, includePhases: true);
        summary.AddInfo(second, includePhases: true);
        summary.AddInfo(new CompTimeInfo(0) { _timerFailure = true }, includePhases: true);

        ref var total = ref TotalInfo(ref summary);
        ref var maximum = ref MaximumInfo(ref summary);
        Assert.That(total._allClrApiCalls, Is.EqualTo(1u));
        Assert.That(total._allClrApiCycles, Is.EqualTo(1UL));
        Assert.That(total._perClrApiCalls[(int)API_isIntrinsic], Is.EqualTo(1u));
        Assert.That(total._perClrApiCycles[(int)API_isIntrinsic], Is.EqualTo(1UL));
        Assert.That(total._clrInvokesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo(1UL));
        Assert.That(total._clrCyclesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo(1UL));
        Assert.That(maximum._allClrApiCalls, Is.EqualTo(uint.MaxValue));
        Assert.That(maximum._allClrApiCycles, Is.EqualTo(ulong.MaxValue));
        Assert.That(maximum._maxClrApiCycles[(int)API_isIntrinsic], Is.EqualTo(uint.MaxValue));
        Assert.That(maximum._clrCyclesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo(ulong.MaxValue));
        Assert.That(first._perClrApiCalls[(int)API_isIntrinsic], Is.EqualTo(uint.MaxValue));
    }

    [Test]
    public static void PartialImportsPreservePinnedPerApiMaximumSlotAndDoNotCreditNormalPhases()
    {
        var first = new CompTimeInfo(7) { _allClrApiCalls = 2, _allClrApiCycles = 10 };
        first._perClrApiCalls[(int)PHASE_CLR_API] = 6;
        first._cyclesByPhase[(int)PHASE_IMPORTATION] = 100;
        var second = new CompTimeInfo(9) { _allClrApiCalls = 1, _allClrApiCycles = 20 };
        CompTimeSummaryInfo summary = default;
        summary.AddInfo(first, includePhases: false);
        summary.AddInfo(second, includePhases: false);

        ref var total = ref TotalInfo(ref summary);
        ref var maximum = ref MaximumInfo(ref summary);
        Assert.That(total._byteCodeBytes, Is.Zero);
        Assert.That(total._cyclesByPhase[(int)PHASE_IMPORTATION], Is.Zero);
        Assert.That(total._invokesByPhase[(int)PHASE_CLR_API], Is.EqualTo(3UL));
        Assert.That(total._cyclesByPhase[(int)PHASE_CLR_API], Is.EqualTo(30UL));
        Assert.That(maximum._invokesByPhase[(int)PHASE_CLR_API], Is.EqualTo(6UL));
        Assert.That(maximum._cyclesByPhase[(int)PHASE_CLR_API], Is.EqualTo(20UL));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ClrLeaveRequiresEnabledTimingAndAValidStartAndAlwaysClearsTheCall(bool enabled, bool validStart)
    {
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        ClrTimingEnabled(ref JitConfig) = enabled ? 1 : 0;

        try
        {
            var timer = new JitTimer(0);
            timer.ClrApiCallEnter(API_isIntrinsic);

            if (!validStart)
            {
                ClrCallStart(timer) = 0;
            }

            timer.ClrApiCallLeave(API_isIntrinsic);

            Assert.That(TimerInfo(timer)._allClrApiCalls, Is.EqualTo(enabled && validStart ? 1u : 0u));
            Assert.That(ClrCallStart(timer), Is.Zero);
            Assert.That(ClrActiveApi(timer), Is.EqualTo((API_ICorJitInfo_Names)(-1)));
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [Test]
    public static void ClrLeaveTruncatesIndividualMaximumAndExcludesEeTimeFromThePhase()
    {
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        ClrTimingEnabled(ref JitConfig) = 1;

        try
        {
            var timer = new JitTimer(0);
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var phaseStart = CurrentPhaseStart(timer);
            timer.ClrApiCallEnter(API_isIntrinsic);
            ClrCallStart(timer) = unchecked(Stopwatch.GetTimestamp() - (long)uint.MaxValue - 100);
            timer.ClrApiCallLeave(API_isIntrinsic);

            var elapsed = TimerInfo(timer)._allClrApiCycles;
            Assert.That(elapsed, Is.GreaterThan((ulong)uint.MaxValue));
            Assert.That(TimerInfo(timer)._maxClrApiCycles[(int)API_isIntrinsic], Is.EqualTo(unchecked((uint)elapsed)));
            Assert.That(CurrentPhaseStart(timer), Is.EqualTo(unchecked(phaseStart + (long)elapsed)));

            // EndPhase requires the exclusive phase clock to precede the current counter.
            CurrentPhaseStart(timer) = Stopwatch.GetTimestamp() - 1;
            timer.EndPhase(compiler, PHASE_IMPORTATION);
            Assert.That(TimerInfo(timer)._clrInvokesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo(1UL));
            Assert.That(TimerInfo(timer)._clrCyclesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo(elapsed));
            Assert.That(TimerInfo(timer)._clrCyclesByPhase[(int)PHASE_PRE_IMPORT], Is.Zero);

            timer.EndPhase(compiler, PHASE_IMPORTATION);
            Assert.That(TimerInfo(timer)._clrInvokesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo(1UL));
            Assert.That(TimerInfo(timer)._clrCyclesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo(elapsed));
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    public static void ManagedEeProxyInstallationRejectsEnabledTiming(int enabled, bool shouldReject)
    {
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        ClrTimingEnabled(ref JitConfig) = enabled;

        try
        {
            if (shouldReject)
            {
                var exception = Assert.Throws<FatalJitException>(() => WrapICorJitInfo.EnsureInstallationSupported());
                Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
                Assert.That(exception?.Message, Does.Contain("unmanaged ICorJitInfo proxy"));
            }
            else
            {
                Assert.DoesNotThrow(() => WrapICorJitInfo.EnsureInstallationSupported());
            }
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [Test]
    public static void ManagedEeWrapperForwardsAndAccountsForTheApiCall()
    {
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        ClrTimingEnabled(ref JitConfig) = 1;

        try
        {
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var timer = new JitTimer(0);
            CompilerJitTimer(compiler) = timer;
            _isIntrinsicCallCount = 0;

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.isIntrinsic = &IsIntrinsic;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            var wrapper = new WrapICorJitInfo(compiler, &jitInfo);

            Assert.That(wrapper.isIntrinsic(default), Is.True);
            Assert.That(_isIntrinsicCallCount, Is.EqualTo(1));
            Assert.That(TimerInfo(timer)._allClrApiCalls, Is.EqualTo(1u));
            Assert.That(TimerInfo(timer)._perClrApiCalls[(int)API_isIntrinsic], Is.EqualTo(1u));
            Assert.That(ClrActiveApi(timer), Is.EqualTo((API_ICorJitInfo_Names)(-1)));
            Assert.That(ClrCallStart(timer), Is.Zero);
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [Test]
    public static void ClrSummaryUsesNativeApiNamesHeaderAndInvariantNumbers()
    {
        var info = new CompTimeInfo(0) {
            _totalCycles = (ulong)Stopwatch.Frequency,
            _allClrApiCalls = 2,
            _allClrApiCycles = (ulong)Stopwatch.Frequency
        };
        info._perClrApiCalls[(int)API_isIntrinsic] = 1;
        info._perClrApiCycles[(int)API_isIntrinsic] = (ulong)Stopwatch.Frequency;
        info._maxClrApiCycles[(int)API_isIntrinsic] = unchecked((uint)Stopwatch.Frequency);
        info._perClrApiCalls[(int)API_getExpectedTargetArchitecture] = 1;
        CompTimeSummaryInfo summary = default;
        summary.AddInfo(info, includePhases: true);

        var previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var output = PrintSummary(summary);
            Assert.That(output, Does.Contain("     " + new string('-', 100) + "\n"));
            Assert.That(output, Does.Contain("     isIntrinsic"));
            Assert.That(output, Does.Contain("     getExpectedTargetArchitecture"));
            Assert.That(output, Does.Not.Contain("API_isIntrinsic"));
            Assert.That(output, Does.Contain("1000.0 ms"));
            Assert.That(output, Does.Not.Contain("1000,0 ms"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEECallTimingInfo")]
    private static extern ref int ClrTimingEnabled(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_clrCallStart")]
    private static extern ref long ClrCallStart(JitTimer timer);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_clrCallApiNum")]
    private static extern ref API_ICorJitInfo_Names ClrActiveApi(JitTimer timer);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compJitTimer")]
    private static extern ref JitTimer? CompilerJitTimer(Compiler compiler);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsic(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* ftn)
    {
        _isIntrinsicCallCount++;
        return 1;
    }
#endif

    [Test]
    public static void SummaryAggregatesCompletedMethodsAndSkipsFailuresAndPartialCompiles()
    {
        var first = new CompTimeInfo(12) { _totalCycles = (ulong)Stopwatch.Frequency, _parentPhaseEndSlop = (ulong)(Stopwatch.Frequency / 100) };
        first._invokesByPhase[(int)PHASE_IMPORTATION] = 2;
        first._cyclesByPhase[(int)PHASE_IMPORTATION] = (ulong)(Stopwatch.Frequency / 2);
        var second = new CompTimeInfo(20) { _totalCycles = 2UL * (ulong)Stopwatch.Frequency };
        second._invokesByPhase[(int)PHASE_IMPORTATION] = 1;
        second._cyclesByPhase[(int)PHASE_IMPORTATION] = (ulong)(Stopwatch.Frequency / 4);

        var summary = new CompTimeSummaryInfo();
        summary.AddInfo(first, includePhases: true);
        summary.AddInfo(second, includePhases: true);
        summary.AddInfo(first, includePhases: false);
        var failed = new CompTimeInfo(1000) { _timerFailure = true };
        summary.AddInfo(failed, includePhases: true);

        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var output = PrintSummary(summary);
            Assert.That(output, Does.StartWith("JIT Compilation time report:\n  Compiled 2 methods.\n"));
            Assert.That(output, Does.Contain("  Compiled 32 bytecodes total (20 max,    16.00 avg).\n"));
            Assert.That(output, Does.Contain("  Time: total:"));
            Assert.That(output, Does.Contain("3000.000 ms"));
            Assert.That(output, Does.Contain("2000.000 ms"));
            Assert.That(output, Does.Contain("1500.000 ms"));
            Assert.That(output, Does.Match(@"Importation\s+1\.50"));
            Assert.That(output, Does.Not.Contain("meet the filter requirement"));
            Assert.That(output, Does.Not.Contain("16,00"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public static void ZeroElapsedTimeStillReportsUndefinedPhasePercentage()
    {
        var summary = new CompTimeSummaryInfo();
        summary.AddInfo(new CompTimeInfo(1), includePhases: true);

        var output = PrintSummary(summary);
#if HOST_WINDOWS
        Assert.That(output, Does.Contain("-nan(ind)%"));
#else
        Assert.That(output, Does.Contain("nan%"));
#endif
        Assert.That(output, Does.Not.Contain("End phase slop"));
        summary.Print(null);
    }

    [Test]
    public static void ZeroFilteredTimeUsesNativeNonfinitePercentage()
    {
        var summary = new CompTimeSummaryInfo();
        FilteredMethods(ref summary) = 1;
        FilteredInfo(ref summary) = new CompTimeInfo(1);

        var output = PrintSummary(summary);
#if HOST_WINDOWS
        Assert.That(output, Does.Contain("-nan(ind)%"));
#else
        Assert.That(output, Does.Contain("nan%"));
#endif
    }

    [Test]
    public static void UnsignedCountsWrapAndMaximumAndAveragesRemainUnsigned()
    {
        var first = new CompTimeInfo(-1) {
            _totalCycles = (ulong)long.MaxValue + 10,
            _parentPhaseEndSlop = (ulong)long.MaxValue + 1
        };
        first._cyclesByPhase[(int)PHASE_IMPORTATION] = (ulong)long.MaxValue + 2;
        first._invokesByPhase[(int)PHASE_IMPORTATION] = ulong.MaxValue;
        first._nodeCountAfterPhase[(int)PHASE_IMPORTATION] = uint.MaxValue;
        var second = new CompTimeInfo(7) { _totalCycles = 2 };
        second._cyclesByPhase[(int)PHASE_IMPORTATION] = 1;
        second._invokesByPhase[(int)PHASE_IMPORTATION] = 2;

        var summary = new CompTimeSummaryInfo();
        summary.AddInfo(first, includePhases: true);
        summary.AddInfo(second, includePhases: true);

        ref var total = ref TotalInfo(ref summary);
        ref var maximum = ref MaximumInfo(ref summary);
        Assert.That(total._byteCodeBytes, Is.EqualTo(6u));
        Assert.That(maximum._byteCodeBytes, Is.EqualTo(uint.MaxValue));
        Assert.That(total._totalCycles, Is.EqualTo((ulong)long.MaxValue + 12));
        Assert.That(maximum._totalCycles, Is.EqualTo((ulong)long.MaxValue + 10));
        Assert.That(total._cyclesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo((ulong)long.MaxValue + 3));
        Assert.That(maximum._cyclesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo((ulong)long.MaxValue + 2));
        Assert.That(total._invokesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo(1UL));
        Assert.That(total._parentPhaseEndSlop, Is.EqualTo((ulong)long.MaxValue + 1));
        Assert.That(first._nodeCountAfterPhase[(int)PHASE_IMPORTATION], Is.EqualTo(uint.MaxValue));

        var output = PrintSummary(summary);
        Assert.That(output, Does.Contain("Compiled 6 bytecodes total (-1 max,     3.00 avg)."));
        Assert.That(output, Does.Match(@"Importation\s+0\.50"));
        Assert.That(output, Does.Not.Contain("-922337"));
    }

    [Test]
    public static void UnsignedTotalsWrapAtTheNativeCounterWidths()
    {
        var summary = new CompTimeSummaryInfo();
        var first = new CompTimeInfo(-1) { _totalCycles = ulong.MaxValue };
        first._cyclesByPhase[(int)PHASE_IMPORTATION] = ulong.MaxValue;
        first._parentPhaseEndSlop = ulong.MaxValue;
        var second = new CompTimeInfo(0) { _totalCycles = 2, _parentPhaseEndSlop = 2 };
        second._cyclesByPhase[(int)PHASE_IMPORTATION] = 2;
        summary.AddInfo(first, includePhases: true);
        summary.AddInfo(second, includePhases: true);

        ref var total = ref TotalInfo(ref summary);
        Assert.That(total._byteCodeBytes, Is.EqualTo(uint.MaxValue));
        Assert.That(total._totalCycles, Is.EqualTo(1UL));
        Assert.That(total._cyclesByPhase[(int)PHASE_IMPORTATION], Is.EqualTo(1UL));
        Assert.That(total._parentPhaseEndSlop, Is.EqualTo(1UL));
        Assert.That(PrintSummary(summary), Does.Contain("Compiled -1 bytecodes total (-1 max, 2147483647.50 avg)."));
    }

    [Test]
    public static void FilteredSlopRetainsNativeMillisecondsLabeledAsPercent()
    {
        var summary = new CompTimeSummaryInfo();
        FilteredMethods(ref summary) = 1;
        FilteredInfo(ref summary) = new CompTimeInfo(5) {
            _totalCycles = (ulong)Stopwatch.Frequency,
            _parentPhaseEndSlop = (ulong)(Stopwatch.Frequency / 2)
        };

        var output = PrintSummary(summary);
        Assert.That(output, Does.Contain("Compiled 1 methods that meet the filter requirement."));
        Assert.That(output, Does.Match(@"Mcycles = 500\.0% of total\."));
    }

    [Test]
    public static void TerminateUpdatesTheProvidedSummaryRatherThanACopy()
    {
        WithCsvPath(null, () => {
            var timer = new JitTimer(17);
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var summary = new CompTimeSummaryInfo();
            timer.Terminate(compiler, ref summary, includePhases: false);
            Assert.That(PrintSummary(summary), Does.Contain("Compiled 0 methods."));

            timer.Terminate(compiler, ref summary, includePhases: true);
            Assert.That(PrintSummary(summary), Does.Contain("Compiled 1 methods."));
            Assert.That(PrintSummary(summary), Does.Contain("Compiled 17 bytecodes total"));
        });
    }

    [Test]
    public static void EndPhaseCreditsLeafAncestorsAndParentSlopWithoutRestartingClock()
    {
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        try
        {
            var timer = new JitTimer(3);
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var phaseStart = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
            CurrentPhaseStart(timer) = phaseStart;

            timer.EndPhase(compiler, PHASE_BUILD_SSA);
            var afterParent = Stopwatch.GetTimestamp();
            Assert.That(CurrentPhaseStart(timer), Is.EqualTo(phaseStart));
            Assert.That(TimerInfo(timer)._parentPhaseEndSlop, Is.InRange((ulong)Stopwatch.Frequency, (ulong)(afterParent - phaseStart)));

            timer.EndPhase(compiler, PHASE_BUILD_SSA_LIVENESS);
            var afterLeaf = Stopwatch.GetTimestamp();
            ref var info = ref TimerInfo(timer);
            Assert.That(info._invokesByPhase[(int)PHASE_BUILD_SSA_LIVENESS], Is.EqualTo(1));
            Assert.That(info._cyclesByPhase[(int)PHASE_BUILD_SSA_LIVENESS], Is.InRange((ulong)Stopwatch.Frequency, (ulong)(afterLeaf - phaseStart)));
            Assert.That(info._cyclesByPhase[(int)PHASE_BUILD_SSA], Is.EqualTo(info._cyclesByPhase[(int)PHASE_BUILD_SSA_LIVENESS]));
            Assert.That(CurrentPhaseStart(timer), Is.InRange(afterParent, afterLeaf));
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [Test]
    public static void FinalLeafRecordsElapsedCompilationTimeWithoutAdvancingPhaseStart()
    {
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        try
        {
            var timer = new JitTimer(1);
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var start = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
            TimerStart(timer) = start;
            CurrentPhaseStart(timer) = start + (Stopwatch.Frequency / 2);
            var phaseStart = CurrentPhaseStart(timer);

            timer.EndPhase(compiler, PHASE_POST_EMIT);
            var after = Stopwatch.GetTimestamp();

            Assert.That(TimerInfo(timer)._totalCycles, Is.InRange((ulong)Stopwatch.Frequency, (ulong)(after - start)));
            Assert.That(TimerInfo(timer)._invokesByPhase[(int)PHASE_POST_EMIT], Is.EqualTo(1));
            Assert.That(CurrentPhaseStart(timer), Is.EqualTo(phaseStart));
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    [Test]
    public static void CsvHeaderIsWrittenOnlyForEmptyFileAndShutdownClosesHandle()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ryujit-timing-{Guid.NewGuid():N}.csv");
        try
        {
            WithCsvPath(path, () => {
                JitTimer.PrintCsvHeader();
                JitTimer.PrintCsvHeader();
                var writer = CsvWriter() ?? throw new InvalidOperationException("CSV output was not opened.");
                Assert.That(ReadSharedCsv(path), Does.StartWith("\"Method Name\",\"Assembly or SPMI Index\","));
                Assert.That(ReadSharedCsv(path).Split("\"Method Name\"").Length, Is.EqualTo(2));
                JitTimer.Shutdown();
                Assert.That(writer.BaseStream.CanWrite, Is.False);
                Assert.That(CsvWriter(), Is.Null);

                JitTimer.PrintCsvHeader();
                Assert.That(ReadSharedCsv(path).Split("\"Method Name\"").Length, Is.EqualTo(2));
            });
            File.WriteAllText(path, "existing\n");
            WithCsvPath(path, JitTimer.PrintCsvHeader);
            Assert.That(File.ReadAllText(path), Is.EqualTo("existing\n"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public static void EmptyCsvPathAttemptsOpenWithoutCreatingAHeader()
    {
        WithCsvPath("", () => {
            JitTimer.PrintCsvHeader();
            Assert.That(CsvWriter(), Is.Null);
        });
    }

#if DEBUG
    [Test]
    public static void CsvMethodRowIncludesManagedAllocationAndSixDecimalCycleFrequency()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ryujit-timing-row-{Guid.NewGuid():N}.csv");
        ICorJitHost.Vtbl vtable = default;
        vtable.getIntConfigValue = &GetSpmiIndex;
        ICorJitHost host = new() { lpVtbl = &vtable };
        var previousHost = CILJit.s_jitHost;
        CILJit.s_jitHost = &host;
        try
        {
            WithCsvPath(path, () => {
                var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
                compiler.info.compFullName = "TimingMethod";
                compiler.info.compILCodeSize = 12;
                compiler.info.compNativeCodeSize = 24;
                compiler.fgBBcount = 2;
                compiler.compInfoBlkSize = 8;
                compiler.opts.compMinOptsIsSet = true;
                compiler._inlineStrategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
                var timer = new JitTimer(12);
                TimerInfo(timer)._totalCycles = 45;

                JitTimer.PrintCsvHeader();
                using (var externalWriter = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    externalWriter.Write("native,first\n"u8);
                }
                timer.PrintCsvMethodStats(compiler);
                using (var externalWriter = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    externalWriter.Write("native,second\n"u8);
                }
                timer.PrintCsvMethodStats(compiler);

                var rows = ReadSharedCsv(path).Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries);
                Assert.That(rows, Has.Length.EqualTo(5));
                Assert.That(rows[1], Is.EqualTo("native,first"));
                Assert.That(rows[3], Is.EqualTo("native,second"));
                var fields = rows[2].Split(',');
                var nextFields = rows[4].Split(',');
                Assert.That(fields[0], Is.EqualTo("\"TimingMethod\""));
                Assert.That(fields[1], Is.EqualTo("-1"));
                Assert.That(long.Parse(fields[^3], CultureInfo.InvariantCulture), Is.GreaterThan(0));
                Assert.That(fields[^2], Is.EqualTo("45"));
                Assert.That(fields[^1], Is.EqualTo(((double)Stopwatch.Frequency).ToString("F6", CultureInfo.InvariantCulture)));
                Assert.That(nextFields[0], Is.EqualTo(fields[0]));
                Assert.That(nextFields[1], Is.EqualTo("-1"));
                Assert.That(nextFields[^2], Is.EqualTo("45"));
            });
        }
        finally
        {
            CILJit.s_jitHost = previousHost;
            File.Delete(path);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetSpmiIndex(ICorJitHost* host, byte* name, int defaultValue) => defaultValue;
#endif

    private static string ReadSharedCsv(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string PrintSummary(CompTimeSummaryInfo summary)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" };
        summary.Print(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WithCsvPath(string? path, Action action)
    {
        var bytes = path is null ? null : Encoding.UTF8.GetBytes(path + '\0');
        fixed (byte* pPath = bytes)
        {
            var previousConfig = JitConfig;
            var config = new JitConfigValues();
            CsvPath(ref config) = pPath;
            JitConfig = config;
            try
            {
                action();
            }
            finally
            {
                JitTimer.Shutdown();
                JitConfig = previousConfig;
            }
        }
    }

    private static StreamWriter? CsvWriter()
    {
        var field = typeof(JitTimer).GetField("s_csvFile", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CSV file field was not found.");
        return (StreamWriter?)field.GetValue(null);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitTimeLogCsv")]
    private static extern ref byte* CsvPath(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_curPhaseStart")]
    private static extern ref long CurrentPhaseStart(JitTimer timer);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_start")]
    private static extern ref long TimerStart(JitTimer timer);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_info")]
    private static extern ref CompTimeInfo TimerInfo(JitTimer timer);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_numFilteredMethods")]
    private static extern ref int FilteredMethods(ref CompTimeSummaryInfo summary);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_filtered")]
    private static extern ref CompTimeInfo FilteredInfo(ref CompTimeSummaryInfo summary);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_total")]
    private static extern ref CompTimeInfo TotalInfo(ref CompTimeSummaryInfo summary);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_maximum")]
    private static extern ref CompTimeInfo MaximumInfo(ref CompTimeSummaryInfo summary);
#endif

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
