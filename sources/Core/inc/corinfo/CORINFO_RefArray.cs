// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public struct CORINFO_RefArray
{
    private CORINFO_Object _base;

    /// <summary>The vtable for the object.</summary>
    [UnscopedRef]
    public unsafe ref CORINFO_MethodPtr* methTable => ref _base.methTable;

    public uint length;

#if HOST_64BIT
    public uint alignpad;
#endif

#if false
    // Multi-dimensional arrays have the lengths and bounds here
    public uint dimLength[length];
    public uint dimBound[length];
#endif

    // actually of variable size
    public unsafe CORINFO_Object* refElems;
}
