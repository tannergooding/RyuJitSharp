// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, flowgraph.cpp.

using System.Collections.Generic;
using static RyuJitSharp.BasicBlockVisit;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class FlowGraphNaturalLoop
{
    public bool MayExecuteBlockMultipleTimesPerIteration(BasicBlock block)
    {
        assert(ContainsBlock(block));

        if (ContainsImproperHeader)
        {
            return true;
        }

        for (var child = Child; child is not null; child = child.Sibling)
        {
            if (child.ContainsBlock(block))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPostDominatedOnLoopIteration(BasicBlock block, BasicBlock postDominator)
    {
        assert(ContainsBlock(block) && ContainsBlock(postDominator));
        var traits = LoopBlockTraits();
        var visited = BitVecOps.MakeEmpty(traits);
        var stack = new Stack<BasicBlock>();
        stack.Push(block);
        BitVecOps.AddElemD(traits, visited, LoopBlockBitVecIndex(block));
        var compiler = DfsTree.GetCompiler();

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == postDominator)
            {
                continue;
            }

            var visitResult = current.VisitAllSuccs(compiler, successor => {
                if (successor == Header)
                {
                    return Abort;
                }
                if (!ContainsBlock(successor))
                {
                    return Continue;
                }

                var index = LoopBlockBitVecIndex(successor);
                if (BitVecOps.TryAddElemD(traits, visited, index))
                {
                    stack.Push(successor);
                }
                return Continue;
            });

            if (visitResult is Abort)
            {
                return false;
            }
        }

        return true;
    }
}
