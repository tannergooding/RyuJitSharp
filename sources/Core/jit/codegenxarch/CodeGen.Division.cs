// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForDivModXarch(GenTreeOp tree)
    {
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_DIV or GT_UDIV or GT_MOD or GT_UMOD);
        var dividend = tree.Op1;
        if (Configuration.TARGET_X86 && varTypeIsLong(dividend.Type))
        {
            genCodeForLongUMod(tree);
            return;
        }
        var divisor = tree.Op2;
        var oper = tree.Oper;
        var size = tree.Type.EmitSize;
        var targetReg = tree.RegNum;
        var targetType = tree.Type;
        assert(varTypeIsIntOrI(targetType));
        assert(dividend.IsUsedFromReg);

        genConsumeOperands(tree);
        genCopyRegIfNeeded(dividend, REG_X64_RAX);

        // DIV/IDIV read RDX:RAX, so prepare the high half of the dividend.
        if ((oper is GT_UMOD or GT_UDIV) ||
            (dividend.Oper.IsIntegralConst && (dividend.AsIntConCommon().IconValue > 0)))
        {
            instGen_Set_Reg_To_Zero(EA_PTRSIZE, REG_X64_RDX);
        }
        else
        {
            Emitter.emitIns(INS_cdq, size);
            _gcInfo.gcMarkRegSetNpt(RBM_RDX);
        }

        var ins = oper is GT_UMOD or GT_UDIV ? INS_div : INS_idiv;
        _ = Emitter.emitInsBinary(ins, size, tree, divisor);

        // The quotient is in RAX and the remainder is in RDX.
        if (oper is GT_DIV or GT_UDIV)
        {
            inst_Mov(targetType, targetReg, REG_X64_RAX, canSkip: true);
        }
        else
        {
            assert(oper is GT_MOD or GT_UMOD);
            inst_Mov(targetType, targetReg, REG_X64_RDX, canSkip: true);
        }

        genProduceReg(tree);
    }
}
