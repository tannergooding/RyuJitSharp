// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class Globals
{
    private static ReadOnlySpan<ulong> insCodesX86 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(0x0030FE)), // INS_push
        unchecked((ulong)(0x00008E)), // INS_pop
        unchecked((ulong)(0x0030FE)), // INS_push_hide
        unchecked((ulong)(0x00008E)), // INS_pop_hide
        unchecked((ulong)(0x0030FF)), // INS_push2
        unchecked((ulong)(0x00008F)), // INS_pop2
        unchecked((ulong)(0x0000FE)), // INS_inc
        unchecked((ulong)(0x0000FE)), // INS_inc_l
        unchecked((ulong)(0x0008FE)), // INS_dec
        unchecked((ulong)(0x0008FE)), // INS_dec_l
        unchecked((ulong)(0x0F00C8)), // INS_bswap
        unchecked((ulong)(0x000000)), // INS_add
        unchecked((ulong)(0x000008)), // INS_or
        unchecked((ulong)(0x000010)), // INS_adc
        unchecked((ulong)(0x000018)), // INS_sbb
        unchecked((ulong)(0x000020)), // INS_and
        unchecked((ulong)(0x000028)), // INS_sub
        unchecked((ulong)(0x000028)), // INS_sub_hide
        unchecked((ulong)(0x000030)), // INS_xor
        unchecked((ulong)(0x000038)), // INS_cmp
        unchecked((ulong)(0x000084)), // INS_test
        unchecked((ulong)(0x000088)), // INS_mov
        unchecked((ulong)(BAD_CODE)), // INS_lea
        unchecked((ulong)(0x0F00A3)), // INS_bt
        unchecked((ulong)(0x0F00AB)), // INS_bts
        unchecked((ulong)(0x0F00B3)), // INS_btr
        unchecked((ulong)(0x0F00BB)), // INS_btc
        unchecked((ulong)(BAD_CODE)), // INS_bsr
        unchecked((ulong)(BAD_CODE)), // INS_bsf
        unchecked((ulong)(BAD_CODE)), // INS_movsx
        unchecked((ulong)(BAD_CODE)), // INS_movzx
        unchecked((ulong)(BAD_CODE)), // INS_cmovo
        unchecked((ulong)(BAD_CODE)), // INS_cmovno
        unchecked((ulong)(BAD_CODE)), // INS_cmovb
        unchecked((ulong)(BAD_CODE)), // INS_cmovae
        unchecked((ulong)(BAD_CODE)), // INS_cmove
        unchecked((ulong)(BAD_CODE)), // INS_cmovne
        unchecked((ulong)(BAD_CODE)), // INS_cmovbe
        unchecked((ulong)(BAD_CODE)), // INS_cmova
        unchecked((ulong)(BAD_CODE)), // INS_cmovs
        unchecked((ulong)(BAD_CODE)), // INS_cmovns
        unchecked((ulong)(BAD_CODE)), // INS_cmovp
        unchecked((ulong)(BAD_CODE)), // INS_cmovnp
        unchecked((ulong)(BAD_CODE)), // INS_cmovl
        unchecked((ulong)(BAD_CODE)), // INS_cmovge
        unchecked((ulong)(BAD_CODE)), // INS_cmovle
        unchecked((ulong)(BAD_CODE)), // INS_cmovg
        unchecked((ulong)(0x000086)), // INS_xchg
        unchecked((ulong)(0x0F00AC)), // INS_imul
        unchecked((ulong)(BAD_CODE)), // INS_imul_AX
        unchecked((ulong)(BAD_CODE)), // INS_imul_CX
        unchecked((ulong)(BAD_CODE)), // INS_imul_DX
        unchecked((ulong)(BAD_CODE)), // INS_imul_BX
        unchecked((ulong)(BAD_CODE)), // INS_imul_SP
        unchecked((ulong)(BAD_CODE)), // INS_imul_BP
        unchecked((ulong)(BAD_CODE)), // INS_imul_SI
        unchecked((ulong)(BAD_CODE)), // INS_imul_DI
        unchecked((ulong)(BAD_CODE)), // INS_addpd
        unchecked((ulong)(BAD_CODE)), // INS_addps
        unchecked((ulong)(BAD_CODE)), // INS_addsd
        unchecked((ulong)(BAD_CODE)), // INS_addss
        unchecked((ulong)(BAD_CODE)), // INS_addsubpd
        unchecked((ulong)(BAD_CODE)), // INS_addsubps
        unchecked((ulong)(BAD_CODE)), // INS_andnpd
        unchecked((ulong)(BAD_CODE)), // INS_andnps
        unchecked((ulong)(BAD_CODE)), // INS_andpd
        unchecked((ulong)(BAD_CODE)), // INS_andps
        unchecked((ulong)(BAD_CODE)), // INS_blendpd
        unchecked((ulong)(BAD_CODE)), // INS_blendps
        unchecked((ulong)(BAD_CODE)), // INS_blendvpd
        unchecked((ulong)(BAD_CODE)), // INS_blendvps
        unchecked((ulong)(BAD_CODE)), // INS_cmppd
        unchecked((ulong)(BAD_CODE)), // INS_cmpps
        unchecked((ulong)(BAD_CODE)), // INS_cmpsd
        unchecked((ulong)(BAD_CODE)), // INS_cmpss
        unchecked((ulong)(BAD_CODE)), // INS_comisd
        unchecked((ulong)(BAD_CODE)), // INS_comiss
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2ss
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2sd
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si64
        unchecked((ulong)(BAD_CODE)), // INS_divpd
        unchecked((ulong)(BAD_CODE)), // INS_divps
        unchecked((ulong)(BAD_CODE)), // INS_divsd
        unchecked((ulong)(BAD_CODE)), // INS_divss
        unchecked((ulong)(BAD_CODE)), // INS_dppd
        unchecked((ulong)(BAD_CODE)), // INS_dpps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x17)))) << 8)))), // INS_extractps
        unchecked((ulong)(BAD_CODE)), // INS_haddpd
        unchecked((ulong)(BAD_CODE)), // INS_haddps
        unchecked((ulong)(BAD_CODE)), // INS_hsubpd
        unchecked((ulong)(BAD_CODE)), // INS_hsubps
        unchecked((ulong)(BAD_CODE)), // INS_insertps
        unchecked((ulong)(BAD_CODE)), // INS_lddqu
        unchecked((ulong)(0x000FE8AE)), // INS_lfence
        unchecked((ulong)(BAD_CODE)), // INS_maskmovdqu
        unchecked((ulong)(BAD_CODE)), // INS_maxpd
        unchecked((ulong)(BAD_CODE)), // INS_maxps
        unchecked((ulong)(BAD_CODE)), // INS_maxsd
        unchecked((ulong)(BAD_CODE)), // INS_maxss
        unchecked((ulong)(0x000FF0AE)), // INS_mfence
        unchecked((ulong)(BAD_CODE)), // INS_minpd
        unchecked((ulong)(BAD_CODE)), // INS_minps
        unchecked((ulong)(BAD_CODE)), // INS_minsd
        unchecked((ulong)(BAD_CODE)), // INS_minss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x29)))))), // INS_movapd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x29)))))), // INS_movaps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd32
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd64
        unchecked((ulong)(BAD_CODE)), // INS_movddup
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqa32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqu32
        unchecked((ulong)(BAD_CODE)), // INS_movhlps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x17)))))), // INS_movhpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x17)))))), // INS_movhps
        unchecked((ulong)(BAD_CODE)), // INS_movlhps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x13)))))), // INS_movlpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x13)))))), // INS_movlps
        unchecked((ulong)(BAD_CODE)), // INS_movmskpd
        unchecked((ulong)(BAD_CODE)), // INS_movmskps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE7)))))), // INS_movntdq
        unchecked((ulong)(BAD_CODE)), // INS_movntdqa
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti32
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti64
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2B)))))), // INS_movntpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x2B)))))), // INS_movntps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_movq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movsd_simd
        unchecked((ulong)(BAD_CODE)), // INS_movshdup
        unchecked((ulong)(BAD_CODE)), // INS_movsldup
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movupd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x11)))))), // INS_movups
        unchecked((ulong)(BAD_CODE)), // INS_mpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_mulpd
        unchecked((ulong)(BAD_CODE)), // INS_mulps
        unchecked((ulong)(BAD_CODE)), // INS_mulsd
        unchecked((ulong)(BAD_CODE)), // INS_mulss
        unchecked((ulong)(BAD_CODE)), // INS_orpd
        unchecked((ulong)(BAD_CODE)), // INS_orps
        unchecked((ulong)(BAD_CODE)), // INS_pabsb
        unchecked((ulong)(BAD_CODE)), // INS_pabsd
        unchecked((ulong)(BAD_CODE)), // INS_pabsw
        unchecked((ulong)(BAD_CODE)), // INS_packssdw
        unchecked((ulong)(BAD_CODE)), // INS_packsswb
        unchecked((ulong)(BAD_CODE)), // INS_packusdw
        unchecked((ulong)(BAD_CODE)), // INS_packuswb
        unchecked((ulong)(BAD_CODE)), // INS_paddb
        unchecked((ulong)(BAD_CODE)), // INS_paddd
        unchecked((ulong)(BAD_CODE)), // INS_paddq
        unchecked((ulong)(BAD_CODE)), // INS_paddsb
        unchecked((ulong)(BAD_CODE)), // INS_paddsw
        unchecked((ulong)(BAD_CODE)), // INS_paddusb
        unchecked((ulong)(BAD_CODE)), // INS_paddusw
        unchecked((ulong)(BAD_CODE)), // INS_paddw
        unchecked((ulong)(BAD_CODE)), // INS_palignr
        unchecked((ulong)(BAD_CODE)), // INS_pandd
        unchecked((ulong)(BAD_CODE)), // INS_pandnd
        unchecked((ulong)(BAD_CODE)), // INS_pavgb
        unchecked((ulong)(BAD_CODE)), // INS_pavgw
        unchecked((ulong)(BAD_CODE)), // INS_pblendvb
        unchecked((ulong)(BAD_CODE)), // INS_pblendw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x14)))) << 8)))), // INS_pextrb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrq
        unchecked((ulong)(BAD_CODE)), // INS_phaddd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x15)))) << 8)))), // INS_pextrw
        unchecked((ulong)(BAD_CODE)), // INS_phaddsw
        unchecked((ulong)(BAD_CODE)), // INS_phaddw
        unchecked((ulong)(BAD_CODE)), // INS_phminposuw
        unchecked((ulong)(BAD_CODE)), // INS_phsubd
        unchecked((ulong)(BAD_CODE)), // INS_phsubsw
        unchecked((ulong)(BAD_CODE)), // INS_phsubw
        unchecked((ulong)(BAD_CODE)), // INS_pinsrb
        unchecked((ulong)(BAD_CODE)), // INS_pinsrd
        unchecked((ulong)(BAD_CODE)), // INS_pinsrq
        unchecked((ulong)(BAD_CODE)), // INS_pinsrw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddubsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddwd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsb
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaxub
        unchecked((ulong)(BAD_CODE)), // INS_pmaxud
        unchecked((ulong)(BAD_CODE)), // INS_pmaxuw
        unchecked((ulong)(BAD_CODE)), // INS_pminsb
        unchecked((ulong)(BAD_CODE)), // INS_pminsd
        unchecked((ulong)(BAD_CODE)), // INS_pminsw
        unchecked((ulong)(BAD_CODE)), // INS_pminub
        unchecked((ulong)(BAD_CODE)), // INS_pminud
        unchecked((ulong)(BAD_CODE)), // INS_pminuw
        unchecked((ulong)(BAD_CODE)), // INS_pmovmskb
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmuldq
        unchecked((ulong)(BAD_CODE)), // INS_pmulhrsw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhuw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhw
        unchecked((ulong)(BAD_CODE)), // INS_pmulld
        unchecked((ulong)(BAD_CODE)), // INS_pmuludq
        unchecked((ulong)(BAD_CODE)), // INS_pmullw
        unchecked((ulong)(BAD_CODE)), // INS_pord
        unchecked((ulong)(0x000F0018)), // INS_prefetchnta
        unchecked((ulong)(0x000F0818)), // INS_prefetcht0
        unchecked((ulong)(0x000F1018)), // INS_prefetcht1
        unchecked((ulong)(0x000F1818)), // INS_prefetcht2
        unchecked((ulong)(BAD_CODE)), // INS_psadbw
        unchecked((ulong)(BAD_CODE)), // INS_pshufb
        unchecked((ulong)(BAD_CODE)), // INS_pshufd
        unchecked((ulong)(BAD_CODE)), // INS_pshufhw
        unchecked((ulong)(BAD_CODE)), // INS_pshuflw
        unchecked((ulong)(BAD_CODE)), // INS_psignb
        unchecked((ulong)(BAD_CODE)), // INS_psignd
        unchecked((ulong)(BAD_CODE)), // INS_psignw
        unchecked((ulong)(BAD_CODE)), // INS_pslld
        unchecked((ulong)(BAD_CODE)), // INS_pslldq
        unchecked((ulong)(BAD_CODE)), // INS_psllq
        unchecked((ulong)(BAD_CODE)), // INS_psllw
        unchecked((ulong)(BAD_CODE)), // INS_psrad
        unchecked((ulong)(BAD_CODE)), // INS_psraw
        unchecked((ulong)(BAD_CODE)), // INS_psrld
        unchecked((ulong)(BAD_CODE)), // INS_psrldq
        unchecked((ulong)(BAD_CODE)), // INS_psrlq
        unchecked((ulong)(BAD_CODE)), // INS_psrlw
        unchecked((ulong)(BAD_CODE)), // INS_psubb
        unchecked((ulong)(BAD_CODE)), // INS_psubd
        unchecked((ulong)(BAD_CODE)), // INS_psubq
        unchecked((ulong)(BAD_CODE)), // INS_psubsb
        unchecked((ulong)(BAD_CODE)), // INS_psubsw
        unchecked((ulong)(BAD_CODE)), // INS_psubusb
        unchecked((ulong)(BAD_CODE)), // INS_psubusw
        unchecked((ulong)(BAD_CODE)), // INS_psubw
        unchecked((ulong)(BAD_CODE)), // INS_ptest
        unchecked((ulong)(BAD_CODE)), // INS_punpckhbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckhdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhwd
        unchecked((ulong)(BAD_CODE)), // INS_punpcklbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckldq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklwd
        unchecked((ulong)(BAD_CODE)), // INS_pxord
        unchecked((ulong)(BAD_CODE)), // INS_rcpps
        unchecked((ulong)(BAD_CODE)), // INS_rcpss
        unchecked((ulong)(BAD_CODE)), // INS_roundpd
        unchecked((ulong)(BAD_CODE)), // INS_roundps
        unchecked((ulong)(BAD_CODE)), // INS_roundsd
        unchecked((ulong)(BAD_CODE)), // INS_roundss
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtps
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtss
        unchecked((ulong)(0x000FF8AE)), // INS_sfence
        unchecked((ulong)(BAD_CODE)), // INS_shufpd
        unchecked((ulong)(BAD_CODE)), // INS_shufps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtpd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtsd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtss
        unchecked((ulong)(BAD_CODE)), // INS_subpd
        unchecked((ulong)(BAD_CODE)), // INS_subps
        unchecked((ulong)(BAD_CODE)), // INS_subsd
        unchecked((ulong)(BAD_CODE)), // INS_subss
        unchecked((ulong)(BAD_CODE)), // INS_ucomisd
        unchecked((ulong)(BAD_CODE)), // INS_ucomiss
        unchecked((ulong)(BAD_CODE)), // INS_unpckhpd
        unchecked((ulong)(BAD_CODE)), // INS_unpckhps
        unchecked((ulong)(BAD_CODE)), // INS_unpcklpd
        unchecked((ulong)(BAD_CODE)), // INS_unpcklps
        unchecked((ulong)(BAD_CODE)), // INS_xorpd
        unchecked((ulong)(BAD_CODE)), // INS_xorps
        unchecked((ulong)(BAD_CODE)), // INS_aesdec
        unchecked((ulong)(BAD_CODE)), // INS_aesdeclast
        unchecked((ulong)(BAD_CODE)), // INS_aesenc
        unchecked((ulong)(BAD_CODE)), // INS_aesenclast
        unchecked((ulong)(BAD_CODE)), // INS_aesimc
        unchecked((ulong)(BAD_CODE)), // INS_aeskeygenassist
        unchecked((ulong)(BAD_CODE)), // INS_pclmulqdq
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha1nexte
        unchecked((ulong)(BAD_CODE)), // INS_sha1rnds4
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha256rnds2
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineinvqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8mulb
        unchecked((ulong)(BAD_CODE)), // INS_vblendvpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendvps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastsd
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2F)))) << 8)))), // INS_vmaskmovpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2E)))) << 8)))), // INS_vmaskmovps
        unchecked((ulong)(BAD_CODE)), // INS_vpblendvb
        unchecked((ulong)(BAD_CODE)), // INS_vperm2f128
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpdvar
        unchecked((ulong)(BAD_CODE)), // INS_vpermilps
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpsvar
        unchecked((ulong)(BAD_CODE)), // INS_vtestpd
        unchecked((ulong)(BAD_CODE)), // INS_vtestps
        unchecked((ulong)(0xC577F8)), // INS_vzeroupper
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1D)))) << 8)))), // INS_vcvtps2ph
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vpblendd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw
        unchecked((ulong)(BAD_CODE)), // INS_vperm2i128
        unchecked((ulong)(BAD_CODE)), // INS_vpermd
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermps
        unchecked((ulong)(BAD_CODE)), // INS_vpermq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovq
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvq
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_andn
        unchecked((ulong)(BAD_CODE)), // INS_bextr
        unchecked((ulong)(BAD_CODE)), // INS_blsi
        unchecked((ulong)(BAD_CODE)), // INS_blsmsk
        unchecked((ulong)(BAD_CODE)), // INS_blsr
        unchecked((ulong)(BAD_CODE)), // INS_bzhi
        unchecked((ulong)(BAD_CODE)), // INS_mulx
        unchecked((ulong)(BAD_CODE)), // INS_pdep
        unchecked((ulong)(BAD_CODE)), // INS_pext
        unchecked((ulong)(BAD_CODE)), // INS_rorx
        unchecked((ulong)(BAD_CODE)), // INS_sarx
        unchecked((ulong)(BAD_CODE)), // INS_shlx
        unchecked((ulong)(BAD_CODE)), // INS_shrx
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuuds
        unchecked((ulong)(BAD_CODE)), // INS_vbmacor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbmacxor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbitrev
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52huq
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52luq
        unchecked((ulong)(BAD_CODE)), // INS_kaddb
        unchecked((ulong)(BAD_CODE)), // INS_kaddd
        unchecked((ulong)(BAD_CODE)), // INS_kaddq
        unchecked((ulong)(BAD_CODE)), // INS_kaddw
        unchecked((ulong)(BAD_CODE)), // INS_kandb
        unchecked((ulong)(BAD_CODE)), // INS_kandd
        unchecked((ulong)(BAD_CODE)), // INS_kandnb
        unchecked((ulong)(BAD_CODE)), // INS_kandnd
        unchecked((ulong)(BAD_CODE)), // INS_kandnq
        unchecked((ulong)(BAD_CODE)), // INS_kandnw
        unchecked((ulong)(BAD_CODE)), // INS_kandq
        unchecked((ulong)(BAD_CODE)), // INS_kandw
        unchecked((ulong)(BAD_CODE)), // INS_kmovb_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovb_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovd_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovd_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovq_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovq_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovw_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovw_msk
        unchecked((ulong)(BAD_CODE)), // INS_knotb
        unchecked((ulong)(BAD_CODE)), // INS_knotd
        unchecked((ulong)(BAD_CODE)), // INS_knotq
        unchecked((ulong)(BAD_CODE)), // INS_knotw
        unchecked((ulong)(BAD_CODE)), // INS_korb
        unchecked((ulong)(BAD_CODE)), // INS_kord
        unchecked((ulong)(BAD_CODE)), // INS_korq
        unchecked((ulong)(BAD_CODE)), // INS_kortestb
        unchecked((ulong)(BAD_CODE)), // INS_kortestd
        unchecked((ulong)(BAD_CODE)), // INS_kortestq
        unchecked((ulong)(BAD_CODE)), // INS_kortestw
        unchecked((ulong)(BAD_CODE)), // INS_korw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftld
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrd
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrw
        unchecked((ulong)(BAD_CODE)), // INS_ktestb
        unchecked((ulong)(BAD_CODE)), // INS_ktestd
        unchecked((ulong)(BAD_CODE)), // INS_ktestq
        unchecked((ulong)(BAD_CODE)), // INS_ktestw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckbw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckdq
        unchecked((ulong)(BAD_CODE)), // INS_kunpckwd
        unchecked((ulong)(BAD_CODE)), // INS_kxnorb
        unchecked((ulong)(BAD_CODE)), // INS_kxnord
        unchecked((ulong)(BAD_CODE)), // INS_kxnorq
        unchecked((ulong)(BAD_CODE)), // INS_kxnorw
        unchecked((ulong)(BAD_CODE)), // INS_kxorb
        unchecked((ulong)(BAD_CODE)), // INS_kxord
        unchecked((ulong)(BAD_CODE)), // INS_kxorq
        unchecked((ulong)(BAD_CODE)), // INS_kxorw
        unchecked((ulong)(BAD_CODE)), // INS_valignd
        unchecked((ulong)(BAD_CODE)), // INS_valignq
        unchecked((ulong)(BAD_CODE)), // INS_vblendmpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendmps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vcmppd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpps
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompresspd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompressps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_vdbpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vexpandpd
        unchecked((ulong)(BAD_CODE)), // INS_vexpandps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf64x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmpd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmps
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmsd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmss
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasspd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassps
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassss
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgetexppd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpps
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpss
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantpd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantps
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantss
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x4
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqa64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu16
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu8
        unchecked((ulong)(BAD_CODE)), // INS_vpabsq
        unchecked((ulong)(BAD_CODE)), // INS_vpandnq
        unchecked((ulong)(BAD_CODE)), // INS_vpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmb
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmd
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmw
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpub
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpud
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressq
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictd
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictq
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermq_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandd
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntd
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxsq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxuq
        unchecked((ulong)(BAD_CODE)), // INS_vpminsq
        unchecked((ulong)(BAD_CODE)), // INS_vpminuq
        unchecked((ulong)(BAD_CODE)), // INS_vpmovb2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovd2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x31))) << 8)))), // INS_vpmovdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x33))) << 8)))), // INS_vpmovdw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2d
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2q
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2w
        unchecked((ulong)(BAD_CODE)), // INS_vpmovq2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x32))) << 8)))), // INS_vpmovqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x35))) << 8)))), // INS_vpmovqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x34))) << 8)))), // INS_vpmovqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x21))) << 8)))), // INS_vpmovsdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x23))) << 8)))), // INS_vpmovsdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x22))) << 8)))), // INS_vpmovsqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x25))) << 8)))), // INS_vpmovsqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x24))) << 8)))), // INS_vpmovsqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x20))) << 8)))), // INS_vpmovswb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x11))) << 8)))), // INS_vpmovusdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x13))) << 8)))), // INS_vpmovusdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x12))) << 8)))), // INS_vpmovusqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x15))) << 8)))), // INS_vpmovusqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x14))) << 8)))), // INS_vpmovusqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x10))) << 8)))), // INS_vpmovuswb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovw2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x30))) << 8)))), // INS_vpmovwb
        unchecked((ulong)(BAD_CODE)), // INS_vpmullq
        unchecked((ulong)(BAD_CODE)), // INS_vporq
        unchecked((ulong)(BAD_CODE)), // INS_vprold
        unchecked((ulong)(BAD_CODE)), // INS_vprolq
        unchecked((ulong)(BAD_CODE)), // INS_vprolvd
        unchecked((ulong)(BAD_CODE)), // INS_vprolvq
        unchecked((ulong)(BAD_CODE)), // INS_vprord
        unchecked((ulong)(BAD_CODE)), // INS_vprorq
        unchecked((ulong)(BAD_CODE)), // INS_vprorvd
        unchecked((ulong)(BAD_CODE)), // INS_vprorvq
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvw
        unchecked((ulong)(BAD_CODE)), // INS_vpsraq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravw
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvw
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogd
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmw
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmw
        unchecked((ulong)(BAD_CODE)), // INS_vpxorq
        unchecked((ulong)(BAD_CODE)), // INS_vrangepd
        unchecked((ulong)(BAD_CODE)), // INS_vrangeps
        unchecked((ulong)(BAD_CODE)), // INS_vrangesd
        unchecked((ulong)(BAD_CODE)), // INS_vrangess
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ss
        unchecked((ulong)(BAD_CODE)), // INS_vreducepd
        unchecked((ulong)(BAD_CODE)), // INS_vreduceps
        unchecked((ulong)(BAD_CODE)), // INS_vreducesd
        unchecked((ulong)(BAD_CODE)), // INS_vreducess
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalepd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleps
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaless
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ss
        unchecked((ulong)(BAD_CODE)), // INS_vscalefpd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefps
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefss
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vshuff32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshuff64x2
        unchecked((ulong)(BAD_CODE)), // INS_vshufi32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshufi64x2
        unchecked((ulong)(BAD_CODE)), // INS_vpermb
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2b
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmultishiftqb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandb
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandw
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntb
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntd
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdw
        unchecked((ulong)(BAD_CODE)), // INS_vpshufbitqmb
        unchecked((ulong)(BAD_CODE)), // INS_vaddph
        unchecked((ulong)(BAD_CODE)), // INS_vaddsh
        unchecked((ulong)(BAD_CODE)), // INS_vcmpph
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsh
        unchecked((ulong)(BAD_CODE)), // INS_vcomish
        unchecked((ulong)(BAD_CODE)), // INS_vcvtdq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtne2ps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtneps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2psx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2phx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2sd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2ss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vdivph
        unchecked((ulong)(BAD_CODE)), // INS_vdivsh
        unchecked((ulong)(BAD_CODE)), // INS_vdpbf16ps
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassph
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssh
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpph
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsh
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantph
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsh
        unchecked((ulong)(BAD_CODE)), // INS_vmaxph
        unchecked((ulong)(BAD_CODE)), // INS_vmaxsh
        unchecked((ulong)(BAD_CODE)), // INS_vminsh
        unchecked((ulong)(BAD_CODE)), // INS_vminph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x11)))))), // INS_vmovsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x7E)))))), // INS_vmovw
        unchecked((ulong)(BAD_CODE)), // INS_vmulph
        unchecked((ulong)(BAD_CODE)), // INS_vmulsh
        unchecked((ulong)(BAD_CODE)), // INS_vrcpph
        unchecked((ulong)(BAD_CODE)), // INS_vrcpsh
        unchecked((ulong)(BAD_CODE)), // INS_vreduceph
        unchecked((ulong)(BAD_CODE)), // INS_vreducesh
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleph
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesh
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vscalefph
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsh
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vsubph
        unchecked((ulong)(BAD_CODE)), // INS_vsubsh
        unchecked((ulong)(BAD_CODE)), // INS_vucomish
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectd
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectq
        unchecked((ulong)(BAD_CODE)), // INS_vcomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vcomxss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxpd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxps
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_vmovd_simd
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7E)))))), // INS_vmovw_simd
        unchecked((ulong)(BAD_CODE)), // INS_vmpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vucomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vucomxss
        unchecked((ulong)(BAD_CODE)), // INS_crc32
        unchecked((ulong)(BAD_CODE)), // INS_tzcnt
        unchecked((ulong)(BAD_CODE)), // INS_lzcnt
        unchecked((ulong)(((((0x0F)) << 16) | (((0x38)) << 24) | (((0xF1)))))), // INS_movbe
        unchecked((ulong)(BAD_CODE)), // INS_popcnt
        unchecked((ulong)(BAD_CODE)), // INS_tpause
        unchecked((ulong)(BAD_CODE)), // INS_umonitor
        unchecked((ulong)(BAD_CODE)), // INS_umwait
        unchecked((ulong)(0x0018F6)), // INS_neg
        unchecked((ulong)(0x0010F6)), // INS_not
        unchecked((ulong)(0x0000D2)), // INS_rol
        unchecked((ulong)(0x0000D0)), // INS_rol_1
        unchecked((ulong)(0x0000C0)), // INS_rol_N
        unchecked((ulong)(0x0008D2)), // INS_ror
        unchecked((ulong)(0x0008D0)), // INS_ror_1
        unchecked((ulong)(0x0008C0)), // INS_ror_N
        unchecked((ulong)(0x0010D2)), // INS_rcl
        unchecked((ulong)(0x0010D0)), // INS_rcl_1
        unchecked((ulong)(0x0010C0)), // INS_rcl_N
        unchecked((ulong)(0x0018D2)), // INS_rcr
        unchecked((ulong)(0x0018D0)), // INS_rcr_1
        unchecked((ulong)(0x0018C0)), // INS_rcr_N
        unchecked((ulong)(0x0020D2)), // INS_shl
        unchecked((ulong)(0x0020D0)), // INS_shl_1
        unchecked((ulong)(0x0020C0)), // INS_shl_N
        unchecked((ulong)(0x0028D2)), // INS_shr
        unchecked((ulong)(0x0028D0)), // INS_shr_1
        unchecked((ulong)(0x0028C0)), // INS_shr_N
        unchecked((ulong)(0x0038D2)), // INS_sar
        unchecked((ulong)(0x0038D0)), // INS_sar_1
        unchecked((ulong)(0x0038C0)), // INS_sar_N
        unchecked((ulong)(0x0000C3)), // INS_ret
        unchecked((ulong)(BAD_CODE)), // INS_loop
        unchecked((ulong)(0x0010FF)), // INS_call
        unchecked((ulong)(0x00A4F3)), // INS_r_movsb
        unchecked((ulong)(0x00A5F3)), // INS_r_movsd
        unchecked((ulong)(0x0000A4)), // INS_movsb
        unchecked((ulong)(0x0000A5)), // INS_movsd
        unchecked((ulong)(0x00AAF3)), // INS_r_stosb
        unchecked((ulong)(0x00ABF3)), // INS_r_stosd
        unchecked((ulong)(0x0000AA)), // INS_stosb
        unchecked((ulong)(0x0000AB)), // INS_stosd
        unchecked((ulong)(0x0000CC)), // INS_int3
        unchecked((ulong)(0x000090)), // INS_nop
        unchecked((ulong)(0x0090F3)), // INS_pause
        unchecked((ulong)(0x0000F0)), // INS_lock
        unchecked((ulong)(0x0000C9)), // INS_leave
        unchecked((ulong)(0x0fe801)), // INS_serialize
        unchecked((ulong)(0x000098)), // INS_cwde
        unchecked((ulong)(0x000099)), // INS_cdq
        unchecked((ulong)(0x0038F6)), // INS_idiv
        unchecked((ulong)(0x0028F6)), // INS_imulEAX
        unchecked((ulong)(0x0030F6)), // INS_div
        unchecked((ulong)(0x0020F6)), // INS_mulEAX
        unchecked((ulong)(0x00009E)), // INS_sahf
        unchecked((ulong)(0x0F00C0)), // INS_xadd
        unchecked((ulong)(0x0F00B0)), // INS_cmpxchg
        unchecked((ulong)(0x0F00A4)), // INS_shld
        unchecked((ulong)(0x0F00AC)), // INS_shrd
        unchecked((ulong)(0x0000D9)), // INS_fld
        unchecked((ulong)(0x0018D9)), // INS_fstp
        unchecked((ulong)(0x0F0090)), // INS_seto
        unchecked((ulong)(0x0F0091)), // INS_setno
        unchecked((ulong)(0x0F0092)), // INS_setb
        unchecked((ulong)(0x0F0093)), // INS_setae
        unchecked((ulong)(0x0F0094)), // INS_sete
        unchecked((ulong)(0x0F0095)), // INS_setne
        unchecked((ulong)(0x0F0096)), // INS_setbe
        unchecked((ulong)(0x0F0097)), // INS_seta
        unchecked((ulong)(0x0F0098)), // INS_sets
        unchecked((ulong)(0x0F0099)), // INS_setns
        unchecked((ulong)(0x0F009A)), // INS_setp
        unchecked((ulong)(0x0F009B)), // INS_setnp
        unchecked((ulong)(0x0F009C)), // INS_setl
        unchecked((ulong)(0x0F009D)), // INS_setge
        unchecked((ulong)(0x0F009E)), // INS_setle
        unchecked((ulong)(0x0F009F)), // INS_setg
        unchecked((ulong)(0x0020FF)), // INS_tail_i_jmp
        unchecked((ulong)(0x0020FF)), // INS_i_jmp
        unchecked((ulong)(0x0000EB)), // INS_jmp
        unchecked((ulong)(0x000070)), // INS_jo
        unchecked((ulong)(0x000071)), // INS_jno
        unchecked((ulong)(0x000072)), // INS_jb
        unchecked((ulong)(0x000073)), // INS_jae
        unchecked((ulong)(0x000074)), // INS_je
        unchecked((ulong)(0x000075)), // INS_jne
        unchecked((ulong)(0x000076)), // INS_jbe
        unchecked((ulong)(0x000077)), // INS_ja
        unchecked((ulong)(0x000078)), // INS_js
        unchecked((ulong)(0x000079)), // INS_jns
        unchecked((ulong)(0x00007A)), // INS_jp
        unchecked((ulong)(0x00007B)), // INS_jnp
        unchecked((ulong)(0x00007C)), // INS_jl
        unchecked((ulong)(0x00007D)), // INS_jge
        unchecked((ulong)(0x00007E)), // INS_jle
        unchecked((ulong)(0x00007F)), // INS_jg
        unchecked((ulong)(0x0000E9)), // INS_l_jmp
        unchecked((ulong)(0x00800F)), // INS_l_jo
        unchecked((ulong)(0x00810F)), // INS_l_jno
        unchecked((ulong)(0x00820F)), // INS_l_jb
        unchecked((ulong)(0x00830F)), // INS_l_jae
        unchecked((ulong)(0x00840F)), // INS_l_je
        unchecked((ulong)(0x00850F)), // INS_l_jne
        unchecked((ulong)(0x00860F)), // INS_l_jbe
        unchecked((ulong)(0x00870F)), // INS_l_ja
        unchecked((ulong)(0x00880F)), // INS_l_js
        unchecked((ulong)(0x00890F)), // INS_l_jns
        unchecked((ulong)(0x008A0F)), // INS_l_jp
        unchecked((ulong)(0x008B0F)), // INS_l_jnp
        unchecked((ulong)(0x008C0F)), // INS_l_jl
        unchecked((ulong)(0x008D0F)), // INS_l_jge
        unchecked((ulong)(0x008E0F)), // INS_l_jle
        unchecked((ulong)(0x008F0F)), // INS_l_jg
        unchecked((ulong)(BAD_CODE)), // INS_align
        unchecked((ulong)(0x000066)), // INS_data16
    ];

    private static ReadOnlySpan<ulong> insCodesX64 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(0x0030FE)), // INS_push
        unchecked((ulong)(0x00008E)), // INS_pop
        unchecked((ulong)(0x0030FE)), // INS_push_hide
        unchecked((ulong)(0x00008E)), // INS_pop_hide
        unchecked((ulong)(0x0030FF)), // INS_push2
        unchecked((ulong)(0x00008F)), // INS_pop2
        unchecked((ulong)(0x0000FE)), // INS_inc
        unchecked((ulong)(0x0000FE)), // INS_inc_l
        unchecked((ulong)(0x0008FE)), // INS_dec
        unchecked((ulong)(0x0008FE)), // INS_dec_l
        unchecked((ulong)(0x0F00C8)), // INS_bswap
        unchecked((ulong)(0x000000)), // INS_add
        unchecked((ulong)(0x000008)), // INS_or
        unchecked((ulong)(0x000010)), // INS_adc
        unchecked((ulong)(0x000018)), // INS_sbb
        unchecked((ulong)(0x000020)), // INS_and
        unchecked((ulong)(0x000028)), // INS_sub
        unchecked((ulong)(0x000028)), // INS_sub_hide
        unchecked((ulong)(0x000030)), // INS_xor
        unchecked((ulong)(0x000038)), // INS_cmp
        unchecked((ulong)(0x000084)), // INS_test
        unchecked((ulong)(0x000088)), // INS_mov
        unchecked((ulong)(BAD_CODE)), // INS_lea
        unchecked((ulong)(0x0F00A3)), // INS_bt
        unchecked((ulong)(0x0F00AB)), // INS_bts
        unchecked((ulong)(0x0F00B3)), // INS_btr
        unchecked((ulong)(0x0F00BB)), // INS_btc
        unchecked((ulong)(BAD_CODE)), // INS_bsr
        unchecked((ulong)(BAD_CODE)), // INS_bsf
        unchecked((ulong)(BAD_CODE)), // INS_movsx
        unchecked((ulong)(BAD_CODE)), // INS_movsxd
        unchecked((ulong)(BAD_CODE)), // INS_movzx
        unchecked((ulong)(BAD_CODE)), // INS_cmovo
        unchecked((ulong)(BAD_CODE)), // INS_cmovno
        unchecked((ulong)(BAD_CODE)), // INS_cmovb
        unchecked((ulong)(BAD_CODE)), // INS_cmovae
        unchecked((ulong)(BAD_CODE)), // INS_cmove
        unchecked((ulong)(BAD_CODE)), // INS_cmovne
        unchecked((ulong)(BAD_CODE)), // INS_cmovbe
        unchecked((ulong)(BAD_CODE)), // INS_cmova
        unchecked((ulong)(BAD_CODE)), // INS_cmovs
        unchecked((ulong)(BAD_CODE)), // INS_cmovns
        unchecked((ulong)(BAD_CODE)), // INS_cmovp
        unchecked((ulong)(BAD_CODE)), // INS_cmovnp
        unchecked((ulong)(BAD_CODE)), // INS_cmovl
        unchecked((ulong)(BAD_CODE)), // INS_cmovge
        unchecked((ulong)(BAD_CODE)), // INS_cmovle
        unchecked((ulong)(BAD_CODE)), // INS_cmovg
        unchecked((ulong)(0x000086)), // INS_xchg
        unchecked((ulong)(0x0F00AC)), // INS_imul
        unchecked((ulong)(BAD_CODE)), // INS_imul_AX
        unchecked((ulong)(BAD_CODE)), // INS_imul_CX
        unchecked((ulong)(BAD_CODE)), // INS_imul_DX
        unchecked((ulong)(BAD_CODE)), // INS_imul_BX
        unchecked((ulong)(BAD_CODE)), // INS_imul_SP
        unchecked((ulong)(BAD_CODE)), // INS_imul_BP
        unchecked((ulong)(BAD_CODE)), // INS_imul_SI
        unchecked((ulong)(BAD_CODE)), // INS_imul_DI
        unchecked((ulong)(BAD_CODE)), // INS_imul_08
        unchecked((ulong)(BAD_CODE)), // INS_imul_09
        unchecked((ulong)(BAD_CODE)), // INS_imul_10
        unchecked((ulong)(BAD_CODE)), // INS_imul_11
        unchecked((ulong)(BAD_CODE)), // INS_imul_12
        unchecked((ulong)(BAD_CODE)), // INS_imul_13
        unchecked((ulong)(BAD_CODE)), // INS_imul_14
        unchecked((ulong)(BAD_CODE)), // INS_imul_15
        unchecked((ulong)(BAD_CODE)), // INS_imul_16
        unchecked((ulong)(BAD_CODE)), // INS_imul_17
        unchecked((ulong)(BAD_CODE)), // INS_imul_18
        unchecked((ulong)(BAD_CODE)), // INS_imul_19
        unchecked((ulong)(BAD_CODE)), // INS_imul_20
        unchecked((ulong)(BAD_CODE)), // INS_imul_21
        unchecked((ulong)(BAD_CODE)), // INS_imul_22
        unchecked((ulong)(BAD_CODE)), // INS_imul_23
        unchecked((ulong)(BAD_CODE)), // INS_imul_24
        unchecked((ulong)(BAD_CODE)), // INS_imul_25
        unchecked((ulong)(BAD_CODE)), // INS_imul_26
        unchecked((ulong)(BAD_CODE)), // INS_imul_27
        unchecked((ulong)(BAD_CODE)), // INS_imul_28
        unchecked((ulong)(BAD_CODE)), // INS_imul_29
        unchecked((ulong)(BAD_CODE)), // INS_imul_30
        unchecked((ulong)(BAD_CODE)), // INS_imul_31
        unchecked((ulong)(BAD_CODE)), // INS_addpd
        unchecked((ulong)(BAD_CODE)), // INS_addps
        unchecked((ulong)(BAD_CODE)), // INS_addsd
        unchecked((ulong)(BAD_CODE)), // INS_addss
        unchecked((ulong)(BAD_CODE)), // INS_addsubpd
        unchecked((ulong)(BAD_CODE)), // INS_addsubps
        unchecked((ulong)(BAD_CODE)), // INS_andnpd
        unchecked((ulong)(BAD_CODE)), // INS_andnps
        unchecked((ulong)(BAD_CODE)), // INS_andpd
        unchecked((ulong)(BAD_CODE)), // INS_andps
        unchecked((ulong)(BAD_CODE)), // INS_blendpd
        unchecked((ulong)(BAD_CODE)), // INS_blendps
        unchecked((ulong)(BAD_CODE)), // INS_blendvpd
        unchecked((ulong)(BAD_CODE)), // INS_blendvps
        unchecked((ulong)(BAD_CODE)), // INS_cmppd
        unchecked((ulong)(BAD_CODE)), // INS_cmpps
        unchecked((ulong)(BAD_CODE)), // INS_cmpsd
        unchecked((ulong)(BAD_CODE)), // INS_cmpss
        unchecked((ulong)(BAD_CODE)), // INS_comisd
        unchecked((ulong)(BAD_CODE)), // INS_comiss
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2ss
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2sd
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si64
        unchecked((ulong)(BAD_CODE)), // INS_divpd
        unchecked((ulong)(BAD_CODE)), // INS_divps
        unchecked((ulong)(BAD_CODE)), // INS_divsd
        unchecked((ulong)(BAD_CODE)), // INS_divss
        unchecked((ulong)(BAD_CODE)), // INS_dppd
        unchecked((ulong)(BAD_CODE)), // INS_dpps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x17)))) << 8)))), // INS_extractps
        unchecked((ulong)(BAD_CODE)), // INS_haddpd
        unchecked((ulong)(BAD_CODE)), // INS_haddps
        unchecked((ulong)(BAD_CODE)), // INS_hsubpd
        unchecked((ulong)(BAD_CODE)), // INS_hsubps
        unchecked((ulong)(BAD_CODE)), // INS_insertps
        unchecked((ulong)(BAD_CODE)), // INS_lddqu
        unchecked((ulong)(0x000FE8AE)), // INS_lfence
        unchecked((ulong)(BAD_CODE)), // INS_maskmovdqu
        unchecked((ulong)(BAD_CODE)), // INS_maxpd
        unchecked((ulong)(BAD_CODE)), // INS_maxps
        unchecked((ulong)(BAD_CODE)), // INS_maxsd
        unchecked((ulong)(BAD_CODE)), // INS_maxss
        unchecked((ulong)(0x000FF0AE)), // INS_mfence
        unchecked((ulong)(BAD_CODE)), // INS_minpd
        unchecked((ulong)(BAD_CODE)), // INS_minps
        unchecked((ulong)(BAD_CODE)), // INS_minsd
        unchecked((ulong)(BAD_CODE)), // INS_minss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x29)))))), // INS_movapd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x29)))))), // INS_movaps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd32
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd64
        unchecked((ulong)(BAD_CODE)), // INS_movddup
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqa32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqu32
        unchecked((ulong)(BAD_CODE)), // INS_movhlps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x17)))))), // INS_movhpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x17)))))), // INS_movhps
        unchecked((ulong)(BAD_CODE)), // INS_movlhps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x13)))))), // INS_movlpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x13)))))), // INS_movlps
        unchecked((ulong)(BAD_CODE)), // INS_movmskpd
        unchecked((ulong)(BAD_CODE)), // INS_movmskps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE7)))))), // INS_movntdq
        unchecked((ulong)(BAD_CODE)), // INS_movntdqa
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti32
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti64
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2B)))))), // INS_movntpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x2B)))))), // INS_movntps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_movq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movsd_simd
        unchecked((ulong)(BAD_CODE)), // INS_movshdup
        unchecked((ulong)(BAD_CODE)), // INS_movsldup
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movupd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x11)))))), // INS_movups
        unchecked((ulong)(BAD_CODE)), // INS_mpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_mulpd
        unchecked((ulong)(BAD_CODE)), // INS_mulps
        unchecked((ulong)(BAD_CODE)), // INS_mulsd
        unchecked((ulong)(BAD_CODE)), // INS_mulss
        unchecked((ulong)(BAD_CODE)), // INS_orpd
        unchecked((ulong)(BAD_CODE)), // INS_orps
        unchecked((ulong)(BAD_CODE)), // INS_pabsb
        unchecked((ulong)(BAD_CODE)), // INS_pabsd
        unchecked((ulong)(BAD_CODE)), // INS_pabsw
        unchecked((ulong)(BAD_CODE)), // INS_packssdw
        unchecked((ulong)(BAD_CODE)), // INS_packsswb
        unchecked((ulong)(BAD_CODE)), // INS_packusdw
        unchecked((ulong)(BAD_CODE)), // INS_packuswb
        unchecked((ulong)(BAD_CODE)), // INS_paddb
        unchecked((ulong)(BAD_CODE)), // INS_paddd
        unchecked((ulong)(BAD_CODE)), // INS_paddq
        unchecked((ulong)(BAD_CODE)), // INS_paddsb
        unchecked((ulong)(BAD_CODE)), // INS_paddsw
        unchecked((ulong)(BAD_CODE)), // INS_paddusb
        unchecked((ulong)(BAD_CODE)), // INS_paddusw
        unchecked((ulong)(BAD_CODE)), // INS_paddw
        unchecked((ulong)(BAD_CODE)), // INS_palignr
        unchecked((ulong)(BAD_CODE)), // INS_pandd
        unchecked((ulong)(BAD_CODE)), // INS_pandnd
        unchecked((ulong)(BAD_CODE)), // INS_pavgb
        unchecked((ulong)(BAD_CODE)), // INS_pavgw
        unchecked((ulong)(BAD_CODE)), // INS_pblendvb
        unchecked((ulong)(BAD_CODE)), // INS_pblendw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x14)))) << 8)))), // INS_pextrb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrq
        unchecked((ulong)(BAD_CODE)), // INS_phaddd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x15)))) << 8)))), // INS_pextrw
        unchecked((ulong)(BAD_CODE)), // INS_phaddsw
        unchecked((ulong)(BAD_CODE)), // INS_phaddw
        unchecked((ulong)(BAD_CODE)), // INS_phminposuw
        unchecked((ulong)(BAD_CODE)), // INS_phsubd
        unchecked((ulong)(BAD_CODE)), // INS_phsubsw
        unchecked((ulong)(BAD_CODE)), // INS_phsubw
        unchecked((ulong)(BAD_CODE)), // INS_pinsrb
        unchecked((ulong)(BAD_CODE)), // INS_pinsrd
        unchecked((ulong)(BAD_CODE)), // INS_pinsrq
        unchecked((ulong)(BAD_CODE)), // INS_pinsrw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddubsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddwd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsb
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaxub
        unchecked((ulong)(BAD_CODE)), // INS_pmaxud
        unchecked((ulong)(BAD_CODE)), // INS_pmaxuw
        unchecked((ulong)(BAD_CODE)), // INS_pminsb
        unchecked((ulong)(BAD_CODE)), // INS_pminsd
        unchecked((ulong)(BAD_CODE)), // INS_pminsw
        unchecked((ulong)(BAD_CODE)), // INS_pminub
        unchecked((ulong)(BAD_CODE)), // INS_pminud
        unchecked((ulong)(BAD_CODE)), // INS_pminuw
        unchecked((ulong)(BAD_CODE)), // INS_pmovmskb
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmuldq
        unchecked((ulong)(BAD_CODE)), // INS_pmulhrsw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhuw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhw
        unchecked((ulong)(BAD_CODE)), // INS_pmulld
        unchecked((ulong)(BAD_CODE)), // INS_pmuludq
        unchecked((ulong)(BAD_CODE)), // INS_pmullw
        unchecked((ulong)(BAD_CODE)), // INS_pord
        unchecked((ulong)(0x000F0018)), // INS_prefetchnta
        unchecked((ulong)(0x000F0818)), // INS_prefetcht0
        unchecked((ulong)(0x000F1018)), // INS_prefetcht1
        unchecked((ulong)(0x000F1818)), // INS_prefetcht2
        unchecked((ulong)(BAD_CODE)), // INS_psadbw
        unchecked((ulong)(BAD_CODE)), // INS_pshufb
        unchecked((ulong)(BAD_CODE)), // INS_pshufd
        unchecked((ulong)(BAD_CODE)), // INS_pshufhw
        unchecked((ulong)(BAD_CODE)), // INS_pshuflw
        unchecked((ulong)(BAD_CODE)), // INS_psignb
        unchecked((ulong)(BAD_CODE)), // INS_psignd
        unchecked((ulong)(BAD_CODE)), // INS_psignw
        unchecked((ulong)(BAD_CODE)), // INS_pslld
        unchecked((ulong)(BAD_CODE)), // INS_pslldq
        unchecked((ulong)(BAD_CODE)), // INS_psllq
        unchecked((ulong)(BAD_CODE)), // INS_psllw
        unchecked((ulong)(BAD_CODE)), // INS_psrad
        unchecked((ulong)(BAD_CODE)), // INS_psraw
        unchecked((ulong)(BAD_CODE)), // INS_psrld
        unchecked((ulong)(BAD_CODE)), // INS_psrldq
        unchecked((ulong)(BAD_CODE)), // INS_psrlq
        unchecked((ulong)(BAD_CODE)), // INS_psrlw
        unchecked((ulong)(BAD_CODE)), // INS_psubb
        unchecked((ulong)(BAD_CODE)), // INS_psubd
        unchecked((ulong)(BAD_CODE)), // INS_psubq
        unchecked((ulong)(BAD_CODE)), // INS_psubsb
        unchecked((ulong)(BAD_CODE)), // INS_psubsw
        unchecked((ulong)(BAD_CODE)), // INS_psubusb
        unchecked((ulong)(BAD_CODE)), // INS_psubusw
        unchecked((ulong)(BAD_CODE)), // INS_psubw
        unchecked((ulong)(BAD_CODE)), // INS_ptest
        unchecked((ulong)(BAD_CODE)), // INS_punpckhbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckhdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhwd
        unchecked((ulong)(BAD_CODE)), // INS_punpcklbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckldq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklwd
        unchecked((ulong)(BAD_CODE)), // INS_pxord
        unchecked((ulong)(BAD_CODE)), // INS_rcpps
        unchecked((ulong)(BAD_CODE)), // INS_rcpss
        unchecked((ulong)(BAD_CODE)), // INS_roundpd
        unchecked((ulong)(BAD_CODE)), // INS_roundps
        unchecked((ulong)(BAD_CODE)), // INS_roundsd
        unchecked((ulong)(BAD_CODE)), // INS_roundss
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtps
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtss
        unchecked((ulong)(0x000FF8AE)), // INS_sfence
        unchecked((ulong)(BAD_CODE)), // INS_shufpd
        unchecked((ulong)(BAD_CODE)), // INS_shufps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtpd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtsd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtss
        unchecked((ulong)(BAD_CODE)), // INS_subpd
        unchecked((ulong)(BAD_CODE)), // INS_subps
        unchecked((ulong)(BAD_CODE)), // INS_subsd
        unchecked((ulong)(BAD_CODE)), // INS_subss
        unchecked((ulong)(BAD_CODE)), // INS_ucomisd
        unchecked((ulong)(BAD_CODE)), // INS_ucomiss
        unchecked((ulong)(BAD_CODE)), // INS_unpckhpd
        unchecked((ulong)(BAD_CODE)), // INS_unpckhps
        unchecked((ulong)(BAD_CODE)), // INS_unpcklpd
        unchecked((ulong)(BAD_CODE)), // INS_unpcklps
        unchecked((ulong)(BAD_CODE)), // INS_xorpd
        unchecked((ulong)(BAD_CODE)), // INS_xorps
        unchecked((ulong)(BAD_CODE)), // INS_aesdec
        unchecked((ulong)(BAD_CODE)), // INS_aesdeclast
        unchecked((ulong)(BAD_CODE)), // INS_aesenc
        unchecked((ulong)(BAD_CODE)), // INS_aesenclast
        unchecked((ulong)(BAD_CODE)), // INS_aesimc
        unchecked((ulong)(BAD_CODE)), // INS_aeskeygenassist
        unchecked((ulong)(BAD_CODE)), // INS_pclmulqdq
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha1nexte
        unchecked((ulong)(BAD_CODE)), // INS_sha1rnds4
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha256rnds2
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineinvqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8mulb
        unchecked((ulong)(BAD_CODE)), // INS_vblendvpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendvps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastsd
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2F)))) << 8)))), // INS_vmaskmovpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2E)))) << 8)))), // INS_vmaskmovps
        unchecked((ulong)(BAD_CODE)), // INS_vpblendvb
        unchecked((ulong)(BAD_CODE)), // INS_vperm2f128
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpdvar
        unchecked((ulong)(BAD_CODE)), // INS_vpermilps
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpsvar
        unchecked((ulong)(BAD_CODE)), // INS_vtestpd
        unchecked((ulong)(BAD_CODE)), // INS_vtestps
        unchecked((ulong)(0xC577F8)), // INS_vzeroupper
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1D)))) << 8)))), // INS_vcvtps2ph
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vpblendd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw
        unchecked((ulong)(BAD_CODE)), // INS_vperm2i128
        unchecked((ulong)(BAD_CODE)), // INS_vpermd
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermps
        unchecked((ulong)(BAD_CODE)), // INS_vpermq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovq
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvq
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_andn
        unchecked((ulong)(BAD_CODE)), // INS_bextr
        unchecked((ulong)(BAD_CODE)), // INS_blsi
        unchecked((ulong)(BAD_CODE)), // INS_blsmsk
        unchecked((ulong)(BAD_CODE)), // INS_blsr
        unchecked((ulong)(BAD_CODE)), // INS_bzhi
        unchecked((ulong)(BAD_CODE)), // INS_mulx
        unchecked((ulong)(BAD_CODE)), // INS_pdep
        unchecked((ulong)(BAD_CODE)), // INS_pext
        unchecked((ulong)(BAD_CODE)), // INS_rorx
        unchecked((ulong)(BAD_CODE)), // INS_sarx
        unchecked((ulong)(BAD_CODE)), // INS_shlx
        unchecked((ulong)(BAD_CODE)), // INS_shrx
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuuds
        unchecked((ulong)(BAD_CODE)), // INS_vbmacor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbmacxor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbitrev
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52huq
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52luq
        unchecked((ulong)(BAD_CODE)), // INS_kaddb
        unchecked((ulong)(BAD_CODE)), // INS_kaddd
        unchecked((ulong)(BAD_CODE)), // INS_kaddq
        unchecked((ulong)(BAD_CODE)), // INS_kaddw
        unchecked((ulong)(BAD_CODE)), // INS_kandb
        unchecked((ulong)(BAD_CODE)), // INS_kandd
        unchecked((ulong)(BAD_CODE)), // INS_kandnb
        unchecked((ulong)(BAD_CODE)), // INS_kandnd
        unchecked((ulong)(BAD_CODE)), // INS_kandnq
        unchecked((ulong)(BAD_CODE)), // INS_kandnw
        unchecked((ulong)(BAD_CODE)), // INS_kandq
        unchecked((ulong)(BAD_CODE)), // INS_kandw
        unchecked((ulong)(BAD_CODE)), // INS_kmovb_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovb_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovd_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovd_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovq_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovq_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovw_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovw_msk
        unchecked((ulong)(BAD_CODE)), // INS_knotb
        unchecked((ulong)(BAD_CODE)), // INS_knotd
        unchecked((ulong)(BAD_CODE)), // INS_knotq
        unchecked((ulong)(BAD_CODE)), // INS_knotw
        unchecked((ulong)(BAD_CODE)), // INS_korb
        unchecked((ulong)(BAD_CODE)), // INS_kord
        unchecked((ulong)(BAD_CODE)), // INS_korq
        unchecked((ulong)(BAD_CODE)), // INS_kortestb
        unchecked((ulong)(BAD_CODE)), // INS_kortestd
        unchecked((ulong)(BAD_CODE)), // INS_kortestq
        unchecked((ulong)(BAD_CODE)), // INS_kortestw
        unchecked((ulong)(BAD_CODE)), // INS_korw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftld
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrd
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrw
        unchecked((ulong)(BAD_CODE)), // INS_ktestb
        unchecked((ulong)(BAD_CODE)), // INS_ktestd
        unchecked((ulong)(BAD_CODE)), // INS_ktestq
        unchecked((ulong)(BAD_CODE)), // INS_ktestw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckbw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckdq
        unchecked((ulong)(BAD_CODE)), // INS_kunpckwd
        unchecked((ulong)(BAD_CODE)), // INS_kxnorb
        unchecked((ulong)(BAD_CODE)), // INS_kxnord
        unchecked((ulong)(BAD_CODE)), // INS_kxnorq
        unchecked((ulong)(BAD_CODE)), // INS_kxnorw
        unchecked((ulong)(BAD_CODE)), // INS_kxorb
        unchecked((ulong)(BAD_CODE)), // INS_kxord
        unchecked((ulong)(BAD_CODE)), // INS_kxorq
        unchecked((ulong)(BAD_CODE)), // INS_kxorw
        unchecked((ulong)(BAD_CODE)), // INS_valignd
        unchecked((ulong)(BAD_CODE)), // INS_valignq
        unchecked((ulong)(BAD_CODE)), // INS_vblendmpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendmps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vcmppd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpps
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompresspd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompressps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_vdbpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vexpandpd
        unchecked((ulong)(BAD_CODE)), // INS_vexpandps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf64x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmpd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmps
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmsd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmss
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasspd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassps
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassss
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgetexppd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpps
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpss
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantpd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantps
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantss
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x4
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqa64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu16
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu8
        unchecked((ulong)(BAD_CODE)), // INS_vpabsq
        unchecked((ulong)(BAD_CODE)), // INS_vpandnq
        unchecked((ulong)(BAD_CODE)), // INS_vpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmb
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmd
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmw
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpub
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpud
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressq
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictd
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictq
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermq_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandd
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntd
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxsq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxuq
        unchecked((ulong)(BAD_CODE)), // INS_vpminsq
        unchecked((ulong)(BAD_CODE)), // INS_vpminuq
        unchecked((ulong)(BAD_CODE)), // INS_vpmovb2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovd2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x31))) << 8)))), // INS_vpmovdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x33))) << 8)))), // INS_vpmovdw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2d
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2q
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2w
        unchecked((ulong)(BAD_CODE)), // INS_vpmovq2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x32))) << 8)))), // INS_vpmovqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x35))) << 8)))), // INS_vpmovqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x34))) << 8)))), // INS_vpmovqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x21))) << 8)))), // INS_vpmovsdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x23))) << 8)))), // INS_vpmovsdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x22))) << 8)))), // INS_vpmovsqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x25))) << 8)))), // INS_vpmovsqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x24))) << 8)))), // INS_vpmovsqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x20))) << 8)))), // INS_vpmovswb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x11))) << 8)))), // INS_vpmovusdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x13))) << 8)))), // INS_vpmovusdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x12))) << 8)))), // INS_vpmovusqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x15))) << 8)))), // INS_vpmovusqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x14))) << 8)))), // INS_vpmovusqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x10))) << 8)))), // INS_vpmovuswb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovw2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x30))) << 8)))), // INS_vpmovwb
        unchecked((ulong)(BAD_CODE)), // INS_vpmullq
        unchecked((ulong)(BAD_CODE)), // INS_vporq
        unchecked((ulong)(BAD_CODE)), // INS_vprold
        unchecked((ulong)(BAD_CODE)), // INS_vprolq
        unchecked((ulong)(BAD_CODE)), // INS_vprolvd
        unchecked((ulong)(BAD_CODE)), // INS_vprolvq
        unchecked((ulong)(BAD_CODE)), // INS_vprord
        unchecked((ulong)(BAD_CODE)), // INS_vprorq
        unchecked((ulong)(BAD_CODE)), // INS_vprorvd
        unchecked((ulong)(BAD_CODE)), // INS_vprorvq
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvw
        unchecked((ulong)(BAD_CODE)), // INS_vpsraq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravw
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvw
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogd
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmw
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmw
        unchecked((ulong)(BAD_CODE)), // INS_vpxorq
        unchecked((ulong)(BAD_CODE)), // INS_vrangepd
        unchecked((ulong)(BAD_CODE)), // INS_vrangeps
        unchecked((ulong)(BAD_CODE)), // INS_vrangesd
        unchecked((ulong)(BAD_CODE)), // INS_vrangess
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ss
        unchecked((ulong)(BAD_CODE)), // INS_vreducepd
        unchecked((ulong)(BAD_CODE)), // INS_vreduceps
        unchecked((ulong)(BAD_CODE)), // INS_vreducesd
        unchecked((ulong)(BAD_CODE)), // INS_vreducess
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalepd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleps
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaless
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ss
        unchecked((ulong)(BAD_CODE)), // INS_vscalefpd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefps
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefss
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vshuff32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshuff64x2
        unchecked((ulong)(BAD_CODE)), // INS_vshufi32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshufi64x2
        unchecked((ulong)(BAD_CODE)), // INS_vpermb
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2b
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmultishiftqb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandb
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandw
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntb
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntd
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdw
        unchecked((ulong)(BAD_CODE)), // INS_vpshufbitqmb
        unchecked((ulong)(BAD_CODE)), // INS_vaddph
        unchecked((ulong)(BAD_CODE)), // INS_vaddsh
        unchecked((ulong)(BAD_CODE)), // INS_vcmpph
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsh
        unchecked((ulong)(BAD_CODE)), // INS_vcomish
        unchecked((ulong)(BAD_CODE)), // INS_vcvtdq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtne2ps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtneps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2psx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2phx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2sd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2ss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vdivph
        unchecked((ulong)(BAD_CODE)), // INS_vdivsh
        unchecked((ulong)(BAD_CODE)), // INS_vdpbf16ps
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassph
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssh
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpph
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsh
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantph
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsh
        unchecked((ulong)(BAD_CODE)), // INS_vmaxph
        unchecked((ulong)(BAD_CODE)), // INS_vmaxsh
        unchecked((ulong)(BAD_CODE)), // INS_vminsh
        unchecked((ulong)(BAD_CODE)), // INS_vminph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x11)))))), // INS_vmovsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x7E)))))), // INS_vmovw
        unchecked((ulong)(BAD_CODE)), // INS_vmulph
        unchecked((ulong)(BAD_CODE)), // INS_vmulsh
        unchecked((ulong)(BAD_CODE)), // INS_vrcpph
        unchecked((ulong)(BAD_CODE)), // INS_vrcpsh
        unchecked((ulong)(BAD_CODE)), // INS_vreduceph
        unchecked((ulong)(BAD_CODE)), // INS_vreducesh
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleph
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesh
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vscalefph
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsh
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vsubph
        unchecked((ulong)(BAD_CODE)), // INS_vsubsh
        unchecked((ulong)(BAD_CODE)), // INS_vucomish
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectd
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectq
        unchecked((ulong)(BAD_CODE)), // INS_vcomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vcomxss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxpd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxps
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_vmovd_simd
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7E)))))), // INS_vmovw_simd
        unchecked((ulong)(BAD_CODE)), // INS_vmpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vucomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vucomxss
        unchecked((ulong)(0x000038)), // INS_ccmpo
        unchecked((ulong)(0x000038)), // INS_ccmpno
        unchecked((ulong)(0x000038)), // INS_ccmpb
        unchecked((ulong)(0x000038)), // INS_ccmpae
        unchecked((ulong)(0x000038)), // INS_ccmpe
        unchecked((ulong)(0x000038)), // INS_ccmpne
        unchecked((ulong)(0x000038)), // INS_ccmpbe
        unchecked((ulong)(0x000038)), // INS_ccmpa
        unchecked((ulong)(0x000038)), // INS_ccmps
        unchecked((ulong)(0x000038)), // INS_ccmpns
        unchecked((ulong)(0x000038)), // INS_ccmpt
        unchecked((ulong)(0x000038)), // INS_ccmpf
        unchecked((ulong)(0x000038)), // INS_ccmpl
        unchecked((ulong)(0x000038)), // INS_ccmpge
        unchecked((ulong)(0x000038)), // INS_ccmple
        unchecked((ulong)(0x000038)), // INS_ccmpg
        unchecked((ulong)(0x000040)), // INS_cfcmovo
        unchecked((ulong)(0x000041)), // INS_cfcmovno
        unchecked((ulong)(0x000042)), // INS_cfcmovb
        unchecked((ulong)(0x000043)), // INS_cfcmovae
        unchecked((ulong)(0x000044)), // INS_cfcmove
        unchecked((ulong)(0x000045)), // INS_cfcmovne
        unchecked((ulong)(0x000046)), // INS_cfcmovbe
        unchecked((ulong)(0x000047)), // INS_cfcmova
        unchecked((ulong)(0x000048)), // INS_cfcmovs
        unchecked((ulong)(0x000049)), // INS_cfcmovns
        unchecked((ulong)(0x00004A)), // INS_cfcmovp
        unchecked((ulong)(0x00004B)), // INS_cfcmovnp
        unchecked((ulong)(0x00004C)), // INS_cfcmovl
        unchecked((ulong)(0x00004D)), // INS_cfcmovge
        unchecked((ulong)(0x00004E)), // INS_cfcmovle
        unchecked((ulong)(0x00004F)), // INS_cfcmovg
        unchecked((ulong)(0x000084)), // INS_ctesto
        unchecked((ulong)(0x000084)), // INS_ctestno
        unchecked((ulong)(0x000084)), // INS_ctestb
        unchecked((ulong)(0x000084)), // INS_ctestae
        unchecked((ulong)(0x000084)), // INS_cteste
        unchecked((ulong)(0x000084)), // INS_ctestne
        unchecked((ulong)(0x000084)), // INS_ctestbe
        unchecked((ulong)(0x000084)), // INS_ctesta
        unchecked((ulong)(0x000084)), // INS_ctests
        unchecked((ulong)(0x000084)), // INS_ctestns
        unchecked((ulong)(0x000084)), // INS_ctestt
        unchecked((ulong)(0x000084)), // INS_ctestf
        unchecked((ulong)(0x000084)), // INS_ctestl
        unchecked((ulong)(0x000084)), // INS_ctestge
        unchecked((ulong)(0x000084)), // INS_ctestle
        unchecked((ulong)(0x000084)), // INS_ctestg
        unchecked((ulong)(BAD_CODE)), // INS_crc32_apx
        unchecked((ulong)(0x000061)), // INS_movbe_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x40)))))), // INS_seto_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x41)))))), // INS_setno_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x42)))))), // INS_setb_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x43)))))), // INS_setae_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x44)))))), // INS_sete_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x45)))))), // INS_setne_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x46)))))), // INS_setbe_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x47)))))), // INS_seta_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x48)))))), // INS_sets_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x49)))))), // INS_setns_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4A)))))), // INS_setp_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4B)))))), // INS_setnp_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4C)))))), // INS_setl_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4D)))))), // INS_setge_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4E)))))), // INS_setle_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4F)))))), // INS_setg_apx
        unchecked((ulong)(BAD_CODE)), // INS_crc32
        unchecked((ulong)(BAD_CODE)), // INS_tzcnt
        unchecked((ulong)(BAD_CODE)), // INS_tzcnt_apx
        unchecked((ulong)(BAD_CODE)), // INS_lzcnt
        unchecked((ulong)(BAD_CODE)), // INS_lzcnt_apx
        unchecked((ulong)(((((0x0F)) << 16) | (((0x38)) << 24) | (((0xF1)))))), // INS_movbe
        unchecked((ulong)(BAD_CODE)), // INS_popcnt
        unchecked((ulong)(BAD_CODE)), // INS_popcnt_apx
        unchecked((ulong)(BAD_CODE)), // INS_tpause
        unchecked((ulong)(BAD_CODE)), // INS_umonitor
        unchecked((ulong)(BAD_CODE)), // INS_umwait
        unchecked((ulong)(0x0018F6)), // INS_neg
        unchecked((ulong)(0x0010F6)), // INS_not
        unchecked((ulong)(0x0000D2)), // INS_rol
        unchecked((ulong)(0x0000D0)), // INS_rol_1
        unchecked((ulong)(0x0000C0)), // INS_rol_N
        unchecked((ulong)(0x0008D2)), // INS_ror
        unchecked((ulong)(0x0008D0)), // INS_ror_1
        unchecked((ulong)(0x0008C0)), // INS_ror_N
        unchecked((ulong)(0x0010D2)), // INS_rcl
        unchecked((ulong)(0x0010D0)), // INS_rcl_1
        unchecked((ulong)(0x0010C0)), // INS_rcl_N
        unchecked((ulong)(0x0018D2)), // INS_rcr
        unchecked((ulong)(0x0018D0)), // INS_rcr_1
        unchecked((ulong)(0x0018C0)), // INS_rcr_N
        unchecked((ulong)(0x0020D2)), // INS_shl
        unchecked((ulong)(0x0020D0)), // INS_shl_1
        unchecked((ulong)(0x0020C0)), // INS_shl_N
        unchecked((ulong)(0x0028D2)), // INS_shr
        unchecked((ulong)(0x0028D0)), // INS_shr_1
        unchecked((ulong)(0x0028C0)), // INS_shr_N
        unchecked((ulong)(0x0038D2)), // INS_sar
        unchecked((ulong)(0x0038D0)), // INS_sar_1
        unchecked((ulong)(0x0038C0)), // INS_sar_N
        unchecked((ulong)(0x0000C3)), // INS_ret
        unchecked((ulong)(BAD_CODE)), // INS_loop
        unchecked((ulong)(0x0010FF)), // INS_call
        unchecked((ulong)(0x00A4F3)), // INS_r_movsb
        unchecked((ulong)(0x00A5F3)), // INS_r_movsd
        unchecked((ulong)(0xF3A548)), // INS_r_movsq
        unchecked((ulong)(0x0000A4)), // INS_movsb
        unchecked((ulong)(0x0000A5)), // INS_movsd
        unchecked((ulong)(0x00A548)), // INS_movsq
        unchecked((ulong)(0x00AAF3)), // INS_r_stosb
        unchecked((ulong)(0x00ABF3)), // INS_r_stosd
        unchecked((ulong)(0xF3AB48)), // INS_r_stosq
        unchecked((ulong)(0x0000AA)), // INS_stosb
        unchecked((ulong)(0x0000AB)), // INS_stosd
        unchecked((ulong)(0x00AB48)), // INS_stosq
        unchecked((ulong)(0x0000CC)), // INS_int3
        unchecked((ulong)(0x000090)), // INS_nop
        unchecked((ulong)(0x0090F3)), // INS_pause
        unchecked((ulong)(0x0000F0)), // INS_lock
        unchecked((ulong)(0x0000C9)), // INS_leave
        unchecked((ulong)(0x0fe801)), // INS_serialize
        unchecked((ulong)(0x000098)), // INS_cwde
        unchecked((ulong)(0x000099)), // INS_cdq
        unchecked((ulong)(0x0038F6)), // INS_idiv
        unchecked((ulong)(0x0028F6)), // INS_imulEAX
        unchecked((ulong)(0x0030F6)), // INS_div
        unchecked((ulong)(0x0020F6)), // INS_mulEAX
        unchecked((ulong)(0x00009E)), // INS_sahf
        unchecked((ulong)(0x0F00C0)), // INS_xadd
        unchecked((ulong)(0x0F00B0)), // INS_cmpxchg
        unchecked((ulong)(0x0F00A4)), // INS_shld
        unchecked((ulong)(0x0F00AC)), // INS_shrd
        unchecked((ulong)(0x0F0090)), // INS_seto
        unchecked((ulong)(0x0F0091)), // INS_setno
        unchecked((ulong)(0x0F0092)), // INS_setb
        unchecked((ulong)(0x0F0093)), // INS_setae
        unchecked((ulong)(0x0F0094)), // INS_sete
        unchecked((ulong)(0x0F0095)), // INS_setne
        unchecked((ulong)(0x0F0096)), // INS_setbe
        unchecked((ulong)(0x0F0097)), // INS_seta
        unchecked((ulong)(0x0F0098)), // INS_sets
        unchecked((ulong)(0x0F0099)), // INS_setns
        unchecked((ulong)(0x0F009A)), // INS_setp
        unchecked((ulong)(0x0F009B)), // INS_setnp
        unchecked((ulong)(0x0F009C)), // INS_setl
        unchecked((ulong)(0x0F009D)), // INS_setge
        unchecked((ulong)(0x0F009E)), // INS_setle
        unchecked((ulong)(0x0F009F)), // INS_setg
        unchecked((ulong)(0x0020FF)), // INS_tail_i_jmp
        unchecked((ulong)(0x0020FF)), // INS_i_jmp
        unchecked((ulong)(0x0000EB)), // INS_jmp
        unchecked((ulong)(0x000070)), // INS_jo
        unchecked((ulong)(0x000071)), // INS_jno
        unchecked((ulong)(0x000072)), // INS_jb
        unchecked((ulong)(0x000073)), // INS_jae
        unchecked((ulong)(0x000074)), // INS_je
        unchecked((ulong)(0x000075)), // INS_jne
        unchecked((ulong)(0x000076)), // INS_jbe
        unchecked((ulong)(0x000077)), // INS_ja
        unchecked((ulong)(0x000078)), // INS_js
        unchecked((ulong)(0x000079)), // INS_jns
        unchecked((ulong)(0x00007A)), // INS_jp
        unchecked((ulong)(0x00007B)), // INS_jnp
        unchecked((ulong)(0x00007C)), // INS_jl
        unchecked((ulong)(0x00007D)), // INS_jge
        unchecked((ulong)(0x00007E)), // INS_jle
        unchecked((ulong)(0x00007F)), // INS_jg
        unchecked((ulong)(0x0000E9)), // INS_l_jmp
        unchecked((ulong)(0x00800F)), // INS_l_jo
        unchecked((ulong)(0x00810F)), // INS_l_jno
        unchecked((ulong)(0x00820F)), // INS_l_jb
        unchecked((ulong)(0x00830F)), // INS_l_jae
        unchecked((ulong)(0x00840F)), // INS_l_je
        unchecked((ulong)(0x00850F)), // INS_l_jne
        unchecked((ulong)(0x00860F)), // INS_l_jbe
        unchecked((ulong)(0x00870F)), // INS_l_ja
        unchecked((ulong)(0x00880F)), // INS_l_js
        unchecked((ulong)(0x00890F)), // INS_l_jns
        unchecked((ulong)(0x008A0F)), // INS_l_jp
        unchecked((ulong)(0x008B0F)), // INS_l_jnp
        unchecked((ulong)(0x008C0F)), // INS_l_jl
        unchecked((ulong)(0x008D0F)), // INS_l_jge
        unchecked((ulong)(0x008E0F)), // INS_l_jle
        unchecked((ulong)(0x008F0F)), // INS_l_jg
        unchecked((ulong)(BAD_CODE)), // INS_align
        unchecked((ulong)(0x000066)), // INS_data16
    ];

    private static ReadOnlySpan<ulong> insCodes =>
        Configuration.TARGET_X86 ? insCodesX86 :
        Configuration.TARGET_AMD64 ? insCodesX64 :
        throw new FatalJitException("xarch opcode table is not available for this target.");

    private static ReadOnlySpan<ulong> insCodesACCX86 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(BAD_CODE)), // INS_push
        unchecked((ulong)(BAD_CODE)), // INS_pop
        unchecked((ulong)(BAD_CODE)), // INS_push_hide
        unchecked((ulong)(BAD_CODE)), // INS_pop_hide
        unchecked((ulong)(BAD_CODE)), // INS_push2
        unchecked((ulong)(BAD_CODE)), // INS_pop2
        unchecked((ulong)(BAD_CODE)), // INS_inc
        unchecked((ulong)(BAD_CODE)), // INS_inc_l
        unchecked((ulong)(BAD_CODE)), // INS_dec
        unchecked((ulong)(BAD_CODE)), // INS_dec_l
        unchecked((ulong)(BAD_CODE)), // INS_bswap
        unchecked((ulong)(0x000004)), // INS_add
        unchecked((ulong)(0x00000C)), // INS_or
        unchecked((ulong)(0x000014)), // INS_adc
        unchecked((ulong)(0x00001C)), // INS_sbb
        unchecked((ulong)(0x000024)), // INS_and
        unchecked((ulong)(0x00002C)), // INS_sub
        unchecked((ulong)(0x00002C)), // INS_sub_hide
        unchecked((ulong)(0x000034)), // INS_xor
        unchecked((ulong)(0x00003C)), // INS_cmp
        unchecked((ulong)(0x0000A8)), // INS_test
        unchecked((ulong)(0x0000B0)), // INS_mov
        unchecked((ulong)(BAD_CODE)), // INS_lea
    ];

    private static ReadOnlySpan<ulong> insCodesACCX64 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(BAD_CODE)), // INS_push
        unchecked((ulong)(BAD_CODE)), // INS_pop
        unchecked((ulong)(BAD_CODE)), // INS_push_hide
        unchecked((ulong)(BAD_CODE)), // INS_pop_hide
        unchecked((ulong)(BAD_CODE)), // INS_push2
        unchecked((ulong)(BAD_CODE)), // INS_pop2
        unchecked((ulong)(BAD_CODE)), // INS_inc
        unchecked((ulong)(BAD_CODE)), // INS_inc_l
        unchecked((ulong)(BAD_CODE)), // INS_dec
        unchecked((ulong)(BAD_CODE)), // INS_dec_l
        unchecked((ulong)(BAD_CODE)), // INS_bswap
        unchecked((ulong)(0x000004)), // INS_add
        unchecked((ulong)(0x00000C)), // INS_or
        unchecked((ulong)(0x000014)), // INS_adc
        unchecked((ulong)(0x00001C)), // INS_sbb
        unchecked((ulong)(0x000024)), // INS_and
        unchecked((ulong)(0x00002C)), // INS_sub
        unchecked((ulong)(0x00002C)), // INS_sub_hide
        unchecked((ulong)(0x000034)), // INS_xor
        unchecked((ulong)(0x00003C)), // INS_cmp
        unchecked((ulong)(0x0000A8)), // INS_test
        unchecked((ulong)(0x0000B0)), // INS_mov
        unchecked((ulong)(BAD_CODE)), // INS_lea
    ];

    private static ReadOnlySpan<ulong> insCodesACC =>
        Configuration.TARGET_X86 ? insCodesACCX86 :
        Configuration.TARGET_AMD64 ? insCodesACCX64 :
        throw new FatalJitException("xarch opcode table is not available for this target.");

    private static ReadOnlySpan<ulong> insCodesRRX86 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(0x000050)), // INS_push
        unchecked((ulong)(0x000058)), // INS_pop
        unchecked((ulong)(0x000050)), // INS_push_hide
        unchecked((ulong)(0x000058)), // INS_pop_hide
        unchecked((ulong)(0x0030FF)), // INS_push2
        unchecked((ulong)(0x00008F)), // INS_pop2
        unchecked((ulong)(0x000040)), // INS_inc
        unchecked((ulong)(0x00C0FE)), // INS_inc_l
        unchecked((ulong)(0x000048)), // INS_dec
        unchecked((ulong)(0x00C8FE)), // INS_dec_l
        unchecked((ulong)(0x00C80F)), // INS_bswap
    ];

    private static ReadOnlySpan<ulong> insCodesRRX64 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(0x000050)), // INS_push
        unchecked((ulong)(0x000058)), // INS_pop
        unchecked((ulong)(0x000050)), // INS_push_hide
        unchecked((ulong)(0x000058)), // INS_pop_hide
        unchecked((ulong)(0x0030FF)), // INS_push2
        unchecked((ulong)(0x00008F)), // INS_pop2
        unchecked((ulong)(0x000040)), // INS_inc
        unchecked((ulong)(0x00C0FE)), // INS_inc_l
        unchecked((ulong)(0x000048)), // INS_dec
        unchecked((ulong)(0x00C8FE)), // INS_dec_l
        unchecked((ulong)(0x00C80F)), // INS_bswap
    ];

    private static ReadOnlySpan<ulong> insCodesRR =>
        Configuration.TARGET_X86 ? insCodesRRX86 :
        Configuration.TARGET_AMD64 ? insCodesRRX64 :
        throw new FatalJitException("xarch opcode table is not available for this target.");

    private static ReadOnlySpan<ulong> insCodesRMX86 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(BAD_CODE)), // INS_push
        unchecked((ulong)(BAD_CODE)), // INS_pop
        unchecked((ulong)(BAD_CODE)), // INS_push_hide
        unchecked((ulong)(BAD_CODE)), // INS_pop_hide
        unchecked((ulong)(0x0030FF)), // INS_push2
        unchecked((ulong)(0x00008F)), // INS_pop2
        unchecked((ulong)(0x0000FE)), // INS_inc
        unchecked((ulong)(BAD_CODE)), // INS_inc_l
        unchecked((ulong)(0x0008FE)), // INS_dec
        unchecked((ulong)(BAD_CODE)), // INS_dec_l
        unchecked((ulong)(BAD_CODE)), // INS_bswap
        unchecked((ulong)(0x000002)), // INS_add
        unchecked((ulong)(0x00000A)), // INS_or
        unchecked((ulong)(0x000012)), // INS_adc
        unchecked((ulong)(0x00001A)), // INS_sbb
        unchecked((ulong)(0x000022)), // INS_and
        unchecked((ulong)(0x00002A)), // INS_sub
        unchecked((ulong)(0x00002A)), // INS_sub_hide
        unchecked((ulong)(0x000032)), // INS_xor
        unchecked((ulong)(0x00003A)), // INS_cmp
        unchecked((ulong)(0x000084)), // INS_test
        unchecked((ulong)(0x00008A)), // INS_mov
        unchecked((ulong)(0x00008D)), // INS_lea
        unchecked((ulong)(0x0F00A3)), // INS_bt
        unchecked((ulong)(0x0F00AB)), // INS_bts
        unchecked((ulong)(0x0F00B3)), // INS_btr
        unchecked((ulong)(0x0F00BB)), // INS_btc
        unchecked((ulong)(0x0F00BD)), // INS_bsr
        unchecked((ulong)(0x0F00BC)), // INS_bsf
        unchecked((ulong)(0x0F00BE)), // INS_movsx
        unchecked((ulong)(0x0F00B6)), // INS_movzx
        unchecked((ulong)(0x0F0040)), // INS_cmovo
        unchecked((ulong)(0x0F0041)), // INS_cmovno
        unchecked((ulong)(0x0F0042)), // INS_cmovb
        unchecked((ulong)(0x0F0043)), // INS_cmovae
        unchecked((ulong)(0x0F0044)), // INS_cmove
        unchecked((ulong)(0x0F0045)), // INS_cmovne
        unchecked((ulong)(0x0F0046)), // INS_cmovbe
        unchecked((ulong)(0x0F0047)), // INS_cmova
        unchecked((ulong)(0x0F0048)), // INS_cmovs
        unchecked((ulong)(0x0F0049)), // INS_cmovns
        unchecked((ulong)(0x0F004A)), // INS_cmovp
        unchecked((ulong)(0x0F004B)), // INS_cmovnp
        unchecked((ulong)(0x0F004C)), // INS_cmovl
        unchecked((ulong)(0x0F004D)), // INS_cmovge
        unchecked((ulong)(0x0F004E)), // INS_cmovle
        unchecked((ulong)(0x0F004F)), // INS_cmovg
        unchecked((ulong)(0x000086)), // INS_xchg
        unchecked((ulong)(0x0F00AF)), // INS_imul
        unchecked((ulong)(BAD_CODE)), // INS_imul_AX
        unchecked((ulong)(BAD_CODE)), // INS_imul_CX
        unchecked((ulong)(BAD_CODE)), // INS_imul_DX
        unchecked((ulong)(BAD_CODE)), // INS_imul_BX
        unchecked((ulong)(BAD_CODE)), // INS_imul_SP
        unchecked((ulong)(BAD_CODE)), // INS_imul_BP
        unchecked((ulong)(BAD_CODE)), // INS_imul_SI
        unchecked((ulong)(BAD_CODE)), // INS_imul_DI
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x58)))))), // INS_addpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x58)))))), // INS_addps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x58)))))), // INS_addsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x58)))))), // INS_addss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD0)))))), // INS_addsubpd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xD0)))))), // INS_addsubps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x55)))))), // INS_andnpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x55)))))), // INS_andnps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x54)))))), // INS_andpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x54)))))), // INS_andps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0D)))) << 8)))), // INS_blendpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0C)))) << 8)))), // INS_blendps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x15)))) << 8)))), // INS_blendvpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x14)))) << 8)))), // INS_blendvps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_cmppd
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC2)))))), // INS_cmpps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_cmpsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_cmpss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2F)))))), // INS_comisd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x2F)))))), // INS_comiss
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_cvtdq2pd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5B)))))), // INS_cvtdq2ps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_cvtpd2dq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5A)))))), // INS_cvtpd2ps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5B)))))), // INS_cvtps2dq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5A)))))), // INS_cvtps2pd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtsd2si32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtsd2si64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5A)))))), // INS_cvtsd2ss
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2sd32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2sd64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2ss32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2ss64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5A)))))), // INS_cvtss2sd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtss2si32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtss2si64
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_cvttpd2dq
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5B)))))), // INS_cvttps2dq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttsd2si32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttsd2si64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttss2si32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttss2si64
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5E)))))), // INS_divpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5E)))))), // INS_divps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5E)))))), // INS_divsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5E)))))), // INS_divss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x41)))) << 8)))), // INS_dppd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x40)))) << 8)))), // INS_dpps
        unchecked((ulong)(BAD_CODE)), // INS_extractps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7C)))))), // INS_haddpd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7C)))))), // INS_haddps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7D)))))), // INS_hsubpd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7D)))))), // INS_hsubps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x21)))) << 8)))), // INS_insertps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xF0)))))), // INS_lddqu
        unchecked((ulong)(BAD_CODE)), // INS_lfence
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF7)))))), // INS_maskmovdqu
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5F)))))), // INS_maxpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5F)))))), // INS_maxps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5F)))))), // INS_maxsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5F)))))), // INS_maxss
        unchecked((ulong)(BAD_CODE)), // INS_mfence
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5D)))))), // INS_minpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5D)))))), // INS_minps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5D)))))), // INS_minsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5D)))))), // INS_minss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x28)))))), // INS_movapd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x28)))))), // INS_movaps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6E)))))), // INS_movd32
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6E)))))), // INS_movd64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x12)))))), // INS_movddup
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_movdqa32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_movdqu32
        unchecked((ulong)(((((0x0f)) << 16) | (((0x12)))))), // INS_movhlps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x16)))))), // INS_movhpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x16)))))), // INS_movhps
        unchecked((ulong)(((((0x0f)) << 16) | (((0x16)))))), // INS_movlhps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x12)))))), // INS_movlpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x12)))))), // INS_movlps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x50)))))), // INS_movmskpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x50)))))), // INS_movmskps
        unchecked((ulong)(BAD_CODE)), // INS_movntdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2A)))) << 8)))), // INS_movntdqa
        unchecked((ulong)(BAD_CODE)), // INS_movnti32
        unchecked((ulong)(BAD_CODE)), // INS_movnti64
        unchecked((ulong)(BAD_CODE)), // INS_movntpd
        unchecked((ulong)(BAD_CODE)), // INS_movntps
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x10)))))), // INS_movsd_simd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x16)))))), // INS_movshdup
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x12)))))), // INS_movsldup
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x10)))))), // INS_movss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x10)))))), // INS_movupd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x10)))))), // INS_movups
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x42)))) << 8)))), // INS_mpsadbw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x59)))))), // INS_mulpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x59)))))), // INS_mulps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x59)))))), // INS_mulsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x59)))))), // INS_mulss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x56)))))), // INS_orpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x56)))))), // INS_orps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1C)))) << 8)))), // INS_pabsb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1E)))) << 8)))), // INS_pabsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1D)))) << 8)))), // INS_pabsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6B)))))), // INS_packssdw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x63)))))), // INS_packsswb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2B)))) << 8)))), // INS_packusdw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x67)))))), // INS_packuswb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFC)))))), // INS_paddb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFE)))))), // INS_paddd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD4)))))), // INS_paddq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEC)))))), // INS_paddsb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xED)))))), // INS_paddsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDC)))))), // INS_paddusb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDD)))))), // INS_paddusw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFD)))))), // INS_paddw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0F)))) << 8)))), // INS_palignr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDB)))))), // INS_pandd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDF)))))), // INS_pandnd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE0)))))), // INS_pavgb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE3)))))), // INS_pavgw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x10)))) << 8)))), // INS_pblendvb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0E)))) << 8)))), // INS_pblendw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x74)))))), // INS_pcmpeqb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x76)))))), // INS_pcmpeqd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x29)))) << 8)))), // INS_pcmpeqq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x75)))))), // INS_pcmpeqw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x64)))))), // INS_pcmpgtb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x66)))))), // INS_pcmpgtd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x37)))) << 8)))), // INS_pcmpgtq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x65)))))), // INS_pcmpgtw
        unchecked((ulong)(BAD_CODE)), // INS_pextrb
        unchecked((ulong)(BAD_CODE)), // INS_pextrd
        unchecked((ulong)(BAD_CODE)), // INS_pextrq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x02)))) << 8)))), // INS_phaddd
        unchecked((ulong)(BAD_CODE)), // INS_pextrw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x03)))) << 8)))), // INS_phaddsw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x01)))) << 8)))), // INS_phaddw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x41)))) << 8)))), // INS_phminposuw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x06)))) << 8)))), // INS_phsubd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x07)))) << 8)))), // INS_phsubsw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x05)))) << 8)))), // INS_phsubw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x20)))) << 8)))), // INS_pinsrb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x22)))) << 8)))), // INS_pinsrd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x22)))) << 8)))), // INS_pinsrq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC4)))))), // INS_pinsrw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x04)))) << 8)))), // INS_pmaddubsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF5)))))), // INS_pmaddwd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3C)))) << 8)))), // INS_pmaxsb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3D)))) << 8)))), // INS_pmaxsd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEE)))))), // INS_pmaxsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDE)))))), // INS_pmaxub
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3F)))) << 8)))), // INS_pmaxud
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3E)))) << 8)))), // INS_pmaxuw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x38)))) << 8)))), // INS_pminsb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x39)))) << 8)))), // INS_pminsd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEA)))))), // INS_pminsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDA)))))), // INS_pminub
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3B)))) << 8)))), // INS_pminud
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3A)))) << 8)))), // INS_pminuw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD7)))))), // INS_pmovmskb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x21)))) << 8)))), // INS_pmovsxbd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x22)))) << 8)))), // INS_pmovsxbq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x20)))) << 8)))), // INS_pmovsxbw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x25)))) << 8)))), // INS_pmovsxdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x23)))) << 8)))), // INS_pmovsxwd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x24)))) << 8)))), // INS_pmovsxwq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x31)))) << 8)))), // INS_pmovzxbd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x32)))) << 8)))), // INS_pmovzxbq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x30)))) << 8)))), // INS_pmovzxbw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x35)))) << 8)))), // INS_pmovzxdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x33)))) << 8)))), // INS_pmovzxwd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x34)))) << 8)))), // INS_pmovzxwq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x28)))) << 8)))), // INS_pmuldq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0B)))) << 8)))), // INS_pmulhrsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE4)))))), // INS_pmulhuw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE5)))))), // INS_pmulhw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x40)))) << 8)))), // INS_pmulld
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF4)))))), // INS_pmuludq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD5)))))), // INS_pmullw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEB)))))), // INS_pord
        unchecked((ulong)(BAD_CODE)), // INS_prefetchnta
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht0
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht1
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht2
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF6)))))), // INS_psadbw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x00)))) << 8)))), // INS_pshufb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x70)))))), // INS_pshufd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x70)))))), // INS_pshufhw
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x70)))))), // INS_pshuflw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x08)))) << 8)))), // INS_psignb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0A)))) << 8)))), // INS_psignd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x09)))) << 8)))), // INS_psignw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF2)))))), // INS_pslld
        unchecked((ulong)(BAD_CODE)), // INS_pslldq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF3)))))), // INS_psllq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF1)))))), // INS_psllw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE2)))))), // INS_psrad
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE1)))))), // INS_psraw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD2)))))), // INS_psrld
        unchecked((ulong)(BAD_CODE)), // INS_psrldq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD3)))))), // INS_psrlq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD1)))))), // INS_psrlw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF8)))))), // INS_psubb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFA)))))), // INS_psubd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFB)))))), // INS_psubq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE8)))))), // INS_psubsb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE9)))))), // INS_psubsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD8)))))), // INS_psubusb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD9)))))), // INS_psubusw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF9)))))), // INS_psubw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x17)))) << 8)))), // INS_ptest
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x68)))))), // INS_punpckhbw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6A)))))), // INS_punpckhdq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6D)))))), // INS_punpckhqdq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x69)))))), // INS_punpckhwd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x60)))))), // INS_punpcklbw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x62)))))), // INS_punpckldq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6C)))))), // INS_punpcklqdq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x61)))))), // INS_punpcklwd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEF)))))), // INS_pxord
        unchecked((ulong)(((((0x0f)) << 16) | (((0x53)))))), // INS_rcpps
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x53)))))), // INS_rcpss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x09)))) << 8)))), // INS_roundpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x08)))) << 8)))), // INS_roundps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0B)))) << 8)))), // INS_roundsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0A)))) << 8)))), // INS_roundss
        unchecked((ulong)(((((0x0f)) << 16) | (((0x52)))))), // INS_rsqrtps
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x52)))))), // INS_rsqrtss
        unchecked((ulong)(BAD_CODE)), // INS_sfence
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC6)))))), // INS_shufpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC6)))))), // INS_shufps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x51)))))), // INS_sqrtpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x51)))))), // INS_sqrtps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x51)))))), // INS_sqrtsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x51)))))), // INS_sqrtss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5C)))))), // INS_subpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5C)))))), // INS_subps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5C)))))), // INS_subsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5C)))))), // INS_subss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2E)))))), // INS_ucomisd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x2E)))))), // INS_ucomiss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x15)))))), // INS_unpckhpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x15)))))), // INS_unpckhps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x14)))))), // INS_unpcklpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x14)))))), // INS_unpcklps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x57)))))), // INS_xorpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x57)))))), // INS_xorps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDE)))) << 8)))), // INS_aesdec
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDF)))) << 8)))), // INS_aesdeclast
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDC)))) << 8)))), // INS_aesenc
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDD)))) << 8)))), // INS_aesenclast
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDB)))) << 8)))), // INS_aesimc
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xDF)))) << 8)))), // INS_aeskeygenassist
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x44)))) << 8)))), // INS_pclmulqdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC9)))) << 8)))), // INS_sha1msg1
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCA)))) << 8)))), // INS_sha1msg2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC8)))) << 8)))), // INS_sha1nexte
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xCC)))) << 8)))), // INS_sha1rnds4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCC)))) << 8)))), // INS_sha256msg1
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCD)))) << 8)))), // INS_sha256msg2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCB)))) << 8)))), // INS_sha256rnds2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xCF)))) << 8)))), // INS_gf2p8affineinvqb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xCE)))) << 8)))), // INS_gf2p8affineqb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCF)))) << 8)))), // INS_gf2p8mulb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x4B)))) << 8)))), // INS_vblendvpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x4A)))) << 8)))), // INS_vblendvps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1A)))) << 8)))), // INS_vbroadcastf32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x19)))) << 8)))), // INS_vbroadcastsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x18)))) << 8)))), // INS_vbroadcastss
        unchecked((ulong)(BAD_CODE)), // INS_vextractf32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x18)))) << 8)))), // INS_vinsertf32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2D)))) << 8)))), // INS_vmaskmovpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2C)))) << 8)))), // INS_vmaskmovps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x4C)))) << 8)))), // INS_vpblendvb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x06)))) << 8)))), // INS_vperm2f128
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x05)))) << 8)))), // INS_vpermilpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0D)))) << 8)))), // INS_vpermilpdvar
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x04)))) << 8)))), // INS_vpermilps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0C)))) << 8)))), // INS_vpermilpsvar
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0F)))) << 8)))), // INS_vtestpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0E)))) << 8)))), // INS_vtestps
        unchecked((ulong)(BAD_CODE)), // INS_vzeroupper
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5A)))) << 8)))), // INS_vbroadcasti32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x13)))) << 8)))), // INS_vcvtph2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2ph
        unchecked((ulong)(BAD_CODE)), // INS_vextracti32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x38)))) << 8)))), // INS_vinserti32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x02)))) << 8)))), // INS_vpblendd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x78)))) << 8)))), // INS_vpbroadcastb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x58)))) << 8)))), // INS_vpbroadcastd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x59)))) << 8)))), // INS_vpbroadcastq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x79)))) << 8)))), // INS_vpbroadcastw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x46)))) << 8)))), // INS_vperm2i128
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x36)))) << 8)))), // INS_vpermd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x01)))) << 8)))), // INS_vpermpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x16)))) << 8)))), // INS_vpermps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x00)))) << 8)))), // INS_vpermq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8C)))) << 8)))), // INS_vpmaskmovd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8C)))) << 8)))), // INS_vpmaskmovq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x47)))) << 8)))), // INS_vpsllvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x47)))) << 8)))), // INS_vpsllvq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x46)))) << 8)))), // INS_vpsravd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x45)))) << 8)))), // INS_vpsrlvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x45)))) << 8)))), // INS_vpsrlvq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x98)))) << 8)))), // INS_vfmadd132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA8)))) << 8)))), // INS_vfmadd213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB8)))) << 8)))), // INS_vfmadd231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x98)))) << 8)))), // INS_vfmadd132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA8)))) << 8)))), // INS_vfmadd213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB8)))) << 8)))), // INS_vfmadd231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x99)))) << 8)))), // INS_vfmadd132sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA9)))) << 8)))), // INS_vfmadd213sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB9)))) << 8)))), // INS_vfmadd231sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x99)))) << 8)))), // INS_vfmadd132ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA9)))) << 8)))), // INS_vfmadd213ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB9)))) << 8)))), // INS_vfmadd231ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x96)))) << 8)))), // INS_vfmaddsub132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA6)))) << 8)))), // INS_vfmaddsub213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB6)))) << 8)))), // INS_vfmaddsub231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x96)))) << 8)))), // INS_vfmaddsub132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA6)))) << 8)))), // INS_vfmaddsub213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB6)))) << 8)))), // INS_vfmaddsub231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x97)))) << 8)))), // INS_vfmsubadd132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA7)))) << 8)))), // INS_vfmsubadd213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB7)))) << 8)))), // INS_vfmsubadd231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x97)))) << 8)))), // INS_vfmsubadd132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA7)))) << 8)))), // INS_vfmsubadd213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB7)))) << 8)))), // INS_vfmsubadd231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9A)))) << 8)))), // INS_vfmsub132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAA)))) << 8)))), // INS_vfmsub213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBA)))) << 8)))), // INS_vfmsub231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9A)))) << 8)))), // INS_vfmsub132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAA)))) << 8)))), // INS_vfmsub213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBA)))) << 8)))), // INS_vfmsub231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9B)))) << 8)))), // INS_vfmsub132sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAB)))) << 8)))), // INS_vfmsub213sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBB)))) << 8)))), // INS_vfmsub231sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9B)))) << 8)))), // INS_vfmsub132ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAB)))) << 8)))), // INS_vfmsub213ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBB)))) << 8)))), // INS_vfmsub231ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9C)))) << 8)))), // INS_vfnmadd132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAC)))) << 8)))), // INS_vfnmadd213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBC)))) << 8)))), // INS_vfnmadd231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9C)))) << 8)))), // INS_vfnmadd132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAC)))) << 8)))), // INS_vfnmadd213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBC)))) << 8)))), // INS_vfnmadd231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9D)))) << 8)))), // INS_vfnmadd132sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAD)))) << 8)))), // INS_vfnmadd213sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBD)))) << 8)))), // INS_vfnmadd231sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9D)))) << 8)))), // INS_vfnmadd132ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAD)))) << 8)))), // INS_vfnmadd213ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBD)))) << 8)))), // INS_vfnmadd231ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9E)))) << 8)))), // INS_vfnmsub132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAE)))) << 8)))), // INS_vfnmsub213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBE)))) << 8)))), // INS_vfnmsub231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9E)))) << 8)))), // INS_vfnmsub132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAE)))) << 8)))), // INS_vfnmsub213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBE)))) << 8)))), // INS_vfnmsub231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9F)))) << 8)))), // INS_vfnmsub132sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAF)))) << 8)))), // INS_vfnmsub213sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBF)))) << 8)))), // INS_vfnmsub231sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9F)))) << 8)))), // INS_vfnmsub132ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAF)))) << 8)))), // INS_vfnmsub213ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBF)))) << 8)))), // INS_vfnmsub231ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF2)))) << 8)))), // INS_andn
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF7)))) << 8)))), // INS_bextr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF3)))) << 8)))), // INS_blsi
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF3)))) << 8)))), // INS_blsmsk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF3)))) << 8)))), // INS_blsr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF5)))) << 8)))), // INS_bzhi
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF6)))) << 8)))), // INS_mulx
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF5)))) << 8)))), // INS_pdep
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF5)))) << 8)))), // INS_pext
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xF0)))) << 8)))), // INS_rorx
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xF7))) << 8)))), // INS_sarx
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF7)))) << 8)))), // INS_shlx
        unchecked((ulong)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xF7))) << 8)))), // INS_shrx
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x50)))) << 8)))), // INS_vpdpbusd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x51)))) << 8)))), // INS_vpdpbusds
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x52)))) << 8)))), // INS_vpdpwssd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x53)))) << 8)))), // INS_vpdpwssds
        unchecked((ulong)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD2))) << 8)))), // INS_vpdpwsud
        unchecked((ulong)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD3))) << 8)))), // INS_vpdpwsuds
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xD2)))) << 8)))), // INS_vpdpwusd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xD3)))) << 8)))), // INS_vpdpwusds
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD2))) << 8)))), // INS_vpdpwuud
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD3))) << 8)))), // INS_vpdpwuuds
        unchecked((ulong)((((((0xf2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x50))) << 8)))), // INS_vpdpbssd
        unchecked((ulong)((((((0xf2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x51))) << 8)))), // INS_vpdpbssds
        unchecked((ulong)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x50))) << 8)))), // INS_vpdpbsud
        unchecked((ulong)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x51))) << 8)))), // INS_vpdpbsuds
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x50))) << 8)))), // INS_vpdpbuud
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x51))) << 8)))), // INS_vpdpbuuds
        unchecked((ulong)((((((0x06))) << 16) | (((0x80)))))), // INS_vbmacor16x16x16
        unchecked((ulong)((((((0x06))) << 16) | (((0x80)))))), // INS_vbmacxor16x16x16
        unchecked((ulong)((((((0x06))) << 16) | (((0x81)))))), // INS_vbitrev
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB5)))) << 8)))), // INS_vpmadd52huq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB4)))) << 8)))), // INS_vpmadd52luq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x4A)))))), // INS_kaddb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x4A)))))), // INS_kaddd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x4A)))))), // INS_kaddq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x4A)))))), // INS_kaddw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x41)))))), // INS_kandb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x41)))))), // INS_kandd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x42)))))), // INS_kandnb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x42)))))), // INS_kandnd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x42)))))), // INS_kandnq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x42)))))), // INS_kandnw
        unchecked((ulong)(((((0x0f)) << 16) | (((0x41)))))), // INS_kandq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x41)))))), // INS_kandw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x92)))))), // INS_kmovb_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x90)))))), // INS_kmovb_msk
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x92)))))), // INS_kmovd_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x90)))))), // INS_kmovd_msk
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x92)))))), // INS_kmovq_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x90)))))), // INS_kmovq_msk
        unchecked((ulong)(((((0x0f)) << 16) | (((0x92)))))), // INS_kmovw_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x90)))))), // INS_kmovw_msk
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x44)))))), // INS_knotb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x44)))))), // INS_knotd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x44)))))), // INS_knotq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x44)))))), // INS_knotw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x45)))))), // INS_korb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x45)))))), // INS_kord
        unchecked((ulong)(((((0x0f)) << 16) | (((0x45)))))), // INS_korq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x98)))))), // INS_kortestb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x98)))))), // INS_kortestd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x98)))))), // INS_kortestq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x98)))))), // INS_kortestw
        unchecked((ulong)(((((0x0f)) << 16) | (((0x45)))))), // INS_korw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x32)))) << 8)))), // INS_kshiftlb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x33)))) << 8)))), // INS_kshiftld
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x33)))) << 8)))), // INS_kshiftlq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x32)))) << 8)))), // INS_kshiftlw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x30)))) << 8)))), // INS_kshiftrb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x31)))) << 8)))), // INS_kshiftrd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x31)))) << 8)))), // INS_kshiftrq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x30)))) << 8)))), // INS_kshiftrw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x99)))))), // INS_ktestb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x99)))))), // INS_ktestd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x99)))))), // INS_ktestq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x99)))))), // INS_ktestw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x4B)))))), // INS_kunpckbw
        unchecked((ulong)(((((0x0f)) << 16) | (((0x4B)))))), // INS_kunpckdq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x4B)))))), // INS_kunpckwd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x46)))))), // INS_kxnorb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x46)))))), // INS_kxnord
        unchecked((ulong)(((((0x0f)) << 16) | (((0x46)))))), // INS_kxnorq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x46)))))), // INS_kxnorw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x47)))))), // INS_kxorb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x47)))))), // INS_kxord
        unchecked((ulong)(((((0x0f)) << 16) | (((0x47)))))), // INS_kxorq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x47)))))), // INS_kxorw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x03)))) << 8)))), // INS_valignd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x03)))) << 8)))), // INS_valignq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x65)))) << 8)))), // INS_vblendmpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x65)))) << 8)))), // INS_vblendmps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x19)))) << 8)))), // INS_vbroadcastf32x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1B)))) << 8)))), // INS_vbroadcastf32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1A)))) << 8)))), // INS_vbroadcastf64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1B)))) << 8)))), // INS_vbroadcastf64x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x59)))) << 8)))), // INS_vbroadcasti32x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5B)))) << 8)))), // INS_vbroadcasti32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5A)))) << 8)))), // INS_vbroadcasti64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5B)))) << 8)))), // INS_vbroadcasti64x4
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_vcmppd
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC2)))))), // INS_vcmpps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_vcmpsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_vcmpss
        unchecked((ulong)(BAD_CODE)), // INS_vcompresspd
        unchecked((ulong)(BAD_CODE)), // INS_vcompressps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtpd2qq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x79)))))), // INS_vcvtpd2udq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtpd2uqq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtps2qq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x79)))))), // INS_vcvtps2udq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtps2uqq
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_vcvtqq2pd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5B)))))), // INS_vcvtqq2ps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtsd2usi32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtsd2usi64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtss2usi32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtss2usi64
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvttpd2qq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x78)))))), // INS_vcvttpd2udq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttpd2uqq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvttps2qq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x78)))))), // INS_vcvttps2udq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttps2uqq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttsd2usi32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttsd2usi64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttss2usi32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttss2usi64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtudq2pd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtudq2ps
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtuqq2pd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtuqq2ps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2sd32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2sd64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2ss32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2ss64
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x42)))) << 8)))), // INS_vdbpsadbw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x88)))) << 8)))), // INS_vexpandpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x88)))) << 8)))), // INS_vexpandps
        unchecked((ulong)(BAD_CODE)), // INS_vextractf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vextractf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vextractf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vextracti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vextracti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vextracti64x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x54)))) << 8)))), // INS_vfixupimmpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x54)))) << 8)))), // INS_vfixupimmps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x55)))) << 8)))), // INS_vfixupimmsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x55)))) << 8)))), // INS_vfixupimmss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x66)))) << 8)))), // INS_vfpclasspd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x66)))) << 8)))), // INS_vfpclassps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x67)))) << 8)))), // INS_vfpclasssd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x67)))) << 8)))), // INS_vfpclassss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdpd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdps_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqpd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqps_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x42)))) << 8)))), // INS_vgetexppd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x42)))) << 8)))), // INS_vgetexpps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x43)))) << 8)))), // INS_vgetexpsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x43)))) << 8)))), // INS_vgetexpss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x26)))) << 8)))), // INS_vgetmantpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x26)))) << 8)))), // INS_vgetmantps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x27)))) << 8)))), // INS_vgetmantsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x27)))) << 8)))), // INS_vgetmantss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1A)))) << 8)))), // INS_vinsertf32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x18)))) << 8)))), // INS_vinsertf64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1A)))) << 8)))), // INS_vinsertf64x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3A)))) << 8)))), // INS_vinserti32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x38)))) << 8)))), // INS_vinserti64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3A)))) << 8)))), // INS_vinserti64x4
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqa64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqu16
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqu64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqu8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1F)))) << 8)))), // INS_vpabsq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDF)))))), // INS_vpandnq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDB)))))), // INS_vpandq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x66)))) << 8)))), // INS_vpblendmb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x64)))) << 8)))), // INS_vpblendmd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x64)))) << 8)))), // INS_vpblendmq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x66)))) << 8)))), // INS_vpblendmw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7A)))) << 8)))), // INS_vpbroadcastb_gpr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7C)))) << 8)))), // INS_vpbroadcastd_gpr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7C)))) << 8)))), // INS_vpbroadcastq_gpr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7B)))) << 8)))), // INS_vpbroadcastw_gpr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3F)))) << 8)))), // INS_vpcmpb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1F)))) << 8)))), // INS_vpcmpd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x74)))))), // INS_vpcmpeqb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x76)))))), // INS_vpcmpeqd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x29)))) << 8)))), // INS_vpcmpeqq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x75)))))), // INS_vpcmpeqw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x64)))))), // INS_vpcmpgtb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x66)))))), // INS_vpcmpgtd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x37)))) << 8)))), // INS_vpcmpgtq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x65)))))), // INS_vpcmpgtw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1F)))) << 8)))), // INS_vpcmpq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3E)))) << 8)))), // INS_vpcmpub
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1E)))) << 8)))), // INS_vpcmpud
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1E)))) << 8)))), // INS_vpcmpuq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3E)))) << 8)))), // INS_vpcmpuw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3F)))) << 8)))), // INS_vpcmpw
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressd
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC4)))) << 8)))), // INS_vpconflictd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC4)))) << 8)))), // INS_vpconflictq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x76)))) << 8)))), // INS_vpermi2d
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x77)))) << 8)))), // INS_vpermi2pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x77)))) << 8)))), // INS_vpermi2ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x76)))) << 8)))), // INS_vpermi2q
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x75)))) << 8)))), // INS_vpermi2w
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x16)))) << 8)))), // INS_vpermpd_reg
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x36)))) << 8)))), // INS_vpermq_reg
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7E)))) << 8)))), // INS_vpermt2d
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7F)))) << 8)))), // INS_vpermt2pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7F)))) << 8)))), // INS_vpermt2ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7E)))) << 8)))), // INS_vpermt2q
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7D)))) << 8)))), // INS_vpermt2w
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8D)))) << 8)))), // INS_vpermw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x89)))) << 8)))), // INS_vpexpandd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x89)))) << 8)))), // INS_vpexpandq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdq_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqq_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x44)))) << 8)))), // INS_vplzcntd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x44)))) << 8)))), // INS_vplzcntq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3D)))) << 8)))), // INS_vpmaxsq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3F)))) << 8)))), // INS_vpmaxuq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x39)))) << 8)))), // INS_vpminsq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3B)))) << 8)))), // INS_vpminuq
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x29))) << 8)))), // INS_vpmovb2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x39))) << 8)))), // INS_vpmovd2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x31))) << 8)))), // INS_vpmovdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x33))) << 8)))), // INS_vpmovdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x28))) << 8)))), // INS_vpmovm2b
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x38))) << 8)))), // INS_vpmovm2d
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x38))) << 8)))), // INS_vpmovm2q
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x28))) << 8)))), // INS_vpmovm2w
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x39))) << 8)))), // INS_vpmovq2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x32))) << 8)))), // INS_vpmovqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x35))) << 8)))), // INS_vpmovqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x34))) << 8)))), // INS_vpmovqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x21))) << 8)))), // INS_vpmovsdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x23))) << 8)))), // INS_vpmovsdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x22))) << 8)))), // INS_vpmovsqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x25))) << 8)))), // INS_vpmovsqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x24))) << 8)))), // INS_vpmovsqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x20))) << 8)))), // INS_vpmovswb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x11))) << 8)))), // INS_vpmovusdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x13))) << 8)))), // INS_vpmovusdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x12))) << 8)))), // INS_vpmovusqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x15))) << 8)))), // INS_vpmovusqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x14))) << 8)))), // INS_vpmovusqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x10))) << 8)))), // INS_vpmovuswb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x29))) << 8)))), // INS_vpmovw2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x30))) << 8)))), // INS_vpmovwb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x40)))) << 8)))), // INS_vpmullq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEB)))))), // INS_vporq
        unchecked((ulong)(BAD_CODE)), // INS_vprold
        unchecked((ulong)(BAD_CODE)), // INS_vprolq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x15)))) << 8)))), // INS_vprolvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x15)))) << 8)))), // INS_vprolvq
        unchecked((ulong)(BAD_CODE)), // INS_vprord
        unchecked((ulong)(BAD_CODE)), // INS_vprorq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x14)))) << 8)))), // INS_vprorvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x14)))) << 8)))), // INS_vprorvq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA0)))) << 8)))), // INS_vpscatterdd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA0)))) << 8)))), // INS_vpscatterdq_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA1)))) << 8)))), // INS_vpscatterqd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA1)))) << 8)))), // INS_vpscatterqq_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x12)))) << 8)))), // INS_vpsllvw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE2)))))), // INS_vpsraq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x46)))) << 8)))), // INS_vpsravq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x11)))) << 8)))), // INS_vpsravw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x10)))) << 8)))), // INS_vpsrlvw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x25)))) << 8)))), // INS_vpternlogd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x25)))) << 8)))), // INS_vpternlogq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x26)))) << 8)))), // INS_vptestmb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x27)))) << 8)))), // INS_vptestmd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x27)))) << 8)))), // INS_vptestmq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x26)))) << 8)))), // INS_vptestmw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x26))) << 8)))), // INS_vptestnmb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x27))) << 8)))), // INS_vptestnmd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x27))) << 8)))), // INS_vptestnmq
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x26))) << 8)))), // INS_vptestnmw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEF)))))), // INS_vpxorq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x50)))) << 8)))), // INS_vrangepd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x50)))) << 8)))), // INS_vrangeps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x51)))) << 8)))), // INS_vrangesd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x51)))) << 8)))), // INS_vrangess
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4C)))) << 8)))), // INS_vrcp14pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4C)))) << 8)))), // INS_vrcp14ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4D)))) << 8)))), // INS_vrcp14sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4D)))) << 8)))), // INS_vrcp14ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x56)))) << 8)))), // INS_vreducepd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x56)))) << 8)))), // INS_vreduceps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x57)))) << 8)))), // INS_vreducesd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x57)))) << 8)))), // INS_vreducess
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x09)))) << 8)))), // INS_vrndscalepd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x08)))) << 8)))), // INS_vrndscaleps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0B)))) << 8)))), // INS_vrndscalesd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0A)))) << 8)))), // INS_vrndscaless
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4E)))) << 8)))), // INS_vrsqrt14pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4E)))) << 8)))), // INS_vrsqrt14ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4F)))) << 8)))), // INS_vrsqrt14sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4F)))) << 8)))), // INS_vrsqrt14ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2C)))) << 8)))), // INS_vscalefpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2C)))) << 8)))), // INS_vscalefps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2D)))) << 8)))), // INS_vscalefsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2D)))) << 8)))), // INS_vscalefss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA2)))) << 8)))), // INS_vscatterdpd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA2)))) << 8)))), // INS_vscatterdps_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA3)))) << 8)))), // INS_vscatterqpd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA3)))) << 8)))), // INS_vscatterqps_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x23)))) << 8)))), // INS_vshuff32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x23)))) << 8)))), // INS_vshuff64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x43)))) << 8)))), // INS_vshufi32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x43)))) << 8)))), // INS_vshufi64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8D)))) << 8)))), // INS_vpermb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x75)))) << 8)))), // INS_vpermi2b
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7D)))) << 8)))), // INS_vpermt2b
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x83)))) << 8)))), // INS_vpmultishiftqb
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressb
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x62)))) << 8)))), // INS_vpexpandb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x62)))) << 8)))), // INS_vpexpandw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x54)))) << 8)))), // INS_vpopcntb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x55)))) << 8)))), // INS_vpopcntd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x55)))) << 8)))), // INS_vpopcntq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x54)))) << 8)))), // INS_vpopcntw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x71)))) << 8)))), // INS_vpshldd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x71)))) << 8)))), // INS_vpshldq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x71)))) << 8)))), // INS_vpshldvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x71)))) << 8)))), // INS_vpshldvq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x70)))) << 8)))), // INS_vpshldvw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x70)))) << 8)))), // INS_vpshldw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x73)))) << 8)))), // INS_vpshrdd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x73)))) << 8)))), // INS_vpshrdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x73)))) << 8)))), // INS_vpshrdvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x73)))) << 8)))), // INS_vpshrdvq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x72)))) << 8)))), // INS_vpshrdvw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x72)))) << 8)))), // INS_vpshrdw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8F)))) << 8)))), // INS_vpshufbitqmb
        unchecked((ulong)((((((0x05))) << 16) | (((0x58)))))), // INS_vaddph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x58)))))), // INS_vaddsh
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0xC2))) << 8)))), // INS_vcmpph
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0xC2))) << 8)))), // INS_vcmpsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x2F)))))), // INS_vcomish
        unchecked((ulong)((((((0x05))) << 16) | (((0x5B)))))), // INS_vcvtdq2ph
        unchecked((ulong)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x72))) << 8)))), // INS_vcvtne2ps2bf16
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x72))) << 8)))), // INS_vcvtneps2bf16
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x5A)))))), // INS_vcvtpd2ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x5B)))))), // INS_vcvtph2dq
        unchecked((ulong)((((((0x05))) << 16) | (((0x5A)))))), // INS_vcvtph2pd
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x13)))))), // INS_vcvtph2psx
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7B)))))), // INS_vcvtph2qq
        unchecked((ulong)((((((0x05))) << 16) | (((0x79)))))), // INS_vcvtph2udq
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x79)))))), // INS_vcvtph2uqq
        unchecked((ulong)((((((0x05))) << 16) | (((0x7D)))))), // INS_vcvtph2uw
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7D)))))), // INS_vcvtph2w
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x1D)))))), // INS_vcvtps2phx
        unchecked((ulong)((((((0x05))) << 16) | (((0x5B)))))), // INS_vcvtqq2ph
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x5A)))))), // INS_vcvtsd2sh
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5A)))))), // INS_vcvtsh2sd
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2D)))))), // INS_vcvtsh2si32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2D)))))), // INS_vcvtsh2si64
        unchecked((ulong)((((((0x06))) << 16) | (((0x13)))))), // INS_vcvtsh2ss
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x79)))))), // INS_vcvtsh2usi32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x79)))))), // INS_vcvtsh2usi64
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2A)))))), // INS_vcvtsi2sh32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2A)))))), // INS_vcvtsi2sh64
        unchecked((ulong)((((((0x05))) << 16) | (((0x1D)))))), // INS_vcvtss2sh
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5B)))))), // INS_vcvttph2dq
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7A)))))), // INS_vcvttph2qq
        unchecked((ulong)((((((0x05))) << 16) | (((0x78)))))), // INS_vcvttph2udq
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x78)))))), // INS_vcvttph2uqq
        unchecked((ulong)((((((0x05))) << 16) | (((0x7C)))))), // INS_vcvttph2uw
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7C)))))), // INS_vcvttph2w
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2C)))))), // INS_vcvttsh2si32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2C)))))), // INS_vcvttsh2si64
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x78)))))), // INS_vcvttsh2usi32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x78)))))), // INS_vcvttsh2usi64
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x7A)))))), // INS_vcvtudq2ph
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x7A)))))), // INS_vcvtuqq2ph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7B)))))), // INS_vcvtusi2sh32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7B)))))), // INS_vcvtusi2sh64
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x7D)))))), // INS_vcvtuw2ph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7D)))))), // INS_vcvtw2ph
        unchecked((ulong)((((((0x05))) << 16) | (((0x5E)))))), // INS_vdivph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5E)))))), // INS_vdivsh
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x52))) << 8)))), // INS_vdpbf16ps
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0x56)))))), // INS_vfcmaddcph
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0x57)))))), // INS_vfcmaddcsh
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0xD6)))))), // INS_vfcmulcph
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0xD7)))))), // INS_vfcmulcsh
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0xD6)))))), // INS_vfmulcph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0xD7)))))), // INS_vfmulcsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x98)))))), // INS_vfmadd132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA8)))))), // INS_vfmadd213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB8)))))), // INS_vfmadd231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x99)))))), // INS_vfmadd132sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA9)))))), // INS_vfmadd213sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB9)))))), // INS_vfmadd231sh
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0x56)))))), // INS_vfmaddcph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0x57)))))), // INS_vfmaddcsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x96)))))), // INS_vfmaddsub132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA6)))))), // INS_vfmaddsub213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB6)))))), // INS_vfmaddsub231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9A)))))), // INS_vfmsub132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAA)))))), // INS_vfmsub213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBA)))))), // INS_vfmsub231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9B)))))), // INS_vfmsub132sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAB)))))), // INS_vfmsub213sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBB)))))), // INS_vfmsub231sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x97)))))), // INS_vfmsubadd132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA7)))))), // INS_vfmsubadd213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB7)))))), // INS_vfmsubadd231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9C)))))), // INS_vfnmadd132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAC)))))), // INS_vfnmadd213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBC)))))), // INS_vfnmadd231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9D)))))), // INS_vfnmadd132sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAD)))))), // INS_vfnmadd213sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBD)))))), // INS_vfnmadd231sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9E)))))), // INS_vfnmsub132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAE)))))), // INS_vfnmsub213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBE)))))), // INS_vfnmsub231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9F)))))), // INS_vfnmsub132sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAF)))))), // INS_vfnmsub213sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBF)))))), // INS_vfnmsub231sh
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x66))) << 8)))), // INS_vfpclassph
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x67))) << 8)))), // INS_vfpclasssh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x42)))))), // INS_vgetexpph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x43)))))), // INS_vgetexpsh
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x26))) << 8)))), // INS_vgetmantph
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x27))) << 8)))), // INS_vgetmantsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x5F)))))), // INS_vmaxph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5F)))))), // INS_vmaxsh
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5D)))))), // INS_vminsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x5D)))))), // INS_vminph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x10)))))), // INS_vmovsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x00))) << 24) | (((0x6E)))))), // INS_vmovw
        unchecked((ulong)((((((0x05))) << 16) | (((0x59)))))), // INS_vmulph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x59)))))), // INS_vmulsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4C)))))), // INS_vrcpph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4D)))))), // INS_vrcpsh
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x56))) << 8)))), // INS_vreduceph
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x57))) << 8)))), // INS_vreducesh
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x08))) << 8)))), // INS_vrndscaleph
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x0A))) << 8)))), // INS_vrndscalesh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4E)))))), // INS_vrsqrtph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4F)))))), // INS_vrsqrtsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x2C)))))), // INS_vscalefph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x2D)))))), // INS_vscalefsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x51)))))), // INS_vsqrtph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x51)))))), // INS_vsqrtsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x5C)))))), // INS_vsubph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5C)))))), // INS_vsubsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x2E)))))), // INS_vucomish
        unchecked((ulong)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x68))) << 8)))), // INS_vp2intersectd
        unchecked((ulong)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x68))) << 8)))), // INS_vp2intersectq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2f)))))), // INS_vcomxsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2f)))))), // INS_vcomxss
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x69)))))), // INS_vcvtps2ibs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6B)))))), // INS_vcvtps2iubs
        unchecked((ulong)((((((0x05))) << 16) | (((0x6D)))))), // INS_vcvttpd2dqs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttpd2qqs
        unchecked((ulong)((((((0x05))) << 16) | (((0x6C)))))), // INS_vcvttpd2udqs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttpd2uqqs
        unchecked((ulong)((((((0x05))) << 16) | (((0x6D)))))), // INS_vcvttps2dqs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x68)))))), // INS_vcvttps2ibs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6A)))))), // INS_vcvttps2iubs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttps2qqs
        unchecked((ulong)((((((0x05))) << 16) | (((0x6C)))))), // INS_vcvttps2udqs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttps2uqqs
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttsd2sis32
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttsd2sis64
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttsd2usis32
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttsd2usis64
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttss2sis32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttss2sis64
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttss2usis32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttss2usis64
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x52)))) << 8)))), // INS_vminmaxpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x52)))) << 8)))), // INS_vminmaxps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x53)))) << 8)))), // INS_vminmaxsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x53)))) << 8)))), // INS_vminmaxss
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_vmovd_simd
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6E)))))), // INS_vmovw_simd
        unchecked((ulong)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x42)))) << 8)))), // INS_vmpsadbw
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2e)))))), // INS_vucomxsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2e)))))), // INS_vucomxss
        unchecked((ulong)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xF0))) << 8)))), // INS_crc32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xBC)))))), // INS_tzcnt
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xBD)))))), // INS_lzcnt
        unchecked((ulong)(((((0x0F)) << 16) | (((0x38)) << 24) | (((0xF0)))))), // INS_movbe
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xB8)))))), // INS_popcnt
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xAE)))))), // INS_tpause
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xAE)))))), // INS_umonitor
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xAE)))))), // INS_umwait
        unchecked((ulong)(0x0018F6)), // INS_neg
        unchecked((ulong)(0x0010F6)), // INS_not
        unchecked((ulong)(0x0000D2)), // INS_rol
        unchecked((ulong)(0x0000D0)), // INS_rol_1
        unchecked((ulong)(BAD_CODE)), // INS_rol_N
        unchecked((ulong)(0x0008D2)), // INS_ror
        unchecked((ulong)(0x0008D0)), // INS_ror_1
        unchecked((ulong)(BAD_CODE)), // INS_ror_N
        unchecked((ulong)(0x0010D2)), // INS_rcl
        unchecked((ulong)(0x0010D0)), // INS_rcl_1
        unchecked((ulong)(BAD_CODE)), // INS_rcl_N
        unchecked((ulong)(0x0018D2)), // INS_rcr
        unchecked((ulong)(0x0018D0)), // INS_rcr_1
        unchecked((ulong)(BAD_CODE)), // INS_rcr_N
        unchecked((ulong)(0x0020D2)), // INS_shl
        unchecked((ulong)(0x0020D0)), // INS_shl_1
        unchecked((ulong)(BAD_CODE)), // INS_shl_N
        unchecked((ulong)(0x0028D2)), // INS_shr
        unchecked((ulong)(0x0028D0)), // INS_shr_1
        unchecked((ulong)(BAD_CODE)), // INS_shr_N
        unchecked((ulong)(0x0038D2)), // INS_sar
        unchecked((ulong)(0x0038D0)), // INS_sar_1
        unchecked((ulong)(BAD_CODE)), // INS_sar_N
    ];

    private static ReadOnlySpan<ulong> insCodesRMX64 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(BAD_CODE)), // INS_push
        unchecked((ulong)(BAD_CODE)), // INS_pop
        unchecked((ulong)(BAD_CODE)), // INS_push_hide
        unchecked((ulong)(BAD_CODE)), // INS_pop_hide
        unchecked((ulong)(0x0030FF)), // INS_push2
        unchecked((ulong)(0x00008F)), // INS_pop2
        unchecked((ulong)(0x0000FE)), // INS_inc
        unchecked((ulong)(BAD_CODE)), // INS_inc_l
        unchecked((ulong)(0x0008FE)), // INS_dec
        unchecked((ulong)(BAD_CODE)), // INS_dec_l
        unchecked((ulong)(BAD_CODE)), // INS_bswap
        unchecked((ulong)(0x000002)), // INS_add
        unchecked((ulong)(0x00000A)), // INS_or
        unchecked((ulong)(0x000012)), // INS_adc
        unchecked((ulong)(0x00001A)), // INS_sbb
        unchecked((ulong)(0x000022)), // INS_and
        unchecked((ulong)(0x00002A)), // INS_sub
        unchecked((ulong)(0x00002A)), // INS_sub_hide
        unchecked((ulong)(0x000032)), // INS_xor
        unchecked((ulong)(0x00003A)), // INS_cmp
        unchecked((ulong)(0x000084)), // INS_test
        unchecked((ulong)(0x00008A)), // INS_mov
        unchecked((ulong)(0x00008D)), // INS_lea
        unchecked((ulong)(0x0F00A3)), // INS_bt
        unchecked((ulong)(0x0F00AB)), // INS_bts
        unchecked((ulong)(0x0F00B3)), // INS_btr
        unchecked((ulong)(0x0F00BB)), // INS_btc
        unchecked((ulong)(0x0F00BD)), // INS_bsr
        unchecked((ulong)(0x0F00BC)), // INS_bsf
        unchecked((ulong)(0x0F00BE)), // INS_movsx
        unchecked((ulong)(0x000063)), // INS_movsxd
        unchecked((ulong)(0x0F00B6)), // INS_movzx
        unchecked((ulong)(0x0F0040)), // INS_cmovo
        unchecked((ulong)(0x0F0041)), // INS_cmovno
        unchecked((ulong)(0x0F0042)), // INS_cmovb
        unchecked((ulong)(0x0F0043)), // INS_cmovae
        unchecked((ulong)(0x0F0044)), // INS_cmove
        unchecked((ulong)(0x0F0045)), // INS_cmovne
        unchecked((ulong)(0x0F0046)), // INS_cmovbe
        unchecked((ulong)(0x0F0047)), // INS_cmova
        unchecked((ulong)(0x0F0048)), // INS_cmovs
        unchecked((ulong)(0x0F0049)), // INS_cmovns
        unchecked((ulong)(0x0F004A)), // INS_cmovp
        unchecked((ulong)(0x0F004B)), // INS_cmovnp
        unchecked((ulong)(0x0F004C)), // INS_cmovl
        unchecked((ulong)(0x0F004D)), // INS_cmovge
        unchecked((ulong)(0x0F004E)), // INS_cmovle
        unchecked((ulong)(0x0F004F)), // INS_cmovg
        unchecked((ulong)(0x000086)), // INS_xchg
        unchecked((ulong)(0x0F00AF)), // INS_imul
        unchecked((ulong)(BAD_CODE)), // INS_imul_AX
        unchecked((ulong)(BAD_CODE)), // INS_imul_CX
        unchecked((ulong)(BAD_CODE)), // INS_imul_DX
        unchecked((ulong)(BAD_CODE)), // INS_imul_BX
        unchecked((ulong)(BAD_CODE)), // INS_imul_SP
        unchecked((ulong)(BAD_CODE)), // INS_imul_BP
        unchecked((ulong)(BAD_CODE)), // INS_imul_SI
        unchecked((ulong)(BAD_CODE)), // INS_imul_DI
        unchecked((ulong)(BAD_CODE)), // INS_imul_08
        unchecked((ulong)(BAD_CODE)), // INS_imul_09
        unchecked((ulong)(BAD_CODE)), // INS_imul_10
        unchecked((ulong)(BAD_CODE)), // INS_imul_11
        unchecked((ulong)(BAD_CODE)), // INS_imul_12
        unchecked((ulong)(BAD_CODE)), // INS_imul_13
        unchecked((ulong)(BAD_CODE)), // INS_imul_14
        unchecked((ulong)(BAD_CODE)), // INS_imul_15
        unchecked((ulong)(BAD_CODE)), // INS_imul_16
        unchecked((ulong)(BAD_CODE)), // INS_imul_17
        unchecked((ulong)(BAD_CODE)), // INS_imul_18
        unchecked((ulong)(BAD_CODE)), // INS_imul_19
        unchecked((ulong)(BAD_CODE)), // INS_imul_20
        unchecked((ulong)(BAD_CODE)), // INS_imul_21
        unchecked((ulong)(BAD_CODE)), // INS_imul_22
        unchecked((ulong)(BAD_CODE)), // INS_imul_23
        unchecked((ulong)(BAD_CODE)), // INS_imul_24
        unchecked((ulong)(BAD_CODE)), // INS_imul_25
        unchecked((ulong)(BAD_CODE)), // INS_imul_26
        unchecked((ulong)(BAD_CODE)), // INS_imul_27
        unchecked((ulong)(BAD_CODE)), // INS_imul_28
        unchecked((ulong)(BAD_CODE)), // INS_imul_29
        unchecked((ulong)(BAD_CODE)), // INS_imul_30
        unchecked((ulong)(BAD_CODE)), // INS_imul_31
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x58)))))), // INS_addpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x58)))))), // INS_addps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x58)))))), // INS_addsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x58)))))), // INS_addss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD0)))))), // INS_addsubpd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xD0)))))), // INS_addsubps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x55)))))), // INS_andnpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x55)))))), // INS_andnps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x54)))))), // INS_andpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x54)))))), // INS_andps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0D)))) << 8)))), // INS_blendpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0C)))) << 8)))), // INS_blendps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x15)))) << 8)))), // INS_blendvpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x14)))) << 8)))), // INS_blendvps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_cmppd
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC2)))))), // INS_cmpps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_cmpsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_cmpss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2F)))))), // INS_comisd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x2F)))))), // INS_comiss
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_cvtdq2pd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5B)))))), // INS_cvtdq2ps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_cvtpd2dq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5A)))))), // INS_cvtpd2ps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5B)))))), // INS_cvtps2dq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5A)))))), // INS_cvtps2pd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtsd2si32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtsd2si64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5A)))))), // INS_cvtsd2ss
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2sd32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2sd64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2ss32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2A)))))), // INS_cvtsi2ss64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5A)))))), // INS_cvtss2sd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtss2si32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2D)))))), // INS_cvtss2si64
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_cvttpd2dq
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5B)))))), // INS_cvttps2dq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttsd2si32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttsd2si64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttss2si32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2C)))))), // INS_cvttss2si64
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5E)))))), // INS_divpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5E)))))), // INS_divps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5E)))))), // INS_divsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5E)))))), // INS_divss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x41)))) << 8)))), // INS_dppd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x40)))) << 8)))), // INS_dpps
        unchecked((ulong)(BAD_CODE)), // INS_extractps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7C)))))), // INS_haddpd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7C)))))), // INS_haddps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7D)))))), // INS_hsubpd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7D)))))), // INS_hsubps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x21)))) << 8)))), // INS_insertps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xF0)))))), // INS_lddqu
        unchecked((ulong)(BAD_CODE)), // INS_lfence
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF7)))))), // INS_maskmovdqu
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5F)))))), // INS_maxpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5F)))))), // INS_maxps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5F)))))), // INS_maxsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5F)))))), // INS_maxss
        unchecked((ulong)(BAD_CODE)), // INS_mfence
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5D)))))), // INS_minpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5D)))))), // INS_minps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5D)))))), // INS_minsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5D)))))), // INS_minss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x28)))))), // INS_movapd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x28)))))), // INS_movaps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6E)))))), // INS_movd32
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6E)))))), // INS_movd64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x12)))))), // INS_movddup
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_movdqa32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_movdqu32
        unchecked((ulong)(((((0x0f)) << 16) | (((0x12)))))), // INS_movhlps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x16)))))), // INS_movhpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x16)))))), // INS_movhps
        unchecked((ulong)(((((0x0f)) << 16) | (((0x16)))))), // INS_movlhps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x12)))))), // INS_movlpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x12)))))), // INS_movlps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x50)))))), // INS_movmskpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x50)))))), // INS_movmskps
        unchecked((ulong)(BAD_CODE)), // INS_movntdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2A)))) << 8)))), // INS_movntdqa
        unchecked((ulong)(BAD_CODE)), // INS_movnti32
        unchecked((ulong)(BAD_CODE)), // INS_movnti64
        unchecked((ulong)(BAD_CODE)), // INS_movntpd
        unchecked((ulong)(BAD_CODE)), // INS_movntps
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x10)))))), // INS_movsd_simd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x16)))))), // INS_movshdup
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x12)))))), // INS_movsldup
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x10)))))), // INS_movss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x10)))))), // INS_movupd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x10)))))), // INS_movups
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x42)))) << 8)))), // INS_mpsadbw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x59)))))), // INS_mulpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x59)))))), // INS_mulps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x59)))))), // INS_mulsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x59)))))), // INS_mulss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x56)))))), // INS_orpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x56)))))), // INS_orps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1C)))) << 8)))), // INS_pabsb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1E)))) << 8)))), // INS_pabsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1D)))) << 8)))), // INS_pabsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6B)))))), // INS_packssdw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x63)))))), // INS_packsswb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2B)))) << 8)))), // INS_packusdw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x67)))))), // INS_packuswb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFC)))))), // INS_paddb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFE)))))), // INS_paddd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD4)))))), // INS_paddq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEC)))))), // INS_paddsb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xED)))))), // INS_paddsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDC)))))), // INS_paddusb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDD)))))), // INS_paddusw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFD)))))), // INS_paddw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0F)))) << 8)))), // INS_palignr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDB)))))), // INS_pandd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDF)))))), // INS_pandnd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE0)))))), // INS_pavgb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE3)))))), // INS_pavgw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x10)))) << 8)))), // INS_pblendvb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0E)))) << 8)))), // INS_pblendw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x74)))))), // INS_pcmpeqb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x76)))))), // INS_pcmpeqd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x29)))) << 8)))), // INS_pcmpeqq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x75)))))), // INS_pcmpeqw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x64)))))), // INS_pcmpgtb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x66)))))), // INS_pcmpgtd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x37)))) << 8)))), // INS_pcmpgtq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x65)))))), // INS_pcmpgtw
        unchecked((ulong)(BAD_CODE)), // INS_pextrb
        unchecked((ulong)(BAD_CODE)), // INS_pextrd
        unchecked((ulong)(BAD_CODE)), // INS_pextrq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x02)))) << 8)))), // INS_phaddd
        unchecked((ulong)(BAD_CODE)), // INS_pextrw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x03)))) << 8)))), // INS_phaddsw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x01)))) << 8)))), // INS_phaddw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x41)))) << 8)))), // INS_phminposuw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x06)))) << 8)))), // INS_phsubd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x07)))) << 8)))), // INS_phsubsw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x05)))) << 8)))), // INS_phsubw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x20)))) << 8)))), // INS_pinsrb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x22)))) << 8)))), // INS_pinsrd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x22)))) << 8)))), // INS_pinsrq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC4)))))), // INS_pinsrw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x04)))) << 8)))), // INS_pmaddubsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF5)))))), // INS_pmaddwd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3C)))) << 8)))), // INS_pmaxsb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3D)))) << 8)))), // INS_pmaxsd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEE)))))), // INS_pmaxsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDE)))))), // INS_pmaxub
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3F)))) << 8)))), // INS_pmaxud
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3E)))) << 8)))), // INS_pmaxuw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x38)))) << 8)))), // INS_pminsb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x39)))) << 8)))), // INS_pminsd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEA)))))), // INS_pminsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDA)))))), // INS_pminub
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3B)))) << 8)))), // INS_pminud
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3A)))) << 8)))), // INS_pminuw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD7)))))), // INS_pmovmskb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x21)))) << 8)))), // INS_pmovsxbd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x22)))) << 8)))), // INS_pmovsxbq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x20)))) << 8)))), // INS_pmovsxbw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x25)))) << 8)))), // INS_pmovsxdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x23)))) << 8)))), // INS_pmovsxwd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x24)))) << 8)))), // INS_pmovsxwq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x31)))) << 8)))), // INS_pmovzxbd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x32)))) << 8)))), // INS_pmovzxbq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x30)))) << 8)))), // INS_pmovzxbw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x35)))) << 8)))), // INS_pmovzxdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x33)))) << 8)))), // INS_pmovzxwd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x34)))) << 8)))), // INS_pmovzxwq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x28)))) << 8)))), // INS_pmuldq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0B)))) << 8)))), // INS_pmulhrsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE4)))))), // INS_pmulhuw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE5)))))), // INS_pmulhw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x40)))) << 8)))), // INS_pmulld
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF4)))))), // INS_pmuludq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD5)))))), // INS_pmullw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEB)))))), // INS_pord
        unchecked((ulong)(BAD_CODE)), // INS_prefetchnta
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht0
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht1
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht2
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF6)))))), // INS_psadbw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x00)))) << 8)))), // INS_pshufb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x70)))))), // INS_pshufd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x70)))))), // INS_pshufhw
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x70)))))), // INS_pshuflw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x08)))) << 8)))), // INS_psignb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0A)))) << 8)))), // INS_psignd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x09)))) << 8)))), // INS_psignw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF2)))))), // INS_pslld
        unchecked((ulong)(BAD_CODE)), // INS_pslldq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF3)))))), // INS_psllq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF1)))))), // INS_psllw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE2)))))), // INS_psrad
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE1)))))), // INS_psraw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD2)))))), // INS_psrld
        unchecked((ulong)(BAD_CODE)), // INS_psrldq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD3)))))), // INS_psrlq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD1)))))), // INS_psrlw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF8)))))), // INS_psubb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFA)))))), // INS_psubd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xFB)))))), // INS_psubq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE8)))))), // INS_psubsb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE9)))))), // INS_psubsw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD8)))))), // INS_psubusb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD9)))))), // INS_psubusw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xF9)))))), // INS_psubw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x17)))) << 8)))), // INS_ptest
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x68)))))), // INS_punpckhbw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6A)))))), // INS_punpckhdq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6D)))))), // INS_punpckhqdq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x69)))))), // INS_punpckhwd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x60)))))), // INS_punpcklbw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x62)))))), // INS_punpckldq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6C)))))), // INS_punpcklqdq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x61)))))), // INS_punpcklwd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEF)))))), // INS_pxord
        unchecked((ulong)(((((0x0f)) << 16) | (((0x53)))))), // INS_rcpps
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x53)))))), // INS_rcpss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x09)))) << 8)))), // INS_roundpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x08)))) << 8)))), // INS_roundps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0B)))) << 8)))), // INS_roundsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0A)))) << 8)))), // INS_roundss
        unchecked((ulong)(((((0x0f)) << 16) | (((0x52)))))), // INS_rsqrtps
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x52)))))), // INS_rsqrtss
        unchecked((ulong)(BAD_CODE)), // INS_sfence
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC6)))))), // INS_shufpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC6)))))), // INS_shufps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x51)))))), // INS_sqrtpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x51)))))), // INS_sqrtps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x51)))))), // INS_sqrtsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x51)))))), // INS_sqrtss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x5C)))))), // INS_subpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5C)))))), // INS_subps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x5C)))))), // INS_subsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x5C)))))), // INS_subss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2E)))))), // INS_ucomisd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x2E)))))), // INS_ucomiss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x15)))))), // INS_unpckhpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x15)))))), // INS_unpckhps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x14)))))), // INS_unpcklpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x14)))))), // INS_unpcklps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x57)))))), // INS_xorpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x57)))))), // INS_xorps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDE)))) << 8)))), // INS_aesdec
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDF)))) << 8)))), // INS_aesdeclast
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDC)))) << 8)))), // INS_aesenc
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDD)))) << 8)))), // INS_aesenclast
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xDB)))) << 8)))), // INS_aesimc
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xDF)))) << 8)))), // INS_aeskeygenassist
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x44)))) << 8)))), // INS_pclmulqdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC9)))) << 8)))), // INS_sha1msg1
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCA)))) << 8)))), // INS_sha1msg2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC8)))) << 8)))), // INS_sha1nexte
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xCC)))) << 8)))), // INS_sha1rnds4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCC)))) << 8)))), // INS_sha256msg1
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCD)))) << 8)))), // INS_sha256msg2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCB)))) << 8)))), // INS_sha256rnds2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xCF)))) << 8)))), // INS_gf2p8affineinvqb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xCE)))) << 8)))), // INS_gf2p8affineqb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xCF)))) << 8)))), // INS_gf2p8mulb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x4B)))) << 8)))), // INS_vblendvpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x4A)))) << 8)))), // INS_vblendvps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1A)))) << 8)))), // INS_vbroadcastf32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x19)))) << 8)))), // INS_vbroadcastsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x18)))) << 8)))), // INS_vbroadcastss
        unchecked((ulong)(BAD_CODE)), // INS_vextractf32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x18)))) << 8)))), // INS_vinsertf32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2D)))) << 8)))), // INS_vmaskmovpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2C)))) << 8)))), // INS_vmaskmovps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x4C)))) << 8)))), // INS_vpblendvb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x06)))) << 8)))), // INS_vperm2f128
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x05)))) << 8)))), // INS_vpermilpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0D)))) << 8)))), // INS_vpermilpdvar
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x04)))) << 8)))), // INS_vpermilps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0C)))) << 8)))), // INS_vpermilpsvar
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0F)))) << 8)))), // INS_vtestpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x0E)))) << 8)))), // INS_vtestps
        unchecked((ulong)(BAD_CODE)), // INS_vzeroupper
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5A)))) << 8)))), // INS_vbroadcasti32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x13)))) << 8)))), // INS_vcvtph2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2ph
        unchecked((ulong)(BAD_CODE)), // INS_vextracti32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x38)))) << 8)))), // INS_vinserti32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x02)))) << 8)))), // INS_vpblendd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x78)))) << 8)))), // INS_vpbroadcastb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x58)))) << 8)))), // INS_vpbroadcastd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x59)))) << 8)))), // INS_vpbroadcastq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x79)))) << 8)))), // INS_vpbroadcastw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x46)))) << 8)))), // INS_vperm2i128
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x36)))) << 8)))), // INS_vpermd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x01)))) << 8)))), // INS_vpermpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x16)))) << 8)))), // INS_vpermps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x00)))) << 8)))), // INS_vpermq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8C)))) << 8)))), // INS_vpmaskmovd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8C)))) << 8)))), // INS_vpmaskmovq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x47)))) << 8)))), // INS_vpsllvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x47)))) << 8)))), // INS_vpsllvq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x46)))) << 8)))), // INS_vpsravd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x45)))) << 8)))), // INS_vpsrlvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x45)))) << 8)))), // INS_vpsrlvq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x98)))) << 8)))), // INS_vfmadd132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA8)))) << 8)))), // INS_vfmadd213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB8)))) << 8)))), // INS_vfmadd231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x98)))) << 8)))), // INS_vfmadd132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA8)))) << 8)))), // INS_vfmadd213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB8)))) << 8)))), // INS_vfmadd231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x99)))) << 8)))), // INS_vfmadd132sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA9)))) << 8)))), // INS_vfmadd213sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB9)))) << 8)))), // INS_vfmadd231sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x99)))) << 8)))), // INS_vfmadd132ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA9)))) << 8)))), // INS_vfmadd213ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB9)))) << 8)))), // INS_vfmadd231ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x96)))) << 8)))), // INS_vfmaddsub132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA6)))) << 8)))), // INS_vfmaddsub213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB6)))) << 8)))), // INS_vfmaddsub231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x96)))) << 8)))), // INS_vfmaddsub132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA6)))) << 8)))), // INS_vfmaddsub213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB6)))) << 8)))), // INS_vfmaddsub231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x97)))) << 8)))), // INS_vfmsubadd132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA7)))) << 8)))), // INS_vfmsubadd213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB7)))) << 8)))), // INS_vfmsubadd231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x97)))) << 8)))), // INS_vfmsubadd132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA7)))) << 8)))), // INS_vfmsubadd213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB7)))) << 8)))), // INS_vfmsubadd231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9A)))) << 8)))), // INS_vfmsub132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAA)))) << 8)))), // INS_vfmsub213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBA)))) << 8)))), // INS_vfmsub231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9A)))) << 8)))), // INS_vfmsub132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAA)))) << 8)))), // INS_vfmsub213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBA)))) << 8)))), // INS_vfmsub231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9B)))) << 8)))), // INS_vfmsub132sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAB)))) << 8)))), // INS_vfmsub213sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBB)))) << 8)))), // INS_vfmsub231sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9B)))) << 8)))), // INS_vfmsub132ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAB)))) << 8)))), // INS_vfmsub213ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBB)))) << 8)))), // INS_vfmsub231ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9C)))) << 8)))), // INS_vfnmadd132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAC)))) << 8)))), // INS_vfnmadd213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBC)))) << 8)))), // INS_vfnmadd231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9C)))) << 8)))), // INS_vfnmadd132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAC)))) << 8)))), // INS_vfnmadd213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBC)))) << 8)))), // INS_vfnmadd231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9D)))) << 8)))), // INS_vfnmadd132sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAD)))) << 8)))), // INS_vfnmadd213sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBD)))) << 8)))), // INS_vfnmadd231sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9D)))) << 8)))), // INS_vfnmadd132ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAD)))) << 8)))), // INS_vfnmadd213ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBD)))) << 8)))), // INS_vfnmadd231ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9E)))) << 8)))), // INS_vfnmsub132pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAE)))) << 8)))), // INS_vfnmsub213pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBE)))) << 8)))), // INS_vfnmsub231pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9E)))) << 8)))), // INS_vfnmsub132ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAE)))) << 8)))), // INS_vfnmsub213ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBE)))) << 8)))), // INS_vfnmsub231ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9F)))) << 8)))), // INS_vfnmsub132sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAF)))) << 8)))), // INS_vfnmsub213sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBF)))) << 8)))), // INS_vfnmsub231sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x9F)))) << 8)))), // INS_vfnmsub132ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xAF)))) << 8)))), // INS_vfnmsub213ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xBF)))) << 8)))), // INS_vfnmsub231ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF2)))) << 8)))), // INS_andn
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF7)))) << 8)))), // INS_bextr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF3)))) << 8)))), // INS_blsi
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF3)))) << 8)))), // INS_blsmsk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF3)))) << 8)))), // INS_blsr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF5)))) << 8)))), // INS_bzhi
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF6)))) << 8)))), // INS_mulx
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF5)))) << 8)))), // INS_pdep
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF5)))) << 8)))), // INS_pext
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0xF0)))) << 8)))), // INS_rorx
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xF7))) << 8)))), // INS_sarx
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xF7)))) << 8)))), // INS_shlx
        unchecked((ulong)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xF7))) << 8)))), // INS_shrx
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x50)))) << 8)))), // INS_vpdpbusd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x51)))) << 8)))), // INS_vpdpbusds
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x52)))) << 8)))), // INS_vpdpwssd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x53)))) << 8)))), // INS_vpdpwssds
        unchecked((ulong)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD2))) << 8)))), // INS_vpdpwsud
        unchecked((ulong)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD3))) << 8)))), // INS_vpdpwsuds
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xD2)))) << 8)))), // INS_vpdpwusd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xD3)))) << 8)))), // INS_vpdpwusds
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD2))) << 8)))), // INS_vpdpwuud
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xD3))) << 8)))), // INS_vpdpwuuds
        unchecked((ulong)((((((0xf2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x50))) << 8)))), // INS_vpdpbssd
        unchecked((ulong)((((((0xf2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x51))) << 8)))), // INS_vpdpbssds
        unchecked((ulong)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x50))) << 8)))), // INS_vpdpbsud
        unchecked((ulong)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x51))) << 8)))), // INS_vpdpbsuds
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x50))) << 8)))), // INS_vpdpbuud
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x51))) << 8)))), // INS_vpdpbuuds
        unchecked((ulong)((((((0x06))) << 16) | (((0x80)))))), // INS_vbmacor16x16x16
        unchecked((ulong)((((((0x06))) << 16) | (((0x80)))))), // INS_vbmacxor16x16x16
        unchecked((ulong)((((((0x06))) << 16) | (((0x81)))))), // INS_vbitrev
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB5)))) << 8)))), // INS_vpmadd52huq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xB4)))) << 8)))), // INS_vpmadd52luq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x4A)))))), // INS_kaddb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x4A)))))), // INS_kaddd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x4A)))))), // INS_kaddq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x4A)))))), // INS_kaddw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x41)))))), // INS_kandb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x41)))))), // INS_kandd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x42)))))), // INS_kandnb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x42)))))), // INS_kandnd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x42)))))), // INS_kandnq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x42)))))), // INS_kandnw
        unchecked((ulong)(((((0x0f)) << 16) | (((0x41)))))), // INS_kandq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x41)))))), // INS_kandw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x92)))))), // INS_kmovb_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x90)))))), // INS_kmovb_msk
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x92)))))), // INS_kmovd_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x90)))))), // INS_kmovd_msk
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x92)))))), // INS_kmovq_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x90)))))), // INS_kmovq_msk
        unchecked((ulong)(((((0x0f)) << 16) | (((0x92)))))), // INS_kmovw_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x90)))))), // INS_kmovw_msk
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x44)))))), // INS_knotb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x44)))))), // INS_knotd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x44)))))), // INS_knotq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x44)))))), // INS_knotw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x45)))))), // INS_korb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x45)))))), // INS_kord
        unchecked((ulong)(((((0x0f)) << 16) | (((0x45)))))), // INS_korq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x98)))))), // INS_kortestb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x98)))))), // INS_kortestd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x98)))))), // INS_kortestq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x98)))))), // INS_kortestw
        unchecked((ulong)(((((0x0f)) << 16) | (((0x45)))))), // INS_korw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x32)))) << 8)))), // INS_kshiftlb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x33)))) << 8)))), // INS_kshiftld
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x33)))) << 8)))), // INS_kshiftlq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x32)))) << 8)))), // INS_kshiftlw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x30)))) << 8)))), // INS_kshiftrb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x31)))) << 8)))), // INS_kshiftrd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x31)))) << 8)))), // INS_kshiftrq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x30)))) << 8)))), // INS_kshiftrw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x99)))))), // INS_ktestb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x99)))))), // INS_ktestd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x99)))))), // INS_ktestq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x99)))))), // INS_ktestw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x4B)))))), // INS_kunpckbw
        unchecked((ulong)(((((0x0f)) << 16) | (((0x4B)))))), // INS_kunpckdq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x4B)))))), // INS_kunpckwd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x46)))))), // INS_kxnorb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x46)))))), // INS_kxnord
        unchecked((ulong)(((((0x0f)) << 16) | (((0x46)))))), // INS_kxnorq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x46)))))), // INS_kxnorw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x47)))))), // INS_kxorb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x47)))))), // INS_kxord
        unchecked((ulong)(((((0x0f)) << 16) | (((0x47)))))), // INS_kxorq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x47)))))), // INS_kxorw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x03)))) << 8)))), // INS_valignd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x03)))) << 8)))), // INS_valignq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x65)))) << 8)))), // INS_vblendmpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x65)))) << 8)))), // INS_vblendmps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x19)))) << 8)))), // INS_vbroadcastf32x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1B)))) << 8)))), // INS_vbroadcastf32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1A)))) << 8)))), // INS_vbroadcastf64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1B)))) << 8)))), // INS_vbroadcastf64x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x59)))) << 8)))), // INS_vbroadcasti32x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5B)))) << 8)))), // INS_vbroadcasti32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5A)))) << 8)))), // INS_vbroadcasti64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x5B)))) << 8)))), // INS_vbroadcasti64x4
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_vcmppd
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC2)))))), // INS_vcmpps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_vcmpsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xC2)))))), // INS_vcmpss
        unchecked((ulong)(BAD_CODE)), // INS_vcompresspd
        unchecked((ulong)(BAD_CODE)), // INS_vcompressps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtpd2qq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x79)))))), // INS_vcvtpd2udq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtpd2uqq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtps2qq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x79)))))), // INS_vcvtps2udq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtps2uqq
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xE6)))))), // INS_vcvtqq2pd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x5B)))))), // INS_vcvtqq2ps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtsd2usi32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtsd2usi64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtss2usi32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x79)))))), // INS_vcvtss2usi64
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvttpd2qq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x78)))))), // INS_vcvttpd2udq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttpd2uqq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvttps2qq
        unchecked((ulong)(((((0x0f)) << 16) | (((0x78)))))), // INS_vcvttps2udq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttps2uqq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttsd2usi32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttsd2usi64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttss2usi32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x78)))))), // INS_vcvttss2usi64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtudq2pd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtudq2ps
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtuqq2pd
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7A)))))), // INS_vcvtuqq2ps
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2sd32
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2sd64
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2ss32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7B)))))), // INS_vcvtusi2ss64
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x42)))) << 8)))), // INS_vdbpsadbw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x88)))) << 8)))), // INS_vexpandpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x88)))) << 8)))), // INS_vexpandps
        unchecked((ulong)(BAD_CODE)), // INS_vextractf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vextractf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vextractf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vextracti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vextracti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vextracti64x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x54)))) << 8)))), // INS_vfixupimmpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x54)))) << 8)))), // INS_vfixupimmps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x55)))) << 8)))), // INS_vfixupimmsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x55)))) << 8)))), // INS_vfixupimmss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x66)))) << 8)))), // INS_vfpclasspd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x66)))) << 8)))), // INS_vfpclassps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x67)))) << 8)))), // INS_vfpclasssd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x67)))) << 8)))), // INS_vfpclassss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdpd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x92)))) << 8)))), // INS_vgatherdps_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqpd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x93)))) << 8)))), // INS_vgatherqps_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x42)))) << 8)))), // INS_vgetexppd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x42)))) << 8)))), // INS_vgetexpps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x43)))) << 8)))), // INS_vgetexpsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x43)))) << 8)))), // INS_vgetexpss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x26)))) << 8)))), // INS_vgetmantpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x26)))) << 8)))), // INS_vgetmantps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x27)))) << 8)))), // INS_vgetmantsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x27)))) << 8)))), // INS_vgetmantss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1A)))) << 8)))), // INS_vinsertf32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x18)))) << 8)))), // INS_vinsertf64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1A)))) << 8)))), // INS_vinsertf64x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3A)))) << 8)))), // INS_vinserti32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x38)))) << 8)))), // INS_vinserti64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3A)))) << 8)))), // INS_vinserti64x4
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqa64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqu16
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqu64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x6F)))))), // INS_vmovdqu8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x1F)))) << 8)))), // INS_vpabsq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDF)))))), // INS_vpandnq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xDB)))))), // INS_vpandq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x66)))) << 8)))), // INS_vpblendmb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x64)))) << 8)))), // INS_vpblendmd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x64)))) << 8)))), // INS_vpblendmq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x66)))) << 8)))), // INS_vpblendmw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7A)))) << 8)))), // INS_vpbroadcastb_gpr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7C)))) << 8)))), // INS_vpbroadcastd_gpr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7C)))) << 8)))), // INS_vpbroadcastq_gpr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7B)))) << 8)))), // INS_vpbroadcastw_gpr
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3F)))) << 8)))), // INS_vpcmpb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1F)))) << 8)))), // INS_vpcmpd
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x74)))))), // INS_vpcmpeqb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x76)))))), // INS_vpcmpeqd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x29)))) << 8)))), // INS_vpcmpeqq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x75)))))), // INS_vpcmpeqw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x64)))))), // INS_vpcmpgtb
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x66)))))), // INS_vpcmpgtd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x37)))) << 8)))), // INS_vpcmpgtq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x65)))))), // INS_vpcmpgtw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1F)))) << 8)))), // INS_vpcmpq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3E)))) << 8)))), // INS_vpcmpub
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1E)))) << 8)))), // INS_vpcmpud
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1E)))) << 8)))), // INS_vpcmpuq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3E)))) << 8)))), // INS_vpcmpuw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3F)))) << 8)))), // INS_vpcmpw
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressd
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC4)))) << 8)))), // INS_vpconflictd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xC4)))) << 8)))), // INS_vpconflictq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x76)))) << 8)))), // INS_vpermi2d
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x77)))) << 8)))), // INS_vpermi2pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x77)))) << 8)))), // INS_vpermi2ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x76)))) << 8)))), // INS_vpermi2q
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x75)))) << 8)))), // INS_vpermi2w
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x16)))) << 8)))), // INS_vpermpd_reg
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x36)))) << 8)))), // INS_vpermq_reg
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7E)))) << 8)))), // INS_vpermt2d
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7F)))) << 8)))), // INS_vpermt2pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7F)))) << 8)))), // INS_vpermt2ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7E)))) << 8)))), // INS_vpermt2q
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7D)))) << 8)))), // INS_vpermt2w
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8D)))) << 8)))), // INS_vpermw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x89)))) << 8)))), // INS_vpexpandd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x89)))) << 8)))), // INS_vpexpandq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x90)))) << 8)))), // INS_vpgatherdq_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x91)))) << 8)))), // INS_vpgatherqq_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x44)))) << 8)))), // INS_vplzcntd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x44)))) << 8)))), // INS_vplzcntq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3D)))) << 8)))), // INS_vpmaxsq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3F)))) << 8)))), // INS_vpmaxuq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x39)))) << 8)))), // INS_vpminsq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x3B)))) << 8)))), // INS_vpminuq
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x29))) << 8)))), // INS_vpmovb2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x39))) << 8)))), // INS_vpmovd2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x31))) << 8)))), // INS_vpmovdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x33))) << 8)))), // INS_vpmovdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x28))) << 8)))), // INS_vpmovm2b
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x38))) << 8)))), // INS_vpmovm2d
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x38))) << 8)))), // INS_vpmovm2q
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x28))) << 8)))), // INS_vpmovm2w
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x39))) << 8)))), // INS_vpmovq2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x32))) << 8)))), // INS_vpmovqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x35))) << 8)))), // INS_vpmovqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x34))) << 8)))), // INS_vpmovqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x21))) << 8)))), // INS_vpmovsdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x23))) << 8)))), // INS_vpmovsdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x22))) << 8)))), // INS_vpmovsqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x25))) << 8)))), // INS_vpmovsqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x24))) << 8)))), // INS_vpmovsqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x20))) << 8)))), // INS_vpmovswb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x11))) << 8)))), // INS_vpmovusdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x13))) << 8)))), // INS_vpmovusdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x12))) << 8)))), // INS_vpmovusqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x15))) << 8)))), // INS_vpmovusqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x14))) << 8)))), // INS_vpmovusqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x10))) << 8)))), // INS_vpmovuswb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x29))) << 8)))), // INS_vpmovw2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x30))) << 8)))), // INS_vpmovwb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x40)))) << 8)))), // INS_vpmullq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEB)))))), // INS_vporq
        unchecked((ulong)(BAD_CODE)), // INS_vprold
        unchecked((ulong)(BAD_CODE)), // INS_vprolq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x15)))) << 8)))), // INS_vprolvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x15)))) << 8)))), // INS_vprolvq
        unchecked((ulong)(BAD_CODE)), // INS_vprord
        unchecked((ulong)(BAD_CODE)), // INS_vprorq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x14)))) << 8)))), // INS_vprorvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x14)))) << 8)))), // INS_vprorvq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA0)))) << 8)))), // INS_vpscatterdd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA0)))) << 8)))), // INS_vpscatterdq_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA1)))) << 8)))), // INS_vpscatterqd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA1)))) << 8)))), // INS_vpscatterqq_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x12)))) << 8)))), // INS_vpsllvw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE2)))))), // INS_vpsraq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x46)))) << 8)))), // INS_vpsravq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x11)))) << 8)))), // INS_vpsravw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x10)))) << 8)))), // INS_vpsrlvw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x25)))) << 8)))), // INS_vpternlogd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x25)))) << 8)))), // INS_vpternlogq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x26)))) << 8)))), // INS_vptestmb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x27)))) << 8)))), // INS_vptestmd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x27)))) << 8)))), // INS_vptestmq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x26)))) << 8)))), // INS_vptestmw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x26))) << 8)))), // INS_vptestnmb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x27))) << 8)))), // INS_vptestnmd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x27))) << 8)))), // INS_vptestnmq
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x26))) << 8)))), // INS_vptestnmw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xEF)))))), // INS_vpxorq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x50)))) << 8)))), // INS_vrangepd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x50)))) << 8)))), // INS_vrangeps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x51)))) << 8)))), // INS_vrangesd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x51)))) << 8)))), // INS_vrangess
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4C)))) << 8)))), // INS_vrcp14pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4C)))) << 8)))), // INS_vrcp14ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4D)))) << 8)))), // INS_vrcp14sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4D)))) << 8)))), // INS_vrcp14ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x56)))) << 8)))), // INS_vreducepd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x56)))) << 8)))), // INS_vreduceps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x57)))) << 8)))), // INS_vreducesd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x57)))) << 8)))), // INS_vreducess
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x09)))) << 8)))), // INS_vrndscalepd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x08)))) << 8)))), // INS_vrndscaleps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0B)))) << 8)))), // INS_vrndscalesd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x0A)))) << 8)))), // INS_vrndscaless
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4E)))) << 8)))), // INS_vrsqrt14pd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4E)))) << 8)))), // INS_vrsqrt14ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4F)))) << 8)))), // INS_vrsqrt14sd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x4F)))) << 8)))), // INS_vrsqrt14ss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2C)))) << 8)))), // INS_vscalefpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2C)))) << 8)))), // INS_vscalefps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2D)))) << 8)))), // INS_vscalefsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2D)))) << 8)))), // INS_vscalefss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA2)))) << 8)))), // INS_vscatterdpd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA2)))) << 8)))), // INS_vscatterdps_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA3)))) << 8)))), // INS_vscatterqpd_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0xA3)))) << 8)))), // INS_vscatterqps_msk
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x23)))) << 8)))), // INS_vshuff32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x23)))) << 8)))), // INS_vshuff64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x43)))) << 8)))), // INS_vshufi32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x43)))) << 8)))), // INS_vshufi64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8D)))) << 8)))), // INS_vpermb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x75)))) << 8)))), // INS_vpermi2b
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x7D)))) << 8)))), // INS_vpermt2b
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x83)))) << 8)))), // INS_vpmultishiftqb
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressb
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x62)))) << 8)))), // INS_vpexpandb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x62)))) << 8)))), // INS_vpexpandw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x54)))) << 8)))), // INS_vpopcntb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x55)))) << 8)))), // INS_vpopcntd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x55)))) << 8)))), // INS_vpopcntq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x54)))) << 8)))), // INS_vpopcntw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x71)))) << 8)))), // INS_vpshldd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x71)))) << 8)))), // INS_vpshldq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x71)))) << 8)))), // INS_vpshldvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x71)))) << 8)))), // INS_vpshldvq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x70)))) << 8)))), // INS_vpshldvw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x70)))) << 8)))), // INS_vpshldw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x73)))) << 8)))), // INS_vpshrdd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x73)))) << 8)))), // INS_vpshrdq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x73)))) << 8)))), // INS_vpshrdvd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x73)))) << 8)))), // INS_vpshrdvq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x72)))) << 8)))), // INS_vpshrdvw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x72)))) << 8)))), // INS_vpshrdw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8F)))) << 8)))), // INS_vpshufbitqmb
        unchecked((ulong)((((((0x05))) << 16) | (((0x58)))))), // INS_vaddph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x58)))))), // INS_vaddsh
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0xC2))) << 8)))), // INS_vcmpph
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0xC2))) << 8)))), // INS_vcmpsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x2F)))))), // INS_vcomish
        unchecked((ulong)((((((0x05))) << 16) | (((0x5B)))))), // INS_vcvtdq2ph
        unchecked((ulong)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x72))) << 8)))), // INS_vcvtne2ps2bf16
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x72))) << 8)))), // INS_vcvtneps2bf16
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x5A)))))), // INS_vcvtpd2ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x5B)))))), // INS_vcvtph2dq
        unchecked((ulong)((((((0x05))) << 16) | (((0x5A)))))), // INS_vcvtph2pd
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x13)))))), // INS_vcvtph2psx
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7B)))))), // INS_vcvtph2qq
        unchecked((ulong)((((((0x05))) << 16) | (((0x79)))))), // INS_vcvtph2udq
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x79)))))), // INS_vcvtph2uqq
        unchecked((ulong)((((((0x05))) << 16) | (((0x7D)))))), // INS_vcvtph2uw
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7D)))))), // INS_vcvtph2w
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x1D)))))), // INS_vcvtps2phx
        unchecked((ulong)((((((0x05))) << 16) | (((0x5B)))))), // INS_vcvtqq2ph
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x5A)))))), // INS_vcvtsd2sh
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5A)))))), // INS_vcvtsh2sd
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2D)))))), // INS_vcvtsh2si32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2D)))))), // INS_vcvtsh2si64
        unchecked((ulong)((((((0x06))) << 16) | (((0x13)))))), // INS_vcvtsh2ss
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x79)))))), // INS_vcvtsh2usi32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x79)))))), // INS_vcvtsh2usi64
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2A)))))), // INS_vcvtsi2sh32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2A)))))), // INS_vcvtsi2sh64
        unchecked((ulong)((((((0x05))) << 16) | (((0x1D)))))), // INS_vcvtss2sh
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5B)))))), // INS_vcvttph2dq
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7A)))))), // INS_vcvttph2qq
        unchecked((ulong)((((((0x05))) << 16) | (((0x78)))))), // INS_vcvttph2udq
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x78)))))), // INS_vcvttph2uqq
        unchecked((ulong)((((((0x05))) << 16) | (((0x7C)))))), // INS_vcvttph2uw
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x7C)))))), // INS_vcvttph2w
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2C)))))), // INS_vcvttsh2si32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x2C)))))), // INS_vcvttsh2si64
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x78)))))), // INS_vcvttsh2usi32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x78)))))), // INS_vcvttsh2usi64
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x7A)))))), // INS_vcvtudq2ph
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x7A)))))), // INS_vcvtuqq2ph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7B)))))), // INS_vcvtusi2sh32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7B)))))), // INS_vcvtusi2sh64
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x7D)))))), // INS_vcvtuw2ph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7D)))))), // INS_vcvtw2ph
        unchecked((ulong)((((((0x05))) << 16) | (((0x5E)))))), // INS_vdivph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5E)))))), // INS_vdivsh
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x52))) << 8)))), // INS_vdpbf16ps
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0x56)))))), // INS_vfcmaddcph
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0x57)))))), // INS_vfcmaddcsh
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0xD6)))))), // INS_vfcmulcph
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x06))) << 24) | (((0xD7)))))), // INS_vfcmulcsh
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0xD6)))))), // INS_vfmulcph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0xD7)))))), // INS_vfmulcsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x98)))))), // INS_vfmadd132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA8)))))), // INS_vfmadd213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB8)))))), // INS_vfmadd231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x99)))))), // INS_vfmadd132sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA9)))))), // INS_vfmadd213sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB9)))))), // INS_vfmadd231sh
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0x56)))))), // INS_vfmaddcph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x06))) << 24) | (((0x57)))))), // INS_vfmaddcsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x96)))))), // INS_vfmaddsub132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA6)))))), // INS_vfmaddsub213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB6)))))), // INS_vfmaddsub231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9A)))))), // INS_vfmsub132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAA)))))), // INS_vfmsub213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBA)))))), // INS_vfmsub231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9B)))))), // INS_vfmsub132sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAB)))))), // INS_vfmsub213sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBB)))))), // INS_vfmsub231sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x97)))))), // INS_vfmsubadd132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xA7)))))), // INS_vfmsubadd213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xB7)))))), // INS_vfmsubadd231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9C)))))), // INS_vfnmadd132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAC)))))), // INS_vfnmadd213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBC)))))), // INS_vfnmadd231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9D)))))), // INS_vfnmadd132sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAD)))))), // INS_vfnmadd213sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBD)))))), // INS_vfnmadd231sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9E)))))), // INS_vfnmsub132ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAE)))))), // INS_vfnmsub213ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBE)))))), // INS_vfnmsub231ph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x9F)))))), // INS_vfnmsub132sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xAF)))))), // INS_vfnmsub213sh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0xBF)))))), // INS_vfnmsub231sh
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x66))) << 8)))), // INS_vfpclassph
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x67))) << 8)))), // INS_vfpclasssh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x42)))))), // INS_vgetexpph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x43)))))), // INS_vgetexpsh
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x26))) << 8)))), // INS_vgetmantph
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x27))) << 8)))), // INS_vgetmantsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x5F)))))), // INS_vmaxph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5F)))))), // INS_vmaxsh
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5D)))))), // INS_vminsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x5D)))))), // INS_vminph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x10)))))), // INS_vmovsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x00))) << 24) | (((0x6E)))))), // INS_vmovw
        unchecked((ulong)((((((0x05))) << 16) | (((0x59)))))), // INS_vmulph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x59)))))), // INS_vmulsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4C)))))), // INS_vrcpph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4D)))))), // INS_vrcpsh
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x56))) << 8)))), // INS_vreduceph
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x57))) << 8)))), // INS_vreducesh
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x08))) << 8)))), // INS_vrndscaleph
        unchecked((ulong)((((((0x00))) << 16) | (((0x0f)) << 24) | ((0x3A)) | ((((0x0A))) << 8)))), // INS_vrndscalesh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4E)))))), // INS_vrsqrtph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x4F)))))), // INS_vrsqrtsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x2C)))))), // INS_vscalefph
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x2D)))))), // INS_vscalefsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x51)))))), // INS_vsqrtph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x51)))))), // INS_vsqrtsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x5C)))))), // INS_vsubph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x5C)))))), // INS_vsubsh
        unchecked((ulong)((((((0x05))) << 16) | (((0x2E)))))), // INS_vucomish
        unchecked((ulong)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x68))) << 8)))), // INS_vp2intersectd
        unchecked((ulong)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x68))) << 8)))), // INS_vp2intersectq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2f)))))), // INS_vcomxsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2f)))))), // INS_vcomxss
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x69)))))), // INS_vcvtps2ibs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6B)))))), // INS_vcvtps2iubs
        unchecked((ulong)((((((0x05))) << 16) | (((0x6D)))))), // INS_vcvttpd2dqs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttpd2qqs
        unchecked((ulong)((((((0x05))) << 16) | (((0x6C)))))), // INS_vcvttpd2udqs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttpd2uqqs
        unchecked((ulong)((((((0x05))) << 16) | (((0x6D)))))), // INS_vcvttps2dqs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x68)))))), // INS_vcvttps2ibs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6A)))))), // INS_vcvttps2iubs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttps2qqs
        unchecked((ulong)((((((0x05))) << 16) | (((0x6C)))))), // INS_vcvttps2udqs
        unchecked((ulong)(((((0x66)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttps2uqqs
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttsd2sis32
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttsd2sis64
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttsd2usis32
        unchecked((ulong)(((((0xf2)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttsd2usis64
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttss2sis32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6D)))))), // INS_vcvttss2sis64
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttss2usis32
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6C)))))), // INS_vcvttss2usis64
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x52)))) << 8)))), // INS_vminmaxpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x52)))) << 8)))), // INS_vminmaxps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x53)))) << 8)))), // INS_vminmaxsd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x53)))) << 8)))), // INS_vminmaxss
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_vmovd_simd
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x6E)))))), // INS_vmovw_simd
        unchecked((ulong)((((((0xf3))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x42)))) << 8)))), // INS_vmpsadbw
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x2e)))))), // INS_vucomxsd
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x2e)))))), // INS_vucomxss
        unchecked((ulong)(0x00003A)), // INS_ccmpo
        unchecked((ulong)(0x00003A)), // INS_ccmpno
        unchecked((ulong)(0x00003A)), // INS_ccmpb
        unchecked((ulong)(0x00003A)), // INS_ccmpae
        unchecked((ulong)(0x00003A)), // INS_ccmpe
        unchecked((ulong)(0x00003A)), // INS_ccmpne
        unchecked((ulong)(0x00003A)), // INS_ccmpbe
        unchecked((ulong)(0x00003A)), // INS_ccmpa
        unchecked((ulong)(0x00003A)), // INS_ccmps
        unchecked((ulong)(0x00003A)), // INS_ccmpns
        unchecked((ulong)(0x00003A)), // INS_ccmpt
        unchecked((ulong)(0x00003A)), // INS_ccmpf
        unchecked((ulong)(0x00003A)), // INS_ccmpl
        unchecked((ulong)(0x00003A)), // INS_ccmpge
        unchecked((ulong)(0x00003A)), // INS_ccmple
        unchecked((ulong)(0x00003A)), // INS_ccmpg
        unchecked((ulong)(0x000040)), // INS_cfcmovo
        unchecked((ulong)(0x000041)), // INS_cfcmovno
        unchecked((ulong)(0x000042)), // INS_cfcmovb
        unchecked((ulong)(0x000043)), // INS_cfcmovae
        unchecked((ulong)(0x000044)), // INS_cfcmove
        unchecked((ulong)(0x000045)), // INS_cfcmovne
        unchecked((ulong)(0x000046)), // INS_cfcmovbe
        unchecked((ulong)(0x000047)), // INS_cfcmova
        unchecked((ulong)(0x000048)), // INS_cfcmovs
        unchecked((ulong)(0x000049)), // INS_cfcmovns
        unchecked((ulong)(0x00004A)), // INS_cfcmovp
        unchecked((ulong)(0x00004B)), // INS_cfcmovnp
        unchecked((ulong)(0x00004C)), // INS_cfcmovl
        unchecked((ulong)(0x00004D)), // INS_cfcmovge
        unchecked((ulong)(0x00004E)), // INS_cfcmovle
        unchecked((ulong)(0x00004F)), // INS_cfcmovg
        unchecked((ulong)(BAD_CODE)), // INS_ctesto
        unchecked((ulong)(BAD_CODE)), // INS_ctestno
        unchecked((ulong)(BAD_CODE)), // INS_ctestb
        unchecked((ulong)(BAD_CODE)), // INS_ctestae
        unchecked((ulong)(BAD_CODE)), // INS_cteste
        unchecked((ulong)(BAD_CODE)), // INS_ctestne
        unchecked((ulong)(BAD_CODE)), // INS_ctestbe
        unchecked((ulong)(BAD_CODE)), // INS_ctesta
        unchecked((ulong)(BAD_CODE)), // INS_ctests
        unchecked((ulong)(BAD_CODE)), // INS_ctestns
        unchecked((ulong)(BAD_CODE)), // INS_ctestt
        unchecked((ulong)(BAD_CODE)), // INS_ctestf
        unchecked((ulong)(BAD_CODE)), // INS_ctestl
        unchecked((ulong)(BAD_CODE)), // INS_ctestge
        unchecked((ulong)(BAD_CODE)), // INS_ctestle
        unchecked((ulong)(BAD_CODE)), // INS_ctestg
        unchecked((ulong)(0x0000F0)), // INS_crc32_apx
        unchecked((ulong)(0x000060)), // INS_movbe_apx
        unchecked((ulong)(BAD_CODE)), // INS_seto_apx
        unchecked((ulong)(BAD_CODE)), // INS_setno_apx
        unchecked((ulong)(BAD_CODE)), // INS_setb_apx
        unchecked((ulong)(BAD_CODE)), // INS_setae_apx
        unchecked((ulong)(BAD_CODE)), // INS_sete_apx
        unchecked((ulong)(BAD_CODE)), // INS_setne_apx
        unchecked((ulong)(BAD_CODE)), // INS_setbe_apx
        unchecked((ulong)(BAD_CODE)), // INS_seta_apx
        unchecked((ulong)(BAD_CODE)), // INS_sets_apx
        unchecked((ulong)(BAD_CODE)), // INS_setns_apx
        unchecked((ulong)(BAD_CODE)), // INS_setp_apx
        unchecked((ulong)(BAD_CODE)), // INS_setnp_apx
        unchecked((ulong)(BAD_CODE)), // INS_setl_apx
        unchecked((ulong)(BAD_CODE)), // INS_setge_apx
        unchecked((ulong)(BAD_CODE)), // INS_setle_apx
        unchecked((ulong)(BAD_CODE)), // INS_setg_apx
        unchecked((ulong)((((((0xF2))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0xF0))) << 8)))), // INS_crc32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xBC)))))), // INS_tzcnt
        unchecked((ulong)(0x0000F4)), // INS_tzcnt_apx
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xBD)))))), // INS_lzcnt
        unchecked((ulong)(0x0000F5)), // INS_lzcnt_apx
        unchecked((ulong)(((((0x0F)) << 16) | (((0x38)) << 24) | (((0xF0)))))), // INS_movbe
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xB8)))))), // INS_popcnt
        unchecked((ulong)(0x000088)), // INS_popcnt_apx
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xAE)))))), // INS_tpause
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0xAE)))))), // INS_umonitor
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0xAE)))))), // INS_umwait
        unchecked((ulong)(0x0018F6)), // INS_neg
        unchecked((ulong)(0x0010F6)), // INS_not
        unchecked((ulong)(0x0000D2)), // INS_rol
        unchecked((ulong)(0x0000D0)), // INS_rol_1
        unchecked((ulong)(BAD_CODE)), // INS_rol_N
        unchecked((ulong)(0x0008D2)), // INS_ror
        unchecked((ulong)(0x0008D0)), // INS_ror_1
        unchecked((ulong)(BAD_CODE)), // INS_ror_N
        unchecked((ulong)(0x0010D2)), // INS_rcl
        unchecked((ulong)(0x0010D0)), // INS_rcl_1
        unchecked((ulong)(BAD_CODE)), // INS_rcl_N
        unchecked((ulong)(0x0018D2)), // INS_rcr
        unchecked((ulong)(0x0018D0)), // INS_rcr_1
        unchecked((ulong)(BAD_CODE)), // INS_rcr_N
        unchecked((ulong)(0x0020D2)), // INS_shl
        unchecked((ulong)(0x0020D0)), // INS_shl_1
        unchecked((ulong)(BAD_CODE)), // INS_shl_N
        unchecked((ulong)(0x0028D2)), // INS_shr
        unchecked((ulong)(0x0028D0)), // INS_shr_1
        unchecked((ulong)(BAD_CODE)), // INS_shr_N
        unchecked((ulong)(0x0038D2)), // INS_sar
        unchecked((ulong)(0x0038D0)), // INS_sar_1
        unchecked((ulong)(BAD_CODE)), // INS_sar_N
    ];

    private static ReadOnlySpan<ulong> insCodesRM =>
        Configuration.TARGET_X86 ? insCodesRMX86 :
        Configuration.TARGET_AMD64 ? insCodesRMX64 :
        throw new FatalJitException("xarch opcode table is not available for this target.");

    private static ReadOnlySpan<ulong> insCodesMIX86 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(0x000068)), // INS_push
        unchecked((ulong)(BAD_CODE)), // INS_pop
        unchecked((ulong)(0x000068)), // INS_push_hide
        unchecked((ulong)(BAD_CODE)), // INS_pop_hide
        unchecked((ulong)(BAD_CODE)), // INS_push2
        unchecked((ulong)(BAD_CODE)), // INS_pop2
        unchecked((ulong)(BAD_CODE)), // INS_inc
        unchecked((ulong)(BAD_CODE)), // INS_inc_l
        unchecked((ulong)(BAD_CODE)), // INS_dec
        unchecked((ulong)(BAD_CODE)), // INS_dec_l
        unchecked((ulong)(BAD_CODE)), // INS_bswap
        unchecked((ulong)(0x000080)), // INS_add
        unchecked((ulong)(0x000880)), // INS_or
        unchecked((ulong)(0x001080)), // INS_adc
        unchecked((ulong)(0x001880)), // INS_sbb
        unchecked((ulong)(0x002080)), // INS_and
        unchecked((ulong)(0x002880)), // INS_sub
        unchecked((ulong)(0x002880)), // INS_sub_hide
        unchecked((ulong)(0x003080)), // INS_xor
        unchecked((ulong)(0x003880)), // INS_cmp
        unchecked((ulong)(0x0000F6)), // INS_test
        unchecked((ulong)(0x0000C6)), // INS_mov
        unchecked((ulong)(BAD_CODE)), // INS_lea
        unchecked((ulong)(BAD_CODE)), // INS_bt
        unchecked((ulong)(BAD_CODE)), // INS_bts
        unchecked((ulong)(BAD_CODE)), // INS_btr
        unchecked((ulong)(BAD_CODE)), // INS_btc
        unchecked((ulong)(BAD_CODE)), // INS_bsr
        unchecked((ulong)(BAD_CODE)), // INS_bsf
        unchecked((ulong)(BAD_CODE)), // INS_movsx
        unchecked((ulong)(BAD_CODE)), // INS_movzx
        unchecked((ulong)(BAD_CODE)), // INS_cmovo
        unchecked((ulong)(BAD_CODE)), // INS_cmovno
        unchecked((ulong)(BAD_CODE)), // INS_cmovb
        unchecked((ulong)(BAD_CODE)), // INS_cmovae
        unchecked((ulong)(BAD_CODE)), // INS_cmove
        unchecked((ulong)(BAD_CODE)), // INS_cmovne
        unchecked((ulong)(BAD_CODE)), // INS_cmovbe
        unchecked((ulong)(BAD_CODE)), // INS_cmova
        unchecked((ulong)(BAD_CODE)), // INS_cmovs
        unchecked((ulong)(BAD_CODE)), // INS_cmovns
        unchecked((ulong)(BAD_CODE)), // INS_cmovp
        unchecked((ulong)(BAD_CODE)), // INS_cmovnp
        unchecked((ulong)(BAD_CODE)), // INS_cmovl
        unchecked((ulong)(BAD_CODE)), // INS_cmovge
        unchecked((ulong)(BAD_CODE)), // INS_cmovle
        unchecked((ulong)(BAD_CODE)), // INS_cmovg
        unchecked((ulong)(BAD_CODE)), // INS_xchg
        unchecked((ulong)(BAD_CODE)), // INS_imul
        unchecked((ulong)(0x000068)), // INS_imul_AX
        unchecked((ulong)(0x000868)), // INS_imul_CX
        unchecked((ulong)(0x001068)), // INS_imul_DX
        unchecked((ulong)(0x001868)), // INS_imul_BX
        unchecked((ulong)(BAD_CODE)), // INS_imul_SP
        unchecked((ulong)(0x002868)), // INS_imul_BP
        unchecked((ulong)(0x003068)), // INS_imul_SI
        unchecked((ulong)(0x003868)), // INS_imul_DI
        unchecked((ulong)(BAD_CODE)), // INS_addpd
        unchecked((ulong)(BAD_CODE)), // INS_addps
        unchecked((ulong)(BAD_CODE)), // INS_addsd
        unchecked((ulong)(BAD_CODE)), // INS_addss
        unchecked((ulong)(BAD_CODE)), // INS_addsubpd
        unchecked((ulong)(BAD_CODE)), // INS_addsubps
        unchecked((ulong)(BAD_CODE)), // INS_andnpd
        unchecked((ulong)(BAD_CODE)), // INS_andnps
        unchecked((ulong)(BAD_CODE)), // INS_andpd
        unchecked((ulong)(BAD_CODE)), // INS_andps
        unchecked((ulong)(BAD_CODE)), // INS_blendpd
        unchecked((ulong)(BAD_CODE)), // INS_blendps
        unchecked((ulong)(BAD_CODE)), // INS_blendvpd
        unchecked((ulong)(BAD_CODE)), // INS_blendvps
        unchecked((ulong)(BAD_CODE)), // INS_cmppd
        unchecked((ulong)(BAD_CODE)), // INS_cmpps
        unchecked((ulong)(BAD_CODE)), // INS_cmpsd
        unchecked((ulong)(BAD_CODE)), // INS_cmpss
        unchecked((ulong)(BAD_CODE)), // INS_comisd
        unchecked((ulong)(BAD_CODE)), // INS_comiss
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2ss
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2sd
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si64
        unchecked((ulong)(BAD_CODE)), // INS_divpd
        unchecked((ulong)(BAD_CODE)), // INS_divps
        unchecked((ulong)(BAD_CODE)), // INS_divsd
        unchecked((ulong)(BAD_CODE)), // INS_divss
        unchecked((ulong)(BAD_CODE)), // INS_dppd
        unchecked((ulong)(BAD_CODE)), // INS_dpps
        unchecked((ulong)(BAD_CODE)), // INS_extractps
        unchecked((ulong)(BAD_CODE)), // INS_haddpd
        unchecked((ulong)(BAD_CODE)), // INS_haddps
        unchecked((ulong)(BAD_CODE)), // INS_hsubpd
        unchecked((ulong)(BAD_CODE)), // INS_hsubps
        unchecked((ulong)(BAD_CODE)), // INS_insertps
        unchecked((ulong)(BAD_CODE)), // INS_lddqu
        unchecked((ulong)(BAD_CODE)), // INS_lfence
        unchecked((ulong)(BAD_CODE)), // INS_maskmovdqu
        unchecked((ulong)(BAD_CODE)), // INS_maxpd
        unchecked((ulong)(BAD_CODE)), // INS_maxps
        unchecked((ulong)(BAD_CODE)), // INS_maxsd
        unchecked((ulong)(BAD_CODE)), // INS_maxss
        unchecked((ulong)(BAD_CODE)), // INS_mfence
        unchecked((ulong)(BAD_CODE)), // INS_minpd
        unchecked((ulong)(BAD_CODE)), // INS_minps
        unchecked((ulong)(BAD_CODE)), // INS_minsd
        unchecked((ulong)(BAD_CODE)), // INS_minss
        unchecked((ulong)(BAD_CODE)), // INS_movapd
        unchecked((ulong)(BAD_CODE)), // INS_movaps
        unchecked((ulong)(BAD_CODE)), // INS_movd32
        unchecked((ulong)(BAD_CODE)), // INS_movd64
        unchecked((ulong)(BAD_CODE)), // INS_movddup
        unchecked((ulong)(BAD_CODE)), // INS_movdqa32
        unchecked((ulong)(BAD_CODE)), // INS_movdqu32
        unchecked((ulong)(BAD_CODE)), // INS_movhlps
        unchecked((ulong)(BAD_CODE)), // INS_movhpd
        unchecked((ulong)(BAD_CODE)), // INS_movhps
        unchecked((ulong)(BAD_CODE)), // INS_movlhps
        unchecked((ulong)(BAD_CODE)), // INS_movlpd
        unchecked((ulong)(BAD_CODE)), // INS_movlps
        unchecked((ulong)(BAD_CODE)), // INS_movmskpd
        unchecked((ulong)(BAD_CODE)), // INS_movmskps
        unchecked((ulong)(BAD_CODE)), // INS_movntdq
        unchecked((ulong)(BAD_CODE)), // INS_movntdqa
        unchecked((ulong)(BAD_CODE)), // INS_movnti32
        unchecked((ulong)(BAD_CODE)), // INS_movnti64
        unchecked((ulong)(BAD_CODE)), // INS_movntpd
        unchecked((ulong)(BAD_CODE)), // INS_movntps
        unchecked((ulong)(BAD_CODE)), // INS_movq
        unchecked((ulong)(BAD_CODE)), // INS_movsd_simd
        unchecked((ulong)(BAD_CODE)), // INS_movshdup
        unchecked((ulong)(BAD_CODE)), // INS_movsldup
        unchecked((ulong)(BAD_CODE)), // INS_movss
        unchecked((ulong)(BAD_CODE)), // INS_movupd
        unchecked((ulong)(BAD_CODE)), // INS_movups
        unchecked((ulong)(BAD_CODE)), // INS_mpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_mulpd
        unchecked((ulong)(BAD_CODE)), // INS_mulps
        unchecked((ulong)(BAD_CODE)), // INS_mulsd
        unchecked((ulong)(BAD_CODE)), // INS_mulss
        unchecked((ulong)(BAD_CODE)), // INS_orpd
        unchecked((ulong)(BAD_CODE)), // INS_orps
        unchecked((ulong)(BAD_CODE)), // INS_pabsb
        unchecked((ulong)(BAD_CODE)), // INS_pabsd
        unchecked((ulong)(BAD_CODE)), // INS_pabsw
        unchecked((ulong)(BAD_CODE)), // INS_packssdw
        unchecked((ulong)(BAD_CODE)), // INS_packsswb
        unchecked((ulong)(BAD_CODE)), // INS_packusdw
        unchecked((ulong)(BAD_CODE)), // INS_packuswb
        unchecked((ulong)(BAD_CODE)), // INS_paddb
        unchecked((ulong)(BAD_CODE)), // INS_paddd
        unchecked((ulong)(BAD_CODE)), // INS_paddq
        unchecked((ulong)(BAD_CODE)), // INS_paddsb
        unchecked((ulong)(BAD_CODE)), // INS_paddsw
        unchecked((ulong)(BAD_CODE)), // INS_paddusb
        unchecked((ulong)(BAD_CODE)), // INS_paddusw
        unchecked((ulong)(BAD_CODE)), // INS_paddw
        unchecked((ulong)(BAD_CODE)), // INS_palignr
        unchecked((ulong)(BAD_CODE)), // INS_pandd
        unchecked((ulong)(BAD_CODE)), // INS_pandnd
        unchecked((ulong)(BAD_CODE)), // INS_pavgb
        unchecked((ulong)(BAD_CODE)), // INS_pavgw
        unchecked((ulong)(BAD_CODE)), // INS_pblendvb
        unchecked((ulong)(BAD_CODE)), // INS_pblendw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtw
        unchecked((ulong)(BAD_CODE)), // INS_pextrb
        unchecked((ulong)(BAD_CODE)), // INS_pextrd
        unchecked((ulong)(BAD_CODE)), // INS_pextrq
        unchecked((ulong)(BAD_CODE)), // INS_phaddd
        unchecked((ulong)(BAD_CODE)), // INS_pextrw
        unchecked((ulong)(BAD_CODE)), // INS_phaddsw
        unchecked((ulong)(BAD_CODE)), // INS_phaddw
        unchecked((ulong)(BAD_CODE)), // INS_phminposuw
        unchecked((ulong)(BAD_CODE)), // INS_phsubd
        unchecked((ulong)(BAD_CODE)), // INS_phsubsw
        unchecked((ulong)(BAD_CODE)), // INS_phsubw
        unchecked((ulong)(BAD_CODE)), // INS_pinsrb
        unchecked((ulong)(BAD_CODE)), // INS_pinsrd
        unchecked((ulong)(BAD_CODE)), // INS_pinsrq
        unchecked((ulong)(BAD_CODE)), // INS_pinsrw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddubsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddwd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsb
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaxub
        unchecked((ulong)(BAD_CODE)), // INS_pmaxud
        unchecked((ulong)(BAD_CODE)), // INS_pmaxuw
        unchecked((ulong)(BAD_CODE)), // INS_pminsb
        unchecked((ulong)(BAD_CODE)), // INS_pminsd
        unchecked((ulong)(BAD_CODE)), // INS_pminsw
        unchecked((ulong)(BAD_CODE)), // INS_pminub
        unchecked((ulong)(BAD_CODE)), // INS_pminud
        unchecked((ulong)(BAD_CODE)), // INS_pminuw
        unchecked((ulong)(BAD_CODE)), // INS_pmovmskb
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmuldq
        unchecked((ulong)(BAD_CODE)), // INS_pmulhrsw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhuw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhw
        unchecked((ulong)(BAD_CODE)), // INS_pmulld
        unchecked((ulong)(BAD_CODE)), // INS_pmuludq
        unchecked((ulong)(BAD_CODE)), // INS_pmullw
        unchecked((ulong)(BAD_CODE)), // INS_pord
        unchecked((ulong)(BAD_CODE)), // INS_prefetchnta
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht0
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht1
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht2
        unchecked((ulong)(BAD_CODE)), // INS_psadbw
        unchecked((ulong)(BAD_CODE)), // INS_pshufb
        unchecked((ulong)(BAD_CODE)), // INS_pshufd
        unchecked((ulong)(BAD_CODE)), // INS_pshufhw
        unchecked((ulong)(BAD_CODE)), // INS_pshuflw
        unchecked((ulong)(BAD_CODE)), // INS_psignb
        unchecked((ulong)(BAD_CODE)), // INS_psignd
        unchecked((ulong)(BAD_CODE)), // INS_psignw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_pslld
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_pslldq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_psllq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x71)))))), // INS_psllw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_psrad
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x71)))))), // INS_psraw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_psrld
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_psrldq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_psrlq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x71)))))), // INS_psrlw
        unchecked((ulong)(BAD_CODE)), // INS_psubb
        unchecked((ulong)(BAD_CODE)), // INS_psubd
        unchecked((ulong)(BAD_CODE)), // INS_psubq
        unchecked((ulong)(BAD_CODE)), // INS_psubsb
        unchecked((ulong)(BAD_CODE)), // INS_psubsw
        unchecked((ulong)(BAD_CODE)), // INS_psubusb
        unchecked((ulong)(BAD_CODE)), // INS_psubusw
        unchecked((ulong)(BAD_CODE)), // INS_psubw
        unchecked((ulong)(BAD_CODE)), // INS_ptest
        unchecked((ulong)(BAD_CODE)), // INS_punpckhbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckhdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhwd
        unchecked((ulong)(BAD_CODE)), // INS_punpcklbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckldq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklwd
        unchecked((ulong)(BAD_CODE)), // INS_pxord
        unchecked((ulong)(BAD_CODE)), // INS_rcpps
        unchecked((ulong)(BAD_CODE)), // INS_rcpss
        unchecked((ulong)(BAD_CODE)), // INS_roundpd
        unchecked((ulong)(BAD_CODE)), // INS_roundps
        unchecked((ulong)(BAD_CODE)), // INS_roundsd
        unchecked((ulong)(BAD_CODE)), // INS_roundss
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtps
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtss
        unchecked((ulong)(BAD_CODE)), // INS_sfence
        unchecked((ulong)(BAD_CODE)), // INS_shufpd
        unchecked((ulong)(BAD_CODE)), // INS_shufps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtpd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtsd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtss
        unchecked((ulong)(BAD_CODE)), // INS_subpd
        unchecked((ulong)(BAD_CODE)), // INS_subps
        unchecked((ulong)(BAD_CODE)), // INS_subsd
        unchecked((ulong)(BAD_CODE)), // INS_subss
        unchecked((ulong)(BAD_CODE)), // INS_ucomisd
        unchecked((ulong)(BAD_CODE)), // INS_ucomiss
        unchecked((ulong)(BAD_CODE)), // INS_unpckhpd
        unchecked((ulong)(BAD_CODE)), // INS_unpckhps
        unchecked((ulong)(BAD_CODE)), // INS_unpcklpd
        unchecked((ulong)(BAD_CODE)), // INS_unpcklps
        unchecked((ulong)(BAD_CODE)), // INS_xorpd
        unchecked((ulong)(BAD_CODE)), // INS_xorps
        unchecked((ulong)(BAD_CODE)), // INS_aesdec
        unchecked((ulong)(BAD_CODE)), // INS_aesdeclast
        unchecked((ulong)(BAD_CODE)), // INS_aesenc
        unchecked((ulong)(BAD_CODE)), // INS_aesenclast
        unchecked((ulong)(BAD_CODE)), // INS_aesimc
        unchecked((ulong)(BAD_CODE)), // INS_aeskeygenassist
        unchecked((ulong)(BAD_CODE)), // INS_pclmulqdq
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha1nexte
        unchecked((ulong)(BAD_CODE)), // INS_sha1rnds4
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha256rnds2
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineinvqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8mulb
        unchecked((ulong)(BAD_CODE)), // INS_vblendvpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendvps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastsd
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastss
        unchecked((ulong)(BAD_CODE)), // INS_vextractf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vmaskmovpd
        unchecked((ulong)(BAD_CODE)), // INS_vmaskmovps
        unchecked((ulong)(BAD_CODE)), // INS_vpblendvb
        unchecked((ulong)(BAD_CODE)), // INS_vperm2f128
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpdvar
        unchecked((ulong)(BAD_CODE)), // INS_vpermilps
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpsvar
        unchecked((ulong)(BAD_CODE)), // INS_vtestpd
        unchecked((ulong)(BAD_CODE)), // INS_vtestps
        unchecked((ulong)(BAD_CODE)), // INS_vzeroupper
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2ph
        unchecked((ulong)(BAD_CODE)), // INS_vextracti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vpblendd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw
        unchecked((ulong)(BAD_CODE)), // INS_vperm2i128
        unchecked((ulong)(BAD_CODE)), // INS_vpermd
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermps
        unchecked((ulong)(BAD_CODE)), // INS_vpermq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaskmovd
        unchecked((ulong)(BAD_CODE)), // INS_vpmaskmovq
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvq
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_andn
        unchecked((ulong)(BAD_CODE)), // INS_bextr
        unchecked((ulong)(BAD_CODE)), // INS_blsi
        unchecked((ulong)(BAD_CODE)), // INS_blsmsk
        unchecked((ulong)(BAD_CODE)), // INS_blsr
        unchecked((ulong)(BAD_CODE)), // INS_bzhi
        unchecked((ulong)(BAD_CODE)), // INS_mulx
        unchecked((ulong)(BAD_CODE)), // INS_pdep
        unchecked((ulong)(BAD_CODE)), // INS_pext
        unchecked((ulong)(BAD_CODE)), // INS_rorx
        unchecked((ulong)(BAD_CODE)), // INS_sarx
        unchecked((ulong)(BAD_CODE)), // INS_shlx
        unchecked((ulong)(BAD_CODE)), // INS_shrx
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuuds
        unchecked((ulong)(BAD_CODE)), // INS_vbmacor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbmacxor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbitrev
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52huq
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52luq
        unchecked((ulong)(BAD_CODE)), // INS_kaddb
        unchecked((ulong)(BAD_CODE)), // INS_kaddd
        unchecked((ulong)(BAD_CODE)), // INS_kaddq
        unchecked((ulong)(BAD_CODE)), // INS_kaddw
        unchecked((ulong)(BAD_CODE)), // INS_kandb
        unchecked((ulong)(BAD_CODE)), // INS_kandd
        unchecked((ulong)(BAD_CODE)), // INS_kandnb
        unchecked((ulong)(BAD_CODE)), // INS_kandnd
        unchecked((ulong)(BAD_CODE)), // INS_kandnq
        unchecked((ulong)(BAD_CODE)), // INS_kandnw
        unchecked((ulong)(BAD_CODE)), // INS_kandq
        unchecked((ulong)(BAD_CODE)), // INS_kandw
        unchecked((ulong)(BAD_CODE)), // INS_kmovb_gpr
        unchecked((ulong)(BAD_CODE)), // INS_kmovb_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovd_gpr
        unchecked((ulong)(BAD_CODE)), // INS_kmovd_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovq_gpr
        unchecked((ulong)(BAD_CODE)), // INS_kmovq_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovw_gpr
        unchecked((ulong)(BAD_CODE)), // INS_kmovw_msk
        unchecked((ulong)(BAD_CODE)), // INS_knotb
        unchecked((ulong)(BAD_CODE)), // INS_knotd
        unchecked((ulong)(BAD_CODE)), // INS_knotq
        unchecked((ulong)(BAD_CODE)), // INS_knotw
        unchecked((ulong)(BAD_CODE)), // INS_korb
        unchecked((ulong)(BAD_CODE)), // INS_kord
        unchecked((ulong)(BAD_CODE)), // INS_korq
        unchecked((ulong)(BAD_CODE)), // INS_kortestb
        unchecked((ulong)(BAD_CODE)), // INS_kortestd
        unchecked((ulong)(BAD_CODE)), // INS_kortestq
        unchecked((ulong)(BAD_CODE)), // INS_kortestw
        unchecked((ulong)(BAD_CODE)), // INS_korw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftld
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrd
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrw
        unchecked((ulong)(BAD_CODE)), // INS_ktestb
        unchecked((ulong)(BAD_CODE)), // INS_ktestd
        unchecked((ulong)(BAD_CODE)), // INS_ktestq
        unchecked((ulong)(BAD_CODE)), // INS_ktestw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckbw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckdq
        unchecked((ulong)(BAD_CODE)), // INS_kunpckwd
        unchecked((ulong)(BAD_CODE)), // INS_kxnorb
        unchecked((ulong)(BAD_CODE)), // INS_kxnord
        unchecked((ulong)(BAD_CODE)), // INS_kxnorq
        unchecked((ulong)(BAD_CODE)), // INS_kxnorw
        unchecked((ulong)(BAD_CODE)), // INS_kxorb
        unchecked((ulong)(BAD_CODE)), // INS_kxord
        unchecked((ulong)(BAD_CODE)), // INS_kxorq
        unchecked((ulong)(BAD_CODE)), // INS_kxorw
        unchecked((ulong)(BAD_CODE)), // INS_valignd
        unchecked((ulong)(BAD_CODE)), // INS_valignq
        unchecked((ulong)(BAD_CODE)), // INS_vblendmpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendmps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vcmppd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpps
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpss
        unchecked((ulong)(BAD_CODE)), // INS_vcompresspd
        unchecked((ulong)(BAD_CODE)), // INS_vcompressps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_vdbpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vexpandpd
        unchecked((ulong)(BAD_CODE)), // INS_vexpandps
        unchecked((ulong)(BAD_CODE)), // INS_vextractf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vextractf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vextractf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vextracti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vextracti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vextracti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmpd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmps
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmsd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmss
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasspd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassps
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassss
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgetexppd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpps
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpss
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantpd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantps
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantss
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vmovdqa64
        unchecked((ulong)(BAD_CODE)), // INS_vmovdqu16
        unchecked((ulong)(BAD_CODE)), // INS_vmovdqu64
        unchecked((ulong)(BAD_CODE)), // INS_vmovdqu8
        unchecked((ulong)(BAD_CODE)), // INS_vpabsq
        unchecked((ulong)(BAD_CODE)), // INS_vpandnq
        unchecked((ulong)(BAD_CODE)), // INS_vpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmb
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmd
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmw
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpub
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpud
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpw
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressd
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressq
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictd
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictq
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermq_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandd
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntd
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxsq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxuq
        unchecked((ulong)(BAD_CODE)), // INS_vpminsq
        unchecked((ulong)(BAD_CODE)), // INS_vpminuq
        unchecked((ulong)(BAD_CODE)), // INS_vpmovb2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovd2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovdb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovdw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2d
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2q
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2w
        unchecked((ulong)(BAD_CODE)), // INS_vpmovq2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovqb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovqd
        unchecked((ulong)(BAD_CODE)), // INS_vpmovqw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovsdb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovsdw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovsqb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovsqd
        unchecked((ulong)(BAD_CODE)), // INS_vpmovsqw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovswb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovusdb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovusdw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovusqb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovusqd
        unchecked((ulong)(BAD_CODE)), // INS_vpmovusqw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovuswb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovw2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovwb
        unchecked((ulong)(BAD_CODE)), // INS_vpmullq
        unchecked((ulong)(BAD_CODE)), // INS_vporq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprold
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprolq
        unchecked((ulong)(BAD_CODE)), // INS_vprolvd
        unchecked((ulong)(BAD_CODE)), // INS_vprolvq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprord
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprorq
        unchecked((ulong)(BAD_CODE)), // INS_vprorvd
        unchecked((ulong)(BAD_CODE)), // INS_vprorvq
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vpsraq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravw
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvw
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogd
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmw
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmw
        unchecked((ulong)(BAD_CODE)), // INS_vpxorq
        unchecked((ulong)(BAD_CODE)), // INS_vrangepd
        unchecked((ulong)(BAD_CODE)), // INS_vrangeps
        unchecked((ulong)(BAD_CODE)), // INS_vrangesd
        unchecked((ulong)(BAD_CODE)), // INS_vrangess
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ss
        unchecked((ulong)(BAD_CODE)), // INS_vreducepd
        unchecked((ulong)(BAD_CODE)), // INS_vreduceps
        unchecked((ulong)(BAD_CODE)), // INS_vreducesd
        unchecked((ulong)(BAD_CODE)), // INS_vreducess
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalepd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleps
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaless
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ss
        unchecked((ulong)(BAD_CODE)), // INS_vscalefpd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefps
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefss
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vshuff32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshuff64x2
        unchecked((ulong)(BAD_CODE)), // INS_vshufi32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshufi64x2
        unchecked((ulong)(BAD_CODE)), // INS_vpermb
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2b
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmultishiftqb
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressb
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandb
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandw
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntb
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntd
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdw
        unchecked((ulong)(BAD_CODE)), // INS_vpshufbitqmb
        unchecked((ulong)(BAD_CODE)), // INS_vaddph
        unchecked((ulong)(BAD_CODE)), // INS_vaddsh
        unchecked((ulong)(BAD_CODE)), // INS_vcmpph
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsh
        unchecked((ulong)(BAD_CODE)), // INS_vcomish
        unchecked((ulong)(BAD_CODE)), // INS_vcvtdq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtne2ps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtneps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2psx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2phx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2sd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2ss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vdivph
        unchecked((ulong)(BAD_CODE)), // INS_vdivsh
        unchecked((ulong)(BAD_CODE)), // INS_vdpbf16ps
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassph
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssh
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpph
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsh
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantph
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsh
        unchecked((ulong)(BAD_CODE)), // INS_vmaxph
        unchecked((ulong)(BAD_CODE)), // INS_vmaxsh
        unchecked((ulong)(BAD_CODE)), // INS_vminsh
        unchecked((ulong)(BAD_CODE)), // INS_vminph
        unchecked((ulong)(BAD_CODE)), // INS_vmovsh
        unchecked((ulong)(BAD_CODE)), // INS_vmovw
        unchecked((ulong)(BAD_CODE)), // INS_vmulph
        unchecked((ulong)(BAD_CODE)), // INS_vmulsh
        unchecked((ulong)(BAD_CODE)), // INS_vrcpph
        unchecked((ulong)(BAD_CODE)), // INS_vrcpsh
        unchecked((ulong)(BAD_CODE)), // INS_vreduceph
        unchecked((ulong)(BAD_CODE)), // INS_vreducesh
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleph
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesh
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vscalefph
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsh
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vsubph
        unchecked((ulong)(BAD_CODE)), // INS_vsubsh
        unchecked((ulong)(BAD_CODE)), // INS_vucomish
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectd
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectq
        unchecked((ulong)(BAD_CODE)), // INS_vcomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vcomxss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxpd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxps
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxss
        unchecked((ulong)(BAD_CODE)), // INS_vmovd_simd
        unchecked((ulong)(BAD_CODE)), // INS_vmovw_simd
        unchecked((ulong)(BAD_CODE)), // INS_vmpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vucomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vucomxss
        unchecked((ulong)(BAD_CODE)), // INS_crc32
        unchecked((ulong)(BAD_CODE)), // INS_tzcnt
        unchecked((ulong)(BAD_CODE)), // INS_lzcnt
        unchecked((ulong)(BAD_CODE)), // INS_movbe
        unchecked((ulong)(BAD_CODE)), // INS_popcnt
        unchecked((ulong)(BAD_CODE)), // INS_tpause
        unchecked((ulong)(BAD_CODE)), // INS_umonitor
        unchecked((ulong)(BAD_CODE)), // INS_umwait
        unchecked((ulong)(BAD_CODE)), // INS_neg
        unchecked((ulong)(BAD_CODE)), // INS_not
        unchecked((ulong)(BAD_CODE)), // INS_rol
        unchecked((ulong)(0x0000D0)), // INS_rol_1
        unchecked((ulong)(0x0000C0)), // INS_rol_N
        unchecked((ulong)(BAD_CODE)), // INS_ror
        unchecked((ulong)(0x0008D0)), // INS_ror_1
        unchecked((ulong)(0x0008C0)), // INS_ror_N
        unchecked((ulong)(BAD_CODE)), // INS_rcl
        unchecked((ulong)(0x0010D0)), // INS_rcl_1
        unchecked((ulong)(0x0010C0)), // INS_rcl_N
        unchecked((ulong)(BAD_CODE)), // INS_rcr
        unchecked((ulong)(0x0018D0)), // INS_rcr_1
        unchecked((ulong)(0x0018C0)), // INS_rcr_N
        unchecked((ulong)(BAD_CODE)), // INS_shl
        unchecked((ulong)(0x0020D0)), // INS_shl_1
        unchecked((ulong)(0x0020C0)), // INS_shl_N
        unchecked((ulong)(BAD_CODE)), // INS_shr
        unchecked((ulong)(0x0028D0)), // INS_shr_1
        unchecked((ulong)(0x0028C0)), // INS_shr_N
        unchecked((ulong)(BAD_CODE)), // INS_sar
        unchecked((ulong)(0x0038D0)), // INS_sar_1
        unchecked((ulong)(0x0038C0)), // INS_sar_N
        unchecked((ulong)(0x0000C2)), // INS_ret
        unchecked((ulong)(0x0000E2)), // INS_loop
        unchecked((ulong)(0x0000E8)), // INS_call
    ];

    private static ReadOnlySpan<ulong> insCodesMIX64 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(0x000068)), // INS_push
        unchecked((ulong)(BAD_CODE)), // INS_pop
        unchecked((ulong)(0x000068)), // INS_push_hide
        unchecked((ulong)(BAD_CODE)), // INS_pop_hide
        unchecked((ulong)(BAD_CODE)), // INS_push2
        unchecked((ulong)(BAD_CODE)), // INS_pop2
        unchecked((ulong)(BAD_CODE)), // INS_inc
        unchecked((ulong)(BAD_CODE)), // INS_inc_l
        unchecked((ulong)(BAD_CODE)), // INS_dec
        unchecked((ulong)(BAD_CODE)), // INS_dec_l
        unchecked((ulong)(BAD_CODE)), // INS_bswap
        unchecked((ulong)(0x000080)), // INS_add
        unchecked((ulong)(0x000880)), // INS_or
        unchecked((ulong)(0x001080)), // INS_adc
        unchecked((ulong)(0x001880)), // INS_sbb
        unchecked((ulong)(0x002080)), // INS_and
        unchecked((ulong)(0x002880)), // INS_sub
        unchecked((ulong)(0x002880)), // INS_sub_hide
        unchecked((ulong)(0x003080)), // INS_xor
        unchecked((ulong)(0x003880)), // INS_cmp
        unchecked((ulong)(0x0000F6)), // INS_test
        unchecked((ulong)(0x0000C6)), // INS_mov
        unchecked((ulong)(BAD_CODE)), // INS_lea
        unchecked((ulong)(BAD_CODE)), // INS_bt
        unchecked((ulong)(BAD_CODE)), // INS_bts
        unchecked((ulong)(BAD_CODE)), // INS_btr
        unchecked((ulong)(BAD_CODE)), // INS_btc
        unchecked((ulong)(BAD_CODE)), // INS_bsr
        unchecked((ulong)(BAD_CODE)), // INS_bsf
        unchecked((ulong)(BAD_CODE)), // INS_movsx
        unchecked((ulong)(BAD_CODE)), // INS_movsxd
        unchecked((ulong)(BAD_CODE)), // INS_movzx
        unchecked((ulong)(BAD_CODE)), // INS_cmovo
        unchecked((ulong)(BAD_CODE)), // INS_cmovno
        unchecked((ulong)(BAD_CODE)), // INS_cmovb
        unchecked((ulong)(BAD_CODE)), // INS_cmovae
        unchecked((ulong)(BAD_CODE)), // INS_cmove
        unchecked((ulong)(BAD_CODE)), // INS_cmovne
        unchecked((ulong)(BAD_CODE)), // INS_cmovbe
        unchecked((ulong)(BAD_CODE)), // INS_cmova
        unchecked((ulong)(BAD_CODE)), // INS_cmovs
        unchecked((ulong)(BAD_CODE)), // INS_cmovns
        unchecked((ulong)(BAD_CODE)), // INS_cmovp
        unchecked((ulong)(BAD_CODE)), // INS_cmovnp
        unchecked((ulong)(BAD_CODE)), // INS_cmovl
        unchecked((ulong)(BAD_CODE)), // INS_cmovge
        unchecked((ulong)(BAD_CODE)), // INS_cmovle
        unchecked((ulong)(BAD_CODE)), // INS_cmovg
        unchecked((ulong)(BAD_CODE)), // INS_xchg
        unchecked((ulong)(BAD_CODE)), // INS_imul
        unchecked((ulong)(0x000068)), // INS_imul_AX
        unchecked((ulong)(0x000868)), // INS_imul_CX
        unchecked((ulong)(0x001068)), // INS_imul_DX
        unchecked((ulong)(0x001868)), // INS_imul_BX
        unchecked((ulong)(BAD_CODE)), // INS_imul_SP
        unchecked((ulong)(0x002868)), // INS_imul_BP
        unchecked((ulong)(0x003068)), // INS_imul_SI
        unchecked((ulong)(0x003868)), // INS_imul_DI
        unchecked((ulong)(0x4400000068)), // INS_imul_08
        unchecked((ulong)(0x4400000868)), // INS_imul_09
        unchecked((ulong)(0x4400001068)), // INS_imul_10
        unchecked((ulong)(0x4400001868)), // INS_imul_11
        unchecked((ulong)(0x4400002068)), // INS_imul_12
        unchecked((ulong)(0x4400002868)), // INS_imul_13
        unchecked((ulong)(0x4400003068)), // INS_imul_14
        unchecked((ulong)(0x4400003868)), // INS_imul_15
        unchecked((ulong)(0xD54000000068)), // INS_imul_16
        unchecked((ulong)(0xD54000000868)), // INS_imul_17
        unchecked((ulong)(0xD54000001068)), // INS_imul_18
        unchecked((ulong)(0xD54000001868)), // INS_imul_19
        unchecked((ulong)(0xD54000002068)), // INS_imul_20
        unchecked((ulong)(0xD54000002868)), // INS_imul_21
        unchecked((ulong)(0xD54000003068)), // INS_imul_22
        unchecked((ulong)(0xD54000003868)), // INS_imul_23
        unchecked((ulong)(0xD54400000068)), // INS_imul_24
        unchecked((ulong)(0xD54400000868)), // INS_imul_25
        unchecked((ulong)(0xD54400001068)), // INS_imul_26
        unchecked((ulong)(0xD54400001868)), // INS_imul_27
        unchecked((ulong)(0xD54400002068)), // INS_imul_28
        unchecked((ulong)(0xD54400002868)), // INS_imul_29
        unchecked((ulong)(0xD54400003068)), // INS_imul_30
        unchecked((ulong)(0xD54400003868)), // INS_imul_31
        unchecked((ulong)(BAD_CODE)), // INS_addpd
        unchecked((ulong)(BAD_CODE)), // INS_addps
        unchecked((ulong)(BAD_CODE)), // INS_addsd
        unchecked((ulong)(BAD_CODE)), // INS_addss
        unchecked((ulong)(BAD_CODE)), // INS_addsubpd
        unchecked((ulong)(BAD_CODE)), // INS_addsubps
        unchecked((ulong)(BAD_CODE)), // INS_andnpd
        unchecked((ulong)(BAD_CODE)), // INS_andnps
        unchecked((ulong)(BAD_CODE)), // INS_andpd
        unchecked((ulong)(BAD_CODE)), // INS_andps
        unchecked((ulong)(BAD_CODE)), // INS_blendpd
        unchecked((ulong)(BAD_CODE)), // INS_blendps
        unchecked((ulong)(BAD_CODE)), // INS_blendvpd
        unchecked((ulong)(BAD_CODE)), // INS_blendvps
        unchecked((ulong)(BAD_CODE)), // INS_cmppd
        unchecked((ulong)(BAD_CODE)), // INS_cmpps
        unchecked((ulong)(BAD_CODE)), // INS_cmpsd
        unchecked((ulong)(BAD_CODE)), // INS_cmpss
        unchecked((ulong)(BAD_CODE)), // INS_comisd
        unchecked((ulong)(BAD_CODE)), // INS_comiss
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2ss
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2sd
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si64
        unchecked((ulong)(BAD_CODE)), // INS_divpd
        unchecked((ulong)(BAD_CODE)), // INS_divps
        unchecked((ulong)(BAD_CODE)), // INS_divsd
        unchecked((ulong)(BAD_CODE)), // INS_divss
        unchecked((ulong)(BAD_CODE)), // INS_dppd
        unchecked((ulong)(BAD_CODE)), // INS_dpps
        unchecked((ulong)(BAD_CODE)), // INS_extractps
        unchecked((ulong)(BAD_CODE)), // INS_haddpd
        unchecked((ulong)(BAD_CODE)), // INS_haddps
        unchecked((ulong)(BAD_CODE)), // INS_hsubpd
        unchecked((ulong)(BAD_CODE)), // INS_hsubps
        unchecked((ulong)(BAD_CODE)), // INS_insertps
        unchecked((ulong)(BAD_CODE)), // INS_lddqu
        unchecked((ulong)(BAD_CODE)), // INS_lfence
        unchecked((ulong)(BAD_CODE)), // INS_maskmovdqu
        unchecked((ulong)(BAD_CODE)), // INS_maxpd
        unchecked((ulong)(BAD_CODE)), // INS_maxps
        unchecked((ulong)(BAD_CODE)), // INS_maxsd
        unchecked((ulong)(BAD_CODE)), // INS_maxss
        unchecked((ulong)(BAD_CODE)), // INS_mfence
        unchecked((ulong)(BAD_CODE)), // INS_minpd
        unchecked((ulong)(BAD_CODE)), // INS_minps
        unchecked((ulong)(BAD_CODE)), // INS_minsd
        unchecked((ulong)(BAD_CODE)), // INS_minss
        unchecked((ulong)(BAD_CODE)), // INS_movapd
        unchecked((ulong)(BAD_CODE)), // INS_movaps
        unchecked((ulong)(BAD_CODE)), // INS_movd32
        unchecked((ulong)(BAD_CODE)), // INS_movd64
        unchecked((ulong)(BAD_CODE)), // INS_movddup
        unchecked((ulong)(BAD_CODE)), // INS_movdqa32
        unchecked((ulong)(BAD_CODE)), // INS_movdqu32
        unchecked((ulong)(BAD_CODE)), // INS_movhlps
        unchecked((ulong)(BAD_CODE)), // INS_movhpd
        unchecked((ulong)(BAD_CODE)), // INS_movhps
        unchecked((ulong)(BAD_CODE)), // INS_movlhps
        unchecked((ulong)(BAD_CODE)), // INS_movlpd
        unchecked((ulong)(BAD_CODE)), // INS_movlps
        unchecked((ulong)(BAD_CODE)), // INS_movmskpd
        unchecked((ulong)(BAD_CODE)), // INS_movmskps
        unchecked((ulong)(BAD_CODE)), // INS_movntdq
        unchecked((ulong)(BAD_CODE)), // INS_movntdqa
        unchecked((ulong)(BAD_CODE)), // INS_movnti32
        unchecked((ulong)(BAD_CODE)), // INS_movnti64
        unchecked((ulong)(BAD_CODE)), // INS_movntpd
        unchecked((ulong)(BAD_CODE)), // INS_movntps
        unchecked((ulong)(BAD_CODE)), // INS_movq
        unchecked((ulong)(BAD_CODE)), // INS_movsd_simd
        unchecked((ulong)(BAD_CODE)), // INS_movshdup
        unchecked((ulong)(BAD_CODE)), // INS_movsldup
        unchecked((ulong)(BAD_CODE)), // INS_movss
        unchecked((ulong)(BAD_CODE)), // INS_movupd
        unchecked((ulong)(BAD_CODE)), // INS_movups
        unchecked((ulong)(BAD_CODE)), // INS_mpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_mulpd
        unchecked((ulong)(BAD_CODE)), // INS_mulps
        unchecked((ulong)(BAD_CODE)), // INS_mulsd
        unchecked((ulong)(BAD_CODE)), // INS_mulss
        unchecked((ulong)(BAD_CODE)), // INS_orpd
        unchecked((ulong)(BAD_CODE)), // INS_orps
        unchecked((ulong)(BAD_CODE)), // INS_pabsb
        unchecked((ulong)(BAD_CODE)), // INS_pabsd
        unchecked((ulong)(BAD_CODE)), // INS_pabsw
        unchecked((ulong)(BAD_CODE)), // INS_packssdw
        unchecked((ulong)(BAD_CODE)), // INS_packsswb
        unchecked((ulong)(BAD_CODE)), // INS_packusdw
        unchecked((ulong)(BAD_CODE)), // INS_packuswb
        unchecked((ulong)(BAD_CODE)), // INS_paddb
        unchecked((ulong)(BAD_CODE)), // INS_paddd
        unchecked((ulong)(BAD_CODE)), // INS_paddq
        unchecked((ulong)(BAD_CODE)), // INS_paddsb
        unchecked((ulong)(BAD_CODE)), // INS_paddsw
        unchecked((ulong)(BAD_CODE)), // INS_paddusb
        unchecked((ulong)(BAD_CODE)), // INS_paddusw
        unchecked((ulong)(BAD_CODE)), // INS_paddw
        unchecked((ulong)(BAD_CODE)), // INS_palignr
        unchecked((ulong)(BAD_CODE)), // INS_pandd
        unchecked((ulong)(BAD_CODE)), // INS_pandnd
        unchecked((ulong)(BAD_CODE)), // INS_pavgb
        unchecked((ulong)(BAD_CODE)), // INS_pavgw
        unchecked((ulong)(BAD_CODE)), // INS_pblendvb
        unchecked((ulong)(BAD_CODE)), // INS_pblendw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtw
        unchecked((ulong)(BAD_CODE)), // INS_pextrb
        unchecked((ulong)(BAD_CODE)), // INS_pextrd
        unchecked((ulong)(BAD_CODE)), // INS_pextrq
        unchecked((ulong)(BAD_CODE)), // INS_phaddd
        unchecked((ulong)(BAD_CODE)), // INS_pextrw
        unchecked((ulong)(BAD_CODE)), // INS_phaddsw
        unchecked((ulong)(BAD_CODE)), // INS_phaddw
        unchecked((ulong)(BAD_CODE)), // INS_phminposuw
        unchecked((ulong)(BAD_CODE)), // INS_phsubd
        unchecked((ulong)(BAD_CODE)), // INS_phsubsw
        unchecked((ulong)(BAD_CODE)), // INS_phsubw
        unchecked((ulong)(BAD_CODE)), // INS_pinsrb
        unchecked((ulong)(BAD_CODE)), // INS_pinsrd
        unchecked((ulong)(BAD_CODE)), // INS_pinsrq
        unchecked((ulong)(BAD_CODE)), // INS_pinsrw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddubsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddwd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsb
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaxub
        unchecked((ulong)(BAD_CODE)), // INS_pmaxud
        unchecked((ulong)(BAD_CODE)), // INS_pmaxuw
        unchecked((ulong)(BAD_CODE)), // INS_pminsb
        unchecked((ulong)(BAD_CODE)), // INS_pminsd
        unchecked((ulong)(BAD_CODE)), // INS_pminsw
        unchecked((ulong)(BAD_CODE)), // INS_pminub
        unchecked((ulong)(BAD_CODE)), // INS_pminud
        unchecked((ulong)(BAD_CODE)), // INS_pminuw
        unchecked((ulong)(BAD_CODE)), // INS_pmovmskb
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmuldq
        unchecked((ulong)(BAD_CODE)), // INS_pmulhrsw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhuw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhw
        unchecked((ulong)(BAD_CODE)), // INS_pmulld
        unchecked((ulong)(BAD_CODE)), // INS_pmuludq
        unchecked((ulong)(BAD_CODE)), // INS_pmullw
        unchecked((ulong)(BAD_CODE)), // INS_pord
        unchecked((ulong)(BAD_CODE)), // INS_prefetchnta
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht0
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht1
        unchecked((ulong)(BAD_CODE)), // INS_prefetcht2
        unchecked((ulong)(BAD_CODE)), // INS_psadbw
        unchecked((ulong)(BAD_CODE)), // INS_pshufb
        unchecked((ulong)(BAD_CODE)), // INS_pshufd
        unchecked((ulong)(BAD_CODE)), // INS_pshufhw
        unchecked((ulong)(BAD_CODE)), // INS_pshuflw
        unchecked((ulong)(BAD_CODE)), // INS_psignb
        unchecked((ulong)(BAD_CODE)), // INS_psignd
        unchecked((ulong)(BAD_CODE)), // INS_psignw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_pslld
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_pslldq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_psllq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x71)))))), // INS_psllw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_psrad
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x71)))))), // INS_psraw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_psrld
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_psrldq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x73)))))), // INS_psrlq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x71)))))), // INS_psrlw
        unchecked((ulong)(BAD_CODE)), // INS_psubb
        unchecked((ulong)(BAD_CODE)), // INS_psubd
        unchecked((ulong)(BAD_CODE)), // INS_psubq
        unchecked((ulong)(BAD_CODE)), // INS_psubsb
        unchecked((ulong)(BAD_CODE)), // INS_psubsw
        unchecked((ulong)(BAD_CODE)), // INS_psubusb
        unchecked((ulong)(BAD_CODE)), // INS_psubusw
        unchecked((ulong)(BAD_CODE)), // INS_psubw
        unchecked((ulong)(BAD_CODE)), // INS_ptest
        unchecked((ulong)(BAD_CODE)), // INS_punpckhbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckhdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhwd
        unchecked((ulong)(BAD_CODE)), // INS_punpcklbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckldq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklwd
        unchecked((ulong)(BAD_CODE)), // INS_pxord
        unchecked((ulong)(BAD_CODE)), // INS_rcpps
        unchecked((ulong)(BAD_CODE)), // INS_rcpss
        unchecked((ulong)(BAD_CODE)), // INS_roundpd
        unchecked((ulong)(BAD_CODE)), // INS_roundps
        unchecked((ulong)(BAD_CODE)), // INS_roundsd
        unchecked((ulong)(BAD_CODE)), // INS_roundss
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtps
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtss
        unchecked((ulong)(BAD_CODE)), // INS_sfence
        unchecked((ulong)(BAD_CODE)), // INS_shufpd
        unchecked((ulong)(BAD_CODE)), // INS_shufps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtpd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtsd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtss
        unchecked((ulong)(BAD_CODE)), // INS_subpd
        unchecked((ulong)(BAD_CODE)), // INS_subps
        unchecked((ulong)(BAD_CODE)), // INS_subsd
        unchecked((ulong)(BAD_CODE)), // INS_subss
        unchecked((ulong)(BAD_CODE)), // INS_ucomisd
        unchecked((ulong)(BAD_CODE)), // INS_ucomiss
        unchecked((ulong)(BAD_CODE)), // INS_unpckhpd
        unchecked((ulong)(BAD_CODE)), // INS_unpckhps
        unchecked((ulong)(BAD_CODE)), // INS_unpcklpd
        unchecked((ulong)(BAD_CODE)), // INS_unpcklps
        unchecked((ulong)(BAD_CODE)), // INS_xorpd
        unchecked((ulong)(BAD_CODE)), // INS_xorps
        unchecked((ulong)(BAD_CODE)), // INS_aesdec
        unchecked((ulong)(BAD_CODE)), // INS_aesdeclast
        unchecked((ulong)(BAD_CODE)), // INS_aesenc
        unchecked((ulong)(BAD_CODE)), // INS_aesenclast
        unchecked((ulong)(BAD_CODE)), // INS_aesimc
        unchecked((ulong)(BAD_CODE)), // INS_aeskeygenassist
        unchecked((ulong)(BAD_CODE)), // INS_pclmulqdq
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha1nexte
        unchecked((ulong)(BAD_CODE)), // INS_sha1rnds4
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha256rnds2
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineinvqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8mulb
        unchecked((ulong)(BAD_CODE)), // INS_vblendvpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendvps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastsd
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastss
        unchecked((ulong)(BAD_CODE)), // INS_vextractf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vmaskmovpd
        unchecked((ulong)(BAD_CODE)), // INS_vmaskmovps
        unchecked((ulong)(BAD_CODE)), // INS_vpblendvb
        unchecked((ulong)(BAD_CODE)), // INS_vperm2f128
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpdvar
        unchecked((ulong)(BAD_CODE)), // INS_vpermilps
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpsvar
        unchecked((ulong)(BAD_CODE)), // INS_vtestpd
        unchecked((ulong)(BAD_CODE)), // INS_vtestps
        unchecked((ulong)(BAD_CODE)), // INS_vzeroupper
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2ph
        unchecked((ulong)(BAD_CODE)), // INS_vextracti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vpblendd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw
        unchecked((ulong)(BAD_CODE)), // INS_vperm2i128
        unchecked((ulong)(BAD_CODE)), // INS_vpermd
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermps
        unchecked((ulong)(BAD_CODE)), // INS_vpermq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaskmovd
        unchecked((ulong)(BAD_CODE)), // INS_vpmaskmovq
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvq
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_andn
        unchecked((ulong)(BAD_CODE)), // INS_bextr
        unchecked((ulong)(BAD_CODE)), // INS_blsi
        unchecked((ulong)(BAD_CODE)), // INS_blsmsk
        unchecked((ulong)(BAD_CODE)), // INS_blsr
        unchecked((ulong)(BAD_CODE)), // INS_bzhi
        unchecked((ulong)(BAD_CODE)), // INS_mulx
        unchecked((ulong)(BAD_CODE)), // INS_pdep
        unchecked((ulong)(BAD_CODE)), // INS_pext
        unchecked((ulong)(BAD_CODE)), // INS_rorx
        unchecked((ulong)(BAD_CODE)), // INS_sarx
        unchecked((ulong)(BAD_CODE)), // INS_shlx
        unchecked((ulong)(BAD_CODE)), // INS_shrx
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuuds
        unchecked((ulong)(BAD_CODE)), // INS_vbmacor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbmacxor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbitrev
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52huq
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52luq
        unchecked((ulong)(BAD_CODE)), // INS_kaddb
        unchecked((ulong)(BAD_CODE)), // INS_kaddd
        unchecked((ulong)(BAD_CODE)), // INS_kaddq
        unchecked((ulong)(BAD_CODE)), // INS_kaddw
        unchecked((ulong)(BAD_CODE)), // INS_kandb
        unchecked((ulong)(BAD_CODE)), // INS_kandd
        unchecked((ulong)(BAD_CODE)), // INS_kandnb
        unchecked((ulong)(BAD_CODE)), // INS_kandnd
        unchecked((ulong)(BAD_CODE)), // INS_kandnq
        unchecked((ulong)(BAD_CODE)), // INS_kandnw
        unchecked((ulong)(BAD_CODE)), // INS_kandq
        unchecked((ulong)(BAD_CODE)), // INS_kandw
        unchecked((ulong)(BAD_CODE)), // INS_kmovb_gpr
        unchecked((ulong)(BAD_CODE)), // INS_kmovb_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovd_gpr
        unchecked((ulong)(BAD_CODE)), // INS_kmovd_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovq_gpr
        unchecked((ulong)(BAD_CODE)), // INS_kmovq_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovw_gpr
        unchecked((ulong)(BAD_CODE)), // INS_kmovw_msk
        unchecked((ulong)(BAD_CODE)), // INS_knotb
        unchecked((ulong)(BAD_CODE)), // INS_knotd
        unchecked((ulong)(BAD_CODE)), // INS_knotq
        unchecked((ulong)(BAD_CODE)), // INS_knotw
        unchecked((ulong)(BAD_CODE)), // INS_korb
        unchecked((ulong)(BAD_CODE)), // INS_kord
        unchecked((ulong)(BAD_CODE)), // INS_korq
        unchecked((ulong)(BAD_CODE)), // INS_kortestb
        unchecked((ulong)(BAD_CODE)), // INS_kortestd
        unchecked((ulong)(BAD_CODE)), // INS_kortestq
        unchecked((ulong)(BAD_CODE)), // INS_kortestw
        unchecked((ulong)(BAD_CODE)), // INS_korw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftld
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrd
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrw
        unchecked((ulong)(BAD_CODE)), // INS_ktestb
        unchecked((ulong)(BAD_CODE)), // INS_ktestd
        unchecked((ulong)(BAD_CODE)), // INS_ktestq
        unchecked((ulong)(BAD_CODE)), // INS_ktestw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckbw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckdq
        unchecked((ulong)(BAD_CODE)), // INS_kunpckwd
        unchecked((ulong)(BAD_CODE)), // INS_kxnorb
        unchecked((ulong)(BAD_CODE)), // INS_kxnord
        unchecked((ulong)(BAD_CODE)), // INS_kxnorq
        unchecked((ulong)(BAD_CODE)), // INS_kxnorw
        unchecked((ulong)(BAD_CODE)), // INS_kxorb
        unchecked((ulong)(BAD_CODE)), // INS_kxord
        unchecked((ulong)(BAD_CODE)), // INS_kxorq
        unchecked((ulong)(BAD_CODE)), // INS_kxorw
        unchecked((ulong)(BAD_CODE)), // INS_valignd
        unchecked((ulong)(BAD_CODE)), // INS_valignq
        unchecked((ulong)(BAD_CODE)), // INS_vblendmpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendmps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vcmppd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpps
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpss
        unchecked((ulong)(BAD_CODE)), // INS_vcompresspd
        unchecked((ulong)(BAD_CODE)), // INS_vcompressps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_vdbpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vexpandpd
        unchecked((ulong)(BAD_CODE)), // INS_vexpandps
        unchecked((ulong)(BAD_CODE)), // INS_vextractf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vextractf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vextractf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vextracti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vextracti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vextracti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmpd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmps
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmsd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmss
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasspd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassps
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassss
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgetexppd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpps
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpss
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantpd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantps
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantss
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vmovdqa64
        unchecked((ulong)(BAD_CODE)), // INS_vmovdqu16
        unchecked((ulong)(BAD_CODE)), // INS_vmovdqu64
        unchecked((ulong)(BAD_CODE)), // INS_vmovdqu8
        unchecked((ulong)(BAD_CODE)), // INS_vpabsq
        unchecked((ulong)(BAD_CODE)), // INS_vpandnq
        unchecked((ulong)(BAD_CODE)), // INS_vpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmb
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmd
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmw
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpub
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpud
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpw
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressd
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressq
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictd
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictq
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermq_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandd
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntd
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxsq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxuq
        unchecked((ulong)(BAD_CODE)), // INS_vpminsq
        unchecked((ulong)(BAD_CODE)), // INS_vpminuq
        unchecked((ulong)(BAD_CODE)), // INS_vpmovb2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovd2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovdb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovdw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2d
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2q
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2w
        unchecked((ulong)(BAD_CODE)), // INS_vpmovq2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovqb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovqd
        unchecked((ulong)(BAD_CODE)), // INS_vpmovqw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovsdb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovsdw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovsqb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovsqd
        unchecked((ulong)(BAD_CODE)), // INS_vpmovsqw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovswb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovusdb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovusdw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovusqb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovusqd
        unchecked((ulong)(BAD_CODE)), // INS_vpmovusqw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovuswb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovw2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovwb
        unchecked((ulong)(BAD_CODE)), // INS_vpmullq
        unchecked((ulong)(BAD_CODE)), // INS_vporq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprold
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprolq
        unchecked((ulong)(BAD_CODE)), // INS_vprolvd
        unchecked((ulong)(BAD_CODE)), // INS_vprolvq
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprord
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vprorq
        unchecked((ulong)(BAD_CODE)), // INS_vprorvd
        unchecked((ulong)(BAD_CODE)), // INS_vprorvq
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvw
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x72)))))), // INS_vpsraq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravw
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvw
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogd
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmw
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmw
        unchecked((ulong)(BAD_CODE)), // INS_vpxorq
        unchecked((ulong)(BAD_CODE)), // INS_vrangepd
        unchecked((ulong)(BAD_CODE)), // INS_vrangeps
        unchecked((ulong)(BAD_CODE)), // INS_vrangesd
        unchecked((ulong)(BAD_CODE)), // INS_vrangess
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ss
        unchecked((ulong)(BAD_CODE)), // INS_vreducepd
        unchecked((ulong)(BAD_CODE)), // INS_vreduceps
        unchecked((ulong)(BAD_CODE)), // INS_vreducesd
        unchecked((ulong)(BAD_CODE)), // INS_vreducess
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalepd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleps
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaless
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ss
        unchecked((ulong)(BAD_CODE)), // INS_vscalefpd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefps
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefss
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vshuff32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshuff64x2
        unchecked((ulong)(BAD_CODE)), // INS_vshufi32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshufi64x2
        unchecked((ulong)(BAD_CODE)), // INS_vpermb
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2b
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmultishiftqb
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressb
        unchecked((ulong)(BAD_CODE)), // INS_vpcompressw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandb
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandw
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntb
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntd
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdw
        unchecked((ulong)(BAD_CODE)), // INS_vpshufbitqmb
        unchecked((ulong)(BAD_CODE)), // INS_vaddph
        unchecked((ulong)(BAD_CODE)), // INS_vaddsh
        unchecked((ulong)(BAD_CODE)), // INS_vcmpph
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsh
        unchecked((ulong)(BAD_CODE)), // INS_vcomish
        unchecked((ulong)(BAD_CODE)), // INS_vcvtdq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtne2ps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtneps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2psx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2phx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2sd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2ss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vdivph
        unchecked((ulong)(BAD_CODE)), // INS_vdivsh
        unchecked((ulong)(BAD_CODE)), // INS_vdpbf16ps
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassph
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssh
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpph
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsh
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantph
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsh
        unchecked((ulong)(BAD_CODE)), // INS_vmaxph
        unchecked((ulong)(BAD_CODE)), // INS_vmaxsh
        unchecked((ulong)(BAD_CODE)), // INS_vminsh
        unchecked((ulong)(BAD_CODE)), // INS_vminph
        unchecked((ulong)(BAD_CODE)), // INS_vmovsh
        unchecked((ulong)(BAD_CODE)), // INS_vmovw
        unchecked((ulong)(BAD_CODE)), // INS_vmulph
        unchecked((ulong)(BAD_CODE)), // INS_vmulsh
        unchecked((ulong)(BAD_CODE)), // INS_vrcpph
        unchecked((ulong)(BAD_CODE)), // INS_vrcpsh
        unchecked((ulong)(BAD_CODE)), // INS_vreduceph
        unchecked((ulong)(BAD_CODE)), // INS_vreducesh
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleph
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesh
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vscalefph
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsh
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vsubph
        unchecked((ulong)(BAD_CODE)), // INS_vsubsh
        unchecked((ulong)(BAD_CODE)), // INS_vucomish
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectd
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectq
        unchecked((ulong)(BAD_CODE)), // INS_vcomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vcomxss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxpd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxps
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxss
        unchecked((ulong)(BAD_CODE)), // INS_vmovd_simd
        unchecked((ulong)(BAD_CODE)), // INS_vmovw_simd
        unchecked((ulong)(BAD_CODE)), // INS_vmpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vucomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vucomxss
        unchecked((ulong)(0x0003880)), // INS_ccmpo
        unchecked((ulong)(0x0003880)), // INS_ccmpno
        unchecked((ulong)(0x0003880)), // INS_ccmpb
        unchecked((ulong)(0x0003880)), // INS_ccmpae
        unchecked((ulong)(0x0003880)), // INS_ccmpe
        unchecked((ulong)(0x0003880)), // INS_ccmpne
        unchecked((ulong)(0x0003880)), // INS_ccmpbe
        unchecked((ulong)(0x0003880)), // INS_ccmpa
        unchecked((ulong)(0x0003880)), // INS_ccmps
        unchecked((ulong)(0x0003880)), // INS_ccmpns
        unchecked((ulong)(0x0003880)), // INS_ccmpt
        unchecked((ulong)(0x0003880)), // INS_ccmpf
        unchecked((ulong)(0x0003880)), // INS_ccmpl
        unchecked((ulong)(0x0003880)), // INS_ccmpge
        unchecked((ulong)(0x0003880)), // INS_ccmple
        unchecked((ulong)(0x0003880)), // INS_ccmpg
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovo
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovno
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovb
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovae
        unchecked((ulong)(BAD_CODE)), // INS_cfcmove
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovne
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovbe
        unchecked((ulong)(BAD_CODE)), // INS_cfcmova
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovs
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovns
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovp
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovnp
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovl
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovge
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovle
        unchecked((ulong)(BAD_CODE)), // INS_cfcmovg
        unchecked((ulong)(0x00000F6)), // INS_ctesto
        unchecked((ulong)(0x00000F6)), // INS_ctestno
        unchecked((ulong)(0x00000F6)), // INS_ctestb
        unchecked((ulong)(0x00000F6)), // INS_ctestae
        unchecked((ulong)(0x00000F6)), // INS_cteste
        unchecked((ulong)(0x00000F6)), // INS_ctestne
        unchecked((ulong)(0x00000F6)), // INS_ctestbe
        unchecked((ulong)(0x00000F6)), // INS_ctesta
        unchecked((ulong)(0x00000F6)), // INS_ctests
        unchecked((ulong)(0x00000F6)), // INS_ctestns
        unchecked((ulong)(0x00000F6)), // INS_ctestt
        unchecked((ulong)(0x00000F6)), // INS_ctestf
        unchecked((ulong)(0x00000F6)), // INS_ctestl
        unchecked((ulong)(0x00000F6)), // INS_ctestge
        unchecked((ulong)(0x00000F6)), // INS_ctestle
        unchecked((ulong)(0x00000F6)), // INS_ctestg
        unchecked((ulong)(BAD_CODE)), // INS_crc32_apx
        unchecked((ulong)(BAD_CODE)), // INS_movbe_apx
        unchecked((ulong)(BAD_CODE)), // INS_seto_apx
        unchecked((ulong)(BAD_CODE)), // INS_setno_apx
        unchecked((ulong)(BAD_CODE)), // INS_setb_apx
        unchecked((ulong)(BAD_CODE)), // INS_setae_apx
        unchecked((ulong)(BAD_CODE)), // INS_sete_apx
        unchecked((ulong)(BAD_CODE)), // INS_setne_apx
        unchecked((ulong)(BAD_CODE)), // INS_setbe_apx
        unchecked((ulong)(BAD_CODE)), // INS_seta_apx
        unchecked((ulong)(BAD_CODE)), // INS_sets_apx
        unchecked((ulong)(BAD_CODE)), // INS_setns_apx
        unchecked((ulong)(BAD_CODE)), // INS_setp_apx
        unchecked((ulong)(BAD_CODE)), // INS_setnp_apx
        unchecked((ulong)(BAD_CODE)), // INS_setl_apx
        unchecked((ulong)(BAD_CODE)), // INS_setge_apx
        unchecked((ulong)(BAD_CODE)), // INS_setle_apx
        unchecked((ulong)(BAD_CODE)), // INS_setg_apx
        unchecked((ulong)(BAD_CODE)), // INS_crc32
        unchecked((ulong)(BAD_CODE)), // INS_tzcnt
        unchecked((ulong)(BAD_CODE)), // INS_tzcnt_apx
        unchecked((ulong)(BAD_CODE)), // INS_lzcnt
        unchecked((ulong)(BAD_CODE)), // INS_lzcnt_apx
        unchecked((ulong)(BAD_CODE)), // INS_movbe
        unchecked((ulong)(BAD_CODE)), // INS_popcnt
        unchecked((ulong)(BAD_CODE)), // INS_popcnt_apx
        unchecked((ulong)(BAD_CODE)), // INS_tpause
        unchecked((ulong)(BAD_CODE)), // INS_umonitor
        unchecked((ulong)(BAD_CODE)), // INS_umwait
        unchecked((ulong)(BAD_CODE)), // INS_neg
        unchecked((ulong)(BAD_CODE)), // INS_not
        unchecked((ulong)(BAD_CODE)), // INS_rol
        unchecked((ulong)(0x0000D0)), // INS_rol_1
        unchecked((ulong)(0x0000C0)), // INS_rol_N
        unchecked((ulong)(BAD_CODE)), // INS_ror
        unchecked((ulong)(0x0008D0)), // INS_ror_1
        unchecked((ulong)(0x0008C0)), // INS_ror_N
        unchecked((ulong)(BAD_CODE)), // INS_rcl
        unchecked((ulong)(0x0010D0)), // INS_rcl_1
        unchecked((ulong)(0x0010C0)), // INS_rcl_N
        unchecked((ulong)(BAD_CODE)), // INS_rcr
        unchecked((ulong)(0x0018D0)), // INS_rcr_1
        unchecked((ulong)(0x0018C0)), // INS_rcr_N
        unchecked((ulong)(BAD_CODE)), // INS_shl
        unchecked((ulong)(0x0020D0)), // INS_shl_1
        unchecked((ulong)(0x0020C0)), // INS_shl_N
        unchecked((ulong)(BAD_CODE)), // INS_shr
        unchecked((ulong)(0x0028D0)), // INS_shr_1
        unchecked((ulong)(0x0028C0)), // INS_shr_N
        unchecked((ulong)(BAD_CODE)), // INS_sar
        unchecked((ulong)(0x0038D0)), // INS_sar_1
        unchecked((ulong)(0x0038C0)), // INS_sar_N
        unchecked((ulong)(0x0000C2)), // INS_ret
        unchecked((ulong)(0x0000E2)), // INS_loop
        unchecked((ulong)(0x0000E8)), // INS_call
    ];

    private static ReadOnlySpan<ulong> insCodesMI =>
        Configuration.TARGET_X86 ? insCodesMIX86 :
        Configuration.TARGET_AMD64 ? insCodesMIX64 :
        throw new FatalJitException("xarch opcode table is not available for this target.");

    private static ReadOnlySpan<ulong> insCodesMRX86 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(0x0030FE)), // INS_push
        unchecked((ulong)(0x00008E)), // INS_pop
        unchecked((ulong)(0x0030FE)), // INS_push_hide
        unchecked((ulong)(0x00008E)), // INS_pop_hide
        unchecked((ulong)(0x0030FF)), // INS_push2
        unchecked((ulong)(0x00008F)), // INS_pop2
        unchecked((ulong)(0x0000FE)), // INS_inc
        unchecked((ulong)(0x0000FE)), // INS_inc_l
        unchecked((ulong)(0x0008FE)), // INS_dec
        unchecked((ulong)(0x0008FE)), // INS_dec_l
        unchecked((ulong)(0x0F00C8)), // INS_bswap
        unchecked((ulong)(0x000000)), // INS_add
        unchecked((ulong)(0x000008)), // INS_or
        unchecked((ulong)(0x000010)), // INS_adc
        unchecked((ulong)(0x000018)), // INS_sbb
        unchecked((ulong)(0x000020)), // INS_and
        unchecked((ulong)(0x000028)), // INS_sub
        unchecked((ulong)(0x000028)), // INS_sub_hide
        unchecked((ulong)(0x000030)), // INS_xor
        unchecked((ulong)(0x000038)), // INS_cmp
        unchecked((ulong)(0x000084)), // INS_test
        unchecked((ulong)(0x000088)), // INS_mov
        unchecked((ulong)(BAD_CODE)), // INS_lea
        unchecked((ulong)(0x0F00A3)), // INS_bt
        unchecked((ulong)(0x0F00AB)), // INS_bts
        unchecked((ulong)(0x0F00B3)), // INS_btr
        unchecked((ulong)(0x0F00BB)), // INS_btc
        unchecked((ulong)(BAD_CODE)), // INS_bsr
        unchecked((ulong)(BAD_CODE)), // INS_bsf
        unchecked((ulong)(BAD_CODE)), // INS_movsx
        unchecked((ulong)(BAD_CODE)), // INS_movzx
        unchecked((ulong)(BAD_CODE)), // INS_cmovo
        unchecked((ulong)(BAD_CODE)), // INS_cmovno
        unchecked((ulong)(BAD_CODE)), // INS_cmovb
        unchecked((ulong)(BAD_CODE)), // INS_cmovae
        unchecked((ulong)(BAD_CODE)), // INS_cmove
        unchecked((ulong)(BAD_CODE)), // INS_cmovne
        unchecked((ulong)(BAD_CODE)), // INS_cmovbe
        unchecked((ulong)(BAD_CODE)), // INS_cmova
        unchecked((ulong)(BAD_CODE)), // INS_cmovs
        unchecked((ulong)(BAD_CODE)), // INS_cmovns
        unchecked((ulong)(BAD_CODE)), // INS_cmovp
        unchecked((ulong)(BAD_CODE)), // INS_cmovnp
        unchecked((ulong)(BAD_CODE)), // INS_cmovl
        unchecked((ulong)(BAD_CODE)), // INS_cmovge
        unchecked((ulong)(BAD_CODE)), // INS_cmovle
        unchecked((ulong)(BAD_CODE)), // INS_cmovg
        unchecked((ulong)(0x000086)), // INS_xchg
        unchecked((ulong)(0x0F00AC)), // INS_imul
        unchecked((ulong)(BAD_CODE)), // INS_imul_AX
        unchecked((ulong)(BAD_CODE)), // INS_imul_CX
        unchecked((ulong)(BAD_CODE)), // INS_imul_DX
        unchecked((ulong)(BAD_CODE)), // INS_imul_BX
        unchecked((ulong)(BAD_CODE)), // INS_imul_SP
        unchecked((ulong)(BAD_CODE)), // INS_imul_BP
        unchecked((ulong)(BAD_CODE)), // INS_imul_SI
        unchecked((ulong)(BAD_CODE)), // INS_imul_DI
        unchecked((ulong)(BAD_CODE)), // INS_addpd
        unchecked((ulong)(BAD_CODE)), // INS_addps
        unchecked((ulong)(BAD_CODE)), // INS_addsd
        unchecked((ulong)(BAD_CODE)), // INS_addss
        unchecked((ulong)(BAD_CODE)), // INS_addsubpd
        unchecked((ulong)(BAD_CODE)), // INS_addsubps
        unchecked((ulong)(BAD_CODE)), // INS_andnpd
        unchecked((ulong)(BAD_CODE)), // INS_andnps
        unchecked((ulong)(BAD_CODE)), // INS_andpd
        unchecked((ulong)(BAD_CODE)), // INS_andps
        unchecked((ulong)(BAD_CODE)), // INS_blendpd
        unchecked((ulong)(BAD_CODE)), // INS_blendps
        unchecked((ulong)(BAD_CODE)), // INS_blendvpd
        unchecked((ulong)(BAD_CODE)), // INS_blendvps
        unchecked((ulong)(BAD_CODE)), // INS_cmppd
        unchecked((ulong)(BAD_CODE)), // INS_cmpps
        unchecked((ulong)(BAD_CODE)), // INS_cmpsd
        unchecked((ulong)(BAD_CODE)), // INS_cmpss
        unchecked((ulong)(BAD_CODE)), // INS_comisd
        unchecked((ulong)(BAD_CODE)), // INS_comiss
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2ss
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2sd
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si64
        unchecked((ulong)(BAD_CODE)), // INS_divpd
        unchecked((ulong)(BAD_CODE)), // INS_divps
        unchecked((ulong)(BAD_CODE)), // INS_divsd
        unchecked((ulong)(BAD_CODE)), // INS_divss
        unchecked((ulong)(BAD_CODE)), // INS_dppd
        unchecked((ulong)(BAD_CODE)), // INS_dpps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x17)))) << 8)))), // INS_extractps
        unchecked((ulong)(BAD_CODE)), // INS_haddpd
        unchecked((ulong)(BAD_CODE)), // INS_haddps
        unchecked((ulong)(BAD_CODE)), // INS_hsubpd
        unchecked((ulong)(BAD_CODE)), // INS_hsubps
        unchecked((ulong)(BAD_CODE)), // INS_insertps
        unchecked((ulong)(BAD_CODE)), // INS_lddqu
        unchecked((ulong)(0x000FE8AE)), // INS_lfence
        unchecked((ulong)(BAD_CODE)), // INS_maskmovdqu
        unchecked((ulong)(BAD_CODE)), // INS_maxpd
        unchecked((ulong)(BAD_CODE)), // INS_maxps
        unchecked((ulong)(BAD_CODE)), // INS_maxsd
        unchecked((ulong)(BAD_CODE)), // INS_maxss
        unchecked((ulong)(0x000FF0AE)), // INS_mfence
        unchecked((ulong)(BAD_CODE)), // INS_minpd
        unchecked((ulong)(BAD_CODE)), // INS_minps
        unchecked((ulong)(BAD_CODE)), // INS_minsd
        unchecked((ulong)(BAD_CODE)), // INS_minss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x29)))))), // INS_movapd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x29)))))), // INS_movaps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd32
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd64
        unchecked((ulong)(BAD_CODE)), // INS_movddup
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqa32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqu32
        unchecked((ulong)(BAD_CODE)), // INS_movhlps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x17)))))), // INS_movhpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x17)))))), // INS_movhps
        unchecked((ulong)(BAD_CODE)), // INS_movlhps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x13)))))), // INS_movlpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x13)))))), // INS_movlps
        unchecked((ulong)(BAD_CODE)), // INS_movmskpd
        unchecked((ulong)(BAD_CODE)), // INS_movmskps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE7)))))), // INS_movntdq
        unchecked((ulong)(BAD_CODE)), // INS_movntdqa
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti32
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti64
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2B)))))), // INS_movntpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x2B)))))), // INS_movntps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_movq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movsd_simd
        unchecked((ulong)(BAD_CODE)), // INS_movshdup
        unchecked((ulong)(BAD_CODE)), // INS_movsldup
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movupd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x11)))))), // INS_movups
        unchecked((ulong)(BAD_CODE)), // INS_mpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_mulpd
        unchecked((ulong)(BAD_CODE)), // INS_mulps
        unchecked((ulong)(BAD_CODE)), // INS_mulsd
        unchecked((ulong)(BAD_CODE)), // INS_mulss
        unchecked((ulong)(BAD_CODE)), // INS_orpd
        unchecked((ulong)(BAD_CODE)), // INS_orps
        unchecked((ulong)(BAD_CODE)), // INS_pabsb
        unchecked((ulong)(BAD_CODE)), // INS_pabsd
        unchecked((ulong)(BAD_CODE)), // INS_pabsw
        unchecked((ulong)(BAD_CODE)), // INS_packssdw
        unchecked((ulong)(BAD_CODE)), // INS_packsswb
        unchecked((ulong)(BAD_CODE)), // INS_packusdw
        unchecked((ulong)(BAD_CODE)), // INS_packuswb
        unchecked((ulong)(BAD_CODE)), // INS_paddb
        unchecked((ulong)(BAD_CODE)), // INS_paddd
        unchecked((ulong)(BAD_CODE)), // INS_paddq
        unchecked((ulong)(BAD_CODE)), // INS_paddsb
        unchecked((ulong)(BAD_CODE)), // INS_paddsw
        unchecked((ulong)(BAD_CODE)), // INS_paddusb
        unchecked((ulong)(BAD_CODE)), // INS_paddusw
        unchecked((ulong)(BAD_CODE)), // INS_paddw
        unchecked((ulong)(BAD_CODE)), // INS_palignr
        unchecked((ulong)(BAD_CODE)), // INS_pandd
        unchecked((ulong)(BAD_CODE)), // INS_pandnd
        unchecked((ulong)(BAD_CODE)), // INS_pavgb
        unchecked((ulong)(BAD_CODE)), // INS_pavgw
        unchecked((ulong)(BAD_CODE)), // INS_pblendvb
        unchecked((ulong)(BAD_CODE)), // INS_pblendw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x14)))) << 8)))), // INS_pextrb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrq
        unchecked((ulong)(BAD_CODE)), // INS_phaddd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x15)))) << 8)))), // INS_pextrw
        unchecked((ulong)(BAD_CODE)), // INS_phaddsw
        unchecked((ulong)(BAD_CODE)), // INS_phaddw
        unchecked((ulong)(BAD_CODE)), // INS_phminposuw
        unchecked((ulong)(BAD_CODE)), // INS_phsubd
        unchecked((ulong)(BAD_CODE)), // INS_phsubsw
        unchecked((ulong)(BAD_CODE)), // INS_phsubw
        unchecked((ulong)(BAD_CODE)), // INS_pinsrb
        unchecked((ulong)(BAD_CODE)), // INS_pinsrd
        unchecked((ulong)(BAD_CODE)), // INS_pinsrq
        unchecked((ulong)(BAD_CODE)), // INS_pinsrw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddubsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddwd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsb
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaxub
        unchecked((ulong)(BAD_CODE)), // INS_pmaxud
        unchecked((ulong)(BAD_CODE)), // INS_pmaxuw
        unchecked((ulong)(BAD_CODE)), // INS_pminsb
        unchecked((ulong)(BAD_CODE)), // INS_pminsd
        unchecked((ulong)(BAD_CODE)), // INS_pminsw
        unchecked((ulong)(BAD_CODE)), // INS_pminub
        unchecked((ulong)(BAD_CODE)), // INS_pminud
        unchecked((ulong)(BAD_CODE)), // INS_pminuw
        unchecked((ulong)(BAD_CODE)), // INS_pmovmskb
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmuldq
        unchecked((ulong)(BAD_CODE)), // INS_pmulhrsw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhuw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhw
        unchecked((ulong)(BAD_CODE)), // INS_pmulld
        unchecked((ulong)(BAD_CODE)), // INS_pmuludq
        unchecked((ulong)(BAD_CODE)), // INS_pmullw
        unchecked((ulong)(BAD_CODE)), // INS_pord
        unchecked((ulong)(0x000F0018)), // INS_prefetchnta
        unchecked((ulong)(0x000F0818)), // INS_prefetcht0
        unchecked((ulong)(0x000F1018)), // INS_prefetcht1
        unchecked((ulong)(0x000F1818)), // INS_prefetcht2
        unchecked((ulong)(BAD_CODE)), // INS_psadbw
        unchecked((ulong)(BAD_CODE)), // INS_pshufb
        unchecked((ulong)(BAD_CODE)), // INS_pshufd
        unchecked((ulong)(BAD_CODE)), // INS_pshufhw
        unchecked((ulong)(BAD_CODE)), // INS_pshuflw
        unchecked((ulong)(BAD_CODE)), // INS_psignb
        unchecked((ulong)(BAD_CODE)), // INS_psignd
        unchecked((ulong)(BAD_CODE)), // INS_psignw
        unchecked((ulong)(BAD_CODE)), // INS_pslld
        unchecked((ulong)(BAD_CODE)), // INS_pslldq
        unchecked((ulong)(BAD_CODE)), // INS_psllq
        unchecked((ulong)(BAD_CODE)), // INS_psllw
        unchecked((ulong)(BAD_CODE)), // INS_psrad
        unchecked((ulong)(BAD_CODE)), // INS_psraw
        unchecked((ulong)(BAD_CODE)), // INS_psrld
        unchecked((ulong)(BAD_CODE)), // INS_psrldq
        unchecked((ulong)(BAD_CODE)), // INS_psrlq
        unchecked((ulong)(BAD_CODE)), // INS_psrlw
        unchecked((ulong)(BAD_CODE)), // INS_psubb
        unchecked((ulong)(BAD_CODE)), // INS_psubd
        unchecked((ulong)(BAD_CODE)), // INS_psubq
        unchecked((ulong)(BAD_CODE)), // INS_psubsb
        unchecked((ulong)(BAD_CODE)), // INS_psubsw
        unchecked((ulong)(BAD_CODE)), // INS_psubusb
        unchecked((ulong)(BAD_CODE)), // INS_psubusw
        unchecked((ulong)(BAD_CODE)), // INS_psubw
        unchecked((ulong)(BAD_CODE)), // INS_ptest
        unchecked((ulong)(BAD_CODE)), // INS_punpckhbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckhdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhwd
        unchecked((ulong)(BAD_CODE)), // INS_punpcklbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckldq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklwd
        unchecked((ulong)(BAD_CODE)), // INS_pxord
        unchecked((ulong)(BAD_CODE)), // INS_rcpps
        unchecked((ulong)(BAD_CODE)), // INS_rcpss
        unchecked((ulong)(BAD_CODE)), // INS_roundpd
        unchecked((ulong)(BAD_CODE)), // INS_roundps
        unchecked((ulong)(BAD_CODE)), // INS_roundsd
        unchecked((ulong)(BAD_CODE)), // INS_roundss
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtps
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtss
        unchecked((ulong)(0x000FF8AE)), // INS_sfence
        unchecked((ulong)(BAD_CODE)), // INS_shufpd
        unchecked((ulong)(BAD_CODE)), // INS_shufps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtpd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtsd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtss
        unchecked((ulong)(BAD_CODE)), // INS_subpd
        unchecked((ulong)(BAD_CODE)), // INS_subps
        unchecked((ulong)(BAD_CODE)), // INS_subsd
        unchecked((ulong)(BAD_CODE)), // INS_subss
        unchecked((ulong)(BAD_CODE)), // INS_ucomisd
        unchecked((ulong)(BAD_CODE)), // INS_ucomiss
        unchecked((ulong)(BAD_CODE)), // INS_unpckhpd
        unchecked((ulong)(BAD_CODE)), // INS_unpckhps
        unchecked((ulong)(BAD_CODE)), // INS_unpcklpd
        unchecked((ulong)(BAD_CODE)), // INS_unpcklps
        unchecked((ulong)(BAD_CODE)), // INS_xorpd
        unchecked((ulong)(BAD_CODE)), // INS_xorps
        unchecked((ulong)(BAD_CODE)), // INS_aesdec
        unchecked((ulong)(BAD_CODE)), // INS_aesdeclast
        unchecked((ulong)(BAD_CODE)), // INS_aesenc
        unchecked((ulong)(BAD_CODE)), // INS_aesenclast
        unchecked((ulong)(BAD_CODE)), // INS_aesimc
        unchecked((ulong)(BAD_CODE)), // INS_aeskeygenassist
        unchecked((ulong)(BAD_CODE)), // INS_pclmulqdq
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha1nexte
        unchecked((ulong)(BAD_CODE)), // INS_sha1rnds4
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha256rnds2
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineinvqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8mulb
        unchecked((ulong)(BAD_CODE)), // INS_vblendvpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendvps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastsd
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2F)))) << 8)))), // INS_vmaskmovpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2E)))) << 8)))), // INS_vmaskmovps
        unchecked((ulong)(BAD_CODE)), // INS_vpblendvb
        unchecked((ulong)(BAD_CODE)), // INS_vperm2f128
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpdvar
        unchecked((ulong)(BAD_CODE)), // INS_vpermilps
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpsvar
        unchecked((ulong)(BAD_CODE)), // INS_vtestpd
        unchecked((ulong)(BAD_CODE)), // INS_vtestps
        unchecked((ulong)(0xC577F8)), // INS_vzeroupper
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1D)))) << 8)))), // INS_vcvtps2ph
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vpblendd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw
        unchecked((ulong)(BAD_CODE)), // INS_vperm2i128
        unchecked((ulong)(BAD_CODE)), // INS_vpermd
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermps
        unchecked((ulong)(BAD_CODE)), // INS_vpermq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovq
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvq
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_andn
        unchecked((ulong)(BAD_CODE)), // INS_bextr
        unchecked((ulong)(BAD_CODE)), // INS_blsi
        unchecked((ulong)(BAD_CODE)), // INS_blsmsk
        unchecked((ulong)(BAD_CODE)), // INS_blsr
        unchecked((ulong)(BAD_CODE)), // INS_bzhi
        unchecked((ulong)(BAD_CODE)), // INS_mulx
        unchecked((ulong)(BAD_CODE)), // INS_pdep
        unchecked((ulong)(BAD_CODE)), // INS_pext
        unchecked((ulong)(BAD_CODE)), // INS_rorx
        unchecked((ulong)(BAD_CODE)), // INS_sarx
        unchecked((ulong)(BAD_CODE)), // INS_shlx
        unchecked((ulong)(BAD_CODE)), // INS_shrx
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuuds
        unchecked((ulong)(BAD_CODE)), // INS_vbmacor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbmacxor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbitrev
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52huq
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52luq
        unchecked((ulong)(BAD_CODE)), // INS_kaddb
        unchecked((ulong)(BAD_CODE)), // INS_kaddd
        unchecked((ulong)(BAD_CODE)), // INS_kaddq
        unchecked((ulong)(BAD_CODE)), // INS_kaddw
        unchecked((ulong)(BAD_CODE)), // INS_kandb
        unchecked((ulong)(BAD_CODE)), // INS_kandd
        unchecked((ulong)(BAD_CODE)), // INS_kandnb
        unchecked((ulong)(BAD_CODE)), // INS_kandnd
        unchecked((ulong)(BAD_CODE)), // INS_kandnq
        unchecked((ulong)(BAD_CODE)), // INS_kandnw
        unchecked((ulong)(BAD_CODE)), // INS_kandq
        unchecked((ulong)(BAD_CODE)), // INS_kandw
        unchecked((ulong)(BAD_CODE)), // INS_kmovb_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovb_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovd_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovd_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovq_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovq_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovw_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovw_msk
        unchecked((ulong)(BAD_CODE)), // INS_knotb
        unchecked((ulong)(BAD_CODE)), // INS_knotd
        unchecked((ulong)(BAD_CODE)), // INS_knotq
        unchecked((ulong)(BAD_CODE)), // INS_knotw
        unchecked((ulong)(BAD_CODE)), // INS_korb
        unchecked((ulong)(BAD_CODE)), // INS_kord
        unchecked((ulong)(BAD_CODE)), // INS_korq
        unchecked((ulong)(BAD_CODE)), // INS_kortestb
        unchecked((ulong)(BAD_CODE)), // INS_kortestd
        unchecked((ulong)(BAD_CODE)), // INS_kortestq
        unchecked((ulong)(BAD_CODE)), // INS_kortestw
        unchecked((ulong)(BAD_CODE)), // INS_korw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftld
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrd
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrw
        unchecked((ulong)(BAD_CODE)), // INS_ktestb
        unchecked((ulong)(BAD_CODE)), // INS_ktestd
        unchecked((ulong)(BAD_CODE)), // INS_ktestq
        unchecked((ulong)(BAD_CODE)), // INS_ktestw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckbw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckdq
        unchecked((ulong)(BAD_CODE)), // INS_kunpckwd
        unchecked((ulong)(BAD_CODE)), // INS_kxnorb
        unchecked((ulong)(BAD_CODE)), // INS_kxnord
        unchecked((ulong)(BAD_CODE)), // INS_kxnorq
        unchecked((ulong)(BAD_CODE)), // INS_kxnorw
        unchecked((ulong)(BAD_CODE)), // INS_kxorb
        unchecked((ulong)(BAD_CODE)), // INS_kxord
        unchecked((ulong)(BAD_CODE)), // INS_kxorq
        unchecked((ulong)(BAD_CODE)), // INS_kxorw
        unchecked((ulong)(BAD_CODE)), // INS_valignd
        unchecked((ulong)(BAD_CODE)), // INS_valignq
        unchecked((ulong)(BAD_CODE)), // INS_vblendmpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendmps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vcmppd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpps
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompresspd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompressps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_vdbpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vexpandpd
        unchecked((ulong)(BAD_CODE)), // INS_vexpandps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf64x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmpd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmps
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmsd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmss
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasspd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassps
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassss
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgetexppd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpps
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpss
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantpd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantps
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantss
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x4
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqa64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu16
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu8
        unchecked((ulong)(BAD_CODE)), // INS_vpabsq
        unchecked((ulong)(BAD_CODE)), // INS_vpandnq
        unchecked((ulong)(BAD_CODE)), // INS_vpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmb
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmd
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmw
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpub
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpud
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressq
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictd
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictq
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermq_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandd
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntd
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxsq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxuq
        unchecked((ulong)(BAD_CODE)), // INS_vpminsq
        unchecked((ulong)(BAD_CODE)), // INS_vpminuq
        unchecked((ulong)(BAD_CODE)), // INS_vpmovb2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovd2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x31))) << 8)))), // INS_vpmovdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x33))) << 8)))), // INS_vpmovdw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2d
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2q
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2w
        unchecked((ulong)(BAD_CODE)), // INS_vpmovq2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x32))) << 8)))), // INS_vpmovqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x35))) << 8)))), // INS_vpmovqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x34))) << 8)))), // INS_vpmovqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x21))) << 8)))), // INS_vpmovsdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x23))) << 8)))), // INS_vpmovsdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x22))) << 8)))), // INS_vpmovsqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x25))) << 8)))), // INS_vpmovsqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x24))) << 8)))), // INS_vpmovsqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x20))) << 8)))), // INS_vpmovswb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x11))) << 8)))), // INS_vpmovusdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x13))) << 8)))), // INS_vpmovusdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x12))) << 8)))), // INS_vpmovusqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x15))) << 8)))), // INS_vpmovusqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x14))) << 8)))), // INS_vpmovusqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x10))) << 8)))), // INS_vpmovuswb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovw2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x30))) << 8)))), // INS_vpmovwb
        unchecked((ulong)(BAD_CODE)), // INS_vpmullq
        unchecked((ulong)(BAD_CODE)), // INS_vporq
        unchecked((ulong)(BAD_CODE)), // INS_vprold
        unchecked((ulong)(BAD_CODE)), // INS_vprolq
        unchecked((ulong)(BAD_CODE)), // INS_vprolvd
        unchecked((ulong)(BAD_CODE)), // INS_vprolvq
        unchecked((ulong)(BAD_CODE)), // INS_vprord
        unchecked((ulong)(BAD_CODE)), // INS_vprorq
        unchecked((ulong)(BAD_CODE)), // INS_vprorvd
        unchecked((ulong)(BAD_CODE)), // INS_vprorvq
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvw
        unchecked((ulong)(BAD_CODE)), // INS_vpsraq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravw
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvw
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogd
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmw
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmw
        unchecked((ulong)(BAD_CODE)), // INS_vpxorq
        unchecked((ulong)(BAD_CODE)), // INS_vrangepd
        unchecked((ulong)(BAD_CODE)), // INS_vrangeps
        unchecked((ulong)(BAD_CODE)), // INS_vrangesd
        unchecked((ulong)(BAD_CODE)), // INS_vrangess
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ss
        unchecked((ulong)(BAD_CODE)), // INS_vreducepd
        unchecked((ulong)(BAD_CODE)), // INS_vreduceps
        unchecked((ulong)(BAD_CODE)), // INS_vreducesd
        unchecked((ulong)(BAD_CODE)), // INS_vreducess
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalepd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleps
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaless
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ss
        unchecked((ulong)(BAD_CODE)), // INS_vscalefpd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefps
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefss
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vshuff32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshuff64x2
        unchecked((ulong)(BAD_CODE)), // INS_vshufi32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshufi64x2
        unchecked((ulong)(BAD_CODE)), // INS_vpermb
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2b
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmultishiftqb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandb
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandw
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntb
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntd
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdw
        unchecked((ulong)(BAD_CODE)), // INS_vpshufbitqmb
        unchecked((ulong)(BAD_CODE)), // INS_vaddph
        unchecked((ulong)(BAD_CODE)), // INS_vaddsh
        unchecked((ulong)(BAD_CODE)), // INS_vcmpph
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsh
        unchecked((ulong)(BAD_CODE)), // INS_vcomish
        unchecked((ulong)(BAD_CODE)), // INS_vcvtdq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtne2ps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtneps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2psx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2phx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2sd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2ss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vdivph
        unchecked((ulong)(BAD_CODE)), // INS_vdivsh
        unchecked((ulong)(BAD_CODE)), // INS_vdpbf16ps
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassph
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssh
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpph
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsh
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantph
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsh
        unchecked((ulong)(BAD_CODE)), // INS_vmaxph
        unchecked((ulong)(BAD_CODE)), // INS_vmaxsh
        unchecked((ulong)(BAD_CODE)), // INS_vminsh
        unchecked((ulong)(BAD_CODE)), // INS_vminph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x11)))))), // INS_vmovsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x7E)))))), // INS_vmovw
        unchecked((ulong)(BAD_CODE)), // INS_vmulph
        unchecked((ulong)(BAD_CODE)), // INS_vmulsh
        unchecked((ulong)(BAD_CODE)), // INS_vrcpph
        unchecked((ulong)(BAD_CODE)), // INS_vrcpsh
        unchecked((ulong)(BAD_CODE)), // INS_vreduceph
        unchecked((ulong)(BAD_CODE)), // INS_vreducesh
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleph
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesh
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vscalefph
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsh
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vsubph
        unchecked((ulong)(BAD_CODE)), // INS_vsubsh
        unchecked((ulong)(BAD_CODE)), // INS_vucomish
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectd
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectq
        unchecked((ulong)(BAD_CODE)), // INS_vcomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vcomxss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxpd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxps
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_vmovd_simd
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7E)))))), // INS_vmovw_simd
        unchecked((ulong)(BAD_CODE)), // INS_vmpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vucomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vucomxss
        unchecked((ulong)(BAD_CODE)), // INS_crc32
        unchecked((ulong)(BAD_CODE)), // INS_tzcnt
        unchecked((ulong)(BAD_CODE)), // INS_lzcnt
        unchecked((ulong)(((((0x0F)) << 16) | (((0x38)) << 24) | (((0xF1)))))), // INS_movbe
        unchecked((ulong)(BAD_CODE)), // INS_popcnt
        unchecked((ulong)(BAD_CODE)), // INS_tpause
        unchecked((ulong)(BAD_CODE)), // INS_umonitor
        unchecked((ulong)(BAD_CODE)), // INS_umwait
        unchecked((ulong)(0x0018F6)), // INS_neg
        unchecked((ulong)(0x0010F6)), // INS_not
        unchecked((ulong)(0x0000D2)), // INS_rol
        unchecked((ulong)(0x0000D0)), // INS_rol_1
        unchecked((ulong)(0x0000C0)), // INS_rol_N
        unchecked((ulong)(0x0008D2)), // INS_ror
        unchecked((ulong)(0x0008D0)), // INS_ror_1
        unchecked((ulong)(0x0008C0)), // INS_ror_N
        unchecked((ulong)(0x0010D2)), // INS_rcl
        unchecked((ulong)(0x0010D0)), // INS_rcl_1
        unchecked((ulong)(0x0010C0)), // INS_rcl_N
        unchecked((ulong)(0x0018D2)), // INS_rcr
        unchecked((ulong)(0x0018D0)), // INS_rcr_1
        unchecked((ulong)(0x0018C0)), // INS_rcr_N
        unchecked((ulong)(0x0020D2)), // INS_shl
        unchecked((ulong)(0x0020D0)), // INS_shl_1
        unchecked((ulong)(0x0020C0)), // INS_shl_N
        unchecked((ulong)(0x0028D2)), // INS_shr
        unchecked((ulong)(0x0028D0)), // INS_shr_1
        unchecked((ulong)(0x0028C0)), // INS_shr_N
        unchecked((ulong)(0x0038D2)), // INS_sar
        unchecked((ulong)(0x0038D0)), // INS_sar_1
        unchecked((ulong)(0x0038C0)), // INS_sar_N
        unchecked((ulong)(0x0000C3)), // INS_ret
        unchecked((ulong)(BAD_CODE)), // INS_loop
        unchecked((ulong)(0x0010FF)), // INS_call
        unchecked((ulong)(0x00A4F3)), // INS_r_movsb
        unchecked((ulong)(0x00A5F3)), // INS_r_movsd
        unchecked((ulong)(0x0000A4)), // INS_movsb
        unchecked((ulong)(0x0000A5)), // INS_movsd
        unchecked((ulong)(0x00AAF3)), // INS_r_stosb
        unchecked((ulong)(0x00ABF3)), // INS_r_stosd
        unchecked((ulong)(0x0000AA)), // INS_stosb
        unchecked((ulong)(0x0000AB)), // INS_stosd
        unchecked((ulong)(0x0000CC)), // INS_int3
        unchecked((ulong)(0x000090)), // INS_nop
        unchecked((ulong)(0x0090F3)), // INS_pause
        unchecked((ulong)(0x0000F0)), // INS_lock
        unchecked((ulong)(0x0000C9)), // INS_leave
        unchecked((ulong)(0x0fe801)), // INS_serialize
        unchecked((ulong)(0x000098)), // INS_cwde
        unchecked((ulong)(0x000099)), // INS_cdq
        unchecked((ulong)(0x0038F6)), // INS_idiv
        unchecked((ulong)(0x0028F6)), // INS_imulEAX
        unchecked((ulong)(0x0030F6)), // INS_div
        unchecked((ulong)(0x0020F6)), // INS_mulEAX
        unchecked((ulong)(0x00009E)), // INS_sahf
        unchecked((ulong)(0x0F00C0)), // INS_xadd
        unchecked((ulong)(0x0F00B0)), // INS_cmpxchg
        unchecked((ulong)(0x0F00A4)), // INS_shld
        unchecked((ulong)(0x0F00AC)), // INS_shrd
        unchecked((ulong)(0x0000D9)), // INS_fld
        unchecked((ulong)(0x0018D9)), // INS_fstp
        unchecked((ulong)(0x0F0090)), // INS_seto
        unchecked((ulong)(0x0F0091)), // INS_setno
        unchecked((ulong)(0x0F0092)), // INS_setb
        unchecked((ulong)(0x0F0093)), // INS_setae
        unchecked((ulong)(0x0F0094)), // INS_sete
        unchecked((ulong)(0x0F0095)), // INS_setne
        unchecked((ulong)(0x0F0096)), // INS_setbe
        unchecked((ulong)(0x0F0097)), // INS_seta
        unchecked((ulong)(0x0F0098)), // INS_sets
        unchecked((ulong)(0x0F0099)), // INS_setns
        unchecked((ulong)(0x0F009A)), // INS_setp
        unchecked((ulong)(0x0F009B)), // INS_setnp
        unchecked((ulong)(0x0F009C)), // INS_setl
        unchecked((ulong)(0x0F009D)), // INS_setge
        unchecked((ulong)(0x0F009E)), // INS_setle
        unchecked((ulong)(0x0F009F)), // INS_setg
        unchecked((ulong)(0x0020FF)), // INS_tail_i_jmp
        unchecked((ulong)(0x0020FF)), // INS_i_jmp
    ];

    private static ReadOnlySpan<ulong> insCodesMRX64 => [
        unchecked((ulong)(BAD_CODE)), // INS_invalid
        unchecked((ulong)(0x0030FE)), // INS_push
        unchecked((ulong)(0x00008E)), // INS_pop
        unchecked((ulong)(0x0030FE)), // INS_push_hide
        unchecked((ulong)(0x00008E)), // INS_pop_hide
        unchecked((ulong)(0x0030FF)), // INS_push2
        unchecked((ulong)(0x00008F)), // INS_pop2
        unchecked((ulong)(0x0000FE)), // INS_inc
        unchecked((ulong)(0x0000FE)), // INS_inc_l
        unchecked((ulong)(0x0008FE)), // INS_dec
        unchecked((ulong)(0x0008FE)), // INS_dec_l
        unchecked((ulong)(0x0F00C8)), // INS_bswap
        unchecked((ulong)(0x000000)), // INS_add
        unchecked((ulong)(0x000008)), // INS_or
        unchecked((ulong)(0x000010)), // INS_adc
        unchecked((ulong)(0x000018)), // INS_sbb
        unchecked((ulong)(0x000020)), // INS_and
        unchecked((ulong)(0x000028)), // INS_sub
        unchecked((ulong)(0x000028)), // INS_sub_hide
        unchecked((ulong)(0x000030)), // INS_xor
        unchecked((ulong)(0x000038)), // INS_cmp
        unchecked((ulong)(0x000084)), // INS_test
        unchecked((ulong)(0x000088)), // INS_mov
        unchecked((ulong)(BAD_CODE)), // INS_lea
        unchecked((ulong)(0x0F00A3)), // INS_bt
        unchecked((ulong)(0x0F00AB)), // INS_bts
        unchecked((ulong)(0x0F00B3)), // INS_btr
        unchecked((ulong)(0x0F00BB)), // INS_btc
        unchecked((ulong)(BAD_CODE)), // INS_bsr
        unchecked((ulong)(BAD_CODE)), // INS_bsf
        unchecked((ulong)(BAD_CODE)), // INS_movsx
        unchecked((ulong)(BAD_CODE)), // INS_movsxd
        unchecked((ulong)(BAD_CODE)), // INS_movzx
        unchecked((ulong)(BAD_CODE)), // INS_cmovo
        unchecked((ulong)(BAD_CODE)), // INS_cmovno
        unchecked((ulong)(BAD_CODE)), // INS_cmovb
        unchecked((ulong)(BAD_CODE)), // INS_cmovae
        unchecked((ulong)(BAD_CODE)), // INS_cmove
        unchecked((ulong)(BAD_CODE)), // INS_cmovne
        unchecked((ulong)(BAD_CODE)), // INS_cmovbe
        unchecked((ulong)(BAD_CODE)), // INS_cmova
        unchecked((ulong)(BAD_CODE)), // INS_cmovs
        unchecked((ulong)(BAD_CODE)), // INS_cmovns
        unchecked((ulong)(BAD_CODE)), // INS_cmovp
        unchecked((ulong)(BAD_CODE)), // INS_cmovnp
        unchecked((ulong)(BAD_CODE)), // INS_cmovl
        unchecked((ulong)(BAD_CODE)), // INS_cmovge
        unchecked((ulong)(BAD_CODE)), // INS_cmovle
        unchecked((ulong)(BAD_CODE)), // INS_cmovg
        unchecked((ulong)(0x000086)), // INS_xchg
        unchecked((ulong)(0x0F00AC)), // INS_imul
        unchecked((ulong)(BAD_CODE)), // INS_imul_AX
        unchecked((ulong)(BAD_CODE)), // INS_imul_CX
        unchecked((ulong)(BAD_CODE)), // INS_imul_DX
        unchecked((ulong)(BAD_CODE)), // INS_imul_BX
        unchecked((ulong)(BAD_CODE)), // INS_imul_SP
        unchecked((ulong)(BAD_CODE)), // INS_imul_BP
        unchecked((ulong)(BAD_CODE)), // INS_imul_SI
        unchecked((ulong)(BAD_CODE)), // INS_imul_DI
        unchecked((ulong)(BAD_CODE)), // INS_imul_08
        unchecked((ulong)(BAD_CODE)), // INS_imul_09
        unchecked((ulong)(BAD_CODE)), // INS_imul_10
        unchecked((ulong)(BAD_CODE)), // INS_imul_11
        unchecked((ulong)(BAD_CODE)), // INS_imul_12
        unchecked((ulong)(BAD_CODE)), // INS_imul_13
        unchecked((ulong)(BAD_CODE)), // INS_imul_14
        unchecked((ulong)(BAD_CODE)), // INS_imul_15
        unchecked((ulong)(BAD_CODE)), // INS_imul_16
        unchecked((ulong)(BAD_CODE)), // INS_imul_17
        unchecked((ulong)(BAD_CODE)), // INS_imul_18
        unchecked((ulong)(BAD_CODE)), // INS_imul_19
        unchecked((ulong)(BAD_CODE)), // INS_imul_20
        unchecked((ulong)(BAD_CODE)), // INS_imul_21
        unchecked((ulong)(BAD_CODE)), // INS_imul_22
        unchecked((ulong)(BAD_CODE)), // INS_imul_23
        unchecked((ulong)(BAD_CODE)), // INS_imul_24
        unchecked((ulong)(BAD_CODE)), // INS_imul_25
        unchecked((ulong)(BAD_CODE)), // INS_imul_26
        unchecked((ulong)(BAD_CODE)), // INS_imul_27
        unchecked((ulong)(BAD_CODE)), // INS_imul_28
        unchecked((ulong)(BAD_CODE)), // INS_imul_29
        unchecked((ulong)(BAD_CODE)), // INS_imul_30
        unchecked((ulong)(BAD_CODE)), // INS_imul_31
        unchecked((ulong)(BAD_CODE)), // INS_addpd
        unchecked((ulong)(BAD_CODE)), // INS_addps
        unchecked((ulong)(BAD_CODE)), // INS_addsd
        unchecked((ulong)(BAD_CODE)), // INS_addss
        unchecked((ulong)(BAD_CODE)), // INS_addsubpd
        unchecked((ulong)(BAD_CODE)), // INS_addsubps
        unchecked((ulong)(BAD_CODE)), // INS_andnpd
        unchecked((ulong)(BAD_CODE)), // INS_andnps
        unchecked((ulong)(BAD_CODE)), // INS_andpd
        unchecked((ulong)(BAD_CODE)), // INS_andps
        unchecked((ulong)(BAD_CODE)), // INS_blendpd
        unchecked((ulong)(BAD_CODE)), // INS_blendps
        unchecked((ulong)(BAD_CODE)), // INS_blendvpd
        unchecked((ulong)(BAD_CODE)), // INS_blendvps
        unchecked((ulong)(BAD_CODE)), // INS_cmppd
        unchecked((ulong)(BAD_CODE)), // INS_cmpps
        unchecked((ulong)(BAD_CODE)), // INS_cmpsd
        unchecked((ulong)(BAD_CODE)), // INS_cmpss
        unchecked((ulong)(BAD_CODE)), // INS_comisd
        unchecked((ulong)(BAD_CODE)), // INS_comiss
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtdq2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtpd2ps
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvtps2pd
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsd2ss
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_cvtsi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2sd
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvtss2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttpd2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttps2dq
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttsd2si64
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si32
        unchecked((ulong)(BAD_CODE)), // INS_cvttss2si64
        unchecked((ulong)(BAD_CODE)), // INS_divpd
        unchecked((ulong)(BAD_CODE)), // INS_divps
        unchecked((ulong)(BAD_CODE)), // INS_divsd
        unchecked((ulong)(BAD_CODE)), // INS_divss
        unchecked((ulong)(BAD_CODE)), // INS_dppd
        unchecked((ulong)(BAD_CODE)), // INS_dpps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x17)))) << 8)))), // INS_extractps
        unchecked((ulong)(BAD_CODE)), // INS_haddpd
        unchecked((ulong)(BAD_CODE)), // INS_haddps
        unchecked((ulong)(BAD_CODE)), // INS_hsubpd
        unchecked((ulong)(BAD_CODE)), // INS_hsubps
        unchecked((ulong)(BAD_CODE)), // INS_insertps
        unchecked((ulong)(BAD_CODE)), // INS_lddqu
        unchecked((ulong)(0x000FE8AE)), // INS_lfence
        unchecked((ulong)(BAD_CODE)), // INS_maskmovdqu
        unchecked((ulong)(BAD_CODE)), // INS_maxpd
        unchecked((ulong)(BAD_CODE)), // INS_maxps
        unchecked((ulong)(BAD_CODE)), // INS_maxsd
        unchecked((ulong)(BAD_CODE)), // INS_maxss
        unchecked((ulong)(0x000FF0AE)), // INS_mfence
        unchecked((ulong)(BAD_CODE)), // INS_minpd
        unchecked((ulong)(BAD_CODE)), // INS_minps
        unchecked((ulong)(BAD_CODE)), // INS_minsd
        unchecked((ulong)(BAD_CODE)), // INS_minss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x29)))))), // INS_movapd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x29)))))), // INS_movaps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd32
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7E)))))), // INS_movd64
        unchecked((ulong)(BAD_CODE)), // INS_movddup
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqa32
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_movdqu32
        unchecked((ulong)(BAD_CODE)), // INS_movhlps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x17)))))), // INS_movhpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x17)))))), // INS_movhps
        unchecked((ulong)(BAD_CODE)), // INS_movlhps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x13)))))), // INS_movlpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x13)))))), // INS_movlps
        unchecked((ulong)(BAD_CODE)), // INS_movmskpd
        unchecked((ulong)(BAD_CODE)), // INS_movmskps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xE7)))))), // INS_movntdq
        unchecked((ulong)(BAD_CODE)), // INS_movntdqa
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti32
        unchecked((ulong)(((((0x0f)) << 16) | (((0xC3)))))), // INS_movnti64
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x2B)))))), // INS_movntpd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x2B)))))), // INS_movntps
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_movq
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movsd_simd
        unchecked((ulong)(BAD_CODE)), // INS_movshdup
        unchecked((ulong)(BAD_CODE)), // INS_movsldup
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x11)))))), // INS_movupd
        unchecked((ulong)(((((0x0f)) << 16) | (((0x11)))))), // INS_movups
        unchecked((ulong)(BAD_CODE)), // INS_mpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_mulpd
        unchecked((ulong)(BAD_CODE)), // INS_mulps
        unchecked((ulong)(BAD_CODE)), // INS_mulsd
        unchecked((ulong)(BAD_CODE)), // INS_mulss
        unchecked((ulong)(BAD_CODE)), // INS_orpd
        unchecked((ulong)(BAD_CODE)), // INS_orps
        unchecked((ulong)(BAD_CODE)), // INS_pabsb
        unchecked((ulong)(BAD_CODE)), // INS_pabsd
        unchecked((ulong)(BAD_CODE)), // INS_pabsw
        unchecked((ulong)(BAD_CODE)), // INS_packssdw
        unchecked((ulong)(BAD_CODE)), // INS_packsswb
        unchecked((ulong)(BAD_CODE)), // INS_packusdw
        unchecked((ulong)(BAD_CODE)), // INS_packuswb
        unchecked((ulong)(BAD_CODE)), // INS_paddb
        unchecked((ulong)(BAD_CODE)), // INS_paddd
        unchecked((ulong)(BAD_CODE)), // INS_paddq
        unchecked((ulong)(BAD_CODE)), // INS_paddsb
        unchecked((ulong)(BAD_CODE)), // INS_paddsw
        unchecked((ulong)(BAD_CODE)), // INS_paddusb
        unchecked((ulong)(BAD_CODE)), // INS_paddusw
        unchecked((ulong)(BAD_CODE)), // INS_paddw
        unchecked((ulong)(BAD_CODE)), // INS_palignr
        unchecked((ulong)(BAD_CODE)), // INS_pandd
        unchecked((ulong)(BAD_CODE)), // INS_pandnd
        unchecked((ulong)(BAD_CODE)), // INS_pavgb
        unchecked((ulong)(BAD_CODE)), // INS_pavgw
        unchecked((ulong)(BAD_CODE)), // INS_pblendvb
        unchecked((ulong)(BAD_CODE)), // INS_pblendw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_pcmpgtw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x14)))) << 8)))), // INS_pextrb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x16)))) << 8)))), // INS_pextrq
        unchecked((ulong)(BAD_CODE)), // INS_phaddd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x15)))) << 8)))), // INS_pextrw
        unchecked((ulong)(BAD_CODE)), // INS_phaddsw
        unchecked((ulong)(BAD_CODE)), // INS_phaddw
        unchecked((ulong)(BAD_CODE)), // INS_phminposuw
        unchecked((ulong)(BAD_CODE)), // INS_phsubd
        unchecked((ulong)(BAD_CODE)), // INS_phsubsw
        unchecked((ulong)(BAD_CODE)), // INS_phsubw
        unchecked((ulong)(BAD_CODE)), // INS_pinsrb
        unchecked((ulong)(BAD_CODE)), // INS_pinsrd
        unchecked((ulong)(BAD_CODE)), // INS_pinsrq
        unchecked((ulong)(BAD_CODE)), // INS_pinsrw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddubsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaddwd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsb
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_pmaxsw
        unchecked((ulong)(BAD_CODE)), // INS_pmaxub
        unchecked((ulong)(BAD_CODE)), // INS_pmaxud
        unchecked((ulong)(BAD_CODE)), // INS_pmaxuw
        unchecked((ulong)(BAD_CODE)), // INS_pminsb
        unchecked((ulong)(BAD_CODE)), // INS_pminsd
        unchecked((ulong)(BAD_CODE)), // INS_pminsw
        unchecked((ulong)(BAD_CODE)), // INS_pminub
        unchecked((ulong)(BAD_CODE)), // INS_pminud
        unchecked((ulong)(BAD_CODE)), // INS_pminuw
        unchecked((ulong)(BAD_CODE)), // INS_pmovmskb
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovsxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxbw
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxdq
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwd
        unchecked((ulong)(BAD_CODE)), // INS_pmovzxwq
        unchecked((ulong)(BAD_CODE)), // INS_pmuldq
        unchecked((ulong)(BAD_CODE)), // INS_pmulhrsw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhuw
        unchecked((ulong)(BAD_CODE)), // INS_pmulhw
        unchecked((ulong)(BAD_CODE)), // INS_pmulld
        unchecked((ulong)(BAD_CODE)), // INS_pmuludq
        unchecked((ulong)(BAD_CODE)), // INS_pmullw
        unchecked((ulong)(BAD_CODE)), // INS_pord
        unchecked((ulong)(0x000F0018)), // INS_prefetchnta
        unchecked((ulong)(0x000F0818)), // INS_prefetcht0
        unchecked((ulong)(0x000F1018)), // INS_prefetcht1
        unchecked((ulong)(0x000F1818)), // INS_prefetcht2
        unchecked((ulong)(BAD_CODE)), // INS_psadbw
        unchecked((ulong)(BAD_CODE)), // INS_pshufb
        unchecked((ulong)(BAD_CODE)), // INS_pshufd
        unchecked((ulong)(BAD_CODE)), // INS_pshufhw
        unchecked((ulong)(BAD_CODE)), // INS_pshuflw
        unchecked((ulong)(BAD_CODE)), // INS_psignb
        unchecked((ulong)(BAD_CODE)), // INS_psignd
        unchecked((ulong)(BAD_CODE)), // INS_psignw
        unchecked((ulong)(BAD_CODE)), // INS_pslld
        unchecked((ulong)(BAD_CODE)), // INS_pslldq
        unchecked((ulong)(BAD_CODE)), // INS_psllq
        unchecked((ulong)(BAD_CODE)), // INS_psllw
        unchecked((ulong)(BAD_CODE)), // INS_psrad
        unchecked((ulong)(BAD_CODE)), // INS_psraw
        unchecked((ulong)(BAD_CODE)), // INS_psrld
        unchecked((ulong)(BAD_CODE)), // INS_psrldq
        unchecked((ulong)(BAD_CODE)), // INS_psrlq
        unchecked((ulong)(BAD_CODE)), // INS_psrlw
        unchecked((ulong)(BAD_CODE)), // INS_psubb
        unchecked((ulong)(BAD_CODE)), // INS_psubd
        unchecked((ulong)(BAD_CODE)), // INS_psubq
        unchecked((ulong)(BAD_CODE)), // INS_psubsb
        unchecked((ulong)(BAD_CODE)), // INS_psubsw
        unchecked((ulong)(BAD_CODE)), // INS_psubusb
        unchecked((ulong)(BAD_CODE)), // INS_psubusw
        unchecked((ulong)(BAD_CODE)), // INS_psubw
        unchecked((ulong)(BAD_CODE)), // INS_ptest
        unchecked((ulong)(BAD_CODE)), // INS_punpckhbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckhdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpckhwd
        unchecked((ulong)(BAD_CODE)), // INS_punpcklbw
        unchecked((ulong)(BAD_CODE)), // INS_punpckldq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklqdq
        unchecked((ulong)(BAD_CODE)), // INS_punpcklwd
        unchecked((ulong)(BAD_CODE)), // INS_pxord
        unchecked((ulong)(BAD_CODE)), // INS_rcpps
        unchecked((ulong)(BAD_CODE)), // INS_rcpss
        unchecked((ulong)(BAD_CODE)), // INS_roundpd
        unchecked((ulong)(BAD_CODE)), // INS_roundps
        unchecked((ulong)(BAD_CODE)), // INS_roundsd
        unchecked((ulong)(BAD_CODE)), // INS_roundss
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtps
        unchecked((ulong)(BAD_CODE)), // INS_rsqrtss
        unchecked((ulong)(0x000FF8AE)), // INS_sfence
        unchecked((ulong)(BAD_CODE)), // INS_shufpd
        unchecked((ulong)(BAD_CODE)), // INS_shufps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtpd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtps
        unchecked((ulong)(BAD_CODE)), // INS_sqrtsd
        unchecked((ulong)(BAD_CODE)), // INS_sqrtss
        unchecked((ulong)(BAD_CODE)), // INS_subpd
        unchecked((ulong)(BAD_CODE)), // INS_subps
        unchecked((ulong)(BAD_CODE)), // INS_subsd
        unchecked((ulong)(BAD_CODE)), // INS_subss
        unchecked((ulong)(BAD_CODE)), // INS_ucomisd
        unchecked((ulong)(BAD_CODE)), // INS_ucomiss
        unchecked((ulong)(BAD_CODE)), // INS_unpckhpd
        unchecked((ulong)(BAD_CODE)), // INS_unpckhps
        unchecked((ulong)(BAD_CODE)), // INS_unpcklpd
        unchecked((ulong)(BAD_CODE)), // INS_unpcklps
        unchecked((ulong)(BAD_CODE)), // INS_xorpd
        unchecked((ulong)(BAD_CODE)), // INS_xorps
        unchecked((ulong)(BAD_CODE)), // INS_aesdec
        unchecked((ulong)(BAD_CODE)), // INS_aesdeclast
        unchecked((ulong)(BAD_CODE)), // INS_aesenc
        unchecked((ulong)(BAD_CODE)), // INS_aesenclast
        unchecked((ulong)(BAD_CODE)), // INS_aesimc
        unchecked((ulong)(BAD_CODE)), // INS_aeskeygenassist
        unchecked((ulong)(BAD_CODE)), // INS_pclmulqdq
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha1msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha1nexte
        unchecked((ulong)(BAD_CODE)), // INS_sha1rnds4
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg1
        unchecked((ulong)(BAD_CODE)), // INS_sha256msg2
        unchecked((ulong)(BAD_CODE)), // INS_sha256rnds2
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineinvqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8affineqb
        unchecked((ulong)(BAD_CODE)), // INS_gf2p8mulb
        unchecked((ulong)(BAD_CODE)), // INS_vblendvpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendvps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastsd
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf32x4
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2F)))) << 8)))), // INS_vmaskmovpd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x2E)))) << 8)))), // INS_vmaskmovps
        unchecked((ulong)(BAD_CODE)), // INS_vpblendvb
        unchecked((ulong)(BAD_CODE)), // INS_vperm2f128
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpdvar
        unchecked((ulong)(BAD_CODE)), // INS_vpermilps
        unchecked((ulong)(BAD_CODE)), // INS_vpermilpsvar
        unchecked((ulong)(BAD_CODE)), // INS_vtestpd
        unchecked((ulong)(BAD_CODE)), // INS_vtestps
        unchecked((ulong)(0xC577F8)), // INS_vzeroupper
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2ps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1D)))) << 8)))), // INS_vcvtps2ph
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x4
        unchecked((ulong)(BAD_CODE)), // INS_vpblendd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw
        unchecked((ulong)(BAD_CODE)), // INS_vperm2i128
        unchecked((ulong)(BAD_CODE)), // INS_vpermd
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd
        unchecked((ulong)(BAD_CODE)), // INS_vpermps
        unchecked((ulong)(BAD_CODE)), // INS_vpermq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8E)))) << 8)))), // INS_vpmaskmovq
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvd
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvq
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231pd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ps
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sd
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ss
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ss
        unchecked((ulong)(BAD_CODE)), // INS_andn
        unchecked((ulong)(BAD_CODE)), // INS_bextr
        unchecked((ulong)(BAD_CODE)), // INS_blsi
        unchecked((ulong)(BAD_CODE)), // INS_blsmsk
        unchecked((ulong)(BAD_CODE)), // INS_blsr
        unchecked((ulong)(BAD_CODE)), // INS_bzhi
        unchecked((ulong)(BAD_CODE)), // INS_mulx
        unchecked((ulong)(BAD_CODE)), // INS_pdep
        unchecked((ulong)(BAD_CODE)), // INS_pext
        unchecked((ulong)(BAD_CODE)), // INS_rorx
        unchecked((ulong)(BAD_CODE)), // INS_sarx
        unchecked((ulong)(BAD_CODE)), // INS_shlx
        unchecked((ulong)(BAD_CODE)), // INS_shrx
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwusds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpwuuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssd
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbssds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbsuds
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuud
        unchecked((ulong)(BAD_CODE)), // INS_vpdpbuuds
        unchecked((ulong)(BAD_CODE)), // INS_vbmacor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbmacxor16x16x16
        unchecked((ulong)(BAD_CODE)), // INS_vbitrev
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52huq
        unchecked((ulong)(BAD_CODE)), // INS_vpmadd52luq
        unchecked((ulong)(BAD_CODE)), // INS_kaddb
        unchecked((ulong)(BAD_CODE)), // INS_kaddd
        unchecked((ulong)(BAD_CODE)), // INS_kaddq
        unchecked((ulong)(BAD_CODE)), // INS_kaddw
        unchecked((ulong)(BAD_CODE)), // INS_kandb
        unchecked((ulong)(BAD_CODE)), // INS_kandd
        unchecked((ulong)(BAD_CODE)), // INS_kandnb
        unchecked((ulong)(BAD_CODE)), // INS_kandnd
        unchecked((ulong)(BAD_CODE)), // INS_kandnq
        unchecked((ulong)(BAD_CODE)), // INS_kandnw
        unchecked((ulong)(BAD_CODE)), // INS_kandq
        unchecked((ulong)(BAD_CODE)), // INS_kandw
        unchecked((ulong)(BAD_CODE)), // INS_kmovb_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovb_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovd_gpr
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x91)))))), // INS_kmovd_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovq_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovq_msk
        unchecked((ulong)(BAD_CODE)), // INS_kmovw_gpr
        unchecked((ulong)(((((0x0f)) << 16) | (((0x91)))))), // INS_kmovw_msk
        unchecked((ulong)(BAD_CODE)), // INS_knotb
        unchecked((ulong)(BAD_CODE)), // INS_knotd
        unchecked((ulong)(BAD_CODE)), // INS_knotq
        unchecked((ulong)(BAD_CODE)), // INS_knotw
        unchecked((ulong)(BAD_CODE)), // INS_korb
        unchecked((ulong)(BAD_CODE)), // INS_kord
        unchecked((ulong)(BAD_CODE)), // INS_korq
        unchecked((ulong)(BAD_CODE)), // INS_kortestb
        unchecked((ulong)(BAD_CODE)), // INS_kortestd
        unchecked((ulong)(BAD_CODE)), // INS_kortestq
        unchecked((ulong)(BAD_CODE)), // INS_kortestw
        unchecked((ulong)(BAD_CODE)), // INS_korw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftld
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftlw
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrb
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrd
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrq
        unchecked((ulong)(BAD_CODE)), // INS_kshiftrw
        unchecked((ulong)(BAD_CODE)), // INS_ktestb
        unchecked((ulong)(BAD_CODE)), // INS_ktestd
        unchecked((ulong)(BAD_CODE)), // INS_ktestq
        unchecked((ulong)(BAD_CODE)), // INS_ktestw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckbw
        unchecked((ulong)(BAD_CODE)), // INS_kunpckdq
        unchecked((ulong)(BAD_CODE)), // INS_kunpckwd
        unchecked((ulong)(BAD_CODE)), // INS_kxnorb
        unchecked((ulong)(BAD_CODE)), // INS_kxnord
        unchecked((ulong)(BAD_CODE)), // INS_kxnorq
        unchecked((ulong)(BAD_CODE)), // INS_kxnorw
        unchecked((ulong)(BAD_CODE)), // INS_kxorb
        unchecked((ulong)(BAD_CODE)), // INS_kxord
        unchecked((ulong)(BAD_CODE)), // INS_kxorq
        unchecked((ulong)(BAD_CODE)), // INS_kxorw
        unchecked((ulong)(BAD_CODE)), // INS_valignd
        unchecked((ulong)(BAD_CODE)), // INS_valignq
        unchecked((ulong)(BAD_CODE)), // INS_vblendmpd
        unchecked((ulong)(BAD_CODE)), // INS_vblendmps
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcastf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vbroadcasti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vcmppd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpps
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsd
        unchecked((ulong)(BAD_CODE)), // INS_vcmpss
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompresspd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8A)))) << 8)))), // INS_vcompressps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ps
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sd64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2ss64
        unchecked((ulong)(BAD_CODE)), // INS_vdbpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vexpandpd
        unchecked((ulong)(BAD_CODE)), // INS_vexpandps
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x19)))) << 8)))), // INS_vextractf64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x1B)))) << 8)))), // INS_vextractf64x4
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti32x8
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x39)))) << 8)))), // INS_vextracti64x2
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x3A)) | (((((0x3B)))) << 8)))), // INS_vextracti64x4
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmpd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmps
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmsd
        unchecked((ulong)(BAD_CODE)), // INS_vfixupimmss
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasspd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassps
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssd
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassss
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgatherqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vgetexppd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpps
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpss
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantpd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantps
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsd
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantss
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinsertf64x4
        unchecked((ulong)(BAD_CODE)), // INS_vinserti32x8
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x2
        unchecked((ulong)(BAD_CODE)), // INS_vinserti64x4
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqa64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu16
        unchecked((ulong)(((((0xf3)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu64
        unchecked((ulong)(((((0xf2)) << 16) | (((0x0f)) << 24) | (((0x7F)))))), // INS_vmovdqu8
        unchecked((ulong)(BAD_CODE)), // INS_vpabsq
        unchecked((ulong)(BAD_CODE)), // INS_vpandnq
        unchecked((ulong)(BAD_CODE)), // INS_vpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmb
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmd
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmq
        unchecked((ulong)(BAD_CODE)), // INS_vpblendmw
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastb_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastd_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastq_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpbroadcastw_gpr
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpeqw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtb
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtd
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpgtw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpub
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpud
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuq
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpuw
        unchecked((ulong)(BAD_CODE)), // INS_vpcmpw
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressd
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x8B)))) << 8)))), // INS_vpcompressq
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictd
        unchecked((ulong)(BAD_CODE)), // INS_vpconflictq
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermpd_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermq_reg
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2d
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2pd
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2ps
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2q
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2w
        unchecked((ulong)(BAD_CODE)), // INS_vpermw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandd
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandq
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpgatherqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntd
        unchecked((ulong)(BAD_CODE)), // INS_vplzcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxsq
        unchecked((ulong)(BAD_CODE)), // INS_vpmaxuq
        unchecked((ulong)(BAD_CODE)), // INS_vpminsq
        unchecked((ulong)(BAD_CODE)), // INS_vpminuq
        unchecked((ulong)(BAD_CODE)), // INS_vpmovb2m
        unchecked((ulong)(BAD_CODE)), // INS_vpmovd2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x31))) << 8)))), // INS_vpmovdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x33))) << 8)))), // INS_vpmovdw
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2d
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2q
        unchecked((ulong)(BAD_CODE)), // INS_vpmovm2w
        unchecked((ulong)(BAD_CODE)), // INS_vpmovq2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x32))) << 8)))), // INS_vpmovqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x35))) << 8)))), // INS_vpmovqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x34))) << 8)))), // INS_vpmovqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x21))) << 8)))), // INS_vpmovsdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x23))) << 8)))), // INS_vpmovsdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x22))) << 8)))), // INS_vpmovsqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x25))) << 8)))), // INS_vpmovsqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x24))) << 8)))), // INS_vpmovsqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x20))) << 8)))), // INS_vpmovswb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x11))) << 8)))), // INS_vpmovusdb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x13))) << 8)))), // INS_vpmovusdw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x12))) << 8)))), // INS_vpmovusqb
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x15))) << 8)))), // INS_vpmovusqd
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x14))) << 8)))), // INS_vpmovusqw
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x10))) << 8)))), // INS_vpmovuswb
        unchecked((ulong)(BAD_CODE)), // INS_vpmovw2m
        unchecked((ulong)((((((0xF3))) << 16) | (((0x0f)) << 24) | ((0x38)) | ((((0x30))) << 8)))), // INS_vpmovwb
        unchecked((ulong)(BAD_CODE)), // INS_vpmullq
        unchecked((ulong)(BAD_CODE)), // INS_vporq
        unchecked((ulong)(BAD_CODE)), // INS_vprold
        unchecked((ulong)(BAD_CODE)), // INS_vprolq
        unchecked((ulong)(BAD_CODE)), // INS_vprolvd
        unchecked((ulong)(BAD_CODE)), // INS_vprolvq
        unchecked((ulong)(BAD_CODE)), // INS_vprord
        unchecked((ulong)(BAD_CODE)), // INS_vprorq
        unchecked((ulong)(BAD_CODE)), // INS_vprorvd
        unchecked((ulong)(BAD_CODE)), // INS_vprorvq
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterdq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpscatterqq_msk
        unchecked((ulong)(BAD_CODE)), // INS_vpsllvw
        unchecked((ulong)(BAD_CODE)), // INS_vpsraq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravq
        unchecked((ulong)(BAD_CODE)), // INS_vpsravw
        unchecked((ulong)(BAD_CODE)), // INS_vpsrlvw
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogd
        unchecked((ulong)(BAD_CODE)), // INS_vpternlogq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestmw
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmb
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmd
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmq
        unchecked((ulong)(BAD_CODE)), // INS_vptestnmw
        unchecked((ulong)(BAD_CODE)), // INS_vpxorq
        unchecked((ulong)(BAD_CODE)), // INS_vrangepd
        unchecked((ulong)(BAD_CODE)), // INS_vrangeps
        unchecked((ulong)(BAD_CODE)), // INS_vrangesd
        unchecked((ulong)(BAD_CODE)), // INS_vrangess
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrcp14ss
        unchecked((ulong)(BAD_CODE)), // INS_vreducepd
        unchecked((ulong)(BAD_CODE)), // INS_vreduceps
        unchecked((ulong)(BAD_CODE)), // INS_vreducesd
        unchecked((ulong)(BAD_CODE)), // INS_vreducess
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalepd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleps
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesd
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaless
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14pd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ps
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14sd
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrt14ss
        unchecked((ulong)(BAD_CODE)), // INS_vscalefpd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefps
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsd
        unchecked((ulong)(BAD_CODE)), // INS_vscalefss
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterdps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqpd_msk
        unchecked((ulong)(BAD_CODE)), // INS_vscatterqps_msk
        unchecked((ulong)(BAD_CODE)), // INS_vshuff32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshuff64x2
        unchecked((ulong)(BAD_CODE)), // INS_vshufi32x4
        unchecked((ulong)(BAD_CODE)), // INS_vshufi64x2
        unchecked((ulong)(BAD_CODE)), // INS_vpermb
        unchecked((ulong)(BAD_CODE)), // INS_vpermi2b
        unchecked((ulong)(BAD_CODE)), // INS_vpermt2b
        unchecked((ulong)(BAD_CODE)), // INS_vpmultishiftqb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressb
        unchecked((ulong)((((((0x66))) << 16) | (((0x0f)) << 24) | ((0x38)) | (((((0x63)))) << 8)))), // INS_vpcompressw
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandb
        unchecked((ulong)(BAD_CODE)), // INS_vpexpandw
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntb
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntd
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntq
        unchecked((ulong)(BAD_CODE)), // INS_vpopcntw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshldvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshldw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvd
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvq
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdvw
        unchecked((ulong)(BAD_CODE)), // INS_vpshrdw
        unchecked((ulong)(BAD_CODE)), // INS_vpshufbitqmb
        unchecked((ulong)(BAD_CODE)), // INS_vaddph
        unchecked((ulong)(BAD_CODE)), // INS_vaddsh
        unchecked((ulong)(BAD_CODE)), // INS_vcmpph
        unchecked((ulong)(BAD_CODE)), // INS_vcmpsh
        unchecked((ulong)(BAD_CODE)), // INS_vcomish
        unchecked((ulong)(BAD_CODE)), // INS_vcvtdq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtne2ps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtneps2bf16
        unchecked((ulong)(BAD_CODE)), // INS_vcvtpd2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2pd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2psx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvtph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2phx
        unchecked((ulong)(BAD_CODE)), // INS_vcvtqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsd2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2sd
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2ss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtsi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtss2sh
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2dq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2qq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2udq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uqq
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2uw
        unchecked((ulong)(BAD_CODE)), // INS_vcvttph2w
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2si64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsh2usi64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtudq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuqq2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh32
        unchecked((ulong)(BAD_CODE)), // INS_vcvtusi2sh64
        unchecked((ulong)(BAD_CODE)), // INS_vcvtuw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vcvtw2ph
        unchecked((ulong)(BAD_CODE)), // INS_vdivph
        unchecked((ulong)(BAD_CODE)), // INS_vdivsh
        unchecked((ulong)(BAD_CODE)), // INS_vdpbf16ps
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfcmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmulcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddcsh
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmaddsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfmsubadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmadd231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231ph
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub132sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub213sh
        unchecked((ulong)(BAD_CODE)), // INS_vfnmsub231sh
        unchecked((ulong)(BAD_CODE)), // INS_vfpclassph
        unchecked((ulong)(BAD_CODE)), // INS_vfpclasssh
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpph
        unchecked((ulong)(BAD_CODE)), // INS_vgetexpsh
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantph
        unchecked((ulong)(BAD_CODE)), // INS_vgetmantsh
        unchecked((ulong)(BAD_CODE)), // INS_vmaxph
        unchecked((ulong)(BAD_CODE)), // INS_vmaxsh
        unchecked((ulong)(BAD_CODE)), // INS_vminsh
        unchecked((ulong)(BAD_CODE)), // INS_vminph
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x11)))))), // INS_vmovsh
        unchecked((ulong)(((((0x66)) << 16) | ((((0x06))) << 24) | (((0x7E)))))), // INS_vmovw
        unchecked((ulong)(BAD_CODE)), // INS_vmulph
        unchecked((ulong)(BAD_CODE)), // INS_vmulsh
        unchecked((ulong)(BAD_CODE)), // INS_vrcpph
        unchecked((ulong)(BAD_CODE)), // INS_vrcpsh
        unchecked((ulong)(BAD_CODE)), // INS_vreduceph
        unchecked((ulong)(BAD_CODE)), // INS_vreducesh
        unchecked((ulong)(BAD_CODE)), // INS_vrndscaleph
        unchecked((ulong)(BAD_CODE)), // INS_vrndscalesh
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vrsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vscalefph
        unchecked((ulong)(BAD_CODE)), // INS_vscalefsh
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtph
        unchecked((ulong)(BAD_CODE)), // INS_vsqrtsh
        unchecked((ulong)(BAD_CODE)), // INS_vsubph
        unchecked((ulong)(BAD_CODE)), // INS_vsubsh
        unchecked((ulong)(BAD_CODE)), // INS_vucomish
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectd
        unchecked((ulong)(BAD_CODE)), // INS_vp2intersectq
        unchecked((ulong)(BAD_CODE)), // INS_vcomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vcomxss
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvtps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttpd2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2dqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2ibs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2iubs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2qqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2udqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttps2uqqs
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttsd2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2sis64
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis32
        unchecked((ulong)(BAD_CODE)), // INS_vcvttss2usis64
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxpd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxps
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxsd
        unchecked((ulong)(BAD_CODE)), // INS_vminmaxss
        unchecked((ulong)(((((0x66)) << 16) | (((0x0f)) << 24) | (((0xD6)))))), // INS_vmovd_simd
        unchecked((ulong)(((((0xf3)) << 16) | ((((0x05))) << 24) | (((0x7E)))))), // INS_vmovw_simd
        unchecked((ulong)(BAD_CODE)), // INS_vmpsadbw
        unchecked((ulong)(BAD_CODE)), // INS_vucomxsd
        unchecked((ulong)(BAD_CODE)), // INS_vucomxss
        unchecked((ulong)(0x000038)), // INS_ccmpo
        unchecked((ulong)(0x000038)), // INS_ccmpno
        unchecked((ulong)(0x000038)), // INS_ccmpb
        unchecked((ulong)(0x000038)), // INS_ccmpae
        unchecked((ulong)(0x000038)), // INS_ccmpe
        unchecked((ulong)(0x000038)), // INS_ccmpne
        unchecked((ulong)(0x000038)), // INS_ccmpbe
        unchecked((ulong)(0x000038)), // INS_ccmpa
        unchecked((ulong)(0x000038)), // INS_ccmps
        unchecked((ulong)(0x000038)), // INS_ccmpns
        unchecked((ulong)(0x000038)), // INS_ccmpt
        unchecked((ulong)(0x000038)), // INS_ccmpf
        unchecked((ulong)(0x000038)), // INS_ccmpl
        unchecked((ulong)(0x000038)), // INS_ccmpge
        unchecked((ulong)(0x000038)), // INS_ccmple
        unchecked((ulong)(0x000038)), // INS_ccmpg
        unchecked((ulong)(0x000040)), // INS_cfcmovo
        unchecked((ulong)(0x000041)), // INS_cfcmovno
        unchecked((ulong)(0x000042)), // INS_cfcmovb
        unchecked((ulong)(0x000043)), // INS_cfcmovae
        unchecked((ulong)(0x000044)), // INS_cfcmove
        unchecked((ulong)(0x000045)), // INS_cfcmovne
        unchecked((ulong)(0x000046)), // INS_cfcmovbe
        unchecked((ulong)(0x000047)), // INS_cfcmova
        unchecked((ulong)(0x000048)), // INS_cfcmovs
        unchecked((ulong)(0x000049)), // INS_cfcmovns
        unchecked((ulong)(0x00004A)), // INS_cfcmovp
        unchecked((ulong)(0x00004B)), // INS_cfcmovnp
        unchecked((ulong)(0x00004C)), // INS_cfcmovl
        unchecked((ulong)(0x00004D)), // INS_cfcmovge
        unchecked((ulong)(0x00004E)), // INS_cfcmovle
        unchecked((ulong)(0x00004F)), // INS_cfcmovg
        unchecked((ulong)(0x000084)), // INS_ctesto
        unchecked((ulong)(0x000084)), // INS_ctestno
        unchecked((ulong)(0x000084)), // INS_ctestb
        unchecked((ulong)(0x000084)), // INS_ctestae
        unchecked((ulong)(0x000084)), // INS_cteste
        unchecked((ulong)(0x000084)), // INS_ctestne
        unchecked((ulong)(0x000084)), // INS_ctestbe
        unchecked((ulong)(0x000084)), // INS_ctesta
        unchecked((ulong)(0x000084)), // INS_ctests
        unchecked((ulong)(0x000084)), // INS_ctestns
        unchecked((ulong)(0x000084)), // INS_ctestt
        unchecked((ulong)(0x000084)), // INS_ctestf
        unchecked((ulong)(0x000084)), // INS_ctestl
        unchecked((ulong)(0x000084)), // INS_ctestge
        unchecked((ulong)(0x000084)), // INS_ctestle
        unchecked((ulong)(0x000084)), // INS_ctestg
        unchecked((ulong)(BAD_CODE)), // INS_crc32_apx
        unchecked((ulong)(0x000061)), // INS_movbe_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x40)))))), // INS_seto_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x41)))))), // INS_setno_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x42)))))), // INS_setb_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x43)))))), // INS_setae_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x44)))))), // INS_sete_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x45)))))), // INS_setne_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x46)))))), // INS_setbe_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x47)))))), // INS_seta_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x48)))))), // INS_sets_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x49)))))), // INS_setns_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4A)))))), // INS_setp_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4B)))))), // INS_setnp_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4C)))))), // INS_setl_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4D)))))), // INS_setge_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4E)))))), // INS_setle_apx
        unchecked((ulong)(((((0xf2)) << 16) | ((((4))) << 24) | (((0x4F)))))), // INS_setg_apx
        unchecked((ulong)(BAD_CODE)), // INS_crc32
        unchecked((ulong)(BAD_CODE)), // INS_tzcnt
        unchecked((ulong)(BAD_CODE)), // INS_tzcnt_apx
        unchecked((ulong)(BAD_CODE)), // INS_lzcnt
        unchecked((ulong)(BAD_CODE)), // INS_lzcnt_apx
        unchecked((ulong)(((((0x0F)) << 16) | (((0x38)) << 24) | (((0xF1)))))), // INS_movbe
        unchecked((ulong)(BAD_CODE)), // INS_popcnt
        unchecked((ulong)(BAD_CODE)), // INS_popcnt_apx
        unchecked((ulong)(BAD_CODE)), // INS_tpause
        unchecked((ulong)(BAD_CODE)), // INS_umonitor
        unchecked((ulong)(BAD_CODE)), // INS_umwait
        unchecked((ulong)(0x0018F6)), // INS_neg
        unchecked((ulong)(0x0010F6)), // INS_not
        unchecked((ulong)(0x0000D2)), // INS_rol
        unchecked((ulong)(0x0000D0)), // INS_rol_1
        unchecked((ulong)(0x0000C0)), // INS_rol_N
        unchecked((ulong)(0x0008D2)), // INS_ror
        unchecked((ulong)(0x0008D0)), // INS_ror_1
        unchecked((ulong)(0x0008C0)), // INS_ror_N
        unchecked((ulong)(0x0010D2)), // INS_rcl
        unchecked((ulong)(0x0010D0)), // INS_rcl_1
        unchecked((ulong)(0x0010C0)), // INS_rcl_N
        unchecked((ulong)(0x0018D2)), // INS_rcr
        unchecked((ulong)(0x0018D0)), // INS_rcr_1
        unchecked((ulong)(0x0018C0)), // INS_rcr_N
        unchecked((ulong)(0x0020D2)), // INS_shl
        unchecked((ulong)(0x0020D0)), // INS_shl_1
        unchecked((ulong)(0x0020C0)), // INS_shl_N
        unchecked((ulong)(0x0028D2)), // INS_shr
        unchecked((ulong)(0x0028D0)), // INS_shr_1
        unchecked((ulong)(0x0028C0)), // INS_shr_N
        unchecked((ulong)(0x0038D2)), // INS_sar
        unchecked((ulong)(0x0038D0)), // INS_sar_1
        unchecked((ulong)(0x0038C0)), // INS_sar_N
        unchecked((ulong)(0x0000C3)), // INS_ret
        unchecked((ulong)(BAD_CODE)), // INS_loop
        unchecked((ulong)(0x0010FF)), // INS_call
        unchecked((ulong)(0x00A4F3)), // INS_r_movsb
        unchecked((ulong)(0x00A5F3)), // INS_r_movsd
        unchecked((ulong)(0xF3A548)), // INS_r_movsq
        unchecked((ulong)(0x0000A4)), // INS_movsb
        unchecked((ulong)(0x0000A5)), // INS_movsd
        unchecked((ulong)(0x00A548)), // INS_movsq
        unchecked((ulong)(0x00AAF3)), // INS_r_stosb
        unchecked((ulong)(0x00ABF3)), // INS_r_stosd
        unchecked((ulong)(0xF3AB48)), // INS_r_stosq
        unchecked((ulong)(0x0000AA)), // INS_stosb
        unchecked((ulong)(0x0000AB)), // INS_stosd
        unchecked((ulong)(0x00AB48)), // INS_stosq
        unchecked((ulong)(0x0000CC)), // INS_int3
        unchecked((ulong)(0x000090)), // INS_nop
        unchecked((ulong)(0x0090F3)), // INS_pause
        unchecked((ulong)(0x0000F0)), // INS_lock
        unchecked((ulong)(0x0000C9)), // INS_leave
        unchecked((ulong)(0x0fe801)), // INS_serialize
        unchecked((ulong)(0x000098)), // INS_cwde
        unchecked((ulong)(0x000099)), // INS_cdq
        unchecked((ulong)(0x0038F6)), // INS_idiv
        unchecked((ulong)(0x0028F6)), // INS_imulEAX
        unchecked((ulong)(0x0030F6)), // INS_div
        unchecked((ulong)(0x0020F6)), // INS_mulEAX
        unchecked((ulong)(0x00009E)), // INS_sahf
        unchecked((ulong)(0x0F00C0)), // INS_xadd
        unchecked((ulong)(0x0F00B0)), // INS_cmpxchg
        unchecked((ulong)(0x0F00A4)), // INS_shld
        unchecked((ulong)(0x0F00AC)), // INS_shrd
        unchecked((ulong)(0x0F0090)), // INS_seto
        unchecked((ulong)(0x0F0091)), // INS_setno
        unchecked((ulong)(0x0F0092)), // INS_setb
        unchecked((ulong)(0x0F0093)), // INS_setae
        unchecked((ulong)(0x0F0094)), // INS_sete
        unchecked((ulong)(0x0F0095)), // INS_setne
        unchecked((ulong)(0x0F0096)), // INS_setbe
        unchecked((ulong)(0x0F0097)), // INS_seta
        unchecked((ulong)(0x0F0098)), // INS_sets
        unchecked((ulong)(0x0F0099)), // INS_setns
        unchecked((ulong)(0x0F009A)), // INS_setp
        unchecked((ulong)(0x0F009B)), // INS_setnp
        unchecked((ulong)(0x0F009C)), // INS_setl
        unchecked((ulong)(0x0F009D)), // INS_setge
        unchecked((ulong)(0x0F009E)), // INS_setle
        unchecked((ulong)(0x0F009F)), // INS_setg
        unchecked((ulong)(0x0020FF)), // INS_tail_i_jmp
        unchecked((ulong)(0x0020FF)), // INS_i_jmp
    ];

    private static ReadOnlySpan<ulong> insCodesMR =>
        Configuration.TARGET_X86 ? insCodesMRX86 :
        Configuration.TARGET_AMD64 ? insCodesMRX64 :
        throw new FatalJitException("xarch opcode table is not available for this target.");


}