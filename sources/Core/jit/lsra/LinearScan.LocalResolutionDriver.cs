// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    internal void resolveRegistersWithLocals()
    {
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
        if (!_enregisterLocalVars)
        {
            throw new FatalJitException("Local register resolution requires enregistered locals.");
        }

        // Reconstruct assignments from entry definitions rather than trusting allocation's final register state.
        for (var index = 0; (int)_regIndices[index] < _availableRegCount; index++)
        {
            var record = getRegisterRecord(_regIndices[index]);
            var assigned = record.assignedInterval;
            if (assigned is not null)
            {
                assigned.assignedReg = null;
                assigned.physReg = REG_NA;
            }
            record.assignedInterval = null;
            record.recentRefPosition = null;
        }

        assert(localVarIntervals is not null);
        for (var index = 0; index < _compiler.lvaTrackedCount; index++)
        {
            var interval = localVarIntervals[index];
            if (interval is not null)
            {
                interval.recentRefPosition = null;
                interval.isActive = false;
            }
            else
            {
                assert(!getTrackedLocal(index).lvLRACandidate);
            }
        }

        var referenceIndex = 0;
        var firstBlock = _compiler.fgFirstBB
            ?? throw new FatalJitException("Local register resolution requires an entry block.");
        var entryMap = getInVarToRegMap(checked((uint)firstBlock.bbNum))
            ?? throw new FatalJitException("Local register resolution requires an entry variable map.");
        for (; referenceIndex < refPositions.Count; referenceIndex++)
        {
            var reference = refPositions[referenceIndex];
            if (reference.refType is not RefType.RefTypeParamDef and not RefType.RefTypeZeroInit)
            {
                break;
            }

            var interval = reference.getInterval();
            assert(interval.isLocalVar);
            resolveLocalRef(null, null, reference);
            var register = REG_STK;
            if (!reference.spillAfter && (reference.registerAssignment != SRBM_NONE))
            {
                register = reference.assignedReg();
            }
            else
            {
                interval.isActive = false;
            }
            setVarReg(entryMap, interval.getVarIndex(_compiler), register);
        }

        for (var block = startBlockSequence(); block is not null; block = moveToNextBlock())
        {
            assert(_currentBlockNumber == block.bbNum);
            if (referenceIndex >= refPositions.Count)
            {
                throw new FatalJitException("Local register resolution requires a block reference.");
            }
            _currentBlockStartLocation = refPositions[referenceIndex].nodeLocation;
            if (block != firstBlock)
            {
                processBlockStartLocations(block);
            }

            for (; referenceIndex < refPositions.Count; referenceIndex++)
            {
                var reference = refPositions[referenceIndex];
                if (reference.refType is not RefType.RefTypeDummyDef)
                {
                    break;
                }

                assert(reference.isIntervalRef());
                reference.reload = false;
                resolveLocalRef(null, null, reference);
                var interval = reference.getInterval();
                var register = reference.registerAssignment != SRBM_NONE ? reference.assignedReg() : REG_STK;
                if (register == REG_STK)
                {
                    interval.isActive = false;
                }
                setInVarRegForBB(_currentBlockNumber, interval.varNum, register);
            }

            if (referenceIndex >= refPositions.Count || refPositions[referenceIndex].refType is not RefType.RefTypeBB)
            {
                throw new FatalJitException("Local register resolution requires an ordered block reference.");
            }
            referenceIndex++;

            for (; referenceIndex < refPositions.Count; referenceIndex++)
            {
                var reference = refPositions[referenceIndex];
                var kind = reference.refType;
                if (kind is RefType.RefTypeBB or RefType.RefTypeDummyDef)
                {
                    break;
                }

                assert(!reference.reload || (!reference.copyReg && !reference.moveReg));
                assert(!reference.copyReg || !reference.moveReg);
                switch (kind)
                {
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                    case RefType.RefTypeUpperVectorSave:
                    case RefType.RefTypeUpperVectorRestore:
#endif
                    case RefType.RefTypeUse:
                    case RefType.RefTypeDef:
                    {
                        break;
                    }

                    case RefType.RefTypeFixedReg:
                    case RefType.RefTypeExpUse:
                    {
                        if (kind is RefType.RefTypeExpUse)
                        {
                            var nextBlock = getNextBlock();
                            assert(nextBlock is null ||
                                !VarSetOps.IsMember(_compiler, nextBlock.bbLiveIn,
                                    checked((int)reference.getInterval().getVarIndex(_compiler))));
                        }
                        assert(reference.referent is not null);
                        reference.referent.recentRefPosition = reference;
                        continue;
                    }

                    case RefType.RefTypeKill:
                    case RefType.RefTypeKillGCRefs:
                    {
                        continue;
                    }

                    default:
                    {
                        throw new FatalJitException($"Unexpected local resolution reference: {kind}.");
                    }
                }

                updateMaxSpill(reference);
                var tree = reference.treeNode;
                var interval = reference.getInterval();
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                if (kind is RefType.RefTypeUpperVectorSave)
                {
                    if (tree is null)
                    {
                        throw new FatalJitException("Upper-vector saves require a kill node.");
                    }
                    if (interval.isUpperVector)
                    {
                        var local = interval.relatedInterval
                            ?? throw new FatalJitException("An upper-vector interval requires its local.");
                        if (local.physReg != REG_NA && !local.isPartiallySpilled)
                        {
                            if (!reference.skipSaveRestore)
                            {
#if DEBUG
                                assert((regMaskTP.CreateFromRegNum(local.physReg,
                                    genSingleTypeRegMask(local.physReg)) & getKillSetForNode(tree)).IsEmpty);
#endif
                                var referent = reference.referent
                                    ?? throw new FatalJitException("An upper-vector save requires its interval.");
                                referent.recentRefPosition = reference;
                                insertUpperVectorSave(tree, reference, interval, block);
                            }
                            if (!reference.IsExtraUpperVectorSave())
                            {
                                local.isPartiallySpilled = true;
                            }
                            else
                            {
                                assert(!reference.liveVarUpperSave);
                            }
                        }
                    }
                    else
                    {
                        assert(!interval.isLocalVar && interval.firstRefPosition!.spillAfter);
                    }
                    continue;
                }
                if (kind is RefType.RefTypeUpperVectorRestore)
                {
                    var local = interval.relatedInterval
                        ?? throw new FatalJitException("Upper-vector restores require a local.");
                    assert(interval.isUpperVector);
                    if (local.physReg != REG_NA)
                    {
                        assert(local.isPartiallySpilled);
                        assert(local.assignedReg is not null &&
                            local.assignedReg.regNum == local.physReg &&
                            ReferenceEquals(local.assignedReg.assignedInterval, local));
                        if (!reference.skipSaveRestore)
                        {
                            insertUpperVectorRestore(tree, reference, interval, block);
                        }
                    }
                    local.isPartiallySpilled = false;
                    continue;
                }
#endif
                if (tree is null)
                {
                    continue;
                }

                assert(reference.isIntervalRef());
                if (interval.isInternal)
                {
                    _compiler.codeGen!.InternalRegisters.Add(tree, new regMaskTP(reference.registerAssignment));
                    continue;
                }

                writeRegisters(reference, tree);
                if (tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR && interval.isLocalVar)
                {
                    resolveLocalRef(block, tree.AsLclVar(), reference);
                }
                else if (reference.spillAfter || reference.nextRefPosition?.moveReg == true)
                {
                    if (reference.spillAfter)
                    {
                        tree.Flags |= GTF_SPILL;
                        if (tree.IsReuseRegVal)
                        {
                            tree.IsReuseRegVal = false;
                        }
                        if (tree.IsMultiRegNode)
                        {
                            tree.SetRegSpillFlagByIdx(GTF_SPILL, checked((int)reference.getMultiRegIdx()));
                        }
                    }

                    var nextReference = reference.nextRefPosition
                        ?? throw new FatalJitException("A spilled or moved temporary requires its next reference.");
                    if (
#if DEBUG
                        alwaysInsertReload() ||
#endif
                        nextReference.assignedReg() != reference.assignedReg())
                    {
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                        if (!interval.isUpperVector &&
                            nextReference.refType is RefType.RefTypeUpperVectorSave)
                        {
                            assert(!interval.isLocalVar);
                            nextReference = nextReference.nextRefPosition
                                ?? throw new FatalJitException("A saved temporary requires a later use.");
                            assert(nextReference.refType is not RefType.RefTypeUpperVectorSave);
                        }
                        if (!interval.isUpperVector)
#endif
                        {
                            if (nextReference.assignedReg() is not REG_NA)
                            {
                                insertCopyOrReload(block, tree, reference.getMultiRegIdx(), nextReference);
                            }
                            else
                            {
                                assert(nextReference.RegOptional());
                                if (reference.spillAfter && kind is RefType.RefTypeDef &&
                                    nextReference.refType is RefType.RefTypeUse)
                                {
                                    assert(nextReference.treeNode is null);
                                    tree.Flags |= GTF_NOREG_AT_USE;
                                }
                            }
                        }
                    }
                }
            }

            processBlockEndLocations(block);
        }

#if DEBUG
        if (VERBOSE)
        {
            dumpLocalResolutionBoundaries();
        }
#endif
        resolveEdges();
        finalizeLocalRegisterAssignments();

#if DEBUG
        if (VERBOSE)
        {
            jitprintf("Trees after linear scan register allocator (LSRA)\n");
            _compiler.fgDispBasicBlocks(true);
        }
        verifyFinalAllocationWithLocals();
#endif
        _compiler.raMarkStkVars();
        recordMaxSpill();
#else
        throw new FatalJitException("Local register resolution is not implemented outside Windows AMD64.");
#endif
    }
}
