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

    private CORINFO_ArrayLengthAndPadding _lengthAndPadding;

    [UnscopedRef]
    public ref int length => ref _lengthAndPadding.length;

    // Multi-dimensional arrays have the lengths and bounds here

    // actually of variable size
    public unsafe CORINFO_Object* refElems;
}
