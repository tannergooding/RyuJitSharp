// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    internal static bool isHighSimdReg(regNumber reg) => reg >= REG_XMM16 && reg <= REG_XMM31;

    private static bool isHighGPReg(regNumber reg) => reg >= REG_R16 && reg <= REG_R31;

    private static bool isMaskReg(regNumber reg) => reg >= REG_K0 && reg <= REG_K7;

    private static bool IsExtendedReg(regNumber reg)
    {
#if TARGET_AMD64
        return (reg >= REG_R8 && reg <= REG_R31) || (reg >= REG_XMM8 && reg <= REG_XMM31);
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Extended register prefixes require AMD64.");
#endif
    }

    internal bool IsExtendedGPReg(regNumber reg)
    {
#if TARGET_AMD64
        if (reg > REG_STK)
        {
            return false;
        }

        if (isHighGPReg(reg))
        {
            assert(UseRex2Encodings);
            return true;
        }
        return false;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Extended general purpose register prefixes require AMD64.");
#endif
    }

    private bool HasHighSIMDReg(instrDesc id)
    {
#if TARGET_AMD64
        if (isHighSimdReg(id.idReg1()) || isHighSimdReg(id.idReg2()))
        {
            return true;
        }
        if (id.idIsSmallDsc())
        {
            return false;
        }
        if ((id.idHasReg3() && isHighSimdReg(id.idReg3()))
            || (id.idHasReg4() && isHighSimdReg(id.idReg4())))
        {
            return true;
        }
        return false;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "High SIMD register prefixes require AMD64.");
#endif
    }

    private bool HasExtendedGPReg(instrDesc id)
    {
#if TARGET_AMD64
        if (id.idHasMemAdr() &&
            (IsExtendedGPReg(id.idAddr().iiaAddrMode.amBaseReg)
             || IsExtendedGPReg(id.idAddr().iiaAddrMode.amIndxReg)))
        {
            return true;
        }

        if ((id.idHasReg1() && IsExtendedGPReg(id.idReg1()))
            || (id.idHasReg2() && IsExtendedGPReg(id.idReg2()))
            || (id.idHasReg3() && IsExtendedGPReg(id.idReg3()))
            || (id.idHasReg4() && IsExtendedGPReg(id.idReg4())))
        {
            return true;
        }
        return false;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Extended general purpose register prefixes require AMD64.");
#endif
    }

    private bool HasMaskReg(instrDesc id)
    {
        if (isMaskReg(id.idReg1()))
        {
            assert(HasKMaskRegisterDest(id.idIns()));
            return true;
        }

#if DEBUG
        if (isMaskReg(id.idReg2()))
        {
            assert((prefixFlags(id.idIns()) & KInstruction) != 0);
            return UsePromotedEvexEncodings;
        }

        if (!id.idIsSmallDsc())
        {
            if (id.idHasReg3())
            {
                assert(!isMaskReg(id.idReg3()));
            }
            if (id.idHasReg4())
            {
                assert(!isMaskReg(id.idReg4()));
            }
        }
#endif
        return false;
    }

    private bool HasKMaskRegisterDest(instruction ins)
    {
        assert(UseEvexEncodings);
        return ins is
            INS_pcmpgtb or INS_pcmpgtd or INS_pcmpgtw or INS_pcmpgtq or
            INS_pcmpeqb or INS_pcmpeqd or INS_pcmpeqq or INS_pcmpeqw or
            INS_cmpps or INS_cmpss or INS_cmppd or INS_cmpsd or
            INS_vpgatherdd or INS_vpgatherqd or INS_vpgatherdq or INS_vpgatherqq or
            INS_vgatherdps or INS_vgatherqps or INS_vgatherdpd or INS_vgatherqpd or
            INS_kmovb_msk or INS_kmovw_msk or INS_kmovd_msk or INS_kmovq_msk;
    }
}
#endif
