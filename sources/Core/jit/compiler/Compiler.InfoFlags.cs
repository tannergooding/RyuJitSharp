// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private enum InfoFlags : byte
    {
        None = 0,
        IsStatic = 1 << 0,
        IsVarArgs = 1 << 1,
        InitMem = 1 << 2,
        ProfilerCallback = 1 << 3,
        PublishStubParam = 1 << 4,
        HasNextCallRetAddr = 1 << 5,
        UsesAsyncContinuation = 1 << 6,
    }
}
