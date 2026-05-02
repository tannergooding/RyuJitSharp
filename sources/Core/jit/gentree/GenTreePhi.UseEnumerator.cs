// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial class GenTreePhi
{
    public struct UseEnumerator : IEnumerator<Use>
    {
        private readonly Use? _use;
        private Use? _current;

        public UseEnumerator(Use? use)
        {
            _use = use;
        }

#nullable disable
        public readonly Use Current => _current;
#nullable restore

        [MemberNotNullWhen(true, nameof(Current))]
        public bool MoveNext()
        {
            var current = _current;

            if (current is not null)
            {
                current = current.Next;
            }
            else
            {
                current = _use;
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
        }

        readonly object IEnumerator.Current => Current;

        readonly void IDisposable.Dispose() { }
    }
}
