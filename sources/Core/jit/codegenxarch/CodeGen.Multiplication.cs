// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForMul(GenTreeOp tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Integer multiplication generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_MUL);
        var targetReg = tree.RegNum;
        var targetType = tree.Type;
        assert(varTypeIsIntOrI(targetType));
        var size = tree.Type.EmitSize;
        var isUnsigned = (tree.Flags & GTF_UNSIGNED) != 0;
        var requiresOverflowCheck = tree.HasOverflowCheckEx;
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        genConsumeOperands(tree);

        GenTree? immediate = null;
        var rmOp = op1;
        if (op2.IsContainedIntOrIImmed)
        {
            immediate = op2;
        }
        else if (op1.IsContainedIntOrIImmed)
        {
            immediate = op1;
            rmOp = op2;
        }

        if (immediate is not null)
        {
            var value = immediate.AsIntConCommon().IconValue;
            if (!requiresOverflowCheck && rmOp.IsUsedFromReg && (value is 3 or 5 or 9))
            {
                // x + x * (constant - 1) encodes these three products in one LEA.
                var scale = (uint)(value - 1);
                Emitter.emitIns_R_ARX(INS_lea, size, targetReg, rmOp.RegNum, rmOp.RegNum, scale, 0);
            }
            else
            {
                // The three-operand destination is encoded in the opcode.
                var ins = RyuJitSharp.Emitter.inst3opImulForReg(targetReg);
                _ = Emitter.emitInsBinary(ins, size, rmOp, immediate);
            }
        }
        else
        {
            var regOp = op1;
            rmOp = op2;
            var mulTargetReg = targetReg;
            var ins = INS_imul;
            if (isUnsigned && requiresOverflowCheck)
            {
                ins = INS_mulEAX;
                mulTargetReg = REG_RAX;
            }

            if (op1.IsUsedFromMemory || (op2.IsUsedFromReg && (op2.RegNum == mulTargetReg)))
            {
                regOp = op2;
                rmOp = op1;
            }
            assert(regOp.IsUsedFromReg);
            _ = Emitter.emitIns_BASE_R_R_RM(ins, size, mulTargetReg, tree, regOp, rmOp);

            if (ins == INS_mulEAX)
            {
                inst_Mov(targetType, targetReg, REG_RAX, canSkip: true);
            }
        }

        if (requiresOverflowCheck)
        {
            noway_assert(!varTypeIsFloating(targetType));
            genCheckOverflow(tree);
        }

        genProduceReg(tree);
#endif
    }

    public void genCodeForMulHi(GenTreeOp tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "High-half multiplication generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(!tree.HasOverflowCheckEx);
        var targetReg = tree.RegNum;
        var targetType = tree.Type;
        var size = tree.Type.EmitSize;
        var op1 = tree.Op1;
        var op2 = tree.Op2;

        // The high half requires RDX:RAX = RAX * rm, or BMI2 MULX.
        // The ordinary three-operand form does not retain the high result.
        genConsumeOperands(tree);

        var regOp = op1;
        var rmOp = op2;
        if (op1.IsUsedFromMemory)
        {
            regOp = op2;
            rmOp = op1;
        }
        assert(regOp.IsUsedFromReg);

        if (((tree.Flags & GTF_UNSIGNED) != 0) && _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2))
        {
            if (rmOp.IsUsedFromReg && (rmOp.RegNum == REG_RDX))
            {
                (regOp, rmOp) = (rmOp, regOp);
            }
            inst_Mov(targetType, REG_RDX, regOp.RegNum, canSkip: true);

            if (tree.Oper is GT_MULHI)
            {
                // Aliasing both MULX outputs retains only the high half.
                inst_RV_RV_TT(INS_mulx, size, targetReg, targetReg, rmOp, isRMW: false, INS_OPTS_NONE);
            }
            else
            {
                assert(false);
            }
        }
        else
        {
            if (rmOp.IsUsedFromReg && (rmOp.RegNum == REG_RAX))
            {
                (regOp, rmOp) = (rmOp, regOp);
            }
            inst_Mov(targetType, REG_RAX, regOp.RegNum, canSkip: true);

            var ins = (tree.Flags & GTF_UNSIGNED) != 0 ? INS_mulEAX : INS_imulEAX;
            _ = Emitter.emitInsBinary(ins, size, tree, rmOp);
            if (tree.Oper is GT_MULHI)
            {
                inst_Mov(targetType, targetReg, REG_RDX, canSkip: true);
            }
        }

        genProduceReg(tree);
#endif
    }
}
#endif
