// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections;
using System.Collections.Generic;

namespace RyuJitSharp;

public readonly partial struct BasicBlockRangeList : IEnumerable<BasicBlock>
{
    private readonly BasicBlock _begin;
    private readonly BasicBlock _end;

    public BasicBlockRangeList(BasicBlock begin, BasicBlock end)
    {
        _begin = begin;
        _end = end;
    }

    public Enumerator GetEnumerator() => new Enumerator(_begin, _end);

    public bool ComplexityExceeds(Compiler compiler, uint limit, Func<GenTree, uint> getTreeComplexity)
    {
        assert(compiler is not null);
        var localCount = 0u;
        uint GetComplexity(GenTree tree)
        {
            var complexity = getTreeComplexity(tree);
            localCount = unchecked(localCount + complexity);
            return complexity;
        }

        foreach (var block in this)
        {
            var slack = unchecked(limit - localCount);
            if (block.ComplexityExceeds(compiler, slack, GetComplexity))
            {
                return true;
            }
        }

        return false;
    }

    IEnumerator<BasicBlock> IEnumerable<BasicBlock>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
