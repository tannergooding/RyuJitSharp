// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    [Conditional("DEBUG")]
    private void verifyFinalAllocationWithLocals()
    {
        assert(_enregisterLocalVars);
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

        BasicBlock? block = null;
        GenTree? firstEndMove = null;
        var location = MinLocation;
        foreach (var reference in refPositions)
        {
            Interval? interval = null;
            RegRecord? record = null;
            var register = REG_NA;
            _activeRefPosition = reference;
            if (reference.refType is not RefType.RefTypeBB)
            {
                if (reference.IsPhysRegRef())
                {
                    record = reference.getReg();
                    record.recentRefPosition = reference;
                    register = record.regNum;
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
                            register = reference.assignedReg();
                            record = getRegisterRecord(register);
                        }
                    }
                }
            }

            location = reference.nodeLocation;
            switch (reference.refType)
            {
                case RefType.RefTypeBB:
                {
                    if (block is null)
                    {
                        block = startBlockSequence();
                    }
                    else
                    {
                        for (var node = firstEndMove; node is not null; node = node.Next)
                        {
                            if (isResolutionMove(node))
                            {
                                verifyResolutionMoveWithLocals(node, location);
                            }
                        }

                        var outMap = getOutVarToRegMap(checked((uint)block.bbNum));
                        if (outMap is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.FinalAllocationVerificationRequiresAnOutgoingMap);
                            return;
                        }
                        _ = VarSetOps.VisitBits(_compiler, block.bbLiveOut, index => {
                            var local = localVarIntervals?[index];
                            if (local is null)
                            {
                                assert(!getTrackedLocal(index).lvLRACandidate);
                                return true;
                            }
                            var expected = getVarReg(outMap, checked((uint)index));
                            if (local.physReg != expected)
                            {
                                assert(expected == REG_STK &&
                                    (local.physReg == REG_NA || local.isWriteThru));
                            }
                            local.physReg = REG_NA;
                            local.assignedReg = null;
                            local.isActive = false;
                            return true;
                        });

                        for (var index = 0; (int)_regIndices[index] < _availableRegCount; index++)
                        {
                            getRegisterRecord(_regIndices[index]).assignedInterval = null;
                        }
                        block = moveToNextBlock();
                    }

                    if (block is not null)
                    {
                        var inMap = getInVarToRegMap(checked((uint)block.bbNum));
                        if (inMap is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.FinalAllocationVerificationRequiresAnIncomingMap);
                            return;
                        }
                        _ = VarSetOps.VisitBits(_compiler, block.bbLiveIn, index => {
                            var local = localVarIntervals?[index];
                            if (local is null)
                            {
                                assert(!getTrackedLocal(index).lvLRACandidate);
                                return true;
                            }
                            var incoming = getVarReg(inMap, checked((uint)index));
                            local.physReg = incoming;
                            local.assignedReg = getRegisterRecord(incoming);
                            local.isActive = true;
                            local.assignedReg.assignedInterval = local;
                            return true;
                        });

                        if (VERBOSE)
                        {
                            dumpRefPositionShort(reference, block);
                            dumpAllocationRegisterRecords();
                        }
                        firstEndMove = null;
                        var seenNonResolutionNode = false;
                        foreach (var node in block)
                        {
                            if (isResolutionNode(block, node))
                            {
                                if (seenNonResolutionNode)
                                {
                                    firstEndMove = node;
                                    break;
                                }
                                if (isResolutionMove(node))
                                {
                                    verifyResolutionMoveWithLocals(node, location);
                                }
                            }
                            else
                            {
                                seenNonResolutionNode = true;
                            }
                        }
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
                    assert(record is not null);
                    dumpVerificationEvent(reference, $"Keep     {record.regNum.Name,-4} ");
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
                    if (interval is null)
                    {
                        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.VerifiedAllocationReferenceRequiresAnInterval);
                        return;
                    }
                    if (interval.isSpecialPutArg)
                    {
                        dumpVerificationEvent(reference, $"PtArg    {register.Name,-4} ",
                            interval: interval, register: register);
                        break;
                    }
                    if (reference.reload)
                    {
                        interval.isActive = true;
                        assert(register != REG_NA);
                        interval.physReg = register;
                        interval.assignedReg = record;
                        var reloaded = record;
                        if (reloaded is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ReloadRequiresAnAssignedRegister);
                            return;
                        }
                        reloaded.assignedInterval = interval;
                        dumpVerificationEvent(reference, $"ReLod    {register.Name,-4} ", printRecords: true);
                    }
                    if (register == REG_NA)
                    {
                        if (interval.physReg != REG_NA && RefTypeIsDef(reference.refType))
                        {
                            var previous = interval.assignedReg;
                            if (previous is null)
                            {
                                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.DefinitionRequiresAnAssignedRegister);
                                return;
                            }
                            if (ReferenceEquals(previous.assignedInterval, interval))
                            {
                                previous.assignedInterval = null;
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
                            var reuse = interval.isConstant && reference.treeNode?.IsReuseRegVal == true;
                            dumpVerificationEvent(reference, $"{(reuse ? "Reuse" : "Alloc"),-5}    {register.Name,-4} ");
                        }
                    }
                    else if (reference.copyReg)
                    {
                        dumpVerificationEvent(reference, $"Copy     {register.Name,-4} ",
                            interval: interval, register: register);
                    }
                    else if (reference.moveReg)
                    {
                        var previous = interval.assignedReg;
                        if (previous is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.MovedIntervalRequiresAnAssignedRegister);
                            return;
                        }
                        previous.assignedInterval = null;
                        interval.physReg = register;
                        interval.assignedReg = record;
                        var moved = record;
                        if (moved is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.MoveRequiresADestinationRegister);
                            return;
                        }
                        moved.assignedInterval = interval;
                        if (VERBOSE)
                        {
                            dumpVerificationIndentedEvent($"Move     {register.Name,-4} ");
                        }
                    }
                    else
                    {
                        dumpVerificationEvent(reference, $"Keep     {register.Name,-4} ");
                    }
                    if (reference.lastUse || (reference.spillAfter && !reference.writeThru))
                    {
                        interval.isActive = false;
                    }
                    if (register != REG_NA)
                    {
                        if (reference.spillAfter)
                        {
                            if (VERBOSE)
                            {
                                var spillRegister = reference.copyReg ? interval.physReg : register;
                                dumpAllocationRegisterRecords();
                                dumpVerificationIndentedEvent(
                                    $"{(reference.writeThru ? "WThru" : "Spill"),-5}    {spillRegister.Name,-4} ");
                            }
                        }
                        else if (reference.copyReg)
                        {
                            var copy = record;
                            if (copy is null)
                            {
                                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.CopyRequiresAnAssignedRegister);
                                return;
                            }
                            copy.assignedInterval = interval;
                        }
                        else
                        {
                            if (RefTypeIsDef(reference.refType) &&
                                interval.physReg != REG_NA && interval.physReg != register)
                            {
                                var previous = interval.assignedReg;
                                if (previous is null)
                                {
                                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.DefinitionRequiresAnAssignedRegister);
                                    return;
                                }
                                previous.assignedInterval = null;
                            }
                            interval.physReg = register;
                            interval.assignedReg = record;
                            var assigned = record;
                            if (assigned is null)
                            {
                                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.IntervalRequiresAnAssignedRegister);
                                return;
                            }
                            assigned.assignedInterval = interval;
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
                        var reg = (regNumber)BitOperations.TrailingZeroCount(candidates);
                        candidates &= candidates - 1;
                        var assigned = getRegisterRecord(reg).assignedInterval;
                        assert(assigned is null || !varTypeIsGC(assigned.registerType));
                    }
                    break;
                }

                case RefType.RefTypeExpUse:
                case RefType.RefTypeDummyDef:
                {
                    if (VERBOSE)
                    {
                        dumpRefPositionShort(reference, block);
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
        FatalJitErrorCode.UnsupportedFinalAllocationReferenceArgument,
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
                        assert(interval.physReg != register);
                        var copied = record;
                        if (copied is null)
                        {
                            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.CopiedIntervalRequiresARegister);
                            return;
                        }
                        copied.assignedInterval = null;
                        record = interval.assignedReg;
                        assert(record is not null);
                    }
                    if (reference.spillAfter || reference.lastUse)
                    {
                        assert(!reference.spillAfter || reference.IsActualRef());
                        if (RefTypeIsDef(reference.refType) &&
                            interval.physReg != REG_NA && interval.physReg != register)
                        {
                            var previous = interval.assignedReg;
                            if (previous is null)
                            {
                                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.DefinitionRequiresAnAssignedRegister);
                                return;
                            }
                            previous.assignedInterval = null;
                        }
                        interval.physReg = REG_NA;
                        interval.assignedReg = null;
                        if (record is not null)
                        {
                            record.assignedInterval = null;
                        }
                        else if (Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE &&
                            interval.isUpperVector && !reference.RegOptional())
                        {
                            if (reference.refType is RefType.RefTypeUpperVectorSave or RefType.RefTypeUpperVectorRestore)
                            {
                                var local = interval.relatedInterval;
                                if (local is null)
                                {
                                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.UpperVectorReferencesRequireALocal);
                                    return;
                                }
                                assert(local.physReg == REG_NA || local.isPartiallySpilled ||
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

        foreach (var resolutionBlock in _compiler.Blocks)
        {
            if ((uint)resolutionBlock.bbNum <= _bbNumMaxBeforeResolution)
            {
                continue;
            }
            if (VERBOSE)
            {
                initializeAllocationDumpFormat();
                dumpAllocationRegisterTitle();
                dumpAllocationLocation(0, 0);
                var predecessors = resolutionBlock.PredEdges.GetEnumerator();
                assert(predecessors.MoveNext());
                if (_allocationDumpPredBbNumWidth < _allocationDumpBbNumWidth)
                {
                    jitprintf("BB" + resolutionBlock.bbNum.ToString(CultureInfo.InvariantCulture)
                        .PadRight(_allocationDumpShortRefPositionWidth - 2));
                }
                else
                {
                    jitprintf("BB" + resolutionBlock.bbNum.ToString(CultureInfo.InvariantCulture)
                        .PadRight(_allocationDumpBbNumWidth) + " PredBB" +
                        predecessors.Current.SourceBlock.bbNum.ToString(CultureInfo.InvariantCulture)
                            .PadRight(_allocationDumpPredBbNumWidth));
                }
                dumpAllocationRegisterRecords();
            }
            for (var index = 0; (int)_regIndices[index] < _availableRegCount; index++)
            {
                getRegisterRecord(_regIndices[index]).assignedInterval = null;
            }
            var incoming = getInVarToRegMap(checked((uint)resolutionBlock.bbNum));
            if (incoming is null)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ResolutionBlockRequiresAnIncomingMap);
                return;
            }
            _ = VarSetOps.VisitBits(_compiler, resolutionBlock.bbLiveIn, index => {
                var local = localVarIntervals?[index];
                if (local is null)
                {
                    assert(!getTrackedLocal(index).lvLRACandidate);
                    return true;
                }
                var reg = getVarReg(incoming, checked((uint)index));
                local.physReg = reg;
                local.assignedReg = getRegisterRecord(reg);
                local.isActive = true;
                local.assignedReg.assignedInterval = local;
                return true;
            });

            foreach (var node in resolutionBlock)
            {
                assert(isResolutionNode(resolutionBlock, node));
                if (isResolutionMove(node))
                {
                    verifyResolutionMoveWithLocals(node, location);
                }
            }
            var outgoing = getOutVarToRegMap(checked((uint)resolutionBlock.bbNum));
            if (outgoing is null)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ResolutionBlockRequiresAnOutgoingMap);
                return;
            }
            _ = VarSetOps.VisitBits(_compiler, resolutionBlock.bbLiveOut, index => {
                var local = localVarIntervals?[index];
                if (local is null)
                {
                    assert(!getTrackedLocal(index).lvLRACandidate);
                    return true;
                }
                var reg = getVarReg(outgoing, checked((uint)index));
                assert(local.physReg == reg || (local.physReg == REG_NA && reg == REG_STK) ||
                    (local.isWriteThru && reg == REG_STK));
                local.physReg = REG_NA;
                local.assignedReg = null;
                local.isActive = false;
                return true;
            });
        }
        if (VERBOSE)
        {
            jitprintf("\n");
        }
    }
}
