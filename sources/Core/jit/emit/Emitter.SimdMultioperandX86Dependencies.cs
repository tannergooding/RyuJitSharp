// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public partial class Emitter
{
    public bool UseSimdEncoding()
    {
        return UseVexEncodings || UseEvexEncodings;
    }

    public static bool IsAVXVNNIFamilyInstruction(instruction ins)
    {
        return ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, FIRST_AVXVNNI_INSTRUCTION, LAST_AVXVNNI_INSTRUCTION)))
            || IsAVXVNNIINTInstruction(ins);
    }

    public static bool IsAVXVNNIINTInstruction(instruction ins)
    {
        return ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, FIRST_AVXVNNIINT8_INSTRUCTION, LAST_AVXVNNIINT8_INSTRUCTION)))
            || ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, FIRST_AVXVNNIINT16_INSTRUCTION, LAST_AVXVNNIINT16_INSTRUCTION)));
    }

    public static bool Is3OpRmwInstruction(instruction ins)
    {
        switch (ins)
        {
            case INS_vpermi2d:
            case INS_vpermi2pd:
            case INS_vpermi2ps:
            case INS_vpermi2q:
            case INS_vpermt2d:
            case INS_vpermt2pd:
            case INS_vpermt2ps:
            case INS_vpermt2q:
            case INS_vpermi2w:
            case INS_vpermt2w:
            case INS_vpermi2b:
            case INS_vpermt2b:
            {
                return true;
            }

            default:
            {
                return ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, FIRST_FMA_INSTRUCTION, LAST_FMA_INSTRUCTION)))
                    || IsAVXVNNIFamilyInstruction(ins)
                    || ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, FIRST_AVX512BMM_INSTRUCTION, LAST_AVX512BMM_INSTRUCTION)))
                    || ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, FIRST_AVXIFMA_INSTRUCTION, LAST_AVXIFMA_INSTRUCTION)))
                    || ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vfmadd132ph, INS_vfnmsub231sh)));
            }
        }
    }

#if FEATURE_HW_INTRINSICS
    private static bool isAvx512Blendv(instruction ins)
    {
        return ins is INS_vblendmps or INS_vblendmpd or INS_vpblendmb or INS_vpblendmd or INS_vpblendmq or INS_vpblendmw;
    }

    private static bool isAvxBlendv(instruction ins)
    {
        return ins is INS_vblendvps or INS_vblendvpd or INS_vpblendvb;
    }

    private static bool isSse41Blendv(instruction ins)
    {
        return ins is INS_blendvps or INS_blendvpd or INS_pblendvb;
    }
#endif
}
#endif
