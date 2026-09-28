// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if CALL_ARG_STATS || COUNT_BASIC_BLOCKS || EMITTER_STATS || MEASURE_NODE_SIZE || MEASURE_MEM_ALLOC
namespace RyuJitSharp;

public sealed class DumpOnShutdown
{
    private static readonly (string? Name, Dumpable? Dumpable)[] s_entries = new (string?, Dumpable?)[16];

    public DumpOnShutdown(string? name, Dumpable? dumpable)
    {
        for (var i = 0; i < s_entries.Length; i++)
        {
            ref var entry = ref s_entries[i];

            if ((entry.Name is null) && (entry.Dumpable is null))
            {
                entry = (name, dumpable);
                return;
            }
        }

        assert(false, "No space left in table");
    }

    public static void DumpAll()
    {
        foreach (var entry in s_entries)
        {
            if (entry.Name is not null)
            {
                jitprintf($"{entry.Name}\n");
            }

            if (entry.Dumpable is not null)
            {
                entry.Dumpable.dump(jitstdout());
                jitprintf("\n");
            }
        }
    }
}
#endif
