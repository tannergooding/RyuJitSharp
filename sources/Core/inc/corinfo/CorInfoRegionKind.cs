// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorInfoRegionKind;

namespace RyuJitSharp;

/// <summary>What type of code region we are in.</summary>
public enum CorInfoRegionKind
{
    CORINFO_REGION_NONE,

    CORINFO_REGION_HOT,

    CORINFO_REGION_COLD,

    CORINFO_REGION_JIT,
}
