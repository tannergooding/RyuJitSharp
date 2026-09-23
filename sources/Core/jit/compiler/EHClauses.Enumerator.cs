// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial struct EHClauses
{
    public ref struct Enumerator
    {
        private readonly ref EHblkDsc _first;
        private readonly ref EHblkDsc _end;
        private ref EHblkDsc _current;

        public Enumerator(ref EHblkDsc first, ushort count)
        {
            _first = ref first;
            _end = ref Unsafe.Add(ref first, count);
        }

        public readonly ref EHblkDsc Current => ref _current;

        public bool MoveNext()
        {
            if (Unsafe.AreSame(in _current, in _end))
            {
                return false;
            }

            ref var current = ref _current;

            if (!Unsafe.IsNullRef(ref current))
            {
                current = ref Unsafe.Add(ref current, 1);
            }
            else
            {
                current = ref _first;
            }

            _current = ref current;

            return !Unsafe.AreSame(in current, in _end);
        }

        public void Reset()
        {
            _current = ref Unsafe.NullRef<EHblkDsc>();
        }
    }
}
