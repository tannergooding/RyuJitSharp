// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForIncSaturate(GenTree tree)
    {
        var targetReg = tree.RegNum;
        assert(!tree.IsContained);
        assert(targetReg != REG_NA);

        var operand = tree.AsUnOp().Op1;
        assert(!operand.IsContained);
        var operandReg = genConsumeReg(operand);

        Emitter.emitIns_R_R_I(INS_addi_d, emitActualTypeSize(tree), targetReg, operandReg, 1);
        // Only a wrapped increment is zero; invert that result to saturate at all ones.
        Emitter.emitIns_R_R_I(INS_bne, emitActualTypeSize(tree), targetReg, REG_R0, 8);
        Emitter.emitIns_R_R_R(INS_orn, emitActualTypeSize(tree), targetReg, REG_R0, targetReg);

        genProduceReg(tree);
    }

    public void genCodeForMulHi(GenTreeOp treeNode)
    {
        assert(!treeNode.HasOverflowCheckEx);

        genConsumeOperands(treeNode);

        var targetReg = treeNode.RegNum;
        var targetType = treeNode.Type;
        var emit = Emitter;
        var attr = emitActualTypeSize(treeNode);
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
