// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum TypeCompareState
{
    /// <summary>Types are not equal.</summary>
    MustNot = -1,

    /// <summary>Types may be equal (must test at runtime).</summary>
    May = 0,

    /// <summary>Types are equal.</summary>
    Must = 1,
}
