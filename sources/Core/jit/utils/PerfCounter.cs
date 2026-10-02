// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_JIT_METHOD_PERF
using System.Diagnostics;

namespace RyuJitSharp;

public sealed class PerfCounter
{
    private long _beg;
    private double _freq;

    public bool Start()
    {
        _freq = (double)Stopwatch.Frequency / 1000.0;
        _beg = Stopwatch.GetTimestamp();

        return true;
    }

    public double ElapsedTime()
    {
        return (double)(Stopwatch.GetTimestamp() - _beg) / _freq;
    }
}
#endif
