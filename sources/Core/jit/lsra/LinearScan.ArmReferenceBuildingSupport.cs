// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildArmCast(GenTreeCast cast)
    {
        var source = cast.CastOp;
        var sourceType = source.Type.ActualType;
        var castType = cast.CastType;
        assert(!varTypeIsLong(sourceType) || ((source.Oper is GT_LONG) && source.IsContained));

        if (varTypeIsFloating(sourceType) && !varTypeIsFloating(castType))
        {
            _ = buildInternalFloatRegisterDefForNode(cast, Globals.SRBM_ALLFLOAT);
            _setInternalRegistersDelayFree = true;
        }

        var sourceCount = buildCastUses(cast, SRBM_NONE);
        buildInternalRegisterUses();
        _ = buildDef(cast, SRBM_NONE);
        return sourceCount;
    }

    private int buildArmIndir(GenTreeIndir indirection)
    {
        assert(indirection.Type is not TYP_STRUCT);

        var address = indirection.Addr;
        if ((indirection.Flags & GTF_IND_UNALIGNED) != 0)
        {
            var type = TYP_UNDEF;
            if (indirection.Oper is GT_STOREIND)
            {
                type = indirection.AsStoreInd().Data.Type;
            }
            else if (indirection.Oper is GT_IND)
            {
                type = indirection.Type;
            }

            if (type is TYP_FLOAT)
            {
                _ = buildInternalIntRegisterDefForNode(indirection, _availableIntRegs);
            }
            else if (type is TYP_DOUBLE)
            {
                _ = buildInternalIntRegisterDefForNode(indirection, _availableIntRegs);
                _ = buildInternalIntRegisterDefForNode(indirection, _availableIntRegs);
            }
        }

        if (address.IsContained)
        {
            if (address.Oper is GT_LEA)
            {
                var addressMode = address.AsAddrMode();
                var index = addressMode.Index;
                var offset = addressMode.Offset;
                if ((index is not null) && (offset != 0))
                {
                    // ARM can encode an index or an offset, but not both.
                    _ = buildInternalIntRegisterDefForNode(indirection, _availableIntRegs);
                }
                else if (!armValidLoadStoreOffset(offset, (emitAttr)indirection.Type.EmitSize))
                {
                    _ = buildInternalIntRegisterDefForNode(indirection, _availableIntRegs);
                }
            }
        }

#if FEATURE_SIMD
        if (indirection.Type is TYP_SIMD12)
        {
            assert(!address.IsContained);
            _ = buildInternalIntRegisterDefForNode(indirection, _availableIntRegs);
        }
#endif

        var sourceCount = buildIndirUses(indirection, SRBM_NONE);
        buildInternalRegisterUses();
        if (indirection.Oper is not (GT_STOREIND or GT_NULLCHECK))
        {
            _ = buildDef(indirection, SRBM_NONE);
        }

        return sourceCount;
    }

    // emitarm.cpp's whole emitIns_valid_imm_for_ldst_offset predicate ignores size.
    // Keep the private mapping here rather than changing the shared emitter surface.
    private static bool armValidLoadStoreOffset(int immediate, emitAttr size)
    {
        if ((immediate & 0x0fff) == immediate)
        {
            return true;
        }
        if (unchecked((uint)(immediate < 0 ? -(long)immediate : immediate)) <= 0x0ff)
        {
            return true;
        }

        return false;
    }

    private int buildArmLclHeap(GenTree tree)
    {
        int sourceCount;
        var size = tree.AsUnOp().Op1;
        int internalIntCount;
        if (size.Oper.IsCnsIntOrI)
        {
            assert(size.IsContained);
            sourceCount = 0;
            // size_t and AlignUp arithmetic are 32-bit for this target.
            var sizeValue = unchecked((uint)size.AsIntCon().IconValue);
            if (sizeValue == 0)
            {
                internalIntCount = 0;
            }
            else
            {
                sizeValue = unchecked((sizeValue + (STACK_ALIGN - 1u)) & ~(STACK_ALIGN - 1u));
                var pushCount = sizeValue / REGSIZE_BYTES;
                if (pushCount <= 4)
                {
                    internalIntCount = 0;
                }
                else if (!_compiler.info.compInitMem)
                {
                    if (sizeValue < _compiler.eeGetPageSize())
                    {
                        internalIntCount = 0;
                    }
                    else
                    {
                        internalIntCount = 1;
                    }
                }
                else
                {
                    internalIntCount = 1;
                }
            }
        }
        else
        {
            sourceCount = 1;
            internalIntCount = 1;
            _ = buildUse(size);
        }

        if (_compiler.lvaOutgoingArgSpaceSize.Value > 0)
        {
            internalIntCount = 1;
        }

        if (internalIntCount > 0)
        {
            _setInternalRegistersDelayFree = true;
            for (var index = 0; index < internalIntCount; index++)
            {
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
            }
        }
        buildInternalRegisterUses();
        _ = buildDef(tree, SRBM_NONE);
        return sourceCount;
    }

    private int buildArmPutArgStk(GenTreePutArgStk argument)
    {
        assert(argument.Oper is GT_PUTARG_STK);
        var source = argument.Data;
        var sourceCount = 0;
        if (source.Type is TYP_STRUCT)
        {
            if (source.Oper is GT_FIELD_LIST)
            {
                assert(source.IsContained);
                foreach (var use in source.AsFieldList().Uses)
                {
                    _ = buildUse(use.Node);
                    sourceCount++;
#if FEATURE_SIMD
                    if (use.Type is TYP_SIMD12)
                    {
                        _ = buildInternalIntRegisterDefForNode(use.Node, _availableIntRegs);
                    }
#endif
                }
            }
            else
            {
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
            if (compAppleArm64Abi() && (argument.StackByteSize == 12))
            {
                _ = buildInternalIntRegisterDefForNode(argument, _availableIntRegs);
            }
#endif
        }

        buildInternalRegisterUses();
        return sourceCount;
    }

    private int buildArmBlockStore(GenTreeBlk block)
    {
        var destinationAddress = block.Addr;
        var source = block.Data;
        _ = block.Size;
        GenTree? sourceAddressOrFill = null;
        var destinationCandidates = SRBM_NONE;
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
                    break;
                }

                case GenTreeBlk.BlkOpKindLoop:
                {
                    _ = buildInternalIntRegisterDefForNode(block, _availableIntRegs);
                    break;
                }

                default:
                {
                    unreached();
                    break;
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
                    break;
                }

                case GenTreeBlk.BlkOpKindUnrollMemmove:
                {
                    unreached();
                    break;
                }

                default:
                {
                    unreached();
                    break;
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
            useCount++;
            _ = buildUse(destinationAddress, destinationCandidates);
        }
        else if (destinationAddress.Oper.IsAddrMode)
        {
            var baseAddress = destinationAddress.AsAddrMode().BaseAddress
                ?? throw new FatalJitException("Contained ARM32 block destination requires a base address.");
            useCount += buildAddrUses(baseAddress);
        }

        if (sourceAddressOrFill is not null)
        {
            if (!sourceAddressOrFill.IsContained)
            {
                useCount++;
                _ = buildUse(sourceAddressOrFill, sourceCandidates);
            }
            else if (sourceAddressOrFill.Oper.IsAddrMode)
            {
                var baseAddress = sourceAddressOrFill.AsAddrMode().BaseAddress
                    ?? throw new FatalJitException("Contained ARM32 block source requires a base address.");
                useCount += buildAddrUses(baseAddress);
            }
        }

        buildInternalRegisterUses();
        var killMask = getKillSetForBlockStore(block);
        buildKills(block, killMask);
        return useCount;
    }

    private int buildArmCall(GenTreeCall call)
    {
        var hasMultiRegRetVal = false;
        var singleDestinationCandidates = SRBM_NONE;
        var sourceCount = 0;
        var destinationCount = 0;
        if (call.Type is not TYP_VOID)
        {
            hasMultiRegRetVal = call.HasMultiRegRetVal;
            destinationCount = hasMultiRegRetVal ? call.ReturnTypeDesc.ReturnRegCount : 1;
        }

        var controlExpression = call.ControlExpr;
        var controlCandidates = SRBM_NONE;
        if (controlExpression is not null)
        {
            assert(controlExpression.Type is not TYP_VOID);
            if (call.IsFastTailCall)
            {
                controlCandidates = allRegs(TYP_INT) & _rbmIntCalleeTrash & ~SRBM_LR;
                if (_compiler.NeedsGSSecurityCookie)
                {
                    controlCandidates &= ~getArmGSCookieTempRegs(true, call).IntRegSet;
                }
                assert(controlCandidates != SRBM_NONE);
            }
        }
        else if (call.IsR2RRelativeIndir || call.IsVirtualStubRelativeIndir)
        {
            if (call.IsFastTailCall)
            {
                var candidates = allRegs(TYP_INT) & _rbmIntCalleeTrash;
                assert(candidates != SRBM_NONE);
                _ = buildInternalIntRegisterDefForNode(call, candidates);
            }
            else
            {
                _ = buildInternalIntRegisterDefForNode(call, _availableIntRegs);
            }
        }
        else
        {
            _ = buildInternalIntRegisterDefForNode(call, _availableIntRegs);
        }

        if (call.NeedsNullCheck)
        {
            // A fast tailcall's null check must leave R12 available for the target.
            var candidates = call.IsFastTailCall ? SRBM_LR : _availableIntRegs;
            _ = buildInternalIntRegisterDefForNode(call, candidates);
        }

        var registerType = call.Type;
        if (call.IsHelperCall(CORINFO_HELP_INIT_PINVOKE_FRAME))
        {
            // targetarm.h binds REG_PINVOKE_TCB/RBM_PINVOKE_TCB to R5.
            singleDestinationCandidates = genSingleTypeRegMask(REG_R5);
        }
        else if (!hasMultiRegRetVal)
        {
            if (varTypeUsesFloatArgReg(registerType))
            {
                singleDestinationCandidates = genSingleTypeRegMask(REG_FLOATRET);
            }
            else if (registerType is TYP_LONG)
            {
                singleDestinationCandidates = genSingleTypeRegMask(REG_LNGRET_LO) |
                    genSingleTypeRegMask(REG_LNGRET_HI);
            }
            else
            {
                singleDestinationCandidates = genSingleTypeRegMask(REG_INTRET);
            }
        }

        sourceCount += buildCallArgUses(call);
        if (controlExpression is not null)
        {
            _ = buildUse(controlExpression, controlCandidates);
            sourceCount++;
        }

        buildInternalRegisterUses();
        if (call.IsAsync && _compiler.compIsAsync && !call.IsFastTailCall)
        {
            markArmAsyncContinuationBusyForCall(call);
        }

        var killMask = getKillSetForCall(call);
        if (destinationCount > 0)
        {
            if (hasMultiRegRetVal)
            {
                ref readonly var returnTypeDesc = ref call.ReturnTypeDesc;
                assert(!Unsafe.IsNullRef(in returnTypeDesc));
                var multiDestinationCandidates = returnTypeDesc.GetAbiReturnRegs(call.UnmanagedCallConv);
                assert(countRegisterMaskBits(multiDestinationCandidates) > 0);
                buildCallDefsWithKills(call, destinationCount, multiDestinationCandidates, killMask);
            }
            else
            {
                assert(destinationCount == 1);
                buildDefWithKills(call, singleDestinationCandidates, killMask);
            }
        }
        else
        {
            buildKills(call, killMask);
        }

#if SWIFT_SUPPORT
        if (call.HasSwiftErrorHandling)
        {
            markArmSwiftErrorBusyForCall(call);
        }
#endif

        _placedArgumentRegisters = RBM_NONE;
        _placedArgumentLocalCount = 0;
        return sourceCount;
    }

    private void markArmAsyncContinuationBusyForCall(GenTreeCall call)
    {
        assert(call.Next is not null);
        assert(call.Next.Oper is GT_ASYNC_CONTINUATION);
        var reference = addKillForRegs(new regMaskTP(genSingleTypeRegMask(REG_ASYNC_CONTINUATION_RET)),
            _referenceBuildLocation + 1);
        setDelayFree(reference);
    }

    private static regMaskTP getArmGSCookieTempRegs(bool tailCall, GenTreeCall? tailCallNode)
    {
        // Whole ARM32 projection of CodeGenInterface::genGetGSCookieTempRegs;
        // targetarm.h defines RBM_GSCOOKIE_TMP as R12 | LR for both call kinds.
        return new regMaskTP(SRBM_R12 | SRBM_LR);
    }

#if SWIFT_SUPPORT
    private static void markArmSwiftErrorBusyForCall(GenTreeCall call)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 REG_SWIFT_ERROR is not defined.");
    }
#endif
}
#endif
