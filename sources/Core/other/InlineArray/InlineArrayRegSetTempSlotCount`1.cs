// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

[InlineArray(RegSet.TEMP_SLOT_COUNT)]
public struct InlineArrayRegSetTempSlotCount<T>
{
    public T e0;
}
