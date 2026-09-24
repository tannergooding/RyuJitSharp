// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

#if TARGET_AMD64
using indexType = ulong;
#else
using indexType = uint;
#endif

namespace RyuJitSharp;

public static partial class Globals
{
    public const int LOG2_BITS_PER_ELEMENT = 5;
    public const int LOG2_ELEMENTS_PER_NODE = 2;
    public const int LOG2_BITS_PER_NODE = LOG2_BITS_PER_ELEMENT + LOG2_ELEMENTS_PER_NODE;
    public const int BITS_PER_ELEMENT = 1 << LOG2_BITS_PER_ELEMENT;
    public const int ELEMENTS_PER_NODE = 1 << LOG2_ELEMENTS_PER_NODE;
    public const int BITS_PER_NODE = 1 << LOG2_BITS_PER_NODE;

    public static HbvWalk ForEachHbvBitSet(hashBv bv, Func<indexType, HbvWalk> func)
    {
        for (var hashNum = 0; hashNum < bv.hashtable_size(); hashNum++)
        {
            var node = bv.nodeArr[hashNum];

            while (node is not null)
            {
                var baseIndex = node.baseIndex;

                for (var el = 0; el < node.numElements(); el++)
                {
                    var bits = node.elements[el];

                    while (bits != 0)
                    {
                        var bit = BitOperations.TrailingZeroCount(bits);
                        var index = baseIndex + (indexType)(el * BITS_PER_ELEMENT) + (indexType)bit;
                        bits ^= (indexType)1 << bit;

                        if (func(index) == HbvWalk.Abort)
                        {
                            return HbvWalk.Abort;
                        }
                    }
                }

                node = node.next;
            }
        }

        return HbvWalk.Continue;
    }
}
