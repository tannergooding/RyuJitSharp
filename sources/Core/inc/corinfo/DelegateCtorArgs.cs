// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Data to optimize delegate construction.</summary>
public struct DelegateCtorArgs
{
    public unsafe void* method;

    public unsafe void* arg3;

    public unsafe void* arg4;

    public unsafe void* arg5;
}
