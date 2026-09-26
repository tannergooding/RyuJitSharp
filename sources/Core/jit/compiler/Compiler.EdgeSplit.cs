// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public BasicBlock fgSplitEdge(BasicBlock curr, BasicBlock succ)
    {
        assert(curr.Kind is BBJ_COND or BBJ_SWITCH or BBJ_ALWAYS or BBJ_CALLFINALLYRET);
        assert(fgPredsComputed);
        assert(fgGetPredForBlock(succ, curr) is not null);

        BasicBlock newBlock;
        if (curr.Next == succ)
        {
            newBlock = fgNewBBafter(BBJ_ALWAYS, curr, extendRegion: true);
        }
        else
        {
            newBlock = fgNewBBinRegion(BBJ_ALWAYS, curr, runRarely: curr.isRunRarely);
        }

        newBlock.CopyFlags(curr, succ.FlagsRaw & BBF_BACKWARD_JUMP);
        // Async resumption stubs may branch into EH regions; the split edge must retain that permission.
        newBlock.CopyFlags(curr, BBF_ASYNC_RESUMPTION);
        JITDUMP($"Splitting edge from {FMT_BB(curr.bbNum)} to {FMT_BB(succ.bbNum)}; adding {FMT_BB(newBlock.bbNum)}\n");

        fgReplaceJumpTarget(curr, succ, newBlock);
        var newSuccEdge = fgAddRefPred(succ, newBlock);
        newBlock.TargetEdge = newSuccEdge;

        var currNewEdge = fgGetPredForBlock(newBlock, curr);
        assert(currNewEdge is not null);
        newBlock.bbWeight = currNewEdge.LikelyWeight;
        newBlock.CopyFlags(curr, BBF_PROF_WEIGHT);
        if (newBlock.bbWeight == BB_ZERO_WEIGHT)
        {
            newBlock.bbSetRunRarely();
        }

        if (fgLocalVarLivenessDone)
        {
            VarSetOps.Assign(this, ref newBlock.bbLiveIn, succ.bbLiveIn);
            VarSetOps.Assign(this, ref newBlock.bbLiveOut, succ.bbLiveIn);
        }

        return newBlock;
    }
}
