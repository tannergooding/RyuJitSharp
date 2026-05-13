// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public ref struct RegisterQueue
{
    private readonly ReadOnlySpan<regNumber> _regs;
    private int _index;

    public RegisterQueue(ReadOnlySpan<regNumber> regs)
    {
        _regs = regs;
    }

    public readonly int Count => _regs.Length - _index;

    public void Clear()
    {
        _index = _regs.Length;
    }

    public regNumber Dequeue()
    {
        assert(Count > 0);
        return _regs[_index++];
    }

    public readonly regNumber Peek()
    {
        assert(Count > 0);
        return _regs[_index];
    }
}
