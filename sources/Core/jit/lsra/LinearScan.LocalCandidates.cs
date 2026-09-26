// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private VARSET_TP _resolutionCandidateVars = [];

    private void identifyCandidatesWithLocals()
    {
#if WINDOWS_AMD64_ABI
        assert(_enregisterLocalVars);
        assert(_compiler.lvaCount != 0);
        VarSetOps.AssignNoCopy(_compiler, ref _registerCandidateVars, VarSetOps.MakeEmpty(_compiler));
        VarSetOps.AssignNoCopy(_compiler, ref _resolutionCandidateVars, VarSetOps.MakeEmpty(_compiler));
        _splitOrSpilledVars = VarSetOps.MakeEmpty(_compiler);
        VarSetOps.AssignNoCopy(_compiler, ref _exceptVars, VarSetOps.MakeEmpty(_compiler));
        VarSetOps.AssignNoCopy(_compiler, ref _finallyVars, VarSetOps.MakeEmpty(_compiler));
        if (_compiler.compHndBBtabCount > 0)
        {
            identifyCandidatesExceptionDataflow();
        }

        // Native uses 4 weighted references for conservative FP preferencing and 2
        // for high-use, single-exit loops. Large vectors always use the higher threshold.
        var floatVarCount = 0;
        var thresholdFPRefCount = 4 * BB_UNITY_WEIGHT;
        var maybeFPRefCount = 2 * BB_UNITY_WEIGHT;
        VarSetOps.AssignNoCopy(_compiler, ref _fpCalleeSaveCandidateVars, VarSetOps.MakeEmpty(_compiler));
        var fpMaybeCandidateVars = VarSetOps.MakeEmpty(_compiler);
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        var thresholdLargeVectorRefCount = 4 * BB_UNITY_WEIGHT;
        VarSetOps.AssignNoCopy(_compiler, ref _largeVectorVars, VarSetOps.MakeEmpty(_compiler));
        VarSetOps.AssignNoCopy(_compiler, ref _largeVectorCalleeSaveCandidateVars, VarSetOps.MakeEmpty(_compiler));
#endif

        if (_compiler.lvaTrackedCount > 0)
        {
            localVarIntervals = new Interval?[_compiler.lvaTrackedCount];
        }

        for (var localNumber = 0; localNumber < _compiler.lvaCount; localNumber++)
        {
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            local.RegNum = REG_STK;
            local.lvLRACandidate = true;
            local.lvRegister = false;
            checkForDNER(localNumber, in local);

            if (!IsRegCandidate(in local))
            {
                local.lvLRACandidate = false;
                if (local.lvTracked)
                {
                    assert(localVarIntervals is not null);
                    localVarIntervals[local._varIndex] = null;
                }

                // A collectively referenced multi-register struct needs either every
                // promoted field or no promoted fields to be register candidates.
                if (local.lvIsStructField)
                {
                    ref var parent = ref _compiler.lvaGetDesc(local.lvParentLcl);
                    if (parent.lvIsMultiRegDest && !parent.lvDoNotEnregister)
                    {
                        JITDUMP($"Setting multi-reg-dest struct V{local.lvParentLcl:D2} as not enregisterable:");
                        _compiler.lvaSetVarDoNotEnregister(local.lvParentLcl, DoNotEnregisterReason.BlockOp);
                        for (var fieldIndex = 0; fieldIndex < parent.lvFieldCnt; fieldIndex++)
                        {
                            var fieldNumber = parent.lvFieldLclStart + fieldIndex;
                            ref var field = ref _compiler.lvaGetDesc(fieldNumber);
                            JITDUMP($" V{fieldNumber:D2}");
                            if (field.lvTracked)
                            {
                                field.lvLRACandidate = false;
                                assert(localVarIntervals is not null);
                                localVarIntervals[field._varIndex] = null;
                                VarSetOps.RemoveElemD(_compiler, _registerCandidateVars, field._varIndex);
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                                VarSetOps.RemoveElemD(_compiler, _largeVectorVars, field._varIndex);
#endif
                                JITDUMP("*");
                            }

                            // Native approximates the parent's count so it gets a stack home.
                            parent.setLvRefCnt(unchecked((ushort)(parent.lvRefCnt() + field.lvRefCnt())));
                        }
                        JITDUMP("\n");
                    }
                }
                continue;
            }

            if (local.lvLRACandidate)
            {
                var type = local.GetStackSlotHomeType();
                if (!varTypeUsesIntReg(type))
                {
                    _compiler.compFloatingPointUsed = true;
                }

                var interval = newInterval(type);
                interval.setLocalNumber(_compiler, checked((uint)localNumber), this);
                VarSetOps.AddElemD(_compiler, _registerCandidateVars, local._varIndex);
                local.lvMustInit = false;
                if (local.lvIsStructField)
                {
                    interval.isStructField = true;
                }
                if (local.IsLiveInOutOfHandler)
                {
                    interval.isWriteThru = local.lvSingleDefRegCandidate;
                    setIntervalAsSpilled(interval);
                }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                if (Compiler.varTypeNeedsPartialCalleeSave(local.GetRegisterType()))
                {
                    VarSetOps.AddElemD(_compiler, _largeVectorVars, local._varIndex);
                    if (local.lvRefCntWtd() >= thresholdLargeVectorRefCount)
                    {
                        VarSetOps.AddElemD(_compiler, _largeVectorCalleeSaveCandidateVars, local._varIndex);
                    }
                }
                else
#endif
                if (type.Register == VTR_FLOAT)
                {
                    floatVarCount++;
                    var refCount = local.lvRefCntWtd();
                    if (local.lvIsRegArg)
                    {
                        // A register argument needs an extra copy to use a callee-save.
                        refCount -= BB_UNITY_WEIGHT;
                    }
                    if (refCount >= thresholdFPRefCount)
                    {
                        VarSetOps.AddElemD(_compiler, _fpCalleeSaveCandidateVars, local._varIndex);
                    }
                    else if (refCount >= maybeFPRefCount)
                    {
                        VarSetOps.AddElemD(_compiler, fpMaybeCandidateVars, local._varIndex);
                    }
                }

                JITDUMP("  ");
#if DEBUG
                if (VERBOSE)
                {
                    dumpInterval(interval);
                }
#endif
            }
        }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        for (var varIndex = 0; varIndex < _compiler.lvaTrackedCount; varIndex++)
        {
            if (VarSetOps.IsMember(_compiler, _largeVectorVars, varIndex))
            {
                makeUpperVectorInterval(checked((uint)varIndex));
            }
        }
#endif

#if DEBUG
        if (VERBOSE)
        {
            jitprintf("\nFP callee save candidate vars: ");
            if (!VarSetOps.IsEmpty(_compiler, _fpCalleeSaveCandidateVars))
            {
                dumpCandidateVarSet(_fpCalleeSaveCandidateVars);
                jitprintf("\n");
            }
            else
            {
                jitprintf("None\n\n");
            }
        }
#endif
        var singleExit = (_compiler.fgReturnBlocks is null) || (_compiler.fgReturnBlocks.Next is null);
        JITDUMP($"floatVarCount = {floatVarCount}; hasLoops = {dspBool(_compiler.fgHasLoops)}, singleExit = {dspBool(singleExit)}\n");
        if ((floatVarCount > 6) && _compiler.fgHasLoops && singleExit)
        {
#if DEBUG
            if (VERBOSE)
            {
                jitprintf("Adding additional fp callee save candidates: \n");
                if (!VarSetOps.IsEmpty(_compiler, fpMaybeCandidateVars))
                {
                    dumpCandidateVarSet(fpMaybeCandidateVars);
                    jitprintf("\n");
                }
                else
                {
                    jitprintf("None\n\n");
                }
            }
#endif
            VarSetOps.UnionD(_compiler, _fpCalleeSaveCandidateVars, fpMaybeCandidateVars);
        }
        if (_compiler.compHndBBtabCount > 0)
        {
            VarSetOps.IntersectionD(_compiler, _exceptVars, _registerCandidateVars);
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED,
            "Local register-candidate construction outside Windows AMD64 is not implemented.");
#endif
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    private void makeUpperVectorInterval(uint varIndex)
    {
        var localInterval = getIntervalForLocalVar(varIndex);
        assert(Compiler.varTypeNeedsPartialCalleeSave(localInterval.registerType));
#if TARGET_AMD64
        var upperInterval = newInterval(TYP_SIMD16);
#elif TARGET_ARM64
        var upperInterval = newInterval(TYP_DOUBLE);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Upper-vector local intervals are unsupported on this target.");
#endif
#if TARGET_AMD64 || TARGET_ARM64
        upperInterval.relatedInterval = localInterval;
        upperInterval.isUpperVector = true;
#endif
    }
#endif
}
