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
        var ins = genGetInsForOper(tree);

        assert(!tree.IsContained);
        assert(targetReg != REG_NA);

        var operand = tree.Op1;
        assert(!operand.IsContained);
        var operandReg = genConsumeReg(operand);
        var attr = emitActualTypeSize(tree);
        Emitter.emitIns_R_R(ins, attr, targetReg, operandReg);

        genProduceReg(tree);
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
        Emitter.emitIns_R_R_I(INS_sltiu, attr, targetReg, operandReg, unchecked((nint)-1));
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
