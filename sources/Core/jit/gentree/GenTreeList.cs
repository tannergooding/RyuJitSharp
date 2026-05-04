// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public readonly partial struct GenTreeList : IEnumerable<GenTree>
{
    private readonly GenTree? _trees;

    public GenTreeList(GenTree? begin)
    {
        _trees = begin;
    }

    public Enumerator GetEnumerator() => new Enumerator(_trees);

    IEnumerator<GenTree> IEnumerable<GenTree>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
