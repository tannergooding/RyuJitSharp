// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

using elemType = ulong;
using indexType = ulong;

namespace RyuJitSharp;

public static partial class Globals
{
    public const int LOG2_BITS_PER_ELEMENT = 5;
    public const int LOG2_ELEMENTS_PER_NODE = 2;
    public const int LOG2_BITS_PER_NODE = LOG2_BITS_PER_ELEMENT + LOG2_ELEMENTS_PER_NODE;
    public const int BITS_PER_ELEMENT = 1 << LOG2_BITS_PER_ELEMENT;
    public const int ELEMENTS_PER_NODE = 1 << LOG2_ELEMENTS_PER_NODE;
    public const int BITS_PER_NODE = 1 << LOG2_BITS_PER_NODE;

    // Native hash-bit-vector indices and elements are 64-bit only for AMD64; use wide storage
    // while retaining the original 32-bit truncation for every other target.
    internal static indexType NormalizeIndex(indexType index)
        => Configuration.TARGET_AMD64 ? index : unchecked((uint)index);

    internal static elemType NormalizeElement(elemType element)
        => Configuration.TARGET_AMD64 ? element : unchecked((uint)element);

    internal static indexType AddIndex(indexType left, indexType right)
        => NormalizeIndex(unchecked(left + right));

    public static int nearest_pow2(uint number)
    {
        return 1 << BitOperations.Log2(number);
    }

    internal static int log2(int number)
    {
        var result = 0;
        number >>= 1;

        while (number != 0)
        {
            result++;
            number >>= 1;
        }

        return result;
    }

    public static HbvWalk ForEachHbvBitSet(hashBv bv, Func<indexType, HbvWalk> func)
    {
        var visitor = new DelegateHbvBitVisitor(func);
        return ForEachHbvBitSet(bv, ref visitor);
    }

    public static HbvWalk ForEachHbvBitSet<TVisitor>(hashBv bv, ref TVisitor visitor)
        where TVisitor : struct, IHbvBitVisitor<TVisitor>, allows ref struct
    {
        for (var hashNum = 0; hashNum < bv.hashtable_size(); hashNum++)
        {
            var node = bv.nodeArr[hashNum];

            while (node is not null)
            {
                var baseIndex = node.baseIndex;

                for (var el = 0; el < node.numElements(); el++)
                {
                    var bits = NormalizeElement(node.elements[el]);

                    while (bits != 0)
                    {
                        var bit = BitOperations.TrailingZeroCount(bits);
                        var index = AddIndex(AddIndex(baseIndex, (indexType)(el * BITS_PER_ELEMENT)), (indexType)bit);
                        bits ^= (indexType)1 << bit;

                        if (visitor.Visit(index) == HbvWalk.Abort)
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

    private readonly struct DelegateHbvBitVisitor(Func<indexType, HbvWalk> visitor)
        : IHbvBitVisitor<DelegateHbvBitVisitor>
    {
        public HbvWalk Visit(ulong index) => visitor(index);
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static void pBit(indexType index)
    {
        jitprintf($"{unchecked((int)index)} ");
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static void SimpleDumpNode(hashBvNode node)
    {
        jitprintf($"base: {node.baseIndex}\n");
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static void DumpNode(hashBvNode node)
    {
        node.dump();
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static void SimpleDumpDualNode(
        hashBv left,
        hashBv right,
        hashBvNode? leftNode,
        hashBvNode? rightNode)
    {
        jitprintf("nodes: ");
        jitprintf(leftNode is null ? "----," : $"{leftNode.baseIndex},");
        jitprintf(rightNode is null ? "----\n" : $"{rightNode.baseIndex}\n");
    }
}
