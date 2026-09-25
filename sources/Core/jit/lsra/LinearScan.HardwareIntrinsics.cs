// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if FEATURE_HW_INTRINSICS && TARGET_AMD64
    private int buildDelayFreeUses(GenTree node, GenTree? rmwNode, SingleTypeRegSet candidates = SRBM_NONE)
    {
        RefPosition? use = null;
        return buildDelayFreeUses(node, rmwNode, candidates, ref use);
    }

    private int buildHWIntrinsic(GenTreeHWIntrinsic intrinsic, out int destinationCount)
    {
        var intrinsicId = intrinsic.HWIntrinsicId;
        var baseType = intrinsic.SimdBaseType;
        var operandCount = intrinsic.Operands.Length;
        var category = HWIntrinsicInfo.lookupCategory(intrinsicId);

        if (intrinsic.SimdSize != 0)
        {
            setContainsAVXFlags((uint)intrinsic.SimdSize);
        }

        destinationCount = intrinsic.IsValue
            ? (HWIntrinsicInfo.IsMultiReg(intrinsicId) ? HWIntrinsicInfo.GetMultiRegCount(intrinsicId) : 1)
            : 0;

        var sourceCount = 0;
        var destinationCandidates = SRBM_NONE;

        if (operandCount != 0)
        {
            assert(operandCount <= 5);
            var op1 = intrinsic.GetOp(1);
            var op2 = operandCount >= 2 ? intrinsic.GetOp(2) : op1;
            var op3 = operandCount >= 3 ? intrinsic.GetOp(3) : op1;
            var op4 = operandCount >= 4 ? intrinsic.GetOp(4) : op1;
            var op5 = operandCount >= 5 ? intrinsic.GetOp(5) : op1;
            var lastOp = intrinsic.GetOp(operandCount);

            var buildUses = true;
            var isRmw = intrinsic.IsRmwHWIntrinsic(_compiler);
            var isEvexCompatible = intrinsic.IsEvexCompatibleHWIntrinsic(_compiler);
            var canUseApxRegs = isEvexCompatible && _evexIsSupported;

            if ((category is HW_Category_IMM) &&
                ((HWIntrinsicInfo.lookupFlags(intrinsicId) & HW_Flag_NoJmpTableIMM) == 0) &&
                HWIntrinsicInfo.isImmOp(intrinsicId, lastOp) && !lastOp.IsContainedIntOrIImmed)
            {
                assert(!lastOp.Oper.IsCnsIntOrI);
                _ = buildInternalIntRegisterDefForNode(intrinsic, _availableIntRegs);
                _ = buildInternalIntRegisterDefForNode(intrinsic, _availableIntRegs);
            }

            if (intrinsic.IsEmbeddedRoundingEnabled && !lastOp.Oper.IsCnsIntOrI)
            {
                _ = buildInternalIntRegisterDefForNode(intrinsic, _availableIntRegs);
                _ = buildInternalIntRegisterDefForNode(intrinsic, _availableIntRegs);
            }

            switch (intrinsicId)
            {
                case NI_Vector_CreateScalar:
                case NI_Vector_CreateScalarUnsafe:
                case NI_Vector_ToScalar:
                {
                    assert(operandCount == 1);
                    if (varTypeIsFloating(baseType))
                    {
                        if (op1.IsContained)
                        {
                            var candidates = forceLowGprForApxIfNeeded(op1, SRBM_NONE, canUseApxRegs);
                            sourceCount += buildOperandUses(op1, candidates);
                        }
                        else
                        {
                            _targetPreferredUse = buildUse(op1);
                            sourceCount++;
                        }
                        buildUses = false;
                    }
                    break;
                }

                case NI_Vector_GetElement:
                {
                    assert(operandCount == 2);
                    if (!op2.Oper.IsConst && !op1.IsContained)
                    {
                        _ = _compiler.getSIMDInitTempVarNum(Compiler.GetSimdTypeForSize(intrinsic.SimdSize));
                    }
                    else if (op1.Oper.IsCnsVec)
                    {
                        _ = buildInternalIntRegisterDefForNode(intrinsic, _availableIntRegs);
                    }
                    break;
                }

                case NI_Vector_WithElement:
                {
                    assert(operandCount == 3);
                    assert(!op1.IsContained && !op2.Oper.IsConst);

                    _ = _compiler.getSIMDInitTempVarNum(intrinsic.Type);
                    sourceCount += buildOperandUses(op1);
                    sourceCount += buildOperandUses(op2);
                    sourceCount += buildOperandUses(op3, varTypeIsByte(baseType) ? _availableIntRegs : SRBM_NONE);
                    buildUses = false;
                    break;
                }

                case NI_Vector_AsVector128Unsafe:
                case NI_Vector_AsVector2:
                case NI_Vector_AsVector3:
                case NI_Vector_ToVector256:
                case NI_Vector_ToVector256Unsafe:
                case NI_Vector_ToVector512:
                case NI_Vector_ToVector512Unsafe:
                case NI_Vector_GetLower:
                case NI_Vector_GetLower128:
                {
                    assert(operandCount == 1);
                    var candidates = forceLowGprForApxIfNeeded(op1, SRBM_NONE, canUseApxRegs);
                    if (op1.IsContained)
                    {
                        sourceCount += buildOperandUses(op1, candidates);
                    }
                    else
                    {
                        _targetPreferredUse = buildUse(op1, candidates);
                        sourceCount++;
                    }
                    buildUses = false;
                    break;
                }

                case NI_X86Base_MaskMove:
                {
                    assert(operandCount == 3 && !isRmw);
                    sourceCount += buildOperandUses(op1, buildEvexIncompatibleMask(op1));
                    sourceCount += buildOperandUses(op2, buildEvexIncompatibleMask(op2));
                    sourceCount += buildOperandUses(op3, SRBM_EDI);
                    buildUses = false;
                    break;
                }

                case NI_X86Base_BlendVariable:
                {
                    assert(operandCount == 3);
                    if (!_compiler.canUseVexEncoding())
                    {
                        assert(isRmw);
                        _targetPreferredUse = buildUse(op1, buildEvexIncompatibleMask(op1));
                        sourceCount++;

                        var candidates = forceLowGprForApx(op2);
                        if (candidates == SRBM_NONE)
                        {
                            candidates = buildEvexIncompatibleMask(op2);
                        }
                        sourceCount += op2.IsContained
                            ? buildOperandUses(op2, candidates)
                            : buildDelayFreeUses(op2, op1, candidates);
                        sourceCount += buildDelayFreeUses(op3, op1, SRBM_XMM0);
                        buildUses = false;
                    }
                    break;
                }

                case NI_X86Base_DivRem:
                case NI_X86Base_X64_DivRem:
                {
                    assert(operandCount == 3 && destinationCount == 2 && isRmw);
                    sourceCount += buildOperandUses(op1, SRBM_EAX);
                    sourceCount += buildOperandUses(op2, SRBM_EDX);
                    if (!op3.IsContained)
                    {
                        RefPosition? thirdUse = null;
                        sourceCount += buildDelayFreeUses(op3, op1, SRBM_NONE, ref thirdUse);
                        if ((thirdUse is not null) && !thirdUse.delayRegFree)
                        {
                            addDelayFreeUses(thirdUse, op2);
                        }
                    }
                    else
                    {
                        sourceCount += buildOperandUses(op3,
                            forceLowGprForApxIfNeeded(op3, SRBM_NONE, canUseApxRegs));
                    }
                    _ = buildDef(intrinsic, SRBM_EAX, 0);
                    _ = buildDef(intrinsic, SRBM_EDX, 1);
                    buildUses = false;
                    break;
                }

                case NI_X86Base_X64_BigMul:
                {
                    assert(operandCount == 2 && destinationCount == 2 && isRmw && !op1.IsContained);
                    var candidates = forceLowGprForApx(op1);
                    sourceCount = buildOperandUses(op1, op2.IsContained ? SRBM_EAX : candidates);
                    sourceCount += buildOperandUses(op2, candidates);
                    _ = buildDef(intrinsic, SRBM_EAX, 0);
                    _ = buildDef(intrinsic, SRBM_EDX, 1);
                    buildUses = false;
                    break;
                }

                case NI_AVX2_MultiplyNoFlags:
                case NI_AVX2_X64_MultiplyNoFlags:
                {
                    assert(operandCount is 2 or 3);
                    sourceCount += buildOperandUses(op1, SRBM_EDX);
                    var candidates = forceLowGprForApxIfNeeded(op2, _availableIntRegs & ~SRBM_EDX, canUseApxRegs);
                    sourceCount += buildOperandUses(op2, candidates);
                    if (operandCount == 3)
                    {
                        sourceCount += buildDelayFreeUses(op3, op1);
                        _ = buildInternalIntRegisterDefForNode(intrinsic, _availableIntRegs);
                        _setInternalRegistersDelayFree = true;
                    }
                    buildUses = false;
                    break;
                }

                case NI_AVX2_MultiplyAdd:
                case NI_AVX2_MultiplyAddNegated:
                case NI_AVX2_MultiplyAddNegatedScalar:
                case NI_AVX2_MultiplyAddScalar:
                case NI_AVX2_MultiplyAddSubtract:
                case NI_AVX2_MultiplySubtract:
                case NI_AVX2_MultiplySubtractAdd:
                case NI_AVX2_MultiplySubtractNegated:
                case NI_AVX2_MultiplySubtractNegatedScalar:
                case NI_AVX2_MultiplySubtractScalar:
                case NI_AVX512_FusedMultiplyAdd:
                case NI_AVX512_FusedMultiplyAddScalar:
                case NI_AVX512_FusedMultiplyAddNegated:
                case NI_AVX512_FusedMultiplyAddNegatedScalar:
                case NI_AVX512_FusedMultiplyAddSubtract:
                case NI_AVX512_FusedMultiplySubtract:
                case NI_AVX512_FusedMultiplySubtractScalar:
                case NI_AVX512_FusedMultiplySubtractAdd:
                case NI_AVX512_FusedMultiplySubtractNegated:
                case NI_AVX512_FusedMultiplySubtractNegatedScalar:
                case NI_AVX10v1_FusedMultiplyAddScalar:
                {
                    assert((operandCount == 3 || intrinsic.IsEmbeddedRoundingEnabled) &&
                        isRmw && HWIntrinsicInfo.IsFmaIntrinsic(intrinsicId));
                    var copiesUpperBits = HWIntrinsicInfo.CopiesUpperBits(intrinsicId);
                    // Reordering permits any register operand to be the RMW target unless op1 supplies upper bits.
                    var emitOp1 = op1;
                    var emitOp2 = op2;
                    var emitOp3 = op3;
                    if (op1.IsContained || op1.IsRegOptional)
                    {
                        assert(!copiesUpperBits);
                        (emitOp1, emitOp3) = (emitOp3, emitOp1);
                    }
                    else if (op2.IsContained || op2.IsRegOptional)
                    {
                        (emitOp2, emitOp3) = (emitOp3, emitOp2);
                    }

                    // Lowering retains LIR order across the three FMA instruction forms.
                    // Build references in that order, but select preferences from the expected emission order.
                    for (var operandIndex = 1; operandIndex <= 3; operandIndex++)
                    {
                        var op = intrinsic.GetOp(operandIndex);
                        if (ReferenceEquals(op, emitOp1))
                        {
                            _targetPreferredUse = buildUse(op);
                            sourceCount++;
                        }
                        else if (ReferenceEquals(op, emitOp2))
                        {
                            if (copiesUpperBits)
                            {
                                sourceCount += buildDelayFreeUses(op, emitOp1);
                            }
                            else
                            {
                                _targetPreferredUse2 = buildUse(op);
                                sourceCount++;
                            }
                        }
                        else if (ReferenceEquals(op, emitOp3))
                        {
                            if (op.IsContained)
                            {
                                sourceCount += buildOperandUses(op,
                                    forceLowGprForApxIfNeeded(op, SRBM_NONE, canUseApxRegs));
                            }
                            else if (copiesUpperBits)
                            {
                                sourceCount += buildDelayFreeUses(op, emitOp1);
                            }
                            else
                            {
                                _targetPreferredUse3 = buildUse(op);
                                sourceCount++;
                            }
                        }
                    }

                    if (intrinsic.IsEmbeddedRoundingEnabled && !intrinsic.GetOp(4).Oper.IsCnsIntOrI)
                    {
                        sourceCount += buildOperandUses(intrinsic.GetOp(4));
                    }
                    buildUses = false;
                    break;
                }

                case NI_AVX512_BlendVariableMask:
                {
                    assert(operandCount == 3);
                    if ((op2.Flags & GTF_HW_EM_OP) != 0)
                    {
                        assert(!op2.IsRmwHWIntrinsic(_compiler));
                        if (isRmw)
                        {
                            assert(!op1.IsContained && op2.IsContained);
                            _targetPreferredUse = buildUse(op1);
                            sourceCount++;
                            foreach (var operand in op2.AsHWIntrinsic().Operands)
                            {
                                sourceCount += buildDelayFreeUses(operand, op1);
                            }
                        }
                        else
                        {
                            assert(op1.IsContained && op1.IsVectorZero && op2.IsContained);
                            sourceCount += buildOperandUses(op1);
                            foreach (var operand in op2.AsHWIntrinsic().Operands)
                            {
                                sourceCount += buildOperandUses(operand);
                            }
                        }
                        assert(!op3.IsContained);
                        sourceCount += buildOperandUses(op3);
                        buildUses = false;
                    }
                    break;
                }

                case NI_AVX512_PermuteVar2x64x2:
                case NI_AVX512_PermuteVar4x32x2:
                case NI_AVX512_PermuteVar4x64x2:
                case NI_AVX512_PermuteVar8x32x2:
                case NI_AVX512_PermuteVar8x64x2:
                case NI_AVX512_PermuteVar8x16x2:
                case NI_AVX512_PermuteVar16x16x2:
                case NI_AVX512_PermuteVar16x32x2:
                case NI_AVX512_PermuteVar32x16x2:
                case NI_AVX512v2_PermuteVar16x8x2:
                case NI_AVX512v2_PermuteVar32x8x2:
                case NI_AVX512v2_PermuteVar64x8x2:
                {
                    assert(operandCount == 3 && isRmw && HWIntrinsicInfo.IsPermuteVar2x(intrinsicId));
                    assert(!op1.IsContained && !op2.IsContained);

                    var currentBlock = _blockSequence
                        ?? throw new FatalJitException("An intrinsic reference requires an initialized block sequence.");
                    GenTree? user = null;
                    if (currentBlock[_currentBlockSequenceNumber].TryGetUse(intrinsic, out var use))
                    {
                        user = use.User();
                    }
                    var resultOpNum = intrinsic.GetResultOpNumForRmwIntrinsic(user, op1, op2, op3);
                    var emitOp1 = resultOpNum == 2 ? op2 : op1;
                    var emitOp2 = resultOpNum == 2 ? op1 : op2;
                    for (var operandIndex = 1; operandIndex <= 3; operandIndex++)
                    {
                        var op = intrinsic.GetOp(operandIndex);
                        if (ReferenceEquals(op, emitOp1))
                        {
                            _targetPreferredUse = buildUse(op);
                            sourceCount++;
                        }
                        else if (ReferenceEquals(op, emitOp2))
                        {
                            sourceCount += buildDelayFreeUses(op, emitOp1, forceLowGprForApx(op));
                        }
                        else
                        {
                            sourceCount += op.IsContained
                                ? buildOperandUses(op, forceLowGprForApx(op))
                                : buildDelayFreeUses(op, emitOp1);
                        }
                    }
                    buildUses = false;
                    break;
                }

                case NI_AVX512_TernaryLogic:
                {
                    assert(operandCount == 4);
                    if (!op4.IsContainedIntOrIImmed)
                    {
                        break;
                    }
                    if (op1.IsContained)
                    {
                        sourceCount += buildOperandUses(op1);
                    }
                    else
                    {
                        _targetPreferredUse = buildUse(op1);
                        sourceCount++;
                    }
                    if (op2.IsContained)
                    {
                        sourceCount += buildOperandUses(op2);
                    }
                    else
                    {
                        _targetPreferredUse2 = buildUse(op2);
                        sourceCount++;
                    }
                    if (op3.IsContained)
                    {
                        sourceCount += buildOperandUses(op3,
                            forceLowGprForApxIfNeeded(op3, SRBM_NONE, canUseApxRegs));
                    }
                    else
                    {
                        _targetPreferredUse3 = buildUse(op3);
                        sourceCount++;
                    }
                    assert(op4.IsContained);
                    sourceCount += buildOperandUses(op4);
                    buildUses = false;
                    break;
                }

                case NI_AVXVNNI_MultiplyWideningAndAdd:
                case NI_AVXVNNI_MultiplyWideningAndAddSaturate:
                case NI_AVX512v3_MultiplyWideningAndAdd:
                case NI_AVX512v3_MultiplyWideningAndAddSaturate:
                case NI_AVXVNNIINT_MultiplyWideningAndAdd:
                case NI_AVXVNNIINT_MultiplyWideningAndAddSaturate:
                case NI_AVXVNNIINT_V512_MultiplyWideningAndAdd:
                case NI_AVXVNNIINT_V512_MultiplyWideningAndAddSaturate:
                {
                    assert(operandCount == 3);
                    _targetPreferredUse = buildUse(op1);
                    sourceCount++;
                    sourceCount += buildDelayFreeUses(op2, op1);
                    sourceCount += op3.IsContained
                        ? buildOperandUses(op3, forceLowGprForApx(op3))
                        : buildDelayFreeUses(op3, op1);
                    buildUses = false;
                    break;
                }

                case NI_AVX2_GatherVector128:
                case NI_AVX2_GatherVector256:
                {
                    assert(operandCount == 3 && !isRmw && op3.IsContained);
                    var op1Candidates = forceLowGprForApx(op1);
                    if (op1Candidates == SRBM_NONE)
                    {
                        op1Candidates = buildEvexIncompatibleMask(op1);
                    }
                    sourceCount += buildOperandUses(op1, op1Candidates);
                    var op2Candidates = forceLowGprForApx(op2);
                    if (op2Candidates == SRBM_NONE)
                    {
                        op2Candidates = buildEvexIncompatibleMask(op2);
                    }
                    sourceCount += buildDelayFreeUses(op2, null, op2Candidates);
                    _ = buildInternalFloatRegisterDefForNode(intrinsic, lowSIMDRegs());
                    _setInternalRegistersDelayFree = true;
                    buildUses = false;
                    break;
                }

                case NI_AVX2_GatherMaskVector128:
                case NI_AVX2_GatherMaskVector256:
                {
                    assert(operandCount == 5 && !isRmw && op5.IsContained);
                    // The index, mask and destination must be distinct; the instruction destroys its mask.
                    var op1Candidates = forceLowGprForApx(op1);
                    if (op1Candidates == SRBM_NONE)
                    {
                        op1Candidates = buildEvexIncompatibleMask(op2);
                    }
                    sourceCount += buildOperandUses(op1, op1Candidates);
                    var op2Candidates = forceLowGprForApx(op2);
                    if (op2Candidates == SRBM_NONE)
                    {
                        op2Candidates = buildEvexIncompatibleMask(op2);
                    }
                    sourceCount += buildDelayFreeUses(op2, null, op2Candidates);
                    sourceCount += buildDelayFreeUses(op3, null, buildEvexIncompatibleMask(op3));
                    sourceCount += buildDelayFreeUses(op4, null, buildEvexIncompatibleMask(op4));
                    _ = buildInternalFloatRegisterDefForNode(intrinsic, lowSIMDRegs());
                    _setInternalRegistersDelayFree = true;
                    buildUses = false;
                    break;
                }

                case NI_Vector_op_Division:
                {
                    sourceCount = buildOperandUses(op1, lowSIMDRegs());
                    sourceCount += buildOperandUses(op2, lowSIMDRegs());
                    _ = buildInternalFloatRegisterDefForNode(intrinsic, lowSIMDRegs());
                    _ = buildInternalFloatRegisterDefForNode(intrinsic, lowSIMDRegs());
                    if (!_compiler.compOpportunisticallyDependsOn(InstructionSet_AVX512))
                    {
                        // Legacy blendvp* takes its mask specifically in XMM0.
                        var candidates = _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX)
                            ? lowSIMDRegs() : SRBM_XMM0;
                        _ = buildInternalFloatRegisterDefForNode(intrinsic, candidates);
                    }
                    _setInternalRegistersDelayFree = true;
                    buildUses = false;
                    break;
                }

                default:
                {
                    assert(intrinsicId > NI_HW_INTRINSIC_START && intrinsicId < NI_HW_INTRINSIC_END);
                    assert(!HWIntrinsicInfo.IsFmaIntrinsic(intrinsicId) &&
                        !HWIntrinsicInfo.IsPermuteVar2x(intrinsicId));
                    break;
                }
            }

            if (buildUses)
            {
                var op1Candidates = isEvexCompatible ? SRBM_NONE : buildEvexIncompatibleMask(op1);
                op1Candidates = forceLowGprForApxIfNeeded(op1, op1Candidates, canUseApxRegs);
                if (intrinsic.IsMemoryLoadOrStore)
                {
                    sourceCount += buildAddrUses(op1, op1Candidates);
                }
                else if (isRmw && !op1.IsContained)
                {
                    _targetPreferredUse = buildUse(op1, op1Candidates);
                    sourceCount++;
                }
                else
                {
                    sourceCount += buildOperandUses(op1, op1Candidates);
                }

                if (operandCount >= 2)
                {
                    var op2Candidates = isEvexCompatible ? SRBM_NONE : buildEvexIncompatibleMask(op2);
                    if (!isEvexCompatible || !_evexIsSupported)
                    {
                        op2Candidates = forceLowGprForApx(op2, op2Candidates);
                    }
                    if (op2.Oper.IsHWIntrinsic && op2.AsHWIntrinsic().IsMemoryLoad() && op2.IsContained)
                    {
                        sourceCount += buildAddrUses(op2.AsHWIntrinsic().GetOp(1), op2Candidates);
                    }
                    else if (isRmw)
                    {
                        if (!op2.IsContained && intrinsic.IsCommutativeHWIntrinsic)
                        {
                            _targetPreferredUse2 = buildUse(op2, op2Candidates);
                            sourceCount++;
                        }
                        else if (!op2.IsContained || varTypeIsArithmetic(intrinsic.Type))
                        {
                            sourceCount += buildDelayFreeUses(op2, op1, op2Candidates);
                        }
                        else
                        {
                            sourceCount += buildOperandUses(op2, op2Candidates);
                        }
                    }
                    else
                    {
                        sourceCount += buildOperandUses(op2, op2Candidates);
                    }

                    if (operandCount >= 3)
                    {
                        var op3Candidates = isEvexCompatible ? SRBM_NONE : buildEvexIncompatibleMask(op3);
                        if (!isEvexCompatible || !_evexIsSupported)
                        {
                            op3Candidates = forceLowGprForApx(op3, op3Candidates);
                        }
                        if (op3.Oper.IsHWIntrinsic && op3.AsHWIntrinsic().IsMemoryLoad() && op3.IsContained)
                        {
                            sourceCount += buildAddrUses(op3.AsHWIntrinsic().GetOp(1), op3Candidates);
                        }
                        else if (isRmw && !op3.IsContained)
                        {
                            sourceCount += buildDelayFreeUses(op3, op1, op3Candidates);
                        }
                        else
                        {
                            sourceCount += buildOperandUses(op3, op3Candidates);
                        }

                        if (operandCount >= 4)
                        {
                            assert(isEvexCompatible);
                            sourceCount += isRmw
                                ? buildDelayFreeUses(op4, op1, SRBM_NONE)
                                : buildOperandUses(op4);
                        }
                    }
                }
            }

            buildInternalRegisterUses();
        }

        if (destinationCount == 1)
        {
            var isEvexCompatible = intrinsic.IsEvexCompatibleHWIntrinsic(_compiler);
            if (!isEvexCompatible)
            {
                destinationCandidates = buildEvexIncompatibleMask(intrinsic);
            }
            if (!isEvexCompatible || !_evexIsSupported)
            {
                destinationCandidates = forceLowGprForApx(intrinsic, destinationCandidates);
            }
            _ = buildDef(intrinsic, destinationCandidates);
        }
        else
        {
            assert(destinationCount == 0 || (destinationCount == 2 && intrinsicId is
                NI_X86Base_DivRem or NI_X86Base_X64_DivRem or NI_X86Base_X64_BigMul));
        }
        return sourceCount;
    }
#endif
}
