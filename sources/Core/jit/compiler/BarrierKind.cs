// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.BarrierKind;

namespace RyuJitSharp;

public enum BarrierKind
{
    // full barrier
    BARRIER_FULL,       

    // load barrier
    BARRIER_LOAD_ONLY,

    // store barrier
    BARRIER_STORE_ONLY, 
}
