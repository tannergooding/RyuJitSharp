// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.BarrierKind;

namespace RyuJitSharp;

public enum BarrierKind
{
    /// <summary>full barrier</summary>
    BARRIER_FULL,

    /// <summary>load barrier</summary>
    BARRIER_LOAD_ONLY,

    /// <summary>store barrier</summary>
    BARRIER_STORE_ONLY, 
}
