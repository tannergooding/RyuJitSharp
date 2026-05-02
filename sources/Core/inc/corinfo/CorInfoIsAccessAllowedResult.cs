// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoIsAccessAllowedResult;

namespace RyuJitSharp;

public enum CorInfoIsAccessAllowedResult
{
    /// <summary>Call allowed.</summary>
    CORINFO_ACCESS_ALLOWED = 0,

    /// <summary>Call not allowed.</summary>
    CORINFO_ACCESS_ILLEGAL = 1,
}
