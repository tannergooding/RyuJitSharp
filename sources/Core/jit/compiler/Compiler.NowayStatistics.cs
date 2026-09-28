// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_NOWAY
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    private static NowayAssertMap? s_nowayAssertMap;

    public void RecordNowayAssert(ReadOnlySpan<char> filePath, int lineNumber, ReadOnlySpan<char> message)
    {
        s_nowayAssertMap ??= new NowayAssertMap();
        s_nowayAssertMap.Record(filePath.ToString(), unchecked((uint)lineNumber), message.ToString());
    }

    internal static unsafe void DisplayNowayAssertMap()
    {
        if (s_nowayAssertMap is null)
        {
            return;
        }

        var fileName = JitConfig.JitMeasureNowayAssertFile;

        if (fileName is not null)
        {
            var path = Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(fileName));
            using var file = OpenJitOutputFile(path);

            if (file is null)
            {
                jitstdout().Write($"Failed to open JitMeasureNowayAssertFile \"{path}\"\n");
                return;
            }

            using var output = new JitTextWriter(file, leaveOpen: true);
            PrintNowayAssertMap(output, includeHeader: false);
        }
        else
        {
            PrintNowayAssertMap(jitstdout(), includeHeader: true);
        }
    }

    private static void PrintNowayAssertMap(StreamWriter output, bool includeHeader)
    {
        assert(s_nowayAssertMap is not null);
        var entries = s_nowayAssertMap.Snapshot();
        SortNative(entries.AsSpan(), new NowayAssertCountLess());

        if (includeHeader)
        {
            output.Write("\nnoway_assert counts:\n");
            output.Write("count, file, line, text\n");
        }

        foreach (var entry in entries)
        {
            output.Write($"{entry.Count}, {entry.Location.File}, {entry.Location.Line}, \"{entry.Location.Condition}\"\n");
        }
    }

    internal readonly record struct NowayAssertCount(nuint Count, NowayAssertEntry Location);

    private readonly struct NowayAssertCountLess : INativeLess<NowayAssertCount>
    {
        public bool Less(NowayAssertCount first, NowayAssertCount second)
        {
            return unchecked((nint)second.Count) < unchecked((nint)first.Count);
        }
    }

    internal sealed class NowayAssertEntry(string file, uint line, string condition, uint hash, NowayAssertEntry? next)
    {
        internal readonly string File = file;
        internal readonly uint Line = line;
        internal readonly string Condition = condition;
        internal readonly uint Hash = hash;
        internal nuint Count = 1;
        internal NowayAssertEntry? Next = next;
    }

    internal sealed class NowayAssertMap
    {
        // FileLineToCountMap iteration precedes an unstable sort, so native bucket
        // and collision-chain order must be preserved even for equal counts.
        private static ReadOnlySpan<int> BucketCounts => [
            9, 23, 59, 131, 239, 433, 761, 1399, 2473, 4327, 7499, 12973, 22433,
            46559, 96581, 200341, 415517, 861719, 1787021, 3705617, 7684087,
            15933877, 33040633, 68513161, 142069021, 294594427, 733045421,
        ];

        private NowayAssertEntry?[] _buckets = [];
        private int _count;
        private int _maximum;

        internal void Record(string file, uint line, string condition)
        {
            var hash = line;

            foreach (var value in Encoding.UTF8.GetBytes(file))
            {
                hash = unchecked(hash + (uint)(sbyte)value);
            }

            if (_buckets.Length != 0)
            {
                for (var entry = _buckets[hash % (uint)_buckets.Length]; entry is not null; entry = entry.Next)
                {
                    if ((entry.Line == line) && string.Equals(entry.File, file, StringComparison.Ordinal))
                    {
                        entry.Count = unchecked(entry.Count + 1);
                        return;
                    }
                }
            }

            if (_count == _maximum)
            {
                Grow();
            }

            var bucket = hash % (uint)_buckets.Length;
            _buckets[bucket] = new NowayAssertEntry(file, line, condition, hash, _buckets[bucket]);
            _count++;
        }

        private void Grow()
        {
            var newSize = unchecked((uint)_count * 3 / 2 * 4 / 3);
            newSize = uint.Max(newSize, 7);

            if (newSize < (uint)_count)
            {
                NOMEM();
            }

            var size = 0;

            foreach (var candidate in BucketCounts)
            {
                if ((uint)candidate >= newSize)
                {
                    size = candidate;
                    break;
                }
            }

            if (size == 0)
            {
                NOMEM();
            }

            var buckets = new NowayAssertEntry[size];

            foreach (var bucket in _buckets)
            {
                var entry = bucket;

                while (entry is not null)
                {
                    var next = entry.Next;
                    var index = entry.Hash % (uint)size;
                    entry.Next = buckets[index];
                    buckets[index] = entry;
                    entry = next;
                }
            }

            _buckets = buckets;
            _maximum = (int)((uint)size * 3 / 4);
        }

        internal NowayAssertCount[] Snapshot()
        {
            var entries = new NowayAssertCount[_count];
            var i = 0;

            foreach (var bucket in _buckets)
            {
                for (var entry = bucket; entry is not null; entry = entry.Next)
                {
                    entries[i++] = new NowayAssertCount(entry.Count, entry);
                }
            }

            return entries;
        }
    }
}
#endif
