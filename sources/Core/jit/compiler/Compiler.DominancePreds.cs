// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    internal BlockToFlowEdgeMap? _dominancePreds;

    public BlockToFlowEdgeMap GetDominancePreds() => _dominancePreds ??= [];

    public FlowEdge? BlockDominancePreds(BasicBlock block)
    {
        if (!bbIsHandlerBeg(block))
        {
            return block.bbPreds;
        }

        var dominancePreds = GetDominancePreds();

        if (dominancePreds.TryGetValue(block, out var result))
        {
            return result;
        }

        ref var handler = ref ehGetBlockHndDsc(block);
        result = BlockPredsWithEH(block);

        foreach (var predecessor in handler.ebdTryBeg.PredBlocks)
        {
            result = new FlowEdge(predecessor, block, result);
        }

        dominancePreds[block] = result;

        return result;
    }
}
