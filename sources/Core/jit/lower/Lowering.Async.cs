// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerAsyncContinuation(GenTree continuation)
    {
        assert(continuation.Oper is GT_ASYNC_CONTINUATION);
        var next = continuation.Next;
        var previous = continuation;
        while (true)
        {
            previous = previous.Prev;
            noway_assert(previous is not null, "Ran out of nodes while looking for call before async continuation");
            if ((previous.Oper is GT_CALL) && previous.AsCall().IsAsync)
            {
                // Both resumption stubs and the async transform can leave nodes
                // between the call and the read of its continuation register.
                BlockRange().Remove(continuation);
                BlockRange().InsertAfter(previous, continuation);
                break;
            }
        }

        return next;
    }

    private void LowerReturnSuspend(GenTree node)
    {
        assert(node.Oper is GT_RETURN_SUSPEND);
        var range = BlockRange();
        while (range.LastNode != node)
        {
            var last = range.LastNode;
            assert(last is not null);
            range.Remove(last, markOperandsUnused: true);
        }

        var compiler = CompilerInstance;
        var block = compiler.compCurBB;
        assert(block is not null);
        if (block.Kind is not BBJ_RETURN)
        {
            var profileInconsistent = false;
            foreach (var successor in block.Succs)
            {
                var edge = compiler.fgRemoveAllRefPreds(successor, block);
                if (block.hasProfileWeight && successor.hasProfileWeight)
                {
                    successor.decreaseBBProfileWeight(edge.LikelyWeight);
                    profileInconsistent |= successor.NumSucc > 0;
                }
            }

            if (profileInconsistent)
            {
                JITDUMP($"Flow removal of {FMT_BB(block.bbNum)} needs to be propagated. Data {(compiler.fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                compiler.fgPgoConsistent = false;
            }
            block.SetKindAndTargetEdge(BBJ_RETURN, null);
            compiler.fgInvalidateDfsTree();
        }

        if (compiler.compMethodRequiresPInvokeFrame)
        {
            InsertPInvokeMethodEpilog(block, node);
        }
    }
}
