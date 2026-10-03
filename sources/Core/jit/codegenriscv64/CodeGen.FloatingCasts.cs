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
}
#endif
