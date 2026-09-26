// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    private static void RequireSsaPolicy()
    {
        if (!TLiveness.SsaLiveness || !TLiveness.ComputeMemoryLiveness ||
            TLiveness.IsEarly || TLiveness.IsLIR || !TLiveness.EliminateDeadCode ||
            TLiveness.TrackAddressExposedLocals)
        {
            throw new NotSupportedException("SSA tree liveness requires the non-LIR SSA policy.");
        }
    }

    public void RunSsa()
    {
        RequireSsaPolicy();
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf("*************** In Liveness::Run()\n");
        }
#endif
        Init();
        _compiler.EndPhase(PHASE_LCLVARLIVENESS_INIT);

        do
        {
            PerBlockLocalVarLiveness();
            _compiler.EndPhase(PHASE_LCLVARLIVENESS_PERBLOCK);
            InterBlockLocalVarLivenessSsa();
        }
        while (_compiler.fgStmtRemoved && _livenessChanged);

        _compiler.EndPhase(PHASE_LCLVARLIVENESS_INTERBLOCK);
    }

    internal void InterBlockLocalVarLivenessSsa()
    {
        RequireSsaPolicy();
        JITDUMP("*************** IngInterBlockLocalVarLiveness()\n");

        _compiler.fgStmtRemoved = false;
        _livenessChanged = false;
        DoLiveVarAnalysis();

        var exceptVars = VarSetOps.MakeEmpty(_compiler);
        var finallyVars = VarSetOps.MakeEmpty(_compiler);
        foreach (var block in _compiler.Blocks)
        {
            if (block.hasEHBoundaryIn)
            {
                VarSetOps.UnionD(_compiler, exceptVars, block.bbLiveIn);
            }
            if (block.hasEHBoundaryOut)
            {
                VarSetOps.UnionD(_compiler, exceptVars, block.bbLiveOut);
                if (block.Kind is BBJ_EHFINALLYRET)
                {
                    VarSetOps.UnionD(_compiler, finallyVars, block.bbLiveOut);
                }
            }
        }

        MarkMustInitAndEHVars(finallyVars, exceptVars);

        var keepAliveVars = VarSetOps.MakeEmpty(_compiler);
        var dfs = _compiler._dfsTree;
        assert(dfs is not null);
        for (var index = dfs.PostOrderCount; index != 0; index--)
        {
            var block = dfs.GetPostOrder(index - 1);
            _compiler.compCurBB = block;
            VarSetOps.ClearD(_compiler, keepAliveVars);
            if (block.HasPotentialEHSuccs(_compiler))
            {
                var memoryLiveness = 0;
                _compiler.fgAddHandlerLiveVars(block, keepAliveVars, ref memoryLiveness);
                noway_assert(VarSetOps.IsSubset(_compiler, keepAliveVars, exceptVars));
            }

            var life = VarSetOps.MakeCopy(_compiler, block.bbLiveOut);
            assert(_compiler.fgNodeThreading is NodeThreading.AllTrees);
            var firstStatement = block.GetFirstNonPhiDef();
            if (firstStatement is null)
            {
                continue;
            }

            var statement = block.LastStmt;
            while (true)
            {
                assert(statement is not null);
                _compiler.compCurStmt = statement;
                var previous = statement.PrevStmt;
#if DEBUG
                var treeModified = false;
#endif
                var statementInfoDirty = ComputeLifeSsa(life, keepAliveVars
#if DEBUG
                    , ref treeModified
#endif
                );
                if (statementInfoDirty)
                {
                    _compiler.gtSetStmtInfo(statement);
                    _compiler.fgSetStmtSeq(statement);
                    _compiler.gtUpdateStmtSideEffects(statement);
                }
#if DEBUG
                if (_compiler.verbose && treeModified)
                {
                    jitprintf("\nfgComputeLife modified tree:\n");
                    _compiler.gtDispTree(statement.RootNode);
                    jitprintf("\n");
                }
#endif
                if (statement == firstStatement)
                {
                    break;
                }
                statement = previous;
            }

            if (!VarSetOps.Equal(_compiler, life, block.bbLiveIn))
            {
                _livenessChanged = true;
                noway_assert(VarSetOps.IsSubset(_compiler, life, block.bbLiveIn));
                VarSetOps.Assign(_compiler, ref block.bbLiveIn, life);
            }

            noway_assert(_compiler.compCurBB == block);
#if DEBUG
            _compiler.compCurBB = null;
#endif
        }

        _compiler.fgLocalVarLivenessDone = true;
    }
}
