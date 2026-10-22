// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private RefPosition addKillForRegs(regMaskTP registers, LsraLocation location)
    {
        var codeGen = _compiler.codeGen;
        if (codeGen is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.RegisterKillsRequireInitializedCodegenState
    );
            return default!;
        }
        codeGen.RegSet.rsSetRegsModified(registers, true);

        var killRefPosition = newRefPosition(
            null, location, RefType.RefTypeKill, null, registers.Lower);
        killRefPosition.killedRegisters = registers;

        if (_killHead is null)
        {
            _killHead = killRefPosition;
        }
        else
        {
            var killTail = _killTail;
            if (killTail is null)
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.NonEmptyKillListMustHaveATailReference);
                return default!;
            }
            killTail.nextRefPosition = killRefPosition;
        }

        _killTail = killRefPosition;
        return killRefPosition;
    }

    private void processKills(RefPosition killRefPosition)
    {
        var nextKill = killRefPosition.nextRefPosition;
        var killedRegisters = killRefPosition.getKilledRegisters();

        freeKilledRegisters(killRefPosition, killedRegisters.Lower, nextKill, REG_LOW_BASE);
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            freeKilledRegisters(killRefPosition, killedRegisters.Upper, nextKill, REG_HIGH_BASE);
        }

        _regsBusyUntilKill = Configuration.HAS_MORE_THAN_64_REGISTERS
            ? new regMaskTP(
                _regsBusyUntilKill.Lower & ~killedRegisters.Lower,
                _regsBusyUntilKill.Upper & ~killedRegisters.Upper)
            : new regMaskTP(_regsBusyUntilKill.Lower & ~killedRegisters.Lower);
        dumpKilledRegistersEvent(killRefPosition, killedRegisters);
    }

    private void freeKilledRegisters(
        RefPosition killRefPosition,
        SingleTypeRegSet killedRegisters,
        RefPosition? nextKill,
        int registerBase)
    {
        var remaining = unchecked((ulong)(long)killedRegisters);
        while (remaining != 0)
        {
            var registerOffset = BitOperations.TrailingZeroCount(remaining);
            remaining &= remaining - 1;
            var registerNumber = (regNumber)(registerOffset + registerBase);
            var regRecord = getRegisterRecord(registerNumber);
            var assignedInterval = regRecord.assignedInterval;
            if (assignedInterval is not null)
            {
                var registerType = assignedInterval.registerType;
                unassignPhysReg(regRecord, assignedInterval.recentRefPosition);
                clearConstantReg(regRecord.regNum, registerType);
                makeRegisterTypeAvailable(regRecord.regNum, registerType);
            }

            assert((_nextFixedRef[(int)registerNumber] == killRefPosition.nodeLocation) ||
                ((int)registerNumber >= _availableRegCount));
            var nextRegisterRef = regRecord.recentRefPosition is null
                ? regRecord.firstRefPosition
                : regRecord.recentRefPosition.nextRefPosition;
            updateNextFixedRef(regRecord, nextRegisterRef, nextKill);
        }
    }

    private void spillGCRefs(RefPosition killRefPosition)
    {
        var candidateRegisters = unchecked((ulong)(long)killRefPosition.registerAssignment);
        var killedAny = false;
        while (candidateRegisters != 0)
        {
            var registerOffset = BitOperations.TrailingZeroCount(candidateRegisters);
            candidateRegisters &= candidateRegisters - 1;
            var registerNumber = (regNumber)registerOffset;
            var regRecord = getRegisterRecord(registerNumber);
            var assignedInterval = regRecord.assignedInterval;
            if ((assignedInterval is null) || !assignedInterval.isActive)
            {
                continue;
            }

            var needsKill = varTypeIsGC(assignedInterval.registerType);
            if (!needsKill)
            {
                var recentRefPosition = assignedInterval.recentRefPosition;
                if ((recentRefPosition is not null) && (recentRefPosition.treeNode is not null))
                {
                    needsKill = varTypeIsGC(recentRefPosition.treeNode.Type);
                }
            }

            if (needsKill)
            {
                killedAny = true;
                var registerType = assignedInterval.registerType;
                unassignPhysReg(regRecord, assignedInterval.recentRefPosition);
                makeRegisterTypeAvailable(registerNumber, registerType);
            }
        }

        dumpGCRefSpillEvent(killRefPosition, killedAny);
    }

    private regNumber assignCopyRegMinimal(RefPosition refPosition)
    {
        var currentInterval = refPosition.getInterval();
        assert(currentInterval.isActive);
        if (!currentInterval.isActive)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.MinimalCopyRegisterAssignmentRequiresAnActiveInterval
    );
            return default!;
        }

        var savedRelatedInterval = currentInterval.relatedInterval;
        var oldPhysicalRegister = currentInterval.physReg;
        var oldRegisterRecord = currentInterval.assignedReg;
        if (oldRegisterRecord is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.ActiveCopyRegisterIntervalMustRetainItsAssignedRegister
    );
            return default!;
        }
        assert(oldRegisterRecord.regNum == oldPhysicalRegister);
        if (oldRegisterRecord.regNum != oldPhysicalRegister)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.IntervalSAssignedRegisterDoesNotMatchItsPhysicalRegister
    );
            return default!;
        }

        currentInterval.relatedInterval = null;
        currentInterval.isActive = false;
        refPosition.copyReg = true;

        try
        {
            var allocatedRegister = allocateRegMinimal(currentInterval, refPosition, out var selectionScore);
            assert(allocatedRegister != REG_NA);
            currentInterval.relatedInterval = savedRelatedInterval;
            dumpCopyRegisterEvent(refPosition, allocatedRegister, selectionScore);
            return allocatedRegister;
        }
        finally
        {
            currentInterval.relatedInterval = savedRelatedInterval;
            currentInterval.physReg = oldPhysicalRegister;
            currentInterval.assignedReg = oldRegisterRecord;
            currentInterval.isActive = true;
        }
    }

    [Conditional("DEBUG")]
    partial void dumpKilledRegistersEvent(RefPosition killRefPosition, regMaskTP killedRegisters);

    [Conditional("DEBUG")]
    partial void dumpGCRefSpillEvent(RefPosition killRefPosition, bool killedAny);

    [Conditional("DEBUG")]
    partial void dumpCopyRegisterEvent(
        RefPosition refPosition, regNumber register, RegisterScore selectionScore);
}
