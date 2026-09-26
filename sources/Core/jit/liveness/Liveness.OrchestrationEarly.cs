// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    private void RequireEarlyPolicy()
    {
        if (!TLiveness.IsEarly || TLiveness.IsLIR || TLiveness.SsaLiveness ||
            TLiveness.ComputeMemoryLiveness || TLiveness.TrackAddressExposedLocals ||
            !TLiveness.EliminateDeadCode)
        {
            throw new NotSupportedException("Early tree liveness requires the non-SSA early policy.");
        }
    }

    public void RunEarly()
    {
        RequireEarlyPolicy();
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
            InterBlockLocalVarLivenessEarly();
        }
        while (_compiler.fgStmtRemoved && _livenessChanged);

        _compiler.EndPhase(PHASE_LCLVARLIVENESS_INTERBLOCK);
    }

    internal void InterBlockLocalVarLivenessEarly()
    {
        RequireEarlyPolicy();
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
            assert(_compiler.fgNodeThreading is NodeThreading.AllLocals);
            _compiler.compCurStmt = null;

            var firstStatement = block.FirstStmt;
            if (firstStatement is null)
            {
                continue;
            }

            var statement = block.LastStmt;
            while (true)
            {
                assert(statement is not null);
                var previous = statement.PrevStmt;
                GenTree? destination = null;
                var qmark = _compiler.compQmarkUsed
                    ? _compiler.fgGetTopLevelQmark(statement.RootNode, out destination)
                    : null;

                for (var current = statement.TreeListEnd; current is not null;)
                {
                    assert(current.Oper.IsAnyLocal);
                    // Conditional definitions must not kill liveness from another qmark branch.
                    var isConditionalDef = qmark is not null &&
                        ((current.Flags & GTF_VAR_DEF) != 0) && (current != destination);
                    if (isConditionalDef || !ComputeLifeLocal(life, keepAliveVars, current))
                    {
                        current = current.Prev;
                        continue;
                    }

                    assert(qmark is null || current == destination);
                    current = TryRemoveDeadStoreEarly(statement, current.AsLclVarCommon());
                }

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

    private GenTree? TryRemoveDeadStoreEarly(Statement statement, GenTreeLclVarCommon current)
    {
        if (!statement.RootNode.Oper.IsLocalStore || (statement.RootNode != current))
        {
            return current.Prev;
        }

#if DEBUG
        JITDUMP($"Store [{statement.RootNode.TreeId:D6}] is dead");
#endif
        assert(statement.TreeListEnd == current);
        GenTree? sideEffects = null;
        _compiler.gtExtractSideEffList(current.Data, ref sideEffects);

        if (sideEffects is null)
        {
            JITDUMP(" and has no side effects, removing statement\n");
            assert(_compiler.compCurBB is not null);
            _compiler.fgRemoveStmt(_compiler.compCurBB, statement);
            return null;
        }

        JITDUMP(" but has side effects. Replacing with:\n\n");
        statement.RootNode = sideEffects;
        _compiler.fgSequenceLocals(statement);
        DISPTREE(sideEffects);
        JITDUMP("\n");
        return statement.TreeListEnd;
    }
}
