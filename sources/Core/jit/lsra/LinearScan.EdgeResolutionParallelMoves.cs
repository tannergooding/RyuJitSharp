// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private static regNumber firstResolutionRegister(SingleTypeRegSet registers)
    {
        assert(registers != SRBM_NONE);
        return (regNumber)BitOperations.TrailingZeroCount(unchecked((ulong)registers));
    }

    private static regNumber popResolutionRegister(ref SingleTypeRegSet registers)
    {
        var register = firstResolutionRegister(registers);
        registers &= ~genSingleTypeRegMask(register);
        return register;
    }

    private void resolveEdge(BasicBlock fromBlock, BasicBlock? toBlock, ResolveType resolveType,
        VARSET_TP liveSet, regMaskTP terminatorConsumedRegs)
    {
        requireWindowsAmd64EdgeResolution();
        var fromMap = getOutVarToRegMap(checked((uint)fromBlock.bbNum))
            ?? throw new FatalJitException("Resolution requires a predecessor outgoing map.");
        var toMap = (resolveType is ResolveType.ResolveSharedCritical
            ? _sharedCriticalVarToRegMap
            : getInVarToRegMap(checked((uint)(toBlock
                ?? throw new FatalJitException("Resolution requires a successor block.")).bbNum)))
            ?? throw new FatalJitException("Resolution requires a successor register map.");
        BasicBlock block;
        switch (resolveType)
        {
            case ResolveType.ResolveJoin:
            case ResolveType.ResolveSharedCritical:
            {
                block = fromBlock;
                break;
            }
            case ResolveType.ResolveSplit:
            {
                block = toBlock ?? throw new FatalJitException("Split resolution requires a successor.");
                break;
            }
            case ResolveType.ResolveCritical:
            {
                block = _compiler.fgSplitEdge(fromBlock,
                    toBlock ?? throw new FatalJitException("Critical resolution requires a successor."));
#if TRACK_LSRA_STATS
                updateLsraStat(LsraStat.STAT_SPLIT_EDGE, checked((uint)fromBlock.bbNum));
#endif
                break;
            }
            default:
            {
                throw new FatalJitException("Unknown LSRA edge-resolution type.");
            }
        }

        // Compute scratch registers before changing either side's map.
        var intTemp = getTempRegForResolution(fromBlock, toBlock, TYP_INT, liveSet, terminatorConsumedRegs);
        var floatTemp = REG_NA;
        if (_compiler.compFloatingPointUsed)
        {
            floatTemp = getTempRegForResolution(fromBlock, toBlock, TYP_FLOAT, liveSet, terminatorConsumedRegs);
        }

        var targetsToDo = SRBM_NONE;
        var targetsReady = SRBM_NONE;
        var targetsFromStack = SRBM_NONE;
        // `source` never changes: `location` tracks where each original
        // register's value resides as moves or swaps break dependency cycles.
        var location = new regNumber[(int)REG_COUNT];
        var source = new regNumber[(int)REG_COUNT];
        Array.Fill(location, REG_NA);
        Array.Fill(source, REG_NA);
        var sourceIntervals = new Interval?[(int)REG_COUNT];
        var stackToRegIntervals = new Interval?[(int)REG_COUNT];
        var insertionPoint = resolveType is ResolveType.ResolveSplit or ResolveType.ResolveCritical
            ? block.FirstNode : null;

        if (resolveType is ResolveType.ResolveJoin && _compiler.compHndBBtabCount > 0)
        {
            var extra = VarSetOps.Diff(_compiler, block.bbLiveOut, toBlock!.bbLiveIn);
            VarSetOps.IntersectionD(_compiler, extra, _exceptVars);
            _ = VarSetOps.VisitBits(_compiler, extra, index =>
            {
                var interval = getIntervalForLocalVar(checked((uint)index));
                assert(interval.isWriteThru);
                var from = getVarReg(fromMap, checked((uint)index));
                if (from != REG_STK)
                {
                    addResolution(block, insertionPoint, interval, REG_STK, from,
                        fromBlock, toBlock, "EH DUMMY");
                    setVarReg(fromMap, checked((uint)index), REG_STK);
                }
                return true;
            });
        }

        _ = VarSetOps.VisitBits(_compiler, liveSet, index =>
        {
            var trackedIndex = checked((uint)index);
            var interval = getIntervalForLocalVar(trackedIndex);
            var from = getVarReg(fromMap, trackedIndex);
            var to = getVarReg(toMap, trackedIndex);
            if (from == to)
            {
                return true;
            }
            if (interval.isWriteThru && to == REG_STK &&
                (resolveType is ResolveType.ResolveSplit || block.hasEHBoundaryOut))
            {
                return true;
            }

            // Only join/split moves change an existing block's map; the
            // critical-edge block has its own aliased maps after resolution.
            if (resolveType is ResolveType.ResolveSplit)
            {
                setVarReg(toMap, trackedIndex, from);
            }
            else if (resolveType is ResolveType.ResolveJoin or ResolveType.ResolveSharedCritical)
            {
                setVarReg(fromMap, trackedIndex, to);
            }

            assert((uint)from < byte.MaxValue && (uint)to < byte.MaxValue);
            if (from == REG_STK)
            {
                stackToRegIntervals[(int)to] = interval;
                targetsFromStack |= genSingleTypeRegMask(to);
            }
            else if (to == REG_STK)
            {
                addResolution(block, insertionPoint, interval, REG_STK, from,
                    fromBlock, toBlock,
                    interval.isWriteThru ? "EH DUMMY" : s_resolveTypeName[(int)resolveType]);
            }
            else
            {
                location[(int)from] = from;
                source[(int)to] = from;
                sourceIntervals[(int)from] = interval;
                targetsToDo |= genSingleTypeRegMask(to);
            }
            return true;
        });

        var targetCandidates = targetsToDo;
        while (targetCandidates != SRBM_NONE)
        {
            var target = popResolutionRegister(ref targetCandidates);
            if (location[(int)target] == REG_NA)
            {
                targetsReady |= genSingleTypeRegMask(target);
            }
        }

        // Register-to-stack moves above free their sources. Execute ready
        // register moves first; a remaining cycle uses XCHG for GPRs without
        // a scratch register, or spills one member before loading it last.
        while (targetsToDo != SRBM_NONE)
        {
            while (targetsReady != SRBM_NONE)
            {
                var target = popResolutionRegister(ref targetsReady);
                targetsToDo &= ~genSingleTypeRegMask(target);
                assert(location[(int)target] != target);
                var original = source[(int)target];
                assert((uint)original < (uint)REG_COUNT);
                var from = location[(int)original];
                assert(from < REG_STK);
                var interval = sourceIntervals[(int)original]
                    ?? throw new FatalJitException("Ready resolution target requires an interval.");
                addResolution(block, insertionPoint, interval, target, from,
                    fromBlock, toBlock, s_resolveTypeName[(int)resolveType]);
                sourceIntervals[(int)original] = null;
                location[(int)original] = REG_NA;

                if (from == original && source[(int)from] != REG_NA &&
                    (targetsFromStack & genSingleTypeRegMask(from)) == SRBM_NONE)
                {
                    targetsReady |= genSingleTypeRegMask(from);
                }
            }
            if (targetsToDo == SRBM_NONE)
            {
                break;
            }

            var targetReg = firstResolutionRegister(targetsToDo);
            var targetMask = genSingleTypeRegMask(targetReg);
            var sourceReg = source[(int)targetReg];
            var fromReg = location[(int)sourceReg];
            if (targetReg == fromReg)
            {
                targetsToDo &= ~targetMask;
                continue;
            }

            var isFloat = genIsValidFloatReg(targetReg);
            var tempReg = isFloat ? floatTemp : intTemp;
            var useSwap = !isFloat && tempReg == REG_NA;
            if (useSwap || tempReg == REG_NA)
            {
                var otherTargetReg = REG_NA;
                if (location[(int)source[(int)fromReg]] == targetReg)
                {
                    otherTargetReg = fromReg;
                    if (useSwap)
                    {
                        targetsToDo &= ~genSingleTypeRegMask(fromReg);
                    }
                }
                else
                {
                    var mask = targetsToDo;
                    while (mask != SRBM_NONE && otherTargetReg == REG_NA)
                    {
                        var next = popResolutionRegister(ref mask);
                        if (location[(int)source[(int)next]] == targetReg)
                        {
                            otherTargetReg = next;
                        }
                    }
                }
                assert(otherTargetReg != REG_NA);
                if (useSwap)
                {
                    var swapped = sourceIntervals[(int)source[(int)otherTargetReg]]
                        ?? throw new FatalJitException("Swap resolution requires the displaced interval.");
                    var moved = sourceIntervals[(int)sourceReg]
                        ?? throw new FatalJitException("Swap resolution requires the source interval.");
                    insertSwap(block, insertionPoint, swapped.varNum, targetReg, moved.varNum, fromReg);
                    location[(int)sourceReg] = REG_NA;
                    location[(int)source[(int)otherTargetReg]] = fromReg;
#if TRACK_LSRA_STATS
                    updateLsraStat(LsraStat.STAT_RESOLUTION_MOV, checked((uint)block.bbNum));
#endif
                }
                else
                {
                    var displaced = sourceIntervals[(int)source[(int)otherTargetReg]]
                        ?? throw new FatalJitException("Cycle spill requires a displaced interval.");
                    setIntervalAsSpilled(displaced);
                    addResolution(block, insertionPoint, displaced, REG_STK, targetReg,
                        fromBlock, toBlock, s_resolveTypeName[(int)resolveType]);
                    location[(int)source[(int)otherTargetReg]] = REG_STK;
                    targetsFromStack |= genSingleTypeRegMask(otherTargetReg);
                    stackToRegIntervals[(int)otherTargetReg] = displaced;
                    targetsToDo &= ~genSingleTypeRegMask(otherTargetReg);

                    var moved = sourceIntervals[(int)sourceReg]
                        ?? throw new FatalJitException("Cycle resolution requires a source interval.");
                    addResolution(block, insertionPoint, moved, targetReg, fromReg,
                        fromBlock, toBlock, s_resolveTypeName[(int)resolveType]);
                    location[(int)sourceReg] = REG_NA;
                    if (source[(int)fromReg] != REG_NA && fromReg != otherTargetReg)
                    {
                        targetsReady |= genSingleTypeRegMask(fromReg);
                    }
                }
                targetsToDo &= ~targetMask;
            }
            else
            {
                var displaced = sourceIntervals[(int)targetReg]
                    ?? throw new FatalJitException("Cycle scratch move requires a displaced interval.");
                _compiler.codeGen!.RegSet.rsSetRegsModified(
                    regMaskTP.CreateFromRegNum(tempReg, genSingleTypeRegMask(tempReg)), true);
                addResolution(block, insertionPoint, displaced, tempReg, targetReg,
                    fromBlock, toBlock, s_resolveTypeName[(int)resolveType]);
                location[(int)targetReg] = tempReg;
                targetsReady |= targetMask;
            }
        }

        while (targetsFromStack != SRBM_NONE)
        {
            var target = popResolutionRegister(ref targetsFromStack);
            var interval = stackToRegIntervals[(int)target]
                ?? throw new FatalJitException("Stack resolution requires a pending interval.");
            addResolution(block, insertionPoint, interval, target, REG_STK,
                fromBlock, toBlock, s_resolveTypeName[(int)resolveType]);
        }
    }
}
