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
        var assignedInterval = regRecord.assignedInterval;
        if (assignedInterval is null)
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage380
            );
            return;
        }
        assert(assignedInterval.isActive);
        if (!assignedInterval.isActive)
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage392
            );
            return;
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
                if (Configuration.TARGET_ARM)
                {
                    assert(assignedInterval.registerType is not TYP_DOUBLE ||
                        genIsValidDoubleReg(regRecord.regNum));
                }
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
            if (Configuration.TARGET_ARM &&
                getRegisterRecord(register).assignedInterval?.registerType is TYP_DOUBLE)
            {
                assert(genIsValidDoubleReg(register));
                remaining &= ~unchecked((ulong)genSingleTypeRegMask(register + 1));
            }
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
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            freeRegistersSingleType(registersToFree.Upper, REG_HIGH_BASE);
        }
    }

    private void makeRegsAvailable(regMaskTP registers)
    {
        _availableRegs[(int)TYP_INT] |= registers.IntRegSet;
        _availableRegs[(int)TYP_FLOAT] |= registers.FltRegSet;
        _availableRegs[(int)TYP_DOUBLE] |= registers.FltRegSet;
        if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            _availableRegs[(int)TYP_MASK] |= registers.MskRegSet;
        }
    }

    private void resetAllRegistersState()
    {
        assert(!_enregisterLocalVars);
        if (_enregisterLocalVars)
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage393
            );
            return;
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
        Interval? interval = null)
    {
        updateRegsFreeBusyState(refPosition, registerType, registersBusy, ref registersToFree, ref delayRegistersToFree, interval, REG_NA);
    }

    private void updateRegsFreeBusyState(
        RefPosition refPosition,
        RegisterType registerType,
        SingleTypeRegSet registersBusy,
        ref regMaskTP registersToFree,
        ref regMaskTP delayRegistersToFree,
        Interval? interval,
        regNumber assignedRegister)
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
        return Configuration.HAS_MORE_THAN_64_REGISTERS && registerType is TYP_MASK
            ? new regMaskTP(SRBM_NONE, registers)
            : new regMaskTP(registers);
    }

    private void verifyFreeRegisters(regMaskTP registersToFree)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

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
            if (recentRefPosition is not null)
            {
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
                        sanityCheck |= Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE &&
                            assignedInterval.IsUpperVector() &&
                            (recentRefPosition.refType is RefType.RefTypeUpperVectorSave or RefType.RefTypeUpperVectorRestore);
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

            // A double occupies this register and the next floating-point register.
            if (Configuration.TARGET_ARM && assignedInterval.registerType is TYP_DOUBLE)
            {
                index++;
            }
        }
    }

    private bool isInitiallyAvailable(RegRecord regRecord)
    {
        var registerMask = genSingleTypeRegMask(regRecord.regNum);
        return regRecord.registerType switch {
            TYP_INT => (_availableIntRegs & registerMask) != SRBM_NONE,
            TYP_FLOAT => (_availableFloatRegs & registerMask) != SRBM_NONE,
            TYP_DOUBLE => (_availableDoubleRegs & registerMask) != SRBM_NONE,
            TYP_MASK when Configuration.FEATURE_MASKED_HW_INTRINSICS =>
                (_availableMaskRegs & registerMask) != SRBM_NONE,
            _ => false,
        };
    }

    private bool isRegAvailable(regNumber register, RegisterType registerType) =>
        (_availableRegs[(int)regType(registerType)] & genSingleTypeRegMask(register)) != SRBM_NONE;

    private bool isRegConstant(regNumber register, RegisterType registerType) =>
        _registersWithConstants.IsSet(register);

    private bool isAssignedToInterval(Interval interval, RegRecord regRecord)
    {
        if (ReferenceEquals(interval.assignedReg, regRecord))
        {
            return true;
        }
        if (Configuration.TARGET_ARM && interval.registerType is TYP_DOUBLE)
        {
            return isSecondHalfReg(regRecord, interval);
        }
        return false;
    }

    private weight_t getSpillWeight(RegRecord regRecord)
    {
        var interval = regRecord.assignedInterval;
        if (interval is null)
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage394
            );
            return default!;
        }
        var recentRefPosition = interval.recentRefPosition;
        if (recentRefPosition is null)
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage395
            );
            return default!;
        }
        // Native currentLoc retains the interval-building cursor during allocation;
        // it is not the traversal's currentLocation, which can still hold delayed uses.
        assert(!isRefPositionActive(recentRefPosition, _referenceBuildLocation));
        return getWeight(recentRefPosition);
    }

    private bool spillAlways() => Configuration.DEBUG && ((_lsraStressMask & 0x800) != 0);
}
