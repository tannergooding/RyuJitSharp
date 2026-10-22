// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_SET(emitJumpKind condition, regNumber reg, insOpts instOptions = INS_OPTS_NONE)
    {
        if (Configuration.TARGET_XARCH)
        {
            if (Configuration.TARGET_AMD64)
            {
                Emitter.RequireSupportedInstructionRecording();
            }

            var ins = condition switch {
                EJ_XARCH_js => INS_sets,
                EJ_XARCH_jns => INS_setns,
                EJ_XARCH_je => INS_sete,
                EJ_XARCH_jne => INS_setne,
                EJ_XARCH_jl => INS_setl,
                EJ_XARCH_jle => INS_setle,
                EJ_XARCH_jge => INS_setge,
                EJ_XARCH_jg => INS_setg,
                EJ_XARCH_jb => INS_setb,
                EJ_XARCH_jbe => INS_setbe,
                EJ_XARCH_jae => INS_setae,
                EJ_XARCH_ja => INS_seta,
                EJ_XARCH_jp => INS_setp,
                EJ_XARCH_jnp => INS_setnp,
                _ => INS_none,
            };
            if (ins == INS_none)
            {
                NO_WAY("unexpected condition type");
                return;
            }

            if (Configuration.TARGET_AMD64 && ((instOptions & INS_OPTS_EVEX_zu_MASK) != 0))
            {
                var nativeOrdinalOffset = InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_seto) -
                                          InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_seto_apx);
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_seto) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_seto_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setno) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setno_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setb) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setb_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setae) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setae_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_sete) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_sete_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setne) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setne_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setbe) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setbe_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_seta) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_seta_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_sets) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_sets_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setns) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setns_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setp) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setp_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setnp) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setnp_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setl) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setl_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setge) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setge_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setle) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setle_apx) + nativeOrdinalOffset));
                assert(InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setg) ==
                       (InstructionOrdinalMaps.GetNativeInstructionOrdinal(INS_setg_apx) + nativeOrdinalOffset));
                ins = InstructionOrdinalMaps.GetInstructionFromNativeOrdinal(
                    InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) - nativeOrdinalOffset);
            }

            if (Configuration.TARGET_X86)
            {
                assert((regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask).Lower &
                    (SRBM_X86_EAX | SRBM_X86_ECX | SRBM_X86_EDX | SRBM_X86_EBX)) != SRBM_NONE);
            }
            else
            {
                assert((regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask).Lower & SRBM_ALLINT) != SRBM_NONE);
            }

            Emitter.emitIns_R(ins, EA_1BYTE, reg, instOptions);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            Emitter.emitIns_R_COND(INS_cset, EA_8BYTE, reg, JumpKindToInsCond(condition));
            return;
        }

        NYI("inst_SET");
        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.SetConditionGenerationIsNotImplementedOnThisTarget
    );
        return;
    }
}
