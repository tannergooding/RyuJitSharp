// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

internal sealed class BitStreamWriter : IDisposable
{
    private const uint BitsPerSlot = 64;
    private const int MemoryBlockSize = 128;
    private const int SlotsPerBlock = MemoryBlockSize / sizeof(ulong);

    private readonly List<ulong[]> m_MemoryBlocks = [];
    private nuint m_BitCount;
    private uint m_FreeBitsInCurrentSlot;
    private int m_CurrentSlot = -1;
    private bool m_Disposed;

    public BitStreamWriter()
    {
        if (IntPtr.Size != sizeof(ulong) || !BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException("The GC bit stream requires a little-endian 64-bit target.");
        }
    }

    public nuint GetBitCount() => m_BitCount;

    public nuint GetByteCount() => unchecked((m_BitCount + 7) / 8);

    public void Write(nuint data, uint count)
    {
        ObjectDisposedException.ThrowIf(m_Disposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, BitsPerSlot);

        if (count == 0)
        {
            return;
        }

        m_BitCount = unchecked(m_BitCount + count);

        if (count > m_FreeBitsInCurrentSlot)
        {
            if (m_FreeBitsInCurrentSlot > 0)
            {
                WriteInCurrentSlot(data, m_FreeBitsInCurrentSlot);
                count -= m_FreeBitsInCurrentSlot;
                data >>= (int)m_FreeBitsInCurrentSlot;
            }

            m_CurrentSlot++;

            if (m_CurrentSlot >= SlotsPerBlock || m_MemoryBlocks.Count == 0)
            {
                AllocMemoryBlock();
            }

            InitCurrentSlot();
            WriteInCurrentSlot(data, count);
            m_FreeBitsInCurrentSlot -= count;
        }
        else
        {
            WriteInCurrentSlot(data, count);
            m_FreeBitsInCurrentSlot -= count;
        }
    }

    public void CopyTo(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(m_Disposed, this);

        var byteCount = GetByteCount();

        if (byteCount > (nuint)buffer.Length)
        {
            throw new ArgumentException("The destination is shorter than the encoded bit stream.", nameof(buffer));
        }

        var remaining = (int)byteCount;

        foreach (var block in m_MemoryBlocks)
        {
            var length = Math.Min(remaining, MemoryBlockSize);
            MemoryMarshal.AsBytes(block.AsSpan()).Slice(0, length).CopyTo(buffer);
            buffer = buffer.Slice(length);
            remaining -= length;

            if (remaining == 0)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        m_MemoryBlocks.Clear();
        m_Disposed = true;
    }

    public static int SizeofVarLengthUnsigned(nuint n, uint @base)
    {
        ValidateBase(@base);
        assert(unchecked((int)(uint)n) >= 0);

        var numEncodings = (nuint)1 << (int)@base;
        int bitsUsed;

        for (bitsUsed = (int)@base + 1; ; bitsUsed += (int)@base + 1)
        {
            if (n < numEncodings)
            {
                return bitsUsed;
            }

            n >>= (int)@base;
        }
    }

    public int EncodeVarLengthUnsigned(nuint n, uint @base)
    {
        ValidateBase(@base);
        assert(unchecked((int)(uint)n) >= 0);

        var numEncodings = (nuint)1 << (int)@base;
        int bitsUsed;

        for (bitsUsed = (int)@base + 1; ; bitsUsed += (int)@base + 1)
        {
            if (n < numEncodings)
            {
                Write(n, @base + 1);
                return bitsUsed;
            }

            var currentChunk = n & (numEncodings - 1);
            Write(currentChunk | numEncodings, @base + 1);
            n >>= (int)@base;
        }
    }

    public int EncodeVarLengthSigned(nint n, uint @base)
    {
        ValidateBase(@base);

        var numEncodings = (nuint)1 << (int)@base;

        for (var bitsUsed = (int)@base + 1; ; bitsUsed += (int)@base + 1)
        {
            var currentChunk = unchecked((nuint)n) & (numEncodings - 1);
            var topmostBit = currentChunk & (numEncodings >> 1);
            n >>= (int)@base;

            if ((topmostBit != 0 && n == -1) || (topmostBit == 0 && n == 0))
            {
                Write(currentChunk, @base + 1);
                return bitsUsed;
            }

            Write(currentChunk | numEncodings, @base + 1);
        }
    }

    private static void ValidateBase(uint @base)
    {
        if (@base == 0 || @base >= BitsPerSlot)
        {
            throw new ArgumentOutOfRangeException(nameof(@base));
        }
    }

    private void WriteInCurrentSlot(nuint data, uint count)
    {
        var mask = ulong.MaxValue >> (int)(BitsPerSlot - count);
        var offset = (int)(BitsPerSlot - m_FreeBitsInCurrentSlot);
        m_MemoryBlocks[^1][m_CurrentSlot] |= ((ulong)data & mask) << offset;
    }

    private void AllocMemoryBlock()
    {
        m_MemoryBlocks.Add(new ulong[SlotsPerBlock]);
        m_CurrentSlot = 0;
    }

    private void InitCurrentSlot()
    {
        m_FreeBitsInCurrentSlot = BitsPerSlot;
        m_MemoryBlocks[^1][m_CurrentSlot] = 0;
    }
}
