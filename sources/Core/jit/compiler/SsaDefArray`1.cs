// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public struct SsaDefArray<T>
{
    /// <summary>Get the minimum valid SSA number.</summary>
    private const int MinSsaNum = SsaConfig.FIRST_SSA_NUM;

    private T[] m_array;
    private int m_count;

    /// <summary>Get the number of SSA definitions in the array.</summary>
    public readonly int Count => m_count;

    public int AllocSsaNum()
    {
        if (m_count == m_array.Length)
        {
            GrowArray();
        }

        var ssaNum = MinSsaNum + m_count;
        m_array[m_count++] = default!;

        // Ensure that the first SSA number we allocate is SsaConfig::FIRST_SSA_NUM
        assert((ssaNum == SsaConfig.FIRST_SSA_NUM) || (m_count > 1));

        return ssaNum;
    }

    /// <summary>Get a reference to the SSA definition associated with the specified SSA number.</summary>
    /// <param name="ssaNum"></param>
    /// <returns></returns>
    public readonly ref T GetSsaDef(int ssaNum)
    {
        assert(ssaNum != SsaConfig.RESERVED_SSA_NUM);
        return ref GetSsaDefByIndex(ssaNum - MinSsaNum);
    }

    // Get a pointer to the SSA definition at the specified index.
    public readonly ref T GetSsaDefByIndex(int index)
    {
        assert((index >= 0) && (index < m_count));
        return ref m_array[index];
    }

    /// <summary>Get an SSA number associated with the specified SSA def (that must be in this array).</summary>
    /// <param name="ssaDef"></param>
    /// <returns></returns>
    public readonly int GetSsaNum(in T ssaDef)
    {
        assert(Unsafe.IsAddressGreaterThanOrEqualTo(in ssaDef, in m_array[0]) && Unsafe.IsAddressLessThan(in ssaDef, ref Unsafe.Add(ref m_array[0], m_count)));
        var ssaNum = MinSsaNum + (int)(Unsafe.ByteOffset(in m_array[0], in ssaDef) / Unsafe.SizeOf<T>());

        assert(Unsafe.AreSame(in ssaDef, in m_array[ssaNum - MinSsaNum]));
        return ssaNum;
    }

    /// <summary>Check if the specified SSA number is valid.</summary>
    /// <param name="ssaNum"></param>
    /// <returns></returns>
    public readonly bool IsValidSsaNum(int ssaNum)
        => (MinSsaNum <= ssaNum) && (ssaNum < (MinSsaNum + m_count));

    public void Reset()
    {
        m_count = 0;
    }

    private void GrowArray()
    {
        var oldArray = m_array;

        var oldSize = oldArray.Length;
        var newSize = int.Max(2, oldSize * 2);

        var newArray = new T[newSize];
        oldArray.AsSpan().CopyTo(newArray);

        m_array = newArray;
    }
}
