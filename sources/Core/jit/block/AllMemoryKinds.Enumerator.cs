// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public partial struct AllMemoryKinds
{
    public struct Enumerator : IEnumerator<MemoryKind>
    {
        private MemoryKind _current;

        public Enumerator(AllMemoryKinds allMemoryKinds)
        {
            _current = MemoryKindCount;
        }

        public readonly MemoryKind Current => _current;
       
        public bool MoveNext()
        {
            var current = _current;

            if (current is MemoryKindCount)
            {
                current = 0;
            }
            else
            {
                current++;
            }

            var succeeded = false;

            if (current is not MemoryKindCount)
            {
                _current = current;
                succeeded = true;
            }
            return succeeded;
        }

        public void Reset()
        {
            _current = MemoryKindCount;
        }

        readonly object IEnumerator.Current => Current;

        readonly void IDisposable.Dispose() { }
    }
}
