// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildNodeLoongArch64(GenTree tree)
    {
        assert(!tree.IsContained);

        var sourceCount = 0;
        var destinationCount = tree.IsValue ? 1 : 0;
        var isLocalDefUse = tree.IsValue && tree.IsUnusedValue;

        clearBuildState();

        switch (tree.Oper)
        {
            default:
            {
                sourceCount = buildSimple(tree);
                break;
            }

            case GT_LCL_VAR:
            {
                // Decide after liveness whether this local is a candidate or contained.
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
                    // The target and temporary are both live while the upper four bytes are read.
                    _ = buildInternalIntRegisterDefForNode(tree);
                    _setInternalRegistersDelayFree = true;
                    buildInternalRegisterUses();
                }
#endif
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_STORE_LCL_VAR:
            {
                if (tree.IsMultiRegLclVar && isCandidateMultiRegLclVar(tree.AsLclVar()))
                {
                    destinationCount = _compiler.lvaGetDesc(tree.AsLclVar().LclNum).lvFieldCnt;
                }
                goto case GT_STORE_LCL_FLD;
            }

            case GT_STORE_LCL_FLD:
            {
                sourceCount = buildStoreLoc(tree.AsLclVarCommon());
                break;
            }

            case GT_FIELD_LIST:
            {
                noway_assert(false, "Non-contained GT_FIELD_LIST");
                sourceCount = 0;
                break;
            }

            case GT_NO_OP:
            case GT_START_NONGC:
            {
                sourceCount = 0;
                assert(destinationCount == 0);
                break;
            }

            case GT_PROF_HOOK:
            {
                sourceCount = 0;
                assert(destinationCount == 0);
                buildKills(tree, getKillSetForProfilerHook());
                break;
            }

            case GT_START_PREEMPTGC:
            {
                sourceCount = 0;
                assert(destinationCount == 0);
                buildKills(tree, new regMaskTP(SRBM_NONE));
                break;
            }

            case GT_CNS_DBL:
            {
                // Float/double immediates load through memory on LoongArch64.
                _ = buildInternalIntRegisterDefForNode(tree);
                buildInternalRegisterUses();
                goto case GT_CNS_INT;
            }

            case GT_CNS_INT:
            {
                sourceCount = 0;
                assert(destinationCount == 1);
                buildDef(tree, SRBM_NONE).getInterval().isConstant = true;
                break;
            }

            case GT_BOX:
            case GT_COMMA:
            case GT_QMARK:
            case GT_COLON:
            {
                sourceCount = 0;
                assert(destinationCount == 0);
                unreached();
                break;
            }

            case GT_RETURN:
            {
                sourceCount = buildReturn(tree);
                buildKills(tree, getKillSetForReturn(tree));
                break;
            }

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

            case GT_JTRUE:
            case GT_JMP:
            {
                sourceCount = 0;
                assert(destinationCount == 0);
                break;
            }

            case GT_SWITCH:
            {
                sourceCount = 0;
                noway_assert(false, "Switch must be lowered at this point.");
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
                _ = buildInternalIntRegisterDefForNode(tree);
                sourceCount = buildBinaryUses(tree.AsOp());
                assert(destinationCount == 0);
                break;
            }

            case GT_ADD:
            case GT_SUB:
            {
                if (varTypeIsFloating(tree.Type))
                {
                    assert(!tree.HasOverflowCheckEx);
                    assert(tree.AsOp().Op1.Type == tree.AsOp().Op2.Type);
                }
                else if (tree.HasOverflowCheckEx)
                {
                    // Keep a register distinct from the result for the overflow check.
                    _ = buildInternalIntRegisterDefForNode(tree);
                    _setInternalRegistersDelayFree = true;
                }
                goto case GT_AND;
            }

            case GT_AND:
            case GT_AND_NOT:
            case GT_OR:
            case GT_XOR:
            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROR:
            {
                sourceCount = buildBinaryUses(tree.AsOp());
                buildInternalRegisterUses();
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_RETURNTRAP:
            {
                // This lowers to a compare followed by a conditional stop-for-GC call.
                _ = buildUse(tree.AsUnOp().Op1);
                sourceCount = 1;
                assert(destinationCount == 0);
                buildKills(tree, _compiler.compHelperCallKillSet(CORINFO_HELP_STOP_FOR_GC));
                break;
            }

            case GT_MUL:
            {
                if (tree.HasOverflowCheckEx)
                {
                    // Keep a register distinct from the result for the overflow check.
                    _ = buildInternalIntRegisterDefForNode(tree);
                    _setInternalRegistersDelayFree = true;
                }
                goto case GT_MOD;
            }

            case GT_MOD:
            case GT_UMOD:
            case GT_DIV:
            case GT_MULHI:
            case GT_UDIV:
            {
                sourceCount = buildBinaryUses(tree.AsOp());
                buildInternalRegisterUses();
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_INTRINSIC:
            {
                var intrinsicName = tree.AsIntrinsic().IntrinsicName;
                noway_assert(
                    (intrinsicName is NI_System_Math_Abs) ||
                    (intrinsicName is NI_System_Math_Ceiling) ||
                    (intrinsicName is NI_System_Math_Floor) ||
                    (intrinsicName is NI_System_Math_MaxNative) ||
                    (intrinsicName is NI_System_Math_MinNative) ||
                    (intrinsicName is NI_System_Math_Round) ||
                    (intrinsicName is NI_System_Math_Sqrt) ||
                    (intrinsicName is NI_PRIMITIVE_SaturateToInt8) ||
                    (intrinsicName is NI_PRIMITIVE_SaturateToInt16) ||
                    (intrinsicName is NI_PRIMITIVE_SaturateToUInt8) ||
                    (intrinsicName is NI_PRIMITIVE_SaturateToUInt16));

                var operand1 = tree.AsUnOp().Op1;
                var operand2 = tree.GetOp2IfPresent();
                _ = buildUse(operand1);
                sourceCount = 1;

                if (operand2 is not null)
                {
                    _ = buildUse(operand2);
                    sourceCount++;
                }

                if ((intrinsicName is NI_PRIMITIVE_SaturateToInt8) ||
                    (intrinsicName is NI_PRIMITIVE_SaturateToInt16) ||
                    (intrinsicName is NI_PRIMITIVE_SaturateToUInt8) ||
                    (intrinsicName is NI_PRIMITIVE_SaturateToUInt16))
                {
                    assert(operand2 is null);
                    assert(varTypeIsIntegral(operand1.Type));
                    assert(varTypeIsIntegral(tree.Type));
                    // Integer saturation materializes its bound constants in a temporary.
                    _ = buildInternalIntRegisterDefForNode(tree);
                    _setInternalRegistersDelayFree = true;
                    buildInternalRegisterUses();
                }
                else
                {
                    assert(varTypeIsFloating(operand1.Type));
                    assert(operand1.Type == tree.Type);
                    if (operand2 is not null)
                    {
                        assert(operand2.Type == tree.Type);
                    }
                }

                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
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

            case GT_NEG:
            case GT_NOT:
            {
                _ = buildUse(tree.AsUnOp().Op1);
                sourceCount = 1;
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
            case GT_JCMP:
            {
                sourceCount = buildCmp(tree);
                break;
            }

            case GT_CKFINITE:
            {
                sourceCount = 1;
                assert(destinationCount == 1);
                _ = buildInternalIntRegisterDefForNode(tree);
                _ = buildUse(tree.AsUnOp().Op1);
                _ = buildDef(tree, SRBM_NONE);
                buildInternalRegisterUses();
                break;
            }

            case GT_CMPXCHG:
            {
                NYI_LOONGARCH64("-----unimplemented on LOONGARCH64 yet----");
                break;
            }

            case GT_LOCKADD:
            case GT_XORR:
            case GT_XAND:
            case GT_XADD:
            case GT_XCHG:
            {
                NYI_LOONGARCH64("-----unimplemented on LOONGARCH64 yet----");
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
                var call = tree.AsCall();
                sourceCount = buildCall(call);
                if (call.HasMultiRegRetVal)
                {
                    destinationCount = call.ReturnTypeDesc.ReturnRegCount;
                }
                break;
            }

            case GT_BLK:
            {
                // Non-store block nodes must be eliminated before lowering.
                assert(false, "Non-store block node in Lowering.");
                sourceCount = 0;
                break;
            }

            case GT_STORE_BLK:
            {
                sourceCount = buildBlockStore(tree.AsBlk());
                break;
            }

            case GT_INIT_VAL:
            {
                // INIT_VAL is a passthrough and must always be contained.
                assert(false, "INIT_VAL should always be contained.");
                sourceCount = 0;
                break;
            }

            case GT_LCLHEAP:
            {
                assert(destinationCount == 1);

                // genLclHeap may need two temporaries only for large allocations that need
                // page probing and are not initialized.
                var size = tree.AsUnOp().Op1;
                if (size.Oper.IsCnsIntOrI)
                {
                    assert(size.IsContained);
                    sourceCount = 0;

                    var sizeValue = unchecked((nuint)size.AsIntCon().IconValue);
                    if (sizeValue != 0)
                    {
                        sizeValue = unchecked(
                            (sizeValue + ((nuint)STACK_ALIGN - 1)) & ~((nuint)STACK_ALIGN - 1));

                        if (sizeValue > (nuint)(REGSIZE_BYTES * 2 * 4) && !_compiler.info.compInitMem)
                        {
                            if (sizeValue >= _compiler.eeGetPageSize())
                            {
                                _ = buildInternalIntRegisterDefForNode(tree);
                                _ = buildInternalIntRegisterDefForNode(tree);
                            }
                        }
                    }
                }
                else
                {
                    sourceCount = 1;
                    if (!_compiler.info.compInitMem)
                    {
                        _ = buildInternalIntRegisterDefForNode(tree);
                        _ = buildInternalIntRegisterDefForNode(tree);
                    }
                }

                if (!size.IsContained)
                {
                    _ = buildUse(size);
                }
                buildInternalRegisterUses();
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_BOUNDS_CHECK:
            {
                var node = tree.AsBoundsChk();
                // Bounds checks consume the array length and index, and produce no value.
                assert(destinationCount == 0);
                sourceCount = buildOperandUses(node.Index);
                sourceCount += buildOperandUses(node.ArrayLength);
                break;
            }

            case GT_LEA:
            {
                var lea = tree.AsAddrMode();
                var baseAddress = lea.BaseAddress;
                var index = lea.Index;
                var offset = lea.Offset;

                sourceCount = 0;
                if (baseAddress is not null)
                {
                    sourceCount++;
                    _ = buildUse(baseAddress);
                }
                if (index is not null)
                {
                    sourceCount++;
                    _ = buildUse(index);
                }
                assert(destinationCount == 1);

                // An indexed address and nonzero offset require materializing the address.
                if ((index is not null) && (offset != 0))
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
                }
                else if (!Emitter.isValidSimm12(offset))
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
                }
                buildInternalRegisterUses();
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_STOREIND:
            {
                assert(destinationCount == 0);
                if (_compiler.codeGen!.GCInfo.gcIsWriteBarrierStoreIndNode(tree.AsStoreInd()))
                {
                    sourceCount = buildGCWriteBarrier(tree);
                    break;
                }

                sourceCount = buildIndir(tree.AsIndir());
                if (!tree.AsStoreInd().Data.IsContained)
                {
                    _ = buildUse(tree.AsStoreInd().Data);
                    sourceCount++;
                }
                break;
            }

            case GT_NULLCHECK:
            case GT_IND:
            {
                assert(destinationCount == (tree.Oper is GT_NULLCHECK ? 0 : 1));
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
                sourceCount = 0;
                _ = buildDef(tree, genSingleTypeRegMask(REG_ASYNC_CONTINUATION_RET));
                break;
            }

            case GT_INDEX_ADDR:
            {
                assert(destinationCount == 1);
                sourceCount = buildBinaryUses(tree.AsOp());
                _ = buildInternalIntRegisterDefForNode(tree);
                buildInternalRegisterUses();
                _ = buildDef(tree, SRBM_NONE);
                break;
            }
        }

        if (tree.IsUnusedValue && (destinationCount != 0))
        {
            isLocalDefUse = true;
        }

        assert((destinationCount < 2) || tree.IsMultiRegNode);
        assert(isLocalDefUse == (tree.IsValue && tree.IsUnusedValue));
        assert(!tree.IsUnusedValue || (destinationCount != 0));
        assert(destinationCount == tree.GetRegisterDstCount(_compiler));
        return sourceCount;
    }

#if FEATURE_HW_INTRINSICS
    private int buildHWIntrinsic(GenTreeHWIntrinsic intrinsic, out int destinationCount)
    {
        NYI_LOONGARCH64("-----unimplemented on LOONGARCH64 yet----");
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 hardware-intrinsic LSRA is not ported.");
    }
#endif
}
#endif
