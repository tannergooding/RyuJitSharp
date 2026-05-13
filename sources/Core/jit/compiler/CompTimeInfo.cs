// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

#if FEATURE_JIT_METHOD_PERF
/// <summary>for tracking the compilation time of one or more methods.</summary>
/// <remarks>
///   <para>We divide a compilation into a sequence of contiguous phases, and track the total(per - thread) cycles of the compilation, as well as the cycles for each phase.  We also track the number of bytecodes.</para>
///   <para>If there is a failure in reading a timer at any point, the "CompTimeInfo" becomes invalid, as indicated by "_timerFailure" being true.</para>
/// </remarks>
public struct CompTimeInfo
{
    public int _byteCodeBytes;
    public long _totalCycles;
    public InlineArrayPhaseCount<long> _invokesByPhase;
    public InlineArrayPhaseCount<long> _cyclesByPhase;

#if MEASURE_CLRAPI_CALLS
    public InlineArrayPhaseCount<long> _clrInvokesByPhase;
    public InlineArrayPhaseCount<long> _clrCyclesByPhase;
#endif

    public InlineArrayPhaseCount<int> _nodeCountAfterPhase;

    // For better documentation, we call EndPhase on
    // non-leaf phases.  We should also call EndPhase on the
    // last leaf subphase; obviously, the elapsed cycles between the EndPhase
    // for the last leaf subphase and the EndPhase for an ancestor should be very small.
    // We add all such "redundant end phase" intervals to this variable below; we print
    // it out in a report, so we can verify that it is, indeed, very small.  If it ever
    // isn't, this means that we're doing something significant between the end of the last
    // declared subphase and the end of its parent.
    public long _parentPhaseEndSlop;
    public bool _timerFailure;

#if MEASURE_CLRAPI_CALLS
    // The following measures the time spent inside each individual CLR API call.
    public int _allClrApiCalls;
    public int[] _perClrApiCalls;
    public long _allClrApiCycles;
    public long[] _perClrApiCycles;
    public long[] _maxClrApiCycles;
#endif

    public CompTimeInfo(int byteCodeBytes)
    {
        _byteCodeBytes = byteCodeBytes;

#if MEASURE_CLRAPI_CALLS
        _perClrApiCalls = new int[(int)(API_ICorJitInfo_Names.API_COUNT)];
        _perClrApiCycles = new long[(int)(API_ICorJitInfo_Names.API_COUNT)];
        _maxClrApiCycles = new long[(int)(API_ICorJitInfo_Names.API_COUNT)];
#endif
    }
}
#endif
