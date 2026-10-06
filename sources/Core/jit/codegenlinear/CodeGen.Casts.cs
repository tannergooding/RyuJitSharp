// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForCast(GenTreeCast tree)
    {
        assert(tree.Oper == GT_CAST);
        var targetType = tree.Type;
        if (varTypeIsFloating(targetType) && varTypeIsFloating(tree.CastOp.Type))
        {
            genFloatToFloatCast(tree);
        }
        else if (varTypeIsFloating(tree.CastOp.Type))
        {
#if TARGET_XARCH
            // Xarch floating-to-integer casts must already be lowered to hardware intrinsics.
            unreached();
#else
            genFloatToIntCast(tree);
#endif
        }
        else if (varTypeIsFloating(targetType))
        {
            genIntToFloatCast(tree);
        }
#if !TARGET_64BIT && !TARGET_WASM
        else if (varTypeIsLong(tree.CastOp.Type))
        {
            genLongToIntCast(tree);
        }
#endif
        else
        {
            genIntToIntCast(tree);
        }
    }

#if !TARGET_XARCH && !TARGET_WASM
#if !TARGET_LOONGARCH64 && !TARGET_RISCV64
    private void genFloatToIntCast(GenTreeCast tree)
    {
#if TARGET_ARM
        assert(tree.Oper is GT_CAST);
        assert(!tree.HasOverflowCheck);

        var targetReg = tree.RegNum;
        assert(genIsValidIntReg(targetReg));

        var op1 = tree.CastOp;
        assert(!op1.IsContained);
        assert(genIsValidFloatReg(op1.RegNum));

        var dstType = tree.CastType;
        var srcType = op1.Type;
        assert(varTypeIsFloating(srcType) && !varTypeIsFloating(dstType));

        var dstSize = dstType.EmitSize;
        noway_assert(dstSize == EA_4BYTE);

        var isUnsigned = varTypeIsUnsigned(dstType);
        var insVcvt = srcType switch
        {
            TYP_DOUBLE => isUnsigned ? INS_vcvt_d2u : INS_vcvt_d2i,
            TYP_FLOAT => isUnsigned ? INS_vcvt_f2u : INS_vcvt_f2i,
            _ => INS_invalid,
        };

        genConsumeOperands(tree);

        var tmpReg = InternalRegisters.GetSingle(tree);
        assert(insVcvt is not INS_invalid);
        Emitter.emitIns_R_R(insVcvt, dstSize, tmpReg, op1.RegNum);
        _ = Emitter.emitIns_Mov(INS_vmov_f2i, dstSize, targetReg, tmpReg, canSkip: false);

        genProduceReg(tree);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Floating-to-integer cast generation outside xarch is not ported.");
#endif
    }
#endif

#if !TARGET_LOONGARCH64
    private void genIntToIntCast(GenTreeCast tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Integer cast generation outside xarch is not ported.");
    }
#endif
#endif

#if TARGET_ARM
    private void genLongToIntCast(GenTreeCast tree)
    {
        assert(tree.Oper is GT_CAST);

        var src = tree.CastOp.AsOp();
        noway_assert(src.Oper is GT_LONG);
        genConsumeRegs(src);

        var srcType = tree.IsUnsigned ? TYP_ULONG : TYP_LONG;
        var dstType = tree.CastType;
        var loSrcReg = src.Op1.RegNum;
        var hiSrcReg = src.Op2.RegNum;
        var dstReg = tree.RegNum;

        assert(dstType is TYP_INT or TYP_UINT);
        assert(genIsValidIntReg(loSrcReg));
        assert(genIsValidIntReg(hiSrcReg));
        assert(genIsValidIntReg(dstReg));

        if (tree.HasOverflowCheck)
        {
            if ((srcType is TYP_LONG) && (dstType is TYP_INT))
            {
                var allOne = genCreateTempLabel();
                var success = genCreateTempLabel();

                inst_RV_RV(INS_tst, loSrcReg, loSrcReg, TYP_INT, EA_4BYTE);
                inst_JMP(EJ_mi, allOne);
                inst_RV_RV(INS_tst, hiSrcReg, hiSrcReg, TYP_INT, EA_4BYTE);
                genJumpToThrowHlpBlk(EJ_ne, SCK_OVERFLOW);
                inst_JMP(EJ_jmp, success);

                genDefineTempLabel(allOne);
                inst_RV_IV(INS_cmp, hiSrcReg, -1, EA_4BYTE);
                genJumpToThrowHlpBlk(EJ_ne, SCK_OVERFLOW);

                genDefineTempLabel(success);
            }
            else
            {
                if ((srcType is TYP_ULONG) && (dstType is TYP_INT))
                {
                    inst_RV_RV(INS_tst, loSrcReg, loSrcReg, TYP_INT, EA_4BYTE);
                    genJumpToThrowHlpBlk(EJ_mi, SCK_OVERFLOW);
                }

                inst_RV_RV(INS_tst, hiSrcReg, hiSrcReg, TYP_INT, EA_4BYTE);
                genJumpToThrowHlpBlk(EJ_ne, SCK_OVERFLOW);
            }
        }

        inst_Mov(TYP_INT, dstReg, loSrcReg, canSkip: true);

        genProduceReg(tree);
    }
#endif
}
