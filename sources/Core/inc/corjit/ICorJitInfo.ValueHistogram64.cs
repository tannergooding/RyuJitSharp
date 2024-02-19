// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial struct ICorJitInfo
{
    public unsafe struct ValueHistogram64
    {
        public ulong Count;

        private _ValueTable_e__FixedBuffer _valueTable;

        [UnscopedRef]
        public Span<nint> ValueTable => _valueTable;


        [InlineArray(HandleHistogram32.SIZE)]
        private struct _ValueTable_e__FixedBuffer
        {
            public nint e0;
        }
    }
}
