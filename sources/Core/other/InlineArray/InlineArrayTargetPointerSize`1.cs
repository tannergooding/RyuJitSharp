// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

// This backing array covers 64-bit targets; callers bound accesses by the configured target pointer size.
[InlineArray(sizeof(ulong))]
public struct InlineArrayTargetPointerSize<T>
{
    public T e0;
}
