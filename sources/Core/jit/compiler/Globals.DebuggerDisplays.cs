// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using DebuggerDisplaySequences = RyuJitSharp.Compiler.DebugData.DebuggerDisplaySequences;

namespace RyuJitSharp;

public static partial class Globals
{
    public static void cBlock(Compiler comp, BasicBlock block)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Block {unchecked(DebuggerDisplaySequences.s_cBlockSequence++)}\n");
        comp.fgTableDispBasicBlock(block);
    }

    public static void cBlocks(Compiler comp)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Blocks {unchecked(DebuggerDisplaySequences.s_cBlocksSequence++)}\n");
        comp.fgDispBasicBlocks();
    }

    public static void cBlocksV(Compiler comp)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *BlocksV {unchecked(DebuggerDisplaySequences.s_cBlocksVSequence++)}\n");
        comp.fgDispBasicBlocks(true);
    }

    public static void cStmt(Compiler comp, Statement statement)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Stmt {unchecked(DebuggerDisplaySequences.s_cStmtSequence++)}\n");
        comp.gtDispStmt(statement, ">>>");
    }

    public static void cTree(Compiler comp, GenTree tree)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Tree {unchecked(DebuggerDisplaySequences.s_cTreeSequence++)}\n");
        comp.gtDispTree(tree, ">>>");
    }

    public static void cTreeLIR(Compiler comp, GenTree tree)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *TreeLIR {unchecked(DebuggerDisplaySequences.s_cTreeLIRSequence++)}\n");
        comp.gtDispLIRNode(tree);
    }

    public static void cTreeRange(Compiler comp, GenTree first, GenTree last)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *TreeRange {unchecked(DebuggerDisplaySequences.s_cTreeRangeSequence++)}\n");
        var cur = first;

        while (true)
        {
            comp.gtDispLIRNode(cur);
            if (cur == last)
            {
                break;
            }

            var next = cur.Next;
            if (next is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LIRRangeDoesNotReachItsLastNode);
            }

            cur = next;
        }
    }

    public static void cTrees(Compiler comp)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Trees {unchecked(DebuggerDisplaySequences.s_cTreesSequence++)}\n");
        comp.fgDumpTrees(comp.fgFirstBB, null);
    }

    public static void cEH(Compiler comp)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *EH {unchecked(DebuggerDisplaySequences.s_cEHSequence++)}\n");
        comp.fgDispHandlerTab();
    }

    public static unsafe void cVar(Compiler comp, int lclNum)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Var {unchecked(DebuggerDisplaySequences.s_cVarSequence++)}\n");
        comp.lvaDumpEntry(lclNum, Compiler.FrameLayoutState.FINAL_FRAME_LAYOUT, 6);
    }

    public static unsafe void cVarDsc(Compiler comp, in LclVarDsc varDsc)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *VarDsc {unchecked(DebuggerDisplaySequences.s_cVarDscSequence++)}\n");
        var lclNum = comp.lvaGetLclNum(in varDsc);
        comp.lvaDumpEntry(lclNum, Compiler.FrameLayoutState.FINAL_FRAME_LAYOUT, 6);
    }

    public static void cVars(Compiler comp)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Vars {unchecked(DebuggerDisplaySequences.s_cVarsSequence++)}\n");
        comp.lvaTableDump();
    }

    public static void cVarsFinal(Compiler comp)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Vars {unchecked(DebuggerDisplaySequences.s_cVarsFinalSequence++)}\n");
        comp.lvaTableDump(Compiler.FrameLayoutState.FINAL_FRAME_LAYOUT);
    }

    public static void cBlockPreds(Compiler comp, BasicBlock block)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *BlockPreds {unchecked(DebuggerDisplaySequences.s_cBlockPredsSequence++)}\n");
        _ = block.dspPreds();
    }

    public static void cBlockSuccs(Compiler comp, BasicBlock block)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *BlockSuccs {unchecked(DebuggerDisplaySequences.s_cBlockSuccsSequence++)}\n");
        block.dspSuccs();
    }

    public static void cReach(Compiler comp)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Reach {unchecked(DebuggerDisplaySequences.s_cReachSequence++)}\n");
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
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Doms {unchecked(DebuggerDisplaySequences.s_cDomsSequence++)}\n");
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
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Liveness {unchecked(DebuggerDisplaySequences.s_cLivenessSequence++)}\n");
        comp.fgDispBBLiveness();
    }

    public static void cCVarSet(Compiler comp, VARSET_TP vars)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *CVarSet {unchecked(DebuggerDisplaySequences.s_cCVarSetSequence++)}\n");
        dumpConvertedVarSet(comp, vars);
        jitprintf("\n"); // dumpConvertedVarSet() doesn't emit a trailing newline.
    }

    public static void cLoops(Compiler comp)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Loops {unchecked(DebuggerDisplaySequences.s_cLoopsSequence++)}\n");
        FlowGraphNaturalLoops.Dump(comp._loops);
    }

    public static void cLoopsA(Compiler comp, FlowGraphNaturalLoops? loops)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *LoopsA {unchecked(DebuggerDisplaySequences.s_cLoopsASequence++)}\n");
        FlowGraphNaturalLoops.Dump(loops);
    }

    public static void cLoop(Compiler comp, FlowGraphNaturalLoop? loop)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Loop {unchecked(DebuggerDisplaySequences.s_cLoopSequence++)}\n");
        FlowGraphNaturalLoop.Dump(loop);
    }

    public static void cScev(Compiler comp, Scev? scev)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *Scev {unchecked(DebuggerDisplaySequences.s_cScevSequence++)}\n");
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
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== *VN {unchecked(DebuggerDisplaySequences.s_cVNSequence++)}\n");
        comp.vnPrint(vn, 1);
        jitprintf("\n");
    }

    private static Compiler GetDebuggerCompiler()
    {
        var compiler = JitTls.Compiler;
        if (compiler is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.NoCompilerIsActiveOnThisThread);
        }

        return compiler;
    }

    public static void dBlock(BasicBlock block)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cBlock(GetDebuggerCompiler(), block);
    }

    public static void dBlocks()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cBlocks(GetDebuggerCompiler());
    }

    public static void dBlocksV()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cBlocksV(GetDebuggerCompiler());
    }

    public static void dStmt(Statement statement)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cStmt(GetDebuggerCompiler(), statement);
    }

    public static void dTree(GenTree tree)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cTree(GetDebuggerCompiler(), tree);
    }

    public static void dTreeLIR(GenTree tree)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cTreeLIR(GetDebuggerCompiler(), tree);
    }

    public static void dTreeRange(GenTree first, GenTree last)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cTreeRange(GetDebuggerCompiler(), first, last);
    }

    public static void dTrees()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cTrees(GetDebuggerCompiler());
    }

    public static void dEH()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cEH(GetDebuggerCompiler());
    }

    public static unsafe void dVar(int lclNum)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cVar(GetDebuggerCompiler(), lclNum);
    }

    public static unsafe void dVarDsc(in LclVarDsc varDsc)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cVarDsc(GetDebuggerCompiler(), in varDsc);
    }

    public static void dVars()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cVars(GetDebuggerCompiler());
    }

    public static void dVarsFinal()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cVarsFinal(GetDebuggerCompiler());
    }

    public static void dBlockPreds(BasicBlock block)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cBlockPreds(GetDebuggerCompiler(), block);
    }

    public static void dBlockSuccs(BasicBlock block)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cBlockSuccs(GetDebuggerCompiler(), block);
    }

    public static void dReach()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cReach(GetDebuggerCompiler());
    }

    public static void dDoms()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cDoms(GetDebuggerCompiler());
    }

    public static void dLiveness()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cLiveness(GetDebuggerCompiler());
    }

    public static void dCVarSet(VARSET_TP vars)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cCVarSet(GetDebuggerCompiler(), vars);
    }

    public static void dLoops()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cLoops(GetDebuggerCompiler());
    }

    public static void dLoopsA(FlowGraphNaturalLoops? loops)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cLoopsA(GetDebuggerCompiler(), loops);
    }

    public static void dLoop(FlowGraphNaturalLoop? loop)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cLoop(GetDebuggerCompiler(), loop);
    }

    public static void dScev(Scev? scev)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cScev(GetDebuggerCompiler(), scev);
    }

    public static void dTreeFlags(GenTree tree)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cTreeFlags(GetDebuggerCompiler(), tree);
    }

    public static void dVN(ValueNum vn)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        cVN(GetDebuggerCompiler(), vn);
    }

    public static void dRegMask(in regMaskTP mask)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== dRegMask {unchecked(DebuggerDisplaySequences.s_dRegMaskSequence++)}\n");
        dspRegMask(mask);
        jitprintf("\n"); // dspRegMask() doesn't emit a trailing newline.
    }

    public static void dBlockList(BasicBlockList? list)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== dBlockList {unchecked(DebuggerDisplaySequences.s_dBlockListSequence++)}\n");
        while (list is not null)
        {
            jitprintf($"{FMT_BB(list.Block.bbNum)} ");
            list = list.Next;
        }

        jitprintf("\n");
    }

    public static void dIsa(ManagedInstructionSet isa)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== dIsa {unchecked(DebuggerDisplaySequences.s_dIsaSequence++)}\n");
        jitprintf($"{InstructionSetToString(isa)}\n");
    }

    public static unsafe void dIsaFlags(in CORINFO_InstructionSetFlags isaFlags)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        jitprintf($"===================================================================== dIsaFlags {unchecked(DebuggerDisplaySequences.s_dIsaFlagsSequence++)}\n");
        if (isaFlags.IsEmpty())
        {
            jitprintf("<empty>\n");
        }
        else
        {
            var first = true;
            // The native flag range starts at 1 and extends to the last bit in the flags storage.
            var isaFirst = 1;
            var isaLast = sizeof(CORINFO_InstructionSetFlags) * 8 - 1;

            for (var nativeOrdinal = isaFirst; nativeOrdinal <= isaLast; nativeOrdinal++)
            {
                if (isaFlags.HasNativeInstructionSetOrdinal(nativeOrdinal))
                {
                    var name = TryGetManagedInstructionSet(nativeOrdinal, out var isa)
                        ? InstructionSetToString(isa)
                        : "UnknownInstructionSet";
                    jitprintf($"{(first ? "" : " ")}{name}");
                    first = false;
                }
            }
        }

        jitprintf("\n");
    }
}
