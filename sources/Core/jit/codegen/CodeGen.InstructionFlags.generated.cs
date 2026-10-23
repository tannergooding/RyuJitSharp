// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private const byte INST_FP = 1;
    internal const byte LD = 1;
    internal const byte ST = 2;
    private const byte CMP = 4;
    private const byte RSH = 8;
    private const byte WID = 16;
    private const byte LNG = 32;
    private const byte NRW = 64;
    private const byte WR2 = 128;
    private const byte ARM_LD = 2;
    private const byte ARM_ST = 4;
    private const byte ARM_CMP = 8;

    private static ReadOnlySpan<insInfoFlags> s_instInfoX86 => [
        (insInfoFlags)(INS_FLAGS_None), // INS_invalid
        (insInfoFlags)(Encoding_REX2), // INS_push
        (insInfoFlags)(Encoding_REX2), // INS_pop
        (insInfoFlags)(Encoding_REX2), // INS_push_hide
        (insInfoFlags)(Encoding_REX2), // INS_pop_hide
        (insInfoFlags)(Encoding_EVEX_APX_ONLY | INS_FLAGS_HasNDD), // INS_push2
        (insInfoFlags)(Encoding_EVEX_APX_ONLY | INS_FLAGS_HasNDD), // INS_pop2
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_inc
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Encoding_REX2 | INS_FLAGS_HasNF), // INS_inc_l
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_dec
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Encoding_REX2 | INS_FLAGS_HasNF), // INS_dec_l
        (insInfoFlags)(Encoding_REX2), // INS_bswap
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_add
        (insInfoFlags)(Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_or
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | Reads_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2), // INS_adc
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | Reads_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2), // INS_sbb
        (insInfoFlags)(Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_and
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_sub
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2), // INS_sub_hide
        (insInfoFlags)(Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_xor
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2), // INS_cmp
        (insInfoFlags)(Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasWBit | Encoding_REX2), // INS_test
        (insInfoFlags)(INS_FLAGS_HasWBit | Encoding_REX2), // INS_mov
        (insInfoFlags)(Encoding_REX2), // INS_lea
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_bt
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_bts
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_btr
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_btc
        (insInfoFlags)(Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Undefined_CF | Encoding_REX2), // INS_bsr
        (insInfoFlags)(Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Undefined_CF | Encoding_REX2), // INS_bsf
        (insInfoFlags)(INS_FLAGS_HasWBit | Encoding_REX2), // INS_movsx
        (insInfoFlags)(INS_FLAGS_HasWBit | Encoding_REX2), // INS_movzx
        (insInfoFlags)(Reads_OF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovo
        (insInfoFlags)(Reads_OF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovno
        (insInfoFlags)(Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovb
        (insInfoFlags)(Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovae
        (insInfoFlags)(Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmove
        (insInfoFlags)(Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovne
        (insInfoFlags)(Reads_ZF | Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovbe
        (insInfoFlags)(Reads_ZF | Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmova
        (insInfoFlags)(Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovs
        (insInfoFlags)(Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovns
        (insInfoFlags)(Reads_PF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovp
        (insInfoFlags)(Reads_PF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovnp
        (insInfoFlags)(Reads_OF | Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovl
        (insInfoFlags)(Reads_OF | Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovge
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovle
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovg
        (insInfoFlags)(INS_FLAGS_HasWBit | Encoding_REX2), // INS_xchg
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_AX
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_CX
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_DX
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_BX
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_SP
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_BP
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_SI
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_DI
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_addpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_addps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_addsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_addss
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_addsubpd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_addsubps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_andnpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_andnps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_andpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_andps
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_blendpd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_blendps
        (insInfoFlags)(REX_W0), // INS_blendvpd
        (insInfoFlags)(REX_W0), // INS_blendvps
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_cmppd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_cmpps
        (insInfoFlags)(Input_64Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_cmpsd
        (insInfoFlags)(Input_32Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_cmpss
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF), // INS_comisd
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF), // INS_comiss
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtdq2pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtdq2ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtpd2dq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtpd2ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtps2dq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtps2pd
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_cvtsd2si32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_cvtsd2si64
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtsd2ss
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtsi2sd32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtsi2sd64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtsi2ss32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtsi2ss64
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtss2sd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_cvtss2si32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_cvtss2si64
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvttpd2dq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvttps2dq
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_cvttsd2si32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_cvttsd2si64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_cvttss2si32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_cvttss2si64
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_divpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_divps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_divsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_divss
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_dppd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_dpps
        (insInfoFlags)(Input_32Bit | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_extractps
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_haddpd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_haddps
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_hsubpd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_hsubps
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_insertps
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_lddqu
        (insInfoFlags)(REX_WIG), // INS_lfence
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_maskmovdqu
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_maxpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_maxps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_maxsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_maxss
        (insInfoFlags)(REX_WIG), // INS_mfence
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_minpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_minps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_minsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_minss
        (insInfoFlags)(REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movapd
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movaps
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | Encoding_REX2), // INS_movd32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | Encoding_REX2), // INS_movd64
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movddup
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Encoding_REX2 | INS_FLAGS_HasPseudoName), // INS_movdqa32
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Encoding_REX2 | INS_FLAGS_HasPseudoName), // INS_movdqu32
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_movhlps
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movhpd
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movhps
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_movlhps
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movlpd
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movlps
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_movmskpd
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_movmskps
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movntdq
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movntdqa
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_REX2), // INS_movnti32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_REX2), // INS_movnti64
        (insInfoFlags)(REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movntpd
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movntps
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | Encoding_REX2), // INS_movq
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movsd_simd
        (insInfoFlags)(KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movshdup
        (insInfoFlags)(KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movsldup
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movss
        (insInfoFlags)(REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movupd
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movups
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_mpsadbw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_mulpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_mulps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_mulsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_mulss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_orpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_orps
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pabsb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_pabsd
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pabsw
        (insInfoFlags)(Input_32Bit | KMask_Base8 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_packssdw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_packsswb
        (insInfoFlags)(Input_32Bit | KMask_Base8 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_packusdw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_packuswb
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddq
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddsb
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddsw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddusb
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddusw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_palignr
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative | INS_FLAGS_HasPseudoName), // INS_pandd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_pandnd
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pavgb
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pavgw
        (insInfoFlags)(REX_W0), // INS_pblendvb
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pblendw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pcmpeqb
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pcmpeqd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pcmpeqq
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pcmpeqw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pcmpgtb
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pcmpgtd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pcmpgtq
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pcmpgtw
        (insInfoFlags)(Input_8Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_pextrb
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_pextrd
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_pextrq
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phaddd
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_pextrw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phaddsw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phaddw
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_phminposuw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phsubd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phsubsw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phsubw
        (insInfoFlags)(Input_8Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pinsrb
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pinsrd
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pinsrq
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pinsrw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pmaddubsw
        (insInfoFlags)(KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaddwd
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxsb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxsd
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxsw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxub
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxud
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxuw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminsb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminsd
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminsw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminub
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminud
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminuw
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_pmovmskb
        (insInfoFlags)(Input_8Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovsxbd
        (insInfoFlags)(Input_8Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovsxbq
        (insInfoFlags)(Input_8Bit | KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovsxbw
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_pmovsxdq
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovsxwd
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovsxwq
        (insInfoFlags)(Input_8Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovzxbd
        (insInfoFlags)(Input_8Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovzxbq
        (insInfoFlags)(Input_8Bit | KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovzxbw
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_pmovzxdq
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovzxwd
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovzxwq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmuldq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmulhrsw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmulhuw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmulhw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmulld
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmuludq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmullw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative | INS_FLAGS_HasPseudoName), // INS_pord
        (insInfoFlags)(Input_8Bit | REX_WIG | Encoding_REX2), // INS_prefetchnta
        (insInfoFlags)(Input_8Bit | REX_WIG | Encoding_REX2), // INS_prefetcht0
        (insInfoFlags)(Input_8Bit | REX_WIG | Encoding_REX2), // INS_prefetcht1
        (insInfoFlags)(Input_8Bit | REX_WIG | Encoding_REX2), // INS_prefetcht2
        (insInfoFlags)(REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psadbw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pshufb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_pshufd
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pshufhw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pshuflw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psignb
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psignd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psignw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pslld
        (insInfoFlags)(REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pslldq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psllq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psllw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psrad
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psraw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psrld
        (insInfoFlags)(REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psrldq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psrlq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psrlw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubq
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubsb
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubsw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubusb
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubusw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubw
        (insInfoFlags)(REX_WIG | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF), // INS_ptest
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpckhbw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpckhdq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpckhqdq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpckhwd
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpcklbw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpckldq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpcklqdq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpcklwd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative | INS_FLAGS_HasPseudoName), // INS_pxord
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_rcpps
        (insInfoFlags)(Input_32Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_rcpss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_roundpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_roundps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_roundsd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_roundss
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_rsqrtps
        (insInfoFlags)(Input_32Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_rsqrtss
        (insInfoFlags)(REX_WIG), // INS_sfence
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_shufpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_shufps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_sqrtpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_sqrtps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_sqrtsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_sqrtss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_subpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_subps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_subsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_subss
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF), // INS_ucomisd
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF), // INS_ucomiss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_unpckhpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_unpckhps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_unpcklpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_unpcklps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_xorpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_xorps
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_aesdec
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_aesdeclast
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_aesenc
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_aesenclast
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_aesimc
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_aeskeygenassist
        (insInfoFlags)(KMask_Base1 | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_pclmulqdq
        (insInfoFlags)(REX_WIG), // INS_sha1msg1
        (insInfoFlags)(REX_WIG), // INS_sha1msg2
        (insInfoFlags)(REX_WIG), // INS_sha1nexte
        (insInfoFlags)(REX_WIG), // INS_sha1rnds4
        (insInfoFlags)(REX_WIG), // INS_sha256msg1
        (insInfoFlags)(REX_WIG), // INS_sha256msg2
        (insInfoFlags)(REX_WIG), // INS_sha256rnds2
        (insInfoFlags)(Input_64Bit | KMask_Base16 | REX_WX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_gf2p8affineinvqb
        (insInfoFlags)(Input_64Bit | KMask_Base16 | REX_WX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_gf2p8affineqb
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_gf2p8mulb
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vblendvpd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vblendvps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_vbroadcastf32x4
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_vbroadcastsd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vbroadcastss
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_vextractf32x4
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vinsertf32x4
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vmaskmovpd
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vmaskmovps
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendvb
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vperm2f128
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_vpermilpd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpermilpdvar
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vpermilps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpermilpsvar
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF), // INS_vtestpd
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF), // INS_vtestps
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_vzeroupper
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_vbroadcasti32x4
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vcvtph2ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vcvtps2ph
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_vextracti32x4
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherdpd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherdps
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherqpd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherqps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vinserti32x4
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendd
        (insInfoFlags)(Input_8Bit | KMask_Base16 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vpbroadcastb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vpbroadcastd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_vpbroadcastq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vpbroadcastw
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vperm2i128
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpermd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_vpermpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpermps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_vpermq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherdd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherdq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherqd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherqq
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmaskmovd
        (insInfoFlags)(REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmaskmovq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsllvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsllvq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsravd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsrlvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsrlvq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231ss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub231ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd231ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231ss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231ss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231ss
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Resets_CF | INS_FLAGS_HasNF), // INS_andn
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Resets_CF | INS_FLAGS_HasNF), // INS_bextr
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF), // INS_blsi
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Resets_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF), // INS_blsmsk
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF), // INS_blsr
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF), // INS_bzhi
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_mulx
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pdep
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pext
        (insInfoFlags)(REX_WX | Encoding_VEX), // INS_rorx
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_sarx
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_shlx
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_shrx
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpdpbusd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpdpbusds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpdpwssd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpdpwssds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwsud
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwsuds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwusd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwusds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwuud
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwuuds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbssd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbssds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbsud
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbsuds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbuud
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbuuds
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vbmacor16x16x16
        (insInfoFlags)(Input_16Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vbmacxor16x16x16
        (insInfoFlags)(Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX), // INS_vbitrev
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmadd52huq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmadd52luq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kaddb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kaddd
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kaddq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kaddw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandd
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandnb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandnd
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandnq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandnw
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kmovb_gpr
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kmovb_msk
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kmovd_gpr
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kmovd_msk
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kmovq_gpr
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kmovq_msk
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kmovw_gpr
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kmovw_msk
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_knotb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_knotd
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_knotq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_knotw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_korb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kord
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_korq
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_kortestb
        (insInfoFlags)(REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_kortestd
        (insInfoFlags)(REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_kortestq
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_kortestw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_korw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kshiftlb
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kshiftld
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kshiftlq
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kshiftlw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kshiftrb
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kshiftrd
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kshiftrq
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kshiftrw
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_ktestb
        (insInfoFlags)(REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_ktestd
        (insInfoFlags)(REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_ktestq
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_ktestw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kunpckbw
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kunpckdq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kunpckwd
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxnorb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxnord
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxnorq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxnorw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxorb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxord
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxorq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxorw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_valignd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_valignq
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vblendmpd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vblendmps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vbroadcastf32x2
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vbroadcastf32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vbroadcastf64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vbroadcastf64x4
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vbroadcasti32x2
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vbroadcasti32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vbroadcasti64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vbroadcasti64x4
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vcmppd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vcmpps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vcmpsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vcmpss
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vcompresspd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vcompressps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtpd2qq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtpd2udq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtpd2uqq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtps2qq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtps2udq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtps2uqq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtqq2pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtqq2ps
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_EVEX), // INS_vcvtsd2usi32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vcvtsd2usi64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vcvtss2usi32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_EVEX), // INS_vcvtss2usi64
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2qq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2udq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2uqq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttps2qq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttps2udq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttps2uqq
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_EVEX), // INS_vcvttsd2usi32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vcvttsd2usi64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vcvttss2usi32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_EVEX), // INS_vcvttss2usi64
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtudq2pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtudq2ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtuqq2pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtuqq2ps
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vcvtusi2sd32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vcvtusi2sd64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vcvtusi2ss32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vcvtusi2ss64
        (insInfoFlags)(KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vdbpsadbw
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vexpandpd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vexpandps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vextractf32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vextractf64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vextractf64x4
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vextracti32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vextracti64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vextracti64x4
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfixupimmpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfixupimmps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfixupimmsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfixupimmss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vfpclasspd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vfpclassps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX), // INS_vfpclasssd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vfpclassss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherdpd_msk
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherdps_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherqpd_msk
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherqps_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vgetexppd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vgetexpps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetexpsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetexpss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vgetmantpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vgetmantps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetmantsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetmantss
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinsertf32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinsertf64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinsertf64x4
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinserti32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinserti64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinserti64x4
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vmovdqa64
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vmovdqu16
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vmovdqu64
        (insInfoFlags)(REX_W0 | Encoding_EVEX), // INS_vmovdqu8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vpabsq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpandnq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpandq
        (insInfoFlags)(REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendmb
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendmd
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendmq
        (insInfoFlags)(REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendmw
        (insInfoFlags)(Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX), // INS_vpbroadcastb_gpr
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpbroadcastd_gpr
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vpbroadcastq_gpr
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vpbroadcastw_gpr
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vpcmpb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vpcmpd
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpeqb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpeqd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpeqq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpeqw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpgtb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpgtd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpgtq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpgtw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vpcmpq
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vpcmpub
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vpcmpud
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vpcmpuq
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vpcmpuw
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vpcmpw
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vpcompressd
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vpcompressq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpconflictd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vpconflictq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2d
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2q
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2w
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermpd_reg
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermq_reg
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2d
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2q
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2w
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermw
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vpexpandd
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vpexpandq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherdd_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherdq_msk
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherqd_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherqq_msk
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vplzcntd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vplzcntq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmaxsq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmaxuq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpminsq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpminuq
        (insInfoFlags)(REX_W0 | Encoding_EVEX), // INS_vpmovb2m
        (insInfoFlags)(REX_W0 | Encoding_EVEX), // INS_vpmovd2m
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovdb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovdw
        (insInfoFlags)(REX_W0 | Encoding_EVEX), // INS_vpmovm2b
        (insInfoFlags)(REX_W0 | Encoding_EVEX), // INS_vpmovm2d
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vpmovm2q
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vpmovm2w
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vpmovq2m
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovqb
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovqd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovqw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovsdb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovsdw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovsqb
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovsqd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovsqw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vpmovswb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovusdb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovusdw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovusqb
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovusqd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovusqw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vpmovuswb
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vpmovw2m
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vpmovwb
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmullq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vporq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprold
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprolq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprolvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprolvq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprord
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprorq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprorvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprorvq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpscatterdd_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpscatterdq_msk
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpscatterqd_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpscatterqq_msk
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsllvw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsraq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsravq
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsravw
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsrlvw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpternlogd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpternlogq
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestmb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestmd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestmq
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestmw
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestnmb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestnmd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestnmq
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestnmw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpxorq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vrangepd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vrangeps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vrangesd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vrangess
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vrcp14pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vrcp14ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrcp14sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrcp14ss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vreducepd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vreduceps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vreducesd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vreducess
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vrndscalepd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vrndscaleps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrndscalesd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrndscaless
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vrsqrt14pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vrsqrt14ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrsqrt14sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrsqrt14ss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscalefpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscalefps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscalefsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscalefss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscatterdpd_msk
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscatterdps_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscatterqpd_msk
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscatterqps_msk
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vshuff32x4
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vshuff64x2
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vshufi32x4
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vshufi64x2
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermb
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2b
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2b
        (insInfoFlags)(Input_64Bit | KMask_Base16 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpmultishiftqb
        (insInfoFlags)(Input_8Bit | REX_W0 | Encoding_EVEX), // INS_vpcompressb
        (insInfoFlags)(Input_16Bit | REX_W1 | Encoding_EVEX), // INS_vpcompressw
        (insInfoFlags)(Input_8Bit | REX_W0 | Encoding_EVEX), // INS_vpexpandb
        (insInfoFlags)(Input_16Bit | REX_W1 | Encoding_EVEX), // INS_vpexpandw
        (insInfoFlags)(Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX), // INS_vpopcntb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpopcntd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vpopcntq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX), // INS_vpopcntw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshldd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshldq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshldvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshldvq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshldvw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshldw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshrdd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshrdq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshrdvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshrdvq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshrdvw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshrdw
        (insInfoFlags)(Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask), // INS_vpshufbitqmb
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_vaddph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vaddsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vcmpph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vcmpsh
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_EVEX), // INS_vcomish
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtdq2ph
        (insInfoFlags)(Input_32Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtne2ps2bf16
        (insInfoFlags)(Input_32Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvtneps2bf16
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtpd2ph
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtph2dq
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtph2pd
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtph2psx
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtph2qq
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtph2udq
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtph2uqq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvtph2uw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvtph2w
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtps2phx
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtqq2ph
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtsd2sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtsh2sd
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vcvtsh2si32
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX), // INS_vcvtsh2si64
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtsh2ss
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vcvtsh2usi32
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX), // INS_vcvtsh2usi64
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtsi2sh32
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtsi2sh64
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtss2sh
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttph2dq
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttph2qq
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttph2udq
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttph2uqq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvttph2uw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvttph2w
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vcvttsh2si32
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX), // INS_vcvttsh2si64
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vcvttsh2usi32
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX), // INS_vcvttsh2usi64
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtudq2ph
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtuqq2ph
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtusi2sh32
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtusi2sh64
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvtuw2ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvtw2ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vdivph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vdivsh
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vdpbf16ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfcmaddcph
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfcmaddcsh
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vfcmulcph
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vfcmulcsh
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vfmulcph
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vfmulcsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231ph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231sh
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddcph
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddcsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub231ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231ph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231sh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd231ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231ph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231sh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231ph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231sh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vfpclassph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vfpclasssh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vgetexpph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetexpsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vgetmantph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetmantsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vmaxph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vmaxsh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vminsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vminph
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_EVEX), // INS_vmovsh
        (insInfoFlags)(Input_16Bit | REX_WIG | Encoding_EVEX), // INS_vmovw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_vmulph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vmulsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vrcpph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrcpsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vreduceph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vreducesh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vrndscaleph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrndscalesh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vrsqrtph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrsqrtsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscalefph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vscalefsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vsqrtph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vsqrtsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vsubph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vsubsh
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_EVEX), // INS_vucomish
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vp2intersectd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vp2intersectq
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF), // INS_vcomxsd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF), // INS_vcomxss
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtps2ibs
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtps2iubs
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2dqs
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2qqs
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2udqs
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2uqqs
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttps2dqs
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttps2ibs
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttps2iubs
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttps2qqs
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttps2udqs
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttps2uqqs
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_EVEX), // INS_vcvttsd2sis32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vcvttsd2sis64
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_EVEX), // INS_vcvttsd2usis32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vcvttsd2usis64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vcvttss2sis32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_EVEX), // INS_vcvttss2sis64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vcvttss2usis32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_EVEX), // INS_vcvttss2usis64
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vminmaxpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vminmaxps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vminmaxsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vminmaxss
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vmovd_simd
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_EVEX), // INS_vmovw_simd
        (insInfoFlags)(KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vmpsadbw
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF), // INS_vucomxsd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF), // INS_vucomxss
        (insInfoFlags)(INS_FLAGS_None), // INS_crc32
        (insInfoFlags)(Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_tzcnt
        (insInfoFlags)(Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_lzcnt
        (insInfoFlags)(INS_FLAGS_None), // INS_movbe
        (insInfoFlags)(Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Resets_CF | Encoding_REX2), // INS_popcnt
        (insInfoFlags)(Resets_OF | Resets_SF | Resets_ZF | Resets_AF | Resets_PF | Writes_CF), // INS_tpause
        (insInfoFlags)(INS_FLAGS_None), // INS_umonitor
        (insInfoFlags)(Resets_OF | Resets_SF | Resets_ZF | Resets_AF | Resets_PF | Writes_CF), // INS_umwait
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_neg
        (insInfoFlags)(INS_FLAGS_None | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_not
        (insInfoFlags)(Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_rol
        (insInfoFlags)(Writes_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_rol_1
        (insInfoFlags)(Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_rol_N
        (insInfoFlags)(Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_ror
        (insInfoFlags)(Writes_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_ror_1
        (insInfoFlags)(Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_ror_N
        (insInfoFlags)(Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcl
        (insInfoFlags)(Writes_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcl_1
        (insInfoFlags)(Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcl_N
        (insInfoFlags)(Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcr
        (insInfoFlags)(Writes_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcr_1
        (insInfoFlags)(Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcr_N
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shl
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shl_1
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shl_N
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shr
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shr_1
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shr_N
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_sar
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_sar_1
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_sar_N
        (insInfoFlags)(INS_FLAGS_None), // INS_ret
        (insInfoFlags)(INS_FLAGS_None), // INS_loop
        (insInfoFlags)(Encoding_REX2), // INS_call
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_r_movsb
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_r_movsd
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_movsb
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_movsd
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_r_stosb
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_r_stosd
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_stosb
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_stosd
        (insInfoFlags)(INS_FLAGS_None), // INS_int3
        (insInfoFlags)(INS_FLAGS_None), // INS_nop
        (insInfoFlags)(INS_FLAGS_None), // INS_pause
        (insInfoFlags)(INS_FLAGS_None), // INS_lock
        (insInfoFlags)(INS_FLAGS_None), // INS_leave
        (insInfoFlags)(INS_FLAGS_None), // INS_serialize
        (insInfoFlags)(INS_FLAGS_HasPseudoName), // INS_cwde
        (insInfoFlags)(INS_FLAGS_HasPseudoName), // INS_cdq
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Undefined_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF), // INS_idiv
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF), // INS_imulEAX
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Undefined_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF), // INS_div
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF), // INS_mulEAX
        (insInfoFlags)(Restore_SF_ZF_AF_PF_CF), // INS_sahf
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2), // INS_xadd
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2), // INS_cmpxchg
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | Encoding_REX2), // INS_shld
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | Encoding_REX2), // INS_shrd
        (insInfoFlags)(INS_FLAGS_X87Instr), // INS_fld
        (insInfoFlags)(INS_FLAGS_X87Instr), // INS_fstp
        (insInfoFlags)(Reads_OF | Encoding_REX2), // INS_seto
        (insInfoFlags)(Reads_OF | Encoding_REX2), // INS_setno
        (insInfoFlags)(Reads_CF | Encoding_REX2), // INS_setb
        (insInfoFlags)(Reads_CF | Encoding_REX2), // INS_setae
        (insInfoFlags)(Reads_ZF | Encoding_REX2), // INS_sete
        (insInfoFlags)(Reads_ZF | Encoding_REX2), // INS_setne
        (insInfoFlags)(Reads_ZF | Reads_CF | Encoding_REX2), // INS_setbe
        (insInfoFlags)(Reads_ZF | Reads_CF | Encoding_REX2), // INS_seta
        (insInfoFlags)(Reads_SF | Encoding_REX2), // INS_sets
        (insInfoFlags)(Reads_SF | Encoding_REX2), // INS_setns
        (insInfoFlags)(Reads_PF | Encoding_REX2), // INS_setp
        (insInfoFlags)(Reads_PF | Encoding_REX2), // INS_setnp
        (insInfoFlags)(Reads_OF | Reads_SF | Encoding_REX2), // INS_setl
        (insInfoFlags)(Reads_OF | Reads_SF | Encoding_REX2), // INS_setge
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2), // INS_setle
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2), // INS_setg
        (insInfoFlags)(Encoding_REX2), // INS_tail_i_jmp
        (insInfoFlags)(Encoding_REX2), // INS_i_jmp
        (insInfoFlags)(INS_FLAGS_None), // INS_jmp
        (insInfoFlags)(Reads_OF), // INS_jo
        (insInfoFlags)(Reads_OF), // INS_jno
        (insInfoFlags)(Reads_CF), // INS_jb
        (insInfoFlags)(Reads_CF), // INS_jae
        (insInfoFlags)(Reads_ZF), // INS_je
        (insInfoFlags)(Reads_ZF), // INS_jne
        (insInfoFlags)(Reads_ZF | Reads_CF), // INS_jbe
        (insInfoFlags)(Reads_ZF | Reads_CF), // INS_ja
        (insInfoFlags)(Reads_SF), // INS_js
        (insInfoFlags)(Reads_SF), // INS_jns
        (insInfoFlags)(Reads_PF), // INS_jp
        (insInfoFlags)(Reads_PF), // INS_jnp
        (insInfoFlags)(Reads_OF | Reads_SF), // INS_jl
        (insInfoFlags)(Reads_OF | Reads_SF), // INS_jge
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF), // INS_jle
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF), // INS_jg
        (insInfoFlags)(INS_FLAGS_None), // INS_l_jmp
        (insInfoFlags)(Reads_OF), // INS_l_jo
        (insInfoFlags)(Reads_OF), // INS_l_jno
        (insInfoFlags)(Reads_CF), // INS_l_jb
        (insInfoFlags)(Reads_CF), // INS_l_jae
        (insInfoFlags)(Reads_ZF), // INS_l_je
        (insInfoFlags)(Reads_ZF), // INS_l_jne
        (insInfoFlags)(Reads_ZF | Reads_CF), // INS_l_jbe
        (insInfoFlags)(Reads_ZF | Reads_CF), // INS_l_ja
        (insInfoFlags)(Reads_SF), // INS_l_js
        (insInfoFlags)(Reads_SF), // INS_l_jns
        (insInfoFlags)(Reads_PF), // INS_l_jp
        (insInfoFlags)(Reads_PF), // INS_l_jnp
        (insInfoFlags)(Reads_OF | Reads_SF), // INS_l_jl
        (insInfoFlags)(Reads_OF | Reads_SF), // INS_l_jge
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF), // INS_l_jle
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF), // INS_l_jg
        (insInfoFlags)(INS_FLAGS_None), // INS_align
        (insInfoFlags)(INS_FLAGS_None), // INS_data16
    ];

    private static ReadOnlySpan<insInfoFlags> s_instInfoAMD64 => [
        (insInfoFlags)(INS_FLAGS_None), // INS_invalid
        (insInfoFlags)(Encoding_REX2), // INS_push
        (insInfoFlags)(Encoding_REX2), // INS_pop
        (insInfoFlags)(Encoding_REX2), // INS_push_hide
        (insInfoFlags)(Encoding_REX2), // INS_pop_hide
        (insInfoFlags)(Encoding_EVEX_APX_ONLY | INS_FLAGS_HasNDD), // INS_push2
        (insInfoFlags)(Encoding_EVEX_APX_ONLY | INS_FLAGS_HasNDD), // INS_pop2
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_inc
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Encoding_REX2 | INS_FLAGS_HasNF), // INS_inc_l
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_dec
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Encoding_REX2 | INS_FLAGS_HasNF), // INS_dec_l
        (insInfoFlags)(Encoding_REX2), // INS_bswap
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_add
        (insInfoFlags)(Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_or
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | Reads_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2), // INS_adc
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | Reads_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2), // INS_sbb
        (insInfoFlags)(Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_and
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_sub
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2), // INS_sub_hide
        (insInfoFlags)(Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_xor
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2), // INS_cmp
        (insInfoFlags)(Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasWBit | Encoding_REX2), // INS_test
        (insInfoFlags)(INS_FLAGS_HasWBit | Encoding_REX2), // INS_mov
        (insInfoFlags)(Encoding_REX2), // INS_lea
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_bt
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_bts
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_btr
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_btc
        (insInfoFlags)(Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Undefined_CF | Encoding_REX2), // INS_bsr
        (insInfoFlags)(Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Undefined_CF | Encoding_REX2), // INS_bsf
        (insInfoFlags)(INS_FLAGS_HasWBit | Encoding_REX2), // INS_movsx
        (insInfoFlags)(REX_W1 | Encoding_REX2), // INS_movsxd
        (insInfoFlags)(INS_FLAGS_HasWBit | Encoding_REX2), // INS_movzx
        (insInfoFlags)(Reads_OF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovo
        (insInfoFlags)(Reads_OF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovno
        (insInfoFlags)(Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovb
        (insInfoFlags)(Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovae
        (insInfoFlags)(Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmove
        (insInfoFlags)(Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovne
        (insInfoFlags)(Reads_ZF | Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovbe
        (insInfoFlags)(Reads_ZF | Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmova
        (insInfoFlags)(Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovs
        (insInfoFlags)(Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovns
        (insInfoFlags)(Reads_PF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovp
        (insInfoFlags)(Reads_PF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovnp
        (insInfoFlags)(Reads_OF | Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovl
        (insInfoFlags)(Reads_OF | Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovge
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovle
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_cmovg
        (insInfoFlags)(INS_FLAGS_HasWBit | Encoding_REX2), // INS_xchg
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_AX
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_CX
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_DX
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_BX
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_SP
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_BP
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_SI
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_DI
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_08
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_09
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_10
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_11
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_12
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_13
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_14
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_15
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_16
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_17
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_18
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_19
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_20
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_21
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_22
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_23
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_24
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_25
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_26
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_27
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_28
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_29
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_30
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2), // INS_imul_31
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_addpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_addps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_addsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_addss
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_addsubpd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_addsubps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_andnpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_andnps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_andpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_andps
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_blendpd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_blendps
        (insInfoFlags)(REX_W0), // INS_blendvpd
        (insInfoFlags)(REX_W0), // INS_blendvps
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_cmppd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_cmpps
        (insInfoFlags)(Input_64Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_cmpsd
        (insInfoFlags)(Input_32Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_cmpss
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF), // INS_comisd
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF), // INS_comiss
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtdq2pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtdq2ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtpd2dq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtpd2ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtps2dq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvtps2pd
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_cvtsd2si32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_cvtsd2si64
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtsd2ss
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtsi2sd32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtsi2sd64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtsi2ss32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtsi2ss64
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_cvtss2sd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_cvtss2si32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_cvtss2si64
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvttpd2dq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_cvttps2dq
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_cvttsd2si32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_cvttsd2si64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_cvttss2si32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_cvttss2si64
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_divpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_divps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_divsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_divss
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_dppd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_dpps
        (insInfoFlags)(Input_32Bit | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_extractps
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_haddpd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_haddps
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_hsubpd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_hsubps
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_insertps
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_lddqu
        (insInfoFlags)(REX_WIG), // INS_lfence
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_maskmovdqu
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_maxpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_maxps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_maxsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_maxss
        (insInfoFlags)(REX_WIG), // INS_mfence
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_minpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_minps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_minsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_minss
        (insInfoFlags)(REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movapd
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movaps
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | Encoding_REX2), // INS_movd32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | Encoding_REX2), // INS_movd64
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movddup
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Encoding_REX2 | INS_FLAGS_HasPseudoName), // INS_movdqa32
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Encoding_REX2 | INS_FLAGS_HasPseudoName), // INS_movdqu32
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_movhlps
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movhpd
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movhps
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_movlhps
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movlpd
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movlps
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_movmskpd
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_movmskps
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movntdq
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movntdqa
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_REX2), // INS_movnti32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_REX2), // INS_movnti64
        (insInfoFlags)(REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movntpd
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movntps
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | Encoding_REX2), // INS_movq
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movsd_simd
        (insInfoFlags)(KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movshdup
        (insInfoFlags)(KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movsldup
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_movss
        (insInfoFlags)(REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movupd
        (insInfoFlags)(REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_movups
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_mpsadbw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_mulpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_mulps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_mulsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_mulss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_orpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_orps
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pabsb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_pabsd
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pabsw
        (insInfoFlags)(Input_32Bit | KMask_Base8 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_packssdw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_packsswb
        (insInfoFlags)(Input_32Bit | KMask_Base8 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_packusdw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_packuswb
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddq
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddsb
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddsw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddusb
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddusw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_paddw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_palignr
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative | INS_FLAGS_HasPseudoName), // INS_pandd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_pandnd
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pavgb
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pavgw
        (insInfoFlags)(REX_W0), // INS_pblendvb
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pblendw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pcmpeqb
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pcmpeqd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pcmpeqq
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pcmpeqw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pcmpgtb
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pcmpgtd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pcmpgtq
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pcmpgtw
        (insInfoFlags)(Input_8Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_pextrb
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_pextrd
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_pextrq
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phaddd
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_pextrw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phaddsw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phaddw
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_phminposuw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phsubd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phsubsw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_phsubw
        (insInfoFlags)(Input_8Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pinsrb
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pinsrd
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pinsrq
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pinsrw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pmaddubsw
        (insInfoFlags)(KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaddwd
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxsb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxsd
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxsw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxub
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxud
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmaxuw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminsb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminsd
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminsw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminub
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminud
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pminuw
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_pmovmskb
        (insInfoFlags)(Input_8Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovsxbd
        (insInfoFlags)(Input_8Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovsxbq
        (insInfoFlags)(Input_8Bit | KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovsxbw
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_pmovsxdq
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovsxwd
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovsxwq
        (insInfoFlags)(Input_8Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovzxbd
        (insInfoFlags)(Input_8Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovzxbq
        (insInfoFlags)(Input_8Bit | KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovzxbw
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_pmovzxdq
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovzxwd
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pmovzxwq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmuldq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmulhrsw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmulhuw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmulhw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmulld
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmuludq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_pmullw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative | INS_FLAGS_HasPseudoName), // INS_pord
        (insInfoFlags)(Input_8Bit | REX_WIG | Encoding_REX2), // INS_prefetchnta
        (insInfoFlags)(Input_8Bit | REX_WIG | Encoding_REX2), // INS_prefetcht0
        (insInfoFlags)(Input_8Bit | REX_WIG | Encoding_REX2), // INS_prefetcht1
        (insInfoFlags)(Input_8Bit | REX_WIG | Encoding_REX2), // INS_prefetcht2
        (insInfoFlags)(REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psadbw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pshufb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_pshufd
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pshufhw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX), // INS_pshuflw
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psignb
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psignd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psignw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pslld
        (insInfoFlags)(REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pslldq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psllq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psllw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psrad
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psraw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psrld
        (insInfoFlags)(REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psrldq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psrlq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psrlw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubq
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubsb
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubsw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubusb
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubusw
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_psubw
        (insInfoFlags)(REX_WIG | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF), // INS_ptest
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpckhbw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpckhdq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpckhqdq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpckhwd
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpcklbw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpckldq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpcklqdq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_punpcklwd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative | INS_FLAGS_HasPseudoName), // INS_pxord
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_rcpps
        (insInfoFlags)(Input_32Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_rcpss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_roundpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_roundps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_roundsd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_roundss
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_rsqrtps
        (insInfoFlags)(Input_32Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_rsqrtss
        (insInfoFlags)(REX_WIG), // INS_sfence
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_shufpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_shufps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_sqrtpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX), // INS_sqrtps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_sqrtsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_sqrtss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_subpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_subps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_subsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_subss
        (insInfoFlags)(Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF), // INS_ucomisd
        (insInfoFlags)(Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF), // INS_ucomiss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_unpckhpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_unpckhps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_unpcklpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_unpcklps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_xorpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_xorps
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_aesdec
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_aesdeclast
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_aesenc
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_aesenclast
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_aesimc
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_aeskeygenassist
        (insInfoFlags)(KMask_Base1 | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_pclmulqdq
        (insInfoFlags)(REX_WIG), // INS_sha1msg1
        (insInfoFlags)(REX_WIG), // INS_sha1msg2
        (insInfoFlags)(REX_WIG), // INS_sha1nexte
        (insInfoFlags)(REX_WIG), // INS_sha1rnds4
        (insInfoFlags)(REX_WIG), // INS_sha256msg1
        (insInfoFlags)(REX_WIG), // INS_sha256msg2
        (insInfoFlags)(REX_WIG), // INS_sha256rnds2
        (insInfoFlags)(Input_64Bit | KMask_Base16 | REX_WX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_gf2p8affineinvqb
        (insInfoFlags)(Input_64Bit | KMask_Base16 | REX_WX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_gf2p8affineqb
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_gf2p8mulb
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vblendvpd
        (insInfoFlags)(REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vblendvps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_vbroadcastf32x4
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_vbroadcastsd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vbroadcastss
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_vextractf32x4
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vinsertf32x4
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vmaskmovpd
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vmaskmovps
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendvb
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vperm2f128
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_vpermilpd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpermilpdvar
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vpermilps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpermilpsvar
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF), // INS_vtestpd
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF), // INS_vtestps
        (insInfoFlags)(REX_WIG | Encoding_VEX), // INS_vzeroupper
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_vbroadcasti32x4
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vcvtph2ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vcvtps2ph
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName), // INS_vextracti32x4
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherdpd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherdps
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherqpd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherqps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vinserti32x4
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendd
        (insInfoFlags)(Input_8Bit | KMask_Base16 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vpbroadcastb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vpbroadcastd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX), // INS_vpbroadcastq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_VEX | Encoding_EVEX), // INS_vpbroadcastw
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vperm2i128
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpermd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_vpermpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpermps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX), // INS_vpermq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherdd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherdq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherqd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherqq
        (insInfoFlags)(REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmaskmovd
        (insInfoFlags)(REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmaskmovq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsllvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsllvq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsravd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsrlvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsrlvq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231ss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub231ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd231ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231ss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231ss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213sd
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213ss
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231ss
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Resets_CF | INS_FLAGS_HasNF), // INS_andn
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Resets_CF | INS_FLAGS_HasNF), // INS_bextr
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF), // INS_blsi
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Resets_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF), // INS_blsmsk
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF), // INS_blsr
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF), // INS_bzhi
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_mulx
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pdep
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_pext
        (insInfoFlags)(REX_WX | Encoding_VEX), // INS_rorx
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_sarx
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_shlx
        (insInfoFlags)(REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_shrx
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpdpbusd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpdpbusds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpdpwssd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpdpwssds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwsud
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwsuds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwusd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwusds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwuud
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpwuuds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbssd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbssds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbsud
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbsuds
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbuud
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpdpbuuds
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vbmacor16x16x16
        (insInfoFlags)(Input_16Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vbmacxor16x16x16
        (insInfoFlags)(Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX), // INS_vbitrev
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmadd52huq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmadd52luq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kaddb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kaddd
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kaddq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kaddw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandd
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandnb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandnd
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandnq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandnw
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kandw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kmovb_gpr
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kmovb_msk
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kmovd_gpr
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kmovd_msk
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kmovq_gpr
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kmovq_msk
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kmovw_gpr
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kmovw_msk
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_knotb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_knotd
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_knotq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_knotw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_korb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kord
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_korq
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_kortestb
        (insInfoFlags)(REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_kortestd
        (insInfoFlags)(REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_kortestq
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_kortestw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_korw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kshiftlb
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kshiftld
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kshiftlq
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kshiftlw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kshiftrb
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction), // INS_kshiftrd
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kshiftrq
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction), // INS_kshiftrw
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_ktestb
        (insInfoFlags)(REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_ktestd
        (insInfoFlags)(REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_ktestq
        (insInfoFlags)(REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction), // INS_ktestw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kunpckbw
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kunpckdq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kunpckwd
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxnorb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxnord
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxnorq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxnorw
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxorb
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxord
        (insInfoFlags)(REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxorq
        (insInfoFlags)(REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit), // INS_kxorw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_valignd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_valignq
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vblendmpd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vblendmps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vbroadcastf32x2
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vbroadcastf32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vbroadcastf64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vbroadcastf64x4
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vbroadcasti32x2
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vbroadcasti32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vbroadcasti64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vbroadcasti64x4
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vcmppd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vcmpps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vcmpsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vcmpss
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vcompresspd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vcompressps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtpd2qq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtpd2udq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtpd2uqq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtps2qq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtps2udq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtps2uqq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtqq2pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtqq2ps
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_EVEX), // INS_vcvtsd2usi32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vcvtsd2usi64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vcvtss2usi32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_EVEX), // INS_vcvtss2usi64
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2qq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2udq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2uqq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttps2qq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttps2udq
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttps2uqq
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_EVEX), // INS_vcvttsd2usi32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vcvttsd2usi64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vcvttss2usi32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_EVEX), // INS_vcvttss2usi64
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtudq2pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtudq2ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtuqq2pd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtuqq2ps
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vcvtusi2sd32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vcvtusi2sd64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vcvtusi2ss32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vcvtusi2ss64
        (insInfoFlags)(KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vdbpsadbw
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vexpandpd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vexpandps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vextractf32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vextractf64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vextractf64x4
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vextracti32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vextracti64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vextracti64x4
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfixupimmpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfixupimmps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfixupimmsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfixupimmss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vfpclasspd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vfpclassps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX), // INS_vfpclasssd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vfpclassss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherdpd_msk
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherdps_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherqpd_msk
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vgatherqps_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vgetexppd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vgetexpps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetexpsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetexpss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vgetmantpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vgetmantps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetmantsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetmantss
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinsertf32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinsertf64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinsertf64x4
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinserti32x8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinserti64x2
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vinserti64x4
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vmovdqa64
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vmovdqu16
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vmovdqu64
        (insInfoFlags)(REX_W0 | Encoding_EVEX), // INS_vmovdqu8
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vpabsq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpandnq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpandq
        (insInfoFlags)(REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendmb
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendmd
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendmq
        (insInfoFlags)(REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpblendmw
        (insInfoFlags)(Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX), // INS_vpbroadcastb_gpr
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpbroadcastd_gpr
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vpbroadcastq_gpr
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vpbroadcastw_gpr
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vpcmpb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vpcmpd
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpeqb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpeqd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpeqq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpeqw
        (insInfoFlags)(KMask_Base16 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpgtb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpgtd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpgtq
        (insInfoFlags)(KMask_Base8 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpcmpgtw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vpcmpq
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vpcmpub
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vpcmpud
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vpcmpuq
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vpcmpuw
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName), // INS_vpcmpw
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vpcompressd
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vpcompressq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpconflictd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vpconflictq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2d
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2q
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2w
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermpd_reg
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermq_reg
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2d
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2ps
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2q
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2w
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermw
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vpexpandd
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vpexpandq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherdd_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherdq_msk
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherqd_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpgatherqq_msk
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vplzcntd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vplzcntq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmaxsq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmaxuq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpminsq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpminuq
        (insInfoFlags)(REX_W0 | Encoding_EVEX), // INS_vpmovb2m
        (insInfoFlags)(REX_W0 | Encoding_EVEX), // INS_vpmovd2m
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovdb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovdw
        (insInfoFlags)(REX_W0 | Encoding_EVEX), // INS_vpmovm2b
        (insInfoFlags)(REX_W0 | Encoding_EVEX), // INS_vpmovm2d
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vpmovm2q
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vpmovm2w
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vpmovq2m
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovqb
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovqd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovqw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovsdb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovsdw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovsqb
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovsqd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovsqw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vpmovswb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovusdb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpmovusdw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovusqb
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovusqd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vpmovusqw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vpmovuswb
        (insInfoFlags)(REX_W1 | Encoding_EVEX), // INS_vpmovw2m
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vpmovwb
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpmullq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vporq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprold
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprolq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprolvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprolvq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprord
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprorq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprorvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vprorvq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpscatterdd_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpscatterdq_msk
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpscatterqd_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpscatterqq_msk
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsllvw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsraq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsravq
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsravw
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpsrlvw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpternlogd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpternlogq
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestmb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestmd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestmq
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestmw
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestnmb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestnmd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestnmq
        (insInfoFlags)(KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vptestnmw
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpxorq
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vrangepd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vrangeps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vrangesd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vrangess
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vrcp14pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vrcp14ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrcp14sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrcp14ss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vreducepd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vreduceps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vreducesd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vreducess
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vrndscalepd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vrndscaleps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrndscalesd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrndscaless
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vrsqrt14pd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vrsqrt14ps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrsqrt14sd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrsqrt14ss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscalefpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscalefps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscalefsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscalefss
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscatterdpd_msk
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscatterdps_msk
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscatterqpd_msk
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscatterqps_msk
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vshuff32x4
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vshuff64x2
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vshufi32x4
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vshufi64x2
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermb
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermi2b
        (insInfoFlags)(KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpermt2b
        (insInfoFlags)(Input_64Bit | KMask_Base16 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpmultishiftqb
        (insInfoFlags)(Input_8Bit | REX_W0 | Encoding_EVEX), // INS_vpcompressb
        (insInfoFlags)(Input_16Bit | REX_W1 | Encoding_EVEX), // INS_vpcompressw
        (insInfoFlags)(Input_8Bit | REX_W0 | Encoding_EVEX), // INS_vpexpandb
        (insInfoFlags)(Input_16Bit | REX_W1 | Encoding_EVEX), // INS_vpexpandw
        (insInfoFlags)(Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX), // INS_vpopcntb
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vpopcntd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vpopcntq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX), // INS_vpopcntw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshldd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshldq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshldvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshldvq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshldvw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshldw
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshrdd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshrdq
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshrdvd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshrdvq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vpshrdvw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vpshrdw
        (insInfoFlags)(Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask), // INS_vpshufbitqmb
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_vaddph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vaddsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vcmpph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName), // INS_vcmpsh
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_EVEX), // INS_vcomish
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtdq2ph
        (insInfoFlags)(Input_32Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtne2ps2bf16
        (insInfoFlags)(Input_32Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvtneps2bf16
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtpd2ph
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtph2dq
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtph2pd
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtph2psx
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtph2qq
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtph2udq
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvtph2uqq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvtph2uw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvtph2w
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtps2phx
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtqq2ph
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtsd2sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtsh2sd
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vcvtsh2si32
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX), // INS_vcvtsh2si64
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtsh2ss
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vcvtsh2usi32
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX), // INS_vcvtsh2usi64
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtsi2sh32
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtsi2sh64
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtss2sh
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttph2dq
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttph2qq
        (insInfoFlags)(Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttph2udq
        (insInfoFlags)(Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttph2uqq
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvttph2uw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvttph2w
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vcvttsh2si32
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX), // INS_vcvttsh2si64
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vcvttsh2usi32
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX), // INS_vcvttsh2usi64
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtudq2ph
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvtuqq2ph
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtusi2sh32
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vcvtusi2sh64
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvtuw2ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vcvtw2ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vdivph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vdivsh
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vdpbf16ps
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfcmaddcph
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfcmaddcsh
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vfcmulcph
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vfcmulcsh
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vfmulcph
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vfmulcsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231ph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd132sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd213sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmadd231sh
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddcph
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddcsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmaddsub231ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231ph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub132sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub213sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsub231sh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfmsubadd231ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231ph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd132sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd213sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmadd231sh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213ph
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231ph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub132sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub213sh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vfnmsub231sh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vfpclassph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX), // INS_vfpclasssh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vgetexpph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetexpsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vgetmantph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vgetmantsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vmaxph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vmaxsh
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vminsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vminph
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_EVEX), // INS_vmovsh
        (insInfoFlags)(Input_16Bit | REX_WIG | Encoding_EVEX), // INS_vmovw
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative), // INS_vmulph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vmulsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vrcpph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrcpsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vreduceph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vreducesh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vrndscaleph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrndscalesh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vrsqrtph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vrsqrtsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vscalefph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vscalefsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX), // INS_vsqrtph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vsqrtsh
        (insInfoFlags)(Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vsubph
        (insInfoFlags)(Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vsubsh
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_EVEX), // INS_vucomish
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vp2intersectd
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vp2intersectq
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF), // INS_vcomxsd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF), // INS_vcomxss
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtps2ibs
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvtps2iubs
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2dqs
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2qqs
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2udqs
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX), // INS_vcvttpd2uqqs
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttps2dqs
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttps2ibs
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttps2iubs
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttps2qqs
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX), // INS_vcvttps2udqs
        (insInfoFlags)(Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX), // INS_vcvttps2uqqs
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_EVEX), // INS_vcvttsd2sis32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vcvttsd2sis64
        (insInfoFlags)(Input_64Bit | REX_W0 | Encoding_EVEX), // INS_vcvttsd2usis32
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX), // INS_vcvttsd2usis64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vcvttss2sis32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_EVEX), // INS_vcvttss2sis64
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vcvttss2usis32
        (insInfoFlags)(Input_32Bit | REX_W1 | Encoding_EVEX), // INS_vcvttss2usis64
        (insInfoFlags)(Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vminmaxpd
        (insInfoFlags)(Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vminmaxps
        (insInfoFlags)(Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vminmaxsd
        (insInfoFlags)(Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction), // INS_vminmaxss
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX), // INS_vmovd_simd
        (insInfoFlags)(Input_16Bit | REX_W0 | Encoding_EVEX), // INS_vmovw_simd
        (insInfoFlags)(KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction), // INS_vmpsadbw
        (insInfoFlags)(Input_64Bit | REX_W1 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF), // INS_vucomxsd
        (insInfoFlags)(Input_32Bit | REX_W0 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF), // INS_vucomxss
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpo
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpno
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpb
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpae
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpe
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpne
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpbe
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpa
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmps
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpns
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpt
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpf
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpl
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpge
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmple
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ccmpg
        (insInfoFlags)(Reads_OF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovo
        (insInfoFlags)(Reads_OF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovno
        (insInfoFlags)(Reads_CF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovb
        (insInfoFlags)(Reads_CF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovae
        (insInfoFlags)(Reads_ZF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmove
        (insInfoFlags)(Reads_ZF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovne
        (insInfoFlags)(Reads_ZF | Reads_CF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovbe
        (insInfoFlags)(Reads_ZF | Reads_CF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmova
        (insInfoFlags)(Reads_SF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovs
        (insInfoFlags)(Reads_SF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovns
        (insInfoFlags)(Reads_PF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovp
        (insInfoFlags)(Reads_PF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovnp
        (insInfoFlags)(Reads_OF | Reads_SF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovl
        (insInfoFlags)(Reads_OF | Reads_SF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovge
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovle
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_cfcmovg
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctesto
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestno
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestb
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestae
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_cteste
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestne
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestbe
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctesta
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctests
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestns
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestt
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestf
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestl
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestge
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestle
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY), // INS_ctestg
        (insInfoFlags)(Encoding_EVEX_APX_ONLY), // INS_crc32_apx
        (insInfoFlags)(Encoding_EVEX_APX_ONLY), // INS_movbe_apx
        (insInfoFlags)(Reads_OF | Encoding_EVEX_APX_ONLY), // INS_seto_apx
        (insInfoFlags)(Reads_OF | Encoding_EVEX_APX_ONLY), // INS_setno_apx
        (insInfoFlags)(Reads_CF | Encoding_EVEX_APX_ONLY), // INS_setb_apx
        (insInfoFlags)(Reads_CF | Encoding_EVEX_APX_ONLY), // INS_setae_apx
        (insInfoFlags)(Reads_ZF | Encoding_EVEX_APX_ONLY), // INS_sete_apx
        (insInfoFlags)(Reads_ZF | Encoding_EVEX_APX_ONLY), // INS_setne_apx
        (insInfoFlags)(Reads_ZF | Reads_CF | Encoding_EVEX_APX_ONLY), // INS_setbe_apx
        (insInfoFlags)(Reads_ZF | Reads_CF | Encoding_EVEX_APX_ONLY), // INS_seta_apx
        (insInfoFlags)(Reads_SF | Encoding_EVEX_APX_ONLY), // INS_sets_apx
        (insInfoFlags)(Reads_SF | Encoding_EVEX_APX_ONLY), // INS_setns_apx
        (insInfoFlags)(Reads_PF | Encoding_EVEX_APX_ONLY), // INS_setp_apx
        (insInfoFlags)(Reads_PF | Encoding_EVEX_APX_ONLY), // INS_setnp_apx
        (insInfoFlags)(Reads_OF | Reads_SF | Encoding_EVEX_APX_ONLY), // INS_setl_apx
        (insInfoFlags)(Reads_OF | Reads_SF | Encoding_EVEX_APX_ONLY), // INS_setge_apx
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | Encoding_EVEX_APX_ONLY), // INS_setle_apx
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | Encoding_EVEX_APX_ONLY), // INS_setg_apx
        (insInfoFlags)(INS_FLAGS_None), // INS_crc32
        (insInfoFlags)(Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_tzcnt
        (insInfoFlags)(Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_tzcnt_apx
        (insInfoFlags)(Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2), // INS_lzcnt
        (insInfoFlags)(Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_lzcnt_apx
        (insInfoFlags)(INS_FLAGS_None), // INS_movbe
        (insInfoFlags)(Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Resets_CF | Encoding_REX2), // INS_popcnt
        (insInfoFlags)(Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Resets_CF | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY), // INS_popcnt_apx
        (insInfoFlags)(Resets_OF | Resets_SF | Resets_ZF | Resets_AF | Resets_PF | Writes_CF), // INS_tpause
        (insInfoFlags)(INS_FLAGS_None), // INS_umonitor
        (insInfoFlags)(Resets_OF | Resets_SF | Resets_ZF | Resets_AF | Resets_PF | Writes_CF), // INS_umwait
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_neg
        (insInfoFlags)(INS_FLAGS_None | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_not
        (insInfoFlags)(Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_rol
        (insInfoFlags)(Writes_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_rol_1
        (insInfoFlags)(Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_rol_N
        (insInfoFlags)(Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_ror
        (insInfoFlags)(Writes_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_ror_1
        (insInfoFlags)(Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_ror_N
        (insInfoFlags)(Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcl
        (insInfoFlags)(Writes_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcl_1
        (insInfoFlags)(Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcl_N
        (insInfoFlags)(Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcr
        (insInfoFlags)(Writes_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcr_1
        (insInfoFlags)(Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD), // INS_rcr_N
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shl
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shl_1
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shl_N
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shr
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shr_1
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_shr_N
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_sar
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_sar_1
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF), // INS_sar_N
        (insInfoFlags)(INS_FLAGS_None), // INS_ret
        (insInfoFlags)(INS_FLAGS_None), // INS_loop
        (insInfoFlags)(Encoding_REX2), // INS_call
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_r_movsb
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_r_movsd
        (insInfoFlags)(Reads_DF), // INS_r_movsq
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_movsb
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_movsd
        (insInfoFlags)(Reads_DF), // INS_movsq
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_r_stosb
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_r_stosd
        (insInfoFlags)(Reads_DF), // INS_r_stosq
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_stosb
        (insInfoFlags)(Reads_DF | INS_FLAGS_HasWBit), // INS_stosd
        (insInfoFlags)(Reads_DF), // INS_stosq
        (insInfoFlags)(INS_FLAGS_None), // INS_int3
        (insInfoFlags)(INS_FLAGS_None), // INS_nop
        (insInfoFlags)(INS_FLAGS_None), // INS_pause
        (insInfoFlags)(INS_FLAGS_None), // INS_lock
        (insInfoFlags)(INS_FLAGS_None), // INS_leave
        (insInfoFlags)(INS_FLAGS_None), // INS_serialize
        (insInfoFlags)(INS_FLAGS_HasPseudoName), // INS_cwde
        (insInfoFlags)(INS_FLAGS_HasPseudoName), // INS_cdq
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Undefined_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF), // INS_idiv
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF), // INS_imulEAX
        (insInfoFlags)(Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Undefined_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF), // INS_div
        (insInfoFlags)(Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF), // INS_mulEAX
        (insInfoFlags)(Restore_SF_ZF_AF_PF_CF), // INS_sahf
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2), // INS_xadd
        (insInfoFlags)(Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2), // INS_cmpxchg
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | Encoding_REX2), // INS_shld
        (insInfoFlags)(Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | Encoding_REX2), // INS_shrd
        (insInfoFlags)(Reads_OF | Encoding_REX2), // INS_seto
        (insInfoFlags)(Reads_OF | Encoding_REX2), // INS_setno
        (insInfoFlags)(Reads_CF | Encoding_REX2), // INS_setb
        (insInfoFlags)(Reads_CF | Encoding_REX2), // INS_setae
        (insInfoFlags)(Reads_ZF | Encoding_REX2), // INS_sete
        (insInfoFlags)(Reads_ZF | Encoding_REX2), // INS_setne
        (insInfoFlags)(Reads_ZF | Reads_CF | Encoding_REX2), // INS_setbe
        (insInfoFlags)(Reads_ZF | Reads_CF | Encoding_REX2), // INS_seta
        (insInfoFlags)(Reads_SF | Encoding_REX2), // INS_sets
        (insInfoFlags)(Reads_SF | Encoding_REX2), // INS_setns
        (insInfoFlags)(Reads_PF | Encoding_REX2), // INS_setp
        (insInfoFlags)(Reads_PF | Encoding_REX2), // INS_setnp
        (insInfoFlags)(Reads_OF | Reads_SF | Encoding_REX2), // INS_setl
        (insInfoFlags)(Reads_OF | Reads_SF | Encoding_REX2), // INS_setge
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2), // INS_setle
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2), // INS_setg
        (insInfoFlags)(Encoding_REX2), // INS_tail_i_jmp
        (insInfoFlags)(Encoding_REX2), // INS_i_jmp
        (insInfoFlags)(INS_FLAGS_None), // INS_jmp
        (insInfoFlags)(Reads_OF), // INS_jo
        (insInfoFlags)(Reads_OF), // INS_jno
        (insInfoFlags)(Reads_CF), // INS_jb
        (insInfoFlags)(Reads_CF), // INS_jae
        (insInfoFlags)(Reads_ZF), // INS_je
        (insInfoFlags)(Reads_ZF), // INS_jne
        (insInfoFlags)(Reads_ZF | Reads_CF), // INS_jbe
        (insInfoFlags)(Reads_ZF | Reads_CF), // INS_ja
        (insInfoFlags)(Reads_SF), // INS_js
        (insInfoFlags)(Reads_SF), // INS_jns
        (insInfoFlags)(Reads_PF), // INS_jp
        (insInfoFlags)(Reads_PF), // INS_jnp
        (insInfoFlags)(Reads_OF | Reads_SF), // INS_jl
        (insInfoFlags)(Reads_OF | Reads_SF), // INS_jge
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF), // INS_jle
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF), // INS_jg
        (insInfoFlags)(INS_FLAGS_None), // INS_l_jmp
        (insInfoFlags)(Reads_OF), // INS_l_jo
        (insInfoFlags)(Reads_OF), // INS_l_jno
        (insInfoFlags)(Reads_CF), // INS_l_jb
        (insInfoFlags)(Reads_CF), // INS_l_jae
        (insInfoFlags)(Reads_ZF), // INS_l_je
        (insInfoFlags)(Reads_ZF), // INS_l_jne
        (insInfoFlags)(Reads_ZF | Reads_CF), // INS_l_jbe
        (insInfoFlags)(Reads_ZF | Reads_CF), // INS_l_ja
        (insInfoFlags)(Reads_SF), // INS_l_js
        (insInfoFlags)(Reads_SF), // INS_l_jns
        (insInfoFlags)(Reads_PF), // INS_l_jp
        (insInfoFlags)(Reads_PF), // INS_l_jnp
        (insInfoFlags)(Reads_OF | Reads_SF), // INS_l_jl
        (insInfoFlags)(Reads_OF | Reads_SF), // INS_l_jge
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF), // INS_l_jle
        (insInfoFlags)(Reads_OF | Reads_SF | Reads_ZF), // INS_l_jg
        (insInfoFlags)(INS_FLAGS_None), // INS_align
        (insInfoFlags)(INS_FLAGS_None), // INS_data16
    ];

    private static ReadOnlySpan<insInfoFlags> s_instInfoARM => [
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_invalid
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_add
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_sub
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldr
        (insInfoFlags)((ARM_ST) | (INST_FP * (0))), // INS_str
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldrb
        (insInfoFlags)((ARM_ST) | (INST_FP * (0))), // INS_strb
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldrh
        (insInfoFlags)((ARM_ST) | (INST_FP * (0))), // INS_strh
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldrsb
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldrsh
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_mov
        (insInfoFlags)((ARM_CMP) | (INST_FP * (0))), // INS_cmp
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_lsl
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_lsr
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_asr
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_ror
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_pld
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_pldw
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_movt
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_movw
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_and
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_eor
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_orr
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_orn
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bic
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_adc
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_sbc
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_rsb
        (insInfoFlags)((ARM_CMP) | (INST_FP * (0))), // INS_tst
        (insInfoFlags)((ARM_CMP) | (INST_FP * (0))), // INS_teq
        (insInfoFlags)((ARM_CMP) | (INST_FP * (0))), // INS_cmn
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_mvn
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_push
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_pop
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_b
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_beq
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bne
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bhs
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_blo
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bmi
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bpl
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bvs
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bvc
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bhi
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bls
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bge
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_blt
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bgt
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_ble
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bx
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_blx
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldm
        (insInfoFlags)((ARM_ST) | (INST_FP * (0))), // INS_stm
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_sxtb
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_sxth
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_uxtb
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_uxth
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_mul
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_adr
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_addw
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bfc
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bfi
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bl
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_bkpt
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_cbnz
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_cbz
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_clz
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_dmb
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_ism
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldmdb
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldrd
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldrex
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldrexb
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldrexd
        (insInfoFlags)((ARM_LD) | (INST_FP * (0))), // INS_ldrexh
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_mla
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_mls
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_nop
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_nopw
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_sbfx
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_sdiv
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_ssat
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_smlal
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_smull
        (insInfoFlags)((ARM_ST) | (INST_FP * (0))), // INS_stmdb
        (insInfoFlags)((ARM_ST) | (INST_FP * (0))), // INS_strd
        (insInfoFlags)((ARM_ST) | (INST_FP * (0))), // INS_strex
        (insInfoFlags)((ARM_ST) | (INST_FP * (0))), // INS_strexb
        (insInfoFlags)((ARM_ST) | (INST_FP * (0))), // INS_strexd
        (insInfoFlags)((ARM_ST) | (INST_FP * (0))), // INS_strexh
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_subw
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_tbb
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_tbh
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_ubfx
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_udiv
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_umlal
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_umull
        (insInfoFlags)((0) | (INST_FP * (0))), // INS_usat
        (insInfoFlags)((ARM_ST) | (INST_FP * (1))), // INS_vstr
        (insInfoFlags)((ARM_LD) | (INST_FP * (1))), // INS_vldr
        (insInfoFlags)((ARM_ST) | (INST_FP * (1))), // INS_vstm
        (insInfoFlags)((ARM_LD) | (INST_FP * (1))), // INS_vldm
        (insInfoFlags)((ARM_ST) | (INST_FP * (1))), // INS_vpush
        (insInfoFlags)((ARM_LD) | (INST_FP * (1))), // INS_vpop
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vmrs
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vadd
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vsub
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vmul
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vdiv
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vmov
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vabs
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vsqrt
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vneg
        (insInfoFlags)((ARM_CMP) | (INST_FP * (1))), // INS_vcmp
        (insInfoFlags)((ARM_CMP) | (INST_FP * (1))), // INS_vcmp0
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vcvt_d2i
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vcvt_f2i
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vcvt_d2u
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vcvt_f2u
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vcvt_i2f
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vcvt_i2d
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vcvt_u2f
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vcvt_u2d
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vcvt_d2f
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vcvt_f2d
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vmov_i2d
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vmov_d2i
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vmov_i2f
        (insInfoFlags)((0) | (INST_FP * (1))), // INS_vmov_f2i
        (insInfoFlags)0, // INS_lea is zero-initialized in native instInfo[INS_count].
    ];

    private static ReadOnlySpan<insInfoFlags> s_instInfoARM64 => [
        (insInfoFlags)(0), // invalid
        (insInfoFlags)(0), // mov
        (insInfoFlags)(0), // add
        (insInfoFlags)(0), // sub
        (insInfoFlags)(LD), // ld1
        (insInfoFlags)(LD), // ld2
        (insInfoFlags)(LD), // ld3
        (insInfoFlags)(LD), // ld4
        (insInfoFlags)(LD), // st1
        (insInfoFlags)(ST), // st2
        (insInfoFlags)(ST), // st3
        (insInfoFlags)(ST), // st4
        (insInfoFlags)(LD), // ldr
        (insInfoFlags)(LD), // ldrsw
        (insInfoFlags)(0), // fmov
        (insInfoFlags)(0), // orr
        (insInfoFlags)(LD), // ldrb
        (insInfoFlags)(LD), // ldrh
        (insInfoFlags)(LD), // ldrsb
        (insInfoFlags)(LD), // ldrsh
        (insInfoFlags)(ST), // str
        (insInfoFlags)(ST), // strb
        (insInfoFlags)(ST), // strh
        (insInfoFlags)(0), // adds
        (insInfoFlags)(0), // subs
        (insInfoFlags)(CMP), // cmp
        (insInfoFlags)(CMP), // cmn
        (insInfoFlags)(0), // fmul
        (insInfoFlags)(0), // fmulx
        (insInfoFlags)(0), // and
        (insInfoFlags)(0), // eor
        (insInfoFlags)(0), // bic
        (insInfoFlags)(0), // neg
        (insInfoFlags)(0), // cmeq
        (insInfoFlags)(0), // cmge
        (insInfoFlags)(0), // cmgt
        (insInfoFlags)(0), // fcmeq
        (insInfoFlags)(0), // fcmge
        (insInfoFlags)(0), // fcmgt
        (insInfoFlags)(0), // sqshl
        (insInfoFlags)(0), // uqshl
        (insInfoFlags)(LNG), // sqdmlal
        (insInfoFlags)(LNG), // sqdmlsl
        (insInfoFlags)(0), // sqdmulh
        (insInfoFlags)(LNG), // sqdmull
        (insInfoFlags)(0), // sqrdmlah
        (insInfoFlags)(0), // sqrdmlsh
        (insInfoFlags)(0), // sqrdmulh
        (insInfoFlags)(0), // ands
        (insInfoFlags)(0), // tst
        (insInfoFlags)(0), // orn
        (insInfoFlags)(0), // dup
        (insInfoFlags)(0), // fmla
        (insInfoFlags)(0), // fmls
        (insInfoFlags)(0), // fcvtas
        (insInfoFlags)(0), // fcvtau
        (insInfoFlags)(0), // fcvtms
        (insInfoFlags)(0), // fcvtmu
        (insInfoFlags)(0), // fcvtns
        (insInfoFlags)(0), // fcvtnu
        (insInfoFlags)(0), // fcvtps
        (insInfoFlags)(0), // fcvtpu
        (insInfoFlags)(0), // fcvtzs
        (insInfoFlags)(0), // fcvtzu
        (insInfoFlags)(0), // scvtf
        (insInfoFlags)(0), // ucvtf
        (insInfoFlags)(0), // mul
        (insInfoFlags)(LNG), // smull
        (insInfoFlags)(LNG), // umull
        (insInfoFlags)(0), // mvn
        (insInfoFlags)(LD), // ld1_2regs
        (insInfoFlags)(LD), // ld1_3regs
        (insInfoFlags)(LD), // ld1_4regs
        (insInfoFlags)(ST), // st1_2regs
        (insInfoFlags)(ST), // st1_3regs
        (insInfoFlags)(ST), // st1_4regs
        (insInfoFlags)(LD), // ld1r
        (insInfoFlags)(LD), // ld2r
        (insInfoFlags)(LD), // ld3r
        (insInfoFlags)(LD), // ld4r
        (insInfoFlags)(0), // negs
        (insInfoFlags)(0), // bics
        (insInfoFlags)(0), // eon
        (insInfoFlags)(0), // lsl
        (insInfoFlags)(0), // lsr
        (insInfoFlags)(0), // asr
        (insInfoFlags)(0), // ror
        (insInfoFlags)(LD), // ldp
        (insInfoFlags)(LD), // ldpsw
        (insInfoFlags)(ST), // stp
        (insInfoFlags)(LD), // ldnp
        (insInfoFlags)(ST), // stnp
        (insInfoFlags)(CMP), // ccmp
        (insInfoFlags)(CMP), // ccmn
        (insInfoFlags)(0), // ins
        (insInfoFlags)(0), // fadd
        (insInfoFlags)(0), // fsub
        (insInfoFlags)(0), // fdiv
        (insInfoFlags)(0), // fmax
        (insInfoFlags)(0), // fmaxnm
        (insInfoFlags)(0), // fmin
        (insInfoFlags)(0), // fminnm
        (insInfoFlags)(0), // fabd
        (insInfoFlags)(0), // facge
        (insInfoFlags)(0), // facgt
        (insInfoFlags)(0), // frecps
        (insInfoFlags)(0), // frsqrts
        (insInfoFlags)(0), // fcmp
        (insInfoFlags)(0), // fcmpe
        (insInfoFlags)(0), // fabs
        (insInfoFlags)(0), // fcmle
        (insInfoFlags)(0), // fcmlt
        (insInfoFlags)(NRW), // fcvtxn
        (insInfoFlags)(0), // fneg
        (insInfoFlags)(0), // frecpe
        (insInfoFlags)(0), // frintn
        (insInfoFlags)(0), // frintp
        (insInfoFlags)(0), // frintm
        (insInfoFlags)(0), // frintz
        (insInfoFlags)(0), // frinta
        (insInfoFlags)(0), // frintx
        (insInfoFlags)(0), // frinti
        (insInfoFlags)(0), // frsqrte
        (insInfoFlags)(0), // fsqrt
        (insInfoFlags)(0), // abs
        (insInfoFlags)(0), // cmle
        (insInfoFlags)(0), // cmlt
        (insInfoFlags)(0), // sqabs
        (insInfoFlags)(0), // sqneg
        (insInfoFlags)(NRW), // sqxtn
        (insInfoFlags)(NRW), // sqxtun
        (insInfoFlags)(0), // suqadd
        (insInfoFlags)(0), // usqadd
        (insInfoFlags)(NRW), // uqxtn
        (insInfoFlags)(0), // cls
        (insInfoFlags)(0), // clz
        (insInfoFlags)(0), // rbit
        (insInfoFlags)(0), // cnt
        (insInfoFlags)(0), // rev16
        (insInfoFlags)(0), // rev32
        (insInfoFlags)(0), // mla
        (insInfoFlags)(0), // mls
        (insInfoFlags)(LNG), // smlal
        (insInfoFlags)(LNG), // smlal2
        (insInfoFlags)(LNG), // smlsl
        (insInfoFlags)(LNG), // smlsl2
        (insInfoFlags)(LNG), // smull2
        (insInfoFlags)(LNG), // sqdmlal2
        (insInfoFlags)(LNG), // sqdmlsl2
        (insInfoFlags)(LNG), // sqdmull2
        (insInfoFlags)(0), // sdot
        (insInfoFlags)(0), // udot
        (insInfoFlags)(LNG), // umlal
        (insInfoFlags)(LNG), // umlal2
        (insInfoFlags)(LNG), // umlsl
        (insInfoFlags)(LNG), // umlsl2
        (insInfoFlags)(LNG), // umull2
        (insInfoFlags)(RSH), // sshr
        (insInfoFlags)(RSH), // ssra
        (insInfoFlags)(RSH), // srshr
        (insInfoFlags)(RSH), // srsra
        (insInfoFlags)(0), // shl
        (insInfoFlags)(RSH), // ushr
        (insInfoFlags)(RSH), // usra
        (insInfoFlags)(RSH), // urshr
        (insInfoFlags)(RSH), // ursra
        (insInfoFlags)(RSH), // sri
        (insInfoFlags)(0), // sli
        (insInfoFlags)(0), // sqshlu
        (insInfoFlags)(RSH | NRW), // sqrshrn
        (insInfoFlags)(RSH | NRW), // sqrshrun
        (insInfoFlags)(RSH | NRW), // sqshrn
        (insInfoFlags)(RSH | NRW), // sqshrun
        (insInfoFlags)(RSH | NRW), // uqrshrn
        (insInfoFlags)(RSH | NRW), // uqshrn
        (insInfoFlags)(0), // cmhi
        (insInfoFlags)(0), // cmhs
        (insInfoFlags)(0), // cmtst
        (insInfoFlags)(0), // sqadd
        (insInfoFlags)(0), // sqrshl
        (insInfoFlags)(0), // sqsub
        (insInfoFlags)(0), // srshl
        (insInfoFlags)(0), // sshl
        (insInfoFlags)(0), // uqadd
        (insInfoFlags)(0), // uqrshl
        (insInfoFlags)(0), // uqsub
        (insInfoFlags)(0), // urshl
        (insInfoFlags)(0), // ushl
        (insInfoFlags)(0), // faddp
        (insInfoFlags)(0), // fmaxnmp
        (insInfoFlags)(0), // fmaxp
        (insInfoFlags)(0), // fminnmp
        (insInfoFlags)(0), // fminp
        (insInfoFlags)(0), // addp
        (insInfoFlags)(LD), // ldar
        (insInfoFlags)(LD), // ldarb
        (insInfoFlags)(LD), // ldarh
        (insInfoFlags)(LD), // ldapr
        (insInfoFlags)(LD), // ldaprb
        (insInfoFlags)(LD), // ldaprh
        (insInfoFlags)(LD), // ldxr
        (insInfoFlags)(LD), // ldxrb
        (insInfoFlags)(LD), // ldxrh
        (insInfoFlags)(LD), // ldaxr
        (insInfoFlags)(LD), // ldaxrb
        (insInfoFlags)(LD), // ldaxrh
        (insInfoFlags)(LD), // ldur
        (insInfoFlags)(LD), // ldurb
        (insInfoFlags)(LD), // ldurh
        (insInfoFlags)(LD), // ldursb
        (insInfoFlags)(LD), // ldursh
        (insInfoFlags)(LD), // ldursw
        (insInfoFlags)(LD), // ldapur
        (insInfoFlags)(LD), // ldapurb
        (insInfoFlags)(LD), // ldapurh
        (insInfoFlags)(ST), // stlr
        (insInfoFlags)(ST), // stlrb
        (insInfoFlags)(ST), // stlrh
        (insInfoFlags)(ST), // stxr
        (insInfoFlags)(ST), // stxrb
        (insInfoFlags)(ST), // stxrh
        (insInfoFlags)(ST), // stlxr
        (insInfoFlags)(ST), // stlxrb
        (insInfoFlags)(ST), // stlxrh
        (insInfoFlags)(ST), // stur
        (insInfoFlags)(ST), // sturb
        (insInfoFlags)(ST), // sturh
        (insInfoFlags)(ST), // stlur
        (insInfoFlags)(ST), // stlurb
        (insInfoFlags)(ST), // stlurh
        (insInfoFlags)(LD | ST), // casb
        (insInfoFlags)(LD | ST), // casab
        (insInfoFlags)(LD | ST), // casalb
        (insInfoFlags)(LD | ST), // caslb
        (insInfoFlags)(LD | ST), // cash
        (insInfoFlags)(LD | ST), // casah
        (insInfoFlags)(LD | ST), // casalh
        (insInfoFlags)(LD | ST), // caslh
        (insInfoFlags)(LD | ST), // cas
        (insInfoFlags)(LD | ST), // casa
        (insInfoFlags)(LD | ST), // casal
        (insInfoFlags)(LD | ST), // casl
        (insInfoFlags)(WR2 | LD | ST), // ldaddb
        (insInfoFlags)(WR2 | LD | ST), // ldaddab
        (insInfoFlags)(WR2 | LD | ST), // ldaddalb
        (insInfoFlags)(WR2 | LD | ST), // ldaddlb
        (insInfoFlags)(WR2 | LD | ST), // ldaddh
        (insInfoFlags)(WR2 | LD | ST), // ldaddah
        (insInfoFlags)(WR2 | LD | ST), // ldaddalh
        (insInfoFlags)(WR2 | LD | ST), // ldaddlh
        (insInfoFlags)(WR2 | LD | ST), // ldadd
        (insInfoFlags)(WR2 | LD | ST), // ldadda
        (insInfoFlags)(WR2 | LD | ST), // ldaddal
        (insInfoFlags)(WR2 | LD | ST), // ldclral
        (insInfoFlags)(WR2 | LD | ST), // ldsetal
        (insInfoFlags)(WR2 | LD | ST), // ldaddl
        (insInfoFlags)(WR2 | ST), // staddb
        (insInfoFlags)(WR2 | ST), // staddlb
        (insInfoFlags)(WR2 | ST), // staddh
        (insInfoFlags)(WR2 | ST), // staddlh
        (insInfoFlags)(WR2 | ST), // stadd
        (insInfoFlags)(WR2 | ST), // staddl
        (insInfoFlags)(WR2 | LD | ST), // swpb
        (insInfoFlags)(WR2 | LD | ST), // swpab
        (insInfoFlags)(WR2 | LD | ST), // swpalb
        (insInfoFlags)(WR2 | LD | ST), // swplb
        (insInfoFlags)(WR2 | LD | ST), // swph
        (insInfoFlags)(WR2 | LD | ST), // swpah
        (insInfoFlags)(WR2 | LD | ST), // swpalh
        (insInfoFlags)(WR2 | LD | ST), // swplh
        (insInfoFlags)(WR2 | LD | ST), // swp
        (insInfoFlags)(WR2 | LD | ST), // swpa
        (insInfoFlags)(WR2 | LD | ST), // swpal
        (insInfoFlags)(WR2 | LD | ST), // swpl
        (insInfoFlags)(0), // adr
        (insInfoFlags)(0), // adrp
        (insInfoFlags)(0), // b
        (insInfoFlags)(0), // b_tail
        (insInfoFlags)(0), // bl_local
        (insInfoFlags)(0), // bl
        (insInfoFlags)(0), // br
        (insInfoFlags)(0), // br_tail
        (insInfoFlags)(0), // blr
        (insInfoFlags)(0), // ret
        (insInfoFlags)(0), // retaa
        (insInfoFlags)(0), // retab
        (insInfoFlags)(0), // beq
        (insInfoFlags)(0), // bne
        (insInfoFlags)(0), // bhs
        (insInfoFlags)(0), // blo
        (insInfoFlags)(0), // bmi
        (insInfoFlags)(0), // bpl
        (insInfoFlags)(0), // bvs
        (insInfoFlags)(0), // bvc
        (insInfoFlags)(0), // bhi
        (insInfoFlags)(0), // bls
        (insInfoFlags)(0), // bge
        (insInfoFlags)(0), // blt
        (insInfoFlags)(0), // bgt
        (insInfoFlags)(0), // ble
        (insInfoFlags)(0), // cbz
        (insInfoFlags)(0), // cbnz
        (insInfoFlags)(0), // tbz
        (insInfoFlags)(0), // tbnz
        (insInfoFlags)(0), // movk
        (insInfoFlags)(0), // movn
        (insInfoFlags)(0), // movz
        (insInfoFlags)(0), // csel
        (insInfoFlags)(0), // csinc
        (insInfoFlags)(0), // csinv
        (insInfoFlags)(0), // csneg
        (insInfoFlags)(0), // cinc
        (insInfoFlags)(0), // cinv
        (insInfoFlags)(0), // cneg
        (insInfoFlags)(0), // cset
        (insInfoFlags)(0), // csetm
        (insInfoFlags)(0), // aese
        (insInfoFlags)(0), // aesd
        (insInfoFlags)(0), // aesmc
        (insInfoFlags)(0), // aesimc
        (insInfoFlags)(0), // rev
        (insInfoFlags)(0), // rev64
        (insInfoFlags)(0), // adc
        (insInfoFlags)(0), // adcs
        (insInfoFlags)(0), // sbc
        (insInfoFlags)(0), // sbcs
        (insInfoFlags)(0), // udiv
        (insInfoFlags)(0), // sdiv
        (insInfoFlags)(0), // mneg
        (insInfoFlags)(0), // madd
        (insInfoFlags)(0), // msub
        (insInfoFlags)(0), // smaddl
        (insInfoFlags)(0), // smnegl
        (insInfoFlags)(0), // smsubl
        (insInfoFlags)(0), // smulh
        (insInfoFlags)(0), // umaddl
        (insInfoFlags)(0), // umnegl
        (insInfoFlags)(0), // umsubl
        (insInfoFlags)(0), // umulh
        (insInfoFlags)(0), // extr
        (insInfoFlags)(0), // lslv
        (insInfoFlags)(0), // lsrv
        (insInfoFlags)(0), // asrv
        (insInfoFlags)(0), // rorv
        (insInfoFlags)(0), // crc32b
        (insInfoFlags)(0), // crc32h
        (insInfoFlags)(0), // crc32w
        (insInfoFlags)(0), // crc32x
        (insInfoFlags)(0), // crc32cb
        (insInfoFlags)(0), // crc32ch
        (insInfoFlags)(0), // crc32cw
        (insInfoFlags)(0), // crc32cx
        (insInfoFlags)(0), // sha1c
        (insInfoFlags)(0), // sha1m
        (insInfoFlags)(0), // sha1p
        (insInfoFlags)(0), // sha1h
        (insInfoFlags)(0), // sha1su0
        (insInfoFlags)(0), // sha1su1
        (insInfoFlags)(0), // sha256h
        (insInfoFlags)(0), // sha256h2
        (insInfoFlags)(0), // sha256su0
        (insInfoFlags)(0), // sha256su1
        (insInfoFlags)(0), // ext
        (insInfoFlags)(0), // sbfm
        (insInfoFlags)(0), // bfm
        (insInfoFlags)(0), // ubfm
        (insInfoFlags)(0), // sbfiz
        (insInfoFlags)(0), // bfi
        (insInfoFlags)(0), // ubfiz
        (insInfoFlags)(0), // sbfx
        (insInfoFlags)(0), // bfxil
        (insInfoFlags)(0), // ubfx
        (insInfoFlags)(0), // sxtb
        (insInfoFlags)(0), // sxth
        (insInfoFlags)(0), // sxtw
        (insInfoFlags)(0), // uxtb
        (insInfoFlags)(0), // uxth
        (insInfoFlags)(0), // autia1716
        (insInfoFlags)(0), // autiasp
        (insInfoFlags)(0), // autib1716
        (insInfoFlags)(0), // autibsp
        (insInfoFlags)(0), // autibz
        (insInfoFlags)(0), // autiaz
        (insInfoFlags)(0), // pacia1716
        (insInfoFlags)(0), // paciasp
        (insInfoFlags)(0), // pacib1716
        (insInfoFlags)(0), // pacibsp
        (insInfoFlags)(0), // pacibz
        (insInfoFlags)(0), // paciaz
        (insInfoFlags)(0), // xpaclri
        (insInfoFlags)(0), // autiza
        (insInfoFlags)(0), // autizb
        (insInfoFlags)(0), // paciza
        (insInfoFlags)(0), // pacizb
        (insInfoFlags)(0), // xpacd
        (insInfoFlags)(0), // xpaci
        (insInfoFlags)(0), // autia
        (insInfoFlags)(0), // autib
        (insInfoFlags)(0), // pacia
        (insInfoFlags)(0), // pacib
        (insInfoFlags)(0), // nop
        (insInfoFlags)(0), // yield
        (insInfoFlags)(0), // brk
        (insInfoFlags)(0), // dsb
        (insInfoFlags)(0), // dmb
        (insInfoFlags)(0), // isb
        (insInfoFlags)(0), // dczva
        (insInfoFlags)(0), // mrs_tpid0
        (insInfoFlags)(0), // umov
        (insInfoFlags)(0), // smov
        (insInfoFlags)(0), // movi
        (insInfoFlags)(0), // mvni
        (insInfoFlags)(0), // urecpe
        (insInfoFlags)(0), // ursqrte
        (insInfoFlags)(0), // bsl
        (insInfoFlags)(0), // bit
        (insInfoFlags)(0), // bif
        (insInfoFlags)(0), // addv
        (insInfoFlags)(0), // ctz
        (insInfoFlags)(0), // not
        (insInfoFlags)(LNG), // saddlv
        (insInfoFlags)(0), // smaxv
        (insInfoFlags)(0), // sminv
        (insInfoFlags)(0), // uaddlv
        (insInfoFlags)(0), // umaxv
        (insInfoFlags)(0), // uminv
        (insInfoFlags)(0), // fmaxnmv
        (insInfoFlags)(0), // fmaxv
        (insInfoFlags)(0), // fminnmv
        (insInfoFlags)(0), // fminv
        (insInfoFlags)(0), // uzp1
        (insInfoFlags)(0), // uzp2
        (insInfoFlags)(0), // zip1
        (insInfoFlags)(0), // zip2
        (insInfoFlags)(0), // trn1
        (insInfoFlags)(0), // trn2
        (insInfoFlags)(NRW), // sqxtn2
        (insInfoFlags)(NRW), // sqxtun2
        (insInfoFlags)(NRW), // uqxtn2
        (insInfoFlags)(NRW), // xtn
        (insInfoFlags)(NRW), // xtn2
        (insInfoFlags)(0), // fnmul
        (insInfoFlags)(0), // fmadd
        (insInfoFlags)(0), // fmsub
        (insInfoFlags)(0), // fnmadd
        (insInfoFlags)(0), // fnmsub
        (insInfoFlags)(0), // fcvt
        (insInfoFlags)(0), // pmul
        (insInfoFlags)(0), // saba
        (insInfoFlags)(0), // sabd
        (insInfoFlags)(0), // smax
        (insInfoFlags)(0), // smaxp
        (insInfoFlags)(0), // smin
        (insInfoFlags)(0), // sminp
        (insInfoFlags)(0), // uaba
        (insInfoFlags)(0), // uabd
        (insInfoFlags)(0), // umax
        (insInfoFlags)(0), // umaxp
        (insInfoFlags)(0), // umin
        (insInfoFlags)(0), // uminp
        (insInfoFlags)(LNG), // fcvtl
        (insInfoFlags)(LNG), // fcvtl2
        (insInfoFlags)(NRW), // fcvtn
        (insInfoFlags)(NRW), // fcvtn2
        (insInfoFlags)(NRW), // fcvtxn2
        (insInfoFlags)(0), // frecpx
        (insInfoFlags)(NRW), // addhn
        (insInfoFlags)(NRW), // addhn2
        (insInfoFlags)(LNG), // pmull
        (insInfoFlags)(LNG), // pmull2
        (insInfoFlags)(NRW), // raddhn
        (insInfoFlags)(NRW), // raddhn2
        (insInfoFlags)(NRW), // rsubhn
        (insInfoFlags)(NRW), // rsubhn2
        (insInfoFlags)(LNG), // sabal
        (insInfoFlags)(LNG), // sabal2
        (insInfoFlags)(LNG), // sabdl
        (insInfoFlags)(LNG), // sabdl2
        (insInfoFlags)(LNG), // sadalp
        (insInfoFlags)(LNG), // saddl
        (insInfoFlags)(LNG), // saddl2
        (insInfoFlags)(LNG), // saddlp
        (insInfoFlags)(WID), // saddw
        (insInfoFlags)(WID), // saddw2
        (insInfoFlags)(0), // shadd
        (insInfoFlags)(0), // shsub
        (insInfoFlags)(0), // srhadd
        (insInfoFlags)(LNG), // ssubl
        (insInfoFlags)(LNG), // ssubl2
        (insInfoFlags)(WID), // ssubw
        (insInfoFlags)(WID), // ssubw2
        (insInfoFlags)(NRW), // subhn
        (insInfoFlags)(NRW), // subhn2
        (insInfoFlags)(LNG), // uabal
        (insInfoFlags)(LNG), // uabal2
        (insInfoFlags)(LNG), // uabdl
        (insInfoFlags)(LNG), // uabdl2
        (insInfoFlags)(LNG), // uadalp
        (insInfoFlags)(LNG), // uaddl
        (insInfoFlags)(LNG), // uaddl2
        (insInfoFlags)(LNG), // uaddlp
        (insInfoFlags)(WID), // uaddw
        (insInfoFlags)(WID), // uaddw2
        (insInfoFlags)(0), // uhadd
        (insInfoFlags)(0), // uhsub
        (insInfoFlags)(0), // urhadd
        (insInfoFlags)(LNG), // usubl
        (insInfoFlags)(LNG), // usubl2
        (insInfoFlags)(WID), // usubw
        (insInfoFlags)(WID), // usubw2
        (insInfoFlags)(LNG), // shll
        (insInfoFlags)(LNG), // shll2
        (insInfoFlags)(LNG), // sshll
        (insInfoFlags)(LNG), // sshll2
        (insInfoFlags)(LNG), // ushll
        (insInfoFlags)(LNG), // ushll2
        (insInfoFlags)(RSH | NRW), // shrn
        (insInfoFlags)(RSH | NRW), // shrn2
        (insInfoFlags)(RSH | NRW), // rshrn
        (insInfoFlags)(RSH | NRW), // rshrn2
        (insInfoFlags)(RSH | NRW), // sqrshrn2
        (insInfoFlags)(RSH | NRW), // sqrshrun2
        (insInfoFlags)(RSH | NRW), // sqshrn2
        (insInfoFlags)(RSH | NRW), // sqshrun2
        (insInfoFlags)(RSH | NRW), // uqrshrn2
        (insInfoFlags)(RSH | NRW), // uqshrn2
        (insInfoFlags)(LNG), // sxtl
        (insInfoFlags)(LNG), // sxtl2
        (insInfoFlags)(LNG), // uxtl
        (insInfoFlags)(LNG), // uxtl2
        (insInfoFlags)(0), // tbl
        (insInfoFlags)(0), // tbl_2regs
        (insInfoFlags)(0), // tbl_3regs
        (insInfoFlags)(0), // tbl_4regs
        (insInfoFlags)(0), // tbx
        (insInfoFlags)(0), // tbx_2regs
        (insInfoFlags)(0), // tbx_3regs
        (insInfoFlags)(0), // tbx_4regs
        (insInfoFlags)(0), // align
        (insInfoFlags)(0), // eor3
        (insInfoFlags)(0), // bcax
        (insInfoFlags)(0), // sm3ss1
        (insInfoFlags)(0), // sha512h
        (insInfoFlags)(0), // sha512h2
        (insInfoFlags)(0), // sha512su1
        (insInfoFlags)(0), // rax1
        (insInfoFlags)(0), // sm3partw1
        (insInfoFlags)(0), // sm3partw2
        (insInfoFlags)(0), // sm4ekey
        (insInfoFlags)(0), // xar
        (insInfoFlags)(0), // sha512su0
        (insInfoFlags)(0), // sm4e
        (insInfoFlags)(0), // invalid
        (insInfoFlags)(0), // mov
        (insInfoFlags)(ST), // st1w
        (insInfoFlags)(LD), // ld1sh
        (insInfoFlags)(LD), // ld1h
        (insInfoFlags)(LD), // ld1w
        (insInfoFlags)(LD), // ld1d
        (insInfoFlags)(ST), // st1h
        (insInfoFlags)(ST), // st1d
        (insInfoFlags)(0), // pmov
        (insInfoFlags)(LD), // ldff1sh
        (insInfoFlags)(LD), // ldff1w
        (insInfoFlags)(LD), // ldff1h
        (insInfoFlags)(LD), // ld1sw
        (insInfoFlags)(0), // mul
        (insInfoFlags)(0), // fdot
        (insInfoFlags)(LD), // ld1sb
        (insInfoFlags)(LD), // ld1b
        (insInfoFlags)(0), // prfb
        (insInfoFlags)(0), // prfd
        (insInfoFlags)(0), // prfh
        (insInfoFlags)(0), // prfw
        (insInfoFlags)(LD), // ldff1d
        (insInfoFlags)(LD), // ldff1sw
        (insInfoFlags)(ST), // st1b
        (insInfoFlags)(RSH), // asr
        (insInfoFlags)(0), // lsl
        (insInfoFlags)(RSH), // lsr
        (insInfoFlags)(0), // fmul
        (insInfoFlags)(0), // sdot
        (insInfoFlags)(0), // udot
        (insInfoFlags)(LD), // ldff1sb
        (insInfoFlags)(LD), // ldff1b
        (insInfoFlags)(0), // and
        (insInfoFlags)(0), // bic
        (insInfoFlags)(0), // eor
        (insInfoFlags)(0), // orr
        (insInfoFlags)(0), // fmov
        (insInfoFlags)(0), // sqdmulh
        (insInfoFlags)(0), // sqrdmulh
        (insInfoFlags)(0), // sqrdmlah
        (insInfoFlags)(0), // sqrdmlsh
        (insInfoFlags)(0), // mla
        (insInfoFlags)(0), // mls
        (insInfoFlags)(0), // fmlalb
        (insInfoFlags)(0), // fmlalt
        (insInfoFlags)(0), // index
        (insInfoFlags)(0), // cpy
        (insInfoFlags)(LD), // ldnt1b
        (insInfoFlags)(LD), // ldnt1h
        (insInfoFlags)(LD), // ldnt1w
        (insInfoFlags)(ST), // stnt1b
        (insInfoFlags)(ST), // stnt1h
        (insInfoFlags)(ST), // stnt1w
        (insInfoFlags)(0), // add
        (insInfoFlags)(0), // sub
        (insInfoFlags)(0), // adr
        (insInfoFlags)(0), // dup
        (insInfoFlags)(0), // trn1
        (insInfoFlags)(0), // trn2
        (insInfoFlags)(0), // uzp1
        (insInfoFlags)(0), // uzp2
        (insInfoFlags)(0), // zip1
        (insInfoFlags)(0), // zip2
        (insInfoFlags)(0), // sqadd
        (insInfoFlags)(0), // sqsub
        (insInfoFlags)(0), // uqadd
        (insInfoFlags)(0), // uqsub
        (insInfoFlags)(0), // fmla
        (insInfoFlags)(0), // fmls
        (insInfoFlags)(0), // luti4
        (insInfoFlags)(0), // fadd
        (insInfoFlags)(0), // fsub
        (insInfoFlags)(0), // clasta
        (insInfoFlags)(0), // clastb
        (insInfoFlags)(CMP), // cmpeq
        (insInfoFlags)(CMP), // cmpge
        (insInfoFlags)(CMP), // cmpgt
        (insInfoFlags)(CMP), // cmple
        (insInfoFlags)(CMP), // cmplt
        (insInfoFlags)(CMP), // cmpne
        (insInfoFlags)(CMP), // cmphi
        (insInfoFlags)(CMP), // cmphs
        (insInfoFlags)(CMP), // cmplo
        (insInfoFlags)(CMP), // cmpls
        (insInfoFlags)(0), // whilege
        (insInfoFlags)(0), // whilegt
        (insInfoFlags)(0), // whilehi
        (insInfoFlags)(0), // whilehs
        (insInfoFlags)(0), // whilele
        (insInfoFlags)(0), // whilelo
        (insInfoFlags)(0), // whilels
        (insInfoFlags)(0), // whilelt
        (insInfoFlags)(0), // cdot
        (insInfoFlags)(0), // cmla
        (insInfoFlags)(0), // sqrdcmlah
        (insInfoFlags)(0), // smlalb
        (insInfoFlags)(0), // smlalt
        (insInfoFlags)(0), // smlslb
        (insInfoFlags)(0), // smlslt
        (insInfoFlags)(0), // umlalb
        (insInfoFlags)(0), // umlalt
        (insInfoFlags)(0), // umlslb
        (insInfoFlags)(0), // umlslt
        (insInfoFlags)(0), // sqdmlalb
        (insInfoFlags)(0), // sqdmlalt
        (insInfoFlags)(0), // sqdmlslb
        (insInfoFlags)(0), // sqdmlslt
        (insInfoFlags)(0), // smullb
        (insInfoFlags)(0), // smullt
        (insInfoFlags)(0), // umullb
        (insInfoFlags)(0), // umullt
        (insInfoFlags)(0), // sqdmullb
        (insInfoFlags)(0), // sqdmullt
        (insInfoFlags)(0), // bfmul
        (insInfoFlags)(LD), // ldnt1d
        (insInfoFlags)(ST), // stnt1d
        (insInfoFlags)(LD), // ldr
        (insInfoFlags)(ST), // str
        (insInfoFlags)(0), // smax
        (insInfoFlags)(0), // smin
        (insInfoFlags)(0), // umax
        (insInfoFlags)(0), // umin
        (insInfoFlags)(0), // addpt
        (insInfoFlags)(0), // subpt
        (insInfoFlags)(0), // rev
        (insInfoFlags)(0), // smulh
        (insInfoFlags)(0), // umulh
        (insInfoFlags)(0), // orn
        (insInfoFlags)(0), // ext
        (insInfoFlags)(0), // sqshl
        (insInfoFlags)(0), // uqshl
        (insInfoFlags)(CMP), // fcmeq
        (insInfoFlags)(CMP), // fcmge
        (insInfoFlags)(CMP), // fcmgt
        (insInfoFlags)(CMP), // fcmle
        (insInfoFlags)(CMP), // fcmlt
        (insInfoFlags)(CMP), // fcmne
        (insInfoFlags)(0), // tbl
        (insInfoFlags)(0), // luti2
        (insInfoFlags)(0), // fmax
        (insInfoFlags)(0), // fmaxnm
        (insInfoFlags)(0), // fmin
        (insInfoFlags)(0), // fminnm
        (insInfoFlags)(0), // fsubr
        (insInfoFlags)(0), // usdot
        (insInfoFlags)(0), // fcmla
        (insInfoFlags)(0), // bfdot
        (insInfoFlags)(0), // fmlallbb
        (insInfoFlags)(0), // fmlallbt
        (insInfoFlags)(0), // fmlalltb
        (insInfoFlags)(0), // fmlalltt
        (insInfoFlags)(0), // not
        (insInfoFlags)(0), // subr
        (insInfoFlags)(0), // movprfx
        (insInfoFlags)(0), // decd
        (insInfoFlags)(0), // dech
        (insInfoFlags)(0), // decw
        (insInfoFlags)(0), // incd
        (insInfoFlags)(0), // inch
        (insInfoFlags)(0), // incw
        (insInfoFlags)(0), // sqdecd
        (insInfoFlags)(0), // sqdech
        (insInfoFlags)(0), // sqdecw
        (insInfoFlags)(0), // sqincd
        (insInfoFlags)(0), // sqinch
        (insInfoFlags)(0), // sqincw
        (insInfoFlags)(0), // uqdecd
        (insInfoFlags)(0), // uqdech
        (insInfoFlags)(0), // uqdecw
        (insInfoFlags)(0), // uqincd
        (insInfoFlags)(0), // uqinch
        (insInfoFlags)(0), // uqincw
        (insInfoFlags)(0), // insr
        (insInfoFlags)(0), // lasta
        (insInfoFlags)(0), // lastb
        (insInfoFlags)(0), // splice
        (insInfoFlags)(0), // sel
        (insInfoFlags)(0), // movs
        (insInfoFlags)(0), // ptrue
        (insInfoFlags)(0), // rdffr
        (insInfoFlags)(0), // cntp
        (insInfoFlags)(0), // decp
        (insInfoFlags)(0), // incp
        (insInfoFlags)(0), // sqdecp
        (insInfoFlags)(0), // sqincp
        (insInfoFlags)(0), // uqdecp
        (insInfoFlags)(0), // uqincp
        (insInfoFlags)(0), // pext
        (insInfoFlags)(0), // pmullb
        (insInfoFlags)(0), // pmullt
        (insInfoFlags)(0), // fcvtnt
        (insInfoFlags)(0), // bfmla
        (insInfoFlags)(0), // bfmls
        (insInfoFlags)(0), // bfmlalb
        (insInfoFlags)(0), // bfmlalt
        (insInfoFlags)(0), // bfmlslb
        (insInfoFlags)(0), // bfmlslt
        (insInfoFlags)(0), // fmlslb
        (insInfoFlags)(0), // fmlslt
        (insInfoFlags)(0), // bfadd
        (insInfoFlags)(0), // bfsub
        (insInfoFlags)(LD), // ldnt1sb
        (insInfoFlags)(LD), // ldnt1sh
        (insInfoFlags)(LD), // ld1rob
        (insInfoFlags)(LD), // ld1rod
        (insInfoFlags)(LD), // ld1roh
        (insInfoFlags)(LD), // ld1row
        (insInfoFlags)(LD), // ld1rqb
        (insInfoFlags)(LD), // ld1rqd
        (insInfoFlags)(LD), // ld1rqh
        (insInfoFlags)(LD), // ld1rqw
        (insInfoFlags)(LD), // ld2q
        (insInfoFlags)(LD), // ld3q
        (insInfoFlags)(LD), // ld4q
        (insInfoFlags)(LD), // ld2b
        (insInfoFlags)(LD), // ld2d
        (insInfoFlags)(LD), // ld2h
        (insInfoFlags)(LD), // ld2w
        (insInfoFlags)(LD), // ld3b
        (insInfoFlags)(LD), // ld3d
        (insInfoFlags)(LD), // ld3h
        (insInfoFlags)(LD), // ld3w
        (insInfoFlags)(LD), // ld4b
        (insInfoFlags)(LD), // ld4d
        (insInfoFlags)(LD), // ld4h
        (insInfoFlags)(LD), // ld4w
        (insInfoFlags)(ST), // st2b
        (insInfoFlags)(ST), // st2d
        (insInfoFlags)(ST), // st2h
        (insInfoFlags)(ST), // st2w
        (insInfoFlags)(ST), // st3b
        (insInfoFlags)(ST), // st3d
        (insInfoFlags)(ST), // st3h
        (insInfoFlags)(ST), // st3w
        (insInfoFlags)(ST), // st4b
        (insInfoFlags)(ST), // st4d
        (insInfoFlags)(ST), // st4h
        (insInfoFlags)(ST), // st4w
        (insInfoFlags)(ST), // st2q
        (insInfoFlags)(ST), // st3q
        (insInfoFlags)(ST), // st4q
        (insInfoFlags)(0), // abs
        (insInfoFlags)(0), // neg
        (insInfoFlags)(0), // sxtb
        (insInfoFlags)(0), // sxth
        (insInfoFlags)(0), // sxtw
        (insInfoFlags)(0), // uxtb
        (insInfoFlags)(0), // uxth
        (insInfoFlags)(0), // uxtw
        (insInfoFlags)(0), // ands
        (insInfoFlags)(0), // bics
        (insInfoFlags)(0), // eors
        (insInfoFlags)(0), // nand
        (insInfoFlags)(0), // nands
        (insInfoFlags)(0), // nor
        (insInfoFlags)(0), // nors
        (insInfoFlags)(0), // nots
        (insInfoFlags)(0), // orns
        (insInfoFlags)(0), // orrs
        (insInfoFlags)(0), // rbit
        (insInfoFlags)(0), // revb
        (insInfoFlags)(0), // revh
        (insInfoFlags)(0), // revw
        (insInfoFlags)(0), // cls
        (insInfoFlags)(0), // clz
        (insInfoFlags)(0), // cnot
        (insInfoFlags)(0), // cnt
        (insInfoFlags)(0), // fabs
        (insInfoFlags)(0), // fneg
        (insInfoFlags)(0), // sdiv
        (insInfoFlags)(0), // sdivr
        (insInfoFlags)(0), // udiv
        (insInfoFlags)(0), // udivr
        (insInfoFlags)(0), // eon
        (insInfoFlags)(0), // smaxv
        (insInfoFlags)(0), // sminv
        (insInfoFlags)(0), // umaxv
        (insInfoFlags)(0), // uminv
        (insInfoFlags)(0), // faddv
        (insInfoFlags)(0), // fmaxnmv
        (insInfoFlags)(0), // fmaxv
        (insInfoFlags)(0), // fminnmv
        (insInfoFlags)(0), // fminv
        (insInfoFlags)(0), // addp
        (insInfoFlags)(0), // smaxp
        (insInfoFlags)(0), // sminp
        (insInfoFlags)(0), // umaxp
        (insInfoFlags)(0), // uminp
        (insInfoFlags)(0), // faddp
        (insInfoFlags)(0), // fmaxnmp
        (insInfoFlags)(0), // fmaxp
        (insInfoFlags)(0), // fminnmp
        (insInfoFlags)(0), // fminp
        (insInfoFlags)(RSH), // srsra
        (insInfoFlags)(RSH), // ssra
        (insInfoFlags)(RSH), // ursra
        (insInfoFlags)(RSH), // usra
        (insInfoFlags)(RSH), // asrd
        (insInfoFlags)(0), // sqshlu
        (insInfoFlags)(RSH), // srshr
        (insInfoFlags)(RSH), // urshr
        (insInfoFlags)(RSH), // sqrshrn
        (insInfoFlags)(RSH), // sqrshrun
        (insInfoFlags)(RSH), // uqrshrn
        (insInfoFlags)(0), // sli
        (insInfoFlags)(RSH), // sri
        (insInfoFlags)(0), // sqrshl
        (insInfoFlags)(0), // sqrshlr
        (insInfoFlags)(0), // sqshlr
        (insInfoFlags)(0), // srshl
        (insInfoFlags)(0), // srshlr
        (insInfoFlags)(0), // uqrshl
        (insInfoFlags)(0), // uqrshlr
        (insInfoFlags)(0), // uqshlr
        (insInfoFlags)(0), // urshl
        (insInfoFlags)(0), // urshlr
        (insInfoFlags)(0), // fabd
        (insInfoFlags)(0), // famax
        (insInfoFlags)(0), // famin
        (insInfoFlags)(0), // fdiv
        (insInfoFlags)(0), // fdivr
        (insInfoFlags)(0), // fmulx
        (insInfoFlags)(0), // fscale
        (insInfoFlags)(0), // frecps
        (insInfoFlags)(0), // frsqrts
        (insInfoFlags)(0), // ftsmul
        (insInfoFlags)(CMP), // facge
        (insInfoFlags)(CMP), // facgt
        (insInfoFlags)(CMP), // facle
        (insInfoFlags)(CMP), // faclt
        (insInfoFlags)(CMP), // fcmuo
        (insInfoFlags)(0), // sqsubr
        (insInfoFlags)(0), // suqadd
        (insInfoFlags)(0), // uqsubr
        (insInfoFlags)(0), // usqadd
        (insInfoFlags)(0), // sqabs
        (insInfoFlags)(0), // sqneg
        (insInfoFlags)(0), // urecpe
        (insInfoFlags)(0), // ursqrte
        (insInfoFlags)(0), // frecpe
        (insInfoFlags)(0), // frsqrte
        (insInfoFlags)(0), // frecpx
        (insInfoFlags)(0), // fsqrt
        (insInfoFlags)(0), // tbx
        (insInfoFlags)(0), // shadd
        (insInfoFlags)(0), // shsub
        (insInfoFlags)(0), // shsubr
        (insInfoFlags)(0), // srhadd
        (insInfoFlags)(0), // uhadd
        (insInfoFlags)(0), // uhsub
        (insInfoFlags)(0), // uhsubr
        (insInfoFlags)(0), // urhadd
        (insInfoFlags)(0), // sabd
        (insInfoFlags)(0), // uabd
        (insInfoFlags)(0), // saba
        (insInfoFlags)(0), // uaba
        (insInfoFlags)(0), // pmul
        (insInfoFlags)(0), // bcax
        (insInfoFlags)(0), // bsl
        (insInfoFlags)(0), // bsl1n
        (insInfoFlags)(0), // bsl2n
        (insInfoFlags)(0), // eor3
        (insInfoFlags)(0), // nbsl
        (insInfoFlags)(0), // bfcvtn
        (insInfoFlags)(NRW), // fcvtn
        (insInfoFlags)(0), // fcvtnb
        (insInfoFlags)(0), // fcadd
        (insInfoFlags)(0), // smmla
        (insInfoFlags)(0), // ummla
        (insInfoFlags)(0), // usmmla
        (insInfoFlags)(0), // bfmmla
        (insInfoFlags)(LNG), // sadalp
        (insInfoFlags)(LNG), // uadalp
        (insInfoFlags)(0), // frinta
        (insInfoFlags)(0), // frinti
        (insInfoFlags)(0), // frintm
        (insInfoFlags)(0), // frintn
        (insInfoFlags)(0), // frintp
        (insInfoFlags)(0), // frintx
        (insInfoFlags)(0), // frintz
        (insInfoFlags)(0), // sudot
        (insInfoFlags)(0), // aesd
        (insInfoFlags)(0), // aese
        (insInfoFlags)(0), // sm4e
        (insInfoFlags)(0), // aesimc
        (insInfoFlags)(0), // aesmc
        (insInfoFlags)(0), // rax1
        (insInfoFlags)(0), // sm4ekey
        (insInfoFlags)(0), // xar
        (insInfoFlags)(0), // andv
        (insInfoFlags)(0), // eorv
        (insInfoFlags)(0), // orv
        (insInfoFlags)(0), // andqv
        (insInfoFlags)(0), // eorqv
        (insInfoFlags)(0), // orqv
        (insInfoFlags)(0), // saddv
        (insInfoFlags)(0), // uaddv
        (insInfoFlags)(0), // addqv
        (insInfoFlags)(0), // smaxqv
        (insInfoFlags)(0), // sminqv
        (insInfoFlags)(0), // umaxqv
        (insInfoFlags)(0), // uminqv
        (insInfoFlags)(RSH), // asrr
        (insInfoFlags)(RSH), // lslr
        (insInfoFlags)(RSH), // lsrr
        (insInfoFlags)(0), // mad
        (insInfoFlags)(0), // msb
        (insInfoFlags)(0), // addpl
        (insInfoFlags)(0), // addvl
        (insInfoFlags)(0), // rdvl
        (insInfoFlags)(0), // fexpa
        (insInfoFlags)(0), // ftssel
        (insInfoFlags)(0), // cntb
        (insInfoFlags)(0), // cntd
        (insInfoFlags)(0), // cnth
        (insInfoFlags)(0), // cntw
        (insInfoFlags)(0), // decb
        (insInfoFlags)(0), // incb
        (insInfoFlags)(0), // sqdecb
        (insInfoFlags)(0), // sqincb
        (insInfoFlags)(0), // uqdecb
        (insInfoFlags)(0), // uqincb
        (insInfoFlags)(0), // dupm
        (insInfoFlags)(0), // fcpy
        (insInfoFlags)(0), // dupq
        (insInfoFlags)(0), // extq
        (insInfoFlags)(0), // tbxq
        (insInfoFlags)(0), // sunpkhi
        (insInfoFlags)(0), // sunpklo
        (insInfoFlags)(0), // uunpkhi
        (insInfoFlags)(0), // uunpklo
        (insInfoFlags)(0), // punpkhi
        (insInfoFlags)(0), // punpklo
        (insInfoFlags)(0), // compact
        (insInfoFlags)(0), // revd
        (insInfoFlags)(0), // brkpa
        (insInfoFlags)(0), // brkpas
        (insInfoFlags)(0), // brkpb
        (insInfoFlags)(0), // brkpbs
        (insInfoFlags)(0), // brka
        (insInfoFlags)(0), // brkb
        (insInfoFlags)(0), // brkas
        (insInfoFlags)(0), // brkbs
        (insInfoFlags)(0), // brkn
        (insInfoFlags)(0), // brkns
        (insInfoFlags)(0), // pfirst
        (insInfoFlags)(0), // ptrues
        (insInfoFlags)(0), // pnext
        (insInfoFlags)(0), // rdffrs
        (insInfoFlags)(0), // ptest
        (insInfoFlags)(0), // pfalse
        (insInfoFlags)(0), // setffr
        (insInfoFlags)(0), // wrffr
        (insInfoFlags)(0), // ctermeq
        (insInfoFlags)(0), // ctermne
        (insInfoFlags)(0), // whilerw
        (insInfoFlags)(0), // whilewr
        (insInfoFlags)(0), // psel
        (insInfoFlags)(0), // fdup
        (insInfoFlags)(0), // sqdmlalbt
        (insInfoFlags)(0), // sqdmlslbt
        (insInfoFlags)(0), // sclamp
        (insInfoFlags)(0), // uclamp
        (insInfoFlags)(0), // mlapt
        (insInfoFlags)(0), // madpt
        (insInfoFlags)(0), // tblq
        (insInfoFlags)(0), // uzpq1
        (insInfoFlags)(0), // uzpq2
        (insInfoFlags)(0), // zipq1
        (insInfoFlags)(0), // zipq2
        (insInfoFlags)(0), // sabdlb
        (insInfoFlags)(0), // sabdlt
        (insInfoFlags)(0), // saddlb
        (insInfoFlags)(0), // saddlt
        (insInfoFlags)(0), // ssublb
        (insInfoFlags)(0), // ssublt
        (insInfoFlags)(0), // uabdlb
        (insInfoFlags)(0), // uabdlt
        (insInfoFlags)(0), // uaddlb
        (insInfoFlags)(0), // uaddlt
        (insInfoFlags)(0), // usublb
        (insInfoFlags)(0), // usublt
        (insInfoFlags)(0), // saddwb
        (insInfoFlags)(0), // saddwt
        (insInfoFlags)(0), // ssubwb
        (insInfoFlags)(0), // ssubwt
        (insInfoFlags)(0), // uaddwb
        (insInfoFlags)(0), // uaddwt
        (insInfoFlags)(0), // usubwb
        (insInfoFlags)(0), // usubwt
        (insInfoFlags)(0), // eorbt
        (insInfoFlags)(0), // eortb
        (insInfoFlags)(0), // bdep
        (insInfoFlags)(0), // bext
        (insInfoFlags)(0), // bgrp
        (insInfoFlags)(0), // sshllb
        (insInfoFlags)(0), // sshllt
        (insInfoFlags)(0), // ushllb
        (insInfoFlags)(0), // ushllt
        (insInfoFlags)(0), // saddlbt
        (insInfoFlags)(0), // ssublbt
        (insInfoFlags)(0), // ssubltb
        (insInfoFlags)(0), // cadd
        (insInfoFlags)(0), // sqcadd
        (insInfoFlags)(0), // sabalb
        (insInfoFlags)(0), // sabalt
        (insInfoFlags)(0), // uabalb
        (insInfoFlags)(0), // uabalt
        (insInfoFlags)(0), // adclb
        (insInfoFlags)(0), // adclt
        (insInfoFlags)(0), // sbclb
        (insInfoFlags)(0), // sbclt
        (insInfoFlags)(0), // sqcvtn
        (insInfoFlags)(0), // sqcvtun
        (insInfoFlags)(0), // uqcvtn
        (insInfoFlags)(0), // rshrnb
        (insInfoFlags)(0), // rshrnt
        (insInfoFlags)(0), // shrnb
        (insInfoFlags)(0), // shrnt
        (insInfoFlags)(0), // sqrshrnb
        (insInfoFlags)(0), // sqrshrnt
        (insInfoFlags)(0), // sqrshrunb
        (insInfoFlags)(0), // sqrshrunt
        (insInfoFlags)(0), // sqshrnb
        (insInfoFlags)(0), // sqshrnt
        (insInfoFlags)(0), // sqshrunb
        (insInfoFlags)(0), // sqshrunt
        (insInfoFlags)(0), // uqrshrnb
        (insInfoFlags)(0), // uqrshrnt
        (insInfoFlags)(0), // uqshrnb
        (insInfoFlags)(0), // uqshrnt
        (insInfoFlags)(0), // addhnb
        (insInfoFlags)(0), // addhnt
        (insInfoFlags)(0), // raddhnb
        (insInfoFlags)(0), // raddhnt
        (insInfoFlags)(0), // rsubhnb
        (insInfoFlags)(0), // rsubhnt
        (insInfoFlags)(0), // subhnb
        (insInfoFlags)(0), // subhnt
        (insInfoFlags)(0), // sqxtnb
        (insInfoFlags)(0), // sqxtnt
        (insInfoFlags)(0), // sqxtunb
        (insInfoFlags)(0), // sqxtunt
        (insInfoFlags)(0), // uqxtnb
        (insInfoFlags)(0), // uqxtnt
        (insInfoFlags)(0), // match
        (insInfoFlags)(0), // nmatch
        (insInfoFlags)(0), // histseg
        (insInfoFlags)(0), // histcnt
        (insInfoFlags)(0), // bfcvtnt
        (insInfoFlags)(0), // fcvtlt
        (insInfoFlags)(0), // fcvtxnt
        (insInfoFlags)(0), // faddqv
        (insInfoFlags)(0), // fmaxnmqv
        (insInfoFlags)(0), // fmaxqv
        (insInfoFlags)(0), // fminnmqv
        (insInfoFlags)(0), // fminqv
        (insInfoFlags)(0), // fclamp
        (insInfoFlags)(0), // bfclamp
        (insInfoFlags)(0), // fmmla
        (insInfoFlags)(0), // bf1cvt
        (insInfoFlags)(0), // bf1cvtlt
        (insInfoFlags)(0), // bf2cvt
        (insInfoFlags)(0), // bf2cvtlt
        (insInfoFlags)(0), // f1cvt
        (insInfoFlags)(0), // f1cvtlt
        (insInfoFlags)(0), // f2cvt
        (insInfoFlags)(0), // f2cvtlt
        (insInfoFlags)(0), // fadda
        (insInfoFlags)(0), // bfmax
        (insInfoFlags)(0), // bfmaxnm
        (insInfoFlags)(0), // bfmin
        (insInfoFlags)(0), // bfminnm
        (insInfoFlags)(0), // ftmad
        (insInfoFlags)(0), // bfcvt
        (insInfoFlags)(0), // fcvt
        (insInfoFlags)(0), // fcvtx
        (insInfoFlags)(0), // flogb
        (insInfoFlags)(0), // fcvtzs
        (insInfoFlags)(0), // fcvtzu
        (insInfoFlags)(0), // scvtf
        (insInfoFlags)(0), // ucvtf
        (insInfoFlags)(0), // fnmla
        (insInfoFlags)(0), // fnmls
        (insInfoFlags)(0), // fmad
        (insInfoFlags)(0), // fmsb
        (insInfoFlags)(0), // fnmad
        (insInfoFlags)(0), // fnmsb
        (insInfoFlags)(LD), // ld1rb
        (insInfoFlags)(LD), // ld1rd
        (insInfoFlags)(LD), // ld1rsw
        (insInfoFlags)(LD), // ld1rh
        (insInfoFlags)(LD), // ld1rsb
        (insInfoFlags)(LD), // ld1rsh
        (insInfoFlags)(LD), // ld1rw
        (insInfoFlags)(LD), // ldnf1b
        (insInfoFlags)(LD), // ldnf1d
        (insInfoFlags)(LD), // ldnf1sw
        (insInfoFlags)(LD), // ldnf1h
        (insInfoFlags)(LD), // ldnf1sb
        (insInfoFlags)(LD), // ldnf1sh
        (insInfoFlags)(LD), // ldnf1w
        (insInfoFlags)(LD), // ld1q
        (insInfoFlags)(LD), // ldnt1sw
        (insInfoFlags)(ST), // st1q
        (insInfoFlags)0, // INS_lea is zero-initialized in native instInfo[INS_count].
    ];

    private static ReadOnlySpan<insInfoFlags> s_instInfoLOONGARCH64 => [
        (insInfoFlags)(0), // INS_invalid
        (insInfoFlags)(0), // INS_nop
        (insInfoFlags)(0), // INS_bceqz
        (insInfoFlags)(0), // INS_bcnez
        (insInfoFlags)(0), // INS_beq
        (insInfoFlags)(0), // INS_bne
        (insInfoFlags)(0), // INS_blt
        (insInfoFlags)(0), // INS_bge
        (insInfoFlags)(0), // INS_bltu
        (insInfoFlags)(0), // INS_bgeu
        (insInfoFlags)(0), // INS_beqz
        (insInfoFlags)(0), // INS_bnez
        (insInfoFlags)(0), // INS_b
        (insInfoFlags)(0), // INS_bl
        (insInfoFlags)(0), // INS_mov
        (insInfoFlags)(0), // INS_dneg
        (insInfoFlags)(0), // INS_neg
        (insInfoFlags)(0), // INS_not
        (insInfoFlags)(0), // INS_add_w
        (insInfoFlags)(0), // INS_add_d
        (insInfoFlags)(0), // INS_sub_w
        (insInfoFlags)(0), // INS_sub_d
        (insInfoFlags)(0), // INS_and
        (insInfoFlags)(0), // INS_or
        (insInfoFlags)(0), // INS_nor
        (insInfoFlags)(0), // INS_xor
        (insInfoFlags)(0), // INS_andn
        (insInfoFlags)(0), // INS_orn
        (insInfoFlags)(0), // INS_mul_w
        (insInfoFlags)(0), // INS_mul_d
        (insInfoFlags)(0), // INS_mulh_w
        (insInfoFlags)(0), // INS_mulh_wu
        (insInfoFlags)(0), // INS_mulh_d
        (insInfoFlags)(0), // INS_mulh_du
        (insInfoFlags)(0), // INS_mulw_d_w
        (insInfoFlags)(0), // INS_mulw_d_wu
        (insInfoFlags)(0), // INS_div_w
        (insInfoFlags)(0), // INS_mod_w
        (insInfoFlags)(0), // INS_div_wu
        (insInfoFlags)(0), // INS_mod_wu
        (insInfoFlags)(0), // INS_div_d
        (insInfoFlags)(0), // INS_mod_d
        (insInfoFlags)(0), // INS_div_du
        (insInfoFlags)(0), // INS_mod_du
        (insInfoFlags)(0), // INS_sll_w
        (insInfoFlags)(0), // INS_srl_w
        (insInfoFlags)(0), // INS_sra_w
        (insInfoFlags)(0), // INS_rotr_w
        (insInfoFlags)(0), // INS_sll_d
        (insInfoFlags)(0), // INS_srl_d
        (insInfoFlags)(0), // INS_sra_d
        (insInfoFlags)(0), // INS_rotr_d
        (insInfoFlags)(0), // INS_maskeqz
        (insInfoFlags)(0), // INS_masknez
        (insInfoFlags)(0), // INS_slt
        (insInfoFlags)(0), // INS_sltu
        (insInfoFlags)(0), // INS_crc_w_b_w
        (insInfoFlags)(0), // INS_crc_w_h_w
        (insInfoFlags)(0), // INS_crc_w_w_w
        (insInfoFlags)(0), // INS_crc_w_d_w
        (insInfoFlags)(0), // INS_crcc_w_b_w
        (insInfoFlags)(0), // INS_crcc_w_h_w
        (insInfoFlags)(0), // INS_crcc_w_w_w
        (insInfoFlags)(0), // INS_crcc_w_d_w
        (insInfoFlags)(0), // INS_amcas_b
        (insInfoFlags)(0), // INS_amcas_h
        (insInfoFlags)(0), // INS_amcas_w
        (insInfoFlags)(0), // INS_amcas_d
        (insInfoFlags)(LD|ST), // INS_amswap_b
        (insInfoFlags)(LD|ST), // INS_amswap_h
        (insInfoFlags)(LD|ST), // INS_amswap_w
        (insInfoFlags)(LD|ST), // INS_amswap_d
        (insInfoFlags)(LD|ST), // INS_amadd_b
        (insInfoFlags)(LD|ST), // INS_amadd_h
        (insInfoFlags)(LD|ST), // INS_amadd_w
        (insInfoFlags)(LD|ST), // INS_amadd_d
        (insInfoFlags)(LD|ST), // INS_amand_w
        (insInfoFlags)(LD|ST), // INS_amand_d
        (insInfoFlags)(LD|ST), // INS_amor_w
        (insInfoFlags)(LD|ST), // INS_amor_d
        (insInfoFlags)(LD|ST), // INS_amxor_w
        (insInfoFlags)(LD|ST), // INS_amxor_d
        (insInfoFlags)(LD|ST), // INS_ammax_w
        (insInfoFlags)(LD|ST), // INS_ammax_d
        (insInfoFlags)(LD|ST), // INS_ammin_w
        (insInfoFlags)(LD|ST), // INS_ammin_d
        (insInfoFlags)(LD|ST), // INS_ammax_wu
        (insInfoFlags)(LD|ST), // INS_ammax_du
        (insInfoFlags)(LD|ST), // INS_ammin_wu
        (insInfoFlags)(LD|ST), // INS_ammin_du
        (insInfoFlags)(0), // INS_amcas_db_b
        (insInfoFlags)(0), // INS_amcas_db_h
        (insInfoFlags)(0), // INS_amcas_db_w
        (insInfoFlags)(0), // INS_amcas_db_d
        (insInfoFlags)(LD|ST), // INS_amswap_db_b
        (insInfoFlags)(LD|ST), // INS_amswap_db_h
        (insInfoFlags)(LD|ST), // INS_amswap_db_w
        (insInfoFlags)(LD|ST), // INS_amswap_db_d
        (insInfoFlags)(LD|ST), // INS_amadd_db_b
        (insInfoFlags)(LD|ST), // INS_amadd_db_h
        (insInfoFlags)(LD|ST), // INS_amadd_db_w
        (insInfoFlags)(LD|ST), // INS_amadd_db_d
        (insInfoFlags)(LD|ST), // INS_amand_db_w
        (insInfoFlags)(LD|ST), // INS_amand_db_d
        (insInfoFlags)(LD|ST), // INS_amor_db_w
        (insInfoFlags)(LD|ST), // INS_amor_db_d
        (insInfoFlags)(LD|ST), // INS_amxor_db_w
        (insInfoFlags)(LD|ST), // INS_amxor_db_d
        (insInfoFlags)(LD|ST), // INS_ammax_db_w
        (insInfoFlags)(LD|ST), // INS_ammax_db_d
        (insInfoFlags)(LD|ST), // INS_ammin_db_w
        (insInfoFlags)(LD|ST), // INS_ammin_db_d
        (insInfoFlags)(LD|ST), // INS_ammax_db_wu
        (insInfoFlags)(LD|ST), // INS_ammax_db_du
        (insInfoFlags)(LD|ST), // INS_ammin_db_wu
        (insInfoFlags)(LD|ST), // INS_ammin_db_du
        (insInfoFlags)(0), // INS_alsl_w
        (insInfoFlags)(0), // INS_alsl_wu
        (insInfoFlags)(0), // INS_alsl_d
        (insInfoFlags)(0), // INS_bytepick_w
        (insInfoFlags)(0), // INS_bytepick_d
        (insInfoFlags)(0), // INS_fsel
        (insInfoFlags)(0), // INS_lu12i_w
        (insInfoFlags)(0), // INS_lu32i_d
        (insInfoFlags)(0), // INS_pcaddi
        (insInfoFlags)(0), // INS_pcaddu12i
        (insInfoFlags)(0), // INS_pcalau12i
        (insInfoFlags)(0), // INS_pcaddu18i
        (insInfoFlags)(0), // INS_ext_w_b
        (insInfoFlags)(0), // INS_ext_w_h
        (insInfoFlags)(0), // INS_clo_w
        (insInfoFlags)(0), // INS_clz_w
        (insInfoFlags)(0), // INS_cto_w
        (insInfoFlags)(0), // INS_ctz_w
        (insInfoFlags)(0), // INS_clo_d
        (insInfoFlags)(0), // INS_clz_d
        (insInfoFlags)(0), // INS_cto_d
        (insInfoFlags)(0), // INS_ctz_d
        (insInfoFlags)(0), // INS_revb_2h
        (insInfoFlags)(0), // INS_revb_4h
        (insInfoFlags)(0), // INS_revb_2w
        (insInfoFlags)(0), // INS_revb_d
        (insInfoFlags)(0), // INS_revh_2w
        (insInfoFlags)(0), // INS_revh_d
        (insInfoFlags)(0), // INS_bitrev_4b
        (insInfoFlags)(0), // INS_bitrev_8b
        (insInfoFlags)(0), // INS_bitrev_w
        (insInfoFlags)(0), // INS_bitrev_d
        (insInfoFlags)(0), // INS_rdtimel_w
        (insInfoFlags)(0), // INS_rdtimeh_w
        (insInfoFlags)(0), // INS_rdtime_d
        (insInfoFlags)(0), // INS_cpucfg
        (insInfoFlags)(0), // INS_movfr2gr_s
        (insInfoFlags)(0), // INS_movfr2gr_d
        (insInfoFlags)(0), // INS_movfrh2gr_s
        (insInfoFlags)(0), // INS_bstrins_w
        (insInfoFlags)(0), // INS_bstrins_d
        (insInfoFlags)(0), // INS_bstrpick_w
        (insInfoFlags)(0), // INS_bstrpick_d
        (insInfoFlags)(LD), // INS_ld_b
        (insInfoFlags)(LD), // INS_ld_h
        (insInfoFlags)(LD), // INS_ld_w
        (insInfoFlags)(LD), // INS_ld_d
        (insInfoFlags)(LD), // INS_ld_bu
        (insInfoFlags)(LD), // INS_ld_hu
        (insInfoFlags)(LD), // INS_ld_wu
        (insInfoFlags)(LD), // INS_ldptr_w
        (insInfoFlags)(LD), // INS_ldptr_d
        (insInfoFlags)(LD), // INS_ll_w
        (insInfoFlags)(LD), // INS_ll_d
        (insInfoFlags)(LD), // INS_ldx_b
        (insInfoFlags)(LD), // INS_ldx_h
        (insInfoFlags)(LD), // INS_ldx_w
        (insInfoFlags)(LD), // INS_ldx_d
        (insInfoFlags)(LD), // INS_ldx_bu
        (insInfoFlags)(LD), // INS_ldx_hu
        (insInfoFlags)(LD), // INS_ldx_wu
        (insInfoFlags)(LD), // INS_ldgt_b
        (insInfoFlags)(LD), // INS_ldgt_h
        (insInfoFlags)(LD), // INS_ldgt_w
        (insInfoFlags)(LD), // INS_ldgt_d
        (insInfoFlags)(LD), // INS_ldle_b
        (insInfoFlags)(LD), // INS_ldle_h
        (insInfoFlags)(LD), // INS_ldle_w
        (insInfoFlags)(LD), // INS_ldle_d
        (insInfoFlags)(0), // INS_addi_w
        (insInfoFlags)(0), // INS_addi_d
        (insInfoFlags)(0), // INS_lu52i_d
        (insInfoFlags)(0), // INS_slti
        (insInfoFlags)(0), // INS_sltui
        (insInfoFlags)(0), // INS_andi
        (insInfoFlags)(0), // INS_ori
        (insInfoFlags)(0), // INS_xori
        (insInfoFlags)(0), // INS_slli_w
        (insInfoFlags)(0), // INS_srli_w
        (insInfoFlags)(0), // INS_srai_w
        (insInfoFlags)(0), // INS_rotri_w
        (insInfoFlags)(0), // INS_slli_d
        (insInfoFlags)(0), // INS_srli_d
        (insInfoFlags)(0), // INS_srai_d
        (insInfoFlags)(0), // INS_rotri_d
        (insInfoFlags)(0), // INS_addu16i_d
        (insInfoFlags)(0), // INS_jirl
        (insInfoFlags)(ST), // INS_st_b
        (insInfoFlags)(ST), // INS_st_h
        (insInfoFlags)(ST), // INS_st_w
        (insInfoFlags)(ST), // INS_st_d
        (insInfoFlags)(ST), // INS_stptr_w
        (insInfoFlags)(ST), // INS_stptr_d
        (insInfoFlags)(ST), // INS_sc_w
        (insInfoFlags)(ST), // INS_sc_d
        (insInfoFlags)(ST), // INS_stx_b
        (insInfoFlags)(ST), // INS_stx_h
        (insInfoFlags)(ST), // INS_stx_w
        (insInfoFlags)(ST), // INS_stx_d
        (insInfoFlags)(ST), // INS_stgt_b
        (insInfoFlags)(ST), // INS_stgt_h
        (insInfoFlags)(ST), // INS_stgt_w
        (insInfoFlags)(ST), // INS_stgt_d
        (insInfoFlags)(ST), // INS_stle_b
        (insInfoFlags)(ST), // INS_stle_h
        (insInfoFlags)(ST), // INS_stle_w
        (insInfoFlags)(ST), // INS_stle_d
        (insInfoFlags)(0), // INS_dbar
        (insInfoFlags)(0), // INS_ibar
        (insInfoFlags)(0), // INS_syscall
        (insInfoFlags)(0), // INS_break
        (insInfoFlags)(LD), // INS_preld
        (insInfoFlags)(LD), // INS_preldx
        (insInfoFlags)(0), // INS_fadd_s
        (insInfoFlags)(0), // INS_fadd_d
        (insInfoFlags)(0), // INS_fsub_s
        (insInfoFlags)(0), // INS_fsub_d
        (insInfoFlags)(0), // INS_fmul_s
        (insInfoFlags)(0), // INS_fmul_d
        (insInfoFlags)(0), // INS_fdiv_s
        (insInfoFlags)(0), // INS_fdiv_d
        (insInfoFlags)(0), // INS_fmax_s
        (insInfoFlags)(0), // INS_fmax_d
        (insInfoFlags)(0), // INS_fmin_s
        (insInfoFlags)(0), // INS_fmin_d
        (insInfoFlags)(0), // INS_fmaxa_s
        (insInfoFlags)(0), // INS_fmaxa_d
        (insInfoFlags)(0), // INS_fmina_s
        (insInfoFlags)(0), // INS_fmina_d
        (insInfoFlags)(0), // INS_fscaleb_s
        (insInfoFlags)(0), // INS_fscaleb_d
        (insInfoFlags)(0), // INS_fcopysign_s
        (insInfoFlags)(0), // INS_fcopysign_d
        (insInfoFlags)(LD), // INS_fldx_s
        (insInfoFlags)(LD), // INS_fldx_d
        (insInfoFlags)(ST), // INS_fstx_s
        (insInfoFlags)(ST), // INS_fstx_d
        (insInfoFlags)(0), // INS_fldgt_s
        (insInfoFlags)(0), // INS_fldgt_d
        (insInfoFlags)(0), // INS_fldle_s
        (insInfoFlags)(0), // INS_fldle_d
        (insInfoFlags)(0), // INS_fstgt_s
        (insInfoFlags)(0), // INS_fstgt_d
        (insInfoFlags)(0), // INS_fstle_s
        (insInfoFlags)(0), // INS_fstle_d
        (insInfoFlags)(0), // INS_fmadd_s
        (insInfoFlags)(0), // INS_fmadd_d
        (insInfoFlags)(0), // INS_fmsub_s
        (insInfoFlags)(0), // INS_fmsub_d
        (insInfoFlags)(0), // INS_fnmadd_s
        (insInfoFlags)(0), // INS_fnmadd_d
        (insInfoFlags)(0), // INS_fnmsub_s
        (insInfoFlags)(0), // INS_fnmsub_d
        (insInfoFlags)(0), // INS_fabs_s
        (insInfoFlags)(0), // INS_fabs_d
        (insInfoFlags)(0), // INS_fneg_s
        (insInfoFlags)(0), // INS_fneg_d
        (insInfoFlags)(0), // INS_fsqrt_s
        (insInfoFlags)(0), // INS_fsqrt_d
        (insInfoFlags)(0), // INS_frsqrt_s
        (insInfoFlags)(0), // INS_frsqrt_d
        (insInfoFlags)(0), // INS_frsqrte_s
        (insInfoFlags)(0), // INS_frsqrte_d
        (insInfoFlags)(0), // INS_frecip_s
        (insInfoFlags)(0), // INS_frecip_d
        (insInfoFlags)(0), // INS_frecipe_s
        (insInfoFlags)(0), // INS_frecipe_d
        (insInfoFlags)(0), // INS_flogb_s
        (insInfoFlags)(0), // INS_flogb_d
        (insInfoFlags)(0), // INS_fclass_s
        (insInfoFlags)(0), // INS_fclass_d
        (insInfoFlags)(0), // INS_fcvt_s_d
        (insInfoFlags)(0), // INS_fcvt_d_s
        (insInfoFlags)(0), // INS_ffint_s_w
        (insInfoFlags)(0), // INS_ffint_s_l
        (insInfoFlags)(0), // INS_ffint_d_w
        (insInfoFlags)(0), // INS_ffint_d_l
        (insInfoFlags)(0), // INS_ftint_w_s
        (insInfoFlags)(0), // INS_ftint_w_d
        (insInfoFlags)(0), // INS_ftint_l_s
        (insInfoFlags)(0), // INS_ftint_l_d
        (insInfoFlags)(0), // INS_ftintrm_w_s
        (insInfoFlags)(0), // INS_ftintrm_w_d
        (insInfoFlags)(0), // INS_ftintrm_l_s
        (insInfoFlags)(0), // INS_ftintrm_l_d
        (insInfoFlags)(0), // INS_ftintrp_w_s
        (insInfoFlags)(0), // INS_ftintrp_w_d
        (insInfoFlags)(0), // INS_ftintrp_l_s
        (insInfoFlags)(0), // INS_ftintrp_l_d
        (insInfoFlags)(0), // INS_ftintrz_w_s
        (insInfoFlags)(0), // INS_ftintrz_w_d
        (insInfoFlags)(0), // INS_ftintrz_l_s
        (insInfoFlags)(0), // INS_ftintrz_l_d
        (insInfoFlags)(0), // INS_ftintrne_w_s
        (insInfoFlags)(0), // INS_ftintrne_w_d
        (insInfoFlags)(0), // INS_ftintrne_l_s
        (insInfoFlags)(0), // INS_ftintrne_l_d
        (insInfoFlags)(0), // INS_frint_s
        (insInfoFlags)(0), // INS_frint_d
        (insInfoFlags)(0), // INS_fmov_s
        (insInfoFlags)(0), // INS_fmov_d
        (insInfoFlags)(0), // INS_movgr2fr_w
        (insInfoFlags)(0), // INS_movgr2fr_d
        (insInfoFlags)(0), // INS_movgr2frh_w
        (insInfoFlags)(0), // INS_movgr2fcsr
        (insInfoFlags)(0), // INS_movfcsr2gr
        (insInfoFlags)(0), // INS_movfr2cf
        (insInfoFlags)(0), // INS_movcf2fr
        (insInfoFlags)(0), // INS_movgr2cf
        (insInfoFlags)(0), // INS_movcf2gr
        (insInfoFlags)(0), // INS_fcmp_caf_s
        (insInfoFlags)(0), // INS_fcmp_cun_s
        (insInfoFlags)(0), // INS_fcmp_ceq_s
        (insInfoFlags)(0), // INS_fcmp_cueq_s
        (insInfoFlags)(0), // INS_fcmp_clt_s
        (insInfoFlags)(0), // INS_fcmp_cult_s
        (insInfoFlags)(0), // INS_fcmp_cle_s
        (insInfoFlags)(0), // INS_fcmp_cule_s
        (insInfoFlags)(0), // INS_fcmp_cne_s
        (insInfoFlags)(0), // INS_fcmp_cor_s
        (insInfoFlags)(0), // INS_fcmp_cune_s
        (insInfoFlags)(0), // INS_fcmp_saf_d
        (insInfoFlags)(0), // INS_fcmp_sun_d
        (insInfoFlags)(0), // INS_fcmp_seq_d
        (insInfoFlags)(0), // INS_fcmp_sueq_d
        (insInfoFlags)(0), // INS_fcmp_slt_d
        (insInfoFlags)(0), // INS_fcmp_sult_d
        (insInfoFlags)(0), // INS_fcmp_sle_d
        (insInfoFlags)(0), // INS_fcmp_sule_d
        (insInfoFlags)(0), // INS_fcmp_sne_d
        (insInfoFlags)(0), // INS_fcmp_sor_d
        (insInfoFlags)(0), // INS_fcmp_sune_d
        (insInfoFlags)(0), // INS_fcmp_caf_d
        (insInfoFlags)(0), // INS_fcmp_cun_d
        (insInfoFlags)(0), // INS_fcmp_ceq_d
        (insInfoFlags)(0), // INS_fcmp_cueq_d
        (insInfoFlags)(0), // INS_fcmp_clt_d
        (insInfoFlags)(0), // INS_fcmp_cult_d
        (insInfoFlags)(0), // INS_fcmp_cle_d
        (insInfoFlags)(0), // INS_fcmp_cule_d
        (insInfoFlags)(0), // INS_fcmp_cne_d
        (insInfoFlags)(0), // INS_fcmp_cor_d
        (insInfoFlags)(0), // INS_fcmp_cune_d
        (insInfoFlags)(0), // INS_fcmp_saf_s
        (insInfoFlags)(0), // INS_fcmp_sun_s
        (insInfoFlags)(0), // INS_fcmp_seq_s
        (insInfoFlags)(0), // INS_fcmp_sueq_s
        (insInfoFlags)(0), // INS_fcmp_slt_s
        (insInfoFlags)(0), // INS_fcmp_sult_s
        (insInfoFlags)(0), // INS_fcmp_sle_s
        (insInfoFlags)(0), // INS_fcmp_sule_s
        (insInfoFlags)(0), // INS_fcmp_sne_s
        (insInfoFlags)(0), // INS_fcmp_sor_s
        (insInfoFlags)(0), // INS_fcmp_sune_s
        (insInfoFlags)(LD), // INS_fld_s
        (insInfoFlags)(LD), // INS_fld_d
        (insInfoFlags)(ST), // INS_fst_s
        (insInfoFlags)(ST), // INS_fst_d
        (insInfoFlags)(0), // INS_vfmadd_s
        (insInfoFlags)(0), // INS_vfmadd_d
        (insInfoFlags)(0), // INS_vfmsub_s
        (insInfoFlags)(0), // INS_vfmsub_d
        (insInfoFlags)(0), // INS_vfnmadd_s
        (insInfoFlags)(0), // INS_vfnmadd_d
        (insInfoFlags)(0), // INS_vfnmsub_s
        (insInfoFlags)(0), // INS_vfnmsub_d
        (insInfoFlags)(0), // INS_vbitsel_v
        (insInfoFlags)(0), // INS_vshuf_b
        (insInfoFlags)(LD), // INS_vldx
        (insInfoFlags)(ST), // INS_vstx
        (insInfoFlags)(0), // INS_vreplve_b
        (insInfoFlags)(0), // INS_vreplve_h
        (insInfoFlags)(0), // INS_vreplve_w
        (insInfoFlags)(0), // INS_vreplve_d
        (insInfoFlags)(0), // INS_vfcmp_caf_s
        (insInfoFlags)(0), // INS_vfcmp_cun_s
        (insInfoFlags)(0), // INS_vfcmp_ceq_s
        (insInfoFlags)(0), // INS_vfcmp_cueq_s
        (insInfoFlags)(0), // INS_vfcmp_clt_s
        (insInfoFlags)(0), // INS_vfcmp_cult_s
        (insInfoFlags)(0), // INS_vfcmp_cle_s
        (insInfoFlags)(0), // INS_vfcmp_cule_s
        (insInfoFlags)(0), // INS_vfcmp_cne_s
        (insInfoFlags)(0), // INS_vfcmp_cor_s
        (insInfoFlags)(0), // INS_vfcmp_cune_s
        (insInfoFlags)(0), // INS_vfcmp_saf_s
        (insInfoFlags)(0), // INS_vfcmp_sun_s
        (insInfoFlags)(0), // INS_vfcmp_seq_s
        (insInfoFlags)(0), // INS_vfcmp_sueq_s
        (insInfoFlags)(0), // INS_vfcmp_slt_s
        (insInfoFlags)(0), // INS_vfcmp_sult_s
        (insInfoFlags)(0), // INS_vfcmp_sle_s
        (insInfoFlags)(0), // INS_vfcmp_sule_s
        (insInfoFlags)(0), // INS_vfcmp_sne_s
        (insInfoFlags)(0), // INS_vfcmp_sor_s
        (insInfoFlags)(0), // INS_vfcmp_sune_s
        (insInfoFlags)(0), // INS_vfcmp_caf_d
        (insInfoFlags)(0), // INS_vfcmp_cun_d
        (insInfoFlags)(0), // INS_vfcmp_ceq_d
        (insInfoFlags)(0), // INS_vfcmp_cueq_d
        (insInfoFlags)(0), // INS_vfcmp_clt_d
        (insInfoFlags)(0), // INS_vfcmp_cult_d
        (insInfoFlags)(0), // INS_vfcmp_cle_d
        (insInfoFlags)(0), // INS_vfcmp_cule_d
        (insInfoFlags)(0), // INS_vfcmp_cne_d
        (insInfoFlags)(0), // INS_vfcmp_cor_d
        (insInfoFlags)(0), // INS_vfcmp_cune_d
        (insInfoFlags)(0), // INS_vfcmp_saf_d
        (insInfoFlags)(0), // INS_vfcmp_sun_d
        (insInfoFlags)(0), // INS_vfcmp_seq_d
        (insInfoFlags)(0), // INS_vfcmp_sueq_d
        (insInfoFlags)(0), // INS_vfcmp_slt_d
        (insInfoFlags)(0), // INS_vfcmp_sult_d
        (insInfoFlags)(0), // INS_vfcmp_sle_d
        (insInfoFlags)(0), // INS_vfcmp_sule_d
        (insInfoFlags)(0), // INS_vfcmp_sne_d
        (insInfoFlags)(0), // INS_vfcmp_sor_d
        (insInfoFlags)(0), // INS_vfcmp_sune_d
        (insInfoFlags)(0), // INS_vseq_b
        (insInfoFlags)(0), // INS_vseq_h
        (insInfoFlags)(0), // INS_vseq_w
        (insInfoFlags)(0), // INS_vseq_d
        (insInfoFlags)(0), // INS_vsle_b
        (insInfoFlags)(0), // INS_vsle_h
        (insInfoFlags)(0), // INS_vsle_w
        (insInfoFlags)(0), // INS_vsle_d
        (insInfoFlags)(0), // INS_vsle_bu
        (insInfoFlags)(0), // INS_vsle_hu
        (insInfoFlags)(0), // INS_vsle_wu
        (insInfoFlags)(0), // INS_vsle_du
        (insInfoFlags)(0), // INS_vslt_b
        (insInfoFlags)(0), // INS_vslt_h
        (insInfoFlags)(0), // INS_vslt_w
        (insInfoFlags)(0), // INS_vslt_d
        (insInfoFlags)(0), // INS_vslt_bu
        (insInfoFlags)(0), // INS_vslt_hu
        (insInfoFlags)(0), // INS_vslt_wu
        (insInfoFlags)(0), // INS_vslt_du
        (insInfoFlags)(0), // INS_vadd_b
        (insInfoFlags)(0), // INS_vadd_h
        (insInfoFlags)(0), // INS_vadd_w
        (insInfoFlags)(0), // INS_vadd_d
        (insInfoFlags)(0), // INS_vsub_b
        (insInfoFlags)(0), // INS_vsub_h
        (insInfoFlags)(0), // INS_vsub_w
        (insInfoFlags)(0), // INS_vsub_d
        (insInfoFlags)(0), // INS_vaddwev_h_b
        (insInfoFlags)(0), // INS_vaddwev_w_h
        (insInfoFlags)(0), // INS_vaddwev_d_w
        (insInfoFlags)(0), // INS_vaddwev_q_d
        (insInfoFlags)(0), // INS_vsubwev_h_b
        (insInfoFlags)(0), // INS_vsubwev_w_h
        (insInfoFlags)(0), // INS_vsubwev_d_w
        (insInfoFlags)(0), // INS_vsubwev_q_d
        (insInfoFlags)(0), // INS_vaddwod_h_b
        (insInfoFlags)(0), // INS_vaddwod_w_h
        (insInfoFlags)(0), // INS_vaddwod_d_w
        (insInfoFlags)(0), // INS_vaddwod_q_d
        (insInfoFlags)(0), // INS_vsubwod_h_b
        (insInfoFlags)(0), // INS_vsubwod_w_h
        (insInfoFlags)(0), // INS_vsubwod_d_w
        (insInfoFlags)(0), // INS_vsubwod_q_d
        (insInfoFlags)(0), // INS_vaddwev_h_bu
        (insInfoFlags)(0), // INS_vaddwev_w_hu
        (insInfoFlags)(0), // INS_vaddwev_d_wu
        (insInfoFlags)(0), // INS_vaddwev_q_du
        (insInfoFlags)(0), // INS_vsubwev_h_bu
        (insInfoFlags)(0), // INS_vsubwev_w_hu
        (insInfoFlags)(0), // INS_vsubwev_d_wu
        (insInfoFlags)(0), // INS_vsubwev_q_du
        (insInfoFlags)(0), // INS_vaddwod_h_bu
        (insInfoFlags)(0), // INS_vaddwod_w_hu
        (insInfoFlags)(0), // INS_vaddwod_d_wu
        (insInfoFlags)(0), // INS_vaddwod_q_du
        (insInfoFlags)(0), // INS_vsubwod_h_bu
        (insInfoFlags)(0), // INS_vsubwod_w_hu
        (insInfoFlags)(0), // INS_vsubwod_d_wu
        (insInfoFlags)(0), // INS_vsubwod_q_du
        (insInfoFlags)(0), // INS_vaddwev_h_bu_b
        (insInfoFlags)(0), // INS_vaddwev_w_hu_h
        (insInfoFlags)(0), // INS_vaddwev_d_wu_w
        (insInfoFlags)(0), // INS_vaddwev_q_du_d
        (insInfoFlags)(0), // INS_vaddwod_h_bu_b
        (insInfoFlags)(0), // INS_vaddwod_w_hu_h
        (insInfoFlags)(0), // INS_vaddwod_d_wu_w
        (insInfoFlags)(0), // INS_vaddwod_q_du_d
        (insInfoFlags)(0), // INS_vsadd_b
        (insInfoFlags)(0), // INS_vsadd_h
        (insInfoFlags)(0), // INS_vsadd_w
        (insInfoFlags)(0), // INS_vsadd_d
        (insInfoFlags)(0), // INS_vssub_b
        (insInfoFlags)(0), // INS_vssub_h
        (insInfoFlags)(0), // INS_vssub_w
        (insInfoFlags)(0), // INS_vssub_d
        (insInfoFlags)(0), // INS_vsadd_bu
        (insInfoFlags)(0), // INS_vsadd_hu
        (insInfoFlags)(0), // INS_vsadd_wu
        (insInfoFlags)(0), // INS_vsadd_du
        (insInfoFlags)(0), // INS_vssub_bu
        (insInfoFlags)(0), // INS_vssub_hu
        (insInfoFlags)(0), // INS_vssub_wu
        (insInfoFlags)(0), // INS_vssub_du
        (insInfoFlags)(0), // INS_vhaddw_h_b
        (insInfoFlags)(0), // INS_vhaddw_w_h
        (insInfoFlags)(0), // INS_vhaddw_d_w
        (insInfoFlags)(0), // INS_vhaddw_q_d
        (insInfoFlags)(0), // INS_vhsubw_h_b
        (insInfoFlags)(0), // INS_vhsubw_w_h
        (insInfoFlags)(0), // INS_vhsubw_d_w
        (insInfoFlags)(0), // INS_vhsubw_q_d
        (insInfoFlags)(0), // INS_vhaddw_hu_bu
        (insInfoFlags)(0), // INS_vhaddw_wu_hu
        (insInfoFlags)(0), // INS_vhaddw_du_wu
        (insInfoFlags)(0), // INS_vhaddw_qu_du
        (insInfoFlags)(0), // INS_vhsubw_hu_bu
        (insInfoFlags)(0), // INS_vhsubw_wu_hu
        (insInfoFlags)(0), // INS_vhsubw_du_wu
        (insInfoFlags)(0), // INS_vhsubw_qu_du
        (insInfoFlags)(0), // INS_vadda_b
        (insInfoFlags)(0), // INS_vadda_h
        (insInfoFlags)(0), // INS_vadda_w
        (insInfoFlags)(0), // INS_vadda_d
        (insInfoFlags)(0), // INS_vabsd_b
        (insInfoFlags)(0), // INS_vabsd_h
        (insInfoFlags)(0), // INS_vabsd_w
        (insInfoFlags)(0), // INS_vabsd_d
        (insInfoFlags)(0), // INS_vabsd_bu
        (insInfoFlags)(0), // INS_vabsd_hu
        (insInfoFlags)(0), // INS_vabsd_wu
        (insInfoFlags)(0), // INS_vabsd_du
        (insInfoFlags)(0), // INS_vavg_b
        (insInfoFlags)(0), // INS_vavg_h
        (insInfoFlags)(0), // INS_vavg_w
        (insInfoFlags)(0), // INS_vavg_d
        (insInfoFlags)(0), // INS_vavg_bu
        (insInfoFlags)(0), // INS_vavg_hu
        (insInfoFlags)(0), // INS_vavg_wu
        (insInfoFlags)(0), // INS_vavg_du
        (insInfoFlags)(0), // INS_vavgr_b
        (insInfoFlags)(0), // INS_vavgr_h
        (insInfoFlags)(0), // INS_vavgr_w
        (insInfoFlags)(0), // INS_vavgr_d
        (insInfoFlags)(0), // INS_vavgr_bu
        (insInfoFlags)(0), // INS_vavgr_hu
        (insInfoFlags)(0), // INS_vavgr_wu
        (insInfoFlags)(0), // INS_vavgr_du
        (insInfoFlags)(0), // INS_vmax_b
        (insInfoFlags)(0), // INS_vmax_h
        (insInfoFlags)(0), // INS_vmax_w
        (insInfoFlags)(0), // INS_vmax_d
        (insInfoFlags)(0), // INS_vmin_b
        (insInfoFlags)(0), // INS_vmin_h
        (insInfoFlags)(0), // INS_vmin_w
        (insInfoFlags)(0), // INS_vmin_d
        (insInfoFlags)(0), // INS_vmax_bu
        (insInfoFlags)(0), // INS_vmax_hu
        (insInfoFlags)(0), // INS_vmax_wu
        (insInfoFlags)(0), // INS_vmax_du
        (insInfoFlags)(0), // INS_vmin_bu
        (insInfoFlags)(0), // INS_vmin_hu
        (insInfoFlags)(0), // INS_vmin_wu
        (insInfoFlags)(0), // INS_vmin_du
        (insInfoFlags)(0), // INS_vmul_b
        (insInfoFlags)(0), // INS_vmul_h
        (insInfoFlags)(0), // INS_vmul_w
        (insInfoFlags)(0), // INS_vmul_d
        (insInfoFlags)(0), // INS_vmuh_b
        (insInfoFlags)(0), // INS_vmuh_h
        (insInfoFlags)(0), // INS_vmuh_w
        (insInfoFlags)(0), // INS_vmuh_d
        (insInfoFlags)(0), // INS_vmuh_bu
        (insInfoFlags)(0), // INS_vmuh_hu
        (insInfoFlags)(0), // INS_vmuh_wu
        (insInfoFlags)(0), // INS_vmuh_du
        (insInfoFlags)(0), // INS_vmulwev_h_b
        (insInfoFlags)(0), // INS_vmulwev_w_h
        (insInfoFlags)(0), // INS_vmulwev_d_w
        (insInfoFlags)(0), // INS_vmulwev_q_d
        (insInfoFlags)(0), // INS_vmulwod_h_b
        (insInfoFlags)(0), // INS_vmulwod_w_h
        (insInfoFlags)(0), // INS_vmulwod_d_w
        (insInfoFlags)(0), // INS_vmulwod_q_d
        (insInfoFlags)(0), // INS_vmulwev_h_bu
        (insInfoFlags)(0), // INS_vmulwev_w_hu
        (insInfoFlags)(0), // INS_vmulwev_d_wu
        (insInfoFlags)(0), // INS_vmulwev_q_du
        (insInfoFlags)(0), // INS_vmulwod_h_bu
        (insInfoFlags)(0), // INS_vmulwod_w_hu
        (insInfoFlags)(0), // INS_vmulwod_d_wu
        (insInfoFlags)(0), // INS_vmulwod_q_du
        (insInfoFlags)(0), // INS_vmulwev_h_bu_b
        (insInfoFlags)(0), // INS_vmulwev_w_hu_h
        (insInfoFlags)(0), // INS_vmulwev_d_wu_w
        (insInfoFlags)(0), // INS_vmulwev_q_du_d
        (insInfoFlags)(0), // INS_vmulwod_h_bu_b
        (insInfoFlags)(0), // INS_vmulwod_w_hu_h
        (insInfoFlags)(0), // INS_vmulwod_d_wu_w
        (insInfoFlags)(0), // INS_vmulwod_q_du_d
        (insInfoFlags)(0), // INS_vmadd_b
        (insInfoFlags)(0), // INS_vmadd_h
        (insInfoFlags)(0), // INS_vmadd_w
        (insInfoFlags)(0), // INS_vmadd_d
        (insInfoFlags)(0), // INS_vmsub_b
        (insInfoFlags)(0), // INS_vmsub_h
        (insInfoFlags)(0), // INS_vmsub_w
        (insInfoFlags)(0), // INS_vmsub_d
        (insInfoFlags)(0), // INS_vmaddwev_h_b
        (insInfoFlags)(0), // INS_vmaddwev_w_h
        (insInfoFlags)(0), // INS_vmaddwev_d_w
        (insInfoFlags)(0), // INS_vmaddwev_q_d
        (insInfoFlags)(0), // INS_vmaddwod_h_b
        (insInfoFlags)(0), // INS_vmaddwod_w_h
        (insInfoFlags)(0), // INS_vmaddwod_d_w
        (insInfoFlags)(0), // INS_vmaddwod_q_d
        (insInfoFlags)(0), // INS_vmaddwev_h_bu
        (insInfoFlags)(0), // INS_vmaddwev_w_hu
        (insInfoFlags)(0), // INS_vmaddwev_d_wu
        (insInfoFlags)(0), // INS_vmaddwev_q_du
        (insInfoFlags)(0), // INS_vmaddwod_h_bu
        (insInfoFlags)(0), // INS_vmaddwod_w_hu
        (insInfoFlags)(0), // INS_vmaddwod_d_wu
        (insInfoFlags)(0), // INS_vmaddwod_q_du
        (insInfoFlags)(0), // INS_vmaddwev_h_bu_b
        (insInfoFlags)(0), // INS_vmaddwev_w_hu_h
        (insInfoFlags)(0), // INS_vmaddwev_d_wu_w
        (insInfoFlags)(0), // INS_vmaddwev_q_du_d
        (insInfoFlags)(0), // INS_vmaddwod_h_bu_b
        (insInfoFlags)(0), // INS_vmaddwod_w_hu_h
        (insInfoFlags)(0), // INS_vmaddwod_d_wu_w
        (insInfoFlags)(0), // INS_vmaddwod_q_du_d
        (insInfoFlags)(0), // INS_vdiv_b
        (insInfoFlags)(0), // INS_vdiv_h
        (insInfoFlags)(0), // INS_vdiv_w
        (insInfoFlags)(0), // INS_vdiv_d
        (insInfoFlags)(0), // INS_vmod_b
        (insInfoFlags)(0), // INS_vmod_h
        (insInfoFlags)(0), // INS_vmod_w
        (insInfoFlags)(0), // INS_vmod_d
        (insInfoFlags)(0), // INS_vdiv_bu
        (insInfoFlags)(0), // INS_vdiv_hu
        (insInfoFlags)(0), // INS_vdiv_wu
        (insInfoFlags)(0), // INS_vdiv_du
        (insInfoFlags)(0), // INS_vmod_bu
        (insInfoFlags)(0), // INS_vmod_hu
        (insInfoFlags)(0), // INS_vmod_wu
        (insInfoFlags)(0), // INS_vmod_du
        (insInfoFlags)(0), // INS_vsll_b
        (insInfoFlags)(0), // INS_vsll_h
        (insInfoFlags)(0), // INS_vsll_w
        (insInfoFlags)(0), // INS_vsll_d
        (insInfoFlags)(0), // INS_vsrl_b
        (insInfoFlags)(0), // INS_vsrl_h
        (insInfoFlags)(0), // INS_vsrl_w
        (insInfoFlags)(0), // INS_vsrl_d
        (insInfoFlags)(0), // INS_vsra_b
        (insInfoFlags)(0), // INS_vsra_h
        (insInfoFlags)(0), // INS_vsra_w
        (insInfoFlags)(0), // INS_vsra_d
        (insInfoFlags)(0), // INS_vrotr_b
        (insInfoFlags)(0), // INS_vrotr_h
        (insInfoFlags)(0), // INS_vrotr_w
        (insInfoFlags)(0), // INS_vrotr_d
        (insInfoFlags)(0), // INS_vsrlr_b
        (insInfoFlags)(0), // INS_vsrlr_h
        (insInfoFlags)(0), // INS_vsrlr_w
        (insInfoFlags)(0), // INS_vsrlr_d
        (insInfoFlags)(0), // INS_vsrar_b
        (insInfoFlags)(0), // INS_vsrar_h
        (insInfoFlags)(0), // INS_vsrar_w
        (insInfoFlags)(0), // INS_vsrar_d
        (insInfoFlags)(0), // INS_vsrln_b_h
        (insInfoFlags)(0), // INS_vsrln_h_w
        (insInfoFlags)(0), // INS_vsrln_w_d
        (insInfoFlags)(0), // INS_vsran_b_h
        (insInfoFlags)(0), // INS_vsran_h_w
        (insInfoFlags)(0), // INS_vsran_w_d
        (insInfoFlags)(0), // INS_vsrlrn_b_h
        (insInfoFlags)(0), // INS_vsrlrn_h_w
        (insInfoFlags)(0), // INS_vsrlrn_w_d
        (insInfoFlags)(0), // INS_vsrarn_b_h
        (insInfoFlags)(0), // INS_vsrarn_h_w
        (insInfoFlags)(0), // INS_vsrarn_w_d
        (insInfoFlags)(0), // INS_vssrln_b_h
        (insInfoFlags)(0), // INS_vssrln_h_w
        (insInfoFlags)(0), // INS_vssrln_w_d
        (insInfoFlags)(0), // INS_vssran_b_h
        (insInfoFlags)(0), // INS_vssran_h_w
        (insInfoFlags)(0), // INS_vssran_w_d
        (insInfoFlags)(0), // INS_vssrlrn_b_h
        (insInfoFlags)(0), // INS_vssrlrn_h_w
        (insInfoFlags)(0), // INS_vssrlrn_w_d
        (insInfoFlags)(0), // INS_vssrarn_b_h
        (insInfoFlags)(0), // INS_vssrarn_h_w
        (insInfoFlags)(0), // INS_vssrarn_w_d
        (insInfoFlags)(0), // INS_vssrln_bu_h
        (insInfoFlags)(0), // INS_vssrln_hu_w
        (insInfoFlags)(0), // INS_vssrln_wu_d
        (insInfoFlags)(0), // INS_vssran_bu_h
        (insInfoFlags)(0), // INS_vssran_hu_w
        (insInfoFlags)(0), // INS_vssran_wu_d
        (insInfoFlags)(0), // INS_vssrlrn_bu_h
        (insInfoFlags)(0), // INS_vssrlrn_hu_w
        (insInfoFlags)(0), // INS_vssrlrn_wu_d
        (insInfoFlags)(0), // INS_vssrarn_bu_h
        (insInfoFlags)(0), // INS_vssrarn_hu_w
        (insInfoFlags)(0), // INS_vssrarn_wu_d
        (insInfoFlags)(0), // INS_vbitclr_b
        (insInfoFlags)(0), // INS_vbitclr_h
        (insInfoFlags)(0), // INS_vbitclr_w
        (insInfoFlags)(0), // INS_vbitclr_d
        (insInfoFlags)(0), // INS_vbitset_b
        (insInfoFlags)(0), // INS_vbitset_h
        (insInfoFlags)(0), // INS_vbitset_w
        (insInfoFlags)(0), // INS_vbitset_d
        (insInfoFlags)(0), // INS_vbitrev_b
        (insInfoFlags)(0), // INS_vbitrev_h
        (insInfoFlags)(0), // INS_vbitrev_w
        (insInfoFlags)(0), // INS_vbitrev_d
        (insInfoFlags)(0), // INS_vpackev_b
        (insInfoFlags)(0), // INS_vpackev_h
        (insInfoFlags)(0), // INS_vpackev_w
        (insInfoFlags)(0), // INS_vpackev_d
        (insInfoFlags)(0), // INS_vpackod_b
        (insInfoFlags)(0), // INS_vpackod_h
        (insInfoFlags)(0), // INS_vpackod_w
        (insInfoFlags)(0), // INS_vpackod_d
        (insInfoFlags)(0), // INS_vilvl_b
        (insInfoFlags)(0), // INS_vilvl_h
        (insInfoFlags)(0), // INS_vilvl_w
        (insInfoFlags)(0), // INS_vilvl_d
        (insInfoFlags)(0), // INS_vilvh_b
        (insInfoFlags)(0), // INS_vilvh_h
        (insInfoFlags)(0), // INS_vilvh_w
        (insInfoFlags)(0), // INS_vilvh_d
        (insInfoFlags)(0), // INS_vpickev_b
        (insInfoFlags)(0), // INS_vpickev_h
        (insInfoFlags)(0), // INS_vpickev_w
        (insInfoFlags)(0), // INS_vpickev_d
        (insInfoFlags)(0), // INS_vpickod_b
        (insInfoFlags)(0), // INS_vpickod_h
        (insInfoFlags)(0), // INS_vpickod_w
        (insInfoFlags)(0), // INS_vpickod_d
        (insInfoFlags)(0), // INS_vand_v
        (insInfoFlags)(0), // INS_vor_v
        (insInfoFlags)(0), // INS_vxor_v
        (insInfoFlags)(0), // INS_vnor_v
        (insInfoFlags)(0), // INS_vandn_v
        (insInfoFlags)(0), // INS_vorn_v
        (insInfoFlags)(0), // INS_vfrstp_b
        (insInfoFlags)(0), // INS_vfrstp_h
        (insInfoFlags)(0), // INS_vadd_q
        (insInfoFlags)(0), // INS_vsub_q
        (insInfoFlags)(0), // INS_vsigncov_b
        (insInfoFlags)(0), // INS_vsigncov_h
        (insInfoFlags)(0), // INS_vsigncov_w
        (insInfoFlags)(0), // INS_vsigncov_d
        (insInfoFlags)(0), // INS_vfadd_s
        (insInfoFlags)(0), // INS_vfadd_d
        (insInfoFlags)(0), // INS_vfsub_s
        (insInfoFlags)(0), // INS_vfsub_d
        (insInfoFlags)(0), // INS_vfmul_s
        (insInfoFlags)(0), // INS_vfmul_d
        (insInfoFlags)(0), // INS_vfdiv_s
        (insInfoFlags)(0), // INS_vfdiv_d
        (insInfoFlags)(0), // INS_vfmax_s
        (insInfoFlags)(0), // INS_vfmax_d
        (insInfoFlags)(0), // INS_vfmin_s
        (insInfoFlags)(0), // INS_vfmin_d
        (insInfoFlags)(0), // INS_vfmaxa_s
        (insInfoFlags)(0), // INS_vfmaxa_d
        (insInfoFlags)(0), // INS_vfmina_s
        (insInfoFlags)(0), // INS_vfmina_d
        (insInfoFlags)(0), // INS_vfcvt_h_s
        (insInfoFlags)(0), // INS_vfcvt_s_d
        (insInfoFlags)(0), // INS_vffint_s_l
        (insInfoFlags)(0), // INS_vftint_w_d
        (insInfoFlags)(0), // INS_vftintrm_w_d
        (insInfoFlags)(0), // INS_vftintrp_w_d
        (insInfoFlags)(0), // INS_vftintrz_w_d
        (insInfoFlags)(0), // INS_vftintrne_w_d
        (insInfoFlags)(0), // INS_vshuf_h
        (insInfoFlags)(0), // INS_vshuf_w
        (insInfoFlags)(0), // INS_vshuf_d
        (insInfoFlags)(0), // INS_vreplgr2vr_b
        (insInfoFlags)(0), // INS_vreplgr2vr_h
        (insInfoFlags)(0), // INS_vreplgr2vr_w
        (insInfoFlags)(0), // INS_vreplgr2vr_d
        (insInfoFlags)(0), // INS_vclo_b
        (insInfoFlags)(0), // INS_vclo_h
        (insInfoFlags)(0), // INS_vclo_w
        (insInfoFlags)(0), // INS_vclo_d
        (insInfoFlags)(0), // INS_vclz_b
        (insInfoFlags)(0), // INS_vclz_h
        (insInfoFlags)(0), // INS_vclz_w
        (insInfoFlags)(0), // INS_vclz_d
        (insInfoFlags)(0), // INS_vpcnt_b
        (insInfoFlags)(0), // INS_vpcnt_h
        (insInfoFlags)(0), // INS_vpcnt_w
        (insInfoFlags)(0), // INS_vpcnt_d
        (insInfoFlags)(0), // INS_vneg_b
        (insInfoFlags)(0), // INS_vneg_h
        (insInfoFlags)(0), // INS_vneg_w
        (insInfoFlags)(0), // INS_vneg_d
        (insInfoFlags)(0), // INS_vmskltz_b
        (insInfoFlags)(0), // INS_vmskltz_h
        (insInfoFlags)(0), // INS_vmskltz_w
        (insInfoFlags)(0), // INS_vmskltz_d
        (insInfoFlags)(0), // INS_vmskgez_b
        (insInfoFlags)(0), // INS_vmsknz_b
        (insInfoFlags)(0), // INS_vflogb_s
        (insInfoFlags)(0), // INS_vflogb_d
        (insInfoFlags)(0), // INS_vfclass_s
        (insInfoFlags)(0), // INS_vfclass_d
        (insInfoFlags)(0), // INS_vfsqrt_s
        (insInfoFlags)(0), // INS_vfsqrt_d
        (insInfoFlags)(0), // INS_vfrecip_s
        (insInfoFlags)(0), // INS_vfrecip_d
        (insInfoFlags)(0), // INS_vfrsqrt_s
        (insInfoFlags)(0), // INS_vfrsqrt_d
        (insInfoFlags)(0), // INS_vfrecipe_s
        (insInfoFlags)(0), // INS_vfrecipe_d
        (insInfoFlags)(0), // INS_vfrsqrte_s
        (insInfoFlags)(0), // INS_vfrsqrte_d
        (insInfoFlags)(0), // INS_vfrint_s
        (insInfoFlags)(0), // INS_vfrint_d
        (insInfoFlags)(0), // INS_vfrintrm_s
        (insInfoFlags)(0), // INS_vfrintrm_d
        (insInfoFlags)(0), // INS_vfrintrp_s
        (insInfoFlags)(0), // INS_vfrintrp_d
        (insInfoFlags)(0), // INS_vfrintrz_s
        (insInfoFlags)(0), // INS_vfrintrz_d
        (insInfoFlags)(0), // INS_vfrintrne_s
        (insInfoFlags)(0), // INS_vfrintrne_d
        (insInfoFlags)(0), // INS_vfcvtl_s_h
        (insInfoFlags)(0), // INS_vfcvth_s_h
        (insInfoFlags)(0), // INS_vfcvtl_d_s
        (insInfoFlags)(0), // INS_vfcvth_d_s
        (insInfoFlags)(0), // INS_vffint_s_w
        (insInfoFlags)(0), // INS_vffint_s_wu
        (insInfoFlags)(0), // INS_vffint_d_l
        (insInfoFlags)(0), // INS_vffint_d_lu
        (insInfoFlags)(0), // INS_vffintl_d_w
        (insInfoFlags)(0), // INS_vffinth_d_w
        (insInfoFlags)(0), // INS_vftint_w_s
        (insInfoFlags)(0), // INS_vftint_l_d
        (insInfoFlags)(0), // INS_vftintrm_w_s
        (insInfoFlags)(0), // INS_vftintrm_l_d
        (insInfoFlags)(0), // INS_vftintrp_w_s
        (insInfoFlags)(0), // INS_vftintrp_l_d
        (insInfoFlags)(0), // INS_vftintrz_w_s
        (insInfoFlags)(0), // INS_vftintrz_l_d
        (insInfoFlags)(0), // INS_vftintrne_w_s
        (insInfoFlags)(0), // INS_vftintrne_l_d
        (insInfoFlags)(0), // INS_vftint_wu_s
        (insInfoFlags)(0), // INS_vftint_lu_d
        (insInfoFlags)(0), // INS_vftintrz_wu_s
        (insInfoFlags)(0), // INS_vftintrz_lu_d
        (insInfoFlags)(0), // INS_vftintl_l_s
        (insInfoFlags)(0), // INS_vftinth_l_s
        (insInfoFlags)(0), // INS_vftintrml_l_s
        (insInfoFlags)(0), // INS_vftintrmh_l_s
        (insInfoFlags)(0), // INS_vftintrpl_l_s
        (insInfoFlags)(0), // INS_vftintrph_l_s
        (insInfoFlags)(0), // INS_vftintrzl_l_s
        (insInfoFlags)(0), // INS_vftintrzh_l_s
        (insInfoFlags)(0), // INS_vftintrnel_l_s
        (insInfoFlags)(0), // INS_vftintrneh_l_s
        (insInfoFlags)(0), // INS_vexth_h_b
        (insInfoFlags)(0), // INS_vexth_w_h
        (insInfoFlags)(0), // INS_vexth_d_w
        (insInfoFlags)(0), // INS_vexth_q_d
        (insInfoFlags)(0), // INS_vexth_hu_bu
        (insInfoFlags)(0), // INS_vexth_wu_hu
        (insInfoFlags)(0), // INS_vexth_du_wu
        (insInfoFlags)(0), // INS_vexth_qu_du
        (insInfoFlags)(0), // INS_vextl_q_d
        (insInfoFlags)(0), // INS_vextl_qu_du
        (insInfoFlags)(LD), // INS_vldi
        (insInfoFlags)(0), // INS_vseteqz_v
        (insInfoFlags)(0), // INS_vsetnez_v
        (insInfoFlags)(0), // INS_vsetanyeqz_b
        (insInfoFlags)(0), // INS_vsetanyeqz_h
        (insInfoFlags)(0), // INS_vsetanyeqz_w
        (insInfoFlags)(0), // INS_vsetanyeqz_d
        (insInfoFlags)(0), // INS_vsetallnez_b
        (insInfoFlags)(0), // INS_vsetallnez_h
        (insInfoFlags)(0), // INS_vsetallnez_w
        (insInfoFlags)(0), // INS_vsetallnez_d
        (insInfoFlags)(ST), // INS_vstelm_b
        (insInfoFlags)(ST), // INS_vstelm_h
        (insInfoFlags)(ST), // INS_vstelm_w
        (insInfoFlags)(ST), // INS_vstelm_d
        (insInfoFlags)(0), // INS_vseqi_b
        (insInfoFlags)(0), // INS_vseqi_h
        (insInfoFlags)(0), // INS_vseqi_w
        (insInfoFlags)(0), // INS_vseqi_d
        (insInfoFlags)(0), // INS_vslei_b
        (insInfoFlags)(0), // INS_vslei_h
        (insInfoFlags)(0), // INS_vslei_w
        (insInfoFlags)(0), // INS_vslei_d
        (insInfoFlags)(0), // INS_vslti_b
        (insInfoFlags)(0), // INS_vslti_h
        (insInfoFlags)(0), // INS_vslti_w
        (insInfoFlags)(0), // INS_vslti_d
        (insInfoFlags)(0), // INS_vmaxi_b
        (insInfoFlags)(0), // INS_vmaxi_h
        (insInfoFlags)(0), // INS_vmaxi_w
        (insInfoFlags)(0), // INS_vmaxi_d
        (insInfoFlags)(0), // INS_vmini_b
        (insInfoFlags)(0), // INS_vmini_h
        (insInfoFlags)(0), // INS_vmini_w
        (insInfoFlags)(0), // INS_vmini_d
        (insInfoFlags)(LD), // INS_vldrepl_d
        (insInfoFlags)(LD), // INS_vldrepl_w
        (insInfoFlags)(LD), // INS_vldrepl_h
        (insInfoFlags)(LD), // INS_vld
        (insInfoFlags)(LD), // INS_vldrepl_b
        (insInfoFlags)(ST), // INS_vst
        (insInfoFlags)(0), // INS_vinsgr2vr_d
        (insInfoFlags)(0), // INS_vreplvei_d
        (insInfoFlags)(0), // INS_vpickve2gr_d
        (insInfoFlags)(0), // INS_vpickve2gr_du
        (insInfoFlags)(0), // INS_vpickve2gr_w
        (insInfoFlags)(0), // INS_vpickve2gr_wu
        (insInfoFlags)(0), // INS_vinsgr2vr_w
        (insInfoFlags)(0), // INS_vreplvei_w
        (insInfoFlags)(0), // INS_vslli_b
        (insInfoFlags)(0), // INS_vsrli_b
        (insInfoFlags)(0), // INS_vsrai_b
        (insInfoFlags)(0), // INS_vsrlri_b
        (insInfoFlags)(0), // INS_vsrari_b
        (insInfoFlags)(0), // INS_vrotri_b
        (insInfoFlags)(0), // INS_vsllwil_h_b
        (insInfoFlags)(0), // INS_vsllwil_hu_bu
        (insInfoFlags)(0), // INS_vbitclri_b
        (insInfoFlags)(0), // INS_vbitseti_b
        (insInfoFlags)(0), // INS_vbitrevi_b
        (insInfoFlags)(0), // INS_vsat_b
        (insInfoFlags)(0), // INS_vsat_bu
        (insInfoFlags)(0), // INS_vreplvei_h
        (insInfoFlags)(0), // INS_vinsgr2vr_h
        (insInfoFlags)(0), // INS_vpickve2gr_h
        (insInfoFlags)(0), // INS_vpickve2gr_hu
        (insInfoFlags)(0), // INS_vpickve2gr_b
        (insInfoFlags)(0), // INS_vpickve2gr_bu
        (insInfoFlags)(0), // INS_vslli_h
        (insInfoFlags)(0), // INS_vsrli_h
        (insInfoFlags)(0), // INS_vsrai_h
        (insInfoFlags)(0), // INS_vsrlri_h
        (insInfoFlags)(0), // INS_vsrari_h
        (insInfoFlags)(0), // INS_vrotri_h
        (insInfoFlags)(0), // INS_vsllwil_w_h
        (insInfoFlags)(0), // INS_vsllwil_wu_hu
        (insInfoFlags)(0), // INS_vsrlni_b_h
        (insInfoFlags)(0), // INS_vsrlrni_b_h
        (insInfoFlags)(0), // INS_vssrlni_b_h
        (insInfoFlags)(0), // INS_vssrlni_bu_h
        (insInfoFlags)(0), // INS_vssrlrni_b_h
        (insInfoFlags)(0), // INS_vssrlrni_bu_h
        (insInfoFlags)(0), // INS_vsrani_b_h
        (insInfoFlags)(0), // INS_vsrarni_b_h
        (insInfoFlags)(0), // INS_vssrani_b_h
        (insInfoFlags)(0), // INS_vssrani_bu_h
        (insInfoFlags)(0), // INS_vssrarni_b_h
        (insInfoFlags)(0), // INS_vssrarni_bu_h
        (insInfoFlags)(0), // INS_vbitclri_h
        (insInfoFlags)(0), // INS_vbitseti_h
        (insInfoFlags)(0), // INS_vbitrevi_h
        (insInfoFlags)(0), // INS_vsat_h
        (insInfoFlags)(0), // INS_vsat_hu
        (insInfoFlags)(0), // INS_vinsgr2vr_b
        (insInfoFlags)(0), // INS_vreplvei_b
        (insInfoFlags)(0), // INS_vslei_bu
        (insInfoFlags)(0), // INS_vslei_hu
        (insInfoFlags)(0), // INS_vslei_wu
        (insInfoFlags)(0), // INS_vslei_du
        (insInfoFlags)(0), // INS_vslti_bu
        (insInfoFlags)(0), // INS_vslti_hu
        (insInfoFlags)(0), // INS_vslti_wu
        (insInfoFlags)(0), // INS_vslti_du
        (insInfoFlags)(0), // INS_vaddi_bu
        (insInfoFlags)(0), // INS_vaddi_hu
        (insInfoFlags)(0), // INS_vaddi_wu
        (insInfoFlags)(0), // INS_vaddi_du
        (insInfoFlags)(0), // INS_vsubi_bu
        (insInfoFlags)(0), // INS_vsubi_hu
        (insInfoFlags)(0), // INS_vsubi_wu
        (insInfoFlags)(0), // INS_vsubi_du
        (insInfoFlags)(0), // INS_vslli_w
        (insInfoFlags)(0), // INS_vsrli_w
        (insInfoFlags)(0), // INS_vsrai_w
        (insInfoFlags)(0), // INS_vbsll_v
        (insInfoFlags)(0), // INS_vbsrl_v
        (insInfoFlags)(0), // INS_vsrlri_w
        (insInfoFlags)(0), // INS_vsrari_w
        (insInfoFlags)(0), // INS_vrotri_w
        (insInfoFlags)(0), // INS_vsllwil_d_w
        (insInfoFlags)(0), // INS_vsllwil_du_wu
        (insInfoFlags)(0), // INS_vsrlni_h_w
        (insInfoFlags)(0), // INS_vsrlrni_h_w
        (insInfoFlags)(0), // INS_vssrlni_h_w
        (insInfoFlags)(0), // INS_vssrlni_hu_w
        (insInfoFlags)(0), // INS_vssrlrni_h_w
        (insInfoFlags)(0), // INS_vssrlrni_hu_w
        (insInfoFlags)(0), // INS_vsrani_h_w
        (insInfoFlags)(0), // INS_vsrarni_h_w
        (insInfoFlags)(0), // INS_vssrani_h_w
        (insInfoFlags)(0), // INS_vssrani_hu_w
        (insInfoFlags)(0), // INS_vssrarni_h_w
        (insInfoFlags)(0), // INS_vssrarni_hu_w
        (insInfoFlags)(0), // INS_vbitclri_w
        (insInfoFlags)(0), // INS_vbitseti_w
        (insInfoFlags)(0), // INS_vbitrevi_w
        (insInfoFlags)(0), // INS_vmaxi_bu
        (insInfoFlags)(0), // INS_vmaxi_hu
        (insInfoFlags)(0), // INS_vmaxi_wu
        (insInfoFlags)(0), // INS_vmaxi_du
        (insInfoFlags)(0), // INS_vmini_bu
        (insInfoFlags)(0), // INS_vmini_hu
        (insInfoFlags)(0), // INS_vmini_wu
        (insInfoFlags)(0), // INS_vmini_du
        (insInfoFlags)(0), // INS_vfrstpi_b
        (insInfoFlags)(0), // INS_vfrstpi_h
        (insInfoFlags)(0), // INS_vsat_w
        (insInfoFlags)(0), // INS_vsat_wu
        (insInfoFlags)(0), // INS_vslli_d
        (insInfoFlags)(0), // INS_vsrli_d
        (insInfoFlags)(0), // INS_vsrai_d
        (insInfoFlags)(0), // INS_vrotri_d
        (insInfoFlags)(0), // INS_vsrlri_d
        (insInfoFlags)(0), // INS_vsrari_d
        (insInfoFlags)(0), // INS_vsrlni_w_d
        (insInfoFlags)(0), // INS_vsrlrni_w_d
        (insInfoFlags)(0), // INS_vssrlni_w_d
        (insInfoFlags)(0), // INS_vssrlni_wu_d
        (insInfoFlags)(0), // INS_vssrlrni_w_d
        (insInfoFlags)(0), // INS_vssrlrni_wu_d
        (insInfoFlags)(0), // INS_vsrani_w_d
        (insInfoFlags)(0), // INS_vsrarni_w_d
        (insInfoFlags)(0), // INS_vssrani_w_d
        (insInfoFlags)(0), // INS_vssrani_wu_d
        (insInfoFlags)(0), // INS_vssrarni_w_d
        (insInfoFlags)(0), // INS_vssrarni_wu_d
        (insInfoFlags)(0), // INS_vbitclri_d
        (insInfoFlags)(0), // INS_vbitseti_d
        (insInfoFlags)(0), // INS_vbitrevi_d
        (insInfoFlags)(0), // INS_vsat_d
        (insInfoFlags)(0), // INS_vsat_du
        (insInfoFlags)(0), // INS_vsrlni_d_q
        (insInfoFlags)(0), // INS_vsrlrni_d_q
        (insInfoFlags)(0), // INS_vssrlni_d_q
        (insInfoFlags)(0), // INS_vssrlni_du_q
        (insInfoFlags)(0), // INS_vssrlrni_d_q
        (insInfoFlags)(0), // INS_vssrlrni_du_q
        (insInfoFlags)(0), // INS_vsrani_d_q
        (insInfoFlags)(0), // INS_vsrarni_d_q
        (insInfoFlags)(0), // INS_vssrani_d_q
        (insInfoFlags)(0), // INS_vssrani_du_q
        (insInfoFlags)(0), // INS_vssrarni_d_q
        (insInfoFlags)(0), // INS_vssrarni_du_q
        (insInfoFlags)(0), // INS_vextrins_d
        (insInfoFlags)(0), // INS_vextrins_w
        (insInfoFlags)(0), // INS_vextrins_h
        (insInfoFlags)(0), // INS_vextrins_b
        (insInfoFlags)(0), // INS_vshuf4i_b
        (insInfoFlags)(0), // INS_vshuf4i_h
        (insInfoFlags)(0), // INS_vshuf4i_w
        (insInfoFlags)(0), // INS_vshuf4i_d
        (insInfoFlags)(0), // INS_vbitseli_b
        (insInfoFlags)(0), // INS_vandi_b
        (insInfoFlags)(0), // INS_vori_b
        (insInfoFlags)(0), // INS_vxori_b
        (insInfoFlags)(0), // INS_vnori_b
        (insInfoFlags)(0), // INS_vpermi_w
        (insInfoFlags)(0), // INS_xvfmadd_s
        (insInfoFlags)(0), // INS_xvfmadd_d
        (insInfoFlags)(0), // INS_xvfmsub_s
        (insInfoFlags)(0), // INS_xvfmsub_d
        (insInfoFlags)(0), // INS_xvfnmadd_s
        (insInfoFlags)(0), // INS_xvfnmadd_d
        (insInfoFlags)(0), // INS_xvfnmsub_s
        (insInfoFlags)(0), // INS_xvfnmsub_d
        (insInfoFlags)(0), // INS_xvbitsel_v
        (insInfoFlags)(0), // INS_xvshuf_b
        (insInfoFlags)(LD), // INS_xvldx
        (insInfoFlags)(ST), // INS_xvstx
        (insInfoFlags)(0), // INS_xvreplve_b
        (insInfoFlags)(0), // INS_xvreplve_h
        (insInfoFlags)(0), // INS_xvreplve_w
        (insInfoFlags)(0), // INS_xvreplve_d
        (insInfoFlags)(0), // INS_xvfcmp_caf_s
        (insInfoFlags)(0), // INS_xvfcmp_cun_s
        (insInfoFlags)(0), // INS_xvfcmp_ceq_s
        (insInfoFlags)(0), // INS_xvfcmp_cueq_s
        (insInfoFlags)(0), // INS_xvfcmp_clt_s
        (insInfoFlags)(0), // INS_xvfcmp_cult_s
        (insInfoFlags)(0), // INS_xvfcmp_cle_s
        (insInfoFlags)(0), // INS_xvfcmp_cule_s
        (insInfoFlags)(0), // INS_xvfcmp_cne_s
        (insInfoFlags)(0), // INS_xvfcmp_cor_s
        (insInfoFlags)(0), // INS_xvfcmp_cune_s
        (insInfoFlags)(0), // INS_xvfcmp_saf_s
        (insInfoFlags)(0), // INS_xvfcmp_sun_s
        (insInfoFlags)(0), // INS_xvfcmp_seq_s
        (insInfoFlags)(0), // INS_xvfcmp_sueq_s
        (insInfoFlags)(0), // INS_xvfcmp_slt_s
        (insInfoFlags)(0), // INS_xvfcmp_sult_s
        (insInfoFlags)(0), // INS_xvfcmp_sle_s
        (insInfoFlags)(0), // INS_xvfcmp_sule_s
        (insInfoFlags)(0), // INS_xvfcmp_sne_s
        (insInfoFlags)(0), // INS_xvfcmp_sor_s
        (insInfoFlags)(0), // INS_xvfcmp_sune_s
        (insInfoFlags)(0), // INS_xvfcmp_caf_d
        (insInfoFlags)(0), // INS_xvfcmp_cun_d
        (insInfoFlags)(0), // INS_xvfcmp_ceq_d
        (insInfoFlags)(0), // INS_xvfcmp_cueq_d
        (insInfoFlags)(0), // INS_xvfcmp_clt_d
        (insInfoFlags)(0), // INS_xvfcmp_cult_d
        (insInfoFlags)(0), // INS_xvfcmp_cle_d
        (insInfoFlags)(0), // INS_xvfcmp_cule_d
        (insInfoFlags)(0), // INS_xvfcmp_cne_d
        (insInfoFlags)(0), // INS_xvfcmp_cor_d
        (insInfoFlags)(0), // INS_xvfcmp_cune_d
        (insInfoFlags)(0), // INS_xvfcmp_saf_d
        (insInfoFlags)(0), // INS_xvfcmp_sun_d
        (insInfoFlags)(0), // INS_xvfcmp_seq_d
        (insInfoFlags)(0), // INS_xvfcmp_sueq_d
        (insInfoFlags)(0), // INS_xvfcmp_slt_d
        (insInfoFlags)(0), // INS_xvfcmp_sult_d
        (insInfoFlags)(0), // INS_xvfcmp_sle_d
        (insInfoFlags)(0), // INS_xvfcmp_sule_d
        (insInfoFlags)(0), // INS_xvfcmp_sne_d
        (insInfoFlags)(0), // INS_xvfcmp_sor_d
        (insInfoFlags)(0), // INS_xvfcmp_sune_d
        (insInfoFlags)(0), // INS_xvseq_b
        (insInfoFlags)(0), // INS_xvseq_h
        (insInfoFlags)(0), // INS_xvseq_w
        (insInfoFlags)(0), // INS_xvseq_d
        (insInfoFlags)(0), // INS_xvsle_b
        (insInfoFlags)(0), // INS_xvsle_h
        (insInfoFlags)(0), // INS_xvsle_w
        (insInfoFlags)(0), // INS_xvsle_d
        (insInfoFlags)(0), // INS_xvsle_bu
        (insInfoFlags)(0), // INS_xvsle_hu
        (insInfoFlags)(0), // INS_xvsle_wu
        (insInfoFlags)(0), // INS_xvsle_du
        (insInfoFlags)(0), // INS_xvslt_b
        (insInfoFlags)(0), // INS_xvslt_h
        (insInfoFlags)(0), // INS_xvslt_w
        (insInfoFlags)(0), // INS_xvslt_d
        (insInfoFlags)(0), // INS_xvslt_bu
        (insInfoFlags)(0), // INS_xvslt_hu
        (insInfoFlags)(0), // INS_xvslt_wu
        (insInfoFlags)(0), // INS_xvslt_du
        (insInfoFlags)(0), // INS_xvadd_b
        (insInfoFlags)(0), // INS_xvadd_h
        (insInfoFlags)(0), // INS_xvadd_w
        (insInfoFlags)(0), // INS_xvadd_d
        (insInfoFlags)(0), // INS_xvsub_b
        (insInfoFlags)(0), // INS_xvsub_h
        (insInfoFlags)(0), // INS_xvsub_w
        (insInfoFlags)(0), // INS_xvsub_d
        (insInfoFlags)(0), // INS_xvaddwev_h_b
        (insInfoFlags)(0), // INS_xvaddwev_w_h
        (insInfoFlags)(0), // INS_xvaddwev_d_w
        (insInfoFlags)(0), // INS_xvaddwev_q_d
        (insInfoFlags)(0), // INS_xvsubwev_h_b
        (insInfoFlags)(0), // INS_xvsubwev_w_h
        (insInfoFlags)(0), // INS_xvsubwev_d_w
        (insInfoFlags)(0), // INS_xvsubwev_q_d
        (insInfoFlags)(0), // INS_xvaddwod_h_b
        (insInfoFlags)(0), // INS_xvaddwod_w_h
        (insInfoFlags)(0), // INS_xvaddwod_d_w
        (insInfoFlags)(0), // INS_xvaddwod_q_d
        (insInfoFlags)(0), // INS_xvsubwod_h_b
        (insInfoFlags)(0), // INS_xvsubwod_w_h
        (insInfoFlags)(0), // INS_xvsubwod_d_w
        (insInfoFlags)(0), // INS_xvsubwod_q_d
        (insInfoFlags)(0), // INS_xvaddwev_h_bu
        (insInfoFlags)(0), // INS_xvaddwev_w_hu
        (insInfoFlags)(0), // INS_xvaddwev_d_wu
        (insInfoFlags)(0), // INS_xvaddwev_q_du
        (insInfoFlags)(0), // INS_xvsubwev_h_bu
        (insInfoFlags)(0), // INS_xvsubwev_w_hu
        (insInfoFlags)(0), // INS_xvsubwev_d_wu
        (insInfoFlags)(0), // INS_xvsubwev_q_du
        (insInfoFlags)(0), // INS_xvaddwod_h_bu
        (insInfoFlags)(0), // INS_xvaddwod_w_hu
        (insInfoFlags)(0), // INS_xvaddwod_d_wu
        (insInfoFlags)(0), // INS_xvaddwod_q_du
        (insInfoFlags)(0), // INS_xvsubwod_h_bu
        (insInfoFlags)(0), // INS_xvsubwod_w_hu
        (insInfoFlags)(0), // INS_xvsubwod_d_wu
        (insInfoFlags)(0), // INS_xvsubwod_q_du
        (insInfoFlags)(0), // INS_xvaddwev_h_bu_b
        (insInfoFlags)(0), // INS_xvaddwev_w_hu_h
        (insInfoFlags)(0), // INS_xvaddwev_d_wu_w
        (insInfoFlags)(0), // INS_xvaddwev_q_du_d
        (insInfoFlags)(0), // INS_xvaddwod_h_bu_b
        (insInfoFlags)(0), // INS_xvaddwod_w_hu_h
        (insInfoFlags)(0), // INS_xvaddwod_d_wu_w
        (insInfoFlags)(0), // INS_xvaddwod_q_du_d
        (insInfoFlags)(0), // INS_xvsadd_b
        (insInfoFlags)(0), // INS_xvsadd_h
        (insInfoFlags)(0), // INS_xvsadd_w
        (insInfoFlags)(0), // INS_xvsadd_d
        (insInfoFlags)(0), // INS_xvssub_b
        (insInfoFlags)(0), // INS_xvssub_h
        (insInfoFlags)(0), // INS_xvssub_w
        (insInfoFlags)(0), // INS_xvssub_d
        (insInfoFlags)(0), // INS_xvsadd_bu
        (insInfoFlags)(0), // INS_xvsadd_hu
        (insInfoFlags)(0), // INS_xvsadd_wu
        (insInfoFlags)(0), // INS_xvsadd_du
        (insInfoFlags)(0), // INS_xvssub_bu
        (insInfoFlags)(0), // INS_xvssub_hu
        (insInfoFlags)(0), // INS_xvssub_wu
        (insInfoFlags)(0), // INS_xvssub_du
        (insInfoFlags)(0), // INS_xvhaddw_h_b
        (insInfoFlags)(0), // INS_xvhaddw_w_h
        (insInfoFlags)(0), // INS_xvhaddw_d_w
        (insInfoFlags)(0), // INS_xvhaddw_q_d
        (insInfoFlags)(0), // INS_xvhsubw_h_b
        (insInfoFlags)(0), // INS_xvhsubw_w_h
        (insInfoFlags)(0), // INS_xvhsubw_d_w
        (insInfoFlags)(0), // INS_xvhsubw_q_d
        (insInfoFlags)(0), // INS_xvhaddw_hu_bu
        (insInfoFlags)(0), // INS_xvhaddw_wu_hu
        (insInfoFlags)(0), // INS_xvhaddw_du_wu
        (insInfoFlags)(0), // INS_xvhaddw_qu_du
        (insInfoFlags)(0), // INS_xvhsubw_hu_bu
        (insInfoFlags)(0), // INS_xvhsubw_wu_hu
        (insInfoFlags)(0), // INS_xvhsubw_du_wu
        (insInfoFlags)(0), // INS_xvhsubw_qu_du
        (insInfoFlags)(0), // INS_xvadda_b
        (insInfoFlags)(0), // INS_xvadda_h
        (insInfoFlags)(0), // INS_xvadda_w
        (insInfoFlags)(0), // INS_xvadda_d
        (insInfoFlags)(0), // INS_xvabsd_b
        (insInfoFlags)(0), // INS_xvabsd_h
        (insInfoFlags)(0), // INS_xvabsd_w
        (insInfoFlags)(0), // INS_xvabsd_d
        (insInfoFlags)(0), // INS_xvabsd_bu
        (insInfoFlags)(0), // INS_xvabsd_hu
        (insInfoFlags)(0), // INS_xvabsd_wu
        (insInfoFlags)(0), // INS_xvabsd_du
        (insInfoFlags)(0), // INS_xvavg_b
        (insInfoFlags)(0), // INS_xvavg_h
        (insInfoFlags)(0), // INS_xvavg_w
        (insInfoFlags)(0), // INS_xvavg_d
        (insInfoFlags)(0), // INS_xvavg_bu
        (insInfoFlags)(0), // INS_xvavg_hu
        (insInfoFlags)(0), // INS_xvavg_wu
        (insInfoFlags)(0), // INS_xvavg_du
        (insInfoFlags)(0), // INS_xvavgr_b
        (insInfoFlags)(0), // INS_xvavgr_h
        (insInfoFlags)(0), // INS_xvavgr_w
        (insInfoFlags)(0), // INS_xvavgr_d
        (insInfoFlags)(0), // INS_xvavgr_bu
        (insInfoFlags)(0), // INS_xvavgr_hu
        (insInfoFlags)(0), // INS_xvavgr_wu
        (insInfoFlags)(0), // INS_xvavgr_du
        (insInfoFlags)(0), // INS_xvmax_b
        (insInfoFlags)(0), // INS_xvmax_h
        (insInfoFlags)(0), // INS_xvmax_w
        (insInfoFlags)(0), // INS_xvmax_d
        (insInfoFlags)(0), // INS_xvmin_b
        (insInfoFlags)(0), // INS_xvmin_h
        (insInfoFlags)(0), // INS_xvmin_w
        (insInfoFlags)(0), // INS_xvmin_d
        (insInfoFlags)(0), // INS_xvmax_bu
        (insInfoFlags)(0), // INS_xvmax_hu
        (insInfoFlags)(0), // INS_xvmax_wu
        (insInfoFlags)(0), // INS_xvmax_du
        (insInfoFlags)(0), // INS_xvmin_bu
        (insInfoFlags)(0), // INS_xvmin_hu
        (insInfoFlags)(0), // INS_xvmin_wu
        (insInfoFlags)(0), // INS_xvmin_du
        (insInfoFlags)(0), // INS_xvmul_b
        (insInfoFlags)(0), // INS_xvmul_h
        (insInfoFlags)(0), // INS_xvmul_w
        (insInfoFlags)(0), // INS_xvmul_d
        (insInfoFlags)(0), // INS_xvmuh_b
        (insInfoFlags)(0), // INS_xvmuh_h
        (insInfoFlags)(0), // INS_xvmuh_w
        (insInfoFlags)(0), // INS_xvmuh_d
        (insInfoFlags)(0), // INS_xvmuh_bu
        (insInfoFlags)(0), // INS_xvmuh_hu
        (insInfoFlags)(0), // INS_xvmuh_wu
        (insInfoFlags)(0), // INS_xvmuh_du
        (insInfoFlags)(0), // INS_xvmulwev_h_b
        (insInfoFlags)(0), // INS_xvmulwev_w_h
        (insInfoFlags)(0), // INS_xvmulwev_d_w
        (insInfoFlags)(0), // INS_xvmulwev_q_d
        (insInfoFlags)(0), // INS_xvmulwod_h_b
        (insInfoFlags)(0), // INS_xvmulwod_w_h
        (insInfoFlags)(0), // INS_xvmulwod_d_w
        (insInfoFlags)(0), // INS_xvmulwod_q_d
        (insInfoFlags)(0), // INS_xvmulwev_h_bu
        (insInfoFlags)(0), // INS_xvmulwev_w_hu
        (insInfoFlags)(0), // INS_xvmulwev_d_wu
        (insInfoFlags)(0), // INS_xvmulwev_q_du
        (insInfoFlags)(0), // INS_xvmulwod_h_bu
        (insInfoFlags)(0), // INS_xvmulwod_w_hu
        (insInfoFlags)(0), // INS_xvmulwod_d_wu
        (insInfoFlags)(0), // INS_xvmulwod_q_du
        (insInfoFlags)(0), // INS_xvmulwev_h_bu_b
        (insInfoFlags)(0), // INS_xvmulwev_w_hu_h
        (insInfoFlags)(0), // INS_xvmulwev_d_wu_w
        (insInfoFlags)(0), // INS_xvmulwev_q_du_d
        (insInfoFlags)(0), // INS_xvmulwod_h_bu_b
        (insInfoFlags)(0), // INS_xvmulwod_w_hu_h
        (insInfoFlags)(0), // INS_xvmulwod_d_wu_w
        (insInfoFlags)(0), // INS_xvmulwod_q_du_d
        (insInfoFlags)(0), // INS_xvmadd_b
        (insInfoFlags)(0), // INS_xvmadd_h
        (insInfoFlags)(0), // INS_xvmadd_w
        (insInfoFlags)(0), // INS_xvmadd_d
        (insInfoFlags)(0), // INS_xvmsub_b
        (insInfoFlags)(0), // INS_xvmsub_h
        (insInfoFlags)(0), // INS_xvmsub_w
        (insInfoFlags)(0), // INS_xvmsub_d
        (insInfoFlags)(0), // INS_xvmaddwev_h_b
        (insInfoFlags)(0), // INS_xvmaddwev_w_h
        (insInfoFlags)(0), // INS_xvmaddwev_d_w
        (insInfoFlags)(0), // INS_xvmaddwev_q_d
        (insInfoFlags)(0), // INS_xvmaddwod_h_b
        (insInfoFlags)(0), // INS_xvmaddwod_w_h
        (insInfoFlags)(0), // INS_xvmaddwod_d_w
        (insInfoFlags)(0), // INS_xvmaddwod_q_d
        (insInfoFlags)(0), // INS_xvmaddwev_h_bu
        (insInfoFlags)(0), // INS_xvmaddwev_w_hu
        (insInfoFlags)(0), // INS_xvmaddwev_d_wu
        (insInfoFlags)(0), // INS_xvmaddwev_q_du
        (insInfoFlags)(0), // INS_xvmaddwod_h_bu
        (insInfoFlags)(0), // INS_xvmaddwod_w_hu
        (insInfoFlags)(0), // INS_xvmaddwod_d_wu
        (insInfoFlags)(0), // INS_xvmaddwod_q_du
        (insInfoFlags)(0), // INS_xvmaddwev_h_bu_b
        (insInfoFlags)(0), // INS_xvmaddwev_w_hu_h
        (insInfoFlags)(0), // INS_xvmaddwev_d_wu_w
        (insInfoFlags)(0), // INS_xvmaddwev_q_du_d
        (insInfoFlags)(0), // INS_xvmaddwod_h_bu_b
        (insInfoFlags)(0), // INS_xvmaddwod_w_hu_h
        (insInfoFlags)(0), // INS_xvmaddwod_d_wu_w
        (insInfoFlags)(0), // INS_xvmaddwod_q_du_d
        (insInfoFlags)(0), // INS_xvdiv_b
        (insInfoFlags)(0), // INS_xvdiv_h
        (insInfoFlags)(0), // INS_xvdiv_w
        (insInfoFlags)(0), // INS_xvdiv_d
        (insInfoFlags)(0), // INS_xvmod_b
        (insInfoFlags)(0), // INS_xvmod_h
        (insInfoFlags)(0), // INS_xvmod_w
        (insInfoFlags)(0), // INS_xvmod_d
        (insInfoFlags)(0), // INS_xvdiv_bu
        (insInfoFlags)(0), // INS_xvdiv_hu
        (insInfoFlags)(0), // INS_xvdiv_wu
        (insInfoFlags)(0), // INS_xvdiv_du
        (insInfoFlags)(0), // INS_xvmod_bu
        (insInfoFlags)(0), // INS_xvmod_hu
        (insInfoFlags)(0), // INS_xvmod_wu
        (insInfoFlags)(0), // INS_xvmod_du
        (insInfoFlags)(0), // INS_xvsll_b
        (insInfoFlags)(0), // INS_xvsll_h
        (insInfoFlags)(0), // INS_xvsll_w
        (insInfoFlags)(0), // INS_xvsll_d
        (insInfoFlags)(0), // INS_xvsrl_b
        (insInfoFlags)(0), // INS_xvsrl_h
        (insInfoFlags)(0), // INS_xvsrl_w
        (insInfoFlags)(0), // INS_xvsrl_d
        (insInfoFlags)(0), // INS_xvsra_b
        (insInfoFlags)(0), // INS_xvsra_h
        (insInfoFlags)(0), // INS_xvsra_w
        (insInfoFlags)(0), // INS_xvsra_d
        (insInfoFlags)(0), // INS_xvrotr_b
        (insInfoFlags)(0), // INS_xvrotr_h
        (insInfoFlags)(0), // INS_xvrotr_w
        (insInfoFlags)(0), // INS_xvrotr_d
        (insInfoFlags)(0), // INS_xvsrlr_b
        (insInfoFlags)(0), // INS_xvsrlr_h
        (insInfoFlags)(0), // INS_xvsrlr_w
        (insInfoFlags)(0), // INS_xvsrlr_d
        (insInfoFlags)(0), // INS_xvsrar_b
        (insInfoFlags)(0), // INS_xvsrar_h
        (insInfoFlags)(0), // INS_xvsrar_w
        (insInfoFlags)(0), // INS_xvsrar_d
        (insInfoFlags)(0), // INS_xvsrln_b_h
        (insInfoFlags)(0), // INS_xvsrln_h_w
        (insInfoFlags)(0), // INS_xvsrln_w_d
        (insInfoFlags)(0), // INS_xvsran_b_h
        (insInfoFlags)(0), // INS_xvsran_h_w
        (insInfoFlags)(0), // INS_xvsran_w_d
        (insInfoFlags)(0), // INS_xvsrlrn_b_h
        (insInfoFlags)(0), // INS_xvsrlrn_h_w
        (insInfoFlags)(0), // INS_xvsrlrn_w_d
        (insInfoFlags)(0), // INS_xvsrarn_b_h
        (insInfoFlags)(0), // INS_xvsrarn_h_w
        (insInfoFlags)(0), // INS_xvsrarn_w_d
        (insInfoFlags)(0), // INS_xvssrln_b_h
        (insInfoFlags)(0), // INS_xvssrln_h_w
        (insInfoFlags)(0), // INS_xvssrln_w_d
        (insInfoFlags)(0), // INS_xvssran_b_h
        (insInfoFlags)(0), // INS_xvssran_h_w
        (insInfoFlags)(0), // INS_xvssran_w_d
        (insInfoFlags)(0), // INS_xvssrlrn_b_h
        (insInfoFlags)(0), // INS_xvssrlrn_h_w
        (insInfoFlags)(0), // INS_xvssrlrn_w_d
        (insInfoFlags)(0), // INS_xvssrarn_b_h
        (insInfoFlags)(0), // INS_xvssrarn_h_w
        (insInfoFlags)(0), // INS_xvssrarn_w_d
        (insInfoFlags)(0), // INS_xvssrln_bu_h
        (insInfoFlags)(0), // INS_xvssrln_hu_w
        (insInfoFlags)(0), // INS_xvssrln_wu_d
        (insInfoFlags)(0), // INS_xvssran_bu_h
        (insInfoFlags)(0), // INS_xvssran_hu_w
        (insInfoFlags)(0), // INS_xvssran_wu_d
        (insInfoFlags)(0), // INS_xvssrlrn_bu_h
        (insInfoFlags)(0), // INS_xvssrlrn_hu_w
        (insInfoFlags)(0), // INS_xvssrlrn_wu_d
        (insInfoFlags)(0), // INS_xvssrarn_bu_h
        (insInfoFlags)(0), // INS_xvssrarn_hu_w
        (insInfoFlags)(0), // INS_xvssrarn_wu_d
        (insInfoFlags)(0), // INS_xvbitclr_b
        (insInfoFlags)(0), // INS_xvbitclr_h
        (insInfoFlags)(0), // INS_xvbitclr_w
        (insInfoFlags)(0), // INS_xvbitclr_d
        (insInfoFlags)(0), // INS_xvbitset_b
        (insInfoFlags)(0), // INS_xvbitset_h
        (insInfoFlags)(0), // INS_xvbitset_w
        (insInfoFlags)(0), // INS_xvbitset_d
        (insInfoFlags)(0), // INS_xvbitrev_b
        (insInfoFlags)(0), // INS_xvbitrev_h
        (insInfoFlags)(0), // INS_xvbitrev_w
        (insInfoFlags)(0), // INS_xvbitrev_d
        (insInfoFlags)(0), // INS_xvpackev_b
        (insInfoFlags)(0), // INS_xvpackev_h
        (insInfoFlags)(0), // INS_xvpackev_w
        (insInfoFlags)(0), // INS_xvpackev_d
        (insInfoFlags)(0), // INS_xvpackod_b
        (insInfoFlags)(0), // INS_xvpackod_h
        (insInfoFlags)(0), // INS_xvpackod_w
        (insInfoFlags)(0), // INS_xvpackod_d
        (insInfoFlags)(0), // INS_xvilvl_b
        (insInfoFlags)(0), // INS_xvilvl_h
        (insInfoFlags)(0), // INS_xvilvl_w
        (insInfoFlags)(0), // INS_xvilvl_d
        (insInfoFlags)(0), // INS_xvilvh_b
        (insInfoFlags)(0), // INS_xvilvh_h
        (insInfoFlags)(0), // INS_xvilvh_w
        (insInfoFlags)(0), // INS_xvilvh_d
        (insInfoFlags)(0), // INS_xvpickev_b
        (insInfoFlags)(0), // INS_xvpickev_h
        (insInfoFlags)(0), // INS_xvpickev_w
        (insInfoFlags)(0), // INS_xvpickev_d
        (insInfoFlags)(0), // INS_xvpickod_b
        (insInfoFlags)(0), // INS_xvpickod_h
        (insInfoFlags)(0), // INS_xvpickod_w
        (insInfoFlags)(0), // INS_xvpickod_d
        (insInfoFlags)(0), // INS_xvand_v
        (insInfoFlags)(0), // INS_xvor_v
        (insInfoFlags)(0), // INS_xvxor_v
        (insInfoFlags)(0), // INS_xvnor_v
        (insInfoFlags)(0), // INS_xvandn_v
        (insInfoFlags)(0), // INS_xvorn_v
        (insInfoFlags)(0), // INS_xvfrstp_b
        (insInfoFlags)(0), // INS_xvfrstp_h
        (insInfoFlags)(0), // INS_xvadd_q
        (insInfoFlags)(0), // INS_xvsub_q
        (insInfoFlags)(0), // INS_xvsigncov_b
        (insInfoFlags)(0), // INS_xvsigncov_h
        (insInfoFlags)(0), // INS_xvsigncov_w
        (insInfoFlags)(0), // INS_xvsigncov_d
        (insInfoFlags)(0), // INS_xvfadd_s
        (insInfoFlags)(0), // INS_xvfadd_d
        (insInfoFlags)(0), // INS_xvfsub_s
        (insInfoFlags)(0), // INS_xvfsub_d
        (insInfoFlags)(0), // INS_xvfmul_s
        (insInfoFlags)(0), // INS_xvfmul_d
        (insInfoFlags)(0), // INS_xvfdiv_s
        (insInfoFlags)(0), // INS_xvfdiv_d
        (insInfoFlags)(0), // INS_xvfmax_s
        (insInfoFlags)(0), // INS_xvfmax_d
        (insInfoFlags)(0), // INS_xvfmin_s
        (insInfoFlags)(0), // INS_xvfmin_d
        (insInfoFlags)(0), // INS_xvfmaxa_s
        (insInfoFlags)(0), // INS_xvfmaxa_d
        (insInfoFlags)(0), // INS_xvfmina_s
        (insInfoFlags)(0), // INS_xvfmina_d
        (insInfoFlags)(0), // INS_xvfcvt_h_s
        (insInfoFlags)(0), // INS_xvfcvt_s_d
        (insInfoFlags)(0), // INS_xvffint_s_l
        (insInfoFlags)(0), // INS_xvftint_w_d
        (insInfoFlags)(0), // INS_xvftintrm_w_d
        (insInfoFlags)(0), // INS_xvftintrp_w_d
        (insInfoFlags)(0), // INS_xvftintrz_w_d
        (insInfoFlags)(0), // INS_xvftintrne_w_d
        (insInfoFlags)(0), // INS_xvshuf_h
        (insInfoFlags)(0), // INS_xvshuf_w
        (insInfoFlags)(0), // INS_xvshuf_d
        (insInfoFlags)(0), // INS_xvperm_w
        (insInfoFlags)(0), // INS_xvreplgr2vr_b
        (insInfoFlags)(0), // INS_xvreplgr2vr_h
        (insInfoFlags)(0), // INS_xvreplgr2vr_w
        (insInfoFlags)(0), // INS_xvreplgr2vr_d
        (insInfoFlags)(0), // INS_xvclo_b
        (insInfoFlags)(0), // INS_xvclo_h
        (insInfoFlags)(0), // INS_xvclo_w
        (insInfoFlags)(0), // INS_xvclo_d
        (insInfoFlags)(0), // INS_xvclz_b
        (insInfoFlags)(0), // INS_xvclz_h
        (insInfoFlags)(0), // INS_xvclz_w
        (insInfoFlags)(0), // INS_xvclz_d
        (insInfoFlags)(0), // INS_xvpcnt_b
        (insInfoFlags)(0), // INS_xvpcnt_h
        (insInfoFlags)(0), // INS_xvpcnt_w
        (insInfoFlags)(0), // INS_xvpcnt_d
        (insInfoFlags)(0), // INS_xvneg_b
        (insInfoFlags)(0), // INS_xvneg_h
        (insInfoFlags)(0), // INS_xvneg_w
        (insInfoFlags)(0), // INS_xvneg_d
        (insInfoFlags)(0), // INS_xvmskltz_b
        (insInfoFlags)(0), // INS_xvmskltz_h
        (insInfoFlags)(0), // INS_xvmskltz_w
        (insInfoFlags)(0), // INS_xvmskltz_d
        (insInfoFlags)(0), // INS_xvmskgez_b
        (insInfoFlags)(0), // INS_xvmsknz_b
        (insInfoFlags)(0), // INS_xvflogb_s
        (insInfoFlags)(0), // INS_xvflogb_d
        (insInfoFlags)(0), // INS_xvfclass_s
        (insInfoFlags)(0), // INS_xvfclass_d
        (insInfoFlags)(0), // INS_xvfsqrt_s
        (insInfoFlags)(0), // INS_xvfsqrt_d
        (insInfoFlags)(0), // INS_xvfrecip_s
        (insInfoFlags)(0), // INS_xvfrecip_d
        (insInfoFlags)(0), // INS_xvfrsqrt_s
        (insInfoFlags)(0), // INS_xvfrsqrt_d
        (insInfoFlags)(0), // INS_xvfrecipe_s
        (insInfoFlags)(0), // INS_xvfrecipe_d
        (insInfoFlags)(0), // INS_xvfrsqrte_s
        (insInfoFlags)(0), // INS_xvfrsqrte_d
        (insInfoFlags)(0), // INS_xvfrint_s
        (insInfoFlags)(0), // INS_xvfrint_d
        (insInfoFlags)(0), // INS_xvfrintrm_s
        (insInfoFlags)(0), // INS_xvfrintrm_d
        (insInfoFlags)(0), // INS_xvfrintrp_s
        (insInfoFlags)(0), // INS_xvfrintrp_d
        (insInfoFlags)(0), // INS_xvfrintrz_s
        (insInfoFlags)(0), // INS_xvfrintrz_d
        (insInfoFlags)(0), // INS_xvfrintrne_s
        (insInfoFlags)(0), // INS_xvfrintrne_d
        (insInfoFlags)(0), // INS_xvfcvtl_s_h
        (insInfoFlags)(0), // INS_xvfcvth_s_h
        (insInfoFlags)(0), // INS_xvfcvtl_d_s
        (insInfoFlags)(0), // INS_xvfcvth_d_s
        (insInfoFlags)(0), // INS_xvffint_s_w
        (insInfoFlags)(0), // INS_xvffint_s_wu
        (insInfoFlags)(0), // INS_xvffint_d_l
        (insInfoFlags)(0), // INS_xvffint_d_lu
        (insInfoFlags)(0), // INS_xvffintl_d_w
        (insInfoFlags)(0), // INS_xvffinth_d_w
        (insInfoFlags)(0), // INS_xvftint_w_s
        (insInfoFlags)(0), // INS_xvftint_l_d
        (insInfoFlags)(0), // INS_xvftintrm_w_s
        (insInfoFlags)(0), // INS_xvftintrm_l_d
        (insInfoFlags)(0), // INS_xvftintrp_w_s
        (insInfoFlags)(0), // INS_xvftintrp_l_d
        (insInfoFlags)(0), // INS_xvftintrz_w_s
        (insInfoFlags)(0), // INS_xvftintrz_l_d
        (insInfoFlags)(0), // INS_xvftintrne_w_s
        (insInfoFlags)(0), // INS_xvftintrne_l_d
        (insInfoFlags)(0), // INS_xvftint_wu_s
        (insInfoFlags)(0), // INS_xvftint_lu_d
        (insInfoFlags)(0), // INS_xvftintrz_wu_s
        (insInfoFlags)(0), // INS_xvftintrz_lu_d
        (insInfoFlags)(0), // INS_xvftintl_l_s
        (insInfoFlags)(0), // INS_xvftinth_l_s
        (insInfoFlags)(0), // INS_xvftintrml_l_s
        (insInfoFlags)(0), // INS_xvftintrmh_l_s
        (insInfoFlags)(0), // INS_xvftintrpl_l_s
        (insInfoFlags)(0), // INS_xvftintrph_l_s
        (insInfoFlags)(0), // INS_xvftintrzl_l_s
        (insInfoFlags)(0), // INS_xvftintrzh_l_s
        (insInfoFlags)(0), // INS_xvftintrnel_l_s
        (insInfoFlags)(0), // INS_xvftintrneh_l_s
        (insInfoFlags)(0), // INS_xvexth_h_b
        (insInfoFlags)(0), // INS_xvexth_w_h
        (insInfoFlags)(0), // INS_xvexth_d_w
        (insInfoFlags)(0), // INS_xvexth_q_d
        (insInfoFlags)(0), // INS_xvexth_hu_bu
        (insInfoFlags)(0), // INS_xvexth_wu_hu
        (insInfoFlags)(0), // INS_xvexth_du_wu
        (insInfoFlags)(0), // INS_xvexth_qu_du
        (insInfoFlags)(0), // INS_vext2xv_h_b
        (insInfoFlags)(0), // INS_vext2xv_w_b
        (insInfoFlags)(0), // INS_vext2xv_d_b
        (insInfoFlags)(0), // INS_vext2xv_w_h
        (insInfoFlags)(0), // INS_vext2xv_d_h
        (insInfoFlags)(0), // INS_vext2xv_d_w
        (insInfoFlags)(0), // INS_vext2xv_hu_bu
        (insInfoFlags)(0), // INS_vext2xv_wu_bu
        (insInfoFlags)(0), // INS_vext2xv_du_bu
        (insInfoFlags)(0), // INS_vext2xv_wu_hu
        (insInfoFlags)(0), // INS_vext2xv_du_hu
        (insInfoFlags)(0), // INS_vext2xv_du_wu
        (insInfoFlags)(0), // INS_xvreplve0_b
        (insInfoFlags)(0), // INS_xvreplve0_h
        (insInfoFlags)(0), // INS_xvreplve0_w
        (insInfoFlags)(0), // INS_xvreplve0_d
        (insInfoFlags)(0), // INS_xvreplve0_q
        (insInfoFlags)(0), // INS_xvextl_q_d
        (insInfoFlags)(0), // INS_xvextl_qu_du
        (insInfoFlags)(ST), // INS_xvstelm_b
        (insInfoFlags)(ST), // INS_xvstelm_h
        (insInfoFlags)(ST), // INS_xvstelm_w
        (insInfoFlags)(ST), // INS_xvstelm_d
        (insInfoFlags)(0), // INS_xvseqi_b
        (insInfoFlags)(0), // INS_xvseqi_h
        (insInfoFlags)(0), // INS_xvseqi_w
        (insInfoFlags)(0), // INS_xvseqi_d
        (insInfoFlags)(0), // INS_xvslei_b
        (insInfoFlags)(0), // INS_xvslei_h
        (insInfoFlags)(0), // INS_xvslei_w
        (insInfoFlags)(0), // INS_xvslei_d
        (insInfoFlags)(0), // INS_xvslti_b
        (insInfoFlags)(0), // INS_xvslti_h
        (insInfoFlags)(0), // INS_xvslti_w
        (insInfoFlags)(0), // INS_xvslti_d
        (insInfoFlags)(0), // INS_xvmaxi_b
        (insInfoFlags)(0), // INS_xvmaxi_h
        (insInfoFlags)(0), // INS_xvmaxi_w
        (insInfoFlags)(0), // INS_xvmaxi_d
        (insInfoFlags)(0), // INS_xvmini_b
        (insInfoFlags)(0), // INS_xvmini_h
        (insInfoFlags)(0), // INS_xvmini_w
        (insInfoFlags)(0), // INS_xvmini_d
        (insInfoFlags)(LD), // INS_xvldrepl_d
        (insInfoFlags)(LD), // INS_xvldrepl_w
        (insInfoFlags)(LD), // INS_xvldrepl_h
        (insInfoFlags)(LD), // INS_xvld
        (insInfoFlags)(LD), // INS_xvldrepl_b
        (insInfoFlags)(ST), // INS_xvst
        (insInfoFlags)(0), // INS_xvrepl128vei_d
        (insInfoFlags)(0), // INS_xvinsve0_d
        (insInfoFlags)(0), // INS_xvrepl128vei_w
        (insInfoFlags)(0), // INS_xvpickve_d
        (insInfoFlags)(0), // INS_xvinsgr2vr_d
        (insInfoFlags)(0), // INS_xvpickve2gr_d
        (insInfoFlags)(0), // INS_xvpickve2gr_du
        (insInfoFlags)(0), // INS_xvpickve2gr_w
        (insInfoFlags)(0), // INS_xvpickve2gr_wu
        (insInfoFlags)(0), // INS_xvslli_b
        (insInfoFlags)(0), // INS_xvsrli_b
        (insInfoFlags)(0), // INS_xvsrai_b
        (insInfoFlags)(0), // INS_xvrotri_b
        (insInfoFlags)(0), // INS_xvsrlri_b
        (insInfoFlags)(0), // INS_xvsrari_b
        (insInfoFlags)(0), // INS_xvsllwil_h_b
        (insInfoFlags)(0), // INS_xvsllwil_hu_bu
        (insInfoFlags)(0), // INS_xvsrlni_b_h
        (insInfoFlags)(0), // INS_xvsrlrni_b_h
        (insInfoFlags)(0), // INS_xvssrlni_b_h
        (insInfoFlags)(0), // INS_xvssrlni_bu_h
        (insInfoFlags)(0), // INS_xvssrlrni_b_h
        (insInfoFlags)(0), // INS_xvssrlrni_bu_h
        (insInfoFlags)(0), // INS_xvsrani_b_h
        (insInfoFlags)(0), // INS_xvsrarni_b_h
        (insInfoFlags)(0), // INS_xvssrani_b_h
        (insInfoFlags)(0), // INS_xvssrani_bu_h
        (insInfoFlags)(0), // INS_xvssrarni_b_h
        (insInfoFlags)(0), // INS_xvssrarni_bu_h
        (insInfoFlags)(0), // INS_xvinsve0_w
        (insInfoFlags)(0), // INS_xvrepl128vei_h
        (insInfoFlags)(0), // INS_xvpickve_w
        (insInfoFlags)(0), // INS_xvbitclri_b
        (insInfoFlags)(0), // INS_xvbitseti_b
        (insInfoFlags)(0), // INS_xvbitrevi_b
        (insInfoFlags)(0), // INS_xvinsgr2vr_w
        (insInfoFlags)(0), // INS_xvsat_b
        (insInfoFlags)(0), // INS_xvsat_bu
        (insInfoFlags)(0), // INS_xvslli_h
        (insInfoFlags)(0), // INS_xvsrli_h
        (insInfoFlags)(0), // INS_xvsrai_h
        (insInfoFlags)(0), // INS_xvrotri_h
        (insInfoFlags)(0), // INS_xvrepl128vei_b
        (insInfoFlags)(0), // INS_xvsrlri_h
        (insInfoFlags)(0), // INS_xvsrari_h
        (insInfoFlags)(0), // INS_xvsllwil_w_h
        (insInfoFlags)(0), // INS_xvsllwil_wu_hu
        (insInfoFlags)(0), // INS_xvbitclri_h
        (insInfoFlags)(0), // INS_xvbitseti_h
        (insInfoFlags)(0), // INS_xvbitrevi_h
        (insInfoFlags)(0), // INS_xvsat_h
        (insInfoFlags)(0), // INS_xvsat_hu
        (insInfoFlags)(0), // INS_xvslei_bu
        (insInfoFlags)(0), // INS_xvslei_hu
        (insInfoFlags)(0), // INS_xvslei_wu
        (insInfoFlags)(0), // INS_xvslei_du
        (insInfoFlags)(0), // INS_xvslti_bu
        (insInfoFlags)(0), // INS_xvslti_hu
        (insInfoFlags)(0), // INS_xvslti_wu
        (insInfoFlags)(0), // INS_xvslti_du
        (insInfoFlags)(0), // INS_xvaddi_bu
        (insInfoFlags)(0), // INS_xvaddi_hu
        (insInfoFlags)(0), // INS_xvaddi_wu
        (insInfoFlags)(0), // INS_xvaddi_du
        (insInfoFlags)(0), // INS_xvsubi_bu
        (insInfoFlags)(0), // INS_xvsubi_hu
        (insInfoFlags)(0), // INS_xvsubi_wu
        (insInfoFlags)(0), // INS_xvsubi_du
        (insInfoFlags)(0), // INS_xvmaxi_bu
        (insInfoFlags)(0), // INS_xvmaxi_hu
        (insInfoFlags)(0), // INS_xvmaxi_wu
        (insInfoFlags)(0), // INS_xvmaxi_du
        (insInfoFlags)(0), // INS_xvmini_bu
        (insInfoFlags)(0), // INS_xvmini_hu
        (insInfoFlags)(0), // INS_xvmini_wu
        (insInfoFlags)(0), // INS_xvmini_du
        (insInfoFlags)(0), // INS_xvfrstpi_b
        (insInfoFlags)(0), // INS_xvfrstpi_h
        (insInfoFlags)(0), // INS_xvslli_w
        (insInfoFlags)(0), // INS_xvsrli_w
        (insInfoFlags)(0), // INS_xvsrai_w
        (insInfoFlags)(0), // INS_xvbsll_v
        (insInfoFlags)(0), // INS_xvbsrl_v
        (insInfoFlags)(0), // INS_xvsrlri_w
        (insInfoFlags)(0), // INS_xvsrari_w
        (insInfoFlags)(0), // INS_xvrotri_w
        (insInfoFlags)(0), // INS_xvsllwil_d_w
        (insInfoFlags)(0), // INS_xvsllwil_du_wu
        (insInfoFlags)(0), // INS_xvsrlni_h_w
        (insInfoFlags)(0), // INS_xvsrlrni_h_w
        (insInfoFlags)(0), // INS_xvssrlni_h_w
        (insInfoFlags)(0), // INS_xvssrlni_hu_w
        (insInfoFlags)(0), // INS_xvssrlrni_h_w
        (insInfoFlags)(0), // INS_xvssrlrni_hu_w
        (insInfoFlags)(0), // INS_xvsrani_h_w
        (insInfoFlags)(0), // INS_xvsrarni_h_w
        (insInfoFlags)(0), // INS_xvssrani_h_w
        (insInfoFlags)(0), // INS_xvssrani_hu_w
        (insInfoFlags)(0), // INS_xvssrarni_h_w
        (insInfoFlags)(0), // INS_xvssrarni_hu_w
        (insInfoFlags)(0), // INS_xvbitclri_w
        (insInfoFlags)(0), // INS_xvbitseti_w
        (insInfoFlags)(0), // INS_xvbitrevi_w
        (insInfoFlags)(0), // INS_xvsat_w
        (insInfoFlags)(0), // INS_xvsat_wu
        (insInfoFlags)(0), // INS_xvslli_d
        (insInfoFlags)(0), // INS_xvsrli_d
        (insInfoFlags)(0), // INS_xvsrai_d
        (insInfoFlags)(0), // INS_xvrotri_d
        (insInfoFlags)(0), // INS_xvsrlri_d
        (insInfoFlags)(0), // INS_xvsrari_d
        (insInfoFlags)(0), // INS_xvsrlni_w_d
        (insInfoFlags)(0), // INS_xvsrlrni_w_d
        (insInfoFlags)(0), // INS_xvssrlni_w_d
        (insInfoFlags)(0), // INS_xvssrlni_wu_d
        (insInfoFlags)(0), // INS_xvssrlrni_w_d
        (insInfoFlags)(0), // INS_xvssrlrni_wu_d
        (insInfoFlags)(0), // INS_xvsrani_w_d
        (insInfoFlags)(0), // INS_xvsrarni_w_d
        (insInfoFlags)(0), // INS_xvssrani_w_d
        (insInfoFlags)(0), // INS_xvssrani_wu_d
        (insInfoFlags)(0), // INS_xvssrarni_w_d
        (insInfoFlags)(0), // INS_xvssrarni_wu_d
        (insInfoFlags)(0), // INS_xvbitclri_d
        (insInfoFlags)(0), // INS_xvbitseti_d
        (insInfoFlags)(0), // INS_xvbitrevi_d
        (insInfoFlags)(0), // INS_xvsat_d
        (insInfoFlags)(0), // INS_xvsat_du
        (insInfoFlags)(0), // INS_xvsrlni_d_q
        (insInfoFlags)(0), // INS_xvsrlrni_d_q
        (insInfoFlags)(0), // INS_xvssrlni_d_q
        (insInfoFlags)(0), // INS_xvssrlni_du_q
        (insInfoFlags)(0), // INS_xvssrlrni_d_q
        (insInfoFlags)(0), // INS_xvssrlrni_du_q
        (insInfoFlags)(0), // INS_xvsrani_d_q
        (insInfoFlags)(0), // INS_xvsrarni_d_q
        (insInfoFlags)(0), // INS_xvssrani_d_q
        (insInfoFlags)(0), // INS_xvssrani_du_q
        (insInfoFlags)(0), // INS_xvssrarni_d_q
        (insInfoFlags)(0), // INS_xvssrarni_du_q
        (insInfoFlags)(0), // INS_xvextrins_d
        (insInfoFlags)(0), // INS_xvextrins_w
        (insInfoFlags)(0), // INS_xvextrins_h
        (insInfoFlags)(0), // INS_xvextrins_b
        (insInfoFlags)(0), // INS_xvshuf4i_b
        (insInfoFlags)(0), // INS_xvshuf4i_h
        (insInfoFlags)(0), // INS_xvshuf4i_w
        (insInfoFlags)(0), // INS_xvshuf4i_d
        (insInfoFlags)(0), // INS_xvbitseli_b
        (insInfoFlags)(0), // INS_xvandi_b
        (insInfoFlags)(0), // INS_xvori_b
        (insInfoFlags)(0), // INS_xvxori_b
        (insInfoFlags)(0), // INS_xvnori_b
        (insInfoFlags)(0), // INS_xvpermi_w
        (insInfoFlags)(0), // INS_xvpermi_d
        (insInfoFlags)(0), // INS_xvpermi_q
        (insInfoFlags)(LD), // INS_xvldi
        (insInfoFlags)(0), // INS_xvseteqz_v
        (insInfoFlags)(0), // INS_xvsetnez_v
        (insInfoFlags)(0), // INS_xvsetanyeqz_b
        (insInfoFlags)(0), // INS_xvsetanyeqz_h
        (insInfoFlags)(0), // INS_xvsetanyeqz_w
        (insInfoFlags)(0), // INS_xvsetanyeqz_d
        (insInfoFlags)(0), // INS_xvsetallnez_b
        (insInfoFlags)(0), // INS_xvsetallnez_h
        (insInfoFlags)(0), // INS_xvsetallnez_w
        (insInfoFlags)(0), // INS_xvsetallnez_d
        (insInfoFlags)0, // INS_lea is zero-initialized in native instInfo[INS_count].
    ];

    private static ReadOnlySpan<insInfoFlags> s_instInfoRISCV64 => [
        (insInfoFlags)(0), // INS_invalid
        (insInfoFlags)(0), // INS_nop
        (insInfoFlags)(0), // INS_mov
        (insInfoFlags)(0), // INS_sext_w
        (insInfoFlags)(0), // INS_not
        (insInfoFlags)(0), // INS_lui
        (insInfoFlags)(0), // INS_auipc
        (insInfoFlags)(0), // INS_addi
        (insInfoFlags)(0), // INS_slti
        (insInfoFlags)(0), // INS_sltiu
        (insInfoFlags)(0), // INS_xori
        (insInfoFlags)(0), // INS_ori
        (insInfoFlags)(0), // INS_andi
        (insInfoFlags)(0), // INS_slli
        (insInfoFlags)(0), // INS_srli
        (insInfoFlags)(0), // INS_srai
        (insInfoFlags)(0), // INS_add
        (insInfoFlags)(0), // INS_sub
        (insInfoFlags)(0), // INS_sll
        (insInfoFlags)(0), // INS_slt
        (insInfoFlags)(0), // INS_sltu
        (insInfoFlags)(0), // INS_xor
        (insInfoFlags)(0), // INS_srl
        (insInfoFlags)(0), // INS_sra
        (insInfoFlags)(0), // INS_or
        (insInfoFlags)(0), // INS_and
        (insInfoFlags)(0), // INS_fence
        (insInfoFlags)(0), // INS_fence_i
        (insInfoFlags)(0), // INS_csrrw
        (insInfoFlags)(0), // INS_csrrs
        (insInfoFlags)(0), // INS_csrrc
        (insInfoFlags)(0), // INS_csrrwi
        (insInfoFlags)(0), // INS_csrrsi
        (insInfoFlags)(0), // INS_csrrci
        (insInfoFlags)(0), // INS_ecall
        (insInfoFlags)(0), // INS_ebreak
        (insInfoFlags)(LD), // INS_lb
        (insInfoFlags)(LD), // INS_lh
        (insInfoFlags)(LD), // INS_lw
        (insInfoFlags)(LD), // INS_lbu
        (insInfoFlags)(LD), // INS_lhu
        (insInfoFlags)(ST), // INS_sb
        (insInfoFlags)(ST), // INS_sh
        (insInfoFlags)(ST), // INS_sw
        (insInfoFlags)(0), // INS_jal
        (insInfoFlags)(0), // INS_j
        (insInfoFlags)(0), // INS_beqz
        (insInfoFlags)(0), // INS_bnez
        (insInfoFlags)(0), // INS_jalr
        (insInfoFlags)(0), // INS_beq
        (insInfoFlags)(0), // INS_bne
        (insInfoFlags)(0), // INS_blt
        (insInfoFlags)(0), // INS_bge
        (insInfoFlags)(0), // INS_bltu
        (insInfoFlags)(0), // INS_bgeu
        (insInfoFlags)(0), // INS_addiw
        (insInfoFlags)(0), // INS_slliw
        (insInfoFlags)(0), // INS_srliw
        (insInfoFlags)(0), // INS_sraiw
        (insInfoFlags)(0), // INS_addw
        (insInfoFlags)(0), // INS_subw
        (insInfoFlags)(0), // INS_sllw
        (insInfoFlags)(0), // INS_srlw
        (insInfoFlags)(0), // INS_sraw
        (insInfoFlags)(LD), // INS_lwu
        (insInfoFlags)(LD), // INS_ld
        (insInfoFlags)(ST), // INS_sd
        (insInfoFlags)(0), // INS_mul
        (insInfoFlags)(0), // INS_mulh
        (insInfoFlags)(0), // INS_mulhsu
        (insInfoFlags)(0), // INS_mulhu
        (insInfoFlags)(0), // INS_div
        (insInfoFlags)(0), // INS_divu
        (insInfoFlags)(0), // INS_rem
        (insInfoFlags)(0), // INS_remu
        (insInfoFlags)(0), // INS_mulw
        (insInfoFlags)(0), // INS_divw
        (insInfoFlags)(0), // INS_divuw
        (insInfoFlags)(0), // INS_remw
        (insInfoFlags)(0), // INS_remuw
        (insInfoFlags)(0), // INS_fmadd_s
        (insInfoFlags)(0), // INS_fmsub_s
        (insInfoFlags)(0), // INS_fnmsub_s
        (insInfoFlags)(0), // INS_fnmadd_s
        (insInfoFlags)(0), // INS_fadd_s
        (insInfoFlags)(0), // INS_fsub_s
        (insInfoFlags)(0), // INS_fmul_s
        (insInfoFlags)(0), // INS_fdiv_s
        (insInfoFlags)(0), // INS_fsqrt_s
        (insInfoFlags)(0), // INS_fsgnj_s
        (insInfoFlags)(0), // INS_fsgnjn_s
        (insInfoFlags)(0), // INS_fsgnjx_s
        (insInfoFlags)(0), // INS_fmin_s
        (insInfoFlags)(0), // INS_fmax_s
        (insInfoFlags)(0), // INS_fcvt_w_s
        (insInfoFlags)(0), // INS_fcvt_wu_s
        (insInfoFlags)(0), // INS_fmv_x_w
        (insInfoFlags)(0), // INS_feq_s
        (insInfoFlags)(0), // INS_flt_s
        (insInfoFlags)(0), // INS_fle_s
        (insInfoFlags)(0), // INS_fclass_s
        (insInfoFlags)(0), // INS_fcvt_s_w
        (insInfoFlags)(0), // INS_fcvt_s_wu
        (insInfoFlags)(0), // INS_fmv_w_x
        (insInfoFlags)(0), // INS_fmadd_d
        (insInfoFlags)(0), // INS_fmsub_d
        (insInfoFlags)(0), // INS_fnmsub_d
        (insInfoFlags)(0), // INS_fnmadd_d
        (insInfoFlags)(0), // INS_fadd_d
        (insInfoFlags)(0), // INS_fsub_d
        (insInfoFlags)(0), // INS_fmul_d
        (insInfoFlags)(0), // INS_fdiv_d
        (insInfoFlags)(0), // INS_fsqrt_d
        (insInfoFlags)(0), // INS_fsgnj_d
        (insInfoFlags)(0), // INS_fsgnjn_d
        (insInfoFlags)(0), // INS_fsgnjx_d
        (insInfoFlags)(0), // INS_fmin_d
        (insInfoFlags)(0), // INS_fmax_d
        (insInfoFlags)(0), // INS_fcvt_s_d
        (insInfoFlags)(0), // INS_fcvt_d_s
        (insInfoFlags)(0), // INS_feq_d
        (insInfoFlags)(0), // INS_flt_d
        (insInfoFlags)(0), // INS_fle_d
        (insInfoFlags)(0), // INS_fclass_d
        (insInfoFlags)(0), // INS_fcvt_w_d
        (insInfoFlags)(0), // INS_fcvt_wu_d
        (insInfoFlags)(0), // INS_fcvt_d_w
        (insInfoFlags)(0), // INS_fcvt_d_wu
        (insInfoFlags)(LD), // INS_flw
        (insInfoFlags)(ST), // INS_fsw
        (insInfoFlags)(LD), // INS_fld
        (insInfoFlags)(ST), // INS_fsd
        (insInfoFlags)(0), // INS_fcvt_l_s
        (insInfoFlags)(0), // INS_fcvt_lu_s
        (insInfoFlags)(0), // INS_fcvt_s_l
        (insInfoFlags)(0), // INS_fcvt_s_lu
        (insInfoFlags)(0), // INS_fcvt_l_d
        (insInfoFlags)(0), // INS_fcvt_lu_d
        (insInfoFlags)(0), // INS_fmv_x_d
        (insInfoFlags)(0), // INS_fcvt_d_l
        (insInfoFlags)(0), // INS_fcvt_d_lu
        (insInfoFlags)(0), // INS_fmv_d_x
        (insInfoFlags)(0), // INS_lr_w
        (insInfoFlags)(0), // INS_lr_d
        (insInfoFlags)(0), // INS_sc_w
        (insInfoFlags)(0), // INS_sc_d
        (insInfoFlags)(0), // INS_amoswap_w
        (insInfoFlags)(0), // INS_amoswap_d
        (insInfoFlags)(0), // INS_amoadd_w
        (insInfoFlags)(0), // INS_amoadd_d
        (insInfoFlags)(0), // INS_amoxor_w
        (insInfoFlags)(0), // INS_amoxor_d
        (insInfoFlags)(0), // INS_amoand_w
        (insInfoFlags)(0), // INS_amoand_d
        (insInfoFlags)(0), // INS_amoor_w
        (insInfoFlags)(0), // INS_amoor_d
        (insInfoFlags)(0), // INS_amomin_w
        (insInfoFlags)(0), // INS_amomin_d
        (insInfoFlags)(0), // INS_amomax_w
        (insInfoFlags)(0), // INS_amomax_d
        (insInfoFlags)(0), // INS_amominu_w
        (insInfoFlags)(0), // INS_amominu_d
        (insInfoFlags)(0), // INS_amomaxu_w
        (insInfoFlags)(0), // INS_amomaxu_d
        (insInfoFlags)(0), // INS_clz
        (insInfoFlags)(0), // INS_clzw
        (insInfoFlags)(0), // INS_ctz
        (insInfoFlags)(0), // INS_ctzw
        (insInfoFlags)(0), // INS_cpop
        (insInfoFlags)(0), // INS_cpopw
        (insInfoFlags)(0), // INS_sext_b
        (insInfoFlags)(0), // INS_sext_h
        (insInfoFlags)(0), // INS_zext_h
        (insInfoFlags)(0), // INS_rev8
        (insInfoFlags)(0), // INS_rol
        (insInfoFlags)(0), // INS_rolw
        (insInfoFlags)(0), // INS_ror
        (insInfoFlags)(0), // INS_rorw
        (insInfoFlags)(0), // INS_xnor
        (insInfoFlags)(0), // INS_orn
        (insInfoFlags)(0), // INS_andn
        (insInfoFlags)(0), // INS_min
        (insInfoFlags)(0), // INS_minu
        (insInfoFlags)(0), // INS_max
        (insInfoFlags)(0), // INS_maxu
        (insInfoFlags)(0), // INS_rori
        (insInfoFlags)(0), // INS_roriw
        (insInfoFlags)(0), // INS_sh1add
        (insInfoFlags)(0), // INS_sh2add
        (insInfoFlags)(0), // INS_sh3add
        (insInfoFlags)(0), // INS_add_uw
        (insInfoFlags)(0), // INS_sh1add_uw
        (insInfoFlags)(0), // INS_sh2add_uw
        (insInfoFlags)(0), // INS_sh3add_uw
        (insInfoFlags)(0), // INS_slli_uw
        (insInfoFlags)(0), // INS_bset
        (insInfoFlags)(0), // INS_bclr
        (insInfoFlags)(0), // INS_bext
        (insInfoFlags)(0), // INS_binv
        (insInfoFlags)(0), // INS_bseti
        (insInfoFlags)(0), // INS_bclri
        (insInfoFlags)(0), // INS_bexti
        (insInfoFlags)(0), // INS_binvi
        (insInfoFlags)(0), // INS_czero_eqz
        (insInfoFlags)(0), // INS_czero_nez
        (insInfoFlags)(0), // INS_c_mv
        (insInfoFlags)(0), // INS_c_add
        (insInfoFlags)(0), // INS_c_and
        (insInfoFlags)(0), // INS_c_or
        (insInfoFlags)(0), // INS_c_xor
        (insInfoFlags)(0), // INS_c_sub
        (insInfoFlags)(0), // INS_c_addw
        (insInfoFlags)(0), // INS_c_subw
        (insInfoFlags)0, // INS_lea is zero-initialized in native instInfo[INS_count].
    ];

    private static ReadOnlySpan<insInfoFlags> s_instInfoWASM => [
        (insInfoFlags)(0), // INS_invalid
        (insInfoFlags)(0), // INS_unreachable
        (insInfoFlags)(0), // INS_label
        (insInfoFlags)(0), // INS_catch_ref
        (insInfoFlags)(0), // INS_local_cnt
        (insInfoFlags)(0), // INS_local_decl
        (insInfoFlags)(0), // INS_code_size
        (insInfoFlags)(0), // INS_nop
        (insInfoFlags)(0), // INS_block
        (insInfoFlags)(0), // INS_loop
        (insInfoFlags)(0), // INS_if
        (insInfoFlags)(0), // INS_else
        (insInfoFlags)(0), // INS_throw_ref
        (insInfoFlags)(0), // INS_end
        (insInfoFlags)(0), // INS_br
        (insInfoFlags)(0), // INS_br_if
        (insInfoFlags)(0), // INS_br_table
        (insInfoFlags)(0), // INS_return
        (insInfoFlags)(0), // INS_call
        (insInfoFlags)(0), // INS_call_indirect
        (insInfoFlags)(0), // INS_return_call
        (insInfoFlags)(0), // INS_return_call_indirect
        (insInfoFlags)(0), // INS_call_funclet
        (insInfoFlags)(0), // INS_drop
        (insInfoFlags)(0), // INS_try_table
        (insInfoFlags)(0), // INS_local_get
        (insInfoFlags)(0), // INS_local_set
        (insInfoFlags)(0), // INS_local_tee
        (insInfoFlags)(0), // INS_global_get
        (insInfoFlags)(0), // INS_global_set
        (insInfoFlags)(0), // INS_i32_load
        (insInfoFlags)(0), // INS_i64_load
        (insInfoFlags)(0), // INS_f32_load
        (insInfoFlags)(0), // INS_f64_load
        (insInfoFlags)(0), // INS_i32_load8_s
        (insInfoFlags)(0), // INS_i32_load8_u
        (insInfoFlags)(0), // INS_i32_load16_s
        (insInfoFlags)(0), // INS_i32_load16_u
        (insInfoFlags)(0), // INS_i64_load8_s
        (insInfoFlags)(0), // INS_i64_load8_u
        (insInfoFlags)(0), // INS_i64_load16_s
        (insInfoFlags)(0), // INS_i64_load16_u
        (insInfoFlags)(0), // INS_i64_load32_s
        (insInfoFlags)(0), // INS_i64_load32_u
        (insInfoFlags)(0), // INS_i32_store
        (insInfoFlags)(0), // INS_i64_store
        (insInfoFlags)(0), // INS_f32_store
        (insInfoFlags)(0), // INS_f64_store
        (insInfoFlags)(0), // INS_i32_store8
        (insInfoFlags)(0), // INS_i32_store16
        (insInfoFlags)(0), // INS_i32_const
        (insInfoFlags)(0), // INS_i32_const_address
        (insInfoFlags)(0), // INS_i32_const_funcptr
        (insInfoFlags)(0), // INS_i32_const_funcletptr
        (insInfoFlags)(0), // INS_i32_const_frameidentity
        (insInfoFlags)(0), // INS_i32_const_dataoffs
        (insInfoFlags)(0), // INS_i64_const
        (insInfoFlags)(0), // INS_f32_const
        (insInfoFlags)(0), // INS_f64_const
        (insInfoFlags)(0), // INS_i32_eqz
        (insInfoFlags)(0), // INS_i32_eq
        (insInfoFlags)(0), // INS_i32_ne
        (insInfoFlags)(0), // INS_i32_lt_s
        (insInfoFlags)(0), // INS_i32_lt_u
        (insInfoFlags)(0), // INS_i32_gt_s
        (insInfoFlags)(0), // INS_i32_gt_u
        (insInfoFlags)(0), // INS_i32_le_s
        (insInfoFlags)(0), // INS_i32_le_u
        (insInfoFlags)(0), // INS_i32_ge_s
        (insInfoFlags)(0), // INS_i32_ge_u
        (insInfoFlags)(0), // INS_i64_eqz
        (insInfoFlags)(0), // INS_i64_eq
        (insInfoFlags)(0), // INS_i64_ne
        (insInfoFlags)(0), // INS_i64_lt_s
        (insInfoFlags)(0), // INS_i64_lt_u
        (insInfoFlags)(0), // INS_i64_gt_s
        (insInfoFlags)(0), // INS_i64_gt_u
        (insInfoFlags)(0), // INS_i64_le_s
        (insInfoFlags)(0), // INS_i64_le_u
        (insInfoFlags)(0), // INS_i64_ge_s
        (insInfoFlags)(0), // INS_i64_ge_u
        (insInfoFlags)(0), // INS_f32_eq
        (insInfoFlags)(0), // INS_f32_ne
        (insInfoFlags)(0), // INS_f32_lt
        (insInfoFlags)(0), // INS_f32_gt
        (insInfoFlags)(0), // INS_f32_le
        (insInfoFlags)(0), // INS_f32_ge
        (insInfoFlags)(0), // INS_f64_eq
        (insInfoFlags)(0), // INS_f64_ne
        (insInfoFlags)(0), // INS_f64_lt
        (insInfoFlags)(0), // INS_f64_gt
        (insInfoFlags)(0), // INS_f64_le
        (insInfoFlags)(0), // INS_f64_ge
        (insInfoFlags)(0), // INS_i32_clz
        (insInfoFlags)(0), // INS_i32_ctz
        (insInfoFlags)(0), // INS_i32_popcnt
        (insInfoFlags)(0), // INS_i32_add
        (insInfoFlags)(0), // INS_i32_sub
        (insInfoFlags)(0), // INS_i32_mul
        (insInfoFlags)(0), // INS_i32_div_s
        (insInfoFlags)(0), // INS_i32_div_u
        (insInfoFlags)(0), // INS_i32_rem_s
        (insInfoFlags)(0), // INS_i32_rem_u
        (insInfoFlags)(0), // INS_i32_and
        (insInfoFlags)(0), // INS_i32_or
        (insInfoFlags)(0), // INS_i32_xor
        (insInfoFlags)(0), // INS_i32_shl
        (insInfoFlags)(0), // INS_i32_shr_s
        (insInfoFlags)(0), // INS_i32_shr_u
        (insInfoFlags)(0), // INS_i32_rotl
        (insInfoFlags)(0), // INS_i32_rotr
        (insInfoFlags)(0), // INS_i64_clz
        (insInfoFlags)(0), // INS_i64_ctz
        (insInfoFlags)(0), // INS_i64_popcnt
        (insInfoFlags)(0), // INS_i64_add
        (insInfoFlags)(0), // INS_i64_sub
        (insInfoFlags)(0), // INS_i64_mul
        (insInfoFlags)(0), // INS_i64_div_s
        (insInfoFlags)(0), // INS_i64_div_u
        (insInfoFlags)(0), // INS_i64_rem_s
        (insInfoFlags)(0), // INS_i64_rem_u
        (insInfoFlags)(0), // INS_i64_and
        (insInfoFlags)(0), // INS_i64_or
        (insInfoFlags)(0), // INS_i64_xor
        (insInfoFlags)(0), // INS_i64_shl
        (insInfoFlags)(0), // INS_i64_shr_s
        (insInfoFlags)(0), // INS_i64_shr_u
        (insInfoFlags)(0), // INS_i64_rotl
        (insInfoFlags)(0), // INS_i64_rotr
        (insInfoFlags)(0), // INS_f32_abs
        (insInfoFlags)(0), // INS_f32_neg
        (insInfoFlags)(0), // INS_f32_ceil
        (insInfoFlags)(0), // INS_f32_floor
        (insInfoFlags)(0), // INS_f32_trunc
        (insInfoFlags)(0), // INS_f32_nearest
        (insInfoFlags)(0), // INS_f32_sqrt
        (insInfoFlags)(0), // INS_f32_add
        (insInfoFlags)(0), // INS_f32_sub
        (insInfoFlags)(0), // INS_f32_mul
        (insInfoFlags)(0), // INS_f32_div
        (insInfoFlags)(0), // INS_f32_min
        (insInfoFlags)(0), // INS_f32_max
        (insInfoFlags)(0), // INS_f32_copysign
        (insInfoFlags)(0), // INS_f64_abs
        (insInfoFlags)(0), // INS_f64_neg
        (insInfoFlags)(0), // INS_f64_ceil
        (insInfoFlags)(0), // INS_f64_floor
        (insInfoFlags)(0), // INS_f64_trunc
        (insInfoFlags)(0), // INS_f64_nearest
        (insInfoFlags)(0), // INS_f64_sqrt
        (insInfoFlags)(0), // INS_f64_add
        (insInfoFlags)(0), // INS_f64_sub
        (insInfoFlags)(0), // INS_f64_mul
        (insInfoFlags)(0), // INS_f64_div
        (insInfoFlags)(0), // INS_f64_min
        (insInfoFlags)(0), // INS_f64_max
        (insInfoFlags)(0), // INS_f64_copysign
        (insInfoFlags)(0), // INS_i32_wrap_i64
        (insInfoFlags)(0), // INS_i32_trunc_s_f32
        (insInfoFlags)(0), // INS_i32_trunc_u_f32
        (insInfoFlags)(0), // INS_i32_trunc_s_f64
        (insInfoFlags)(0), // INS_i32_trunc_u_f64
        (insInfoFlags)(0), // INS_i64_extend_s_i32
        (insInfoFlags)(0), // INS_i64_extend_u_i32
        (insInfoFlags)(0), // INS_i64_trunc_s_f32
        (insInfoFlags)(0), // INS_i64_trunc_u_f32
        (insInfoFlags)(0), // INS_i64_trunc_s_f64
        (insInfoFlags)(0), // INS_i64_trunc_u_f64
        (insInfoFlags)(0), // INS_f32_convert_s_i32
        (insInfoFlags)(0), // INS_f32_convert_u_i32
        (insInfoFlags)(0), // INS_f32_convert_s_i64
        (insInfoFlags)(0), // INS_f32_convert_u_i64
        (insInfoFlags)(0), // INS_f32_demote_f64
        (insInfoFlags)(0), // INS_f64_convert_s_i32
        (insInfoFlags)(0), // INS_f64_convert_u_i32
        (insInfoFlags)(0), // INS_f64_convert_s_i64
        (insInfoFlags)(0), // INS_f64_convert_u_i64
        (insInfoFlags)(0), // INS_f64_promote_f32
        (insInfoFlags)(0), // INS_i32_reinterpret_f32
        (insInfoFlags)(0), // INS_i64_reinterpret_f64
        (insInfoFlags)(0), // INS_f32_reinterpret_i32
        (insInfoFlags)(0), // INS_f64_reinterpret_i64
        (insInfoFlags)(0), // INS_i32_extend8_s
        (insInfoFlags)(0), // INS_i32_extend16_s
        (insInfoFlags)(0), // INS_i64_extend8_s
        (insInfoFlags)(0), // INS_i64_extend16_s
        (insInfoFlags)(0), // INS_i64_extend32_s
        (insInfoFlags)(0), // INS_i32_trunc_sat_f32_s
        (insInfoFlags)(0), // INS_i32_trunc_sat_f32_u
        (insInfoFlags)(0), // INS_i32_trunc_sat_f64_s
        (insInfoFlags)(0), // INS_i32_trunc_sat_f64_u
        (insInfoFlags)(0), // INS_i64_trunc_sat_f32_s
        (insInfoFlags)(0), // INS_i64_trunc_sat_f32_u
        (insInfoFlags)(0), // INS_i64_trunc_sat_f64_s
        (insInfoFlags)(0), // INS_i64_trunc_sat_f64_u
        (insInfoFlags)(0), // INS_memory_copy
        (insInfoFlags)(0), // INS_memory_fill
        (insInfoFlags)(0), // INS_v128_load
        (insInfoFlags)(0), // INS_v128_load8x8_s
        (insInfoFlags)(0), // INS_v128_load8x8_u
        (insInfoFlags)(0), // INS_v128_load16x4_s
        (insInfoFlags)(0), // INS_v128_load16x4_u
        (insInfoFlags)(0), // INS_v128_load32x2_s
        (insInfoFlags)(0), // INS_v128_load32x2_u
        (insInfoFlags)(0), // INS_v128_load8_splat
        (insInfoFlags)(0), // INS_v128_load16_splat
        (insInfoFlags)(0), // INS_v128_load32_splat
        (insInfoFlags)(0), // INS_v128_load64_splat
        (insInfoFlags)(0), // INS_v128_store
        (insInfoFlags)(0), // INS_v128_const
        (insInfoFlags)(0), // INS_i8x16_shuffle
        (insInfoFlags)(0), // INS_i8x16_swizzle
        (insInfoFlags)(0), // INS_i8x16_splat
        (insInfoFlags)(0), // INS_i16x8_splat
        (insInfoFlags)(0), // INS_i32x4_splat
        (insInfoFlags)(0), // INS_i64x2_splat
        (insInfoFlags)(0), // INS_f32x4_splat
        (insInfoFlags)(0), // INS_f64x2_splat
        (insInfoFlags)(2), // INS_i8x16_extract_lane_s
        (insInfoFlags)(2), // INS_i8x16_extract_lane_u
        (insInfoFlags)(2), // INS_i8x16_replace_lane
        (insInfoFlags)(4), // INS_i16x8_extract_lane_s
        (insInfoFlags)(4), // INS_i16x8_extract_lane_u
        (insInfoFlags)(4), // INS_i16x8_replace_lane
        (insInfoFlags)(8), // INS_i32x4_extract_lane
        (insInfoFlags)(8), // INS_i32x4_replace_lane
        (insInfoFlags)(16), // INS_i64x2_extract_lane
        (insInfoFlags)(16), // INS_i64x2_replace_lane
        (insInfoFlags)(8), // INS_f32x4_extract_lane
        (insInfoFlags)(8), // INS_f32x4_replace_lane
        (insInfoFlags)(16), // INS_f64x2_extract_lane
        (insInfoFlags)(16), // INS_f64x2_replace_lane
        (insInfoFlags)(0), // INS_i8x16_eq
        (insInfoFlags)(0), // INS_i8x16_ne
        (insInfoFlags)(0), // INS_i8x16_lt_s
        (insInfoFlags)(0), // INS_i8x16_lt_u
        (insInfoFlags)(0), // INS_i8x16_gt_s
        (insInfoFlags)(0), // INS_i8x16_gt_u
        (insInfoFlags)(0), // INS_i8x16_le_s
        (insInfoFlags)(0), // INS_i8x16_le_u
        (insInfoFlags)(0), // INS_i8x16_ge_s
        (insInfoFlags)(0), // INS_i8x16_ge_u
        (insInfoFlags)(0), // INS_i16x8_eq
        (insInfoFlags)(0), // INS_i16x8_ne
        (insInfoFlags)(0), // INS_i16x8_lt_s
        (insInfoFlags)(0), // INS_i16x8_lt_u
        (insInfoFlags)(0), // INS_i16x8_gt_s
        (insInfoFlags)(0), // INS_i16x8_gt_u
        (insInfoFlags)(0), // INS_i16x8_le_s
        (insInfoFlags)(0), // INS_i16x8_le_u
        (insInfoFlags)(0), // INS_i16x8_ge_s
        (insInfoFlags)(0), // INS_i16x8_ge_u
        (insInfoFlags)(0), // INS_i32x4_eq
        (insInfoFlags)(0), // INS_i32x4_ne
        (insInfoFlags)(0), // INS_i32x4_lt_s
        (insInfoFlags)(0), // INS_i32x4_lt_u
        (insInfoFlags)(0), // INS_i32x4_gt_s
        (insInfoFlags)(0), // INS_i32x4_gt_u
        (insInfoFlags)(0), // INS_i32x4_le_s
        (insInfoFlags)(0), // INS_i32x4_le_u
        (insInfoFlags)(0), // INS_i32x4_ge_s
        (insInfoFlags)(0), // INS_i32x4_ge_u
        (insInfoFlags)(0), // INS_i64x2_eq
        (insInfoFlags)(0), // INS_i64x2_ne
        (insInfoFlags)(0), // INS_i64x2_lt_s
        (insInfoFlags)(0), // INS_i64x2_gt_s
        (insInfoFlags)(0), // INS_i64x2_le_s
        (insInfoFlags)(0), // INS_i64x2_ge_s
        (insInfoFlags)(0), // INS_f32x4_eq
        (insInfoFlags)(0), // INS_f32x4_ne
        (insInfoFlags)(0), // INS_f32x4_lt
        (insInfoFlags)(0), // INS_f32x4_gt
        (insInfoFlags)(0), // INS_f32x4_le
        (insInfoFlags)(0), // INS_f32x4_ge
        (insInfoFlags)(0), // INS_f64x2_eq
        (insInfoFlags)(0), // INS_f64x2_ne
        (insInfoFlags)(0), // INS_f64x2_lt
        (insInfoFlags)(0), // INS_f64x2_gt
        (insInfoFlags)(0), // INS_f64x2_le
        (insInfoFlags)(0), // INS_f64x2_ge
        (insInfoFlags)(0), // INS_v128_not
        (insInfoFlags)(0), // INS_v128_and
        (insInfoFlags)(0), // INS_v128_andnot
        (insInfoFlags)(0), // INS_v128_or
        (insInfoFlags)(0), // INS_v128_xor
        (insInfoFlags)(0), // INS_v128_bitselect
        (insInfoFlags)(0), // INS_v128_any_true
        (insInfoFlags)(2), // INS_v128_load8_lane
        (insInfoFlags)(4), // INS_v128_load16_lane
        (insInfoFlags)(8), // INS_v128_load32_lane
        (insInfoFlags)(16), // INS_v128_load64_lane
        (insInfoFlags)(2), // INS_v128_store8_lane
        (insInfoFlags)(4), // INS_v128_store16_lane
        (insInfoFlags)(8), // INS_v128_store32_lane
        (insInfoFlags)(16), // INS_v128_store64_lane
        (insInfoFlags)(0), // INS_v128_load32_zero
        (insInfoFlags)(0), // INS_v128_load64_zero
        (insInfoFlags)(0), // INS_f32x4_demote_f64x2_zero
        (insInfoFlags)(0), // INS_f64x2_promote_low_f32x4
        (insInfoFlags)(0), // INS_i8x16_abs
        (insInfoFlags)(0), // INS_i8x16_neg
        (insInfoFlags)(0), // INS_i8x16_popcnt
        (insInfoFlags)(0), // INS_i8x16_all_true
        (insInfoFlags)(0), // INS_i8x16_bitmask
        (insInfoFlags)(0), // INS_i8x16_narrow_i16x8_s
        (insInfoFlags)(0), // INS_i8x16_narrow_i16x8_u
        (insInfoFlags)(0), // INS_f32x4_ceil
        (insInfoFlags)(0), // INS_f32x4_floor
        (insInfoFlags)(0), // INS_f32x4_trunc
        (insInfoFlags)(0), // INS_f32x4_nearest
        (insInfoFlags)(0), // INS_i8x16_shl
        (insInfoFlags)(0), // INS_i8x16_shr_s
        (insInfoFlags)(0), // INS_i8x16_shr_u
        (insInfoFlags)(0), // INS_i8x16_add
        (insInfoFlags)(0), // INS_i8x16_add_sat_s
        (insInfoFlags)(0), // INS_i8x16_add_sat_u
        (insInfoFlags)(0), // INS_i8x16_sub
        (insInfoFlags)(0), // INS_i8x16_sub_sat_s
        (insInfoFlags)(0), // INS_i8x16_sub_sat_u
        (insInfoFlags)(0), // INS_f64x2_ceil
        (insInfoFlags)(0), // INS_f64x2_floor
        (insInfoFlags)(0), // INS_i8x16_min_s
        (insInfoFlags)(0), // INS_i8x16_min_u
        (insInfoFlags)(0), // INS_i8x16_max_s
        (insInfoFlags)(0), // INS_i8x16_max_u
        (insInfoFlags)(0), // INS_f64x2_trunc
        (insInfoFlags)(0), // INS_i8x16_avgr_u
        (insInfoFlags)(0), // INS_i16x8_extadd_pairwise_s_i8x16
        (insInfoFlags)(0), // INS_i16x8_extadd_pairwise_u_i8x16
        (insInfoFlags)(0), // INS_i32x4_extadd_pairwise_s_i16x8
        (insInfoFlags)(0), // INS_i32x4_extadd_pairwise_u_i16x8
        (insInfoFlags)(0), // INS_i16x8_abs
        (insInfoFlags)(0), // INS_i16x8_neg
        (insInfoFlags)(0), // INS_i16x8_q15mulr_sat_s
        (insInfoFlags)(0), // INS_i16x8_all_true
        (insInfoFlags)(0), // INS_i16x8_bitmask
        (insInfoFlags)(0), // INS_i16x8_narrow_i32x4_s
        (insInfoFlags)(0), // INS_i16x8_narrow_i32x4_u
        (insInfoFlags)(0), // INS_i16x8_extend_low_s_i8x16
        (insInfoFlags)(0), // INS_i16x8_extend_high_s_i8x16
        (insInfoFlags)(0), // INS_i16x8_extend_low_u_i8x16
        (insInfoFlags)(0), // INS_i16x8_extend_high_u_i8x16
        (insInfoFlags)(0), // INS_i16x8_shl
        (insInfoFlags)(0), // INS_i16x8_shr_s
        (insInfoFlags)(0), // INS_i16x8_shr_u
        (insInfoFlags)(0), // INS_i16x8_add
        (insInfoFlags)(0), // INS_i16x8_add_sat_s
        (insInfoFlags)(0), // INS_i16x8_add_sat_u
        (insInfoFlags)(0), // INS_i16x8_sub
        (insInfoFlags)(0), // INS_i16x8_sub_sat_s
        (insInfoFlags)(0), // INS_i16x8_sub_sat_u
        (insInfoFlags)(0), // INS_f64x2_nearest
        (insInfoFlags)(0), // INS_i16x8_mul
        (insInfoFlags)(0), // INS_i16x8_min_s
        (insInfoFlags)(0), // INS_i16x8_min_u
        (insInfoFlags)(0), // INS_i16x8_max_s
        (insInfoFlags)(0), // INS_i16x8_max_u
        (insInfoFlags)(0), // INS_i16x8_avgr_u
        (insInfoFlags)(0), // INS_i16x8_extmul_low_s_i8x16
        (insInfoFlags)(0), // INS_i16x8_extmul_high_s_i8x16
        (insInfoFlags)(0), // INS_i16x8_extmul_low_u_i8x16
        (insInfoFlags)(0), // INS_i16x8_extmul_high_u_i8x16
        (insInfoFlags)(0), // INS_i32x4_abs
        (insInfoFlags)(0), // INS_i32x4_neg
        (insInfoFlags)(0), // INS_i32x4_all_true
        (insInfoFlags)(0), // INS_i32x4_bitmask
        (insInfoFlags)(0), // INS_i32x4_extend_low_s_i16x8
        (insInfoFlags)(0), // INS_i32x4_extend_high_s_i16x8
        (insInfoFlags)(0), // INS_i32x4_extend_low_u_i16x8
        (insInfoFlags)(0), // INS_i32x4_extend_high_u_i16x8
        (insInfoFlags)(0), // INS_i32x4_shl
        (insInfoFlags)(0), // INS_i32x4_shr_s
        (insInfoFlags)(0), // INS_i32x4_shr_u
        (insInfoFlags)(0), // INS_i32x4_add
        (insInfoFlags)(0), // INS_i32x4_sub
        (insInfoFlags)(0), // INS_i32x4_mul
        (insInfoFlags)(0), // INS_i32x4_min_s
        (insInfoFlags)(0), // INS_i32x4_min_u
        (insInfoFlags)(0), // INS_i32x4_max_s
        (insInfoFlags)(0), // INS_i32x4_max_u
        (insInfoFlags)(0), // INS_i32x4_dot_i16x8_s
        (insInfoFlags)(0), // INS_i32x4_extmul_low_s_i16x8
        (insInfoFlags)(0), // INS_i32x4_extmul_high_s_i16x8
        (insInfoFlags)(0), // INS_i32x4_extmul_low_u_i16x8
        (insInfoFlags)(0), // INS_i32x4_extmul_high_u_i16x8
        (insInfoFlags)(0), // INS_i64x2_abs
        (insInfoFlags)(0), // INS_i64x2_neg
        (insInfoFlags)(0), // INS_i64x2_all_true
        (insInfoFlags)(0), // INS_i64x2_bitmask
        (insInfoFlags)(0), // INS_i64x2_extend_low_s_i32x4
        (insInfoFlags)(0), // INS_i64x2_extend_high_s_i32x4
        (insInfoFlags)(0), // INS_i64x2_extend_low_u_i32x4
        (insInfoFlags)(0), // INS_i64x2_extend_high_u_i32x4
        (insInfoFlags)(0), // INS_i64x2_shl
        (insInfoFlags)(0), // INS_i64x2_shr_s
        (insInfoFlags)(0), // INS_i64x2_shr_u
        (insInfoFlags)(0), // INS_i64x2_add
        (insInfoFlags)(0), // INS_i64x2_sub
        (insInfoFlags)(0), // INS_i64x2_mul
        (insInfoFlags)(0), // INS_i64x2_extmul_low_s_i32x4
        (insInfoFlags)(0), // INS_i64x2_extmul_high_s_i32x4
        (insInfoFlags)(0), // INS_i64x2_extmul_low_u_i32x4
        (insInfoFlags)(0), // INS_i64x2_extmul_high_u_i32x4
        (insInfoFlags)(0), // INS_f32x4_abs
        (insInfoFlags)(0), // INS_f32x4_neg
        (insInfoFlags)(0), // INS_f32x4_sqrt
        (insInfoFlags)(0), // INS_f32x4_add
        (insInfoFlags)(0), // INS_f32x4_sub
        (insInfoFlags)(0), // INS_f32x4_mul
        (insInfoFlags)(0), // INS_f32x4_div
        (insInfoFlags)(0), // INS_f32x4_min
        (insInfoFlags)(0), // INS_f32x4_max
        (insInfoFlags)(0), // INS_f32x4_pmin
        (insInfoFlags)(0), // INS_f32x4_pmax
        (insInfoFlags)(0), // INS_f64x2_abs
        (insInfoFlags)(0), // INS_f64x2_neg
        (insInfoFlags)(0), // INS_f64x2_sqrt
        (insInfoFlags)(0), // INS_f64x2_add
        (insInfoFlags)(0), // INS_f64x2_sub
        (insInfoFlags)(0), // INS_f64x2_mul
        (insInfoFlags)(0), // INS_f64x2_div
        (insInfoFlags)(0), // INS_f64x2_min
        (insInfoFlags)(0), // INS_f64x2_max
        (insInfoFlags)(0), // INS_f64x2_pmin
        (insInfoFlags)(0), // INS_f64x2_pmax
        (insInfoFlags)(0), // INS_i32x4_trunc_sat_s_f32x4
        (insInfoFlags)(0), // INS_i32x4_trunc_sat_u_f32x4
        (insInfoFlags)(0), // INS_f32x4_convert_s_i32x4
        (insInfoFlags)(0), // INS_f32x4_convert_u_i32x4
        (insInfoFlags)(0), // INS_i32x4_trunc_sat_s_f64x2_zero
        (insInfoFlags)(0), // INS_i32x4_trunc_sat_u_f64x2_zero
        (insInfoFlags)(0), // INS_f64x2_convert_low_s_i32x4
        (insInfoFlags)(0), // INS_f64x2_convert_low_u_i32x4
        (insInfoFlags)(0), // INS_i8x16_relaxed_swizzle
        (insInfoFlags)(0), // INS_i32x4_relaxed_trunc_f32x4_s
        (insInfoFlags)(0), // INS_i32x4_relaxed_trunc_f32x4_u
        (insInfoFlags)(0), // INS_i32x4_relaxed_trunc_f64x2_s_zero
        (insInfoFlags)(0), // INS_i32x4_relaxed_trunc_f64x2_u_zero
        (insInfoFlags)(0), // INS_f32x4_relaxed_madd
        (insInfoFlags)(0), // INS_f32x4_relaxed_nmadd
        (insInfoFlags)(0), // INS_f64x2_relaxed_madd
        (insInfoFlags)(0), // INS_f64x2_relaxed_nmadd
        (insInfoFlags)(0), // INS_i8x16_relaxed_laneselect
        (insInfoFlags)(0), // INS_i16x8_relaxed_laneselect
        (insInfoFlags)(0), // INS_i32x4_relaxed_laneselect
        (insInfoFlags)(0), // INS_i64x2_relaxed_laneselect
        (insInfoFlags)(0), // INS_f32x4_relaxed_min
        (insInfoFlags)(0), // INS_f32x4_relaxed_max
        (insInfoFlags)(0), // INS_f64x2_relaxed_min
        (insInfoFlags)(0), // INS_f64x2_relaxed_max
        (insInfoFlags)(0), // INS_i16x8_relaxed_q15mulr_s
        (insInfoFlags)(0), // INS_i16x8_relaxed_dot_i8x16_i7x16_s
        (insInfoFlags)(0), // INS_i32x4_relaxed_dot_i8x16_i7x16_add_s
    ];

    internal static ReadOnlySpan<insInfoFlags> instInfo
    {
        get
        {
            if (Configuration.TARGET_X86)
            {
                return s_instInfoX86;
            }

            if (Configuration.TARGET_AMD64)
            {
                return s_instInfoAMD64;
            }

            if (Configuration.TARGET_ARM)
            {
                return s_instInfoARM;
            }

            if (Configuration.TARGET_ARM64)
            {
                return s_instInfoARM64;
            }

            if (Configuration.TARGET_LOONGARCH64)
            {
                return s_instInfoLOONGARCH64;
            }

            if (Configuration.TARGET_RISCV64)
            {
                return s_instInfoRISCV64;
            }

            if (Configuration.TARGET_WASM)
            {
                return s_instInfoWASM;
            }

            FatalJitException.ThrowFatalJitException(
                CORJIT_SKIPPED,
                FatalJitErrorCode.InstructionMetadataIsNotAvailableForTheConfiguredTarget);
            return default;
        }
    }
}