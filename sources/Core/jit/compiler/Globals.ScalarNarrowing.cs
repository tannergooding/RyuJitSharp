// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Threading;

namespace RyuJitSharp;

public static partial class Globals
{
    public static float forceCastToFloat(double value)
    {
        // Native Volatile<float> forces the intermediate to be materialized at the narrowed width.
        var narrowed = (float)value;

        return Volatile.Read(ref narrowed);
    }

    public static uint forceCastToUInt32(double value)
    {
        var narrowed = unchecked((uint)value);

        return Volatile.Read(ref narrowed);
    }

    public static uint ulo32(ulong value)
    {
        return unchecked((uint)value);
    }

    public static uint uhi32(ulong value)
    {
        return (uint)(value >> 32);
    }
}
