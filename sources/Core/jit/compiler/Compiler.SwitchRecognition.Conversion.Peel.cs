// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private void fgPeelSwitch(BasicBlock block)
    {
        assert((block.Kind is BBJ_SWITCH) && block.SwitchTargets.HasDominantCase &&
            !block.isRunRarely && !block.IsLIR && block.hasProfileWeight);

        var dominantCase = block.SwitchTargets.DominantCase;
        JITDUMP($"{FMT_BB(block.bbNum)} has switch with dominant case {dominantCase}, considering peeling\n");
        var switchTargets = block.SwitchTargets;
        assert(dominantCase < switchTargets.Cases.Length - 1);
        var dominantEdge = switchTargets.Cases[dominantCase];
        var dominantTarget = dominantEdge.DestinationBlock;
        var switchStatement = block.LastStmt;
        assert(switchStatement is not null);
        var switchTree = switchStatement.RootNode;
        assert(switchTree.Oper is GT_SWITCH);
        var switchValue = switchTree.AsUnOp().Op1;

        var newBlock = block.FirstStmt == switchStatement
            ? fgSplitBlockAtBeginning(block)
            : fgSplitBlockAfterStatement(block, switchStatement.PrevStmt);

        var comparison = gtNewBinaryNode(GT_EQ, TYP_INT, switchValue,
            gtNewIconNode(TYP_INT, dominantCase));
        var jump = gtNewUnaryNode(GT_JTRUE, TYP_VOID, comparison);

        // Duplicate the value before sequencing the compare: sequencing can swap its operands.
        switchTree.AsUnOp().Op1 = fgMakeMultiUse(ref comparison.Op1Ref);
        switchTree.Flags = switchTree.AsUnOp().Op1.Flags & GTF_ALL_EFFECT;
        comparison.Flags |= comparison.Op1.Flags & GTF_ALL_EFFECT;
        jump.Flags |= comparison.Flags & GTF_ALL_EFFECT;
        comparison.Flags |= GTF_RELOP_JMP_USED | GTF_DONT_CSE;

        var jumpStatement = fgNewStmtFromTree(jump, di: switchStatement.DebugInfo);
        fgInsertStmtAtEnd(block, jumpStatement);

        var toTarget = fgAddRefPred(dominantTarget, block, dominantEdge);
        var toSwitch = newBlock.bbPreds ?? throw new System.InvalidOperationException();
        block.SetCond(toTarget, toSwitch);

        var fraction = dominantEdge.Likelihood;
        var removedWeight = block.bbWeight * fraction;
        newBlock.decreaseBBProfileWeight(removedWeight);
        toSwitch.Likelihood = double.Max(0.0, 1.0 - fraction);

        dominantEdge.Likelihood = BB_ZERO_WEIGHT;
        var successors = newBlock.SwitchTargets.Succs;
        for (var i = 0; i < successors.Length; i++)
        {
            var previous = successors[i].Likelihood;
            var likelihood = fraction == 1.0
                ? 1.0 / successors.Length
                : previous / (1.0 - fraction);
            successors[i].Likelihood = double.Min(1.0, likelihood);
        }

        newBlock.SwitchTargets.RemoveDominantCase();
        if (fgNodeThreading is NodeThreading.AllTrees)
        {
#if DEBUG
            JITDUMP($"Rethreading {FMT_STMT(switchStatement.Id)}\n");
#endif
            gtSetStmtInfo(switchStatement);
            fgSetStmtSeq(switchStatement);
        }
    }

    public PhaseStatus optRecognizeAndOptimizeSwitchJumps()
    {
        var modified = false;
        for (var block = fgFirstBB; block is not null; block = block.Next)
        {
            if (block.isRunRarely)
            {
                continue;
            }

            if ((block.Kind is BBJ_COND) && optSwitchDetectForConversion(block))
            {
                JITDUMP($"Converted block {FMT_BB(block.bbNum)} to switch\n");
                modified = true;
                assert(!block.SwitchTargets.HasDominantCase);
            }
            else if ((block.Kind is BBJ_SWITCH) && block.SwitchTargets.HasDominantCase)
            {
                fgPeelSwitch(block);
                modified = true;
                assert(block.Next?.Kind is BBJ_SWITCH);
                block = block.Next;
            }
        }

        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
}
