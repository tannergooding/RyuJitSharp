// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public readonly partial struct BasicBlockSimpleList : IEnumerable<BasicBlock>
{
    private readonly BasicBlock? _begin;

    public BasicBlockSimpleList(BasicBlock? begin)
    {
        _begin = begin;
    }

    public Enumerator GetEnumerator() => new Enumerator(_begin);

    IEnumerator<BasicBlock> IEnumerable<BasicBlock>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
