// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public unsafe struct CORINFO_String
{
    private CORINFO_Object _base;

    /// <summary>The vtable for the object.</summary>
    [UnscopedRef]
    public ref CORINFO_MethodPtr* methTable => ref _base.methTable;

    public uint stringLen;

    /// <summary>Actually of variable size.</summary>
    public char chars;
}
