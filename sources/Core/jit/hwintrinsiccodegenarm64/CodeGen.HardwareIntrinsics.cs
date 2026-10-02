// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
using static RyuJitSharp.insScalableOpts;
using static RyuJitSharp.insCond;
using static RyuJitSharp.insSvePattern;
using Arm64Emitter = RyuJitSharp.Emitter;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genHWIntrinsic(GenTreeHWIntrinsic node)
    {
        var intrin = new Arm64HWIntrinsic(node);
        var isa = HWIntrinsicInfo.lookupIsa(intrin.Id);
        if (isa == InstructionSet_Vector)
        {
            if (node.SimdSize == 8)
            {
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_Vector64));
            }
            else
            {
                assert(node.SimdSize is 12 or 16);
                assert(_compiler.compIsaSupportedDebugOnly(InstructionSet_Vector128));
            }
        }
        else
        {
            assert(_compiler.compIsaSupportedDebugOnly(isa));
        }

        var targetReg = node.RegNum;
        var op1Reg = REG_NA;
        var op2Reg = REG_NA;
        var op3Reg = REG_NA;
        var op4Reg = REG_NA;
        var op5Reg = REG_NA;
        switch (intrin.NumOperands)
        {
            case 5:
            {
                op5Reg = intrin.Op5.RegNum;
                goto case 4;
            }
            case 4:
            {
                op4Reg = intrin.Op4.RegNum;
                goto case 3;
            }
            case 3:
            {
                op3Reg = intrin.Op3.RegNum;
                goto case 2;
            }
            case 2:
            {
                op2Reg = intrin.Op2.RegNum;
                goto case 1;
            }
            case 1:
            {
                op1Reg = intrin.Op1.RegNum;
                break;
            }
            case 0:
            {
                break;
            }
            default:
            {
                unreached();
                break;
            }
        }

        emitAttr emitSize;
        insOpts opt;
        if ((HWIntrinsicInfo.lookupFlags(intrin.Id) & HW_Flag_SIMDScalar) != 0)
        {
            emitSize = emitTypeSize(intrin.BaseType);
            opt = INS_OPTS_NONE;
        }
        else if (intrin.Category == HW_Category_Scalar)
        {
            emitSize = emitActualTypeSize(intrin.BaseType);
            opt = INS_OPTS_NONE;
        }
        else if (HWIntrinsicInfo.IsScalable(intrin.Id))
        {
            emitSize = EA_SCALABLE;
            opt = Arm64Emitter.optGetSveInsOpt(emitTypeSize(intrin.BaseType));
        }
        else if (intrin.Category == HW_Category_Special)
        {
            assert(intrin.Id == NI_ArmBase_Yield);
            emitSize = EA_UNKNOWN;
            opt = INS_OPTS_NONE;
        }
        else
        {
            emitSize = emitActualTypeSize(Compiler.GetSimdTypeForSize(node.SimdSize));
            opt = genGetSimdInsOpt(emitSize, intrin.BaseType);
        }

        var isRMW = node.IsRmwHWIntrinsic(_compiler);
        var hasImmediateOperand = HWIntrinsicInfo.HasImmediateOperand(intrin.Id);
        genConsumeMultiOpOperands(node);
#if DEBUG
        if (isRMW && !HWIntrinsicInfo.IsOptionalEmbeddedMaskedOperation(intrin.Id))
        {
            checkRMWRegisters(intrin, targetReg);
        }
#endif
        if (intrin.CodeGenIsTableDriven)
        {
            var ins = HWIntrinsicInfo.lookupIns(intrin.Id, intrin.BaseType, _compiler);
            assert(ins != INS_invalid);
            if (intrin.Category == HW_Category_SIMDByIndexedElement)
            {
                if (hasImmediateOperand)
                {
                    switch (intrin.NumOperands)
                    {
                        case 2:
                        {
                            assert(!isRMW);
                            var helper = new HWIntrinsicImmOpHelper(this, intrin.Op2, node);
                            for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                            {
                                Emitter.emitIns_R_R_I(ins, emitSize, targetReg, op1Reg, helper.ImmValue, opt);
                            }
                            break;
                        }
                        case 3:
                        {
                            assert(!isRMW);
                            var helper = new HWIntrinsicImmOpHelper(this, intrin.Op3, node);
                            for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                            {
                                Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, helper.ImmValue, opt);
                            }
                            break;
                        }
                        case 4:
                        {
                            assert(isRMW);
                            var numInstrs = targetReg != op1Reg ? 2 : 1;
                            var helper = new HWIntrinsicImmOpHelper(this, intrin.Op4, node, numInstrs);
                            for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                            {
                                Emitter.emitIns_R_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg,
                                    helper.ImmValue, opt);
                            }
                            break;
                        }
                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                }
                else if (isRMW)
                {
                    Emitter.emitIns_R_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg, 0, opt);
                }
                else
                {
                    Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, 0, opt);
                }
            }
            else if (intrin.Category is HW_Category_ShiftLeftByImmediate or HW_Category_ShiftRightByImmediate)
            {
                assert(hasImmediateOperand);
                var shiftOp = isRMW ? intrin.Op3 : intrin.Op2;
                var numInstrs = isRMW && targetReg != op1Reg ? 2 : 1;
                var helper = new HWIntrinsicImmOpHelper(this, shiftOp, node, numInstrs);
                for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                {
                    var shiftAmount = helper.ImmValue;
                    assert(shiftAmount != 0 || intrin.Category == HW_Category_ShiftLeftByImmediate);
                    if (isRMW)
                    {
                        assert(intrin.NumOperands == 3);
                        Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, shiftAmount, opt);
                    }
                    else
                    {
                        assert(intrin.NumOperands == 2);
                        Emitter.emitIns_R_R_I(ins, emitSize, targetReg, op1Reg, shiftAmount, opt);
                    }
                }
            }
            else if (HWIntrinsicInfo.IsSveConditionalSelect(intrin.Id) &&
                intrin.Op2.Oper.IsHWIntrinsic && (intrin.Op2.Flags & GTF_HW_EM_OP) != 0)
            {
                genEmbeddedMaskedHWIntrinsic(node, targetReg);
            }
            else
            {
                switch (intrin.NumOperands)
                {
                    case 0:
                    {
                        assert(!hasImmediateOperand);
                        Emitter.emitIns_R(ins, emitSize, targetReg, opt);
                        break;
                    }
                    case 1:
                    {
                        if (hasImmediateOperand)
                        {
                            assert(HWIntrinsicInfo.IsScalable(intrin.Id));
                            var helper = new HWIntrinsicImmOpHelper(this, intrin.Op1, node);
                            for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                            {
                                var pattern = (insSvePattern)helper.ImmValue;
                                Emitter.emitIns_R_PATTERN(ins, emitSize, targetReg, opt, pattern);
                            }
                        }
                        else if (HWIntrinsicInfo.IsEmbeddedMaskedOperation(intrin.Id) && intrin.Op1.IsContained)
                        {
                            assert(intrin.Op1.Oper.IsHWIntrinsic);
                            var cselIntrin = new Arm64HWIntrinsic(intrin.Op1.AsHWIntrinsic());
                            assert(cselIntrin.Id == NI_Sve_ConditionalSelect);
                            var maskReg = cselIntrin.Op1.RegNum;
                            op1Reg = cselIntrin.Op2.RegNum;
                            Emitter.emitIns_R_R_R(ins, emitSize, targetReg, maskReg, op1Reg, opt);
                        }
                        else
                        {
                            Emitter.emitIns_R_R(ins, emitSize, targetReg, op1Reg, opt);
                        }
                        break;
                    }
                    case 2:
                    {
                        assert(!hasImmediateOperand);
                        if (HWIntrinsicInfo.SupportsContainment(intrin.Id) &&
                            intrin.Op2.IsContained && intrin.Op2.IsZeroForSelect)
                        {
                            Emitter.emitIns_R_R(ins, emitSize, targetReg, op1Reg, opt);
                        }
                        else
                        {
                            Emitter.emitIns_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                        }
                        break;
                    }
                    case 3:
                    {
                        if (hasImmediateOperand)
                        {
                            assert(!isRMW);
                            var helper = new HWIntrinsicImmOpHelper(this, intrin.Op3, node);
                            for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                            {
                                Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, helper.ImmValue, opt);
                            }
                        }
                        else
                        {
                            Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg, opt);
                        }
                        break;
                    }
                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }
        }
        else
        {
            var ins = INS_invalid;
            switch (intrin.Id)
            {
                case NI_AdvSimd_AddWideningLower:
                {
                    assert(varTypeIsIntegral(intrin.BaseType));
                    if (intrin.Op1.Type == TYP_SIMD8)
                    {
                        ins = varTypeIsUnsigned(intrin.BaseType) ? INS_uaddl : INS_saddl;
                    }
                    else
                    {
                        assert(intrin.Op1.Type == TYP_SIMD16);
                        ins = varTypeIsUnsigned(intrin.BaseType) ? INS_uaddw : INS_saddw;
                    }
                    break;
                }
                case NI_AdvSimd_SubtractWideningLower:
                {
                    assert(varTypeIsIntegral(intrin.BaseType));
                    if (intrin.Op1.Type == TYP_SIMD8)
                    {
                        ins = varTypeIsUnsigned(intrin.BaseType) ? INS_usubl : INS_ssubl;
                    }
                    else
                    {
                        assert(intrin.Op1.Type == TYP_SIMD16);
                        ins = varTypeIsUnsigned(intrin.BaseType) ? INS_usubw : INS_ssubw;
                    }
                    break;
                }
                case NI_AdvSimd_AddWideningUpper:
                {
                    assert(varTypeIsIntegral(intrin.BaseType));
                    ins = node.AuxiliaryType == intrin.BaseType
                        ? (varTypeIsUnsigned(intrin.BaseType) ? INS_uaddl2 : INS_saddl2)
                        : (varTypeIsUnsigned(intrin.BaseType) ? INS_uaddw2 : INS_saddw2);
                    break;
                }
                case NI_AdvSimd_SubtractWideningUpper:
                {
                    assert(varTypeIsIntegral(intrin.BaseType));
                    ins = node.AuxiliaryType == intrin.BaseType
                        ? (varTypeIsUnsigned(intrin.BaseType) ? INS_usubl2 : INS_ssubl2)
                        : (varTypeIsUnsigned(intrin.BaseType) ? INS_usubw2 : INS_ssubw2);
                    break;
                }
                case NI_ArmBase_Yield:
                {
                    ins = INS_yield;
                    break;
                }
                case NI_ArmBase_Arm64_MultiplyLongAdd:
                {
                    ins = varTypeIsUnsigned(intrin.BaseType) ? INS_umaddl : INS_smaddl;
                    break;
                }
                case NI_ArmBase_Arm64_MultiplyLongSub:
                {
                    ins = varTypeIsUnsigned(intrin.BaseType) ? INS_umsubl : INS_smsubl;
                    break;
                }
                case NI_Sve_StoreNarrowing:
                {
                    ins = HWIntrinsicInfo.lookupIns(intrin.Id, node.AuxiliaryType, _compiler);
                    break;
                }
                default:
                {
                    ins = HWIntrinsicInfo.lookupIns(intrin.Id, intrin.BaseType, _compiler);
                    break;
                }
            }
            assert(ins != INS_invalid);

            switch (intrin.Id)
            {
                case NI_AdvSimd_BitwiseSelect:
                {
                    assert(!isRMW);
                    if (targetReg == op1Reg)
                    {
                        Emitter.emitIns_R_R_R(INS_bsl, emitSize, targetReg, op2Reg, op3Reg, opt);
                    }
                    else if (targetReg == op2Reg)
                    {
                        Emitter.emitIns_R_R_R(INS_bif, emitSize, targetReg, op3Reg, op1Reg, opt);
                    }
                    else if (targetReg == op3Reg)
                    {
                        Emitter.emitIns_R_R_R(INS_bit, emitSize, targetReg, op2Reg, op1Reg, opt);
                    }
                    else
                    {
                        Emitter.emitIns_Mov(INS_mov, emitSize, targetReg, op1Reg, false);
                        Emitter.emitIns_R_R_R(INS_bsl, emitSize, targetReg, op2Reg, op3Reg, opt);
                    }
                    break;
                }
                case NI_ArmBase_ConvertToSingle:
                {
                    Emitter.emitIns_R_R(ins, EA_4BYTE, targetReg, op1Reg, INS_OPTS_H_TO_S);
                    break;
                }
                case NI_ArmBase_ConvertToDouble:
                {
                    Emitter.emitIns_R_R(ins, EA_8BYTE, targetReg, op1Reg, INS_OPTS_H_TO_D);
                    break;
                }
                case NI_ArmBase_ConvertToHalf:
                {
                    assert(intrin.BaseType is TYP_FLOAT or TYP_DOUBLE);
                    var cvtOption = intrin.BaseType == TYP_FLOAT ? INS_OPTS_S_TO_H : INS_OPTS_D_TO_H;
                    Emitter.emitIns_R_R(ins, EA_2BYTE, targetReg, op1Reg, cvtOption);
                    break;
                }
                case NI_Fp16_ConvertToInt32:
                case NI_Fp16_ConvertToUInt32:
                {
                    Emitter.emitIns_R_R(ins, EA_4BYTE, targetReg, op1Reg, INS_OPTS_H_TO_4BYTE);
                    break;
                }
                case NI_Fp16_ConvertToInt64:
                case NI_Fp16_ConvertToUInt64:
                {
                    Emitter.emitIns_R_R(ins, EA_8BYTE, targetReg, op1Reg, INS_OPTS_H_TO_8BYTE);
                    break;
                }
                case NI_Fp16_ConvertToHalf:
                {
                    var cvtOption = INS_OPTS_NONE;
                    switch (intrin.BaseType)
                    {
                        case TYP_INT:
                        case TYP_UINT:
                        {
                            cvtOption = INS_OPTS_4BYTE_TO_H;
                            break;
                        }
                        case TYP_LONG:
                        case TYP_ULONG:
                        {
                            cvtOption = INS_OPTS_8BYTE_TO_H;
                            break;
                        }
                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                    Emitter.emitIns_R_R(ins, EA_2BYTE, targetReg, op1Reg, cvtOption);
                    break;
                }
                case NI_Fp16_CompareEqual:
                case NI_Fp16_CompareGreaterThan:
                case NI_Fp16_CompareGreaterThanOrEqual:
                case NI_Fp16_CompareLessThan:
                case NI_Fp16_CompareLessThanOrEqual:
                case NI_Fp16_CompareNotEqual:
                {
                    var cond = INS_COND_EQ;
                    switch (intrin.Id)
                    {
                        case NI_Fp16_CompareEqual:
                        {
                            cond = INS_COND_EQ;
                            break;
                        }
                        case NI_Fp16_CompareGreaterThan:
                        {
                            cond = INS_COND_GT;
                            break;
                        }
                        case NI_Fp16_CompareGreaterThanOrEqual:
                        {
                            cond = INS_COND_GE;
                            break;
                        }
                        case NI_Fp16_CompareLessThan:
                        {
                            // MI, not LT, makes an unordered comparison false.
                            cond = INS_COND_MI;
                            break;
                        }
                        case NI_Fp16_CompareLessThanOrEqual:
                        {
                            cond = INS_COND_LS;
                            break;
                        }
                        case NI_Fp16_CompareNotEqual:
                        {
                            cond = INS_COND_NE;
                            break;
                        }
                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                    Emitter.emitIns_R_R(INS_fcmp, EA_2BYTE, op1Reg, op2Reg);
                    Emitter.emitIns_R_COND(INS_cset, EA_4BYTE, targetReg, cond);
                    break;
                }
                case NI_Crc32_ComputeCrc32:
                case NI_Crc32_ComputeCrc32C:
                case NI_Crc32_Arm64_ComputeCrc32:
                case NI_Crc32_Arm64_ComputeCrc32C:
                {
                    Emitter.emitIns_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_AdvSimd_CompareLessThan:
                case NI_AdvSimd_CompareLessThanOrEqual:
                case NI_AdvSimd_Arm64_CompareLessThan:
                case NI_AdvSimd_Arm64_CompareLessThanScalar:
                case NI_AdvSimd_Arm64_CompareLessThanOrEqual:
                case NI_AdvSimd_Arm64_CompareLessThanOrEqualScalar:
                {
                    if (intrin.Op2.IsContained)
                    {
                        assert(intrin.Op2.IsVectorZero);
                        var zeroIns = INS_invalid;
                        switch (ins)
                        {
                            case INS_cmgt:
                            {
                                zeroIns = INS_cmlt;
                                break;
                            }
                            case INS_cmge:
                            {
                                zeroIns = INS_cmle;
                                break;
                            }
                            case INS_fcmgt:
                            {
                                zeroIns = INS_fcmlt;
                                break;
                            }
                            case INS_fcmge:
                            {
                                zeroIns = INS_fcmle;
                                break;
                            }
                            default:
                            {
                                unreached();
                                break;
                            }
                        }
                        Emitter.emitIns_R_R(zeroIns, emitSize, targetReg, op1Reg, opt);
                    }
                    else
                    {
                        Emitter.emitIns_R_R_R(ins, emitSize, targetReg, op2Reg, op1Reg, opt);
                    }
                    break;
                }
                case NI_AdvSimd_AbsoluteCompareLessThan:
                case NI_AdvSimd_AbsoluteCompareLessThanOrEqual:
                case NI_AdvSimd_Arm64_AbsoluteCompareLessThan:
                case NI_AdvSimd_Arm64_AbsoluteCompareLessThanScalar:
                case NI_AdvSimd_Arm64_AbsoluteCompareLessThanOrEqual:
                case NI_AdvSimd_Arm64_AbsoluteCompareLessThanOrEqualScalar:
                {
                    Emitter.emitIns_R_R_R(ins, emitSize, targetReg, op2Reg, op1Reg, opt);
                    break;
                }
                case NI_AdvSimd_FusedMultiplyAddScalar:
                case NI_AdvSimd_FusedMultiplyAddNegatedScalar:
                case NI_AdvSimd_FusedMultiplySubtractNegatedScalar:
                case NI_AdvSimd_FusedMultiplySubtractScalar:
                {
                    assert(opt == INS_OPTS_NONE);
                    Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op2Reg, op3Reg, op1Reg);
                    break;
                }
                case NI_AdvSimd_DuplicateSelectedScalarToVector64:
                case NI_AdvSimd_DuplicateSelectedScalarToVector128:
                case NI_AdvSimd_Arm64_DuplicateSelectedScalarToVector128:
                {
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op2, node);
                    assert(Arm64Emitter.isValidVectorIndex(emitSize, Arm64Emitter.optGetElemsize(opt), helper.ImmValue));
                    emitSize = emitActualTypeSize(node.Type);
                    opt = genGetSimdInsOpt(emitSize, intrin.BaseType);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        assert(opt != INS_OPTS_NONE);
                        Emitter.emitIns_R_R_I(ins, emitSize, targetReg, op1Reg, helper.ImmValue, opt);
                    }
                    break;
                }
                case NI_AdvSimd_Extract:
                {
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op2, node);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        Emitter.emitIns_R_R_I(ins, emitTypeSize(intrin.BaseType), targetReg, op1Reg,
                            helper.ImmValue, INS_OPTS_NONE);
                    }
                    break;
                }
                case NI_AdvSimd_ExtractVector64:
                case NI_AdvSimd_ExtractVector128:
                {
                    opt = intrin.Id == NI_AdvSimd_ExtractVector64 ? INS_OPTS_8B : INS_OPTS_16B;
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op3, node);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        var byteIndex = unchecked(intrin.BaseType.Size * helper.ImmValue);
                        Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, byteIndex, opt);
                    }
                    break;
                }
                case NI_AdvSimd_Insert:
                {
                    assert(isRMW);
                    Emitter.emitIns_Mov(INS_mov, emitTypeSize(node), targetReg, op1Reg, true);
                    assert(!intrin.Op3.IsContainedFltOrDblImmed);
                    assert(targetReg != op3Reg || targetReg == op1Reg);
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op2, node);
                    if (varTypeIsFloating(intrin.BaseType))
                    {
                        for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                        {
                            Emitter.emitIns_R_R_I_I(ins, emitSize, targetReg, op3Reg, helper.ImmValue, 0, opt);
                        }
                    }
                    else
                    {
                        for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                        {
                            Emitter.emitIns_R_R_I(ins, emitSize, targetReg, op3Reg, helper.ImmValue, opt);
                        }
                    }
                    break;
                }
                case NI_AdvSimd_InsertScalar:
                {
                    assert(isRMW);
                    Emitter.emitIns_Mov(INS_mov, emitTypeSize(node), targetReg, op1Reg, true);
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op2, node);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        Emitter.emitIns_R_R_I_I(ins, emitSize, targetReg, op3Reg, helper.ImmValue, 0, opt);
                    }
                    break;
                }
                case NI_AdvSimd_Arm64_InsertSelectedScalar:
                {
                    assert(isRMW);
                    Emitter.emitIns_Mov(INS_mov, emitTypeSize(node), targetReg, op1Reg, true);
                    var resultIndex = unchecked((int)intrin.Op2.AsIntCon().IconValue);
                    var valueIndex = unchecked((int)intrin.Op4.AsIntCon().IconValue);
                    Emitter.emitIns_R_R_I_I(ins, emitSize, targetReg, op3Reg, resultIndex, valueIndex, opt);
                    break;
                }
                case NI_AdvSimd_LoadAndInsertScalar:
                {
                    assert(isRMW);
                    Emitter.emitIns_Mov(INS_mov, emitTypeSize(node), targetReg, op1Reg, true);
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op2, node);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        Emitter.emitIns_R_R_I(ins, emitSize, targetReg, op3Reg, helper.ImmValue);
                    }
                    break;
                }
                case NI_AdvSimd_LoadAndInsertScalarVector64x2:
                case NI_AdvSimd_LoadAndInsertScalarVector64x3:
                case NI_AdvSimd_LoadAndInsertScalarVector64x4:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
                case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
                {
                    assert(isRMW);
                    byte fieldIdx = 0;
                    op2Reg = intrin.Op2.RegNum;
                    op3Reg = intrin.Op3.RegNum;
                    assert(intrin.Op1.Oper.IsFieldList);
                    var fieldList = intrin.Op1.AsFieldList();
                    var firstField = fieldList.Uses.Head;
                    assert(firstField is not null);
                    op1Reg = firstField.Node.RegNum;
                    foreach (var use in fieldList.Uses)
                    {
                        var fieldNode = use.Node;
                        var targetFieldReg = node.GetRegByIndex(fieldIdx);
                        var op1FieldReg = fieldNode.RegNum;
                        Emitter.emitIns_Mov(INS_mov, emitTypeSize(fieldNode), targetFieldReg, op1FieldReg, true);
                        fieldIdx++;
                    }
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op2, node);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        Emitter.emitIns_R_R_I(ins, emitSize, targetReg, op3Reg, helper.ImmValue);
                    }
                    break;
                }
                case NI_AdvSimd_Arm64_LoadPairVector128:
                case NI_AdvSimd_Arm64_LoadPairVector128NonTemporal:
                case NI_AdvSimd_Arm64_LoadPairVector64:
                case NI_AdvSimd_Arm64_LoadPairVector64NonTemporal:
                {
                    Emitter.emitIns_R_R_R(ins, emitSize, targetReg, node.GetRegByIndex(1), op1Reg);
                    break;
                }
                case NI_AdvSimd_Arm64_LoadPairScalarVector64:
                case NI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal:
                {
                    Emitter.emitIns_R_R_R(ins, emitTypeSize(intrin.BaseType), targetReg, node.GetRegByIndex(1), op1Reg);
                    break;
                }
                case NI_AdvSimd_Arm64_StorePair:
                case NI_AdvSimd_Arm64_StorePairNonTemporal:
                {
                    Emitter.emitIns_R_R_R(ins, emitSize, op2Reg, op3Reg, op1Reg);
                    break;
                }
                case NI_AdvSimd_Arm64_StorePairScalar:
                case NI_AdvSimd_Arm64_StorePairScalarNonTemporal:
                {
                    Emitter.emitIns_R_R_R(ins, emitTypeSize(intrin.BaseType), op2Reg, op3Reg, op1Reg);
                    break;
                }
                case NI_AdvSimd_StoreSelectedScalar:
                case NI_AdvSimd_Arm64_StoreSelectedScalar:
                {
                    var regCount = intrin.Op2.Oper.IsFieldList
                        ? GetArm64IntrinsicRegisterList(intrin.Op2, out op2Reg) : 1u;
                    switch (regCount)
                    {
                        case 2:
                        {
                            ins = INS_st2;
                            break;
                        }
                        case 3:
                        {
                            ins = INS_st3;
                            break;
                        }
                        case 4:
                        {
                            ins = INS_st4;
                            break;
                        }
                        default:
                        {
                            assert(regCount == 1);
                            ins = INS_st1;
                            break;
                        }
                    }
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op3, node);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        Emitter.emitIns_R_R_I(ins, emitSize, op2Reg, op1Reg, helper.ImmValue, opt);
                    }
                    break;
                }
                case NI_AdvSimd_Store:
                case NI_AdvSimd_Arm64_Store:
                case NI_AdvSimd_StoreVectorAndZip:
                case NI_AdvSimd_Arm64_StoreVectorAndZip:
                {
                    assert(intrin.Op2.Oper.IsFieldList);
                    var regCount = GetArm64IntrinsicRegisterList(intrin.Op2, out op2Reg);
                    var isSequentialStore = intrin.Id is NI_AdvSimd_Arm64_Store or NI_AdvSimd_Store;
                    switch (regCount)
                    {
                        case 2:
                        {
                            ins = isSequentialStore ? INS_st1_2regs : INS_st2;
                            break;
                        }
                        case 3:
                        {
                            ins = isSequentialStore ? INS_st1_3regs : INS_st3;
                            break;
                        }
                        case 4:
                        {
                            ins = isSequentialStore ? INS_st1_4regs : INS_st4;
                            break;
                        }
                        default:
                        {
                            unreached();
                            break;
                        }
                    }
                    Emitter.emitIns_R_R(ins, emitSize, op2Reg, op1Reg, opt);
                    break;
                }
                case NI_Vector_CreateScalarUnsafe:
                {
                    var simdType = Compiler.GetSimdTypeForSize(node.SimdSize);
                    if (simdType == TYP_SIMD)
                    {
                        emitSize = opt == INS_OPTS_SCALABLE_D ? EA_8BYTE : EA_4BYTE;
                        if (varTypeIsFloating(intrin.BaseType))
                        {
                            var tmpReg = _internalRegisters.Extract(node, new regMaskTP(SRBM_ALLINT));
                            var fmovOpt = emitSize == EA_8BYTE ? INS_OPTS_D_TO_8BYTE : INS_OPTS_S_TO_4BYTE;
                            Emitter.emitIns_Mov(INS_fmov, emitSize, tmpReg, op1Reg, false, fmovOpt);
                            op1Reg = tmpReg;
                        }
                        Emitter.emitInsSve_R_R(ins, emitSize, targetReg, op1Reg, opt);
                    }
                    else if (intrin.Op1.IsContainedFltOrDblImmed)
                    {
                        var dataValue = intrin.Op1.AsDblCon().DconVal;
                        Emitter.emitIns_R_F(ins, emitTypeSize(intrin.BaseType), targetReg, dataValue, INS_OPTS_NONE);
                    }
                    else if (varTypeIsFloating(intrin.BaseType))
                    {
                        assert(Arm64Emitter.IsMovInstruction(ins));
                        assert(intrin.BaseType == intrin.Op1.Type);
                        Emitter.emitIns_Mov(ins, emitTypeSize(intrin.BaseType), targetReg, op1Reg, true, INS_OPTS_NONE);
                    }
                    else if (intrin.Op1.IsContainedIntOrIImmed)
                    {
                        var dataValue = intrin.Op1.AsIntCon().IconValue;
                        Emitter.emitIns_R_I(INS_movi, emitSize, targetReg, dataValue, opt);
                    }
                    else
                    {
                        Emitter.emitIns_R_R_I(ins, emitTypeSize(intrin.BaseType), targetReg, op1Reg, 0, INS_OPTS_NONE);
                    }
                    break;
                }
                case NI_AdvSimd_AddWideningLower:
                case NI_AdvSimd_AddWideningUpper:
                case NI_AdvSimd_SubtractWideningLower:
                case NI_AdvSimd_SubtractWideningUpper:
                {
                    Emitter.emitIns_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_AdvSimd_Arm64_AddSaturateScalar:
                {
                    if (varTypeIsUnsigned(node.AuxiliaryType) != varTypeIsUnsigned(intrin.BaseType))
                    {
                        ins = varTypeIsUnsigned(intrin.BaseType) ? INS_usqadd : INS_suqadd;
                    }
                    Emitter.emitIns_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_ArmBase_Yield:
                {
                    Emitter.emitIns(ins);
                    break;
                }
                case NI_AdvSimd_DuplicateToVector64:
                case NI_AdvSimd_DuplicateToVector128:
                case NI_AdvSimd_Arm64_DuplicateToVector64:
                case NI_AdvSimd_Arm64_DuplicateToVector128:
                {
                    if (varTypeIsFloating(intrin.BaseType))
                    {
                        if (intrin.Op1.IsContainedFltOrDblImmed)
                        {
                            var dataValue = intrin.Op1.AsDblCon().DconVal;
                            Emitter.emitIns_R_F(INS_fmov, emitSize, targetReg, dataValue, opt);
                        }
                        else if (intrin.Id == NI_AdvSimd_Arm64_DuplicateToVector64)
                        {
                            assert(intrin.BaseType == TYP_DOUBLE);
                            assert(Arm64Emitter.IsMovInstruction(ins));
                            assert(intrin.BaseType == intrin.Op1.Type);
                            Emitter.emitIns_Mov(ins, emitSize, targetReg, op1Reg, true, opt);
                        }
                        else
                        {
                            Emitter.emitIns_R_R_I(ins, emitSize, targetReg, op1Reg, 0, opt);
                        }
                    }
                    else if (intrin.Op1.IsContainedIntOrIImmed)
                    {
                        Emitter.emitIns_R_I(INS_movi, emitSize, targetReg, intrin.Op1.AsIntCon().IconValue, opt);
                    }
                    else if (Arm64Emitter.IsMovInstruction(ins))
                    {
                        Emitter.emitIns_Mov(ins, emitSize, targetReg, op1Reg, false, opt);
                    }
                    else
                    {
                        Emitter.emitIns_R_R(ins, emitSize, targetReg, op1Reg, opt);
                    }
                    break;
                }
                case NI_Sve_Load2xVectorAndUnzip:
                case NI_Sve_Load3xVectorAndUnzip:
                case NI_Sve_Load4xVectorAndUnzip:
                {
#if DEBUG
                    assert(node.GetMultiRegCount(_compiler) == (uint)Arm64IntrinsicSveReg1ListSize(ins));
                    var argReg = targetReg;
                    for (byte i = 0; i < node.GetMultiRegCount(_compiler); i++)
                    {
                        assert(argReg == node.GetRegNumByIdx(i));
                        argReg = getNextSIMDRegWithWraparound(argReg);
                    }
#endif
                    Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, 0, opt);
                    break;
                }
                case NI_Sve2_VectorTableLookup:
                {
                    assert(intrin.Op1.Oper.IsFieldList);
                    var firstField = intrin.Op1.AsFieldList().Uses.Head;
                    assert(firstField is not null);
                    op1Reg = firstField.Node.RegNum;
#if DEBUG
                    var regCount = GetArm64IntrinsicRegisterList(intrin.Op1, out op1Reg);
                    assert(regCount == 2);
#endif
                    Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt,
                        INS_SCALABLE_OPTS_WITH_VECTOR_PAIR);
                    break;
                }
                case NI_Sve_StoreAndZipx2:
                case NI_Sve_StoreAndZipx3:
                case NI_Sve_StoreAndZipx4:
                {
                    assert(intrin.Op3.Oper.IsFieldList);
                    var firstField = intrin.Op3.AsFieldList().Uses.Head;
                    assert(firstField is not null);
                    op3Reg = firstField.Node.RegNum;
#if DEBUG
                    var regCount = GetArm64IntrinsicRegisterList(intrin.Op3, out op3Reg);
                    switch (ins)
                    {
                        case INS_sve_st2b:
                        case INS_sve_st2d:
                        case INS_sve_st2h:
                        case INS_sve_st2w:
                        case INS_sve_st2q:
                        {
                            assert(regCount == 2);
                            break;
                        }
                        case INS_sve_st3b:
                        case INS_sve_st3d:
                        case INS_sve_st3h:
                        case INS_sve_st3w:
                        case INS_sve_st3q:
                        {
                            assert(regCount == 3);
                            break;
                        }
                        case INS_sve_st4b:
                        case INS_sve_st4d:
                        case INS_sve_st4h:
                        case INS_sve_st4w:
                        case INS_sve_st4q:
                        {
                            assert(regCount == 4);
                            break;
                        }
                        default:
                        {
                            unreached();
                            break;
                        }
                    }
#endif
                    Emitter.emitIns_R_R_R_I(ins, emitSize, op3Reg, op1Reg, op2Reg, 0, opt);
                    break;
                }
                case NI_Sve_StoreAndZip:
                case NI_Sve_StoreNonTemporal:
                {
                    Emitter.emitIns_R_R_R_I(ins, emitSize, op3Reg, op1Reg, op2Reg, 0, opt);
                    break;
                }
                case NI_Sve_Prefetch16Bit:
                case NI_Sve_Prefetch32Bit:
                case NI_Sve_Prefetch64Bit:
                case NI_Sve_Prefetch8Bit:
                {
                    assert(hasImmediateOperand);
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op3, node);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        var prfop = (insSvePrfop)helper.ImmValue;
                        Arm64IntrinsicEmitPrefetchTwoRegistersImmediate(ins, emitSize, prfop, op1Reg, op2Reg, 0);
                    }
                    break;
                }
                case NI_Vector_ToVector128:
                {
                    Emitter.emitIns_Mov(ins, emitSize, targetReg, op1Reg, false);
                    break;
                }
                case NI_Vector_ToVector128Unsafe:
                case NI_Vector_AsVector128Unsafe:
                case NI_Vector_GetLower:
                {
                    Emitter.emitIns_Mov(ins, emitSize, targetReg, op1Reg, true);
                    break;
                }
                case NI_Vector_GetElement:
                {
                    assert(intrin.NumOperands == 2);
                    assert(!intrin.Op1.IsContained);
                    assert(intrin.Op2.Oper.IsConst);
                    assert(intrin.Op2.IsContained);
                    var simdType = Compiler.GetSimdTypeForSize(node.SimdSize);
                    if (simdType == TYP_SIMD12)
                    {
                        simdType = TYP_SIMD16;
                    }
                    var ival = intrin.Op2.AsIntCon().IconValue;
                    if (!Arm64Emitter.isValidVectorIndex(emitTypeSize(simdType), emitTypeSize(intrin.BaseType), ival))
                    {
                        // The preceding range check throws for an invalid index.
                        break;
                    }
                    if (varTypeIsFloating(intrin.BaseType) && targetReg == op1Reg && ival == 0)
                    {
                        break;
                    }
                    Emitter.emitIns_R_R_I(ins, emitTypeSize(intrin.BaseType), targetReg, op1Reg, ival, INS_OPTS_NONE);
                    break;
                }
                case NI_Vector_GetUpper:
                {
                    const int byteIndex = 8;
                    Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op1Reg, byteIndex, INS_OPTS_16B);
                    break;
                }
                case NI_Vector_AsVector3:
                {
                    Emitter.emitIns_Mov(ins, emitSize, targetReg, op1Reg, true);
                    break;
                }
                case NI_Vector_ToScalar:
                {
                    if (varTypeIsFloating(intrin.BaseType) && targetReg == op1Reg)
                    {
                        break;
                    }
                    if (varTypeIsLong(intrin.BaseType))
                    {
                        Emitter.emitIns_Mov(INS_fmov, EA_8BYTE, targetReg, op1Reg, false);
                    }
                    else
                    {
                        Emitter.emitIns_R_R_I(ins, emitTypeSize(intrin.BaseType), targetReg, op1Reg, 0, INS_OPTS_NONE);
                    }
                    break;
                }
                case NI_AdvSimd_ReverseElement16:
                {
                    Emitter.emitIns_R_R(ins, emitSize, targetReg, op1Reg,
                        emitSize == EA_8BYTE ? INS_OPTS_4H : INS_OPTS_8H);
                    break;
                }
                case NI_AdvSimd_ReverseElement32:
                {
                    Emitter.emitIns_R_R(ins, emitSize, targetReg, op1Reg,
                        emitSize == EA_8BYTE ? INS_OPTS_2S : INS_OPTS_4S);
                    break;
                }
                case NI_AdvSimd_ReverseElement8:
                {
                    Emitter.emitIns_R_R(ins, emitSize, targetReg, op1Reg,
                        emitSize == EA_8BYTE ? INS_OPTS_8B : INS_OPTS_16B);
                    break;
                }
                case NI_AdvSimd_VectorTableLookup:
                case NI_AdvSimd_Arm64_VectorTableLookup:
                {
                    var regCount = intrin.Op1.Oper.IsFieldList
                        ? GetArm64IntrinsicRegisterList(intrin.Op1, out op1Reg) : 1u;
                    if (!intrin.Op1.Oper.IsFieldList)
                    {
                        op1Reg = intrin.Op1.RegNum;
                    }
                    switch (regCount)
                    {
                        case 2:
                        {
                            ins = INS_tbl_2regs;
                            break;
                        }
                        case 3:
                        {
                            ins = INS_tbl_3regs;
                            break;
                        }
                        case 4:
                        {
                            ins = INS_tbl_4regs;
                            break;
                        }
                        default:
                        {
                            assert(regCount == 1);
                            assert(ins == INS_tbl);
                            break;
                        }
                    }
                    Emitter.emitIns_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_AdvSimd_VectorTableLookupExtension:
                case NI_AdvSimd_Arm64_VectorTableLookupExtension:
                {
                    assert(isRMW);
                    op1Reg = intrin.Op1.RegNum;
                    op3Reg = intrin.Op3.RegNum;
                    var regCount = intrin.Op2.Oper.IsFieldList
                        ? GetArm64IntrinsicRegisterList(intrin.Op2, out op2Reg, targetReg) : 1u;
                    if (!intrin.Op2.Oper.IsFieldList)
                    {
                        op2Reg = intrin.Op2.RegNum;
                    }
                    switch (regCount)
                    {
                        case 2:
                        {
                            ins = INS_tbx_2regs;
                            break;
                        }
                        case 3:
                        {
                            ins = INS_tbx_3regs;
                            break;
                        }
                        case 4:
                        {
                            ins = INS_tbx_4regs;
                            break;
                        }
                        default:
                        {
                            assert(regCount == 1);
                            assert(ins == INS_tbx);
                            break;
                        }
                    }
                    Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg, opt);
                    break;
                }
                case NI_ArmBase_Arm64_MultiplyLongAdd:
                case NI_ArmBase_Arm64_MultiplyLongSub:
                {
                    assert(opt == INS_OPTS_NONE);
                    Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg);
                    break;
                }
                case NI_Sha3_BitwiseClearXor:
                case NI_Sha3_Xor:
                {
                    Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg, INS_OPTS_16B);
                    break;
                }
                case NI_Sve_ConvertMaskToVector:
                {
                    Emitter.emitIns_R_R_I(ins, emitSize, targetReg, op1Reg, -1, opt);
                    break;
                }
                case NI_Sve_ConvertVectorToMask:
                {
                    Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, 0, opt);
                    break;
                }
                case NI_Sve_Count16BitElements:
                case NI_Sve_Count32BitElements:
                case NI_Sve_Count64BitElements:
                case NI_Sve_Count8BitElements:
                {
                    assert(hasImmediateOperand);
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op1, node);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        var pattern = (insSvePattern)helper.ImmValue;
                        Arm64IntrinsicEmitRegisterPatternImmediate(ins, emitSize, targetReg, pattern, 1, opt);
                    }
                    break;
                }
                case NI_Sve_ConversionTrueMask:
                {
                    Emitter.emitIns_R_PATTERN(ins, emitSize, targetReg, opt, SVE_PATTERN_ALL);
                    break;
                }
                case NI_Sve_CreateWhileLessThanMaskByte:
                case NI_Sve_CreateWhileLessThanMaskDouble:
                case NI_Sve_CreateWhileLessThanMaskInt16:
                case NI_Sve_CreateWhileLessThanMaskInt32:
                case NI_Sve_CreateWhileLessThanMaskInt64:
                case NI_Sve_CreateWhileLessThanMaskSByte:
                case NI_Sve_CreateWhileLessThanMaskSingle:
                case NI_Sve_CreateWhileLessThanMaskUInt16:
                case NI_Sve_CreateWhileLessThanMaskUInt32:
                case NI_Sve_CreateWhileLessThanMaskUInt64:
                {
                    genEmitCreateWhileMask(Emitter, node, ins, INS_sve_whilelo, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve_CreateWhileLessThanOrEqualMaskByte:
                case NI_Sve_CreateWhileLessThanOrEqualMaskDouble:
                case NI_Sve_CreateWhileLessThanOrEqualMaskInt16:
                case NI_Sve_CreateWhileLessThanOrEqualMaskInt32:
                case NI_Sve_CreateWhileLessThanOrEqualMaskInt64:
                case NI_Sve_CreateWhileLessThanOrEqualMaskSByte:
                case NI_Sve_CreateWhileLessThanOrEqualMaskSingle:
                case NI_Sve_CreateWhileLessThanOrEqualMaskUInt16:
                case NI_Sve_CreateWhileLessThanOrEqualMaskUInt32:
                case NI_Sve_CreateWhileLessThanOrEqualMaskUInt64:
                {
                    genEmitCreateWhileMask(Emitter, node, ins, INS_sve_whilels, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve2_CreateWhileGreaterThanMaskByte:
                case NI_Sve2_CreateWhileGreaterThanMaskDouble:
                case NI_Sve2_CreateWhileGreaterThanMaskInt16:
                case NI_Sve2_CreateWhileGreaterThanMaskInt32:
                case NI_Sve2_CreateWhileGreaterThanMaskInt64:
                case NI_Sve2_CreateWhileGreaterThanMaskSByte:
                case NI_Sve2_CreateWhileGreaterThanMaskSingle:
                case NI_Sve2_CreateWhileGreaterThanMaskUInt16:
                case NI_Sve2_CreateWhileGreaterThanMaskUInt32:
                case NI_Sve2_CreateWhileGreaterThanMaskUInt64:
                {
                    genEmitCreateWhileMask(Emitter, node, ins, INS_sve_whilehi, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve2_CreateWhileGreaterThanOrEqualMaskByte:
                case NI_Sve2_CreateWhileGreaterThanOrEqualMaskDouble:
                case NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt16:
                case NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt32:
                case NI_Sve2_CreateWhileGreaterThanOrEqualMaskInt64:
                case NI_Sve2_CreateWhileGreaterThanOrEqualMaskSByte:
                case NI_Sve2_CreateWhileGreaterThanOrEqualMaskSingle:
                case NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt16:
                case NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt32:
                case NI_Sve2_CreateWhileGreaterThanOrEqualMaskUInt64:
                {
                    genEmitCreateWhileMask(Emitter, node, ins, INS_sve_whilehs, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve2_CreateWhileReadAfterWriteMaskByte:
                case NI_Sve2_CreateWhileReadAfterWriteMaskDouble:
                case NI_Sve2_CreateWhileReadAfterWriteMaskInt16:
                case NI_Sve2_CreateWhileReadAfterWriteMaskInt32:
                case NI_Sve2_CreateWhileReadAfterWriteMaskInt64:
                case NI_Sve2_CreateWhileReadAfterWriteMaskSByte:
                case NI_Sve2_CreateWhileReadAfterWriteMaskSingle:
                case NI_Sve2_CreateWhileReadAfterWriteMaskUInt16:
                case NI_Sve2_CreateWhileReadAfterWriteMaskUInt32:
                case NI_Sve2_CreateWhileReadAfterWriteMaskUInt64:
                case NI_Sve2_CreateWhileWriteAfterReadMaskByte:
                case NI_Sve2_CreateWhileWriteAfterReadMaskDouble:
                case NI_Sve2_CreateWhileWriteAfterReadMaskInt16:
                case NI_Sve2_CreateWhileWriteAfterReadMaskInt32:
                case NI_Sve2_CreateWhileWriteAfterReadMaskInt64:
                case NI_Sve2_CreateWhileWriteAfterReadMaskSByte:
                case NI_Sve2_CreateWhileWriteAfterReadMaskSingle:
                case NI_Sve2_CreateWhileWriteAfterReadMaskUInt16:
                case NI_Sve2_CreateWhileWriteAfterReadMaskUInt32:
                case NI_Sve2_CreateWhileWriteAfterReadMaskUInt64:
                {
                    Emitter.emitIns_R_R_R(ins, EA_8BYTE, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve_GatherPrefetch8Bit:
                case NI_Sve_GatherPrefetch16Bit:
                case NI_Sve_GatherPrefetch32Bit:
                case NI_Sve_GatherPrefetch64Bit:
                {
                    assert(hasImmediateOperand);
                    if (!varTypeIsSimd(intrin.Op2.Type))
                    {
                        assert(intrin.NumOperands == 4);
                        var baseSize = emitActualTypeSize(intrin.BaseType);
                        var sopt = INS_SCALABLE_OPTS_NONE;
                        if (baseSize == EA_8BYTE)
                        {
                            sopt = ins == INS_sve_prfb ? INS_SCALABLE_OPTS_NONE : INS_SCALABLE_OPTS_LSL_N;
                        }
                        else
                        {
                            assert(baseSize == EA_4BYTE);
                            opt = varTypeIsUnsigned(node.AuxiliaryType)
                                ? INS_OPTS_SCALABLE_S_UXTW : INS_OPTS_SCALABLE_S_SXTW;
                            sopt = ins == INS_sve_prfb ? INS_SCALABLE_OPTS_NONE : INS_SCALABLE_OPTS_MOD_N;
                        }
                        var helper = new HWIntrinsicImmOpHelper(this, intrin.Op4, node);
                        for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                        {
                            var prfop = (insSvePrfop)helper.ImmValue;
                            Arm64IntrinsicEmitPrefetchThreeRegisters(ins, emitSize, prfop, op1Reg, op2Reg, op3Reg, opt, sopt);
                        }
                    }
                    else
                    {
                        opt = Arm64Emitter.optGetSveInsOpt(emitTypeSize(node.AuxiliaryType));
                        assert(intrin.NumOperands == 3);
                        var helper = new HWIntrinsicImmOpHelper(this, intrin.Op3, node);
                        for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                        {
                            var prfop = (insSvePrfop)helper.ImmValue;
                            Arm64IntrinsicEmitPrefetchTwoRegistersImmediate(ins, emitSize, prfop, op1Reg, op2Reg, 0, opt);
                        }
                    }
                    break;
                }
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt16:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt32:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToInt64:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt16:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt32:
                case NI_Sve_LoadVectorByteNonFaultingZeroExtendToUInt64:
                case NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt32:
                case NI_Sve_LoadVectorInt16NonFaultingSignExtendToInt64:
                case NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt32:
                case NI_Sve_LoadVectorInt16NonFaultingSignExtendToUInt64:
                case NI_Sve_LoadVectorInt32NonFaultingSignExtendToInt64:
                case NI_Sve_LoadVectorInt32NonFaultingSignExtendToUInt64:
                case NI_Sve_LoadVectorNonFaulting:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt16:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt32:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToInt64:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt16:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt32:
                case NI_Sve_LoadVectorSByteNonFaultingSignExtendToUInt64:
                case NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt32:
                case NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToInt64:
                case NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt32:
                case NI_Sve_LoadVectorUInt16NonFaultingZeroExtendToUInt64:
                case NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToInt64:
                case NI_Sve_LoadVectorUInt32NonFaultingZeroExtendToUInt64:
                {
                    if (intrin.NumOperands == 3)
                    {
                        assert(op3Reg != REG_NA);
                        Emitter.emitIns_R(INS_sve_wrffr, emitSize, op3Reg, opt);
                    }
                    Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, 0, opt);
                    break;
                }
                case NI_Sve_GatherVectorByteZeroExtendFirstFaulting:
                case NI_Sve_GatherVectorFirstFaulting:
                case NI_Sve_GatherVectorInt16SignExtendFirstFaulting:
                case NI_Sve_GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting:
                case NI_Sve_GatherVectorInt32SignExtendFirstFaulting:
                case NI_Sve_GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting:
                case NI_Sve_GatherVectorSByteSignExtendFirstFaulting:
                case NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting:
                case NI_Sve_GatherVectorUInt16ZeroExtendFirstFaulting:
                case NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting:
                case NI_Sve_GatherVectorUInt32ZeroExtendFirstFaulting:
                {
                    if (node.AuxiliaryType == TYP_UNKNOWN)
                    {
                        if (intrin.NumOperands == 3)
                        {
                            assert(op3Reg != REG_NA);
                            Emitter.emitIns_R(INS_sve_wrffr, emitSize, op3Reg, opt);
                        }
                    }
                    else if (intrin.NumOperands == 4)
                    {
                        assert(op4Reg != REG_NA);
                        Emitter.emitIns_R(INS_sve_wrffr, emitSize, op4Reg, opt);
                    }
                    goto case NI_Sve_GatherVector;
                }
                case NI_Sve_GatherVector:
                case NI_Sve_GatherVectorByteZeroExtend:
                case NI_Sve_GatherVectorInt16SignExtend:
                case NI_Sve_GatherVectorInt16WithByteOffsetsSignExtend:
                case NI_Sve_GatherVectorInt32SignExtend:
                case NI_Sve_GatherVectorInt32WithByteOffsetsSignExtend:
                case NI_Sve_GatherVectorSByteSignExtend:
                case NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtend:
                case NI_Sve_GatherVectorUInt16ZeroExtend:
                case NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtend:
                case NI_Sve_GatherVectorUInt32ZeroExtend:
                case NI_Sve_GatherVectorWithByteOffsetFirstFaulting:
                {
                    if (!varTypeIsSimd(intrin.Op2.Type))
                    {
                        var baseSize = emitActualTypeSize(intrin.BaseType);
                        var isLoadingFromOffsets = intrin.Id is
                            NI_Sve_GatherVectorByteZeroExtend or NI_Sve_GatherVectorByteZeroExtendFirstFaulting or
                            NI_Sve_GatherVectorInt16WithByteOffsetsSignExtend or
                            NI_Sve_GatherVectorInt16WithByteOffsetsSignExtendFirstFaulting or
                            NI_Sve_GatherVectorInt32WithByteOffsetsSignExtend or
                            NI_Sve_GatherVectorInt32WithByteOffsetsSignExtendFirstFaulting or
                            NI_Sve_GatherVectorSByteSignExtend or NI_Sve_GatherVectorSByteSignExtendFirstFaulting or
                            NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtend or
                            NI_Sve_GatherVectorUInt16WithByteOffsetsZeroExtendFirstFaulting or
                            NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtend or
                            NI_Sve_GatherVectorUInt32WithByteOffsetsZeroExtendFirstFaulting or
                            NI_Sve_GatherVectorWithByteOffsetFirstFaulting;
                        var sopt = INS_SCALABLE_OPTS_NONE;
                        if (baseSize == EA_4BYTE)
                        {
                            opt = varTypeIsUnsigned(node.AuxiliaryType)
                                ? INS_OPTS_SCALABLE_S_UXTW : INS_OPTS_SCALABLE_S_SXTW;
                            sopt = isLoadingFromOffsets ? INS_SCALABLE_OPTS_NONE : INS_SCALABLE_OPTS_MOD_N;
                        }
                        else
                        {
                            assert(baseSize == EA_8BYTE);
                            sopt = isLoadingFromOffsets ? INS_SCALABLE_OPTS_NONE : INS_SCALABLE_OPTS_LSL_N;
                        }
                        Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg, opt, sopt);
                    }
                    else
                    {
                        Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, 0, opt);
                    }
                    break;
                }
                case NI_Sve_GatherVectorWithByteOffsets:
                {
                    assert(!varTypeIsSimd(intrin.Op2.Type));
                    assert(intrin.NumOperands == 3);
                    var baseSize = emitActualTypeSize(intrin.BaseType);
                    if (baseSize == EA_4BYTE)
                    {
                        opt = varTypeIsUnsigned(node.AuxiliaryType)
                            ? INS_OPTS_SCALABLE_S_UXTW : INS_OPTS_SCALABLE_S_SXTW;
                    }
                    else
                    {
                        assert(baseSize == EA_8BYTE);
                    }
                    Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg, opt);
                    break;
                }
                case NI_Sve2_GatherVectorInt16SignExtendNonTemporal:
                case NI_Sve2_GatherVectorInt32SignExtendNonTemporal:
                case NI_Sve2_GatherVectorNonTemporal:
                case NI_Sve2_GatherVectorUInt16ZeroExtendNonTemporal:
                case NI_Sve2_GatherVectorUInt32ZeroExtendNonTemporal:
                {
                    if (!varTypeIsSimd(intrin.Op2.Type))
                    {
                        assert(intrin.NumOperands == 3);
                        nint shift = 0;
                        var tempReg = _internalRegisters.GetSingle(node, new regMaskTP(SRBM_ALLFLOAT));
                        if (intrin.Id is NI_Sve2_GatherVectorInt16SignExtendNonTemporal or
                            NI_Sve2_GatherVectorUInt16ZeroExtendNonTemporal)
                        {
                            shift = 1;
                        }
                        else if (intrin.Id is NI_Sve2_GatherVectorInt32SignExtendNonTemporal or
                            NI_Sve2_GatherVectorUInt32ZeroExtendNonTemporal)
                        {
                            shift = 2;
                        }
                        else
                        {
                            assert(intrin.Id == NI_Sve2_GatherVectorNonTemporal);
                            assert(emitActualTypeSize(intrin.BaseType) == EA_8BYTE);
                            shift = 3;
                        }
                        Emitter.emitIns_R_R_I(INS_sve_lsl, emitSize, tempReg, op3Reg, shift, opt);
                        Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, tempReg, op2Reg, opt);
                    }
                    else
                    {
                        assert(intrin.NumOperands == 2);
                        Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, REG_ZR, opt);
                    }
                    break;
                }
                case NI_Sve2_GatherVectorByteZeroExtendNonTemporal:
                case NI_Sve2_GatherVectorSByteSignExtendNonTemporal:
                {
                    if (!varTypeIsSimd(intrin.Op2.Type))
                    {
                        assert(intrin.NumOperands == 3);
                        Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op3Reg, op2Reg, opt);
                    }
                    else
                    {
                        assert(intrin.NumOperands == 2);
                        Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, REG_ZR, opt);
                    }
                    break;
                }
                case NI_Sve2_GatherVectorInt16WithByteOffsetsSignExtendNonTemporal:
                case NI_Sve2_GatherVectorInt32WithByteOffsetsSignExtendNonTemporal:
                case NI_Sve2_GatherVectorUInt16WithByteOffsetsZeroExtendNonTemporal:
                case NI_Sve2_GatherVectorUInt32WithByteOffsetsZeroExtendNonTemporal:
                case NI_Sve2_GatherVectorWithByteOffsetsNonTemporal:
                {
                    assert(!varTypeIsSimd(intrin.Op2.Type));
                    assert(intrin.NumOperands == 3);
                    Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op3Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve_ReverseElement:
                {
                    Emitter.emitIns_R_R(ins, emitSize, targetReg, op1Reg, opt);
                    break;
                }
                case NI_Sve_Scatter:
                case NI_Sve_Scatter16BitNarrowing:
                case NI_Sve_Scatter32BitNarrowing:
                case NI_Sve_Scatter8BitNarrowing:
                {
                    if (!varTypeIsSimd(intrin.Op2.Type))
                    {
                        assert(intrin.NumOperands == 4);
                        var baseSize = emitActualTypeSize(intrin.BaseType);
                        insScalableOpts sopt;
                        if (baseSize == EA_8BYTE)
                        {
                            sopt = ins == INS_sve_st1b ? INS_SCALABLE_OPTS_NONE : INS_SCALABLE_OPTS_LSL_N;
                            Emitter.emitIns_R_R_R_R(ins, emitSize, op4Reg, op1Reg, op2Reg, op3Reg, opt, sopt);
                        }
                        else
                        {
                            assert(baseSize == EA_4BYTE);
                            opt = varTypeIsUnsigned(node.AuxiliaryType)
                                ? INS_OPTS_SCALABLE_S_UXTW : INS_OPTS_SCALABLE_S_SXTW;
                            sopt = ins == INS_sve_st1b ? INS_SCALABLE_OPTS_NONE : INS_SCALABLE_OPTS_MOD_N;
                            Emitter.emitIns_R_R_R_R(ins, emitSize, op4Reg, op1Reg, op2Reg, op3Reg, opt, sopt);
                        }
                    }
                    else
                    {
                        assert(intrin.NumOperands == 3);
                        Emitter.emitIns_R_R_R_I(ins, emitSize, op3Reg, op1Reg, op2Reg, 0, opt);
                    }
                    break;
                }
                case NI_Sve_Scatter16BitWithByteOffsetsNarrowing:
                case NI_Sve_Scatter32BitWithByteOffsetsNarrowing:
                case NI_Sve_Scatter8BitWithByteOffsetsNarrowing:
                case NI_Sve_ScatterWithByteOffsets:
                {
                    var baseSize = emitActualTypeSize(intrin.BaseType);
                    if (baseSize == EA_4BYTE)
                    {
                        opt = varTypeIsUnsigned(node.AuxiliaryType)
                            ? INS_OPTS_SCALABLE_S_UXTW : INS_OPTS_SCALABLE_S_SXTW;
                    }
                    Emitter.emitIns_R_R_R_R(ins, emitSize, op4Reg, op1Reg, op2Reg, op3Reg, opt);
                    break;
                }
                case NI_Sve2_Scatter16BitNarrowingNonTemporal:
                case NI_Sve2_Scatter32BitNarrowingNonTemporal:
                case NI_Sve2_ScatterNonTemporal:
                {
                    if (!varTypeIsSimd(intrin.Op2.Type))
                    {
                        assert(intrin.NumOperands == 4);
                        nint shift = 0;
                        var tempReg = _internalRegisters.GetSingle(node, new regMaskTP(SRBM_ALLFLOAT));
                        if (intrin.Id == NI_Sve2_Scatter16BitNarrowingNonTemporal)
                        {
                            shift = 1;
                        }
                        else if (intrin.Id == NI_Sve2_Scatter32BitNarrowingNonTemporal)
                        {
                            shift = 2;
                        }
                        else
                        {
                            assert(intrin.Id == NI_Sve2_ScatterNonTemporal);
                            shift = 3;
                        }
                        Emitter.emitIns_R_R_I(INS_sve_lsl, emitSize, tempReg, op3Reg, shift, opt);
                        Emitter.emitIns_R_R_R_R(ins, emitSize, op4Reg, op1Reg, tempReg, op2Reg, opt);
                    }
                    else
                    {
                        assert(intrin.NumOperands == 3);
                        Emitter.emitIns_R_R_R_R(ins, emitSize, op3Reg, op1Reg, op2Reg, REG_ZR, opt);
                    }
                    break;
                }
                case NI_Sve2_Scatter8BitNarrowingNonTemporal:
                {
                    if (!varTypeIsSimd(intrin.Op2.Type))
                    {
                        assert(intrin.NumOperands == 4);
                        Emitter.emitIns_R_R_R_R(ins, emitSize, op4Reg, op1Reg, op3Reg, op2Reg, opt);
                    }
                    else
                    {
                        assert(intrin.NumOperands == 3);
                        Emitter.emitIns_R_R_R_R(ins, emitSize, op3Reg, op1Reg, op2Reg, REG_ZR, opt);
                    }
                    break;
                }
                case NI_Sve2_Scatter16BitWithByteOffsetsNarrowingNonTemporal:
                case NI_Sve2_Scatter32BitWithByteOffsetsNarrowingNonTemporal:
                case NI_Sve2_Scatter8BitWithByteOffsetsNarrowingNonTemporal:
                case NI_Sve2_ScatterWithByteOffsetsNonTemporal:
                {
                    assert(!varTypeIsSimd(intrin.Op2.Type));
                    assert(intrin.NumOperands == 4);
                    Emitter.emitIns_R_R_R_R(ins, emitSize, op4Reg, op1Reg, op3Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve_StoreNarrowing:
                {
                    opt = Arm64Emitter.optGetSveInsOpt(emitTypeSize(intrin.BaseType));
                    Emitter.emitIns_R_R_R_I(ins, emitSize, op3Reg, op1Reg, op2Reg, 0, opt);
                    break;
                }
                case NI_Sve_TransposeEven:
                case NI_Sve_TransposeOdd:
                case NI_Sve_UnzipEven:
                case NI_Sve_UnzipOdd:
                case NI_Sve_ZipHigh:
                case NI_Sve_ZipLow:
                {
                    Emitter.emitIns_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve_SaturatingDecrementBy16BitElementCountScalar:
                case NI_Sve_SaturatingDecrementBy32BitElementCountScalar:
                case NI_Sve_SaturatingDecrementBy64BitElementCountScalar:
                case NI_Sve_SaturatingIncrementBy16BitElementCountScalar:
                case NI_Sve_SaturatingIncrementBy32BitElementCountScalar:
                case NI_Sve_SaturatingIncrementBy64BitElementCountScalar:
                {
                    emitSize = emitActualTypeSize(node.Type);
                    opt = INS_OPTS_NONE;
                    goto case NI_Sve_SaturatingDecrementBy16BitElementCount;
                }
                case NI_Sve_SaturatingDecrementBy16BitElementCount:
                case NI_Sve_SaturatingDecrementBy32BitElementCount:
                case NI_Sve_SaturatingDecrementBy64BitElementCount:
                case NI_Sve_SaturatingDecrementBy8BitElementCount:
                case NI_Sve_SaturatingIncrementBy16BitElementCount:
                case NI_Sve_SaturatingIncrementBy32BitElementCount:
                case NI_Sve_SaturatingIncrementBy64BitElementCount:
                case NI_Sve_SaturatingIncrementBy8BitElementCount:
                {
                    assert(isRMW);
                    if (intrin.Op2.Oper.IsCnsIntOrI && intrin.Op3.Oper.IsCnsIntOrI)
                    {
                        assert(intrin.Op2.IsContainedIntOrIImmed && intrin.Op3.IsContainedIntOrIImmed);
                        var scale = unchecked((int)intrin.Op2.AsIntCon().IconValue);
                        var pattern = unchecked((insSvePattern)intrin.Op3.AsIntCon().IconValue);
                        Arm64IntrinsicEmitTwoRegistersPatternImmediate(ins, emitSize, targetReg, op1Reg, pattern, scale, opt);
                    }
                    else
                    {
                        assert(!intrin.Op2.IsContainedIntOrIImmed && !intrin.Op3.IsContainedIntOrIImmed);
                        var scalarSize = emitActualTypeSize(node.SimdBaseType);
                        Emitter.emitIns_R_R_I(INS_sub, scalarSize, op2Reg, op2Reg, 1);
                        Emitter.emitIns_R_R_I(INS_lsl, scalarSize, op3Reg, op3Reg, 4);
                        Emitter.emitIns_R_R_R(INS_orr, scalarSize, op2Reg, op2Reg, op3Reg);
                        var numInstrs = targetReg != op1Reg ? 2 : 1;
                        var helper = new HWIntrinsicImmOpHelper(this, op2Reg, 0, 511, node, numInstrs);
                        for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                        {
                            var value = helper.ImmValue;
                            var scale = (value & 0xF) + 1;
                            var pattern = (insSvePattern)(value >> 4);
                            Arm64IntrinsicEmitTwoRegistersPatternImmediate(ins, emitSize, targetReg, op1Reg, pattern, scale, opt);
                        }
                        Emitter.emitIns_R_R_I(INS_and, scalarSize, op2Reg, op2Reg, 0xF);
                        Emitter.emitIns_R_R_I(INS_lsr, scalarSize, op3Reg, op3Reg, 4);
                        Emitter.emitIns_R_R_I(INS_add, scalarSize, op2Reg, op2Reg, 1);
                    }
                    break;
                }
                case NI_Sve_SaturatingDecrementByActiveElementCount:
                case NI_Sve_SaturatingIncrementByActiveElementCount:
                {
                    if (varTypeIsUnsigned(node.AuxiliaryType))
                    {
                        ins = intrin.Id == NI_Sve_SaturatingDecrementByActiveElementCount ? INS_sve_uqdecp : INS_sve_uqincp;
                    }
                    if (!varTypeIsSimd(node.Type))
                    {
                        emitSize = emitActualTypeSize(intrin.Op1);
                    }
                    Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve_Compute8BitAddresses:
                case NI_Sve_Compute16BitAddresses:
                case NI_Sve_Compute32BitAddresses:
                case NI_Sve_Compute64BitAddresses:
                {
                    Emitter.emitInsSve_R_R_R_I(ins, EA_SCALABLE, targetReg, op1Reg, op2Reg,
                        HWIntrinsicInfo.lookupIval(intrin.Id), opt, INS_SCALABLE_OPTS_LSL_N);
                    break;
                }
                case NI_Sve_TestAnyTrue:
                case NI_Sve_TestFirstTrue:
                case NI_Sve_TestLastTrue:
                {
                    assert(targetReg == REG_NA);
                    Emitter.emitIns_R_R(ins, EA_SCALABLE, op1Reg, op2Reg, INS_OPTS_SCALABLE_B);
                    break;
                }
                case NI_Sve_ExtractVector:
                {
                    assert(isRMW);
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op3, node, targetReg != op1Reg ? 2 : 1);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        var byteIndex = unchecked(intrin.BaseType.Size * helper.ImmValue);
                        Emitter.emitIns_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, byteIndex, INS_OPTS_SCALABLE_B);
                    }
                    break;
                }
                case NI_Sve_InsertIntoShiftedVector:
                {
                    assert(isRMW);
                    assert(Arm64Emitter.isVectorRegister(op2Reg) == varTypeIsFloating(intrin.BaseType));
                    Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve_CreateBreakAfterMask:
                case NI_Sve_CreateBreakBeforeMask:
                {
                    Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, INS_OPTS_SCALABLE_B);
                    break;
                }
                case NI_Sve_CreateBreakAfterPropagateMask:
                case NI_Sve_CreateBreakBeforePropagateMask:
                {
                    Emitter.emitInsSve_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg, INS_OPTS_SCALABLE_B);
                    break;
                }
                case NI_Sve_CreateMaskForFirstActiveElement:
                {
                    assert(isRMW);
                    assert(HWIntrinsicInfo.IsExplicitMaskedOperation(intrin.Id));
                    Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, INS_OPTS_SCALABLE_B);
                    break;
                }
                case NI_Sve_LoadVectorFirstFaulting:
                case NI_Sve_LoadVectorInt16SignExtendFirstFaulting:
                case NI_Sve_LoadVectorInt32SignExtendFirstFaulting:
                case NI_Sve_LoadVectorUInt16ZeroExtendFirstFaulting:
                case NI_Sve_LoadVectorUInt32ZeroExtendFirstFaulting:
                {
                    if (intrin.NumOperands == 3)
                    {
                        assert(op3Reg != REG_NA);
                        Emitter.emitIns_R(INS_sve_wrffr, emitSize, op3Reg, opt);
                    }
                    var sopt = opt == INS_OPTS_SCALABLE_B ? INS_SCALABLE_OPTS_NONE : INS_SCALABLE_OPTS_LSL_N;
                    Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, REG_ZR, opt, sopt);
                    break;
                }
                case NI_Sve_LoadVectorByteZeroExtendFirstFaulting:
                case NI_Sve_LoadVectorSByteSignExtendFirstFaulting:
                {
                    if (intrin.NumOperands == 3)
                    {
                        assert(op3Reg != REG_NA);
                        Emitter.emitIns_R(INS_sve_wrffr, emitSize, op3Reg, opt);
                    }
                    Emitter.emitIns_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, REG_ZR, opt);
                    break;
                }
                case NI_Sve_SetFfr:
                {
                    assert(targetReg == REG_NA);
                    Emitter.emitIns_R(ins, emitSize, op1Reg, opt);
                    break;
                }
                case NI_Sve_ConditionalExtractAfterLastActiveElementScalar:
                case NI_Sve_ConditionalExtractLastActiveElementScalar:
                {
                    opt = Arm64Emitter.optGetSveInsOpt(emitTypeSize(node.SimdBaseType));
                    if (Arm64Emitter.isGeneralRegisterOrZR(targetReg))
                    {
                        assert(varTypeIsIntegralOrI(intrin.BaseType));
                        emitSize = varTypeIsLong(intrin.BaseType) ? EA_8BYTE : EA_4BYTE;
                        Emitter.emitInsSve_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg,
                            opt, INS_SCALABLE_OPTS_NONE);
                        if (varTypeIsSmall(intrin.BaseType))
                        {
                            var castSize = emitActualTypeSize(node.Type);
                            inst_Mov_Extend(intrin.BaseType, true, targetReg, targetReg, false, castSize);
                        }
                        break;
                    }
                    goto case NI_Sve_ConditionalExtractAfterLastActiveElement;
                }
                case NI_Sve_ConditionalExtractAfterLastActiveElement:
                case NI_Sve_ConditionalExtractLastActiveElement:
                {
                    assert(Arm64Emitter.isVectorRegister(targetReg));
                    assert(varTypeIsFloating(node.Type) || varTypeIsSimd(node.Type));
                    Emitter.emitInsSve_R_R_R_R(ins, EA_SCALABLE, targetReg, op1Reg, op2Reg, op3Reg,
                        opt, INS_SCALABLE_OPTS_WITH_SIMD_SCALAR);
                    break;
                }
                case NI_Sve_ExtractAfterLastActiveElementScalar:
                case NI_Sve_ExtractLastActiveElementScalar:
                {
                    opt = Arm64Emitter.optGetSveInsOpt(emitTypeSize(node.SimdBaseType));
                    if (Arm64Emitter.isGeneralRegisterOrZR(targetReg))
                    {
                        assert(varTypeIsIntegralOrI(intrin.BaseType));
                        emitSize = varTypeIsLong(intrin.BaseType) ? EA_8BYTE : EA_4BYTE;
                        Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt, INS_SCALABLE_OPTS_NONE);
                        if (varTypeIsSmall(intrin.BaseType))
                        {
                            var castSize = emitActualTypeSize(node.Type);
                            inst_Mov_Extend(intrin.BaseType, true, targetReg, targetReg, false, castSize);
                        }
                        break;
                    }
                    goto case NI_Sve_ExtractAfterLastActiveElement;
                }
                case NI_Sve_ExtractAfterLastActiveElement:
                case NI_Sve_ExtractLastActiveElement:
                {
                    assert(Arm64Emitter.isVectorRegister(targetReg));
                    assert(varTypeIsFloating(node.Type) || varTypeIsSimd(node.Type));
                    Emitter.emitInsSve_R_R_R(ins, EA_SCALABLE, targetReg, op1Reg, op2Reg, opt,
                        INS_SCALABLE_OPTS_WITH_SIMD_SCALAR);
                    break;
                }
                case NI_Sve_TrigonometricMultiplyAddCoefficient:
                case NI_Sve2_AddRotateComplex:
                case NI_Sve2_AddSaturateRotateComplex:
                {
                    assert(isRMW);
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op3, node, targetReg != op1Reg ? 2 : 1);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        Emitter.emitInsSve_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, helper.ImmValue, opt);
                    }
                    break;
                }
                case NI_Sve_MultiplyAddRotateComplexBySelectedScalar:
                case NI_Sve2_MultiplyAddRotateComplexBySelectedScalar:
                case NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar:
                {
                    assert(isRMW);
                    assert(hasImmediateOperand);
                    if (intrin.Op4.Oper.IsCnsIntOrI && intrin.Op5.Oper.IsCnsIntOrI)
                    {
                        assert(intrin.Op4.IsContainedIntOrIImmed && intrin.Op5.IsContainedIntOrIImmed);
                        Arm64IntrinsicEmitFourRegistersTwoImmediates(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg,
                            intrin.Op4.AsIntCon().IconValue, intrin.Op5.AsIntCon().IconValue, opt);
                    }
                    else
                    {
                        assert(!intrin.Op4.IsContainedIntOrIImmed && !intrin.Op5.IsContainedIntOrIImmed);
                        var scalarSize = emitActualTypeSize(node.SimdBaseType);
                        var baseType = node.SimdBaseType;
                        const uint rotMask = 0b11;
                        var indexMask = baseType is TYP_SHORT or TYP_USHORT ? 0b11u : 0b1u;
                        var numIndexBits = genCountBits(indexMask);
                        Emitter.emitIns_R_R_I(INS_lsl, scalarSize, op5Reg, op5Reg, (nint)numIndexBits);
                        Emitter.emitIns_R_R_R(INS_orr, scalarSize, op4Reg, op4Reg, op5Reg);
                        var upperBound = (rotMask << (int)numIndexBits) | indexMask;
                        var helper = new HWIntrinsicImmOpHelper(this, op4Reg, 0, (int)upperBound, node,
                            targetReg != op1Reg ? 2 : 1);
                        for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                        {
                            var value = helper.ImmValue;
                            var index = unchecked((nint)((uint)value & indexMask));
                            nint rotation = value >> (int)numIndexBits;
                            Arm64IntrinsicEmitFourRegistersTwoImmediates(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg,
                                index, rotation, opt);
                        }
                        Emitter.emitIns_R_R_I(INS_and, scalarSize, op4Reg, op4Reg, (nint)indexMask);
                        Emitter.emitIns_R_R_I(INS_lsr, scalarSize, op5Reg, op5Reg, (nint)numIndexBits);
                    }
                    break;
                }
                case NI_Sve2_AddWideningEven:
                {
                    if (node.SimdBaseType != node.AuxiliaryType)
                    {
                        ins = varTypeIsUnsigned(intrin.BaseType) ? INS_sve_uaddlb : INS_sve_saddlb;
                    }
                    Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve2_AddWideningOdd:
                {
                    if (node.SimdBaseType != node.AuxiliaryType)
                    {
                        ins = varTypeIsUnsigned(intrin.BaseType) ? INS_sve_uaddlt : INS_sve_saddlt;
                    }
                    Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve2_BitwiseClearXor:
                case NI_Sve2_Xor:
                {
                    Emitter.emitInsSve_R_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg, INS_OPTS_SCALABLE_D);
                    break;
                }
                case NI_Sve2_BitwiseSelect:
                case NI_Sve2_BitwiseSelectLeftInverted:
                case NI_Sve2_BitwiseSelectRightInverted:
                {
                    Emitter.emitInsSve_R_R_R_R(ins, emitSize, targetReg, op2Reg, op3Reg, op1Reg, INS_OPTS_SCALABLE_D);
                    break;
                }
                case NI_Sve2_MultiplyAddRotateComplex:
                case NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplex:
                case NI_Sve2_DotProductRotateComplex:
                {
                    assert(isRMW);
                    assert(hasImmediateOperand);
                    var helper = new HWIntrinsicImmOpHelper(this, intrin.Op4, node, targetReg != op1Reg ? 2 : 1);
                    for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                    {
                        Emitter.emitInsSve_R_R_R_R_I(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg,
                            helper.ImmValue, opt);
                    }
                    break;
                }
                case NI_Sve2_DotProductRotateComplexBySelectedIndex:
                {
                    assert(isRMW);
                    assert(hasImmediateOperand);
                    if (intrin.Op4.Oper.IsCnsIntOrI && intrin.Op5.Oper.IsCnsIntOrI)
                    {
                        assert(intrin.Op4.IsContainedIntOrIImmed && intrin.Op5.IsContainedIntOrIImmed);
                        Arm64IntrinsicEmitFourRegistersTwoImmediates(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg,
                            intrin.Op4.AsIntCon().IconValue, intrin.Op5.AsIntCon().IconValue, opt);
                    }
                    else
                    {
                        assert(!intrin.Op4.IsContainedIntOrIImmed && !intrin.Op5.IsContainedIntOrIImmed);
                        var scalarSize = emitActualTypeSize(node.SimdBaseType);
                        var baseType = node.SimdBaseType;
                        const uint rotMask = 0b11;
                        var indexMask = baseType == TYP_BYTE ? 0b11u : 0b1u;
                        var numIndexBits = genCountBits(indexMask);
                        Emitter.emitIns_R_R_I(INS_lsl, scalarSize, op5Reg, op5Reg, (nint)numIndexBits);
                        Emitter.emitIns_R_R_R(INS_orr, scalarSize, op4Reg, op4Reg, op5Reg);
                        var upperBound = (rotMask << (int)numIndexBits) | indexMask;
                        var helper = new HWIntrinsicImmOpHelper(this, op4Reg, 0, (int)upperBound, node,
                            targetReg != op1Reg ? 2 : 1);
                        for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                        {
                            var value = helper.ImmValue;
                            var index = unchecked((nint)((uint)value & indexMask));
                            nint rotation = value >> (int)numIndexBits;
                            Arm64IntrinsicEmitFourRegistersTwoImmediates(ins, emitSize, targetReg, op1Reg, op2Reg, op3Reg,
                                index, rotation, opt);
                        }
                        Emitter.emitIns_R_R_I(INS_and, scalarSize, op4Reg, op4Reg, (nint)indexMask);
                        Emitter.emitIns_R_R_I(INS_lsr, scalarSize, op5Reg, op5Reg, (nint)numIndexBits);
                    }
                    break;
                }
                case NI_Sve2_SubtractWideningEven:
                {
                    if (node.SimdBaseType != node.AuxiliaryType)
                    {
                        ins = varTypeIsUnsigned(intrin.BaseType) ? INS_sve_usublb : INS_sve_ssublb;
                    }
                    Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Sve2_SubtractWideningOdd:
                {
                    if (node.SimdBaseType != node.AuxiliaryType)
                    {
                        ins = varTypeIsUnsigned(intrin.BaseType) ? INS_sve_usublt : INS_sve_ssublt;
                    }
                    Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_SveSha3_BitwiseRotateLeftBy1AndXor:
                {
                    opt = INS_OPTS_SCALABLE_D;
                    Emitter.emitInsSve_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                case NI_Vector_Create:
                {
                    emitSize = opt == INS_OPTS_SCALABLE_D ? EA_8BYTE : EA_4BYTE;
                    if (varTypeIsFloating(intrin.BaseType))
                    {
                        var tmpReg = _internalRegisters.Extract(node, new regMaskTP(SRBM_ALLINT));
                        var fmovOpt = emitSize == EA_8BYTE ? INS_OPTS_D_TO_8BYTE : INS_OPTS_S_TO_4BYTE;
                        Emitter.emitIns_Mov(INS_fmov, emitSize, tmpReg, op1Reg, false, fmovOpt);
                        op1Reg = tmpReg;
                    }
                    Emitter.emitInsSve_R_R(ins, emitSize, targetReg, op1Reg, opt);
                    break;
                }
                case NI_Vector_CreateSequence:
                {
                    emitSize = opt == INS_OPTS_SCALABLE_D ? EA_8BYTE : EA_4BYTE;
                    Emitter.emitIns_R_R_R(ins, emitSize, targetReg, op1Reg, op2Reg, opt);
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
        }

        genProduceReg(node);
    }
}
#endif
