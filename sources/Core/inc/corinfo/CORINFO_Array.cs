// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public unsafe struct CORINFO_Array
{
    private CORINFO_Object _base;

    /// <summary>The vtable for the object.</summary>
    [UnscopedRef]
    public ref CORINFO_MethodPtr* methTable => ref _base.methTable;

    private _Anonymous1_e__Union _anonymous1;

    [UnscopedRef]
    public ref uint length => ref _anonymous1.length;

    // // Multi-dimensional arrays have the dimension lengths and bounds here.
    // // The element count of these arrays is the array rank (the number of dimensions in the
    // // multi-dimensional array). So, there is one element for each dimension. The upper bound
    // // of a dimension is `dimBound[d] + dimLength[d] - 1`.
    // public int dimLength[rank]; // Number of array elements in each dimension.
    // public int dimBound[rank];  // Lower bound of each dimension (possibly negative).

    // actually of variable size
    private _Anonymous2_e__Union _anonymous2;

    [UnscopedRef]
    public ref sbyte i1Elems => ref _anonymous2.i1Elems;

    [UnscopedRef]
    public ref byte u1Elems => ref _anonymous2.u1Elems;

    [UnscopedRef]
    public ref short i2Elems => ref _anonymous2.i2Elems;

    [UnscopedRef]
    public ref ushort u2Elems => ref _anonymous2.u2Elems;

    [UnscopedRef]
    public ref int i4Elems => ref _anonymous2.i4Elems;

    [UnscopedRef]
    public ref uint u4Elems => ref _anonymous2.u4Elems;

    [UnscopedRef]
    public ref float r4Elems => ref _anonymous2.r4Elems;

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
        public sbyte i1Elems;

        [FieldOffset(0)]
        public byte u1Elems;

        [FieldOffset(0)]
        public short i2Elems;

        [FieldOffset(0)]
        public ushort u2Elems;

        [FieldOffset(0)]
        public int i4Elems;

        [FieldOffset(0)]
        public uint u4Elems;

        [FieldOffset(0)]
        public float r4Elems;
    }
}
