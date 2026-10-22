// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public static class CheckedOps
{
    public static bool CastFromIntOverflows(int fromValue, var_types toType, bool fromUnsigned) {
    switch (toType)
    {
        case TYP_BYTE or TYP_UBYTE or TYP_SHORT or TYP_USHORT or TYP_INT or TYP_UINT or TYP_LONG or TYP_ULONG:
            return fromUnsigned ? !FitsIn(toType, unchecked((uint)(fromValue))) : !FitsIn(toType, fromValue);
        case TYP_FLOAT or TYP_DOUBLE:
            return false;
        default:
            JitExceptionHelpers.ThrowUnreachableException();
            return default;
    }
}
    public static bool CastFromLongOverflows(long fromValue, var_types toType, bool fromUnsigned) {
    switch (toType)
    {
        case TYP_BYTE or TYP_UBYTE or TYP_SHORT or TYP_USHORT or TYP_INT or TYP_UINT or TYP_LONG or TYP_ULONG:
            return fromUnsigned ? !FitsIn(toType, unchecked((ulong)(fromValue))) : !FitsIn(toType, fromValue);
        case TYP_FLOAT or TYP_DOUBLE:
            return false;
        default:
            JitExceptionHelpers.ThrowUnreachableException();
            return default;
    }
}
    // Checked casts truncate toward zero: the exact valid interval is (MIN - 1, MAX + 1).
    // When MIN - 1 is not representable, use the inclusive MIN bound instead. This occurs
    // for float -> int/long and double -> long. Negating the conjunction also rejects NaNs.
    // See the floating-point cast boundary derivation in native jit/utils.cpp (CheckedOps).
    public static bool CastFromFloatOverflows(float fromValue, var_types toType) {
    switch (toType)
    {
        case TYP_BYTE:
            return !(-129.0f < fromValue && fromValue < 128.0f);
        case TYP_UBYTE:
            return !(-1.0f < fromValue && fromValue < 256.0f);
        case TYP_SHORT:
            return !(-32769.0f < fromValue && fromValue < 32768.0f);
        case TYP_USHORT:
            return !(-1.0f < fromValue && fromValue < 65536.0f);
        case TYP_INT:
            return !(-2147483648.0f <= fromValue && fromValue < 2147483648.0f);
        case TYP_UINT:
            return !(-1.0 < fromValue && fromValue < 4294967296.0f);
        case TYP_LONG:
            return !(-9223372036854775808.0 <= fromValue && fromValue < 9223372036854775808.0f);
        case TYP_ULONG:
            return !(-1.0f < fromValue && fromValue < 18446744073709551616.0f);
        case TYP_FLOAT or TYP_DOUBLE:
            return false;
        default:
            JitExceptionHelpers.ThrowUnreachableException();
            return default;
    }
}
    public static bool CastFromDoubleOverflows(double fromValue, var_types toType) {
    switch (toType)
    {
        case TYP_BYTE:
            return !(-129.0 < fromValue && fromValue < 128.0);
        case TYP_UBYTE:
            return !(-1.0 < fromValue && fromValue < 256.0);
        case TYP_SHORT:
            return !(-32769.0 < fromValue && fromValue < 32768.0);
        case TYP_USHORT:
            return !(-1.0 < fromValue && fromValue < 65536.0);
        case TYP_INT:
            return !(-2147483649.0 < fromValue && fromValue < 2147483648.0);
        case TYP_UINT:
            return !(-1.0 < fromValue && fromValue < 4294967296.0);
        case TYP_LONG:
            return !(-9223372036854775808.0 <= fromValue && fromValue < 9223372036854775808.0);
        case TYP_ULONG:
            return !(-1.0 < fromValue && fromValue < 18446744073709551616.0);
        case TYP_FLOAT or TYP_DOUBLE:
            return false;
        default:
            JitExceptionHelpers.ThrowUnreachableException();
            return default;
    }
}
    public static bool TryAdd<T>(T left, T right, out T result)
        where T : IBinaryInteger<T>, ISignedNumber<T>
    {
        assert((typeof(T) == typeof(int)) || (typeof(T) == typeof(long)));
        result = unchecked(left + right);
        return !T.IsNegative((result ^ left) & ~(left ^ right));
    }

    public static bool TryAddUns<T>(T left, T right, out T result)
        where T : IBinaryInteger<T>, ISignedNumber<T>
    {
        assert((typeof(T) == typeof(int)) || (typeof(T) == typeof(long)));
        result = unchecked(left + right);

        // Sign extension also preserves unsigned ordering when T is int.
        return ulong.CreateTruncating(result) >= ulong.CreateTruncating(left);
    }

    public static bool TryAlignUp(int value, int alignment, out int result)
    {
        assert(int.IsPow2(alignment));

        var adjustment = alignment - 1;
        var succeed = TryAdd(value, adjustment, out result);

        if (succeed)
        {
            result &= ~adjustment;
        }
        return succeed;
    }

    public static bool TryAlignUp(long value, long alignment, out long result)
    {
        assert(long.IsPow2(alignment));

        var adjustment = alignment - 1;
        var succeed = TryAdd(value, adjustment, out result);

        if (succeed)
        {
            result &= ~adjustment;
        }
        return succeed;
    }

    public static bool TryMul(int left, int right, out int result)
    {
        var result64 = int.BigMul(left, right);
        result = unchecked((int)(result64));
        return result64 == result;
    }

    public static bool TryMul(long left, long right, out long result)
    {
        var upper = Math.BigMul(left, right, out result);
        return upper == (result >> 63);
    }

    public static bool TryMulUns(int left, int right, out int result)
    {
        var result64 = uint.BigMul(unchecked((uint)(left)), unchecked((uint)(right)));
        result = unchecked((int)(result64));
        return (uint)(result64 >>> 32) == 0;
    }

    public static bool TryMulUns(long left, long right, out long result)
    {
        Unsafe.SkipInit(out result);
        var upper = Math.BigMul(unchecked((ulong)(left)), unchecked((ulong)(right)), out Unsafe.As<long, ulong>(ref result));
        return upper == 0;
    }

    public static bool TrySub<T>(T left, T right, out T result)
        where T : IBinaryInteger<T>, ISignedNumber<T>
    {
        assert((typeof(T) == typeof(int)) || (typeof(T) == typeof(long)));
        result = unchecked(left - right);
        return !T.IsNegative((result ^ left) & (left ^ right));
    }

    public static bool TrySubUns<T>(T left, T right, out T result)
        where T : IBinaryInteger<T>, ISignedNumber<T>
    {
        assert((typeof(T) == typeof(int)) || (typeof(T) == typeof(long)));
        result = unchecked(left - right);
        return ulong.CreateTruncating(result) <= ulong.CreateTruncating(left);
    }
}
