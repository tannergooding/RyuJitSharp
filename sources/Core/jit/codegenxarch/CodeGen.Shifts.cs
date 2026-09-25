// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForShift(GenTree tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Shift node generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper.IsShiftOrRotate);
        assert(tree.RegNum != REG_NA);
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
            assert(tree.Oper.IsRotate || (operandReg != REG_NA));
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
                Emitter.emitIns_R_ARX(INS_lea, size, tree.RegNum, REG_NA, operandReg, 4, 0);
            }
            else if (mightOptimizeLsh && shiftBy.IsIntegralConst(3) && (tree.RegNum != operandReg))
            {
                Emitter.emitIns_R_ARX(INS_lea, size, tree.RegNum, REG_NA, operandReg, 8, 0);
            }
            else
            {
                var value = unchecked((int)shiftBy.AsIntConCommon().IconValue);
                if (tree.Oper.IsRotate && _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2) && !setFlags)
                {
                    // RORX handles contained sources and avoids MOV+REX overhead
                    // for distinct-register 64-bit rotates.
                    if ((operandReg == REG_NA) || (varTypeIsLong(targetType) && (tree.RegNum != operandReg)))
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
            ins = tree.Oper switch
            {
                GT_LSH => INS_shlx,
                GT_RSH => INS_sarx,
                GT_RSZ => INS_shrx,
                _ => throw new FatalJitException("Invalid shift operator."),
            };

            // BMI2 encodes the count before the shifted source.
            inst_RV_RV_TT(ins, size, tree.RegNum, shiftBy.RegNum, operand, isRMW: false, INS_OPTS_NONE);
        }
        else
        {
            genCopyRegIfNeeded(shiftBy, REG_RCX);
            noway_assert(operandReg != REG_RCX);
            Emitter.emitIns_BASE_R_R(ins, size, tree.RegNum, operandReg);
        }

        genProduceReg(tree);
#endif
    }

    public static instruction genMapShiftInsToShiftByConstantIns(instruction ins, int value)
    {
        assert(ins is INS_rcl or INS_rcr or INS_rol or INS_ror or INS_shl or INS_shr or INS_sar);
        instruction result;
        if (value == 1)
        {
            assert(INS_rcl + 1 == INS_rcl_1);
            assert(INS_rcr + 1 == INS_rcr_1);
            assert(INS_rol + 1 == INS_rol_1);
            assert(INS_ror + 1 == INS_ror_1);
            assert(INS_shl + 1 == INS_shl_1);
            assert(INS_shr + 1 == INS_shr_1);
            assert(INS_sar + 1 == INS_sar_1);
            result = ins + 1;
        }
        else
        {
            assert(INS_rcl + 2 == INS_rcl_N);
            assert(INS_rcr + 2 == INS_rcr_N);
            assert(INS_rol + 2 == INS_rol_N);
            assert(INS_ror + 2 == INS_ror_N);
            assert(INS_shl + 2 == INS_shl_N);
            assert(INS_shr + 2 == INS_shr_N);
            assert(INS_sar + 2 == INS_sar_N);
            result = ins + 2;
        }

        return result;
    }

    public void genCodeForShiftRMW(GenTreeStoreInd storeInd)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Memory read-modify-write shift generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var data = storeInd.Data;
        assert(data.Oper.IsShiftOrRotate);
        assert(data.AsOp().Op1.IsUsedFromMemory);
        assert(data.AsOp().Op1.Oper.IsIndir);
        assert(Lowering.IndirsAreEquivalent(data.AsOp().Op1, storeInd));
        assert(data.RegNum == REG_NA);

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
            genCopyRegIfNeeded(shiftBy, REG_RCX);
            Emitter.emitInsRMW(ins, attr, storeInd);
        }
#endif
    }
}
#endif
