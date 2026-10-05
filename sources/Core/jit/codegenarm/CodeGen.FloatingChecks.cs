// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCkfinite(GenTree treeNode)
    {
        assert(treeNode.Oper is GT_CKFINITE);

        var emit = Emitter;
        var targetType = treeNode.Type;
        var intReg = InternalRegisters.GetSingle(treeNode);
        var fpReg = genConsumeReg(treeNode.AsUnOp().Op1);
        var targetReg = treeNode.RegNum;

        // Extract and sign-extend the exponent into an integer register.
        if (targetType is TYP_FLOAT)
        {
            _ = emit.emitIns_Mov(INS_vmov_f2i, EA_4BYTE, intReg, fpReg, canSkip: false);
            emit.emitIns_R_R_I_I(INS_sbfx, EA_4BYTE, intReg, intReg, 23, 8);
        }
        else
        {
            assert(targetType is TYP_DOUBLE);
            _ = emit.emitIns_Mov(INS_vmov_f2i, EA_4BYTE, intReg, REG_NEXT(fpReg), canSkip: false);
            emit.emitIns_R_R_I_I(INS_sbfx, EA_4BYTE, intReg, intReg, 20, 11);
        }

        // If exponent is all 1's, throw ArithmeticException.
        emit.emitIns_R_I(INS_add, EA_4BYTE, intReg, 1, INS_FLAGS_SET);
        genJumpToThrowHlpBlk(EJ_eq, SCK_ARITH_EXCPN);

        // If it's a finite value, copy it to targetReg.
        inst_Mov(targetType, targetReg, fpReg, canSkip: true, size: targetType.EmitSize);

        genProduceReg(treeNode);
    }
}
#endif
