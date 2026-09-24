// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    // jitstd/algorithm.h: preserve pivot movement and comparison order, including
    // the insertion-sort cutoff, because profile-weight comparisons use tolerance.
    internal static void SortTrackedCandidates<TLess>(Span<int> values, TLess less)
        where TLess : struct, ILocalLess
    {
        assert(values.Length < int.MaxValue);
        if (values.IsEmpty)
        {
            return;
        }

        Span<int> firstStack = stackalloc int[32];
        Span<int> lastStack = stackalloc int[32];
        var depth = 0;
        var first = 0;
        var last = values.Length - 1;
        for (;;)
        {
            var count = last - first + 1;
            if (count <= 8)
            {
                for (var index = first; index < last; index++)
                {
                    var insertionIndex = index;
                    var value = values[insertionIndex + 1];
                    for (; (insertionIndex >= first) && less.Less(value, values[insertionIndex]); insertionIndex--)
                    {
                        values[insertionIndex + 1] = values[insertionIndex];
                    }

                    values[insertionIndex + 1] = value;
                }

                if (depth == 0)
                {
                    break;
                }

                depth--;
                first = firstStack[depth];
                last = lastStack[depth];
                continue;
            }

            var pivot = first + count / 2;
            if (less.Less(values[pivot], values[first]))
            {
                (values[pivot], values[first]) = (values[first], values[pivot]);
            }

            if (less.Less(values[last], values[pivot]))
            {
                (values[pivot], values[last]) = (values[last], values[pivot]);
                if (less.Less(values[pivot], values[first]))
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
                while ((newFirst != pivot) && less.Less(values[newFirst], values[pivot]));

                do
                {
                    newLast--;
                }
                while ((newLast != pivot) && less.Less(values[pivot], values[newLast]));

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

            // Process the smaller partition now to bound the explicit stack by
            // log2(n), as in native jitstd::quick_sort.
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

#if DEBUG
        for (var index = 0; index < values.Length - 1; index++)
        {
            assert(!less.Less(values[1], values[0]));
        }
#endif
    }
}
