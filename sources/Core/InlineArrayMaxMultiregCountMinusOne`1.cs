// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

[InlineArray(MAX_MULTIREG_COUNT - 1)]
public struct InlineArrayMaxMultiregCountMinusOne<T>
{
    public T e0;
}
