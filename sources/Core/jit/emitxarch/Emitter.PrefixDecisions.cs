// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private bool TakesSimdPrefix(instrDesc id) => TakesVexPrefix(id.idIns()) || TakesEvexPrefix(id);

    private bool TakesVexPrefix(instruction ins) => IsVexEncodableInstruction(ins) && ins != INS_vzeroupper;

    public ulong AddSimdPrefixIfNeeded(instrDesc id, ulong code, emitAttr size)
    {
        if (TakesEvexPrefix(id))
        {
            return AddEvexPrefix(id, code, size);
        }

        var ins = id.idIns();
        if (TakesVexPrefix(ins))
        {
            return AddVexPrefix(ins, code, size);
        }

        return code;
    }

    internal bool TakesEvexPrefix(instrDesc id)
    {
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

            bool __jitExceptionCondition383;
            bool __jitExceptionShortCircuitLeft384;
            __jitExceptionShortCircuitLeft384 = Configuration.DEBUG;
            if (__jitExceptionShortCircuitLeft384)
            {
                global::RyuJitSharp.Compiler __jitExceptionReceiver385;
                var __jitExceptionCoalesceLeft386 = _compiler;
                if (__jitExceptionCoalesceLeft386 != null)
                {
                    __jitExceptionReceiver385 = __jitExceptionCoalesceLeft386;
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsNotInitialized);
                    __jitExceptionReceiver385 = default;
                }

                __jitExceptionCondition383 = __jitExceptionReceiver385.DoJitStressPromotedEvexEncoding();
            }
            else
            {
                __jitExceptionCondition383 = false;
            }
            if (__jitExceptionCondition383)
            {
                if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_cmovo, INS_cmovg))
                {
                    return false;
                }
                return true;
            }
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
                bool __jitExceptionCondition387;
                bool __jitExceptionShortCircuitLeft388;
                __jitExceptionShortCircuitLeft388 = Configuration.DEBUG;
                if (__jitExceptionShortCircuitLeft388)
                {
                    global::RyuJitSharp.Compiler __jitExceptionReceiver389;
                    var __jitExceptionCoalesceLeft390 = _compiler;
                    if (__jitExceptionCoalesceLeft390 != null)
                    {
                        __jitExceptionReceiver389 = __jitExceptionCoalesceLeft390;
                    }
                    else
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsNotInitialized);
                        __jitExceptionReceiver389 = default;
                    }

                    __jitExceptionCondition387 = __jitExceptionReceiver389.DoJitStressPromotedEvexEncoding();
                }
                else
                {
                    __jitExceptionCondition387 = false;
                }
                if (__jitExceptionCondition387)
                {
                    return true;
                }
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

        if (Configuration.DEBUG)
        {
            global::RyuJitSharp.Compiler __jitExceptionResult391;
            var __jitExceptionCoalesceLeft392 = _compiler;
            if (__jitExceptionCoalesceLeft392 != null)
            {
                __jitExceptionResult391 = __jitExceptionCoalesceLeft392;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsNotInitialized);
                __jitExceptionResult391 = default;
            }
            var compiler = __jitExceptionResult391;
            if (compiler.DoJitStressEvexEncoding() && !IsBMIInstruction(ins) && !IsKMOVInstruction(ins))
            {
                return true;
            }
            if (compiler.DoJitStressPromotedEvexEncoding() && IsBMIInstruction(ins))
            {
                return true;
            }
        }

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
    }

    private bool TakesRex2Prefix(instrDesc id)
    {
        if (!Configuration.TARGET_AMD64)
        {
            return false;
        }

        var ins = id.idIns();
        if (!IsRex2EncodableInstruction(ins) || TakesEvexPrefix(id))
        {
            return false;
        }

        if (HasExtendedGPReg(id) || (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_imul_16, INS_imul_31))
            || id.idIsApxPpxContextSet())
        {
            return true;
        }

        bool __jitExceptionCondition393;
        bool __jitExceptionShortCircuitLeft394;
        __jitExceptionShortCircuitLeft394 = Configuration.DEBUG;
        if (__jitExceptionShortCircuitLeft394)
        {
            global::RyuJitSharp.Compiler __jitExceptionReceiver395;
            var __jitExceptionCoalesceLeft396 = _compiler;
            if (__jitExceptionCoalesceLeft396 != null)
            {
                __jitExceptionReceiver395 = __jitExceptionCoalesceLeft396;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.EmitterIsNotInitialized);
                __jitExceptionReceiver395 = default;
            }

            __jitExceptionCondition393 = __jitExceptionReceiver395.DoJitStressRex2Encoding();
        }
        else
        {
            __jitExceptionCondition393 = false;
        }
        if (__jitExceptionCondition393)
        {
            return ins is not (INS_i_jmp or INS_tail_i_jmp);
        }
        return false;
    }

    private bool TakesRexWPrefix(instrDesc id)
    {
        if (Configuration.TARGET_X86 && !UseVexEncodings)
        {
            return false;
        }

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
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.UnsupportedRexWxInstruction);
                    return default!;
                }
            }
        }

        assert(!IsSimdInstruction(ins));
        if (Configuration.TARGET_AMD64)
        {
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
                !(InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_i_jmp, INS_l_jg));
        }

        return false;
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
