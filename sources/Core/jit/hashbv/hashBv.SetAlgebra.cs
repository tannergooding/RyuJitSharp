// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

#if TARGET_AMD64
using indexType = ulong;
#else
using indexType = uint;
#endif

namespace RyuJitSharp;

public sealed partial class hashBv
{
    private enum SetAction
    {
        And,
        Or,
        Subtract,
        Compare,
    }

    public static hashBv CreateFrom(hashBv other, Compiler compiler)
    {
        var result = Create(compiler);
        result.copyFrom(other, compiler);
        return result;
    }

    public void copyFrom(hashBv other, Compiler compiler) => copyFrom(other);

    private void copyFrom(hashBv other)
    {
        assert(!ReferenceEquals(this, other));

        ZeroAll();

        if (log2_hashSize != other.log2_hashSize)
        {
            nodeArr = new hashBvNode?[other.hashtable_size()];
            log2_hashSize = other.log2_hashSize;
        }

        for (var bucket = 0; bucket < hashtable_size(); bucket++)
        {
            ref var link = ref nodeArr[bucket];
            var source = other.nodeArr[bucket];

            while (source is not null)
            {
                var node = new hashBvNode(source.baseIndex);
                node.copyFrom(source);
                link = node;
                link = ref node.next;
                numNodes = unchecked((ushort)(numNodes + 1));
                source = source.next;
            }
        }

        assert(IsValid());
    }

    public void ZeroAll()
    {
        Array.Clear(nodeArr);
        numNodes = 0;
    }

    public bool CompareWith(hashBv other) => MultiTraverse(other, SetAction.Compare);

    public bool AndWithChange(hashBv other) => MultiTraverse(other, SetAction.And);

    public bool OrWithChange(hashBv other) => MultiTraverse(other, SetAction.Or);

    public bool SubtractWithChange(hashBv other) => MultiTraverse(other, SetAction.Subtract);

    public void AndWith(hashBv other)
    {
        _ = AndWithChange(other);
    }

    public void OrWith(hashBv other)
    {
        _ = OrWithChange(other);
    }

    public void Subtract(hashBv other)
    {
        _ = SubtractWithChange(other);
    }

    public void Subtract3(hashBv first, hashBv second)
    {
        copyFrom(first);
        Subtract(second);
    }

    public void UnionMinus(hashBv first, hashBv second, hashBv third)
    {
        Subtract3(first, second);
        _ = OrWithChange(third);
    }

    private bool MultiTraverse(hashBv other, SetAction action)
    {
        assert(numNodes == getNodeCount());

        if (action is SetAction.Or)
        {
            if (log2_hashSize + 2 < other.log2_hashSize)
            {
                Resize(other.numNodes);
            }

            if (other.numNodes > other.hashtable_size() * 4)
            {
                other.Resize(other.numNodes);
            }
        }

        if (log2_hashSize == other.log2_hashSize)
        {
            return MultiTraverseEqual(other, action);
        }
        else if (log2_hashSize > other.log2_hashSize)
        {
            return MultiTraverseLHSBigger(other, action);
        }
        else
        {
            return MultiTraverseRHSBigger(other, action);
        }
    }

    private bool MultiTraverseLHSBigger(hashBv other, SetAction action)
    {
        var otherSize = other.hashtable_size();
        var expansionFactor = hashtable_size() / otherSize;
        var cursors = new hashBvNode?[expansionFactor];
        var result = action is SetAction.Compare;
        var terminate = false;

        for (var bucket = 0; bucket < otherSize; bucket++)
        {
            Array.Clear(cursors);
            var rhs = other.nodeArr[bucket];

            while (rhs is not null)
            {
                var hash = getHashForIndex(rhs.baseIndex, hashtable_size());
                var slot = (hash - bucket) >> other.log2_hashSize;
                ref var cursor = ref (cursors[slot] is hashBvNode previous
                    ? ref previous.next
                    : ref nodeArr[hash]);
                var lhs = cursor;
                hashBvNode? advanced;

                if (lhs is null)
                {
                    advanced = ApplyLeftEmpty(action, ref cursor, ref rhs, ref result, ref terminate);
                }
                else if (lhs.baseIndex == rhs.baseIndex)
                {
                    advanced = ApplyBoth(action, ref cursor, ref rhs, ref result, ref terminate);
                }
                else if (lhs.baseIndex > rhs.baseIndex)
                {
                    advanced = ApplyLeftGap(action, ref cursor, ref rhs, ref result, ref terminate);
                }
                else
                {
                    advanced = ApplyRightGap(action, ref cursor, ref rhs, ref result, ref terminate);
                }

                if (terminate)
                {
                    return result;
                }

                if (advanced is not null)
                {
                    cursors[slot] = advanced;
                }
            }

            for (var slot = 0; slot < expansionFactor; slot++)
            {
                var hash = otherSize * slot + bucket;
                ref var cursor = ref (cursors[slot] is hashBvNode previous
                    ? ref previous.next
                    : ref nodeArr[hash]);

                while (cursor is not null)
                {
                    var advanced = ApplyRightGap(action, ref cursor, ref rhs, ref result, ref terminate);

                    if (terminate)
                    {
                        return result;
                    }

                    if (advanced is not null)
                    {
                        cursor = ref advanced.next;
                    }
                }
            }
        }

        assert(numNodes == getNodeCount());
        return result;
    }

    private bool MultiTraverseRHSBigger(hashBv other, SetAction action)
    {
        var otherSize = other.hashtable_size();
        var result = action is SetAction.Compare;
        var terminate = false;

        for (var bucket = 0; bucket < otherSize; bucket++)
        {
            var destination = getHashForIndex((indexType)(BITS_PER_NODE * bucket), hashtable_size());
            ref var cursor = ref nodeArr[destination];
            var rhs = other.nodeArr[bucket];

            while ((cursor is not null) && (rhs is not null))
            {
                var lhs = cursor;
                hashBvNode? advanced = null;

                if (lhs.baseIndex < rhs.baseIndex)
                {
                    if (getHashForIndex(lhs.baseIndex, otherSize) == bucket)
                    {
                        advanced = ApplyRightGap(action, ref cursor, ref rhs, ref result, ref terminate);
                    }
                    else
                    {
                        advanced = lhs;
                    }
                }
                else if (lhs.baseIndex == rhs.baseIndex)
                {
                    advanced = ApplyBoth(action, ref cursor, ref rhs, ref result, ref terminate);
                }
                else
                {
                    advanced = ApplyLeftGap(action, ref cursor, ref rhs, ref result, ref terminate);
                }

                if (terminate)
                {
                    return result;
                }

                if (advanced is not null)
                {
                    cursor = ref advanced.next;
                }
            }

            while (cursor is not null)
            {
                hashBvNode? advanced;

                if (getHashForIndex(cursor.baseIndex, otherSize) == bucket)
                {
                    advanced = ApplyRightGap(action, ref cursor, ref rhs, ref result, ref terminate);
                }
                else
                {
                    advanced = cursor;
                }

                if (terminate)
                {
                    return result;
                }

                if (advanced is not null)
                {
                    cursor = ref advanced.next;
                }
            }

            while (rhs is not null)
            {
                var advanced = ApplyLeftEmpty(action, ref cursor, ref rhs, ref result, ref terminate);

                if (terminate)
                {
                    return result;
                }

                if (advanced is not null)
                {
                    cursor = ref advanced.next;
                }
            }
        }

        assert(numNodes == getNodeCount());
        return result;
    }

    private bool MultiTraverseEqual(hashBv other, SetAction action)
    {
        var result = action is SetAction.Compare;
        var terminate = false;

        for (var bucket = 0; bucket < hashtable_size(); bucket++)
        {
            ref var cursor = ref nodeArr[bucket];
            var rhs = other.nodeArr[bucket];

            while ((cursor is not null) && (rhs is not null))
            {
                hashBvNode? advanced;

                if (cursor.baseIndex < rhs.baseIndex)
                {
                    advanced = ApplyRightGap(action, ref cursor, ref rhs, ref result, ref terminate);
                }
                else if (cursor.baseIndex == rhs.baseIndex)
                {
                    advanced = ApplyBoth(action, ref cursor, ref rhs, ref result, ref terminate);
                }
                else
                {
                    advanced = ApplyLeftGap(action, ref cursor, ref rhs, ref result, ref terminate);
                }

                if (terminate)
                {
                    return result;
                }

                if (advanced is not null)
                {
                    cursor = ref advanced.next;
                }
            }

            while (cursor is not null)
            {
                var advanced = ApplyRightGap(action, ref cursor, ref rhs, ref result, ref terminate);

                if (terminate)
                {
                    return result;
                }

                if (advanced is not null)
                {
                    cursor = ref advanced.next;
                }
            }

            while (rhs is not null)
            {
                var advanced = ApplyLeftEmpty(action, ref cursor, ref rhs, ref result, ref terminate);

                if (terminate)
                {
                    return result;
                }

                if (advanced is not null)
                {
                    cursor = ref advanced.next;
                }
            }
        }

        assert(numNodes == getNodeCount());
        return result;
    }

    private hashBvNode? ApplyLeftGap(SetAction action, ref hashBvNode? lhs, ref hashBvNode? rhs,
        ref bool result, ref bool terminate)
    {
        if (action is SetAction.Compare)
        {
            result = false;
            terminate = true;
        }
        else if (action is SetAction.Or)
        {
            var source = rhs!;
            var node = new hashBvNode(source.baseIndex);
            node.OrWith(source);
            node.next = lhs;
            lhs = node;
            numNodes = unchecked((ushort)(numNodes + 1));
            result = true;
            rhs = source.next;
            return node;
        }
        else
        {
            rhs = rhs!.next;
        }

        return null;
    }

    private hashBvNode? ApplyRightGap(SetAction action, ref hashBvNode? lhs, ref hashBvNode? rhs,
        ref bool result, ref bool terminate)
    {
        if (action is SetAction.Compare)
        {
            result = false;
            terminate = true;
        }
        else if (action is SetAction.And)
        {
            lhs = lhs!.next;
            numNodes = unchecked((ushort)(numNodes - 1));
            result = true;
        }
        else
        {
            return lhs;
        }

        return null;
    }

    private hashBvNode? ApplyBoth(SetAction action, ref hashBvNode? lhs, ref hashBvNode? rhs,
        ref bool result, ref bool terminate)
    {
        var node = lhs!;
        var changed = false;

        switch (action)
        {
            case SetAction.And:
            {
                changed = node.AndWithChange(rhs!) != 0;
                break;
            }

            case SetAction.Or:
            {
                changed = node.OrWithChange(rhs!) != 0;
                break;
            }

            case SetAction.Subtract:
            {
                changed = node.SubtractWithChange(rhs!) != 0;
                break;
            }

            case SetAction.Compare:
            {
                if (!node.sameAs(rhs!))
                {
                    result = false;
                    terminate = true;
                }
                break;
            }
        }

        result |= changed;

        if (terminate)
        {
            return null;
        }

        rhs = rhs!.next;

        if (changed && (action is SetAction.And or SetAction.Subtract) && !node.anySet())
        {
            lhs = node.next;
            numNodes = unchecked((ushort)(numNodes - 1));
            return null;
        }

        return node;
    }

    private hashBvNode? ApplyLeftEmpty(SetAction action, ref hashBvNode? lhs, ref hashBvNode? rhs,
        ref bool result, ref bool terminate)
    {
        if (action is SetAction.Compare)
        {
            result = false;
            terminate = true;
            return null;
        }

        if (action is SetAction.Or)
        {
            var source = rhs!;
            var node = new hashBvNode(source.baseIndex);
            node.OrWith(source);
            node.next = lhs;
            lhs = node;
            numNodes = unchecked((ushort)(numNodes + 1));
            result = true;
            rhs = source.next;
            return node;
        }

        rhs = rhs!.next;
        return null;
    }
}
