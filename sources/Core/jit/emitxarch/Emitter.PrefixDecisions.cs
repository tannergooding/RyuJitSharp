// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
namespace RyuJitSharp;

public partial class Emitter
{
    private bool TakesSimdPrefix(instrDesc id) => TakesVexPrefix(id.idIns()) || TakesEvexPrefix(id);

    private bool TakesVexPrefix(instruction ins) => IsVexEncodableInstruction(ins) && ins != INS_vzeroupper;

    internal bool TakesEvexPrefix(instrDesc id)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "EVEX prefix decisions require AMD64.");
#else
        var ins = id.idIns();
        if (!IsEvexEncodableInstruction(ins))
        {
            return false;
        }

        if (IsApxExtendedEvexInstruction(ins) && !IsSimdInstruction(ins))
        {
            if (IsApxOnlyInstruction(ins))
            {
                return true;
            }

            if (id.idIsNoApxEvexPromotion())
            {
                return false;
            }

            if (IsApxNddCompatibleInstruction(ins) && id.idIsEvexNdContextSet())
            {
                return true;
            }

            if (IsApxNfCompatibleInstruction(ins) && id.idIsEvexNfContextSet())
            {
                return true;
            }

#if DEBUG
            if ((_compiler ?? throw new System.InvalidOperationException("Emitter is not initialized."))
                .DoJitStressPromotedEvexEncoding())
            {
                if (ins >= INS_cmovo && ins <= INS_cmovg)
                {
                    return false;
                }
                return true;
            }
#endif
            return false;
        }

        if (!IsVexEncodableInstruction(ins))
        {
            return true;
        }

        if (HasHighSIMDReg(id) || id.idOpSize() == EA_64BYTE || HasMaskReg(id))
        {
            if (IsKMOVInstruction(ins))
            {
#if DEBUG
                if ((_compiler ?? throw new System.InvalidOperationException("Emitter is not initialized."))
                    .DoJitStressPromotedEvexEncoding())
                {
                    return true;
                }
#endif
                return HasExtendedGPReg(id);
            }
            return true;
        }

        if (id.idIsEvexbContextSet() || HasEmbeddedMask(id))
        {
            return true;
        }

        if (HasExtendedGPReg(id))
        {
            return true;
        }

        if (id.idIsEvexNfContextSet() && IsBMIInstruction(ins))
        {
            return true;
        }

#if DEBUG
        var compiler = _compiler ?? throw new System.InvalidOperationException("Emitter is not initialized.");
        if (compiler.DoJitStressEvexEncoding() && !IsBMIInstruction(ins) && !IsKMOVInstruction(ins))
        {
            return true;
        }
        if (compiler.DoJitStressPromotedEvexEncoding() && IsBMIInstruction(ins))
        {
            return true;
        }
#endif

        if (id.idHasMem())
        {
            if (ins is INS_pslldq or INS_psrldq)
            {
                return true;
            }

            if ((insTupleTypeInfo(ins) & INS_TT_MEM128) != 0)
            {
                assert(ins is INS_pslld or INS_psllq or INS_psllw or INS_psrad or
                    INS_psraw or INS_psrld or INS_psrlq or INS_psrlw);
                if (id.idHasMemAndCns())
                {
                    return true;
                }
            }
        }
        return false;
#endif
    }

    private bool TakesRex2Prefix(instrDesc id)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "REX2 prefix decisions require AMD64.");
#else
        var ins = id.idIns();
        if (!IsRex2EncodableInstruction(ins) || TakesEvexPrefix(id))
        {
            return false;
        }

        if (HasExtendedGPReg(id) || (ins >= INS_imul_16 && ins <= INS_imul_31)
            || id.idIsApxPpxContextSet())
        {
            return true;
        }

#if DEBUG
        if ((_compiler ?? throw new System.InvalidOperationException("Emitter is not initialized."))
            .DoJitStressRex2Encoding())
        {
            return ins is not (INS_i_jmp or INS_tail_i_jmp);
        }
#endif
        return false;
#endif
    }

    private bool TakesRexWPrefix(instrDesc id)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "REX.W prefix decisions require AMD64.");
#else
        var ins = id.idIns();
        var attr = id.idOpSize();

        if (IsRexW0Instruction(ins))
        {
            return false;
        }
        if (IsRexW1Instruction(ins))
        {
            return true;
        }
        if (IsRexW1EvexInstruction(ins))
        {
            return TakesEvexPrefix(id);
        }

        if (IsRexWXInstruction(ins))
        {
            switch (ins)
            {
                case INS_andn or INS_bextr or INS_blsi or INS_blsmsk or INS_blsr or INS_bzhi or
                     INS_mulx or INS_pdep or INS_pext or INS_rorx or INS_sarx or INS_shlx or INS_shrx:
                {
                    if (attr == EA_8BYTE)
                    {
                        return true;
                    }
                    assert(attr is EA_4BYTE or EA_16BYTE);
                    return false;
                }

                case INS_gf2p8affineinvqb or INS_gf2p8affineqb:
                {
                    return TakesVexPrefix(ins);
                }

                default:
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Unsupported REX.WX instruction.");
                }
            }
        }

        assert(!IsSimdInstruction(ins));
        if (ins == INS_movsx)
        {
            return true;
        }
        if (EA_SIZE(attr) != EA_8BYTE)
        {
            return false;
        }
        return ins is not (INS_push or INS_pop or INS_movq or INS_movzx or INS_push_hide or
            INS_pop_hide or INS_ret or INS_call or INS_tail_i_jmp) &&
            !(ins >= INS_i_jmp && ins <= INS_l_jg);
#endif
    }

    private static bool HasEmbeddedMask(instrDesc id) =>
        id.idIsEvexAaaContextSet() || id.idIsEvexZContextSet();

    private static bool HasEmbeddedBroadcast(instrDesc id)
    {
        assert(id.idHasMem());
        return (id.idGetEvexbContext() & 1) != 0;
    }

    private void SetEvexCompressedDisplacement(instrDesc id)
    {
        assert(id.idHasMem());
        assert(UseEvexEncodings);
        id.idSetEvexCompressedDisplacementBit();
    }
}
#endif
