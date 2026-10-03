// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForNegNot(GenTreeUnOp tree)
    {
        assert(tree.Oper is GT_NEG or GT_NOT);

        var targetType = tree.Type;
        assert(tree.Oper is not GT_NOT || !varTypeIsFloating(targetType));

        var targetReg = tree.RegNum;
        assert(!tree.IsContained);
        assert(targetReg != REG_NA);

        var operand = tree.Op1;
        assert(!operand.IsContained);
        var operandReg = genConsumeReg(operand);
        var attr = emitActualTypeSize(tree);

        if (tree.Oper is GT_NEG)
        {
            if (varTypeIsFloating(targetType))
            {
                Emitter.emitIns_R_R_R(targetType == TYP_DOUBLE ? INS_fsgnjn_d : INS_fsgnjn_s,
                    attr, targetReg, operandReg, operandReg);
            }
            else
            {
                Emitter.emitIns_R_R_R(attr == EA_4BYTE ? INS_subw : INS_sub,
                    attr, targetReg, REG_R0, operandReg);
            }
        }
        else if (tree.Oper is GT_NOT)
        {
            assert(!varTypeIsFloating(targetType));
            Emitter.emitIns_R_R(INS_not, attr, targetReg, operandReg);
        }

        genProduceReg(tree);
    }

    public void genCodeForBswap(GenTree tree)
    {
        assert(tree.Oper is GT_BSWAP or GT_BSWAP16);

        var attr = emitActualTypeSize(tree);
        var targetReg = tree.RegNum;
        var operand = tree.AsUnOp().Op1;
        assert(!operand.IsContained);
        var operandReg = genConsumeReg(operand);

        instruction ins;
        if (tree.Oper is GT_BSWAP16)
        {
            ins = INS_revb_4h;
        }
        else if (attr == EA_8BYTE)
        {
            ins = INS_revb_d;
        }
        else
        {
            assert(attr == EA_4BYTE);
            ins = INS_revb_2w;
        }

        Emitter.emitIns_R_R(ins, attr, targetReg, operandReg);

        if (tree.Oper is GT_BSWAP16 && !genCanOmitNormalizationForBswap16(tree))
        {
            Emitter.emitIns_R_R_I_I(INS_bstrpick_d, EA_8BYTE, targetReg, targetReg, 15, 0);
        }

        genProduceReg(tree);
    }

    public void genCodeForDivMod(GenTreeOp tree)
    {
        assert(tree.Oper is GT_MOD or GT_UMOD or GT_DIV or GT_UDIV);

        var targetType = tree.Type;
        var emit = Emitter;

        genConsumeOperands(tree);

        if (varTypeIsFloating(targetType))
        {
            assert(varTypeIsFloating(tree.Op1.Type));
            assert(varTypeIsFloating(tree.Op2.Type));
            assert(tree.Oper is GT_DIV);

            var ins = genGetInsForOper(tree);
            emit.emitIns_R_R_R(ins, targetType.EmitActualSize, tree.RegNum,
                tree.Op1.RegNum, tree.Op2.RegNum);
        }
        else
        {
            var dividend = tree.Op1;
            var divisor = tree.Op2;
            var dividendReg = dividend.RegNum;
            var divisorReg = divisor.RegNum;

            assert(!dividend.IsContained && !dividend.IsContainedIntOrIImmed);
            assert(!divisor.IsContained || divisor.IsContainedIntOrIImmed);

            var exceptions = tree.Exceptions(_compiler);
            if ((exceptions & ExceptionSetFlags.DivideByZeroException) is not ExceptionSetFlags.None)
            {
                if (divisor.IsIntegralConst(0) || (divisorReg == REG_R0))
                {
                    genJumpToThrowHlpBlk(EJ_jmp, SCK_DIV_BY_ZERO);
                    genProduceReg(tree);
                    return;
                }
                else
                {
                    assert(Emitter.isGeneralRegister(divisorReg));
                    genJumpToThrowHlpBlk_la(SCK_DIV_BY_ZERO, INS_beq, divisorReg);
                }
            }

            var size = tree.Type.EmitActualSize;
            var sizeCode = EA_SIZE(size);

            assert(!divisor.IsIntegralConst(0));

            instruction ins;
            if (divisor.IsContainedIntOrIImmed)
            {
                var immediate = unchecked((nint)(int)divisor.AsIntCon().IconValue);
                divisorReg = Emitter.isGeneralRegister(divisorReg) ? divisorReg : REG_R21;
                emit.emitIns_I_la(EA_PTRSIZE, divisorReg, immediate);
            }
            else
            {
                assert(!dividend.IsContained);
                assert(Emitter.isGeneralRegister(dividendReg));
                assert(Emitter.isGeneralRegister(divisorReg));
            }

            var is4 = sizeCode == EA_4BYTE;
            assert(is4 || sizeCode == EA_8BYTE);

            if (tree.Oper is GT_DIV or GT_MOD)
            {
                if ((exceptions & ExceptionSetFlags.ArithmeticException) is not ExceptionSetFlags.None)
                {
                    emit.emitIns_R_R_I(INS_addi_d, EA_PTRSIZE, REG_R21, REG_R0, -1);
                    var sdivLabel = genCreateTempLabel();
                    emit.emitIns_J_cond_la(INS_bne, sdivLabel, REG_R21, divisorReg);

                    var checkedDividendReg = dividend.RegNum;
                    if (is4)
                    {
                        emit.emitIns_R_R_I(INS_slli_w, EA_4BYTE, REG_R21, REG_R21, 31);
                    }
                    else
                    {
                        assert(sizeCode == EA_8BYTE);
                        emit.emitIns_R_R_I(INS_slli_d, EA_8BYTE, REG_R21, REG_R21, 63);
                    }

                    genJumpToThrowHlpBlk_la(SCK_ARITH_EXCPN, INS_beq, REG_R21, null,
                        checkedDividendReg);
                    genDefineTempLabel(sdivLabel);
                }

                if (tree.Oper is GT_DIV)
                {
                    ins = is4 ? INS_div_w : INS_div_d;
                }
                else
                {
                    ins = is4 ? INS_mod_w : INS_mod_d;
                }

                emit.emitIns_R_R_R(ins, size, tree.RegNum, dividendReg, divisorReg);
            }
            else
            {
                if (is4)
                {
                    ins = tree.Oper is GT_UDIV ? INS_div_wu : INS_mod_wu;
                    // TODO-LOONGARCH64: here is just for signed-extension ?
                    emit.emitIns_R_R_I(INS_slli_w, EA_4BYTE, dividendReg, dividendReg, 0);
                    emit.emitIns_R_R_I(INS_slli_w, EA_4BYTE, divisorReg, divisorReg, 0);
                }
                else
                {
                    ins = tree.Oper is GT_UDIV ? INS_div_du : INS_mod_du;
                }

                emit.emitIns_R_R_R(ins, size, tree.RegNum, dividendReg, divisorReg);
            }
        }

        genProduceReg(tree);
    }

    public void genJumpToThrowHlpBlk_la(SpecialCodeKind codeKind, instruction ins, regNumber reg1,
        BasicBlock? failBlk = null, regNumber reg2 = REG_R0)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 conditional exception-branch generation is not ported.");
    }

    public void genCodeForIncSaturate(GenTree tree)
    {
        var targetReg = tree.RegNum;
        assert(!tree.IsContained);
        assert(targetReg != REG_NA);

        var operand = tree.AsUnOp().Op1;
        assert(!operand.IsContained);
        var operandReg = genConsumeReg(operand);

        Emitter.emitIns_R_R_I(INS_addi_d, tree.Type.EmitActualSize, targetReg, operandReg, 1);
        // Only a wrapped increment is zero; invert that result to saturate at all ones.
        Emitter.emitIns_R_R_I(INS_bne, tree.Type.EmitActualSize, targetReg, REG_R0, 8);
        Emitter.emitIns_R_R_R(INS_orn, tree.Type.EmitActualSize, targetReg, REG_R0, targetReg);

        genProduceReg(tree);
    }

    public void genCodeForMulHi(GenTreeOp treeNode)
    {
        assert(!treeNode.HasOverflowCheckEx);

        genConsumeOperands(treeNode);

        var targetReg = treeNode.RegNum;
        var targetType = treeNode.Type;
        var emit = Emitter;
        var attr = treeNode.Type.EmitActualSize;
        var isUnsigned = treeNode.IsUnsigned;

        var op1 = treeNode.Op1;
        var op2 = treeNode.Op2;

        assert(!varTypeIsFloating(targetType));
        assert(!op1.IsContained);
        assert(!op2.IsContained);
        assert(targetReg != REG_NA);

        if (EA_SIZE(attr) == EA_8BYTE)
        {
            var ins = isUnsigned ? INS_mulh_du : INS_mulh_d;
            emit.emitIns_R_R_R(ins, attr, targetReg, op1.RegNum, op2.RegNum);
        }
        else
        {
            assert(EA_SIZE(attr) == EA_4BYTE);

            var ins = isUnsigned ? INS_mulh_wu : INS_mulh_w;
            emit.emitIns_R_R_R(ins, attr, targetReg, op1.RegNum, op2.RegNum);
        }

        genProduceReg(treeNode);
    }
}
#endif
