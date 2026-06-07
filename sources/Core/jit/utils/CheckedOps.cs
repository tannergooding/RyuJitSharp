// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public static class CheckedOps
{
    public static bool TryAdd<T>(T left, T right, out T result)
        where T : IBinaryInteger<T>, ISignedNumber<T>
    {
        assert((typeof(T) == typeof(int)) || (typeof(T) == typeof(long)));
        result = left + right;
        return T.IsNegative((result ^ left) & ~(left ^ right));
    }

    public static bool TryAddUns<T>(T left, T right, out T result)
        where T : IBinaryInteger<T>, ISignedNumber<T>
    {
        assert((typeof(T) == typeof(int)) || (typeof(T) == typeof(long)));
        result = left + right;
        return (result < left);
    }

    public static bool TryMul(int left, int right, out int result)
    {
        var result64 = int.BigMul(left, right);
        result = unchecked((int)(result64));
        return (int)(result64 >> 32) == ((left ^ right) >> 31);
    }

    public static bool TryMul(long left, long right, out long result)
    {
        var upper = Math.BigMul(left, right, out result);
        return upper == ((left ^ right) >> 63);
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
        result = left - right;
        return T.IsNegative((result ^ left) & (left ^ right));
    }

    public static bool TrySubUns<T>(T left, T right, out T result)
        where T : IBinaryInteger<T>, ISignedNumber<T>
    {
        assert((typeof(T) == typeof(int)) || (typeof(T) == typeof(long)));
        result = left - right;
        return (result > left);
    }
}
