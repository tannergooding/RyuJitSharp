// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial struct CallLateArgList
{
    public struct Enumerator : IEnumerator<CallArg>
    {
        private readonly CallArg? _use;
        private CallArg? _current;

        public Enumerator(CallArg? use)
        {
            _use = use;
        }

#nullable disable
        public readonly CallArg Current => _current;
#nullable restore

        [MemberNotNullWhen(true, nameof(Current))]
        public bool MoveNext()
        {
            var current = _current;

            if (current is not null)
            {
                current = current.LateNext;
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
