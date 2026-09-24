// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Expand imported Tier0 patchpoints, sharing one loop counter per stack frame.</summary>
public sealed class PatchpointTransformer
{
    private const int HIGH_PROBABILITY = 99;

    private readonly Compiler _compiler;
    private int _ppCounterLclNum = BAD_VAR_NUM;

    public PatchpointTransformer(Compiler compiler)
    {
        _compiler = compiler;
    }

    public int Run()
    {
        var count = 0;
        assert(_compiler.fgFirstBB is not null);

        foreach (var block in new BasicBlockSimpleList(_compiler.fgFirstBB.Next))
        {
            if (block.HasFlag(BBF_OSR_PATCHPOINT))
            {
                assert(!block.hasHndIndex);
                block.RemoveFlags(BBF_OSR_PATCHPOINT);
                JITDUMP($"Patchpoint: regular patchpoint in {FMT_BB(block.bbNum)}\n");
                TransformBlock(block);
                count++;
            }
            else if (block.HasFlag(BBF_PARTIAL_COMPILATION_PATCHPOINT))
            {
                assert(!block.hasHndIndex);
                assert(!block.HasFlag(BBF_HAS_HISTOGRAM_PROFILE));
                block.RemoveFlags(BBF_PARTIAL_COMPILATION_PATCHPOINT);
                JITDUMP($"Patchpoint: partial compilation patchpoint in {FMT_BB(block.bbNum)}\n");
                TransformPartialCompilation(block);
                count++;
            }
        }

        return count;
    }

    private BasicBlock CreateAndInsertBasicBlock(BBKinds jumpKind, BasicBlock insertAfter)
    {
        var block = _compiler.fgNewBBafter(jumpKind, insertAfter, true);
        block.SetFlags(BBF_IMPORTED);
        return block;
    }

    private void TransformBlock(BasicBlock block)
    {
        if (_ppCounterLclNum == BAD_VAR_NUM)
        {
            _ppCounterLclNum = _compiler.lvaGrabTemp(true, "patchpoint counter");
            _compiler.lvaTable[_ppCounterLclNum].Type = TYP_INT;
            assert(_compiler.fgFirstBB is not null);
            TransformEntry(_compiler.fgFirstBB);
        }

        var ilOffset = block.bbCodeOffs;
        assert(ilOffset != BAD_IL_OFFSET);
        var remainderBlock = _compiler.fgSplitBlockAtBeginning(block);
        var helperBlock = CreateAndInsertBasicBlock(BBJ_ALWAYS, block);

        block.SetFlags(BBF_INTERNAL);
        helperBlock.SetFlags(BBF_BACKWARD_JUMP);
        assert(block.Target == remainderBlock);
        var falseEdge = _compiler.fgAddRefPred(helperBlock, block);
        var trueEdge = block.TargetEdge;
        trueEdge.Likelihood = HIGH_PROBABILITY / 100.0;
        falseEdge.Likelihood = (100 - HIGH_PROBABILITY) / 100.0;
        block.SetCond(trueEdge, falseEdge);
        helperBlock.TargetEdge = _compiler.fgAddRefPred(remainderBlock, helperBlock);
        remainderBlock.inheritWeight(block);
        helperBlock.inheritWeightPercentage(block, 100 - HIGH_PROBABILITY);

        var counterBefore = _compiler.gtNewLclvNode(TYP_INT, _ppCounterLclNum);
        var one = _compiler.gtNewIconNode(TYP_INT, 1);
        var decrement = _compiler.gtNewBinaryNode(GT_SUB, TYP_INT, counterBefore, one);
        var update = _compiler.gtNewStoreLclVarNode(_ppCounterLclNum, decrement);
        _compiler.fgInsertStmtAtEnd(block, _compiler.gtNewStmt(update));

        var counterUpdated = _compiler.gtNewLclvNode(TYP_INT, _ppCounterLclNum);
        var zero = _compiler.gtNewIconNode(TYP_INT, 0);
        var compare = _compiler.gtNewBinaryNode(GT_GT, TYP_INT, counterUpdated, zero);
        var jump = _compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, compare);
        _compiler.fgInsertStmtAtEnd(block, _compiler.gtNewStmt(jump));

        // The helper returns either an OSR entry address or a skip address that
        // jumps past GT_PATCHPOINT's unconditional jump and resumes Tier0.
        var ilOffsetNode = _compiler.gtNewIconNode(TYP_INT, ilOffset);
        var counterAddr = _compiler.gtNewLclVarAddrNode(TYP_BYREF, _ppCounterLclNum);
        var patchpoint = _compiler.gtNewBinaryNode(GT_PATCHPOINT, TYP_VOID, counterAddr, ilOffsetNode);
        patchpoint.Flags |= GTF_CALL;
        _compiler.fgInsertStmtAtEnd(helperBlock, _compiler.gtNewStmt(patchpoint));
    }

    private void TransformEntry(BasicBlock block)
    {
        var initialCounterValue = JitConfig.TC_OnStackReplacement_InitialCounter;

        if (initialCounterValue < 0)
        {
            initialCounterValue = 0;
        }

        var initialCounter = _compiler.gtNewIconNode(TYP_INT, initialCounterValue);
        var store = _compiler.gtNewStoreLclVarNode(_ppCounterLclNum, initialCounter);
        _compiler.fgInsertStmtAtBeg(block, _compiler.gtNewStmt(store));
    }

    private void TransformPartialCompilation(BasicBlock block)
    {
        var ilOffset = block.bbCodeOffs;
        assert(ilOffset != BAD_IL_OFFSET);

        foreach (var stmt in block.Statements)
        {
            _compiler.fgRemoveStmt(block, stmt);
        }

        block.SetKindAndTargetEdge(BBJ_THROW, null);
        var ilOffsetNode = _compiler.gtNewIconNode(TYP_INT, ilOffset);
        var patchpoint = _compiler.gtNewUnaryNode(GT_PATCHPOINT_FORCED, TYP_VOID, ilOffsetNode);
        patchpoint.Flags |= GTF_CALL;
        _compiler.fgInsertStmtAtEnd(block, _compiler.gtNewStmt(patchpoint));
    }
}
