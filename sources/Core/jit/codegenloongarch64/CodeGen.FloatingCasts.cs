// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genIntToFloatCast(GenTree treeNode)
    {
        assert(treeNode.Oper is GT_CAST);
        assert(!treeNode.HasOverflowCheck);

        var cast = treeNode.AsCast();
        var targetReg = cast.RegNum;
        assert(genIsValidFloatReg(targetReg));

        var op1 = cast.CastOp;
        assert(!op1.IsContained);
        assert(genIsValidIntReg(op1.RegNum));

        var dstType = cast.CastType;
        var srcType = op1.Type.ActualType;
        assert(!varTypeIsFloating(srcType) && varTypeIsFloating(dstType));

        var emit = Emitter;
        var attr = dstType.EmitActualSize;
        var srcSize = srcType.EmitSize;
        noway_assert((srcSize == EA_4BYTE) || (srcSize == EA_8BYTE));

        genConsumeOperands(cast);

        if (cast.IsUnsigned)
        {
            // Keep the original bits so the correction can distinguish unsigned values above Int64.MaxValue.
            emit.emitIns_R_R(INS_movgr2fr_d, EA_8BYTE, REG_SCRATCH_FLT, op1.RegNum);

            if (srcSize == EA_8BYTE)
            {
                // Convert a rounded-up half as signed, then double it to cover the unsigned high half.
                var skipAdjustment = (nint)(4 << 2);
                emit.emitIns_R_R_I(INS_bge, EA_8BYTE, op1.RegNum, REG_R0, skipAdjustment);

                emit.emitIns_R_R_I(INS_andi, EA_8BYTE, REG_R21, op1.RegNum, 1);
                emit.emitIns_R_R_I(INS_srli_d, EA_8BYTE, op1.RegNum, op1.RegNum, 1);
                emit.emitIns_R_R_R(INS_or, EA_8BYTE, op1.RegNum, op1.RegNum, REG_R21);
            }
            else
            {
                srcSize = EA_8BYTE;
                emit.emitIns_R_R_I_I(INS_bstrins_d, EA_8BYTE, op1.RegNum, REG_R0, 63, 32);
            }
        }

        var moveIns = srcSize == EA_8BYTE ? INS_movgr2fr_d : INS_movgr2fr_w;
        emit.emitIns_R_R(moveIns, attr, targetReg, op1.RegNum);

        instruction ins;
        if (dstType is TYP_DOUBLE)
        {
            if (srcSize is EA_4BYTE)
            {
                ins = INS_ffint_d_w;
            }
            else
            {
                assert(srcSize is EA_8BYTE);
                ins = INS_ffint_d_l;
            }
        }
        else
        {
            assert(dstType is TYP_FLOAT);
            if (srcSize is EA_4BYTE)
            {
                ins = INS_ffint_s_w;
            }
            else
            {
                assert(srcSize is EA_8BYTE);
                ins = INS_ffint_s_l;
            }
        }

        emit.emitIns_R_R(ins, attr, targetReg, targetReg);

        if (cast.IsUnsigned)
        {
            srcSize = op1.Type.ActualType.EmitSize;
            emit.emitIns_R_R(INS_movfr2gr_d, attr, op1.RegNum, REG_SCRATCH_FLT);

            if (srcSize is EA_8BYTE)
            {
                var skipCorrection = (nint)(3 << 2);
                emit.emitIns_R_R_I(INS_bge, EA_8BYTE, op1.RegNum, REG_R0, skipCorrection);

                var moveFloatIns = dstType is TYP_DOUBLE ? INS_fmov_d : INS_fmov_s;
                emit.emitIns_R_R(moveFloatIns, attr, REG_SCRATCH_FLT, targetReg);

                var addFloatIns = dstType is TYP_DOUBLE ? INS_fadd_d : INS_fadd_s;
                emit.emitIns_R_R_R(addFloatIns, attr, targetReg, REG_SCRATCH_FLT, targetReg);
            }
        }

        genProduceReg(cast);
    }

    public void genFloatToIntCast(GenTreeCast treeNode)
    {
        assert(treeNode.Oper is GT_CAST);
        assert(!treeNode.HasOverflowCheck);

        var targetReg = treeNode.RegNum;
        assert(genIsValidIntReg(targetReg));

        var op1 = treeNode.CastOp;
        assert(!op1.IsContained);
        assert(genIsValidFloatReg(op1.RegNum));

        var dstType = treeNode.CastType;
        var srcType = op1.Type;
        assert(varTypeIsFloating(srcType) && !varTypeIsFloating(dstType));

        var emit = Emitter;
        var dstSize = dstType.EmitSize;
        noway_assert((dstSize == EA_4BYTE) || (dstSize == EA_8BYTE));

        instruction ins1 = INS_invalid;
        instruction ins2 = INS_invalid;
        var isUnsigned = varTypeIsUnsigned(dstType);

        var tempReg = REG_SCRATCH_FLT;
        assert(tempReg != op1.RegNum);

        if (srcType is TYP_DOUBLE)
        {
            if (dstSize == EA_4BYTE)
            {
                ins1 = INS_ftintrz_w_d;
                ins2 = INS_movfr2gr_s;
            }
            else
            {
                assert(dstSize == EA_8BYTE);
                ins1 = INS_ftintrz_l_d;
                ins2 = INS_movfr2gr_d;
            }
        }
        else
        {
            assert(srcType is TYP_FLOAT);
            if (dstSize == EA_4BYTE)
            {
                ins1 = INS_ftintrz_w_s;
                ins2 = INS_movfr2gr_s;
            }
            else
            {
                assert(dstSize == EA_8BYTE);
                ins1 = INS_ftintrz_l_s;
                ins2 = INS_movfr2gr_d;
            }
        }

        genConsumeOperands(treeNode);

        // Convert unsigned values above the signed limit relative to that limit, then restore the high bit.
        if (isUnsigned)
        {
            nint imm = 0;
            if (srcType is TYP_DOUBLE)
            {
                if (dstSize == EA_4BYTE)
                {
                    imm = 0x41e00;
                }
                else
                {
                    imm = 0x43e00;
                }
            }
            else
            {
                assert(srcType is TYP_FLOAT);
                if (dstSize == EA_4BYTE)
                {
                    imm = 0x4f000;
                }
                else
                {
                    imm = 0x5f000;
                }
            }

            emit.emitIns_R_R_I(INS_ori, dstSize, targetReg, REG_R0, 0);
            var intToFloatIns = srcType is TYP_DOUBLE ? INS_movgr2fr_d : INS_movgr2fr_w;
            emit.emitIns_R_R(intToFloatIns, EA_8BYTE, tempReg, REG_R0);

            emit.emitIns_R_R_I(
                srcType is TYP_DOUBLE ? INS_fcmp_cult_d : INS_fcmp_cult_s, EA_8BYTE, op1.RegNum, tempReg, 2);

            nint skipUnsignedCase = dstType is TYP_UINT ? 16 << 2 : 13 << 2;
            emit.emitIns_I_I(INS_bcnez, EA_PTRSIZE, 2, skipUnsignedCase);

            if (srcType is TYP_DOUBLE)
            {
                emit.emitIns_R_R_I(INS_lu52i_d, EA_8BYTE, REG_R21, REG_R0, imm >> 8);
            }
            else
            {
                emit.emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, REG_R21, imm);
            }

            emit.emitIns_R_R(intToFloatIns, EA_8BYTE, tempReg, REG_R21);

            emit.emitIns_R_R_I(
                srcType is TYP_DOUBLE ? INS_fcmp_clt_d : INS_fcmp_clt_s, EA_8BYTE, op1.RegNum, tempReg, 2);

            emit.emitIns_R_R_I(INS_ori, EA_PTRSIZE, REG_R21, REG_R0, 0);
            emit.emitIns_I_I(INS_bcnez, EA_PTRSIZE, 2, 4 << 2);

            var floatSubtractIns = srcType is TYP_DOUBLE ? INS_fsub_d : INS_fsub_s;
            emit.emitIns_R_R_R(floatSubtractIns, EA_8BYTE, tempReg, op1.RegNum, tempReg);

            emit.emitIns_R_R_I(INS_ori, EA_PTRSIZE, REG_R21, REG_R0, 1);
            emit.emitIns_R_R_I(
                dstSize == EA_8BYTE ? INS_slli_d : INS_slli_w,
                EA_PTRSIZE,
                REG_R21,
                REG_R21,
                dstSize == EA_8BYTE ? 63 : 31);

            emit.emitIns_R_R_R_I(INS_fsel, EA_PTRSIZE, tempReg, tempReg, op1.RegNum, 2);

            emit.emitIns_R_R(ins1, dstSize, tempReg, tempReg);
            emit.emitIns_R_R(ins2, dstSize, targetReg, tempReg);

            if (dstType is TYP_UINT)
            {
                emit.emitIns_R_R_I(INS_addu16i_d, EA_PTRSIZE, REG_RA, REG_R0, -32768);
                emit.emitIns_R_R_I(INS_bne, EA_PTRSIZE, targetReg, REG_RA, 2 << 2);
                emit.emitIns_R_R_I(INS_ori, dstSize, targetReg, REG_R0, 0);
            }

            emit.emitIns_R_R_R(INS_or, dstSize, targetReg, REG_R21, targetReg);
        }
        else
        {
            emit.emitIns_R_R(ins1, dstSize, tempReg, op1.RegNum);
            emit.emitIns_R_R(ins2, dstSize, targetReg, tempReg);
        }

        genProduceReg(treeNode);
    }
}
#endif
