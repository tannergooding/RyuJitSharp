// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genPutArgRegRiscV64(GenTreeUnOp tree)
    {
        assert(tree.Oper is GT_PUTARG_REG);

        var targetType = tree.Type;
        var targetReg = tree.RegNum;
        if (varTypeIsFloating(targetType) && Emitter.isGeneralRegister(targetReg))
        {
            targetType = EA_SIZE(targetType.EmitActualSize) == EA_4BYTE ? TYP_INT : TYP_LONG;
        }

        var operand = tree.Op1;
        _ = genConsumeReg(operand);

        _ = Emitter.emitIns_Mov(ins_Copy(operand.RegNum, targetType), targetType.EmitActualSize, targetReg,
            operand.RegNum, canSkip: true);
        genProduceReg(tree);
    }

    private void genCodeForPhysRegRiscV64(GenTreePhysReg tree)
    {
        assert(tree.Oper is GT_PHYSREG);

        var targetType = tree.Type;
        var targetReg = tree.RegNum;

        if (targetReg != tree.SrcReg)
        {
            _ = Emitter.emitIns_Mov(ins_Copy(targetType), targetType.EmitActualSize, targetReg, tree.SrcReg,
                canSkip: false);
            genTransferRegGCState(targetReg, tree.SrcReg);
        }

        genProduceReg(tree);
    }
}
#endif
