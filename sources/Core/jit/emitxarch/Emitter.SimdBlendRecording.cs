// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    public static bool IsAVXVNNIFamilyInstruction(instruction ins)
    {
        return ((ins >= FIRST_AVXVNNI_INSTRUCTION) && (ins <= LAST_AVXVNNI_INSTRUCTION))
            || IsAVXVNNIINTInstruction(ins);
    }

    public static bool IsAVXVNNIINTInstruction(instruction ins)
    {
        return ((ins >= FIRST_AVXVNNIINT8_INSTRUCTION) && (ins <= LAST_AVXVNNIINT8_INSTRUCTION))
            || ((ins >= FIRST_AVXVNNIINT16_INSTRUCTION) && (ins <= LAST_AVXVNNIINT16_INSTRUCTION));
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
                // instrsxarch.h:1050-1083 defines the AVX10v1 FMA range by these endpoints.
                return ((ins >= FIRST_FMA_INSTRUCTION) && (ins <= LAST_FMA_INSTRUCTION))
                    || IsAVXVNNIFamilyInstruction(ins)
                    || ((ins >= FIRST_AVX512BMM_INSTRUCTION) && (ins <= LAST_AVX512BMM_INSTRUCTION))
                    || ((ins >= FIRST_AVXIFMA_INSTRUCTION) && (ins <= LAST_AVXIFMA_INSTRUCTION))
                    || ((ins >= INS_vfmadd132ph) && (ins <= INS_vfnmsub231sh));
            }
        }
    }

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
