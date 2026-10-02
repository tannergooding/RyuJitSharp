// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForShiftLong(GenTree tree)
    {
        // Only the non-RMW case here.
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

        var regResult = (oper is GT_LSH_HI) ? regHi : regLo;
        inst_Mov(targetType, tree.RegNum, regResult, canSkip: true);

        if (oper is GT_LSH_HI)
        {
            inst_RV_SH(ins, EA_4BYTE, tree.RegNum, count);
            Emitter.emitIns_R_R_R_I(
                INS_orr,
                EA_4BYTE,
                tree.RegNum,
                tree.RegNum,
                regLo,
                unchecked((int)(32u - count)),
                INS_FLAGS_DONT_CARE,
                INS_OPTS_LSR);
        }
        else
        {
            assert(oper is GT_RSH_LO);
            inst_RV_SH(INS_lsr, EA_4BYTE, tree.RegNum, count);
            Emitter.emitIns_R_R_R_I(
                INS_orr,
                EA_4BYTE,
                tree.RegNum,
                tree.RegNum,
                regHi,
                unchecked((int)(32u - count)),
                INS_FLAGS_DONT_CARE,
                INS_OPTS_LSL);
        }

        genProduceReg(tree);
    }
}
#endif
