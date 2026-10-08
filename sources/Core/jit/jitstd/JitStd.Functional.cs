// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

internal abstract class JitStdBinaryFunction<TArg1, TArg2, TResult>
{
    internal Type first_argument_type => typeof(TArg1);

    internal Type second_argument_type => typeof(TArg2);

    internal Type result_type => typeof(TResult);

    internal abstract TResult Invoke(TArg1 first, TArg2 second);
}

internal sealed class JitStdGreater<T> : JitStdBinaryFunction<T, T, bool>
    where T : IComparisonOperators<T, T, bool>
{
    internal override bool Invoke(T first, T second) => first > second;
}

internal static class JitStdComparison<T>
{
    internal static bool Greater(T first, T second)
    {
        if (typeof(T) == typeof(float))
        {
            return Unsafe.As<T, float>(ref first) > Unsafe.As<T, float>(ref second);
        }

        if (typeof(T) == typeof(double))
        {
            return Unsafe.As<T, double>(ref first) > Unsafe.As<T, double>(ref second);
        }

        return Comparer<T>.Default.Compare(first, second) > 0;
    }

    internal static bool LessOrEqual(T first, T second)
    {
        if (typeof(T) == typeof(float))
        {
            return Unsafe.As<T, float>(ref first) <= Unsafe.As<T, float>(ref second);
        }

        if (typeof(T) == typeof(double))
        {
            return Unsafe.As<T, double>(ref first) <= Unsafe.As<T, double>(ref second);
        }

        return Comparer<T>.Default.Compare(first, second) <= 0;
    }

    internal static bool Equal(T first, T second)
    {
        if (typeof(T) == typeof(float))
        {
            return Unsafe.As<T, float>(ref first) == Unsafe.As<T, float>(ref second);
        }

        if (typeof(T) == typeof(double))
        {
            return Unsafe.As<T, double>(ref first) == Unsafe.As<T, double>(ref second);
        }

        return EqualityComparer<T>.Default.Equals(first, second);
    }
}
