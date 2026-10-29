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

    private bool _finalizedDefs
    {
        get
        {
            return Configuration.DEBUG && Compiler.DebugData.Get(this).FinalizedDefs;
        }

        set
        {
            if (Configuration.DEBUG)
            {
                Compiler.DebugData.Get(this).FinalizedDefs = value;
            }
        }
    }

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
        var useTree = use.Tree;
        if (useTree is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAStatementUseHasNoTree);
        }
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
        if (Configuration.DEBUG)
        {
            assert(!_finalizedDefs);
        }

        _defs.Add(def);
    }

    public bool FinalizeDefs()
    {
        if (Configuration.DEBUG)
        {
            assert(!_finalizedDefs);
        }

        if (Configuration.DEBUG && _compiler.verbose)
        {
            jitprintf($"Finalizing defs for SSA insertion of V{_lclNum:D2}\n");
            jitprintf($"  {_defs.Count} defs:");
            foreach (var def in _defs)
            {
                var tree = def.Tree;
                if (tree is null)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionHasNoTree);
                }
                jitprintf($" [{tree.TreeId:D6}]");
            }
            jitprintf("\n");
        }

        ref var descriptor = ref _compiler.lvaGetDesc(_lclNum);
        if (_defs.Count == 1)
        {
            JITDUMP("  Single-def local; putting into SSA directly\n");
            var def = _defs[0];
            var tree = def.Tree;
            if (tree is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionHasNoTree);
            }
            var ssaNum = descriptor.lvPerSsaData.AllocSsaNum();
            descriptor.GetPerSsaData(ssaNum) = new LclSsaVarDsc(def.Block, tree);
            tree.SsaNum = ssaNum;
            JITDUMP($"  [{tree.TreeId:D6}] d:{ssaNum}\n");
            descriptor.lvInSsa = true;
            var vnStore = _compiler.vnStore;
            if (vnStore is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAInsertionRequiresValueNumbering);
            }
            descriptor.GetPerSsaData(ssaNum)._vnPair = vnStore.VNPNormalPair(tree.Data._vnPair);
            if (Configuration.DEBUG)
            {
                _finalizedDefs = true;
            }
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

        var valueNumbers = _compiler.vnStore;
        if (valueNumbers is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAInsertionRequiresValueNumbering);
        }
        foreach (var def in _defs)
        {
            if (dfsTree.Contains(def.Block))
            {
                BitVecOps.AddElemD(_poTraits, _defBlocks, def.Block.bbPostorderNum);
            }

            var tree = def.Tree;
            if (tree is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionHasNoTree);
            }
            var ssaNum = descriptor.lvPerSsaData.AllocSsaNum();
            ref var ssaDesc = ref descriptor.GetPerSsaData(ssaNum);
            ssaDesc = new LclSsaVarDsc(def.Block, tree);
            tree.SsaNum = ssaNum;
            ssaDesc._vnPair = valueNumbers.VNPNormalPair(tree.Data._vnPair);
            JITDUMP($"  [{tree.TreeId:D6}] d:{ssaNum}\n");
        }

        descriptor.lvInSsa = true;
        if (Configuration.DEBUG)
        {
            _finalizedDefs = true;
        }
        return true;
    }

    public void InsertUse(UseDefLocation use)
    {
        if (Configuration.DEBUG)
        {
            assert(_finalizedDefs);
        }

        var useTree = use.Tree;
        if (useTree is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAUseHasNoTree);
        }
        JITDUMP($"Inserting use [{useTree.TreeId:D6}] into SSA\n");

        UseDefLocation reachingDef;
        if (_defs.Count == 1)
        {
            reachingDef = _defs[0];
        }
        else
        {
            var dfsTree = _compiler._dfsTree;
            if (dfsTree is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADFSTreeIsNotAvailable);
            }
            if (!dfsTree.Contains(use.Block))
            {
                reachingDef = _defs[0];
                if (Configuration.DEBUG)
                {
                    var unreachableDefTree = reachingDef.Tree;
                    if (unreachableDefTree is null)
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSADefinitionHasNoTree);
                    }
                    JITDUMP($"  Use is in unreachable block {FMT_BB(use.Block.bbNum)}, using first def " +
                        $"[{unreachableDefTree.TreeId:D6}] in {FMT_BB(reachingDef.Block.bbNum)}\n");
                }
            }
            else
            {
                reachingDef = FindOrCreateReachingDef(use);
            }
        }

        var reachingTree = reachingDef.Tree;
        if (reachingTree is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.SSAUseHasNoReachingDefinition);
        }
        JITDUMP($"  Reaching def is [{reachingTree.TreeId:D6}] d:{reachingTree.SsaNum}\n");
        useTree.SsaNum = reachingTree.SsaNum;
        _liveInBuilder.MarkLiveInBackwards(_lclNum, use, reachingDef);
        _compiler.lvaGetDesc(_lclNum).GetPerSsaData(reachingTree.SsaNum).AddUse(use.Block);
    }
}
