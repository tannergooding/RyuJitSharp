// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

/// <summary>describes inline candidate argument and local variable properties.</summary>
public partial struct InlLclVarInfo
{
    [Flags]
    private enum Flags : byte
    {
        None = 0,
        HasLdlocaOp = 1 << 0,
        HasStlocOp = 1 << 1,
        HasMultipleStlocOp = 1 << 2,
        IsPinned = 1 << 3
    }
}
