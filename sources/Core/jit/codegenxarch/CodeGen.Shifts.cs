// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForShiftXarch(GenTree tree)
    {
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        assert(tree.Oper.IsShiftOrRotate);
        assert(tree.RegNum != regNumberExtensions.NotAvailable);
        assert(tree.AsOp().Op1.IsUsedFromReg || _compiler.compIsaSupportedDebugOnly(InstructionSet_AVX2));
        genConsumeOperands(tree.AsOp());

        var targetType = tree.Type;
        var ins = genGetInsForOper(tree.Oper, targetType);
        var operand = tree.AsOp().Op1;
        var operandReg = operand.RegNum;
        var shiftBy = tree.AsOp().Op2;
        var size = tree.Type.EmitSize;
        var setFlags = (tree.Flags & GTF_SET_FLAGS) != 0;

        if (shiftBy.IsContainedIntOrIImmed)
        {
            assert(tree.Oper.IsRotate || (operandReg != regNumberExtensions.NotAvailable));
            var mightOptimizeLsh = (tree.Oper is GT_LSH) && !setFlags;
            if (mightOptimizeLsh && shiftBy.IsIntegralConst(1))
            {
                if (tree.RegNum == operandReg)
                {
                    Emitter.emitIns_R_R(INS_add, size, tree.RegNum, operandReg);
                }
                else
                {
                    Emitter.emitIns_R_ARX(INS_lea, size, tree.RegNum, operandReg, operandReg, 1, 0);
                }
            }
            else if (mightOptimizeLsh && shiftBy.IsIntegralConst(2) && (tree.RegNum != operandReg))
            {
                Emitter.emitIns_R_ARX(INS_lea, size, tree.RegNum, regNumberExtensions.NotAvailable, operandReg, 4, 0);
            }
            else if (mightOptimizeLsh && shiftBy.IsIntegralConst(3) && (tree.RegNum != operandReg))
            {
                Emitter.emitIns_R_ARX(INS_lea, size, tree.RegNum, regNumberExtensions.NotAvailable, operandReg, 8, 0);
            }
            else
            {
                var value = unchecked((int)shiftBy.AsIntConCommon().IconValue);
                if (tree.Oper.IsRotate && _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2) && !setFlags)
                {
                    // RORX handles contained sources and avoids MOV+REX overhead
                    // for distinct-register 64-bit rotates.
                    if ((operandReg == regNumberExtensions.NotAvailable) || (varTypeIsLong(targetType) && (tree.RegNum != operandReg)))
                    {
                        if (tree.Oper is GT_ROL)
                        {
                            value &= ((int)size * BITS_PER_BYTE) - 1;
                            value = ((int)size * BITS_PER_BYTE) - value;
                        }

                        inst_RV_TT_IV(INS_rorx, size, tree.RegNum, operand, value, INS_OPTS_NONE);
                        genProduceReg(tree);
                        return;
                    }
                }

                ins = genMapShiftInsToShiftByConstantIns(ins, value);
                Emitter.emitIns_BASE_R_R_I(ins, size, tree.RegNum, operandReg, value);
                genProduceReg(tree);
                return;
            }
        }
        else if (tree.Oper.IsShift && _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2) && !setFlags)
        {
            switch (tree.Oper)
            {
                case GT_LSH:
                {
                    ins = INS_shlx;
                    break;
                }

                case GT_RSH:
                {
                    ins = INS_sarx;
                    break;
                }

                case GT_RSZ:
                {
                    ins = INS_shrx;
                    break;
                }

                default:
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage098
                    );
                    break;
                }
            }

            // BMI2 encodes the count before the shifted source.
            inst_RV_RV_TT(ins, size, tree.RegNum, shiftBy.RegNum, operand, isRMW: false, INS_OPTS_NONE);
        }
        else
        {
            genCopyRegIfNeeded(shiftBy, REG_X64_RCX);
            noway_assert(operandReg != REG_X64_RCX);
            Emitter.emitIns_BASE_R_R(ins, size, tree.RegNum, operandReg);
        }

        genProduceReg(tree);
    }

    public void genCodeForShiftLongXarch(GenTree tree)
    {
        if (!Configuration.TARGET_X86)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Long shift generation requires x86.");
            return;
        }

        var oper = tree.Oper;
        assert(oper is GT_LSH_HI or GT_RSH_LO);

        var operand = tree.AsOp().Op1;
        assert(operand.Oper is GT_LONG);
        assert(operand.AsOp().Op1.IsUsedFromReg);
        assert(operand.AsOp().Op2.IsUsedFromReg);

        var operandLo = operand.AsOp().Op1;
        var operandHi = operand.AsOp().Op2;
        var regLo = operandLo.RegNum;
        var regHi = operandHi.RegNum;

        genConsumeOperands(tree.AsOp());

        var targetType = tree.Type;
        var ins = genGetInsForOper(oper, targetType);
        var shiftBy = tree.AsOp().Op2;
        assert(shiftBy.IsContainedIntOrIImmed);
        var count = unchecked((uint)shiftBy.AsIntConCommon().IconValue);
        var targetReg = tree.RegNum;

        if (oper is GT_LSH_HI)
        {
            assert(regLo != targetReg);
            inst_Mov(targetType, targetReg, regHi, canSkip: true);
            inst_RV_RV_IV(ins, targetType.EmitSize, targetReg, regLo, count);
        }
        else
        {
            assert(regHi != targetReg);
            inst_Mov(targetType, targetReg, regLo, canSkip: true);
            inst_RV_RV_IV(ins, targetType.EmitSize, targetReg, regHi, count);
        }

        genProduceReg(tree);
    }

    public static instruction genMapShiftInsToShiftByConstantIns(instruction ins, int value)
    {
        assert(ins is INS_rcl or INS_rcr or INS_rol or INS_ror or INS_shl or INS_shr or INS_sar);
        instruction result;
        if (value == 1)
        {
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rcl) + 1 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rcl_1));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rcr) + 1 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rcr_1));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rol) + 1 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rol_1));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_ror) + 1 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_ror_1));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_shl) + 1 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_shl_1));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_shr) + 1 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_shr_1));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_sar) + 1 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_sar_1));
            result = InstructionOrdinalMaps.GetInstructionFromNativeOrdinal(
                InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) + 1);
        }
        else
        {
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rcl) + 2 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rcl_N));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rcr) + 2 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rcr_N));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rol) + 2 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_rol_N));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_ror) + 2 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_ror_N));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_shl) + 2 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_shl_N));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_shr) + 2 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_shr_N));
            assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_sar) + 2 ==
                   InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_sar_N));
            result = InstructionOrdinalMaps.GetInstructionFromNativeOrdinal(
                InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) + 2);
        }

        return result;
    }

    public void genCodeForShiftRMW(GenTreeStoreInd storeInd)
    {
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        var data = storeInd.Data;
        assert(data.Oper.IsShiftOrRotate);
        assert(data.AsOp().Op1.IsUsedFromMemory);
        assert(data.AsOp().Op1.Oper.IsIndir);
        assert(Lowering.IndirsAreEquivalent(data.AsOp().Op1, storeInd));
        assert(data.RegNum == regNumberExtensions.NotAvailable);

        var ins = genGetInsForOper(data.Oper, data.Type);
        var attr = (emitAttr)data.Type.Size;
        var shiftBy = data.AsOp().Op2;
        if (shiftBy.IsContainedIntOrIImmed)
        {
            var value = unchecked((int)shiftBy.AsIntConCommon().IconValue);
            ins = genMapShiftInsToShiftByConstantIns(ins, value);
            if (value == 1)
            {
                Emitter.emitInsRMW(ins, attr, storeInd);
            }
            else
            {
                Emitter.emitInsRMW(ins, attr, storeInd, shiftBy);
            }
        }
        else
        {
            genCopyRegIfNeeded(shiftBy, REG_X64_RCX);
            Emitter.emitInsRMW(ins, attr, storeInd);
        }
    }
}
