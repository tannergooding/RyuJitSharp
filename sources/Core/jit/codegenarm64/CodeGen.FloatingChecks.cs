// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCkfinite(GenTree treeNode)
    {
        assert(treeNode.Oper is GT_CKFINITE);

        var op1 = treeNode.AsUnOp().Op1;
        var targetType = treeNode.Type;
        var expMask = targetType is TYP_FLOAT ? 0x7F8 : 0x7FF;
        var shiftAmount = targetType is TYP_FLOAT ? 20 : 52;

        var emit = Emitter;
        var attr = emitActualTypeSize(treeNode);
        var intReg = InternalRegisters.GetSingle(treeNode);
        var fpReg = genConsumeReg(op1);

        inst_Mov(targetType, intReg, fpReg, canSkip: false, size: attr);
        emit.emitIns_R_R_I(INS_lsr, attr, intReg, intReg, shiftAmount);
        emit.emitIns_R_R_I(INS_and, EA_4BYTE, intReg, intReg, expMask);
        emit.emitIns_R_I(INS_cmp, EA_4BYTE, intReg, expMask);

        genJumpToThrowHlpBlk(EJ_eq, SCK_ARITH_EXCPN);

        inst_Mov(targetType, treeNode.RegNum, fpReg, canSkip: true);
        genProduceReg(treeNode);
    }
}
#endif
