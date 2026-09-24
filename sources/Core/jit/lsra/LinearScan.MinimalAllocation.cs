// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private regNumber allocateRegMinimal(Interval currentInterval, RefPosition refPosition) =>
        allocateRegMinimal(currentInterval, refPosition, out _);

    private regNumber allocateRegMinimal(
        Interval currentInterval, RefPosition refPosition, out RegisterScore selectionScore)
    {
        assert(!_enregisterLocalVars);
        if (_enregisterLocalVars)
        {
            throw new FatalJitException("Minimal register allocation requires locals to remain un-enregistered.");
        }

        var foundRegBit = selectMinimal(currentInterval, refPosition, out selectionScore);
        if (foundRegBit == SRBM_NONE)
        {
            return REG_NA;
        }

        var foundReg = genRegNumFromMask(foundRegBit, currentInterval.registerType);
        var availablePhysRegRecord = getRegisterRecord(foundReg);
        var assignedInterval = availablePhysRegRecord.assignedInterval;
        if (!ReferenceEquals(assignedInterval, currentInterval) &&
            isAssigned(availablePhysRegRecord, getRegisterType(currentInterval, refPosition)))
        {
            unassignPhysReg(availablePhysRegRecord, currentInterval.registerType);
        }

        assignPhysReg(availablePhysRegRecord, currentInterval);
        refPosition.registerAssignment = foundRegBit;
        return foundReg;
    }
}
