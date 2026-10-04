// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
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
        var attr = tree.Type.EmitActualSize;

        if (tree.Oper is GT_NEG)
        {
            if (varTypeIsFloating(targetType))
            {
                Emitter.emitIns_R_R_R(
                    targetType is TYP_DOUBLE ? INS_fsgnjn_d : INS_fsgnjn_s,
                    attr,
                    targetReg,
                    operandReg,
                    operandReg);
            }
            else
            {
                Emitter.emitIns_R_R_R(
                    attr is EA_4BYTE ? INS_subw : INS_sub,
                    attr,
                    targetReg,
                    REG_R0,
                    operandReg);
            }
        }
        else
        {
            assert(!varTypeIsFloating(targetType));
            Emitter.emitIns_R_R(INS_not, attr, targetReg, operandReg);
        }

        genProduceReg(tree);
    }

    public void genCodeForBswap(GenTree tree)
    {
        assert(tree.Oper is GT_BSWAP or GT_BSWAP16);

        var operand = tree.AsUnOp().Op1;
        var attr = operand.Type.EmitSize;
        var targetReg = tree.RegNum;
        var operandReg = genConsumeReg(operand);

        assert(_compiler.compOpportunisticallyDependsOn(InstructionSet_Zbb));
        Emitter.emitIns_R_R(INS_rev8, attr, targetReg, operandReg);

        if (attr < EA_PTRSIZE)
        {
            var shiftAmount = tree.Oper is GT_BSWAP16 ? 48 : 32;
            // TODO: we need to right-shift the byte-reversed register anyway. Remove the cast (in Lowering::LowerCast?)
            // wrapping GT_BSWAP16 and pass the exact destination type here, so that this codegen could leave the register
            // properly extended.
            Emitter.emitIns_R_R_I(INS_srli, attr, targetReg, targetReg, shiftAmount);
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

            assert(!divisor.IsIntegralConst(0));

            var tempReg = REG_NA;
            instruction ins;

            if (divisor.IsContainedIntOrIImmed)
            {
                var immediate = unchecked((nint)(int)divisor.AsIntCon().IconValue);
                if (!Emitter.isGeneralRegister(divisorReg))
                {
                    tempReg = _internalRegisters.GetSingle(tree);
                    divisorReg = tempReg;
                }

                emit.emitLoadImmediateAddress(EA_PTRSIZE, divisorReg, immediate);
            }
            else
            {
                assert(!dividend.IsContained);
                assert(Emitter.isGeneralRegister(dividendReg));
                assert(Emitter.isGeneralRegister(divisorReg));
            }

            var size = tree.Type.EmitActualSize;
            var sizeCode = EA_SIZE(size);
            var is4 = sizeCode == EA_4BYTE;
            assert(is4 || sizeCode == EA_8BYTE);

            if (tree.Oper is GT_DIV or GT_MOD)
            {
                if ((exceptions & ExceptionSetFlags.ArithmeticException) is not ExceptionSetFlags.None)
                {
                    if (tempReg == REG_NA)
                    {
                        tempReg = _internalRegisters.GetSingle(tree);
                    }

                    emit.emitIns_R_R_I(INS_addi, EA_PTRSIZE, tempReg, REG_R0, -1);
                    var sdivLabel = genCreateTempLabel();
                    emit.emitIns_J_cond_la(INS_bne, sdivLabel, tempReg, divisorReg);

                    var checkedDividendReg = dividend.RegNum;
                    var shiftIns = is4 ? INS_slliw : INS_slli;
                    var shiftBy = is4 ? 31 : 63;
                    emit.emitIns_R_R_I(shiftIns, size, tempReg, tempReg, shiftBy);

                    genJumpToThrowHlpBlk_la(SCK_ARITH_EXCPN, INS_beq, tempReg, null,
                        checkedDividendReg);
                    genDefineTempLabel(sdivLabel);
                }

                if (tree.Oper is GT_DIV)
                {
                    ins = is4 ? INS_divw : INS_div;
                }
                else
                {
                    ins = is4 ? INS_remw : INS_rem;
                }

                emit.emitIns_R_R_R(ins, size, tree.RegNum, dividendReg, divisorReg);
            }
            else
            {
                if (tree.Oper is GT_UDIV)
                {
                    ins = is4 ? INS_divuw : INS_divu;
                }
                else
                {
                    ins = is4 ? INS_remuw : INS_remu;
                }

                emit.emitIns_R_R_R(ins, size, tree.RegNum, dividendReg, divisorReg);
            }
        }

        genProduceReg(tree);
    }

    public void genJumpToThrowHlpBlk_la(SpecialCodeKind codeKind, instruction ins, regNumber reg1,
        BasicBlock? failBlk = null, regNumber reg2 = REG_R0)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 conditional exception-branch generation is not ported.");
    }

    public void genCodeForIncSaturate(GenTree tree)
    {
        var targetReg = tree.RegNum;
        assert(!tree.IsContained);
        assert(targetReg != REG_NA);

        var operand = tree.AsUnOp().Op1;
        assert(!operand.IsContained);
        var operandReg = genConsumeReg(operand);
        var attr = tree.Type.EmitActualSize;
        assert(EA_SIZE(attr) == EA_PTRSIZE);
        noway_assert(targetReg != operandReg, "lifetime of the operand register should have been extended");

        // sltiu sign-extends -1 to SIZE_T_MAX, yielding zero only for the maximum input.
        Emitter.emitIns_R_R_I(INS_sltiu, attr, targetReg, operandReg, unchecked((nint)(-1)));
        Emitter.emitIns_R_R_R(INS_add, attr, targetReg, operandReg, targetReg);

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
            var ins = isUnsigned ? INS_mulhu : INS_mulh;
            emit.emitIns_R_R_R(ins, attr, targetReg, op1.RegNum, op2.RegNum);
        }
        else
        {
            assert(EA_SIZE(attr) == EA_4BYTE);

            if (isUnsigned)
            {
                var tempReg = _internalRegisters.GetSingle(treeNode);
                // Moving each 32-bit operand into the high half makes mulhu return the high half of their product.
                emit.emitIns_R_R_I(INS_slli, EA_8BYTE, tempReg, op1.RegNum, 32);
                emit.emitIns_R_R_I(INS_slli, EA_8BYTE, targetReg, op2.RegNum, 32);
                emit.emitIns_R_R_R(INS_mulhu, EA_8BYTE, targetReg, tempReg, targetReg);
                emit.emitIns_R_R_I(INS_srai, attr, targetReg, targetReg, 32);
            }
            else
            {
                emit.emitIns_R_R_R(INS_mul, EA_8BYTE, targetReg, op1.RegNum, op2.RegNum);
                emit.emitIns_R_R_I(INS_srai, attr, targetReg, targetReg, 32);
            }
        }

        genProduceReg(treeNode);
    }
}
#endif
