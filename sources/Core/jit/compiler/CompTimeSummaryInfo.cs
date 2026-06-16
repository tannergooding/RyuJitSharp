// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_JIT_METHOD_PERF
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace RyuJitSharp;

/// <summary>summarizes the JIT time information over the course of a run: the number of methods compiled, and the total and maximum timings.</summary>
/// <remarks>
///   <para>The operation of adding a single method's timing to the summary may be performed concurrently by several threads, so it is protected by a lock.</para>
///   <para>This class is intended to be used as a singleton type, with only a single instance.</para>
/// </remarks>
public struct CompTimeSummaryInfo
{
    private static readonly Lock s_compTimeSummaryLock = new Lock();

    private int _numMethods;
    private int _totMethods;
    private CompTimeInfo _total;
    private CompTimeInfo _maximum;

    private int _numFilteredMethods;
    private CompTimeInfo _filtered;

    public static CompTimeSummaryInfo s_compTimeSummary;

    /// <summary>Record timing info from one compile.</summary>
    /// <param name="info">The timing information to record.</param>
    /// <param name="includePhases">If "true", the per-phase info in "info" is valid, which means that a "normal" compile has ended; if the value is "false" we are recording the results of a partial compile (typically an import-only run on behalf of the inliner) in which case the phase info is not valid and so we only record EE call overhead.</param>
    /// <remarks>
    ///   <para>Assumes that "info" is a completed CompTimeInfo for a compilation; adds it to the summary.</para>
    ///   <para>This is thread safe.</para>
    /// </remarks>
    public void AddInfo(in CompTimeInfo info, bool includePhases)
    {
        if (info._timerFailure)
        {
            // Don't update if there was a failure.
            return;
        }

        lock (s_compTimeSummaryLock)
        {
            if (includePhases)
            {
                var includeInFiltered = IncludedInFilteredData(info);

                _numMethods++;

                // Update the totals and maxima.
                _total._byteCodeBytes += info._byteCodeBytes;
                _maximum._byteCodeBytes = int.Max(_maximum._byteCodeBytes, info._byteCodeBytes);

                _total._totalCycles += info._totalCycles;
                _maximum._totalCycles = long.Max(_maximum._totalCycles, info._totalCycles);

#if MEASURE_CLRAPI_CALLS
                // Update the CLR-API values.
                _total._allClrApiCalls += info._allClrApiCalls;
                _maximum._allClrApiCalls = int.Max(_maximum._allClrApiCalls, info._allClrApiCalls);

                _total._allClrApiCycles += info._allClrApiCycles;
                _maximum._allClrApiCycles = long.Max(_maximum._allClrApiCycles, info._allClrApiCycles);
#endif

                if (includeInFiltered)
                {
                    _numFilteredMethods++;
                    _filtered._byteCodeBytes += info._byteCodeBytes;
                    _filtered._totalCycles += info._totalCycles;
                    _filtered._parentPhaseEndSlop += info._parentPhaseEndSlop;
                }

                for (var phase = default(Phases); phase < PHASE_NUMBER_OF; phase++)
                {
                    _total._invokesByPhase[(int)(phase)] += info._invokesByPhase[(int)(phase)];
                    _total._cyclesByPhase[(int)(phase)] += info._cyclesByPhase[(int)(phase)];

#if MEASURE_CLRAPI_CALLS
                    _total._clrInvokesByPhase[(int)(phase)] += info._clrInvokesByPhase[(int)(phase)];
                    _total._clrCyclesByPhase[(int)(phase)] += info._clrCyclesByPhase[(int)(phase)];
#endif

                    if (includeInFiltered)
                    {
                        _filtered._invokesByPhase[(int)(phase)] += info._invokesByPhase[(int)(phase)];
                        _filtered._cyclesByPhase[(int)(phase)] += info._cyclesByPhase[(int)(phase)];

#if MEASURE_CLRAPI_CALLS
                        _filtered._clrInvokesByPhase[(int)(phase)] += info._clrInvokesByPhase[(int)(phase)];
                        _filtered._clrCyclesByPhase[(int)(phase)] += info._clrCyclesByPhase[(int)(phase)];
#endif
                    }

                    _maximum._cyclesByPhase[(int)(phase)] = long.Max(_maximum._cyclesByPhase[(int)(phase)], info._cyclesByPhase[(int)(phase)]);

#if MEASURE_CLRAPI_CALLS
                    _maximum._CLRcyclesByPhase[(int)(phase)] = max(_maximum._CLRcyclesByPhase[(int)(phase)], info._CLRcyclesByPhase[(int)(phase)]);
#endif
                }

                _total._parentPhaseEndSlop += info._parentPhaseEndSlop;
                _maximum._parentPhaseEndSlop = long.Max(_maximum._parentPhaseEndSlop, info._parentPhaseEndSlop);
            }
#if MEASURE_CLRAPI_CALLS
            else
            {
                _totMethods++;

                // Update the "global" CLR-API values.
                _total._allClrApiCalls += info._allClrApiCalls;
                _maximum._allClrApiCalls = int.Max(_maximum._allClrApiCalls, info._allClrApiCalls);

                _total._allClrApiCycles += info._allClrApiCycles;
                _maximum._allClrApiCycles = long.Max(_maximum._allClrApiCycles, info._allClrApiCycles);

                // Update the per-phase CLR-API values.
                _total._invokesByPhase[PHASE_CLR_API] += info._allClrApiCalls;
                _maximum._invokesByPhase[PHASE_CLR_API] = int.Max(_maximum._perClrApiCalls[PHASE_CLR_API], info._allClrApiCalls);

                _total._cyclesByPhase[PHASE_CLR_API] += info._allClrApiCycles;
                _maximum._cyclesByPhase[PHASE_CLR_API] = long.Max(_maximum._cyclesByPhase[PHASE_CLR_API], info._allClrApiCycles);
            }

            for (int i = 0; i < API_ICorJitInfo_Names.API_COUNT; i++)
            {
                _total._perClrApiCalls[i] += info._perClrApiCalls[i];
                _maximum._perClrApiCalls[i] = int.Max(_maximum._perClrApiCalls[i], info._perClrApiCalls[i]);

                _total._perClrApiCycles[i] += info._perClrApiCycles[i];
                _maximum._perClrApiCycles[i] = long.Max(_maximum._perClrApiCycles[i], info._perClrApiCycles[i]);

                _maximum._maxClrApiCycles[i] = long.Max(_maximum._maxClrApiCycles[i], info._maxClrApiCycles[i]);
            }
#endif
        }
    }

    // Print the summary information to "f".
    // This is not thread-safe; assumed to be called by only one thread.
    public readonly void Print(StreamWriter? streamWriter)
    {
        if (streamWriter is null)
        {
            return;
        }

        var totTime_ms = 0.0;

        streamWriter.WriteLine("JIT Compilation time report:");
        streamWriter.WriteLine($"  Compiled {_numMethods} methods.");

        if (_numMethods is not 0)
        {
            totTime_ms = Stopwatch.GetElapsedTime(0, _total._totalCycles).TotalMilliseconds;

            streamWriter.WriteLine($"  Compiled {_total._byteCodeBytes} bytecodes total ({_maximum._byteCodeBytes} max, {_total._byteCodeBytes / (double)(_numMethods),8:F2} avg).");
            streamWriter.WriteLine($"  Time: total: {(_total._totalCycles / 1000000.0),10:F3} Mcycles/{totTime_ms,10:F3} ms");
            streamWriter.WriteLine($"          max: {(_maximum._totalCycles) / 1000000.0,10:F3} Mcycles/{Stopwatch.GetElapsedTime(0, _maximum._totalCycles).TotalMilliseconds,10:F3} ms");
            streamWriter.WriteLine($"          avg: {(_total._totalCycles) / 1000000.0 / _numMethods,10:F3} Mcycles/{totTime_ms / _numMethods,10:F3} ms");

            var extraHdr1 = "";
            var extraHdr2 = "";

#if MEASURE_CLRAPI_CALLS
            var extraInfo = JitConfig[ConfigInteger.JitEECallTimingInfo] is not 0;

            if (extraInfo)
            {
                extraHdr1 = "    CLRs/meth   % in CLR";
                extraHdr2 = "-----------------------";
            }
#endif

            streamWriter.WriteLine();
            streamWriter.WriteLine("  Total time by phases:");
            streamWriter.WriteLine($"     PHASE                          inv/meth   Mcycles    time (ms)  % of total    max (ms){extraHdr1}");
            streamWriter.WriteLine($"     ---------------------------------------------------------------------------------------{extraHdr2}");

            // Ensure that at least the names array and the Phases enum have the same number of entries:
            for (var phase = default(Phases); phase < PHASE_NUMBER_OF; phase++)
            {
                var phase_tot_ms = Stopwatch.GetElapsedTime(0, _total._cyclesByPhase[(int)(phase)]).TotalMilliseconds;
                var phase_max_ms = Stopwatch.GetElapsedTime(0, _maximum._cyclesByPhase[(int)(phase)]).TotalMilliseconds;

#if MEASURE_CLRAPI_CALLS  
                if ((phase is PHASE_CLR_API) && !extraInfo)
                {
                    // Skip showing CLR API call info if we didn't collect any
                    continue;
                }
#endif

                // Indent nested phases, according to depth.
                var ancPhase = phase.Parent;

                while (ancPhase != (Phases)(-1))
                {
                    streamWriter.Write("  ");
                    ancPhase = ancPhase.Parent;
                }
                streamWriter.Write($"     {phase.Name,-30} {_total._invokesByPhase[(int)(phase)] / (double)(_numMethods),6:F2}  {_total._cyclesByPhase[(int)(phase)] / 1000000.0,10:F2}   {phase_tot_ms,9:F3}   {((phase_tot_ms * 100.0) / totTime_ms),8:F2}%    {phase_max_ms,8:F3}");

#if MEASURE_CLRAPI_CALLS
                if (extraInfo && (phase != PHASE_CLR_API))
                {
                    var nest_tot_ms  = Stopwatch.GetTimestamp(0, _total._clrCyclesByPhase[(int)(phase)]).TotalMilliseconds;
                    var nest_percent = (nest_tot_ms * 100.0) / totTime_ms;
                    var calls_per_fn = _total._clrInvokesByPhase[(int)(phase)] / (double)(_numMethods);

                    if ((nest_percent > 0.1) || (calls_per_fn > 10))
                    {
                        streamWriter.Write($"       {calls_per_fn,5:F1}   {nest_percent,8:F2}%");
                    }
                }
#endif
                streamWriter.WriteLine();
            }

            // Show slop if it's over a certain percentage of the total
            var pslop_pct = ((_total._parentPhaseEndSlop * 100000.0) / Stopwatch.Frequency) / totTime_ms;

            if (pslop_pct >= 1.0)
            {
                streamWriter.WriteLine();
                streamWriter.WriteLine($"  'End phase slop' should be very small (if not, there's unattributed time): {_total._parentPhaseEndSlop / 1000000.0,9:F3} Mcycles = {pslop_pct,3:F1}% of total.");
                streamWriter.WriteLine();
            }
        }

        if (_numFilteredMethods > 0)
        {
            var totFilteredTime_ms = Stopwatch.GetElapsedTime(0, _filtered._totalCycles).TotalMilliseconds;

            streamWriter.WriteLine($"  Compiled {_numFilteredMethods} methods that meet the filter requirement.");
            streamWriter.WriteLine($"  Compiled {_filtered._byteCodeBytes} bytecodes total ({_filtered._byteCodeBytes / (double)(_numFilteredMethods),8:F2} avg).");
            streamWriter.WriteLine($"  Time: total: {(_filtered._totalCycles / 1000000.0),10:F3} Mcycles/{totFilteredTime_ms,10:F3} ms");
            streamWriter.WriteLine($"          avg: {(_filtered._totalCycles / 1000000.0) / _numFilteredMethods,10:F3} Mcycles/{totFilteredTime_ms / (double)(_numFilteredMethods),10:F3} ms");
            streamWriter.WriteLine("  Total time by phases:");
            streamWriter.WriteLine("     PHASE                            inv/meth Mcycles    time (ms)  % of total");
            streamWriter.WriteLine("     --------------------------------------------------------------------------------------");

            for (var phase = default(Phases); phase < PHASE_NUMBER_OF; phase++)
            {
                var phase_tot_ms = Stopwatch.GetElapsedTime(0, _filtered._cyclesByPhase[(int)(phase)]).TotalMilliseconds;

                // Indent nested phases, according to depth.
                var ancPhase = phase.Parent;

                while (ancPhase != (Phases)(-1))
                {
                    streamWriter.Write("  ");
                    ancPhase = ancPhase.Parent;
                }
                streamWriter.WriteLine($"     {phase.Name,-30}  {_filtered._invokesByPhase[(int)(phase)] / ((double)(_numFilteredMethods)),5:F2}  {_filtered._cyclesByPhase[(int)phase] / 1000000.0,10:F2}   {phase_tot_ms,9:F3}   {(phase_tot_ms * 100.0) / totFilteredTime_ms,8:F2}%");
            }

            var fslop_ms = Stopwatch.GetElapsedTime(0, _filtered._parentPhaseEndSlop).TotalMilliseconds;

            if (fslop_ms > 1.0)
            {
                streamWriter.WriteLine();
                streamWriter.WriteLine($"  'End phase slop' should be very small (if not, there's unattributed time): {_filtered._parentPhaseEndSlop / 1000000.0,9:F3} Mcycles = {fslop_ms,3:F1}% of total.");
                streamWriter.WriteLine();
            }
        }

#if MEASURE_CLRAPI_CALLS
        if ((_total._allClrApiCalls > 0) && (_total._allClrApiCycles > 0))
        {
            streamWriter.WriteLine();

            if (_totMethods > 0)
            {
                streamWriter.WriteLine($"  Imported {_numMethods + _totMethods} methods.");
                streamWriter.WriteLine();
            }

            streamWriter.WriteLine("     CLR API                                   # calls   total time    max time     avg time   % of total");
            streamWriter.WriteLine("     -------------------------------------------------------------------------------");
            streamWriter.WriteLine("---------------------");

            var shownCalls  = 0;
            var shownMillis = 0.0;
#if DEBUG
            var checkedCalls = 0;
            var checkedMillis = 0.0;
#endif

            for (var pass = 0; pass < 2; pass++)
            {
                for (var api = default(API_ICorJitInfo_Names); api < API_COUNT; api++)
                {
                    var calls = _total._perClrApiCalls[(int)(api)];

                    if (calls is 0)
                    {
                        continue;
                    }

                    var ms = Stopwatch.GetElapsedTime(0, _total._perClrApiCycles[(int)(api)]).TotalMilliseconds;

                    // Don't show the small fry to keep the results manageable
                    if (ms < 0.5)
                    {
                        // We always show the following API because it is always called
                        // exactly once for each method and its body is the simplest one
                        // possible (it just returns an integer constant), and therefore
                        // it can be used to measure the overhead of adding the CLR API
                        // timing code. Roughly speaking, on a 3GHz x64 box the overhead
                        // per call should be around 40 ns when using RDTSC, compared to
                        // about 140 ns when using GetThreadCycles() under Windows.
                        if (api != API_getExpectedTargetArchitecture)
                        {
                            continue;
                        }
                    }

                    // In the first pass we just compute the totals.
                    if (pass is 0)
                    {
                        shownCalls += _total._perClrApiCalls[(int)(api)];
                        shownMillis += ms;
                        continue;
                    }

                    var max_ms = Stopwatch.GetElapsedTime(0, _maximum._maxClrApiCycles[(int)(api)]).TotalMilliseconds;

                    //                        API name  #calls,    total time   max time          avg time                               % of total
                    streamWriter.WriteLine($"     {api,-40} {calls,8} {ms,9:F1} ms {max_ms,8:F1} ms  {(1000000.0 * ms) / calls,8:F1} ns     {(100.0 * ms) / shownMillis,5:F1}%");

#if DEBUG
                    checkedCalls += _total._perClrApiCalls[(int)(api)];
                    checkedMillis += ms;
#endif
                }
            }

#if DEBUG
            assert(checkedCalls == shownCalls);
            assert(checkedMillis == shownMillis);
#endif

            if ((shownCalls > 0) || (shownMillis > 0))
            {
                streamWriter.WriteLine("     ----------------------------------------------------------------------------------------------------");
                streamWriter.Write($"     Total for calls shown above              {shownCalls,8} {shownMillis,10:F1} ms");

                if (totTime_ms > 0.0)
                {
                    streamWriter.Write($" ({(shownMillis * 100.0) / totTime_ms,4:F1}% of overall JIT time)");
                }
                streamWriter.WriteLine();
            }
            streamWriter.WriteLine();
        }
#endif

        streamWriter.WriteLine();
    }

    // This can use what ever data you want to determine if the value to be added
    // belongs in the filtered section (it's always included in the unfiltered section)
    private readonly bool IncludedInFilteredData(in CompTimeInfo info) => false;
}
#endif
