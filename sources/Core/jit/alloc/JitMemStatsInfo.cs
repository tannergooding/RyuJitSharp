// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_MEM_ALLOC
using System.IO;
using System.Threading;

namespace RyuJitSharp;

internal sealed class ManagedAllocationStats
{
    private readonly Lock _sync = new();

    private uint _methodCount;
    private ulong _totalBytesAllocated;
    private ulong _maximumBytesAllocated;

    internal void Add(long managedBytesAllocated)
    {
        var bytesAllocated = unchecked((ulong)managedBytesAllocated);

        lock (_sync)
        {
            _methodCount = unchecked(_methodCount + 1);
            _totalBytesAllocated = unchecked(_totalBytesAllocated + bytesAllocated);

            if (bytesAllocated > _maximumBytesAllocated)
            {
                _maximumBytesAllocated = bytesAllocated;
            }
        }
    }

    internal void dumpAggregateMemStats(StreamWriter output)
    {
        uint methodCount;
        ulong totalBytesAllocated;
        ulong maximumBytesAllocated;

        lock (_sync)
        {
            methodCount = _methodCount;
            totalBytesAllocated = _totalBytesAllocated;
            maximumBytesAllocated = _maximumBytesAllocated;
        }

        output.Write($"For {methodCount,9} method compilations:\n");
        if (methodCount == 0)
        {
            return;
        }

        output.Write($"  managed bytes allocated: {totalBytesAllocated,12} (avg {totalBytesAllocated / methodCount,7} per compilation)\n");
        output.Write($"  maximum managed allocation: {maximumBytesAllocated,12} bytes\n\n");
    }

    internal void dumpMaxMemStats(StreamWriter output)
    {
        ulong maximumBytesAllocated;

        lock (_sync)
        {
            maximumBytesAllocated = _maximumBytesAllocated;
        }

        output.Write($"maximum managed allocation: {maximumBytesAllocated} bytes\n");
    }

    internal static void dumpMethodMemStats(StreamWriter output, long managedBytesAllocated)
    {
        var bytesAllocated = unchecked((ulong)managedBytesAllocated);
        output.Write($"managed bytes allocated: {bytesAllocated,12}\n");
    }
}

internal static class JitMemStatsInfo
{
    private static readonly ManagedAllocationStats s_stats = new();

    internal static void finishMemStats(long managedBytesAllocated)
    {
        s_stats.Add(managedBytesAllocated);
    }

    internal static void dumpAggregateMemStats(StreamWriter output)
    {
        s_stats.dumpAggregateMemStats(output);
    }

    internal static void dumpMaxMemStats(StreamWriter output)
    {
        s_stats.dumpMaxMemStats(output);
    }

    internal static void dumpMethodMemStats(StreamWriter output, long managedBytesAllocated)
    {
        ManagedAllocationStats.dumpMethodMemStats(output, managedBytesAllocated);
    }
}
#endif
