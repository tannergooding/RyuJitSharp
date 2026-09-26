// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private regMaskTP _regsInUseNextLocation;
    private LsraLocation _currentAllocationLocation;

    private void unassignPhysRegNoSpill(RegRecord regRecord)
    {
        var assignedInterval = regRecord.assignedInterval
            ?? throw new FatalJitException("Cannot unassign a physical register without an interval.");
        assert(assignedInterval.isActive);
        if (!assignedInterval.isActive)
        {
            throw new FatalJitException("A no-spill unassignment requires an active interval.");
        }

        assignedInterval.isActive = false;
        unassignPhysReg(regRecord, (RefPosition?)null);
        assignedInterval.isActive = true;
    }

    private void freeRegister(RegRecord regRecord)
    {
        var assignedInterval = regRecord.assignedInterval;
        makeRegisterTypeAvailable(regRecord.regNum, regRecord.registerType);
        clearSpillCost(regRecord.regNum, regRecord.registerType);
        makeRegisterInactive(regRecord);

        if (assignedInterval is not null)
        {
            var nextRefPosition = assignedInterval.getNextRefPosition();
            if (!assignedInterval.isConstant &&
                ((nextRefPosition is null) || RefTypeIsDef(nextRefPosition.refType)))
            {
                unassignPhysReg(regRecord, (RefPosition?)null);
            }
        }
    }

    private void makeRegisterTypeAvailable(regNumber register, RegisterType registerType)
    {
        makeRegAvailable(register, registerType);
        if (registerType is TYP_FLOAT or TYP_DOUBLE)
        {
            makeRegAvailable(register, registerType is TYP_FLOAT ? TYP_DOUBLE : TYP_FLOAT);
        }
    }

    private void makeRegisterInactive(RegRecord regRecord)
    {
        var assignedInterval = regRecord.assignedInterval;
        if ((assignedInterval is not null) && (assignedInterval.physReg == regRecord.regNum))
        {
            assignedInterval.isActive = false;
            if (assignedInterval.isConstant)
            {
                clearNextIntervalRef(regRecord.regNum, assignedInterval.registerType);
            }
        }
    }

    private void freeRegistersSingleType(SingleTypeRegSet registersToFree, int registerBase)
    {
        var remaining = unchecked((ulong)(long)registersToFree);
        while (remaining != 0)
        {
            var registerOffset = BitOperations.TrailingZeroCount(remaining);
            remaining &= remaining - 1;
            var register = (regNumber)(registerOffset + registerBase);
            freeRegister(getRegisterRecord(register));
        }
    }

    private void freeRegisters(regMaskTP registersToFree)
    {
        if (registersToFree.IsEmpty)
        {
            return;
        }

#if DEBUG
        dumpLsraAllocationEvent(LsraDumpEvent.FREE_REGS, null, null);
#endif
        makeRegsAvailable(registersToFree);

        freeRegistersSingleType(registersToFree.Lower, REG_LOW_BASE);
#if HAS_MORE_THAN_64_REGISTERS
        freeRegistersSingleType(registersToFree.Upper, REG_HIGH_BASE);
#endif
    }

    private void makeRegsAvailable(regMaskTP registers)
    {
        _availableRegs[(int)TYP_INT] |= registers.IntRegSet;
        _availableRegs[(int)TYP_FLOAT] |= registers.FltRegSet;
        _availableRegs[(int)TYP_DOUBLE] |= registers.FltRegSet;
#if FEATURE_MASKED_HW_INTRINSICS
        _availableRegs[(int)TYP_MASK] |= registers.MskRegSet;
#endif
    }

    private void resetAllRegistersState()
    {
        assert(!_enregisterLocalVars);
        if (_enregisterLocalVars)
        {
            throw new FatalJitException("Minimal register-state reset cannot run with enregistered locals.");
        }

        initializeAvailableRegs();
        _registersWithConstants = RBM_NONE;
        clearAllNextIntervalRef();
        clearAllSpillCost();
        for (var index = 0; (int)_regIndices[index] < _availableRegCount; index++)
        {
            var register = getRegisterRecord(_regIndices[index]);
            assert((register.assignedInterval is null) || register.assignedInterval.isConstant);
            register.assignedInterval = null;
        }
    }

    private void updateRegsFreeBusyState(
        RefPosition refPosition,
        RegisterType registerType,
        SingleTypeRegSet registersBusy,
        ref regMaskTP registersToFree,
        ref regMaskTP delayRegistersToFree,
        Interval? interval = null,
        regNumber assignedRegister = REG_NA)
    {
        var busyMask = createRegisterMask(registersBusy, registerType);
        _regsInUseThisLocation |= busyMask;
        if (refPosition.lastUse)
        {
            if (refPosition.delayRegFree)
            {
#if DEBUG
                dumpLsraAllocationEvent(
                    LsraDumpEvent.LAST_USE_DELAYED, interval, refPosition, assignedRegister);
#endif
                delayRegistersToFree |= busyMask;
                _regsInUseNextLocation |= busyMask;
            }
            else
            {
#if DEBUG
                dumpLsraAllocationEvent(LsraDumpEvent.LAST_USE, interval, refPosition, assignedRegister);
#endif
                registersToFree |= busyMask;
            }
        }
        else if (refPosition.delayRegFree)
        {
            _regsInUseNextLocation |= busyMask;
        }
    }

    private static regMaskTP createRegisterMask(SingleTypeRegSet registers, RegisterType registerType)
    {
#if HAS_MORE_THAN_64_REGISTERS
        return registerType is TYP_MASK
            ? new regMaskTP(SRBM_NONE, registers)
            : new regMaskTP(registers);
#else
        return new regMaskTP(registers);
#endif
    }

    private void verifyFreeRegisters(regMaskTP registersToFree)
    {
#if DEBUG
        for (var index = 0; (int)_regIndices[index] < _availableRegCount; index++)
        {
            var register = _regIndices[index];
            var regRecord = getRegisterRecord(register);
            if (!isInitiallyAvailable(regRecord) || registersToFree.IsSet(register))
            {
                continue;
            }

            var assignedInterval = regRecord.assignedInterval;
            if (assignedInterval is null)
            {
                assert(isRegAvailable(register, regRecord.registerType));
                assert(!isRegConstant(register, regRecord.registerType) || spillAlways());
                assert(getNextIntervalRef(register, regRecord.registerType) == MaxLocation);
                assert(_spillCost[(int)register] == 0);
                continue;
            }

            var isAssignedRegister = assignedInterval.physReg == register;
            var recentRefPosition = assignedInterval.recentRefPosition;
            if (recentRefPosition is null)
            {
                continue;
            }

            if (recentRefPosition.refType is RefType.RefTypeExpUse)
            {
                continue;
            }

            if (recentRefPosition.copyReg || recentRefPosition.moveReg)
            {
                continue;
            }

            assert(assignedInterval.isConstant == isRegConstant(register, assignedInterval.registerType));
            if (assignedInterval.isActive)
            {
                if (!isAssignedToInterval(assignedInterval, regRecord))
                {
                    var sanityCheck = assignedInterval.isLocalVar;
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                    sanityCheck |= assignedInterval.IsUpperVector() &&
                        (recentRefPosition.refType is RefType.RefTypeUpperVectorSave or RefType.RefTypeUpperVectorRestore);
#endif
                    assert(sanityCheck);
                }

                if (isAssignedRegister)
                {
                    assert(getNextIntervalRef(register, assignedInterval.registerType) ==
                        assignedInterval.getNextRefLocation());
                    assert(!isRegAvailable(register, assignedInterval.registerType));
                    assert(_spillCost[(int)register] == getSpillWeight(regRecord));
                }
                else
                {
                    assert((getNextIntervalRef(register, assignedInterval.registerType) == MaxLocation) ||
                        isRegBusy(register, assignedInterval.registerType));
                }
            }
            else if ((assignedInterval.physReg == register) && !assignedInterval.isConstant)
            {
                assert(getNextIntervalRef(register, assignedInterval.registerType) ==
                    assignedInterval.getNextRefLocation());
            }
            else
            {
                assert(getNextIntervalRef(register, assignedInterval.registerType) == MaxLocation);
                assert(isRegAvailable(register, assignedInterval.registerType));
                assert(_spillCost[(int)register] == 0);
            }
        }
#endif
    }

    private bool isInitiallyAvailable(RegRecord regRecord)
    {
        var registerMask = genSingleTypeRegMask(regRecord.regNum);
        return regRecord.registerType switch
        {
            TYP_INT => (_availableIntRegs & registerMask) != SRBM_NONE,
            TYP_FLOAT => (_availableFloatRegs & registerMask) != SRBM_NONE,
            TYP_DOUBLE => (_availableDoubleRegs & registerMask) != SRBM_NONE,
#if FEATURE_MASKED_HW_INTRINSICS
            TYP_MASK => (_availableMaskRegs & registerMask) != SRBM_NONE,
#endif
            _ => false,
        };
    }

    private bool isRegAvailable(regNumber register, RegisterType registerType) =>
        (_availableRegs[(int)regType(registerType)] & genSingleTypeRegMask(register)) != SRBM_NONE;

    private bool isRegConstant(regNumber register, RegisterType registerType) =>
        _registersWithConstants.IsSet(register);

    private bool isAssignedToInterval(Interval interval, RegRecord regRecord) =>
        ReferenceEquals(interval.assignedReg, regRecord);

    private weight_t getSpillWeight(RegRecord regRecord)
    {
        var interval = regRecord.assignedInterval
            ?? throw new FatalJitException("Spill-weight calculation requires an assigned interval.");
        var recentRefPosition = interval.recentRefPosition
            ?? throw new FatalJitException("Spill-weight calculation requires a recent reference.");
        // Native currentLoc retains the interval-building cursor during allocation;
        // it is not the traversal's currentLocation, which can still hold delayed uses.
        assert(!isRefPositionActive(recentRefPosition, _referenceBuildLocation));
        return getWeight(recentRefPosition);
    }

#if DEBUG
    private bool spillAlways() => (_lsraStressMask & 0x800) != 0;
#else
    private bool spillAlways() => false;
#endif
}
