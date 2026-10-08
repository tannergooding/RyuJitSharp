// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

internal enum JitStdIteratorCategory
{
    Input,
    Forward,
    Bidirectional,
    RandomAccess,
    NotAnIterator,
}

internal interface IJitStdIterator
{
    JitStdIteratorCategory Category { get; }
}

internal static class JitStdIteratorTraits<TIterator>
{
    internal static JitStdIteratorCategory Category(TIterator iterator)
    {
        if (iterator is IJitStdIterator jitStdIterator)
        {
            return jitStdIterator.Category;
        }

        if (IsInteger(typeof(TIterator)))
        {
            return JitStdIteratorCategory.NotAnIterator;
        }

        throw new NotSupportedException($"Iterator traits are not defined for {typeof(TIterator)}.");
    }

    internal static JitStdIteratorCategory PointerCategory => JitStdIteratorCategory.RandomAccess;

    private static bool IsInteger(Type type)
    {
        return (type == typeof(bool))
            || (type == typeof(char))
            || (type == typeof(sbyte))
            || (type == typeof(byte))
            || (type == typeof(short))
            || (type == typeof(ushort))
            || (type == typeof(int))
            || (type == typeof(uint))
            || (type == typeof(long))
            || (type == typeof(ulong));
    }
}
