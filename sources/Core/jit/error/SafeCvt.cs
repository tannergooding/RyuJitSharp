// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public partial class Globals
{
    public static TDestination SafeCvtAssert<TDestination, TSource>(TSource value)
        where TDestination : IBinaryInteger<TDestination>
        where TSource : IBinaryInteger<TSource>
    {
        assert(IsSafeCvtInRange<TDestination, TSource>(value));
        return TDestination.CreateTruncating(value);
    }

    public static TDestination SafeCvtNowayAssert<TDestination, TSource>(TSource value)
        where TDestination : IBinaryInteger<TDestination>
        where TSource : IBinaryInteger<TSource>
    {
        noway_assert(IsSafeCvtInRange<TDestination, TSource>(value));
        return TDestination.CreateTruncating(value);
    }

    private static bool IsSafeCvtInRange<TDestination, TSource>(TSource value)
        where TDestination : IBinaryInteger<TDestination>
        where TSource : IBinaryInteger<TSource>
    {
        var destinationType = GetSafeCvtDestinationType<TDestination>();

        if (TSource.IsNegative(value))
        {
            var signedValue = long.CreateSaturating(value);
            return (TSource.CreateChecked(signedValue) == value) && FitsIn(destinationType, signedValue);
        }

        var unsignedValue = ulong.CreateSaturating(value);
        return (TSource.CreateChecked(unsignedValue) == value) && FitsIn(destinationType, unsignedValue);
    }

    private static var_types GetSafeCvtDestinationType<TDestination>()
        where TDestination : IBinaryInteger<TDestination>
    {
        if (typeof(TDestination) == typeof(nint))
        {
            return (IntPtr.Size == 8) ? TYP_LONG : TYP_INT;
        }

        if (typeof(TDestination) == typeof(nuint))
        {
            return (IntPtr.Size == 8) ? TYP_ULONG : TYP_UINT;
        }

        return Type.GetTypeCode(typeof(TDestination)) switch {
            TypeCode.SByte => TYP_BYTE,
            TypeCode.Byte => TYP_UBYTE,
            TypeCode.Int16 => TYP_SHORT,
            TypeCode.UInt16 => TYP_USHORT,
            TypeCode.Int32 => TYP_INT,
            TypeCode.UInt32 => TYP_UINT,
            TypeCode.Int64 => TYP_LONG,
            TypeCode.UInt64 => TYP_ULONG,
            _ => throw new NotSupportedException($"SafeCvt does not support destination type {typeof(TDestination)}."),
        };
    }
}
