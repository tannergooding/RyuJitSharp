// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class FixedBitVect
{
    private uint bitVectSize;
    private readonly uint[] bitVect;

    private FixedBitVect(uint numberOfChunks)
    {
        bitVect = new uint[numberOfChunks];
    }

    private static uint bitChunkSize()
    {
        return sizeof(uint) * 8;
    }

    private static uint bitNumToBit(uint bitNum)
    {
        assert(bitNum < bitChunkSize());
        assert(bitChunkSize() <= sizeof(int) * 8);

        return 1U << (int)bitNum;
    }

    public static FixedBitVect bitVectInit(uint size, Compiler comp)
    {
        assert(size != 0);

        var numberOfChunks = unchecked(size - 1) / bitChunkSize() + 1;
        var bitVectMemSize = numberOfChunks * (bitChunkSize() / 8);

        assert(unchecked(bitVectMemSize * bitChunkSize()) >= size,
            "bitVectMemSize * bitChunkSize() >= size");

        // Managed compiler bitsets use GC-backed, zero-initialized storage instead of the native arena.
        var bv = new FixedBitVect(numberOfChunks) {
            bitVectSize = size,
        };

        return bv;
    }

    public uint bitVectGetSize()
    {
        return bitVectSize;
    }

    public void bitVectSet(uint bitNum)
    {
        assert(bitNum <= bitVectSize);

        var index = bitNum / bitChunkSize();
        bitNum -= index * bitChunkSize();

        bitVect[index] |= bitNumToBit(bitNum);
    }

    public void bitVectClear(uint bitNum)
    {
        assert(bitNum <= bitVectSize);

        var index = bitNum / bitChunkSize();
        bitNum -= index * bitChunkSize();

        bitVect[index] &= ~bitNumToBit(bitNum);
    }

    public bool bitVectTest(uint bitNum)
    {
        assert(bitNum <= bitVectSize);

        var index = bitNum / bitChunkSize();
        bitNum -= index * bitChunkSize();

        return (bitVect[index] & bitNumToBit(bitNum)) != 0;
    }

    public void bitVectOr(FixedBitVect bv)
    {
        var bitChunkCnt = unchecked(bitVectSize - 1) / bitChunkSize() + 1;

        assert(bitVectSize == bv.bitVectSize, "bitVectSize == bv->bitVectSize");

        for (uint i = 0; i < bitChunkCnt; i++)
        {
            bitVect[i] |= bv.bitVect[i];
        }
    }

    public void bitVectAnd(FixedBitVect bv)
    {
        var bitChunkCnt = unchecked(bitVectSize - 1) / bitChunkSize() + 1;

        assert(bitVectSize == bv.bitVectSize);

        for (uint i = 0; i < bitChunkCnt; i++)
        {
            bitVect[i] &= bv.bitVect[i];
        }
    }

    public uint bitVectGetFirst()
    {
        return bitVectGetNext(uint.MaxValue);
    }

    public uint bitVectGetNext(uint bitNumPrev)
    {
        var bitNum = uint.MaxValue;
        uint index;
        uint bitMask;
        var bitChunkCnt = unchecked(bitVectSize - 1) / bitChunkSize() + 1;
        uint i;

        if (bitNumPrev == uint.MaxValue)
        {
            index = 0;
            bitMask = uint.MaxValue;
        }
        else
        {
            index = bitNumPrev / bitChunkSize();
            bitNumPrev -= index * bitChunkSize();
            var bit = bitNumToBit(bitNumPrev);
            bitMask = ~(bit | (bit - 1));
        }

        for (i = index; i < bitChunkCnt; i++)
        {
            var bitChunk = bitVect[i] & bitMask;

            if (bitChunk != 0)
            {
                bitNum = BitScanForward(bitChunk);
                break;
            }

            bitMask = 0xFFFFFFFF;
        }

        if (bitNum == uint.MaxValue)
        {
            return uint.MaxValue;
        }

        bitNum += i * bitChunkSize();
        assert(bitNum <= bitVectSize);

        return bitNum;
    }

    public uint bitVectGetNextAndClear()
    {
        var bitNum = uint.MaxValue;
        var bitChunkCnt = unchecked(bitVectSize - 1) / bitChunkSize() + 1;
        uint i;

        for (i = 0; i < bitChunkCnt; i++)
        {
            if (bitVect[i] != 0)
            {
                bitNum = BitScanForward(bitVect[i]);
                break;
            }
        }

        if (bitNum == uint.MaxValue)
        {
            return uint.MaxValue;
        }

        // Clear the chunk-relative bit before converting it to the absolute position.
        bitVect[i] &= ~bitNumToBit(bitNum);
        bitNum += i * bitChunkSize();
        assert(bitNum <= bitVectSize);

        return bitNum;
    }
}
