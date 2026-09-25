// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R(instruction ins, emitAttr attr, regNumber reg, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Single-register instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        var size = EA_SIZE(attr);
        assert(size <= EA_PTRSIZE);
        noway_assert(emitVerifyEncodable(ins, size, reg));

        uint sz;
        var id = emitNewInstrSmall(attr);
        var fmt = emitInsModeFormat(ins, IF_RRD);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idReg1(reg);

        switch (ins)
        {
            case INS_inc or INS_dec:
            {
                // x64 has no one-byte opcode: that encoding is the REX prefix.
                sz = 2;
                break;
            }

            case INS_pop or INS_push:
            {
                SetApxPpxIfNeeded(id, instOptions);
                goto case INS_pop_hide;
            }

            case INS_pop_hide:
            case INS_push_hide:
            {
                // Small values are not currently pushed or popped.
                assert(size == EA_PTRSIZE);
                sz = 1;
                break;
            }

            default:
            {
                if ((INS_seto <= ins) && (ins <= INS_setg))
                {
                    assert((INS_seto + 0xF) == INS_setg);
                    assert(attr == EA_1BYTE);
                    assert((insEncodeMRreg(id, reg, attr, insCodeMR(ins)) & 0x00FF0000) != 0);
                    sz = 3;
                }
                else
                {
                    sz = 2;
                }
                break;
            }
        }

        SetEvexNfIfNeeded(id, instOptions);
        SetEvexZuIfNeeded(id, instOptions);
        sz += emitGetAdjustedSize(id, insEncodeMRreg(id, reg, attr, insCodeMR(ins)));

        if (IsExtendedReg(reg, attr) || TakesRexWPrefix(id))
        {
            sz += emitGetRexPrefixSize(id, ins);
        }

        id.idCodeSize(sz);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);

        // Native emitAdjustStackDepthPushPop is empty with AMD64's FEATURE_FIXED_OUT_ARGS.
#endif
    }

    public void emitIns_BASE_R_R(instruction ins, emitAttr attr, regNumber op1Reg, regNumber op2Reg)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Base unary instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (DoJitUseApxNDD(ins) && (op1Reg != op2Reg))
        {
            emitIns_R_R(ins, attr, op1Reg, op2Reg, INS_OPTS_EVEX_nd);
        }
        else
        {
            _ = emitIns_Mov(INS_mov, attr, op1Reg, op2Reg, canSkip: true);
            emitIns_R(ins, attr, op1Reg);
        }
#endif
    }

    public bool DoJitUseApxNDD(instruction ins)
    {
#if TARGET_AMD64
        return (JitConfig.EnableApxNDD != 0) && IsApxNddEncodableInstruction(ins);
#else
        return false;
#endif
    }

#if TARGET_AMD64
    public static bool IsApxZuCompatibleInstruction(instruction ins)
    {
        return (ins >= INS_seto_apx) && (ins <= INS_setg_apx);
    }

    public void SetEvexZuIfNeeded(instrDesc id, insOpts instOptions)
    {
        if ((instOptions & INS_OPTS_EVEX_zu_MASK) != 0)
        {
            assert(UsePromotedEvexEncodings);
            assert(IsApxZuCompatibleInstruction(id.idIns()));
            id.idSetEvexZuContext();
        }
        else
        {
            assert((instOptions & INS_OPTS_EVEX_zu_MASK) == 0);
        }
    }
#endif
}
