// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForJTrue(GenTreeUnOp jtrue)
    {
        var block = _compiler.compCurBB;
        assert(block is not null);
        assert(block.Kind is BBJ_COND);

        var op = jtrue.Op1;
        var reg = genConsumeReg(op);
        inst_RV_RV(INS_tst, reg, reg, op.Type.ActualType);
        inst_JMP(EJ_ne, block.TrueTarget);

        var falseTarget = block.FalseTarget;
        // If we cannot fall into the false target, emit a jump to it.
        if (!block.CanRemoveJumpToTarget(falseTarget, _compiler))
        {
            inst_JMP(EJ_jmp, falseTarget);
        }
    }
}
#endif
