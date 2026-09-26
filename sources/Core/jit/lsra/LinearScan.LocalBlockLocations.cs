// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void requireWindowsAmd64BlockLocations()
    {
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
        if (!_enregisterLocalVars)
        {
            throw new FatalJitException("Local block locations require enregistered locals.");
        }
#else
        throw new FatalJitException("Local block locations are not implemented outside Windows AMD64.");
#endif
    }

    private void processBlockEndAllocationWithLocals(BasicBlock currentBlock)
    {
        requireWindowsAmd64BlockLocations();
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
        requireWindowsAmd64BlockLocations();
        assert(currentBlock.bbNum == _currentBlockNumber);
        var outMap = getOutVarToRegMap(_currentBlockNumber)
            ?? throw new FatalJitException("Block locations require an outgoing variable map.");
        VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVars,
            VarSetOps.Intersection(_compiler, _registerCandidateVars, currentBlock.bbLiveOut));
#if DEBUG
        if (extendLifetimes())
        {
            VarSetOps.Assign(_compiler, ref _currentLiveVars, _registerCandidateVars);
        }
#endif
        _ = VarSetOps.VisitBits(_compiler, _currentLiveVars, variableIndex =>
        {
            var interval = getIntervalForLocalVar(checked((uint)variableIndex));
            if (interval.isActive)
            {
                assert(interval.physReg is not REG_NA and not REG_STK);
                setVarReg(outMap, checked((uint)variableIndex), interval.physReg);
            }
            else
            {
                outMap[variableIndex] = REG_STK;
            }
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
            assert(!Compiler.varTypeNeedsPartialCalleeSave(interval.registerType) || !interval.isPartiallySpilled);
#endif
            return true;
        });
        // Native LSRA_EVENT_END_BB has no diagnostic output.
    }

    private void processBlockStartLocations(BasicBlock currentBlock)
    {
        requireWindowsAmd64BlockLocations();
        var blockInfo = _blockInfo
            ?? throw new FatalJitException("Block locations require initialized LSRA block information.");
        var predecessorNumber = blockInfo[currentBlock.bbNum].predBBNum;
        var inMap = getInVarToRegMap(checked((uint)currentBlock.bbNum))
            ?? throw new FatalJitException("Block locations require an incoming variable map.");
        var predecessorMap = predecessorNumber == 0 ? inMap : getOutVarToRegMap(predecessorNumber)
            ?? throw new FatalJitException("Block locations require a predecessor outgoing map.");

        // EH entries have no register predecessor; their initialized map starts on the stack.
        if (predecessorNumber == 0)
        {
#if DEBUG
            if (blockInfo[currentBlock.bbNum].hasEHBoundaryIn || !_allocationPassComplete)
            {
                for (var index = 0; index < _compiler.lvaTrackedCount; index++)
                {
                    if (!extendLifetimes() || VarSetOps.IsMember(_compiler, currentBlock.bbLiveIn, index))
                    {
                        assert(inMap[index] == REG_STK);
                    }
                }
            }
#endif
        }

        VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVars,
            VarSetOps.Intersection(_compiler, _registerCandidateVars, currentBlock.bbLiveIn));
#if DEBUG
        if (extendLifetimes())
        {
            VarSetOps.AssignNoCopy(_compiler, ref _currentLiveVars, _registerCandidateVars);
        }
        var inactiveRegs = RBM_NONE;
#endif
        var liveRegs = RBM_NONE;
        _ = VarSetOps.VisitBits(_compiler, _currentLiveVars, variableIndex =>
        {
            if (!getTrackedLocal(variableIndex).lvLRACandidate)
            {
                return true;
            }

            var trackedIndex = checked((uint)variableIndex);
            var interval = getIntervalForLocalVar(trackedIndex);
            var nextReference = interval.getNextRefPosition();
            assert((nextReference is not null) || interval.isWriteThru);

            // EH edges cannot acquire a join reload, and artificial liveness has no use to free a home.
            var leaveOnStack = interval.isWriteThru &&
                ((predecessorNumber == 0) || (nextReference is null) ||
                 RefTypeIsDef(nextReference.refType) || blockInfo[currentBlock.bbNum].hasEHPred);

            regNumber targetReg;
            if (!_allocationPassComplete)
            {
                targetReg = leaveOnStack ? REG_STK : getVarReg(predecessorMap, trackedIndex);
#if DEBUG
                var rotated = rotateBlockStartLocation(interval, targetReg, ~liveRegs | inactiveRegs);
                if (rotated != targetReg)
                {
                    targetReg = rotated;
                    setIntervalAsSplitMinimal(interval);
                }
#endif
                setVarReg(inMap, trackedIndex, targetReg);
            }
            else
            {
                targetReg = getVarReg(inMap, trackedIndex);
                // A later predecessor spill invalidates the original home, except for a copyReg
                // whose reference does not itself record that home.
                if (targetReg != REG_STK)
                {
                    if (getVarReg(predecessorMap, trackedIndex) != REG_STK)
                    {
#if DEBUG
                        assert((getVarReg(predecessorMap, trackedIndex) == targetReg) ||
                            ((_lsraStressMask & 0x300) == 0x200));
#endif
                    }
                    else
                    {
                        var incomingReference = nextReference
                            ?? throw new FatalJitException(
                                "Resolution of a predecessor-spilled register requires a next reference.");
                        if (!incomingReference.copyReg)
                        {
                            setVarReg(inMap, trackedIndex, REG_STK);
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
                    liveRegs |= regMaskTP.CreateFromRegNum(targetReg, genSingleTypeRegMask(targetReg));
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
                        unassignPhysReg(getRegisterRecord(interval.physReg), (RefPosition?)null);
                    }
                    else
                    {
                        interval.physReg = REG_NA;
                    }
                }
                else if (!_allocationPassComplete)
                {
                    targetReg = interval.physReg;
                    interval.isActive = true;
                    liveRegs |= regMaskTP.CreateFromRegNum(targetReg, genSingleTypeRegMask(targetReg));
#if DEBUG
                    inactiveRegs |= regMaskTP.CreateFromRegNum(targetReg, genSingleTypeRegMask(targetReg));
#endif
                    setVarReg(inMap, trackedIndex, targetReg);
                }
                else
                {
                    interval.physReg = REG_NA;
                }
            }

            if (targetReg != REG_STK)
            {
                var targetRecord = getRegisterRecord(targetReg);
                liveRegs |= regMaskTP.CreateFromRegNum(targetReg, genSingleTypeRegMask(targetReg));
                if (!_allocationPassComplete)
                {
                    updateNextIntervalRef(targetReg, interval);
                    updateSpillCost(targetReg, interval);
                }
                if (!interval.isActive)
                {
                    interval.isActive = true;
                    interval.physReg = targetReg;
                    interval.assignedReg = targetRecord;
                }
                if (!ReferenceEquals(targetRecord.assignedInterval, interval))
                {
                    unassignIntervalBlockStart(targetRecord, _allocationPassComplete ? null : inMap);
                    assignPhysReg(targetRecord, interval);
                }
                if ((interval.recentRefPosition is not null) &&
                    !interval.recentRefPosition.copyReg &&
                    (interval.recentRefPosition.registerAssignment != genSingleTypeRegMask(targetReg)))
                {
                    var next = interval.getNextRefPosition()
                        ?? throw new FatalJitException("An out-of-order interval requires a next reference.");
                    next.outOfOrder = true;
                }
            }
            return true;
        });

        if (!_allocationPassComplete)
        {
            resetRegStateWithLocals();
            setRegsInUseAtBlockStart(liveRegs);
        }
        var deadCandidates = ~liveRegs & _actualRegistersMask;
        handleDeadBlockCandidates(deadCandidates.Lower, REG_LOW_BASE, inMap);
#if HAS_MORE_THAN_64_REGISTERS
        handleDeadBlockCandidates(deadCandidates.Upper, REG_HIGH_BASE, inMap);
#endif
    }
}
