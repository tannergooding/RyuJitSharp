// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public struct CORINFO_SWIFT_LOWERING
{
    public bool byReference;

    private _loweredElements_e__FixedBuffer _loweredElements;

    [UnscopedRef]
    public Span<CorInfoType> loweredElements => _loweredElements;

    private _offsets_e__FixedBuffer _offsets;

    [UnscopedRef]
    public Span<uint> offsets => _offsets;

    public nuint numLoweredElements;

    [InlineArray(MAX_SWIFT_LOWERED_ELEMENTS)]
    private struct _loweredElements_e__FixedBuffer
    {
        public CorInfoType e0;
    }

    [InlineArray(MAX_SWIFT_LOWERED_ELEMENTS)]
    private struct _offsets_e__FixedBuffer
    {
        public uint e0;
    }
}
