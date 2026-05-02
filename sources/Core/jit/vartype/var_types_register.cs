// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.var_types_register;

namespace RyuJitSharp;

public enum var_types_register : byte
{
    VTR_UNKNOWN = 0,

    VTR_INT = 1,

    VTR_FLOAT = 2,

    VTR_MASK = 3,
}
