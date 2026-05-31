// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public ref struct LikelyClassMethodHistogram
{
    // Sum of counts from all entries in the histogram. This includes "unknown" entries which are not captured in m_histogram
    public int _totalCount;

    // Rough guess at count of unknown handles
    public int _unknownHandles;

    // Histogram entries, in no particular order.
    public InlineArrayHistogramMaxSizeCount<LikelyClassMethodHistogramEntry> _histogram;

    public int countHistogramElements;

    public LikelyClassMethodHistogram(Span<int> histogramEntries)
    {
        LikelyClassMethodHistogramInner(histogramEntries);
    }

    public LikelyClassMethodHistogram(Span<nint> histogramEntries)
    {
        LikelyClassMethodHistogramInner(histogramEntries);
    }

    public void LikelyClassMethodHistogramInner<TElem>(Span<TElem> histogramEntries)
        where TElem : IBinaryInteger<TElem>
    {
        _unknownHandles = 0;
        _totalCount = 0;

        for (var k = 0; k < histogramEntries.Length; k++)
        {
            if (TElem.IsZero(histogramEntries[k]))
            {
                continue;
            }
            _totalCount++;

            var currentEntry = nint.CreateTruncating(histogramEntries[k]);

            for (var h = 0; h < countHistogramElements; h++)
            {
                if (_histogram[h]._handle == currentEntry)
                {
                    _histogram[h]._count++;
                    return;
                }
            }

            if (countHistogramElements >= HISTOGRAM_MAX_SIZE_COUNT)
            {
                continue;
            }

            var newEntry = new LikelyClassMethodHistogramEntry {
                _handle = currentEntry,
                _count = 1,
            };
            _histogram[countHistogramElements++] = newEntry;
        }
    }

    public readonly LikelyClassMethodHistogramEntry HistogramEntryAt(int index)
    {
        return _histogram[index];
    }
}
