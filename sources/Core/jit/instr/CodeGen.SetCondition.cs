// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_SET(emitJumpKind condition, regNumber reg, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Set-condition instruction generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        var ins = condition switch
        {
            EJ_js => INS_sets,
            EJ_jns => INS_setns,
            EJ_je => INS_sete,
            EJ_jne => INS_setne,
            EJ_jl => INS_setl,
            EJ_jle => INS_setle,
            EJ_jge => INS_setge,
            EJ_jg => INS_setg,
            EJ_jb => INS_setb,
            EJ_jbe => INS_setbe,
            EJ_jae => INS_setae,
            EJ_ja => INS_seta,
            EJ_jp => INS_setp,
            EJ_jnp => INS_setnp,
            _ => INS_none,
        };
        if (ins == INS_none)
        {
            NO_WAY("unexpected condition type");
            return;
        }

        if ((instOptions & INS_OPTS_EVEX_zu_MASK) != 0)
        {
            const int offset = INS_seto - INS_seto_apx;
            assert(INS_seto == (INS_seto_apx + offset));
            assert(INS_setno == (INS_setno_apx + offset));
            assert(INS_setb == (INS_setb_apx + offset));
            assert(INS_setae == (INS_setae_apx + offset));
            assert(INS_sete == (INS_sete_apx + offset));
            assert(INS_setne == (INS_setne_apx + offset));
            assert(INS_setbe == (INS_setbe_apx + offset));
            assert(INS_seta == (INS_seta_apx + offset));
            assert(INS_sets == (INS_sets_apx + offset));
            assert(INS_setns == (INS_setns_apx + offset));
            assert(INS_setp == (INS_setp_apx + offset));
            assert(INS_setnp == (INS_setnp_apx + offset));
            assert(INS_setl == (INS_setl_apx + offset));
            assert(INS_setge == (INS_setge_apx + offset));
            assert(INS_setle == (INS_setle_apx + offset));
            assert(INS_setg == (INS_setg_apx + offset));
            ins = (instruction)(ins - offset);
        }

        assert((regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask).Lower & SRBM_ALLINT) != SRBM_NONE);
        Emitter.emitIns_R(ins, EA_1BYTE, reg, instOptions);
#endif
    }
}
