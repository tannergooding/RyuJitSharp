// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    internal enum insDisasmFmt : byte
    {
        DF_G_INVALID,
        DF_G_ALIAS,
        DF_G_B2,
        DF_G_B1,
        DF_G_B0,
        DF_G_15I,
        DF_G_R20I,
        DF_G_2R,
        DF_G_2R5IU,
        DF_G_2R6IU,
        DF_G_2R5IW,
        DF_G_2R6ID,
        DF_G_2R12I,
        DF_G_2R12IU,
        DF_G_2R14I,
        DF_G_2R16I,
        DF_G_3R,
        DF_G_3R2IU,
        DF_G_3RX,
        DF_F_B1,
        DF_F_GR,
        DF_F_RG,
        DF_F_RG12I,
        DF_F_FG,
        DF_F_GF,
        DF_F_CR,
        DF_F_RC,
        DF_F_CG,
        DF_F_GC,
        DF_F_C2R,
        DF_F_2R,
        DF_F_R2G,
        DF_F_3R,
        DF_F_4R,
        DF_F_3RX3,
        DF_S_4R,
        DF_S_3R,
        DF_S_RG,
        DF_S_2R,
        DF_S_2RX,
        DF_S_CR,
        DF_S_RGX,
        DF_S_GRX,
        DF_S_2RG,
        DF_S_2R3IU,
        DF_S_2R4IU,
        DF_S_2R5I,
        DF_S_2R5IU,
        DF_S_2R6IU,
        DF_S_2R7IU,
        DF_S_2R8IU,
        DF_S_2R8IX,
        DF_S_R13IU,
        DF_A_4R,
        DF_A_3R,
        DF_A_RG,
        DF_A_2R,
        DF_A_CR,
        DF_A_RGX,
        DF_A_GRX,
        DF_A_2RG,
        DF_A_2RX,
        DF_A_2R3IU,
        DF_A_2R4IU,
        DF_A_2R5I,
        DF_A_2R5IU,
        DF_A_2R6IU,
        DF_A_2R7IU,
        DF_A_2R8IU,
        DF_A_R13IU,
        DF_A_RG8IX,
    }

    private static uint emitInsCodeLoongArch64(instruction ins)
    {
        RequireLoongArch64Target();
        if (!Configuration.FEATURE_SIMD &&
            (uint)ins >= (uint)INS_vfmadd_s &&
            (uint)ins <= (uint)INS_xvsetallnez_d)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "LoongArch64 SIMD instruction encoding requires FEATURE_SIMD.");
            return default!;
        }

        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)insCodesLoongArch64.Length);
        assert(insCodesLoongArch64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)] != Globals.BAD_CODE);

        return insCodesLoongArch64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }

    private static uint emitGetInsMaskLoongArch64(instruction ins)
    {
        RequireLoongArch64Target();
        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)insMasksLoongArch64.Length);

        return insMasksLoongArch64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }

    private static insDisasmFmt emitGetInsFmtLoongArch64(instruction ins)
    {
        RequireLoongArch64Target();
        assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)insDisasmFormatsLoongArch64.Length);

        return insDisasmFormatsLoongArch64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
    }
}
