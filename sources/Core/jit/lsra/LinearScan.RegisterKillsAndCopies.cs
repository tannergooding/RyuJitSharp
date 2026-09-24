// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private RefPosition addKillForRegs(regMaskTP registers, LsraLocation location)
    {
        var codeGen = _compiler.codeGen
            ?? throw new FatalJitException("Register kills require initialized codegen state.");
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
            var killTail = _killTail
                ?? throw new FatalJitException("A non-empty kill list must have a tail reference.");
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
#if HAS_MORE_THAN_64_REGISTERS
        freeKilledRegisters(killRefPosition, killedRegisters.Upper, nextKill, REG_HIGH_BASE);
#endif

#if HAS_MORE_THAN_64_REGISTERS
        _regsBusyUntilKill = new regMaskTP(
            _regsBusyUntilKill.Lower & ~killedRegisters.Lower,
            _regsBusyUntilKill.Upper & ~killedRegisters.Upper);
#else
        _regsBusyUntilKill = new regMaskTP(_regsBusyUntilKill.Lower & ~killedRegisters.Lower);
#endif
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
                clearConstantReg(regRecord.regNum);
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
            throw new FatalJitException("A minimal copy-register assignment requires an active interval.");
        }

        var savedRelatedInterval = currentInterval.relatedInterval;
        var oldPhysicalRegister = currentInterval.physReg;
        var oldRegisterRecord = currentInterval.assignedReg
            ?? throw new FatalJitException("An active copy-register interval must retain its assigned register.");
        assert(oldRegisterRecord.regNum == oldPhysicalRegister);
        if (oldRegisterRecord.regNum != oldPhysicalRegister)
        {
            throw new FatalJitException("The interval's assigned register does not match its physical register.");
        }

        currentInterval.relatedInterval = null;
        currentInterval.isActive = false;
        refPosition.copyReg = true;

        try
        {
            var allocatedRegister = allocateRegMinimal(currentInterval, refPosition, out var selectionScore);
            assert(allocatedRegister is not REG_NA);
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

#if DEBUG
    private void dumpKilledRegistersEvent(RefPosition killRefPosition, regMaskTP killedRegisters)
    {
        if (!VERBOSE)
        {
            return;
        }

        initializeAllocationDumpFormat();
        dumpRefPositionShort(killRefPosition);
        jitprintf("None     ");
        _compiler.dumpRegMask(killedRegisters);
        jitprintf("\n");
        dumpRefPositionShort(killRefPosition);
        jitprintf("              ");
    }

    private void dumpGCRefSpillEvent(RefPosition killRefPosition, bool killedAny)
    {
        if (!VERBOSE)
        {
            return;
        }

        initializeAllocationDumpFormat();
        dumpRefPositionShort(killRefPosition);
        jitprintf(killedAny ? "Done          " : "None          ");
    }

    private void dumpCopyRegisterEvent(
        RefPosition refPosition, regNumber register, RegisterScore selectionScore)
    {
        if (!VERBOSE)
        {
            return;
        }

        initializeAllocationDumpFormat();
        _allocationDumpRegisters |=
            regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
        dumpAllocationRegisterTitleIfNeeded();
        dumpRefPositionShort(refPosition);
        if (_allocationPassComplete || (selectionScore is RegisterScore.NONE))
        {
            jitprintf($"Copy     {register.Name.ToUpperInvariant(),-4} ");
        }
        else
        {
            jitprintf($"{getScoreName(selectionScore),-5}(C) {register.Name.ToUpperInvariant(),-4} ");
        }
    }
#else
    private void dumpKilledRegistersEvent(RefPosition killRefPosition, regMaskTP killedRegisters)
    {
    }

    private void dumpGCRefSpillEvent(RefPosition killRefPosition, bool killedAny)
    {
    }

    private void dumpCopyRegisterEvent(
        RefPosition refPosition, regNumber register, RegisterScore selectionScore)
    {
    }
#endif
}
