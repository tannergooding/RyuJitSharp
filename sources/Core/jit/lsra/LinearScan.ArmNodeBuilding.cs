// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private int buildShiftLongCarry(GenTree tree)
    {
        assert(tree.Oper is GT_LSH_HI or GT_RSH_LO);

        var sourceCount = 2;
        var source = tree.AsOp().Op1;
        assert((source.Oper is GT_LONG) && source.IsContained);

        var sourceLo = source.AsOp().Op1;
        var sourceHi = source.AsOp().Op2;
        var shiftBy = tree.AsOp().Op2;
        assert(!sourceLo.IsContained && !sourceHi.IsContained);
        var sourceLoUse = buildUse(sourceLo);
        var sourceHiUse = buildUse(sourceHi);

        if (!tree.IsContained)
        {
            if (tree.Oper is GT_LSH_HI)
            {
                setDelayFree(sourceLoUse);
            }
            else
            {
                setDelayFree(sourceHiUse);
            }

            if (!shiftBy.IsContained)
            {
                _ = buildUse(shiftBy);
                sourceCount++;
            }
            _ = buildDef(tree, SRBM_NONE);
        }
        else
        {
            if (!shiftBy.IsContained)
            {
                _ = buildUse(shiftBy);
                sourceCount++;
            }
        }

        return sourceCount;
    }

    private int buildNodeArm(GenTree tree)
    {
        assert(!tree.IsContained);
        int sourceCount;
        int destinationCount;
        regMaskTP killMask;
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
                if ((tree.Oper is GT_LCL_FLD) && tree.AsLclFld().IsOffsetMisaligned)
                {
                    // The address and each floating-point word require integer temporaries.
                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    if (tree.Type is TYP_DOUBLE)
                    {
                        _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    }
                    buildInternalRegisterUses();
                }

                sourceCount = 0;
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

            case GT_INTRINSIC:
            {
                var operand = tree.AsUnOp().Op1;
                _ = buildUse(operand);
                sourceCount = 1;

                switch (tree.AsIntrinsic().IntrinsicName)
                {
                    case NI_System_Math_Abs:
                    case NI_System_Math_Sqrt:
                    {
                        assert(varTypeIsFloating(operand.Type));
                        assert(operand.Type == tree.Type);
                        assert(destinationCount == 1);
                        _ = buildDef(tree, SRBM_NONE);
                        break;
                    }

                    case NI_PRIMITIVE_SaturateToInt8:
                    case NI_PRIMITIVE_SaturateToInt16:
                    case NI_PRIMITIVE_SaturateToUInt8:
                    case NI_PRIMITIVE_SaturateToUInt16:
                    {
                        assert(operand.Type is TYP_INT);
                        assert(tree.Type is TYP_INT);
                        assert(destinationCount == 1);
                        _ = buildDef(tree, SRBM_NONE);
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            case GT_CAST:
            {
                assert(destinationCount == 1);
                sourceCount = buildArmCast(tree.AsCast());
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
                noway_assert(false, "!\"Switch must be lowered at this point\"");
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
                sourceCount = buildBinaryUses(tree.AsOp());
                assert(sourceCount == 2);
                break;
            }

            case GT_ADD_LO:
            case GT_ADD_HI:
            case GT_SUB_LO:
            case GT_SUB_HI:
            case GT_ADD:
            case GT_SUB:
            {
                if (varTypeIsFloating(tree.Type))
                {
                    assert(!tree.HasOverflowCheckEx);
                    assert(tree.AsOp().Op1.Type == tree.AsOp().Op2.Type);
                    assert(destinationCount == 1);
                    sourceCount = buildBinaryUses(tree.AsOp());
                    assert(sourceCount == 2);
                    _ = buildDef(tree, SRBM_NONE);
                    break;
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
                assert(destinationCount == 1);
                sourceCount = buildBinaryUses(tree.AsOp());
                assert(sourceCount == (tree.AsOp().Op2.IsContained ? 1 : 2));
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_LSH_HI:
            case GT_RSH_LO:
            {
                assert(destinationCount == 1);
                sourceCount = buildShiftLongCarry(tree);
                assert(sourceCount == (tree.AsOp().Op2.IsContained ? 2 : 3));
                break;
            }

            case GT_RETURNTRAP:
            {
                sourceCount = 1;
                assert(destinationCount == 0);
                _ = buildUse(tree.AsUnOp().Op1);
                killMask = _compiler.compHelperCallKillSet(CORINFO_HELP_STOP_FOR_GC);
                buildKills(tree, killMask);
                break;
            }

            case GT_MUL:
            {
                if (tree.HasOverflowCheckEx)
                {
                    _setInternalRegistersDelayFree = true;
                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                }
                goto case GT_DIV;
            }

            case GT_DIV:
            case GT_MULHI:
            case GT_UDIV:
            {
                assert(destinationCount == 1);
                sourceCount = buildBinaryUses(tree.AsOp());
                assert(sourceCount == 2);
                buildInternalRegisterUses();
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_MUL_LONG:
            {
                destinationCount = 2;
                sourceCount = buildBinaryUses(tree.AsOp());
                assert(sourceCount == 2);
                buildDefs(tree, 2, SRBM_NONE);
                break;
            }

            case GT_FIELD_LIST:
            {
                noway_assert(false, "!\"Non-contained GT_FIELD_LIST\"");
                sourceCount = 0;
                break;
            }

            case GT_NO_OP:
            case GT_START_NONGC:
            case GT_PROF_HOOK:
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

            case GT_LONG:
            {
                // Only an unused, non-contained pair reaches this dispatcher.
                assert(tree.IsUnusedValue);
                tree.Type = TYP_VOID;
                tree.IsUnusedValue = false;
                isLocalDefUse = false;
                sourceCount = 2;
                destinationCount = 0;
                _ = buildUse(tree.AsOp().Op1);
                _ = buildUse(tree.AsOp().Op2);
                break;
            }

            case GT_CNS_DBL:
            {
                if (tree.Type is TYP_FLOAT)
                {
                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                }
                else
                {
                    assert(tree.Type is TYP_DOUBLE);
                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                }
                goto case GT_CNS_INT;
            }

            case GT_CNS_INT:
            {
                sourceCount = 0;
                assert(destinationCount == 1);
                buildInternalRegisterUses();
                var definition = buildDef(tree, SRBM_NONE);
                definition.getInterval().isConstant = true;
                break;
            }

            case GT_RETURN:
            {
                sourceCount = buildReturn(tree);
                killMask = getKillSetForReturn(tree);
                buildKills(tree, killMask);
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
                    _ = buildUse(tree.AsUnOp().Op1, genSingleTypeRegMask(REG_INTRET));
                }
                break;
            }

            case GT_BOUNDS_CHECK:
            {
                sourceCount = 2;
                assert(destinationCount == 0);
                _ = buildUse(tree.AsBoundsChk().Index);
                _ = buildUse(tree.AsBoundsChk().ArrayLength);
                break;
            }

            case GT_LEA:
            {
                var address = tree.AsAddrMode();
                var offset = address.Offset;
                sourceCount = 0;
                assert(destinationCount == 1);
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

                if (address.HasBaseAddress && address.HasIndex)
                {
                    if (offset != 0)
                    {
                        _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    }
                }
                else if (address.HasBaseAddress)
                {
                    if (!Emitter.emitIns_valid_imm_for_add(offset, INS_FLAGS_DONT_CARE))
                    {
                        _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                    }
                }
                buildInternalRegisterUses();
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_NEG:
            case GT_NOT:
            {
                sourceCount = 1;
                assert(destinationCount == 1);
                _ = buildUse(tree.AsUnOp().Op1);
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            case GT_CMP:
            {
                sourceCount = buildCmp(tree);
                break;
            }

            case GT_CKFINITE:
            {
                sourceCount = 1;
                assert(destinationCount == 1);
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                _ = buildUse(tree.AsUnOp().Op1);
                buildInternalRegisterUses();
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            case GT_CALL:
            {
                sourceCount = buildArmCall(tree.AsCall());
                if (tree.AsCall().HasMultiRegRetVal)
                {
                    destinationCount = tree.AsCall().ReturnTypeDesc.ReturnRegCount;
                }
                break;
            }

            case GT_STORE_BLK:
            {
                sourceCount = buildArmBlockStore(tree.AsBlk());
                break;
            }

            case GT_INIT_VAL:
            {
                assert(false, "!\"INIT_VAL should always be contained\"");
                sourceCount = 0;
                break;
            }

            case GT_LCLHEAP:
            {
                sourceCount = buildArmLclHeap(tree);
                break;
            }

            case GT_STOREIND:
            {
                assert(destinationCount == 0);
                var source = tree.AsOp().Op2;
                var codeGen = _compiler.codeGen
                    ?? throw new FatalJitException("ARM32 store reference building requires initialized CodeGen.");
                if (codeGen.GCInfo.gcIsWriteBarrierStoreIndNode(tree.AsStoreInd()))
                {
                    sourceCount = buildGCWriteBarrier(tree);
                    break;
                }

                sourceCount = buildArmIndir(tree.AsIndir());
                assert(!source.IsContained);
                sourceCount++;
                _ = buildUse(source);
                break;
            }

            case GT_NULLCHECK:
            {
                // A null check needs a target, not an overlapping internal-register lifetime.
                assert(false, "!\"Should never see GT_NULLCHECK on Arm/32\"");
                goto case GT_IND;
            }

            case GT_IND:
            {
                assert(destinationCount == ((tree.Oper is GT_NULLCHECK) ? 0 : 1));
                sourceCount = buildArmIndir(tree.AsIndir());
                break;
            }

            case GT_CATCH_ARG:
            {
                sourceCount = 0;
                assert(destinationCount == 1);
                _ = buildDef(tree, genSingleTypeRegMask(REG_EXCEPTION_OBJECT));
                break;
            }

            case GT_ASYNC_CONTINUATION:
            {
                sourceCount = 0;
                assert(destinationCount == 1);
                _ = buildDef(tree, genSingleTypeRegMask(REG_ASYNC_CONTINUATION_RET));
                break;
            }

            case GT_COPY:
            {
                sourceCount = 1;
                // Soft-float double arguments may be represented as a long pair.
                if (tree.Type is TYP_LONG)
                {
                    destinationCount = 2;
                }
                else
                {
                    assert(destinationCount == 1);
                }
                _ = buildUse(tree.AsUnOp().Op1);
                buildDefs(tree, destinationCount, SRBM_NONE);
                break;
            }

            case GT_PUTARG_STK:
            {
                sourceCount = buildArmPutArgStk(tree.AsPutArgStk());
                break;
            }

            case GT_PUTARG_REG:
            {
                sourceCount = buildPutArgReg(tree.AsUnOp());
                break;
            }

            case GT_BITCAST:
            {
                assert(destinationCount == 1);
                var argumentRegister = tree.RegNum;
                var argumentMask = SRBM_NONE;
                if (argumentRegister != REG_COUNT)
                {
                    argumentMask = genSingleTypeRegMask(argumentRegister);
                }

                if (!tree.AsUnOp().Op1.IsContained)
                {
                    _ = buildUse(tree.AsUnOp().Op1);
                    sourceCount = 1;
                }
                else
                {
                    sourceCount = 0;
                }
                buildDefs(tree, destinationCount, argumentMask);
                break;
            }

            case GT_LCL_ADDR:
            case GT_PHYSREG:
            case GT_IL_OFFSET:
            case GT_RECORD_ASYNC_RESUME:
            case GT_ASYNC_RESUME_INFO:
            case GT_LABEL:
            case GT_JCC:
            case GT_SETCC:
            case GT_MEMORYBARRIER:
            case GT_RETURN_SUSPEND:
            {
                sourceCount = buildSimple(tree);
                break;
            }

            case GT_PATCHPOINT:
            case GT_PATCHPOINT_FORCED:
            {
                NYI_ARM("GT_PATCHPOINT");
                throw new FatalJitException(CORJIT_SKIPPED);
            }

            case GT_JTRUE:
            {
                _ = buildOperandUses(tree.AsUnOp().Op1, SRBM_NONE);
                sourceCount = 1;
                break;
            }

            case GT_INDEX_ADDR:
            {
                destinationCount = 1;
                _ = buildInternalIntRegisterDefForNode(tree, _availableIntRegs);
                sourceCount = buildBinaryUses(tree.AsOp());
                assert(sourceCount == 2);
                buildInternalRegisterUses();
                _ = buildDef(tree, SRBM_NONE);
                break;
            }

            default:
            {
#if DEBUG
                NYIRAW($"NYI: Unimplemented node type {tree.Oper.Name}");
#endif
                unreached();
                throw new FatalJitException(CORJIT_RECOVERABLEERROR);
            }
        }

        assert((destinationCount < 2) || tree.IsMultiRegNode);
        assert(isLocalDefUse == (tree.IsValue && tree.IsUnusedValue));
        assert(!tree.IsValue || (destinationCount != 0));
        assert(destinationCount == tree.GetRegisterDstCount(_compiler));
        return sourceCount;
    }
}
#endif
