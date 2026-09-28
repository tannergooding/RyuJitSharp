// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.IO;
using System.Threading;

namespace RyuJitSharp;

public sealed class Histogram : Dumpable
{
    private readonly int _sizeCount;
    private readonly uint[] _sizeTable;
    private InlineArrayHistogramMaxSizeCount<uint> _counts;

    public Histogram(uint[] sizeTable)
    {
        _sizeTable = sizeTable;
        var sizeCount = 0;

        while ((sizeTable[sizeCount] != 0) && (sizeCount < HISTOGRAM_MAX_SIZE_COUNT - 1))
        {
            sizeCount++;
        }

        assert(sizeCount < HISTOGRAM_MAX_SIZE_COUNT);
        _sizeCount = sizeCount;
    }

    public override void dump(StreamWriter output)
    {
        uint total = 0;

        for (var i = 0; i <= _sizeCount; i++)
        {
            total = unchecked(total + Volatile.Read(ref _counts[i]));
        }

        if (total == 0)
        {
            output.Write("  (no data recorded)\n");
            return;
        }

        uint cumulative = 0;

        for (var i = 0; i <= _sizeCount; i++)
        {
            var count = Volatile.Read(ref _counts[i]);

            if (i == _sizeCount)
            {
                if (count == 0)
                {
                    break;
                }

                output.Write($"      >    {_sizeTable[i - 1],7}");
            }
            else
            {
                if (i == 0)
                {
                    output.Write("     <=    ");
                }
                else
                {
                    output.Write($"{unchecked(_sizeTable[i - 1] + 1),7} .. ");
                }

                output.Write($"{_sizeTable[i],7}");
            }

            cumulative = unchecked(cumulative + count);
            var percentage = (int)(100.0 * cumulative / total);
            output.Write($" ===> {count,7} count ({unchecked((uint)percentage),3}% of total)\n");
        }
    }

    public void record(uint size)
    {
        var i = 0;

        for (; i < _sizeCount; i++)
        {
            if (_sizeTable[i] >= size)
            {
                break;
            }
        }

        _ = Interlocked.Increment(ref _counts[i]);
    }
}
