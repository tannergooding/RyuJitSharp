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
