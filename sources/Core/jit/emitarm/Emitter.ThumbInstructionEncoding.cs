// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public partial class Emitter
{
    internal static uint insEncodeSetFlags(insFlags flags)
    {
        return flags == INS_FLAGS_SET ? (1u << 20) : 0;
    }

    internal static uint insEncodeShiftOpts(insOpts options)
    {
        switch (options)
        {
            case INS_OPTS_NONE:
            case INS_OPTS_LSL:
            {
                return 0;
            }

            case INS_OPTS_LSR:
            {
                return 0x10;
            }

            case INS_OPTS_ASR:
            {
                return 0x20;
            }

            case INS_OPTS_ROR:
            case INS_OPTS_RRX:
            {
                return 0x30;
            }

            default:
            {
                assert(false);
                return 0;
            }
        }
    }

    internal static uint insEncodeShiftCount(int imm)
    {
        assert((imm & 0x001F) == imm);
        var result = (uint)(imm & 0x03) << 6;
        result |= (uint)(imm & 0x1C) << 10;

        return result;
    }

    internal static uint insEncodeBitFieldImm(int imm)
    {
        assert((imm & 0x03FF) == imm);
        var result = (uint)(imm & 0x001F);
        result |= (uint)(imm & 0x0060) << 1;
        result |= (uint)(imm & 0x0380) << 5;

        return result;
    }

    internal static uint insEncodeImmT2_Mov(int imm)
    {
        assert((imm & 0x0000FFFF) == imm);
        var result = (uint)(imm & 0x00FF);
        result |= (uint)(imm & 0x0700) << 4;
        result |= (uint)(imm & 0x0800) << 15;
        result |= (uint)(imm & 0xF000) << 4;

        return result;
    }

    internal static uint insEncodeRegT2_T(regNumber reg)
    {
        assert(reg < REG_STK);

        return (uint)reg << 12;
    }

    internal static uint insEncodeRegT2_D(regNumber reg)
    {
        assert(reg < REG_STK);

        return (uint)reg << 8;
    }

    internal static uint insEncodeRegT2_M(regNumber reg)
    {
        assert(reg < REG_STK);

        return (uint)reg;
    }

    internal static uint insEncodeRegT2_N(regNumber reg)
    {
        assert(reg < REG_STK);

        return (uint)reg << 16;
    }

    internal static uint floatRegIndex(regNumber reg, int size)
    {
        assert(size == (int)EA_8BYTE || size == (int)EA_4BYTE);

        if (size == (int)EA_8BYTE)
        {
            assert(isDoubleReg(reg));
        }
        else
        {
            assert(isFloatReg(reg));
        }

        var result = unchecked((uint)reg - (uint)REG_F0);
        if (size == (int)EA_8BYTE)
        {
            result >>= 1;
        }

        return result;
    }

    // Some ARM VFP instructions encode the split bit as the index's MSB for doubles
    // and as the index's LSB for singles.
    internal static uint floatRegEncoding(uint index, int size, bool variant = false)
    {
        if (!variant || size == (int)EA_8BYTE)
        {
            return index;
        }

        return ((index & 0x1) << 4) | (index >> 1);
    }

    internal static uint insEncodeRegT2_VectorM(regNumber reg, int size, bool variant)
    {
        var encoding = floatRegEncoding(floatRegIndex(reg, size), size, variant);
        return ((encoding & 0xF) << 0) | ((encoding & 0x10) << 1);
    }

    internal static uint insEncodeRegT2_VectorN(regNumber reg, int size, bool variant)
    {
        var encoding = floatRegEncoding(floatRegIndex(reg, size), size, variant);
        return ((encoding & 0xF) << 16) | ((encoding & 0x10) << 3);
    }

    internal static uint insEncodeRegT2_VectorD(regNumber reg, int size, bool variant)
    {
        var encoding = floatRegEncoding(floatRegIndex(reg, size), size, variant);
        return ((encoding & 0xF) << 12) | ((encoding & 0x10) << 18);
    }

    internal unsafe uint emitOutput_Thumb1Instr(byte* dst, uint code)
    {
        var word1 = code & 0xFFFF;
        assert(word1 == code);

#if DEBUG
        var top5bits = (word1 & 0xF800) >> 11;
        assert(top5bits < 29);
#endif

        return emitOutputWord(dst, word1);
    }

    internal unsafe uint emitOutput_Thumb2Instr(byte* dst, uint code)
    {
        var word1 = (code >> 16) & 0xFFFF;
        var word2 = code & 0xFFFF;
        assert(((word1 << 16) | word2) == code);

#if DEBUG
        var top5bits = (word1 & 0xF800) >> 11;
        assert(top5bits >= 29);
#endif

        emitOutputWord(dst, word1);
        emitOutputWord(unchecked(dst + sizeof(short)), word2);

        return sizeof(short) * 2;
    }

    internal static int insUnscaleImm(instruction ins, int imm)
    {
        switch (ins)
        {
            case INS_ldr:
            case INS_str:
            {
                assert((imm & 0x0003) == 0);
                imm >>= 2;
                break;
            }

            case INS_ldrh:
            case INS_strh:
            {
                assert((imm & 0x0001) == 0);
                imm >>= 1;
                break;
            }

            case INS_ldrb:
            case INS_strb:
            case INS_lsl:
            case INS_lsr:
            case INS_asr:
            {
                break;
            }

            default:
            {
                assert(false);
                break;
            }
        }

        return imm;
    }

    internal static uint insEncodePUW_G0(insOpts options, int imm)
    {
        uint result = 0;

        if (options != INS_OPTS_LDST_POST_INC)
        {
            result |= 1u << 24;
        }

        if (imm >= 0)
        {
            result |= 1u << 23;
        }

        if (options != INS_OPTS_NONE)
        {
            result |= 1u << 21;
        }

        return result;
    }

    internal static uint insEncodePUW_H0(insOpts options, int imm)
    {
        uint result = 0;

        if (options != INS_OPTS_LDST_POST_INC)
        {
            result |= 1u << 10;
        }

        if (imm >= 0)
        {
            result |= 1u << 9;
        }

        if (options != INS_OPTS_NONE)
        {
            result |= 1u << 8;
        }

        return result;
    }
}
#endif
