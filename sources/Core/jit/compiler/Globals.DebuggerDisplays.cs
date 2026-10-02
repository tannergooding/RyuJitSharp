// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class Globals
{
#if DEBUG
    // Each debugger entrypoint has its own counter, matching the native function-local statics.
    private static uint s_cBlockSequence;
    private static uint s_cBlocksSequence;
    private static uint s_cBlocksVSequence;
    private static uint s_cStmtSequence;
    private static uint s_cTreeSequence;
    private static uint s_cTreeLIRSequence;
    private static uint s_cTreeRangeSequence;
    private static uint s_cTreesSequence;
    private static uint s_cEHSequence;
    private static uint s_cVarSequence;
    private static uint s_cVarDscSequence;
    private static uint s_cVarsSequence;
    private static uint s_cVarsFinalSequence;
    private static uint s_cBlockPredsSequence;
    private static uint s_cBlockSuccsSequence;
    private static uint s_cReachSequence;
    private static uint s_cDomsSequence;
    private static uint s_cLivenessSequence;
    private static uint s_cCVarSetSequence;
    private static uint s_cLoopsSequence;
    private static uint s_cLoopsASequence;
    private static uint s_cLoopSequence;
    private static uint s_cScevSequence;
    private static uint s_cVNSequence;
    private static uint s_dRegMaskSequence;
    private static uint s_dBlockListSequence;
    private static uint s_dIsaSequence;
    private static uint s_dIsaFlagsSequence;

    public static void cBlock(Compiler comp, BasicBlock block)
    {
        jitprintf($"===================================================================== *Block {unchecked(s_cBlockSequence++)}\n");
        comp.fgTableDispBasicBlock(block);
    }

    public static void cBlocks(Compiler comp)
    {
        jitprintf($"===================================================================== *Blocks {unchecked(s_cBlocksSequence++)}\n");
        comp.fgDispBasicBlocks();
    }

    public static void cBlocksV(Compiler comp)
    {
        jitprintf($"===================================================================== *BlocksV {unchecked(s_cBlocksVSequence++)}\n");
        comp.fgDispBasicBlocks(true);
    }

    public static void cStmt(Compiler comp, Statement statement)
    {
        jitprintf($"===================================================================== *Stmt {unchecked(s_cStmtSequence++)}\n");
        comp.gtDispStmt(statement, ">>>");
    }

    public static void cTree(Compiler comp, GenTree tree)
    {
        jitprintf($"===================================================================== *Tree {unchecked(s_cTreeSequence++)}\n");
        comp.gtDispTree(tree, ">>>");
    }

    public static void cTreeLIR(Compiler comp, GenTree tree)
    {
        jitprintf($"===================================================================== *TreeLIR {unchecked(s_cTreeLIRSequence++)}\n");
        comp.gtDispLIRNode(tree);
    }

    public static void cTreeRange(Compiler comp, GenTree first, GenTree last)
    {
        jitprintf($"===================================================================== *TreeRange {unchecked(s_cTreeRangeSequence++)}\n");
        var cur = first;

        while (true)
        {
            comp.gtDispLIRNode(cur);
            if (cur == last)
            {
                break;
            }

            cur = cur.Next ?? throw new InvalidOperationException("The LIR range does not reach its last node.");
        }
    }

    public static void cTrees(Compiler comp)
    {
        jitprintf($"===================================================================== *Trees {unchecked(s_cTreesSequence++)}\n");
        comp.fgDumpTrees(comp.fgFirstBB, null);
    }

    public static void cEH(Compiler comp)
    {
        jitprintf($"===================================================================== *EH {unchecked(s_cEHSequence++)}\n");
        comp.fgDispHandlerTab();
    }

    public static unsafe void cVar(Compiler comp, int lclNum)
    {
        jitprintf($"===================================================================== *Var {unchecked(s_cVarSequence++)}\n");
        comp.lvaDumpEntry(lclNum, Compiler.FrameLayoutState.FINAL_FRAME_LAYOUT, 6);
    }

    public static unsafe void cVarDsc(Compiler comp, in LclVarDsc varDsc)
    {
        jitprintf($"===================================================================== *VarDsc {unchecked(s_cVarDscSequence++)}\n");
        var lclNum = comp.lvaGetLclNum(in varDsc);
        comp.lvaDumpEntry(lclNum, Compiler.FrameLayoutState.FINAL_FRAME_LAYOUT, 6);
    }

    public static void cVars(Compiler comp)
    {
        jitprintf($"===================================================================== *Vars {unchecked(s_cVarsSequence++)}\n");
        comp.lvaTableDump();
    }

    public static void cVarsFinal(Compiler comp)
    {
        jitprintf($"===================================================================== *Vars {unchecked(s_cVarsFinalSequence++)}\n");
        comp.lvaTableDump(Compiler.FrameLayoutState.FINAL_FRAME_LAYOUT);
    }

    public static void cBlockPreds(Compiler comp, BasicBlock block)
    {
        jitprintf($"===================================================================== *BlockPreds {unchecked(s_cBlockPredsSequence++)}\n");
        _ = block.dspPreds();
    }

    public static void cBlockSuccs(Compiler comp, BasicBlock block)
    {
        jitprintf($"===================================================================== *BlockSuccs {unchecked(s_cBlockSuccsSequence++)}\n");
        block.dspSuccs();
    }

    public static void cReach(Compiler comp)
    {
        jitprintf($"===================================================================== *Reach {unchecked(s_cReachSequence++)}\n");
        if (comp._reachabilitySets is not null)
        {
            comp._reachabilitySets.Dump();
        }
        else
        {
            jitprintf("  Not computed\n");
        }
    }

    public static void cDoms(Compiler comp)
    {
        jitprintf($"===================================================================== *Doms {unchecked(s_cDomsSequence++)}\n");
        if (comp._domTree is not null)
        {
            comp._domTree.Dump();
        }
        else
        {
            jitprintf("  Not computed\n");
        }
    }

    public static void cLiveness(Compiler comp)
    {
        jitprintf($"===================================================================== *Liveness {unchecked(s_cLivenessSequence++)}\n");
        comp.fgDispBBLiveness();
    }

    public static void cCVarSet(Compiler comp, VARSET_TP vars)
    {
        jitprintf($"===================================================================== *CVarSet {unchecked(s_cCVarSetSequence++)}\n");
        dumpConvertedVarSet(comp, vars);
        jitprintf("\n"); // dumpConvertedVarSet() doesn't emit a trailing newline.
    }

    public static void cLoops(Compiler comp)
    {
        jitprintf($"===================================================================== *Loops {unchecked(s_cLoopsSequence++)}\n");
        FlowGraphNaturalLoops.Dump(comp._loops);
    }

    public static void cLoopsA(Compiler comp, FlowGraphNaturalLoops? loops)
    {
        jitprintf($"===================================================================== *LoopsA {unchecked(s_cLoopsASequence++)}\n");
        FlowGraphNaturalLoops.Dump(loops);
    }

    public static void cLoop(Compiler comp, FlowGraphNaturalLoop? loop)
    {
        jitprintf($"===================================================================== *Loop {unchecked(s_cLoopSequence++)}\n");
        FlowGraphNaturalLoop.Dump(loop);
    }

    public static void cScev(Compiler comp, Scev? scev)
    {
        jitprintf($"===================================================================== *Scev {unchecked(s_cScevSequence++)}\n");
        if (scev is null)
        {
            jitprintf("  NULL\n");
        }
        else
        {
            scev.Dump(comp);
            jitprintf("\n");
        }
    }

    public static void cVN(Compiler comp, ValueNum vn)
    {
        jitprintf($"===================================================================== *VN {unchecked(s_cVNSequence++)}\n");
        comp.vnPrint(vn, 1);
        jitprintf("\n");
    }

    private static Compiler GetDebuggerCompiler()
    {
        return JitTls.Compiler ?? throw new InvalidOperationException("No compiler is active on this thread.");
    }

    public static void dBlock(BasicBlock block)
    {
        cBlock(GetDebuggerCompiler(), block);
    }

    public static void dBlocks()
    {
        cBlocks(GetDebuggerCompiler());
    }

    public static void dBlocksV()
    {
        cBlocksV(GetDebuggerCompiler());
    }

    public static void dStmt(Statement statement)
    {
        cStmt(GetDebuggerCompiler(), statement);
    }

    public static void dTree(GenTree tree)
    {
        cTree(GetDebuggerCompiler(), tree);
    }

    public static void dTreeLIR(GenTree tree)
    {
        cTreeLIR(GetDebuggerCompiler(), tree);
    }

    public static void dTreeRange(GenTree first, GenTree last)
    {
        cTreeRange(GetDebuggerCompiler(), first, last);
    }

    public static void dTrees()
    {
        cTrees(GetDebuggerCompiler());
    }

    public static void dEH()
    {
        cEH(GetDebuggerCompiler());
    }

    public static unsafe void dVar(int lclNum)
    {
        cVar(GetDebuggerCompiler(), lclNum);
    }

    public static unsafe void dVarDsc(in LclVarDsc varDsc)
    {
        cVarDsc(GetDebuggerCompiler(), in varDsc);
    }

    public static void dVars()
    {
        cVars(GetDebuggerCompiler());
    }

    public static void dVarsFinal()
    {
        cVarsFinal(GetDebuggerCompiler());
    }

    public static void dBlockPreds(BasicBlock block)
    {
        cBlockPreds(GetDebuggerCompiler(), block);
    }

    public static void dBlockSuccs(BasicBlock block)
    {
        cBlockSuccs(GetDebuggerCompiler(), block);
    }

    public static void dReach()
    {
        cReach(GetDebuggerCompiler());
    }

    public static void dDoms()
    {
        cDoms(GetDebuggerCompiler());
    }

    public static void dLiveness()
    {
        cLiveness(GetDebuggerCompiler());
    }

    public static void dCVarSet(VARSET_TP vars)
    {
        cCVarSet(GetDebuggerCompiler(), vars);
    }

    public static void dLoops()
    {
        cLoops(GetDebuggerCompiler());
    }

    public static void dLoopsA(FlowGraphNaturalLoops? loops)
    {
        cLoopsA(GetDebuggerCompiler(), loops);
    }

    public static void dLoop(FlowGraphNaturalLoop? loop)
    {
        cLoop(GetDebuggerCompiler(), loop);
    }

    public static void dScev(Scev? scev)
    {
        cScev(GetDebuggerCompiler(), scev);
    }

    public static void dTreeFlags(GenTree tree)
    {
        cTreeFlags(GetDebuggerCompiler(), tree);
    }

    public static void dVN(ValueNum vn)
    {
        cVN(GetDebuggerCompiler(), vn);
    }

    public static void dRegMask(in regMaskTP mask)
    {
        jitprintf($"===================================================================== dRegMask {unchecked(s_dRegMaskSequence++)}\n");
        dspRegMask(mask);
        jitprintf("\n"); // dspRegMask() doesn't emit a trailing newline.
    }

    public static void dBlockList(BasicBlockList? list)
    {
        jitprintf($"===================================================================== dBlockList {unchecked(s_dBlockListSequence++)}\n");
        while (list is not null)
        {
            jitprintf($"{FMT_BB(list.Block.bbNum)} ");
            list = list.Next;
        }

        jitprintf("\n");
    }

    public static void dIsa(CORINFO_InstructionSet isa)
    {
        jitprintf($"===================================================================== dIsa {unchecked(s_dIsaSequence++)}\n");
        jitprintf($"{InstructionSetToString(isa)}\n");
    }

    public static unsafe void dIsaFlags(in CORINFO_InstructionSetFlags isaFlags)
    {
        jitprintf($"===================================================================== dIsaFlags {unchecked(s_dIsaFlagsSequence++)}\n");
        if (isaFlags.IsEmpty())
        {
            jitprintf("<empty>\n");
        }
        else
        {
            var first = true;
            // The native flag range starts at 1 and extends to the last bit in the flags storage.
            var isaFirst = (CORINFO_InstructionSet)1;
            var isaLast = (CORINFO_InstructionSet)(sizeof(CORINFO_InstructionSetFlags) * 8 - 1);

            for (var isa = isaFirst; isa <= isaLast; isa = (CORINFO_InstructionSet)((int)isa + 1))
            {
                if (isaFlags.HasInstructionSet(isa))
                {
                    jitprintf($"{(first ? "" : " ")}{InstructionSetToString(isa)}");
                    first = false;
                }
            }
        }

        jitprintf("\n");
    }
#endif
}
