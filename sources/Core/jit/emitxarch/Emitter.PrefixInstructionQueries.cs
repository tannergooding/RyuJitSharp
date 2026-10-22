// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private static insInfoFlags prefixFlags(instruction ins) => CodeGen.instInfo[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];

    private static bool IsBMIInstruction(instruction ins) =>
        InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, FIRST_BMI_INSTRUCTION, LAST_BMI_INSTRUCTION);

    private static bool IsJccInstruction(instruction ins) =>
        InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_jo, INS_jg) ||
        InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_l_jo, INS_l_jg);

    private static bool IsJmpInstruction(instruction ins) =>
        ins is INS_i_jmp or INS_jmp or INS_l_jmp or INS_tail_i_jmp;

    private static bool IsKMOVInstruction(instruction ins) => ins is
        INS_kmovb_gpr or INS_kmovw_gpr or INS_kmovd_gpr or INS_kmovq_gpr or
        INS_kmovb_msk or INS_kmovw_msk or INS_kmovd_msk or INS_kmovq_msk;

    private static bool IsApxOnlyInstruction(instruction ins)
    {
        var isApxOnly = (prefixFlags(ins) & Encoding_EVEX_APX_ONLY) != 0;
        if (Configuration.DEBUG)
        {
            if (Configuration.TARGET_AMD64 && isApxOnly)
            {
                var known = InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_push2, INS_pop2)
                    || InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, FIRST_APX_INSTRUCTION, LAST_APX_INSTRUCTION)
                    || ins is INS_tzcnt_apx or INS_lzcnt_apx or INS_popcnt_apx;
                assert(known);
            }
        }
        return isApxOnly;
    }

    private static bool IsApxNfCompatibleInstruction(instruction ins) =>
        (prefixFlags(ins) & INS_FLAGS_HasNF) != 0;

    private bool IsApxExtendedEvexInstruction(instruction ins)
    {
        return Configuration.TARGET_AMD64 && UsePromotedEvexEncodings &&
            (prefixFlags(ins) & INS_FLAGS_ApxEvexMask) != 0;
    }

    internal bool IsVexEncodableInstruction(instruction ins)
    {
        if (!UseVexEncodings)
        {
            return false;
        }

        if (Configuration.FEATURE_HW_INTRINSICS)
        {
            switch (ins)
            {
                case INS_vpdpbusd or INS_vpdpwssd or INS_vpdpbusds or INS_vpdpwssds:
                {
                    global::RyuJitSharp.Compiler __jitExceptionReceiver397;
                    var __jitExceptionCoalesceLeft398 = _compiler;
                    if (__jitExceptionCoalesceLeft398 != null)
                    {
                        __jitExceptionReceiver397 = __jitExceptionCoalesceLeft398;
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsNotInitialized);
                        __jitExceptionReceiver397 = default;
                    }
                    return __jitExceptionReceiver397.compSupportsHWIntrinsic(InstructionSet_AVXVNNI);
                }

                case INS_vpdpwsud or INS_vpdpwsuds or INS_vpdpwusd or INS_vpdpwusds or INS_vpdpwuud or
                     INS_vpdpwuuds or INS_vpdpbssd or INS_vpdpbssds or INS_vpdpbsud or INS_vpdpbsuds or
                     INS_vpdpbuud or INS_vpdpbuuds:
                {
                    global::RyuJitSharp.Compiler __jitExceptionReceiver399;
                    var __jitExceptionCoalesceLeft400 = _compiler;
                    if (__jitExceptionCoalesceLeft400 != null)
                    {
                        __jitExceptionReceiver399 = __jitExceptionCoalesceLeft400;
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsNotInitialized);
                        __jitExceptionReceiver399 = default;
                    }
                    return __jitExceptionReceiver399.compSupportsHWIntrinsic(InstructionSet_AVXVNNIINT);
                }

                case INS_vpmadd52huq or INS_vpmadd52luq:
                {
                    global::RyuJitSharp.Compiler __jitExceptionReceiver401;
                    var __jitExceptionCoalesceLeft402 = _compiler;
                    if (__jitExceptionCoalesceLeft402 != null)
                    {
                        __jitExceptionReceiver401 = __jitExceptionCoalesceLeft402;
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsNotInitialized);
                        __jitExceptionReceiver401 = default;
                    }
                    return __jitExceptionReceiver401.compSupportsHWIntrinsic(InstructionSet_AVXIFMA);
                }
            }
        }

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
