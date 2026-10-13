// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private uint emitInsCodeArm64(instruction ins, insFormat fmt)
    {
        RequireArm64Target();
        var code = BAD_CODE;
        var insFmt = emitInsFormatArm64(ins);
        var encoding_found = false;
        var index = -1;

        ReadOnlySpan<insFormat> formats = insFmt switch
        {
            IF_ARM64_EN9 => [
                IF_ARM64_DR_2E, IF_ARM64_DR_2G, IF_ARM64_DI_1B, IF_ARM64_DI_1D, IF_ARM64_DV_3C,
                IF_ARM64_DV_2B, IF_ARM64_DV_2C, IF_ARM64_DV_2E, IF_ARM64_DV_2F,
            ],
            IF_ARM64_EN6A => [IF_ARM64_DR_3A, IF_ARM64_DR_3B, IF_ARM64_DR_3C, IF_ARM64_DI_2A, IF_ARM64_DV_3A, IF_ARM64_DV_3E],
            IF_ARM64_EN6B => [IF_ARM64_LS_2D, IF_ARM64_LS_3F, IF_ARM64_LS_2E, IF_ARM64_LS_2F, IF_ARM64_LS_3G, IF_ARM64_LS_2G],
            IF_ARM64_EN5A => [IF_ARM64_LS_2A, IF_ARM64_LS_2B, IF_ARM64_LS_2C, IF_ARM64_LS_3A, IF_ARM64_LS_1A],
            IF_ARM64_EN5B => [IF_ARM64_DV_2G, IF_ARM64_DV_2H, IF_ARM64_DV_2I, IF_ARM64_DV_1A, IF_ARM64_DV_1B],
            IF_ARM64_EN5C => [IF_ARM64_DR_3A, IF_ARM64_DR_3B, IF_ARM64_DI_2C, IF_ARM64_DV_3C, IF_ARM64_DV_1B],
            IF_ARM64_EN4A => [IF_ARM64_LS_2A, IF_ARM64_LS_2B, IF_ARM64_LS_2C, IF_ARM64_LS_3A],
            IF_ARM64_EN4B => [IF_ARM64_DR_3A, IF_ARM64_DR_3B, IF_ARM64_DR_3C, IF_ARM64_DI_2A],
            IF_ARM64_EN4C => [IF_ARM64_DR_2A, IF_ARM64_DR_2B, IF_ARM64_DR_2C, IF_ARM64_DI_1A],
            IF_ARM64_EN4D => [IF_ARM64_DV_3B, IF_ARM64_DV_3D, IF_ARM64_DV_3BI, IF_ARM64_DV_3DI],
            IF_ARM64_EN4E => [IF_ARM64_DR_3A, IF_ARM64_DR_3B, IF_ARM64_DI_2C, IF_ARM64_DV_3C],
            IF_ARM64_EN4F => [IF_ARM64_DR_3A, IF_ARM64_DR_3B, IF_ARM64_DV_3C, IF_ARM64_DV_1B],
            IF_ARM64_EN4G => [IF_ARM64_DR_2E, IF_ARM64_DR_2F, IF_ARM64_DV_2M, IF_ARM64_DV_2L],
            IF_ARM64_EN4H => [IF_ARM64_DV_3E, IF_ARM64_DV_3A, IF_ARM64_DV_2L, IF_ARM64_DV_2M],
            IF_ARM64_EN4I => [IF_ARM64_DV_3D, IF_ARM64_DV_3B, IF_ARM64_DV_2G, IF_ARM64_DV_2A],
            IF_ARM64_EN4J => [IF_ARM64_DV_2N, IF_ARM64_DV_2O, IF_ARM64_DV_3E, IF_ARM64_DV_3A],
            IF_ARM64_EN4K => [IF_ARM64_DV_3E, IF_ARM64_DV_3A, IF_ARM64_DV_3EI, IF_ARM64_DV_3AI],
            IF_ARM64_EN3A => [IF_ARM64_DR_3A, IF_ARM64_DR_3B, IF_ARM64_DI_2C],
            IF_ARM64_EN3B => [IF_ARM64_DR_2A, IF_ARM64_DR_2B, IF_ARM64_DI_1C],
            IF_ARM64_EN3C => [IF_ARM64_DR_3A, IF_ARM64_DR_3B, IF_ARM64_DV_3C],
            IF_ARM64_EN3D => [IF_ARM64_DV_2C, IF_ARM64_DV_2D, IF_ARM64_DV_2E],
            IF_ARM64_EN3E => [IF_ARM64_DV_3B, IF_ARM64_DV_3BI, IF_ARM64_DV_3DI],
            IF_ARM64_EN3F => [IF_ARM64_DV_2A, IF_ARM64_DV_2G, IF_ARM64_DV_2H],
            IF_ARM64_EN3G => [IF_ARM64_DV_2A, IF_ARM64_DV_2G, IF_ARM64_DV_2I],
            IF_ARM64_EN3H => [IF_ARM64_DR_3A, IF_ARM64_DV_3A, IF_ARM64_DV_3AI],
            IF_ARM64_EN3I => [IF_ARM64_DR_2E, IF_ARM64_DR_2F, IF_ARM64_DV_2M],
            IF_ARM64_EN3J => [IF_ARM64_LS_2D, IF_ARM64_LS_3F, IF_ARM64_LS_2E],
            IF_ARM64_EN2A => [IF_ARM64_DR_2E, IF_ARM64_DR_2F],
            IF_ARM64_EN2B => [IF_ARM64_DR_3A, IF_ARM64_DR_3B],
            IF_ARM64_EN2C => [IF_ARM64_DR_3A, IF_ARM64_DI_2D],
            IF_ARM64_EN2D => [IF_ARM64_DR_3A, IF_ARM64_DI_2B],
            IF_ARM64_EN2E => [IF_ARM64_LS_3B, IF_ARM64_LS_3C],
            IF_ARM64_EN2F => [IF_ARM64_DR_2I, IF_ARM64_DI_1F],
            IF_ARM64_EN2G => [IF_ARM64_DV_3B, IF_ARM64_DV_3D],
            IF_ARM64_EN2H => [IF_ARM64_DV_2C, IF_ARM64_DV_2F],
            IF_ARM64_EN2I => [IF_ARM64_DV_2K, IF_ARM64_DV_1C],
            IF_ARM64_EN2J => [IF_ARM64_DV_2A, IF_ARM64_DV_2G],
            IF_ARM64_EN2K => [IF_ARM64_DV_2M, IF_ARM64_DV_2L],
            IF_ARM64_EN2L => [IF_ARM64_DR_2G, IF_ARM64_DV_2M],
            IF_ARM64_EN2M => [IF_ARM64_DV_3A, IF_ARM64_DV_3AI],
            IF_ARM64_EN2N => [IF_ARM64_DV_2N, IF_ARM64_DV_2O],
            IF_ARM64_EN2O => [IF_ARM64_DV_3E, IF_ARM64_DV_3A],
            IF_ARM64_EN2P => [IF_ARM64_DV_2Q, IF_ARM64_DV_3B],
            IF_ARM64_EN2Q => [IF_ARM64_DV_2S, IF_ARM64_DV_3A],
            _ => [],
        };

        if (!formats.IsEmpty)
        {
            for (index = 0; index < formats.Length; index++)
            {
                if (fmt == formats[index])
                {
                    encoding_found = true;
                    break;
                }
            }
        }
        else if (fmt == insFmt)
        {
            encoding_found = true;
            index = 0;
        }
        else
        {
            encoding_found = false;
        }

        assert(encoding_found, nameof(encoding_found));

        switch (index)
        {
            case 0:
            {
                assert(unchecked((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)) < (uint)ordinaryInsCodes1_ARM64.Length, "ins < ArrLen(insCodes1)");
                code = ordinaryInsCodes1_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 1:
            {
                assert(unchecked((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)) < (uint)ordinaryInsCodes2_ARM64.Length, "ins < ArrLen(insCodes2)");
                code = ordinaryInsCodes2_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 2:
            {
                assert(unchecked((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)) < (uint)ordinaryInsCodes3_ARM64.Length, "ins < ArrLen(insCodes3)");
                code = ordinaryInsCodes3_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 3:
            {
                assert(unchecked((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)) < (uint)ordinaryInsCodes4_ARM64.Length, "ins < ArrLen(insCodes4)");
                code = ordinaryInsCodes4_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 4:
            {
                assert(unchecked((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)) < (uint)ordinaryInsCodes5_ARM64.Length, "ins < ArrLen(insCodes5)");
                code = ordinaryInsCodes5_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 5:
            {
                assert(unchecked((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)) < (uint)ordinaryInsCodes6_ARM64.Length, "ins < ArrLen(insCodes6)");
                code = ordinaryInsCodes6_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 6:
            {
                assert(unchecked((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)) < (uint)ordinaryInsCodes7_ARM64.Length, "ins < ArrLen(insCodes7)");
                code = ordinaryInsCodes7_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 7:
            {
                assert(unchecked((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)) < (uint)ordinaryInsCodes8_ARM64.Length, "ins < ArrLen(insCodes8)");
                code = ordinaryInsCodes8_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 8:
            {
                assert(unchecked((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)) < (uint)ordinaryInsCodes9_ARM64.Length, "ins < ArrLen(insCodes9)");
                code = ordinaryInsCodes9_ARM64[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }
        }

        assert(code != BAD_CODE, "(code != BAD_CODE)");

        return code;
    }
}
