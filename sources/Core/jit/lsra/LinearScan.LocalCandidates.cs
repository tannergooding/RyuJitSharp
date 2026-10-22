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
        var thresholdLargeVectorRefCount = 4 * BB_UNITY_WEIGHT;
        VarSetOps.AssignNoCopy(_compiler, ref _largeVectorVars, VarSetOps.MakeEmpty(_compiler));
        VarSetOps.AssignNoCopy(_compiler, ref _largeVectorCalleeSaveCandidateVars, VarSetOps.MakeEmpty(_compiler));

        uint refCntStk = 0;
        uint refCntReg = 0;
        double refCntWtdReg = 0;
        uint refCntStkParam = 0;
        double refCntWtdStkDbl = 0;
        _doDoubleAlign = false;
        var checkDoubleAlign = false;
        if (Configuration.DOUBLE_ALIGN)
        {
            checkDoubleAlign = true;
            var codeGen = _compiler.codeGen;
            assert(codeGen is not null);
            if (codeGen.IsFramePointerRequired || _compiler.opts.MinOpts)
            {
                checkDoubleAlign = false;
            }
            else
            {
                switch (getCanDoubleAlign())
                {
                    case CanDoubleAlign.MUST_DOUBLE_ALIGN:
                    {
                        _doDoubleAlign = true;
                        checkDoubleAlign = false;
                        break;
                    }
                    case CanDoubleAlign.CAN_DOUBLE_ALIGN:
                    {
                        break;
                    }
                    case CanDoubleAlign.CANT_DOUBLE_ALIGN:
                    {
                        _doDoubleAlign = false;
                        checkDoubleAlign = false;
                        break;
                    }
                    default:
                    {
                        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.UnrecognizedDoubleAlignmentPolicy);
                        return;
                    }
                }
            }
        }

        if (_compiler.lvaTrackedCount > 0)
        {
            localVarIntervals = new Interval?[_compiler.lvaTrackedCount];
        }

        for (var localNumber = 0; localNumber < _compiler.lvaCount; localNumber++)
        {
            ref var local = ref _compiler.lvaGetDesc(localNumber);
            local.RegNum = REG_STK;
            if (!Configuration.TARGET_64BIT)
            {
                local.OtherReg = REG_STK;
            }
            if (checkDoubleAlign)
            {
                if (local.lvIsParam && !local.lvIsRegArg)
                {
                    refCntStkParam += local.lvRefCnt();
                }
                else if (!IsRegCandidate(in local) || local.lvDoNotEnregister)
                {
                    refCntStk += local.lvRefCnt();
                    if ((local.Type is TYP_DOUBLE) ||
                        (varTypeIsStruct(local.Type) && local.lvStructDoubleAlign &&
                         (_compiler.lvaGetPromotionType(in local) is not Compiler.PROMOTION_TYPE_INDEPENDENT)))
                    {
                        refCntWtdStkDbl += local.lvRefCntWtd();
                    }
                }
                else
                {
                    refCntReg += local.lvRefCnt();
                    refCntWtdReg += local.lvRefCntWtd();
                }
            }
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
                                VarSetOps.RemoveElemD(_compiler, _largeVectorVars, field._varIndex);
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

                if (Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE &&
                    LsraConfiguration.VarTypeNeedsPartialCalleeSave(local.GetRegisterType()))
                {
                    VarSetOps.AddElemD(_compiler, _largeVectorVars, local._varIndex);
                    if (local.lvRefCntWtd() >= thresholdLargeVectorRefCount)
                    {
                        VarSetOps.AddElemD(_compiler, _largeVectorCalleeSaveCandidateVars, local._varIndex);
                    }
                }
                else if (type.Register == VTR_FLOAT)
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
                if (Configuration.DEBUG && VERBOSE)
                {
                    dumpInterval(interval);
                }
            }
        }

        if (Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE)
        {
            for (var varIndex = 0; varIndex < _compiler.lvaTrackedCount; varIndex++)
            {
                if (VarSetOps.IsMember(_compiler, _largeVectorVars, varIndex))
                {
                    makeUpperVectorInterval(checked((uint)varIndex));
                }
            }
        }

        if (checkDoubleAlign)
        {
            var refCntEBP = refCntReg / 8;
            var refCntWtdEBP = refCntWtdReg / 8;
            _doDoubleAlign = shouldDoubleAlign(refCntStk, refCntEBP, refCntWtdEBP, refCntStkParam, refCntWtdStkDbl);
        }

        if (Configuration.DEBUG && VERBOSE)
        {
            jitprintf("\nFP callee save candidate vars: ");
            if (!VarSetOps.IsEmpty(_compiler, _fpCalleeSaveCandidateVars))
            {
                dumpConvertedVarSet(_compiler, _fpCalleeSaveCandidateVars);
                jitprintf("\n");
            }
            else
            {
                jitprintf("None\n\n");
            }
        }
        var singleExit = (_compiler.fgReturnBlocks is null) || (_compiler.fgReturnBlocks.Next is null);
        JITDUMP($"floatVarCount = {floatVarCount}; hasLoops = {dspBool(_compiler.fgHasLoops)}, singleExit = {dspBool(singleExit)}\n");
        if ((floatVarCount > 6) && _compiler.fgHasLoops && singleExit)
        {
            if (Configuration.DEBUG && VERBOSE)
            {
                jitprintf("Adding additional fp callee save candidates: \n");
                if (!VarSetOps.IsEmpty(_compiler, fpMaybeCandidateVars))
                {
                    dumpConvertedVarSet(_compiler, fpMaybeCandidateVars);
                    jitprintf("\n");
                }
                else
                {
                    jitprintf("None\n\n");
                }
            }
            VarSetOps.UnionD(_compiler, _fpCalleeSaveCandidateVars, fpMaybeCandidateVars);
        }
        if (_compiler.compHndBBtabCount > 0)
        {
            VarSetOps.IntersectionD(_compiler, _exceptVars, _registerCandidateVars);
        }
        if (Configuration.DEBUG && VERBOSE)
        {
            if (Configuration.TARGET_ARM)
            {
                jitprintf("\nlvaTable after IdentifyCandidates\n");
                _compiler.lvaTableDump(Compiler.PRE_REGALLOC_FRAME_LAYOUT);
            }
        }
    }

    private CanDoubleAlign getCanDoubleAlign()
    {
        if (Configuration.DEBUG)
        {
            if (_compiler.compStressCompile(Compiler.STRESS_DBL_ALN, 20))
            {
                return CanDoubleAlign.MUST_DOUBLE_ALIGN;
            }

            return (CanDoubleAlign)JitConfig.JitDoubleAlign;
        }

        return CanDoubleAlign.DEFAULT_DOUBLE_ALIGN;
    }

    // Estimate the bytes saved by frame-pointer-relative parameter references against
    // setup and non-parameter stack references, then weigh alignment costs and EBP locals.
    private bool shouldDoubleAlign(uint refCntStk, uint refCntEBP, double refCntWtdEBP,
        uint refCntStkParam, double refCntWtdStkDbl)
    {
        var doDoubleAlign = false;
        // MOV EBP,ESP; LEA ESP,[EBP-offset]; AND ESP,-8.
        const uint DBL_ALIGN_SETUP_SIZE = 7;

        var bytesUsed = unchecked(refCntStk + refCntEBP - refCntStkParam + DBL_ALIGN_SETUP_SIZE);
        // Estimated misalignment penalty: 0 for SMALL_CODE, 4 normally, 16 for FAST_CODE.
        uint misalignedWeight = 4;

        if (_compiler.compCodeOpt is Compiler.SMALL_CODE)
        {
            misalignedWeight = 0;
        }

        if (_compiler.compCodeOpt is Compiler.FAST_CODE)
        {
            misalignedWeight *= 4;
        }

        JITDUMP("\nDouble alignment:\n");
        JITDUMP($"  Bytes that could be saved by not using EBP frame: {bytesUsed}\n");
        JITDUMP($"  Sum of weighted ref counts for EBP enregistered variables: {formatFloat(refCntWtdEBP, "F6")}\n");
        JITDUMP($"  Sum of weighted ref counts for weighted stack based doubles: {formatFloat(refCntWtdStkDbl, "F6")}\n");

        if (bytesUsed > (refCntWtdStkDbl * misalignedWeight / BB_UNITY_WEIGHT))
        {
            JITDUMP($"    Predicting not to double-align ESP to save {bytesUsed} bytes of code.\n");
        }
        else if (refCntWtdEBP > refCntWtdStkDbl * 2)
        {
            // TODO-CQ: On P4 2 Proc XEON's, SciMark.FFT degrades if SciMark.FFT.transform_internal is
            // not double aligned.
            // Here are the numbers that make this not double-aligned.
            //     refCntWtdStkDbl = 0x164
            //     refCntWtdEBP    = 0x1a4
            // We think we do need to change the heuristic to be in favor of double-align.

            JITDUMP("    Predicting not to double-align ESP to allow EBP to be used to enregister variables.\n");
        }
        else
        {
            // OK we passed all of the benefit tests, so we'll predict a double aligned frame.
            JITDUMP("    Predicting to create a double-aligned frame\n");
            doDoubleAlign = true;
        }

        return doDoubleAlign;
    }

    private void makeUpperVectorInterval(uint varIndex)
    {
        var localInterval = getIntervalForLocalVar(varIndex);
        assert(LsraConfiguration.VarTypeNeedsPartialCalleeSave(localInterval.registerType));
        Interval upperInterval;
        if (Configuration.TARGET_AMD64)
        {
            upperInterval = newInterval(TYP_SIMD16);
        }
        else if (Configuration.TARGET_ARM64)
        {
            upperInterval = newInterval(TYP_DOUBLE);
        }
        else
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.UpperVectorLocalIntervalsAreUnsupportedOnThisTarget
    );
            return;
        }

        upperInterval.relatedInterval = localInterval;
        upperInterval.isUpperVector = true;
    }
}
