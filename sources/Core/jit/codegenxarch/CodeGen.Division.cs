// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForDivMod(GenTreeOp tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Integer division generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_DIV or GT_UDIV or GT_MOD or GT_UMOD);
        var dividend = tree.Op1;
        var divisor = tree.Op2;
        var oper = tree.Oper;
        var size = tree.Type.EmitSize;
        var targetReg = tree.RegNum;
        var targetType = tree.Type;
        assert(varTypeIsIntOrI(targetType));
        assert(dividend.IsUsedFromReg);

        genConsumeOperands(tree);
        genCopyRegIfNeeded(dividend, REG_RAX);

        // DIV/IDIV read RDX:RAX, so prepare the high half of the dividend.
        if ((oper is GT_UMOD or GT_UDIV) ||
            (dividend.Oper.IsIntegralConst && (dividend.AsIntConCommon().IconValue > 0)))
        {
            instGen_Set_Reg_To_Zero(EA_PTRSIZE, REG_RDX);
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
            inst_Mov(targetType, targetReg, REG_RAX, canSkip: true);
        }
        else
        {
            assert(oper is GT_MOD or GT_UMOD);
            inst_Mov(targetType, targetReg, REG_RDX, canSkip: true);
        }

        genProduceReg(tree);
#endif
    }
}
#endif
