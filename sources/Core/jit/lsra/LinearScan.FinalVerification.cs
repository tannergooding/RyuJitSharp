// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private static bool isResolutionMove(GenTree node)
    {
        if ((node._debugFlags & GTF_DEBUG_NODE_LSRA_ADDED) == 0)
        {
            return false;
        }

        return node.Oper switch
        {
            GT_LCL_VAR or GT_COPY => node.IsUnusedValue,
            GT_SWAP => true,
            _ => false,
        };
    }

    private static bool isResolutionNode(BasicBlock block, GenTree node)
    {
        while (true)
        {
            if (isResolutionMove(node))
            {
                return true;
            }

            if (((node._debugFlags & GTF_DEBUG_NODE_LSRA_ADDED) == 0) || (node.Oper is not GT_LCL_VAR))
            {
                return false;
            }

            var foundUse = block.TryGetUse(node, out var use);
            assert(foundUse);
            if (!foundUse)
            {
                throw new FatalJitException("An LSRA-added local must have an owning use.");
            }

            node = use.User();
        }
    }

    private void verifyFinalAllocationMinimal()
    {
        if (_enregisterLocalVars)
        {
            throw new FatalJitException("Final allocation verification with enregistered locals is not available.");
        }
        assert(!_enregisterLocalVars);

        // Without tracked register locals, neither edge resolution nor an inserted move
        // can be part of the allocation being replayed.
        foreach (var block in _compiler.Blocks)
        {
            if ((uint)block.bbNum > _bbNumMaxBeforeResolution)
            {
                throw new FatalJitException("Resolution blocks require enregistered locals.");
            }
            assert((uint)block.bbNum <= _bbNumMaxBeforeResolution);

            foreach (var node in block)
            {
                if (isResolutionNode(block, node))
                {
                    throw new FatalJitException("Resolution moves require enregistered locals.");
                }
            }
        }

        if (VERBOSE)
        {
            jitprintf("\nFinal allocation\n");
        }

        for (var index = 0; (int)_regIndices[index] < _availableRegCount; index++)
        {
            getRegisterRecord(_regIndices[index]).assignedInterval = null;
        }

        foreach (var interval in intervals)
        {
            interval.assignedReg = null;
            interval.physReg = REG_NA;
        }

        if (VERBOSE)
        {
            initializeAllocationDumpFormat();
            dumpAllocationRegisterTitle();
        }

        BasicBlock? currentBlock = null;
        var currentLocation = MinLocation;
        foreach (var reference in refPositions)
        {
            Interval? interval = null;
            RegRecord? regRecord = null;
            var regNum = REG_NA;
            _activeRefPosition = reference;

            if (reference.refType is not RefType.RefTypeBB)
            {
                if (reference.IsPhysRegRef())
                {
                    regRecord = reference.getReg();
                    regRecord.recentRefPosition = reference;
                    regNum = regRecord.regNum;
                }
                else if (reference.isIntervalRef())
                {
                    interval = reference.getInterval();
                    interval.recentRefPosition = reference;
                    if (reference.registerAssignment != SRBM_NONE)
                    {
                        if (!genMaxOneBit(reference.registerAssignment))
                        {
                            assert(reference.refType is RefType.RefTypeExpUse or RefType.RefTypeDummyDef);
                        }
                        else
                        {
                            regNum = reference.assignedReg();
                            regRecord = getRegisterRecord(regNum);
                        }
                    }
                }
            }

            currentLocation = reference.nodeLocation;
            switch (reference.refType)
            {
                case RefType.RefTypeBB:
                {
                    if (currentBlock is null)
                    {
                        currentBlock = startBlockSequence();
                    }
                    else
                    {
                        for (var index = 0; (int)_regIndices[index] < _availableRegCount; index++)
                        {
                            getRegisterRecord(_regIndices[index]).assignedInterval = null;
                        }

                        currentBlock = moveToNextBlock();
                    }

                    if (currentBlock is not null && VERBOSE)
                    {
                        dumpMinimalNewBlock(currentBlock, currentLocation, reference);
                    }
                    break;
                }

                case RefType.RefTypeKill:
                {
                    dumpVerificationEvent(reference, "None     ", registerMask: reference.getKilledRegisters());
                    break;
                }

                case RefType.RefTypeFixedReg:
                {
                    assert(regRecord is not null);
                    dumpVerificationEvent(reference, $"Keep     {regRecord.regNum.Name,-4} ");
                    break;
                }

                case RefType.RefTypeUpperVectorSave:
                {
                    dumpVerificationEvent(reference, "UVSav    NA   ");
                    break;
                }

                case RefType.RefTypeUpperVectorRestore:
                {
                    dumpVerificationEvent(reference, "UVRes    NA   ");
                    break;
                }

                case RefType.RefTypeDef:
                case RefType.RefTypeUse:
                case RefType.RefTypeParamDef:
                case RefType.RefTypeZeroInit:
                {
                    assert(interval is not null);
                    if (interval is null)
                    {
                        throw new FatalJitException("An allocation reference must refer to an interval.");
                    }

                    if (interval.isSpecialPutArg)
                    {
                        dumpVerificationEvent(reference, $"PtArg    {regNum.Name,-4} ",
                            interval: interval, register: regNum);
                        break;
                    }

                    if (reference.reload)
                    {
                        interval.isActive = true;
                        assert(regNum is not REG_NA);
                        interval.physReg = regNum;
                        interval.assignedReg = regRecord;
                        var reloadedRegister = regRecord
                            ?? throw new FatalJitException("A reload requires an assigned register.");
                        reloadedRegister.assignedInterval = interval;
                        dumpVerificationEvent(reference, $"ReLod    {regNum.Name,-4} ", printRecords: true);
                    }

                    if (regNum is REG_NA)
                    {
                        if ((interval.physReg is not REG_NA) && RefTypeIsDef(reference.refType))
                        {
                            var oldRegister = interval.assignedReg
                                ?? throw new FatalJitException("An assigned interval requires a register record.");
                            if (ReferenceEquals(oldRegister.assignedInterval, interval))
                            {
                                oldRegister.assignedInterval = null;
                            }
                            interval.physReg = REG_NA;
                            interval.assignedReg = null;
                        }

                        dumpVerificationEvent(reference, "NoReg         ", interval: interval);
                    }
                    else if (RefTypeIsDef(reference.refType))
                    {
                        interval.isActive = true;
                        if (VERBOSE)
                        {
                            var reuse = interval.isConstant && (reference.treeNode is not null) &&
                                reference.treeNode.IsReuseRegVal;
                            dumpVerificationEvent(reference, $"{(reuse ? "Reuse" : "Alloc"),-5}    {regNum.Name,-4} ");
                        }
                    }
                    else if (reference.copyReg)
                    {
                        dumpVerificationEvent(reference, $"Copy     {regNum.Name,-4} ",
                            interval: interval, register: regNum);
                    }
                    else if (reference.moveReg)
                    {
                        var oldRegister = interval.assignedReg
                            ?? throw new FatalJitException("A register move requires a previous assignment.");
                        oldRegister.assignedInterval = null;
                        interval.physReg = regNum;
                        interval.assignedReg = regRecord;
                        var movedRegister = regRecord
                            ?? throw new FatalJitException("A register move requires a destination register.");
                        movedRegister.assignedInterval = interval;
                        if (VERBOSE)
                        {
                            dumpVerificationIndentedEvent($"Move     {regNum.Name,-4} ");
                        }
                    }
                    else
                    {
                        dumpVerificationEvent(reference, $"Keep     {regNum.Name,-4} ");
                    }

                    if (reference.lastUse || (reference.spillAfter && !reference.writeThru))
                    {
                        interval.isActive = false;
                    }

                    if (regNum is not REG_NA)
                    {
                        if (reference.spillAfter)
                        {
                            if (VERBOSE)
                            {
                                var spillRegister = reference.copyReg ? interval.physReg : regNum;
                                dumpAllocationRegisterRecords();
                                dumpVerificationIndentedEvent(
                                    $"{(reference.writeThru ? "WThru" : "Spill"),-5}    {spillRegister.Name,-4} ");
                            }
                        }
                        else if (reference.copyReg)
                        {
                            var copiedRegister = regRecord
                                ?? throw new FatalJitException("A copy requires a destination register.");
                            copiedRegister.assignedInterval = interval;
                        }
                        else
                        {
                            if (RefTypeIsDef(reference.refType) &&
                                (interval.physReg is not REG_NA) && (interval.physReg != regNum))
                            {
                                var oldRegister = interval.assignedReg
                                    ?? throw new FatalJitException("An assigned interval requires a register record.");
                                oldRegister.assignedInterval = null;
                            }

                            interval.physReg = regNum;
                            interval.assignedReg = regRecord;
                            var assignedRegister = regRecord
                                ?? throw new FatalJitException("A register assignment requires a register record.");
                            assignedRegister.assignedInterval = interval;
                        }
                    }
                    break;
                }

                case RefType.RefTypeKillGCRefs:
                {
                    if (VERBOSE)
                    {
                        jitprintf("           ");
                    }

                    var candidates = unchecked((ulong)(long)reference.registerAssignment);
                    while (candidates != 0)
                    {
                        var register = (regNumber)BitOperations.TrailingZeroCount(candidates);
                        candidates &= candidates - 1;
                        var assignedInterval = getRegisterRecord(register).assignedInterval;
                        assert(assignedInterval is null || !varTypeIsGC(assignedInterval.registerType));
                    }
                    break;
                }

                case RefType.RefTypeExpUse:
                case RefType.RefTypeDummyDef:
                {
                    if (VERBOSE)
                    {
                        dumpRefPositionShort(reference);
                        jitprintf("              ");
                    }
                    break;
                }

                case RefType.RefTypeInvalid:
                {
                    break;
                }

                default:
                {
                    throw new FatalJitException($"Unsupported final-allocation reference type: {reference.refType}.");
                }
            }

            if (reference.refType is not RefType.RefTypeBB)
            {
                if (VERBOSE)
                {
                    dumpAllocationRegisterRecords();
                }

                if (interval is not null)
                {
                    if (reference.copyReg)
                    {
                        assert(interval.physReg != regNum);
                        var copiedRegister = regRecord
                            ?? throw new FatalJitException("A copy requires a destination register.");
                        copiedRegister.assignedInterval = null;
                        regRecord = interval.assignedReg
                            ?? throw new FatalJitException("A copy requires a home register.");
                    }

                    if (reference.spillAfter || reference.lastUse)
                    {
                        assert(!reference.spillAfter || reference.IsActualRef());
                        if (RefTypeIsDef(reference.refType) &&
                            (interval.physReg is not REG_NA) && (interval.physReg != regNum))
                        {
                            var oldRegister = interval.assignedReg
                                ?? throw new FatalJitException("An assigned interval requires a register record.");
                            oldRegister.assignedInterval = null;
                        }

                        interval.physReg = REG_NA;
                        interval.assignedReg = null;
                        if (regRecord is not null)
                        {
                            regRecord.assignedInterval = null;
                        }
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                        else if (interval.isUpperVector && !reference.RegOptional())
                        {
                            if (reference.refType is RefType.RefTypeUpperVectorSave or RefType.RefTypeUpperVectorRestore)
                            {
                                var local = interval.relatedInterval
                                    ?? throw new FatalJitException("Upper-vector references require a related interval.");
                                assert((local.physReg is REG_NA) || local.isPartiallySpilled ||
                                    (reference.refType is RefType.RefTypeUpperVectorSave &&
                                        reference.IsExtraUpperVectorSave()));
                            }
                        }
#endif
                        else
                        {
                            assert(reference.RegOptional());
                        }
                    }
                }
            }
        }

        if (VERBOSE)
        {
            jitprintf("\n");
        }
    }

    private void dumpVerificationEvent(RefPosition reference, string action, Interval? interval = null,
        regNumber register = REG_NA, bool printRecords = false, regMaskTP registerMask = default)
    {
        if (!VERBOSE)
        {
            return;
        }

        if ((interval is not null) && (register is not REG_NA and not REG_STK))
        {
            _allocationDumpRegisters |= regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
            dumpAllocationRegisterTitleIfNeeded();
        }

        dumpRefPositionShort(reference);
        jitprintf(action);
        if (reference.refType is RefType.RefTypeKill)
        {
            _compiler.dumpRegMask(registerMask);
            jitprintf("\n");
            dumpRefPositionShort(reference);
            jitprintf("              ");
        }
        else if (printRecords)
        {
            dumpAllocationRegisterRecords();
        }
    }

    private void dumpVerificationIndentedEvent(string action)
    {
        dumpRefPositionShort(null);
        jitprintf(action);
    }
}
#endif
