// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

// Keep capacity for the largest supported target; register-count loops use the target-specific maximum.
[InlineArray(3)]
public struct InlineArrayMaxMultiregCountMinusOne<T>
{
    public T e0;
}
