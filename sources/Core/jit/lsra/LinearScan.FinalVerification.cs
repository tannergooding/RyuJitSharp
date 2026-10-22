// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
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

        return node.Oper switch {
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
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LSRAAddedLocalMustHaveAnOwningUse);
                return default!;
            }

            node = use.User();
        }
    }

    [Conditional("DEBUG")]
    private void verifyFinalAllocationMinimal()
    {
        if (_enregisterLocalVars)
        {
            FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.FinalAllocationVerificationWithEnregisteredLocalsIsNotAvailable
    );
            return;
        }
        assert(!_enregisterLocalVars);

        // Without tracked register locals, neither edge resolution nor an inserted move
        // can be part of the allocation being replayed.
        foreach (var block in _compiler.Blocks)
        {
            if ((uint)block.bbNum > _bbNumMaxBeforeResolution)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ResolutionBlocksRequireEnregisteredLocals);
                return;
            }
            assert((uint)block.bbNum <= _bbNumMaxBeforeResolution);

            foreach (var node in block)
            {
                if (isResolutionNode(block, node))
                {
                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ResolutionMovesRequireEnregisteredLocals);
                    return;
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

            var currentLocation = reference.nodeLocation;
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
                        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AllocationReferenceMustReferToAnInterval);
                        return;
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
                        assert(regNum != REG_NA);
                        interval.physReg = regNum;
                        interval.assignedReg = regRecord;
                        var reloadedRegister = regRecord;
                        if (reloadedRegister is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ReloadRequiresAnAssignedRegister);
                            return;
                        }
                        reloadedRegister.assignedInterval = interval;
                        dumpVerificationEvent(reference, $"ReLod    {regNum.Name,-4} ", printRecords: true);
                    }

                    if (regNum == REG_NA)
                    {
                        if ((interval.physReg != REG_NA) && RefTypeIsDef(reference.refType))
                        {
                            var oldRegister = interval.assignedReg;
                            if (oldRegister is null)
                            {
                                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AssignedIntervalRequiresARegisterRecord);
                                return;
                            }
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
                        var oldRegister = interval.assignedReg;
                        if (oldRegister is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMoveRequiresAPreviousAssignment);
                            return;
                        }
                        oldRegister.assignedInterval = null;
                        interval.physReg = regNum;
                        interval.assignedReg = regRecord;
                        var movedRegister = regRecord;
                        if (movedRegister is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterMoveRequiresADestinationRegister);
                            return;
                        }
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

                    if (regNum != REG_NA)
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
                            var copiedRegister = regRecord;
                            if (copiedRegister is null)
                            {
                                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.CopyRequiresADestinationRegister);
                                return;
                            }
                            copiedRegister.assignedInterval = interval;
                        }
                        else
                        {
                            if (RefTypeIsDef(reference.refType) &&
                                (interval.physReg != REG_NA) && (interval.physReg != regNum))
                            {
                                var oldRegister = interval.assignedReg;
                                if (oldRegister is null)
                                {
                                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AssignedIntervalRequiresARegisterRecord);
                                    return;
                                }
                                oldRegister.assignedInterval = null;
                            }

                            interval.physReg = regNum;
                            interval.assignedReg = regRecord;
                            var assignedRegister = regRecord;
                            if (assignedRegister is null)
                            {
                                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RegisterAssignmentRequiresARegisterRecord);
                                return;
                            }
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
                    FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnsupportedFinalAllocationReferenceTypeArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? reference.refType : default)
    );
                    return;
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
                        var copiedRegister = regRecord;
                        if (copiedRegister is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.CopyRequiresADestinationRegister);
                            return;
                        }
                        copiedRegister.assignedInterval = null;
                        var homeRegister = interval.assignedReg;
                        if (homeRegister is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.CopyRequiresAHomeRegister);
                            return;
                        }

                        regRecord = homeRegister;
                    }

                    if (reference.spillAfter || reference.lastUse)
                    {
                        assert(!reference.spillAfter || reference.IsActualRef());
                        if (RefTypeIsDef(reference.refType) &&
                            (interval.physReg != REG_NA) && (interval.physReg != regNum))
                        {
                            var oldRegister = interval.assignedReg;
                            if (oldRegister is null)
                            {
                                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.AssignedIntervalRequiresARegisterRecord);
                                return;
                            }
                            oldRegister.assignedInterval = null;
                        }

                        interval.physReg = REG_NA;
                        interval.assignedReg = null;
                        if (regRecord is not null)
                        {
                            regRecord.assignedInterval = null;
                        }
                        else if (Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE &&
                            interval.isUpperVector && !reference.RegOptional())
                        {
                            if (reference.refType is RefType.RefTypeUpperVectorSave or RefType.RefTypeUpperVectorRestore)
                            {
                                var local = interval.relatedInterval;
                                if (local is null)
                                {
                                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.UpperVectorReferencesRequireARelatedInterval);
                                    return;
                                }
                                assert((local.physReg == REG_NA) || local.isPartiallySpilled ||
                                    reference.IsExtraUpperVectorSave());
                            }
                        }
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

    [Conditional("DEBUG")]
    private void dumpVerificationEvent(
        RefPosition reference,
        string action,
        Interval? interval = null,
        bool printRecords = false,
        regMaskTP registerMask = default)
    {
        dumpVerificationEvent(reference, action, interval, REG_NA, printRecords, registerMask);
    }

    [Conditional("DEBUG")]
    private void dumpVerificationEvent(RefPosition reference, string action, Interval? interval,
        regNumber register, bool printRecords = false, regMaskTP registerMask = default)
    {
        if (!VERBOSE)
        {
            return;
        }

        if ((interval is not null) && (register != REG_NA) && (register != REG_STK))
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

    [Conditional("DEBUG")]
    private void dumpVerificationIndentedEvent(string action)
    {
        dumpRefPositionShort(null);
        jitprintf(action);
    }
}
