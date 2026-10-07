// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using RyuJitSharp;

namespace RyuJitSharp.UnitTests;

internal static class JitStdAlgorithmTests
{
    private static readonly int[] s_expectedInsertionSort = [99, 1, 2, 3, 4, 5, 6, 7, 8, 88];
    private static readonly int[] s_expectedQuickSort = [99, 1, 2, 3, 4, 5, 6, 7, 8, 9, 88];

    [Test]
    public static void SortUsesInsertionSortForEightElementsAndPreservesRangeBoundaries()
    {
        int[] values = [99, 4, 1, 8, 3, 7, 2, 6, 5, 88];

        Globals.SortNative(values.AsSpan(1, 8), default(IntLess));

        Assert.That(values, Is.EqualTo(s_expectedInsertionSort));
    }

    [Test]
    public static void SortUsesQuickSortForNineElementsAndPreservesRangeBoundaries()
    {
        int[] values = [99, 6, 3, 9, 1, 7, 2, 8, 5, 4, 88];

        Globals.SortNative(values.AsSpan(1, 9), default(IntLess));

        Assert.That(values, Is.EqualTo(s_expectedQuickSort));
    }

    private readonly struct IntLess : Globals.INativeLess<int>
    {
        public bool Less(int first, int second) => first < second;
    }
}
