// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public unsafe struct CORINFO_RefArray
{
    private CORINFO_Object _base;

    /// <summary>The vtable for the object.</summary>
    [UnscopedRef]
    public ref CORINFO_MethodPtr* methTable => ref _base.methTable;

    private _Anonymous_e__Union _anonymous;

    [UnscopedRef]
    public ref uint length => ref _anonymous.length;

    // / Multi-dimensional arrays have the lengths and bounds here
    // public uint dimLength[length];
    // public uint dimBound[length];

    // actually of variable size
    public CORINFO_Object* refElems;

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous_e__Union
    {
        [FieldOffset(0)]
        public uint length;

        [FieldOffset(0)]
        public nuint alignpad;
    }
}
