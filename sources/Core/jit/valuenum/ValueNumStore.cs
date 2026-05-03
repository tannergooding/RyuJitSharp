// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed class ValueNumStore
{
    /// <summary>We will reserve "max unsigned" to represent "not a value number", for maps that might start uninitialized.</summary>
    public const ValueNum NoVN = uint.MaxValue;

    [Conditional("DEBUG")]
    public static void ValidateValueNumStoreStatics()
    {
        // TODO: Port ValueNumStore.ValidateValueNumStoreStatics
    }
}
