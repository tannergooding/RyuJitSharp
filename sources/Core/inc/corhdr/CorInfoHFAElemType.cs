// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoHFAElemType;

namespace RyuJitSharp;

/// <summary>Enum used for HFA type recognition.</summary>
/// <remarks>Supported across architectures, so that it can be used in altjits and cross-compilation.</remarks>
public enum CorInfoHFAElemType : uint
{
    CORINFO_HFA_ELEM_NONE,

    CORINFO_HFA_ELEM_FLOAT,

    CORINFO_HFA_ELEM_DOUBLE,

    CORINFO_HFA_ELEM_VECTOR64,

    CORINFO_HFA_ELEM_VECTOR128,
}
