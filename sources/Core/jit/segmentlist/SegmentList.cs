// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

/// <summary>Represents a list of segments.</summary>
/// <remarks>Essentially a segment tree (but not stored as a tree) that supports boolean Add/Subtract operations of segments. Used to compute the remainder after replacements have been handled as part of a decomposed block operation in physical promotion. Also used to store non-padding of class layouts.</remarks>
public sealed partial class SegmentList : IEnumerable<SegmentList.Segment>
{
    private List<Segment> _segments;

    public SegmentList()
    {
        _segments = [];
    }

    /// <summary>Check if the segment tree is empty.</summary>
    public bool IsEmpty => _segments.Count == 0;

    /// <summary>Add a segment to the data structure.</summary>
    /// <param name="segment">The segment to add.</param>
    public void Add(Segment segment)
    {
        var index = BinarySearchEnd(segment.Start);

        if (index < 0)
        {
            index = ~index;
        }

        var segmentsList = _segments;
        segmentsList.Insert(index, segment);

        int endIndex;
        var segments = CollectionsMarshal.AsSpan(segmentsList);

        for (endIndex = index + 1; endIndex < segments.Length; endIndex++)
        {
            if (!segments[index].IntersectsOrAdjacent(segments[endIndex]))
            {
                break;
            }
            segments[index].Merge(segments[endIndex]);
        }
        segmentsList.RemoveRange(index + 1, endIndex - (index + 1));
    }

    /// <summary>Compute a segment that covers all contained segments in this segment tree.</summary>
    /// <param name="result">The single segment. Only valid if the method returns true.</param>
    /// <returns>True if this segment tree was non-empty; otherwise false.</returns>
    public bool CoveringSegment(out Segment result)
    {
        var segments = CollectionsMarshal.AsSpan(_segments);

        if (segments.Length == 0)
        {
            result = default;
            return false;
        }
        else
        {
            result = new Segment(segments[0].Start, segments[^1].End);
        }
        return true;
    }

#if DEBUG
    /// <summary>Dump a string representation of the segment tree</summary>
    public void Dump()
    {
        var segments = CollectionsMarshal.AsSpan(_segments);

        if (segments.Length == 0)
        {
            jitprintf("<empty>");
        }
        else
        {
            var sep = "";

            foreach (var segment in segments)
            {
                jitprintf($"{sep}[{segment.Start:D3}..{segment.End:D3})");
                sep = " ";
            }
        }
    }
#endif

    public Enumerator GetEnumerator() => new Enumerator(_segments);

    /// <summary>Check if a segment intersects with any segment in this segment tree.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns>True if the input segment intersects with any segment in the tree; otherwise false.</returns>
    public bool Intersects(Segment segment)
    {
        var index = BinarySearchEnd(segment.Start);

        if (index < 0)
        {
            index = ~index;
        }
        else
        {
            // Start == segment[index].End, which makes it non-interesting.
            index++;
        }

        var segments = CollectionsMarshal.AsSpan(_segments);

        if (index >= segments.Length)
        {
            return false;
        }

        // Here we know Start < segment[index].End. Do they not intersect at all?
        if (segments[index].Start >= segment.End)
        {
            // Does not intersect any segment.
            return false;
        }

        assert(segments[index].Intersects(segment));
        return true;
    }

    /// <summary>Subtract a segment from the data structure.</summary>
    /// <param name="segment">The segment to subtract.</param>
    public void Subtract(Segment segment)
    {
        var index = BinarySearchEnd(segment.Start);

        if (index < 0)
        {
            index = ~index;
        }
        else
        {
            // Start == segment[index].End, which makes it non-interesting.
            index++;
        }

        var segmentsList = _segments;
        var segments = CollectionsMarshal.AsSpan(segmentsList);

        if (index >= segments.Length)
        {
            return;
        }

        // Here we know Start < segment[index].End. Do they not intersect at all?
        if (segments[index].Start >= segment.End)
        {
            // Does not intersect any segment.
            return;
        }

        assert(segments[index].Intersects(segment));

        if (segments[index].Contains(segment))
        {
            if (segment.Start > segments[index].Start)
            {
                // New segment (existing.Start, segment.Start)
                if (segment.End < segments[index].End)
                {
                    var insertedSegment = new Segment(segments[index].Start, segment.Start);
                    segmentsList.Insert(index, insertedSegment);

                    // And new segment (segment.End, existing.End)
                    segments = CollectionsMarshal.AsSpan(_segments);
                    segments[index + 1].Start = segment.End;
                    return;
                }

                segments[index].End = segment.Start;
                return;
            }
            if (segment.End < segments[index].End)
            {
                // New segment (segment.End, existing.End)
                segments[index].Start = segment.End;
                return;
            }

            // Full segment is being removed
            segmentsList.RemoveAt(index);
        }
        else
        {
            if (segment.Start > segments[index].Start)
            {
                segments[index].End = segment.Start;
                index++;
            }

            var endIndex = BinarySearchEnd(segment.End);

            if (endIndex >= 0)
            {
                segmentsList.RemoveRange(index, endIndex - index + 1);
            }
            else
            {
                endIndex = ~endIndex;

                if (endIndex == segments.Length)
                {
                    segmentsList.RemoveRange(index, segments.Length - index);
                }
                else
                {
                    if (segment.End > segments[endIndex].Start)
                    {
                        segments[endIndex].Start = segment.End;
                    }
                    segmentsList.RemoveRange(index, endIndex - index);
                }
            }
        }
    }

    /// <summary>Binary search the ends of segments stored.</summary>
    /// <param name="offset">The offset to search for</param>
    /// <returns>Index of the first entry with an equal 'End' offset, or bitwise complement of first entry with a higher 'End' offset.</returns>
    private int BinarySearchEnd(int offset)
    {
        var segments = CollectionsMarshal.AsSpan(_segments);

        var min = 0;
        var max = segments.Length;

        while (min < max)
        {
            var mid = min + (max - min) / 2;

            if (segments[mid].End == offset)
            {
                return mid;
            }
            else if (segments[mid].End < offset)
            {
                min = mid + 1;
            }
            else
            {
                max = mid;
            }
        }
        return ~min;
    }

    IEnumerator<Segment> IEnumerable<Segment>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
