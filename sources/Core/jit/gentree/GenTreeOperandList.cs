// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public readonly partial struct GenTreeOperandsList : IEnumerable<GenTree>
{
    private readonly GenTree _tree;

    public GenTreeOperandsList(GenTree tree)
    {
        _tree = tree;
    }

    public Enumerator GetEnumerator() => new Enumerator(_tree);

    IEnumerator<GenTree> IEnumerable<GenTree>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
