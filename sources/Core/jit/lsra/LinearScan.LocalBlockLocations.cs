// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private readonly struct BlockEndLocationsVisitor : IBitSetVisitor<BlockEndLocationsVisitor>
    {
        private readonly LinearScan _linearScan;
        private readonly regNumberSmall[] _outMap;

        public BlockEndLocationsVisitor(LinearScan linearScan, regNumberSmall[] outMap)
        {
            _linearScan = linearScan;
            _outMap = outMap;
        }

        public readonly bool Visit(int variableIndex)
        {
            var interval = _linearScan.getIntervalForLocalVar(checked((uint)variableIndex));
            if (interval.isActive)
            {
                assert(interval.physReg != REG_NA && interval.physReg != REG_STK);
                _linearScan.setVarReg(_outMap, checked((uint)variableIndex), interval.physReg);
            }
            else
            {
                _outMap[variableIndex] = (regNumberSmall)REG_STK;
            }
            if (Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE)
            {
                assert(!LsraConfiguration.VarTypeNeedsPartialCalleeSave(interval.registerType) ||
                    !interval.isPartiallySpilled);
            }
            return true;
        }
    }

    private struct BlockStartLocationsVisitor : IBitSetVisitor<BlockStartLocationsVisitor>
    {
        private readonly LinearScan _linearScan;
        private readonly BasicBlock _currentBlock;
        private readonly LsraBlockInfo[] _blockInfo;
        private readonly uint _predecessorNumber;
        private readonly regNumberSmall[] _inMap;
        private readonly regNumberSmall[] _predecessorMap;
        private regMaskTP _inactiveRegs;
        private regMaskTP _liveRegs;

        public BlockStartLocationsVisitor(
            LinearScan linearScan,
            BasicBlock currentBlock,
            LsraBlockInfo[] blockInfo,
            uint predecessorNumber,
            regNumberSmall[] inMap,
            regNumberSmall[] predecessorMap)
        {
            _linearScan = linearScan;
            _currentBlock = currentBlock;
            _blockInfo = blockInfo;
            _predecessorNumber = predecessorNumber;
            _inMap = inMap;
            _predecessorMap = predecessorMap;
            _inactiveRegs = RBM_NONE;
            _liveRegs = RBM_NONE;
        }

        public readonly regMaskTP LiveRegs => _liveRegs;

        public bool Visit(int variableIndex)
        {
            if (!_linearScan.getTrackedLocal(variableIndex).lvLRACandidate)
            {
                return true;
            }

            var trackedIndex = checked((uint)variableIndex);
            var interval = _linearScan.getIntervalForLocalVar(trackedIndex);
            var nextReference = interval.getNextRefPosition();
            assert((nextReference is not null) || interval.isWriteThru);

            // EH edges cannot acquire a join reload, and artificial liveness has no use to free a home.
            var leaveOnStack = interval.isWriteThru &&
                ((_predecessorNumber == 0) || (nextReference is null) ||
                 RefTypeIsDef(nextReference.refType) || _blockInfo[_currentBlock.bbNum].hasEHPred);

            regNumber targetReg;
            if (!_linearScan._allocationPassComplete)
            {
                targetReg = leaveOnStack ? REG_STK : _linearScan.getVarReg(_predecessorMap, trackedIndex);
                if (Configuration.DEBUG)
                {
                    var rotated = _linearScan.rotateBlockStartLocation(interval, targetReg, ~_liveRegs | _inactiveRegs);
                    if (rotated != targetReg)
                    {
                        targetReg = rotated;
                        _linearScan.setIntervalAsSplitMinimal(interval);
                    }
                }
                _linearScan.setVarReg(_inMap, trackedIndex, targetReg);
            }
            else
            {
                targetReg = _linearScan.getVarReg(_inMap, trackedIndex);
                // A later predecessor spill invalidates the original home, except for a copyReg
                // whose reference does not itself record that home.
                if (targetReg != REG_STK)
                {
                    if (_linearScan.getVarReg(_predecessorMap, trackedIndex) != REG_STK)
                    {
                        assert((_linearScan.getVarReg(_predecessorMap, trackedIndex) == targetReg) ||
                            ((_linearScan._lsraStressMask & 0x300) == 0x200));
                    }
                    else
                    {
                        var incomingReference = nextReference;
                        if (incomingReference is null)
                        {
                            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.ResolutionOfAPredecessorSpilledRegisterRequiresANextReference
    );
                            return false;
                        }
                        if (!incomingReference.copyReg)
                        {
                            _linearScan.setVarReg(_inMap, trackedIndex, REG_STK);
                            targetReg = REG_STK;
                        }
                    }
                }
            }

            if (interval.physReg == targetReg)
            {
                if (interval.isActive)
                {
                    assert(targetReg != REG_STK);
                    assert((interval.assignedReg is not null) &&
                        (interval.assignedReg.regNum == targetReg) &&
                        ReferenceEquals(interval.assignedReg.assignedInterval, interval));
                    _liveRegs |= LinearScan.blockLiveRegMask(targetReg, interval.registerType);
                    return true;
                }
            }
            else if (interval.physReg != REG_NA)
            {
                // A non-layout predecessor can supply a different register from the prior block.
                if ((targetReg != REG_STK) || leaveOnStack)
                {
                    if ((interval.assignedReg is not null) &&
                        ReferenceEquals(interval.assignedReg.assignedInterval, interval))
                    {
                        interval.isActive = false;
                        _linearScan.unassignPhysReg(_linearScan.getRegisterRecord(interval.physReg), (RefPosition?)null);
                    }
                    else
                    {
                        interval.physReg = REG_NA;
                    }
                }
                else if (!_linearScan._allocationPassComplete)
                {
                    targetReg = interval.physReg;
                    interval.isActive = true;
                    _liveRegs |= LinearScan.blockLiveRegMask(targetReg, interval.registerType);
                    if (Configuration.DEBUG)
                    {
                        _inactiveRegs |= regMaskTP.CreateFromRegNum(targetReg, genSingleTypeRegMask(targetReg));
                    }
                    _linearScan.setVarReg(_inMap, trackedIndex, targetReg);
                }
                else
                {
                    interval.physReg = REG_NA;
                }
            }

            if (targetReg != REG_STK)
            {
                var targetRecord = _linearScan.getRegisterRecord(targetReg);
                _liveRegs |= LinearScan.blockLiveRegMask(targetReg, interval.registerType);
                if (!_linearScan._allocationPassComplete)
                {
                    _linearScan.updateNextIntervalRef(targetReg, interval);
                    _linearScan.updateSpillCost(targetReg, interval);
                }
                if (!interval.isActive)
                {
                    interval.isActive = true;
                    interval.physReg = targetReg;
                    interval.assignedReg = targetRecord;
                }
                if (!ReferenceEquals(targetRecord.assignedInterval, interval))
                {
                    if (Configuration.TARGET_ARM && (interval.registerType is TYP_DOUBLE) &&
                        ((targetRecord.assignedInterval is null) ||
                         (targetRecord.assignedInterval.registerType is TYP_FLOAT)))
                    {
                        assert(genIsValidDoubleReg(targetReg));
                        _linearScan.unassignIntervalBlockStart(_linearScan.getSecondHalfRegRec(targetRecord),
                            _linearScan._allocationPassComplete ? null : _inMap);
                    }
                    if (Configuration.TARGET_ARM && (interval.registerType is TYP_FLOAT) &&
                        (targetRecord.assignedInterval?.registerType is TYP_DOUBLE))
                    {
                        var otherHalf = _linearScan.findAnotherHalfRegRec(targetRecord);
                        _liveRegs &= ~regMaskTP.CreateFromRegNum(otherHalf.regNum,
                            genSingleTypeRegMask(otherHalf.regNum));
                    }
                    _linearScan.unassignIntervalBlockStart(targetRecord,
                        _linearScan._allocationPassComplete ? null : _inMap);
                    _linearScan.assignPhysReg(targetRecord, interval);
                }
                if ((interval.recentRefPosition is not null) &&
                    !interval.recentRefPosition.copyReg &&
                    (interval.recentRefPosition.registerAssignment != genSingleTypeRegMask(targetReg)))
                {
                    var next = interval.getNextRefPosition();
                    if (next is null)
                    {
                        FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.OutOfOrderIntervalRequiresANextReference
    );
                        return false;
                    }
                    next.outOfOrder = true;
                }
            }
            return true;
        }
    }

    private void requireLocalBlockLocations()
    {
        if (!_enregisterLocalVars)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.LocalBlockLocationsRequireEnregisteredLocals
    );
            return;
        }
    }

    private void processBlockEndAllocationWithLocals(BasicBlock currentBlock)
    {
        requireLocalBlockLocations();
        markBlockVisited(currentBlock);
        processBlockEndLocations(currentBlock);

        // A final block-boundary reference can require an out-map even when there is no next block.
        var nextBlock = getNextBlock();
        if (nextBlock is not null)
        {
            processBlockStartLocations(nextBlock);
        }
    }

    private void processBlockEndLocations(BasicBlock currentBlock)
    {
        requireLocalBlockLocations();
        assert(currentBlock.bbNum == _currentBlockNumber);
        var outMap = getOutVarToRegMap(_currentBlockNumber);
        if (outMap is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.BlockLocationsRequireAnOutgoingVariableMap
    );
            return;
        }
        VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVars,
            VarSetOps.Intersection(_compiler, _registerCandidateVars, currentBlock.bbLiveOut));
        if (extendLifetimes())
        {
            VarSetOps.Assign(_compiler, ref _currentLiveVars, _registerCandidateVars);
        }
        var visitor = new BlockEndLocationsVisitor(this, outMap);
        _ = VarSetOps.VisitBits(_compiler, _currentLiveVars, ref visitor);
        // Native LSRA_EVENT_END_BB has no diagnostic output.
    }

    private void processBlockStartLocations(BasicBlock currentBlock)
    {
        requireLocalBlockLocations();
        var blockInfo = _blockInfo;
        if (blockInfo is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.BlockLocationsRequireInitializedLSRABlockInformation
    );
            return;
        }
        var predecessorNumber = blockInfo[currentBlock.bbNum].predBBNum;
        var inMap = getInVarToRegMap(checked((uint)currentBlock.bbNum));
        if (inMap is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.BlockLocationsRequireAnIncomingVariableMap
    );
            return;
        }
        var predecessorMap = inMap;
        if (predecessorNumber != 0)
        {
            predecessorMap = getOutVarToRegMap(predecessorNumber);
            if (predecessorMap is null)
            {
                FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.BlockLocationsRequireAPredecessorOutgoingMap
    );
                return;
            }
        }

        // EH entries have no register predecessor; their initialized map starts on the stack.
        if (predecessorNumber == 0)
        {
            if (Configuration.DEBUG)
            {
                if (blockInfo[currentBlock.bbNum].hasEHBoundaryIn || !_allocationPassComplete)
                {
                    for (var index = 0; index < _compiler.lvaTrackedCount; index++)
                    {
                        if (!extendLifetimes() || VarSetOps.IsMember(_compiler, currentBlock.bbLiveIn, index))
                        {
                            assert((regNumber)inMap[index] == REG_STK);
                        }
                    }
                }
            }
        }

        VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVars,
            VarSetOps.Intersection(_compiler, _registerCandidateVars, currentBlock.bbLiveIn));
        if (extendLifetimes())
        {
            VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVars, _registerCandidateVars);
        }
        var visitor = new BlockStartLocationsVisitor(
            this,
            currentBlock,
            blockInfo,
            predecessorNumber,
            inMap,
            predecessorMap);
        _ = VarSetOps.VisitBits(_compiler, _currentLiveVars, ref visitor);
        var liveRegs = visitor.LiveRegs;

        if (!_allocationPassComplete)
        {
            resetRegStateWithLocals();
            setRegsInUseAtBlockStart(liveRegs);
        }
        if (Configuration.TARGET_ARM)
        {
            for (var register = TargetRuntimeMetadata.FirstRegister; (int)register < _availableRegCount; register++)
            {
                var record = getRegisterRecord(register);
                var assigned = record.assignedInterval;
                if (!liveRegs.IsSet(register))
                {
                    makeRegAvailable(register, record.registerType);
                    if (assigned is not null)
                    {
                        assert(assigned.isLocalVar || assigned.isConstant || assigned.IsUpperVector());
                        if (!assigned.isConstant && ReferenceEquals(assigned.assignedReg, record))
                        {
                            assigned.isActive = false;
                            if (assigned.getNextRefPosition() is null)
                            {
                                unassignPhysReg(record, (RefPosition?)null);
                            }
                            if (!assigned.IsUpperVector())
                            {
                                setVarReg(inMap, assigned.getVarIndex(_compiler), REG_STK);
                            }
                        }
                        else
                        {
                            clearAssignedInterval(record, assigned.registerType);
                        }

                        assigned = record.assignedInterval ?? assigned;
                        if (assigned.registerType is TYP_DOUBLE)
                        {
                            assert(genIsValidDoubleReg(register));
                            register++;
                            makeRegAvailable(register, record.registerType);
                        }
                    }
                }
                else if ((assigned is not null) && (assigned.registerType is TYP_DOUBLE))
                {
                    assert(genIsValidDoubleReg(register));
                    register++;
                }
            }
        }
        else
        {
            var deadCandidates = ~liveRegs & _actualRegistersMask;
            handleDeadBlockCandidates(deadCandidates.Lower, REG_LOW_BASE, inMap);
            if (Configuration.HAS_MORE_THAN_64_REGISTERS)
            {
                handleDeadBlockCandidates(deadCandidates.Upper, REG_HIGH_BASE, inMap);
            }
        }
    }

    private static regMaskTP blockLiveRegMask(regNumber register, RegisterType registerType)
    {
        if (Configuration.TARGET_ARM && (registerType is TYP_FLOAT or TYP_DOUBLE))
        {
            return register.GetFloatMask(registerType);
        }
        return regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
    }
}
