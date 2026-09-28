// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if CALL_ARG_STATS || COUNT_BASIC_BLOCKS || EMITTER_STATS || MEASURE_NODE_SIZE || MEASURE_MEM_ALLOC
using System.IO;

namespace RyuJitSharp;

public sealed class Counter(long initialValue = 0) : Dumpable
{
    public long Value = initialValue;

    public override void dump(StreamWriter output)
    {
        output.Write($"{Value}\n");
    }
}
#endif
