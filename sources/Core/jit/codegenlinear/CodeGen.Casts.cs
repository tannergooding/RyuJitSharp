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
#elif TARGET_ARM64
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
        noway_assert(dstSize is EA_4BYTE or EA_8BYTE);
        var isUnsigned = varTypeIsUnsigned(dstType);
        var ins = isUnsigned ? INS_fcvtzu : INS_fcvtzs;
        var cvtOption = srcType switch
        {
            TYP_DOUBLE => dstSize == EA_4BYTE ? INS_OPTS_D_TO_4BYTE : INS_OPTS_D_TO_8BYTE,
            TYP_FLOAT => dstSize == EA_4BYTE ? INS_OPTS_S_TO_4BYTE : INS_OPTS_S_TO_8BYTE,
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, "Invalid floating-to-integral cast source."),
        };

        genConsumeOperands(tree);
        Emitter.emitIns_R_R(ins, dstSize, targetReg, op1.RegNum, cvtOption);
        genProduceReg(tree);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Floating-to-integer cast generation outside xarch is not ported.");
#endif
    }
#endif

#if !TARGET_LOONGARCH64
#if TARGET_ARMARCH
    private void genIntCastOverflowCheck(GenTreeCast tree, in GenIntCastDesc desc, regNumber reg)
    {
        switch (desc.Check)
        {
            case GenIntCastDesc.CheckKind.CHECK_POSITIVE:
            {
                Emitter.emitIns_R_I(INS_cmp, (emitAttr)desc.CheckSrcSize, reg, 0);
                genJumpToThrowHlpBlk(EJ_lt, SCK_OVERFLOW);
                break;
            }

#if TARGET_64BIT
            case GenIntCastDesc.CheckKind.CHECK_UINT_RANGE:
            {
                // The upper 32 bits must be zero; this mask is not encodable by CMP.
                Emitter.emitIns_R_I(
                    INS_tst, EA_8BYTE, reg, unchecked((nint)0xFFFFFFFF00000000UL));
                genJumpToThrowHlpBlk(EJ_ne, SCK_OVERFLOW);
                break;
            }

            case GenIntCastDesc.CheckKind.CHECK_POSITIVE_INT_RANGE:
            {
                // The upper 33 bits must be zero for a positive signed-int result.
                Emitter.emitIns_R_I(
                    INS_tst, EA_8BYTE, reg, unchecked((nint)0xFFFFFFFF80000000UL));
                genJumpToThrowHlpBlk(EJ_ne, SCK_OVERFLOW);
                break;
            }

            case GenIntCastDesc.CheckKind.CHECK_INT_RANGE:
            {
                // Compare against the sign-extended 32-bit value.
                Emitter.emitIns_R_R(INS_cmp, EA_8BYTE, reg, reg, INS_OPTS_SXTW);
                genJumpToThrowHlpBlk(EJ_ne, SCK_OVERFLOW);
                break;
            }
#endif

            default:
            {
                assert(desc.Check is GenIntCastDesc.CheckKind.CHECK_SMALL_INT_RANGE);
                var max = desc.CheckSmallIntMax;
                var min = desc.CheckSmallIntMin;
                if (max > 255)
                {
                    // ARM can encode 255 directly; for larger bounds compare against max + 1.
                    assert((max is 32767) || (max is 65535));
                    Emitter.emitIns_R_I(
                        INS_cmp, (emitAttr)desc.CheckSrcSize, reg, max + 1);
                    genJumpToThrowHlpBlk((min == 0) ? EJ_hs : EJ_ge, SCK_OVERFLOW);
                }
                else
                {
                    Emitter.emitIns_R_I(INS_cmp, (emitAttr)desc.CheckSrcSize, reg, max);
                    genJumpToThrowHlpBlk((min == 0) ? EJ_hi : EJ_gt, SCK_OVERFLOW);
                }

                if (min != 0)
                {
                    Emitter.emitIns_R_I(INS_cmp, (emitAttr)desc.CheckSrcSize, reg, min);
                    genJumpToThrowHlpBlk(EJ_lt, SCK_OVERFLOW);
                }
                break;
            }
        }
    }
#endif

    private void genIntToIntCast(GenTreeCast tree)
    {
#if TARGET_ARMARCH
        genConsumeRegs(tree.CastOp);
        var src = tree.CastOp;
        var srcReg = src.IsUsedFromReg ? src.RegNum : REG_NA;
        var dstReg = tree.RegNum;
        assert(genIsValidIntReg(dstReg));

        var desc = new GenIntCastDesc(tree);
        if (desc.Check is not GenIntCastDesc.CheckKind.CHECK_NONE)
        {
            assert(genIsValidIntReg(srcReg));
            genIntCastOverflowCheck(tree, in desc, srcReg);
        }

        if ((desc.Extend is not GenIntCastDesc.ExtendKind.COPY) || (srcReg != dstReg))
        {
            instruction ins;
            uint insSize;

            switch (desc.Extend)
            {
                case GenIntCastDesc.ExtendKind.ZERO_EXTEND_SMALL_INT:
                {
                    ins = (desc.ExtendSrcSize == 1) ? INS_uxtb : INS_uxth;
                    insSize = 4;
                    break;
                }

                case GenIntCastDesc.ExtendKind.SIGN_EXTEND_SMALL_INT:
                {
                    ins = (desc.ExtendSrcSize == 1) ? INS_sxtb : INS_sxth;
                    insSize = 4;
                    break;
                }

#if TARGET_64BIT
                case GenIntCastDesc.ExtendKind.ZERO_EXTEND_INT:
                {
                    ins = INS_mov;
                    insSize = 4;
                    break;
                }

                case GenIntCastDesc.ExtendKind.SIGN_EXTEND_INT:
                {
                    ins = INS_sxtw;
                    insSize = 8;
                    break;
                }
#endif

                case GenIntCastDesc.ExtendKind.COPY:
                {
                    ins = INS_mov;
                    insSize = desc.ExtendSrcSize;
                    break;
                }

                case GenIntCastDesc.ExtendKind.LOAD_ZERO_EXTEND_SMALL_INT:
                {
                    ins = (desc.ExtendSrcSize == 1) ? INS_ldrb : INS_ldrh;
                    insSize = TARGET_POINTER_SIZE;
                    break;
                }

                case GenIntCastDesc.ExtendKind.LOAD_SIGN_EXTEND_SMALL_INT:
                {
                    ins = (desc.ExtendSrcSize == 1) ? INS_ldrsb : INS_ldrsh;
                    insSize = TARGET_POINTER_SIZE;
                    break;
                }

#if TARGET_64BIT
                case GenIntCastDesc.ExtendKind.LOAD_ZERO_EXTEND_INT:
                {
                    ins = INS_ldr;
                    insSize = 4;
                    break;
                }

                case GenIntCastDesc.ExtendKind.LOAD_SIGN_EXTEND_INT:
                {
                    ins = INS_ldrsw;
                    insSize = 8;
                    break;
                }
#endif

                case GenIntCastDesc.ExtendKind.LOAD_SOURCE:
                {
                    ins = ins_Load(src.Type);
                    insSize = src.Type.ActualType.Size;
                    break;
                }

                default:
                {
                    unreached();
                    return;
                }
            }

            if (srcReg is not REG_NA)
            {
#if TARGET_ARM64
                Emitter.emitIns_Mov(ins, (emitAttr)insSize, dstReg, srcReg, canSkip: false);
#else
                _ = Emitter.emitIns_Mov(ins, (emitAttr)insSize, dstReg, srcReg, canSkip: false);
#endif
            }
            else
            {
                assert(src.IsUsedFromMemory);
                if (src.IsUsedFromSpillTemp)
                {
                    assert(src.IsRegOptional);
                    var temp = getSpillTempDsc(src);
                    var tempNum = temp.tdTempNum;
                    _regSet.tmpRlsTemp(temp);
                    Emitter.emitIns_R_S(ins, (emitAttr)insSize, dstReg, tempNum, 0);
                }
                else if (src.Oper.IsLocalRead)
                {
                    var local = src.AsLclVarCommon();
                    Emitter.emitIns_R_S(ins, (emitAttr)insSize, dstReg, local.LclNum, local.LclOffs);
                }
                else
                {
                    assert(src.Oper is GT_IND && !src.AsIndir().IsVolatile && !src.AsIndir().IsUnaligned);
                    var indir = src.AsIndir();
                    assert(indir.HasBase);
#if TARGET_ARM
                    Emitter.emitIns_R_R_I(ins, (emitAttr)insSize, dstReg, indir.Base.RegNum,
                        unchecked((int)indir.Offset), INS_FLAGS_DONT_CARE);
#else
                    Emitter.emitIns_R_R_I(
                        ins, (emitAttr)insSize, dstReg, indir.Base.RegNum, unchecked((int)indir.Offset));
#endif
                }
            }
        }

        genProduceReg(tree);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Integer cast generation outside xarch is not ported.");
#endif
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
