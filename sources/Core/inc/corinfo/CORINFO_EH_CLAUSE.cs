// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public struct CORINFO_EH_CLAUSE
{
    public CORINFO_EH_CLAUSE_FLAGS Flags;

    public int TryOffset;

    public int TryLength;

    public int HandlerOffset;

    public int HandlerLength;

    private _Anonymous_e__Union _anonymous;

    // use for type-based exception handlers
    [UnscopedRef]
    public ref int ClassToken => ref _anonymous.ClassToken;

    // use for filter-based exception handlers (COR_ILEXCEPTION_FILTER is set)
    [UnscopedRef]
    public ref int FilterOffset => ref _anonymous.FilterOffset;

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous_e__Union
    {
        [FieldOffset(0)]
        public int ClassToken;

        [FieldOffset(0)]
        public int FilterOffset;
    }
}
