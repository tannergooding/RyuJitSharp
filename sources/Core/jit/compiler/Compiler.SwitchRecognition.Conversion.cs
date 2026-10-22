// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public partial class Compiler
{
    // This entry covers the conversion mode of native optSwitchDetectAndConvert.
    // The CCMP detection entry retains its own mode-specific threshold and state.
    private bool optSwitchDetectForConversion(BasicBlock firstBlock)
    {
        assert(firstBlock.Kind is BBJ_COND);
        if (Configuration.TARGET_ARM)
        {
            return false;
        }

        if (!IsSwitchConstantTestCondBlock(firstBlock, true, out var trueTarget,
                out var falseTarget, out var reversed, out var variableNode, out var constant))
        {
            return false;
        }

        if (reversed)
        {
            return false;
        }

        global::RyuJitSharp.GenTree __jitExceptionResult236;
        var __jitExceptionCoalesceLeft237 = variableNode;
        if (__jitExceptionCoalesceLeft237 != null)
        {
            __jitExceptionResult236 = __jitExceptionCoalesceLeft237;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException();
            __jitExceptionResult236 = default;
        }
        var switchValue = __jitExceptionResult236;
        var values = new nint[SwitchMaxDistance];
        values[0] = constant;
        var count = 1;
        var falseLikelihood = firstBlock.FalseEdge.Likelihood;
        var previous = firstBlock;

        for (var current = falseTarget; current is not null;)
        {
            if ((current.FirstStmt is null) || (current.FirstStmt != current.LastStmt) ||
                !IsSwitchConstantTestCondBlock(current, false, out var currentTrue,
                    out var currentFalse, out reversed, out var currentVariable, out constant) ||
                (currentTrue != trueTarget) ||
                !GenTree.Compare(currentVariable, switchValue.EffectiveVal) ||
                (current.GetUniquePred(this) != previous) ||
                !BasicBlock.sameEHRegion(previous, current))
            {
                return optSwitchConvertChain(firstBlock, count, values, falseLikelihood, switchValue);
            }

            values[count++] = constant;
            falseLikelihood *= current.FalseEdge.Likelihood;
            if ((count == SwitchMaxDistance) || reversed)
            {
                return optSwitchConvertChain(firstBlock, count, values, falseLikelihood, switchValue);
            }

            previous = current;
            current = currentFalse;
        }

        return false;
    }

    private bool optSwitchConvertChain(BasicBlock firstBlock, int count, nint[] values,
        weight_t falseLikelihood, GenTree switchValue)
    {
        assert(firstBlock.Kind is BBJ_COND);
        assert(!varTypeIsSmall(switchValue.Type));

        var minTests = Configuration.TARGET_ARM64 ? 5 : SwitchMinTests;
        if (count < minTests)
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
            assert(newMax >= newMin);
            if ((newMax - newMin) > SwitchMaxDistance)
            {
                break;
            }

            min = newMin;
            max = newMax;
        }

        count = index;
        if (count < minTests)
        {
            return false;
        }

        var doesntFitTable = max > SwitchMaxDistance;
        var continuousRange = count == (max - min + 1);
        if (doesntFitTable || continuousRange)
        {
            if (min != 0)
            {
                switchValue = gtNewBinaryNode(GT_ADD, switchValue.Type, switchValue,
                    gtNewIconNode(switchValue.Type, -min));
            }
        }
        else
        {
            min = 0;
        }

        var last = firstBlock;
        for (var i = 0; i < count - 1; i++)
        {
            assert(last.Kind is BBJ_COND);
            assert(last.LastStmt?.RootNode.Oper is GT_JTRUE);
            assert(last.LastStmt.RootNode.AsUnOp().Op1.Oper is GT_EQ);
            last = last.FalseTarget;
        }

        var isTest = IsSwitchConstantTestCondBlock(last, false, out var blockIfTrue,
            out var blockIfFalse, out _, out _, out _);
        assert(isTest);
        global::RyuJitSharp.BasicBlock __jitExceptionResult238;
        var __jitExceptionCoalesceLeft239 = blockIfTrue;
        if (__jitExceptionCoalesceLeft239 != null)
        {
            __jitExceptionResult238 = __jitExceptionCoalesceLeft239;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException();
            __jitExceptionResult238 = default;
        }
        var trueTarget = __jitExceptionResult238;
        global::RyuJitSharp.BasicBlock __jitExceptionResult240;
        var __jitExceptionCoalesceLeft241 = blockIfFalse;
        if (__jitExceptionCoalesceLeft241 != null)
        {
            __jitExceptionResult240 = __jitExceptionCoalesceLeft241;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException();
            __jitExceptionResult240 = default;
        }
        var falseTarget = __jitExceptionResult240;
        assert(SkipSwitchFallthroughBlocks(firstBlock.TrueTarget) ==
            SkipSwitchFallthroughBlocks(trueTarget));
        var trueEdge = firstBlock.TrueEdge;
        var falseEdge = firstBlock.FalseEdge;

        var jumpCount = checked((int)(max - min + 1));
        assert((jumpCount > 0) && (jumpCount <= SwitchMaxDistance + 1));
        var uniqueEdges = new FlowEdge[trueTarget == falseTarget ? 1 : 2];
        var targets = new BBswtDesc(uniqueEdges, new int[jumpCount + 1], hasDefault: true);
        firstBlock.SwitchTargets = targets;
        firstBlock.bbCodeOffsEnd = last.bbCodeOffsEnd;

        var statement = firstBlock.LastStmt;
        assert(statement is not null);
        var oldRoot = statement.RootNode;
        assert(oldRoot.Oper is GT_JTRUE);
        var switchTree = new GenTreeUnOp(GT_SWITCH, oldRoot.Type, switchValue, oldRoot, fgNodeThreading);
        oldRoot.Prev = null;
        oldRoot.Next = null;
        statement.RootNode = switchTree;
        gtSetStmtInfo(statement);
        fgSetStmtSeq(statement);
        gtUpdateStmtSideEffects(statement);

        fgRemoveRefPred(falseEdge);
        var toRemove = falseEdge.DestinationBlock;
        for (var i = 0; i < count - 1; i++)
        {
            assert(toRemove.Kind is BBJ_COND);
            var next = toRemove.FalseTarget;
            _ = fgRemoveBlock(toRemove, true);
            toRemove = next;
        }

        fgHasSwitch = true;
        opts.compProcedureSplitting = false;

        ulong bits = 0;
        for (var i = 0; i < count; i++)
        {
            bits |= 1UL << checked((int)(values[i] - min));
        }

        fgRemoveRefPred(trueEdge);
        FlowEdge? switchTrueEdge = null;
        var cases = targets.Cases;
        for (var i = 0; i < jumpCount; i++)
        {
            var isTrue = ((bits >> i) & 1UL) != 0;
            var edge = fgAddRefPred(isTrue ? trueTarget : falseTarget, firstBlock);
            cases[i] = edge;
            if ((switchTrueEdge is null) && isTrue)
            {
                switchTrueEdge = edge;
            }
        }

        assert(switchTrueEdge is not null);
        var defaultEdge = fgAddRefPred(falseTarget, firstBlock);
        cases[jumpCount] = defaultEdge;
        defaultEdge.Likelihood = falseLikelihood;
        switchTrueEdge.Likelihood = 1.0 - falseLikelihood;

        uniqueEdges[0] = cases[0];
        if (uniqueEdges.Length > 1)
        {
            uniqueEdges[1] = cases[0] == switchTrueEdge ? defaultEdge : switchTrueEdge;
        }

        return true;
    }
}
