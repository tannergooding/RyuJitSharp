// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Data to optimize delegate construction.</summary>
public unsafe struct DelegateCtorArgs
{
    public void* pMethod;

    public void* pArg3;

    public void* pArg4;

    public void* pArg5;
}
