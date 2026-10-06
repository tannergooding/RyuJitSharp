// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;

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
