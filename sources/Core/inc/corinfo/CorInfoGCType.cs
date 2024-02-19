// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoGCType;

namespace RyuJitSharp;

public enum CorInfoGCType
{
    /// <summary>No embedded object refs.</summary>
    TYPE_GC_NONE,

    /// <summary>Is an object ref.</summary>
    TYPE_GC_REF,

    /// <summary>Is an interior pointer - promote it but don't scan it.</summary>
    TYPE_GC_BYREF,

    /// <summary>Requires type-specific treatment.</summary>
    TYPE_GC_OTHER,
}
