// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool AreContiguous<TEnum>(TEnum value1, TEnum value2)
        where TEnum : unmanaged, Enum
    {
        if (sizeof(TEnum) == sizeof(byte))
        {
            return ((byte)(object)(value1) + 1) == (byte)(object)(value2);
        }
        else if (sizeof(TEnum) == sizeof(ushort))
        {
            return ((ushort)(object)(value1) + 1) == (ushort)(object)(value2);
        }
        else if (sizeof(TEnum) == sizeof(uint))
        {
            return ((uint)(object)(value1) + 1) == (uint)(object)(value2);
        }
        else
        {
            return ((ulong)(object)(value1) + 1) == (ulong)(object)(value2);
        }
    }

    public static bool AreContiguous<TEnum>(params ReadOnlySpan<TEnum> values)
        where TEnum : struct, Enum
    {
        var areContiguous = true;

        if (values.Length >= 2)
        {
            var previousValue = values[0];

            for (var i = 1; i < values.Length; i++)
            {
                var value = values[i];

                if (!AreContiguous(value, previousValue))
                {
                    areContiguous = false;
                    break;
                }

                previousValue = value;
            }
        }

        return areContiguous;
    }
}
