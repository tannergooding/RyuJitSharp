// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.bbCatchType;

namespace RyuJitSharp;

// Special values for bbCatchType, which is normally a class token of the catch handler.
// These special values will not collide with real tokens.

public enum bbCatchType
{
    BBCT_NONE = 0,

    BBCT_FAULT = -4,

    BBCT_FINALLY = -3,

    BBCT_FILTER = -2,

    BBCT_FILTER_HANDLER = -1,
}
