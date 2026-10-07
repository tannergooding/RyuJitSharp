// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

internal static class JitStd
{
    internal static void Sort<T>(Span<T> values, Func<T, T, bool> less)
    {
        assert(values.Length == 0 || (values.Length - 1) < int.MaxValue);

        if (values.Length != 0)
        {
            QuickSort(values, 0, values.Length - 1, less);

#if DEBUG
            for (var i = 0; i < values.Length - 1; i++)
            {
                assert(!less(values[1], values[0]));
            }
#endif
        }
    }

    private static void InsertionSort<T>(Span<T> values, int first, int last, Func<T, T, bool> less)
    {
        for (var i = first; i < last; i++)
        {
            var j = i;
            var temp = values[j + 1];

            for (; (j >= first) && less(temp, values[j]); j--)
            {
                values[j + 1] = values[j];
            }

            values[j + 1] = temp;
        }
    }

    private static void QuickSort<T>(Span<T> values, int first, int last, Func<T, T, bool> less)
    {
        // Processing the smaller partition inline bounds the pending stack depth logarithmically.
        Span<int> firstStack = stackalloc int[32];
        Span<int> lastStack = stackalloc int[32];
        var depth = 0;

        for (;;)
        {
            var count = (last - first) + 1;

            // Use insertion sort for small partitions.
            if (count <= 8)
            {
                InsertionSort(values, first, last, less);

                if (depth == 0)
                {
                    break;
                }

                depth--;
                first = firstStack[depth];
                last = lastStack[depth];
                continue;
            }

            var pivot = first + (count / 2);

            if (less(values[pivot], values[first]))
            {
                (values[pivot], values[first]) = (values[first], values[pivot]);
            }

            if (less(values[last], values[pivot]))
            {
                (values[pivot], values[last]) = (values[last], values[pivot]);

                if (less(values[pivot], values[first]))
                {
                    (values[pivot], values[first]) = (values[first], values[pivot]);
                }
            }

            var newFirst = first;
            var newLast = last;

            for (;;)
            {
                do
                {
                    newFirst++;
                }
                while ((newFirst != pivot) && less(values[newFirst], values[pivot]));

                do
                {
                    newLast--;
                }
                while ((newLast != pivot) && less(values[pivot], values[newLast]));

                if (newFirst >= newLast)
                {
                    break;
                }

                (values[newFirst], values[newLast]) = (values[newLast], values[newFirst]);

                if (pivot == newFirst)
                {
                    pivot = newLast;
                }
                else if (pivot == newLast)
                {
                    pivot = newFirst;
                }
            }

            var leftFirst = first;
            var leftLast = newLast;
            var rightFirst = newLast + 1;
            var rightLast = last;

            assert(depth < firstStack.Length);

            if ((leftLast - leftFirst) < (rightLast - rightFirst))
            {
                firstStack[depth] = rightFirst;
                lastStack[depth] = rightLast;
                first = leftFirst;
                last = leftLast;
            }
            else
            {
                firstStack[depth] = leftFirst;
                lastStack[depth] = leftLast;
                first = rightFirst;
                last = rightLast;
            }

            depth++;
        }
    }
}
