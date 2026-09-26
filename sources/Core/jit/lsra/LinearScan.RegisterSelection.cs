// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
#if DEBUG
using System.Collections.Generic;
using System.Runtime.InteropServices;
#endif

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private enum RegisterScore : int
    {
        NONE = 0,
        FREE = 0x10000,
        CONST_AVAILABLE = 0x08000,
        THIS_ASSIGNED = 0x04000,
        COVERS = 0x02000,
        OWN_PREFERENCE = 0x01000,
        COVERS_RELATED = 0x00800,
        RELATED_PREFERENCE = 0x00400,
        CALLER_CALLEE = 0x00200,
        UNASSIGNED = 0x00100,
        COVERS_FULL = 0x00080,
        BEST_FIT = 0x00040,
        IS_PREV_REG = 0x00020,
        REG_ORDER = 0x00010,
        SPILL_COST = 0x00008,
        FAR_NEXT_REF = 0x00004,
        PREV_REG_OPT = 0x00002,
        REG_NUM = 0x00001,
    }

    private sealed partial class RegisterSelection
    {
        private const int HeuristicCount = 17;

        private readonly LinearScan _linearScan;
        private Interval? _currentInterval;
        private RefPosition? _refPosition;
        private RegisterType _regType = TYP_UNKNOWN;
        private SingleTypeRegSet _candidates = SRBM_NONE;
        private SingleTypeRegSet _preferences = SRBM_NONE;
        private Interval? _relatedInterval;
        private SingleTypeRegSet _relatedPreferences = SRBM_NONE;
        private LsraLocation _rangeEndLocation;
        private LsraLocation _relatedLastLocation;
        private bool _preferCalleeSave;
        private RefPosition? _rangeEndRefPosition;
        private RefPosition? _lastRefPosition;
        private SingleTypeRegSet _callerCalleePrefs = SRBM_NONE;
        private LsraLocation _lastLocation;
        private SingleTypeRegSet _foundRegBit = SRBM_NONE;
        private SingleTypeRegSet _prevRegBit = SRBM_NONE;
        private SingleTypeRegSet _freeCandidates = SRBM_NONE;
        private SingleTypeRegSet _matchingConstants = SRBM_NONE;
        private SingleTypeRegSet _unassignedSet = SRBM_NONE;
        private SingleTypeRegSet _coversSet = SRBM_NONE;
        private SingleTypeRegSet _preferenceSet = SRBM_NONE;
        private SingleTypeRegSet _coversRelatedSet = SRBM_NONE;
        private SingleTypeRegSet _coversFullSet = SRBM_NONE;
        private bool _coversSetsCalculated;
        private bool _found;
        private bool _skipAllocation;
        private bool _coversFullApplied;
        private bool _constAvailableApplied;

#if DEBUG
        private readonly RegisterScore[] _regSelectionOrder = new RegisterScore[HeuristicCount];
        private readonly Dictionary<RegisterScore, System.Action> _mappingTable;
#endif

        public unsafe RegisterSelection(LinearScan linearScan)
        {
            _linearScan = linearScan;

#if DEBUG
            _mappingTable = new(HeuristicCount)
            {
                [RegisterScore.FREE] = try_FREE,
                [RegisterScore.CONST_AVAILABLE] = try_CONST_AVAILABLE,
                [RegisterScore.THIS_ASSIGNED] = try_THIS_ASSIGNED,
                [RegisterScore.COVERS] = try_COVERS,
                [RegisterScore.OWN_PREFERENCE] = try_OWN_PREFERENCE,
                [RegisterScore.COVERS_RELATED] = try_COVERS_RELATED,
                [RegisterScore.RELATED_PREFERENCE] = try_RELATED_PREFERENCE,
                [RegisterScore.CALLER_CALLEE] = try_CALLER_CALLEE,
                [RegisterScore.UNASSIGNED] = try_UNASSIGNED,
                [RegisterScore.COVERS_FULL] = try_COVERS_FULL,
                [RegisterScore.BEST_FIT] = try_BEST_FIT,
                [RegisterScore.IS_PREV_REG] = try_IS_PREV_REG,
                [RegisterScore.REG_ORDER] = try_REG_ORDER,
                [RegisterScore.SPILL_COST] = try_SPILL_COST,
                [RegisterScore.FAR_NEXT_REF] = try_FAR_NEXT_REF,
                [RegisterScore.PREV_REG_OPT] = try_PREV_REG_OPT,
                [RegisterScore.REG_NUM] = try_REG_NUM,
            };
            assert(_mappingTable.Count == HeuristicCount);
            if (_mappingTable.Count != HeuristicCount)
            {
                throw new FatalJitException("LSRA selector heuristic mapping is incomplete.");
            }

            foreach (var score in _mappingTable.Keys)
            {
                assert(getScoreName(score) != "  -  ");
            }

            var orderingPointer = JitConfig.JitLsraOrdering;
            var ordering = orderingPointer is null ? null : Marshal.PtrToStringAnsi((nint)orderingPointer);
            if (ordering is null)
            {
                ordering = "ABCDEFGHIJKLMNOPQ";
                if (!linearScan._enregisterLocalVars && linearScan._compiler.opts.OptimizationDisabled)
                {
                    ordering = "MQQQQQQQQQQQQQQQQ";
                }
            }

            if (ordering.Length < HeuristicCount)
            {
                assert(false, "Invalid lsraOrdering value.");
                throw new FatalJitException("Invalid lsraOrdering value.");
            }

            for (var orderId = 0; orderId < HeuristicCount; orderId++)
            {
                assert(_regSelectionOrder[orderId] is RegisterScore.NONE);
                _regSelectionOrder[orderId] = ordering[orderId] switch
                {
                    'A' => RegisterScore.FREE,
                    'B' => RegisterScore.CONST_AVAILABLE,
                    'C' => RegisterScore.THIS_ASSIGNED,
                    'D' => RegisterScore.COVERS,
                    'E' => RegisterScore.OWN_PREFERENCE,
                    'F' => RegisterScore.COVERS_RELATED,
                    'G' => RegisterScore.RELATED_PREFERENCE,
                    'H' => RegisterScore.CALLER_CALLEE,
                    'I' => RegisterScore.UNASSIGNED,
                    'J' => RegisterScore.COVERS_FULL,
                    'K' => RegisterScore.BEST_FIT,
                    'L' => RegisterScore.IS_PREV_REG,
                    'M' => RegisterScore.REG_ORDER,
                    'N' => RegisterScore.SPILL_COST,
                    'O' => RegisterScore.FAR_NEXT_REF,
                    'P' => RegisterScore.PREV_REG_OPT,
                    'Q' => RegisterScore.REG_NUM,
                    _ => InvalidOrderingCharacter(),
                };
            }
#endif
        }

        private Interval CurrentInterval =>
            _currentInterval ?? throw new FatalJitException("Register selection has not been reset.");

        private RefPosition CurrentRefPosition =>
            _refPosition ?? throw new FatalJitException("Register selection has not been reset.");

        internal bool foundUnassignedReg()
        {
            assert(_found && isSingleRegister(_foundRegBit));
            return ((_foundRegBit & _unassignedSet) != SRBM_NONE) && !isAlreadyAssigned();
        }

        internal bool isSpilling() => (_foundRegBit & _freeCandidates) == SRBM_NONE;

        internal bool isMatchingConstant()
        {
            assert(_found && isSingleRegister(_foundRegBit));
            return (_matchingConstants & _foundRegBit) != SRBM_NONE;
        }

        internal bool isConstAvailable() => _constAvailableApplied;

        private bool isAlreadyAssigned()
        {
            assert(_found && isSingleRegister(_candidates));
            return (_prevRegBit & _preferences) == _foundRegBit;
        }

        private void resetMinimal(Interval interval, RefPosition refPosition)
        {
            _currentInterval = interval;
            _refPosition = refPosition;
            _regType = _linearScan.getRegisterType(interval, refPosition);
            _candidates = refPosition.registerAssignment;
            _found = false;
        }

        internal SingleTypeRegSet selectMinimal(Interval currentInterval, RefPosition refPosition)
        {
            return selectMinimal(currentInterval, refPosition, out _);
        }

        internal SingleTypeRegSet selectMinimal(
            Interval currentInterval, RefPosition refPosition, out RegisterScore selectionScore)
        {
            selectionScore = RegisterScore.NONE;
#if DEBUG
            if (VERBOSE)
            {
                _linearScan.initializeAllocationDumpFormat();
            }
#endif
            assert(!_linearScan._enregisterLocalVars);
            if (_linearScan._enregisterLocalVars)
            {
                throw new FatalJitException("Minimal LSRA selection requires local variables to remain un-enregistered.");
            }

            resetMinimal(currentInterval, refPosition);

            if (RefTypeIsDef(refPosition.refType))
            {
                var nextRefPosition = refPosition.nextRefPosition;
                if (currentInterval.hasConflictingDefUse)
                {
                    _linearScan.resolveConflictingDefAndUse(currentInterval, refPosition);
                    _candidates = refPosition.registerAssignment;
                }
                else if (refPosition.isFixedRegRef &&
                         (nextRefPosition is not null) &&
                         RefTypeIsUse(nextRefPosition.refType) &&
                         !nextRefPosition.isFixedRegRef &&
                         isSingleRegister(refPosition.registerAssignment))
                {
                    var defReg = refPosition.assignedReg();
                    var nextFixedRegRefLocation = _linearScan.getNextFixedRef(defReg, currentInterval.registerType);
                    if (nextFixedRegRefLocation <= nextRefPosition.getRefEndLocation())
                    {
                        _candidates |= nextRefPosition.registerAssignment;
                    }
                }
            }

            _candidates = _linearScan.getAvailableGPRsForType(_candidates, (var_types)_regType);
#if DEBUG
            _candidates = _linearScan.stressLimitRegs(refPosition, _regType, _candidates);
#endif
            assert(_candidates != SRBM_NONE);
            if (_candidates == SRBM_NONE)
            {
                throw new FatalJitException("Minimal LSRA selection has no candidate registers.");
            }

            var fixedRegMask = SRBM_NONE;
            if (refPosition.isFixedRegRef)
            {
                assert(isSingleRegister(refPosition.registerAssignment));
                if (_candidates == refPosition.registerAssignment)
                {
                    _found = true;
                    _foundRegBit = _candidates;
                    return _candidates;
                }

                fixedRegMask = refPosition.registerAssignment;
            }

            var busyRegs = getRegSetForType(
                _linearScan._regsBusyUntilKill | _linearScan._regsInUseThisLocation,
                _regType);
            _candidates &= ~busyRegs;

            var checkConflictMask = _candidates & _linearScan._fixedRegsLow;
            while (checkConflictMask != SRBM_NONE)
            {
                var conflictReg = firstRegNumFromMask(checkConflictMask, _regType);
                var conflictBit = genSingleTypeRegMask(conflictReg);
                checkConflictMask ^= conflictBit;

                var conflictLocation = _linearScan.getNextFixedRef(conflictReg, _regType);
                if ((conflictLocation == refPosition.nodeLocation) ||
                    (refPosition.delayRegFree && (conflictLocation == unchecked(refPosition.nodeLocation + 1))))
                {
                    _candidates &= ~conflictBit;
                }
            }

            _candidates |= fixedRegMask;
            _found = isSingleRegister(_candidates);
            if (_found)
            {
                _foundRegBit = _candidates;
                return _candidates;
            }

            if (_candidates == SRBM_NONE)
            {
                assert(refPosition.RegOptional());
                if (!refPosition.RegOptional())
                {
                    throw new FatalJitException("A required reference lost all candidate registers.");
                }

                currentInterval.assignedReg = null;
                return SRBM_NONE;
            }

            _freeCandidates = _linearScan.getFreeCandidates(_candidates, _regType);
            if (_freeCandidates != SRBM_NONE)
            {
                _candidates = _freeCandidates;
                try_REG_ORDER();
                if (_found)
                {
                    selectionScore = RegisterScore.REG_ORDER;
                }
            }

#if DEBUG && TRACK_LSRA_STATS
            if (_found)
            {
                _linearScan.updateLsraStat(LsraStat.STAT_REG_ORDER, refPosition.bbNum);
            }
#endif

            if (!_found)
            {
                if (refPosition.RegOptional() || !refPosition.IsActualRef())
                {
                    currentInterval.assignedReg = null;
                    return SRBM_NONE;
                }

                try_REG_NUM();
                if (_found)
                {
                    selectionScore = RegisterScore.REG_NUM;
                }
#if DEBUG && TRACK_LSRA_STATS
                if (_found)
                {
                    _linearScan.updateLsraStat(LsraStat.STAT_REG_NUM, refPosition.bbNum);
                }
#endif
            }

            assert(_found && isSingleRegister(_candidates));
            if (!_found || !isSingleRegister(_candidates))
            {
                throw new FatalJitException("Minimal LSRA selection did not choose exactly one register.");
            }

            _foundRegBit = _candidates;
            return _candidates;
        }

        private void reset(Interval interval, RefPosition refPosition)
        {
            _currentInterval = interval;
            _refPosition = refPosition;
            _regType = _linearScan.getRegisterType(interval, refPosition);
            _candidates = refPosition.registerAssignment;
            _preferences = interval.registerPreferences & ~interval.registerAversion;
            if (_preferences == SRBM_NONE)
            {
                _preferences = _linearScan.allRegs(_regType) & ~interval.registerAversion;
            }

            _relatedInterval = interval.isSpecialPutArg ? null : interval.relatedInterval;
            _relatedPreferences = _relatedInterval?.getCurrentPreferences() ?? SRBM_NONE;

            var rangeEndRefPosition = refPosition.getRangeEndRef();
            _rangeEndRefPosition = rangeEndRefPosition;
            _rangeEndLocation = rangeEndRefPosition.getRefEndLocation();
            _relatedLastLocation = _rangeEndLocation;
            _preferCalleeSave = interval.preferCalleeSave;
            _lastRefPosition = interval.lastRefPosition;

            _freeCandidates = SRBM_NONE;
            _matchingConstants = SRBM_NONE;
            _unassignedSet = SRBM_NONE;
            _coversSet = SRBM_NONE;
            _preferenceSet = SRBM_NONE;
            _coversRelatedSet = SRBM_NONE;
            _coversFullSet = SRBM_NONE;
            _coversSetsCalculated = false;
            _found = false;
            _skipAllocation = false;
            _coversFullApplied = false;
            _constAvailableApplied = false;
        }

        private bool applySelection(RegisterScore selectionScore, SingleTypeRegSet selectionCandidates)
        {
            _ = selectionScore;
            var newCandidates = _candidates & selectionCandidates;
            if (newCandidates != SRBM_NONE)
            {
                _candidates = newCandidates;
                return isSingleRegister(_candidates);
            }

            return false;
        }

        private bool applySingleRegSelection(RegisterScore selectionScore, SingleTypeRegSet selectionCandidate)
        {
            _ = selectionScore;
            assert(isSingleRegister(selectionCandidate));
            if (!isSingleRegister(selectionCandidate))
            {
                throw new FatalJitException("A single-register heuristic must select exactly one register.");
            }

            var newCandidates = _candidates & selectionCandidate;
            if (newCandidates != SRBM_NONE)
            {
                _candidates = newCandidates;
                return true;
            }

            return false;
        }

        private void calculateUnassignedSets()
        {
            if ((_freeCandidates == SRBM_NONE) || _coversSetsCalculated)
            {
                return;
            }

            for (var candidates = _candidates; candidates != SRBM_NONE;)
            {
                var regNum = firstRegNumFromMask(candidates, _regType);
                var candidateBit = genSingleTypeRegMask(regNum);
                candidates ^= candidateBit;

                if (_linearScan.getNextIntervalRef(regNum, _regType) > _lastLocation)
                {
                    _unassignedSet |= candidateBit;
                }
            }
        }

        private void calculateCoversSets()
        {
            if ((_freeCandidates == SRBM_NONE) || _coversSetsCalculated)
            {
                return;
            }

            _preferenceSet = _candidates & _preferences;
            var coversCandidates = (_preferenceSet == SRBM_NONE) ? _candidates : _preferenceSet;
            while (coversCandidates != SRBM_NONE)
            {
                var regNum = firstRegNumFromMask(coversCandidates, _regType);
                var candidateBit = genSingleTypeRegMask(regNum);
                coversCandidates ^= candidateBit;

                if (!_found)
                {
                    var nextIntervalLocation = _linearScan.getNextIntervalRef(regNum, _regType);
                    var nextPhysRefLocation = _linearScan.getNextFixedRef(regNum, _regType);
                    var coversCandidateLocation = Math.Min(nextPhysRefLocation, nextIntervalLocation);

                    var rangeEndRefPosition = _rangeEndRefPosition
                        ?? throw new FatalJitException("Register selection is missing its range-end reference.");
                    if ((coversCandidateLocation == _rangeEndLocation) &&
                        rangeEndRefPosition.isFixedRefOfReg(regNum))
                    {
                        coversCandidateLocation = unchecked(coversCandidateLocation + 1);
                    }

                    if (coversCandidateLocation > _rangeEndLocation)
                    {
                        _coversSet |= candidateBit;
                    }

                    if ((candidateBit & _relatedPreferences) != SRBM_NONE)
                    {
                        if (coversCandidateLocation > _relatedLastLocation)
                        {
                            _coversRelatedSet |= candidateBit;
                        }
                    }
                    else if (candidateBit == CurrentRefPosition.registerAssignment)
                    {
                        _coversRelatedSet |= candidateBit;
                    }

                    if (coversCandidateLocation > _lastLocation)
                    {
                        _coversFullSet |= candidateBit;
                    }
                }

                if (_linearScan.getNextIntervalRef(regNum, _regType) > _lastLocation)
                {
                    _unassignedSet |= candidateBit;
                }
            }

            _coversSetsCalculated = true;
        }

        private void try_FREE()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }
#endif
            _found = applySelection(RegisterScore.FREE, _freeCandidates);
        }

        private void try_CONST_AVAILABLE()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }
#endif
            if (CurrentInterval.isConstant && RefTypeIsDef(CurrentRefPosition.refType))
            {
                var newCandidates = _candidates & _matchingConstants;
                if (newCandidates != SRBM_NONE)
                {
                    _candidates = newCandidates;
                    _constAvailableApplied = true;
                    _found = isSingleRegister(newCandidates);
                }
            }
        }

        private void try_THIS_ASSIGNED()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }
#endif
            if (CurrentInterval.assignedReg is not null)
            {
                _found = applySelection(RegisterScore.THIS_ASSIGNED, _freeCandidates & _preferences & _prevRegBit);
            }
        }

        private void try_COVERS()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }
#endif
            calculateCoversSets();
            _found = applySelection(RegisterScore.COVERS, _coversSet & _preferenceSet);
        }

        private void try_OWN_PREFERENCE()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }

            calculateCoversSets();
#endif
            _found = applySelection(RegisterScore.OWN_PREFERENCE, _preferenceSet & _freeCandidates);
        }

        private void try_COVERS_RELATED()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }

            calculateCoversSets();
#endif
            _found = applySelection(RegisterScore.COVERS_RELATED, _coversRelatedSet & _freeCandidates);
        }

        private void try_RELATED_PREFERENCE()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }
#endif
            _found = applySelection(RegisterScore.RELATED_PREFERENCE, _relatedPreferences & _freeCandidates);
        }

        private void try_CALLER_CALLEE()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }
#endif
            _found = applySelection(RegisterScore.CALLER_CALLEE, _callerCalleePrefs & _freeCandidates);
        }

        private void try_UNASSIGNED()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }

            calculateCoversSets();
#endif
            _found = applySelection(RegisterScore.UNASSIGNED, _unassignedSet);
        }

        private void try_COVERS_FULL()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }

            calculateCoversSets();
#endif
            var newCandidates = _candidates & _coversFullSet & _freeCandidates;
            if (newCandidates != SRBM_NONE)
            {
                _candidates = newCandidates;
                _found = isSingleRegister(_candidates);
                _coversFullApplied = true;
            }
        }

        private void try_BEST_FIT()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }
#endif
            var bestFitSet = SRBM_NONE;
            var bestFitLocation = _coversFullApplied ? MaxLocation : MinLocation;
            for (var bestFitCandidates = _candidates; bestFitCandidates != SRBM_NONE;)
            {
                var regNum = firstRegNumFromMask(bestFitCandidates, _regType);
                var candidateBit = genSingleTypeRegMask(regNum);
                bestFitCandidates ^= candidateBit;

                var nextIntervalLocation = _linearScan.getNextIntervalRef(regNum, _regType);
                var nextPhysRefLocation = _linearScan.getNextFixedRef(regNum, _regType);
                nextPhysRefLocation = Math.Min(nextPhysRefLocation, nextIntervalLocation);
                var rangeEndRefPosition = _rangeEndRefPosition
                    ?? throw new FatalJitException("Register selection is missing its range-end reference.");
                if ((nextPhysRefLocation == _rangeEndLocation) && rangeEndRefPosition.isFixedRefOfReg(regNum))
                {
                    nextPhysRefLocation = unchecked(nextPhysRefLocation + 1);
                }

                if (nextPhysRefLocation == bestFitLocation)
                {
                    bestFitSet |= candidateBit;
                }
                else
                {
                    var isBetter = nextPhysRefLocation > _lastLocation
                        ? (bestFitLocation <= _lastLocation) || (nextPhysRefLocation < bestFitLocation)
                        : (bestFitLocation <= _lastLocation) && (nextPhysRefLocation > bestFitLocation);
                    if (isBetter)
                    {
                        bestFitSet = candidateBit;
                        bestFitLocation = nextPhysRefLocation;
                    }
                }
            }

            assert(bestFitSet != SRBM_NONE);
            _found = applySelection(RegisterScore.BEST_FIT, bestFitSet);
        }

        private void try_IS_PREV_REG()
        {
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }
#endif
            if ((CurrentInterval.assignedReg is not null) && _coversFullApplied)
            {
                _found = applySingleRegSelection(RegisterScore.IS_PREV_REG, _prevRegBit);
            }
        }

        private void try_REG_ORDER()
        {
            assert(!_found);
#if DEBUG
            if (_freeCandidates == SRBM_NONE)
            {
                return;
            }
#endif
            var lowestRegOrder = uint.MaxValue;
            var lowestRegOrderBit = SRBM_NONE;
            for (var regOrderCandidates = _candidates; regOrderCandidates != SRBM_NONE;)
            {
                var regNum = firstRegNumFromMask(regOrderCandidates, _regType);
                var candidateBit = genSingleTypeRegMask(regNum);
                regOrderCandidates ^= candidateBit;

                var thisRegOrder = _linearScan.getRegisterRecord(regNum).regOrder;
                if (thisRegOrder < lowestRegOrder)
                {
                    lowestRegOrder = thisRegOrder;
                    lowestRegOrderBit = candidateBit;
                }
            }

            assert(lowestRegOrderBit != SRBM_NONE);
            _found = applySingleRegSelection(RegisterScore.REG_ORDER, lowestRegOrderBit);
        }

        private void try_SPILL_COST()
        {
            assert(!_found);
            var lowestCostSpillSet = SRBM_NONE;
            var thisSpillWeight = _linearScan.getWeight(CurrentRefPosition);
            var bestSpillWeight = double.PositiveInfinity;
            var thisLocation = CurrentRefPosition.nodeLocation;

            for (var spillCandidates = _candidates; spillCandidates != SRBM_NONE;)
            {
                var regNum = firstRegNumFromMask(spillCandidates, _regType);
                var candidateBit = genSingleTypeRegMask(regNum);
                spillCandidates ^= candidateBit;

                var regRecord = _linearScan.getRegisterRecord(regNum);
                var assignedInterval = regRecord.assignedInterval
                    ?? throw new FatalJitException("Spill-cost selection requires each busy candidate to have an assigned interval.");
                var recentRefPosition = assignedInterval.recentRefPosition;
                var currentSpillWeight = 0.0;

                if (_linearScan.getNextIntervalRef(regNum, _regType) == thisLocation)
                {
                    var nextIntervalRef = assignedInterval.getNextRefPosition()
                        ?? throw new FatalJitException("A current next-interval location must have a reference.");

                    if (!nextIntervalRef.RegOptional())
                    {
                        continue;
                    }
                }

                if (!_linearScan.isSpillCandidate(CurrentInterval, CurrentRefPosition, regRecord))
                {
                    continue;
                }

                if (recentRefPosition is not null)
                {
                    var reloadRefPosition = assignedInterval.getNextRefPosition();
                    if (reloadRefPosition is not null &&
                        recentRefPosition.RegOptional() &&
                        !(assignedInterval.isLocalVar && recentRefPosition.IsActualRef()))
                    {
                        currentSpillWeight = _linearScan.getWeight(reloadRefPosition);
                    }
                }

                if (currentSpillWeight == 0)
                {
                    currentSpillWeight = _linearScan._spillCost[(int)regNum];
                }

                if (currentSpillWeight < bestSpillWeight)
                {
                    bestSpillWeight = currentSpillWeight;
                    lowestCostSpillSet = candidateBit;
                }
                else if (currentSpillWeight == bestSpillWeight)
                {
                    lowestCostSpillSet |= candidateBit;
                }
            }

            if (lowestCostSpillSet == SRBM_NONE)
            {
                return;
            }

            if ((bestSpillWeight >= thisSpillWeight) && CurrentRefPosition.RegOptional())
            {
                CurrentInterval.assignedReg = null;
                _skipAllocation = true;
                _found = true;
            }

            assert(lowestCostSpillSet != SRBM_NONE);
            _found = applySelection(RegisterScore.SPILL_COST, lowestCostSpillSet);
        }

        private void try_FAR_NEXT_REF()
        {
            assert(!_found);
            var farthestLocation = MinLocation;
            var farthestSet = SRBM_NONE;
            for (var farthestCandidates = _candidates; farthestCandidates != SRBM_NONE;)
            {
                var regNum = firstRegNumFromMask(farthestCandidates, _regType);
                var candidateBit = genSingleTypeRegMask(regNum);
                farthestCandidates ^= candidateBit;

                var nextIntervalLocation = _linearScan.getNextIntervalRef(regNum, CurrentInterval.registerType);
                var nextPhysRefLocation = Math.Min(_linearScan._nextFixedRef[(int)regNum], nextIntervalLocation);
                if (nextPhysRefLocation == farthestLocation)
                {
                    farthestSet |= candidateBit;
                }
                else if (nextPhysRefLocation > farthestLocation)
                {
                    farthestSet = candidateBit;
                    farthestLocation = nextPhysRefLocation;
                }
            }

            assert(farthestSet != SRBM_NONE);
            _found = applySelection(RegisterScore.FAR_NEXT_REF, farthestSet);
        }

        private void try_PREV_REG_OPT()
        {
            assert(!_found);
            var prevRegOptSet = SRBM_NONE;
            for (var candidates = _candidates; candidates != SRBM_NONE;)
            {
                var regNum = firstRegNumFromMask(candidates, _regType);
                var candidateBit = genSingleTypeRegMask(regNum);
                candidates ^= candidateBit;

                var assignedInterval = _linearScan.physRegs[(int)regNum].assignedInterval;
                var foundPrevRegOptReg = (assignedInterval is not null) &&
                                         (assignedInterval.recentRefPosition is not null) &&
                                         assignedInterval.recentRefPosition.reload &&
                                         assignedInterval.recentRefPosition.RegOptional();
#if DEBUG
                if ((assignedInterval is null) || (assignedInterval.recentRefPosition is null))
                {
                    assert(false, "Spill candidate has no assignedInterval recentRefPosition.");
                }
#endif
                if (foundPrevRegOptReg)
                {
                    prevRegOptSet = candidateBit;
                }
            }

            _found = applySelection(RegisterScore.PREV_REG_OPT, prevRegOptSet);
        }

        private void try_REG_NUM()
        {
            assert(!_found);
            _found = applySingleRegSelection(RegisterScore.REG_NUM, findLowestBit(_candidates));
        }

        private static SingleTypeRegSet findLowestBit(SingleTypeRegSet registers)
        {
#if TARGET_X86
            var bits = unchecked((uint)registers);
            return unchecked((SingleTypeRegSet)(bits & (0u - bits)));
#else
            var bits = unchecked((ulong)registers);
            return unchecked((SingleTypeRegSet)(long)(bits & (0UL - bits)));
#endif
        }

        private static regNumber firstRegNumFromMask(SingleTypeRegSet registers, RegisterType registerType) =>
            genRegNumFromMask(findLowestBit(registers), registerType);

#if DEBUG
        private static RegisterScore InvalidOrderingCharacter()
        {
            assert(false, "Invalid lsraOrdering value.");
            throw new FatalJitException("Invalid lsraOrdering value.");
        }
#endif
    }

    internal SingleTypeRegSet selectMinimal(Interval currentInterval, RefPosition refPosition) =>
        _regSelector.selectMinimal(currentInterval, refPosition);

    private SingleTypeRegSet selectMinimal(
        Interval currentInterval, RefPosition refPosition, out RegisterScore selectionScore) =>
        _regSelector.selectMinimal(currentInterval, refPosition, out selectionScore);

#if DEBUG
    private static string getScoreName(RegisterScore score) => score switch
    {
        RegisterScore.FREE => "FREE ",
        RegisterScore.CONST_AVAILABLE => "CONST",
        RegisterScore.THIS_ASSIGNED => "THISA",
        RegisterScore.COVERS => "COVRS",
        RegisterScore.OWN_PREFERENCE => "OWNPR",
        RegisterScore.COVERS_RELATED => "COREL",
        RegisterScore.RELATED_PREFERENCE => "RELPR",
        RegisterScore.CALLER_CALLEE => "CRCE ",
        RegisterScore.UNASSIGNED => "UNASG",
        RegisterScore.COVERS_FULL => "COFUL",
        RegisterScore.BEST_FIT => "BSFIT",
        RegisterScore.IS_PREV_REG => "PRVRG",
        RegisterScore.REG_ORDER => "ORDER",
        RegisterScore.SPILL_COST => "SPILL",
        RegisterScore.FAR_NEXT_REF => "FNREF",
        RegisterScore.PREV_REG_OPT => "PRGOP",
        RegisterScore.REG_NUM => "RGNUM",
        _ => "  -  ",
    };
#endif
}
