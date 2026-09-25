// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildNode(GenTree tree)
    {
#if TARGET_AMD64
        assert(!tree.IsContained);
        clearBuildState();

        var destinationCount = tree.IsValue ? 1 : 0;
        var isLocalDefUse = tree.IsValue && tree.IsUnusedValue;
        if (!varTypeUsesIntReg(tree.Type))
        {
            setContainsAVXFlags();
        }

        int sourceCount;
        switch (tree.Oper)
        {
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
                if ((tree.Type is TYP_SIMD12) && (tree.Oper is GT_STORE_LCL_FLD))
                {
                    if (!tree.AsLclFld().Data.IsVectorZero)
                    {
                        _ = buildInternalFloatRegisterDefForNode(tree, _availableFloatRegs);
                        buildInternalRegisterUses();
                    }
                }
#endif
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_STORE_LCL_FLD:
            case GT_STORE_LCL_VAR:
            {
                if (tree.IsMultiRegLclVar && isCandidateMultiRegLclVar(tree.AsLclVar()))
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
                throw new FatalJitException($"Unexpected non-contained {tree.Oper} in LSRA node building.");
            }

            case GT_NO_OP:
            case GT_START_NONGC:
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

            case GT_CNS_INT:
            case GT_CNS_LNG:
            case GT_CNS_DBL:
#if FEATURE_SIMD
            case GT_CNS_VEC:
#endif
#if FEATURE_MASKED_HW_INTRINSICS
            case GT_CNS_MSK:
#endif
            {
                sourceCount = 0;
                assert(destinationCount == 1);
                assert(!tree.IsReuseRegVal);
                var definition = buildDef(tree, SRBM_NONE);
                definition.getInterval().isConstant = true;
                break;
            }

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
                if (tree.Type is TYP_VOID)
                {
                    sourceCount = 0;
                }
                else
                {
                    assert(tree.Type is TYP_INT);
                    sourceCount = 1;
                    _ = buildUse(tree.AsUnOp().Op1, SRBM_INTRET);
                }
                break;
            }

            case GT_NOP:
            {
                sourceCount = 0;
                assert(tree.Type is TYP_VOID);
                assert(destinationCount == 0);
                break;
            }

            case GT_KEEPALIVE:
            {
                assert(destinationCount == 0);
                sourceCount = buildOperandUses(tree.AsUnOp().Op1);
                break;
            }

            case GT_JTRUE:
            {
                _ = buildOperandUses(tree.AsUnOp().Op1);
                sourceCount = 1;
                break;
            }

            case GT_JCC:
            case GT_JMP:
            {
                sourceCount = 0;
                assert(destinationCount == 0);
                break;
            }

            case GT_PATCHPOINT:
            {
                sourceCount = buildOperandUses(tree.AsOp().Op1, SRBM_ARG_0);
                _ = buildOperandUses(tree.AsOp().Op2, SRBM_ARG_1);
                sourceCount++;
                buildKills(tree, _compiler.compHelperCallKillSet(CORINFO_HELP_PATCHPOINT));
                break;
            }

            case GT_PATCHPOINT_FORCED:
            {
                sourceCount = buildOperandUses(tree.AsUnOp().Op1, SRBM_ARG_0);
                buildKills(tree, _compiler.compHelperCallKillSet(CORINFO_HELP_PATCHPOINT_FORCED));
                break;
            }

            case GT_SETCC:
            {
                sourceCount = 0;
                assert(destinationCount == 1);
                _ = buildDef(tree, _availableIntRegs);
                break;
            }

            case GT_SELECT:
            case GT_SELECTCC:
            {
                assert(destinationCount == 1);
                sourceCount = buildSelect(tree.AsOp());
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
                assert(destinationCount == 0);
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                sourceCount = buildBinaryUses(tree.AsOp());
                buildInternalRegisterUses();
                assert(sourceCount == 2);
                break;
            }

            case GT_ADD:
            case GT_SUB:
            case GT_AND:
            case GT_OR:
            case GT_XOR:
            case GT_BIT_SET:
            case GT_BIT_CLEAR:
            case GT_BIT_INVERT:
            {
                sourceCount = buildBinaryUses(tree.AsOp());
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_RETURNTRAP:
            {
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                sourceCount = buildOperandUses(tree.AsUnOp().Op1);
                buildInternalRegisterUses();
                buildKills(tree, _compiler.compHelperCallKillSet(CORINFO_HELP_STOP_FOR_GC));
                break;
            }

            case GT_MOD:
            case GT_DIV:
            case GT_UMOD:
            case GT_UDIV:
            {
                sourceCount = buildModDiv(tree);
                break;
            }

            case GT_MUL:
            case GT_MULHI:
            {
                sourceCount = buildMul(tree);
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
                sourceCount = buildHWIntrinsic(tree.AsHWIntrinsic(), out destinationCount);
                break;
            }
#endif

            case GT_CAST:
            {
                assert(destinationCount == 1);
                sourceCount = buildCast(tree.AsCast());
                break;
            }

            case GT_BITCAST:
            {
                assert(destinationCount == 1);
                var source = tree.AsUnOp().Op1;
                if (!source.IsContained)
                {
                    _ = buildUse(source, varTypeUsesFloatReg(tree.Type) && varTypeUsesIntReg(source.Type)
                        ? _lowGprRegs
                        : SRBM_NONE);
                    sourceCount = 1;
                }
                else
                {
                    sourceCount = 0;
                }

                _ = buildDef(tree, varTypeUsesFloatReg(source.Type) && varTypeUsesIntReg(tree.Type)
                    ? _lowGprRegs
                    : SRBM_NONE);
                break;
            }

            case GT_NEG:
            {
                // The scalar floating negate needs an XMM temporary for the sign-bit mask.
                if (varTypeIsFloating(tree.Type))
                {
                    _ = buildInternalFloatRegisterDefForNode(tree, internalFloatRegCandidates());
                }
                sourceCount = buildOperandUses(tree.AsUnOp().Op1);
                if (varTypeIsFloating(tree.Type))
                {
                    buildInternalRegisterUses();
                }
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_NOT:
            {
                sourceCount = buildOperandUses(tree.AsUnOp().Op1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROL:
            case GT_ROR:
            {
                sourceCount = buildShiftRotate(tree);
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
            case GT_BITTEST_EQ:
            case GT_BITTEST_NE:
            case GT_CMP:
            case GT_TEST:
            case GT_BT:
            case GT_CCMP:
            {
                sourceCount = buildCmp(tree);
                break;
            }

            case GT_CKFINITE:
            {
                assert(destinationCount == 1);
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                sourceCount = buildOperandUses(tree.AsUnOp().Op1);
                buildInternalRegisterUses();
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_CMPXCHG:
            {
                sourceCount = 3;
                assert(destinationCount == 1);
                var compareExchange = tree.AsCmpXchg();
                var candidates = _availableIntRegs & ~SRBM_RAX;
                _ = buildUse(compareExchange.Addr, candidates);
                _ = buildUse(compareExchange.Data, varTypeIsByte(tree.Type) ? candidates & _rbmAllInt : candidates);
                _ = buildUse(compareExchange.Comparand, SRBM_RAX);
                _ = buildDef(tree, SRBM_RAX);
                break;
            }

            case GT_XORR:
            case GT_XAND:
            {
                if (!tree.IsUnusedValue)
                {
                    var address = tree.AsOp().Op1;
                    var data = tree.AsOp().Op2;
                    assert(!varTypeIsByte(data.Type));
                    var candidates = _availableIntRegs & ~SRBM_RAX;
                    _ = buildInternalIntRegisterDefForNode(tree, candidates);
                    _ = buildUse(address, candidates);
                    _ = buildUse(data, candidates);
                    _ = buildDef(tree, SRBM_RAX);
                    buildInternalRegisterUses();
                    sourceCount = 2;
                    assert(destinationCount == 1);
                    break;
                }
                goto case GT_XADD;
            }

            case GT_XADD:
            case GT_XCHG:
            {
                var address = tree.AsOp().Op1;
                var data = tree.AsOp().Op2;
                assert(!address.IsContained);
                var addressUse = buildUse(address);
                setDelayFree(addressUse);
                _targetPreferredUse = addressUse;
                assert(!data.IsContained);
                _ = buildUse(data, varTypeIsByte(tree.Type) ? _rbmAllInt : SRBM_NONE);
                sourceCount = 2;
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
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

            case GT_PUTARG_STK:
            {
                sourceCount = buildPutArgStk(tree.AsPutArgStk());
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
                sourceCount = 0;
                assert(destinationCount == 1);
                var address = tree.AsAddrMode();
                if (address.HasBaseAddress)
                {
                    sourceCount++;
                    _ = buildUse(address.BaseAddress);
                }
                if (address.HasIndex)
                {
                    sourceCount++;
                    _ = buildUse(address.Index);
                }
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_STOREIND:
            {
                assert(_compiler.codeGen is not null);
                sourceCount = _compiler.codeGen.GCInfo.gcIsWriteBarrierStoreIndNode(tree.AsStoreInd())
                    ? buildGCWriteBarrier(tree)
                    : buildIndir(tree.AsIndir());
                break;
            }

            case GT_NULLCHECK:
            {
                assert(destinationCount == 0);
                _ = buildUse(tree.AsUnOp().Op1);
                sourceCount = 1;
                break;
            }

            case GT_IND:
            {
                sourceCount = buildIndir(tree.AsIndir());
                assert(destinationCount == 1);
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
                sourceCount = 0;
                _ = buildDef(tree, genSingleTypeRegMask(REG_ASYNC_CONTINUATION_RET));
                break;
            }

            case GT_INDEX_ADDR:
            {
                assert(destinationCount == 1);
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                sourceCount = buildBinaryUses(tree.AsOp());
                buildInternalRegisterUses();
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

#if SWIFT_SUPPORT
            case GT_SWIFT_ERROR:
            {
                sourceCount = 0;
                assert(destinationCount == 1);
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

        assert((destinationCount < 2) || tree.IsMultiRegNode);
        assert(isLocalDefUse == (tree.IsValue && tree.IsUnusedValue));
        assert(!tree.IsValue || (destinationCount != 0));
        assert(destinationCount == tree.GetRegisterDstCount(_compiler));
        return sourceCount;
#else
        NYI("LinearScan.buildNode outside AMD64");
        throw new FatalJitException("LinearScan.buildNode outside AMD64.");
#endif
    }

    private void clearBuildState()
    {
        _targetPreferredUse = null;
        _targetPreferredUse2 = null;
        _targetPreferredUse3 = null;
        _internalDefinitionCount = 0;
        _setInternalRegistersDelayFree = false;
        _pendingDelayFree = false;
    }

    private bool isCandidateMultiRegLclVar(GenTreeLclVar local)
    {
        assert(_compiler.lvaEnregMultiRegVars && local.IsMultiReg);
        ref var descriptor = ref _compiler.lvaGetDesc(local.LclNum);
        assert(descriptor.lvPromoted);
        var isMultiReg = _compiler.lvaGetPromotionType(in descriptor) is
            Compiler.lvaPromotionType.PROMOTION_TYPE_INDEPENDENT;
        if (!isMultiReg)
        {
            local.ClearMultiReg();
        }

#if DEBUG
        for (var index = 0; index < descriptor.lvFieldCnt; index++)
        {
            ref var field = ref _compiler.lvaGetDesc(descriptor.lvFieldLclStart + index);
            assert(field.lvLRACandidate == isMultiReg);
        }
#endif
        return isMultiReg;
    }

    private bool checkContainedOrCandidateLclVar(GenTreeLclVar local)
    {
        assert(!local.IsContained);
        bool isCandidate;
        bool makeContained;
        if (local.IsMultiReg)
        {
            isCandidate = isCandidateMultiRegLclVar(local);
            if (isCandidate)
            {
                assert(!local.IsRegOptional);
            }
            makeContained = !isCandidate;
        }
        else
        {
            isCandidate = _compiler.lvaGetDesc(local.LclNum).lvLRACandidate;
            makeContained = !isCandidate && local.IsRegOptional;
        }

        if (makeContained)
        {
            local.IsRegOptional = false;
            local.IsContained = true;
            return true;
        }
        return isCandidate;
    }
}
