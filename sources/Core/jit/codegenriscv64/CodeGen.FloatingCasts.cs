// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
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

        var srcSize = srcType.EmitSize;
        noway_assert((srcSize == EA_4BYTE) || (srcSize == EA_8BYTE));

        instruction ins = INS_invalid;
        if (cast.IsUnsigned)
        {
            if (dstType is TYP_DOUBLE)
            {
                if (srcSize is EA_4BYTE)
                {
                    ins = INS_fcvt_d_wu;
                }
                else
                {
                    assert(srcSize is EA_8BYTE);
                    ins = INS_fcvt_d_lu;
                }
            }
            else
            {
                assert(dstType is TYP_FLOAT);
                if (srcSize is EA_4BYTE)
                {
                    ins = INS_fcvt_s_wu;
                }
                else
                {
                    assert(srcSize is EA_8BYTE);
                    ins = INS_fcvt_s_lu;
                }
            }
        }
        else if (dstType is TYP_DOUBLE)
        {
            if (srcSize is EA_4BYTE)
            {
                ins = INS_fcvt_d_w;
            }
            else
            {
                assert(srcSize is EA_8BYTE);
                ins = INS_fcvt_d_l;
            }
        }
        else
        {
            assert(dstType is TYP_FLOAT);
            if (srcSize is EA_4BYTE)
            {
                ins = INS_fcvt_s_w;
            }
            else
            {
                assert(srcSize is EA_8BYTE);
                ins = INS_fcvt_s_l;
            }
        }

        genConsumeOperands(cast);

        Emitter.emitIns_R_R(ins, dstType.EmitActualSize, targetReg, op1.RegNum);
        genProduceReg(cast);
    }

    public void genFloatToIntCast(GenTreeCast treeNode)
    {
        assert(treeNode.Oper is GT_CAST);
        assert(!treeNode.HasOverflowCheck);
        assert(genIsValidIntReg(treeNode.RegNum));

        var op1 = treeNode.CastOp;
        assert(!op1.IsContained);
        assert(genIsValidFloatReg(op1.RegNum));

        var dstType = treeNode.CastType;
        var srcType = op1.Type.ActualType;
        assert(varTypeIsFloating(srcType) && !varTypeIsFloating(dstType));

        var dstSize = dstType.EmitSize;
        noway_assert((dstSize == EA_4BYTE) || (dstSize == EA_8BYTE));

        var isUnsigned = varTypeIsUnsigned(dstType);
        instruction ins = INS_invalid;

        if (isUnsigned)
        {
            if (srcType is TYP_DOUBLE)
            {
                if (dstSize == EA_4BYTE)
                {
                    ins = INS_fcvt_wu_d;
                }
                else
                {
                    ins = INS_fcvt_lu_d;
                }
            }
            else
            {
                assert(srcType is TYP_FLOAT);
                if (dstSize == EA_4BYTE)
                {
                    ins = INS_fcvt_wu_s;
                }
                else
                {
                    ins = INS_fcvt_lu_s;
                }
            }
        }
        else
        {
            if (srcType is TYP_DOUBLE)
            {
                if (dstSize == EA_4BYTE)
                {
                    ins = INS_fcvt_w_d;
                }
                else
                {
                    ins = INS_fcvt_l_d;
                }
            }
            else
            {
                assert(srcType is TYP_FLOAT);
                if (dstSize == EA_4BYTE)
                {
                    ins = INS_fcvt_w_s;
                }
                else
                {
                    ins = INS_fcvt_l_s;
                }
            }
        }

        genConsumeOperands(treeNode);

        var tempReg = InternalRegisters.GetSingle(treeNode);
        assert(tempReg != treeNode.RegNum);
        assert(tempReg != op1.RegNum);

        Emitter.emitIns_R_R(ins, dstSize, treeNode.RegNum, op1.RegNum);

        // This emulates the "flush to zero" option because the RISC-V specification does not provide it.
        instruction feqIns = INS_feq_s;
        if (srcType is TYP_DOUBLE)
        {
            feqIns = INS_feq_d;
        }

        // feq returns zero for NaN and one for every other value; negating it yields a mask for valid conversions.
        Emitter.emitIns_R_R_R(feqIns, dstSize, tempReg, op1.RegNum, op1.RegNum);
        Emitter.emitIns_R_R_R(INS_sub, dstSize, tempReg, REG_ZERO, tempReg);
        Emitter.emitIns_R_R_R(INS_and, dstSize, treeNode.RegNum, treeNode.RegNum, tempReg);

        genProduceReg(treeNode);
    }

    public void genCkfinite(GenTree treeNode)
    {
        assert(treeNode.Oper is GT_CKFINITE);

        var op1 = treeNode.AsOp().Op1;
        var targetType = treeNode.Type;
        var expMask = 0x381; // fclass bits 0 and 7-9 identify infinities and NaNs.

        var emit = Emitter;
        var attr = targetType.EmitActualSize;

        var intReg = InternalRegisters.GetSingle(treeNode);
        var fpReg = genConsumeReg(op1);

        var classifyIns = attr == EA_4BYTE ? INS_fclass_s : INS_fclass_d;
        emit.emitIns_R_R(classifyIns, attr, intReg, fpReg);
        emit.emitIns_R_R_I(INS_andi, EA_PTRSIZE, intReg, intReg, expMask);

        genJumpToThrowHlpBlk_la(SCK_ARITH_EXCPN, INS_bne, intReg);

        if (treeNode.RegNum != fpReg)
        {
            inst_Mov(targetType, treeNode.RegNum, fpReg, canSkip: true);
        }

        genProduceReg(treeNode);
    }
}
#endif
