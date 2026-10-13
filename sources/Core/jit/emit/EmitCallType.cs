// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.EmitCallType;

namespace RyuJitSharp;

public enum EmitCallType
{
    // Native ordinals vary by target; keep the xarch union values and compare call kinds symbolically.
    EC_FUNC_TOKEN = 0,
    EC_FUNC_TOKEN_INDIR = 1,
    EC_INDIR_R = 2,
    EC_INDIR_ARD = 3,
    EC_COUNT = 4,
}
