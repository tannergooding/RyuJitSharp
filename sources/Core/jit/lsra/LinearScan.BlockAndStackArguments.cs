// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildBlockStore(GenTreeBlk block)
    {
        if (Configuration.TARGET_XARCH)
        {
            var destinationAddress = block.Addr;
            var source = block.Data;
            var size = block.Size;
            GenTree? sourceAddressOrFill = null;
            var sourceCandidates = SRBM_NONE;
            RefPosition? internalIntDef = null;
            var internalIsByte = false;

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
                        var useSimdMove = size >= Configuration.XMM_REGSIZE_BYTES;
                        if (useSimdMove && block.IsOnHeapAndContainsReferences)
                        {
                            var layout = block.Layout;
                            var simdCandidates = 0u;
                            var continuousNonGc = 0u;
                            for (var slot = 0; slot < layout.SlotCount; slot++)
                            {
                                if (layout.IsGCPtr(slot))
                                {
                                    simdCandidates +=
                                        unchecked((uint)(continuousNonGc * Configuration.TARGET_POINTER_SIZE /
                                            Configuration.XMM_REGSIZE_BYTES));
                                    continuousNonGc = 0;
                                }
                                else
                                {
                                    continuousNonGc++;
                                }
                            }

                            simdCandidates +=
                                unchecked((uint)(continuousNonGc * Configuration.TARGET_POINTER_SIZE /
                                    Configuration.XMM_REGSIZE_BYTES));
                            useSimdMove = simdCandidates > 1;
                        }

                        if (useSimdMove)
                        {
                            _ = buildInternalFloatRegisterDefForNode(block, internalFloatRegCandidates());
                            setContainsAVXFlags(source.IsIntegralConst(0)
                                ? (uint)Configuration.XMM_REGSIZE_BYTES
                                : (uint)_compiler.roundDownSimdSize(size));
                        }

                        if (Configuration.TARGET_X86 && (size & 1) != 0)
                        {
                            sourceCandidates = _availableIntRegs & ~RBM_NON_BYTE_REGS.IntRegSet;
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
                        FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnsupportedInitializationBlockKind,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? block._kind : default)
    );
                        return default!;
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
                            ((registerSize == 0) ||
                             (uint.IsPow2(remainder) && (remainder <= Configuration.REGSIZE_BYTES))))
                        {
                            var registerCandidates = _availableIntRegs;
                            if (Configuration.TARGET_X86)
                            {
                                if ((size & 1) != 0)
                                {
                                    registerCandidates &= ~RBM_NON_BYTE_REGS.IntRegSet;
                                    internalIsByte = true;
                                }

                                internalIntDef = buildInternalIntRegisterDefForNode(block, registerCandidates);
                            }
                            else
                            {
                                _ = buildInternalIntRegisterDefForNode(block, registerCandidates);
                            }
                        }

                        break;
                    }

                    case GenTreeBlk.BlkOpKindUnrollMemmove:
                    {
                        if (Configuration.TARGET_X86)
                        {
                            assert(false, "Configuration.TARGET_POINTER_SIZE == 8");
                        }
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
                        FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnsupportedCopyBlockKind,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? block._kind : default)
    );
                        return default!;
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

            if (Configuration.TARGET_X86)
            {
                // x86 has four byte registers; four incoming address uses can otherwise occupy them all.
                assert((useCount < 4) || !block.IsInitBlkOp);
                if (internalIsByte && (useCount >= 4))
                {
                    if (internalIntDef is null)
                    {
                        noway_assert(false, "An x86 byte block store requires an internal integer definition.");
                        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.X86ByteBlockStoreRequiresAnInternalIntegerDefinition);
                        return default!;
                    }

                    internalIntDef.registerAssignment = SRBM_X86_EAX;
                }
            }

            buildInternalRegisterUses();
            buildKills(block, getKillSetForBlockStore(block));
            return useCount;
        }

        if (Configuration.TARGET_ARM64)
        {
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
                        FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnsupportedARM64InitializationBlockKindArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? block._kind : default)
    );
                        return default!;
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
                        if (size >= (2 * Configuration.REGSIZE_BYTES))
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
                        FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnsupportedARM64CopyBlockKindArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? block._kind : default)
    );
                        return default!;
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
                var baseAddress = destinationAddress.AsAddrMode().BaseAddress;
                if (baseAddress is null)
                {
                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ContainedARM64BlockDestinationRequiresABaseAddress);
                    return default!;
                }
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
                    var baseAddress = sourceAddressOrFill.AsAddrMode().BaseAddress;
                    if (baseAddress is null)
                    {
                        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.ContainedARM64BlockSourceRequiresABaseAddress);
                        return default!;
                    }
                    useCount += buildAddrUses(baseAddress);
                }
            }

            buildInternalRegisterUses();
            buildKills(block, getKillSetForBlockStore(block));
            return useCount;
        }

        if (Configuration.TARGET_RISCV64 || Configuration.TARGET_LOONGARCH64)
        {
            if (Configuration.TARGET_RISCV64)
            {
                assert(block.Oper.IsStoreBlk);
            }

            var destinationAddress = block.Addr;
            var source = block.Data;
            var size = block.Size;
            GenTree? sourceAddressOrFill = null;

            var destinationAddressCandidates = SRBM_NONE;
            var sourceCandidates = SRBM_NONE;
            var sizeCandidates = SRBM_NONE;

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
                            // Code generation computes contained destinations and may need a register for the address.
                            _ = buildInternalIntRegisterDefForNode(block);
                        }

                        var destinationAlignmentIsKnown = destinationAddress.Oper is GT_LCL_ADDR;
                        if (destinationAlignmentIsKnown && (size > Configuration.FP_REGSIZE_BYTES))
                        {
                            // Larger aligned blocks may use SIMD instructions; LSRA still reserves an integer temp.
                            _ = buildInternalIntRegisterDefForNode(block);
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
                        if (Configuration.TARGET_LOONGARCH64)
                        {
                            unreached();
                            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.UnsupportedLoongArch64InitializationBlockKindArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? block._kind : default)
    );
                            return default!;
                        }
                        FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnsupportedRISCVInitializationBlockKindArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? block._kind : default)
    );
                        return default!;
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
                        _ = buildInternalIntRegisterDefForNode(block);
                        break;
                    }

                    default:
                    {
                        if (Configuration.TARGET_LOONGARCH64)
                        {
                            unreached();
                            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.UnsupportedLoongArch64CopyBlockKindArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? block._kind : default)
    );
                            return default!;
                        }
                        FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnsupportedRISCVCopyBlockKindArgument,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? block._kind : default)
    );
                        return default!;
                    }
                }
            }

            if (sizeCandidates != SRBM_NONE)
            {
                _ = buildInternalIntRegisterDefForNode(block, sizeCandidates);
            }

            var useCount = 0;
            if (!destinationAddress.IsContained)
            {
                _ = buildUse(destinationAddress, destinationAddressCandidates);
                useCount++;
            }
            else if (destinationAddress.Oper.IsAddrMode)
            {
                var addressMode = destinationAddress.AsAddrMode();
                if (addressMode.BaseAddress is { } baseAddress)
                {
                    useCount += buildAddrUses(baseAddress, destinationAddressCandidates);
                }
            }

            if (sourceAddressOrFill is not null)
            {
                if (!sourceAddressOrFill.IsContained)
                {
                    _ = buildUse(sourceAddressOrFill, sourceCandidates);
                    useCount++;
                }
                else if (sourceAddressOrFill.Oper.IsAddrMode)
                {
                    var addressMode = sourceAddressOrFill.AsAddrMode();
                    if (addressMode.BaseAddress is { } baseAddress)
                    {
                        useCount += buildAddrUses(baseAddress, sourceCandidates);
                    }
                }
            }

            buildInternalRegisterUses();
            buildKills(block, getKillSetForBlockStore(block));
            return useCount;
        }

        NYI("LinearScan.buildBlockStore on this target");
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LinearScanBuildBlockStoreOnThisTargetIsNotImplemented
    );
        return default!;
    }

    private int buildPutArgStk(GenTreePutArgStk argument)
    {
        if (Configuration.TARGET_XARCH)
        {
            var source = argument.Op1;
            if (source.Oper is GT_FIELD_LIST)
            {
                assert(source.IsContained);
                var fields = source.AsFieldList();
                RefPosition? simdTemp = null;
                RefPosition? intTemp = null;
                var previousOffset = unchecked((uint)argument.StackByteSize);

                foreach (var field in fields.Uses)
                {
                    if (Configuration.TARGET_X86)
                    {
                        assert(field.Type is not TYP_LONG);
                    }
                    if (Configuration.FEATURE_SIMD && (field.Type is TYP_SIMD12) && (simdTemp is null))
                    {
                        simdTemp = buildInternalFloatRegisterDefForNode(argument, _availableFloatRegs);
                    }
                    if (Configuration.TARGET_X86)
                    {
                        var fieldNode = field.Node;
                        var fieldOffset = (uint)field.Offset;
                        if (varTypeIsIntegralOrI(fieldNode.Type))
                        {
                            assert(fieldNode.Type.Size <= Configuration.TARGET_POINTER_SIZE);
                            var canStoreFullSlot = ((fieldOffset % 4) == 0) &&
                                (unchecked(previousOffset - fieldOffset) >= 4);
                            var canLoadFullSlot = (fieldNode.Type.Size == Configuration.TARGET_POINTER_SIZE) ||
                                (fieldNode.Oper.IsLocalRead && (fieldNode.Type.Size >= field.Type.Size));

                            if ((!canStoreFullSlot || !canLoadFullSlot) && (intTemp is null))
                            {
                                intTemp = buildInternalIntRegisterDefForNode(argument, _availableIntRegs);
                            }

                            if (!canStoreFullSlot && varTypeIsByte(field.Type))
                            {
                                var byteTemp = intTemp;
                                if (byteTemp is null)
                                {
                                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.PartialX86FieldStoreRequiresAnInternalRegister);
                                    return default!;
                                }
                                byteTemp.registerAssignment &= _availableIntRegs & ~RBM_NON_BYTE_REGS.IntRegSet;
                            }
                        }

                        previousOffset = fieldOffset;
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
                if (Configuration.FEATURE_SIMD && Configuration.TARGET_X86 && argument.IsSimd12)
                {
                    _ = buildInternalFloatRegisterDefForNode(argument, internalFloatRegCandidates());
                    _ = buildUse(source);
                    buildInternalRegisterUses();
                    return 1;
                }
                return buildOperandUses(source);
            }

            var loadSize = argument.ArgLoadSize;
            switch (argument._kind)
            {
                case GenTreePutArgStk.Kind.Unroll:
                {
                    if ((loadSize % Configuration.XMM_REGSIZE_BYTES) != 0)
                    {
                        var candidates = _availableIntRegs;
                        if (Configuration.TARGET_X86 && (loadSize & 1) != 0)
                        {
                            candidates &= ~RBM_NON_BYTE_REGS.IntRegSet;
                        }
                        _ = buildInternalIntRegisterDefForNode(argument, candidates);
                    }

                    if (loadSize >= (Configuration.TARGET_X86 ? 8u : Configuration.XMM_REGSIZE_BYTES))
                    {
                        _ = buildInternalFloatRegisterDefForNode(argument, internalFloatRegCandidates());
                        setContainsAVXFlags();
                    }

                    break;
                }

                case GenTreePutArgStk.Kind.RepInstr:
                case GenTreePutArgStk.Kind.PartialRepInstr when !Configuration.TARGET_X86:
                {
                    _ = buildInternalIntRegisterDefForNode(argument,
                        TargetRuntimeMetadata.SelectXarch(SRBM_X86_EDI, SRBM_X64_RDI));
                    _ = buildInternalIntRegisterDefForNode(argument,
                        TargetRuntimeMetadata.SelectXarch(SRBM_X86_ECX, SRBM_X64_RCX));
                    _ = buildInternalIntRegisterDefForNode(argument,
                        TargetRuntimeMetadata.SelectXarch(SRBM_X86_ESI, SRBM_X64_RSI));
                    break;
                }

                case GenTreePutArgStk.Kind.Push when Configuration.TARGET_X86:
                {
                    break;
                }
                default:
                {
                    FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.UnsupportedStackArgumentKind,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? argument._kind : default)
    );
                    return default!;
                }
            }

            var sourceCount = buildOperandUses(source);
            buildInternalRegisterUses();
            if (Configuration.TARGET_X86)
            {
                assert(sourceCount < 4);
            }
            return sourceCount;
        }

        if (Configuration.TARGET_ARM64)
        {
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
                        if (Configuration.FEATURE_SIMD && field.Type is TYP_SIMD12)
                        {
                            _ = buildInternalIntRegisterDefForNode(field.Node, _availableIntRegs);
                        }
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
                if (Configuration.FEATURE_SIMD && compAppleArm64Abi() && (argument.StackByteSize is 12))
                {
                    _ = buildInternalIntRegisterDefForNode(argument, _availableIntRegs);
                }
            }

            buildInternalRegisterUses();
            return sourceCount;
        }

        if (Configuration.TARGET_RISCV64 || Configuration.TARGET_LOONGARCH64)
        {
            assert(argument.Oper.IsPutArgStk);

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
                    }
                }
                else
                {
                    _ = buildInternalIntRegisterDefForNode(argument);
                    _ = buildInternalIntRegisterDefForNode(argument);

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
            }

            buildInternalRegisterUses();
            return sourceCount;
        }

        NYI("LinearScan.buildPutArgStk on this target");
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LinearScanBuildPutArgStkOnThisTargetIsNotImplemented
    );
        return default!;
    }
}
