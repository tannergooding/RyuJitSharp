// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

/// <summary>Lowering information on fields of a struct passed by hardware floating-point calling convention on RISC-V and LoongArch</summary>
public struct CORINFO_FPSTRUCT_LOWERING
{
    /// <summary>Whether the struct should be passed by integer calling convention (cannot be passed by FP calling convention).</summary>
    public bool byIntegerCallConv;

    /// <summary>Types of lowered struct fields.</summary>
    /// <remarks>Note: the integer field is denoted with a signed type reflecting size only so e.g. ushort is reported as CORINFO_TYPE_SHORT and object or string is reported as CORINFO_TYPE_LONG.</remarks>
    public loweredElementsInlineArray loweredElements;

    /// <summary>Offsets of lowered struct fields.</summary>
    public offsetsInlineArray offsets;

    /// <summary>Number of lowered struct fields.</summary>
    public nuint numLoweredElements;

    [InlineArray(MAX_FPSTRUCT_LOWERED_ELEMENTS)]
    public struct loweredElementsInlineArray
    {
        public CorInfoType e0;
    }

    [InlineArray(MAX_FPSTRUCT_LOWERED_ELEMENTS)]
    public struct offsetsInlineArray
    {
        public uint e0;
    }
}
