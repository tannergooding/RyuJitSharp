// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if CALL_ARG_STATS || COUNT_BASIC_BLOCKS || EMITTER_STATS || MEASURE_NODE_SIZE || MEASURE_MEM_ALLOC
using System;
using System.IO;
using System.Threading;

namespace RyuJitSharp;

public sealed class NodeCounts : Dumpable
{
    private readonly int[] _counts = new int[(int)GT_COUNT];

    public override void dump(StreamWriter output)
    {
        var entries = new (genTreeOps Oper, uint Count)[(int)GT_COUNT];

        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = ((genTreeOps)i, unchecked((uint)Volatile.Read(ref _counts[i])));
        }

        SortNative(entries.AsSpan(), new EntryLess());

        foreach (var (oper, count) in entries)
        {
            if (count == 0)
            {
                break;
            }

            output.Write($"{oper.Name,-20} : {count,7}\n");
        }
    }

    public void record(genTreeOps oper)
    {
        assert(oper < GT_COUNT);
        _ = Interlocked.Increment(ref _counts[(int)oper]);
    }

    private readonly struct EntryLess : INativeLess<(genTreeOps Oper, uint Count)>
    {
        public bool Less((genTreeOps Oper, uint Count) first, (genTreeOps Oper, uint Count) second)
        {
            if (first.Count > second.Count)
            {
                return true;
            }

            if (first.Count < second.Count)
            {
                return false;
            }

            return first.Oper < second.Oper;
        }
    }
}
#endif
