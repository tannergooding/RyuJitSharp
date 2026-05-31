// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Used to fill in missing contexts during late devirtualization.</summary>
public sealed class LateDevirtualizationInfo
{
    public unsafe CORINFO_METHOD_HANDLE methodHnd;

    public unsafe CORINFO_CONTEXT_HANDLE exactContextHnd;

    public ILLocation ilLocation;
}
