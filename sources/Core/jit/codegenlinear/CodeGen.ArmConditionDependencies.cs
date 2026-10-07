// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_SETCC(GenCondition condition, var_types type, regNumber dstReg)
    {
        assert(varTypeIsIntegral(type));
        assert(genIsValidIntReg(dstReg));
#if TARGET_ARM64
        var desc = GenConditionDesc.Get(condition);

        inst_SET(desc.JumpKind1, dstReg);
        if (desc.Oper is not GT_NONE)
        {
            var next = genCreateTempLabel();
            inst_JMP(
                (desc.Oper is GT_OR) ? desc.JumpKind1 : Emitter.emitReverseJumpKind(desc.JumpKind1),
                next);
            inst_SET(desc.JumpKind2, dstReg);
            genDefineTempLabel(next);
        }
#else
        var labelTrue = genCreateTempLabel();
        inst_JCC(condition, labelTrue);

        Emitter.emitIns_R_I(INS_mov, type.EmitActualSize, dstReg, 0);

        var labelNext = genCreateTempLabel();
        Emitter.emitIns_J(INS_b, labelNext);

        genDefineTempLabel(labelTrue);
        Emitter.emitIns_R_I(INS_mov, type.EmitActualSize, dstReg, 1);
        genDefineTempLabel(labelNext);
#endif
    }
}
#endif
