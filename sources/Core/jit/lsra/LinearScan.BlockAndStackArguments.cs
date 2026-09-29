// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildBlockStore(GenTreeBlk block)
    {
#if TARGET_XARCH
        var destinationAddress = block.Addr;
        var source = block.Data;
        var size = block.Size;
        GenTree? sourceAddressOrFill = null;
        var sourceCandidates = SRBM_NONE;
#if TARGET_X86
        RefPosition? internalIntDef = null;
        var internalIsByte = false;
#endif

        if (block.IsInitBlkOp)
        {
            if (source.Oper is GT_INIT_VAL)
            {
                assert(source.IsContained);
                source = source.AsUnOp().Op1;
            }

            sourceAddressOrFill = source;
            switch (block._kind)
            {
                case GenTreeBlk.BlkOpKindUnroll:
                {
                    var useSimdMove = size >= XMM_REGSIZE_BYTES;
                    if (useSimdMove && block.IsOnHeapAndContainsReferences)
                    {
                        var layout = block.Layout;
                        var simdCandidates = 0u;
                        var continuousNonGc = 0u;
                        for (var slot = 0; slot < layout.SlotCount; slot++)
                        {
                            if (layout.IsGCPtr(slot))
                            {
                                simdCandidates += (continuousNonGc * TARGET_POINTER_SIZE) / XMM_REGSIZE_BYTES;
                                continuousNonGc = 0;
                            }
                            else
                            {
                                continuousNonGc++;
                            }
                        }

                        simdCandidates += (continuousNonGc * TARGET_POINTER_SIZE) / XMM_REGSIZE_BYTES;
                        useSimdMove = simdCandidates > 1;
                    }

                    if (useSimdMove)
                    {
                        _ = buildInternalFloatRegisterDefForNode(block, internalFloatRegCandidates());
                        setContainsAVXFlags(source.IsIntegralConst(0)
                            ? XMM_REGSIZE_BYTES
                            : (uint)_compiler.roundDownSimdSize(size));
                    }

#if TARGET_X86
                    if ((size & 1) != 0)
                    {
                        sourceCandidates = _availableIntRegs & ~RBM_NON_BYTE_REGS.GetIntRegSet();
                    }
#endif
                    break;
                }

                case GenTreeBlk.BlkOpKindLoop:
                {
                    _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                    break;
                }

                default:
                {
                    throw new FatalJitException($"Unsupported initialization block kind {block._kind}.");
                }
            }
        }
        else
        {
            if (source.Oper is GT_IND)
            {
                assert(source.IsContained);
                sourceAddressOrFill = source.AsIndir().Addr;
            }

            switch (block._kind)
            {
                case GenTreeBlk.BlkOpKindUnroll:
                {
                    var registerSize = (uint)_compiler.roundDownSimdSize(size);
                    var remainder = size;
                    if ((size >= registerSize) && (registerSize > 0))
                    {
                        _ = buildInternalFloatRegisterDefForNode(block, internalFloatRegCandidates());
                        setContainsAVXFlags(registerSize);
                        remainder %= registerSize;
                    }

                    if ((remainder > 0) &&
                        ((registerSize == 0) || (uint.IsPow2(remainder) && (remainder <= REGSIZE_BYTES))))
                    {
                        var registerCandidates = _availableIntRegs;
#if TARGET_X86
                        if ((size & 1) != 0)
                        {
                            registerCandidates &= ~RBM_NON_BYTE_REGS.GetIntRegSet();
                            internalIsByte = true;
                        }
                        internalIntDef = buildInternalIntRegisterDefForNode(block, registerCandidates);
#else
                        _ = buildInternalIntRegisterDefForNode(block, registerCandidates);
#endif
                    }

                    break;
                }

                case GenTreeBlk.BlkOpKindUnrollMemmove:
                {
#if TARGET_X86
                    assert(false, "TARGET_POINTER_SIZE == 8");
#endif
                    assert(size > 0);
                    var simdSize = (uint)_compiler.roundDownSimdSize(size);
                    if ((size >= simdSize) && (simdSize > 0))
                    {
                        var simdRegisters = size / simdSize;
                        if ((size % simdSize) != 0)
                        {
                            simdRegisters++;
                        }

                        for (var index = 0u; index < simdRegisters; index++)
                        {
                            _ = buildInternalFloatRegisterDefForNode(block, internalFloatRegCandidates());
                        }

                        setContainsAVXFlags(simdSize);
                    }
                    else if (uint.IsPow2(size))
                    {
                        _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                    }
                    else
                    {
                        _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                        _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                    }

                    break;
                }

                default:
                {
                    throw new FatalJitException($"Unsupported copy block kind {block._kind}.");
                }
            }
        }

        var useCount = 0;
        if (!destinationAddress.IsContained)
        {
            useCount++;
            _ = buildUse(destinationAddress,
                forceLowGprForApxIfNeeded(destinationAddress, SRBM_NONE, _evexIsSupported));
        }
        else if (destinationAddress.Oper.IsAddrMode)
        {
            useCount += buildAddrUses(destinationAddress,
                forceLowGprForApxIfNeeded(destinationAddress, SRBM_NONE, _evexIsSupported));
        }

        if (sourceAddressOrFill is not null)
        {
            if (!sourceAddressOrFill.IsContained)
            {
                useCount++;
                _ = buildUse(sourceAddressOrFill,
                    forceLowGprForApxIfNeeded(sourceAddressOrFill, sourceCandidates, _evexIsSupported));
            }
            else if (sourceAddressOrFill.Oper.IsAddrMode)
            {
                useCount += buildAddrUses(sourceAddressOrFill,
                    forceLowGprForApxIfNeeded(sourceAddressOrFill, SRBM_NONE, _evexIsSupported));
            }
        }

#if TARGET_X86
        // x86 has four byte registers; four incoming address uses can otherwise occupy them all.
        assert((useCount < 4) || !block.IsInitBlkOp);
        if (internalIsByte && (useCount >= 4))
        {
            if (internalIntDef is null)
            {
                noway_assert(false, "An x86 byte block store requires an internal integer definition.");
                throw new FatalJitException("An x86 byte block store requires an internal integer definition.");
            }

            internalIntDef.registerAssignment = SRBM_RAX;
        }
#endif

        buildInternalRegisterUses();
        buildKills(block, getKillSetForBlockStore(block));
        return useCount;
#elif TARGET_ARM64
        var destinationAddress = block.Addr;
        var source = block.Data;
        var size = block.Size;
        GenTree? sourceAddressOrFill = null;

        if (block.IsInitBlkOp)
        {
            if (source.Oper is GT_INIT_VAL)
            {
                assert(source.IsContained);
                source = source.AsUnOp().Op1;
            }

            sourceAddressOrFill = source;
            switch (block._kind)
            {
                case GenTreeBlk.BlkOpKindUnroll:
                {
                    if (destinationAddress.IsContained)
                    {
                        _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                    }

                    if (size > 16)
                    {
                        _ = buildInternalFloatRegisterDefForNode(block, internalFloatRegCandidates());
                    }

                    break;
                }

                case GenTreeBlk.BlkOpKindLoop:
                {
                    _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                    break;
                }

                default:
                {
                    throw new FatalJitException($"Unsupported ARM64 initialization block kind {block._kind}.");
                }
            }
        }
        else
        {
            if (source.Oper is GT_IND)
            {
                assert(source.IsContained);
                sourceAddressOrFill = source.AsIndir().Addr;
            }

            switch (block._kind)
            {
                case GenTreeBlk.BlkOpKindUnroll:
                {
                    _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                    if (size >= (2 * REGSIZE_BYTES))
                    {
                        _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                    }

                    if (size >= 32)
                    {
                        _ = buildInternalFloatRegisterDefForNode(block, internalFloatRegCandidates());
                        _ = buildInternalFloatRegisterDefForNode(block, internalFloatRegCandidates());
                    }

                    var sourceIsLocal = source.Oper is GT_LCL_VAR or GT_LCL_FLD ||
                        ((sourceAddressOrFill is not null) && (sourceAddressOrFill.Oper is GT_LCL_ADDR));
                    var destinationIsLocal = destinationAddress.Oper is GT_LCL_ADDR;
                    if ((sourceIsLocal || (sourceAddressOrFill?.IsContained is true)) &&
                        (destinationIsLocal || destinationAddress.IsContained))
                    {
                        _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                    }

                    break;
                }

                case GenTreeBlk.BlkOpKindUnrollMemmove:
                {
                    assert(size > 0);
                    if (size >= 16)
                    {
                        var registerCount = (size + 15) / 16;
                        for (var index = 0; index < registerCount; index++)
                        {
                            _ = buildInternalFloatRegisterDefForNode(block, internalFloatRegCandidates());
                        }
                    }
                    else
                    {
                        var registerCount = uint.IsPow2(size) ? 1 : 2;
                        for (var index = 0; index < registerCount; index++)
                        {
                            _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                        }
                    }

                    break;
                }

                default:
                {
                    throw new FatalJitException($"Unsupported ARM64 copy block kind {block._kind}.");
                }
            }
        }

        var useCount = 0;
        if (!destinationAddress.IsContained)
        {
            _ = buildUse(destinationAddress);
            useCount++;
        }
        else if (destinationAddress.Oper.IsAddrMode)
        {
            var baseAddress = destinationAddress.AsAddrMode().BaseAddress
                ?? throw new FatalJitException("Contained ARM64 block destination requires a base address.");
            useCount += buildAddrUses(baseAddress);
        }

        if (sourceAddressOrFill is not null)
        {
            if (!sourceAddressOrFill.IsContained)
            {
                _ = buildUse(sourceAddressOrFill);
                useCount++;
            }
            else if (sourceAddressOrFill.Oper.IsAddrMode)
            {
                var baseAddress = sourceAddressOrFill.AsAddrMode().BaseAddress
                    ?? throw new FatalJitException("Contained ARM64 block source requires a base address.");
                useCount += buildAddrUses(baseAddress);
            }
        }

        buildInternalRegisterUses();
        buildKills(block, getKillSetForBlockStore(block));
        return useCount;
#else
        NYI("LinearScan.buildBlockStore outside xarch and ARM64");
        throw new FatalJitException("LinearScan.buildBlockStore outside xarch and ARM64.");
#endif
    }

    private int buildPutArgStk(GenTreePutArgStk argument)
    {
#if TARGET_AMD64
        var source = argument.Op1;
        if (source.Oper is GT_FIELD_LIST)
        {
            assert(source.IsContained);
            var fields = source.AsFieldList();
            RefPosition? simdTemp = null;

            foreach (var field in fields.Uses)
            {
                if ((field.Type is TYP_SIMD12) && (simdTemp is null))
                {
                    simdTemp = buildInternalFloatRegisterDefForNode(argument, _availableFloatRegs);
                }
            }

            var fieldCount = 0;
            foreach (var field in fields.Uses)
            {
                fieldCount += buildOperandUses(field.Node);
            }

            buildInternalRegisterUses();
            return fieldCount;
        }

        if (source.Type is not TYP_STRUCT)
        {
            return buildOperandUses(source);
        }

        var loadSize = argument.ArgLoadSize;
        switch (argument._kind)
        {
            case GenTreePutArgStk.Kind.Unroll:
            {
                if ((loadSize % XMM_REGSIZE_BYTES) != 0)
                {
                    _ = buildInternalIntRegisterDefForNode(argument, _availableIntRegs);
                }

                if (loadSize >= XMM_REGSIZE_BYTES)
                {
                    _ = buildInternalFloatRegisterDefForNode(argument, internalFloatRegCandidates());
                    setContainsAVXFlags();
                }

                break;
            }

            case GenTreePutArgStk.Kind.RepInstr:
            case GenTreePutArgStk.Kind.PartialRepInstr:
            {
                _ = buildInternalIntRegisterDefForNode(argument, SRBM_RDI);
                _ = buildInternalIntRegisterDefForNode(argument, SRBM_RCX);
                _ = buildInternalIntRegisterDefForNode(argument, SRBM_RSI);
                break;
            }

            default:
            {
                throw new FatalJitException($"Unsupported stack argument kind {argument._kind}.");
            }
        }

        var sourceCount = buildOperandUses(source);
        buildInternalRegisterUses();
        return sourceCount;
#elif TARGET_ARM64
        var source = argument.Op1;
        var sourceCount = 0;
        if (source.Type is TYP_STRUCT)
        {
            if (source.Oper is GT_FIELD_LIST)
            {
                assert(source.IsContained);
                foreach (var field in source.AsFieldList().Uses)
                {
                    _ = buildUse(field.Node);
                    sourceCount++;
#if FEATURE_SIMD
                    if (field.Type is TYP_SIMD12)
                    {
                        _ = buildInternalIntRegisterDefForNode(field.Node, _availableIntRegs);
                    }
#endif
                }
            }
            else
            {
                _ = buildInternalIntRegisterDefForNode(argument, _availableIntRegs);
                _ = buildInternalIntRegisterDefForNode(argument, _availableIntRegs);
                assert(source.IsContained);
                if (source.Oper is GT_BLK)
                {
                    sourceCount = buildOperandUses(source.AsBlk().Addr);
                }
                else
                {
                    assert(source.Oper is GT_LCL_VAR or GT_LCL_FLD);
                }
            }
        }
        else
        {
            assert(!source.IsContained);
            sourceCount = buildOperandUses(source);
#if FEATURE_SIMD
            if (compAppleArm64Abi() && (argument.StackByteSize is 12))
            {
                _ = buildInternalIntRegisterDefForNode(argument, _availableIntRegs);
            }
#endif
        }

        buildInternalRegisterUses();
        return sourceCount;
#else
        NYI("LinearScan.buildPutArgStk outside AMD64");
        throw new FatalJitException("LinearScan.buildPutArgStk outside AMD64.");
#endif
    }
}
