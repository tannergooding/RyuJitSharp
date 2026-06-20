// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class SegmentList
{
    public struct Enumerator : IEnumerator<Segment>
    {
        private List<Segment> _segments;
        private int _index;

        public Enumerator(List<Segment> segments)
        {
            _segments = segments;
            _index = -1;
        }

#nullable disable
        public readonly Segment Current => _segments[_index];
#nullable restore

        public bool MoveNext()
        {
            var index = _index + 1;
            var succeeded = false;

            if (index != _segments.Count)
            {
                _index = index;
                succeeded = true;
            }
            return succeeded;
        }

        public void Reset()
        {
            _index = -1;
        }

        readonly object IEnumerator.Current => Current;

        readonly void IDisposable.Dispose() { }
    }
}
