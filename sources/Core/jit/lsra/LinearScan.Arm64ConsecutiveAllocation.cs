// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if TARGET_ARM64
    private SingleTypeRegSet _consecutiveRegsInUseThisLocation;

    private static regNumber getNextFPRegWraparound(regNumber register) =>
        register == REG_FP_LAST ? REG_FP_FIRST : (regNumber)((int)register + 1);

    private void assignConsecutiveRegisters(RefPosition first, regNumber firstRegister)
    {
        assert(_compiler.info.compNeedsConsecutiveRegisters);
        assert(first.isFirstRefPositionOfConsecutiveRegisters());
        assert(first.assignedReg() == firstRegister);
        assert(firstRegister is >= REG_FP_FIRST and <= REG_FP_LAST);
        assert(_consecutiveRegsInUseThisLocation == SRBM_NONE);
        assert(first.refType is not RefType.RefTypeUpperVectorRestore);

        var next = getNextConsecutiveRefPosition(first);
        var register = firstRegister;
        _consecutiveRegsInUseThisLocation = SRBM_NONE;
        for (var index = 0; index < first.regCount; index++)
        {
            _consecutiveRegsInUseThisLocation |= genSingleTypeRegMask(register);
            register = getNextFPRegWraparound(register);
        }

        register = getNextFPRegWraparound(firstRegister);
#if DEBUG
        var assignedCount = 1;
#endif
        while (next is not null)
        {
            assert(next.regCount == 0);
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
            if (next.refType is RefType.RefTypeUpperVectorRestore)
            {
                var upper = next.getInterval();
                assert(upper.isUpperVector && upper.relatedInterval is not null);
                var related = upper.relatedInterval
                    ?? throw new FatalJitException("A consecutive upper-vector restore requires a related interval.");
                if (related.isPartiallySpilled)
                {
                    next.registerAssignment &= ~_consecutiveRegsInUseThisLocation;
                }
                next = getNextConsecutiveRefPosition(next)
                    ?? throw new FatalJitException("A consecutive upper-vector restore must precede a use.");
            }
#endif
            assert(next.refType is RefType.RefTypeDef or RefType.RefTypeUse);
            next.registerAssignment = genSingleTypeRegMask(register);
            next = getNextConsecutiveRefPosition(next);
            register = getNextFPRegWraparound(register);
#if DEBUG
            assignedCount++;
#endif
        }
#if DEBUG
        assert(assignedCount == first.regCount);
#endif
    }

    private bool canAssignNextConsecutiveRegisters(RefPosition first, regNumber firstRegister)
    {
        assert(_compiler.info.compNeedsConsecutiveRegisters);
        assert(first.regCount > 1);
        assert(firstRegister is >= REG_FP_FIRST and <= REG_FP_LAST);

        var next = first;
        var register = firstRegister;
        for (var index = 1; index < first.regCount; index++)
        {
            next = getNextConsecutiveRefPosition(next)
                ?? throw new FatalJitException("A consecutive register sequence ended prematurely.");
            register = getNextFPRegWraparound(register);
            if (!isFree(getRegisterRecord(register)))
            {
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                if (next.refType is RefType.RefTypeUpperVectorRestore)
                {
                    next = getNextConsecutiveRefPosition(next)
                        ?? throw new FatalJitException("A consecutive upper-vector restore must precede a use.");
                }
#endif
                var interval = next.getInterval();
                if (interval is not null && !isRegInUse(register, interval.registerType) &&
                    interval.assignedReg?.regNum == register)
                {
                    continue;
                }
                return false;
            }
        }
        return true;
    }

    private SingleTypeRegSet filterConsecutiveCandidates(
        SingleTypeRegSet candidates, uint needed, out SingleTypeRegSet allConsecutiveCandidates)
    {
        assert(candidates == SRBM_NONE || (candidates & _availableFloatRegs) != SRBM_NONE);
        assert(needed is >= 2 and <= 4);
        allConsecutiveCandidates = SRBM_NONE;
        if (BitOperations.PopCount(unchecked((ulong)candidates)) < needed)
        {
            return SRBM_NONE;
        }

        var remaining = unchecked((ulong)candidates);
        var starts = 0UL;
        var all = 0UL;
        do
        {
            var start = BitOperations.TrailingZeroCount(remaining);
            var beforeStart = (1UL << start) - 1;
            var unavailable = ~(remaining | beforeStart);
            var end = unavailable == 0 ? 64 : BitOperations.TrailingZeroCount(unavailable);
            var beforeEnd = end == 64 ? ulong.MaxValue : (1UL << end) - 1;
            if (end - start >= needed)
            {
                var range = remaining & beforeEnd & ~beforeStart;
                starts |= range & ((1UL << (end - (int)needed + 1)) - 1);
                all |= range;
            }
            remaining &= ~beforeEnd;
        } while (remaining != 0);

        // The floating-point register bank wraps from V31 to V0.
        var endpoints = SRBM_V0 | SRBM_V31;
        if ((candidates & endpoints) == endpoints)
        {
            switch (needed)
            {
                case 2:
                {
                    starts |= unchecked((ulong)SRBM_V31);
                    all |= unchecked((ulong)endpoints);
                    break;
                }
                case 3:
                {
                    var fromV30 = SRBM_V0 | SRBM_V30 | SRBM_V31;
                    if ((candidates & fromV30) != SRBM_NONE)
                    {
                        starts |= unchecked((ulong)SRBM_V30);
                        all |= unchecked((ulong)fromV30);
                    }
                    var fromV31 = SRBM_V0 | SRBM_V1 | SRBM_V31;
                    if ((candidates & fromV31) != SRBM_NONE)
                    {
                        starts |= unchecked((ulong)SRBM_V31);
                        all |= unchecked((ulong)fromV31);
                    }
                    break;
                }
                case 4:
                {
                    var fromV29 = SRBM_V0 | SRBM_V29 | SRBM_V30 | SRBM_V31;
                    if ((candidates & fromV29) != SRBM_NONE)
                    {
                        starts |= unchecked((ulong)SRBM_V29);
                        all |= unchecked((ulong)fromV29);
                    }
                    var fromV30 = SRBM_V0 | SRBM_V29 | SRBM_V30 | SRBM_V31;
                    if ((candidates & fromV30) != SRBM_NONE)
                    {
                        starts |= unchecked((ulong)SRBM_V30);
                        all |= unchecked((ulong)fromV30);
                    }
                    var fromV31 = SRBM_V0 | SRBM_V29 | SRBM_V30 | SRBM_V31;
                    if ((candidates & fromV31) != SRBM_NONE)
                    {
                        starts |= unchecked((ulong)SRBM_V31);
                        all |= unchecked((ulong)fromV31);
                    }
                    break;
                }
            }
        }

        allConsecutiveCandidates = unchecked((SingleTypeRegSet)all);
        var result = unchecked((SingleTypeRegSet)starts);
        assert((allConsecutiveCandidates & result) == result);
        return result;
    }

    private SingleTypeRegSet filterConsecutiveCandidatesForSpill(SingleTypeRegSet candidates, uint needed)
    {
        assert(candidates != SRBM_NONE);
        assert(needed is >= 2 and <= 4);

        var unprocessed = unchecked((ulong)candidates);
        var sequenceMask = (1UL << (int)needed) - 1;
        var free = unchecked((ulong)_availableRegs[(int)TYP_FLOAT]);
        var best = 0UL;
        var minSpills = (int)needed;
        do
        {
            var start = BitOperations.TrailingZeroCount(unprocessed);
            var range = sequenceMask << start;
            if (start + needed > 64)
            {
                range |= (1UL << (start + (int)needed - 64)) - 1;
            }
            var freeInRange = range & free;
            if (freeInRange != 0)
            {
                var spills = (int)needed - BitOperations.PopCount(freeInRange);
                if (spills < minSpills)
                {
                    best = 1UL << start;
                    minSpills = spills;
                }
                else if (spills == minSpills)
                {
                    best |= 1UL << start;
                }
            }
            unprocessed &= ~(1UL << start);
        } while (unprocessed != 0);

        var result = unchecked((SingleTypeRegSet)best);
        assert((candidates & result) == result);
        return result;
    }

    private SingleTypeRegSet getConsecutiveCandidates(
        SingleTypeRegSet candidates, RefPosition first, out SingleTypeRegSet busyCandidates)
    {
        assert(_compiler.info.compNeedsConsecutiveRegisters);
        assert(first.isFirstRefPositionOfConsecutiveRegisters());
        var free = candidates & _availableRegs[(int)TYP_FLOAT];
#if DEBUG
        if ((_lsraStressMask & 0x3) != 0)
        {
            free &= SRBM_V0 | SRBM_V2 | SRBM_V4 | SRBM_V6 | SRBM_V8 |
                SRBM_V10 | SRBM_V12 | SRBM_V14 | SRBM_V16 | SRBM_V18 |
                SRBM_V20 | SRBM_V22 | SRBM_V24 | SRBM_V26 | SRBM_V28 | SRBM_V30;
        }
#endif
        busyCandidates = SRBM_NONE;
        var needed = (uint)first.regCount;
        if (free != SRBM_NONE)
        {
            var freeStarts = filterConsecutiveCandidates(free, needed, out var allFree);
            if (freeStarts != SRBM_NONE)
            {
                var next = getNextConsecutiveRefPosition(first)
                    ?? throw new FatalJitException("A consecutive sequence requires additional references.");
                var firstAssigned = REG_NA;
                var previousAssigned = REG_NA;
                var foundCount = 0;
                for (var index = 1; index < needed; index++)
                {
                    var interval = next.getInterval();
                    if (index + 1 < needed)
                    {
                        next = getNextConsecutiveRefPosition(next)
                            ?? throw new FatalJitException("A consecutive sequence ended prematurely.");
                    }
                    if (!interval.isActive)
                    {
                        foundCount = 0;
                        continue;
                    }

                    var current = interval.assignedReg?.regNum
                        ?? throw new FatalJitException("An active consecutive interval has no register.");
                    if (previousAssigned == REG_NA ||
                        (int)previousAssigned + 1 == (int)current ||
                        (previousAssigned == REG_FP_LAST && current == REG_FP_FIRST))
                    {
                        if (previousAssigned == REG_NA)
                        {
                            firstAssigned = current;
                        }
                        previousAssigned = current;
                        foundCount++;
                        continue;
                    }
                    foundCount = 0;
                    break;
                }
                if (foundCount != 0)
                {
                    assert(firstAssigned != REG_NA);
                    // Native uses the bit before V0, not V31, when the first assigned register is V0.
                    var start = (int)firstAssigned - 1;
                    var remaining = unchecked((SingleTypeRegSet)(
                        ((1UL << ((int)needed - foundCount)) - 1) << start));
                    if ((allFree & remaining) != SRBM_NONE)
                    {
                        freeStarts = unchecked((SingleTypeRegSet)(1UL << start));
                    }
                }
                return freeStarts;
            }
        }

        var busyStarts = filterConsecutiveCandidates(candidates, needed, out var allBusy);
        busyCandidates = busyStarts;
        if ((_availableRegs[(int)TYP_FLOAT] & allBusy) != SRBM_NONE)
        {
            var optimal = filterConsecutiveCandidatesForSpill(busyStarts, needed);
            if (optimal != SRBM_NONE)
            {
                busyCandidates = optimal;
            }
            else if ((_availableRegs[(int)TYP_FLOAT] & busyStarts) != SRBM_NONE)
            {
                busyCandidates = _availableRegs[(int)TYP_FLOAT] & busyStarts;
            }
        }
        return SRBM_NONE;
    }
#endif
}
