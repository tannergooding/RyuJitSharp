// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public partial class LIR
{
    public partial class ReadOnlyRange
    {
        public struct Enumerator : IEnumerator<GenTree>
        {
            private readonly GenTree? _node;
            private GenTree? _current;

            public Enumerator(GenTree? node)
            {
                _node = node;
            }

#nullable disable
            public readonly GenTree Current => _current;
#nullable restore

            /// <inheritdoc cref="IEnumerator.MoveNext" />
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
                    current = _node;
                }

                var succeeded = false;

                if (current is not null)
                {
                    _current = current;
                    succeeded = true;
                }
                return succeeded;
            }

            /// <inheritdoc cref="IEnumerator.Reset" />
            public void Reset()
            {
                _current = null;
            }

            readonly object IEnumerator.Current => Current;

            readonly void IDisposable.Dispose() { }
        }
    }
}
