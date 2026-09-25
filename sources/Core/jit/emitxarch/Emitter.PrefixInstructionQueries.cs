// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    private static insFlags prefixFlags(instruction ins) => CodeGen.instInfo[(int)ins];

    private static bool IsSimdInstruction(instruction ins) =>
        ins >= FIRST_SSE_INSTRUCTION && ins <= LAST_AVX512_INSTRUCTION;

    private static bool IsBMIInstruction(instruction ins) =>
        ins >= FIRST_BMI_INSTRUCTION && ins <= LAST_BMI_INSTRUCTION;

    private static bool IsKMOVInstruction(instruction ins) => ins is
        INS_kmovb_gpr or INS_kmovw_gpr or INS_kmovd_gpr or INS_kmovq_gpr or
        INS_kmovb_msk or INS_kmovw_msk or INS_kmovd_msk or INS_kmovq_msk;

    private static bool IsApxOnlyInstruction(instruction ins)
    {
        var isApxOnly = (prefixFlags(ins) & Encoding_EVEX_APX_ONLY) != 0;
#if DEBUG && TARGET_AMD64
        if (isApxOnly)
        {
            var known = (ins >= INS_push2 && ins <= INS_pop2)
                || (ins >= FIRST_APX_INSTRUCTION && ins <= LAST_APX_INSTRUCTION)
                || ins is INS_tzcnt_apx or INS_lzcnt_apx or INS_popcnt_apx;
            assert(known);
        }
#endif
        return isApxOnly;
    }

    private static bool IsApxNfCompatibleInstruction(instruction ins) =>
        (prefixFlags(ins) & INS_FLAGS_HasNF) != 0;

    private bool IsApxExtendedEvexInstruction(instruction ins)
    {
#if TARGET_AMD64
        return UsePromotedEvexEncodings && (prefixFlags(ins) & INS_FLAGS_ApxEvexMask) != 0;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "APX prefix decisions require AMD64.");
#endif
    }

    internal bool IsVexEncodableInstruction(instruction ins)
    {
        if (!UseVexEncodings)
        {
            return false;
        }

#if FEATURE_HW_INTRINSICS
        switch (ins)
        {
            case INS_vpdpbusd or INS_vpdpwssd or INS_vpdpbusds or INS_vpdpwssds:
            {
                return (_compiler ?? throw new System.InvalidOperationException("Emitter is not initialized."))
                    .compSupportsHWIntrinsic(InstructionSet_AVXVNNI);
            }

            case INS_vpdpwsud or INS_vpdpwsuds or INS_vpdpwusd or INS_vpdpwusds or INS_vpdpwuud or
                 INS_vpdpwuuds or INS_vpdpbssd or INS_vpdpbssds or INS_vpdpbsud or INS_vpdpbsuds or
                 INS_vpdpbuud or INS_vpdpbuuds:
            {
                return (_compiler ?? throw new System.InvalidOperationException("Emitter is not initialized."))
                    .compSupportsHWIntrinsic(InstructionSet_AVXVNNIINT);
            }

            case INS_vpmadd52huq or INS_vpmadd52luq:
            {
                return (_compiler ?? throw new System.InvalidOperationException("Emitter is not initialized."))
                    .compSupportsHWIntrinsic(InstructionSet_AVXIFMA);
            }
        }
#endif
        return (prefixFlags(ins) & Encoding_VEX) != 0;
    }

    private bool IsRex2EncodableInstruction(instruction ins) =>
        UseRex2Encodings && (prefixFlags(ins) & Encoding_REX2) != 0;

    private static bool IsRexW0Instruction(instruction ins)
    {
        var flags = prefixFlags(ins);
        if ((flags & REX_W0) == 0)
        {
            return false;
        }
        assert((flags & (REX_W1 | REX_WX | REX_W1_EVEX)) == 0);
        return true;
    }

    private static bool IsRexW1Instruction(instruction ins)
    {
        var flags = prefixFlags(ins);
        if ((flags & REX_W1) == 0)
        {
            return false;
        }
        assert((flags & (REX_W0 | REX_WX | REX_W1_EVEX)) == 0);
        return true;
    }

    private static bool IsRexWXInstruction(instruction ins)
    {
        var flags = prefixFlags(ins);
        if ((flags & REX_WX) == 0)
        {
            return false;
        }
        assert((flags & (REX_W0 | REX_W1 | REX_W1_EVEX)) == 0);
        return true;
    }

    private static bool IsRexW1EvexInstruction(instruction ins)
    {
        var flags = prefixFlags(ins);
        if ((flags & REX_W1_EVEX) == 0)
        {
            return false;
        }
        assert((flags & (REX_W0 | REX_W1 | REX_WX)) == 0);
        return true;
    }

    private static bool HasApxPpx(instruction ins) => ins is INS_push or INS_pop or INS_push2 or INS_pop2;
}
#endif
