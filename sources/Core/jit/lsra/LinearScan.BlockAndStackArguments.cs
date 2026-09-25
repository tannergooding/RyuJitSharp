// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildBlockStore(GenTreeBlk block)
    {
#if TARGET_AMD64
        var destinationAddress = block.Addr;
        var source = block.Data;
        var size = block.Size;
        GenTree? sourceAddressOrFill = null;
        var sourceCandidates = SRBM_NONE;

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
                        _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                    }

                    break;
                }

                case GenTreeBlk.BlkOpKindUnrollMemmove:
                {
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

        buildInternalRegisterUses();
        buildKills(block, getKillSetForBlockStore(block));
        return useCount;
#else
        NYI("LinearScan.buildBlockStore outside AMD64");
        throw new FatalJitException("LinearScan.buildBlockStore outside AMD64.");
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
#else
        NYI("LinearScan.buildPutArgStk outside AMD64");
        throw new FatalJitException("LinearScan.buildPutArgStk outside AMD64.");
#endif
    }
}
