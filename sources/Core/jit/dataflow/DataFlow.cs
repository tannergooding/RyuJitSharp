// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class DataFlow
{
    private readonly Compiler _compiler;

    public DataFlow(Compiler compiler)
    {
        _compiler = compiler;
    }

    public void ForwardAnalysis<TCallback>(ref TCallback callback)
        where TCallback : ICallback
    {
#if DEBUG
        assert(_compiler.fgTrysContiguous());
#endif
        _compiler._dfsTree ??= _compiler.fgComputeDfs();

        bool changed;
        do
        {
            changed = false;

            for (var index = _compiler._dfsTree.PostOrderCount; index > 0; index--)
            {
                var block = _compiler._dfsTree.GetPostOrder(index - 1);
                callback.StartMerge(block);

                if (_compiler.bbIsHandlerBeg(block))
                {
                    ref var descriptor = ref _compiler.ehGetBlockHndDsc(block);
                    var firstTryBlock = descriptor.ebdTryBeg;
                    var lastTryBlock = descriptor.ebdTryLast;
                    assert(firstTryBlock is not null);
                    assert(lastTryBlock is not null);
                    callback.MergeHandler(block, firstTryBlock, lastTryBlock);
                }
                else
                {
                    foreach (var predecessor in block.PredEdges)
                    {
                        callback.Merge(block, predecessor.SourceBlock, predecessor.DupCount);
                    }
                }

                changed |= callback.EndMerge(block);
            }
        }
        while (changed && _compiler._dfsTree.HasCycle);
    }

    public interface ICallback
    {
        void StartMerge(BasicBlock block);

        void Merge(BasicBlock block, BasicBlock predecessor, int duplicateCount);

        void MergeHandler(BasicBlock block, BasicBlock firstTryBlock, BasicBlock lastTryBlock);

        bool EndMerge(BasicBlock block);
    }
}
