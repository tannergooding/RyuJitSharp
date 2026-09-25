// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    private bool _livenessChanged;

    // The LIR specialization of native Run. HIR/early orchestration still requires
    // its own backward walkers and rewriting; it must not enter this mode.
    public unsafe void RunLIR()
    {
        RequireLIRPolicy();
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf("*************** In Liveness::Run()\n");
            _compiler.lvaTableDump();
        }
#endif

        Init();
        _compiler.EndPhase(PHASE_LCLVARLIVENESS_INIT);
        do
        {
            PerBlockLocalVarLiveness();
            _compiler.EndPhase(PHASE_LCLVARLIVENESS_PERBLOCK);
            InterBlockLocalVarLivenessLIR();
        }
        while (_compiler.fgStmtRemoved && _livenessChanged);

        _compiler.EndPhase(PHASE_LCLVARLIVENESS_INTERBLOCK);
    }

    internal unsafe void InterBlockLocalVarLivenessLIR()
    {
        RequireLIRPolicy();
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
            ComputeLifeLIR(life, block, keepAliveVars);

            if (!VarSetOps.Equal(_compiler, life, block.bbLiveIn))
            {
                // A smaller live-in can expose dead stores in predecessors on
                // the next complete use/def and inter-block pass.
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

    private static void RequireLIRPolicy()
    {
        if (!TLiveness.IsLIR || TLiveness.IsEarly)
        {
            throw new NotSupportedException("LIR liveness orchestration requires a non-early LIR policy.");
        }
    }
}
