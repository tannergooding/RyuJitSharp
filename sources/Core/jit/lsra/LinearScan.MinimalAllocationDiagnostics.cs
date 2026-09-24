// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if DEBUG
    private void dumpLsraIntervals(string message)
    {
        jitprintf($"\nLinear scan intervals {message}:\n");
        foreach (var interval in intervals)
        {
            dumpInterval(interval);
        }

        jitprintf("\n");
    }

    private void dumpRefPositions(string title)
    {
        jitprintf("------------\n");
        jitprintf($"REFPOSITIONS {title}: \n");
        jitprintf("------------\n");
        foreach (var refPosition in refPositions)
        {
            refPosition.dump(this);
        }
    }

    private void dumpVarRefPositions(string title)
    {
        if (!_enregisterLocalVars)
        {
            return;
        }

        jitprintf($"\nVAR REFPOSITIONS {title}\n");
        for (uint index = 0; index < _compiler.lvaCount; index++)
        {
            jitprintf($"--- V{index:D2}");
            ref var local = ref _compiler.lvaGetDesc(checked((int)index));
            if (local.lvIsRegCandidate)
            {
                var localVarIntervals = this.localVarIntervals
                    ?? throw new FatalJitException("Register-candidate locals require LSRA interval mappings.");
                var interval = localVarIntervals[local._varIndex]
                    ?? throw new FatalJitException("A register-candidate local has no LSRA interval.");
                jitprintf($"  (Interval {interval.intervalIndex})\n");
                for (var refPosition = interval.firstRefPosition;
                     refPosition is not null;
                     refPosition = refPosition.nextRefPosition)
                {
                    refPosition.dump(this);
                }
            }
            else
            {
                jitprintf("\n");
            }
        }

        jitprintf("\n");
    }

    private void dumpActiveIntervalsAtEnd()
    {
        jitprintf("Active intervals at end of allocation:\n");
        foreach (var interval in intervals)
        {
            if (interval.isActive)
            {
                jitprintf("Active ");
                dumpInterval(interval);
            }
        }

        jitprintf("\n");
    }

    private void dumpInterval(Interval interval)
    {
        jitprintf($"Interval {interval.intervalIndex,2}:");
        if (interval.isLocalVar)
        {
            jitprintf($" (V{interval.varNum:D2})");
        }
        else if (interval.IsUpperVector())
        {
            var relatedInterval = interval.relatedInterval
                ?? throw new FatalJitException("An upper-vector interval requires a related interval.");
            jitprintf($" (U{relatedInterval.varNum:D2})");
        }

        jitprintf($" {interval.registerType.Name}");
        if (interval.isInternal)
        {
            jitprintf(" (INTERNAL)");
        }
        if (interval.isSpilled)
        {
            jitprintf(" (SPILLED)");
        }
        if (interval.isSplit)
        {
            jitprintf(" (SPLIT)");
        }
        if (interval.isStructField)
        {
            jitprintf(" (field)");
        }
        if (interval.isPromotedStruct)
        {
            jitprintf(" (promoted struct)");
        }
        if (interval.hasConflictingDefUse)
        {
            jitprintf(" (def-use conflict)");
        }
        if (interval.hasInterferingUses)
        {
            jitprintf(" (interfering uses)");
        }
        if (interval.isSpecialPutArg)
        {
            jitprintf(" (specialPutArg)");
        }
        if (interval.isConstant)
        {
            jitprintf(" (constant)");
        }
        if (interval.isWriteThru)
        {
            jitprintf(" (writeThru)");
        }

        jitprintf(" RefPositions {");
        for (var refPosition = interval.firstRefPosition;
             refPosition is not null;
             refPosition = refPosition.nextRefPosition)
        {
            jitprintf($"#{refPosition.rpNum}@{refPosition.nodeLocation}");
            if (refPosition.nextRefPosition is not null)
            {
                jitprintf(" ");
            }
        }

        jitprintf("} physReg:");
        jitprintf(interval.physReg is REG_NA ? "NA" : interval.physReg.Name);
        jitprintf(" Preferences=");
        _compiler.dumpRegMask(interval.registerPreferences, interval.registerType);
        jitprintf(" Aversions=");
        _compiler.dumpRegMask(interval.registerAversion, interval.registerType);
        if (interval.relatedInterval is not null)
        {
            jitprintf(" RelatedInterval ");
            interval.relatedInterval.microDump();
        }

        jitprintf("\n");
    }
#endif
}
