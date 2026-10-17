// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Threading;

namespace RyuJitSharp;

public sealed class DumpOnShutdown
{
    private static bool s_hasEntries;

    private static class EntryTable
    {
        internal static readonly (string? Name, Dumpable? Dumpable)[] Entries = new (string?, Dumpable?)[16];
    }

    public DumpOnShutdown(string? name, Dumpable? dumpable)
    {
        var entries = EntryTable.Entries;

        for (var i = 0; i < entries.Length; i++)
        {
            ref var entry = ref entries[i];

            if ((entry.Name is null) && (entry.Dumpable is null))
            {
                entry = (name, dumpable);
                Volatile.Write(ref s_hasEntries, true);
                return;
            }
        }

        assert(false, "No space left in table");
    }

    public static void DumpAll()
    {
        if (!Volatile.Read(ref s_hasEntries))
        {
            return;
        }

        foreach (var entry in EntryTable.Entries)
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
