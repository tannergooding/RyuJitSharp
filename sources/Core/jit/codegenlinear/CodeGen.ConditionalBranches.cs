// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForJcc(GenTreeCC jcc)
    {
        if (Configuration.TARGET_WASM || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.FlagBranchGenerationHasNoNativeImplementationForThisTarget
    );
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        var block = _compiler.compCurBB;
        assert(block is not null);
        assert(block.Kind is BBJ_COND);
        assert(jcc.Oper is GT_JCC);
        inst_JCC(jcc.Condition, block.TrueTarget);

        var falseTarget = block.FalseTarget;
        if (!block.CanRemoveJumpToTarget(falseTarget, _compiler))
        {
            inst_JMP(EJ_jmp, falseTarget);
        }
    }

    public void inst_JCC(GenCondition condition, BasicBlock target)
    {
        if (Configuration.TARGET_WASM || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ConditionalBranchSequencesHaveNoNativeImplementationForThisTarget
    );
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        var desc = GenConditionDesc.Get(condition);
        if (desc.Oper is GT_NONE)
        {
            inst_JMP(desc.JumpKind1, target);
        }
        else if (desc.Oper is GT_OR)
        {
            inst_JMP(desc.JumpKind1, target);
            inst_JMP(desc.JumpKind2, target);
        }
        else
        {
            var next = genCreateTempLabel();
            inst_JMP(RyuJitSharp.Emitter.emitReverseJumpKind(desc.JumpKind1), next);
            inst_JMP(desc.JumpKind2, target);
            genDefineTempLabel(next);
        }
    }

    public void genCodeForSetcc(GenTreeCC setcc)
    {
        if (Configuration.TARGET_WASM || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.FlagResultGenerationHasNoNativeImplementationForThisTarget
    );
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }

        assert(setcc.Oper is GT_SETCC);
        if (Configuration.TARGET_ARMARCH)
        {
            inst_SETCCArmArch(setcc.Condition, setcc.Type, setcc.RegNum);
        }
        else
        {
            inst_SETCCXarch(setcc.Condition, setcc.Type, setcc.RegNum);
        }
        genProduceReg(setcc);
    }
}
