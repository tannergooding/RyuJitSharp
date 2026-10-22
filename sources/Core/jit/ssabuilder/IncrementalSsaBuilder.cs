// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public sealed class IncrementalSsaBuilder
{
    private readonly Compiler _compiler;
    private readonly int _lclNum;
    private readonly List<UseDefLocation> _defs = [];
    private readonly IncrementalLiveInBuilder _liveInBuilder;
    private BitVecTraits? _poTraits;
    private BitVec? _defBlocks;
    private BitVec? _iteratedDominanceFrontiers;

#if DEBUG
    private bool _finalizedDefs;
#endif

    public IncrementalSsaBuilder(Compiler compiler, int lclNum)
    {
        _compiler = compiler;
        _lclNum = lclNum;
        _liveInBuilder = new IncrementalLiveInBuilder(compiler);
    }

    private UseDefLocation FindOrCreateReachingDef(UseDefLocation use)
    {
        if (_poTraits is not BitVecTraits traits)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionBlocksAreNotFinalized);
            return default;
        }

        if (_defBlocks is not { } defBlocks)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionBlocksAreNotFinalized);
            return default;
        }

        if (_iteratedDominanceFrontiers is not { } frontiers)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAFrontiersAreNotFinalized);
            return default;
        }

        for (var dom = use.Block; dom is not null; dom = dom.bbIDom)
        {
            if (BitVecOps.IsMember(traits, defBlocks, dom.bbPostorderNum) &&
                FindReachingDefInBlock(use, dom, out var reachingDef))
            {
                return reachingDef;
            }

            if (!BitVecOps.IsMember(traits, frontiers, dom.bbPostorderNum))
            {
                continue;
            }

            var phiDef = SsaBuilder.GetIncrementalPhiNode(dom, _lclNum);
            if (phiDef is null)
            {
                phiDef = SsaBuilder.InsertIncrementalPhi(_compiler, dom, _lclNum);

                ref var descriptor = ref _compiler.lvaGetDesc(_lclNum);
                var phiTree = phiDef.RootNode.AsLclVar();
                var ssaNum = descriptor.lvPerSsaData.AllocSsaNum();
                descriptor.GetPerSsaData(ssaNum) = new LclSsaVarDsc(dom, phiTree);
                phiTree.SsaNum = ssaNum;

                var phi = phiTree.Data.AsPhi();
                var marked = _compiler.AddInsertedSsaLiveIn(dom, _lclNum);
                assert(marked);

                if (_compiler._dfsTree is not FlowGraphDfsTree dfsTree)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADFSTreeIsNotAvailable);
                    return default;
                }

                for (var edge = _compiler.BlockPredsWithEH(dom); edge is not null; edge = edge.NextPredEdge)
                {
                    var pred = edge.SourceBlock;
                    if (!dfsTree.Contains(pred))
                    {
                        continue;
                    }

                    var phiArgUse = new UseDefLocation(pred, null, null);
                    var phiArgReachingDef = FindOrCreateReachingDef(phiArgUse);
                    if (phiArgReachingDef.Tree is not GenTreeLclVar phiArgTree)
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.PhiArgumentHasNoReachingDefinition);
                        return default;
                    }

                    SsaBuilder.AddIncrementalPhiArg(_compiler, dom, phiDef, phi, _lclNum,
                        phiArgTree.SsaNum, pred);
                    _liveInBuilder.MarkLiveInBackwards(_lclNum, phiArgUse, phiArgReachingDef);
                }

                _compiler.fgValueNumberPhiDef(phiTree, dom);
                JITDUMP("  New phi def:\n");
                DISPSTMT(phiDef);
            }

            return new UseDefLocation(dom, phiDef, phiDef.RootNode.AsLclVar());
        }

        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAUseHasNoReachingDefinition);
        return default;
    }

    private bool FindReachingDefInBlock(UseDefLocation use, BasicBlock block, out UseDefLocation def)
    {
        Statement? latestDefStmt = null;
        GenTreeLclVar? latestTree = null;

        foreach (var candidate in _defs)
        {
            if (candidate.Block != block)
            {
                continue;
            }

            if (candidate.Stmt == use.Stmt)
            {
                if (FindReachingDefInSameStatement(use, out def))
                {
                    return true;
                }

                continue;
            }

            if ((candidate.Block == use.Block) && (use.Stmt is Statement useStatement))
            {
                if (candidate.Stmt is not Statement candidateStatement)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionHasNoStatement);
                    def = default;
                    return false;
                }

                if (LatestStatement(useStatement, candidateStatement) != useStatement)
                {
                    continue;
                }
            }

            if (candidate.Stmt == latestDefStmt)
            {
                latestTree = null;
            }
            else
            {
                if (latestDefStmt is null)
                {
                    latestDefStmt = candidate.Stmt;
                    latestTree = candidate.Tree;
                }
                else
                {
                    if (candidate.Stmt is not Statement candidateStatement)
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionHasNoStatement);
                        def = default;
                        return false;
                    }

                    if (LatestStatement(candidateStatement, latestDefStmt) == candidateStatement)
                    {
                        latestDefStmt = candidateStatement;
                        latestTree = candidate.Tree;
                    }
                }
            }
        }

        if (latestDefStmt is null)
        {
            def = default;
            return false;
        }

        if (latestTree is null)
        {
            foreach (var tree in latestDefStmt.TreeList)
            {
                if ((tree.Oper is GT_STORE_LCL_VAR) && (tree.AsLclVar().LclNum == _lclNum))
                {
                    latestTree = tree.AsLclVar();
                }
            }

            assert(latestTree is not null);
        }

        if (latestTree is not GenTreeLclVar definitionTree)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAStatementHasNoMatchingDefinition);
            def = default;
            return false;
        }

        def = new UseDefLocation(use.Block, latestDefStmt, definitionTree);
        return true;
    }

    private bool FindReachingDefInSameStatement(UseDefLocation use, out UseDefLocation def)
    {
        global::RyuJitSharp.GenTreeLclVar __jitExceptionResult693;
        var __jitExceptionCoalesceLeft694 = use.Tree;
        if (__jitExceptionCoalesceLeft694 != null)
        {
            __jitExceptionResult693 = __jitExceptionCoalesceLeft694;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAStatementUseHasNoTree);
            __jitExceptionResult693 = default;
        }
        var useTree = __jitExceptionResult693;
        for (var tree = useTree.Prev; tree is not null; tree = tree.Prev)
        {
            if ((tree.Oper is GT_STORE_LCL_VAR) && (tree.AsLclVar().LclNum == _lclNum))
            {
                def = new UseDefLocation(use.Block, use.Stmt, tree.AsLclVar());
                return true;
            }
        }

        def = default;
        return false;
    }

    private static Statement LatestStatement(Statement first, Statement second)
    {
        if (first == second)
        {
            return first;
        }

        var firstCursor = first.NextStmt;
        var secondCursor = second.NextStmt;
        while (true)
        {
            if ((firstCursor == second) || (secondCursor is null))
            {
                return second;
            }

            if ((secondCursor == first) || (firstCursor is null))
            {
                return first;
            }

            firstCursor = firstCursor.NextStmt;
            secondCursor = secondCursor.NextStmt;
        }
    }

    public void InsertDef(UseDefLocation def)
    {
#if DEBUG
        assert(!_finalizedDefs);
#endif
        _defs.Add(def);
    }

    public bool FinalizeDefs()
    {
#if DEBUG
        assert(!_finalizedDefs);
#endif

        if (Configuration.DEBUG && _compiler.verbose)
        {
            jitprintf($"Finalizing defs for SSA insertion of V{_lclNum:D2}\n");
            jitprintf($"  {_defs.Count} defs:");
            foreach (var def in _defs)
            {
                global::RyuJitSharp.GenTreeLclVar __jitExceptionResult695;
                var __jitExceptionCoalesceLeft696 = def.Tree;
                if (__jitExceptionCoalesceLeft696 != null)
                {
                    __jitExceptionResult695 = __jitExceptionCoalesceLeft696;
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionHasNoTree);
                    __jitExceptionResult695 = default;
                }
                var tree = __jitExceptionResult695;
                jitprintf($" [{tree.TreeId:D6}]");
            }
            jitprintf("\n");
        }

        ref var descriptor = ref _compiler.lvaGetDesc(_lclNum);
        if (_defs.Count == 1)
        {
            JITDUMP("  Single-def local; putting into SSA directly\n");
            var def = _defs[0];
            global::RyuJitSharp.GenTreeLclVar __jitExceptionResult697;
            var __jitExceptionCoalesceLeft698 = def.Tree;
            if (__jitExceptionCoalesceLeft698 != null)
            {
                __jitExceptionResult697 = __jitExceptionCoalesceLeft698;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionHasNoTree);
                __jitExceptionResult697 = default;
            }
            var tree = __jitExceptionResult697;
            var ssaNum = descriptor.lvPerSsaData.AllocSsaNum();
            descriptor.GetPerSsaData(ssaNum) = new LclSsaVarDsc(def.Block, tree);
            tree.SsaNum = ssaNum;
            JITDUMP($"  [{tree.TreeId:D6}] d:{ssaNum}\n");
            descriptor.lvInSsa = true;
            global::RyuJitSharp.ValueNumStore __jitExceptionResult149;
            var __jitExceptionCoalesceLeft150 = _compiler.vnStore;
            if (__jitExceptionCoalesceLeft150 != null)
            {
                __jitExceptionResult149 = __jitExceptionCoalesceLeft150;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAInsertionRequiresValueNumbering);
                __jitExceptionResult149 = default;
            }
            var vnStore = __jitExceptionResult149;
            descriptor.GetPerSsaData(ssaNum)._vnPair = vnStore.VNPNormalPair(tree.Data._vnPair);
#if DEBUG
            _finalizedDefs = true;
#endif
            return true;
        }

        _compiler._dfsTree ??= _compiler.fgComputeDfs();
        _compiler._domTree ??= FlowGraphDominatorTree.Build(_compiler._dfsTree);
        _compiler._domFrontiers ??= FlowGraphDominanceFrontiers.Build(_compiler._domTree);

        var dfsTree = _compiler._dfsTree;
        var frontiers = _compiler._domFrontiers;
        _poTraits = dfsTree.PostOrderTraits();
        _defBlocks = BitVecOps.MakeEmpty(_poTraits);
        _iteratedDominanceFrontiers = BitVecOps.MakeEmpty(_poTraits);
        List<BasicBlock> idf = [];

        foreach (var def in _defs)
        {
            idf.Clear();
            frontiers.ComputeIteratedDominanceFrontier(def.Block, idf);
            foreach (var block in idf)
            {
                BitVecOps.AddElemD(_poTraits, _iteratedDominanceFrontiers, block.bbPostorderNum);
            }
        }

        if (BitVecOps.Count(_poTraits, _iteratedDominanceFrontiers) > 100)
        {
            return false;
        }

        global::RyuJitSharp.ValueNumStore __jitExceptionResult151;
        var __jitExceptionCoalesceLeft152 = _compiler.vnStore;
        if (__jitExceptionCoalesceLeft152 != null)
        {
            __jitExceptionResult151 = __jitExceptionCoalesceLeft152;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAInsertionRequiresValueNumbering);
            __jitExceptionResult151 = default;
        }
        var valueNumbers = __jitExceptionResult151;
        foreach (var def in _defs)
        {
            if (dfsTree.Contains(def.Block))
            {
                BitVecOps.AddElemD(_poTraits, _defBlocks, def.Block.bbPostorderNum);
            }

            global::RyuJitSharp.GenTreeLclVar __jitExceptionResult699;
            var __jitExceptionCoalesceLeft700 = def.Tree;
            if (__jitExceptionCoalesceLeft700 != null)
            {
                __jitExceptionResult699 = __jitExceptionCoalesceLeft700;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionHasNoTree);
                __jitExceptionResult699 = default;
            }
            var tree = __jitExceptionResult699;
            var ssaNum = descriptor.lvPerSsaData.AllocSsaNum();
            ref var ssaDesc = ref descriptor.GetPerSsaData(ssaNum);
            ssaDesc = new LclSsaVarDsc(def.Block, tree);
            tree.SsaNum = ssaNum;
            ssaDesc._vnPair = valueNumbers.VNPNormalPair(tree.Data._vnPair);
            JITDUMP($"  [{tree.TreeId:D6}] d:{ssaNum}\n");
        }

        descriptor.lvInSsa = true;
#if DEBUG
        _finalizedDefs = true;
#endif
        return true;
    }

    public void InsertUse(UseDefLocation use)
    {
#if DEBUG
        assert(_finalizedDefs);
#endif
        global::RyuJitSharp.GenTreeLclVar __jitExceptionResult701;
        var __jitExceptionCoalesceLeft702 = use.Tree;
        if (__jitExceptionCoalesceLeft702 != null)
        {
            __jitExceptionResult701 = __jitExceptionCoalesceLeft702;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAUseHasNoTree);
            __jitExceptionResult701 = default;
        }
        var useTree = __jitExceptionResult701;
        JITDUMP($"Inserting use [{useTree.TreeId:D6}] into SSA\n");

        UseDefLocation reachingDef;
        if (_defs.Count == 1)
        {
            reachingDef = _defs[0];
        }
        else
        {
            global::RyuJitSharp.FlowGraphDfsTree __jitExceptionResult153;
            var __jitExceptionCoalesceLeft154 = _compiler._dfsTree;
            if (__jitExceptionCoalesceLeft154 != null)
            {
                __jitExceptionResult153 = __jitExceptionCoalesceLeft154;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADFSTreeIsNotAvailable);
                __jitExceptionResult153 = default;
            }
            var dfsTree = __jitExceptionResult153;
            if (!dfsTree.Contains(use.Block))
            {
                reachingDef = _defs[0];
                if (Configuration.DEBUG)
                {
                    global::RyuJitSharp.GenTreeLclVar __jitExceptionResult155;
                    var __jitExceptionCoalesceLeft156 = reachingDef.Tree;
                    if (__jitExceptionCoalesceLeft156 != null)
                    {
                        __jitExceptionResult155 = __jitExceptionCoalesceLeft156;
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionHasNoTree);
                        __jitExceptionResult155 = default;
                    }
                    var unreachableDefTree = __jitExceptionResult155;
                    JITDUMP($"  Use is in unreachable block {FMT_BB(use.Block.bbNum)}, using first def " +
                        $"[{unreachableDefTree.TreeId:D6}] in {FMT_BB(reachingDef.Block.bbNum)}\n");
                }
            }
            else
            {
                reachingDef = FindOrCreateReachingDef(use);
            }
        }

        global::RyuJitSharp.GenTreeLclVar __jitExceptionResult157;
        var __jitExceptionCoalesceLeft158 = reachingDef.Tree;
        if (__jitExceptionCoalesceLeft158 != null)
        {
            __jitExceptionResult157 = __jitExceptionCoalesceLeft158;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAUseHasNoReachingDefinition);
            __jitExceptionResult157 = default;
        }
        var reachingTree = __jitExceptionResult157;
        JITDUMP($"  Reaching def is [{reachingTree.TreeId:D6}] d:{reachingTree.SsaNum}\n");
        useTree.SsaNum = reachingTree.SsaNum;
        _liveInBuilder.MarkLiveInBackwards(_lclNum, use, reachingDef);
        _compiler.lvaGetDesc(_lclNum).GetPerSsaData(reachingTree.SsaNum).AddUse(use.Block);
    }
}
