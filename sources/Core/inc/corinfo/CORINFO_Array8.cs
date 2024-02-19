// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public unsafe struct CORINFO_Array8
{
    private CORINFO_Object _base;

    /// <summary>The vtable for the object.</summary>
    [UnscopedRef]
    public ref CORINFO_MethodPtr* methTable => ref _base.methTable;

    private _Anonymous1_e__Union _anonymous1;

    [UnscopedRef]
    public ref uint length => ref _anonymous1.length;

    // actually of variable size
    private _Anonymous2_e__Union _anonymous2;

    [UnscopedRef]
    public ref double r8Elems => ref _anonymous2.r8Elems;

    [UnscopedRef]
    public ref long i8Elems => ref _anonymous2.i8Elems;

    [UnscopedRef]
    public ref ulong u8Elems => ref _anonymous2.u8Elems;

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous1_e__Union
    {
        [FieldOffset(0)]
        public uint length;

        [FieldOffset(0)]
        public nuint alignpad;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous2_e__Union
    {
        [FieldOffset(0)]
        public double r8Elems;

        [FieldOffset(0)]
        public long i8Elems;

        [FieldOffset(0)]
        public ulong u8Elems;
    }
}
