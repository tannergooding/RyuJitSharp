// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public bool IsEvexEncodableInstruction(instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            return false;
        }

        var flags = CodeGen.instInfo[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
        var isBmi = InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, FIRST_BMI_INSTRUCTION, LAST_BMI_INSTRUCTION);
        var isKmov = ins is INS_kmovb_gpr or INS_kmovw_gpr or INS_kmovd_gpr or INS_kmovq_gpr or
            INS_kmovb_msk or INS_kmovw_msk or INS_kmovd_msk or INS_kmovq_msk;

        if (!UseEvexEncodings)
        {
            if (!UsePromotedEvexEncodings)
            {
                return false;
            }

            return ((flags & INS_FLAGS_ApxEvexMask) != 0) || isBmi || isKmov;
        }

        if ((flags & Encoding_EVEX) != 0)
        {
            if (Configuration.FEATURE_HW_INTRINSICS &&
                InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(
                ins, FIRST_AVXVNNIINT8_INSTRUCTION, LAST_AVXVNNIINT16_INSTRUCTION))
            {
                var compiler = _compiler ?? throw new System.InvalidOperationException("Emitter is not initialized.");
                return compiler.compSupportsHWIntrinsic(InstructionSet_AVXVNNIINT_V512);
            }
            return true;
        }

        if (((flags & INS_FLAGS_ApxEvexMask) != 0) || isBmi || isKmov)
        {
            return UsePromotedEvexEncodings;
        }

        if (Configuration.FEATURE_HW_INTRINSICS)
        {
            var instructionCompiler = _compiler ?? throw new System.InvalidOperationException("Emitter is not initialized.");
            return ins switch
            {
                INS_aesdec or INS_aesdeclast or INS_aesenc or INS_aesenclast or INS_pclmulqdq =>
                    instructionCompiler.compSupportsHWIntrinsic(InstructionSet_AES_V512),
                INS_vpdpbusd or INS_vpdpwssd or INS_vpdpbusds or INS_vpdpwssds =>
                    instructionCompiler.compSupportsHWIntrinsic(InstructionSet_AVX512v3),
                INS_vpmadd52huq or INS_vpmadd52luq =>
                    instructionCompiler.compSupportsHWIntrinsic(InstructionSet_AVX512v2),
                _ => false,
            };
        }

        return false;
    }
}
