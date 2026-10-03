// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    private const ulong EXTENDED_EVEX_PP_BITS = 0x10000000000UL;

    private static bool HasRegularWideForm(instruction ins) => (CodeGen.instInfo[(int)ins] & INS_FLAGS_HasWBit) != 0;

    private static bool HasCompressedDisplacement(instrDesc id)
    {
        assert(id.idHasMem());

        return (id.idGetEvexbContext() & (uint)INS_OPTS_EVEX_cd) != 0;
    }

    private static uint insSSval(uint scale)
    {
        assert(scale is 1 or 2 or 4 or 8);
        ReadOnlySpan<byte> scales = [0x00, 0x40, 0xFF, 0x80, 0xFF, 0xFF, 0xFF, 0xC0];

        return scales[(int)scale - 1];
    }

#if TARGET_AMD64
    private ulong AddEvexVPrimePrefix(ulong code)
    {
        assert((UseEvexEncodings || UsePromotedEvexEncodings) && hasEvexPrefix(code));

        return code & 0xFFFFFFF7FFFFFFFFUL;
    }

    private unsafe uint insEncodeRegSIB(instrDesc id, regNumber reg, ulong* code)
    {
        assert(reg < REG_STK);
        assert((code != null) || (reg < REG_R8) || ((reg >= REG_XMM0) && (reg < REG_XMM8)));
        if (IsExtendedReg(reg))
        {
            if (isHighSimdReg(reg))
            {
                *code = AddEvexVPrimePrefix(*code);
            }
            if (((uint)AbsRegNumber(reg) & 8) != 0)
            {
                *code = AddRexXPrefix(id, *code);
            }
            if (IsExtendedGPReg(reg))
            {
                assert(TakesRex2Prefix(id) || TakesEvexPrefix(id));
                if (hasRex2Prefix(*code))
                {
                    *code |= 0x002000000000UL;
                }
                else if (hasEvexPrefix(*code))
                {
                    // APX uses X4 for the high index bit; VSIB uses V4 instead.
                    *code &= 0xFFFFFBFFFFFFFFFFUL;
                }
            }
        }
        var regBits = RegEncoding(reg);
        assert(regBits < 8);

        return regBits;
    }
#endif
#endif
}
