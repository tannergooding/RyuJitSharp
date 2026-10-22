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

        switch (Type.GetTypeCode(typeof(TDestination)))
        {
            case TypeCode.SByte:
                return TYP_BYTE;
            case TypeCode.Byte:
                return TYP_UBYTE;
            case TypeCode.Int16:
                return TYP_SHORT;
            case TypeCode.UInt16:
                return TYP_USHORT;
            case TypeCode.Int32:
                return TYP_INT;
            case TypeCode.UInt32:
                return TYP_UINT;
            case TypeCode.Int64:
                return TYP_LONG;
            case TypeCode.UInt64:
                return TYP_ULONG;
            default:
                JitExceptionHelpers.ThrowNotSupportedException(FatalJitErrorCode.SafeCvtDoesNotSupportDestinationTypeArgument, (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? typeof(TDestination) : default));
                return default;
        }
    }
}
