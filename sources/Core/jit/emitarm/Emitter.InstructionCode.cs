// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    private uint emitInsCodeArm32(instruction ins, insFormat fmt)
    {
        RequireArm32Target();
        var code = BAD_CODE;
        var insFmt = emitInsFormatArm32(ins);
        var found = false;
        var index = 0;

        ReadOnlySpan<insFormat> formats = insFmt switch
        {
            IF_ARM32_EN9 => [IF_ARM32_T1_D0, IF_ARM32_T1_H, IF_ARM32_T1_J0, IF_ARM32_T1_G, IF_ARM32_T2_L0, IF_ARM32_T2_C0, IF_ARM32_T1_F, IF_ARM32_T1_J2, IF_ARM32_T1_J3],
            IF_ARM32_EN8 => [IF_ARM32_T1_H, IF_ARM32_T1_C, IF_ARM32_T2_E0, IF_ARM32_T2_H0, IF_ARM32_T2_K1, IF_ARM32_T2_K4, IF_ARM32_T1_J2, IF_ARM32_T1_J3],
            IF_ARM32_EN6A => [IF_ARM32_T1_H, IF_ARM32_T1_C, IF_ARM32_T2_E0, IF_ARM32_T2_H0, IF_ARM32_T2_K1, IF_ARM32_T2_K4],
            IF_ARM32_EN6B => [IF_ARM32_T1_H, IF_ARM32_T1_C, IF_ARM32_T2_E0, IF_ARM32_T2_H0, IF_ARM32_T2_K1, IF_ARM32_T1_J2],
            IF_ARM32_EN5A => [IF_ARM32_T1_E, IF_ARM32_T1_D0, IF_ARM32_T1_J0, IF_ARM32_T2_L1, IF_ARM32_T2_C3],
            IF_ARM32_EN5B => [IF_ARM32_T1_E, IF_ARM32_T1_D0, IF_ARM32_T1_J0, IF_ARM32_T2_L2, IF_ARM32_T2_C8],
            IF_ARM32_EN4A => [IF_ARM32_T1_E, IF_ARM32_T1_C, IF_ARM32_T2_C4, IF_ARM32_T2_C2],
            IF_ARM32_EN4B => [IF_ARM32_T2_K2, IF_ARM32_T2_H2, IF_ARM32_T2_C7, IF_ARM32_T2_K3],
            IF_ARM32_EN4C => [IF_ARM32_T2_N, IF_ARM32_T2_N1, IF_ARM32_T2_N2, IF_ARM32_T2_N3],
            IF_ARM32_EN3A => [IF_ARM32_T1_E, IF_ARM32_T2_C0, IF_ARM32_T2_L0],
            IF_ARM32_EN3B => [IF_ARM32_T1_E, IF_ARM32_T2_C8, IF_ARM32_T2_L2],
            IF_ARM32_EN3C => [IF_ARM32_T1_E, IF_ARM32_T2_C1, IF_ARM32_T2_L1],
            IF_ARM32_EN3D => [IF_ARM32_T1_L1, IF_ARM32_T2_E2, IF_ARM32_T2_I1],
            IF_ARM32_EN3E => [IF_ARM32_T1_M, IF_ARM32_T2_J2, IF_ARM32_T2_J3],
            IF_ARM32_EN2A => [IF_ARM32_T1_K, IF_ARM32_T2_J1],
            IF_ARM32_EN2B => [IF_ARM32_T1_D1, IF_ARM32_T1_D2],
            IF_ARM32_EN2C => [IF_ARM32_T1_D2, IF_ARM32_T2_J3],
            IF_ARM32_EN2D => [IF_ARM32_T1_J1, IF_ARM32_T2_I0],
            IF_ARM32_EN2E => [IF_ARM32_T1_E, IF_ARM32_T2_C6],
            IF_ARM32_EN2F => [IF_ARM32_T1_E, IF_ARM32_T2_C5],
            IF_ARM32_EN2G => [IF_ARM32_T1_J3, IF_ARM32_T2_M1],
            _ => [],
        };

        if (!formats.IsEmpty)
        {
            for (index = 0; index < formats.Length; index++)
            {
                if (fmt == formats[index])
                {
                    found = true;
                    break;
                }
            }
        }
        else
        {
            found = true;
        }

        assert(found);

        switch (index)
        {
            case 0:
            {
                assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)ordinaryInsCodes1_ARM.Length, "ins < ArrLen(insCodes1)");
                code = ordinaryInsCodes1_ARM[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 1:
            {
                assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)ordinaryInsCodes2_ARM.Length, "ins < ArrLen(insCodes2)");
                code = ordinaryInsCodes2_ARM[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 2:
            {
                assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)ordinaryInsCodes3_ARM.Length, "ins < ArrLen(insCodes3)");
                code = ordinaryInsCodes3_ARM[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 3:
            {
                assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)ordinaryInsCodes4_ARM.Length, "ins < ArrLen(insCodes4)");
                code = ordinaryInsCodes4_ARM[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 4:
            {
                assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)ordinaryInsCodes5_ARM.Length, "ins < ArrLen(insCodes5)");
                code = ordinaryInsCodes5_ARM[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 5:
            {
                assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)ordinaryInsCodes6_ARM.Length, "ins < ArrLen(insCodes6)");
                code = ordinaryInsCodes6_ARM[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 6:
            {
                assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)ordinaryInsCodes7_ARM.Length, "ins < ArrLen(insCodes7)");
                code = ordinaryInsCodes7_ARM[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 7:
            {
                assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)ordinaryInsCodes8_ARM.Length, "ins < ArrLen(insCodes8)");
                code = ordinaryInsCodes8_ARM[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }

            case 8:
            {
                assert((uint)InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins) < (uint)ordinaryInsCodes9_ARM.Length, "ins < ArrLen(insCodes9)");
                code = ordinaryInsCodes9_ARM[InstructionOrdinalMaps.GetNativeInstructionOrdinal(ins)];
                break;
            }
        }

        assert(code != BAD_CODE, "(code != BAD_CODE)");

        return code;
    }
}
