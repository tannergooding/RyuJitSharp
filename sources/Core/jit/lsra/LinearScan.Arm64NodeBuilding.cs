// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if TARGET_ARM64
    private int buildNodeArm64(GenTree tree)
    {
        assert(!tree.IsContained);
        clearBuildState();

        var destinationCount = tree.IsValue ? 1 : 0;
        var isLocalDefUse = tree.IsValue && tree.IsUnusedValue;
        int sourceCount;

        switch (tree.Oper)
        {
            case GT_PHYSREG:
            {
                sourceCount = 0;
                if (varTypeUsesMaskReg(tree.Type))
                {
                    assert(tree.AsPhysReg().SrcReg == REG_FFR);
                    _ = buildDef(tree, genSingleTypeRegMask(REG_FFR));
                }
                else
                {
                    _ = buildSimple(tree);
                }
                break;
            }

            case GT_LCL_VAR:
            {
                if (checkContainedOrCandidateLclVar(tree.AsLclVar()))
                {
                    return 0;
                }
                goto case GT_LCL_FLD;
            }

            case GT_LCL_FLD:
            {
                sourceCount = 0;
#if FEATURE_SIMD
                if (tree.Type is TYP_SIMD12)
                {
                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    _setInternalRegistersDelayFree = true;
                    buildInternalRegisterUses();
                }
#endif
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
            {
                if ((tree.Oper is GT_STORE_LCL_VAR) && tree.IsMultiRegLclVar &&
                    isCandidateMultiRegLclVar(tree.AsLclVar()))
                {
                    destinationCount = _compiler.lvaGetDesc(tree.AsLclVar().LclNum).lvFieldCnt;
                }
                sourceCount = buildStoreLoc(tree.AsLclVarCommon());
                break;
            }

            case GT_FIELD_LIST:
            case GT_BOX:
            case GT_COMMA:
            case GT_QMARK:
            case GT_COLON:
            case GT_SWITCH:
            case GT_BLK:
            case GT_INIT_VAL:
            {
                throw new FatalJitException($"Unexpected non-contained {tree.Oper} in ARM64 LSRA.");
            }

            case GT_NO_OP:
            case GT_START_NONGC:
            case GT_NOP:
            case GT_JMP:
            {
                sourceCount = 0;
                assert(destinationCount == 0);
                break;
            }

            case GT_START_PREEMPTGC:
            {
                sourceCount = 0;
                assert(destinationCount == 0);
                buildKills(tree, RBM_NONE);
                break;
            }

            case GT_PROF_HOOK:
            {
                sourceCount = 0;
                assert(destinationCount == 0);
                buildKills(tree, getKillSetForProfilerHook());
                break;
            }

            case GT_CNS_DBL:
            {
                if (!Emitter.emitIns_valid_imm_for_fmov(tree.AsDblCon().DconVal))
                {
                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    buildInternalRegisterUses();
                }
                goto case GT_CNS_INT;
            }

            case GT_CNS_INT:
            {
                sourceCount = 0;
                assert(destinationCount == 1);
                buildDef(tree, SRBM_NONE).getInterval().isConstant = true;
                break;
            }

#if FEATURE_SIMD
            case GT_CNS_VEC:
            {
                var vector = tree.AsVecCon();
                if (!vector.IsAllBitsSet && !vector.IsZero)
                {
                    if (tree.Type is TYP_SIMD)
                    {
                        throw new FatalJitException("ARM64 scalable vector constant encoding is not implemented in LSRA.");
                    }

                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    buildInternalRegisterUses();
                }
                sourceCount = 0;
                assert(destinationCount == 1);
                buildDef(tree, SRBM_NONE).getInterval().isConstant = true;
                break;
            }
#endif

#if FEATURE_MASKED_HW_INTRINSICS
            case GT_CNS_MSK:
            {
                var mask = tree.AsMskCon();
                if (!mask.IsAllBitsSet && !mask.IsZero)
                {
                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    buildInternalRegisterUses();
                }
                sourceCount = 0;
                assert(destinationCount == 1);
                buildDef(tree, SRBM_NONE).getInterval().isConstant = true;
                break;
            }
#endif

            case GT_RETURN:
            {
                sourceCount = buildReturn(tree);
                buildKills(tree, getKillSetForReturn(tree));
                break;
            }

#if SWIFT_SUPPORT
            case GT_SWIFT_ERROR_RET:
            {
                _ = buildUse(tree.AsUnOp().Op1, SRBM_SWIFT_ERROR);
                sourceCount = buildReturn(tree) + 1;
                buildKills(tree, getKillSetForReturn(tree));
                break;
            }
#endif

            case GT_RETFILT:
            {
                assert(destinationCount == 0);
                sourceCount = tree.Type is TYP_VOID ? 0 : 1;
                if (sourceCount != 0)
                {
                    assert(tree.Type is TYP_INT);
                    _ = buildUse(tree.AsUnOp().Op1, SRBM_INTRET);
                }
                break;
            }

            case GT_KEEPALIVE:
            {
                assert(destinationCount == 0);
                sourceCount = buildOperandUses(tree.AsUnOp().Op1);
                break;
            }

            case GT_PATCHPOINT:
            {
                sourceCount = buildOperandUses(tree.AsOp().Op1, SRBM_ARG_0);
                sourceCount += buildOperandUses(tree.AsOp().Op2, SRBM_ARG_1);
                buildKills(tree, _compiler.compHelperCallKillSet(CORINFO_HELP_PATCHPOINT));
                break;
            }

            case GT_PATCHPOINT_FORCED:
            {
                sourceCount = buildOperandUses(tree.AsUnOp().Op1, SRBM_ARG_0);
                buildKills(tree, _compiler.compHelperCallKillSet(CORINFO_HELP_PATCHPOINT_FORCED));
                break;
            }

            case GT_JMPTABLE:
            {
                sourceCount = 0;
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_SWITCH_TABLE:
            {
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                sourceCount = buildBinaryUses(tree.AsOp());
                assert(destinationCount == 0);
                break;
            }

            case GT_ADD:
            case GT_SUB:
            {
                if (varTypeIsFloating(tree.Type))
                {
                    assert(!tree.AsOp().HasOverflowCheckEx);
                    assert(tree.AsOp().Op1.Type == tree.AsOp().Op2.Type);
                }
                goto case GT_AND;
            }

            case GT_AND:
            case GT_AND_NOT:
            case GT_OR:
            case GT_OR_NOT:
            case GT_XOR:
            case GT_XOR_NOT:
            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROR:
            {
                sourceCount = buildBinaryUses(tree.AsOp());
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_BFIZ:
            {
                assert(tree.AsUnOp().Op1.Oper is GT_CAST);
                sourceCount = buildOperandUses(tree.AsUnOp().Op1.AsCast().CastOp);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_BFX:
            {
                sourceCount = buildOperandUses(tree.AsUnOp().Op1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_RETURNTRAP:
            {
                _ = buildUse(tree.AsUnOp().Op1);
                sourceCount = 1;
                assert(destinationCount == 0);
                buildKills(tree, _compiler.compHelperCallKillSet(CORINFO_HELP_STOP_FOR_GC));
                break;
            }

            case GT_MOD:
            case GT_UMOD:
            {
                throw new FatalJitException("ARM64 integer remainder must be lowered and floating remainder is not implemented.");
            }

            case GT_MUL:
            case GT_DIV:
            case GT_MULHI:
            case GT_MUL_LONG:
            case GT_UDIV:
            {
                if ((tree.Oper is GT_MUL) && tree.AsOp().HasOverflowCheckEx)
                {
                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    _setInternalRegistersDelayFree = true;
                }
                sourceCount = buildBinaryUses(tree.AsOp());
                buildInternalRegisterUses();
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_INTRINSIC:
            {
                sourceCount = buildIntrinsic(tree);
                break;
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                sourceCount = buildHWIntrinsicArm64(tree.AsHWIntrinsic(), out destinationCount);
                break;
            }
#endif

            case GT_CAST:
            {
                assert(destinationCount == 1);
                sourceCount = buildCast(tree.AsCast());
                break;
            }

            case GT_NEG:
            case GT_NOT:
            {
                sourceCount = buildOperandUses(tree.AsUnOp().Op1);
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            case GT_TEST_EQ:
            case GT_TEST_NE:
            case GT_CMP:
            case GT_TEST:
            case GT_CCMP:
            case GT_JCMP:
            case GT_JTEST:
            {
                sourceCount = buildCmp(tree);
                break;
            }

            case GT_JTRUE:
            {
                sourceCount = buildOperandUses(tree.AsUnOp().Op1);
                break;
            }

            case GT_CKFINITE:
            {
                assert(destinationCount == 1);
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                _ = buildUse(tree.AsUnOp().Op1);
                sourceCount = 1;
                _ = buildDef(tree, SRBM_NONE);
                buildInternalRegisterUses();
                break;
            }

            case GT_CMPXCHG:
            {
                sourceCount = buildArm64CompareExchange(tree.AsCmpXchg());
                break;
            }

            case GT_LOCKADD:
            case GT_XORR:
            case GT_XAND:
            case GT_XADD:
            case GT_XCHG:
            {
                sourceCount = buildArm64Atomic(tree, destinationCount);
                break;
            }

            case GT_PUTARG_STK:
            {
                sourceCount = buildPutArgStk(tree.AsPutArgStk());
                break;
            }

            case GT_PUTARG_REG:
            {
                sourceCount = buildPutArgReg(tree.AsUnOp());
                break;
            }

            case GT_CALL:
            {
                sourceCount = buildCall(tree.AsCall());
                if (tree.AsCall().HasMultiRegRetVal)
                {
                    destinationCount = tree.AsCall().ReturnTypeDesc.ReturnRegCount;
                }
                break;
            }

            case GT_STORE_BLK:
            {
                sourceCount = buildBlockStore(tree.AsBlk());
                break;
            }

            case GT_LCLHEAP:
            {
                sourceCount = buildLclHeap(tree);
                break;
            }

            case GT_BOUNDS_CHECK:
            {
                assert(destinationCount == 0);
                sourceCount = buildOperandUses(tree.AsBoundsChk().Index);
                sourceCount += buildOperandUses(tree.AsBoundsChk().ArrayLength);
                break;
            }

            case GT_LEA:
            {
                sourceCount = buildArm64Address(tree.AsAddrMode());
                break;
            }

            case GT_STOREIND:
            {
                assert(destinationCount == 0);
                assert(_compiler.codeGen is not null);
                if (_compiler.codeGen.GCInfo.gcIsWriteBarrierStoreIndNode(tree.AsStoreInd()))
                {
                    sourceCount = buildGCWriteBarrier(tree);
                }
                else
                {
                    sourceCount = buildIndir(tree.AsIndir());
                    sourceCount += buildOperandUses(tree.AsStoreInd().Data);
                }
                break;
            }

            case GT_NULLCHECK:
            case GT_IND:
            {
                sourceCount = buildIndir(tree.AsIndir());
                break;
            }

            case GT_CATCH_ARG:
            {
                sourceCount = 0;
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_EXCEPTION_OBJECT);
                break;
            }

            case GT_ASYNC_CONTINUATION:
            {
                throw new FatalJitException("ARM64 async continuation return register is not defined.");
            }

            case GT_INDEX_ADDR:
            {
                sourceCount = buildArm64IndexAddress(tree);
                break;
            }

            case GT_SELECT:
            case GT_SELECTCC:
            {
                assert(destinationCount == 1);
                sourceCount = buildSelect(tree.AsOp());
                break;
            }

#if SWIFT_SUPPORT
            case GT_SWIFT_ERROR:
            {
                sourceCount = 0;
                _ = buildDef(tree, SRBM_SWIFT_ERROR);
                break;
            }
#endif

            default:
            {
                sourceCount = buildSimple(tree);
                break;
            }
        }

        if (tree.IsUnusedValue && (destinationCount != 0))
        {
            isLocalDefUse = true;
        }

        assert((destinationCount < 2) || tree.IsMultiRegNode);
        assert(isLocalDefUse == (tree.IsValue && tree.IsUnusedValue));
        assert(!tree.IsValue || (destinationCount != 0));
        assert(destinationCount == tree.GetRegisterDstCount(_compiler));
        return sourceCount;
    }

    private int buildArm64CompareExchange(GenTreeCmpXchg node)
    {
        var sourceCount = node.Comparand.IsContained ? 2 : 3;
        var useAtomics = _compiler.compOpportunisticallyDependsOn(InstructionSet_Atomics);
        if (!useAtomics)
        {
            _ = buildInternalIntRegisterDefForNode(node, _availableIntRegs);
        }

        var location = buildUse(node.Addr);
        setDelayFree(location);
        var value = buildUse(node.Data);
        setDelayFree(value);
        if (!node.Comparand.IsContained)
        {
            var comparand = buildUse(node.Comparand);
            if (!useAtomics)
            {
                setDelayFree(comparand);
            }
        }

        _setInternalRegistersDelayFree = true;
        buildInternalRegisterUses();
        _ = buildDef(node, SRBM_NONE);
        return sourceCount;
    }

    private int buildArm64Atomic(GenTree tree, int destinationCount)
    {
        var operation = tree.AsOp();
        var sourceCount = operation.Op2.IsContained ? 1 : 2;
        var useAtomics = _compiler.compOpportunisticallyDependsOn(InstructionSet_Atomics);
        if (!useAtomics)
        {
            _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
            if (tree.Oper is not GT_XCHG)
            {
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
            }
        }
        else if (tree.Oper is GT_XAND)
        {
            _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
        }

        assert(!operation.Op1.IsContained);
        var addressUse = buildUse(operation.Op1);
        RefPosition? valueUse = null;
        if (!operation.Op2.IsContained)
        {
            valueUse = buildUse(operation.Op2);
        }

        if (!useAtomics && (destinationCount == 1))
        {
            setDelayFree(addressUse);
            if (valueUse is not null)
            {
                setDelayFree(valueUse);
            }
            _setInternalRegistersDelayFree = true;
        }

        buildInternalRegisterUses();
        if (useAtomics || (destinationCount == 1))
        {
            _ = buildDef(tree, SRBM_NONE);
        }
        return sourceCount;
    }

    private int buildArm64Address(GenTreeAddrMode address)
    {
        var sourceCount = 0;
        if (address.HasBaseAddress)
        {
            _ = buildUse(address.BaseAddress);
            sourceCount++;
        }
        if (address.HasIndex)
        {
            var index = address.Index;
            if (index.IsContained && (index.Oper is GT_BFIZ))
            {
                assert(address.Offset == 0);
                var cast = index.AsUnOp().Op1.AsCast();
                assert(cast.IsContained);
                _ = buildUse(cast.CastOp);
            }
            else if (index.IsContained && (index.Oper is GT_CAST))
            {
                assert(address.Offset == 0);
                _ = buildUse(index.AsCast().CastOp);
            }
            else
            {
                _ = buildUse(index);
            }
            sourceCount++;
        }

        if ((address.HasIndex && (address.Offset != 0)) ||
            !Emitter.emitIns_valid_imm_for_add(address.Offset, EA_8BYTE))
        {
            _ = buildInternalIntRegisterDefForNode(address, _availableIntRegs);
        }
        buildInternalRegisterUses();
        _ = buildDef(address, SRBM_NONE);
        return sourceCount;
    }

    private int buildArm64IndexAddress(GenTree tree)
    {
        var indexAddress = tree.AsIndexAddr();
        var sourceCount = buildBinaryUses(tree.AsOp());
        _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
        if ((indexAddress.Index.Type is not TYP_I_IMPL) &&
            !(uint.IsPow2((uint)indexAddress.ElemSize) && (indexAddress.ElemSize <= 32768)))
        {
            _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
        }
        buildInternalRegisterUses();
        _ = buildDef(tree, SRBM_NONE);
        return sourceCount;
    }
#endif
}
