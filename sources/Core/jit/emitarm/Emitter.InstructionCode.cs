// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    private uint emitInsCode(instruction ins, insFormat fmt)
    {
        var code = BAD_CODE;
        var insFmt = emitInsFormat(ins);
        var found = false;
        var index = 0;

        ReadOnlySpan<insFormat> formats = insFmt switch
        {
            IF_EN9 => [IF_T1_D0, IF_T1_H, IF_T1_J0, IF_T1_G, IF_T2_L0, IF_T2_C0, IF_T1_F, IF_T1_J2, IF_T1_J3],
            IF_EN8 => [IF_T1_H, IF_T1_C, IF_T2_E0, IF_T2_H0, IF_T2_K1, IF_T2_K4, IF_T1_J2, IF_T1_J3],
            IF_EN6A => [IF_T1_H, IF_T1_C, IF_T2_E0, IF_T2_H0, IF_T2_K1, IF_T2_K4],
            IF_EN6B => [IF_T1_H, IF_T1_C, IF_T2_E0, IF_T2_H0, IF_T2_K1, IF_T1_J2],
            IF_EN5A => [IF_T1_E, IF_T1_D0, IF_T1_J0, IF_T2_L1, IF_T2_C3],
            IF_EN5B => [IF_T1_E, IF_T1_D0, IF_T1_J0, IF_T2_L2, IF_T2_C8],
            IF_EN4A => [IF_T1_E, IF_T1_C, IF_T2_C4, IF_T2_C2],
            IF_EN4B => [IF_T2_K2, IF_T2_H2, IF_T2_C7, IF_T2_K3],
            IF_EN4C => [IF_T2_N, IF_T2_N1, IF_T2_N2, IF_T2_N3],
            IF_EN3A => [IF_T1_E, IF_T2_C0, IF_T2_L0],
            IF_EN3B => [IF_T1_E, IF_T2_C8, IF_T2_L2],
            IF_EN3C => [IF_T1_E, IF_T2_C1, IF_T2_L1],
            IF_EN3D => [IF_T1_L1, IF_T2_E2, IF_T2_I1],
            IF_EN3E => [IF_T1_M, IF_T2_J2, IF_T2_J3],
            IF_EN2A => [IF_T1_K, IF_T2_J1],
            IF_EN2B => [IF_T1_D1, IF_T1_D2],
            IF_EN2C => [IF_T1_D2, IF_T2_J3],
            IF_EN2D => [IF_T1_J1, IF_T2_I0],
            IF_EN2E => [IF_T1_E, IF_T2_C6],
            IF_EN2F => [IF_T1_E, IF_T2_C5],
            IF_EN2G => [IF_T1_J3, IF_T2_M1],
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
                assert((uint)ins < (uint)ordinaryInsCodes1.Length, "ins < ArrLen(insCodes1)");
                code = ordinaryInsCodes1[(int)ins];
                break;
            }

            case 1:
            {
                assert((uint)ins < (uint)ordinaryInsCodes2.Length, "ins < ArrLen(insCodes2)");
                code = ordinaryInsCodes2[(int)ins];
                break;
            }

            case 2:
            {
                assert((uint)ins < (uint)ordinaryInsCodes3.Length, "ins < ArrLen(insCodes3)");
                code = ordinaryInsCodes3[(int)ins];
                break;
            }

            case 3:
            {
                assert((uint)ins < (uint)ordinaryInsCodes4.Length, "ins < ArrLen(insCodes4)");
                code = ordinaryInsCodes4[(int)ins];
                break;
            }

            case 4:
            {
                assert((uint)ins < (uint)ordinaryInsCodes5.Length, "ins < ArrLen(insCodes5)");
                code = ordinaryInsCodes5[(int)ins];
                break;
            }

            case 5:
            {
                assert((uint)ins < (uint)ordinaryInsCodes6.Length, "ins < ArrLen(insCodes6)");
                code = ordinaryInsCodes6[(int)ins];
                break;
            }

            case 6:
            {
                assert((uint)ins < (uint)ordinaryInsCodes7.Length, "ins < ArrLen(insCodes7)");
                code = ordinaryInsCodes7[(int)ins];
                break;
            }

            case 7:
            {
                assert((uint)ins < (uint)ordinaryInsCodes8.Length, "ins < ArrLen(insCodes8)");
                code = ordinaryInsCodes8[(int)ins];
                break;
            }

            case 8:
            {
                assert((uint)ins < (uint)ordinaryInsCodes9.Length, "ins < ArrLen(insCodes9)");
                code = ordinaryInsCodes9[(int)ins];
                break;
            }
        }

        assert(code != BAD_CODE, "(code != BAD_CODE)");

        return code;
    }
}
#endif
