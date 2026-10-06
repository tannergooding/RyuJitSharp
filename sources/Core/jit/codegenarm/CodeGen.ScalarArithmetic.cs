// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForDivMod(GenTreeOp tree)
    {
        assert(tree.Oper is GT_DIV or GT_UDIV or GT_MOD or GT_UMOD);
        noway_assert((tree.Oper is GT_DIV) || !varTypeIsFloating(tree.Type));

#if USE_HELPERS_FOR_INT_DIV
        noway_assert(!varTypeIsIntOrI(tree));
#endif

        var targetType = tree.Type;
        var targetReg = tree.RegNum;
        genConsumeOperands(tree);
        noway_assert(targetReg != REG_NA);

        var dividend = tree.Op1;
        var divisor = tree.Op2;
        var ins = genGetInsForOper(tree.Oper, targetType);
        var size = targetType.EmitSize;

        assert(!tree.IsContained);
        assert(!dividend.IsContained || !divisor.IsContained);

        if (varTypeIsFloating(targetType))
        {
            Emitter.emitIns_R_R_R(ins, size, targetReg, dividend.RegNum, divisor.RegNum);
        }
        else
        {
            var exceptionFlags = tree.Exceptions(_compiler);
            if ((exceptionFlags & ExceptionSetFlags.DivideByZeroException) != ExceptionSetFlags.None)
            {
                if (divisor.IsIntegralConst(0))
                {
                    genJumpToThrowHlpBlk(emitJumpKind.EJ_jmp, SCK_DIV_BY_ZERO);
                    genProduceReg(tree);
                    return;
                }
                else
                {
                    Emitter.emitIns_R_I(INS_cmp, size, divisor.RegNum, 0);
                    genJumpToThrowHlpBlk(emitJumpKind.EJ_eq, SCK_DIV_BY_ZERO);
                }
            }

            if ((exceptionFlags & ExceptionSetFlags.ArithmeticException) != ExceptionSetFlags.None)
            {
                genCodeForDivModOverflowCheck(tree);
            }

            Emitter.emitIns_R_R_R(ins, size, targetReg, dividend.RegNum, divisor.RegNum);
        }

        genProduceReg(tree);
    }

    private void genCodeForDivModOverflowCheck(GenTreeOp tree)
    {
        assert(tree.Oper is GT_DIV);
        assert(!tree.Op2.IsIntegralConst(0));

        var size = tree.Type.EmitActualSize;
        var divisorReg = tree.Op2.RegNum;
        var dividendReg = tree.Op1.RegNum;
        var sdivLabel = genCreateTempLabel();

        Emitter.emitIns_R_I(INS_cmp, size, divisorReg, -1);
        inst_JMP(emitJumpKind.EJ_ne, sdivLabel);
        Emitter.emitIns_R_I(INS_cmp, size, dividendReg, 1);
        genJumpToThrowHlpBlk(emitJumpKind.EJ_vs, SCK_ARITH_EXCPN);
        genDefineTempLabel(sdivLabel);
    }
}
#endif
