// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private VARSET_TP? _splitOrSpilledVars;

    private regMaskTP _registersWithConstants;
    private LsraLocation? _currentBlockStartLocation;

    private bool isAssigned(RegRecord regRecord, RegisterType newRegisterType)
    {
        assert(newRegisterType is not TYP_UNDEF and not TYP_STRUCT);
        return regRecord.assignedInterval is not null;
    }

    private void checkAndAssignInterval(RegRecord regRecord, Interval interval)
    {
        var assignedInterval = regRecord.assignedInterval;
        if ((assignedInterval is not null) && !ReferenceEquals(assignedInterval, interval))
        {
            if (ReferenceEquals(assignedInterval.assignedReg, regRecord))
            {
                assert(!assignedInterval.isActive);
                assignedInterval.physReg = REG_NA;
            }

            unassignPhysReg(regRecord, (RefPosition?)null);
        }

        updateAssignedInterval(regRecord, interval);
    }

    private void assignPhysReg(RegRecord regRecord, Interval interval)
    {
        var codeGen = _compiler.codeGen
            ?? throw new FatalJitException("Register assignment requires initialized codegen state.");
        codeGen.RegSet.rsSetRegsModified(
            regMaskTP.CreateFromRegNum(regRecord.regNum, genSingleTypeRegMask(regRecord.regNum)), true);

        interval.assignedReg = regRecord;
        checkAndAssignInterval(regRecord, interval);
        interval.physReg = regRecord.regNum;
        interval.isActive = true;
        if (interval.isLocalVar)
        {
            interval.updateRegisterPreferences(genSingleTypeRegMask(regRecord.regNum));
        }
    }

    private void clearAssignedInterval(RegRecord regRecord)
    {
        regRecord.assignedInterval = null;
        clearNextIntervalRef(regRecord.regNum, regRecord.registerType);
        clearSpillCost(regRecord.regNum, regRecord.registerType);
        clearConstantReg(regRecord.regNum);
    }

    private void updateAssignedInterval(RegRecord regRecord, Interval interval)
    {
        regRecord.assignedInterval = interval;
        setRegInUse(regRecord.regNum, interval.registerType);
        if (interval.isConstant)
        {
            setConstantReg(regRecord.regNum);
        }
        else
        {
            clearConstantReg(regRecord.regNum);
        }

        updateNextIntervalRef(regRecord.regNum, interval);
        updateSpillCost(regRecord.regNum, interval);
    }

    private void updatePreviousInterval(RegRecord regRecord, Interval? interval)
    {
        regRecord.previousInterval = interval;
    }

    private bool canRestorePreviousInterval(RegRecord regRecord, Interval assignedInterval)
    {
        var previousInterval = regRecord.previousInterval;
        return (previousInterval is not null) &&
            !ReferenceEquals(previousInterval, assignedInterval) &&
            ReferenceEquals(previousInterval.assignedReg, regRecord) &&
            (previousInterval.getNextRefPosition() is not null);
    }

    private void checkAndClearInterval(RegRecord regRecord, RefPosition? spillRefPosition)
    {
        var assignedInterval = regRecord.assignedInterval
            ?? throw new FatalJitException("Cannot clear an unassigned physical register.");

        if (spillRefPosition is null)
        {
            if (assignedInterval.physReg == regRecord.regNum)
            {
                assert(!assignedInterval.isActive);
            }
        }
        else
        {
            assert(ReferenceEquals(spillRefPosition.getInterval(), assignedInterval));
        }

        clearAssignedInterval(regRecord);
    }

    private void unassignPhysReg(RegRecord regRecord, RegisterType newRegisterType)
    {
        var assignedInterval = regRecord.assignedInterval;
        if (assignedInterval is not null)
        {
            unassignPhysReg(regRecord, assignedInterval.recentRefPosition);
        }
    }

    private void unassignPhysReg(RegRecord regRecord, RefPosition? spillRefPosition)
    {
        var assignedInterval = regRecord.assignedInterval
            ?? throw new FatalJitException("Cannot unassign a physical register without an interval.");
        assert((spillRefPosition is null) ||
            ReferenceEquals(spillRefPosition.getInterval(), assignedInterval));

        var register = regRecord.regNum;
        var intervalIsAssigned = assignedInterval.physReg == register;

        clearNextIntervalRef(register, assignedInterval.registerType);
        clearSpillCost(register, assignedInterval.registerType);
        checkAndClearInterval(regRecord, spillRefPosition);
        makeRegAvailable(register, assignedInterval.registerType);

        if (!intervalIsAssigned && (assignedInterval.physReg != REG_NA))
        {
            return;
        }

        var nextRefPosition = spillRefPosition?.nextRefPosition;
        assignedInterval.physReg = REG_NA;

        var spill = assignedInterval.isActive && (nextRefPosition is not null);
        if (spill)
        {
            var spillFrom = spillRefPosition
                ?? throw new FatalJitException("An active interval spill requires its last reference.");
            var spillTo = nextRefPosition
                ?? throw new FatalJitException("A spilled interval requires a following reference.");
#if DEBUG
            if (extendLifetimes() && assignedInterval.isLocalVar &&
                RefTypeIsUse(spillFrom.refType) && spillFrom.treeNode is not null &&
                spillFrom.treeNode.AsLclVar().IsLastUse(spillFrom.multiRegIdx))
            {
                assignedInterval.isActive = false;
                spill = false;
                if (spillFrom.nodeLocation <= (_currentBlockStartLocation
                        ?? throw new FatalJitException("LSRA spilling requires the current block start location.")))
                {
                    setInVarRegForBB(_currentBlockNumber, assignedInterval.varNum, REG_STK);
                    if (spillFrom.nextRefPosition is not null)
                    {
                        setIntervalAsSpilled(assignedInterval);
                    }
                }
                else
                {
                    spillFrom.lastUse = true;
                }
            }
            else
#endif
            {
                spillInterval(assignedInterval, spillFrom, spillTo);
            }
        }

        if (nextRefPosition is not null)
        {
            assignedInterval.assignedReg = regRecord;
        }
        else if (canRestorePreviousInterval(regRecord, assignedInterval))
        {
            var previousInterval = regRecord.previousInterval
                ?? throw new FatalJitException("A restorable register has no previous interval.");
            regRecord.assignedInterval = previousInterval;
            regRecord.previousInterval = null;
            if (previousInterval.physReg != register)
            {
                clearNextIntervalRef(register, previousInterval.registerType);
            }
            else
            {
                updateNextIntervalRef(register, previousInterval);
            }

#if DEBUG
            if (VERBOSE)
            {
                dumpRegisterAssignmentRestore(register, spill);
            }
#endif
        }
        else
        {
            clearAssignedInterval(regRecord);
            updatePreviousInterval(regRecord, null);
        }
    }

    private void setRegInUse(regNumber register, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        _availableRegs[(int)registerType] &= ~genSingleTypeRegMask(register);
    }

    private void makeRegAvailable(regNumber register, RegisterType registerType)
    {
        assert(registerType is not TYP_UNDEF and not TYP_STRUCT);
        _availableRegs[(int)registerType] |= genSingleTypeRegMask(register);
    }

    private void setConstantReg(regNumber register)
    {
        _registersWithConstants |=
            regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
    }

    private void clearConstantReg(regNumber register)
    {
        var registerMask = genSingleTypeRegMask(register);
#if HAS_MORE_THAN_64_REGISTERS
        _registersWithConstants = ((int)register < 64)
            ? new regMaskTP(_registersWithConstants.Lower & ~registerMask, _registersWithConstants.Upper)
            : new regMaskTP(_registersWithConstants.Lower, _registersWithConstants.Upper & ~registerMask);
#else
        _registersWithConstants = new regMaskTP(_registersWithConstants.Lower & ~registerMask);
#endif
    }

    internal void setCurrentBlockStartLocation(LsraLocation blockStartLocation)
    {
        _currentBlockStartLocation = blockStartLocation;
    }

    private void setIntervalAsSpilled(Interval interval)
    {
        if (!_enregisterLocalVars)
        {
            interval.isSpilled = true;
            return;
        }

        // Tracked locals may still be added during lowering, so size this set when spilling begins.
        var splitOrSpilledVars = _splitOrSpilledVars ??= VarSetOps.MakeEmpty(_compiler);
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        if (interval.isUpperVector)
        {
            var relatedInterval = interval.relatedInterval
                ?? throw new FatalJitException("An upper-vector interval requires its related local interval.");
            assert(relatedInterval.isLocalVar);
            if (!relatedInterval.isLocalVar)
            {
                throw new FatalJitException("An upper-vector interval must be related to a local interval.");
            }

            interval.isSpilled = true;
            interval = relatedInterval;
            var recentRefPosition = interval.recentRefPosition;
            if (!interval.isSpilled && interval.isActive && recentRefPosition is not null)
            {
                VarSetOps.AddElemD(
                    _compiler, splitOrSpilledVars, checked((int)interval.getVarIndex(_compiler)));
                interval.isSpilled = true;
                var register = interval.physReg;
                _spillCost[(int)register] = getSpillWeight(getRegisterRecord(register));
            }
        }
#endif
        if (interval.isLocalVar)
        {
            var varIndex = interval.getVarIndex(_compiler);
            if (!interval.isSpilled)
            {
                VarSetOps.AddElemD(_compiler, splitOrSpilledVars, checked((int)varIndex));
            }
            else
            {
                assert(VarSetOps.IsMember(_compiler, splitOrSpilledVars, checked((int)varIndex)));
            }
        }

        interval.isSpilled = true;
    }

    private void spillInterval(Interval interval, RefPosition fromRefPosition, RefPosition toRefPosition)
    {
        assert(ReferenceEquals(fromRefPosition.getInterval(), interval));
        assert(ReferenceEquals(toRefPosition.getInterval(), interval));
        assert(ReferenceEquals(fromRefPosition.nextRefPosition, toRefPosition));

        if (!fromRefPosition.lastUse)
        {
            if (fromRefPosition.RegOptional() && !(interval.isLocalVar && fromRefPosition.IsActualRef()))
            {
                fromRefPosition.registerAssignment = SRBM_NONE;
            }
            else
            {
                fromRefPosition.spillAfter = true;
            }
        }

        if (interval.isSingleDef)
        {
            var firstRefPosition = interval.firstRefPosition
                ?? throw new FatalJitException("A single-definition interval must have a first reference.");
            if (RefTypeIsDef(firstRefPosition.refType))
            {
                firstRefPosition.singleDefSpill = true;
            }
        }

#if DEBUG
        if (VERBOSE)
        {
            dumpRegisterAssignmentSpill(interval);
        }
#endif

#if TRACK_LSRA_STATS
        updateLsraStat(LsraStat.STAT_SPILL, fromRefPosition.bbNum);
#endif

        interval.isActive = false;
        setIntervalAsSpilled(interval);

        if (fromRefPosition.nodeLocation <= (_currentBlockStartLocation
                ?? throw new FatalJitException("LSRA spilling requires the current block start location.")))
        {
            assert(interval.isLocalVar);
            if (!interval.isLocalVar)
            {
                throw new FatalJitException("Only local-variable intervals can be live on block entry.");
            }

            setInVarRegForBB(_currentBlockNumber, interval.varNum, REG_STK);
        }
    }

#if DEBUG
    private void dumpRegisterAssignmentSpill(Interval interval)
    {
        var register = interval.assignedReg
            ?? throw new FatalJitException("A spilled interval must retain its assigned register.");
        initializeAllocationDumpFormat();
        _allocationDumpRegisters |=
            regMaskTP.CreateFromRegNum(register.regNum, genSingleTypeRegMask(register.regNum));
        dumpAllocationRegisterTitleIfNeeded();
        dumpRefPositionShort(_activeRefPosition);
        jitprintf($"Spill    {register.regNum.Name.ToUpperInvariant(),-4} ");
        dumpAllocationRegisterRecords();
    }

    private void dumpRegisterAssignmentRestore(regNumber register, bool spilled)
    {
        initializeAllocationDumpFormat();
        _allocationDumpRegisters |= regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
        dumpAllocationRegisterTitleIfNeeded();
        dumpRefPositionShort(_activeRefPosition);
        jitprintf($"{(spilled ? "SRstr" : "Restr")}    {register.Name.ToUpperInvariant(),-4} ");
        dumpAllocationRegisterRecords();
    }
#endif
}
