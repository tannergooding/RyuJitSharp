// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
using static RyuJitSharp.insScalableOpts;
using static RyuJitSharp.insSveMovOpts;
using Arm64Emitter = RyuJitSharp.Emitter;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genEmbeddedMaskedHWIntrinsic(GenTreeHWIntrinsic cndSelNode, regNumber targetReg)
    {
        var intrinCndSel = new Arm64HWIntrinsic(cndSelNode);
        assert(HWIntrinsicInfo.IsSveConditionalSelect(intrinCndSel.Id));
        var maskOp = intrinCndSel.Op1;
        var embMaskOp = intrinCndSel.Op2;
        var falseOp = intrinCndSel.Op3;
        assert(embMaskOp.Oper.IsHWIntrinsic);
        assert(embMaskOp.IsContained);
        assert(embMaskOp.Oper.IsHWIntrinsic && (embMaskOp.Flags & GTF_HW_EM_OP) != 0);

        var intrinEmbMask = new Arm64HWIntrinsic(embMaskOp.AsHWIntrinsic());
        var insEmbMask = HWIntrinsicInfo.lookupIns(intrinEmbMask.Id, intrinEmbMask.BaseType, _compiler);
        var isRMW = embMaskOp.IsRmwHWIntrinsic(_compiler);
        var isOptionalEmbMask = HWIntrinsicInfo.IsOptionalEmbeddedMaskedOperation(intrinEmbMask.Id);
        var maskReg = maskOp.RegNum;
        var embMaskOp1Reg = REG_NA;
        var embMaskOp2Reg = REG_NA;
        var embMaskOp3Reg = REG_NA;
        var falseReg = falseOp.RegNum;
        var tempReg = REG_NA;

        switch (intrinEmbMask.NumOperands)
        {
            case 4:
            {
                _ = intrinEmbMask.Op4.RegNum;
                goto case 3;
            }
            case 3:
            {
                embMaskOp3Reg = intrinEmbMask.Op3.RegNum;
                goto case 2;
            }
            case 2:
            {
                embMaskOp2Reg = intrinEmbMask.Op2.RegNum;
                goto case 1;
            }
            case 1:
            {
                embMaskOp1Reg = intrinEmbMask.Op1.RegNum;
                break;
            }
            default:
            {
                unreached();
                break;
            }
        }

        if (intrinEmbMask.Id == NI_Sve_MultiplyAddRotateComplex)
        {
            assert(intrinEmbMask.NumOperands == 4);
            tempReg = _internalRegisters.GetSingle(cndSelNode, new regMaskTP(SRBM_ALLFLOAT));
        }
        var emitSize = EA_SCALABLE;
        var opt = Arm64Emitter.optGetSveInsOpt(emitTypeSize(intrinCndSel.BaseType));
        var embOpt = opt;
        var sopt = INS_SCALABLE_OPTS_NONE;
#if DEBUG
        if (isRMW)
        {
            checkRMWRegisters(intrinEmbMask, targetReg);
        }
#endif
        if (intrinEmbMask.NumOperands == 1)
        {
            assert(!isRMW);
            if (HWIntrinsicInfo.IsReduceOperation(intrinEmbMask.Id))
            {
                Emitter.emitInsSve_R_R_R(insEmbMask, emitSize, targetReg, maskReg, embMaskOp1Reg, embOpt);
                return;
            }

            switch (intrinEmbMask.Id)
            {
                case NI_Sve2_ConvertToDoubleOdd:
                {
                    // This form cannot use MOVPRFX; preserve inactive lanes with SEL.
                    embOpt = emitTypeSize(intrinEmbMask.BaseType) == EA_4BYTE
                        ? INS_OPTS_S_TO_D : INS_OPTS_SCALABLE_D;
                    if (!maskOp.IsTrueMask(intrinCndSel.BaseType) && targetReg != falseReg)
                    {
                        assert(!falseOp.IsContained);
                        Emitter.emitIns_R_R_R_R(INS_sve_sel, emitSize, targetReg, maskReg, targetReg, falseReg, opt);
                    }
                    Emitter.emitInsSve_R_R_R(insEmbMask, emitSize, targetReg, maskReg, embMaskOp1Reg, embOpt, sopt);
                    return;
                }
                case NI_Sve_ConvertToInt32:
                case NI_Sve_ConvertToUInt32:
                case NI_Sve_ConvertToSingle:
                case NI_Sve2_ConvertToSingleEvenRoundToOdd:
                {
                    embOpt = emitTypeSize(intrinEmbMask.BaseType) == EA_8BYTE
                        ? INS_OPTS_D_TO_S : INS_OPTS_SCALABLE_S;
                    break;
                }
                case NI_Sve_ConvertToInt64:
                case NI_Sve_ConvertToUInt64:
                case NI_Sve_ConvertToDouble:
                {
                    embOpt = emitTypeSize(intrinEmbMask.BaseType) == EA_4BYTE
                        ? INS_OPTS_S_TO_D : INS_OPTS_SCALABLE_D;
                    break;
                }
                default:
                {
                    break;
                }
            }

            if (targetReg == falseReg)
            {
                Emitter.emitIns_R_R_R(insEmbMask, emitSize, targetReg, maskReg, embMaskOp1Reg, embOpt);
            }
            else if (falseOp.IsContained)
            {
                assert(falseOp.IsZeroForSelect);
                if (maskOp.IsTrueMask(intrinCndSel.BaseType))
                {
                    Emitter.emitIns_R_R_R(insEmbMask, emitSize, targetReg, maskReg, embMaskOp1Reg, embOpt);
                }
                else
                {
                    Emitter.emitInsSve_R_R_R_R(insEmbMask, emitSize, targetReg, maskReg, targetReg,
                        embMaskOp1Reg, embOpt, sopt, INS_SVE_MOV_OPTS_ZEROING);
                }
            }
            else if (Arm64Emitter.isVectorRegister(embMaskOp1Reg) && targetReg == embMaskOp1Reg)
            {
                // MOVPRFX would clobber the source already held in the destination.
                Emitter.emitIns_R_R_R(insEmbMask, emitSize, targetReg, maskReg, embMaskOp1Reg, embOpt);
                Emitter.emitIns_R_R_R_R(INS_sve_sel, emitSize, targetReg, maskReg, targetReg, falseReg, opt);
            }
            else
            {
                Emitter.emitInsSve_R_R_R_R(insEmbMask, emitSize, targetReg, maskReg, falseReg,
                    embMaskOp1Reg, embOpt, sopt, INS_SVE_MOV_OPTS_UNPRED);
            }

            return;
        }
        else if (intrinEmbMask.NumOperands == 2)
        {
            switch (intrinEmbMask.Id)
            {
                case NI_Sve_CreateBreakPropagateMask:
                {
                    embOpt = INS_OPTS_SCALABLE_B;
                    assert(falseOp.IsZeroForSelect);
                    Emitter.emitInsSve_R_R_R_R(insEmbMask, emitSize, targetReg, maskReg, embMaskOp1Reg,
                        embMaskOp2Reg, embOpt, sopt);
                    return;
                }
                case NI_Sve_AddSequentialAcross:
                {
                    assert(falseOp.IsVectorZero);
                    Emitter.emitInsSve_R_R_R_R(insEmbMask, emitSize, targetReg, maskReg, embMaskOp1Reg,
                        embMaskOp2Reg, embOpt, sopt);
                    return;
                }
                case NI_Sve2_ConvertToSingleOdd:
                case NI_Sve2_ConvertToSingleOddRoundToOdd:
                {
                    embOpt = INS_OPTS_D_TO_S;
                    if (falseOp.IsVectorZero && !maskOp.IsTrueMask(intrinCndSel.BaseType) &&
                        targetReg == falseReg)
                    {
                        Emitter.emitIns_R_R_R(INS_sve_mov, emitSize, targetReg, maskReg, embMaskOp1Reg,
                            opt, INS_SCALABLE_OPTS_PREDICATE_MERGE);
                        Emitter.emitInsSve_R_R_R(insEmbMask, emitSize, targetReg, maskReg, embMaskOp2Reg,
                            embOpt, sopt);
                        return;
                    }
                    goto case NI_Sve2_AddPairwise;
                }
                case NI_Sve2_AddPairwise:
                case NI_Sve2_MaxNumberPairwise:
                case NI_Sve2_MaxPairwise:
                case NI_Sve2_MinNumberPairwise:
                case NI_Sve2_MinPairwise:
                {
                    // Predicated MOVPRFX is unpredictable for these instructions.
                    Emitter.emitInsSve_R_R_R_R(insEmbMask, emitSize, targetReg, maskReg, embMaskOp1Reg,
                        embMaskOp2Reg, embOpt, sopt);
                    if (!maskOp.IsTrueMask(intrinCndSel.BaseType) && targetReg != falseReg)
                    {
                        assert(!falseOp.IsContained);
                        Emitter.emitInsSve_R_R_R_R(INS_sve_sel, emitSize, targetReg, maskReg, targetReg,
                            falseReg, opt);
                    }
                    return;
                }
                case NI_Sve2_AddSaturate:
                {
                    var baseType = embMaskOp.AsHWIntrinsic().SimdBaseType;
                    var auxType = embMaskOp.AsHWIntrinsic().AuxiliaryType;
                    if (baseType != auxType)
                    {
                        insEmbMask = varTypeIsUnsigned(baseType) ? INS_sve_usqadd : INS_sve_suqadd;
                        isOptionalEmbMask = false;
                    }
                    else
                    {
                        isOptionalEmbMask = true;
                    }
                    break;
                }
                case NI_Sve_ShiftLeftLogical:
                case NI_Sve_ShiftRightArithmetic:
                case NI_Sve_ShiftRightLogical:
                {
                    var op2Size = emitTypeSize(embMaskOp.AsHWIntrinsic().AuxiliaryType);
                    if (op2Size != emitTypeSize(intrinEmbMask.BaseType))
                    {
                        assert(Arm64Emitter.optGetSveInsOpt(op2Size) == INS_OPTS_SCALABLE_D);
                        sopt = INS_SCALABLE_OPTS_WIDE;
                    }
                    break;
                }
                default:
                {
                    break;
                }
            }

            if (!isRMW)
            {
                switch (intrinEmbMask.Id)
                {
                    case NI_Sve_And_Predicates:
                    case NI_Sve_BitwiseClear_Predicates:
                    case NI_Sve_Or_Predicates:
                    case NI_Sve_Xor_Predicates:
                    {
                        embOpt = INS_OPTS_SCALABLE_B;
                        break;
                    }
                    default:
                    {
                        break;
                    }
                }
                Emitter.emitIns_R_R_R_R(insEmbMask, emitSize, targetReg, maskReg, embMaskOp1Reg,
                    embMaskOp2Reg, embOpt);
                return;
            }
            else if (isOptionalEmbMask)
            {
                if (maskOp.IsTrueMask(intrinEmbMask.BaseType) ||
                    (!falseOp.IsZeroForSelect && targetReg != falseReg && falseReg != embMaskOp1Reg))
                {
                    if (HWIntrinsicInfo.HasImmediateOperand(intrinEmbMask.Id))
                    {
                        var helper = new HWIntrinsicImmOpHelper(this, intrinEmbMask.Op2, embMaskOp.AsHWIntrinsic());
                        for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
                        {
                            Emitter.emitInsSve_R_R_I(insEmbMask, emitSize, targetReg, embMaskOp1Reg,
                                helper.ImmValue, embOpt, sopt);
                        }
                    }
                    else
                    {
                        Emitter.emitIns_R_R_R(insEmbMask, emitSize, targetReg, embMaskOp1Reg,
                            embMaskOp2Reg, embOpt, sopt);
                    }
                    if (!maskOp.IsTrueMask(intrinCndSel.BaseType))
                    {
                        Emitter.emitIns_R_R_R_R(INS_sve_sel, emitSize, targetReg, maskReg, targetReg, falseReg, opt);
                    }
                    return;
                }
            }
        }
        else if (HWIntrinsicInfo.IsFmaIntrinsic(intrinEmbMask.Id) && intrinEmbMask.NumOperands == 3)
        {
            // FMLA destroys the addend; FMAD destroys a multiplicand.
            var useAddend = true;
            if (targetReg == embMaskOp2Reg)
            {
                useAddend = false;
                (embMaskOp1Reg, embMaskOp3Reg) = (embMaskOp3Reg, embMaskOp1Reg);
                (embMaskOp1Reg, embMaskOp2Reg) = (embMaskOp2Reg, embMaskOp1Reg);
            }
            else if (targetReg == embMaskOp3Reg)
            {
                useAddend = false;
                (embMaskOp1Reg, embMaskOp3Reg) = (embMaskOp3Reg, embMaskOp1Reg);
            }
            switch (intrinEmbMask.Id)
            {
                case NI_Sve_FusedMultiplyAdd:
                {
                    insEmbMask = useAddend ? INS_sve_fmla : INS_sve_fmad;
                    break;
                }
                case NI_Sve_FusedMultiplyAddNegated:
                {
                    insEmbMask = useAddend ? INS_sve_fnmla : INS_sve_fnmad;
                    break;
                }
                case NI_Sve_FusedMultiplySubtract:
                {
                    insEmbMask = useAddend ? INS_sve_fmls : INS_sve_fmsb;
                    break;
                }
                case NI_Sve_FusedMultiplySubtractNegated:
                {
                    insEmbMask = useAddend ? INS_sve_fnmls : INS_sve_fnmsb;
                    break;
                }
                case NI_Sve_MultiplyAdd:
                {
                    insEmbMask = useAddend ? INS_sve_mla : INS_sve_mad;
                    break;
                }
                case NI_Sve_MultiplySubtract:
                {
                    insEmbMask = useAddend ? INS_sve_mls : INS_sve_msb;
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
        }

        var mopt = INS_SVE_MOV_OPTS_UNPRED;
        if (falseOp.IsZeroForSelect)
        {
            mopt = INS_SVE_MOV_OPTS_ZEROING;
        }
        else if (targetReg != falseReg)
        {
            if (falseReg != embMaskOp1Reg)
            {
                assert(HWIntrinsicInfo.IsEmbeddedMaskedOperation(intrinEmbMask.Id));
                assert(!HWIntrinsicInfo.IsZeroingMaskedOperation(intrinEmbMask.Id));
                Emitter.emitIns_R_R_R_R(INS_sve_sel, emitSize, targetReg, maskReg, embMaskOp1Reg, falseReg, opt);
                embMaskOp1Reg = targetReg;
                mopt = INS_SVE_MOV_OPTS_UNPRED;
            }
            else
            {
                mopt = INS_SVE_MOV_OPTS_UNPRED;
            }
        }
        else if (falseReg != embMaskOp1Reg)
        {
            mopt = INS_SVE_MOV_OPTS_MERGING;
        }
        if (maskOp.IsTrueMask(intrinCndSel.BaseType))
        {
            mopt = INS_SVE_MOV_OPTS_UNPRED;
        }

        if (HWIntrinsicInfo.HasImmediateOperand(intrinEmbMask.Id))
        {
            var immOp = embMaskOp.AsHWIntrinsic().GetOp(intrinEmbMask.NumOperands);
            assert(immOp.IsContained == (immOp.RegNum == REG_NA));
            if (intrinEmbMask.Id == NI_Sve_MultiplyAddRotateComplex && targetReg != embMaskOp1Reg)
            {
                if (targetReg == embMaskOp2Reg)
                {
                    Emitter.emitInsSve_Mov(INS_sve_mov, EA_SCALABLE, tempReg, embMaskOp2Reg, true, opt,
                        INS_SVE_MOV_OPTS_UNPRED);
                    embMaskOp2Reg = tempReg;
                    if (embMaskOp3Reg == targetReg)
                    {
                        embMaskOp3Reg = tempReg;
                    }
                }
                else if (targetReg == embMaskOp3Reg)
                {
                    Emitter.emitInsSve_Mov(INS_sve_mov, EA_SCALABLE, tempReg, embMaskOp3Reg, true, opt,
                        INS_SVE_MOV_OPTS_UNPRED);
                    embMaskOp3Reg = tempReg;
                }
            }
            var numInstrs = mopt != INS_SVE_MOV_OPTS_UNPRED || targetReg != embMaskOp1Reg ? 2 : 1;
            var helper = new HWIntrinsicImmOpHelper(this, immOp, embMaskOp.AsHWIntrinsic(), numInstrs);
            for (helper.EmitBegin(); !helper.Done; helper.EmitCaseEnd())
            {
                nint imm = helper.ImmValue;
                switch (intrinEmbMask.NumOperands)
                {
                    case 2:
                    {
                        Emitter.emitInsSve_R_R_R_I(insEmbMask, emitSize, targetReg, maskReg,
                            embMaskOp1Reg, imm, embOpt, sopt, mopt);
                        break;
                    }
                    case 3:
                    {
                        Emitter.emitInsSve_R_R_R_R_I(insEmbMask, emitSize, targetReg, maskReg,
                            embMaskOp1Reg, embMaskOp2Reg, imm, embOpt, sopt, mopt);
                        break;
                    }
                    case 4:
                    {
                        Arm64IntrinsicEmitFiveRegistersImmediate(insEmbMask, emitSize, targetReg, maskReg,
                            embMaskOp1Reg, embMaskOp2Reg, embMaskOp3Reg, imm, embOpt, sopt, mopt);
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
            switch (intrinEmbMask.NumOperands)
            {
                case 2:
                {
                    Emitter.emitInsSve_R_R_R_R(insEmbMask, emitSize, targetReg, maskReg,
                        embMaskOp1Reg, embMaskOp2Reg, embOpt, sopt, mopt);
                    break;
                }
                case 3:
                {
                    Arm64IntrinsicEmitFiveRegisters(insEmbMask, emitSize, targetReg, maskReg,
                        embMaskOp1Reg, embMaskOp2Reg, embMaskOp3Reg, embOpt, sopt, mopt);
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
}
#endif
