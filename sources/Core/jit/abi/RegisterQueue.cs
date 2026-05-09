// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public ref struct RegisterQueue
{
    private readonly ReadOnlySpan<regNumber> m_regs;
    private int m_index;

    public RegisterQueue(ReadOnlySpan<regNumber> regs)
    {
        m_regs = regs;
    }

    public readonly int Count => m_regs.Length - m_index;

    public void Clear()
    {
        m_index = m_regs.Length;
    }

    public regNumber Dequeue()
    {
        assert(Count > 0);
        return m_regs[m_index++];
    }

    public readonly regNumber Peek()
    {
        assert(Count > 0);
        return m_regs[m_index];
    }
}
