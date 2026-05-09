// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial struct PredEdgeList
{
    public struct Enumerator : IEnumerator<FlowEdge>
    {
        private readonly FlowEdge? _pred;
        private readonly bool _allowEdits;

        private FlowEdge? _current;

        // allowEdits=false
        //   try to guard against the user of the iterator from modifying the predecessor list being traversed,
        //   cache the edge we think should be next, then check it when we actually do the `MoveNext`
        //   This is a bit conservative, but attempts to protect against callers assuming too much about this iterator implementation.
        // allowEdits=true
        //   m_next is always used to update m_pred, so changes to m_pred don't break the iterator.
        private FlowEdge? _next;

        public Enumerator(FlowEdge? pred, bool allowEdits)
        {
            _pred = pred;

#if DEBUG
            _allowEdits = true;
#else
            _allowEdits = allowEdits;
#endif
        }

#nullable disable
        public readonly FlowEdge Current => _current;
#nullable restore

        [MemberNotNullWhen(true, nameof(Current))]
        public bool MoveNext()
        {
            var current = _current;
            var next = _next;

            if (current is not null)
            {
                if (_allowEdits)
                {
                    current = next;
                    _next = current?.NextPredEdge;
                }
                else
                {
                    var actualNext = current.NextPredEdge;
                    assert(next == actualNext);

#if DEBUG
                    _next = next?.NextPredEdge;
#endif

                    current = actualNext;
                }
            }
            else
            {
                current = _pred;

                if (_allowEdits)
                {
                    _next = current?.NextPredEdge;
                }
            }

            var succeeded = false;

            if (current is not null)
            {
                _current = current;
                succeeded = true;
            }
            return succeeded;
        }

        public void Reset()
        {
            _current = null;
            _next = null;
        }

        readonly object IEnumerator.Current => Current;

        readonly void IDisposable.Dispose() { }
    }
}
