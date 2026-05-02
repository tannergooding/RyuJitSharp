// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.gtCallTypes;

namespace RyuJitSharp;

public enum gtCallTypes : byte
{
    // User function

    CT_USER_FUNC, 
    // Jit-helper

    CT_HELPER,    
    // Indirect call

    CT_INDIRECT,  

    // fake entry (must be last)
    CT_COUNT,
}
