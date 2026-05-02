// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public struct CORINFO_SWIFT_LOWERING
{
    public bool byReference;

    public loweredElementsInlineArray loweredElements;

    public offsetsInlineArray offsets;

    public nuint numLoweredElements;

    [InlineArray(MAX_SWIFT_LOWERED_ELEMENTS)]
    public struct loweredElementsInlineArray
    {
        public CorInfoType e0;
    }

    [InlineArray(MAX_SWIFT_LOWERED_ELEMENTS)]
    public struct offsetsInlineArray
    {
        public uint e0;
    }
}
