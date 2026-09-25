// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForIncSaturate(GenTree tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Saturating increment generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var targetReg = tree.RegNum;
        var targetType = tree.Type;
        var operand = tree.AsUnOp().Op1;
        assert(operand.IsUsedFromReg);
        var operandReg = genConsumeReg(operand);

        inst_Mov(targetType, targetReg, operandReg, canSkip: true);
        inst_RV_IV(INS_add, targetReg, 1, targetType.EmitActualSize);
        inst_RV_IV(INS_sbb, targetReg, 0, targetType.EmitActualSize);
        genProduceReg(tree);
#endif
    }

    public void genCodeForBitOp(GenTreeOp tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Bit modification generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_BIT_SET or GT_BIT_CLEAR or GT_BIT_INVERT);
        var op1 = tree.Op1;
        var op2 = tree.Op2;
        genConsumeOperands(tree);
        var targetReg = tree.RegNum;
        var targetType = tree.Type.ActualType;
        assert(targetType is TYP_INT or TYP_LONG);
        assert(op1.IsUsedFromReg && op2.IsUsedFromReg);
        var ins = tree.Oper == GT_BIT_SET ? INS_bts : tree.Oper == GT_BIT_CLEAR ? INS_btr : INS_btc;

        // LSRA delays freeing the index unless it shares the value's interval.
        // Only that same-value case permits this move to overwrite the index.
        inst_Mov(targetType, targetReg, op1.RegNum, canSkip: true);
        Emitter.emitIns_R_R(ins, targetType.EmitSize, targetReg, op2.RegNum);
        genProduceReg(tree);
#endif
    }

    public void genCodeForPhysReg(GenTreePhysReg tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Physical register generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.Oper == GT_PHYSREG);
        inst_Mov(tree.Type, tree.RegNum, tree.SrcReg, canSkip: true);
        genTransferRegGCState(tree.RegNum, tree.SrcReg);
        genProduceReg(tree);
#endif
    }

    public void genCodeForCatchArg(GenTree tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Catch argument generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var block = _compiler.compCurBB;
        assert(block is not null);
        noway_assert(handlerGetsXcptnObj(block.CatchType));
        var exceptionMask = new regMaskTP(SRBM_EXCEPTION_OBJECT);
        noway_assert((_gcInfo.gcRegGCrefSetCur & exceptionMask).IsNonEmpty);
        inst_Mov(TYP_REF, tree.RegNum, REG_EXCEPTION_OBJECT, canSkip: true);
        if (tree.RegNum != REG_EXCEPTION_OBJECT)
        {
            _gcInfo.gcMarkRegSetNpt(exceptionMask);
        }
        genProduceReg(tree);
#endif
    }

    public void genCodeForReuseVal(GenTree tree)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Register value reuse generation requires Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        assert(tree.IsReuseRegVal);
#if FEATURE_MASKED_HW_INTRINSICS
        assert(tree.Oper is GT_CNS_INT or GT_CNS_DBL or GT_CNS_VEC or GT_CNS_MSK);
#elif FEATURE_SIMD
        assert(tree.Oper is GT_CNS_INT or GT_CNS_DBL or GT_CNS_VEC);
#else
        assert(tree.Oper is GT_CNS_INT or GT_CNS_DBL);
#endif
        JITDUMP("  TreeNode is marked ReuseReg\n");

        // An integer zero can reuse a formerly GC-tracked null register.
        // A label propagates that GC transition without another instruction.
        if (tree.IsIntegralConst(0) && Emitter.emitCurIGnonEmpty())
        {
            genDefineTempLabel(genCreateTempLabel());
        }
#endif
    }
}
