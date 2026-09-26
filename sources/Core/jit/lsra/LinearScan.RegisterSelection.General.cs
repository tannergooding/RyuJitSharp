// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private sealed partial class RegisterSelection
    {
        internal SingleTypeRegSet select(
            Interval currentInterval, RefPosition refPosition, out RegisterScore selectionScore)
        {
            selectionScore = RegisterScore.NONE;
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
            reset(currentInterval, refPosition);

            if (RefTypeIsDef(refPosition.refType))
            {
                var nextReference = refPosition.nextRefPosition;
                if (currentInterval.hasConflictingDefUse)
                {
                    _linearScan.resolveConflictingDefAndUse(currentInterval, refPosition);
                    _candidates = refPosition.registerAssignment;
                }
                else if (refPosition.isFixedRegRef && nextReference is not null &&
                         RefTypeIsUse(nextReference.refType) && !nextReference.isFixedRegRef &&
                         genMaxOneBit(refPosition.registerAssignment))
                {
                    var register = refPosition.assignedReg();
                    var nextFixedLocation = _linearScan.getNextFixedRef(register, currentInterval.registerType);
                    if (nextFixedLocation <= nextReference.getRefEndLocation())
                    {
                        _candidates |= nextReference.registerAssignment;
                        if (_preferences == refPosition.registerAssignment)
                        {
                            _preferences = _candidates;
                        }
                    }
                }
            }

            _preferences &= _candidates;
            if (_preferences == SRBM_NONE)
            {
                _preferences = _candidates;
            }
            _candidates = _linearScan.getAvailableGPRsForType(_candidates, _regType);
#if DEBUG
            _candidates = _linearScan.stressLimitRegs(refPosition, _regType, _candidates);
#endif
            assert(_candidates != SRBM_NONE);

            var nextRelatedInterval = _relatedInterval;
            var rangeEndInterval = _relatedInterval;
            while (nextRelatedInterval is not null)
            {
                var nextRelatedReference = nextRelatedInterval.getNextRefPosition();
                if (nextRelatedReference is not null && RefTypeIsDef(nextRelatedReference.refType))
                {
                    var finalRelatedInterval = nextRelatedInterval;
                    nextRelatedInterval = null;
                    var relatedPreferences = finalRelatedInterval.getCurrentPreferences() & _relatedPreferences;
                    if (relatedPreferences != SRBM_NONE)
                    {
                        // Keep native isFree rather than the stronger availability check: the
                        // historical treatment of delayed frees affects allocation preferences.
                        if (!isSingleRegister(relatedPreferences) ||
                            _linearScan.isFree(_linearScan.getRegisterRecord(
                                genRegNumFromMask(relatedPreferences, _regType))))
                        {
                            _relatedPreferences = relatedPreferences;
                            // Only downstream definitions extend the chain; preferences can be circular.
                            if (nextRelatedReference.nodeLocation > _rangeEndLocation)
                            {
                                _preferCalleeSave |= finalRelatedInterval.preferCalleeSave;
                                _rangeEndLocation = nextRelatedReference.getRangeEndLocation();
                                rangeEndInterval = finalRelatedInterval;
                                nextRelatedInterval = finalRelatedInterval.relatedInterval;
                            }
                        }
                    }
                }
                else
                {
                    if (nextRelatedInterval == _relatedInterval)
                    {
                        _relatedInterval = null;
                        _relatedPreferences = SRBM_NONE;
                    }
                    nextRelatedInterval = null;
                }
            }

            if (varTypeUsesFloatReg(currentInterval.registerType))
            {
                _rangeEndRefPosition = refPosition;
                _preferCalleeSave = currentInterval.preferCalleeSave;
            }
            else if (currentInterval.isWriteThru && refPosition.spillAfter)
            {
                _rangeEndRefPosition = refPosition;
            }
            else
            {
                _rangeEndRefPosition = refPosition.getRangeEndRef();
                if (rangeEndInterval is not null && rangeEndInterval.assignedReg is null &&
                    !rangeEndInterval.isWriteThru &&
                    rangeEndInterval.getNextRefLocation() >= _rangeEndRefPosition.nodeLocation)
                {
                    _lastRefPosition = rangeEndInterval.lastRefPosition;
                }
            }
            if (_relatedInterval is not null && !_relatedInterval.isWriteThru)
            {
                assert(_relatedInterval.lastRefPosition is not null);
                _relatedLastLocation = _relatedInterval.lastRefPosition.nodeLocation;
            }

            if (_preferCalleeSave)
            {
                var calleeSaveCandidates = calleeSaveRegs(currentInterval.registerType);
                if (currentInterval.isWriteThru)
                {
                    assert(_linearScan._compiler.codeGen is not null);
                    var unusedCalleeSaves = calleeSaveCandidates &
                        ~_linearScan._compiler.codeGen.RegSet.rsGetModifiedRegsMask().GetRegSetForType(_regType);
                    _callerCalleePrefs = calleeSaveCandidates & ~unusedCalleeSaves;
                    _preferences &= ~unusedCalleeSaves;
                }
                else
                {
                    _callerCalleePrefs = calleeSaveCandidates;
                }
            }
            else
            {
                _callerCalleePrefs = _linearScan.callerSaveRegs(currentInterval.registerType);
            }

            assert(_lastRefPosition is not null);
            _rangeEndLocation = _rangeEndRefPosition.getRefEndLocation();
            _lastLocation = _lastRefPosition.getRefEndLocation();
            _found = false;

            var fixedRegMask = SRBM_NONE;
            if (refPosition.isFixedRegRef)
            {
                assert(genMaxOneBit(refPosition.registerAssignment));
                fixedRegMask = refPosition.registerAssignment;
                if (_candidates == refPosition.registerAssignment)
                {
                    _found = true;
                    if (_linearScan.getNextIntervalRef(genRegNumFromMask(_candidates, _regType), _regType) >
                        _lastLocation)
                    {
                        _unassignedSet = _candidates;
                    }
                }
            }

            if (!_found)
            {
                var busy = (_linearScan._regsBusyUntilKill | _linearScan._regsInUseThisLocation)
                    .GetRegSetForType(_regType);
                _candidates &= ~busy;
                var conflicts = _candidates;
#if HAS_MORE_THAN_64_REGISTERS
                conflicts &= varTypeIsMask(_regType) ? _linearScan._fixedRegsHigh : _linearScan._fixedRegsLow;
#else
                conflicts &= _linearScan._fixedRegsLow;
#endif
                while (conflicts != SRBM_NONE)
                {
                    var register = firstRegNumFromMask(conflicts, _regType);
                    var bit = genSingleTypeRegMask(register);
                    conflicts ^= bit;
                    var location = _linearScan.getNextFixedRef(register, _regType);
                    if (location == refPosition.nodeLocation ||
                        (refPosition.delayRegFree && location == unchecked(refPosition.nodeLocation + 1)))
                    {
                        _candidates &= ~bit;
                    }
                }
                _candidates |= fixedRegMask;
                _found = isSingleRegister(_candidates);
            }

            if (!_found && currentInterval.assignedReg is RegRecord previousRegister)
            {
                _prevRegBit = genSingleTypeRegMask(previousRegister.regNum);
                if (previousRegister.assignedInterval == currentInterval && (_candidates & _prevRegBit) != SRBM_NONE)
                {
                    _candidates = _prevRegBit;
                    _found = true;
#if DEBUG
                    selectionScore = RegisterScore.THIS_ASSIGNED;
#endif
                }
            }
            else
            {
                _prevRegBit = SRBM_NONE;
            }

            if (!_found && _candidates == SRBM_NONE)
            {
                assert(refPosition.RegOptional());
                currentInterval.assignedReg = null;
                return SRBM_NONE;
            }

            _freeCandidates = _linearScan.getFreeCandidates(_candidates, _regType);
            if (_freeCandidates == SRBM_NONE)
            {
                if (!refPosition.IsActualRef())
                {
                    currentInterval.assignedReg = null;
                    return SRBM_NONE;
                }
            }
            else if (currentInterval.isConstant && RefTypeIsDef(refPosition.refType))
            {
                _matchingConstants = _linearScan.getMatchingConstants(_candidates, currentInterval, refPosition);
            }

#if DEBUG
            for (var index = 0; index < HeuristicCount && !_found; index++)
            {
                var score = _regSelectionOrder[index];
                if (!_mappingTable.TryGetValue(score, out var heuristic))
                {
                    throw new FatalJitException("Unexpected LSRA register-selection heuristic.");
                }
                heuristic();
                if (_found)
                {
                    selectionScore = score;
#if TRACK_LSRA_STATS
                    _linearScan.updateLsraStat(getLsraStatFromScore(score), refPosition.bbNum);
#endif
                }
            }
#else
            if (_freeCandidates != SRBM_NONE)
            {
                try_FREE();
                if (!_found)
                {
                    try_CONST_AVAILABLE();
                }
                if (!_found)
                {
                    try_THIS_ASSIGNED();
                }
                if (!_found)
                {
                    try_COVERS();
                }
                if (!_found)
                {
                    try_OWN_PREFERENCE();
                }
                if (!_found)
                {
                    try_COVERS_RELATED();
                }
                if (!_found)
                {
                    try_RELATED_PREFERENCE();
                }
                if (!_found)
                {
                    try_CALLER_CALLEE();
                }
                if (!_found)
                {
                    try_UNASSIGNED();
                }
                if (!_found)
                {
                    try_COVERS_FULL();
                }
                if (!_found)
                {
                    try_BEST_FIT();
                }
                if (!_found)
                {
                    try_IS_PREV_REG();
                }
                if (!_found)
                {
                    try_REG_ORDER();
                }
            }
            if (_freeCandidates == SRBM_NONE || !_found)
            {
                try_SPILL_COST();
            }
            if (!_found)
            {
                try_FAR_NEXT_REF();
            }
            if (!_found)
            {
                try_PREV_REG_OPT();
            }
            if (!_found)
            {
                try_REG_NUM();
            }
#endif
            if (_skipAllocation)
            {
                _foundRegBit = SRBM_NONE;
                return SRBM_NONE;
            }

            calculateUnassignedSets();
            assert(_found && isSingleRegister(_candidates));
            _foundRegBit = _candidates;
            return _candidates;
#else
            const string message = "General register selection outside Windows AMD64 is not implemented.";
            JITDUMP($"\nCOMPILATION FAILED: {message}\n");
            throw new FatalJitException(CORJIT_SKIPPED, message);
#endif
        }
    }

#if DEBUG && TRACK_LSRA_STATS
    private static LsraStat getLsraStatFromScore(RegisterScore score) => score switch
    {
        RegisterScore.CONST_AVAILABLE => LsraStat.STAT_CONST_AVAILABLE,
        RegisterScore.THIS_ASSIGNED => LsraStat.STAT_THIS_ASSIGNED,
        RegisterScore.COVERS => LsraStat.STAT_COVERS,
        RegisterScore.OWN_PREFERENCE => LsraStat.STAT_OWN_PREFERENCE,
        RegisterScore.COVERS_RELATED => LsraStat.STAT_COVERS_RELATED,
        RegisterScore.RELATED_PREFERENCE => LsraStat.STAT_RELATED_PREFERENCE,
        RegisterScore.CALLER_CALLEE => LsraStat.STAT_CALLER_CALLEE,
        RegisterScore.UNASSIGNED => LsraStat.STAT_UNASSIGNED,
        RegisterScore.COVERS_FULL => LsraStat.STAT_COVERS_FULL,
        RegisterScore.BEST_FIT => LsraStat.STAT_BEST_FIT,
        RegisterScore.IS_PREV_REG => LsraStat.STAT_IS_PREV_REG,
        RegisterScore.REG_ORDER => LsraStat.STAT_REG_ORDER,
        RegisterScore.SPILL_COST => LsraStat.STAT_SPILL_COST,
        RegisterScore.FAR_NEXT_REF => LsraStat.STAT_FAR_NEXT_REF,
        RegisterScore.PREV_REG_OPT => LsraStat.STAT_PREV_REG_OPT,
        RegisterScore.REG_NUM => LsraStat.STAT_REG_NUM,
        _ => LsraStat.STAT_FREE,
    };
#endif
}
