// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optIsProfitableToHoistTree(GenTree tree, FlowGraphNaturalLoop loop,
        LoopHoistContext context, bool defExecuted)
    {
        var loopContainsCall = _loopSideEffects![loop.Index].ContainsCall;
        int available;
        int hoisted;
        int loopVars;
        int inOut;

        if (varTypeUsesIntReg(tree.Type))
        {
            hoisted = context.HoistedExprCount;
            loopVars = context.LoopVarCount;
            inOut = context.LoopVarInOutCount;
            available = CNT_CALLEE_SAVED - 1;
            if (!loopContainsCall)
            {
                available += CNT_CALLEE_TRASH_INT - 1;
            }
#if !TARGET_64BIT
            if (varTypeIsLong(tree.Type))
            {
                available = (available + 1) / 2;
            }
#endif
        }
#if FEATURE_MASKED_HW_INTRINSICS
        else if (varTypeUsesMaskReg(tree.Type))
        {
            hoisted = context.HoistedMskExprCount;
            loopVars = context.LoopVarMskCount;
            inOut = context.LoopVarInOutMskCount;
            available = CNT_CALLEE_SAVED_MASK;
            if (!loopContainsCall)
            {
                available += CNT_CALLEE_TRASH_MASK - 1;
            }
        }
#endif
        else
        {
            assert(varTypeUsesFloatReg(tree.Type));
            hoisted = context.HoistedFPExprCount;
            loopVars = context.LoopVarFPCount;
            inOut = context.LoopVarInOutFPCount;
            available = CNT_CALLEE_SAVED_FLOAT;
            if (!loopContainsCall)
            {
                available += CNT_CALLEE_TRASH_FLOAT - 1;
            }
#if TARGET_ARM
            available /= 2;
#endif
        }

        available -= hoisted;
        assert(loopVars <= inOut);
        if (!defExecuted && tree.CostEx < IND_COST_EX * 16)
        {
            return false;
        }

        if (loopVars >= available && tree.CostEx < 2 * IND_COST_EX)
        {
            JITDUMP($"    tree cost too low: {tree.CostEx} < {2 * IND_COST_EX} " +
                $"(loopVarCount {loopVars} >= availRegCount {available})\n");
            return false;
        }

        if (inOut > available && tree.CostEx <= MIN_CSE_COST + 1)
        {
            JITDUMP($"    tree not good CSE: {tree.CostEx} <= {2 * MIN_CSE_COST + 1} " +
                $"(varInOutCount {inOut} > availRegCount {available})\n");
            return false;
        }

        return true;
    }

    private void optHoistCandidate(GenTree tree, BasicBlock block, FlowGraphNaturalLoop loop,
        LoopHoistContext context, bool defExecuted)
    {
        if (!optIsProfitableToHoistTree(tree, loop, context, defExecuted))
        {
            JITDUMP("   ... not profitable to hoist\n");
            return;
        }

        var vn = tree._vnPair.Liberal;
        if (context.GetHoistedInCurLoop().ContainsKey(vn))
        {
#if DEBUG
            JITDUMP($"      [{tree.TreeId:D6}] ... already hoisted ${vn:x} in L{loop.Index:D2}\n ");
#endif
            return;
        }

        assert(loop.EntryEdges.Length == 1);
        var preheader = loop.EntryEdge(0).SourceBlock;
        if (!BasicBlock.sameTryRegion(preheader, block))
        {
            JITDUMP($"   ... not hoisting in L{loop.Index:D2}, eh region constraint " +
                $"(pre-header try index {preheader.bbTryIndex}, candidate {FMT_BB(block.bbNum)} " +
                $"try index {block.bbTryIndex}\n");
            return;
        }

#if DEBUG
        var limit = JitConfig.JitHoistLimit;
        var current = unchecked((uint)_totalHoistedExpressions);
        if (limit >= 0 && current >= (uint)limit)
        {
            JITDUMP($"   ... not hoisting in L{loop.Index:D2}, hoist count {current} >= JitHoistLimit {(uint)limit}\n");
            return;
        }
#endif
        optPerformHoistExpr(tree, block, loop);
        if (varTypeUsesIntReg(tree.Type))
        {
            context.HoistedExprCount++;
#if !TARGET_64BIT
            if (varTypeIsLong(tree.Type))
            {
                context.HoistedExprCount++;
            }
#endif
        }
#if FEATURE_MASKED_HW_INTRINSICS
        else if (varTypeUsesMaskReg(tree.Type))
        {
            context.HoistedMskExprCount++;
        }
#endif
        else
        {
            assert(varTypeUsesFloatReg(tree.Type));
            context.HoistedFPExprCount++;
        }

        context.GetHoistedInCurLoop()[vn] = true;
        Metrics.HoistedExpressions++;
    }
}
