// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private uint emitInsCode(instruction ins, insFormat fmt)
    {
        var code = BAD_CODE;
        var insFmt = emitInsFormat(ins);
        var encoding_found = false;
        var index = -1;

        ReadOnlySpan<insFormat> formats = insFmt switch
        {
            IF_EN9 => [
                IF_DR_2E, IF_DR_2G, IF_DI_1B, IF_DI_1D, IF_DV_3C,
                IF_DV_2B, IF_DV_2C, IF_DV_2E, IF_DV_2F,
            ],
            IF_EN6A => [IF_DR_3A, IF_DR_3B, IF_DR_3C, IF_DI_2A, IF_DV_3A, IF_DV_3E],
            IF_EN6B => [IF_LS_2D, IF_LS_3F, IF_LS_2E, IF_LS_2F, IF_LS_3G, IF_LS_2G],
            IF_EN5A => [IF_LS_2A, IF_LS_2B, IF_LS_2C, IF_LS_3A, IF_LS_1A],
            IF_EN5B => [IF_DV_2G, IF_DV_2H, IF_DV_2I, IF_DV_1A, IF_DV_1B],
            IF_EN5C => [IF_DR_3A, IF_DR_3B, IF_DI_2C, IF_DV_3C, IF_DV_1B],
            IF_EN4A => [IF_LS_2A, IF_LS_2B, IF_LS_2C, IF_LS_3A],
            IF_EN4B => [IF_DR_3A, IF_DR_3B, IF_DR_3C, IF_DI_2A],
            IF_EN4C => [IF_DR_2A, IF_DR_2B, IF_DR_2C, IF_DI_1A],
            IF_EN4D => [IF_DV_3B, IF_DV_3D, IF_DV_3BI, IF_DV_3DI],
            IF_EN4E => [IF_DR_3A, IF_DR_3B, IF_DI_2C, IF_DV_3C],
            IF_EN4F => [IF_DR_3A, IF_DR_3B, IF_DV_3C, IF_DV_1B],
            IF_EN4G => [IF_DR_2E, IF_DR_2F, IF_DV_2M, IF_DV_2L],
            IF_EN4H => [IF_DV_3E, IF_DV_3A, IF_DV_2L, IF_DV_2M],
            IF_EN4I => [IF_DV_3D, IF_DV_3B, IF_DV_2G, IF_DV_2A],
            IF_EN4J => [IF_DV_2N, IF_DV_2O, IF_DV_3E, IF_DV_3A],
            IF_EN4K => [IF_DV_3E, IF_DV_3A, IF_DV_3EI, IF_DV_3AI],
            IF_EN3A => [IF_DR_3A, IF_DR_3B, IF_DI_2C],
            IF_EN3B => [IF_DR_2A, IF_DR_2B, IF_DI_1C],
            IF_EN3C => [IF_DR_3A, IF_DR_3B, IF_DV_3C],
            IF_EN3D => [IF_DV_2C, IF_DV_2D, IF_DV_2E],
            IF_EN3E => [IF_DV_3B, IF_DV_3BI, IF_DV_3DI],
            IF_EN3F => [IF_DV_2A, IF_DV_2G, IF_DV_2H],
            IF_EN3G => [IF_DV_2A, IF_DV_2G, IF_DV_2I],
            IF_EN3H => [IF_DR_3A, IF_DV_3A, IF_DV_3AI],
            IF_EN3I => [IF_DR_2E, IF_DR_2F, IF_DV_2M],
            IF_EN3J => [IF_LS_2D, IF_LS_3F, IF_LS_2E],
            IF_EN2A => [IF_DR_2E, IF_DR_2F],
            IF_EN2B => [IF_DR_3A, IF_DR_3B],
            IF_EN2C => [IF_DR_3A, IF_DI_2D],
            IF_EN2D => [IF_DR_3A, IF_DI_2B],
            IF_EN2E => [IF_LS_3B, IF_LS_3C],
            IF_EN2F => [IF_DR_2I, IF_DI_1F],
            IF_EN2G => [IF_DV_3B, IF_DV_3D],
            IF_EN2H => [IF_DV_2C, IF_DV_2F],
            IF_EN2I => [IF_DV_2K, IF_DV_1C],
            IF_EN2J => [IF_DV_2A, IF_DV_2G],
            IF_EN2K => [IF_DV_2M, IF_DV_2L],
            IF_EN2L => [IF_DR_2G, IF_DV_2M],
            IF_EN2M => [IF_DV_3A, IF_DV_3AI],
            IF_EN2N => [IF_DV_2N, IF_DV_2O],
            IF_EN2O => [IF_DV_3E, IF_DV_3A],
            IF_EN2P => [IF_DV_2Q, IF_DV_3B],
            IF_EN2Q => [IF_DV_2S, IF_DV_3A],
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
                assert(unchecked((uint)ins) < (uint)ordinaryInsCodes1.Length, "ins < ArrLen(insCodes1)");
                code = ordinaryInsCodes1[(int)ins];
                break;
            }

            case 1:
            {
                assert(unchecked((uint)ins) < (uint)ordinaryInsCodes2.Length, "ins < ArrLen(insCodes2)");
                code = ordinaryInsCodes2[(int)ins];
                break;
            }

            case 2:
            {
                assert(unchecked((uint)ins) < (uint)ordinaryInsCodes3.Length, "ins < ArrLen(insCodes3)");
                code = ordinaryInsCodes3[(int)ins];
                break;
            }

            case 3:
            {
                assert(unchecked((uint)ins) < (uint)ordinaryInsCodes4.Length, "ins < ArrLen(insCodes4)");
                code = ordinaryInsCodes4[(int)ins];
                break;
            }

            case 4:
            {
                assert(unchecked((uint)ins) < (uint)ordinaryInsCodes5.Length, "ins < ArrLen(insCodes5)");
                code = ordinaryInsCodes5[(int)ins];
                break;
            }

            case 5:
            {
                assert(unchecked((uint)ins) < (uint)ordinaryInsCodes6.Length, "ins < ArrLen(insCodes6)");
                code = ordinaryInsCodes6[(int)ins];
                break;
            }

            case 6:
            {
                assert(unchecked((uint)ins) < (uint)ordinaryInsCodes7.Length, "ins < ArrLen(insCodes7)");
                code = ordinaryInsCodes7[(int)ins];
                break;
            }

            case 7:
            {
                assert(unchecked((uint)ins) < (uint)ordinaryInsCodes8.Length, "ins < ArrLen(insCodes8)");
                code = ordinaryInsCodes8[(int)ins];
                break;
            }

            case 8:
            {
                assert(unchecked((uint)ins) < (uint)ordinaryInsCodes9.Length, "ins < ArrLen(insCodes9)");
                code = ordinaryInsCodes9[(int)ins];
                break;
            }
        }

        assert(code != BAD_CODE, "(code != BAD_CODE)");

        return code;
    }
}
#endif
