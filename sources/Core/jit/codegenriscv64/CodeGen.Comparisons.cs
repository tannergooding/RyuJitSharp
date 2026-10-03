// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForCompare(GenTreeOp tree)
    {
        assert(tree.Oper.IsCompare);

        var op1 = tree.Op1;
        var op2 = tree.Op2;
        var op1Type = genActualType(op1.Type);

        assert(!op1.IsUsedFromMemory);
        assert(!op2.IsUsedFromMemory);

        var cmpSize = EA_SIZE(op1Type.EmitSize);
        assert(cmpSize is EA_4BYTE or EA_8BYTE);

        var emit = Emitter;
        var targetReg = tree.RegNum;

        assert(targetReg != REG_NA);
        assert(!tree.TypeIs(TYP_VOID));

        var isReversed = false;
        if (varTypeIsFloating(op1Type))
        {
            assert(!op1.IsContainedIntOrIImmed && !op2.IsContainedIntOrIImmed);
            assert(op1.TypeIs(op2.Type));

            var oper = tree.Oper;
            isReversed = (tree.Flags & GTF_RELOP_NAN_UN) != 0;
            if (isReversed)
            {
                oper = oper.ReverseRelop;
            }

            if (oper is GT_GT or GT_GE)
            {
                oper = oper.SwapRelop;
                (op1, op2) = (op2, op1);
            }

            instruction instr = INS_none;
            switch (oper)
            {
                case GT_LT:
                {
                    instr = cmpSize == EA_4BYTE ? INS_flt_s : INS_flt_d;
                    break;
                }
                case GT_LE:
                {
                    instr = cmpSize == EA_4BYTE ? INS_fle_s : INS_fle_d;
                    break;
                }
                case GT_EQ:
                {
                    instr = cmpSize == EA_4BYTE ? INS_feq_s : INS_feq_d;
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }

            emit.emitIns_R_R_R(instr, cmpSize, targetReg, op1.RegNum, op2.RegNum);
        }
        else
        {
            var isUnsigned = tree.IsUnsigned;

            var oper = tree.Oper;
            if (oper is GT_EQ or GT_NE)
            {
                assert(op2.IsContainedIntOrIImmed && op2.IsIntegralConst(0));
                oper = oper is GT_EQ ? GT_LE : GT_GT;
                isUnsigned = true;
            }

            if ((oper is GT_LE) && op2.IsContainedIntOrIImmed)
            {
                oper = GT_LT;
                assert(op2.AsIntCon().IconValue == 0);
                op2.AsIntCon().IconValue = 1;
            }

            isReversed = oper is GT_LE or GT_GE;
            if (isReversed)
            {
                oper = oper.ReverseRelop;
            }

            if (oper is GT_GT)
            {
#if DEBUG
                oper = oper.SwapRelop;
#endif
                (op1, op2) = (op2, op1);
            }

            assert(oper is GT_LT);

            assert(!op1.IsContainedIntOrIImmed || op1.IsIntegralConst(0));
            var reg1 = op1.IsContainedIntOrIImmed ? REG_ZERO : op1.RegNum;
            if (op2.IsContainedIntOrIImmed)
            {
                var slti = isUnsigned ? INS_sltiu : INS_slti;
                emit.emitIns_R_R_I(slti, EA_PTRSIZE, targetReg, reg1, op2.AsIntCon().IconValue);
            }
            else
            {
                var slt = isUnsigned ? INS_sltu : INS_slt;
                emit.emitIns_R_R_R(slt, EA_PTRSIZE, targetReg, reg1, op2.RegNum);
            }
        }

        if (isReversed)
        {
            emit.emitIns_R_R_I(INS_xori, EA_8BYTE, targetReg, targetReg, 1);
        }

        genProduceReg(tree);
    }
}
#endif
