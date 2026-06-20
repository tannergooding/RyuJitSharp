// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class SegmentList
{
    public struct Segment
    {
        public int Start;
        public int End;

        public Segment(int start, int end)
        {
            Start = start;
            End = end;
        }

        /// <summary>Check if this segment contains another segment.</summary>
        /// <param name="other">The other segment.</param>
        /// <returns>True if so.</returns>
        public readonly bool Contains(Segment other) => (other.Start >= Start) && (other.End <= End);

        /// <summary>Check if this segment intersects another segment.</summary>
        /// <param name="other">The other segment.</param>
        /// <returns>True if so.</returns>
        public readonly bool Intersects(Segment other)
        {
            if (End <= other.Start)
            {
                return false;
            }

            if (other.End <= Start)
            {
                return false;
            }

            return true;
        }

        /// <summary>Check if this segment intersects or is adjacent to another segment.</summary>
        /// <param name="other">The other segment.</param>
        /// <returns>True if so.</returns>
        public readonly bool IntersectsOrAdjacent(Segment other)
        {
            if (End < other.Start)
            {
                return false;
            }

            if (other.End < Start)
            {
                return false;
            }

            return true;
        }

        /// <summary>Update this segment to also contain another segment.</summary>
        /// <param name="other">The other segment.</param>
        public void Merge(Segment other)
        {
            Start = int.Min(Start, other.Start);
            End = int.Max(End, other.End);
        }
    }
}
