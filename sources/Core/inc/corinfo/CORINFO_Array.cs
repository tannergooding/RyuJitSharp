// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public struct CORINFO_Array
{
    private CORINFO_Object _base;

    /// <summary>The vtable for the object.</summary>
    [UnscopedRef]
    public unsafe ref CORINFO_MethodPtr* methTable => ref _base.methTable;

    private CORINFO_ArrayLengthAndPadding _lengthAndPadding;

    [UnscopedRef]
    public ref int length => ref _lengthAndPadding.length;

    // Multi-dimensional arrays have the dimension lengths and bounds here.
    // The element count of these arrays is the array rank (the number of dimensions in the
    // multi-dimensional array). So, there is one element for each dimension. The upper bound
    // of a dimension is `dimBound[d] + dimLength[d] - 1`.

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

// The native header exposes a 32-bit length in a host-pointer-sized slot.
[StructLayout(LayoutKind.Explicit)]
internal struct CORINFO_ArrayLengthAndPadding
{
    [FieldOffset(0)]
    public int length;

    [FieldOffset(0)]
    public nint hostSlot;
}
