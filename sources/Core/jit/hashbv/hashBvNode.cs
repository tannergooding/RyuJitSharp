// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using elemType = ulong;
using indexType = ulong;

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
        baseIndex = Globals.NormalizeIndex(baseIndex);
        assert((baseIndex % BITS_PER_NODE) == 0);
        this.baseIndex = baseIndex;
    }

    public int numElements() => ELEMENTS_PER_NODE;

    public void setLowest(indexType numToSet)
    {
        numToSet = Globals.NormalizeIndex(numToSet);
        assert(numToSet <= BITS_PER_NODE);

        var elementIndex = 0;
        var allOnes = Globals.NormalizeElement(ulong.MaxValue);
        while (numToSet > BITS_PER_ELEMENT)
        {
            elements[elementIndex] = allOnes;
            numToSet -= BITS_PER_ELEMENT;
            elementIndex++;
        }

        if (numToSet != 0)
        {
            var numToShift = BITS_PER_ELEMENT - (int)numToSet;
            elements[elementIndex] = allOnes >> numToShift;
        }
    }

    public int countBits()
    {
        var result = 0;

        for (var i = 0; i < numElements(); i++)
        {
            var bits = Globals.NormalizeElement(elements[i]);
            result = unchecked(result + BitOperations.PopCount(bits));
            result = unchecked(result + (int)bits);
        }

        return result;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public void dump()
    {
        jitprintf($"base: {baseIndex} {{ ");
        foreachBit(index => jitprintf($"{unchecked((int)index)} "));
        jitprintf("}\n");
    }

    public void foreachBit(Action<indexType> action)
    {
        indexType baseIndex;

        for (var i = 0; i < numElements(); i++)
        {
            baseIndex = Globals.AddIndex(this.baseIndex, (indexType)(i * BITS_PER_ELEMENT));
            var bits = Globals.NormalizeElement(elements[i]);

            while (bits != 0)
            {
                if ((bits & 1) != 0)
                {
                    action(baseIndex);
                }

                bits >>= 1;
                baseIndex = Globals.AddIndex(baseIndex, 1);
            }
        }
    }

    public void setBit(indexType index)
    {
        index = Globals.NormalizeIndex(index);
        assert(index >= baseIndex);
        assert(index - baseIndex < BITS_PER_NODE);
        index -= baseIndex;
        var elem = (int)(index / BITS_PER_ELEMENT);
        var pos = (int)(index % BITS_PER_ELEMENT);
        elements[elem] = Globals.NormalizeElement(elements[elem] | ((elemType)1 << pos));
    }

    public void clrBit(indexType index)
    {
        index = Globals.NormalizeIndex(index);
        assert(index >= baseIndex);
        assert(index - baseIndex < BITS_PER_NODE);
        index -= baseIndex;
        var elem = (int)(index / BITS_PER_ELEMENT);
        var pos = (int)(index % BITS_PER_ELEMENT);
        elements[elem] = Globals.NormalizeElement(elements[elem] & ~((elemType)1 << pos));
    }

    public bool getBit(indexType index)
    {
        index = Globals.NormalizeIndex(index);
        assert(index >= baseIndex);
        assert(index - baseIndex < BITS_PER_NODE);
        index -= baseIndex;
        var elem = (int)(index / BITS_PER_ELEMENT);
        var pos = (int)(index % BITS_PER_ELEMENT);
        return (Globals.NormalizeElement(elements[elem]) & ((elemType)1 << pos)) != 0;
    }

    public bool belongsIn(indexType index)
    {
        index = Globals.NormalizeIndex(index);
        return (index >= baseIndex) && (index < Globals.AddIndex(baseIndex, BITS_PER_NODE));
    }

    public bool anySet()
    {
        for (var i = 0; i < numElements(); i++)
        {
            if (Globals.NormalizeElement(elements[i]) != 0)
            {
                return true;
            }
        }

        return false;
    }

    public void copyFrom(hashBvNode other)
    {
        baseIndex = Globals.NormalizeIndex(other.baseIndex);

        for (var i = 0; i < numElements(); i++)
        {
            elements[i] = Globals.NormalizeElement(other.elements[i]);
        }
    }

    public elemType AndWithChange(hashBvNode other)
    {
        elemType result = 0;

        for (var i = 0; i < numElements(); i++)
        {
            var source = Globals.NormalizeElement(elements[i]);
            var destination = Globals.NormalizeElement(source & Globals.NormalizeElement(other.elements[i]));
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
            var source = Globals.NormalizeElement(elements[i]);
            var destination = Globals.NormalizeElement(source | Globals.NormalizeElement(other.elements[i]));
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
            var source = Globals.NormalizeElement(elements[i]);
            var destination = Globals.NormalizeElement(source ^ Globals.NormalizeElement(other.elements[i]));
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
            var source = Globals.NormalizeElement(elements[i]);
            var destination = Globals.NormalizeElement(source & ~Globals.NormalizeElement(other.elements[i]));
            result |= source ^ destination;
            elements[i] = destination;
        }

        return result;
    }

    public bool Intersects(hashBvNode other)
    {
        for (var i = 0; i < numElements(); i++)
        {
            if ((Globals.NormalizeElement(elements[i]) & Globals.NormalizeElement(other.elements[i])) != 0)
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
            elements[i] = Globals.NormalizeElement(
                Globals.NormalizeElement(elements[i]) & Globals.NormalizeElement(other.elements[i]));
        }
    }

    public void OrWith(hashBvNode other)
    {
        for (var i = 0; i < numElements(); i++)
        {
            elements[i] = Globals.NormalizeElement(
                Globals.NormalizeElement(elements[i]) | Globals.NormalizeElement(other.elements[i]));
        }
    }

    public void XorWith(hashBvNode other)
    {
        for (var i = 0; i < numElements(); i++)
        {
            elements[i] = Globals.NormalizeElement(
                Globals.NormalizeElement(elements[i]) ^ Globals.NormalizeElement(other.elements[i]));
        }
    }

    public void Subtract(hashBvNode other)
    {
        for (var i = 0; i < numElements(); i++)
        {
            elements[i] = Globals.NormalizeElement(
                Globals.NormalizeElement(elements[i]) & ~Globals.NormalizeElement(other.elements[i]));
        }
    }
}
