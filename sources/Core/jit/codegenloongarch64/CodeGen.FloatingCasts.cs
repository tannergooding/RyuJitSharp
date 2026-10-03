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
}
#endif
