// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, inductionvariableopts.cpp.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static RyuJitSharp.BasicBlockVisit;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private GenTree? optFindIVParent(Statement stmt, GenTree tree)
    {
        var link = gtFindLink(stmt, tree);
        if (Unsafe.IsNullRef(ref link.result))
        {
            throw new FatalJitException("An induction variable use is not owned by its statement.");
        }
        return link.parent;
    }

    private bool optIsIVWideningProfitable(int lclNum, BasicBlock initBlock, bool initedToConstant,
        FlowGraphNaturalLoop loop, PerLoopInfo loopInfo)
    {
        var loops = _loops ?? throw new FatalJitException("IV widening requires natural loops.");
        foreach (var otherLoop in loops.InReversePostOrder())
        {
            if (otherLoop == loop)
            {
                continue;
            }
            for (var stmt = otherLoop.Header.FirstStmt;
                (stmt is not null) && stmt.IsPhiDefnStmt; stmt = stmt.NextStmt)
            {
                if (stmt.RootNode.AsLclVarCommon().LclNum == lclNum)
                {
#if DEBUG
                    JITDUMP($"  V{lclNum:D2} has a phi [{stmt.RootNode.TreeId:D6}] in " +
                        $"L{otherLoop.Index:D2}'s header {FMT_BB(otherLoop.Header.bbNum)}\n");
#endif
                    return false;
                }
            }
        }

        const int extensionSize = 3;
        const double extensionCost = 2;
        var savedCost = 0.0;
        var savedSize = 0;
        _ = loopInfo.VisitOccurrences(loop, lclNum, (block, stmt, local) => {
            var parent = optFindIVParent(stmt, local);
            if ((parent is null) || (parent.Oper is not GT_CAST))
            {
                return true;
            }

            var cast = parent.AsCast();
            if ((cast.CastType is not TYP_LONG) || !cast.IsUnsigned || cast.HasOverflowCheck)
            {
                return true;
            }
            var grandparent = optFindIVParent(stmt, cast);
            if ((grandparent is not null) && (grandparent.Oper is GT_STORE_LCL_VAR))
            {
                return true;
            }

            savedSize += extensionSize;
            savedCost += block.getBBWeight(this) * extensionCost;
            return true;
        });

        if (!initedToConstant)
        {
            savedSize -= extensionSize;
            savedCost -= initBlock.getBBWeight(this) * extensionCost;
        }

        assert(lvaGetDesc(lclNum).lvInSsa);
        _ = loop.VisitRegularExitBlocks(exit => {
            if (optLocalIsLiveIntoBlock(lclNum, exit))
            {
                savedSize -= extensionSize;
                savedCost -= exit.getBBWeight(this) * extensionCost;
            }
            return Continue;
        });

        const double allowedSizeRegressionPerCycleImprovement = 2;
        var entry = fgFirstBB ?? throw new FatalJitException("IV widening requires an entry block.");
        var cycleImprovementPerInvocation = savedCost / entry.getBBWeight(this);
        JITDUMP($"  Estimated cycle improvement: {FMT_WT(cycleImprovementPerInvocation)} cycles per invocation\n");
        JITDUMP($"  Estimated size improvement: {savedSize} bytes\n");
        if ((cycleImprovementPerInvocation > 0) &&
            ((cycleImprovementPerInvocation * allowedSizeRegressionPerCycleImprovement) >= -savedSize))
        {
            JITDUMP("    Widening is profitable (cycle improvement)\n");
            return true;
        }

        const double allowedCycleRegressionPerSizeImprovement = 0.01;
        if ((savedSize > 0) &&
            ((savedSize * allowedCycleRegressionPerSizeImprovement) >= -cycleImprovementPerInvocation))
        {
            JITDUMP("  Widening is profitable (size improvement)\n");
            return true;
        }

        JITDUMP("  Widening is not profitable\n");
        return false;
    }

    private void optSinkWidenedIV(int lclNum, int newLclNum, FlowGraphNaturalLoop loop)
    {
        assert(lvaGetDesc(lclNum).lvInSsa);
        _ = loop.VisitRegularExitBlocks(exit => {
            if (!optLocalIsLiveIntoBlock(lclNum, exit))
            {
                return Continue;
            }

            var narrowing = gtNewCastNode(TYP_INT, gtNewLclvNode(TYP_LONG, newLclNum), false, TYP_INT);
            var store = gtNewStoreLclVarNode(lclNum, narrowing);
            var statement = fgNewStmtFromTree(store);
            JITDUMP($"Narrow IV local V{lclNum:D2} live into exit block " +
                $"{FMT_BB(exit.bbNum)}; sinking a narrowing\n");
            DISPSTMT(statement);
            fgInsertStmtAtBeg(exit, statement);
            return Continue;
        });
    }

    private struct ReplaceWidenedIVVisitor(Compiler compiler, int lclNum, int ssaNum, int newLclNum)
        : IGenTreeVisitor<ReplaceWidenedIVVisitor>
    {
        private readonly GenTreeStack _ancestors = [];
        public static bool DoPreOrder => true;
        public bool MadeChanges { get; private set; }

        private readonly bool IsLocal(GenTreeLclVarCommon local)
            => (local.LclNum == lclNum) &&
                ((ssaNum == SsaConfig.RESERVED_SSA_NUM) || (local.SsaNum == ssaNum));

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use.Oper is GT_CAST)
            {
                var cast = use.AsCast();
                if ((cast.CastType is TYP_LONG) && cast.IsUnsigned && !cast.HasOverflowCheck &&
                    (cast.CastOp.Oper is GT_LCL_VAR) && IsLocal(cast.CastOp.AsLclVarCommon()))
                {
                    use = compiler.gtNewLclvNode(TYP_LONG, newLclNum);
                    MadeChanges = true;
                    return WALK_SKIP_SUBTREES;
                }
            }
            else if ((use.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR or GT_LCL_FLD or GT_STORE_LCL_FLD) &&
                IsLocal(use.AsLclVarCommon()))
            {
                var local = use.AsLclVarCommon();
                switch (use.Oper)
                {
                    case GT_LCL_VAR:
                    {
                        local.LclNum = newLclNum;
                        break;
                    }
                    case GT_STORE_LCL_VAR:
                    {
                        local.LclNum = newLclNum;
                        local.Type = TYP_LONG;
                        local.DataRef = compiler.gtNewCastNode(TYP_LONG, local.Data, true, TYP_LONG);
                        break;
                    }
                    case GT_LCL_FLD:
                    case GT_STORE_LCL_FLD:
                    {
                        assert(false, "Unexpected field use for local not marked DNER");
                        break;
                    }
                }
                MadeChanges = true;
            }
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<ReplaceWidenedIVVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private void optReplaceWidenedIV(int lclNum, int ssaNum, int newLclNum, Statement stmt)
    {
        var visitor = new ReplaceWidenedIVVisitor(this, lclNum, ssaNum, newLclNum);
        _ = visitor.WalkTree(ref stmt.RootNodeRef, null);
        if (visitor.MadeChanges)
        {
            gtSetStmtInfo(stmt);
            fgSetStmtSeq(stmt);
            JITDUMP("New tree:\n");
            DISPTREE(stmt.RootNode);
            JITDUMP("\n");
        }
        else
        {
            JITDUMP("No replacements made\n");
        }
    }

    private void optBestEffortReplaceNarrowIVUses(int lclNum, int ssaNum, int newLclNum,
        BasicBlock block, Statement? firstStmt)
    {
#if DEBUG
        JITDUMP($"Replacing V{lclNum:D2} -> V{newLclNum:D2} in {FMT_BB(block.bbNum)} " +
            $"starting at {FMT_STMT(firstStmt?.Id ?? 0)}\n");
#endif
        for (var stmt = firstStmt; stmt is not null; stmt = stmt.NextStmt)
        {
#if DEBUG
            JITDUMP($"Replacing V{lclNum:D2} -> V{newLclNum:D2} in [{stmt.RootNode.TreeId:D6}]\n");
#endif
            DISPSTMT(stmt);
            JITDUMP("\n");
            optReplaceWidenedIV(lclNum, ssaNum, newLclNum, stmt);
        }
        _ = block.VisitRegularSuccs(this, successor => {
            if (successor.GetUniquePred(this) == block)
            {
                optBestEffortReplaceNarrowIVUses(lclNum, ssaNum, newLclNum,
                    successor, successor.FirstStmt);
            }
            return Continue;
        });
    }

    private bool optWidenIVs(ScalarEvolutionContext scevContext, FlowGraphNaturalLoop loop, PerLoopInfo loopInfo)
    {
        JITDUMP($"Considering primary IVs of L{loop.Index:D2} for widening\n");
        var widened = 0;
        for (var stmt = loop.Header.FirstStmt; (stmt is not null) && stmt.IsPhiDefnStmt; stmt = stmt.NextStmt)
        {
            JITDUMP("\n");
            DISPSTMT(stmt);
            var scev = scevContext.Analyze(loop.Header, stmt.RootNode.AsLclVarCommon().Data);
            if (scev is null)
            {
                JITDUMP("  Could not analyze header PHI\n");
                continue;
            }
#if DEBUG
            JITDUMP("  => ");
            if (verbose)
            {
                scev.Dump(this);
            }
            JITDUMP("\n");
#endif
            if (scev is not ScevAddRec addRec)
            {
                JITDUMP("  Not an addrec\n");
                continue;
            }

            var lclNum = stmt.RootNode.AsLclVarCommon().LclNum;
            ref var descriptor = ref lvaGetDesc(lclNum);
            JITDUMP($"  V{lclNum:D2} is a primary induction variable in L{loop.Index:D2}\n");
            assert(!descriptor.lvPromoted);
            if (descriptor.lvIsStructField && loopInfo.HasAnyOccurrences(loop, descriptor.lvParentLcl))
            {
                JITDUMP($"  V{lclNum:D2} is a struct field whose parent local " +
                    $"V{descriptor.lvParentLcl:D2} has occurrences inside the loop\n");
                continue;
            }
            if (optWidenPrimaryIV(loop, lclNum, addRec, loopInfo))
            {
                widened++;
            }
        }
        Metrics.WidenedIVs += widened;
        return widened > 0;
    }

    private bool optWidenPrimaryIV(FlowGraphNaturalLoop loop, int lclNum, ScevAddRec addRec,
        PerLoopInfo loopInfo)
    {
        ref var descriptor = ref lvaGetDesc(lclNum);
        if (descriptor.Type is not TYP_INT)
        {
            JITDUMP($"  Type is {descriptor.Type.Name}, no widening to be done\n");
            return false;
        }
        if (descriptor.lvDoNotEnregister || descriptor.IsLiveInOutOfHandler)
        {
            JITDUMP($"  V{lclNum:D2} is marked DNER or lives into a handler\n");
            return false;
        }
        if (!optCanSinkWidenedIV(lclNum, loop))
        {
            return false;
        }

        assert(addRec.Start is ScevLocal);
        var startLocal = (ScevLocal)addRec.Start;
        var initToConstant = startLocal.GetConstantValue(this, out var startConstant);
        var startSsa = descriptor.GetPerSsaData(startLocal.SsaNum);
        var preheader = loop.EntryEdge(0).SourceBlock;
        var initBlock = preheader;
        if ((startSsa.Block is not null) && (startSsa.DefNode is not null) &&
            !startSsa.DefNode.IsPhiDefn)
        {
            initBlock = startSsa.Block;
        }
        if (!optIsIVWideningProfitable(lclNum, initBlock, initToConstant, loop, loopInfo))
        {
            return false;
        }

        Statement? insertInitAfter = null;
        if (initBlock != preheader)
        {
            var narrowInitRoot = startSsa.DefNode
                ?? throw new FatalJitException("A non-preheader IV initializer needs its SSA definition.");
            for (var stmt = initBlock.FirstStmt; stmt is not null; stmt = stmt.NextStmt)
            {
                if (gtFindNodeInTree(stmt.RootNode, tree => tree == narrowInitRoot, GTF_EMPTY) is not null)
                {
                    insertInitAfter = stmt;
                    break;
                }
            }
            assert(insertInitAfter is not null);
            if (insertInitAfter is not null && insertInitAfter.IsPhiDefnStmt)
            {
                while ((insertInitAfter.NextStmt is not null) && insertInitAfter.NextStmt.IsPhiDefnStmt)
                {
                    insertInitAfter = insertInitAfter.NextStmt;
                }
            }
        }

        var newLclNum = lvaGrabTemp(false, $"Widened IV V{lclNum:D2}");
        assert(startLocal.LclNum == lclNum);
        if (initBlock != preheader)
        {
            JITDUMP($"Adding initialization of new widened local to same block as " +
                $"reaching def outside loop, {FMT_BB(initBlock.bbNum)}\n");
        }
        else
        {
            JITDUMP($"Adding initialization of new widened local to preheader {FMT_BB(initBlock.bbNum)}\n");
        }

        GenTree initValue = initToConstant
            ? gtNewLconNode(unchecked((uint)startConstant))
            : gtNewCastNode(TYP_LONG, gtNewLclvNode(TYP_INT, lclNum), true, TYP_LONG);
        var initStmt = fgNewStmtFromTree(gtNewTempStore(newLclNum, initValue));
        if (insertInitAfter is not null)
        {
            fgInsertStmtAfter(initBlock, insertInitAfter, initStmt);
        }
        else
        {
            fgInsertStmtNearEnd(initBlock, initStmt);
        }
        DISPSTMT(initStmt);
        JITDUMP("\n");
        JITDUMP($"  Replacing uses of V{lclNum:D2} with widened version V{newLclNum:D2}\n");
        JITDUMP("    Replacing on the way to the loop\n");
        optBestEffortReplaceNarrowIVUses(lclNum, startLocal.SsaNum, newLclNum,
            initBlock, initStmt.NextStmt);
        JITDUMP("    Replacing inside the loop\n");
        _ = loopInfo.VisitStatementsWithOccurrences(loop, lclNum, (_, stmt) => {
#if DEBUG
            JITDUMP($"Replacing V{lclNum:D2} -> V{newLclNum:D2} in [{stmt.RootNode.TreeId:D6}]\n");
#endif
            DISPSTMT(stmt);
            JITDUMP("\n");
            optReplaceWidenedIV(lclNum, SsaConfig.RESERVED_SSA_NUM, newLclNum, stmt);
            return true;
        });

        optSinkWidenedIV(lclNum, newLclNum, loop);
        loopInfo.Invalidate(loop);
        return true;
    }
}
