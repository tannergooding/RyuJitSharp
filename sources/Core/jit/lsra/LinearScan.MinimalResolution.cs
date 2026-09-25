// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if DEBUG
    private bool alwaysInsertReload() => (_lsraStressMask & 0x400) != 0;
#endif

    private void resolveRegistersMinimal()
    {
        if (_enregisterLocalVars)
        {
            throw new FatalJitException("Minimal register resolution cannot run with enregistered locals.");
        }

#if TARGET_AMD64 && !UNIX_AMD64_ABI
        var referenceIndex = 0;
        assert((refPositions.Count == 0) ||
            (refPositions[0].refType is not RefType.RefTypeParamDef and not RefType.RefTypeZeroInit));
        assert(_compiler.codeGen is not null);

        for (var block = startBlockSequence(); block is not null; block = moveToNextBlock())
        {
            assert(_currentBlockNumber == block.bbNum);
            assert(referenceIndex < refPositions.Count);
            assert(refPositions[referenceIndex].refType is RefType.RefTypeBB);
            referenceIndex++;

            for (; referenceIndex < refPositions.Count; referenceIndex++)
            {
                var reference = refPositions[referenceIndex];
                var refType = reference.refType;
                if (refType is RefType.RefTypeBB or RefType.RefTypeDummyDef)
                {
                    break;
                }

                assert(!reference.reload || (!reference.copyReg && !reference.moveReg));
                assert(!reference.copyReg || !reference.moveReg);
                switch (refType)
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
                    {
                        assert(reference.referent is not null);
                        reference.referent.recentRefPosition = reference;
                        continue;
                    }

                    case RefType.RefTypeExpUse:
                    {
                        var nextBlock = getNextBlock();
                        assert((nextBlock is null) ||
                            !VarSetOps.IsMember(_compiler, nextBlock.bbLiveIn,
                                checked((int)reference.getInterval().getVarIndex(_compiler))));
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
                        unreached();
                        throw new FatalJitException($"Unexpected reference kind during register resolution: {refType}.");
                    }
                }

                updateMaxSpill(reference);
                var tree = reference.treeNode;
                var interval = reference.getInterval();
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                if (refType is RefType.RefTypeUpperVectorSave)
                {
                    assert(tree is not null);
                    // Only enregistered locals have upper-half intervals; tree temps spill in full.
                    assert(!interval.isUpperVector && !interval.isLocalVar);
                    if (interval.isUpperVector || interval.isLocalVar)
                    {
                        throw new FatalJitException("Minimal resolution cannot save an enregistered local vector.");
                    }
                    assert(interval.firstRefPosition is not null);
                    assert(interval.firstRefPosition.spillAfter);
                    continue;
                }
                else if (refType is RefType.RefTypeUpperVectorRestore)
                {
                    throw new FatalJitException("Upper-vector restores require enregistered-local resolution.");
                }
#endif
                if (tree is null)
                {
                    assert((refType is RefType.RefTypeUse) || (reference.registerAssignment == SRBM_NONE) ||
                        interval.isStructField || interval.IsUpperVector());
                    assert(!interval.isStructField || (!reference.reload && !reference.spillAfter));
                    if (interval.isLocalVar && !interval.isStructField)
                    {
                        assert(refType is RefType.RefTypeDef);
                        interval.getLocalVar(_compiler).RegNum = REG_STK;
                    }
                    continue;
                }

                assert(reference.isIntervalRef());
                if (interval.isInternal)
                {
                    _compiler.codeGen.InternalRegisters.Add(tree, new regMaskTP(reference.registerAssignment));
                }
                else
                {
                    writeRegisters(reference, tree);
                    if (reference.spillAfter || (reference.nextRefPosition?.moveReg == true))
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

                        var nextReference = reference.nextRefPosition;
                        noway_assert(nextReference is not null);
                        if (
#if DEBUG
                            alwaysInsertReload() ||
#endif
                            (nextReference.assignedReg() != reference.assignedReg()))
                        {
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                            if (!interval.isUpperVector &&
                                (nextReference.refType is RefType.RefTypeUpperVectorSave))
                            {
                                assert(!interval.isLocalVar);
                                nextReference = nextReference.nextRefPosition;
                                assert(nextReference is not null);
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
                                    if (reference.spillAfter && (refType is RefType.RefTypeDef) &&
                                        (nextReference.refType is RefType.RefTypeUse))
                                    {
                                        assert(nextReference.treeNode is null);
                                        tree.Flags |= GTF_NOREG_AT_USE;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

#if DEBUG
        if (VERBOSE)
        {
            jitprintf("Trees after linear scan register allocator (LSRA)\n");
            _compiler.fgDispBasicBlocks(true);
        }
        verifyFinalAllocationMinimal();
#endif
        _compiler.raMarkStkVars();
        recordMaxSpill();
#else
        NYI("LinearScan.resolveRegistersMinimal outside Windows AMD64");
        throw new FatalJitException("LinearScan.resolveRegistersMinimal outside Windows AMD64.");
#endif
    }
}
