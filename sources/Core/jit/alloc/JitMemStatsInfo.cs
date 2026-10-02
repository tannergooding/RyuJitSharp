// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_MEM_ALLOC
using System.IO;

namespace RyuJitSharp;

internal struct JitMemStatsInfo
{
    internal static void dumpAggregateMemStats(StreamWriter output)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JitMemStatsInfo::dumpAggregateMemStats is not ported.");
    }

    internal static void dumpMaxMemStats(StreamWriter output)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "JitMemStatsInfo::dumpMaxMemStats is not ported.");
    }
}
#endif
