// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

#if TARGET_AMD64
using elemType = ulong;
using indexType = ulong;
#else
using elemType = uint;
using indexType = uint;
#endif

namespace RyuJitSharp;

public sealed class hashBvNode
{
    public hashBvNode? next;
    public indexType baseIndex;
    public ElementArray elements;

    [InlineArray(ELEMENTS_PER_NODE)]
    public struct ElementArray
    {
        private elemType _e0;
    }

    public hashBvNode(indexType baseIndex)
    {
        assert((baseIndex % BITS_PER_NODE) == 0);
        this.baseIndex = baseIndex;
    }

    public int numElements() => ELEMENTS_PER_NODE;

    public void setBit(indexType index)
    {
        assert(index >= baseIndex);
        assert(index - baseIndex < BITS_PER_NODE);
        index -= baseIndex;
        var elem = (int)(index / BITS_PER_ELEMENT);
        var pos = (int)(index % BITS_PER_ELEMENT);
        elements[elem] |= (elemType)1 << pos;
    }

    public void clrBit(indexType index)
    {
        assert(index >= baseIndex);
        assert(index - baseIndex < BITS_PER_NODE);
        index -= baseIndex;
        var elem = (int)(index / BITS_PER_ELEMENT);
        var pos = (int)(index % BITS_PER_ELEMENT);
        elements[elem] &= ~((elemType)1 << pos);
    }

    public bool getBit(indexType index)
    {
        assert(index >= baseIndex);
        assert(index - baseIndex < BITS_PER_NODE);
        index -= baseIndex;
        var elem = (int)(index / BITS_PER_ELEMENT);
        var pos = (int)(index % BITS_PER_ELEMENT);
        return (elements[elem] & ((elemType)1 << pos)) != 0;
    }

    public bool belongsIn(indexType index) => (index >= baseIndex) && (index < baseIndex + BITS_PER_NODE);

    public bool anySet()
    {
        for (var i = 0; i < numElements(); i++)
        {
            if (elements[i] != 0)
            {
                return true;
            }
        }

        return false;
    }

    public void copyFrom(hashBvNode other)
    {
        baseIndex = other.baseIndex;

        for (var i = 0; i < numElements(); i++)
        {
            elements[i] = other.elements[i];
        }
    }

    public elemType AndWithChange(hashBvNode other)
    {
        elemType result = 0;

        for (var i = 0; i < numElements(); i++)
        {
            var source = elements[i];
            var destination = source & other.elements[i];
            result |= source ^ destination;
            elements[i] = destination;
        }

        return result;
    }

    public elemType OrWithChange(hashBvNode other)
    {
        elemType result = 0;

        for (var i = 0; i < numElements(); i++)
        {
            var source = elements[i];
            var destination = source | other.elements[i];
            result |= source ^ destination;
            elements[i] = destination;
        }

        return result;
    }

    public elemType XorWithChange(hashBvNode other)
    {
        elemType result = 0;

        for (var i = 0; i < numElements(); i++)
        {
            var source = elements[i];
            var destination = source ^ other.elements[i];
            result |= source ^ destination;
            elements[i] = destination;
        }

        return result;
    }

    public elemType SubtractWithChange(hashBvNode other)
    {
        elemType result = 0;

        for (var i = 0; i < numElements(); i++)
        {
            var source = elements[i];
            var destination = source & ~other.elements[i];
            result |= source ^ destination;
            elements[i] = destination;
        }

        return result;
    }

    public bool Intersects(hashBvNode other)
    {
        for (var i = 0; i < numElements(); i++)
        {
            if ((elements[i] & other.elements[i]) != 0)
            {
                return true;
            }
        }

        return false;
    }

    public void AndWith(hashBvNode other)
    {
        for (var i = 0; i < numElements(); i++)
        {
            elements[i] &= other.elements[i];
        }
    }

    public void OrWith(hashBvNode other)
    {
        for (var i = 0; i < numElements(); i++)
        {
            elements[i] |= other.elements[i];
        }
    }

    public void XorWith(hashBvNode other)
    {
        for (var i = 0; i < numElements(); i++)
        {
            elements[i] ^= other.elements[i];
        }
    }

    public void Subtract(hashBvNode other)
    {
        for (var i = 0; i < numElements(); i++)
        {
            elements[i] &= ~other.elements[i];
        }
    }

    public bool sameAs(hashBvNode other)
    {
        if (baseIndex != other.baseIndex)
        {
            return false;
        }

        for (var i = 0; i < numElements(); i++)
        {
            if (elements[i] != other.elements[i])
            {
                return false;
            }
        }

        return true;
    }
}
