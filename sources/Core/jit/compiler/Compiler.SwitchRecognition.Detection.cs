// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    private const int SwitchMaxDistance = (TARGET_POINTER_SIZE * 8) - 1;
    private const int SwitchMinTests = 3;
    private const int ConvertSwitchToCcmpMinTest = 5;

    private BasicBlock SkipSwitchFallthroughBlocks(BasicBlock block)
    {
        var original = block;
        if ((block.Kind is not BBJ_ALWAYS) || (block.FirstStmt is not null))
        {
            return block;
        }

        var traits = new BitVecTraits(this, compBasicBlockID);
        var visited = BitVecOps.MakeEmpty(traits);
        BitVecOps.AddElemD(traits, visited, block.bbID);

        while ((block.Kind is BBJ_ALWAYS) && (block.FirstStmt is null) &&
               BasicBlock.sameEHRegion(block, block.Target))
        {
            block = block.Target;
            if (!BitVecOps.TryAddElemD(traits, visited, block.bbID))
            {
                return original;
            }
        }

        return block;
    }

    private bool IsSwitchConstantTestCondBlock(BasicBlock block, bool allowSideEffects,
        out BasicBlock? trueTarget, out BasicBlock? falseTarget, out bool isReversed,
        out GenTree? variableNode, out nint constant)
    {
        trueTarget = null;
        falseTarget = null;
        isReversed = false;
        variableNode = null;
        constant = 0;

        if ((block.Kind is not BBJ_COND) || (block.LastStmt is null) || block.HasFlag(BBF_DONT_REMOVE))
        {
            return false;
        }

        var root = block.LastStmt.RootNode;
        assert(root.Oper is GT_JTRUE);
        var comparison = root.AsUnOp().Op1;
        if (comparison.Oper is not (GT_EQ or GT_NE))
        {
            return false;
        }

        var left = comparison.AsOp().Op1;
        var right = comparison.AsOp().Op2;
        if (!varTypeIsIntOrI(left.Type) || !varTypeIsIntOrI(right.Type))
        {
            return false;
        }

        var leftConstant = left.Oper.IsCnsIntOrI && !left.IsIconHandle();
        var rightConstant = right.Oper.IsCnsIntOrI && !right.IsIconHandle();
        if (leftConstant == rightConstant)
        {
            return false;
        }

        if (allowSideEffects)
        {
            if ((left.EffectiveVal.Oper is not GT_LCL_VAR) &&
                (right.EffectiveVal.Oper is not GT_LCL_VAR))
            {
                return false;
            }
        }
        else if ((left.Oper is not GT_LCL_VAR) && (right.Oper is not GT_LCL_VAR))
        {
            return false;
        }

        isReversed = comparison.Oper is GT_NE;
        trueTarget = SkipSwitchFallthroughBlocks(isReversed ? block.FalseTarget : block.TrueTarget);
        falseTarget = SkipSwitchFallthroughBlocks(isReversed ? block.TrueTarget : block.FalseTarget);
        if ((block.FalseTarget == block) || (block.TrueTarget == block))
        {
            return false;
        }

        variableNode = leftConstant ? right : left;
        constant = (leftConstant ? left : right).AsIntCon().IconValue;
        return true;
    }

    // This entry is the detection/CCMP mode of optSwitchDetectAndConvert. Conversion is a
    // separate, unported phase; this caller never requests it.
    private bool optSwitchDetectForCcmp(BasicBlock firstBlock, BitVec ccmpVec)
    {
        assert(firstBlock.Kind is BBJ_COND);
        var traits = new BitVecTraits(this, fgBBNumMax + 1);
        if (!IsSwitchConstantTestCondBlock(firstBlock, true, out var trueTarget,
                out var falseTarget, out var reversed, out var variableNode, out var constant))
        {
            return false;
        }

        if (reversed)
        {
            return false;
        }

        var testedVariable = variableNode ?? throw new System.InvalidOperationException();
        if (BitVecOps.IsMember(traits, ccmpVec, firstBlock.bbNum))
        {
            BitVecOps.RemoveElemD(traits, ccmpVec, firstBlock.bbNum);
            return true;
        }

        BitVecOps.ClearD(traits, ccmpVec);

        var values = new nint[SwitchMaxDistance];
        values[0] = constant;
        var count = 1;
        var previous = firstBlock;

        for (var current = falseTarget; current is not null;)
        {
            if ((current.FirstStmt is null) || (current.FirstStmt != current.LastStmt) ||
                !IsSwitchConstantTestCondBlock(current, false, out var currentTrue,
                    out var currentFalse, out reversed, out var currentVariable, out constant) ||
                (currentTrue != trueTarget) ||
                !GenTree.Compare(currentVariable, testedVariable.EffectiveVal) ||
                (current.GetUniquePred(this) != previous) ||
                !BasicBlock.sameEHRegion(previous, current))
            {
                return optSwitchConvertForCcmp(firstBlock, count, values, testedVariable, ccmpVec);
            }

            values[count++] = constant;
            if ((count == SwitchMaxDistance) || reversed)
            {
                return optSwitchConvertForCcmp(firstBlock, count, values, testedVariable, ccmpVec);
            }

            previous = current;
            current = currentFalse;
        }

        return false;
    }

    private bool optSwitchConvertForCcmp(BasicBlock firstBlock, int count, nint[] values,
        GenTree switchValue, BitVec ccmpVec)
    {
        if ((count < ConvertSwitchToCcmpMinTest) || (count < SwitchMinTests))
        {
            return false;
        }

        var min = values[0];
        var max = values[0];
        var index = 0;
        for (; index < count; index++)
        {
            var value = values[index];
            if (value < 0)
            {
                break;
            }

            var newMin = nint.Min(min, value);
            var newMax = nint.Max(max, value);
            if ((newMax - newMin) > SwitchMaxDistance)
            {
                break;
            }

            min = newMin;
            max = newMax;
        }

        count = index;
        if (count < SwitchMinTests)
        {
            return false;
        }

        if (((max > SwitchMaxDistance) || (count == (max - min + 1))) && (min != 0))
        {
            // The native test mode still constructs this unused normalization tree.
            _ = gtNewBinaryNode(GT_ADD, switchValue.Type, switchValue,
                gtNewIconNode(switchValue.Type, -min));
        }

        var last = firstBlock;
        for (var i = 0; i < count - 1; i++)
        {
            assert(last.LastStmt?.RootNode.Oper is GT_JTRUE);
            assert(last.LastStmt.RootNode.AsUnOp().Op1.Oper is GT_EQ);
            last = last.FalseTarget;
        }

        assert(IsSwitchConstantTestCondBlock(last, false, out var trueTarget,
            out _, out _, out _, out _));
        assert(SkipSwitchFallthroughBlocks(firstBlock.TrueTarget) == trueTarget);

        var traits = new BitVecTraits(this, fgBBNumMax + 1);
        var current = firstBlock;
        for (var i = 0; i < count; i++)
        {
            BitVecOps.AddElemD(traits, ccmpVec, current.bbNum);
            current = current.FalseTarget;
        }

        return true;
    }
}
