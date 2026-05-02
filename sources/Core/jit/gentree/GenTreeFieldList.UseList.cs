// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial class GenTreeFieldList
{
    public struct UseList : IEnumerable<Use>
    {
        private Use? _head;
        private Use? _tail;

        public readonly Use? Head => _head;

        public readonly bool IsSorted
        {
            get
            {
                var previousOffset = 0;

                foreach (var use in this)
                {
                    var useOffset = use.Offset;

                    if (useOffset < previousOffset)
                    {
                        return false;
                    }
                    previousOffset = useOffset;
                }
                return true;
            }
        }

        public readonly UseEnumerator GetEnumerator() => new UseEnumerator(_head);

        public void AddUse(Use newUse)
        {
            assert(newUse.Next is null);

            if (_head is null)
            {
                _head = newUse;
            }
            else
            {
                assert(_tail is not null);
                _tail.Next = newUse;
            }

            _tail = newUse;
        }

        public void Clear()
        {
            _head = null;
            _tail = null;
        }

        public void InsertUse(Use insertAfter, Use newUse)
        {
            assert(newUse.Next is null);

            newUse.Next = insertAfter.Next;
            insertAfter.Next = newUse;

            if (_tail == insertAfter)
            {
                _tail = newUse;
            }
        }

        public void Reverse()
        {
            _tail = _head;
            _head = null;

            for (Use? next, use = _tail; use != null; use = next)
            {
                next = use.Next;
                use.Next = _head;
                _head = use;
            }
        }

        readonly IEnumerator<Use> IEnumerable<Use>.GetEnumerator() => GetEnumerator();

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
