// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if TARGET_RISCV64
    private int buildNodeRiscV64(GenTree tree)
    {
        assert(!tree.IsContained);

        var sourceCount = 0;
        var destinationCount = 0;
        var isLocalDefUse = false;

        clearBuildState();

        if (tree.IsValue)
        {
            destinationCount = 1;
            if (tree.IsUnusedValue)
            {
                isLocalDefUse = true;
            }
        }
        else
        {
            destinationCount = 0;
        }

        switch (tree.Oper)
        {
            default:
            {
                sourceCount = buildSimple(tree);
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
                    // Reading the upper four bytes of Vector3 needs a temporary distinct from the result.
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
                var killMask = getKillSetForProfilerHook();
                buildKills(tree, killMask);
                break;
            }

            case GT_START_PREEMPTGC:
            {
                sourceCount = 0;
                assert(destinationCount == 0);
                buildKills(tree, RBM_NONE);
                break;
            }

            case GT_CNS_DBL:
            {
                var size = tree.Type.EmitActualSize;
                if (Emitter.isSingleInstructionFpImm(tree.AsDblCon().DconVal, size, out var bits) && (bits != 0))
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
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
                    _ = buildInternalIntRegisterDefForNode(tree);
                    if (!tree.AsOp().IsUnsigned)
                    {
                        _ = buildInternalIntRegisterDefForNode(tree);
                    }
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
            case GT_ROL:
            case GT_SH1ADD:
            case GT_SH1ADD_UW:
            case GT_SH2ADD:
            case GT_SH2ADD_UW:
            case GT_SH3ADD:
            case GT_SH3ADD_UW:
            case GT_ADD_UW:
            case GT_SLLI_UW:
            case GT_BIT_SET:
            case GT_BIT_CLEAR:
            case GT_BIT_INVERT:
            {
                if ((tree.Oper is GT_ROR or GT_ROL) &&
                    !_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb))
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
                }
                sourceCount = buildBinaryUses(tree.AsOp());
                buildInternalRegisterUses();
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_RETURNTRAP:
            {
                _ = buildUse(tree.AsUnOp().Op1);
                sourceCount = 1;
                assert(destinationCount == 0);
                var killMask = _compiler.compHelperCallKillSet(CORINFO_HELP_STOP_FOR_GC);
                buildKills(tree, killMask);
                break;
            }

            case GT_MUL:
            {
                if (tree.HasOverflowCheckEx)
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
                    if (!tree.AsOp().IsUnsigned)
                    {
                        _ = buildInternalIntRegisterDefForNode(tree);
                    }
                    _setInternalRegistersDelayFree = true;
                }
                goto case GT_MOD;
            }

            case GT_MOD:
            case GT_UMOD:
            case GT_DIV:
            case GT_UDIV:
            {
                sourceCount = buildBinaryUses(tree.AsOp());

                var divisor = tree.AsOp().Op2;
                var exceptions = tree.Exceptions(_compiler);
                if (!varTypeIsFloating(tree.Type) &&
                    !(((exceptions & ExceptionSetFlags.DivideByZeroException) is not ExceptionSetFlags.None) &&
                      (divisor.IsIntegralConst(0) || (divisor.RegNum == REG_ZERO))))
                {
                    var needTemp = false;
                    if (divisor.IsContainedIntOrIImmed && !Emitter.isGeneralRegister(divisor.RegNum))
                    {
                        needTemp = true;
                    }

                    if (!needTemp && (tree.Oper is GT_DIV or GT_MOD) &&
                        ((exceptions & ExceptionSetFlags.ArithmeticException) is not ExceptionSetFlags.None))
                    {
                        needTemp = true;
                    }

                    if (needTemp)
                    {
                        _ = buildInternalIntRegisterDefForNode(tree);
                    }
                }
                buildInternalRegisterUses();
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_MULHI:
            {
                sourceCount = buildBinaryUses(tree.AsOp());

                var size = tree.Type.EmitActualSize;
                if ((size != EA_8BYTE) && tree.AsOp().IsUnsigned)
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
                }

                buildInternalRegisterUses();
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_INTRINSIC:
            {
                var op1 = tree.AsOp().Op1;
                var op2 = tree.GetOp2IfPresent();

                switch (tree.AsIntrinsic().IntrinsicName)
                {
                    case NI_System_Math_MinNative:
                    case NI_System_Math_MaxNative:
                    {
                        assert(op2 is not null);
                        assert(op2.Type == tree.Type);
                        goto case NI_System_Math_Abs;
                    }

                    case NI_System_Math_Abs:
                    case NI_System_Math_Sqrt:
                    {
                        assert(op1.Type == tree.Type);
                        assert(varTypeIsFloating(tree.Type));
                        break;
                    }

                    case NI_System_Math_Min:
                    case NI_System_Math_Max:
                    case NI_System_Math_MinUnsigned:
                    case NI_System_Math_MaxUnsigned:
                    {
                        assert(_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb));
                        assert(op2 is not null);
                        assert(op2.Type == tree.Type);
                        assert(op1.Type == tree.Type);
                        assert(tree.Type is TYP_I_IMPL);
                        break;
                    }

                    case NI_PRIMITIVE_LeadingZeroCount:
                    case NI_PRIMITIVE_TrailingZeroCount:
                    case NI_PRIMITIVE_PopCount:
                    {
                        assert(_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb));
                        assert(op2 is null);
                        assert(varTypeIsIntegral(op1.Type));
                        assert(varTypeIsIntegral(tree.Type));
                        break;
                    }

                    case NI_PRIMITIVE_SaturateToInt8:
                    case NI_PRIMITIVE_SaturateToInt16:
                    case NI_PRIMITIVE_SaturateToUInt8:
                    case NI_PRIMITIVE_SaturateToUInt16:
                    {
                        assert(op2 is null);
                        assert(op1.Type is TYP_INT);
                        assert(tree.Type is TYP_INT);
                        // The clamp bounds are materialized in an internal register.
                        _ = buildInternalIntRegisterDefForNode(tree);
                        break;
                    }

                    default:
                    {
                        throw new FatalJitException($"Unknown RISC-V intrinsic {tree.AsIntrinsic().IntrinsicName}.");
                    }
                }

                _ = buildUse(op1);
                sourceCount = 1;
                if (op2 is not null)
                {
                    _ = buildUse(op2);
                    sourceCount++;
                }
                buildInternalRegisterUses();
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

#if FEATURE_SIMD
            case GT_SIMD:
            {
                sourceCount = buildSIMD(tree.AsSIMD());
                break;
            }
#endif

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

            case GT_SELECT:
            {
                var select = tree.AsConditional();
                sourceCount = buildOperandUses(select.Cond);
                sourceCount += buildOperandUses(select.Op1);
                sourceCount += buildOperandUses(select.Op2);
                if (!select.Op1.IsContained && !select.Op2.IsContained)
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
                }
                assert(destinationCount == 1);
                _ = buildDef(tree, SRBM_NONE);
                buildInternalRegisterUses();
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
                var compareExchange = tree.AsCmpXchg();
                assert(destinationCount == 1);

                sourceCount = 1;
                // Keep argument registers live because the retry loop may reuse them.
                assert(!compareExchange.Addr.IsContained);
                setDelayFree(buildUse(compareExchange.Addr));

                var data = compareExchange.Data;
                if (!data.IsContained)
                {
                    sourceCount++;
                    setDelayFree(buildUse(data));
                }
                else
                {
                    assert(data.IsIntegralConst(0));
                }

                var comparand = compareExchange.Comparand;
                if (!comparand.IsContained)
                {
                    sourceCount++;
                    var use = buildUse(comparand);
                    if (comparand.Type is TYP_INT or TYP_UINT)
                    {
                        _ = buildInternalIntRegisterDefForNode(tree);
                    }
                    else
                    {
                        setDelayFree(use);
                    }
                }
                else
                {
                    assert(comparand.IsIntegralConst(0));
                }

                _ = buildInternalIntRegisterDefForNode(tree);
                _setInternalRegistersDelayFree = true;
                buildInternalRegisterUses();
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_LOCKADD:
            {
                assert(false, "GT_LOCKADD is unimplemented on RISC-V64.");
                break;
            }

            case GT_XORR:
            case GT_XAND:
            case GT_XADD:
            case GT_XCHG:
            {
                assert(destinationCount == (tree.Type is TYP_VOID ? 0 : 1));
                var address = tree.AsOp().Op1;
                var data = tree.AsOp().Op2;
                assert(!address.IsContained);

                sourceCount = 1;
                _ = buildUse(address);
                if (!data.IsContained)
                {
                    sourceCount++;
                    _ = buildUse(data);
                }
                else
                {
                    assert(data.IsIntegralConst(0));
                }

                if (destinationCount == 1)
                {
                    _ = buildDef(tree, SRBM_NONE);
                }
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

            case GT_BLK:
            {
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
                assert(false, "INIT_VAL should always be contained.");
                sourceCount = 0;
                break;
            }

            case GT_LCLHEAP:
            {
                assert(destinationCount == 1);

                // See genLclHeap: large uninitialized allocations may need probe state and a temporary.
                var needExtraTemp = _compiler.lvaOutgoingArgSpaceSize.Value > 0;
                var size = tree.AsUnOp().Op1;
                if (size.Oper.IsCnsIntOrI)
                {
                    assert(size.IsContained);
                    sourceCount = 0;

                    var sizeValue = unchecked((nuint)size.AsIntCon().IconValue);
                    if (sizeValue != 0)
                    {
                        // Recompute the aligned size without changing the tree.
                        var alignment = (nuint)STACK_ALIGN;
                        sizeValue = unchecked(sizeValue + (alignment - 1)) & ~(alignment - 1);

                        // Allocations up to four stores need no internal registers.
                        if (sizeValue > (nuint)(REGSIZE_BYTES * 2 * 4) && !_compiler.info.compInitMem)
                        {
                            if (sizeValue < _compiler.eeGetPageSize())
                            {
                                var immediate = unchecked(-(nint)sizeValue);
                                needExtraTemp |= !Emitter.isValidSimm12(immediate);
                            }
                            else
                            {
                                // Page probing requires a counter and a temporary register.
                                _ = buildInternalIntRegisterDefForNode(tree);
                                _ = buildInternalIntRegisterDefForNode(tree);
                                needExtraTemp = true;
                            }
                        }
                    }
                }
                else
                {
                    sourceCount = 1;
                    if (!_compiler.info.compInitMem)
                    {
                        // A non-constant size with uninitialized memory also requires page-probe state.
                        _ = buildInternalIntRegisterDefForNode(tree);
                        _ = buildInternalIntRegisterDefForNode(tree);
                        needExtraTemp = true;
                    }
                }

                if (needExtraTemp)
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
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
                var boundsCheck = tree.AsBoundsChk();
                if (genActualType(boundsCheck.ArrayLength) is TYP_INT)
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
                }
                if (genActualType(boundsCheck.Index) is TYP_INT)
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
                }
                buildInternalRegisterUses();
                assert(destinationCount == 0);
                sourceCount = buildOperandUses(boundsCheck.Index);
                sourceCount += buildOperandUses(boundsCheck.ArrayLength);
                break;
            }

            case GT_LEA:
            {
                var address = tree.AsAddrMode();
                assert(address.HasBaseAddress);
                assert(!address.HasIndex);
                assert(address.Scale <= 1);

                sourceCount = 1;
                _ = buildUse(address.BaseAddress);
                assert(destinationCount == 1);

                if (!Emitter.isValidSimm12(address.Offset))
                {
                    _ = buildInternalIntRegisterDefForNode(tree);
                    buildInternalRegisterUses();
                }

                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_STOREIND:
            {
                assert(destinationCount == 0);
                assert(_compiler.codeGen is not null);
                if (_compiler.codeGen.GCInfo.gcIsWriteBarrierStoreIndNode(tree.AsStoreInd()))
                {
                    sourceCount = buildGCWriteBarrier(tree);
                    break;
                }

                sourceCount = buildIndir(tree.AsIndir());
                if (!tree.AsOp().Op2.IsContained)
                {
                    _ = buildUse(tree.AsOp().Op2);
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

            case GT_INC_SATURATE:
            {
                assert(destinationCount == 1);
                sourceCount = 1;
                setDelayFree(buildUse(tree.AsUnOp().Op1));
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

#if FEATURE_SIMD
    private int buildSIMD(GenTreeSIMD simdTree)
    {
        NYI_RISCV64("-----unimplemented on RISCV64 yet----");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD LSRA is not ported.");
    }
#endif

#if FEATURE_HW_INTRINSICS
    private int buildHWIntrinsic(GenTreeHWIntrinsic intrinsicTree, out int destinationCount)
    {
        NYI_RISCV64("-----unimplemented on RISCV64 yet----");
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 hardware-intrinsic LSRA is not ported.");
    }
#endif
#endif
}
