// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
#if TARGET_AMD64
using OpcodeNative = System.UInt64;
#else
using OpcodeNative = System.UInt32;
#endif

namespace RyuJitSharp;

public static partial class Globals
{
#if TARGET_XARCH
    private static ReadOnlySpan<uint> insCodes => [
#if TARGET_XARCH
#if !TARGET_XARCH
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0x0030FE)), // INS_push
        unchecked((uint)(0x00008E)), // INS_pop
        unchecked((uint)(0x0030FE)), // INS_push_hide
        unchecked((uint)(0x00008E)), // INS_pop_hide
        unchecked((uint)(0x0030FF)), // INS_push2
        unchecked((uint)(0x00008F)), // INS_pop2
        unchecked((uint)(0x0000FE)), // INS_inc
        unchecked((uint)(0x0000FE)), // INS_inc_l
        unchecked((uint)(0x0008FE)), // INS_dec
        unchecked((uint)(0x0008FE)), // INS_dec_l
        unchecked((uint)(0x0F00C8)), // INS_bswap
        unchecked((uint)(0x000000)), // INS_add
        unchecked((uint)(0x000008)), // INS_or
        unchecked((uint)(0x000010)), // INS_adc
        unchecked((uint)(0x000018)), // INS_sbb
        unchecked((uint)(0x000020)), // INS_and
        unchecked((uint)(0x000028)), // INS_sub
        unchecked((uint)(0x000028)), // INS_sub_hide
        unchecked((uint)(0x000030)), // INS_xor
        unchecked((uint)(0x000038)), // INS_cmp
        unchecked((uint)(0x000084)), // INS_test
        unchecked((uint)(0x000088)), // INS_mov
        unchecked((uint)(BAD_CODE)), // INS_lea
        unchecked((uint)(0x0F00A3)), // INS_bt
        unchecked((uint)(0x0F00AB)), // INS_bts
        unchecked((uint)(0x0F00B3)), // INS_btr
        unchecked((uint)(0x0F00BB)), // INS_btc
        unchecked((uint)(BAD_CODE)), // INS_bsr
        unchecked((uint)(BAD_CODE)), // INS_bsf
        unchecked((uint)(BAD_CODE)), // INS_movsx
#if TARGET_AMD64
        unchecked((uint)(BAD_CODE)), // INS_movsxd
#endif
        unchecked((uint)(BAD_CODE)), // INS_movzx
        unchecked((uint)(BAD_CODE)), // INS_cmovo
        unchecked((uint)(BAD_CODE)), // INS_cmovno
        unchecked((uint)(BAD_CODE)), // INS_cmovb
        unchecked((uint)(BAD_CODE)), // INS_cmovae
        unchecked((uint)(BAD_CODE)), // INS_cmove
        unchecked((uint)(BAD_CODE)), // INS_cmovne
        unchecked((uint)(BAD_CODE)), // INS_cmovbe
        unchecked((uint)(BAD_CODE)), // INS_cmova
        unchecked((uint)(BAD_CODE)), // INS_cmovs
        unchecked((uint)(BAD_CODE)), // INS_cmovns
        unchecked((uint)(BAD_CODE)), // INS_cmovp
        unchecked((uint)(BAD_CODE)), // INS_cmovnp
        unchecked((uint)(BAD_CODE)), // INS_cmovl
        unchecked((uint)(BAD_CODE)), // INS_cmovge
        unchecked((uint)(BAD_CODE)), // INS_cmovle
        unchecked((uint)(BAD_CODE)), // INS_cmovg
        unchecked((uint)(0x000086)), // INS_xchg
        unchecked((uint)(0x0F00AC)), // INS_imul
        unchecked((uint)(BAD_CODE)), // INS_imul_AX
        unchecked((uint)(BAD_CODE)), // INS_imul_CX
        unchecked((uint)(BAD_CODE)), // INS_imul_DX
        unchecked((uint)(BAD_CODE)), // INS_imul_BX
        unchecked((uint)(BAD_CODE)), // INS_imul_SP
        unchecked((uint)(BAD_CODE)), // INS_imul_BP
        unchecked((uint)(BAD_CODE)), // INS_imul_SI
        unchecked((uint)(BAD_CODE)), // INS_imul_DI
#if TARGET_AMD64
        unchecked((uint)(BAD_CODE)), // INS_imul_08
        unchecked((uint)(BAD_CODE)), // INS_imul_09
        unchecked((uint)(BAD_CODE)), // INS_imul_10
        unchecked((uint)(BAD_CODE)), // INS_imul_11
        unchecked((uint)(BAD_CODE)), // INS_imul_12
        unchecked((uint)(BAD_CODE)), // INS_imul_13
        unchecked((uint)(BAD_CODE)), // INS_imul_14
        unchecked((uint)(BAD_CODE)), // INS_imul_15
        unchecked((uint)(BAD_CODE)), // INS_imul_16
        unchecked((uint)(BAD_CODE)), // INS_imul_17
        unchecked((uint)(BAD_CODE)), // INS_imul_18
        unchecked((uint)(BAD_CODE)), // INS_imul_19
        unchecked((uint)(BAD_CODE)), // INS_imul_20
        unchecked((uint)(BAD_CODE)), // INS_imul_21
        unchecked((uint)(BAD_CODE)), // INS_imul_22
        unchecked((uint)(BAD_CODE)), // INS_imul_23
        unchecked((uint)(BAD_CODE)), // INS_imul_24
        unchecked((uint)(BAD_CODE)), // INS_imul_25
        unchecked((uint)(BAD_CODE)), // INS_imul_26
        unchecked((uint)(BAD_CODE)), // INS_imul_27
        unchecked((uint)(BAD_CODE)), // INS_imul_28
        unchecked((uint)(BAD_CODE)), // INS_imul_29
        unchecked((uint)(BAD_CODE)), // INS_imul_30
        unchecked((uint)(BAD_CODE)), // INS_imul_31
#endif
        unchecked((uint)(BAD_CODE)), // INS_addpd
        unchecked((uint)(BAD_CODE)), // INS_addps
        unchecked((uint)(BAD_CODE)), // INS_addsd
        unchecked((uint)(BAD_CODE)), // INS_addss
        unchecked((uint)(BAD_CODE)), // INS_addsubpd
        unchecked((uint)(BAD_CODE)), // INS_addsubps
        unchecked((uint)(BAD_CODE)), // INS_andnpd
        unchecked((uint)(BAD_CODE)), // INS_andnps
        unchecked((uint)(BAD_CODE)), // INS_andpd
        unchecked((uint)(BAD_CODE)), // INS_andps
        unchecked((uint)(BAD_CODE)), // INS_blendpd
        unchecked((uint)(BAD_CODE)), // INS_blendps
        unchecked((uint)(BAD_CODE)), // INS_blendvpd
        unchecked((uint)(BAD_CODE)), // INS_blendvps
        unchecked((uint)(BAD_CODE)), // INS_cmppd
        unchecked((uint)(BAD_CODE)), // INS_cmpps
        unchecked((uint)(BAD_CODE)), // INS_cmpsd
        unchecked((uint)(BAD_CODE)), // INS_cmpss
        unchecked((uint)(BAD_CODE)), // INS_comisd
        unchecked((uint)(BAD_CODE)), // INS_comiss
        unchecked((uint)(BAD_CODE)), // INS_cvtdq2pd
        unchecked((uint)(BAD_CODE)), // INS_cvtdq2ps
        unchecked((uint)(BAD_CODE)), // INS_cvtpd2dq
        unchecked((uint)(BAD_CODE)), // INS_cvtpd2ps
        unchecked((uint)(BAD_CODE)), // INS_cvtps2dq
        unchecked((uint)(BAD_CODE)), // INS_cvtps2pd
        unchecked((uint)(BAD_CODE)), // INS_cvtsd2si32
        unchecked((uint)(BAD_CODE)), // INS_cvtsd2si64
        unchecked((uint)(BAD_CODE)), // INS_cvtsd2ss
        unchecked((uint)(BAD_CODE)), // INS_cvtsi2sd32
        unchecked((uint)(BAD_CODE)), // INS_cvtsi2sd64
        unchecked((uint)(BAD_CODE)), // INS_cvtsi2ss32
        unchecked((uint)(BAD_CODE)), // INS_cvtsi2ss64
        unchecked((uint)(BAD_CODE)), // INS_cvtss2sd
        unchecked((uint)(BAD_CODE)), // INS_cvtss2si32
        unchecked((uint)(BAD_CODE)), // INS_cvtss2si64
        unchecked((uint)(BAD_CODE)), // INS_cvttpd2dq
        unchecked((uint)(BAD_CODE)), // INS_cvttps2dq
        unchecked((uint)(BAD_CODE)), // INS_cvttsd2si32
        unchecked((uint)(BAD_CODE)), // INS_cvttsd2si64
        unchecked((uint)(BAD_CODE)), // INS_cvttss2si32
        unchecked((uint)(BAD_CODE)), // INS_cvttss2si64
        unchecked((uint)(BAD_CODE)), // INS_divpd
        unchecked((uint)(BAD_CODE)), // INS_divps
        unchecked((uint)(BAD_CODE)), // INS_divsd
        unchecked((uint)(BAD_CODE)), // INS_divss
        unchecked((uint)(BAD_CODE)), // INS_dppd
        unchecked((uint)(BAD_CODE)), // INS_dpps
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x17)))) << 8)))), // INS_extractps
        unchecked((uint)(BAD_CODE)), // INS_haddpd
        unchecked((uint)(BAD_CODE)), // INS_haddps
        unchecked((uint)(BAD_CODE)), // INS_hsubpd
        unchecked((uint)(BAD_CODE)), // INS_hsubps
        unchecked((uint)(BAD_CODE)), // INS_insertps
        unchecked((uint)(BAD_CODE)), // INS_lddqu
        unchecked((uint)(0x000FE8AE)), // INS_lfence
        unchecked((uint)(BAD_CODE)), // INS_maskmovdqu
        unchecked((uint)(BAD_CODE)), // INS_maxpd
        unchecked((uint)(BAD_CODE)), // INS_maxps
        unchecked((uint)(BAD_CODE)), // INS_maxsd
        unchecked((uint)(BAD_CODE)), // INS_maxss
        unchecked((uint)(0x000FF0AE)), // INS_mfence
        unchecked((uint)(BAD_CODE)), // INS_minpd
        unchecked((uint)(BAD_CODE)), // INS_minps
        unchecked((uint)(BAD_CODE)), // INS_minsd
        unchecked((uint)(BAD_CODE)), // INS_minss
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x29)))))), // INS_movapd
        unchecked((uint)(((((0x0f)) << 16) | (((0x29)))))), // INS_movaps
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd32
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd64
        unchecked((uint)(BAD_CODE)), // INS_movddup
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqa32
        unchecked((uint)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqu32
        unchecked((uint)(BAD_CODE)), // INS_movhlps
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x17)))))), // INS_movhpd
        unchecked((uint)(((((0x0f)) << 16) | (((0x17)))))), // INS_movhps
        unchecked((uint)(BAD_CODE)), // INS_movlhps
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x13)))))), // INS_movlpd
        unchecked((uint)(((((0x0f)) << 16) | (((0x13)))))), // INS_movlps
        unchecked((uint)(BAD_CODE)), // INS_movmskpd
        unchecked((uint)(BAD_CODE)), // INS_movmskps
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE7)))))), // INS_movntdq
        unchecked((uint)(BAD_CODE)), // INS_movntdqa
        unchecked((uint)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti32
        unchecked((uint)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti64
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2B)))))), // INS_movntpd
        unchecked((uint)(((((0x0f)) << 16) | (((0x2B)))))), // INS_movntps
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_movq
        unchecked((uint)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movsd_simd
        unchecked((uint)(BAD_CODE)), // INS_movshdup
        unchecked((uint)(BAD_CODE)), // INS_movsldup
        unchecked((uint)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movss
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movupd
        unchecked((uint)(((((0x0f)) << 16) | (((0x11)))))), // INS_movups
        unchecked((uint)(BAD_CODE)), // INS_mpsadbw
        unchecked((uint)(BAD_CODE)), // INS_mulpd
        unchecked((uint)(BAD_CODE)), // INS_mulps
        unchecked((uint)(BAD_CODE)), // INS_mulsd
        unchecked((uint)(BAD_CODE)), // INS_mulss
        unchecked((uint)(BAD_CODE)), // INS_orpd
        unchecked((uint)(BAD_CODE)), // INS_orps
        unchecked((uint)(BAD_CODE)), // INS_pabsb
        unchecked((uint)(BAD_CODE)), // INS_pabsd
        unchecked((uint)(BAD_CODE)), // INS_pabsw
        unchecked((uint)(BAD_CODE)), // INS_packssdw
        unchecked((uint)(BAD_CODE)), // INS_packsswb
        unchecked((uint)(BAD_CODE)), // INS_packusdw
        unchecked((uint)(BAD_CODE)), // INS_packuswb
        unchecked((uint)(BAD_CODE)), // INS_paddb
        unchecked((uint)(BAD_CODE)), // INS_paddd
        unchecked((uint)(BAD_CODE)), // INS_paddq
        unchecked((uint)(BAD_CODE)), // INS_paddsb
        unchecked((uint)(BAD_CODE)), // INS_paddsw
        unchecked((uint)(BAD_CODE)), // INS_paddusb
        unchecked((uint)(BAD_CODE)), // INS_paddusw
        unchecked((uint)(BAD_CODE)), // INS_paddw
        unchecked((uint)(BAD_CODE)), // INS_palignr
        unchecked((uint)(BAD_CODE)), // INS_pandd
        unchecked((uint)(BAD_CODE)), // INS_pandnd
        unchecked((uint)(BAD_CODE)), // INS_pavgb
        unchecked((uint)(BAD_CODE)), // INS_pavgw
        unchecked((uint)(BAD_CODE)), // INS_pblendvb
        unchecked((uint)(BAD_CODE)), // INS_pblendw
        unchecked((uint)(BAD_CODE)), // INS_pcmpeqb
        unchecked((uint)(BAD_CODE)), // INS_pcmpeqd
        unchecked((uint)(BAD_CODE)), // INS_pcmpeqq
        unchecked((uint)(BAD_CODE)), // INS_pcmpeqw
        unchecked((uint)(BAD_CODE)), // INS_pcmpgtb
        unchecked((uint)(BAD_CODE)), // INS_pcmpgtd
        unchecked((uint)(BAD_CODE)), // INS_pcmpgtq
        unchecked((uint)(BAD_CODE)), // INS_pcmpgtw
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x14)))) << 8)))), // INS_pextrb
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrq
        unchecked((uint)(BAD_CODE)), // INS_phaddd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x15)))) << 8)))), // INS_pextrw
        unchecked((uint)(BAD_CODE)), // INS_phaddsw
        unchecked((uint)(BAD_CODE)), // INS_phaddw
        unchecked((uint)(BAD_CODE)), // INS_phminposuw
        unchecked((uint)(BAD_CODE)), // INS_phsubd
        unchecked((uint)(BAD_CODE)), // INS_phsubsw
        unchecked((uint)(BAD_CODE)), // INS_phsubw
        unchecked((uint)(BAD_CODE)), // INS_pinsrb
        unchecked((uint)(BAD_CODE)), // INS_pinsrd
        unchecked((uint)(BAD_CODE)), // INS_pinsrq
        unchecked((uint)(BAD_CODE)), // INS_pinsrw
        unchecked((uint)(BAD_CODE)), // INS_pmaddubsw
        unchecked((uint)(BAD_CODE)), // INS_pmaddwd
        unchecked((uint)(BAD_CODE)), // INS_pmaxsb
        unchecked((uint)(BAD_CODE)), // INS_pmaxsd
        unchecked((uint)(BAD_CODE)), // INS_pmaxsw
        unchecked((uint)(BAD_CODE)), // INS_pmaxub
        unchecked((uint)(BAD_CODE)), // INS_pmaxud
        unchecked((uint)(BAD_CODE)), // INS_pmaxuw
        unchecked((uint)(BAD_CODE)), // INS_pminsb
        unchecked((uint)(BAD_CODE)), // INS_pminsd
        unchecked((uint)(BAD_CODE)), // INS_pminsw
        unchecked((uint)(BAD_CODE)), // INS_pminub
        unchecked((uint)(BAD_CODE)), // INS_pminud
        unchecked((uint)(BAD_CODE)), // INS_pminuw
        unchecked((uint)(BAD_CODE)), // INS_pmovmskb
        unchecked((uint)(BAD_CODE)), // INS_pmovsxbd
        unchecked((uint)(BAD_CODE)), // INS_pmovsxbq
        unchecked((uint)(BAD_CODE)), // INS_pmovsxbw
        unchecked((uint)(BAD_CODE)), // INS_pmovsxdq
        unchecked((uint)(BAD_CODE)), // INS_pmovsxwd
        unchecked((uint)(BAD_CODE)), // INS_pmovsxwq
        unchecked((uint)(BAD_CODE)), // INS_pmovzxbd
        unchecked((uint)(BAD_CODE)), // INS_pmovzxbq
        unchecked((uint)(BAD_CODE)), // INS_pmovzxbw
        unchecked((uint)(BAD_CODE)), // INS_pmovzxdq
        unchecked((uint)(BAD_CODE)), // INS_pmovzxwd
        unchecked((uint)(BAD_CODE)), // INS_pmovzxwq
        unchecked((uint)(BAD_CODE)), // INS_pmuldq
        unchecked((uint)(BAD_CODE)), // INS_pmulhrsw
        unchecked((uint)(BAD_CODE)), // INS_pmulhuw
        unchecked((uint)(BAD_CODE)), // INS_pmulhw
        unchecked((uint)(BAD_CODE)), // INS_pmulld
        unchecked((uint)(BAD_CODE)), // INS_pmuludq
        unchecked((uint)(BAD_CODE)), // INS_pmullw
        unchecked((uint)(BAD_CODE)), // INS_pord
        unchecked((uint)(0x000F0018)), // INS_prefetchnta
        unchecked((uint)(0x000F0818)), // INS_prefetcht0
        unchecked((uint)(0x000F1018)), // INS_prefetcht1
        unchecked((uint)(0x000F1818)), // INS_prefetcht2
        unchecked((uint)(BAD_CODE)), // INS_psadbw
        unchecked((uint)(BAD_CODE)), // INS_pshufb
        unchecked((uint)(BAD_CODE)), // INS_pshufd
        unchecked((uint)(BAD_CODE)), // INS_pshufhw
        unchecked((uint)(BAD_CODE)), // INS_pshuflw
        unchecked((uint)(BAD_CODE)), // INS_psignb
        unchecked((uint)(BAD_CODE)), // INS_psignd
        unchecked((uint)(BAD_CODE)), // INS_psignw
        unchecked((uint)(BAD_CODE)), // INS_pslld
        unchecked((uint)(BAD_CODE)), // INS_pslldq
        unchecked((uint)(BAD_CODE)), // INS_psllq
        unchecked((uint)(BAD_CODE)), // INS_psllw
        unchecked((uint)(BAD_CODE)), // INS_psrad
        unchecked((uint)(BAD_CODE)), // INS_psraw
        unchecked((uint)(BAD_CODE)), // INS_psrld
        unchecked((uint)(BAD_CODE)), // INS_psrldq
        unchecked((uint)(BAD_CODE)), // INS_psrlq
        unchecked((uint)(BAD_CODE)), // INS_psrlw
        unchecked((uint)(BAD_CODE)), // INS_psubb
        unchecked((uint)(BAD_CODE)), // INS_psubd
        unchecked((uint)(BAD_CODE)), // INS_psubq
        unchecked((uint)(BAD_CODE)), // INS_psubsb
        unchecked((uint)(BAD_CODE)), // INS_psubsw
        unchecked((uint)(BAD_CODE)), // INS_psubusb
        unchecked((uint)(BAD_CODE)), // INS_psubusw
        unchecked((uint)(BAD_CODE)), // INS_psubw
        unchecked((uint)(BAD_CODE)), // INS_ptest
        unchecked((uint)(BAD_CODE)), // INS_punpckhbw
        unchecked((uint)(BAD_CODE)), // INS_punpckhdq
        unchecked((uint)(BAD_CODE)), // INS_punpckhqdq
        unchecked((uint)(BAD_CODE)), // INS_punpckhwd
        unchecked((uint)(BAD_CODE)), // INS_punpcklbw
        unchecked((uint)(BAD_CODE)), // INS_punpckldq
        unchecked((uint)(BAD_CODE)), // INS_punpcklqdq
        unchecked((uint)(BAD_CODE)), // INS_punpcklwd
        unchecked((uint)(BAD_CODE)), // INS_pxord
        unchecked((uint)(BAD_CODE)), // INS_rcpps
        unchecked((uint)(BAD_CODE)), // INS_rcpss
        unchecked((uint)(BAD_CODE)), // INS_roundpd
        unchecked((uint)(BAD_CODE)), // INS_roundps
        unchecked((uint)(BAD_CODE)), // INS_roundsd
        unchecked((uint)(BAD_CODE)), // INS_roundss
        unchecked((uint)(BAD_CODE)), // INS_rsqrtps
        unchecked((uint)(BAD_CODE)), // INS_rsqrtss
        unchecked((uint)(0x000FF8AE)), // INS_sfence
        unchecked((uint)(BAD_CODE)), // INS_shufpd
        unchecked((uint)(BAD_CODE)), // INS_shufps
        unchecked((uint)(BAD_CODE)), // INS_sqrtpd
        unchecked((uint)(BAD_CODE)), // INS_sqrtps
        unchecked((uint)(BAD_CODE)), // INS_sqrtsd
        unchecked((uint)(BAD_CODE)), // INS_sqrtss
        unchecked((uint)(BAD_CODE)), // INS_subpd
        unchecked((uint)(BAD_CODE)), // INS_subps
        unchecked((uint)(BAD_CODE)), // INS_subsd
        unchecked((uint)(BAD_CODE)), // INS_subss
        unchecked((uint)(BAD_CODE)), // INS_ucomisd
        unchecked((uint)(BAD_CODE)), // INS_ucomiss
        unchecked((uint)(BAD_CODE)), // INS_unpckhpd
        unchecked((uint)(BAD_CODE)), // INS_unpckhps
        unchecked((uint)(BAD_CODE)), // INS_unpcklpd
        unchecked((uint)(BAD_CODE)), // INS_unpcklps
        unchecked((uint)(BAD_CODE)), // INS_xorpd
        unchecked((uint)(BAD_CODE)), // INS_xorps
        unchecked((uint)(BAD_CODE)), // INS_aesdec
        unchecked((uint)(BAD_CODE)), // INS_aesdeclast
        unchecked((uint)(BAD_CODE)), // INS_aesenc
        unchecked((uint)(BAD_CODE)), // INS_aesenclast
        unchecked((uint)(BAD_CODE)), // INS_aesimc
        unchecked((uint)(BAD_CODE)), // INS_aeskeygenassist
        unchecked((uint)(BAD_CODE)), // INS_pclmulqdq
        unchecked((uint)(BAD_CODE)), // INS_sha1msg1
        unchecked((uint)(BAD_CODE)), // INS_sha1msg2
        unchecked((uint)(BAD_CODE)), // INS_sha1nexte
        unchecked((uint)(BAD_CODE)), // INS_sha1rnds4
        unchecked((uint)(BAD_CODE)), // INS_sha256msg1
        unchecked((uint)(BAD_CODE)), // INS_sha256msg2
        unchecked((uint)(BAD_CODE)), // INS_sha256rnds2
        unchecked((uint)(BAD_CODE)), // INS_gf2p8affineinvqb
        unchecked((uint)(BAD_CODE)), // INS_gf2p8affineqb
        unchecked((uint)(BAD_CODE)), // INS_gf2p8mulb
        unchecked((uint)(BAD_CODE)), // INS_vblendvpd
        unchecked((uint)(BAD_CODE)), // INS_vblendvps
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastf32x4
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastsd
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastss
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf32x4
        unchecked((uint)(BAD_CODE)), // INS_vinsertf32x4
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2F)))) << 8)))), // INS_vmaskmovpd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2E)))) << 8)))), // INS_vmaskmovps
        unchecked((uint)(BAD_CODE)), // INS_vpblendvb
        unchecked((uint)(BAD_CODE)), // INS_vperm2f128
        unchecked((uint)(BAD_CODE)), // INS_vpermilpd
        unchecked((uint)(BAD_CODE)), // INS_vpermilpdvar
        unchecked((uint)(BAD_CODE)), // INS_vpermilps
        unchecked((uint)(BAD_CODE)), // INS_vpermilpsvar
        unchecked((uint)(BAD_CODE)), // INS_vtestpd
        unchecked((uint)(BAD_CODE)), // INS_vtestps
        unchecked((uint)(0xC577F8)), // INS_vzeroupper
        unchecked((uint)(BAD_CODE)), // INS_vbroadcasti32x4
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2ps
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1D)))) << 8)))), // INS_vcvtps2ph
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti32x4
        unchecked((uint)(BAD_CODE)), // INS_vgatherdpd
        unchecked((uint)(BAD_CODE)), // INS_vgatherdps
        unchecked((uint)(BAD_CODE)), // INS_vgatherqpd
        unchecked((uint)(BAD_CODE)), // INS_vgatherqps
        unchecked((uint)(BAD_CODE)), // INS_vinserti32x4
        unchecked((uint)(BAD_CODE)), // INS_vpblendd
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastb
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastd
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastq
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastw
        unchecked((uint)(BAD_CODE)), // INS_vperm2i128
        unchecked((uint)(BAD_CODE)), // INS_vpermd
        unchecked((uint)(BAD_CODE)), // INS_vpermpd
        unchecked((uint)(BAD_CODE)), // INS_vpermps
        unchecked((uint)(BAD_CODE)), // INS_vpermq
        unchecked((uint)(BAD_CODE)), // INS_vpgatherdd
        unchecked((uint)(BAD_CODE)), // INS_vpgatherdq
        unchecked((uint)(BAD_CODE)), // INS_vpgatherqd
        unchecked((uint)(BAD_CODE)), // INS_vpgatherqq
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovq
        unchecked((uint)(BAD_CODE)), // INS_vpsllvd
        unchecked((uint)(BAD_CODE)), // INS_vpsllvq
        unchecked((uint)(BAD_CODE)), // INS_vpsravd
        unchecked((uint)(BAD_CODE)), // INS_vpsrlvd
        unchecked((uint)(BAD_CODE)), // INS_vpsrlvq
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132pd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213pd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231pd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132ps
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213ps
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231ps
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132sd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213sd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231sd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132ss
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213ss
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231ss
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub132pd
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub213pd
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub231pd
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub132ps
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub213ps
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub231ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd132pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd213pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd231pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd132ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd213ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd231ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132sd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213sd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231sd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132ss
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213ss
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231ss
        unchecked((uint)(BAD_CODE)), // INS_andn
        unchecked((uint)(BAD_CODE)), // INS_bextr
        unchecked((uint)(BAD_CODE)), // INS_blsi
        unchecked((uint)(BAD_CODE)), // INS_blsmsk
        unchecked((uint)(BAD_CODE)), // INS_blsr
        unchecked((uint)(BAD_CODE)), // INS_bzhi
        unchecked((uint)(BAD_CODE)), // INS_mulx
        unchecked((uint)(BAD_CODE)), // INS_pdep
        unchecked((uint)(BAD_CODE)), // INS_pext
        unchecked((uint)(BAD_CODE)), // INS_rorx
        unchecked((uint)(BAD_CODE)), // INS_sarx
        unchecked((uint)(BAD_CODE)), // INS_shlx
        unchecked((uint)(BAD_CODE)), // INS_shrx
        unchecked((uint)(BAD_CODE)), // INS_vpdpbusd
        unchecked((uint)(BAD_CODE)), // INS_vpdpbusds
        unchecked((uint)(BAD_CODE)), // INS_vpdpwssd
        unchecked((uint)(BAD_CODE)), // INS_vpdpwssds
        unchecked((uint)(BAD_CODE)), // INS_vpdpwsud
        unchecked((uint)(BAD_CODE)), // INS_vpdpwsuds
        unchecked((uint)(BAD_CODE)), // INS_vpdpwusd
        unchecked((uint)(BAD_CODE)), // INS_vpdpwusds
        unchecked((uint)(BAD_CODE)), // INS_vpdpwuud
        unchecked((uint)(BAD_CODE)), // INS_vpdpwuuds
        unchecked((uint)(BAD_CODE)), // INS_vpdpbssd
        unchecked((uint)(BAD_CODE)), // INS_vpdpbssds
        unchecked((uint)(BAD_CODE)), // INS_vpdpbsud
        unchecked((uint)(BAD_CODE)), // INS_vpdpbsuds
        unchecked((uint)(BAD_CODE)), // INS_vpdpbuud
        unchecked((uint)(BAD_CODE)), // INS_vpdpbuuds
        unchecked((uint)(BAD_CODE)), // INS_vbmacor16x16x16
        unchecked((uint)(BAD_CODE)), // INS_vbmacxor16x16x16
        unchecked((uint)(BAD_CODE)), // INS_vbitrev
        unchecked((uint)(BAD_CODE)), // INS_vpmadd52huq
        unchecked((uint)(BAD_CODE)), // INS_vpmadd52luq
        unchecked((uint)(BAD_CODE)), // INS_kaddb
        unchecked((uint)(BAD_CODE)), // INS_kaddd
        unchecked((uint)(BAD_CODE)), // INS_kaddq
        unchecked((uint)(BAD_CODE)), // INS_kaddw
        unchecked((uint)(BAD_CODE)), // INS_kandb
        unchecked((uint)(BAD_CODE)), // INS_kandd
        unchecked((uint)(BAD_CODE)), // INS_kandnb
        unchecked((uint)(BAD_CODE)), // INS_kandnd
        unchecked((uint)(BAD_CODE)), // INS_kandnq
        unchecked((uint)(BAD_CODE)), // INS_kandnw
        unchecked((uint)(BAD_CODE)), // INS_kandq
        unchecked((uint)(BAD_CODE)), // INS_kandw
        unchecked((uint)(BAD_CODE)), // INS_kmovb_gpr
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovb_msk
        unchecked((uint)(BAD_CODE)), // INS_kmovd_gpr
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovd_msk
        unchecked((uint)(BAD_CODE)), // INS_kmovq_gpr
        unchecked((uint)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovq_msk
        unchecked((uint)(BAD_CODE)), // INS_kmovw_gpr
        unchecked((uint)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovw_msk
        unchecked((uint)(BAD_CODE)), // INS_knotb
        unchecked((uint)(BAD_CODE)), // INS_knotd
        unchecked((uint)(BAD_CODE)), // INS_knotq
        unchecked((uint)(BAD_CODE)), // INS_knotw
        unchecked((uint)(BAD_CODE)), // INS_korb
        unchecked((uint)(BAD_CODE)), // INS_kord
        unchecked((uint)(BAD_CODE)), // INS_korq
        unchecked((uint)(BAD_CODE)), // INS_kortestb
        unchecked((uint)(BAD_CODE)), // INS_kortestd
        unchecked((uint)(BAD_CODE)), // INS_kortestq
        unchecked((uint)(BAD_CODE)), // INS_kortestw
        unchecked((uint)(BAD_CODE)), // INS_korw
        unchecked((uint)(BAD_CODE)), // INS_kshiftlb
        unchecked((uint)(BAD_CODE)), // INS_kshiftld
        unchecked((uint)(BAD_CODE)), // INS_kshiftlq
        unchecked((uint)(BAD_CODE)), // INS_kshiftlw
        unchecked((uint)(BAD_CODE)), // INS_kshiftrb
        unchecked((uint)(BAD_CODE)), // INS_kshiftrd
        unchecked((uint)(BAD_CODE)), // INS_kshiftrq
        unchecked((uint)(BAD_CODE)), // INS_kshiftrw
        unchecked((uint)(BAD_CODE)), // INS_ktestb
        unchecked((uint)(BAD_CODE)), // INS_ktestd
        unchecked((uint)(BAD_CODE)), // INS_ktestq
        unchecked((uint)(BAD_CODE)), // INS_ktestw
        unchecked((uint)(BAD_CODE)), // INS_kunpckbw
        unchecked((uint)(BAD_CODE)), // INS_kunpckdq
        unchecked((uint)(BAD_CODE)), // INS_kunpckwd
        unchecked((uint)(BAD_CODE)), // INS_kxnorb
        unchecked((uint)(BAD_CODE)), // INS_kxnord
        unchecked((uint)(BAD_CODE)), // INS_kxnorq
        unchecked((uint)(BAD_CODE)), // INS_kxnorw
        unchecked((uint)(BAD_CODE)), // INS_kxorb
        unchecked((uint)(BAD_CODE)), // INS_kxord
        unchecked((uint)(BAD_CODE)), // INS_kxorq
        unchecked((uint)(BAD_CODE)), // INS_kxorw
        unchecked((uint)(BAD_CODE)), // INS_valignd
        unchecked((uint)(BAD_CODE)), // INS_valignq
        unchecked((uint)(BAD_CODE)), // INS_vblendmpd
        unchecked((uint)(BAD_CODE)), // INS_vblendmps
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastf32x2
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastf32x8
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastf64x2
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastf64x4
        unchecked((uint)(BAD_CODE)), // INS_vbroadcasti32x2
        unchecked((uint)(BAD_CODE)), // INS_vbroadcasti32x8
        unchecked((uint)(BAD_CODE)), // INS_vbroadcasti64x2
        unchecked((uint)(BAD_CODE)), // INS_vbroadcasti64x4
        unchecked((uint)(BAD_CODE)), // INS_vcmppd
        unchecked((uint)(BAD_CODE)), // INS_vcmpps
        unchecked((uint)(BAD_CODE)), // INS_vcmpsd
        unchecked((uint)(BAD_CODE)), // INS_vcmpss
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompresspd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompressps
        unchecked((uint)(BAD_CODE)), // INS_vcvtpd2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvtpd2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvtpd2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvtqq2pd
        unchecked((uint)(BAD_CODE)), // INS_vcvtqq2ps
        unchecked((uint)(BAD_CODE)), // INS_vcvtsd2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvtsd2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvtss2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvtss2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvtudq2pd
        unchecked((uint)(BAD_CODE)), // INS_vcvtudq2ps
        unchecked((uint)(BAD_CODE)), // INS_vcvtuqq2pd
        unchecked((uint)(BAD_CODE)), // INS_vcvtuqq2ps
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2sd32
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2sd64
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2ss32
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2ss64
        unchecked((uint)(BAD_CODE)), // INS_vdbpsadbw
        unchecked((uint)(BAD_CODE)), // INS_vexpandpd
        unchecked((uint)(BAD_CODE)), // INS_vexpandps
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf32x8
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf64x2
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf64x4
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti32x8
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti64x2
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti64x4
        unchecked((uint)(BAD_CODE)), // INS_vfixupimmpd
        unchecked((uint)(BAD_CODE)), // INS_vfixupimmps
        unchecked((uint)(BAD_CODE)), // INS_vfixupimmsd
        unchecked((uint)(BAD_CODE)), // INS_vfixupimmss
        unchecked((uint)(BAD_CODE)), // INS_vfpclasspd
        unchecked((uint)(BAD_CODE)), // INS_vfpclassps
        unchecked((uint)(BAD_CODE)), // INS_vfpclasssd
        unchecked((uint)(BAD_CODE)), // INS_vfpclassss
        unchecked((uint)(BAD_CODE)), // INS_vgatherdpd_msk
        unchecked((uint)(BAD_CODE)), // INS_vgatherdps_msk
        unchecked((uint)(BAD_CODE)), // INS_vgatherqpd_msk
        unchecked((uint)(BAD_CODE)), // INS_vgatherqps_msk
        unchecked((uint)(BAD_CODE)), // INS_vgetexppd
        unchecked((uint)(BAD_CODE)), // INS_vgetexpps
        unchecked((uint)(BAD_CODE)), // INS_vgetexpsd
        unchecked((uint)(BAD_CODE)), // INS_vgetexpss
        unchecked((uint)(BAD_CODE)), // INS_vgetmantpd
        unchecked((uint)(BAD_CODE)), // INS_vgetmantps
        unchecked((uint)(BAD_CODE)), // INS_vgetmantsd
        unchecked((uint)(BAD_CODE)), // INS_vgetmantss
        unchecked((uint)(BAD_CODE)), // INS_vinsertf32x8
        unchecked((uint)(BAD_CODE)), // INS_vinsertf64x2
        unchecked((uint)(BAD_CODE)), // INS_vinsertf64x4
        unchecked((uint)(BAD_CODE)), // INS_vinserti32x8
        unchecked((uint)(BAD_CODE)), // INS_vinserti64x2
        unchecked((uint)(BAD_CODE)), // INS_vinserti64x4
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqa64
        unchecked((uint)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu16
        unchecked((uint)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu64
        unchecked((uint)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu8
        unchecked((uint)(BAD_CODE)), // INS_vpabsq
        unchecked((uint)(BAD_CODE)), // INS_vpandnq
        unchecked((uint)(BAD_CODE)), // INS_vpandq
        unchecked((uint)(BAD_CODE)), // INS_vpblendmb
        unchecked((uint)(BAD_CODE)), // INS_vpblendmd
        unchecked((uint)(BAD_CODE)), // INS_vpblendmq
        unchecked((uint)(BAD_CODE)), // INS_vpblendmw
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastb_gpr
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastd_gpr
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastq_gpr
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastw_gpr
        unchecked((uint)(BAD_CODE)), // INS_vpcmpb
        unchecked((uint)(BAD_CODE)), // INS_vpcmpd
        unchecked((uint)(BAD_CODE)), // INS_vpcmpeqb
        unchecked((uint)(BAD_CODE)), // INS_vpcmpeqd
        unchecked((uint)(BAD_CODE)), // INS_vpcmpeqq
        unchecked((uint)(BAD_CODE)), // INS_vpcmpeqw
        unchecked((uint)(BAD_CODE)), // INS_vpcmpgtb
        unchecked((uint)(BAD_CODE)), // INS_vpcmpgtd
        unchecked((uint)(BAD_CODE)), // INS_vpcmpgtq
        unchecked((uint)(BAD_CODE)), // INS_vpcmpgtw
        unchecked((uint)(BAD_CODE)), // INS_vpcmpq
        unchecked((uint)(BAD_CODE)), // INS_vpcmpub
        unchecked((uint)(BAD_CODE)), // INS_vpcmpud
        unchecked((uint)(BAD_CODE)), // INS_vpcmpuq
        unchecked((uint)(BAD_CODE)), // INS_vpcmpuw
        unchecked((uint)(BAD_CODE)), // INS_vpcmpw
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressq
        unchecked((uint)(BAD_CODE)), // INS_vpconflictd
        unchecked((uint)(BAD_CODE)), // INS_vpconflictq
        unchecked((uint)(BAD_CODE)), // INS_vpermi2d
        unchecked((uint)(BAD_CODE)), // INS_vpermi2pd
        unchecked((uint)(BAD_CODE)), // INS_vpermi2ps
        unchecked((uint)(BAD_CODE)), // INS_vpermi2q
        unchecked((uint)(BAD_CODE)), // INS_vpermi2w
        unchecked((uint)(BAD_CODE)), // INS_vpermpd_reg
        unchecked((uint)(BAD_CODE)), // INS_vpermq_reg
        unchecked((uint)(BAD_CODE)), // INS_vpermt2d
        unchecked((uint)(BAD_CODE)), // INS_vpermt2pd
        unchecked((uint)(BAD_CODE)), // INS_vpermt2ps
        unchecked((uint)(BAD_CODE)), // INS_vpermt2q
        unchecked((uint)(BAD_CODE)), // INS_vpermt2w
        unchecked((uint)(BAD_CODE)), // INS_vpermw
        unchecked((uint)(BAD_CODE)), // INS_vpexpandd
        unchecked((uint)(BAD_CODE)), // INS_vpexpandq
        unchecked((uint)(BAD_CODE)), // INS_vpgatherdd_msk
        unchecked((uint)(BAD_CODE)), // INS_vpgatherdq_msk
        unchecked((uint)(BAD_CODE)), // INS_vpgatherqd_msk
        unchecked((uint)(BAD_CODE)), // INS_vpgatherqq_msk
        unchecked((uint)(BAD_CODE)), // INS_vplzcntd
        unchecked((uint)(BAD_CODE)), // INS_vplzcntq
        unchecked((uint)(BAD_CODE)), // INS_vpmaxsq
        unchecked((uint)(BAD_CODE)), // INS_vpmaxuq
        unchecked((uint)(BAD_CODE)), // INS_vpminsq
        unchecked((uint)(BAD_CODE)), // INS_vpminuq
        unchecked((uint)(BAD_CODE)), // INS_vpmovb2m
        unchecked((uint)(BAD_CODE)), // INS_vpmovd2m
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x31))) << 8)))), // INS_vpmovdb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x33))) << 8)))), // INS_vpmovdw
        unchecked((uint)(BAD_CODE)), // INS_vpmovm2b
        unchecked((uint)(BAD_CODE)), // INS_vpmovm2d
        unchecked((uint)(BAD_CODE)), // INS_vpmovm2q
        unchecked((uint)(BAD_CODE)), // INS_vpmovm2w
        unchecked((uint)(BAD_CODE)), // INS_vpmovq2m
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x32))) << 8)))), // INS_vpmovqb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x35))) << 8)))), // INS_vpmovqd
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x34))) << 8)))), // INS_vpmovqw
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x21))) << 8)))), // INS_vpmovsdb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x23))) << 8)))), // INS_vpmovsdw
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x22))) << 8)))), // INS_vpmovsqb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x25))) << 8)))), // INS_vpmovsqd
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x24))) << 8)))), // INS_vpmovsqw
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x20))) << 8)))), // INS_vpmovswb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x11))) << 8)))), // INS_vpmovusdb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x13))) << 8)))), // INS_vpmovusdw
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x12))) << 8)))), // INS_vpmovusqb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x15))) << 8)))), // INS_vpmovusqd
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x14))) << 8)))), // INS_vpmovusqw
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x10))) << 8)))), // INS_vpmovuswb
        unchecked((uint)(BAD_CODE)), // INS_vpmovw2m
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x30))) << 8)))), // INS_vpmovwb
        unchecked((uint)(BAD_CODE)), // INS_vpmullq
        unchecked((uint)(BAD_CODE)), // INS_vporq
        unchecked((uint)(BAD_CODE)), // INS_vprold
        unchecked((uint)(BAD_CODE)), // INS_vprolq
        unchecked((uint)(BAD_CODE)), // INS_vprolvd
        unchecked((uint)(BAD_CODE)), // INS_vprolvq
        unchecked((uint)(BAD_CODE)), // INS_vprord
        unchecked((uint)(BAD_CODE)), // INS_vprorq
        unchecked((uint)(BAD_CODE)), // INS_vprorvd
        unchecked((uint)(BAD_CODE)), // INS_vprorvq
        unchecked((uint)(BAD_CODE)), // INS_vpscatterdd_msk
        unchecked((uint)(BAD_CODE)), // INS_vpscatterdq_msk
        unchecked((uint)(BAD_CODE)), // INS_vpscatterqd_msk
        unchecked((uint)(BAD_CODE)), // INS_vpscatterqq_msk
        unchecked((uint)(BAD_CODE)), // INS_vpsllvw
        unchecked((uint)(BAD_CODE)), // INS_vpsraq
        unchecked((uint)(BAD_CODE)), // INS_vpsravq
        unchecked((uint)(BAD_CODE)), // INS_vpsravw
        unchecked((uint)(BAD_CODE)), // INS_vpsrlvw
        unchecked((uint)(BAD_CODE)), // INS_vpternlogd
        unchecked((uint)(BAD_CODE)), // INS_vpternlogq
        unchecked((uint)(BAD_CODE)), // INS_vptestmb
        unchecked((uint)(BAD_CODE)), // INS_vptestmd
        unchecked((uint)(BAD_CODE)), // INS_vptestmq
        unchecked((uint)(BAD_CODE)), // INS_vptestmw
        unchecked((uint)(BAD_CODE)), // INS_vptestnmb
        unchecked((uint)(BAD_CODE)), // INS_vptestnmd
        unchecked((uint)(BAD_CODE)), // INS_vptestnmq
        unchecked((uint)(BAD_CODE)), // INS_vptestnmw
        unchecked((uint)(BAD_CODE)), // INS_vpxorq
        unchecked((uint)(BAD_CODE)), // INS_vrangepd
        unchecked((uint)(BAD_CODE)), // INS_vrangeps
        unchecked((uint)(BAD_CODE)), // INS_vrangesd
        unchecked((uint)(BAD_CODE)), // INS_vrangess
        unchecked((uint)(BAD_CODE)), // INS_vrcp14pd
        unchecked((uint)(BAD_CODE)), // INS_vrcp14ps
        unchecked((uint)(BAD_CODE)), // INS_vrcp14sd
        unchecked((uint)(BAD_CODE)), // INS_vrcp14ss
        unchecked((uint)(BAD_CODE)), // INS_vreducepd
        unchecked((uint)(BAD_CODE)), // INS_vreduceps
        unchecked((uint)(BAD_CODE)), // INS_vreducesd
        unchecked((uint)(BAD_CODE)), // INS_vreducess
        unchecked((uint)(BAD_CODE)), // INS_vrndscalepd
        unchecked((uint)(BAD_CODE)), // INS_vrndscaleps
        unchecked((uint)(BAD_CODE)), // INS_vrndscalesd
        unchecked((uint)(BAD_CODE)), // INS_vrndscaless
        unchecked((uint)(BAD_CODE)), // INS_vrsqrt14pd
        unchecked((uint)(BAD_CODE)), // INS_vrsqrt14ps
        unchecked((uint)(BAD_CODE)), // INS_vrsqrt14sd
        unchecked((uint)(BAD_CODE)), // INS_vrsqrt14ss
        unchecked((uint)(BAD_CODE)), // INS_vscalefpd
        unchecked((uint)(BAD_CODE)), // INS_vscalefps
        unchecked((uint)(BAD_CODE)), // INS_vscalefsd
        unchecked((uint)(BAD_CODE)), // INS_vscalefss
        unchecked((uint)(BAD_CODE)), // INS_vscatterdpd_msk
        unchecked((uint)(BAD_CODE)), // INS_vscatterdps_msk
        unchecked((uint)(BAD_CODE)), // INS_vscatterqpd_msk
        unchecked((uint)(BAD_CODE)), // INS_vscatterqps_msk
        unchecked((uint)(BAD_CODE)), // INS_vshuff32x4
        unchecked((uint)(BAD_CODE)), // INS_vshuff64x2
        unchecked((uint)(BAD_CODE)), // INS_vshufi32x4
        unchecked((uint)(BAD_CODE)), // INS_vshufi64x2
        unchecked((uint)(BAD_CODE)), // INS_vpermb
        unchecked((uint)(BAD_CODE)), // INS_vpermi2b
        unchecked((uint)(BAD_CODE)), // INS_vpermt2b
        unchecked((uint)(BAD_CODE)), // INS_vpmultishiftqb
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressb
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressw
        unchecked((uint)(BAD_CODE)), // INS_vpexpandb
        unchecked((uint)(BAD_CODE)), // INS_vpexpandw
        unchecked((uint)(BAD_CODE)), // INS_vpopcntb
        unchecked((uint)(BAD_CODE)), // INS_vpopcntd
        unchecked((uint)(BAD_CODE)), // INS_vpopcntq
        unchecked((uint)(BAD_CODE)), // INS_vpopcntw
        unchecked((uint)(BAD_CODE)), // INS_vpshldd
        unchecked((uint)(BAD_CODE)), // INS_vpshldq
        unchecked((uint)(BAD_CODE)), // INS_vpshldvd
        unchecked((uint)(BAD_CODE)), // INS_vpshldvq
        unchecked((uint)(BAD_CODE)), // INS_vpshldvw
        unchecked((uint)(BAD_CODE)), // INS_vpshldw
        unchecked((uint)(BAD_CODE)), // INS_vpshrdd
        unchecked((uint)(BAD_CODE)), // INS_vpshrdq
        unchecked((uint)(BAD_CODE)), // INS_vpshrdvd
        unchecked((uint)(BAD_CODE)), // INS_vpshrdvq
        unchecked((uint)(BAD_CODE)), // INS_vpshrdvw
        unchecked((uint)(BAD_CODE)), // INS_vpshrdw
        unchecked((uint)(BAD_CODE)), // INS_vpshufbitqmb
        unchecked((uint)(BAD_CODE)), // INS_vaddph
        unchecked((uint)(BAD_CODE)), // INS_vaddsh
        unchecked((uint)(BAD_CODE)), // INS_vcmpph
        unchecked((uint)(BAD_CODE)), // INS_vcmpsh
        unchecked((uint)(BAD_CODE)), // INS_vcomish
        unchecked((uint)(BAD_CODE)), // INS_vcvtdq2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtne2ps2bf16
        unchecked((uint)(BAD_CODE)), // INS_vcvtneps2bf16
        unchecked((uint)(BAD_CODE)), // INS_vcvtpd2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2dq
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2pd
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2psx
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2uw
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2w
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2phx
        unchecked((uint)(BAD_CODE)), // INS_vcvtqq2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtsd2sh
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2sd
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2si32
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2si64
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2ss
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvtsi2sh32
        unchecked((uint)(BAD_CODE)), // INS_vcvtsi2sh64
        unchecked((uint)(BAD_CODE)), // INS_vcvtss2sh
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2dq
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2uw
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2w
        unchecked((uint)(BAD_CODE)), // INS_vcvttsh2si32
        unchecked((uint)(BAD_CODE)), // INS_vcvttsh2si64
        unchecked((uint)(BAD_CODE)), // INS_vcvttsh2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvttsh2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvtudq2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtuqq2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2sh32
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2sh64
        unchecked((uint)(BAD_CODE)), // INS_vcvtuw2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtw2ph
        unchecked((uint)(BAD_CODE)), // INS_vdivph
        unchecked((uint)(BAD_CODE)), // INS_vdivsh
        unchecked((uint)(BAD_CODE)), // INS_vdpbf16ps
        unchecked((uint)(BAD_CODE)), // INS_vfcmaddcph
        unchecked((uint)(BAD_CODE)), // INS_vfcmaddcsh
        unchecked((uint)(BAD_CODE)), // INS_vfcmulcph
        unchecked((uint)(BAD_CODE)), // INS_vfcmulcsh
        unchecked((uint)(BAD_CODE)), // INS_vfmulcph
        unchecked((uint)(BAD_CODE)), // INS_vfmulcsh
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132ph
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213ph
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231ph
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132sh
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213sh
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231sh
        unchecked((uint)(BAD_CODE)), // INS_vfmaddcph
        unchecked((uint)(BAD_CODE)), // INS_vfmaddcsh
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub132ph
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub213ph
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub231ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132sh
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213sh
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231sh
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd132ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd213ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd231ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132sh
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213sh
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231sh
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132sh
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213sh
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231sh
        unchecked((uint)(BAD_CODE)), // INS_vfpclassph
        unchecked((uint)(BAD_CODE)), // INS_vfpclasssh
        unchecked((uint)(BAD_CODE)), // INS_vgetexpph
        unchecked((uint)(BAD_CODE)), // INS_vgetexpsh
        unchecked((uint)(BAD_CODE)), // INS_vgetmantph
        unchecked((uint)(BAD_CODE)), // INS_vgetmantsh
        unchecked((uint)(BAD_CODE)), // INS_vmaxph
        unchecked((uint)(BAD_CODE)), // INS_vmaxsh
        unchecked((uint)(BAD_CODE)), // INS_vminsh
        unchecked((uint)(BAD_CODE)), // INS_vminph
        unchecked((uint)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x11)))))), // INS_vmovsh
        unchecked((uint)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x7E)))))), // INS_vmovw
        unchecked((uint)(BAD_CODE)), // INS_vmulph
        unchecked((uint)(BAD_CODE)), // INS_vmulsh
        unchecked((uint)(BAD_CODE)), // INS_vrcpph
        unchecked((uint)(BAD_CODE)), // INS_vrcpsh
        unchecked((uint)(BAD_CODE)), // INS_vreduceph
        unchecked((uint)(BAD_CODE)), // INS_vreducesh
        unchecked((uint)(BAD_CODE)), // INS_vrndscaleph
        unchecked((uint)(BAD_CODE)), // INS_vrndscalesh
        unchecked((uint)(BAD_CODE)), // INS_vrsqrtph
        unchecked((uint)(BAD_CODE)), // INS_vrsqrtsh
        unchecked((uint)(BAD_CODE)), // INS_vscalefph
        unchecked((uint)(BAD_CODE)), // INS_vscalefsh
        unchecked((uint)(BAD_CODE)), // INS_vsqrtph
        unchecked((uint)(BAD_CODE)), // INS_vsqrtsh
        unchecked((uint)(BAD_CODE)), // INS_vsubph
        unchecked((uint)(BAD_CODE)), // INS_vsubsh
        unchecked((uint)(BAD_CODE)), // INS_vucomish
        unchecked((uint)(BAD_CODE)), // INS_vp2intersectd
        unchecked((uint)(BAD_CODE)), // INS_vp2intersectq
        unchecked((uint)(BAD_CODE)), // INS_vcomxsd
        unchecked((uint)(BAD_CODE)), // INS_vcomxss
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2ibs
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2iubs
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2dqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2qqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2udqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2uqqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2dqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2ibs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2iubs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2qqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2udqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2uqqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2sis32
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2sis64
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2usis32
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2usis64
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2sis32
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2sis64
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2usis32
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2usis64
        unchecked((uint)(BAD_CODE)), // INS_vminmaxpd
        unchecked((uint)(BAD_CODE)), // INS_vminmaxps
        unchecked((uint)(BAD_CODE)), // INS_vminmaxsd
        unchecked((uint)(BAD_CODE)), // INS_vminmaxss
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_vmovd_simd
        unchecked((uint)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7E)))))), // INS_vmovw_simd
        unchecked((uint)(BAD_CODE)), // INS_vmpsadbw
        unchecked((uint)(BAD_CODE)), // INS_vucomxsd
        unchecked((uint)(BAD_CODE)), // INS_vucomxss
#if TARGET_AMD64
        unchecked((uint)(0x000038)), // INS_ccmpo
        unchecked((uint)(0x000038)), // INS_ccmpno
        unchecked((uint)(0x000038)), // INS_ccmpb
        unchecked((uint)(0x000038)), // INS_ccmpae
        unchecked((uint)(0x000038)), // INS_ccmpe
        unchecked((uint)(0x000038)), // INS_ccmpne
        unchecked((uint)(0x000038)), // INS_ccmpbe
        unchecked((uint)(0x000038)), // INS_ccmpa
        unchecked((uint)(0x000038)), // INS_ccmps
        unchecked((uint)(0x000038)), // INS_ccmpns
        unchecked((uint)(0x000038)), // INS_ccmpt
        unchecked((uint)(0x000038)), // INS_ccmpf
        unchecked((uint)(0x000038)), // INS_ccmpl
        unchecked((uint)(0x000038)), // INS_ccmpge
        unchecked((uint)(0x000038)), // INS_ccmple
        unchecked((uint)(0x000038)), // INS_ccmpg
        unchecked((uint)(0x000040)), // INS_cfcmovo
        unchecked((uint)(0x000041)), // INS_cfcmovno
        unchecked((uint)(0x000042)), // INS_cfcmovb
        unchecked((uint)(0x000043)), // INS_cfcmovae
        unchecked((uint)(0x000044)), // INS_cfcmove
        unchecked((uint)(0x000045)), // INS_cfcmovne
        unchecked((uint)(0x000046)), // INS_cfcmovbe
        unchecked((uint)(0x000047)), // INS_cfcmova
        unchecked((uint)(0x000048)), // INS_cfcmovs
        unchecked((uint)(0x000049)), // INS_cfcmovns
        unchecked((uint)(0x00004A)), // INS_cfcmovp
        unchecked((uint)(0x00004B)), // INS_cfcmovnp
        unchecked((uint)(0x00004C)), // INS_cfcmovl
        unchecked((uint)(0x00004D)), // INS_cfcmovge
        unchecked((uint)(0x00004E)), // INS_cfcmovle
        unchecked((uint)(0x00004F)), // INS_cfcmovg
        unchecked((uint)(0x000084)), // INS_ctesto
        unchecked((uint)(0x000084)), // INS_ctestno
        unchecked((uint)(0x000084)), // INS_ctestb
        unchecked((uint)(0x000084)), // INS_ctestae
        unchecked((uint)(0x000084)), // INS_cteste
        unchecked((uint)(0x000084)), // INS_ctestne
        unchecked((uint)(0x000084)), // INS_ctestbe
        unchecked((uint)(0x000084)), // INS_ctesta
        unchecked((uint)(0x000084)), // INS_ctests
        unchecked((uint)(0x000084)), // INS_ctestns
        unchecked((uint)(0x000084)), // INS_ctestt
        unchecked((uint)(0x000084)), // INS_ctestf
        unchecked((uint)(0x000084)), // INS_ctestl
        unchecked((uint)(0x000084)), // INS_ctestge
        unchecked((uint)(0x000084)), // INS_ctestle
        unchecked((uint)(0x000084)), // INS_ctestg
        unchecked((uint)(BAD_CODE)), // INS_crc32_apx
        unchecked((uint)(0x000061)), // INS_movbe_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x40)))))), // INS_seto_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x41)))))), // INS_setno_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x42)))))), // INS_setb_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x43)))))), // INS_setae_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x44)))))), // INS_sete_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x45)))))), // INS_setne_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x46)))))), // INS_setbe_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x47)))))), // INS_seta_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x48)))))), // INS_sets_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x49)))))), // INS_setns_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4A)))))), // INS_setp_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4B)))))), // INS_setnp_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4C)))))), // INS_setl_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4D)))))), // INS_setge_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4E)))))), // INS_setle_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4F)))))), // INS_setg_apx
#endif
        unchecked((uint)(BAD_CODE)), // INS_crc32
        unchecked((uint)(BAD_CODE)), // INS_tzcnt
#if TARGET_AMD64
        unchecked((uint)(BAD_CODE)), // INS_tzcnt_apx
#endif
        unchecked((uint)(BAD_CODE)), // INS_lzcnt
#if TARGET_AMD64
        unchecked((uint)(BAD_CODE)), // INS_lzcnt_apx
#endif
        unchecked((uint)(((((0x0F)) << 16) | (((0x38)) << 24) | (((0xF1)))))), // INS_movbe
        unchecked((uint)(BAD_CODE)), // INS_popcnt
#if TARGET_AMD64
        unchecked((uint)(BAD_CODE)), // INS_popcnt_apx
#endif
        unchecked((uint)(BAD_CODE)), // INS_tpause
        unchecked((uint)(BAD_CODE)), // INS_umonitor
        unchecked((uint)(BAD_CODE)), // INS_umwait
        unchecked((uint)(0x0018F6)), // INS_neg
        unchecked((uint)(0x0010F6)), // INS_not
        unchecked((uint)(0x0000D2)), // INS_rol
        unchecked((uint)(0x0000D0)), // INS_rol_1
        unchecked((uint)(0x0000C0)), // INS_rol_N
        unchecked((uint)(0x0008D2)), // INS_ror
        unchecked((uint)(0x0008D0)), // INS_ror_1
        unchecked((uint)(0x0008C0)), // INS_ror_N
        unchecked((uint)(0x0010D2)), // INS_rcl
        unchecked((uint)(0x0010D0)), // INS_rcl_1
        unchecked((uint)(0x0010C0)), // INS_rcl_N
        unchecked((uint)(0x0018D2)), // INS_rcr
        unchecked((uint)(0x0018D0)), // INS_rcr_1
        unchecked((uint)(0x0018C0)), // INS_rcr_N
        unchecked((uint)(0x0020D2)), // INS_shl
        unchecked((uint)(0x0020D0)), // INS_shl_1
        unchecked((uint)(0x0020C0)), // INS_shl_N
        unchecked((uint)(0x0028D2)), // INS_shr
        unchecked((uint)(0x0028D0)), // INS_shr_1
        unchecked((uint)(0x0028C0)), // INS_shr_N
        unchecked((uint)(0x0038D2)), // INS_sar
        unchecked((uint)(0x0038D0)), // INS_sar_1
        unchecked((uint)(0x0038C0)), // INS_sar_N
        unchecked((uint)(0x0000C3)), // INS_ret
        unchecked((uint)(BAD_CODE)), // INS_loop
        unchecked((uint)(0x0010FF)), // INS_call
        unchecked((uint)(0x00A4F3)), // INS_r_movsb
        unchecked((uint)(0x00A5F3)), // INS_r_movsd
#if TARGET_AMD64
        unchecked((uint)(0xF3A548)), // INS_r_movsq
#endif
        unchecked((uint)(0x0000A4)), // INS_movsb
        unchecked((uint)(0x0000A5)), // INS_movsd
#if TARGET_AMD64
        unchecked((uint)(0x00A548)), // INS_movsq
#endif
        unchecked((uint)(0x00AAF3)), // INS_r_stosb
        unchecked((uint)(0x00ABF3)), // INS_r_stosd
#if TARGET_AMD64
        unchecked((uint)(0xF3AB48)), // INS_r_stosq
#endif
        unchecked((uint)(0x0000AA)), // INS_stosb
        unchecked((uint)(0x0000AB)), // INS_stosd
#if TARGET_AMD64
        unchecked((uint)(0x00AB48)), // INS_stosq
#endif
        unchecked((uint)(0x0000CC)), // INS_int3
        unchecked((uint)(0x000090)), // INS_nop
        unchecked((uint)(0x0090F3)), // INS_pause
        unchecked((uint)(0x0000F0)), // INS_lock
        unchecked((uint)(0x0000C9)), // INS_leave
        unchecked((uint)(0x0fe801)), // INS_serialize
        unchecked((uint)(0x000098)), // INS_cwde
        unchecked((uint)(0x000099)), // INS_cdq
        unchecked((uint)(0x0038F6)), // INS_idiv
        unchecked((uint)(0x0028F6)), // INS_imulEAX
        unchecked((uint)(0x0030F6)), // INS_div
        unchecked((uint)(0x0020F6)), // INS_mulEAX
        unchecked((uint)(0x00009E)), // INS_sahf
        unchecked((uint)(0x0F00C0)), // INS_xadd
        unchecked((uint)(0x0F00B0)), // INS_cmpxchg
        unchecked((uint)(0x0F00A4)), // INS_shld
        unchecked((uint)(0x0F00AC)), // INS_shrd
#if TARGET_X86
        unchecked((uint)(0x0000D9)), // INS_fld
        unchecked((uint)(0x0018D9)), // INS_fstp
#endif
        unchecked((uint)(0x0F0090)), // INS_seto
        unchecked((uint)(0x0F0091)), // INS_setno
        unchecked((uint)(0x0F0092)), // INS_setb
        unchecked((uint)(0x0F0093)), // INS_setae
        unchecked((uint)(0x0F0094)), // INS_sete
        unchecked((uint)(0x0F0095)), // INS_setne
        unchecked((uint)(0x0F0096)), // INS_setbe
        unchecked((uint)(0x0F0097)), // INS_seta
        unchecked((uint)(0x0F0098)), // INS_sets
        unchecked((uint)(0x0F0099)), // INS_setns
        unchecked((uint)(0x0F009A)), // INS_setp
        unchecked((uint)(0x0F009B)), // INS_setnp
        unchecked((uint)(0x0F009C)), // INS_setl
        unchecked((uint)(0x0F009D)), // INS_setge
        unchecked((uint)(0x0F009E)), // INS_setle
        unchecked((uint)(0x0F009F)), // INS_setg
        unchecked((uint)(0x0020FF)), // INS_tail_i_jmp
        unchecked((uint)(0x0020FF)), // INS_i_jmp
        unchecked((uint)(0x0000EB)), // INS_jmp
        unchecked((uint)(0x000070)), // INS_jo
        unchecked((uint)(0x000071)), // INS_jno
        unchecked((uint)(0x000072)), // INS_jb
        unchecked((uint)(0x000073)), // INS_jae
        unchecked((uint)(0x000074)), // INS_je
        unchecked((uint)(0x000075)), // INS_jne
        unchecked((uint)(0x000076)), // INS_jbe
        unchecked((uint)(0x000077)), // INS_ja
        unchecked((uint)(0x000078)), // INS_js
        unchecked((uint)(0x000079)), // INS_jns
        unchecked((uint)(0x00007A)), // INS_jp
        unchecked((uint)(0x00007B)), // INS_jnp
        unchecked((uint)(0x00007C)), // INS_jl
        unchecked((uint)(0x00007D)), // INS_jge
        unchecked((uint)(0x00007E)), // INS_jle
        unchecked((uint)(0x00007F)), // INS_jg
        unchecked((uint)(0x0000E9)), // INS_l_jmp
        unchecked((uint)(0x00800F)), // INS_l_jo
        unchecked((uint)(0x00810F)), // INS_l_jno
        unchecked((uint)(0x00820F)), // INS_l_jb
        unchecked((uint)(0x00830F)), // INS_l_jae
        unchecked((uint)(0x00840F)), // INS_l_je
        unchecked((uint)(0x00850F)), // INS_l_jne
        unchecked((uint)(0x00860F)), // INS_l_jbe
        unchecked((uint)(0x00870F)), // INS_l_ja
        unchecked((uint)(0x00880F)), // INS_l_js
        unchecked((uint)(0x00890F)), // INS_l_jns
        unchecked((uint)(0x008A0F)), // INS_l_jp
        unchecked((uint)(0x008B0F)), // INS_l_jnp
        unchecked((uint)(0x008C0F)), // INS_l_jl
        unchecked((uint)(0x008D0F)), // INS_l_jge
        unchecked((uint)(0x008E0F)), // INS_l_jle
        unchecked((uint)(0x008F0F)), // INS_l_jg
        unchecked((uint)(BAD_CODE)), // INS_align
        unchecked((uint)(0x000066)), // INS_data16
#elif TARGET_ARM
#if !TARGET_ARM
#error Unexpected target type
#endif
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
#elif TARGET_ARM64
#if !TARGET_ARM64
#error Unexpected target type
#endif
#if FEATURE_LOOP_ALIGN
#endif
#if !TARGET_ARM64
#error Unexpected target type
#endif
#elif TARGET_LOONGARCH64
#if !TARGET_LOONGARCH64
#error Unexpected target type
#endif
#if FEATURE_SIMD
#endif
#elif TARGET_RISCV64
#if !TARGET_RISCV64
#error Unexpected target type
#endif
#elif TARGET_WASM
#if !TARGET_WASM
#error Unexpected target type
#endif
#else
#error Unsupported or unset target architecture
#endif
    ];

    private static ReadOnlySpan<uint> insCodesACC => [
#if TARGET_XARCH
#if !TARGET_XARCH
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(BAD_CODE)), // INS_push
        unchecked((uint)(BAD_CODE)), // INS_pop
        unchecked((uint)(BAD_CODE)), // INS_push_hide
        unchecked((uint)(BAD_CODE)), // INS_pop_hide
        unchecked((uint)(BAD_CODE)), // INS_push2
        unchecked((uint)(BAD_CODE)), // INS_pop2
        unchecked((uint)(BAD_CODE)), // INS_inc
        unchecked((uint)(BAD_CODE)), // INS_inc_l
        unchecked((uint)(BAD_CODE)), // INS_dec
        unchecked((uint)(BAD_CODE)), // INS_dec_l
        unchecked((uint)(BAD_CODE)), // INS_bswap
        unchecked((uint)(0x000004)), // INS_add
        unchecked((uint)(0x00000C)), // INS_or
        unchecked((uint)(0x000014)), // INS_adc
        unchecked((uint)(0x00001C)), // INS_sbb
        unchecked((uint)(0x000024)), // INS_and
        unchecked((uint)(0x00002C)), // INS_sub
        unchecked((uint)(0x00002C)), // INS_sub_hide
        unchecked((uint)(0x000034)), // INS_xor
        unchecked((uint)(0x00003C)), // INS_cmp
        unchecked((uint)(0x0000A8)), // INS_test
        unchecked((uint)(0x0000B0)), // INS_mov
        unchecked((uint)(BAD_CODE)), // INS_lea
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_X86
#endif
#elif TARGET_ARM
#if !TARGET_ARM
#error Unexpected target type
#endif
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
#elif TARGET_ARM64
#if !TARGET_ARM64
#error Unexpected target type
#endif
#if FEATURE_LOOP_ALIGN
#endif
#if !TARGET_ARM64
#error Unexpected target type
#endif
#elif TARGET_LOONGARCH64
#if !TARGET_LOONGARCH64
#error Unexpected target type
#endif
#if FEATURE_SIMD
#endif
#elif TARGET_RISCV64
#if !TARGET_RISCV64
#error Unexpected target type
#endif
#elif TARGET_WASM
#if !TARGET_WASM
#error Unexpected target type
#endif
#else
#error Unsupported or unset target architecture
#endif
    ];

    private static ReadOnlySpan<uint> insCodesRR => [
#if TARGET_XARCH
#if !TARGET_XARCH
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0x000050)), // INS_push
        unchecked((uint)(0x000058)), // INS_pop
        unchecked((uint)(0x000050)), // INS_push_hide
        unchecked((uint)(0x000058)), // INS_pop_hide
        unchecked((uint)(0x0030FF)), // INS_push2
        unchecked((uint)(0x00008F)), // INS_pop2
        unchecked((uint)(0x000040)), // INS_inc
        unchecked((uint)(0x00C0FE)), // INS_inc_l
        unchecked((uint)(0x000048)), // INS_dec
        unchecked((uint)(0x00C8FE)), // INS_dec_l
        unchecked((uint)(0x00C80F)), // INS_bswap
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_X86
#endif
#elif TARGET_ARM
#if !TARGET_ARM
#error Unexpected target type
#endif
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
#elif TARGET_ARM64
#if !TARGET_ARM64
#error Unexpected target type
#endif
#if FEATURE_LOOP_ALIGN
#endif
#if !TARGET_ARM64
#error Unexpected target type
#endif
#elif TARGET_LOONGARCH64
#if !TARGET_LOONGARCH64
#error Unexpected target type
#endif
#if FEATURE_SIMD
#endif
#elif TARGET_RISCV64
#if !TARGET_RISCV64
#error Unexpected target type
#endif
#elif TARGET_WASM
#if !TARGET_WASM
#error Unexpected target type
#endif
#else
#error Unsupported or unset target architecture
#endif
    ];

    private static ReadOnlySpan<OpcodeNative> insCodesRM => [
#if TARGET_XARCH
#if !TARGET_XARCH
#error Unexpected target type
#endif
        unchecked((OpcodeNative)(BAD_CODE)), // INS_invalid
        unchecked((OpcodeNative)(BAD_CODE)), // INS_push
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pop
        unchecked((OpcodeNative)(BAD_CODE)), // INS_push_hide
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pop_hide
        unchecked((OpcodeNative)(0x0030FF)), // INS_push2
        unchecked((OpcodeNative)(0x00008F)), // INS_pop2
        unchecked((OpcodeNative)(0x0000FE)), // INS_inc
        unchecked((OpcodeNative)(BAD_CODE)), // INS_inc_l
        unchecked((OpcodeNative)(0x0008FE)), // INS_dec
        unchecked((OpcodeNative)(BAD_CODE)), // INS_dec_l
        unchecked((OpcodeNative)(BAD_CODE)), // INS_bswap
        unchecked((OpcodeNative)(0x000002)), // INS_add
        unchecked((OpcodeNative)(0x00000A)), // INS_or
        unchecked((OpcodeNative)(0x000012)), // INS_adc
        unchecked((OpcodeNative)(0x00001A)), // INS_sbb
        unchecked((OpcodeNative)(0x000022)), // INS_and
        unchecked((OpcodeNative)(0x00002A)), // INS_sub
        unchecked((OpcodeNative)(0x00002A)), // INS_sub_hide
        unchecked((OpcodeNative)(0x000032)), // INS_xor
        unchecked((OpcodeNative)(0x00003A)), // INS_cmp
        unchecked((OpcodeNative)(0x000084)), // INS_test
        unchecked((OpcodeNative)(0x00008A)), // INS_mov
        unchecked((OpcodeNative)(0x00008D)), // INS_lea
        unchecked((OpcodeNative)(0x0F00A3)), // INS_bt
        unchecked((OpcodeNative)(0x0F00AB)), // INS_bts
        unchecked((OpcodeNative)(0x0F00B3)), // INS_btr
        unchecked((OpcodeNative)(0x0F00BB)), // INS_btc
        unchecked((OpcodeNative)(0x0F00BD)), // INS_bsr
        unchecked((OpcodeNative)(0x0F00BC)), // INS_bsf
        unchecked((OpcodeNative)(0x0F00BE)), // INS_movsx
#if TARGET_AMD64
        unchecked((OpcodeNative)(0x000063)), // INS_movsxd
#endif
        unchecked((OpcodeNative)(0x0F00B6)), // INS_movzx
        unchecked((OpcodeNative)(0x0F0040)), // INS_cmovo
        unchecked((OpcodeNative)(0x0F0041)), // INS_cmovno
        unchecked((OpcodeNative)(0x0F0042)), // INS_cmovb
        unchecked((OpcodeNative)(0x0F0043)), // INS_cmovae
        unchecked((OpcodeNative)(0x0F0044)), // INS_cmove
        unchecked((OpcodeNative)(0x0F0045)), // INS_cmovne
        unchecked((OpcodeNative)(0x0F0046)), // INS_cmovbe
        unchecked((OpcodeNative)(0x0F0047)), // INS_cmova
        unchecked((OpcodeNative)(0x0F0048)), // INS_cmovs
        unchecked((OpcodeNative)(0x0F0049)), // INS_cmovns
        unchecked((OpcodeNative)(0x0F004A)), // INS_cmovp
        unchecked((OpcodeNative)(0x0F004B)), // INS_cmovnp
        unchecked((OpcodeNative)(0x0F004C)), // INS_cmovl
        unchecked((OpcodeNative)(0x0F004D)), // INS_cmovge
        unchecked((OpcodeNative)(0x0F004E)), // INS_cmovle
        unchecked((OpcodeNative)(0x0F004F)), // INS_cmovg
        unchecked((OpcodeNative)(0x000086)), // INS_xchg
        unchecked((OpcodeNative)(0x0F00AF)), // INS_imul
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_AX
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_CX
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_DX
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_BX
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_SP
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_BP
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_SI
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_DI
#if TARGET_AMD64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_08
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_09
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_10
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_11
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_12
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_13
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_14
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_15
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_16
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_17
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_18
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_19
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_20
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_21
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_22
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_23
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_24
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_25
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_26
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_27
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_28
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_29
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_30
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_31
#endif
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x58)))))), // INS_addpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x58)))))), // INS_addps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x58)))))), // INS_addsd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x58)))))), // INS_addss
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD0)))))), // INS_addsubpd
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xD0)))))), // INS_addsubps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x55)))))), // INS_andnpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x55)))))), // INS_andnps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x54)))))), // INS_andpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x54)))))), // INS_andps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0D)))) << 8)))), // INS_blendpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0C)))) << 8)))), // INS_blendps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x15)))) << 8)))), // INS_blendvpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x14)))) << 8)))), // INS_blendvps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_cmppd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0xC2)))))), // INS_cmpps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_cmpsd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_cmpss
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2F)))))), // INS_comisd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x2F)))))), // INS_comiss
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_cvtdq2pd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x5B)))))), // INS_cvtdq2ps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_cvtpd2dq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5A)))))), // INS_cvtpd2ps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5B)))))), // INS_cvtps2dq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x5A)))))), // INS_cvtps2pd
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtsd2si32
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtsd2si64
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5A)))))), // INS_cvtsd2ss
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2sd32
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2sd64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2ss32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2ss64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5A)))))), // INS_cvtss2sd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtss2si32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtss2si64
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_cvttpd2dq
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5B)))))), // INS_cvttps2dq
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttsd2si32
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttsd2si64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttss2si32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttss2si64
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5E)))))), // INS_divpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x5E)))))), // INS_divps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5E)))))), // INS_divsd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5E)))))), // INS_divss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x41)))) << 8)))), // INS_dppd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x40)))) << 8)))), // INS_dpps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_extractps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7C)))))), // INS_haddpd
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7C)))))), // INS_haddps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7D)))))), // INS_hsubpd
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7D)))))), // INS_hsubps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x21)))) << 8)))), // INS_insertps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xF0)))))), // INS_lddqu
        unchecked((OpcodeNative)(BAD_CODE)), // INS_lfence
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF7)))))), // INS_maskmovdqu
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5F)))))), // INS_maxpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x5F)))))), // INS_maxps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5F)))))), // INS_maxsd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5F)))))), // INS_maxss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_mfence
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5D)))))), // INS_minpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x5D)))))), // INS_minps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5D)))))), // INS_minsd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5D)))))), // INS_minss
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x28)))))), // INS_movapd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x28)))))), // INS_movaps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6E)))))), // INS_movd32
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6E)))))), // INS_movd64
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x12)))))), // INS_movddup
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_movdqa32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_movdqu32
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x12)))))), // INS_movhlps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x16)))))), // INS_movhpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x16)))))), // INS_movhps
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x16)))))), // INS_movlhps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x12)))))), // INS_movlpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x12)))))), // INS_movlps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x50)))))), // INS_movmskpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x50)))))), // INS_movmskps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movntdq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2A)))) << 8)))), // INS_movntdqa
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movnti32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movnti64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movntpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movntps
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movq
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x10)))))), // INS_movsd_simd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x16)))))), // INS_movshdup
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x12)))))), // INS_movsldup
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x10)))))), // INS_movss
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x10)))))), // INS_movupd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x10)))))), // INS_movups
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x42)))) << 8)))), // INS_mpsadbw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x59)))))), // INS_mulpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x59)))))), // INS_mulps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x59)))))), // INS_mulsd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x59)))))), // INS_mulss
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x56)))))), // INS_orpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x56)))))), // INS_orps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1C)))) << 8)))), // INS_pabsb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1E)))) << 8)))), // INS_pabsd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1D)))) << 8)))), // INS_pabsw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6B)))))), // INS_packssdw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x63)))))), // INS_packsswb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2B)))) << 8)))), // INS_packusdw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x67)))))), // INS_packuswb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFC)))))), // INS_paddb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFE)))))), // INS_paddd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD4)))))), // INS_paddq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEC)))))), // INS_paddsb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xED)))))), // INS_paddsw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDC)))))), // INS_paddusb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDD)))))), // INS_paddusw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFD)))))), // INS_paddw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0F)))) << 8)))), // INS_palignr
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDB)))))), // INS_pandd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDF)))))), // INS_pandnd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE0)))))), // INS_pavgb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE3)))))), // INS_pavgw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x10)))) << 8)))), // INS_pblendvb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0E)))) << 8)))), // INS_pblendw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x74)))))), // INS_pcmpeqb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x76)))))), // INS_pcmpeqd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x29)))) << 8)))), // INS_pcmpeqq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x75)))))), // INS_pcmpeqw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x64)))))), // INS_pcmpgtb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x66)))))), // INS_pcmpgtd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x37)))) << 8)))), // INS_pcmpgtq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x65)))))), // INS_pcmpgtw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pextrb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pextrd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pextrq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x02)))) << 8)))), // INS_phaddd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pextrw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x03)))) << 8)))), // INS_phaddsw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x01)))) << 8)))), // INS_phaddw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x41)))) << 8)))), // INS_phminposuw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x06)))) << 8)))), // INS_phsubd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x07)))) << 8)))), // INS_phsubsw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x05)))) << 8)))), // INS_phsubw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x20)))) << 8)))), // INS_pinsrb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x22)))) << 8)))), // INS_pinsrd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x22)))) << 8)))), // INS_pinsrq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC4)))))), // INS_pinsrw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x04)))) << 8)))), // INS_pmaddubsw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF5)))))), // INS_pmaddwd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3C)))) << 8)))), // INS_pmaxsb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3D)))) << 8)))), // INS_pmaxsd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEE)))))), // INS_pmaxsw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDE)))))), // INS_pmaxub
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3F)))) << 8)))), // INS_pmaxud
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3E)))) << 8)))), // INS_pmaxuw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x38)))) << 8)))), // INS_pminsb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x39)))) << 8)))), // INS_pminsd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEA)))))), // INS_pminsw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDA)))))), // INS_pminub
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3B)))) << 8)))), // INS_pminud
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3A)))) << 8)))), // INS_pminuw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD7)))))), // INS_pmovmskb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x21)))) << 8)))), // INS_pmovsxbd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x22)))) << 8)))), // INS_pmovsxbq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x20)))) << 8)))), // INS_pmovsxbw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x25)))) << 8)))), // INS_pmovsxdq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x23)))) << 8)))), // INS_pmovsxwd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x24)))) << 8)))), // INS_pmovsxwq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x31)))) << 8)))), // INS_pmovzxbd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x32)))) << 8)))), // INS_pmovzxbq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x30)))) << 8)))), // INS_pmovzxbw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x35)))) << 8)))), // INS_pmovzxdq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x33)))) << 8)))), // INS_pmovzxwd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x34)))) << 8)))), // INS_pmovzxwq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x28)))) << 8)))), // INS_pmuldq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0B)))) << 8)))), // INS_pmulhrsw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE4)))))), // INS_pmulhuw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE5)))))), // INS_pmulhw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x40)))) << 8)))), // INS_pmulld
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF4)))))), // INS_pmuludq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD5)))))), // INS_pmullw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEB)))))), // INS_pord
        unchecked((OpcodeNative)(BAD_CODE)), // INS_prefetchnta
        unchecked((OpcodeNative)(BAD_CODE)), // INS_prefetcht0
        unchecked((OpcodeNative)(BAD_CODE)), // INS_prefetcht1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_prefetcht2
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF6)))))), // INS_psadbw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x00)))) << 8)))), // INS_pshufb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x70)))))), // INS_pshufd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x70)))))), // INS_pshufhw
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x70)))))), // INS_pshuflw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x08)))) << 8)))), // INS_psignb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0A)))) << 8)))), // INS_psignd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x09)))) << 8)))), // INS_psignw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF2)))))), // INS_pslld
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pslldq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF3)))))), // INS_psllq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF1)))))), // INS_psllw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE2)))))), // INS_psrad
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE1)))))), // INS_psraw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD2)))))), // INS_psrld
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psrldq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD3)))))), // INS_psrlq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD1)))))), // INS_psrlw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF8)))))), // INS_psubb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFA)))))), // INS_psubd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFB)))))), // INS_psubq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE8)))))), // INS_psubsb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE9)))))), // INS_psubsw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD8)))))), // INS_psubusb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD9)))))), // INS_psubusw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF9)))))), // INS_psubw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x17)))) << 8)))), // INS_ptest
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x68)))))), // INS_punpckhbw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6A)))))), // INS_punpckhdq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6D)))))), // INS_punpckhqdq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x69)))))), // INS_punpckhwd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x60)))))), // INS_punpcklbw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x62)))))), // INS_punpckldq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6C)))))), // INS_punpcklqdq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x61)))))), // INS_punpcklwd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEF)))))), // INS_pxord
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x53)))))), // INS_rcpps
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x53)))))), // INS_rcpss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x09)))) << 8)))), // INS_roundpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x08)))) << 8)))), // INS_roundps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0B)))) << 8)))), // INS_roundsd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0A)))) << 8)))), // INS_roundss
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x52)))))), // INS_rsqrtps
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x52)))))), // INS_rsqrtss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sfence
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC6)))))), // INS_shufpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0xC6)))))), // INS_shufps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x51)))))), // INS_sqrtpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x51)))))), // INS_sqrtps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x51)))))), // INS_sqrtsd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x51)))))), // INS_sqrtss
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5C)))))), // INS_subpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x5C)))))), // INS_subps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5C)))))), // INS_subsd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5C)))))), // INS_subss
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2E)))))), // INS_ucomisd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x2E)))))), // INS_ucomiss
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x15)))))), // INS_unpckhpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x15)))))), // INS_unpckhps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x14)))))), // INS_unpcklpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x14)))))), // INS_unpcklps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x57)))))), // INS_xorpd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x57)))))), // INS_xorps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDE)))) << 8)))), // INS_aesdec
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDF)))) << 8)))), // INS_aesdeclast
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDC)))) << 8)))), // INS_aesenc
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDD)))) << 8)))), // INS_aesenclast
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDB)))) << 8)))), // INS_aesimc
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xDF)))) << 8)))), // INS_aeskeygenassist
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x44)))) << 8)))), // INS_pclmulqdq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC9)))) << 8)))), // INS_sha1msg1
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCA)))) << 8)))), // INS_sha1msg2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC8)))) << 8)))), // INS_sha1nexte
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xCC)))) << 8)))), // INS_sha1rnds4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCC)))) << 8)))), // INS_sha256msg1
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCD)))) << 8)))), // INS_sha256msg2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCB)))) << 8)))), // INS_sha256rnds2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xCF)))) << 8)))), // INS_gf2p8affineinvqb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xCE)))) << 8)))), // INS_gf2p8affineqb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCF)))) << 8)))), // INS_gf2p8mulb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x4B)))) << 8)))), // INS_vblendvpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x4A)))) << 8)))), // INS_vblendvps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1A)))) << 8)))), // INS_vbroadcastf32x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x19)))) << 8)))), // INS_vbroadcastsd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x18)))) << 8)))), // INS_vbroadcastss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextractf32x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x18)))) << 8)))), // INS_vinsertf32x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2D)))) << 8)))), // INS_vmaskmovpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2C)))) << 8)))), // INS_vmaskmovps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x4C)))) << 8)))), // INS_vpblendvb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x06)))) << 8)))), // INS_vperm2f128
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x05)))) << 8)))), // INS_vpermilpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0D)))) << 8)))), // INS_vpermilpdvar
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x04)))) << 8)))), // INS_vpermilps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0C)))) << 8)))), // INS_vpermilpsvar
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0F)))) << 8)))), // INS_vtestpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0E)))) << 8)))), // INS_vtestps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vzeroupper
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5A)))) << 8)))), // INS_vbroadcasti32x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x13)))) << 8)))), // INS_vcvtph2ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtps2ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextracti32x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x38)))) << 8)))), // INS_vinserti32x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x02)))) << 8)))), // INS_vpblendd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x78)))) << 8)))), // INS_vpbroadcastb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x58)))) << 8)))), // INS_vpbroadcastd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x59)))) << 8)))), // INS_vpbroadcastq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x79)))) << 8)))), // INS_vpbroadcastw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x46)))) << 8)))), // INS_vperm2i128
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x36)))) << 8)))), // INS_vpermd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x01)))) << 8)))), // INS_vpermpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x16)))) << 8)))), // INS_vpermps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x00)))) << 8)))), // INS_vpermq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8C)))) << 8)))), // INS_vpmaskmovd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8C)))) << 8)))), // INS_vpmaskmovq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x47)))) << 8)))), // INS_vpsllvd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x47)))) << 8)))), // INS_vpsllvq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x46)))) << 8)))), // INS_vpsravd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x45)))) << 8)))), // INS_vpsrlvd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x45)))) << 8)))), // INS_vpsrlvq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x98)))) << 8)))), // INS_vfmadd132pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA8)))) << 8)))), // INS_vfmadd213pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB8)))) << 8)))), // INS_vfmadd231pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x98)))) << 8)))), // INS_vfmadd132ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA8)))) << 8)))), // INS_vfmadd213ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB8)))) << 8)))), // INS_vfmadd231ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x99)))) << 8)))), // INS_vfmadd132sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA9)))) << 8)))), // INS_vfmadd213sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB9)))) << 8)))), // INS_vfmadd231sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x99)))) << 8)))), // INS_vfmadd132ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA9)))) << 8)))), // INS_vfmadd213ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB9)))) << 8)))), // INS_vfmadd231ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x96)))) << 8)))), // INS_vfmaddsub132pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA6)))) << 8)))), // INS_vfmaddsub213pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB6)))) << 8)))), // INS_vfmaddsub231pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x96)))) << 8)))), // INS_vfmaddsub132ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA6)))) << 8)))), // INS_vfmaddsub213ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB6)))) << 8)))), // INS_vfmaddsub231ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x97)))) << 8)))), // INS_vfmsubadd132pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA7)))) << 8)))), // INS_vfmsubadd213pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB7)))) << 8)))), // INS_vfmsubadd231pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x97)))) << 8)))), // INS_vfmsubadd132ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA7)))) << 8)))), // INS_vfmsubadd213ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB7)))) << 8)))), // INS_vfmsubadd231ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9A)))) << 8)))), // INS_vfmsub132pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAA)))) << 8)))), // INS_vfmsub213pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBA)))) << 8)))), // INS_vfmsub231pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9A)))) << 8)))), // INS_vfmsub132ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAA)))) << 8)))), // INS_vfmsub213ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBA)))) << 8)))), // INS_vfmsub231ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9B)))) << 8)))), // INS_vfmsub132sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAB)))) << 8)))), // INS_vfmsub213sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBB)))) << 8)))), // INS_vfmsub231sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9B)))) << 8)))), // INS_vfmsub132ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAB)))) << 8)))), // INS_vfmsub213ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBB)))) << 8)))), // INS_vfmsub231ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9C)))) << 8)))), // INS_vfnmadd132pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAC)))) << 8)))), // INS_vfnmadd213pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBC)))) << 8)))), // INS_vfnmadd231pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9C)))) << 8)))), // INS_vfnmadd132ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAC)))) << 8)))), // INS_vfnmadd213ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBC)))) << 8)))), // INS_vfnmadd231ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9D)))) << 8)))), // INS_vfnmadd132sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAD)))) << 8)))), // INS_vfnmadd213sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBD)))) << 8)))), // INS_vfnmadd231sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9D)))) << 8)))), // INS_vfnmadd132ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAD)))) << 8)))), // INS_vfnmadd213ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBD)))) << 8)))), // INS_vfnmadd231ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9E)))) << 8)))), // INS_vfnmsub132pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAE)))) << 8)))), // INS_vfnmsub213pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBE)))) << 8)))), // INS_vfnmsub231pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9E)))) << 8)))), // INS_vfnmsub132ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAE)))) << 8)))), // INS_vfnmsub213ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBE)))) << 8)))), // INS_vfnmsub231ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9F)))) << 8)))), // INS_vfnmsub132sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAF)))) << 8)))), // INS_vfnmsub213sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBF)))) << 8)))), // INS_vfnmsub231sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9F)))) << 8)))), // INS_vfnmsub132ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAF)))) << 8)))), // INS_vfnmsub213ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBF)))) << 8)))), // INS_vfnmsub231ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF2)))) << 8)))), // INS_andn
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF7)))) << 8)))), // INS_bextr
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF3)))) << 8)))), // INS_blsi
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF3)))) << 8)))), // INS_blsmsk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF3)))) << 8)))), // INS_blsr
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF5)))) << 8)))), // INS_bzhi
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF6)))) << 8)))), // INS_mulx
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF5)))) << 8)))), // INS_pdep
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF5)))) << 8)))), // INS_pext
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xF0)))) << 8)))), // INS_rorx
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xF7))) << 8)))), // INS_sarx
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF7)))) << 8)))), // INS_shlx
        unchecked((OpcodeNative)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xF7))) << 8)))), // INS_shrx
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x50)))) << 8)))), // INS_vpdpbusd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x51)))) << 8)))), // INS_vpdpbusds
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x52)))) << 8)))), // INS_vpdpwssd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x53)))) << 8)))), // INS_vpdpwssds
        unchecked((OpcodeNative)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD2))) << 8)))), // INS_vpdpwsud
        unchecked((OpcodeNative)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD3))) << 8)))), // INS_vpdpwsuds
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xD2)))) << 8)))), // INS_vpdpwusd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xD3)))) << 8)))), // INS_vpdpwusds
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD2))) << 8)))), // INS_vpdpwuud
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD3))) << 8)))), // INS_vpdpwuuds
        unchecked((OpcodeNative)((((((0xf2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x50))) << 8)))), // INS_vpdpbssd
        unchecked((OpcodeNative)((((((0xf2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x51))) << 8)))), // INS_vpdpbssds
        unchecked((OpcodeNative)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x50))) << 8)))), // INS_vpdpbsud
        unchecked((OpcodeNative)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x51))) << 8)))), // INS_vpdpbsuds
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x50))) << 8)))), // INS_vpdpbuud
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x51))) << 8)))), // INS_vpdpbuuds
        unchecked((OpcodeNative)((((((0x06))) << 16) | (((0x80)))))), // INS_vbmacor16x16x16
        unchecked((OpcodeNative)((((((0x06))) << 16) | (((0x80)))))), // INS_vbmacxor16x16x16
        unchecked((OpcodeNative)((((((0x06))) << 16) | (((0x81)))))), // INS_vbitrev
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB5)))) << 8)))), // INS_vpmadd52huq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB4)))) << 8)))), // INS_vpmadd52luq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x4A)))))), // INS_kaddb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x4A)))))), // INS_kaddd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x4A)))))), // INS_kaddq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x4A)))))), // INS_kaddw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x41)))))), // INS_kandb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x41)))))), // INS_kandd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x42)))))), // INS_kandnb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x42)))))), // INS_kandnd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x42)))))), // INS_kandnq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x42)))))), // INS_kandnw
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x41)))))), // INS_kandq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x41)))))), // INS_kandw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x92)))))), // INS_kmovb_gpr
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x90)))))), // INS_kmovb_msk
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x92)))))), // INS_kmovd_gpr
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x90)))))), // INS_kmovd_msk
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x92)))))), // INS_kmovq_gpr
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x90)))))), // INS_kmovq_msk
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x92)))))), // INS_kmovw_gpr
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x90)))))), // INS_kmovw_msk
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x44)))))), // INS_knotb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x44)))))), // INS_knotd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x44)))))), // INS_knotq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x44)))))), // INS_knotw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x45)))))), // INS_korb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x45)))))), // INS_kord
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x45)))))), // INS_korq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x98)))))), // INS_kortestb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x98)))))), // INS_kortestd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x98)))))), // INS_kortestq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x98)))))), // INS_kortestw
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x45)))))), // INS_korw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x32)))) << 8)))), // INS_kshiftlb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x33)))) << 8)))), // INS_kshiftld
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x33)))) << 8)))), // INS_kshiftlq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x32)))) << 8)))), // INS_kshiftlw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x30)))) << 8)))), // INS_kshiftrb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x31)))) << 8)))), // INS_kshiftrd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x31)))) << 8)))), // INS_kshiftrq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x30)))) << 8)))), // INS_kshiftrw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x99)))))), // INS_ktestb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x99)))))), // INS_ktestd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x99)))))), // INS_ktestq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x99)))))), // INS_ktestw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x4B)))))), // INS_kunpckbw
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x4B)))))), // INS_kunpckdq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x4B)))))), // INS_kunpckwd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x46)))))), // INS_kxnorb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x46)))))), // INS_kxnord
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x46)))))), // INS_kxnorq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x46)))))), // INS_kxnorw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x47)))))), // INS_kxorb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x47)))))), // INS_kxord
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x47)))))), // INS_kxorq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x47)))))), // INS_kxorw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x03)))) << 8)))), // INS_valignd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x03)))) << 8)))), // INS_valignq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x65)))) << 8)))), // INS_vblendmpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x65)))) << 8)))), // INS_vblendmps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x19)))) << 8)))), // INS_vbroadcastf32x2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1B)))) << 8)))), // INS_vbroadcastf32x8
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1A)))) << 8)))), // INS_vbroadcastf64x2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1B)))) << 8)))), // INS_vbroadcastf64x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x59)))) << 8)))), // INS_vbroadcasti32x2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5B)))) << 8)))), // INS_vbroadcasti32x8
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5A)))) << 8)))), // INS_vbroadcasti64x2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5B)))) << 8)))), // INS_vbroadcasti64x4
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_vcmppd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0xC2)))))), // INS_vcmpps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_vcmpsd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_vcmpss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcompresspd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcompressps
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtpd2qq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x79)))))), // INS_vcvtpd2udq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtpd2uqq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtps2qq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x79)))))), // INS_vcvtps2udq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtps2uqq
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_vcvtqq2pd
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x5B)))))), // INS_vcvtqq2ps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtsd2usi32
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtsd2usi64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtss2usi32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtss2usi64
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvttpd2qq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x78)))))), // INS_vcvttpd2udq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttpd2uqq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvttps2qq
        unchecked((OpcodeNative)(((((0x0f)) << 16) | (((0x78)))))), // INS_vcvttps2udq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttps2uqq
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttsd2usi32
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttsd2usi64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttss2usi32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttss2usi64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtudq2pd
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtudq2ps
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtuqq2pd
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtuqq2ps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2sd32
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2sd64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2ss32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2ss64
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x42)))) << 8)))), // INS_vdbpsadbw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x88)))) << 8)))), // INS_vexpandpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x88)))) << 8)))), // INS_vexpandps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextractf32x8
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextractf64x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextractf64x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextracti32x8
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextracti64x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextracti64x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x54)))) << 8)))), // INS_vfixupimmpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x54)))) << 8)))), // INS_vfixupimmps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x55)))) << 8)))), // INS_vfixupimmsd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x55)))) << 8)))), // INS_vfixupimmss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x66)))) << 8)))), // INS_vfpclasspd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x66)))) << 8)))), // INS_vfpclassps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x67)))) << 8)))), // INS_vfpclasssd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x67)))) << 8)))), // INS_vfpclassss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdpd_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdps_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqpd_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqps_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x42)))) << 8)))), // INS_vgetexppd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x42)))) << 8)))), // INS_vgetexpps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x43)))) << 8)))), // INS_vgetexpsd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x43)))) << 8)))), // INS_vgetexpss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x26)))) << 8)))), // INS_vgetmantpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x26)))) << 8)))), // INS_vgetmantps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x27)))) << 8)))), // INS_vgetmantsd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x27)))) << 8)))), // INS_vgetmantss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1A)))) << 8)))), // INS_vinsertf32x8
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x18)))) << 8)))), // INS_vinsertf64x2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1A)))) << 8)))), // INS_vinsertf64x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3A)))) << 8)))), // INS_vinserti32x8
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x38)))) << 8)))), // INS_vinserti64x2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3A)))) << 8)))), // INS_vinserti64x4
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqa64
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqu16
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqu64
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqu8
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1F)))) << 8)))), // INS_vpabsq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDF)))))), // INS_vpandnq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDB)))))), // INS_vpandq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x66)))) << 8)))), // INS_vpblendmb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x64)))) << 8)))), // INS_vpblendmd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x64)))) << 8)))), // INS_vpblendmq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x66)))) << 8)))), // INS_vpblendmw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7A)))) << 8)))), // INS_vpbroadcastb_gpr
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7C)))) << 8)))), // INS_vpbroadcastd_gpr
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7C)))) << 8)))), // INS_vpbroadcastq_gpr
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7B)))) << 8)))), // INS_vpbroadcastw_gpr
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3F)))) << 8)))), // INS_vpcmpb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1F)))) << 8)))), // INS_vpcmpd
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x74)))))), // INS_vpcmpeqb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x76)))))), // INS_vpcmpeqd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x29)))) << 8)))), // INS_vpcmpeqq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x75)))))), // INS_vpcmpeqw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x64)))))), // INS_vpcmpgtb
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x66)))))), // INS_vpcmpgtd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x37)))) << 8)))), // INS_vpcmpgtq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x65)))))), // INS_vpcmpgtw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1F)))) << 8)))), // INS_vpcmpq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3E)))) << 8)))), // INS_vpcmpub
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1E)))) << 8)))), // INS_vpcmpud
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1E)))) << 8)))), // INS_vpcmpuq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3E)))) << 8)))), // INS_vpcmpuw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3F)))) << 8)))), // INS_vpcmpw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcompressd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcompressq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC4)))) << 8)))), // INS_vpconflictd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC4)))) << 8)))), // INS_vpconflictq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x76)))) << 8)))), // INS_vpermi2d
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x77)))) << 8)))), // INS_vpermi2pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x77)))) << 8)))), // INS_vpermi2ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x76)))) << 8)))), // INS_vpermi2q
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x75)))) << 8)))), // INS_vpermi2w
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x16)))) << 8)))), // INS_vpermpd_reg
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x36)))) << 8)))), // INS_vpermq_reg
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7E)))) << 8)))), // INS_vpermt2d
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7F)))) << 8)))), // INS_vpermt2pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7F)))) << 8)))), // INS_vpermt2ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7E)))) << 8)))), // INS_vpermt2q
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7D)))) << 8)))), // INS_vpermt2w
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8D)))) << 8)))), // INS_vpermw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x89)))) << 8)))), // INS_vpexpandd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x89)))) << 8)))), // INS_vpexpandq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdd_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdq_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqd_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqq_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x44)))) << 8)))), // INS_vplzcntd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x44)))) << 8)))), // INS_vplzcntq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3D)))) << 8)))), // INS_vpmaxsq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3F)))) << 8)))), // INS_vpmaxuq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x39)))) << 8)))), // INS_vpminsq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3B)))) << 8)))), // INS_vpminuq
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x29))) << 8)))), // INS_vpmovb2m
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x39))) << 8)))), // INS_vpmovd2m
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x31))) << 8)))), // INS_vpmovdb
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x33))) << 8)))), // INS_vpmovdw
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x28))) << 8)))), // INS_vpmovm2b
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x38))) << 8)))), // INS_vpmovm2d
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x38))) << 8)))), // INS_vpmovm2q
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x28))) << 8)))), // INS_vpmovm2w
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x39))) << 8)))), // INS_vpmovq2m
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x32))) << 8)))), // INS_vpmovqb
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x35))) << 8)))), // INS_vpmovqd
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x34))) << 8)))), // INS_vpmovqw
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x21))) << 8)))), // INS_vpmovsdb
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x23))) << 8)))), // INS_vpmovsdw
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x22))) << 8)))), // INS_vpmovsqb
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x25))) << 8)))), // INS_vpmovsqd
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x24))) << 8)))), // INS_vpmovsqw
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x20))) << 8)))), // INS_vpmovswb
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x11))) << 8)))), // INS_vpmovusdb
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x13))) << 8)))), // INS_vpmovusdw
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x12))) << 8)))), // INS_vpmovusqb
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x15))) << 8)))), // INS_vpmovusqd
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x14))) << 8)))), // INS_vpmovusqw
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x10))) << 8)))), // INS_vpmovuswb
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x29))) << 8)))), // INS_vpmovw2m
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x30))) << 8)))), // INS_vpmovwb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x40)))) << 8)))), // INS_vpmullq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEB)))))), // INS_vporq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vprold
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vprolq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x15)))) << 8)))), // INS_vprolvd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x15)))) << 8)))), // INS_vprolvq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vprord
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vprorq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x14)))) << 8)))), // INS_vprorvd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x14)))) << 8)))), // INS_vprorvq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA0)))) << 8)))), // INS_vpscatterdd_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA0)))) << 8)))), // INS_vpscatterdq_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA1)))) << 8)))), // INS_vpscatterqd_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA1)))) << 8)))), // INS_vpscatterqq_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x12)))) << 8)))), // INS_vpsllvw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE2)))))), // INS_vpsraq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x46)))) << 8)))), // INS_vpsravq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x11)))) << 8)))), // INS_vpsravw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x10)))) << 8)))), // INS_vpsrlvw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x25)))) << 8)))), // INS_vpternlogd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x25)))) << 8)))), // INS_vpternlogq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x26)))) << 8)))), // INS_vptestmb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x27)))) << 8)))), // INS_vptestmd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x27)))) << 8)))), // INS_vptestmq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x26)))) << 8)))), // INS_vptestmw
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x26))) << 8)))), // INS_vptestnmb
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x27))) << 8)))), // INS_vptestnmd
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x27))) << 8)))), // INS_vptestnmq
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x26))) << 8)))), // INS_vptestnmw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEF)))))), // INS_vpxorq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x50)))) << 8)))), // INS_vrangepd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x50)))) << 8)))), // INS_vrangeps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x51)))) << 8)))), // INS_vrangesd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x51)))) << 8)))), // INS_vrangess
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4C)))) << 8)))), // INS_vrcp14pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4C)))) << 8)))), // INS_vrcp14ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4D)))) << 8)))), // INS_vrcp14sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4D)))) << 8)))), // INS_vrcp14ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x56)))) << 8)))), // INS_vreducepd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x56)))) << 8)))), // INS_vreduceps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x57)))) << 8)))), // INS_vreducesd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x57)))) << 8)))), // INS_vreducess
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x09)))) << 8)))), // INS_vrndscalepd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x08)))) << 8)))), // INS_vrndscaleps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0B)))) << 8)))), // INS_vrndscalesd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0A)))) << 8)))), // INS_vrndscaless
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4E)))) << 8)))), // INS_vrsqrt14pd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4E)))) << 8)))), // INS_vrsqrt14ps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4F)))) << 8)))), // INS_vrsqrt14sd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4F)))) << 8)))), // INS_vrsqrt14ss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2C)))) << 8)))), // INS_vscalefpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2C)))) << 8)))), // INS_vscalefps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2D)))) << 8)))), // INS_vscalefsd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2D)))) << 8)))), // INS_vscalefss
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA2)))) << 8)))), // INS_vscatterdpd_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA2)))) << 8)))), // INS_vscatterdps_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA3)))) << 8)))), // INS_vscatterqpd_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA3)))) << 8)))), // INS_vscatterqps_msk
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x23)))) << 8)))), // INS_vshuff32x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x23)))) << 8)))), // INS_vshuff64x2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x43)))) << 8)))), // INS_vshufi32x4
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x43)))) << 8)))), // INS_vshufi64x2
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8D)))) << 8)))), // INS_vpermb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x75)))) << 8)))), // INS_vpermi2b
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7D)))) << 8)))), // INS_vpermt2b
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x83)))) << 8)))), // INS_vpmultishiftqb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcompressb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcompressw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x62)))) << 8)))), // INS_vpexpandb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x62)))) << 8)))), // INS_vpexpandw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x54)))) << 8)))), // INS_vpopcntb
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x55)))) << 8)))), // INS_vpopcntd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x55)))) << 8)))), // INS_vpopcntq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x54)))) << 8)))), // INS_vpopcntw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x71)))) << 8)))), // INS_vpshldd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x71)))) << 8)))), // INS_vpshldq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x71)))) << 8)))), // INS_vpshldvd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x71)))) << 8)))), // INS_vpshldvq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x70)))) << 8)))), // INS_vpshldvw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x70)))) << 8)))), // INS_vpshldw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x73)))) << 8)))), // INS_vpshrdd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x73)))) << 8)))), // INS_vpshrdq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x73)))) << 8)))), // INS_vpshrdvd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x73)))) << 8)))), // INS_vpshrdvq
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x72)))) << 8)))), // INS_vpshrdvw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x72)))) << 8)))), // INS_vpshrdw
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8F)))) << 8)))), // INS_vpshufbitqmb
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x58)))))), // INS_vaddph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x58)))))), // INS_vaddsh
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0xC2))) << 8)))), // INS_vcmpph
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0xC2))) << 8)))), // INS_vcmpsh
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x2F)))))), // INS_vcomish
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x5B)))))), // INS_vcvtdq2ph
        unchecked((OpcodeNative)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x72))) << 8)))), // INS_vcvtne2ps2bf16
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x72))) << 8)))), // INS_vcvtneps2bf16
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x5A)))))), // INS_vcvtpd2ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x5B)))))), // INS_vcvtph2dq
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x5A)))))), // INS_vcvtph2pd
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x13)))))), // INS_vcvtph2psx
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7B)))))), // INS_vcvtph2qq
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x79)))))), // INS_vcvtph2udq
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x79)))))), // INS_vcvtph2uqq
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x7D)))))), // INS_vcvtph2uw
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7D)))))), // INS_vcvtph2w
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x1D)))))), // INS_vcvtps2phx
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x5B)))))), // INS_vcvtqq2ph
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x5A)))))), // INS_vcvtsd2sh
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5A)))))), // INS_vcvtsh2sd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2D)))))), // INS_vcvtsh2si32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2D)))))), // INS_vcvtsh2si64
        unchecked((OpcodeNative)((((((0x06))) << 16) | (((0x13)))))), // INS_vcvtsh2ss
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x79)))))), // INS_vcvtsh2usi32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x79)))))), // INS_vcvtsh2usi64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2A)))))), // INS_vcvtsi2sh32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2A)))))), // INS_vcvtsi2sh64
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x1D)))))), // INS_vcvtss2sh
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5B)))))), // INS_vcvttph2dq
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7A)))))), // INS_vcvttph2qq
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x78)))))), // INS_vcvttph2udq
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x78)))))), // INS_vcvttph2uqq
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x7C)))))), // INS_vcvttph2uw
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7C)))))), // INS_vcvttph2w
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2C)))))), // INS_vcvttsh2si32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2C)))))), // INS_vcvttsh2si64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x78)))))), // INS_vcvttsh2usi32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x78)))))), // INS_vcvttsh2usi64
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x7A)))))), // INS_vcvtudq2ph
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x7A)))))), // INS_vcvtuqq2ph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7B)))))), // INS_vcvtusi2sh32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7B)))))), // INS_vcvtusi2sh64
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x7D)))))), // INS_vcvtuw2ph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7D)))))), // INS_vcvtw2ph
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x5E)))))), // INS_vdivph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5E)))))), // INS_vdivsh
        unchecked((OpcodeNative)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x52))) << 8)))), // INS_vdpbf16ps
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0x56)))))), // INS_vfcmaddcph
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0x57)))))), // INS_vfcmaddcsh
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0xD6)))))), // INS_vfcmulcph
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0xD7)))))), // INS_vfcmulcsh
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0xD6)))))), // INS_vfmulcph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0xD7)))))), // INS_vfmulcsh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x98)))))), // INS_vfmadd132ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA8)))))), // INS_vfmadd213ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB8)))))), // INS_vfmadd231ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x99)))))), // INS_vfmadd132sh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA9)))))), // INS_vfmadd213sh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB9)))))), // INS_vfmadd231sh
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0x56)))))), // INS_vfmaddcph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0x57)))))), // INS_vfmaddcsh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x96)))))), // INS_vfmaddsub132ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA6)))))), // INS_vfmaddsub213ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB6)))))), // INS_vfmaddsub231ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9A)))))), // INS_vfmsub132ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAA)))))), // INS_vfmsub213ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBA)))))), // INS_vfmsub231ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9B)))))), // INS_vfmsub132sh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAB)))))), // INS_vfmsub213sh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBB)))))), // INS_vfmsub231sh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x97)))))), // INS_vfmsubadd132ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA7)))))), // INS_vfmsubadd213ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB7)))))), // INS_vfmsubadd231ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9C)))))), // INS_vfnmadd132ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAC)))))), // INS_vfnmadd213ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBC)))))), // INS_vfnmadd231ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9D)))))), // INS_vfnmadd132sh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAD)))))), // INS_vfnmadd213sh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBD)))))), // INS_vfnmadd231sh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9E)))))), // INS_vfnmsub132ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAE)))))), // INS_vfnmsub213ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBE)))))), // INS_vfnmsub231ph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9F)))))), // INS_vfnmsub132sh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAF)))))), // INS_vfnmsub213sh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBF)))))), // INS_vfnmsub231sh
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x66))) << 8)))), // INS_vfpclassph
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x67))) << 8)))), // INS_vfpclasssh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x42)))))), // INS_vgetexpph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x43)))))), // INS_vgetexpsh
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x26))) << 8)))), // INS_vgetmantph
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x27))) << 8)))), // INS_vgetmantsh
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x5F)))))), // INS_vmaxph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5F)))))), // INS_vmaxsh
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5D)))))), // INS_vminsh
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x5D)))))), // INS_vminph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x10)))))), // INS_vmovsh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x00))) << 24) | (((0x6E)))))), // INS_vmovw
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x59)))))), // INS_vmulph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x59)))))), // INS_vmulsh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4C)))))), // INS_vrcpph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4D)))))), // INS_vrcpsh
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x56))) << 8)))), // INS_vreduceph
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x57))) << 8)))), // INS_vreducesh
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x08))) << 8)))), // INS_vrndscaleph
        unchecked((OpcodeNative)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x0A))) << 8)))), // INS_vrndscalesh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4E)))))), // INS_vrsqrtph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4F)))))), // INS_vrsqrtsh
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x2C)))))), // INS_vscalefph
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x2D)))))), // INS_vscalefsh
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x51)))))), // INS_vsqrtph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x51)))))), // INS_vsqrtsh
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x5C)))))), // INS_vsubph
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5C)))))), // INS_vsubsh
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x2E)))))), // INS_vucomish
        unchecked((OpcodeNative)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x68))) << 8)))), // INS_vp2intersectd
        unchecked((OpcodeNative)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x68))) << 8)))), // INS_vp2intersectq
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2f)))))), // INS_vcomxsd
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2f)))))), // INS_vcomxss
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x69)))))), // INS_vcvtps2ibs
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6B)))))), // INS_vcvtps2iubs
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x6D)))))), // INS_vcvttpd2dqs
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttpd2qqs
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x6C)))))), // INS_vcvttpd2udqs
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttpd2uqqs
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x6D)))))), // INS_vcvttps2dqs
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x68)))))), // INS_vcvttps2ibs
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6A)))))), // INS_vcvttps2iubs
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttps2qqs
        unchecked((OpcodeNative)((((((0x05))) << 16) | (((0x6C)))))), // INS_vcvttps2udqs
        unchecked((OpcodeNative)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttps2uqqs
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttsd2sis32
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttsd2sis64
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttsd2usis32
        unchecked((OpcodeNative)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttsd2usis64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttss2sis32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttss2sis64
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttss2usis32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttss2usis64
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x52)))) << 8)))), // INS_vminmaxpd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x52)))) << 8)))), // INS_vminmaxps
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x53)))) << 8)))), // INS_vminmaxsd
        unchecked((OpcodeNative)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x53)))) << 8)))), // INS_vminmaxss
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_vmovd_simd
        unchecked((OpcodeNative)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6E)))))), // INS_vmovw_simd
        unchecked((OpcodeNative)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x42)))) << 8)))), // INS_vmpsadbw
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2f)))))), // INS_vucomxsd
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2E)))))), // INS_vucomxss
#if TARGET_AMD64
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpo
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpno
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpb
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpae
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpe
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpne
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpbe
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpa
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmps
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpns
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpt
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpf
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpl
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpge
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmple
        unchecked((OpcodeNative)(0x00003A)), // INS_ccmpg
        unchecked((OpcodeNative)(0x000040)), // INS_cfcmovo
        unchecked((OpcodeNative)(0x000041)), // INS_cfcmovno
        unchecked((OpcodeNative)(0x000042)), // INS_cfcmovb
        unchecked((OpcodeNative)(0x000043)), // INS_cfcmovae
        unchecked((OpcodeNative)(0x000044)), // INS_cfcmove
        unchecked((OpcodeNative)(0x000045)), // INS_cfcmovne
        unchecked((OpcodeNative)(0x000046)), // INS_cfcmovbe
        unchecked((OpcodeNative)(0x000047)), // INS_cfcmova
        unchecked((OpcodeNative)(0x000048)), // INS_cfcmovs
        unchecked((OpcodeNative)(0x000049)), // INS_cfcmovns
        unchecked((OpcodeNative)(0x00004A)), // INS_cfcmovp
        unchecked((OpcodeNative)(0x00004B)), // INS_cfcmovnp
        unchecked((OpcodeNative)(0x00004C)), // INS_cfcmovl
        unchecked((OpcodeNative)(0x00004D)), // INS_cfcmovge
        unchecked((OpcodeNative)(0x00004E)), // INS_cfcmovle
        unchecked((OpcodeNative)(0x00004F)), // INS_cfcmovg
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctesto
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestno
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestae
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cteste
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestne
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestbe
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctesta
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctests
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestns
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestt
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestf
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestl
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestge
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestle
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ctestg
        unchecked((OpcodeNative)(0x0000F0)), // INS_crc32_apx
        unchecked((OpcodeNative)(0x000060)), // INS_movbe_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_seto_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setno_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setb_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setae_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sete_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setne_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setbe_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_seta_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sets_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setns_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setp_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setnp_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setl_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setge_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setle_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setg_apx
#endif
        unchecked((OpcodeNative)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xF0))) << 8)))), // INS_crc32
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xBC)))))), // INS_tzcnt
#if TARGET_AMD64
        unchecked((OpcodeNative)(0x0000F4)), // INS_tzcnt_apx
#endif
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xBD)))))), // INS_lzcnt
#if TARGET_AMD64
        unchecked((OpcodeNative)(0x0000F5)), // INS_lzcnt_apx
#endif
        unchecked((OpcodeNative)(((((0x0F)) << 16) | (((0x38)) << 24) | (((0xF0)))))), // INS_movbe
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xB8)))))), // INS_popcnt
#if TARGET_AMD64
        unchecked((OpcodeNative)(0x000088)), // INS_popcnt_apx
#endif
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xAE)))))), // INS_tpause
        unchecked((OpcodeNative)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xAE)))))), // INS_umonitor
        unchecked((OpcodeNative)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xAE)))))), // INS_umwait
        unchecked((OpcodeNative)(0x0018F6)), // INS_neg
        unchecked((OpcodeNative)(0x0010F6)), // INS_not
        unchecked((OpcodeNative)(0x0000D2)), // INS_rol
        unchecked((OpcodeNative)(0x0000D0)), // INS_rol_1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rol_N
        unchecked((OpcodeNative)(0x0008D2)), // INS_ror
        unchecked((OpcodeNative)(0x0008D0)), // INS_ror_1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ror_N
        unchecked((OpcodeNative)(0x0010D2)), // INS_rcl
        unchecked((OpcodeNative)(0x0010D0)), // INS_rcl_1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rcl_N
        unchecked((OpcodeNative)(0x0018D2)), // INS_rcr
        unchecked((OpcodeNative)(0x0018D0)), // INS_rcr_1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rcr_N
        unchecked((OpcodeNative)(0x0020D2)), // INS_shl
        unchecked((OpcodeNative)(0x0020D0)), // INS_shl_1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_shl_N
        unchecked((OpcodeNative)(0x0028D2)), // INS_shr
        unchecked((OpcodeNative)(0x0028D0)), // INS_shr_1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_shr_N
        unchecked((OpcodeNative)(0x0038D2)), // INS_sar
        unchecked((OpcodeNative)(0x0038D0)), // INS_sar_1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sar_N
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_X86
#endif
#elif TARGET_ARM
#if !TARGET_ARM
#error Unexpected target type
#endif
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
#elif TARGET_ARM64
#if !TARGET_ARM64
#error Unexpected target type
#endif
#if FEATURE_LOOP_ALIGN
#endif
#if !TARGET_ARM64
#error Unexpected target type
#endif
#elif TARGET_LOONGARCH64
#if !TARGET_LOONGARCH64
#error Unexpected target type
#endif
#if FEATURE_SIMD
#endif
#elif TARGET_RISCV64
#if !TARGET_RISCV64
#error Unexpected target type
#endif
#elif TARGET_WASM
#if !TARGET_WASM
#error Unexpected target type
#endif
#else
#error Unsupported or unset target architecture
#endif
    ];

    private static ReadOnlySpan<OpcodeNative> insCodesMI => [
#if TARGET_XARCH
#if !TARGET_XARCH
#error Unexpected target type
#endif
        unchecked((OpcodeNative)(BAD_CODE)), // INS_invalid
        unchecked((OpcodeNative)(0x000068)), // INS_push
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pop
        unchecked((OpcodeNative)(0x000068)), // INS_push_hide
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pop_hide
        unchecked((OpcodeNative)(BAD_CODE)), // INS_push2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pop2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_inc
        unchecked((OpcodeNative)(BAD_CODE)), // INS_inc_l
        unchecked((OpcodeNative)(BAD_CODE)), // INS_dec
        unchecked((OpcodeNative)(BAD_CODE)), // INS_dec_l
        unchecked((OpcodeNative)(BAD_CODE)), // INS_bswap
        unchecked((OpcodeNative)(0x000080)), // INS_add
        unchecked((OpcodeNative)(0x000880)), // INS_or
        unchecked((OpcodeNative)(0x001080)), // INS_adc
        unchecked((OpcodeNative)(0x001880)), // INS_sbb
        unchecked((OpcodeNative)(0x002080)), // INS_and
        unchecked((OpcodeNative)(0x002880)), // INS_sub
        unchecked((OpcodeNative)(0x002880)), // INS_sub_hide
        unchecked((OpcodeNative)(0x003080)), // INS_xor
        unchecked((OpcodeNative)(0x003880)), // INS_cmp
        unchecked((OpcodeNative)(0x0000F6)), // INS_test
        unchecked((OpcodeNative)(0x0000C6)), // INS_mov
        unchecked((OpcodeNative)(BAD_CODE)), // INS_lea
        unchecked((OpcodeNative)(BAD_CODE)), // INS_bt
        unchecked((OpcodeNative)(BAD_CODE)), // INS_bts
        unchecked((OpcodeNative)(BAD_CODE)), // INS_btr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_btc
        unchecked((OpcodeNative)(BAD_CODE)), // INS_bsr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_bsf
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movsx
#if TARGET_AMD64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movsxd
#endif
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movzx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovo
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovno
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovae
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmove
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovne
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovbe
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmova
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovns
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovp
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovnp
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovl
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovge
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovle
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmovg
        unchecked((OpcodeNative)(BAD_CODE)), // INS_xchg
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul
        unchecked((OpcodeNative)(0x000068)), // INS_imul_AX
        unchecked((OpcodeNative)(0x000868)), // INS_imul_CX
        unchecked((OpcodeNative)(0x001068)), // INS_imul_DX
        unchecked((OpcodeNative)(0x001868)), // INS_imul_BX
        unchecked((OpcodeNative)(BAD_CODE)), // INS_imul_SP
        unchecked((OpcodeNative)(0x002868)), // INS_imul_BP
        unchecked((OpcodeNative)(0x003068)), // INS_imul_SI
        unchecked((OpcodeNative)(0x003868)), // INS_imul_DI
#if TARGET_AMD64
        unchecked((OpcodeNative)(0x4400000068)), // INS_imul_08
        unchecked((OpcodeNative)(0x4400000868)), // INS_imul_09
        unchecked((OpcodeNative)(0x4400001068)), // INS_imul_10
        unchecked((OpcodeNative)(0x4400001868)), // INS_imul_11
        unchecked((OpcodeNative)(0x4400002068)), // INS_imul_12
        unchecked((OpcodeNative)(0x4400002868)), // INS_imul_13
        unchecked((OpcodeNative)(0x4400003068)), // INS_imul_14
        unchecked((OpcodeNative)(0x4400003868)), // INS_imul_15
        unchecked((OpcodeNative)(0xD54000000068)), // INS_imul_16
        unchecked((OpcodeNative)(0xD54000000868)), // INS_imul_17
        unchecked((OpcodeNative)(0xD54000001068)), // INS_imul_18
        unchecked((OpcodeNative)(0xD54000001868)), // INS_imul_19
        unchecked((OpcodeNative)(0xD54000002068)), // INS_imul_20
        unchecked((OpcodeNative)(0xD54000002868)), // INS_imul_21
        unchecked((OpcodeNative)(0xD54000003068)), // INS_imul_22
        unchecked((OpcodeNative)(0xD54000003868)), // INS_imul_23
        unchecked((OpcodeNative)(0xD54400000068)), // INS_imul_24
        unchecked((OpcodeNative)(0xD54400000868)), // INS_imul_25
        unchecked((OpcodeNative)(0xD54400001068)), // INS_imul_26
        unchecked((OpcodeNative)(0xD54400001868)), // INS_imul_27
        unchecked((OpcodeNative)(0xD54400002068)), // INS_imul_28
        unchecked((OpcodeNative)(0xD54400002868)), // INS_imul_29
        unchecked((OpcodeNative)(0xD54400003068)), // INS_imul_30
        unchecked((OpcodeNative)(0xD54400003868)), // INS_imul_31
#endif
        unchecked((OpcodeNative)(BAD_CODE)), // INS_addpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_addps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_addsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_addss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_addsubpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_addsubps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_andnpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_andnps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_andpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_andps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_blendpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_blendps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_blendvpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_blendvps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmppd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmpps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmpsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cmpss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_comisd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_comiss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtdq2pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtdq2ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtpd2dq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtpd2ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtps2dq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtps2pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtsd2si32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtsd2si64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtsd2ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtsi2sd32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtsi2sd64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtsi2ss32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtsi2ss64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtss2sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtss2si32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvtss2si64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvttpd2dq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvttps2dq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvttsd2si32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvttsd2si64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvttss2si32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cvttss2si64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_divpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_divps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_divsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_divss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_dppd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_dpps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_extractps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_haddpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_haddps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_hsubpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_hsubps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_insertps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_lddqu
        unchecked((OpcodeNative)(BAD_CODE)), // INS_lfence
        unchecked((OpcodeNative)(BAD_CODE)), // INS_maskmovdqu
        unchecked((OpcodeNative)(BAD_CODE)), // INS_maxpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_maxps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_maxsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_maxss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_mfence
        unchecked((OpcodeNative)(BAD_CODE)), // INS_minpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_minps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_minsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_minss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movapd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movaps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movd32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movd64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movddup
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movdqa32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movdqu32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movhlps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movhpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movhps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movlhps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movlpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movlps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movmskpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movmskps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movntdq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movntdqa
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movnti32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movnti64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movntpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movntps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movsd_simd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movshdup
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movsldup
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movupd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movups
        unchecked((OpcodeNative)(BAD_CODE)), // INS_mpsadbw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_mulpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_mulps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_mulsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_mulss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_orpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_orps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pabsb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pabsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pabsw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_packssdw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_packsswb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_packusdw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_packuswb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_paddb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_paddd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_paddq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_paddsb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_paddsw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_paddusb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_paddusw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_paddw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_palignr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pandd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pandnd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pavgb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pavgw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pblendvb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pblendw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pcmpeqb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pcmpeqd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pcmpeqq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pcmpeqw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pcmpgtb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pcmpgtd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pcmpgtq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pcmpgtw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pextrb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pextrd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pextrq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_phaddd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pextrw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_phaddsw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_phaddw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_phminposuw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_phsubd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_phsubsw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_phsubw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pinsrb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pinsrd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pinsrq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pinsrw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmaddubsw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmaddwd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmaxsb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmaxsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmaxsw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmaxub
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmaxud
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmaxuw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pminsb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pminsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pminsw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pminub
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pminud
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pminuw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovmskb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovsxbd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovsxbq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovsxbw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovsxdq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovsxwd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovsxwq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovzxbd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovzxbq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovzxbw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovzxdq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovzxwd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmovzxwq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmuldq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmulhrsw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmulhuw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmulhw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmulld
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmuludq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pmullw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pord
        unchecked((OpcodeNative)(BAD_CODE)), // INS_prefetchnta
        unchecked((OpcodeNative)(BAD_CODE)), // INS_prefetcht0
        unchecked((OpcodeNative)(BAD_CODE)), // INS_prefetcht1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_prefetcht2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psadbw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pshufb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pshufd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pshufhw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pshuflw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psignb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psignd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psignw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_pslld
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_pslldq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_psllq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x71)))))), // INS_psllw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_psrad
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x71)))))), // INS_psraw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_psrld
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_psrldq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_psrlq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x71)))))), // INS_psrlw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psubb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psubd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psubq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psubsb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psubsw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psubusb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psubusw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_psubw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ptest
        unchecked((OpcodeNative)(BAD_CODE)), // INS_punpckhbw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_punpckhdq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_punpckhqdq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_punpckhwd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_punpcklbw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_punpckldq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_punpcklqdq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_punpcklwd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pxord
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rcpps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rcpss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_roundpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_roundps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_roundsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_roundss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rsqrtps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rsqrtss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sfence
        unchecked((OpcodeNative)(BAD_CODE)), // INS_shufpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_shufps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sqrtpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sqrtps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sqrtsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sqrtss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_subpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_subps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_subsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_subss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ucomisd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ucomiss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_unpckhpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_unpckhps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_unpcklpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_unpcklps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_xorpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_xorps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_aesdec
        unchecked((OpcodeNative)(BAD_CODE)), // INS_aesdeclast
        unchecked((OpcodeNative)(BAD_CODE)), // INS_aesenc
        unchecked((OpcodeNative)(BAD_CODE)), // INS_aesenclast
        unchecked((OpcodeNative)(BAD_CODE)), // INS_aesimc
        unchecked((OpcodeNative)(BAD_CODE)), // INS_aeskeygenassist
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pclmulqdq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sha1msg1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sha1msg2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sha1nexte
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sha1rnds4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sha256msg1
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sha256msg2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sha256rnds2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_gf2p8affineinvqb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_gf2p8affineqb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_gf2p8mulb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vblendvpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vblendvps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcastf32x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcastsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcastss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextractf32x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vinsertf32x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmaskmovpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmaskmovps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpblendvb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vperm2f128
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermilpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermilpdvar
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermilps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermilpsvar
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vtestpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vtestps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vzeroupper
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcasti32x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtph2ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtps2ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextracti32x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgatherdpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgatherdps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgatherqpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgatherqps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vinserti32x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpblendd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpbroadcastb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpbroadcastd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpbroadcastq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpbroadcastw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vperm2i128
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpgatherdd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpgatherdq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpgatherqd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpgatherqq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmaskmovd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmaskmovq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpsllvd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpsllvq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpsravd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpsrlvd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpsrlvq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd132pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd213pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd231pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd132ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd213ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd231ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd132sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd213sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd231sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd132ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd213ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd231ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddsub132pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddsub213pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddsub231pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddsub132ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddsub213ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddsub231ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsubadd132pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsubadd213pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsubadd231pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsubadd132ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsubadd213ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsubadd231ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub132pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub213pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub231pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub132ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub213ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub231ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub132sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub213sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub231sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub132ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub213ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub231ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd132pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd213pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd231pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd132ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd213ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd231ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd132sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd213sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd231sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd132ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd213ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd231ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub132pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub213pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub231pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub132ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub213ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub231ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub132sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub213sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub231sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub132ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub213ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub231ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_andn
        unchecked((OpcodeNative)(BAD_CODE)), // INS_bextr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_blsi
        unchecked((OpcodeNative)(BAD_CODE)), // INS_blsmsk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_blsr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_bzhi
        unchecked((OpcodeNative)(BAD_CODE)), // INS_mulx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pdep
        unchecked((OpcodeNative)(BAD_CODE)), // INS_pext
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rorx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sarx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_shlx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_shrx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpbusd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpbusds
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpwssd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpwssds
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpwsud
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpwsuds
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpwusd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpwusds
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpwuud
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpwuuds
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpbssd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpbssds
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpbsud
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpbsuds
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpbuud
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpdpbuuds
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbmacor16x16x16
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbmacxor16x16x16
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbitrev
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmadd52huq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmadd52luq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kaddb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kaddd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kaddq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kaddw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kandb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kandd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kandnb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kandnd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kandnq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kandnw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kandq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kandw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kmovb_gpr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kmovb_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kmovd_gpr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kmovd_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kmovq_gpr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kmovq_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kmovw_gpr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kmovw_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_knotb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_knotd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_knotq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_knotw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_korb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kord
        unchecked((OpcodeNative)(BAD_CODE)), // INS_korq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kortestb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kortestd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kortestq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kortestw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_korw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kshiftlb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kshiftld
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kshiftlq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kshiftlw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kshiftrb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kshiftrd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kshiftrq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kshiftrw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ktestb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ktestd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ktestq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ktestw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kunpckbw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kunpckdq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kunpckwd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kxnorb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kxnord
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kxnorq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kxnorw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kxorb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kxord
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kxorq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_kxorw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_valignd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_valignq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vblendmpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vblendmps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcastf32x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcastf32x8
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcastf64x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcastf64x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcasti32x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcasti32x8
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcasti64x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vbroadcasti64x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcmppd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcmpps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcmpsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcmpss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcompresspd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcompressps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtpd2qq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtpd2udq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtpd2uqq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtps2qq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtps2udq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtps2uqq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtqq2pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtqq2ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsd2usi32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsd2usi64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtss2usi32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtss2usi64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttpd2qq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttpd2udq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttpd2uqq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttps2qq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttps2udq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttps2uqq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttsd2usi32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttsd2usi64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttss2usi32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttss2usi64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtudq2pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtudq2ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtuqq2pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtuqq2ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtusi2sd32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtusi2sd64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtusi2ss32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtusi2ss64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vdbpsadbw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vexpandpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vexpandps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextractf32x8
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextractf64x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextractf64x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextracti32x8
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextracti64x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vextracti64x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfixupimmpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfixupimmps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfixupimmsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfixupimmss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfpclasspd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfpclassps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfpclasssd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfpclassss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgatherdpd_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgatherdps_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgatherqpd_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgatherqps_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetexppd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetexpps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetexpsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetexpss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetmantpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetmantps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetmantsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetmantss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vinsertf32x8
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vinsertf64x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vinsertf64x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vinserti32x8
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vinserti64x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vinserti64x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmovdqa64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmovdqu16
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmovdqu64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmovdqu8
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpabsq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpandnq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpandq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpblendmb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpblendmd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpblendmq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpblendmw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpbroadcastb_gpr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpbroadcastd_gpr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpbroadcastq_gpr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpbroadcastw_gpr
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpeqb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpeqd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpeqq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpeqw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpgtb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpgtd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpgtq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpgtw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpub
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpud
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpuq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpuw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcmpw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcompressd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcompressq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpconflictd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpconflictq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermi2d
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermi2pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermi2ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermi2q
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermi2w
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermpd_reg
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermq_reg
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermt2d
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermt2pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermt2ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermt2q
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermt2w
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpexpandd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpexpandq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpgatherdd_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpgatherdq_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpgatherqd_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpgatherqq_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vplzcntd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vplzcntq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmaxsq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmaxuq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpminsq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpminuq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovb2m
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovd2m
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovdb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovdw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovm2b
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovm2d
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovm2q
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovm2w
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovq2m
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovqb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovqd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovqw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovsdb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovsdw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovsqb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovsqd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovsqw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovswb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovusdb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovusdw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovusqb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovusqd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovusqw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovuswb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovw2m
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmovwb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmullq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vporq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprold
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprolq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vprolvd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vprolvq
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprord
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprorq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vprorvd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vprorvq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpscatterdd_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpscatterdq_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpscatterqd_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpscatterqq_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpsllvw
        unchecked((OpcodeNative)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vpsraq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpsravq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpsravw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpsrlvw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpternlogd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpternlogq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vptestmb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vptestmd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vptestmq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vptestmw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vptestnmb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vptestnmd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vptestnmq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vptestnmw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpxorq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrangepd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrangeps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrangesd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrangess
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrcp14pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrcp14ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrcp14sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrcp14ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vreducepd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vreduceps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vreducesd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vreducess
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrndscalepd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrndscaleps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrndscalesd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrndscaless
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrsqrt14pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrsqrt14ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrsqrt14sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrsqrt14ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vscalefpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vscalefps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vscalefsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vscalefss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vscatterdpd_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vscatterdps_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vscatterqpd_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vscatterqps_msk
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vshuff32x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vshuff64x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vshufi32x4
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vshufi64x2
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermi2b
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpermt2b
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpmultishiftqb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcompressb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpcompressw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpexpandb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpexpandw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpopcntb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpopcntd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpopcntq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpopcntw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshldd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshldq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshldvd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshldvq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshldvw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshldw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshrdd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshrdq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshrdvd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshrdvq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshrdvw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshrdw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vpshufbitqmb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vaddph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vaddsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcmpph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcmpsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcomish
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtdq2ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtne2ps2bf16
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtneps2bf16
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtpd2ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtph2dq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtph2pd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtph2psx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtph2qq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtph2udq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtph2uqq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtph2uw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtph2w
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtps2phx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtqq2ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsd2sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsh2sd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsh2si32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsh2si64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsh2ss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsh2usi32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsh2usi64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsi2sh32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtsi2sh64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtss2sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttph2dq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttph2qq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttph2udq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttph2uqq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttph2uw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttph2w
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttsh2si32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttsh2si64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttsh2usi32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttsh2usi64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtudq2ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtuqq2ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtusi2sh32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtusi2sh64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtuw2ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtw2ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vdivph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vdivsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vdpbf16ps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfcmaddcph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfcmaddcsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfcmulcph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfcmulcsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmulcph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmulcsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd132ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd213ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd231ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd132sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd213sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmadd231sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddcph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddcsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddsub132ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddsub213ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmaddsub231ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub132ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub213ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub231ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub132sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub213sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsub231sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsubadd132ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsubadd213ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfmsubadd231ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd132ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd213ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd231ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd132sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd213sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmadd231sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub132ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub213ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub231ph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub132sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub213sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfnmsub231sh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfpclassph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vfpclasssh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetexpph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetexpsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetmantph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vgetmantsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmaxph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmaxsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vminsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vminph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmovsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmovw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmulph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmulsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrcpph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrcpsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vreduceph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vreducesh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrndscaleph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrndscalesh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrsqrtph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vrsqrtsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vscalefph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vscalefsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vsqrtph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vsqrtsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vsubph
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vsubsh
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vucomish
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vp2intersectd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vp2intersectq
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcomxsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcomxss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtps2ibs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvtps2iubs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttpd2dqs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttpd2qqs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttpd2udqs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttpd2uqqs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttps2dqs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttps2ibs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttps2iubs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttps2qqs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttps2udqs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttps2uqqs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttsd2sis32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttsd2sis64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttsd2usis32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttsd2usis64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttss2sis32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttss2sis64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttss2usis32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vcvttss2usis64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vminmaxpd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vminmaxps
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vminmaxsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vminmaxss
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmovd_simd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmovw_simd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vmpsadbw
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vucomxsd
        unchecked((OpcodeNative)(BAD_CODE)), // INS_vucomxss
#if TARGET_AMD64
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpo
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpno
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpb
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpae
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpe
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpne
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpbe
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpa
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmps
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpns
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpt
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpf
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpl
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpge
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmple
        unchecked((OpcodeNative)(0x0003880)), // INS_ccmpg
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovo
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovno
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovb
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovae
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmove
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovne
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovbe
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmova
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovs
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovns
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovp
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovnp
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovl
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovge
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovle
        unchecked((OpcodeNative)(BAD_CODE)), // INS_cfcmovg
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctesto
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestno
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestb
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestae
        unchecked((OpcodeNative)(0x00000F6)), // INS_cteste
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestne
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestbe
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctesta
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctests
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestns
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestt
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestf
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestl
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestge
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestle
        unchecked((OpcodeNative)(0x00000F6)), // INS_ctestg
        unchecked((OpcodeNative)(BAD_CODE)), // INS_crc32_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movbe_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_seto_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setno_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setb_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setae_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sete_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setne_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setbe_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_seta_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sets_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setns_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setp_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setnp_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setl_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setge_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setle_apx
        unchecked((OpcodeNative)(BAD_CODE)), // INS_setg_apx
#endif
        unchecked((OpcodeNative)(BAD_CODE)), // INS_crc32
        unchecked((OpcodeNative)(BAD_CODE)), // INS_tzcnt
#if TARGET_AMD64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_tzcnt_apx
#endif
        unchecked((OpcodeNative)(BAD_CODE)), // INS_lzcnt
#if TARGET_AMD64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_lzcnt_apx
#endif
        unchecked((OpcodeNative)(BAD_CODE)), // INS_movbe
        unchecked((OpcodeNative)(BAD_CODE)), // INS_popcnt
#if TARGET_AMD64
        unchecked((OpcodeNative)(BAD_CODE)), // INS_popcnt_apx
#endif
        unchecked((OpcodeNative)(BAD_CODE)), // INS_tpause
        unchecked((OpcodeNative)(BAD_CODE)), // INS_umonitor
        unchecked((OpcodeNative)(BAD_CODE)), // INS_umwait
        unchecked((OpcodeNative)(BAD_CODE)), // INS_neg
        unchecked((OpcodeNative)(BAD_CODE)), // INS_not
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rol
        unchecked((OpcodeNative)(0x0000D0)), // INS_rol_1
        unchecked((OpcodeNative)(0x0000C0)), // INS_rol_N
        unchecked((OpcodeNative)(BAD_CODE)), // INS_ror
        unchecked((OpcodeNative)(0x0008D0)), // INS_ror_1
        unchecked((OpcodeNative)(0x0008C0)), // INS_ror_N
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rcl
        unchecked((OpcodeNative)(0x0010D0)), // INS_rcl_1
        unchecked((OpcodeNative)(0x0010C0)), // INS_rcl_N
        unchecked((OpcodeNative)(BAD_CODE)), // INS_rcr
        unchecked((OpcodeNative)(0x0018D0)), // INS_rcr_1
        unchecked((OpcodeNative)(0x0018C0)), // INS_rcr_N
        unchecked((OpcodeNative)(BAD_CODE)), // INS_shl
        unchecked((OpcodeNative)(0x0020D0)), // INS_shl_1
        unchecked((OpcodeNative)(0x0020C0)), // INS_shl_N
        unchecked((OpcodeNative)(BAD_CODE)), // INS_shr
        unchecked((OpcodeNative)(0x0028D0)), // INS_shr_1
        unchecked((OpcodeNative)(0x0028C0)), // INS_shr_N
        unchecked((OpcodeNative)(BAD_CODE)), // INS_sar
        unchecked((OpcodeNative)(0x0038D0)), // INS_sar_1
        unchecked((OpcodeNative)(0x0038C0)), // INS_sar_N
        unchecked((OpcodeNative)(0x0000C2)), // INS_ret
        unchecked((OpcodeNative)(0x0000E2)), // INS_loop
        unchecked((OpcodeNative)(0x0000E8)), // INS_call
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_AMD64
#endif
#if TARGET_X86
#endif
#elif TARGET_ARM
#if !TARGET_ARM
#error Unexpected target type
#endif
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
#elif TARGET_ARM64
#if !TARGET_ARM64
#error Unexpected target type
#endif
#if FEATURE_LOOP_ALIGN
#endif
#if !TARGET_ARM64
#error Unexpected target type
#endif
#elif TARGET_LOONGARCH64
#if !TARGET_LOONGARCH64
#error Unexpected target type
#endif
#if FEATURE_SIMD
#endif
#elif TARGET_RISCV64
#if !TARGET_RISCV64
#error Unexpected target type
#endif
#elif TARGET_WASM
#if !TARGET_WASM
#error Unexpected target type
#endif
#else
#error Unsupported or unset target architecture
#endif
    ];

    private static ReadOnlySpan<uint> insCodesMR => [
#if TARGET_XARCH
#if !TARGET_XARCH
#error Unexpected target type
#endif
        unchecked((uint)(BAD_CODE)), // INS_invalid
        unchecked((uint)(0x0030FE)), // INS_push
        unchecked((uint)(0x00008E)), // INS_pop
        unchecked((uint)(0x0030FE)), // INS_push_hide
        unchecked((uint)(0x00008E)), // INS_pop_hide
        unchecked((uint)(0x0030FF)), // INS_push2
        unchecked((uint)(0x00008F)), // INS_pop2
        unchecked((uint)(0x0000FE)), // INS_inc
        unchecked((uint)(0x0000FE)), // INS_inc_l
        unchecked((uint)(0x0008FE)), // INS_dec
        unchecked((uint)(0x0008FE)), // INS_dec_l
        unchecked((uint)(0x0F00C8)), // INS_bswap
        unchecked((uint)(0x000000)), // INS_add
        unchecked((uint)(0x000008)), // INS_or
        unchecked((uint)(0x000010)), // INS_adc
        unchecked((uint)(0x000018)), // INS_sbb
        unchecked((uint)(0x000020)), // INS_and
        unchecked((uint)(0x000028)), // INS_sub
        unchecked((uint)(0x000028)), // INS_sub_hide
        unchecked((uint)(0x000030)), // INS_xor
        unchecked((uint)(0x000038)), // INS_cmp
        unchecked((uint)(0x000084)), // INS_test
        unchecked((uint)(0x000088)), // INS_mov
        unchecked((uint)(BAD_CODE)), // INS_lea
        unchecked((uint)(0x0F00A3)), // INS_bt
        unchecked((uint)(0x0F00AB)), // INS_bts
        unchecked((uint)(0x0F00B3)), // INS_btr
        unchecked((uint)(0x0F00BB)), // INS_btc
        unchecked((uint)(BAD_CODE)), // INS_bsr
        unchecked((uint)(BAD_CODE)), // INS_bsf
        unchecked((uint)(BAD_CODE)), // INS_movsx
#if TARGET_AMD64
        unchecked((uint)(BAD_CODE)), // INS_movsxd
#endif
        unchecked((uint)(BAD_CODE)), // INS_movzx
        unchecked((uint)(BAD_CODE)), // INS_cmovo
        unchecked((uint)(BAD_CODE)), // INS_cmovno
        unchecked((uint)(BAD_CODE)), // INS_cmovb
        unchecked((uint)(BAD_CODE)), // INS_cmovae
        unchecked((uint)(BAD_CODE)), // INS_cmove
        unchecked((uint)(BAD_CODE)), // INS_cmovne
        unchecked((uint)(BAD_CODE)), // INS_cmovbe
        unchecked((uint)(BAD_CODE)), // INS_cmova
        unchecked((uint)(BAD_CODE)), // INS_cmovs
        unchecked((uint)(BAD_CODE)), // INS_cmovns
        unchecked((uint)(BAD_CODE)), // INS_cmovp
        unchecked((uint)(BAD_CODE)), // INS_cmovnp
        unchecked((uint)(BAD_CODE)), // INS_cmovl
        unchecked((uint)(BAD_CODE)), // INS_cmovge
        unchecked((uint)(BAD_CODE)), // INS_cmovle
        unchecked((uint)(BAD_CODE)), // INS_cmovg
        unchecked((uint)(0x000086)), // INS_xchg
        unchecked((uint)(0x0F00AC)), // INS_imul
        unchecked((uint)(BAD_CODE)), // INS_imul_AX
        unchecked((uint)(BAD_CODE)), // INS_imul_CX
        unchecked((uint)(BAD_CODE)), // INS_imul_DX
        unchecked((uint)(BAD_CODE)), // INS_imul_BX
        unchecked((uint)(BAD_CODE)), // INS_imul_SP
        unchecked((uint)(BAD_CODE)), // INS_imul_BP
        unchecked((uint)(BAD_CODE)), // INS_imul_SI
        unchecked((uint)(BAD_CODE)), // INS_imul_DI
#if TARGET_AMD64
        unchecked((uint)(BAD_CODE)), // INS_imul_08
        unchecked((uint)(BAD_CODE)), // INS_imul_09
        unchecked((uint)(BAD_CODE)), // INS_imul_10
        unchecked((uint)(BAD_CODE)), // INS_imul_11
        unchecked((uint)(BAD_CODE)), // INS_imul_12
        unchecked((uint)(BAD_CODE)), // INS_imul_13
        unchecked((uint)(BAD_CODE)), // INS_imul_14
        unchecked((uint)(BAD_CODE)), // INS_imul_15
        unchecked((uint)(BAD_CODE)), // INS_imul_16
        unchecked((uint)(BAD_CODE)), // INS_imul_17
        unchecked((uint)(BAD_CODE)), // INS_imul_18
        unchecked((uint)(BAD_CODE)), // INS_imul_19
        unchecked((uint)(BAD_CODE)), // INS_imul_20
        unchecked((uint)(BAD_CODE)), // INS_imul_21
        unchecked((uint)(BAD_CODE)), // INS_imul_22
        unchecked((uint)(BAD_CODE)), // INS_imul_23
        unchecked((uint)(BAD_CODE)), // INS_imul_24
        unchecked((uint)(BAD_CODE)), // INS_imul_25
        unchecked((uint)(BAD_CODE)), // INS_imul_26
        unchecked((uint)(BAD_CODE)), // INS_imul_27
        unchecked((uint)(BAD_CODE)), // INS_imul_28
        unchecked((uint)(BAD_CODE)), // INS_imul_29
        unchecked((uint)(BAD_CODE)), // INS_imul_30
        unchecked((uint)(BAD_CODE)), // INS_imul_31
#endif
        unchecked((uint)(BAD_CODE)), // INS_addpd
        unchecked((uint)(BAD_CODE)), // INS_addps
        unchecked((uint)(BAD_CODE)), // INS_addsd
        unchecked((uint)(BAD_CODE)), // INS_addss
        unchecked((uint)(BAD_CODE)), // INS_addsubpd
        unchecked((uint)(BAD_CODE)), // INS_addsubps
        unchecked((uint)(BAD_CODE)), // INS_andnpd
        unchecked((uint)(BAD_CODE)), // INS_andnps
        unchecked((uint)(BAD_CODE)), // INS_andpd
        unchecked((uint)(BAD_CODE)), // INS_andps
        unchecked((uint)(BAD_CODE)), // INS_blendpd
        unchecked((uint)(BAD_CODE)), // INS_blendps
        unchecked((uint)(BAD_CODE)), // INS_blendvpd
        unchecked((uint)(BAD_CODE)), // INS_blendvps
        unchecked((uint)(BAD_CODE)), // INS_cmppd
        unchecked((uint)(BAD_CODE)), // INS_cmpps
        unchecked((uint)(BAD_CODE)), // INS_cmpsd
        unchecked((uint)(BAD_CODE)), // INS_cmpss
        unchecked((uint)(BAD_CODE)), // INS_comisd
        unchecked((uint)(BAD_CODE)), // INS_comiss
        unchecked((uint)(BAD_CODE)), // INS_cvtdq2pd
        unchecked((uint)(BAD_CODE)), // INS_cvtdq2ps
        unchecked((uint)(BAD_CODE)), // INS_cvtpd2dq
        unchecked((uint)(BAD_CODE)), // INS_cvtpd2ps
        unchecked((uint)(BAD_CODE)), // INS_cvtps2dq
        unchecked((uint)(BAD_CODE)), // INS_cvtps2pd
        unchecked((uint)(BAD_CODE)), // INS_cvtsd2si32
        unchecked((uint)(BAD_CODE)), // INS_cvtsd2si64
        unchecked((uint)(BAD_CODE)), // INS_cvtsd2ss
        unchecked((uint)(BAD_CODE)), // INS_cvtsi2sd32
        unchecked((uint)(BAD_CODE)), // INS_cvtsi2sd64
        unchecked((uint)(BAD_CODE)), // INS_cvtsi2ss32
        unchecked((uint)(BAD_CODE)), // INS_cvtsi2ss64
        unchecked((uint)(BAD_CODE)), // INS_cvtss2sd
        unchecked((uint)(BAD_CODE)), // INS_cvtss2si32
        unchecked((uint)(BAD_CODE)), // INS_cvtss2si64
        unchecked((uint)(BAD_CODE)), // INS_cvttpd2dq
        unchecked((uint)(BAD_CODE)), // INS_cvttps2dq
        unchecked((uint)(BAD_CODE)), // INS_cvttsd2si32
        unchecked((uint)(BAD_CODE)), // INS_cvttsd2si64
        unchecked((uint)(BAD_CODE)), // INS_cvttss2si32
        unchecked((uint)(BAD_CODE)), // INS_cvttss2si64
        unchecked((uint)(BAD_CODE)), // INS_divpd
        unchecked((uint)(BAD_CODE)), // INS_divps
        unchecked((uint)(BAD_CODE)), // INS_divsd
        unchecked((uint)(BAD_CODE)), // INS_divss
        unchecked((uint)(BAD_CODE)), // INS_dppd
        unchecked((uint)(BAD_CODE)), // INS_dpps
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x17)))) << 8)))), // INS_extractps
        unchecked((uint)(BAD_CODE)), // INS_haddpd
        unchecked((uint)(BAD_CODE)), // INS_haddps
        unchecked((uint)(BAD_CODE)), // INS_hsubpd
        unchecked((uint)(BAD_CODE)), // INS_hsubps
        unchecked((uint)(BAD_CODE)), // INS_insertps
        unchecked((uint)(BAD_CODE)), // INS_lddqu
        unchecked((uint)(0x000FE8AE)), // INS_lfence
        unchecked((uint)(BAD_CODE)), // INS_maskmovdqu
        unchecked((uint)(BAD_CODE)), // INS_maxpd
        unchecked((uint)(BAD_CODE)), // INS_maxps
        unchecked((uint)(BAD_CODE)), // INS_maxsd
        unchecked((uint)(BAD_CODE)), // INS_maxss
        unchecked((uint)(0x000FF0AE)), // INS_mfence
        unchecked((uint)(BAD_CODE)), // INS_minpd
        unchecked((uint)(BAD_CODE)), // INS_minps
        unchecked((uint)(BAD_CODE)), // INS_minsd
        unchecked((uint)(BAD_CODE)), // INS_minss
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x29)))))), // INS_movapd
        unchecked((uint)(((((0x0f)) << 16) | (((0x29)))))), // INS_movaps
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd32
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd64
        unchecked((uint)(BAD_CODE)), // INS_movddup
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqa32
        unchecked((uint)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqu32
        unchecked((uint)(BAD_CODE)), // INS_movhlps
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x17)))))), // INS_movhpd
        unchecked((uint)(((((0x0f)) << 16) | (((0x17)))))), // INS_movhps
        unchecked((uint)(BAD_CODE)), // INS_movlhps
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x13)))))), // INS_movlpd
        unchecked((uint)(((((0x0f)) << 16) | (((0x13)))))), // INS_movlps
        unchecked((uint)(BAD_CODE)), // INS_movmskpd
        unchecked((uint)(BAD_CODE)), // INS_movmskps
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE7)))))), // INS_movntdq
        unchecked((uint)(BAD_CODE)), // INS_movntdqa
        unchecked((uint)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti32
        unchecked((uint)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti64
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2B)))))), // INS_movntpd
        unchecked((uint)(((((0x0f)) << 16) | (((0x2B)))))), // INS_movntps
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_movq
        unchecked((uint)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movsd_simd
        unchecked((uint)(BAD_CODE)), // INS_movshdup
        unchecked((uint)(BAD_CODE)), // INS_movsldup
        unchecked((uint)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movss
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movupd
        unchecked((uint)(((((0x0f)) << 16) | (((0x11)))))), // INS_movups
        unchecked((uint)(BAD_CODE)), // INS_mpsadbw
        unchecked((uint)(BAD_CODE)), // INS_mulpd
        unchecked((uint)(BAD_CODE)), // INS_mulps
        unchecked((uint)(BAD_CODE)), // INS_mulsd
        unchecked((uint)(BAD_CODE)), // INS_mulss
        unchecked((uint)(BAD_CODE)), // INS_orpd
        unchecked((uint)(BAD_CODE)), // INS_orps
        unchecked((uint)(BAD_CODE)), // INS_pabsb
        unchecked((uint)(BAD_CODE)), // INS_pabsd
        unchecked((uint)(BAD_CODE)), // INS_pabsw
        unchecked((uint)(BAD_CODE)), // INS_packssdw
        unchecked((uint)(BAD_CODE)), // INS_packsswb
        unchecked((uint)(BAD_CODE)), // INS_packusdw
        unchecked((uint)(BAD_CODE)), // INS_packuswb
        unchecked((uint)(BAD_CODE)), // INS_paddb
        unchecked((uint)(BAD_CODE)), // INS_paddd
        unchecked((uint)(BAD_CODE)), // INS_paddq
        unchecked((uint)(BAD_CODE)), // INS_paddsb
        unchecked((uint)(BAD_CODE)), // INS_paddsw
        unchecked((uint)(BAD_CODE)), // INS_paddusb
        unchecked((uint)(BAD_CODE)), // INS_paddusw
        unchecked((uint)(BAD_CODE)), // INS_paddw
        unchecked((uint)(BAD_CODE)), // INS_palignr
        unchecked((uint)(BAD_CODE)), // INS_pandd
        unchecked((uint)(BAD_CODE)), // INS_pandnd
        unchecked((uint)(BAD_CODE)), // INS_pavgb
        unchecked((uint)(BAD_CODE)), // INS_pavgw
        unchecked((uint)(BAD_CODE)), // INS_pblendvb
        unchecked((uint)(BAD_CODE)), // INS_pblendw
        unchecked((uint)(BAD_CODE)), // INS_pcmpeqb
        unchecked((uint)(BAD_CODE)), // INS_pcmpeqd
        unchecked((uint)(BAD_CODE)), // INS_pcmpeqq
        unchecked((uint)(BAD_CODE)), // INS_pcmpeqw
        unchecked((uint)(BAD_CODE)), // INS_pcmpgtb
        unchecked((uint)(BAD_CODE)), // INS_pcmpgtd
        unchecked((uint)(BAD_CODE)), // INS_pcmpgtq
        unchecked((uint)(BAD_CODE)), // INS_pcmpgtw
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x14)))) << 8)))), // INS_pextrb
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrq
        unchecked((uint)(BAD_CODE)), // INS_phaddd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x15)))) << 8)))), // INS_pextrw
        unchecked((uint)(BAD_CODE)), // INS_phaddsw
        unchecked((uint)(BAD_CODE)), // INS_phaddw
        unchecked((uint)(BAD_CODE)), // INS_phminposuw
        unchecked((uint)(BAD_CODE)), // INS_phsubd
        unchecked((uint)(BAD_CODE)), // INS_phsubsw
        unchecked((uint)(BAD_CODE)), // INS_phsubw
        unchecked((uint)(BAD_CODE)), // INS_pinsrb
        unchecked((uint)(BAD_CODE)), // INS_pinsrd
        unchecked((uint)(BAD_CODE)), // INS_pinsrq
        unchecked((uint)(BAD_CODE)), // INS_pinsrw
        unchecked((uint)(BAD_CODE)), // INS_pmaddubsw
        unchecked((uint)(BAD_CODE)), // INS_pmaddwd
        unchecked((uint)(BAD_CODE)), // INS_pmaxsb
        unchecked((uint)(BAD_CODE)), // INS_pmaxsd
        unchecked((uint)(BAD_CODE)), // INS_pmaxsw
        unchecked((uint)(BAD_CODE)), // INS_pmaxub
        unchecked((uint)(BAD_CODE)), // INS_pmaxud
        unchecked((uint)(BAD_CODE)), // INS_pmaxuw
        unchecked((uint)(BAD_CODE)), // INS_pminsb
        unchecked((uint)(BAD_CODE)), // INS_pminsd
        unchecked((uint)(BAD_CODE)), // INS_pminsw
        unchecked((uint)(BAD_CODE)), // INS_pminub
        unchecked((uint)(BAD_CODE)), // INS_pminud
        unchecked((uint)(BAD_CODE)), // INS_pminuw
        unchecked((uint)(BAD_CODE)), // INS_pmovmskb
        unchecked((uint)(BAD_CODE)), // INS_pmovsxbd
        unchecked((uint)(BAD_CODE)), // INS_pmovsxbq
        unchecked((uint)(BAD_CODE)), // INS_pmovsxbw
        unchecked((uint)(BAD_CODE)), // INS_pmovsxdq
        unchecked((uint)(BAD_CODE)), // INS_pmovsxwd
        unchecked((uint)(BAD_CODE)), // INS_pmovsxwq
        unchecked((uint)(BAD_CODE)), // INS_pmovzxbd
        unchecked((uint)(BAD_CODE)), // INS_pmovzxbq
        unchecked((uint)(BAD_CODE)), // INS_pmovzxbw
        unchecked((uint)(BAD_CODE)), // INS_pmovzxdq
        unchecked((uint)(BAD_CODE)), // INS_pmovzxwd
        unchecked((uint)(BAD_CODE)), // INS_pmovzxwq
        unchecked((uint)(BAD_CODE)), // INS_pmuldq
        unchecked((uint)(BAD_CODE)), // INS_pmulhrsw
        unchecked((uint)(BAD_CODE)), // INS_pmulhuw
        unchecked((uint)(BAD_CODE)), // INS_pmulhw
        unchecked((uint)(BAD_CODE)), // INS_pmulld
        unchecked((uint)(BAD_CODE)), // INS_pmuludq
        unchecked((uint)(BAD_CODE)), // INS_pmullw
        unchecked((uint)(BAD_CODE)), // INS_pord
        unchecked((uint)(0x000F0018)), // INS_prefetchnta
        unchecked((uint)(0x000F0818)), // INS_prefetcht0
        unchecked((uint)(0x000F1018)), // INS_prefetcht1
        unchecked((uint)(0x000F1818)), // INS_prefetcht2
        unchecked((uint)(BAD_CODE)), // INS_psadbw
        unchecked((uint)(BAD_CODE)), // INS_pshufb
        unchecked((uint)(BAD_CODE)), // INS_pshufd
        unchecked((uint)(BAD_CODE)), // INS_pshufhw
        unchecked((uint)(BAD_CODE)), // INS_pshuflw
        unchecked((uint)(BAD_CODE)), // INS_psignb
        unchecked((uint)(BAD_CODE)), // INS_psignd
        unchecked((uint)(BAD_CODE)), // INS_psignw
        unchecked((uint)(BAD_CODE)), // INS_pslld
        unchecked((uint)(BAD_CODE)), // INS_pslldq
        unchecked((uint)(BAD_CODE)), // INS_psllq
        unchecked((uint)(BAD_CODE)), // INS_psllw
        unchecked((uint)(BAD_CODE)), // INS_psrad
        unchecked((uint)(BAD_CODE)), // INS_psraw
        unchecked((uint)(BAD_CODE)), // INS_psrld
        unchecked((uint)(BAD_CODE)), // INS_psrldq
        unchecked((uint)(BAD_CODE)), // INS_psrlq
        unchecked((uint)(BAD_CODE)), // INS_psrlw
        unchecked((uint)(BAD_CODE)), // INS_psubb
        unchecked((uint)(BAD_CODE)), // INS_psubd
        unchecked((uint)(BAD_CODE)), // INS_psubq
        unchecked((uint)(BAD_CODE)), // INS_psubsb
        unchecked((uint)(BAD_CODE)), // INS_psubsw
        unchecked((uint)(BAD_CODE)), // INS_psubusb
        unchecked((uint)(BAD_CODE)), // INS_psubusw
        unchecked((uint)(BAD_CODE)), // INS_psubw
        unchecked((uint)(BAD_CODE)), // INS_ptest
        unchecked((uint)(BAD_CODE)), // INS_punpckhbw
        unchecked((uint)(BAD_CODE)), // INS_punpckhdq
        unchecked((uint)(BAD_CODE)), // INS_punpckhqdq
        unchecked((uint)(BAD_CODE)), // INS_punpckhwd
        unchecked((uint)(BAD_CODE)), // INS_punpcklbw
        unchecked((uint)(BAD_CODE)), // INS_punpckldq
        unchecked((uint)(BAD_CODE)), // INS_punpcklqdq
        unchecked((uint)(BAD_CODE)), // INS_punpcklwd
        unchecked((uint)(BAD_CODE)), // INS_pxord
        unchecked((uint)(BAD_CODE)), // INS_rcpps
        unchecked((uint)(BAD_CODE)), // INS_rcpss
        unchecked((uint)(BAD_CODE)), // INS_roundpd
        unchecked((uint)(BAD_CODE)), // INS_roundps
        unchecked((uint)(BAD_CODE)), // INS_roundsd
        unchecked((uint)(BAD_CODE)), // INS_roundss
        unchecked((uint)(BAD_CODE)), // INS_rsqrtps
        unchecked((uint)(BAD_CODE)), // INS_rsqrtss
        unchecked((uint)(0x000FF8AE)), // INS_sfence
        unchecked((uint)(BAD_CODE)), // INS_shufpd
        unchecked((uint)(BAD_CODE)), // INS_shufps
        unchecked((uint)(BAD_CODE)), // INS_sqrtpd
        unchecked((uint)(BAD_CODE)), // INS_sqrtps
        unchecked((uint)(BAD_CODE)), // INS_sqrtsd
        unchecked((uint)(BAD_CODE)), // INS_sqrtss
        unchecked((uint)(BAD_CODE)), // INS_subpd
        unchecked((uint)(BAD_CODE)), // INS_subps
        unchecked((uint)(BAD_CODE)), // INS_subsd
        unchecked((uint)(BAD_CODE)), // INS_subss
        unchecked((uint)(BAD_CODE)), // INS_ucomisd
        unchecked((uint)(BAD_CODE)), // INS_ucomiss
        unchecked((uint)(BAD_CODE)), // INS_unpckhpd
        unchecked((uint)(BAD_CODE)), // INS_unpckhps
        unchecked((uint)(BAD_CODE)), // INS_unpcklpd
        unchecked((uint)(BAD_CODE)), // INS_unpcklps
        unchecked((uint)(BAD_CODE)), // INS_xorpd
        unchecked((uint)(BAD_CODE)), // INS_xorps
        unchecked((uint)(BAD_CODE)), // INS_aesdec
        unchecked((uint)(BAD_CODE)), // INS_aesdeclast
        unchecked((uint)(BAD_CODE)), // INS_aesenc
        unchecked((uint)(BAD_CODE)), // INS_aesenclast
        unchecked((uint)(BAD_CODE)), // INS_aesimc
        unchecked((uint)(BAD_CODE)), // INS_aeskeygenassist
        unchecked((uint)(BAD_CODE)), // INS_pclmulqdq
        unchecked((uint)(BAD_CODE)), // INS_sha1msg1
        unchecked((uint)(BAD_CODE)), // INS_sha1msg2
        unchecked((uint)(BAD_CODE)), // INS_sha1nexte
        unchecked((uint)(BAD_CODE)), // INS_sha1rnds4
        unchecked((uint)(BAD_CODE)), // INS_sha256msg1
        unchecked((uint)(BAD_CODE)), // INS_sha256msg2
        unchecked((uint)(BAD_CODE)), // INS_sha256rnds2
        unchecked((uint)(BAD_CODE)), // INS_gf2p8affineinvqb
        unchecked((uint)(BAD_CODE)), // INS_gf2p8affineqb
        unchecked((uint)(BAD_CODE)), // INS_gf2p8mulb
        unchecked((uint)(BAD_CODE)), // INS_vblendvpd
        unchecked((uint)(BAD_CODE)), // INS_vblendvps
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastf32x4
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastsd
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastss
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf32x4
        unchecked((uint)(BAD_CODE)), // INS_vinsertf32x4
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2F)))) << 8)))), // INS_vmaskmovpd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2E)))) << 8)))), // INS_vmaskmovps
        unchecked((uint)(BAD_CODE)), // INS_vpblendvb
        unchecked((uint)(BAD_CODE)), // INS_vperm2f128
        unchecked((uint)(BAD_CODE)), // INS_vpermilpd
        unchecked((uint)(BAD_CODE)), // INS_vpermilpdvar
        unchecked((uint)(BAD_CODE)), // INS_vpermilps
        unchecked((uint)(BAD_CODE)), // INS_vpermilpsvar
        unchecked((uint)(BAD_CODE)), // INS_vtestpd
        unchecked((uint)(BAD_CODE)), // INS_vtestps
        unchecked((uint)(0xC577F8)), // INS_vzeroupper
        unchecked((uint)(BAD_CODE)), // INS_vbroadcasti32x4
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2ps
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1D)))) << 8)))), // INS_vcvtps2ph
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti32x4
        unchecked((uint)(BAD_CODE)), // INS_vgatherdpd
        unchecked((uint)(BAD_CODE)), // INS_vgatherdps
        unchecked((uint)(BAD_CODE)), // INS_vgatherqpd
        unchecked((uint)(BAD_CODE)), // INS_vgatherqps
        unchecked((uint)(BAD_CODE)), // INS_vinserti32x4
        unchecked((uint)(BAD_CODE)), // INS_vpblendd
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastb
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastd
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastq
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastw
        unchecked((uint)(BAD_CODE)), // INS_vperm2i128
        unchecked((uint)(BAD_CODE)), // INS_vpermd
        unchecked((uint)(BAD_CODE)), // INS_vpermpd
        unchecked((uint)(BAD_CODE)), // INS_vpermps
        unchecked((uint)(BAD_CODE)), // INS_vpermq
        unchecked((uint)(BAD_CODE)), // INS_vpgatherdd
        unchecked((uint)(BAD_CODE)), // INS_vpgatherdq
        unchecked((uint)(BAD_CODE)), // INS_vpgatherqd
        unchecked((uint)(BAD_CODE)), // INS_vpgatherqq
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovq
        unchecked((uint)(BAD_CODE)), // INS_vpsllvd
        unchecked((uint)(BAD_CODE)), // INS_vpsllvq
        unchecked((uint)(BAD_CODE)), // INS_vpsravd
        unchecked((uint)(BAD_CODE)), // INS_vpsrlvd
        unchecked((uint)(BAD_CODE)), // INS_vpsrlvq
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132pd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213pd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231pd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132ps
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213ps
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231ps
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132sd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213sd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231sd
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132ss
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213ss
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231ss
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub132pd
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub213pd
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub231pd
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub132ps
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub213ps
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub231ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd132pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd213pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd231pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd132ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd213ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd231ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231pd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231ps
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132sd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213sd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231sd
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132ss
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213ss
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231pd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231ps
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231sd
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213ss
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231ss
        unchecked((uint)(BAD_CODE)), // INS_andn
        unchecked((uint)(BAD_CODE)), // INS_bextr
        unchecked((uint)(BAD_CODE)), // INS_blsi
        unchecked((uint)(BAD_CODE)), // INS_blsmsk
        unchecked((uint)(BAD_CODE)), // INS_blsr
        unchecked((uint)(BAD_CODE)), // INS_bzhi
        unchecked((uint)(BAD_CODE)), // INS_mulx
        unchecked((uint)(BAD_CODE)), // INS_pdep
        unchecked((uint)(BAD_CODE)), // INS_pext
        unchecked((uint)(BAD_CODE)), // INS_rorx
        unchecked((uint)(BAD_CODE)), // INS_sarx
        unchecked((uint)(BAD_CODE)), // INS_shlx
        unchecked((uint)(BAD_CODE)), // INS_shrx
        unchecked((uint)(BAD_CODE)), // INS_vpdpbusd
        unchecked((uint)(BAD_CODE)), // INS_vpdpbusds
        unchecked((uint)(BAD_CODE)), // INS_vpdpwssd
        unchecked((uint)(BAD_CODE)), // INS_vpdpwssds
        unchecked((uint)(BAD_CODE)), // INS_vpdpwsud
        unchecked((uint)(BAD_CODE)), // INS_vpdpwsuds
        unchecked((uint)(BAD_CODE)), // INS_vpdpwusd
        unchecked((uint)(BAD_CODE)), // INS_vpdpwusds
        unchecked((uint)(BAD_CODE)), // INS_vpdpwuud
        unchecked((uint)(BAD_CODE)), // INS_vpdpwuuds
        unchecked((uint)(BAD_CODE)), // INS_vpdpbssd
        unchecked((uint)(BAD_CODE)), // INS_vpdpbssds
        unchecked((uint)(BAD_CODE)), // INS_vpdpbsud
        unchecked((uint)(BAD_CODE)), // INS_vpdpbsuds
        unchecked((uint)(BAD_CODE)), // INS_vpdpbuud
        unchecked((uint)(BAD_CODE)), // INS_vpdpbuuds
        unchecked((uint)(BAD_CODE)), // INS_vbmacor16x16x16
        unchecked((uint)(BAD_CODE)), // INS_vbmacxor16x16x16
        unchecked((uint)(BAD_CODE)), // INS_vbitrev
        unchecked((uint)(BAD_CODE)), // INS_vpmadd52huq
        unchecked((uint)(BAD_CODE)), // INS_vpmadd52luq
        unchecked((uint)(BAD_CODE)), // INS_kaddb
        unchecked((uint)(BAD_CODE)), // INS_kaddd
        unchecked((uint)(BAD_CODE)), // INS_kaddq
        unchecked((uint)(BAD_CODE)), // INS_kaddw
        unchecked((uint)(BAD_CODE)), // INS_kandb
        unchecked((uint)(BAD_CODE)), // INS_kandd
        unchecked((uint)(BAD_CODE)), // INS_kandnb
        unchecked((uint)(BAD_CODE)), // INS_kandnd
        unchecked((uint)(BAD_CODE)), // INS_kandnq
        unchecked((uint)(BAD_CODE)), // INS_kandnw
        unchecked((uint)(BAD_CODE)), // INS_kandq
        unchecked((uint)(BAD_CODE)), // INS_kandw
        unchecked((uint)(BAD_CODE)), // INS_kmovb_gpr
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovb_msk
        unchecked((uint)(BAD_CODE)), // INS_kmovd_gpr
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovd_msk
        unchecked((uint)(BAD_CODE)), // INS_kmovq_gpr
        unchecked((uint)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovq_msk
        unchecked((uint)(BAD_CODE)), // INS_kmovw_gpr
        unchecked((uint)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovw_msk
        unchecked((uint)(BAD_CODE)), // INS_knotb
        unchecked((uint)(BAD_CODE)), // INS_knotd
        unchecked((uint)(BAD_CODE)), // INS_knotq
        unchecked((uint)(BAD_CODE)), // INS_knotw
        unchecked((uint)(BAD_CODE)), // INS_korb
        unchecked((uint)(BAD_CODE)), // INS_kord
        unchecked((uint)(BAD_CODE)), // INS_korq
        unchecked((uint)(BAD_CODE)), // INS_kortestb
        unchecked((uint)(BAD_CODE)), // INS_kortestd
        unchecked((uint)(BAD_CODE)), // INS_kortestq
        unchecked((uint)(BAD_CODE)), // INS_kortestw
        unchecked((uint)(BAD_CODE)), // INS_korw
        unchecked((uint)(BAD_CODE)), // INS_kshiftlb
        unchecked((uint)(BAD_CODE)), // INS_kshiftld
        unchecked((uint)(BAD_CODE)), // INS_kshiftlq
        unchecked((uint)(BAD_CODE)), // INS_kshiftlw
        unchecked((uint)(BAD_CODE)), // INS_kshiftrb
        unchecked((uint)(BAD_CODE)), // INS_kshiftrd
        unchecked((uint)(BAD_CODE)), // INS_kshiftrq
        unchecked((uint)(BAD_CODE)), // INS_kshiftrw
        unchecked((uint)(BAD_CODE)), // INS_ktestb
        unchecked((uint)(BAD_CODE)), // INS_ktestd
        unchecked((uint)(BAD_CODE)), // INS_ktestq
        unchecked((uint)(BAD_CODE)), // INS_ktestw
        unchecked((uint)(BAD_CODE)), // INS_kunpckbw
        unchecked((uint)(BAD_CODE)), // INS_kunpckdq
        unchecked((uint)(BAD_CODE)), // INS_kunpckwd
        unchecked((uint)(BAD_CODE)), // INS_kxnorb
        unchecked((uint)(BAD_CODE)), // INS_kxnord
        unchecked((uint)(BAD_CODE)), // INS_kxnorq
        unchecked((uint)(BAD_CODE)), // INS_kxnorw
        unchecked((uint)(BAD_CODE)), // INS_kxorb
        unchecked((uint)(BAD_CODE)), // INS_kxord
        unchecked((uint)(BAD_CODE)), // INS_kxorq
        unchecked((uint)(BAD_CODE)), // INS_kxorw
        unchecked((uint)(BAD_CODE)), // INS_valignd
        unchecked((uint)(BAD_CODE)), // INS_valignq
        unchecked((uint)(BAD_CODE)), // INS_vblendmpd
        unchecked((uint)(BAD_CODE)), // INS_vblendmps
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastf32x2
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastf32x8
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastf64x2
        unchecked((uint)(BAD_CODE)), // INS_vbroadcastf64x4
        unchecked((uint)(BAD_CODE)), // INS_vbroadcasti32x2
        unchecked((uint)(BAD_CODE)), // INS_vbroadcasti32x8
        unchecked((uint)(BAD_CODE)), // INS_vbroadcasti64x2
        unchecked((uint)(BAD_CODE)), // INS_vbroadcasti64x4
        unchecked((uint)(BAD_CODE)), // INS_vcmppd
        unchecked((uint)(BAD_CODE)), // INS_vcmpps
        unchecked((uint)(BAD_CODE)), // INS_vcmpsd
        unchecked((uint)(BAD_CODE)), // INS_vcmpss
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompresspd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompressps
        unchecked((uint)(BAD_CODE)), // INS_vcvtpd2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvtpd2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvtpd2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvtqq2pd
        unchecked((uint)(BAD_CODE)), // INS_vcvtqq2ps
        unchecked((uint)(BAD_CODE)), // INS_vcvtsd2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvtsd2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvtss2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvtss2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvtudq2pd
        unchecked((uint)(BAD_CODE)), // INS_vcvtudq2ps
        unchecked((uint)(BAD_CODE)), // INS_vcvtuqq2pd
        unchecked((uint)(BAD_CODE)), // INS_vcvtuqq2ps
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2sd32
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2sd64
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2ss32
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2ss64
        unchecked((uint)(BAD_CODE)), // INS_vdbpsadbw
        unchecked((uint)(BAD_CODE)), // INS_vexpandpd
        unchecked((uint)(BAD_CODE)), // INS_vexpandps
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf32x8
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf64x2
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf64x4
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti32x8
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti64x2
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti64x4
        unchecked((uint)(BAD_CODE)), // INS_vfixupimmpd
        unchecked((uint)(BAD_CODE)), // INS_vfixupimmps
        unchecked((uint)(BAD_CODE)), // INS_vfixupimmsd
        unchecked((uint)(BAD_CODE)), // INS_vfixupimmss
        unchecked((uint)(BAD_CODE)), // INS_vfpclasspd
        unchecked((uint)(BAD_CODE)), // INS_vfpclassps
        unchecked((uint)(BAD_CODE)), // INS_vfpclasssd
        unchecked((uint)(BAD_CODE)), // INS_vfpclassss
        unchecked((uint)(BAD_CODE)), // INS_vgatherdpd_msk
        unchecked((uint)(BAD_CODE)), // INS_vgatherdps_msk
        unchecked((uint)(BAD_CODE)), // INS_vgatherqpd_msk
        unchecked((uint)(BAD_CODE)), // INS_vgatherqps_msk
        unchecked((uint)(BAD_CODE)), // INS_vgetexppd
        unchecked((uint)(BAD_CODE)), // INS_vgetexpps
        unchecked((uint)(BAD_CODE)), // INS_vgetexpsd
        unchecked((uint)(BAD_CODE)), // INS_vgetexpss
        unchecked((uint)(BAD_CODE)), // INS_vgetmantpd
        unchecked((uint)(BAD_CODE)), // INS_vgetmantps
        unchecked((uint)(BAD_CODE)), // INS_vgetmantsd
        unchecked((uint)(BAD_CODE)), // INS_vgetmantss
        unchecked((uint)(BAD_CODE)), // INS_vinsertf32x8
        unchecked((uint)(BAD_CODE)), // INS_vinsertf64x2
        unchecked((uint)(BAD_CODE)), // INS_vinsertf64x4
        unchecked((uint)(BAD_CODE)), // INS_vinserti32x8
        unchecked((uint)(BAD_CODE)), // INS_vinserti64x2
        unchecked((uint)(BAD_CODE)), // INS_vinserti64x4
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqa64
        unchecked((uint)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu16
        unchecked((uint)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu64
        unchecked((uint)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu8
        unchecked((uint)(BAD_CODE)), // INS_vpabsq
        unchecked((uint)(BAD_CODE)), // INS_vpandnq
        unchecked((uint)(BAD_CODE)), // INS_vpandq
        unchecked((uint)(BAD_CODE)), // INS_vpblendmb
        unchecked((uint)(BAD_CODE)), // INS_vpblendmd
        unchecked((uint)(BAD_CODE)), // INS_vpblendmq
        unchecked((uint)(BAD_CODE)), // INS_vpblendmw
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastb_gpr
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastd_gpr
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastq_gpr
        unchecked((uint)(BAD_CODE)), // INS_vpbroadcastw_gpr
        unchecked((uint)(BAD_CODE)), // INS_vpcmpb
        unchecked((uint)(BAD_CODE)), // INS_vpcmpd
        unchecked((uint)(BAD_CODE)), // INS_vpcmpeqb
        unchecked((uint)(BAD_CODE)), // INS_vpcmpeqd
        unchecked((uint)(BAD_CODE)), // INS_vpcmpeqq
        unchecked((uint)(BAD_CODE)), // INS_vpcmpeqw
        unchecked((uint)(BAD_CODE)), // INS_vpcmpgtb
        unchecked((uint)(BAD_CODE)), // INS_vpcmpgtd
        unchecked((uint)(BAD_CODE)), // INS_vpcmpgtq
        unchecked((uint)(BAD_CODE)), // INS_vpcmpgtw
        unchecked((uint)(BAD_CODE)), // INS_vpcmpq
        unchecked((uint)(BAD_CODE)), // INS_vpcmpub
        unchecked((uint)(BAD_CODE)), // INS_vpcmpud
        unchecked((uint)(BAD_CODE)), // INS_vpcmpuq
        unchecked((uint)(BAD_CODE)), // INS_vpcmpuw
        unchecked((uint)(BAD_CODE)), // INS_vpcmpw
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressd
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressq
        unchecked((uint)(BAD_CODE)), // INS_vpconflictd
        unchecked((uint)(BAD_CODE)), // INS_vpconflictq
        unchecked((uint)(BAD_CODE)), // INS_vpermi2d
        unchecked((uint)(BAD_CODE)), // INS_vpermi2pd
        unchecked((uint)(BAD_CODE)), // INS_vpermi2ps
        unchecked((uint)(BAD_CODE)), // INS_vpermi2q
        unchecked((uint)(BAD_CODE)), // INS_vpermi2w
        unchecked((uint)(BAD_CODE)), // INS_vpermpd_reg
        unchecked((uint)(BAD_CODE)), // INS_vpermq_reg
        unchecked((uint)(BAD_CODE)), // INS_vpermt2d
        unchecked((uint)(BAD_CODE)), // INS_vpermt2pd
        unchecked((uint)(BAD_CODE)), // INS_vpermt2ps
        unchecked((uint)(BAD_CODE)), // INS_vpermt2q
        unchecked((uint)(BAD_CODE)), // INS_vpermt2w
        unchecked((uint)(BAD_CODE)), // INS_vpermw
        unchecked((uint)(BAD_CODE)), // INS_vpexpandd
        unchecked((uint)(BAD_CODE)), // INS_vpexpandq
        unchecked((uint)(BAD_CODE)), // INS_vpgatherdd_msk
        unchecked((uint)(BAD_CODE)), // INS_vpgatherdq_msk
        unchecked((uint)(BAD_CODE)), // INS_vpgatherqd_msk
        unchecked((uint)(BAD_CODE)), // INS_vpgatherqq_msk
        unchecked((uint)(BAD_CODE)), // INS_vplzcntd
        unchecked((uint)(BAD_CODE)), // INS_vplzcntq
        unchecked((uint)(BAD_CODE)), // INS_vpmaxsq
        unchecked((uint)(BAD_CODE)), // INS_vpmaxuq
        unchecked((uint)(BAD_CODE)), // INS_vpminsq
        unchecked((uint)(BAD_CODE)), // INS_vpminuq
        unchecked((uint)(BAD_CODE)), // INS_vpmovb2m
        unchecked((uint)(BAD_CODE)), // INS_vpmovd2m
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x31))) << 8)))), // INS_vpmovdb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x33))) << 8)))), // INS_vpmovdw
        unchecked((uint)(BAD_CODE)), // INS_vpmovm2b
        unchecked((uint)(BAD_CODE)), // INS_vpmovm2d
        unchecked((uint)(BAD_CODE)), // INS_vpmovm2q
        unchecked((uint)(BAD_CODE)), // INS_vpmovm2w
        unchecked((uint)(BAD_CODE)), // INS_vpmovq2m
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x32))) << 8)))), // INS_vpmovqb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x35))) << 8)))), // INS_vpmovqd
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x34))) << 8)))), // INS_vpmovqw
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x21))) << 8)))), // INS_vpmovsdb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x23))) << 8)))), // INS_vpmovsdw
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x22))) << 8)))), // INS_vpmovsqb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x25))) << 8)))), // INS_vpmovsqd
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x24))) << 8)))), // INS_vpmovsqw
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x20))) << 8)))), // INS_vpmovswb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x11))) << 8)))), // INS_vpmovusdb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x13))) << 8)))), // INS_vpmovusdw
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x12))) << 8)))), // INS_vpmovusqb
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x15))) << 8)))), // INS_vpmovusqd
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x14))) << 8)))), // INS_vpmovusqw
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x10))) << 8)))), // INS_vpmovuswb
        unchecked((uint)(BAD_CODE)), // INS_vpmovw2m
        unchecked((uint)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x30))) << 8)))), // INS_vpmovwb
        unchecked((uint)(BAD_CODE)), // INS_vpmullq
        unchecked((uint)(BAD_CODE)), // INS_vporq
        unchecked((uint)(BAD_CODE)), // INS_vprold
        unchecked((uint)(BAD_CODE)), // INS_vprolq
        unchecked((uint)(BAD_CODE)), // INS_vprolvd
        unchecked((uint)(BAD_CODE)), // INS_vprolvq
        unchecked((uint)(BAD_CODE)), // INS_vprord
        unchecked((uint)(BAD_CODE)), // INS_vprorq
        unchecked((uint)(BAD_CODE)), // INS_vprorvd
        unchecked((uint)(BAD_CODE)), // INS_vprorvq
        unchecked((uint)(BAD_CODE)), // INS_vpscatterdd_msk
        unchecked((uint)(BAD_CODE)), // INS_vpscatterdq_msk
        unchecked((uint)(BAD_CODE)), // INS_vpscatterqd_msk
        unchecked((uint)(BAD_CODE)), // INS_vpscatterqq_msk
        unchecked((uint)(BAD_CODE)), // INS_vpsllvw
        unchecked((uint)(BAD_CODE)), // INS_vpsraq
        unchecked((uint)(BAD_CODE)), // INS_vpsravq
        unchecked((uint)(BAD_CODE)), // INS_vpsravw
        unchecked((uint)(BAD_CODE)), // INS_vpsrlvw
        unchecked((uint)(BAD_CODE)), // INS_vpternlogd
        unchecked((uint)(BAD_CODE)), // INS_vpternlogq
        unchecked((uint)(BAD_CODE)), // INS_vptestmb
        unchecked((uint)(BAD_CODE)), // INS_vptestmd
        unchecked((uint)(BAD_CODE)), // INS_vptestmq
        unchecked((uint)(BAD_CODE)), // INS_vptestmw
        unchecked((uint)(BAD_CODE)), // INS_vptestnmb
        unchecked((uint)(BAD_CODE)), // INS_vptestnmd
        unchecked((uint)(BAD_CODE)), // INS_vptestnmq
        unchecked((uint)(BAD_CODE)), // INS_vptestnmw
        unchecked((uint)(BAD_CODE)), // INS_vpxorq
        unchecked((uint)(BAD_CODE)), // INS_vrangepd
        unchecked((uint)(BAD_CODE)), // INS_vrangeps
        unchecked((uint)(BAD_CODE)), // INS_vrangesd
        unchecked((uint)(BAD_CODE)), // INS_vrangess
        unchecked((uint)(BAD_CODE)), // INS_vrcp14pd
        unchecked((uint)(BAD_CODE)), // INS_vrcp14ps
        unchecked((uint)(BAD_CODE)), // INS_vrcp14sd
        unchecked((uint)(BAD_CODE)), // INS_vrcp14ss
        unchecked((uint)(BAD_CODE)), // INS_vreducepd
        unchecked((uint)(BAD_CODE)), // INS_vreduceps
        unchecked((uint)(BAD_CODE)), // INS_vreducesd
        unchecked((uint)(BAD_CODE)), // INS_vreducess
        unchecked((uint)(BAD_CODE)), // INS_vrndscalepd
        unchecked((uint)(BAD_CODE)), // INS_vrndscaleps
        unchecked((uint)(BAD_CODE)), // INS_vrndscalesd
        unchecked((uint)(BAD_CODE)), // INS_vrndscaless
        unchecked((uint)(BAD_CODE)), // INS_vrsqrt14pd
        unchecked((uint)(BAD_CODE)), // INS_vrsqrt14ps
        unchecked((uint)(BAD_CODE)), // INS_vrsqrt14sd
        unchecked((uint)(BAD_CODE)), // INS_vrsqrt14ss
        unchecked((uint)(BAD_CODE)), // INS_vscalefpd
        unchecked((uint)(BAD_CODE)), // INS_vscalefps
        unchecked((uint)(BAD_CODE)), // INS_vscalefsd
        unchecked((uint)(BAD_CODE)), // INS_vscalefss
        unchecked((uint)(BAD_CODE)), // INS_vscatterdpd_msk
        unchecked((uint)(BAD_CODE)), // INS_vscatterdps_msk
        unchecked((uint)(BAD_CODE)), // INS_vscatterqpd_msk
        unchecked((uint)(BAD_CODE)), // INS_vscatterqps_msk
        unchecked((uint)(BAD_CODE)), // INS_vshuff32x4
        unchecked((uint)(BAD_CODE)), // INS_vshuff64x2
        unchecked((uint)(BAD_CODE)), // INS_vshufi32x4
        unchecked((uint)(BAD_CODE)), // INS_vshufi64x2
        unchecked((uint)(BAD_CODE)), // INS_vpermb
        unchecked((uint)(BAD_CODE)), // INS_vpermi2b
        unchecked((uint)(BAD_CODE)), // INS_vpermt2b
        unchecked((uint)(BAD_CODE)), // INS_vpmultishiftqb
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressb
        unchecked((uint)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressw
        unchecked((uint)(BAD_CODE)), // INS_vpexpandb
        unchecked((uint)(BAD_CODE)), // INS_vpexpandw
        unchecked((uint)(BAD_CODE)), // INS_vpopcntb
        unchecked((uint)(BAD_CODE)), // INS_vpopcntd
        unchecked((uint)(BAD_CODE)), // INS_vpopcntq
        unchecked((uint)(BAD_CODE)), // INS_vpopcntw
        unchecked((uint)(BAD_CODE)), // INS_vpshldd
        unchecked((uint)(BAD_CODE)), // INS_vpshldq
        unchecked((uint)(BAD_CODE)), // INS_vpshldvd
        unchecked((uint)(BAD_CODE)), // INS_vpshldvq
        unchecked((uint)(BAD_CODE)), // INS_vpshldvw
        unchecked((uint)(BAD_CODE)), // INS_vpshldw
        unchecked((uint)(BAD_CODE)), // INS_vpshrdd
        unchecked((uint)(BAD_CODE)), // INS_vpshrdq
        unchecked((uint)(BAD_CODE)), // INS_vpshrdvd
        unchecked((uint)(BAD_CODE)), // INS_vpshrdvq
        unchecked((uint)(BAD_CODE)), // INS_vpshrdvw
        unchecked((uint)(BAD_CODE)), // INS_vpshrdw
        unchecked((uint)(BAD_CODE)), // INS_vpshufbitqmb
        unchecked((uint)(BAD_CODE)), // INS_vaddph
        unchecked((uint)(BAD_CODE)), // INS_vaddsh
        unchecked((uint)(BAD_CODE)), // INS_vcmpph
        unchecked((uint)(BAD_CODE)), // INS_vcmpsh
        unchecked((uint)(BAD_CODE)), // INS_vcomish
        unchecked((uint)(BAD_CODE)), // INS_vcvtdq2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtne2ps2bf16
        unchecked((uint)(BAD_CODE)), // INS_vcvtneps2bf16
        unchecked((uint)(BAD_CODE)), // INS_vcvtpd2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2dq
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2pd
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2psx
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2uw
        unchecked((uint)(BAD_CODE)), // INS_vcvtph2w
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2phx
        unchecked((uint)(BAD_CODE)), // INS_vcvtqq2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtsd2sh
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2sd
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2si32
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2si64
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2ss
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvtsh2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvtsi2sh32
        unchecked((uint)(BAD_CODE)), // INS_vcvtsi2sh64
        unchecked((uint)(BAD_CODE)), // INS_vcvtss2sh
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2dq
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2qq
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2udq
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2uqq
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2uw
        unchecked((uint)(BAD_CODE)), // INS_vcvttph2w
        unchecked((uint)(BAD_CODE)), // INS_vcvttsh2si32
        unchecked((uint)(BAD_CODE)), // INS_vcvttsh2si64
        unchecked((uint)(BAD_CODE)), // INS_vcvttsh2usi32
        unchecked((uint)(BAD_CODE)), // INS_vcvttsh2usi64
        unchecked((uint)(BAD_CODE)), // INS_vcvtudq2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtuqq2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2sh32
        unchecked((uint)(BAD_CODE)), // INS_vcvtusi2sh64
        unchecked((uint)(BAD_CODE)), // INS_vcvtuw2ph
        unchecked((uint)(BAD_CODE)), // INS_vcvtw2ph
        unchecked((uint)(BAD_CODE)), // INS_vdivph
        unchecked((uint)(BAD_CODE)), // INS_vdivsh
        unchecked((uint)(BAD_CODE)), // INS_vdpbf16ps
        unchecked((uint)(BAD_CODE)), // INS_vfcmaddcph
        unchecked((uint)(BAD_CODE)), // INS_vfcmaddcsh
        unchecked((uint)(BAD_CODE)), // INS_vfcmulcph
        unchecked((uint)(BAD_CODE)), // INS_vfcmulcsh
        unchecked((uint)(BAD_CODE)), // INS_vfmulcph
        unchecked((uint)(BAD_CODE)), // INS_vfmulcsh
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132ph
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213ph
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231ph
        unchecked((uint)(BAD_CODE)), // INS_vfmadd132sh
        unchecked((uint)(BAD_CODE)), // INS_vfmadd213sh
        unchecked((uint)(BAD_CODE)), // INS_vfmadd231sh
        unchecked((uint)(BAD_CODE)), // INS_vfmaddcph
        unchecked((uint)(BAD_CODE)), // INS_vfmaddcsh
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub132ph
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub213ph
        unchecked((uint)(BAD_CODE)), // INS_vfmaddsub231ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsub132sh
        unchecked((uint)(BAD_CODE)), // INS_vfmsub213sh
        unchecked((uint)(BAD_CODE)), // INS_vfmsub231sh
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd132ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd213ph
        unchecked((uint)(BAD_CODE)), // INS_vfmsubadd231ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd132sh
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd213sh
        unchecked((uint)(BAD_CODE)), // INS_vfnmadd231sh
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231ph
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub132sh
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub213sh
        unchecked((uint)(BAD_CODE)), // INS_vfnmsub231sh
        unchecked((uint)(BAD_CODE)), // INS_vfpclassph
        unchecked((uint)(BAD_CODE)), // INS_vfpclasssh
        unchecked((uint)(BAD_CODE)), // INS_vgetexpph
        unchecked((uint)(BAD_CODE)), // INS_vgetexpsh
        unchecked((uint)(BAD_CODE)), // INS_vgetmantph
        unchecked((uint)(BAD_CODE)), // INS_vgetmantsh
        unchecked((uint)(BAD_CODE)), // INS_vmaxph
        unchecked((uint)(BAD_CODE)), // INS_vmaxsh
        unchecked((uint)(BAD_CODE)), // INS_vminsh
        unchecked((uint)(BAD_CODE)), // INS_vminph
        unchecked((uint)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x11)))))), // INS_vmovsh
        unchecked((uint)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x7E)))))), // INS_vmovw
        unchecked((uint)(BAD_CODE)), // INS_vmulph
        unchecked((uint)(BAD_CODE)), // INS_vmulsh
        unchecked((uint)(BAD_CODE)), // INS_vrcpph
        unchecked((uint)(BAD_CODE)), // INS_vrcpsh
        unchecked((uint)(BAD_CODE)), // INS_vreduceph
        unchecked((uint)(BAD_CODE)), // INS_vreducesh
        unchecked((uint)(BAD_CODE)), // INS_vrndscaleph
        unchecked((uint)(BAD_CODE)), // INS_vrndscalesh
        unchecked((uint)(BAD_CODE)), // INS_vrsqrtph
        unchecked((uint)(BAD_CODE)), // INS_vrsqrtsh
        unchecked((uint)(BAD_CODE)), // INS_vscalefph
        unchecked((uint)(BAD_CODE)), // INS_vscalefsh
        unchecked((uint)(BAD_CODE)), // INS_vsqrtph
        unchecked((uint)(BAD_CODE)), // INS_vsqrtsh
        unchecked((uint)(BAD_CODE)), // INS_vsubph
        unchecked((uint)(BAD_CODE)), // INS_vsubsh
        unchecked((uint)(BAD_CODE)), // INS_vucomish
        unchecked((uint)(BAD_CODE)), // INS_vp2intersectd
        unchecked((uint)(BAD_CODE)), // INS_vp2intersectq
        unchecked((uint)(BAD_CODE)), // INS_vcomxsd
        unchecked((uint)(BAD_CODE)), // INS_vcomxss
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2ibs
        unchecked((uint)(BAD_CODE)), // INS_vcvtps2iubs
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2dqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2qqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2udqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttpd2uqqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2dqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2ibs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2iubs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2qqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2udqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttps2uqqs
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2sis32
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2sis64
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2usis32
        unchecked((uint)(BAD_CODE)), // INS_vcvttsd2usis64
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2sis32
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2sis64
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2usis32
        unchecked((uint)(BAD_CODE)), // INS_vcvttss2usis64
        unchecked((uint)(BAD_CODE)), // INS_vminmaxpd
        unchecked((uint)(BAD_CODE)), // INS_vminmaxps
        unchecked((uint)(BAD_CODE)), // INS_vminmaxsd
        unchecked((uint)(BAD_CODE)), // INS_vminmaxss
        unchecked((uint)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_vmovd_simd
        unchecked((uint)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7E)))))), // INS_vmovw_simd
        unchecked((uint)(BAD_CODE)), // INS_vmpsadbw
        unchecked((uint)(BAD_CODE)), // INS_vucomxsd
        unchecked((uint)(BAD_CODE)), // INS_vucomxss
#if TARGET_AMD64
        unchecked((uint)(0x000038)), // INS_ccmpo
        unchecked((uint)(0x000038)), // INS_ccmpno
        unchecked((uint)(0x000038)), // INS_ccmpb
        unchecked((uint)(0x000038)), // INS_ccmpae
        unchecked((uint)(0x000038)), // INS_ccmpe
        unchecked((uint)(0x000038)), // INS_ccmpne
        unchecked((uint)(0x000038)), // INS_ccmpbe
        unchecked((uint)(0x000038)), // INS_ccmpa
        unchecked((uint)(0x000038)), // INS_ccmps
        unchecked((uint)(0x000038)), // INS_ccmpns
        unchecked((uint)(0x000038)), // INS_ccmpt
        unchecked((uint)(0x000038)), // INS_ccmpf
        unchecked((uint)(0x000038)), // INS_ccmpl
        unchecked((uint)(0x000038)), // INS_ccmpge
        unchecked((uint)(0x000038)), // INS_ccmple
        unchecked((uint)(0x000038)), // INS_ccmpg
        unchecked((uint)(0x000040)), // INS_cfcmovo
        unchecked((uint)(0x000041)), // INS_cfcmovno
        unchecked((uint)(0x000042)), // INS_cfcmovb
        unchecked((uint)(0x000043)), // INS_cfcmovae
        unchecked((uint)(0x000044)), // INS_cfcmove
        unchecked((uint)(0x000045)), // INS_cfcmovne
        unchecked((uint)(0x000046)), // INS_cfcmovbe
        unchecked((uint)(0x000047)), // INS_cfcmova
        unchecked((uint)(0x000048)), // INS_cfcmovs
        unchecked((uint)(0x000049)), // INS_cfcmovns
        unchecked((uint)(0x00004A)), // INS_cfcmovp
        unchecked((uint)(0x00004B)), // INS_cfcmovnp
        unchecked((uint)(0x00004C)), // INS_cfcmovl
        unchecked((uint)(0x00004D)), // INS_cfcmovge
        unchecked((uint)(0x00004E)), // INS_cfcmovle
        unchecked((uint)(0x00004F)), // INS_cfcmovg
        unchecked((uint)(0x000084)), // INS_ctesto
        unchecked((uint)(0x000084)), // INS_ctestno
        unchecked((uint)(0x000084)), // INS_ctestb
        unchecked((uint)(0x000084)), // INS_ctestae
        unchecked((uint)(0x000084)), // INS_cteste
        unchecked((uint)(0x000084)), // INS_ctestne
        unchecked((uint)(0x000084)), // INS_ctestbe
        unchecked((uint)(0x000084)), // INS_ctesta
        unchecked((uint)(0x000084)), // INS_ctests
        unchecked((uint)(0x000084)), // INS_ctestns
        unchecked((uint)(0x000084)), // INS_ctestt
        unchecked((uint)(0x000084)), // INS_ctestf
        unchecked((uint)(0x000084)), // INS_ctestl
        unchecked((uint)(0x000084)), // INS_ctestge
        unchecked((uint)(0x000084)), // INS_ctestle
        unchecked((uint)(0x000084)), // INS_ctestg
        unchecked((uint)(BAD_CODE)), // INS_crc32_apx
        unchecked((uint)(0x000061)), // INS_movbe_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x40)))))), // INS_seto_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x41)))))), // INS_setno_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x42)))))), // INS_setb_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x43)))))), // INS_setae_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x44)))))), // INS_sete_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x45)))))), // INS_setne_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x46)))))), // INS_setbe_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x47)))))), // INS_seta_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x48)))))), // INS_sets_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x49)))))), // INS_setns_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4A)))))), // INS_setp_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4B)))))), // INS_setnp_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4C)))))), // INS_setl_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4D)))))), // INS_setge_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4E)))))), // INS_setle_apx
        unchecked((uint)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4F)))))), // INS_setg_apx
#endif
        unchecked((uint)(BAD_CODE)), // INS_crc32
        unchecked((uint)(BAD_CODE)), // INS_tzcnt
#if TARGET_AMD64
        unchecked((uint)(BAD_CODE)), // INS_tzcnt_apx
#endif
        unchecked((uint)(BAD_CODE)), // INS_lzcnt
#if TARGET_AMD64
        unchecked((uint)(BAD_CODE)), // INS_lzcnt_apx
#endif
        unchecked((uint)(((((0x0F)) << 16) | (((0x38)) << 24) | (((0xF1)))))), // INS_movbe
        unchecked((uint)(BAD_CODE)), // INS_popcnt
#if TARGET_AMD64
        unchecked((uint)(BAD_CODE)), // INS_popcnt_apx
#endif
        unchecked((uint)(BAD_CODE)), // INS_tpause
        unchecked((uint)(BAD_CODE)), // INS_umonitor
        unchecked((uint)(BAD_CODE)), // INS_umwait
        unchecked((uint)(0x0018F6)), // INS_neg
        unchecked((uint)(0x0010F6)), // INS_not
        unchecked((uint)(0x0000D2)), // INS_rol
        unchecked((uint)(0x0000D0)), // INS_rol_1
        unchecked((uint)(0x0000C0)), // INS_rol_N
        unchecked((uint)(0x0008D2)), // INS_ror
        unchecked((uint)(0x0008D0)), // INS_ror_1
        unchecked((uint)(0x0008C0)), // INS_ror_N
        unchecked((uint)(0x0010D2)), // INS_rcl
        unchecked((uint)(0x0010D0)), // INS_rcl_1
        unchecked((uint)(0x0010C0)), // INS_rcl_N
        unchecked((uint)(0x0018D2)), // INS_rcr
        unchecked((uint)(0x0018D0)), // INS_rcr_1
        unchecked((uint)(0x0018C0)), // INS_rcr_N
        unchecked((uint)(0x0020D2)), // INS_shl
        unchecked((uint)(0x0020D0)), // INS_shl_1
        unchecked((uint)(0x0020C0)), // INS_shl_N
        unchecked((uint)(0x0028D2)), // INS_shr
        unchecked((uint)(0x0028D0)), // INS_shr_1
        unchecked((uint)(0x0028C0)), // INS_shr_N
        unchecked((uint)(0x0038D2)), // INS_sar
        unchecked((uint)(0x0038D0)), // INS_sar_1
        unchecked((uint)(0x0038C0)), // INS_sar_N
        unchecked((uint)(0x0000C3)), // INS_ret
        unchecked((uint)(BAD_CODE)), // INS_loop
        unchecked((uint)(0x0010FF)), // INS_call
        unchecked((uint)(0x00A4F3)), // INS_r_movsb
        unchecked((uint)(0x00A5F3)), // INS_r_movsd
#if TARGET_AMD64
        unchecked((uint)(0xF3A548)), // INS_r_movsq
#endif
        unchecked((uint)(0x0000A4)), // INS_movsb
        unchecked((uint)(0x0000A5)), // INS_movsd
#if TARGET_AMD64
        unchecked((uint)(0x00A548)), // INS_movsq
#endif
        unchecked((uint)(0x00AAF3)), // INS_r_stosb
        unchecked((uint)(0x00ABF3)), // INS_r_stosd
#if TARGET_AMD64
        unchecked((uint)(0xF3AB48)), // INS_r_stosq
#endif
        unchecked((uint)(0x0000AA)), // INS_stosb
        unchecked((uint)(0x0000AB)), // INS_stosd
#if TARGET_AMD64
        unchecked((uint)(0x00AB48)), // INS_stosq
#endif
        unchecked((uint)(0x0000CC)), // INS_int3
        unchecked((uint)(0x000090)), // INS_nop
        unchecked((uint)(0x0090F3)), // INS_pause
        unchecked((uint)(0x0000F0)), // INS_lock
        unchecked((uint)(0x0000C9)), // INS_leave
        unchecked((uint)(0x0fe801)), // INS_serialize
        unchecked((uint)(0x000098)), // INS_cwde
        unchecked((uint)(0x000099)), // INS_cdq
        unchecked((uint)(0x0038F6)), // INS_idiv
        unchecked((uint)(0x0028F6)), // INS_imulEAX
        unchecked((uint)(0x0030F6)), // INS_div
        unchecked((uint)(0x0020F6)), // INS_mulEAX
        unchecked((uint)(0x00009E)), // INS_sahf
        unchecked((uint)(0x0F00C0)), // INS_xadd
        unchecked((uint)(0x0F00B0)), // INS_cmpxchg
        unchecked((uint)(0x0F00A4)), // INS_shld
        unchecked((uint)(0x0F00AC)), // INS_shrd
#if TARGET_X86
        unchecked((uint)(0x0000D9)), // INS_fld
        unchecked((uint)(0x0018D9)), // INS_fstp
#endif
        unchecked((uint)(0x0F0090)), // INS_seto
        unchecked((uint)(0x0F0091)), // INS_setno
        unchecked((uint)(0x0F0092)), // INS_setb
        unchecked((uint)(0x0F0093)), // INS_setae
        unchecked((uint)(0x0F0094)), // INS_sete
        unchecked((uint)(0x0F0095)), // INS_setne
        unchecked((uint)(0x0F0096)), // INS_setbe
        unchecked((uint)(0x0F0097)), // INS_seta
        unchecked((uint)(0x0F0098)), // INS_sets
        unchecked((uint)(0x0F0099)), // INS_setns
        unchecked((uint)(0x0F009A)), // INS_setp
        unchecked((uint)(0x0F009B)), // INS_setnp
        unchecked((uint)(0x0F009C)), // INS_setl
        unchecked((uint)(0x0F009D)), // INS_setge
        unchecked((uint)(0x0F009E)), // INS_setle
        unchecked((uint)(0x0F009F)), // INS_setg
        unchecked((uint)(0x0020FF)), // INS_tail_i_jmp
        unchecked((uint)(0x0020FF)), // INS_i_jmp
#elif TARGET_ARM
#if !TARGET_ARM
#error Unexpected target type
#endif
#if FEATURE_PLI_INSTRUCTION
#endif
#if FEATURE_ITINSTRUCTION
#endif
#elif TARGET_ARM64
#if !TARGET_ARM64
#error Unexpected target type
#endif
#if FEATURE_LOOP_ALIGN
#endif
#if !TARGET_ARM64
#error Unexpected target type
#endif
#elif TARGET_LOONGARCH64
#if !TARGET_LOONGARCH64
#error Unexpected target type
#endif
#if FEATURE_SIMD
#endif
#elif TARGET_RISCV64
#if !TARGET_RISCV64
#error Unexpected target type
#endif
#elif TARGET_WASM
#if !TARGET_WASM
#error Unexpected target type
#endif
#else
#error Unsupported or unset target architecture
#endif
    ];

#endif
}