// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private VARSET_TP _exceptVars = [];
    private VARSET_TP _finallyVars = [];

    private void identifyCandidatesMinimal()
    {
        if (_compiler.lvaCount == 0)
        {
            return;
        }

        VarSetOps.AssignNoCopy(_compiler, ref _exceptVars, VarSetOps.MakeEmpty(_compiler));
        VarSetOps.AssignNoCopy(_compiler, ref _finallyVars, VarSetOps.MakeEmpty(_compiler));
        if (_compiler.compHndBBtabCount > 0)
        {
            identifyCandidatesExceptionDataflow();
        }

        localVarIntervals = null;
        for (var localNumber = 0; localNumber < _compiler.lvaCount; localNumber++)
        {
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            local.RegNum = REG_STK;
#if !TARGET_64BIT
            local.OtherReg = REG_STK;
#endif
            local.lvLRACandidate = false;
        }

#if DEBUG
        if (VERBOSE)
        {
            jitprintf("\nFP callee save candidate vars: None\n\n");
            var singleExit = (_compiler.fgReturnBlocks is null) || (_compiler.fgReturnBlocks.Next is null);
            jitprintf($"floatVarCount = 0; hasLoops = {dspBool(_compiler.fgHasLoops)}, singleExit = {dspBool(singleExit)}\n");
        }
#endif
    }

    private void identifyCandidatesExceptionDataflow()
    {
        foreach (var block in _compiler.Blocks)
        {
            if (block.hasEHBoundaryIn)
            {
                VarSetOps.UnionD(_compiler, _exceptVars, block.bbLiveIn);
            }

            if (block.hasEHBoundaryOut)
            {
                VarSetOps.UnionD(_compiler, _exceptVars, block.bbLiveOut);
                if (block.Kind is BBJ_EHFINALLYRET)
                {
                    VarSetOps.UnionD(_compiler, _finallyVars, block.bbLiveOut);
                }
            }
        }

#if DEBUG
        if (VERBOSE)
        {
            JITDUMP("EH Vars: ");
            dumpCandidateVarSet(_exceptVars);
            JITDUMP("\nFinally Vars: ");
            dumpCandidateVarSet(_finallyVars);
            JITDUMP("\n\n");
        }

        for (var localNumber = 0; localNumber < _compiler.lvaCount; localNumber++)
        {
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            if (!local.lvTracked || !VarSetOps.IsMember(_compiler, _exceptVars, local._varIndex))
            {
                continue;
            }

            assert(local.IsLiveInOutOfHandler);
            if (varTypeIsGC(local.Type) &&
                VarSetOps.IsMember(_compiler, _finallyVars, local._varIndex) &&
                !local.lvIsParam &&
                !local.lvIsParamRegTarget)
            {
                assert(local.lvMustInit);
            }
        }
#endif
    }

#if DEBUG
    private void dumpCandidateVarSet(VARSET_TP variables)
    {
        jitprintf("{");
        var first = true;
        for (var localNumber = 0; localNumber < _compiler.lvaCount; localNumber++)
        {
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            if (!local.lvTracked || !VarSetOps.IsMember(_compiler, variables, local._varIndex))
            {
                continue;
            }

            if (!first)
            {
                jitprintf(" ");
            }

            jitprintf($"V{localNumber:D2}");
            first = false;
        }

        jitprintf("}");
    }
#endif
}
