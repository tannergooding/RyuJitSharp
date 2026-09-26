// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    protected PhaseStatus optHoistLoopCode()
    {
        var loops = _loops ?? throw new FatalJitException("Loop hoisting requires loop discovery.");
        if (loops.NumLoops == 0)
        {
            JITDUMP("\nNo loops; no hoisting\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

#if DEBUG
        if (unchecked((uint)JitConfig.JitNoHoist) > 0)
        {
            JITDUMP("\nJitNoHoist set; no hoisting\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        if (verbose)
        {
            jitprintf("\n*************** In optHoistLoopCode()\n");
            fgDispHandlerTab();
        }
#endif
        optComputeInterestingVarSets();

        var modified = false;
        var context = new LoopHoistContext();
        foreach (var loop in loops.InPostOrder())
        {
#if LOOP_HOIST_STATS
            _curLoopHasHoistedExpression = false;
            _loopsConsidered++;
#endif
            modified |= optHoistThisLoop(loop, context);
        }

#if DEBUG
        if (_nodeTestData is not null)
        {
            foreach (var (node, annotation) in _nodeTestData)
            {
                if (annotation._tl is not TL_LoopHoist)
                {
                    continue;
                }

                assert(annotation._num < 100);
                if (annotation._num >= 0)
                {
                    jitprintf("Node ");
                    printTreeId(node);
                    jitprintf(" was declared 'must hoist', but has not been hoisted.\n");
                    assert(false);
                }
            }
        }
#endif
        return modified ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }

    private bool optHoistThisLoop(FlowGraphNaturalLoop loop, LoopHoistContext context)
    {
        context.CurLoopVnInvariantCache.Clear();
        var sideEffects = _loopSideEffects?[loop.Index]
            ?? throw new FatalJitException("Loop hoisting requires loop side-effect analysis.");
#if DEBUG
        if (verbose)
        {
            jitprintf("optHoistThisLoop processing ");
            FlowGraphNaturalLoop.Dump(loop);
            jitprintf($"  Loop body {(sideEffects.ContainsCall ? "contains" : "does not contain")} a call\n");
        }
#endif
        var loopVars = VarSetOps.Intersection(this, sideEffects.VarInOut, sideEffects.VarUseDef);
        context.LoopVarInOutCount = (int)VarSetOps.Count(this, sideEffects.VarInOut);
        context.LoopVarCount = (int)VarSetOps.Count(this, loopVars);
        context.HoistedExprCount = 0;

#if !TARGET_64BIT
        if (!VarSetOps.IsEmpty(this, lvaLongVars))
        {
            var loopLongVars = VarSetOps.Intersection(this, loopVars, lvaLongVars);
            var inOutLongVars = VarSetOps.Intersection(this, sideEffects.VarInOut, lvaLongVars);
#if DEBUG
            if (verbose)
            {
                jitprintf($"\n  LONGVARS({VarSetOps.Count(this, lvaLongVars)})=");
                dumpConvertedVarSet(this, lvaLongVars);
            }
#endif
            context.LoopVarCount += (int)VarSetOps.Count(this, loopLongVars);
            context.LoopVarInOutCount += (int)VarSetOps.Count(this, inOutLongVars);
        }
#endif
#if DEBUG
        if (verbose)
        {
            jitprintf($"\n  USEDEF  ({VarSetOps.Count(this, sideEffects.VarUseDef)})=");
            dumpConvertedVarSet(this, sideEffects.VarUseDef);
            jitprintf($"\n  INOUT   ({context.LoopVarInOutCount})=");
            dumpConvertedVarSet(this, sideEffects.VarInOut);
            jitprintf($"\n  LOOPVARS({context.LoopVarCount})=");
            dumpConvertedVarSet(this, loopVars);
            jitprintf("\n");
        }
#endif
        if (!VarSetOps.IsEmpty(this, lvaFloatVars))
        {
            var loopFPVars = VarSetOps.Intersection(this, loopVars, lvaFloatVars);
            var inOutFPVars = VarSetOps.Intersection(this, sideEffects.VarInOut, lvaFloatVars);
            context.LoopVarFPCount = (int)VarSetOps.Count(this, loopFPVars);
            context.LoopVarInOutFPCount = (int)VarSetOps.Count(this, inOutFPVars);
            context.HoistedFPExprCount = 0;
            context.LoopVarCount -= context.LoopVarFPCount;
            context.LoopVarInOutCount -= context.LoopVarInOutFPCount;
#if DEBUG
            if (verbose)
            {
                jitprintf($"  INOUT-FP({context.LoopVarInOutFPCount})=");
                dumpConvertedVarSet(this, inOutFPVars);
                jitprintf($"\n  LOOPV-FP({context.LoopVarFPCount})=");
                dumpConvertedVarSet(this, loopFPVars);
                jitprintf("\n");
            }
#endif
        }
        else
        {
            context.LoopVarFPCount = 0;
            context.LoopVarInOutFPCount = 0;
            context.HoistedFPExprCount = 0;
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (!VarSetOps.IsEmpty(this, lvaMaskVars))
        {
            var loopMskVars = VarSetOps.Intersection(this, loopVars, lvaMaskVars);
            var inOutMskVars = VarSetOps.Intersection(this, sideEffects.VarInOut, lvaMaskVars);
            context.LoopVarMskCount = (int)VarSetOps.Count(this, loopMskVars);
            context.LoopVarInOutMskCount = (int)VarSetOps.Count(this, inOutMskVars);
            context.HoistedMskExprCount = 0;
            context.LoopVarCount -= context.LoopVarMskCount;
            context.LoopVarInOutCount -= context.LoopVarInOutMskCount;
#if DEBUG
            if (verbose)
            {
                jitprintf($"  INOUT-MSK({context.LoopVarInOutMskCount})=");
                dumpConvertedVarSet(this, inOutMskVars);
                jitprintf($"\n  LOOPV-MSK({context.LoopVarMskCount})=");
                dumpConvertedVarSet(this, loopMskVars);
                jitprintf("\n");
            }
#endif
        }
        else
        {
            context.LoopVarMskCount = 0;
            context.LoopVarInOutMskCount = 0;
            context.HoistedMskExprCount = 0;
        }
#endif
        var dfsTree = _dfsTree ?? throw new FatalJitException("Loop hoisting requires a DFS tree.");
        var domTree = _domTree ?? throw new FatalJitException("Loop hoisting requires dominators.");
        var traits = dfsTree.PostOrderTraits();
        var defExec = BitVecOps.MakeEmpty(traits);

        for (var child = loop.Child; child is not null; child = child.Sibling)
        {
            assert(child.EntryEdges.Length == 1);
            var childPreheader = child.EntryEdge(0).SourceBlock;
            if (loop.ExitEdges.Length == 1)
            {
                if (domTree.Dominates(childPreheader, loop.ExitEdge(0).SourceBlock))
                {
                    continue;
                }
            }
            else if (childPreheader == loop.Header)
            {
                continue;
            }

            JITDUMP($"  --  {FMT_BB(childPreheader.bbNum)} (child loop pre-header)\n");
            BitVecOps.AddElemD(traits, defExec, childPreheader.bbPostorderNum);
        }

        if (loop.ExitEdges.Length == 1)
        {
            var exiting = loop.ExitEdge(0).SourceBlock;
            JITDUMP($"  Considering hoisting in blocks that either dominate exit block {FMT_BB(exiting.bbNum)}, " +
                "or pre-headers of nested loops, if any:\n");
            var cur = exiting;
            while (cur is not null && cur != loop.Header && loop.ContainsBlock(cur))
            {
                JITDUMP($"  --  {FMT_BB(cur.bbNum)} (dominate exit block)\n");
                BitVecOps.AddElemD(traits, defExec, cur.bbPostorderNum);
                cur = cur.bbIDom;
            }
            assert(cur == loop.Header || bbIsTryBeg(loop.Header));
        }
        else
        {
            JITDUMP($"  Considering hoisting in entry block {FMT_BB(loop.Header.bbNum)} " +
                $"because L{loop.Index:D2} has more than one exit\n");
        }

        JITDUMP($"  --  {FMT_BB(loop.Header.bbNum)} (header block)\n");
        BitVecOps.AddElemD(traits, defExec, loop.Header.bbPostorderNum);
        optHoistLoopBlocks(loop, traits, defExec, context);

        var numHoisted = context.HoistedExprCount + context.HoistedFPExprCount;
#if FEATURE_MASKED_HW_INTRINSICS
        numHoisted += context.HoistedMskExprCount;
#endif
        return numHoisted > 0;
    }
}
