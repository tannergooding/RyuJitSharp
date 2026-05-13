// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.HandleKindFlag;
using System;

namespace RyuJitSharp;

[Flags]
public enum HandleKindFlag : byte
{
    // Points to invariant data.
    HKF_INVARIANT = 1,

    // Points to non-null data.
    HKF_NONNULL = 2, 
}
