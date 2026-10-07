// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genPutArgRegLoongArch64(GenTreeUnOp tree)
    {
        assert(tree.Oper is GT_PUTARG_REG);

        var targetType = tree.Type;
        var targetReg = tree.RegNum;
        assert(targetType != TYP_STRUCT);

        var operand = tree.Op1;
        _ = genConsumeReg(operand);

        if (targetReg != operand.RegNum)
        {
            if (Emitter.isFloatReg(targetReg) == Emitter.isFloatReg(operand.RegNum))
            {
                Emitter.emitIns_R_R(ins_Copy(targetType), targetType.EmitActualSize, targetReg, operand.RegNum);
            }
            else if (Emitter.isFloatReg(targetReg))
            {
                Emitter.emitIns_R_R(INS_movgr2fr_d, EA_8BYTE, targetReg, operand.RegNum);
            }
            else
            {
                assert(!Emitter.isFloatReg(targetReg));
                Emitter.emitIns_R_R(INS_movfr2gr_d, EA_8BYTE, targetReg, operand.RegNum);
            }
        }

        genProduceReg(tree);
    }

    private void genCodeForPhysRegLoongArch64(GenTreePhysReg tree)
    {
        assert(tree.Oper is GT_PHYSREG);

        var targetType = tree.Type;
        var targetReg = tree.RegNum;

        if (targetReg != tree.SrcReg)
        {
            Emitter.emitIns_R_R(ins_Copy(targetType), targetType.EmitActualSize, targetReg, tree.SrcReg);
            genTransferRegGCState(targetReg, tree.SrcReg);
        }

        genProduceReg(tree);
    }
}
#endif
