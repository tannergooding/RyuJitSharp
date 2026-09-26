// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if DEBUG
    private enum FullAllocationEvent
    {
        ZERO_REF,
        NO_ENTRY_REG_ALLOCATED,
        NO_REG_ALLOCATED,
        EXP_USE,
        RELOAD,
        SPECIAL_PUTARG,
        MOVE_REG,
        KEPT_ALLOCATION,
        NEEDS_NEW_REG,
        ALLOC_REG,
        REUSE_REG,
    }

    private void dumpFullAllocationEvent(
        FullAllocationEvent allocationEvent,
        RefPosition reference,
        Interval? interval = null,
        regNumber register = REG_NA,
        RegisterScore selectionScore = RegisterScore.NONE)
    {
        if (!VERBOSE)
        {
            return;
        }

        switch (allocationEvent)
        {
            case FullAllocationEvent.ZERO_REF:
            {
                assert(interval?.isLocalVar == true);
                dumpRefPositionShort(reference);
                jitprintf("NoRef      ");
                dumpAllocationRegisterRecords();
                break;
            }
            case FullAllocationEvent.NO_ENTRY_REG_ALLOCATED:
            {
                assert(interval?.isLocalVar == true);
                dumpRefPositionShort(reference);
                jitprintf("LoRef         ");
                break;
            }
            case FullAllocationEvent.EXP_USE:
            case FullAllocationEvent.KEPT_ALLOCATION:
            {
                dumpMinimalAllocationEvent(MinimalAllocationEvent.KEPT_ALLOCATION,
                    reference, interval, register);
                break;
            }
            case FullAllocationEvent.SPECIAL_PUTARG:
            {
                dumpRefPositionShort(reference);
                jitprintf($"PtArg    {register.Name,-4} ");
                break;
            }
            case FullAllocationEvent.NO_REG_ALLOCATED:
            {
                dumpMinimalAllocationEvent(MinimalAllocationEvent.NO_REG_ALLOCATED,
                    reference, interval, register);
                break;
            }
            case FullAllocationEvent.RELOAD:
            {
                dumpMinimalAllocationEvent(MinimalAllocationEvent.RELOAD,
                    reference, interval, register);
                break;
            }
            case FullAllocationEvent.MOVE_REG:
            {
                dumpMinimalAllocationEvent(MinimalAllocationEvent.MOVE_REG,
                    reference, interval, register);
                break;
            }
            case FullAllocationEvent.NEEDS_NEW_REG:
            {
                dumpMinimalAllocationEvent(MinimalAllocationEvent.NEEDS_NEW_REG,
                    reference, interval, register);
                break;
            }
            case FullAllocationEvent.ALLOC_REG:
            case FullAllocationEvent.REUSE_REG:
            {
                dumpMinimalAllocationEvent(
                    allocationEvent is FullAllocationEvent.ALLOC_REG
                        ? MinimalAllocationEvent.ALLOC_REG
                        : MinimalAllocationEvent.REUSE_REG,
                    reference, interval, register, selectionScore: selectionScore);
                break;
            }
            default:
            {
                throw new FatalJitException($"Unsupported full LSRA allocation event: {allocationEvent}.");
            }
        }
    }
#endif
}
