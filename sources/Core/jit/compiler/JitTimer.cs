// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_JIT_METHOD_PERF
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace RyuJitSharp;

/// <summary>Encapsulates a CompTimeInfo for a single compilation.</summary>
/// <remarks>This also tracks the start of compilation and when the current phase stated.</remarks>
public sealed class JitTimer
{
    /// <summary>Lock to protect the time log file.</summary>
    private static Lock s_csvLock = new Lock();

    /// <summary>The time log file handle.</summary>
    private static StreamWriter? s_csvFile;

    /// <summary>Start of the compilation.</summary>
    private long _start;

    /// <summary>Start of the current phase.</summary>
    private long _curPhaseStart;

#if MEASURE_CLRAPI_CALLS
    /// <summary>Start of the current CLR API call (if any).</summary>
    private long _clrCallStart;

    /// <summary>CLR API invokes under current outer so far</summary>
    private long _clrCallInvokes;

    /// <summary>CLR API  cycles under current outer so far.</summary>
    private long _clrCallCycles;

    /// <summary>The enum/index of the current CLR API call (or -1).</summary>
    private API_ICorJitInfo_Names _clrCallApiNum;
#endif

#if DEBUG
    /// <summary>The last phase that was completed (or (Phases)-1 to start).</summary>
    private Phases _lastPhase;
#endif

    /// <summary>The CompTimeInfo for this compilation.</summary>
    private CompTimeInfo _info;

    // Initialized the timer instance
    public JitTimer(int byteCodeSize)
    {
        _info = new CompTimeInfo(byteCodeSize);

#if DEBUG
        _lastPhase = (Phases)(-1);
#endif

#if MEASURE_CLRAPI_CALLS
        _clrCallApiNum = (API_ICorJitInfo_Names)(-1);
#endif

        var timestamp = Stopwatch.GetTimestamp();
        _start = timestamp;
        _curPhaseStart = timestamp;
    }

    public static void PrintCsvHeader()
    {
        var jitTimeLogCsv = Compiler.JitTimeLogCsv;

        if (string.IsNullOrEmpty(jitTimeLogCsv))
        {
            return;
        }

        lock (s_csvLock)
        {
            var streamWriter = s_csvFile;

            if (streamWriter is null)
            {
                streamWriter = new StreamWriter(jitTimeLogCsv, append: true);
                s_csvFile = streamWriter;
            }

            if (streamWriter.BaseStream.Length is 0)
            {
                streamWriter.Write("\"Method Name\",");
                streamWriter.Write("\"Assembly or SPMI Index\",");
                streamWriter.Write("\"IL Bytes\",");
                streamWriter.Write("\"Basic Blocks\",");
                streamWriter.Write("\"Min Opts\",");
                streamWriter.Write("\"Loops\",");
                streamWriter.Write("\"Loops Cloned\",");

#if DEBUG && FEATURE_LOOP_ALIGN
                streamWriter.Write("\"Alignment Candidates\",");
                streamWriter.Write("\"Loops Aligned\",");
#endif
                for (var phase = default(Phases); phase < PHASE_NUMBER_OF; phase++)
                {
                    streamWriter.Write($"\"{phase.Name}\",");

                    if ((JitConfig.JitMeasureIR is not 0) && phase.ReportsIRSize)
                    {
                        streamWriter.Write($"\"Node Count After {phase.Name}\",");
                    }
                }

                InlineStrategy.DumpCsvHeader(streamWriter);

                streamWriter.Write("\"Executable Code Bytes\",");
                streamWriter.Write("\"GC Info Bytes\",");
                streamWriter.Write("\"Total Bytes Allocated\",");
                streamWriter.Write("\"Total Cycles\",");
                streamWriter.Write("\"CPS\"\n");

                streamWriter.Flush();
            }
        }
    }

    public static void Shutdown()
    {
        lock (s_csvLock)
        {
            s_csvFile?.Close();
        }
    }

    // Ends the current phase (argument is for a redundant check).
    public void EndPhase(Compiler compiler, Phases phase)
    {
        // Otherwise...
        // We re-run some phases currently, so this following assert doesn't work.
        // assert((int)phase > (int)_lastPhase);  // We should end phases in increasing order.

        var timestamp = Stopwatch.GetTimestamp();
        var phaseCycles = (timestamp - _curPhaseStart);

        // If this is not a leaf phase, the assumption is that the last subphase must have just recently ended.
        // Credit the duration to "slop", the total of which should be very small.
        if (phase.HasChildren)
        {
            _info._parentPhaseEndSlop += phaseCycles;
        }
        else
        {
            // It is a leaf phase.  Credit duration to it.
            _info._invokesByPhase[(int)(phase)]++;
            _info._cyclesByPhase[(int)(phase)] += phaseCycles;

#if MEASURE_CLRAPI_CALLS
        // Record the CLR API timing info as well.
        _info._CLRinvokesByPhase[(int)(phase)] += _CLRcallInvokes;
        _info._CLRcyclesByPhase[(int)(phase)] += _CLRcallCycles;
#endif

            // Credit the phase's ancestors, if any.
            var ancPhase = phase.Parent;

            while (ancPhase != (Phases)(-1))
            {
                _info._cyclesByPhase[(int)(ancPhase)] += phaseCycles;
                ancPhase = ancPhase.Parent;
            }

#if MEASURE_CLRAPI_CALLS
            var lastPhase = PHASE_CLR_API;
#else
            var lastPhase = PHASE_NUMBER_OF;
#endif

            if ((phase + 1) == lastPhase)
            {
                _info._totalCycles = (timestamp - _start);
            }
            else
            {
                _curPhaseStart = timestamp;
            }
        }

        if ((JitConfig.JitMeasureIR is not 0) && phase.ReportsIRSize)
        {
            _info._nodeCountAfterPhase[(int)(phase)] = compiler.fgMeasureIR();
        }
        else
        {
            _info._nodeCountAfterPhase[(int)(phase)] = 0;
        }

#if DEBUG
        _lastPhase = phase;
#endif

#if MEASURE_CLRAPI_CALLS
        _clrCallInvokes = 0;
        _clrCallCycles = 0;
#endif
    }

    public unsafe void PrintCsvMethodStats(Compiler compiler)
    {
        var jitTimeLogCsv = Compiler.JitTimeLogCsv;

        if (jitTimeLogCsv is "")
        {
            return;
        }

        // eeGetMethodFullName uses locks, so don't enter crit sec before this call.
#if DEBUG || LATE_DISASM
        // If we already have computed the name because for some reason we're generating the CSV
        // for a DEBUG build (presumably not for the time info), just re-use it.
        var methName = compiler.info.compFullName;
#else
        var methName = compiler.eeGetMethodFullName(compiler.info.compMethodHnd);
#endif

        // Try and access the SPMI index to report in the data set.
        //
        // If the jit is not hosted under SPMI this will return the
        // default value of zero.
        //
        // Query the jit host directly here instead of going via the
        // config cache, since value will change for each method.
        var index = 0;

        fixed (byte* pName = "SuperPMIMethodContextNumber"u8)
        {
            index = CILJit.s_jitHost->getIntConfigValue(pName, defaultValue: -1);
        }

        lock (s_csvLock)
        {
            var streamWriter = s_csvFile;

            if (streamWriter is null)
            {
                return;
            }

            streamWriter.Write($"\"{methName}\",");

            if (index is not 0)
            {
                streamWriter.Write($"{index},");
            }
            else
            {
                var methodAssemblyName = compiler.eeGetClassAssemblyName(compiler.info.compClassHnd);
                streamWriter.Write($"\"{methodAssemblyName}\",");
            }

            streamWriter.Write($"{compiler.info.compILCodeSize},");
            streamWriter.Write($"{compiler.fgBBcount},");
            streamWriter.Write($"{(compiler.opts.MinOpts ? 1 : 0)},");
            streamWriter.Write($"{compiler.Metrics.LoopsFoundDuringOpts},");
            streamWriter.Write($"{compiler.Metrics.LoopsCloned},");

#if DEBUG && FEATURE_LOOP_ALIGN
            streamWriter.Write($"{compiler.Metrics.LoopAlignmentCandidates},");
            streamWriter.Write($"{compiler.Metrics.LoopsAligned},");
#endif

            var totCycles = 0L;

            for (var phase = default(Phases); phase < PHASE_NUMBER_OF; phase++)
            {
                if (!phase.HasChildren)
                {
                    totCycles += _info._cyclesByPhase[(int)(phase)];
                }
                streamWriter.Write($"{_info._cyclesByPhase[(int)(phase)]},");

                if ((JitConfig.JitMeasureIR is not 0) && phase.ReportsIRSize)
                {
                    streamWriter.Write($"{_info._nodeCountAfterPhase[(int)(phase)]},");
                }
            }

            var inlineStrategy = compiler._inlineStrategy;
            assert(inlineStrategy is not null);
            inlineStrategy.DumpCsvData(streamWriter);

            streamWriter.Write($"{compiler.info.compNativeCodeSize},");
            streamWriter.Write($"{compiler.compInfoBlkSize},");
            streamWriter.Write($"{0},");
            streamWriter.Write($"{_info._totalCycles},");
            streamWriter.WriteLine($"{Stopwatch.Frequency:F}");

            streamWriter.Flush();
        }
    }

#if MEASURE_CLRAPI_CALLS
    /// <summary>Start the stopwatch for an EE call.</summary>
    /// <param name="apix">The API index</param>
    public void ClrApiCallEnter(API_ICorJitInfo_Names apix)
    {
        // Nested calls not allowed
        assert((int)(_clrCallApiNum) == -1);

        _clrCallApiNum = apix;
        _clrCallStart = Stopwatch.GetTimestamp();
    }

    /// <summary>compute / record time spent in an EE call.</summary>
    /// <param name="apix">The API's "enum API_ICorJitInfo_Names" value; this value should match the value passed to the most recent call to <see cref="ClrApiCallEnter(API_ICorJitInfo_Names)" /> (i.e. these must come as matched pairs), and they also may not nest.</param>
    public void ClrApiCallLeave(API_ICorJitInfo_Names apix)
    {
        // Make sure we're actually inside a measured CLR call.
        assert(_clrCallApiNum == apix);

        _clrCallApiNum = (API_ICorJitInfo_Names)(-1);

        // Ignore this one if we don't have a valid starting counter.
        if (JitConfig.JitEECallTimingInfo is not 0)
        {
            // Compute the cycles spent in the call.
            assert(_clrCallStart is not 0);

            var elapsed = Stopwatch.GetTimestamp() - _clrCallStart;
            _clrCallStart = 0;

            // Add the cycles to the 'phase' and bump its use count.
            _info._cyclesByPhase[PHASE_CLR_API] += elapsed;
            _info._invokesByPhase[PHASE_CLR_API] += 1;

            // Add the values to the "per API" info.
            _info._allClrAPIcycles += elapsed;
            _info._allClrAPIcalls += 1;

            _info._perClrApiCalls[apix] += 1;
            _info._perClrApiCycles[apix] += elapsed;
            _info._maxClrApiCycles[apix] = long.Max(_info._maxClrApiCycles[apix], elapsed);

            // Subtract the cycles from the enclosing phase by bumping its start time
            _curPhaseStart += elapsed;

            // Update the running totals.
            _clrCallInvokes += 1;
            _clrCallCycles += elapsed;
        }
    }
#endif

    // Completes the timing of the current method, which is assumed to have "byteCodeBytes" bytes of bytecode, and adds it to "sum".
    public void Terminate(Compiler comp, in CompTimeSummaryInfo sum, bool includePhases)
    {
        if (includePhases)
        {
            PrintCsvMethodStats(comp);
        }
        sum.AddInfo(_info, includePhases);
    }
#endif
}
