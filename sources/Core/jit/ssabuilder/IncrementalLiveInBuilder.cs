// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed class IncrementalLiveInBuilder
{
    private readonly Compiler _compiler;
    private readonly Stack<BasicBlock> _queue = new();

    public IncrementalLiveInBuilder(Compiler compiler)
    {
        _compiler = compiler;
    }

    public void MarkLiveInBackwards(int lclNum, UseDefLocation use, UseDefLocation reachingDef)
    {
        if (use.Block == reachingDef.Block)
        {
            return;
        }

        if (!_compiler.AddInsertedSsaLiveIn(use.Block, lclNum))
        {
            return;
        }

        _queue.Clear();
        _queue.Push(use.Block);

        while (_queue.Count != 0)
        {
            var block = _queue.Pop();
            for (var edge = _compiler.BlockPredsWithEH(block); edge is not null; edge = edge.NextPredEdge)
            {
                var pred = edge.SourceBlock;
                if (pred == reachingDef.Block)
                {
                    continue;
                }

                if (_compiler.AddInsertedSsaLiveIn(pred, lclNum))
                {
                    _queue.Push(pred);
                }
            }
        }
    }
}
