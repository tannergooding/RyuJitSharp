// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public readonly struct VNMemoryPhiDef(BasicBlock block, ReadOnlyMemory<int> ssaArgs)
{
    public BasicBlock Block { get; } = block;

    public ReadOnlyMemory<int> SsaArgs { get; } = ssaArgs;
}
