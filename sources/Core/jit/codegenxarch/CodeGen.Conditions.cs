// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_SETCCXarch(GenCondition condition, var_types type, regNumber dstReg)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Boolean condition generation requires AMD64.");
            return;
        }

        Emitter.RequireSupportedInstructionRecording();
        assert(varTypeIsIntegral(type));
        assert(genIsValidIntReg(dstReg));
        if (Configuration.TARGET_X86)
        {
            assert(dstReg is REG_X86_EAX or REG_X86_ECX or REG_X86_EDX or REG_X86_EBX);
        }

        var desc = GenConditionDesc.Get(condition);
        var instOptions = INS_OPTS_NONE;
        var needsMovzx = !varTypeIsByte(type);
        if (needsMovzx && _compiler.canUseApxEvexEncoding() && (JitConfig.EnableApxZU != 0))
        {
            instOptions = INS_OPTS_EVEX_zu;
            needsMovzx = false;
        }

        inst_SET(desc.JumpKind1, dstReg, instOptions);
        if (desc.Oper is not GT_NONE)
        {
            var next = genCreateTempLabel();
            inst_JMPXarch(desc.Oper is GT_OR ? desc.JumpKind1 : RyuJitSharp.Emitter.emitReverseJumpKind(desc.JumpKind1), next);
            inst_SET(desc.JumpKind2, dstReg, instOptions);
            genDefineTempLabel(next);
        }

        if (needsMovzx)
        {
            _ = Emitter.emitIns_Mov(INS_movzx, EA_1BYTE, dstReg, dstReg, canSkip: false);
        }
    }
}
