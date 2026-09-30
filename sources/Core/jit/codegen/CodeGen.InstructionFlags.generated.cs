// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_XARCH
    internal static ReadOnlySpan<insFlags> instInfo => [
#if TARGET_XARCH
#if !TARGET_XARCH
#error Unexpected target type
#endif
        INS_FLAGS_None, // INS_invalid
        Encoding_REX2, // INS_push
        Encoding_REX2, // INS_pop
        Encoding_REX2, // INS_push_hide
        Encoding_REX2, // INS_pop_hide
        Encoding_EVEX_APX_ONLY | INS_FLAGS_HasNDD, // INS_push2
        Encoding_EVEX_APX_ONLY | INS_FLAGS_HasNDD, // INS_pop2
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_inc
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Encoding_REX2 | INS_FLAGS_HasNF, // INS_inc_l
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_dec
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Encoding_REX2 | INS_FLAGS_HasNF, // INS_dec_l
        Encoding_REX2, // INS_bswap
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_add
        Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_or
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | Reads_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2, // INS_adc
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | Reads_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2, // INS_sbb
        Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_and
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_sub
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2, // INS_sub_hide
        Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_xor
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2, // INS_cmp
        Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Resets_CF | INS_FLAGS_HasWBit | Encoding_REX2, // INS_test
        INS_FLAGS_HasWBit | Encoding_REX2, // INS_mov
        Encoding_REX2, // INS_lea
        Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2, // INS_bt
        Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2, // INS_bts
        Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2, // INS_btr
        Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2, // INS_btc
        Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Undefined_CF | Encoding_REX2, // INS_bsr
        Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Undefined_CF | Encoding_REX2, // INS_bsf
        INS_FLAGS_HasWBit | Encoding_REX2, // INS_movsx
#if TARGET_AMD64
        REX_W1 | Encoding_REX2, // INS_movsxd
#endif
        INS_FLAGS_HasWBit | Encoding_REX2, // INS_movzx
        Reads_OF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovo
        Reads_OF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovno
        Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovb
        Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovae
        Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmove
        Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovne
        Reads_ZF | Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovbe
        Reads_ZF | Reads_CF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmova
        Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovs
        Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovns
        Reads_PF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovp
        Reads_PF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovnp
        Reads_OF | Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovl
        Reads_OF | Reads_SF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovge
        Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovle
        Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_cmovg
        INS_FLAGS_HasWBit | Encoding_REX2, // INS_xchg
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_AX
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_CX
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_DX
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_BX
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_SP
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_BP
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_SI
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_DI
#if TARGET_AMD64
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_08
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_09
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_10
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_11
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_12
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_13
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_14
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_15
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_16
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_17
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_18
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_19
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_20
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_21
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_22
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_23
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_24
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_25
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_26
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_27
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_28
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_29
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_30
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasSBit | INS_FLAGS_HasNF | Encoding_REX2, // INS_imul_31
#endif
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_addpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_addps
        Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_addsd
        Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_addss
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_addsubpd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_addsubps
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_andnpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_andnps
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_andpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_andps
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_blendpd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_blendps
        REX_W0, // INS_blendvpd
        REX_W0, // INS_blendvps
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_cmppd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_cmpps
        Input_64Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_cmpsd
        Input_32Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_cmpss
        Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF, // INS_comisd
        Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF, // INS_comiss
        Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_cvtdq2pd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_cvtdq2ps
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_cvtpd2dq
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_cvtpd2ps
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_cvtps2dq
        Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_cvtps2pd
        Input_64Bit | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_cvtsd2si32
        Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX, // INS_cvtsd2si64
        Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_cvtsd2ss
        Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_cvtsi2sd32
        Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_cvtsi2sd64
        Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_cvtsi2ss32
        Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_cvtsi2ss64
        Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_cvtss2sd
        Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_cvtss2si32
        Input_32Bit | REX_W1 | Encoding_VEX | Encoding_EVEX, // INS_cvtss2si64
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_cvttpd2dq
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_cvttps2dq
        Input_64Bit | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_cvttsd2si32
        Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX, // INS_cvttsd2si64
        Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_cvttss2si32
        Input_32Bit | REX_W1 | Encoding_VEX | Encoding_EVEX, // INS_cvttss2si64
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_divpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_divps
        Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_divsd
        Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_divss
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_dppd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_dpps
        Input_32Bit | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_extractps
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_haddpd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_haddps
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_hsubpd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_hsubps
        Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_insertps
        REX_WIG | Encoding_VEX, // INS_lddqu
        REX_WIG, // INS_lfence
        REX_WIG | Encoding_VEX, // INS_maskmovdqu
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_maxpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_maxps
        Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_maxsd
        Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_maxss
        REX_WIG, // INS_mfence
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_minpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_minps
        Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_minsd
        Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_minss
        REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movapd
        REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movaps
        Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | Encoding_REX2, // INS_movd32
        Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | Encoding_REX2, // INS_movd64
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movddup
        REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Encoding_REX2 | INS_FLAGS_HasPseudoName, // INS_movdqa32
        REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Encoding_REX2 | INS_FLAGS_HasPseudoName, // INS_movdqu32
        REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_movhlps
        Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_movhpd
        Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_movhps
        REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_movlhps
        Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_movlpd
        Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_movlps
        REX_WIG | Encoding_VEX, // INS_movmskpd
        REX_WIG | Encoding_VEX, // INS_movmskps
        REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movntdq
        REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movntdqa
        Input_32Bit | REX_W0 | Encoding_REX2, // INS_movnti32
        Input_64Bit | REX_W1 | Encoding_REX2, // INS_movnti64
        REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movntpd
        REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movntps
        Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | Encoding_REX2, // INS_movq
        Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_movsd_simd
        KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movshdup
        KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movsldup
        Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_movss
        REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movupd
        REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_movups
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_mpsadbw
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_mulpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_mulps
        Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_mulsd
        Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_mulss
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_orpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_orps
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pabsb
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_pabsd
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pabsw
        Input_32Bit | KMask_Base8 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_packssdw
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_packsswb
        Input_32Bit | KMask_Base8 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_packusdw
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_packuswb
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_paddb
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_paddd
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_paddq
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_paddsb
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_paddsw
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_paddusb
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_paddusw
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_paddw
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_palignr
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative | INS_FLAGS_HasPseudoName, // INS_pandd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_pandnd
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pavgb
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pavgw
        REX_W0, // INS_pblendvb
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pblendw
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pcmpeqb
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pcmpeqd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pcmpeqq
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pcmpeqw
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pcmpgtb
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pcmpgtd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pcmpgtq
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pcmpgtw
        Input_8Bit | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_pextrb
        Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_pextrd
        Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX, // INS_pextrq
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_phaddd
        Input_16Bit | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_pextrw
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_phaddsw
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_phaddw
        REX_WIG | Encoding_VEX, // INS_phminposuw
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_phsubd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_phsubsw
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_phsubw
        Input_8Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pinsrb
        Input_32Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pinsrd
        Input_64Bit | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pinsrq
        Input_16Bit | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pinsrw
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pmaddubsw
        KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmaddwd
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmaxsb
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmaxsd
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmaxsw
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmaxub
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmaxud
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmaxuw
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pminsb
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pminsd
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pminsw
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pminub
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pminud
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pminuw
        REX_WIG | Encoding_VEX, // INS_pmovmskb
        Input_8Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pmovsxbd
        Input_8Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pmovsxbq
        Input_8Bit | KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pmovsxbw
        Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_pmovsxdq
        Input_16Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pmovsxwd
        Input_16Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pmovsxwq
        Input_8Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pmovzxbd
        Input_8Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pmovzxbq
        Input_8Bit | KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pmovzxbw
        Input_32Bit | KMask_Base2 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_pmovzxdq
        Input_16Bit | KMask_Base4 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pmovzxwd
        Input_16Bit | KMask_Base2 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pmovzxwq
        Input_32Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmuldq
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmulhrsw
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmulhuw
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmulhw
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmulld
        Input_32Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmuludq
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_pmullw
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative | INS_FLAGS_HasPseudoName, // INS_pord
        Input_8Bit | REX_WIG | Encoding_REX2, // INS_prefetchnta
        Input_8Bit | REX_WIG | Encoding_REX2, // INS_prefetcht0
        Input_8Bit | REX_WIG | Encoding_REX2, // INS_prefetcht1
        Input_8Bit | REX_WIG | Encoding_REX2, // INS_prefetcht2
        REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psadbw
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pshufb
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_pshufd
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pshufhw
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX, // INS_pshuflw
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psignb
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psignd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psignw
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pslld
        REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pslldq
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psllq
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psllw
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psrad
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psraw
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psrld
        REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psrldq
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psrlq
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psrlw
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psubb
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psubd
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psubq
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psubsb
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psubsw
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psubusb
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psubusw
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_psubw
        REX_WIG | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF, // INS_ptest
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_punpckhbw
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_punpckhdq
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_punpckhqdq
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_punpckhwd
        KMask_Base16 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_punpcklbw
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_punpckldq
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_punpcklqdq
        KMask_Base8 | REX_WIG | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_punpcklwd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative | INS_FLAGS_HasPseudoName, // INS_pxord
        REX_WIG | Encoding_VEX, // INS_rcpps
        Input_32Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_rcpss
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName, // INS_roundpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName, // INS_roundps
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_roundsd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_roundss
        REX_WIG | Encoding_VEX, // INS_rsqrtps
        Input_32Bit | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_rsqrtss
        REX_WIG, // INS_sfence
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_shufpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_shufps
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_sqrtpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX, // INS_sqrtps
        Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_sqrtsd
        Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_sqrtss
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_subpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_subps
        Input_64Bit | KMask_Base1 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_subsd
        Input_32Bit | KMask_Base1 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_subss
        Input_64Bit | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF, // INS_ucomisd
        Input_32Bit | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Writes_PF | Writes_CF, // INS_ucomiss
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_unpckhpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_unpckhps
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_unpcklpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_unpcklps
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_xorpd
        Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative, // INS_xorps
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_aesdec
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_aesdeclast
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_aesenc
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_aesenclast
        REX_WIG | Encoding_VEX, // INS_aesimc
        REX_WIG | Encoding_VEX, // INS_aeskeygenassist
        KMask_Base1 | REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_pclmulqdq
        REX_WIG, // INS_sha1msg1
        REX_WIG, // INS_sha1msg2
        REX_WIG, // INS_sha1nexte
        REX_WIG, // INS_sha1rnds4
        REX_WIG, // INS_sha256msg1
        REX_WIG, // INS_sha256msg2
        REX_WIG, // INS_sha256rnds2
        Input_64Bit | KMask_Base2 | REX_WX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_gf2p8affineinvqb
        Input_64Bit | KMask_Base2 | REX_WX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_gf2p8affineqb
        KMask_Base16 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_gf2p8mulb
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vblendvpd
        REX_WIG | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vblendvps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName, // INS_vbroadcastf32x4
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_vbroadcastsd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_vbroadcastss
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName, // INS_vextractf32x4
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_vinsertf32x4
        REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vmaskmovpd
        REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vmaskmovps
        REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpblendvb
        REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vperm2f128
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_vpermilpd
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpermilpdvar
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_vpermilps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpermilpsvar
        REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF, // INS_vtestpd
        REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF, // INS_vtestps
        REX_WIG | Encoding_VEX, // INS_vzeroupper
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName, // INS_vbroadcasti32x4
        Input_16Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_vcvtph2ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_vcvtps2ph
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_HasPseudoName, // INS_vextracti32x4
        Input_64Bit | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vgatherdpd
        Input_32Bit | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vgatherdps
        Input_64Bit | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vgatherqpd
        Input_32Bit | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vgatherqps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_vinserti32x4
        REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpblendd
        Input_8Bit | KMask_Base16 | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_vpbroadcastb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_vpbroadcastd
        Input_64Bit | KMask_Base2 | REX_W1_EVEX | Encoding_VEX | Encoding_EVEX, // INS_vpbroadcastq
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_VEX | Encoding_EVEX, // INS_vpbroadcastw
        REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vperm2i128
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpermd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX, // INS_vpermpd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpermps
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX, // INS_vpermq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpgatherdd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpgatherdq
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpgatherqd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpgatherqq
        REX_W0 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpmaskmovd
        REX_W1 | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpmaskmovq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpsllvd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpsllvq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpsravd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpsrlvd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpsrlvq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd132pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd213pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd231pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd132ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd213ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd231ps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd132sd
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd213sd
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd231sd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd132ss
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd213ss
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd231ss
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmaddsub132pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmaddsub213pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmaddsub231pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmaddsub132ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmaddsub213ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmaddsub231ps
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsubadd132pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsubadd213pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsubadd231pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsubadd132ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsubadd213ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsubadd231ps
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub132pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub213pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub231pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub132ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub213ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub231ps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub132sd
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub213sd
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub231sd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub132ss
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub213ss
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub231ss
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd132pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd213pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd231pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd132ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd213ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd231ps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd132sd
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd213sd
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd231sd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd132ss
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd213ss
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd231ss
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub132pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub213pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub231pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub132ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub213ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub231ps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub132sd
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub213sd
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub231sd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub132ss
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub213ss
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub231ss
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Resets_CF | INS_FLAGS_HasNF, // INS_andn
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Resets_CF | INS_FLAGS_HasNF, // INS_bextr
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF, // INS_blsi
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Resets_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF, // INS_blsmsk
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF, // INS_blsr
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction | Resets_OF | Writes_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF, // INS_bzhi
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_mulx
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pdep
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_pext
        REX_WX | Encoding_VEX, // INS_rorx
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_sarx
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_shlx
        REX_WX | Encoding_VEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_shrx
        Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpdpbusd
        Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpdpbusds
        Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpdpwssd
        Input_32Bit | KMask_Base4 | REX_W0 | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpdpwssds
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpwsud
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpwsuds
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpwusd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpwusds
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpwuud
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpwuuds
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpbssd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpbssds
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpbsud
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpbsuds
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpbuud
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_VEX | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpdpbuuds
        Input_16Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vbmacor16x16x16
        Input_16Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vbmacxor16x16x16
        Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX, // INS_vbitrev
        Input_64Bit | KMask_Base2 | REX_W1 | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpmadd52huq
        Input_64Bit | KMask_Base2 | REX_W1 | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpmadd52luq
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kaddb
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kaddd
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kaddq
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kaddw
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kandb
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kandd
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kandnb
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kandnd
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kandnq
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kandnw
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kandq
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kandw
        REX_W0 | Encoding_VEX | KInstruction, // INS_kmovb_gpr
        REX_W0 | Encoding_VEX | KInstruction, // INS_kmovb_msk
        REX_W0 | Encoding_VEX | KInstruction, // INS_kmovd_gpr
        REX_W1 | Encoding_VEX | KInstruction, // INS_kmovd_msk
        REX_W1 | Encoding_VEX | KInstruction, // INS_kmovq_gpr
        REX_W1 | Encoding_VEX | KInstruction, // INS_kmovq_msk
        REX_W0 | Encoding_VEX | KInstruction, // INS_kmovw_gpr
        REX_W0 | Encoding_VEX | KInstruction, // INS_kmovw_msk
        REX_W0 | Encoding_VEX | KInstruction, // INS_knotb
        REX_W1 | Encoding_VEX | KInstruction, // INS_knotd
        REX_W1 | Encoding_VEX | KInstruction, // INS_knotq
        REX_W0 | Encoding_VEX | KInstruction, // INS_knotw
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_korb
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kord
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_korq
        REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction, // INS_kortestb
        REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction, // INS_kortestd
        REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction, // INS_kortestq
        REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction, // INS_kortestw
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_korw
        REX_W0 | Encoding_VEX | KInstruction, // INS_kshiftlb
        REX_W0 | Encoding_VEX | KInstruction, // INS_kshiftld
        REX_W1 | Encoding_VEX | KInstruction, // INS_kshiftlq
        REX_W1 | Encoding_VEX | KInstruction, // INS_kshiftlw
        REX_W0 | Encoding_VEX | KInstruction, // INS_kshiftrb
        REX_W0 | Encoding_VEX | KInstruction, // INS_kshiftrd
        REX_W1 | Encoding_VEX | KInstruction, // INS_kshiftrq
        REX_W1 | Encoding_VEX | KInstruction, // INS_kshiftrw
        REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction, // INS_ktestb
        REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction, // INS_ktestd
        REX_W1 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction, // INS_ktestq
        REX_W0 | Encoding_VEX | Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Writes_CF | KInstruction, // INS_ktestw
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kunpckbw
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kunpckdq
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kunpckwd
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kxnorb
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kxnord
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kxnorq
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kxnorw
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kxorb
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kxord
        REX_W1 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kxorq
        REX_W0 | Encoding_VEX | KInstruction | KInstructionWithLBit, // INS_kxorw
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_valignd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_valignq
        Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vblendmpd
        Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vblendmps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vbroadcastf32x2
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vbroadcastf32x8
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vbroadcastf64x2
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vbroadcastf64x4
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vbroadcasti32x2
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vbroadcasti32x8
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vbroadcasti64x2
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vbroadcasti64x4
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_vcmppd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_vcmpps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_vcmpsd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_vcmpss
        Input_64Bit | REX_W1 | Encoding_EVEX, // INS_vcompresspd
        Input_32Bit | REX_W0 | Encoding_EVEX, // INS_vcompressps
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvtpd2qq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvtpd2udq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvtpd2uqq
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvtps2qq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvtps2udq
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvtps2uqq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvtqq2pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvtqq2ps
        Input_64Bit | REX_W0 | Encoding_EVEX, // INS_vcvtsd2usi32
        Input_64Bit | REX_W1 | Encoding_EVEX, // INS_vcvtsd2usi64
        Input_32Bit | REX_W0 | Encoding_EVEX, // INS_vcvtss2usi32
        Input_32Bit | REX_W1 | Encoding_EVEX, // INS_vcvtss2usi64
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvttpd2qq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvttpd2udq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvttpd2uqq
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvttps2qq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvttps2udq
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvttps2uqq
        Input_64Bit | REX_W0 | Encoding_EVEX, // INS_vcvttsd2usi32
        Input_64Bit | REX_W1 | Encoding_EVEX, // INS_vcvttsd2usi64
        Input_32Bit | REX_W0 | Encoding_EVEX, // INS_vcvttss2usi32
        Input_32Bit | REX_W1 | Encoding_EVEX, // INS_vcvttss2usi64
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvtudq2pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvtudq2ps
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvtuqq2pd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvtuqq2ps
        Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vcvtusi2sd32
        Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vcvtusi2sd64
        Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vcvtusi2ss32
        Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vcvtusi2ss64
        KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vdbpsadbw
        Input_64Bit | REX_W1 | Encoding_EVEX, // INS_vexpandpd
        Input_32Bit | REX_W0 | Encoding_EVEX, // INS_vexpandps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vextractf32x8
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vextractf64x2
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vextractf64x4
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vextracti32x8
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vextracti64x2
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vextracti64x4
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfixupimmpd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfixupimmps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfixupimmsd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfixupimmss
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vfpclasspd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vfpclassps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX, // INS_vfpclasssd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vfpclassss
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vgatherdpd_msk
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vgatherdps_msk
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vgatherqpd_msk
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vgatherqps_msk
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vgetexppd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vgetexpps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vgetexpsd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vgetexpss
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vgetmantpd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vgetmantps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vgetmantsd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vgetmantss
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vinsertf32x8
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vinsertf64x2
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vinsertf64x4
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vinserti32x8
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vinserti64x2
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vinserti64x4
        REX_W1 | Encoding_EVEX, // INS_vmovdqa64
        REX_W1 | Encoding_EVEX, // INS_vmovdqu16
        REX_W1 | Encoding_EVEX, // INS_vmovdqu64
        REX_W0 | Encoding_EVEX, // INS_vmovdqu8
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vpabsq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpandnq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpandq
        REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpblendmb
        Input_32Bit | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpblendmd
        Input_64Bit | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpblendmq
        REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpblendmw
        Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX, // INS_vpbroadcastb_gpr
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpbroadcastd_gpr
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vpbroadcastq_gpr
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vpbroadcastw_gpr
        KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_vpcmpb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName, // INS_vpcmpd
        KMask_Base16 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpcmpeqb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpcmpeqd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpcmpeqq
        KMask_Base8 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpcmpeqw
        KMask_Base16 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpcmpgtb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpcmpgtd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpcmpgtq
        KMask_Base8 | REX_WIG | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpcmpgtw
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName, // INS_vpcmpq
        KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_vpcmpub
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName, // INS_vpcmpud
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_Is3OperandInstructionMask | INS_FLAGS_HasPseudoName, // INS_vpcmpuq
        KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_vpcmpuw
        KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_HasPseudoName, // INS_vpcmpw
        Input_32Bit | REX_W0 | Encoding_EVEX, // INS_vpcompressd
        Input_64Bit | REX_W1 | Encoding_EVEX, // INS_vpcompressq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpconflictd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vpconflictq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermi2d
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermi2pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermi2ps
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermi2q
        KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermi2w
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermpd_reg
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermq_reg
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermt2d
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermt2pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermt2ps
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermt2q
        KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermt2w
        KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermw
        Input_32Bit | REX_W0 | Encoding_EVEX, // INS_vpexpandd
        Input_64Bit | REX_W1 | Encoding_EVEX, // INS_vpexpandq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpgatherdd_msk
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpgatherdq_msk
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpgatherqd_msk
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpgatherqq_msk
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vplzcntd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vplzcntq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpmaxsq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpmaxuq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpminsq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpminuq
        REX_W0 | Encoding_EVEX, // INS_vpmovb2m
        REX_W0 | Encoding_EVEX, // INS_vpmovd2m
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpmovdb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpmovdw
        REX_W0 | Encoding_EVEX, // INS_vpmovm2b
        REX_W0 | Encoding_EVEX, // INS_vpmovm2d
        REX_W1 | Encoding_EVEX, // INS_vpmovm2q
        REX_W1 | Encoding_EVEX, // INS_vpmovm2w
        REX_W1 | Encoding_EVEX, // INS_vpmovq2m
        Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vpmovqb
        Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vpmovqd
        Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vpmovqw
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpmovsdb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpmovsdw
        Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vpmovsqb
        Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vpmovsqd
        Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vpmovsqw
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vpmovswb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpmovusdb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpmovusdw
        Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vpmovusqb
        Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vpmovusqd
        Input_64Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vpmovusqw
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vpmovuswb
        REX_W1 | Encoding_EVEX, // INS_vpmovw2m
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vpmovwb
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpmullq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vporq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vprold
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vprolq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vprolvd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vprolvq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vprord
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vprorq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vprorvd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vprorvq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpscatterdd_msk
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpscatterdq_msk
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpscatterqd_msk
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpscatterqq_msk
        KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpsllvw
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpsraq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpsravq
        KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpsravw
        KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpsrlvw
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpternlogd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpternlogq
        KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vptestmb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vptestmd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vptestmq
        KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vptestmw
        KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vptestnmb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vptestnmd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vptestnmq
        KMask_Base8 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vptestnmw
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vpxorq
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vrangepd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vrangeps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vrangesd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vrangess
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vrcp14pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vrcp14ps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vrcp14sd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vrcp14ss
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vreducepd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vreduceps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vreducesd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vreducess
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vrndscalepd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vrndscaleps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vrndscalesd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vrndscaless
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vrsqrt14pd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vrsqrt14ps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vrsqrt14sd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vrsqrt14ss
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vscalefpd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vscalefps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vscalefsd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vscalefss
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vscatterdpd_msk
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vscatterdps_msk
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vscatterqpd_msk
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vscatterqps_msk
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vshuff32x4
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vshuff64x2
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vshufi32x4
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vshufi64x2
        KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermb
        KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermi2b
        KMask_Base16 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpermt2b
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vpmultishiftqb
        Input_8Bit | REX_W0 | Encoding_EVEX, // INS_vpcompressb
        Input_16Bit | REX_W1 | Encoding_EVEX, // INS_vpcompressw
        Input_8Bit | REX_W0 | Encoding_EVEX, // INS_vpexpandb
        Input_16Bit | REX_W1 | Encoding_EVEX, // INS_vpexpandw
        Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX, // INS_vpopcntb
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpopcntd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vpopcntq
        Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX, // INS_vpopcntw
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpshldd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vpshldq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpshldvd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vpshldvq
        Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX, // INS_vpshldvw
        Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX, // INS_vpshldw
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpshrdd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vpshrdq
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vpshrdvd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vpshrdvq
        Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX, // INS_vpshrdvw
        Input_16Bit | KMask_Base8 | REX_W1 | Encoding_EVEX, // INS_vpshrdw
        Input_8Bit | KMask_Base16 | REX_W0 | Encoding_EVEX, // INS_vpshufbitqmb
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vaddph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vaddsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vcmpph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vcmpsh
        Input_16Bit | REX_W0 | Encoding_EVEX, // INS_vcomish
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvtdq2ph
        Input_32Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vcvtne2ps2bf16
        Input_32Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vcvtneps2bf16
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvtpd2ph
        Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvtph2dq
        Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvtph2pd
        Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvtph2psx
        Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvtph2qq
        Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvtph2udq
        Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvtph2uqq
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vcvtph2uw
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vcvtph2w
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvtps2phx
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvtqq2ph
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vcvtsd2sh
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vcvtsh2sd
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vcvtsh2si32
        Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX, // INS_vcvtsh2si64
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vcvtsh2ss
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vcvtsh2usi32
        Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX, // INS_vcvtsh2usi64
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vcvtsi2sh32
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vcvtsi2sh64
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vcvtss2sh
        Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvttph2dq
        Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvttph2qq
        Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvttph2udq
        Input_16Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvttph2uqq
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vcvttph2uw
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vcvttph2w
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vcvttsh2si32
        Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX, // INS_vcvttsh2si64
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vcvttsh2usi32
        Input_16Bit | KMask_Base1 | REX_W1 | Encoding_EVEX, // INS_vcvttsh2usi64
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvtudq2ph
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvtuqq2ph
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vcvtusi2sh32
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vcvtusi2sh64
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vcvtuw2ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vcvtw2ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vdivph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vdivsh
        Input_16Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vdpbf16ps
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vfcmaddcph
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vfcmaddcsh
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vfcmulcph
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vfcmulcsh
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vfmulcph
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vfmulcsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd132ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd213ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd231ph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd132sh
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd213sh
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmadd231sh
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vfmaddcph
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vfmaddcsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmaddsub132ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmaddsub213ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmaddsub231ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub132ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub213ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub231ph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub132sh
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub213sh
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsub231sh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsubadd132ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsubadd213ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfmsubadd231ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd132ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd213ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd231ph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd132sh
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd213sh
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmadd231sh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub132ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub213ph
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub231ph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub132sh
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub213sh
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vfnmsub231sh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vfpclassph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vfpclasssh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vgetexpph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vgetexpsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vgetmantph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vgetmantsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vmaxph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vmaxsh
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vminsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vminph
        Input_16Bit | REX_W0 | Encoding_EVEX, // INS_vmovsh
        Input_16Bit | REX_WIG | Encoding_EVEX, // INS_vmovw
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vmulph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vmulsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vrcpph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vrcpsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vreduceph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vreducesh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vrndscaleph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vrndscalesh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vrsqrtph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vrsqrtsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vscalefph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX, // INS_vscalefsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vsqrtph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vsqrtsh
        Input_16Bit | KMask_Base8 | REX_W0 | Encoding_EVEX, // INS_vsubph
        Input_16Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vsubsh
        Input_16Bit | REX_W0 | Encoding_EVEX, // INS_vucomish
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vp2intersectd
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vp2intersectq
        Input_64Bit | REX_W1 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF, // INS_vcomxsd
        Input_32Bit | REX_W0 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF, // INS_vcomxss
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvtps2ibs
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvtps2iubs
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvttpd2dqs
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvttpd2qqs
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvttpd2udqs
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX, // INS_vcvttpd2uqqs
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvttps2dqs
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvttps2ibs
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvttps2iubs
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvttps2qqs
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX, // INS_vcvttps2udqs
        Input_32Bit | KMask_Base2 | REX_W0 | Encoding_EVEX, // INS_vcvttps2uqqs
        Input_64Bit | REX_W0 | Encoding_EVEX, // INS_vcvttsd2sis32
        Input_64Bit | REX_W1 | Encoding_EVEX, // INS_vcvttsd2sis64
        Input_64Bit | REX_W0 | Encoding_EVEX, // INS_vcvttsd2usis32
        Input_64Bit | REX_W1 | Encoding_EVEX, // INS_vcvttsd2usis64
        Input_32Bit | REX_W0 | Encoding_EVEX, // INS_vcvttss2sis32
        Input_32Bit | REX_W1 | Encoding_EVEX, // INS_vcvttss2sis64
        Input_32Bit | REX_W0 | Encoding_EVEX, // INS_vcvttss2usis32
        Input_32Bit | REX_W1 | Encoding_EVEX, // INS_vcvttss2usis64
        Input_64Bit | KMask_Base2 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vminmaxpd
        Input_32Bit | KMask_Base4 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vminmaxps
        Input_64Bit | KMask_Base1 | REX_W1 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vminmaxsd
        Input_32Bit | KMask_Base1 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstSrcSrcAVXInstruction, // INS_vminmaxss
        Input_32Bit | REX_W0 | Encoding_EVEX, // INS_vmovd_simd
        Input_16Bit | REX_W0 | Encoding_EVEX, // INS_vmovw_simd
        KMask_Base8 | REX_W0 | Encoding_EVEX | INS_FLAGS_IsDstDstSrcAVXInstruction, // INS_vmpsadbw
        Input_64Bit | REX_W1 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF, // INS_vucomxsd
        Input_32Bit | REX_W0 | Encoding_EVEX | Writes_OF | Writes_SF | Writes_ZF | Writes_PF | Writes_CF | Resets_AF, // INS_vucomxss
#if TARGET_AMD64
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpo
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpno
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpb
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpae
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpe
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpne
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpbe
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpa
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmps
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpns
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpt
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpf
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpl
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpge
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmple
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ccmpg
        Reads_OF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovo
        Reads_OF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovno
        Reads_CF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovb
        Reads_CF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovae
        Reads_ZF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmove
        Reads_ZF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovne
        Reads_ZF | Reads_CF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovbe
        Reads_ZF | Reads_CF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmova
        Reads_SF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovs
        Reads_SF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovns
        Reads_PF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovp
        Reads_PF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovnp
        Reads_OF | Reads_SF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovl
        Reads_OF | Reads_SF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovge
        Reads_OF | Reads_SF | Reads_ZF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovle
        Reads_OF | Reads_SF | Reads_ZF | INS_FLAGS_HasNDD | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_cfcmovg
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctesto
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestno
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestb
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestae
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_cteste
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestne
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestbe
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctesta
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctests
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestns
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestt
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestf
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestl
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestge
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestle
        Writes_OF | Writes_SF | Writes_ZF | Writes_CF | INS_FLAGS_HasSBit | Encoding_EVEX_APX_ONLY, // INS_ctestg
        Encoding_EVEX_APX_ONLY, // INS_crc32_apx
        Encoding_EVEX_APX_ONLY, // INS_movbe_apx
        Reads_OF | Encoding_EVEX_APX_ONLY, // INS_seto_apx
        Reads_OF | Encoding_EVEX_APX_ONLY, // INS_setno_apx
        Reads_CF | Encoding_EVEX_APX_ONLY, // INS_setb_apx
        Reads_CF | Encoding_EVEX_APX_ONLY, // INS_setae_apx
        Reads_ZF | Encoding_EVEX_APX_ONLY, // INS_sete_apx
        Reads_ZF | Encoding_EVEX_APX_ONLY, // INS_setne_apx
        Reads_ZF | Reads_CF | Encoding_EVEX_APX_ONLY, // INS_setbe_apx
        Reads_ZF | Reads_CF | Encoding_EVEX_APX_ONLY, // INS_seta_apx
        Reads_SF | Encoding_EVEX_APX_ONLY, // INS_sets_apx
        Reads_SF | Encoding_EVEX_APX_ONLY, // INS_setns_apx
        Reads_PF | Encoding_EVEX_APX_ONLY, // INS_setp_apx
        Reads_PF | Encoding_EVEX_APX_ONLY, // INS_setnp_apx
        Reads_OF | Reads_SF | Encoding_EVEX_APX_ONLY, // INS_setl_apx
        Reads_OF | Reads_SF | Encoding_EVEX_APX_ONLY, // INS_setge_apx
        Reads_OF | Reads_SF | Reads_ZF | Encoding_EVEX_APX_ONLY, // INS_setle_apx
        Reads_OF | Reads_SF | Reads_ZF | Encoding_EVEX_APX_ONLY, // INS_setg_apx
#endif
        INS_FLAGS_None, // INS_crc32
        Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2, // INS_tzcnt
#if TARGET_AMD64
        Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_tzcnt_apx
#endif
        Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | Encoding_REX2, // INS_lzcnt
#if TARGET_AMD64
        Undefined_OF | Undefined_SF | Writes_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_lzcnt_apx
#endif
        INS_FLAGS_None, // INS_movbe
        Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Resets_CF | Encoding_REX2, // INS_popcnt
#if TARGET_AMD64
        Resets_OF | Resets_SF | Writes_ZF | Resets_AF | Resets_PF | Resets_CF | INS_FLAGS_HasNF | Encoding_EVEX_APX_ONLY, // INS_popcnt_apx
#endif
        Resets_OF | Resets_SF | Resets_ZF | Resets_AF | Resets_PF | Writes_CF, // INS_tpause
        INS_FLAGS_None, // INS_umonitor
        Resets_OF | Resets_SF | Resets_ZF | Resets_AF | Resets_PF | Writes_CF, // INS_umwait
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_neg
        INS_FLAGS_None | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_not
        Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_rol
        Writes_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_rol_1
        Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_rol_N
        Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_ror
        Writes_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_ror_1
        Undefined_OF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_ror_N
        Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_rcl
        Writes_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_rcl_1
        Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_rcl_N
        Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_rcr
        Writes_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_rcr_1
        Undefined_OF | Writes_CF | Reads_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD, // INS_rcr_N
        Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_shl
        Writes_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_shl_1
        Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_shl_N
        Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_shr
        Writes_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_shr_1
        Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_shr_N
        Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_sar
        Writes_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_sar_1
        Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF, // INS_sar_N
        INS_FLAGS_None, // INS_ret
        INS_FLAGS_None, // INS_loop
        Encoding_REX2, // INS_call
        Reads_DF | INS_FLAGS_HasWBit, // INS_r_movsb
        Reads_DF | INS_FLAGS_HasWBit, // INS_r_movsd
#if TARGET_AMD64
        Reads_DF, // INS_r_movsq
#endif
        Reads_DF | INS_FLAGS_HasWBit, // INS_movsb
        Reads_DF | INS_FLAGS_HasWBit, // INS_movsd
#if TARGET_AMD64
        Reads_DF, // INS_movsq
#endif
        Reads_DF | INS_FLAGS_HasWBit, // INS_r_stosb
        Reads_DF | INS_FLAGS_HasWBit, // INS_r_stosd
#if TARGET_AMD64
        Reads_DF, // INS_r_stosq
#endif
        Reads_DF | INS_FLAGS_HasWBit, // INS_stosb
        Reads_DF | INS_FLAGS_HasWBit, // INS_stosd
#if TARGET_AMD64
        Reads_DF, // INS_stosq
#endif
        INS_FLAGS_None, // INS_int3
        INS_FLAGS_None, // INS_nop
        INS_FLAGS_None, // INS_pause
        INS_FLAGS_None, // INS_lock
        INS_FLAGS_None, // INS_leave
        INS_FLAGS_None, // INS_serialize
        INS_FLAGS_HasPseudoName, // INS_cwde
        INS_FLAGS_HasPseudoName, // INS_cdq
        Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Undefined_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF, // INS_idiv
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF, // INS_imulEAX
        Undefined_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Undefined_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF, // INS_div
        Writes_OF | Undefined_SF | Undefined_ZF | Undefined_AF | Undefined_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNF, // INS_mulEAX
        Restore_SF_ZF_AF_PF_CF, // INS_sahf
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2, // INS_xadd
        Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF | INS_FLAGS_HasWBit | Encoding_REX2, // INS_cmpxchg
        Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | Encoding_REX2, // INS_shld
        Undefined_OF | Writes_SF | Writes_ZF | Undefined_AF | Writes_PF | Writes_CF | Encoding_REX2, // INS_shrd
#if TARGET_X86
        INS_FLAGS_X87Instr, // INS_fld
        INS_FLAGS_X87Instr, // INS_fstp
#endif
        Reads_OF | Encoding_REX2, // INS_seto
        Reads_OF | Encoding_REX2, // INS_setno
        Reads_CF | Encoding_REX2, // INS_setb
        Reads_CF | Encoding_REX2, // INS_setae
        Reads_ZF | Encoding_REX2, // INS_sete
        Reads_ZF | Encoding_REX2, // INS_setne
        Reads_ZF | Reads_CF | Encoding_REX2, // INS_setbe
        Reads_ZF | Reads_CF | Encoding_REX2, // INS_seta
        Reads_SF | Encoding_REX2, // INS_sets
        Reads_SF | Encoding_REX2, // INS_setns
        Reads_PF | Encoding_REX2, // INS_setp
        Reads_PF | Encoding_REX2, // INS_setnp
        Reads_OF | Reads_SF | Encoding_REX2, // INS_setl
        Reads_OF | Reads_SF | Encoding_REX2, // INS_setge
        Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2, // INS_setle
        Reads_OF | Reads_SF | Reads_ZF | Encoding_REX2, // INS_setg
        Encoding_REX2, // INS_tail_i_jmp
        Encoding_REX2, // INS_i_jmp
        INS_FLAGS_None, // INS_jmp
        Reads_OF, // INS_jo
        Reads_OF, // INS_jno
        Reads_CF, // INS_jb
        Reads_CF, // INS_jae
        Reads_ZF, // INS_je
        Reads_ZF, // INS_jne
        Reads_ZF | Reads_CF, // INS_jbe
        Reads_ZF | Reads_CF, // INS_ja
        Reads_SF, // INS_js
        Reads_SF, // INS_jns
        Reads_PF, // INS_jp
        Reads_PF, // INS_jnp
        Reads_OF | Reads_SF, // INS_jl
        Reads_OF | Reads_SF, // INS_jge
        Reads_OF | Reads_SF | Reads_ZF, // INS_jle
        Reads_OF | Reads_SF | Reads_ZF, // INS_jg
        INS_FLAGS_None, // INS_l_jmp
        Reads_OF, // INS_l_jo
        Reads_OF, // INS_l_jno
        Reads_CF, // INS_l_jb
        Reads_CF, // INS_l_jae
        Reads_ZF, // INS_l_je
        Reads_ZF, // INS_l_jne
        Reads_ZF | Reads_CF, // INS_l_jbe
        Reads_ZF | Reads_CF, // INS_l_ja
        Reads_SF, // INS_l_js
        Reads_SF, // INS_l_jns
        Reads_PF, // INS_l_jp
        Reads_PF, // INS_l_jnp
        Reads_OF | Reads_SF, // INS_l_jl
        Reads_OF | Reads_SF, // INS_l_jge
        Reads_OF | Reads_SF | Reads_ZF, // INS_l_jle
        Reads_OF | Reads_SF | Reads_ZF, // INS_l_jg
        INS_FLAGS_None, // INS_align
        INS_FLAGS_None, // INS_data16
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
#elif TARGET_ARM64
    private const byte INST_FP = 1;
    internal const byte LD = 1;
    internal const byte ST = 2;
    private const byte CMP = 4;
    private const byte RSH = 8;
    private const byte WID = 16;
    private const byte LNG = 32;
    private const byte NRW = 64;
    private const byte WR2 = 128;

    internal static ReadOnlySpan<byte> instInfo => [
#if TARGET_XARCH
#if !TARGET_XARCH
#error Unexpected target type
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
        0, // invalid
        0, // mov
        0, // add
        0, // sub
        LD, // ld1
        LD, // ld2
        LD, // ld3
        LD, // ld4
        LD, // st1
        ST, // st2
        ST, // st3
        ST, // st4
        LD, // ldr
        LD, // ldrsw
        0, // fmov
        0, // orr
        LD, // ldrb
        LD, // ldrh
        LD, // ldrsb
        LD, // ldrsh
        ST, // str
        ST, // strb
        ST, // strh
        0, // adds
        0, // subs
        CMP, // cmp
        CMP, // cmn
        0, // fmul
        0, // fmulx
        0, // and
        0, // eor
        0, // bic
        0, // neg
        0, // cmeq
        0, // cmge
        0, // cmgt
        0, // fcmeq
        0, // fcmge
        0, // fcmgt
        0, // sqshl
        0, // uqshl
        LNG, // sqdmlal
        LNG, // sqdmlsl
        0, // sqdmulh
        LNG, // sqdmull
        0, // sqrdmlah
        0, // sqrdmlsh
        0, // sqrdmulh
        0, // ands
        0, // tst
        0, // orn
        0, // dup
        0, // fmla
        0, // fmls
        0, // fcvtas
        0, // fcvtau
        0, // fcvtms
        0, // fcvtmu
        0, // fcvtns
        0, // fcvtnu
        0, // fcvtps
        0, // fcvtpu
        0, // fcvtzs
        0, // fcvtzu
        0, // scvtf
        0, // ucvtf
        0, // mul
        LNG, // smull
        LNG, // umull
        0, // mvn
        LD, // ld1_2regs
        LD, // ld1_3regs
        LD, // ld1_4regs
        ST, // st1_2regs
        ST, // st1_3regs
        ST, // st1_4regs
        LD, // ld1r
        LD, // ld2r
        LD, // ld3r
        LD, // ld4r
        0, // negs
        0, // bics
        0, // eon
        0, // lsl
        0, // lsr
        0, // asr
        0, // ror
        LD, // ldp
        LD, // ldpsw
        ST, // stp
        LD, // ldnp
        ST, // stnp
        CMP, // ccmp
        CMP, // ccmn
        0, // ins
        0, // fadd
        0, // fsub
        0, // fdiv
        0, // fmax
        0, // fmaxnm
        0, // fmin
        0, // fminnm
        0, // fabd
        0, // facge
        0, // facgt
        0, // frecps
        0, // frsqrts
        0, // fcmp
        0, // fcmpe
        0, // fabs
        0, // fcmle
        0, // fcmlt
        NRW, // fcvtxn
        0, // fneg
        0, // frecpe
        0, // frintn
        0, // frintp
        0, // frintm
        0, // frintz
        0, // frinta
        0, // frintx
        0, // frinti
        0, // frsqrte
        0, // fsqrt
        0, // abs
        0, // cmle
        0, // cmlt
        0, // sqabs
        0, // sqneg
        NRW, // sqxtn
        NRW, // sqxtun
        0, // suqadd
        0, // usqadd
        NRW, // uqxtn
        0, // cls
        0, // clz
        0, // rbit
        0, // cnt
        0, // rev16
        0, // rev32
        0, // mla
        0, // mls
        LNG, // smlal
        LNG, // smlal2
        LNG, // smlsl
        LNG, // smlsl2
        LNG, // smull2
        LNG, // sqdmlal2
        LNG, // sqdmlsl2
        LNG, // sqdmull2
        0, // sdot
        0, // udot
        LNG, // umlal
        LNG, // umlal2
        LNG, // umlsl
        LNG, // umlsl2
        LNG, // umull2
        RSH, // sshr
        RSH, // ssra
        RSH, // srshr
        RSH, // srsra
        0, // shl
        RSH, // ushr
        RSH, // usra
        RSH, // urshr
        RSH, // ursra
        RSH, // sri
        0, // sli
        0, // sqshlu
        RSH | NRW, // sqrshrn
        RSH | NRW, // sqrshrun
        RSH | NRW, // sqshrn
        RSH | NRW, // sqshrun
        RSH | NRW, // uqrshrn
        RSH | NRW, // uqshrn
        0, // cmhi
        0, // cmhs
        0, // cmtst
        0, // sqadd
        0, // sqrshl
        0, // sqsub
        0, // srshl
        0, // sshl
        0, // uqadd
        0, // uqrshl
        0, // uqsub
        0, // urshl
        0, // ushl
        0, // faddp
        0, // fmaxnmp
        0, // fmaxp
        0, // fminnmp
        0, // fminp
        0, // addp
        LD, // ldar
        LD, // ldarb
        LD, // ldarh
        LD, // ldapr
        LD, // ldaprb
        LD, // ldaprh
        LD, // ldxr
        LD, // ldxrb
        LD, // ldxrh
        LD, // ldaxr
        LD, // ldaxrb
        LD, // ldaxrh
        LD, // ldur
        LD, // ldurb
        LD, // ldurh
        LD, // ldursb
        LD, // ldursh
        LD, // ldursw
        LD, // ldapur
        LD, // ldapurb
        LD, // ldapurh
        ST, // stlr
        ST, // stlrb
        ST, // stlrh
        ST, // stxr
        ST, // stxrb
        ST, // stxrh
        ST, // stlxr
        ST, // stlxrb
        ST, // stlxrh
        ST, // stur
        ST, // sturb
        ST, // sturh
        ST, // stlur
        ST, // stlurb
        ST, // stlurh
        LD | ST, // casb
        LD | ST, // casab
        LD | ST, // casalb
        LD | ST, // caslb
        LD | ST, // cash
        LD | ST, // casah
        LD | ST, // casalh
        LD | ST, // caslh
        LD | ST, // cas
        LD | ST, // casa
        LD | ST, // casal
        LD | ST, // casl
        WR2 | LD | ST, // ldaddb
        WR2 | LD | ST, // ldaddab
        WR2 | LD | ST, // ldaddalb
        WR2 | LD | ST, // ldaddlb
        WR2 | LD | ST, // ldaddh
        WR2 | LD | ST, // ldaddah
        WR2 | LD | ST, // ldaddalh
        WR2 | LD | ST, // ldaddlh
        WR2 | LD | ST, // ldadd
        WR2 | LD | ST, // ldadda
        WR2 | LD | ST, // ldaddal
        WR2 | LD | ST, // ldclral
        WR2 | LD | ST, // ldsetal
        WR2 | LD | ST, // ldaddl
        WR2 | ST, // staddb
        WR2 | ST, // staddlb
        WR2 | ST, // staddh
        WR2 | ST, // staddlh
        WR2 | ST, // stadd
        WR2 | ST, // staddl
        WR2 | LD | ST, // swpb
        WR2 | LD | ST, // swpab
        WR2 | LD | ST, // swpalb
        WR2 | LD | ST, // swplb
        WR2 | LD | ST, // swph
        WR2 | LD | ST, // swpah
        WR2 | LD | ST, // swpalh
        WR2 | LD | ST, // swplh
        WR2 | LD | ST, // swp
        WR2 | LD | ST, // swpa
        WR2 | LD | ST, // swpal
        WR2 | LD | ST, // swpl
        0, // adr
        0, // adrp
        0, // b
        0, // b_tail
        0, // bl_local
        0, // bl
        0, // br
        0, // br_tail
        0, // blr
        0, // ret
        0, // retaa
        0, // retab
        0, // beq
        0, // bne
        0, // bhs
        0, // blo
        0, // bmi
        0, // bpl
        0, // bvs
        0, // bvc
        0, // bhi
        0, // bls
        0, // bge
        0, // blt
        0, // bgt
        0, // ble
        0, // cbz
        0, // cbnz
        0, // tbz
        0, // tbnz
        0, // movk
        0, // movn
        0, // movz
        0, // csel
        0, // csinc
        0, // csinv
        0, // csneg
        0, // cinc
        0, // cinv
        0, // cneg
        0, // cset
        0, // csetm
        0, // aese
        0, // aesd
        0, // aesmc
        0, // aesimc
        0, // rev
        0, // rev64
        0, // adc
        0, // adcs
        0, // sbc
        0, // sbcs
        0, // udiv
        0, // sdiv
        0, // mneg
        0, // madd
        0, // msub
        0, // smaddl
        0, // smnegl
        0, // smsubl
        0, // smulh
        0, // umaddl
        0, // umnegl
        0, // umsubl
        0, // umulh
        0, // extr
        0, // lslv
        0, // lsrv
        0, // asrv
        0, // rorv
        0, // crc32b
        0, // crc32h
        0, // crc32w
        0, // crc32x
        0, // crc32cb
        0, // crc32ch
        0, // crc32cw
        0, // crc32cx
        0, // sha1c
        0, // sha1m
        0, // sha1p
        0, // sha1h
        0, // sha1su0
        0, // sha1su1
        0, // sha256h
        0, // sha256h2
        0, // sha256su0
        0, // sha256su1
        0, // ext
        0, // sbfm
        0, // bfm
        0, // ubfm
        0, // sbfiz
        0, // bfi
        0, // ubfiz
        0, // sbfx
        0, // bfxil
        0, // ubfx
        0, // sxtb
        0, // sxth
        0, // sxtw
        0, // uxtb
        0, // uxth
        0, // autia1716
        0, // autiasp
        0, // autib1716
        0, // autibsp
        0, // autibz
        0, // autiaz
        0, // pacia1716
        0, // paciasp
        0, // pacib1716
        0, // pacibsp
        0, // pacibz
        0, // paciaz
        0, // xpaclri
        0, // autiza
        0, // autizb
        0, // paciza
        0, // pacizb
        0, // xpacd
        0, // xpaci
        0, // autia
        0, // autib
        0, // pacia
        0, // pacib
        0, // nop
        0, // yield
        0, // brk
        0, // dsb
        0, // dmb
        0, // isb
        0, // dczva
        0, // mrs_tpid0
        0, // umov
        0, // smov
        0, // movi
        0, // mvni
        0, // urecpe
        0, // ursqrte
        0, // bsl
        0, // bit
        0, // bif
        0, // addv
        0, // ctz
        0, // not
        LNG, // saddlv
        0, // smaxv
        0, // sminv
        0, // uaddlv
        0, // umaxv
        0, // uminv
        0, // fmaxnmv
        0, // fmaxv
        0, // fminnmv
        0, // fminv
        0, // uzp1
        0, // uzp2
        0, // zip1
        0, // zip2
        0, // trn1
        0, // trn2
        NRW, // sqxtn2
        NRW, // sqxtun2
        NRW, // uqxtn2
        NRW, // xtn
        NRW, // xtn2
        0, // fnmul
        0, // fmadd
        0, // fmsub
        0, // fnmadd
        0, // fnmsub
        0, // fcvt
        0, // pmul
        0, // saba
        0, // sabd
        0, // smax
        0, // smaxp
        0, // smin
        0, // sminp
        0, // uaba
        0, // uabd
        0, // umax
        0, // umaxp
        0, // umin
        0, // uminp
        LNG, // fcvtl
        LNG, // fcvtl2
        NRW, // fcvtn
        NRW, // fcvtn2
        NRW, // fcvtxn2
        0, // frecpx
        NRW, // addhn
        NRW, // addhn2
        LNG, // pmull
        LNG, // pmull2
        NRW, // raddhn
        NRW, // raddhn2
        NRW, // rsubhn
        NRW, // rsubhn2
        LNG, // sabal
        LNG, // sabal2
        LNG, // sabdl
        LNG, // sabdl2
        LNG, // sadalp
        LNG, // saddl
        LNG, // saddl2
        LNG, // saddlp
        WID, // saddw
        WID, // saddw2
        0, // shadd
        0, // shsub
        0, // srhadd
        LNG, // ssubl
        LNG, // ssubl2
        WID, // ssubw
        WID, // ssubw2
        NRW, // subhn
        NRW, // subhn2
        LNG, // uabal
        LNG, // uabal2
        LNG, // uabdl
        LNG, // uabdl2
        LNG, // uadalp
        LNG, // uaddl
        LNG, // uaddl2
        LNG, // uaddlp
        WID, // uaddw
        WID, // uaddw2
        0, // uhadd
        0, // uhsub
        0, // urhadd
        LNG, // usubl
        LNG, // usubl2
        WID, // usubw
        WID, // usubw2
        LNG, // shll
        LNG, // shll2
        LNG, // sshll
        LNG, // sshll2
        LNG, // ushll
        LNG, // ushll2
        RSH | NRW, // shrn
        RSH | NRW, // shrn2
        RSH | NRW, // rshrn
        RSH | NRW, // rshrn2
        RSH | NRW, // sqrshrn2
        RSH | NRW, // sqrshrun2
        RSH | NRW, // sqshrn2
        RSH | NRW, // sqshrun2
        RSH | NRW, // uqrshrn2
        RSH | NRW, // uqshrn2
        LNG, // sxtl
        LNG, // sxtl2
        LNG, // uxtl
        LNG, // uxtl2
        0, // tbl
        0, // tbl_2regs
        0, // tbl_3regs
        0, // tbl_4regs
        0, // tbx
        0, // tbx_2regs
        0, // tbx_3regs
        0, // tbx_4regs
#if FEATURE_LOOP_ALIGN
        0, // align
#endif
        0, // eor3
        0, // bcax
        0, // sm3ss1
        0, // sha512h
        0, // sha512h2
        0, // sha512su1
        0, // rax1
        0, // sm3partw1
        0, // sm3partw2
        0, // sm4ekey
        0, // xar
        0, // sha512su0
        0, // sm4e
#if !TARGET_ARM64
#error Unexpected target type
#endif
        0, // invalid
        0, // mov
        ST, // st1w
        LD, // ld1sh
        LD, // ld1h
        LD, // ld1w
        LD, // ld1d
        ST, // st1h
        ST, // st1d
        0, // pmov
        LD, // ldff1sh
        LD, // ldff1w
        LD, // ldff1h
        LD, // ld1sw
        0, // mul
        0, // fdot
        LD, // ld1sb
        LD, // ld1b
        0, // prfb
        0, // prfd
        0, // prfh
        0, // prfw
        LD, // ldff1d
        LD, // ldff1sw
        ST, // st1b
        RSH, // asr
        0, // lsl
        RSH, // lsr
        0, // fmul
        0, // sdot
        0, // udot
        LD, // ldff1sb
        LD, // ldff1b
        0, // and
        0, // bic
        0, // eor
        0, // orr
        0, // fmov
        0, // sqdmulh
        0, // sqrdmulh
        0, // sqrdmlah
        0, // sqrdmlsh
        0, // mla
        0, // mls
        0, // fmlalb
        0, // fmlalt
        0, // index
        0, // cpy
        LD, // ldnt1b
        LD, // ldnt1h
        LD, // ldnt1w
        ST, // stnt1b
        ST, // stnt1h
        ST, // stnt1w
        0, // add
        0, // sub
        0, // adr
        0, // dup
        0, // trn1
        0, // trn2
        0, // uzp1
        0, // uzp2
        0, // zip1
        0, // zip2
        0, // sqadd
        0, // sqsub
        0, // uqadd
        0, // uqsub
        0, // fmla
        0, // fmls
        0, // luti4
        0, // fadd
        0, // fsub
        0, // clasta
        0, // clastb
        CMP, // cmpeq
        CMP, // cmpge
        CMP, // cmpgt
        CMP, // cmple
        CMP, // cmplt
        CMP, // cmpne
        CMP, // cmphi
        CMP, // cmphs
        CMP, // cmplo
        CMP, // cmpls
        0, // whilege
        0, // whilegt
        0, // whilehi
        0, // whilehs
        0, // whilele
        0, // whilelo
        0, // whilels
        0, // whilelt
        0, // cdot
        0, // cmla
        0, // sqrdcmlah
        0, // smlalb
        0, // smlalt
        0, // smlslb
        0, // smlslt
        0, // umlalb
        0, // umlalt
        0, // umlslb
        0, // umlslt
        0, // sqdmlalb
        0, // sqdmlalt
        0, // sqdmlslb
        0, // sqdmlslt
        0, // smullb
        0, // smullt
        0, // umullb
        0, // umullt
        0, // sqdmullb
        0, // sqdmullt
        0, // bfmul
        LD, // ldnt1d
        ST, // stnt1d
        LD, // ldr
        ST, // str
        0, // smax
        0, // smin
        0, // umax
        0, // umin
        0, // addpt
        0, // subpt
        0, // rev
        0, // smulh
        0, // umulh
        0, // orn
        0, // ext
        0, // sqshl
        0, // uqshl
        CMP, // fcmeq
        CMP, // fcmge
        CMP, // fcmgt
        CMP, // fcmle
        CMP, // fcmlt
        CMP, // fcmne
        0, // tbl
        0, // luti2
        0, // fmax
        0, // fmaxnm
        0, // fmin
        0, // fminnm
        0, // fsubr
        0, // usdot
        0, // fcmla
        0, // bfdot
        0, // fmlallbb
        0, // fmlallbt
        0, // fmlalltb
        0, // fmlalltt
        0, // not
        0, // subr
        0, // movprfx
        0, // decd
        0, // dech
        0, // decw
        0, // incd
        0, // inch
        0, // incw
        0, // sqdecd
        0, // sqdech
        0, // sqdecw
        0, // sqincd
        0, // sqinch
        0, // sqincw
        0, // uqdecd
        0, // uqdech
        0, // uqdecw
        0, // uqincd
        0, // uqinch
        0, // uqincw
        0, // insr
        0, // lasta
        0, // lastb
        0, // splice
        0, // sel
        0, // movs
        0, // ptrue
        0, // rdffr
        0, // cntp
        0, // decp
        0, // incp
        0, // sqdecp
        0, // sqincp
        0, // uqdecp
        0, // uqincp
        0, // pext
        0, // pmullb
        0, // pmullt
        0, // fcvtnt
        0, // bfmla
        0, // bfmls
        0, // bfmlalb
        0, // bfmlalt
        0, // bfmlslb
        0, // bfmlslt
        0, // fmlslb
        0, // fmlslt
        0, // bfadd
        0, // bfsub
        LD, // ldnt1sb
        LD, // ldnt1sh
        LD, // ld1rob
        LD, // ld1rod
        LD, // ld1roh
        LD, // ld1row
        LD, // ld1rqb
        LD, // ld1rqd
        LD, // ld1rqh
        LD, // ld1rqw
        LD, // ld2q
        LD, // ld3q
        LD, // ld4q
        LD, // ld2b
        LD, // ld2d
        LD, // ld2h
        LD, // ld2w
        LD, // ld3b
        LD, // ld3d
        LD, // ld3h
        LD, // ld3w
        LD, // ld4b
        LD, // ld4d
        LD, // ld4h
        LD, // ld4w
        ST, // st2b
        ST, // st2d
        ST, // st2h
        ST, // st2w
        ST, // st3b
        ST, // st3d
        ST, // st3h
        ST, // st3w
        ST, // st4b
        ST, // st4d
        ST, // st4h
        ST, // st4w
        ST, // st2q
        ST, // st3q
        ST, // st4q
        0, // abs
        0, // neg
        0, // sxtb
        0, // sxth
        0, // sxtw
        0, // uxtb
        0, // uxth
        0, // uxtw
        0, // ands
        0, // bics
        0, // eors
        0, // nand
        0, // nands
        0, // nor
        0, // nors
        0, // nots
        0, // orns
        0, // orrs
        0, // rbit
        0, // revb
        0, // revh
        0, // revw
        0, // cls
        0, // clz
        0, // cnot
        0, // cnt
        0, // fabs
        0, // fneg
        0, // sdiv
        0, // sdivr
        0, // udiv
        0, // udivr
        0, // eon
        0, // smaxv
        0, // sminv
        0, // umaxv
        0, // uminv
        0, // faddv
        0, // fmaxnmv
        0, // fmaxv
        0, // fminnmv
        0, // fminv
        0, // addp
        0, // smaxp
        0, // sminp
        0, // umaxp
        0, // uminp
        0, // faddp
        0, // fmaxnmp
        0, // fmaxp
        0, // fminnmp
        0, // fminp
        RSH, // srsra
        RSH, // ssra
        RSH, // ursra
        RSH, // usra
        RSH, // asrd
        0, // sqshlu
        RSH, // srshr
        RSH, // urshr
        RSH, // sqrshrn
        RSH, // sqrshrun
        RSH, // uqrshrn
        0, // sli
        RSH, // sri
        0, // sqrshl
        0, // sqrshlr
        0, // sqshlr
        0, // srshl
        0, // srshlr
        0, // uqrshl
        0, // uqrshlr
        0, // uqshlr
        0, // urshl
        0, // urshlr
        0, // fabd
        0, // famax
        0, // famin
        0, // fdiv
        0, // fdivr
        0, // fmulx
        0, // fscale
        0, // frecps
        0, // frsqrts
        0, // ftsmul
        CMP, // facge
        CMP, // facgt
        CMP, // facle
        CMP, // faclt
        CMP, // fcmuo
        0, // sqsubr
        0, // suqadd
        0, // uqsubr
        0, // usqadd
        0, // sqabs
        0, // sqneg
        0, // urecpe
        0, // ursqrte
        0, // frecpe
        0, // frsqrte
        0, // frecpx
        0, // fsqrt
        0, // tbx
        0, // shadd
        0, // shsub
        0, // shsubr
        0, // srhadd
        0, // uhadd
        0, // uhsub
        0, // uhsubr
        0, // urhadd
        0, // sabd
        0, // uabd
        0, // saba
        0, // uaba
        0, // pmul
        0, // bcax
        0, // bsl
        0, // bsl1n
        0, // bsl2n
        0, // eor3
        0, // nbsl
        0, // bfcvtn
        NRW, // fcvtn
        0, // fcvtnb
        0, // fcadd
        0, // smmla
        0, // ummla
        0, // usmmla
        0, // bfmmla
        LNG, // sadalp
        LNG, // uadalp
        0, // frinta
        0, // frinti
        0, // frintm
        0, // frintn
        0, // frintp
        0, // frintx
        0, // frintz
        0, // sudot
        0, // aesd
        0, // aese
        0, // sm4e
        0, // aesimc
        0, // aesmc
        0, // rax1
        0, // sm4ekey
        0, // xar
        0, // andv
        0, // eorv
        0, // orv
        0, // andqv
        0, // eorqv
        0, // orqv
        0, // saddv
        0, // uaddv
        0, // addqv
        0, // smaxqv
        0, // sminqv
        0, // umaxqv
        0, // uminqv
        RSH, // asrr
        RSH, // lslr
        RSH, // lsrr
        0, // mad
        0, // msb
        0, // addpl
        0, // addvl
        0, // rdvl
        0, // fexpa
        0, // ftssel
        0, // cntb
        0, // cntd
        0, // cnth
        0, // cntw
        0, // decb
        0, // incb
        0, // sqdecb
        0, // sqincb
        0, // uqdecb
        0, // uqincb
        0, // dupm
        0, // fcpy
        0, // dupq
        0, // extq
        0, // tbxq
        0, // sunpkhi
        0, // sunpklo
        0, // uunpkhi
        0, // uunpklo
        0, // punpkhi
        0, // punpklo
        0, // compact
        0, // revd
        0, // brkpa
        0, // brkpas
        0, // brkpb
        0, // brkpbs
        0, // brka
        0, // brkb
        0, // brkas
        0, // brkbs
        0, // brkn
        0, // brkns
        0, // pfirst
        0, // ptrues
        0, // pnext
        0, // rdffrs
        0, // ptest
        0, // pfalse
        0, // setffr
        0, // wrffr
        0, // ctermeq
        0, // ctermne
        0, // whilerw
        0, // whilewr
        0, // psel
        0, // fdup
        0, // sqdmlalbt
        0, // sqdmlslbt
        0, // sclamp
        0, // uclamp
        0, // mlapt
        0, // madpt
        0, // tblq
        0, // uzpq1
        0, // uzpq2
        0, // zipq1
        0, // zipq2
        0, // sabdlb
        0, // sabdlt
        0, // saddlb
        0, // saddlt
        0, // ssublb
        0, // ssublt
        0, // uabdlb
        0, // uabdlt
        0, // uaddlb
        0, // uaddlt
        0, // usublb
        0, // usublt
        0, // saddwb
        0, // saddwt
        0, // ssubwb
        0, // ssubwt
        0, // uaddwb
        0, // uaddwt
        0, // usubwb
        0, // usubwt
        0, // eorbt
        0, // eortb
        0, // bdep
        0, // bext
        0, // bgrp
        0, // sshllb
        0, // sshllt
        0, // ushllb
        0, // ushllt
        0, // saddlbt
        0, // ssublbt
        0, // ssubltb
        0, // cadd
        0, // sqcadd
        0, // sabalb
        0, // sabalt
        0, // uabalb
        0, // uabalt
        0, // adclb
        0, // adclt
        0, // sbclb
        0, // sbclt
        0, // sqcvtn
        0, // sqcvtun
        0, // uqcvtn
        0, // rshrnb
        0, // rshrnt
        0, // shrnb
        0, // shrnt
        0, // sqrshrnb
        0, // sqrshrnt
        0, // sqrshrunb
        0, // sqrshrunt
        0, // sqshrnb
        0, // sqshrnt
        0, // sqshrunb
        0, // sqshrunt
        0, // uqrshrnb
        0, // uqrshrnt
        0, // uqshrnb
        0, // uqshrnt
        0, // addhnb
        0, // addhnt
        0, // raddhnb
        0, // raddhnt
        0, // rsubhnb
        0, // rsubhnt
        0, // subhnb
        0, // subhnt
        0, // sqxtnb
        0, // sqxtnt
        0, // sqxtunb
        0, // sqxtunt
        0, // uqxtnb
        0, // uqxtnt
        0, // match
        0, // nmatch
        0, // histseg
        0, // histcnt
        0, // bfcvtnt
        0, // fcvtlt
        0, // fcvtxnt
        0, // faddqv
        0, // fmaxnmqv
        0, // fmaxqv
        0, // fminnmqv
        0, // fminqv
        0, // fclamp
        0, // bfclamp
        0, // fmmla
        0, // bf1cvt
        0, // bf1cvtlt
        0, // bf2cvt
        0, // bf2cvtlt
        0, // f1cvt
        0, // f1cvtlt
        0, // f2cvt
        0, // f2cvtlt
        0, // fadda
        0, // bfmax
        0, // bfmaxnm
        0, // bfmin
        0, // bfminnm
        0, // ftmad
        0, // bfcvt
        0, // fcvt
        0, // fcvtx
        0, // flogb
        0, // fcvtzs
        0, // fcvtzu
        0, // scvtf
        0, // ucvtf
        0, // fnmla
        0, // fnmls
        0, // fmad
        0, // fmsb
        0, // fnmad
        0, // fnmsb
        LD, // ld1rb
        LD, // ld1rd
        LD, // ld1rsw
        LD, // ld1rh
        LD, // ld1rsb
        LD, // ld1rsh
        LD, // ld1rw
        LD, // ldnf1b
        LD, // ldnf1d
        LD, // ldnf1sw
        LD, // ldnf1h
        LD, // ldnf1sb
        LD, // ldnf1sh
        LD, // ldnf1w
        LD, // ld1q
        LD, // ldnt1sw
        ST, // st1q
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

        0, // INS_lea is zero-initialized in native instInfo[INS_count].
    ];
#elif TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
    private const byte INST_FP = 1;

#if TARGET_ARM
    private const byte LD = 2;
    private const byte ST = 4;
    private const byte CMP = 8;
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
    private const byte LD = 1;
    private const byte ST = 2;
#endif

    internal static ReadOnlySpan<byte> instInfo => [
#if TARGET_XARCH
#if !TARGET_XARCH
#error Unexpected target type
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
#if TARGET_AMD64
#endif
#if TARGET_X86
#endif
#elif TARGET_ARM
#if !TARGET_ARM
#error Unexpected target type
#endif
        (0) | (INST_FP * (0)), // INS_invalid
        (0) | (INST_FP * (0)), // INS_add
        (0) | (INST_FP * (0)), // INS_sub
        (LD) | (INST_FP * (0)), // INS_ldr
        (ST) | (INST_FP * (0)), // INS_str
        (LD) | (INST_FP * (0)), // INS_ldrb
        (ST) | (INST_FP * (0)), // INS_strb
        (LD) | (INST_FP * (0)), // INS_ldrh
        (ST) | (INST_FP * (0)), // INS_strh
        (LD) | (INST_FP * (0)), // INS_ldrsb
        (LD) | (INST_FP * (0)), // INS_ldrsh
        (0) | (INST_FP * (0)), // INS_mov
        (CMP) | (INST_FP * (0)), // INS_cmp
        (0) | (INST_FP * (0)), // INS_lsl
        (0) | (INST_FP * (0)), // INS_lsr
        (0) | (INST_FP * (0)), // INS_asr
        (0) | (INST_FP * (0)), // INS_ror
        (LD) | (INST_FP * (0)), // INS_pld
        (LD) | (INST_FP * (0)), // INS_pldw
#if FEATURE_PLI_INSTRUCTION
        (LD) | (INST_FP * (0)), // INS_pli
#endif
        (0) | (INST_FP * (0)), // INS_movt
        (0) | (INST_FP * (0)), // INS_movw
        (0) | (INST_FP * (0)), // INS_and
        (0) | (INST_FP * (0)), // INS_eor
        (0) | (INST_FP * (0)), // INS_orr
        (0) | (INST_FP * (0)), // INS_orn
        (0) | (INST_FP * (0)), // INS_bic
        (0) | (INST_FP * (0)), // INS_adc
        (0) | (INST_FP * (0)), // INS_sbc
        (0) | (INST_FP * (0)), // INS_rsb
        (CMP) | (INST_FP * (0)), // INS_tst
        (CMP) | (INST_FP * (0)), // INS_teq
        (CMP) | (INST_FP * (0)), // INS_cmn
        (0) | (INST_FP * (0)), // INS_mvn
        (0) | (INST_FP * (0)), // INS_push
        (0) | (INST_FP * (0)), // INS_pop
        (0) | (INST_FP * (0)), // INS_b
        (0) | (INST_FP * (0)), // INS_beq
        (0) | (INST_FP * (0)), // INS_bne
        (0) | (INST_FP * (0)), // INS_bhs
        (0) | (INST_FP * (0)), // INS_blo
        (0) | (INST_FP * (0)), // INS_bmi
        (0) | (INST_FP * (0)), // INS_bpl
        (0) | (INST_FP * (0)), // INS_bvs
        (0) | (INST_FP * (0)), // INS_bvc
        (0) | (INST_FP * (0)), // INS_bhi
        (0) | (INST_FP * (0)), // INS_bls
        (0) | (INST_FP * (0)), // INS_bge
        (0) | (INST_FP * (0)), // INS_blt
        (0) | (INST_FP * (0)), // INS_bgt
        (0) | (INST_FP * (0)), // INS_ble
        (0) | (INST_FP * (0)), // INS_bx
        (0) | (INST_FP * (0)), // INS_blx
        (LD) | (INST_FP * (0)), // INS_ldm
        (ST) | (INST_FP * (0)), // INS_stm
        (0) | (INST_FP * (0)), // INS_sxtb
        (0) | (INST_FP * (0)), // INS_sxth
        (0) | (INST_FP * (0)), // INS_uxtb
        (0) | (INST_FP * (0)), // INS_uxth
        (0) | (INST_FP * (0)), // INS_mul
        (0) | (INST_FP * (0)), // INS_adr
        (0) | (INST_FP * (0)), // INS_addw
        (0) | (INST_FP * (0)), // INS_bfc
        (0) | (INST_FP * (0)), // INS_bfi
        (0) | (INST_FP * (0)), // INS_bl
        (0) | (INST_FP * (0)), // INS_bkpt
        (0) | (INST_FP * (0)), // INS_cbnz
        (0) | (INST_FP * (0)), // INS_cbz
        (0) | (INST_FP * (0)), // INS_clz
        (0) | (INST_FP * (0)), // INS_dmb
        (0) | (INST_FP * (0)), // INS_ism
        (LD) | (INST_FP * (0)), // INS_ldmdb
        (LD) | (INST_FP * (0)), // INS_ldrd
        (LD) | (INST_FP * (0)), // INS_ldrex
        (LD) | (INST_FP * (0)), // INS_ldrexb
        (LD) | (INST_FP * (0)), // INS_ldrexd
        (LD) | (INST_FP * (0)), // INS_ldrexh
        (0) | (INST_FP * (0)), // INS_mla
        (0) | (INST_FP * (0)), // INS_mls
        (0) | (INST_FP * (0)), // INS_nop
        (0) | (INST_FP * (0)), // INS_nopw
        (0) | (INST_FP * (0)), // INS_sbfx
        (0) | (INST_FP * (0)), // INS_sdiv
        (0) | (INST_FP * (0)), // INS_ssat
        (0) | (INST_FP * (0)), // INS_smlal
        (0) | (INST_FP * (0)), // INS_smull
        (ST) | (INST_FP * (0)), // INS_stmdb
        (ST) | (INST_FP * (0)), // INS_strd
        (ST) | (INST_FP * (0)), // INS_strex
        (ST) | (INST_FP * (0)), // INS_strexb
        (ST) | (INST_FP * (0)), // INS_strexd
        (ST) | (INST_FP * (0)), // INS_strexh
        (0) | (INST_FP * (0)), // INS_subw
        (0) | (INST_FP * (0)), // INS_tbb
        (0) | (INST_FP * (0)), // INS_tbh
        (0) | (INST_FP * (0)), // INS_ubfx
        (0) | (INST_FP * (0)), // INS_udiv
        (0) | (INST_FP * (0)), // INS_umlal
        (0) | (INST_FP * (0)), // INS_umull
        (0) | (INST_FP * (0)), // INS_usat
#if FEATURE_ITINSTRUCTION
        (0) | (INST_FP * (0)), // INS_it
        (0) | (INST_FP * (0)), // INS_itt
        (0) | (INST_FP * (0)), // INS_ite
        (0) | (INST_FP * (0)), // INS_ittt
        (0) | (INST_FP * (0)), // INS_itte
        (0) | (INST_FP * (0)), // INS_itet
        (0) | (INST_FP * (0)), // INS_itee
        (0) | (INST_FP * (0)), // INS_itttt
        (0) | (INST_FP * (0)), // INS_ittte
        (0) | (INST_FP * (0)), // INS_ittet
        (0) | (INST_FP * (0)), // INS_ittee
        (0) | (INST_FP * (0)), // INS_itett
        (0) | (INST_FP * (0)), // INS_itete
        (0) | (INST_FP * (0)), // INS_iteet
        (0) | (INST_FP * (0)), // INS_iteee
#endif
        (ST) | (INST_FP * (1)), // INS_vstr
        (LD) | (INST_FP * (1)), // INS_vldr
        (ST) | (INST_FP * (1)), // INS_vstm
        (LD) | (INST_FP * (1)), // INS_vldm
        (ST) | (INST_FP * (1)), // INS_vpush
        (LD) | (INST_FP * (1)), // INS_vpop
        (0) | (INST_FP * (1)), // INS_vmrs
        (0) | (INST_FP * (1)), // INS_vadd
        (0) | (INST_FP * (1)), // INS_vsub
        (0) | (INST_FP * (1)), // INS_vmul
        (0) | (INST_FP * (1)), // INS_vdiv
        (0) | (INST_FP * (1)), // INS_vmov
        (0) | (INST_FP * (1)), // INS_vabs
        (0) | (INST_FP * (1)), // INS_vsqrt
        (0) | (INST_FP * (1)), // INS_vneg
        (CMP) | (INST_FP * (1)), // INS_vcmp
        (CMP) | (INST_FP * (1)), // INS_vcmp0
        (0) | (INST_FP * (1)), // INS_vcvt_d2i
        (0) | (INST_FP * (1)), // INS_vcvt_f2i
        (0) | (INST_FP * (1)), // INS_vcvt_d2u
        (0) | (INST_FP * (1)), // INS_vcvt_f2u
        (0) | (INST_FP * (1)), // INS_vcvt_i2f
        (0) | (INST_FP * (1)), // INS_vcvt_i2d
        (0) | (INST_FP * (1)), // INS_vcvt_u2f
        (0) | (INST_FP * (1)), // INS_vcvt_u2d
        (0) | (INST_FP * (1)), // INS_vcvt_d2f
        (0) | (INST_FP * (1)), // INS_vcvt_f2d
        (0) | (INST_FP * (1)), // INS_vmov_i2d
        (0) | (INST_FP * (1)), // INS_vmov_d2i
        (0) | (INST_FP * (1)), // INS_vmov_i2f
        (0) | (INST_FP * (1)), // INS_vmov_f2i
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
        0, // INS_invalid
        0, // INS_nop
        0, // INS_bceqz
        0, // INS_bcnez
        0, // INS_beq
        0, // INS_bne
        0, // INS_blt
        0, // INS_bge
        0, // INS_bltu
        0, // INS_bgeu
        0, // INS_beqz
        0, // INS_bnez
        0, // INS_b
        0, // INS_bl
        0, // INS_mov
        0, // INS_dneg
        0, // INS_neg
        0, // INS_not
        0, // INS_add_w
        0, // INS_add_d
        0, // INS_sub_w
        0, // INS_sub_d
        0, // INS_and
        0, // INS_or
        0, // INS_nor
        0, // INS_xor
        0, // INS_andn
        0, // INS_orn
        0, // INS_mul_w
        0, // INS_mul_d
        0, // INS_mulh_w
        0, // INS_mulh_wu
        0, // INS_mulh_d
        0, // INS_mulh_du
        0, // INS_mulw_d_w
        0, // INS_mulw_d_wu
        0, // INS_div_w
        0, // INS_mod_w
        0, // INS_div_wu
        0, // INS_mod_wu
        0, // INS_div_d
        0, // INS_mod_d
        0, // INS_div_du
        0, // INS_mod_du
        0, // INS_sll_w
        0, // INS_srl_w
        0, // INS_sra_w
        0, // INS_rotr_w
        0, // INS_sll_d
        0, // INS_srl_d
        0, // INS_sra_d
        0, // INS_rotr_d
        0, // INS_maskeqz
        0, // INS_masknez
        0, // INS_slt
        0, // INS_sltu
        0, // INS_crc_w_b_w
        0, // INS_crc_w_h_w
        0, // INS_crc_w_w_w
        0, // INS_crc_w_d_w
        0, // INS_crcc_w_b_w
        0, // INS_crcc_w_h_w
        0, // INS_crcc_w_w_w
        0, // INS_crcc_w_d_w
        0, // INS_amcas_b
        0, // INS_amcas_h
        0, // INS_amcas_w
        0, // INS_amcas_d
        LD|ST, // INS_amswap_b
        LD|ST, // INS_amswap_h
        LD|ST, // INS_amswap_w
        LD|ST, // INS_amswap_d
        LD|ST, // INS_amadd_b
        LD|ST, // INS_amadd_h
        LD|ST, // INS_amadd_w
        LD|ST, // INS_amadd_d
        LD|ST, // INS_amand_w
        LD|ST, // INS_amand_d
        LD|ST, // INS_amor_w
        LD|ST, // INS_amor_d
        LD|ST, // INS_amxor_w
        LD|ST, // INS_amxor_d
        LD|ST, // INS_ammax_w
        LD|ST, // INS_ammax_d
        LD|ST, // INS_ammin_w
        LD|ST, // INS_ammin_d
        LD|ST, // INS_ammax_wu
        LD|ST, // INS_ammax_du
        LD|ST, // INS_ammin_wu
        LD|ST, // INS_ammin_du
        0, // INS_amcas_db_b
        0, // INS_amcas_db_h
        0, // INS_amcas_db_w
        0, // INS_amcas_db_d
        LD|ST, // INS_amswap_db_b
        LD|ST, // INS_amswap_db_h
        LD|ST, // INS_amswap_db_w
        LD|ST, // INS_amswap_db_d
        LD|ST, // INS_amadd_db_b
        LD|ST, // INS_amadd_db_h
        LD|ST, // INS_amadd_db_w
        LD|ST, // INS_amadd_db_d
        LD|ST, // INS_amand_db_w
        LD|ST, // INS_amand_db_d
        LD|ST, // INS_amor_db_w
        LD|ST, // INS_amor_db_d
        LD|ST, // INS_amxor_db_w
        LD|ST, // INS_amxor_db_d
        LD|ST, // INS_ammax_db_w
        LD|ST, // INS_ammax_db_d
        LD|ST, // INS_ammin_db_w
        LD|ST, // INS_ammin_db_d
        LD|ST, // INS_ammax_db_wu
        LD|ST, // INS_ammax_db_du
        LD|ST, // INS_ammin_db_wu
        LD|ST, // INS_ammin_db_du
        0, // INS_alsl_w
        0, // INS_alsl_wu
        0, // INS_alsl_d
        0, // INS_bytepick_w
        0, // INS_bytepick_d
        0, // INS_fsel
        0, // INS_lu12i_w
        0, // INS_lu32i_d
        0, // INS_pcaddi
        0, // INS_pcaddu12i
        0, // INS_pcalau12i
        0, // INS_pcaddu18i
        0, // INS_ext_w_b
        0, // INS_ext_w_h
        0, // INS_clo_w
        0, // INS_clz_w
        0, // INS_cto_w
        0, // INS_ctz_w
        0, // INS_clo_d
        0, // INS_clz_d
        0, // INS_cto_d
        0, // INS_ctz_d
        0, // INS_revb_2h
        0, // INS_revb_4h
        0, // INS_revb_2w
        0, // INS_revb_d
        0, // INS_revh_2w
        0, // INS_revh_d
        0, // INS_bitrev_4b
        0, // INS_bitrev_8b
        0, // INS_bitrev_w
        0, // INS_bitrev_d
        0, // INS_rdtimel_w
        0, // INS_rdtimeh_w
        0, // INS_rdtime_d
        0, // INS_cpucfg
        0, // INS_movfr2gr_s
        0, // INS_movfr2gr_d
        0, // INS_movfrh2gr_s
        0, // INS_bstrins_w
        0, // INS_bstrins_d
        0, // INS_bstrpick_w
        0, // INS_bstrpick_d
        LD, // INS_ld_b
        LD, // INS_ld_h
        LD, // INS_ld_w
        LD, // INS_ld_d
        LD, // INS_ld_bu
        LD, // INS_ld_hu
        LD, // INS_ld_wu
        LD, // INS_ldptr_w
        LD, // INS_ldptr_d
        LD, // INS_ll_w
        LD, // INS_ll_d
        LD, // INS_ldx_b
        LD, // INS_ldx_h
        LD, // INS_ldx_w
        LD, // INS_ldx_d
        LD, // INS_ldx_bu
        LD, // INS_ldx_hu
        LD, // INS_ldx_wu
        LD, // INS_ldgt_b
        LD, // INS_ldgt_h
        LD, // INS_ldgt_w
        LD, // INS_ldgt_d
        LD, // INS_ldle_b
        LD, // INS_ldle_h
        LD, // INS_ldle_w
        LD, // INS_ldle_d
        0, // INS_addi_w
        0, // INS_addi_d
        0, // INS_lu52i_d
        0, // INS_slti
        0, // INS_sltui
        0, // INS_andi
        0, // INS_ori
        0, // INS_xori
        0, // INS_slli_w
        0, // INS_srli_w
        0, // INS_srai_w
        0, // INS_rotri_w
        0, // INS_slli_d
        0, // INS_srli_d
        0, // INS_srai_d
        0, // INS_rotri_d
        0, // INS_addu16i_d
        0, // INS_jirl
        ST, // INS_st_b
        ST, // INS_st_h
        ST, // INS_st_w
        ST, // INS_st_d
        ST, // INS_stptr_w
        ST, // INS_stptr_d
        ST, // INS_sc_w
        ST, // INS_sc_d
        ST, // INS_stx_b
        ST, // INS_stx_h
        ST, // INS_stx_w
        ST, // INS_stx_d
        ST, // INS_stgt_b
        ST, // INS_stgt_h
        ST, // INS_stgt_w
        ST, // INS_stgt_d
        ST, // INS_stle_b
        ST, // INS_stle_h
        ST, // INS_stle_w
        ST, // INS_stle_d
        0, // INS_dbar
        0, // INS_ibar
        0, // INS_syscall
        0, // INS_break
        LD, // INS_preld
        LD, // INS_preldx
        0, // INS_fadd_s
        0, // INS_fadd_d
        0, // INS_fsub_s
        0, // INS_fsub_d
        0, // INS_fmul_s
        0, // INS_fmul_d
        0, // INS_fdiv_s
        0, // INS_fdiv_d
        0, // INS_fmax_s
        0, // INS_fmax_d
        0, // INS_fmin_s
        0, // INS_fmin_d
        0, // INS_fmaxa_s
        0, // INS_fmaxa_d
        0, // INS_fmina_s
        0, // INS_fmina_d
        0, // INS_fscaleb_s
        0, // INS_fscaleb_d
        0, // INS_fcopysign_s
        0, // INS_fcopysign_d
        LD, // INS_fldx_s
        LD, // INS_fldx_d
        ST, // INS_fstx_s
        ST, // INS_fstx_d
        0, // INS_fldgt_s
        0, // INS_fldgt_d
        0, // INS_fldle_s
        0, // INS_fldle_d
        0, // INS_fstgt_s
        0, // INS_fstgt_d
        0, // INS_fstle_s
        0, // INS_fstle_d
        0, // INS_fmadd_s
        0, // INS_fmadd_d
        0, // INS_fmsub_s
        0, // INS_fmsub_d
        0, // INS_fnmadd_s
        0, // INS_fnmadd_d
        0, // INS_fnmsub_s
        0, // INS_fnmsub_d
        0, // INS_fabs_s
        0, // INS_fabs_d
        0, // INS_fneg_s
        0, // INS_fneg_d
        0, // INS_fsqrt_s
        0, // INS_fsqrt_d
        0, // INS_frsqrt_s
        0, // INS_frsqrt_d
        0, // INS_frsqrte_s
        0, // INS_frsqrte_d
        0, // INS_frecip_s
        0, // INS_frecip_d
        0, // INS_frecipe_s
        0, // INS_frecipe_d
        0, // INS_flogb_s
        0, // INS_flogb_d
        0, // INS_fclass_s
        0, // INS_fclass_d
        0, // INS_fcvt_s_d
        0, // INS_fcvt_d_s
        0, // INS_ffint_s_w
        0, // INS_ffint_s_l
        0, // INS_ffint_d_w
        0, // INS_ffint_d_l
        0, // INS_ftint_w_s
        0, // INS_ftint_w_d
        0, // INS_ftint_l_s
        0, // INS_ftint_l_d
        0, // INS_ftintrm_w_s
        0, // INS_ftintrm_w_d
        0, // INS_ftintrm_l_s
        0, // INS_ftintrm_l_d
        0, // INS_ftintrp_w_s
        0, // INS_ftintrp_w_d
        0, // INS_ftintrp_l_s
        0, // INS_ftintrp_l_d
        0, // INS_ftintrz_w_s
        0, // INS_ftintrz_w_d
        0, // INS_ftintrz_l_s
        0, // INS_ftintrz_l_d
        0, // INS_ftintrne_w_s
        0, // INS_ftintrne_w_d
        0, // INS_ftintrne_l_s
        0, // INS_ftintrne_l_d
        0, // INS_frint_s
        0, // INS_frint_d
        0, // INS_fmov_s
        0, // INS_fmov_d
        0, // INS_movgr2fr_w
        0, // INS_movgr2fr_d
        0, // INS_movgr2frh_w
        0, // INS_movgr2fcsr
        0, // INS_movfcsr2gr
        0, // INS_movfr2cf
        0, // INS_movcf2fr
        0, // INS_movgr2cf
        0, // INS_movcf2gr
        0, // INS_fcmp_caf_s
        0, // INS_fcmp_cun_s
        0, // INS_fcmp_ceq_s
        0, // INS_fcmp_cueq_s
        0, // INS_fcmp_clt_s
        0, // INS_fcmp_cult_s
        0, // INS_fcmp_cle_s
        0, // INS_fcmp_cule_s
        0, // INS_fcmp_cne_s
        0, // INS_fcmp_cor_s
        0, // INS_fcmp_cune_s
        0, // INS_fcmp_saf_d
        0, // INS_fcmp_sun_d
        0, // INS_fcmp_seq_d
        0, // INS_fcmp_sueq_d
        0, // INS_fcmp_slt_d
        0, // INS_fcmp_sult_d
        0, // INS_fcmp_sle_d
        0, // INS_fcmp_sule_d
        0, // INS_fcmp_sne_d
        0, // INS_fcmp_sor_d
        0, // INS_fcmp_sune_d
        0, // INS_fcmp_caf_d
        0, // INS_fcmp_cun_d
        0, // INS_fcmp_ceq_d
        0, // INS_fcmp_cueq_d
        0, // INS_fcmp_clt_d
        0, // INS_fcmp_cult_d
        0, // INS_fcmp_cle_d
        0, // INS_fcmp_cule_d
        0, // INS_fcmp_cne_d
        0, // INS_fcmp_cor_d
        0, // INS_fcmp_cune_d
        0, // INS_fcmp_saf_s
        0, // INS_fcmp_sun_s
        0, // INS_fcmp_seq_s
        0, // INS_fcmp_sueq_s
        0, // INS_fcmp_slt_s
        0, // INS_fcmp_sult_s
        0, // INS_fcmp_sle_s
        0, // INS_fcmp_sule_s
        0, // INS_fcmp_sne_s
        0, // INS_fcmp_sor_s
        0, // INS_fcmp_sune_s
        LD, // INS_fld_s
        LD, // INS_fld_d
        ST, // INS_fst_s
        ST, // INS_fst_d
#if FEATURE_SIMD
        0, // INS_vfmadd_s
        0, // INS_vfmadd_d
        0, // INS_vfmsub_s
        0, // INS_vfmsub_d
        0, // INS_vfnmadd_s
        0, // INS_vfnmadd_d
        0, // INS_vfnmsub_s
        0, // INS_vfnmsub_d
        0, // INS_vbitsel_v
        0, // INS_vshuf_b
        LD, // INS_vldx
        ST, // INS_vstx
        0, // INS_vreplve_b
        0, // INS_vreplve_h
        0, // INS_vreplve_w
        0, // INS_vreplve_d
        0, // INS_vfcmp_caf_s
        0, // INS_vfcmp_cun_s
        0, // INS_vfcmp_ceq_s
        0, // INS_vfcmp_cueq_s
        0, // INS_vfcmp_clt_s
        0, // INS_vfcmp_cult_s
        0, // INS_vfcmp_cle_s
        0, // INS_vfcmp_cule_s
        0, // INS_vfcmp_cne_s
        0, // INS_vfcmp_cor_s
        0, // INS_vfcmp_cune_s
        0, // INS_vfcmp_saf_s
        0, // INS_vfcmp_sun_s
        0, // INS_vfcmp_seq_s
        0, // INS_vfcmp_sueq_s
        0, // INS_vfcmp_slt_s
        0, // INS_vfcmp_sult_s
        0, // INS_vfcmp_sle_s
        0, // INS_vfcmp_sule_s
        0, // INS_vfcmp_sne_s
        0, // INS_vfcmp_sor_s
        0, // INS_vfcmp_sune_s
        0, // INS_vfcmp_caf_d
        0, // INS_vfcmp_cun_d
        0, // INS_vfcmp_ceq_d
        0, // INS_vfcmp_cueq_d
        0, // INS_vfcmp_clt_d
        0, // INS_vfcmp_cult_d
        0, // INS_vfcmp_cle_d
        0, // INS_vfcmp_cule_d
        0, // INS_vfcmp_cne_d
        0, // INS_vfcmp_cor_d
        0, // INS_vfcmp_cune_d
        0, // INS_vfcmp_saf_d
        0, // INS_vfcmp_sun_d
        0, // INS_vfcmp_seq_d
        0, // INS_vfcmp_sueq_d
        0, // INS_vfcmp_slt_d
        0, // INS_vfcmp_sult_d
        0, // INS_vfcmp_sle_d
        0, // INS_vfcmp_sule_d
        0, // INS_vfcmp_sne_d
        0, // INS_vfcmp_sor_d
        0, // INS_vfcmp_sune_d
        0, // INS_vseq_b
        0, // INS_vseq_h
        0, // INS_vseq_w
        0, // INS_vseq_d
        0, // INS_vsle_b
        0, // INS_vsle_h
        0, // INS_vsle_w
        0, // INS_vsle_d
        0, // INS_vsle_bu
        0, // INS_vsle_hu
        0, // INS_vsle_wu
        0, // INS_vsle_du
        0, // INS_vslt_b
        0, // INS_vslt_h
        0, // INS_vslt_w
        0, // INS_vslt_d
        0, // INS_vslt_bu
        0, // INS_vslt_hu
        0, // INS_vslt_wu
        0, // INS_vslt_du
        0, // INS_vadd_b
        0, // INS_vadd_h
        0, // INS_vadd_w
        0, // INS_vadd_d
        0, // INS_vsub_b
        0, // INS_vsub_h
        0, // INS_vsub_w
        0, // INS_vsub_d
        0, // INS_vaddwev_h_b
        0, // INS_vaddwev_w_h
        0, // INS_vaddwev_d_w
        0, // INS_vaddwev_q_d
        0, // INS_vsubwev_h_b
        0, // INS_vsubwev_w_h
        0, // INS_vsubwev_d_w
        0, // INS_vsubwev_q_d
        0, // INS_vaddwod_h_b
        0, // INS_vaddwod_w_h
        0, // INS_vaddwod_d_w
        0, // INS_vaddwod_q_d
        0, // INS_vsubwod_h_b
        0, // INS_vsubwod_w_h
        0, // INS_vsubwod_d_w
        0, // INS_vsubwod_q_d
        0, // INS_vaddwev_h_bu
        0, // INS_vaddwev_w_hu
        0, // INS_vaddwev_d_wu
        0, // INS_vaddwev_q_du
        0, // INS_vsubwev_h_bu
        0, // INS_vsubwev_w_hu
        0, // INS_vsubwev_d_wu
        0, // INS_vsubwev_q_du
        0, // INS_vaddwod_h_bu
        0, // INS_vaddwod_w_hu
        0, // INS_vaddwod_d_wu
        0, // INS_vaddwod_q_du
        0, // INS_vsubwod_h_bu
        0, // INS_vsubwod_w_hu
        0, // INS_vsubwod_d_wu
        0, // INS_vsubwod_q_du
        0, // INS_vaddwev_h_bu_b
        0, // INS_vaddwev_w_hu_h
        0, // INS_vaddwev_d_wu_w
        0, // INS_vaddwev_q_du_d
        0, // INS_vaddwod_h_bu_b
        0, // INS_vaddwod_w_hu_h
        0, // INS_vaddwod_d_wu_w
        0, // INS_vaddwod_q_du_d
        0, // INS_vsadd_b
        0, // INS_vsadd_h
        0, // INS_vsadd_w
        0, // INS_vsadd_d
        0, // INS_vssub_b
        0, // INS_vssub_h
        0, // INS_vssub_w
        0, // INS_vssub_d
        0, // INS_vsadd_bu
        0, // INS_vsadd_hu
        0, // INS_vsadd_wu
        0, // INS_vsadd_du
        0, // INS_vssub_bu
        0, // INS_vssub_hu
        0, // INS_vssub_wu
        0, // INS_vssub_du
        0, // INS_vhaddw_h_b
        0, // INS_vhaddw_w_h
        0, // INS_vhaddw_d_w
        0, // INS_vhaddw_q_d
        0, // INS_vhsubw_h_b
        0, // INS_vhsubw_w_h
        0, // INS_vhsubw_d_w
        0, // INS_vhsubw_q_d
        0, // INS_vhaddw_hu_bu
        0, // INS_vhaddw_wu_hu
        0, // INS_vhaddw_du_wu
        0, // INS_vhaddw_qu_du
        0, // INS_vhsubw_hu_bu
        0, // INS_vhsubw_wu_hu
        0, // INS_vhsubw_du_wu
        0, // INS_vhsubw_qu_du
        0, // INS_vadda_b
        0, // INS_vadda_h
        0, // INS_vadda_w
        0, // INS_vadda_d
        0, // INS_vabsd_b
        0, // INS_vabsd_h
        0, // INS_vabsd_w
        0, // INS_vabsd_d
        0, // INS_vabsd_bu
        0, // INS_vabsd_hu
        0, // INS_vabsd_wu
        0, // INS_vabsd_du
        0, // INS_vavg_b
        0, // INS_vavg_h
        0, // INS_vavg_w
        0, // INS_vavg_d
        0, // INS_vavg_bu
        0, // INS_vavg_hu
        0, // INS_vavg_wu
        0, // INS_vavg_du
        0, // INS_vavgr_b
        0, // INS_vavgr_h
        0, // INS_vavgr_w
        0, // INS_vavgr_d
        0, // INS_vavgr_bu
        0, // INS_vavgr_hu
        0, // INS_vavgr_wu
        0, // INS_vavgr_du
        0, // INS_vmax_b
        0, // INS_vmax_h
        0, // INS_vmax_w
        0, // INS_vmax_d
        0, // INS_vmin_b
        0, // INS_vmin_h
        0, // INS_vmin_w
        0, // INS_vmin_d
        0, // INS_vmax_bu
        0, // INS_vmax_hu
        0, // INS_vmax_wu
        0, // INS_vmax_du
        0, // INS_vmin_bu
        0, // INS_vmin_hu
        0, // INS_vmin_wu
        0, // INS_vmin_du
        0, // INS_vmul_b
        0, // INS_vmul_h
        0, // INS_vmul_w
        0, // INS_vmul_d
        0, // INS_vmuh_b
        0, // INS_vmuh_h
        0, // INS_vmuh_w
        0, // INS_vmuh_d
        0, // INS_vmuh_bu
        0, // INS_vmuh_hu
        0, // INS_vmuh_wu
        0, // INS_vmuh_du
        0, // INS_vmulwev_h_b
        0, // INS_vmulwev_w_h
        0, // INS_vmulwev_d_w
        0, // INS_vmulwev_q_d
        0, // INS_vmulwod_h_b
        0, // INS_vmulwod_w_h
        0, // INS_vmulwod_d_w
        0, // INS_vmulwod_q_d
        0, // INS_vmulwev_h_bu
        0, // INS_vmulwev_w_hu
        0, // INS_vmulwev_d_wu
        0, // INS_vmulwev_q_du
        0, // INS_vmulwod_h_bu
        0, // INS_vmulwod_w_hu
        0, // INS_vmulwod_d_wu
        0, // INS_vmulwod_q_du
        0, // INS_vmulwev_h_bu_b
        0, // INS_vmulwev_w_hu_h
        0, // INS_vmulwev_d_wu_w
        0, // INS_vmulwev_q_du_d
        0, // INS_vmulwod_h_bu_b
        0, // INS_vmulwod_w_hu_h
        0, // INS_vmulwod_d_wu_w
        0, // INS_vmulwod_q_du_d
        0, // INS_vmadd_b
        0, // INS_vmadd_h
        0, // INS_vmadd_w
        0, // INS_vmadd_d
        0, // INS_vmsub_b
        0, // INS_vmsub_h
        0, // INS_vmsub_w
        0, // INS_vmsub_d
        0, // INS_vmaddwev_h_b
        0, // INS_vmaddwev_w_h
        0, // INS_vmaddwev_d_w
        0, // INS_vmaddwev_q_d
        0, // INS_vmaddwod_h_b
        0, // INS_vmaddwod_w_h
        0, // INS_vmaddwod_d_w
        0, // INS_vmaddwod_q_d
        0, // INS_vmaddwev_h_bu
        0, // INS_vmaddwev_w_hu
        0, // INS_vmaddwev_d_wu
        0, // INS_vmaddwev_q_du
        0, // INS_vmaddwod_h_bu
        0, // INS_vmaddwod_w_hu
        0, // INS_vmaddwod_d_wu
        0, // INS_vmaddwod_q_du
        0, // INS_vmaddwev_h_bu_b
        0, // INS_vmaddwev_w_hu_h
        0, // INS_vmaddwev_d_wu_w
        0, // INS_vmaddwev_q_du_d
        0, // INS_vmaddwod_h_bu_b
        0, // INS_vmaddwod_w_hu_h
        0, // INS_vmaddwod_d_wu_w
        0, // INS_vmaddwod_q_du_d
        0, // INS_vdiv_b
        0, // INS_vdiv_h
        0, // INS_vdiv_w
        0, // INS_vdiv_d
        0, // INS_vmod_b
        0, // INS_vmod_h
        0, // INS_vmod_w
        0, // INS_vmod_d
        0, // INS_vdiv_bu
        0, // INS_vdiv_hu
        0, // INS_vdiv_wu
        0, // INS_vdiv_du
        0, // INS_vmod_bu
        0, // INS_vmod_hu
        0, // INS_vmod_wu
        0, // INS_vmod_du
        0, // INS_vsll_b
        0, // INS_vsll_h
        0, // INS_vsll_w
        0, // INS_vsll_d
        0, // INS_vsrl_b
        0, // INS_vsrl_h
        0, // INS_vsrl_w
        0, // INS_vsrl_d
        0, // INS_vsra_b
        0, // INS_vsra_h
        0, // INS_vsra_w
        0, // INS_vsra_d
        0, // INS_vrotr_b
        0, // INS_vrotr_h
        0, // INS_vrotr_w
        0, // INS_vrotr_d
        0, // INS_vsrlr_b
        0, // INS_vsrlr_h
        0, // INS_vsrlr_w
        0, // INS_vsrlr_d
        0, // INS_vsrar_b
        0, // INS_vsrar_h
        0, // INS_vsrar_w
        0, // INS_vsrar_d
        0, // INS_vsrln_b_h
        0, // INS_vsrln_h_w
        0, // INS_vsrln_w_d
        0, // INS_vsran_b_h
        0, // INS_vsran_h_w
        0, // INS_vsran_w_d
        0, // INS_vsrlrn_b_h
        0, // INS_vsrlrn_h_w
        0, // INS_vsrlrn_w_d
        0, // INS_vsrarn_b_h
        0, // INS_vsrarn_h_w
        0, // INS_vsrarn_w_d
        0, // INS_vssrln_b_h
        0, // INS_vssrln_h_w
        0, // INS_vssrln_w_d
        0, // INS_vssran_b_h
        0, // INS_vssran_h_w
        0, // INS_vssran_w_d
        0, // INS_vssrlrn_b_h
        0, // INS_vssrlrn_h_w
        0, // INS_vssrlrn_w_d
        0, // INS_vssrarn_b_h
        0, // INS_vssrarn_h_w
        0, // INS_vssrarn_w_d
        0, // INS_vssrln_bu_h
        0, // INS_vssrln_hu_w
        0, // INS_vssrln_wu_d
        0, // INS_vssran_bu_h
        0, // INS_vssran_hu_w
        0, // INS_vssran_wu_d
        0, // INS_vssrlrn_bu_h
        0, // INS_vssrlrn_hu_w
        0, // INS_vssrlrn_wu_d
        0, // INS_vssrarn_bu_h
        0, // INS_vssrarn_hu_w
        0, // INS_vssrarn_wu_d
        0, // INS_vbitclr_b
        0, // INS_vbitclr_h
        0, // INS_vbitclr_w
        0, // INS_vbitclr_d
        0, // INS_vbitset_b
        0, // INS_vbitset_h
        0, // INS_vbitset_w
        0, // INS_vbitset_d
        0, // INS_vbitrev_b
        0, // INS_vbitrev_h
        0, // INS_vbitrev_w
        0, // INS_vbitrev_d
        0, // INS_vpackev_b
        0, // INS_vpackev_h
        0, // INS_vpackev_w
        0, // INS_vpackev_d
        0, // INS_vpackod_b
        0, // INS_vpackod_h
        0, // INS_vpackod_w
        0, // INS_vpackod_d
        0, // INS_vilvl_b
        0, // INS_vilvl_h
        0, // INS_vilvl_w
        0, // INS_vilvl_d
        0, // INS_vilvh_b
        0, // INS_vilvh_h
        0, // INS_vilvh_w
        0, // INS_vilvh_d
        0, // INS_vpickev_b
        0, // INS_vpickev_h
        0, // INS_vpickev_w
        0, // INS_vpickev_d
        0, // INS_vpickod_b
        0, // INS_vpickod_h
        0, // INS_vpickod_w
        0, // INS_vpickod_d
        0, // INS_vand_v
        0, // INS_vor_v
        0, // INS_vxor_v
        0, // INS_vnor_v
        0, // INS_vandn_v
        0, // INS_vorn_v
        0, // INS_vfrstp_b
        0, // INS_vfrstp_h
        0, // INS_vadd_q
        0, // INS_vsub_q
        0, // INS_vsigncov_b
        0, // INS_vsigncov_h
        0, // INS_vsigncov_w
        0, // INS_vsigncov_d
        0, // INS_vfadd_s
        0, // INS_vfadd_d
        0, // INS_vfsub_s
        0, // INS_vfsub_d
        0, // INS_vfmul_s
        0, // INS_vfmul_d
        0, // INS_vfdiv_s
        0, // INS_vfdiv_d
        0, // INS_vfmax_s
        0, // INS_vfmax_d
        0, // INS_vfmin_s
        0, // INS_vfmin_d
        0, // INS_vfmaxa_s
        0, // INS_vfmaxa_d
        0, // INS_vfmina_s
        0, // INS_vfmina_d
        0, // INS_vfcvt_h_s
        0, // INS_vfcvt_s_d
        0, // INS_vffint_s_l
        0, // INS_vftint_w_d
        0, // INS_vftintrm_w_d
        0, // INS_vftintrp_w_d
        0, // INS_vftintrz_w_d
        0, // INS_vftintrne_w_d
        0, // INS_vshuf_h
        0, // INS_vshuf_w
        0, // INS_vshuf_d
        0, // INS_vreplgr2vr_b
        0, // INS_vreplgr2vr_h
        0, // INS_vreplgr2vr_w
        0, // INS_vreplgr2vr_d
        0, // INS_vclo_b
        0, // INS_vclo_h
        0, // INS_vclo_w
        0, // INS_vclo_d
        0, // INS_vclz_b
        0, // INS_vclz_h
        0, // INS_vclz_w
        0, // INS_vclz_d
        0, // INS_vpcnt_b
        0, // INS_vpcnt_h
        0, // INS_vpcnt_w
        0, // INS_vpcnt_d
        0, // INS_vneg_b
        0, // INS_vneg_h
        0, // INS_vneg_w
        0, // INS_vneg_d
        0, // INS_vmskltz_b
        0, // INS_vmskltz_h
        0, // INS_vmskltz_w
        0, // INS_vmskltz_d
        0, // INS_vmskgez_b
        0, // INS_vmsknz_b
        0, // INS_vflogb_s
        0, // INS_vflogb_d
        0, // INS_vfclass_s
        0, // INS_vfclass_d
        0, // INS_vfsqrt_s
        0, // INS_vfsqrt_d
        0, // INS_vfrecip_s
        0, // INS_vfrecip_d
        0, // INS_vfrsqrt_s
        0, // INS_vfrsqrt_d
        0, // INS_vfrecipe_s
        0, // INS_vfrecipe_d
        0, // INS_vfrsqrte_s
        0, // INS_vfrsqrte_d
        0, // INS_vfrint_s
        0, // INS_vfrint_d
        0, // INS_vfrintrm_s
        0, // INS_vfrintrm_d
        0, // INS_vfrintrp_s
        0, // INS_vfrintrp_d
        0, // INS_vfrintrz_s
        0, // INS_vfrintrz_d
        0, // INS_vfrintrne_s
        0, // INS_vfrintrne_d
        0, // INS_vfcvtl_s_h
        0, // INS_vfcvth_s_h
        0, // INS_vfcvtl_d_s
        0, // INS_vfcvth_d_s
        0, // INS_vffint_s_w
        0, // INS_vffint_s_wu
        0, // INS_vffint_d_l
        0, // INS_vffint_d_lu
        0, // INS_vffintl_d_w
        0, // INS_vffinth_d_w
        0, // INS_vftint_w_s
        0, // INS_vftint_l_d
        0, // INS_vftintrm_w_s
        0, // INS_vftintrm_l_d
        0, // INS_vftintrp_w_s
        0, // INS_vftintrp_l_d
        0, // INS_vftintrz_w_s
        0, // INS_vftintrz_l_d
        0, // INS_vftintrne_w_s
        0, // INS_vftintrne_l_d
        0, // INS_vftint_wu_s
        0, // INS_vftint_lu_d
        0, // INS_vftintrz_wu_s
        0, // INS_vftintrz_lu_d
        0, // INS_vftintl_l_s
        0, // INS_vftinth_l_s
        0, // INS_vftintrml_l_s
        0, // INS_vftintrmh_l_s
        0, // INS_vftintrpl_l_s
        0, // INS_vftintrph_l_s
        0, // INS_vftintrzl_l_s
        0, // INS_vftintrzh_l_s
        0, // INS_vftintrnel_l_s
        0, // INS_vftintrneh_l_s
        0, // INS_vexth_h_b
        0, // INS_vexth_w_h
        0, // INS_vexth_d_w
        0, // INS_vexth_q_d
        0, // INS_vexth_hu_bu
        0, // INS_vexth_wu_hu
        0, // INS_vexth_du_wu
        0, // INS_vexth_qu_du
        0, // INS_vextl_q_d
        0, // INS_vextl_qu_du
        LD, // INS_vldi
        0, // INS_vseteqz_v
        0, // INS_vsetnez_v
        0, // INS_vsetanyeqz_b
        0, // INS_vsetanyeqz_h
        0, // INS_vsetanyeqz_w
        0, // INS_vsetanyeqz_d
        0, // INS_vsetallnez_b
        0, // INS_vsetallnez_h
        0, // INS_vsetallnez_w
        0, // INS_vsetallnez_d
        ST, // INS_vstelm_b
        ST, // INS_vstelm_h
        ST, // INS_vstelm_w
        ST, // INS_vstelm_d
        0, // INS_vseqi_b
        0, // INS_vseqi_h
        0, // INS_vseqi_w
        0, // INS_vseqi_d
        0, // INS_vslei_b
        0, // INS_vslei_h
        0, // INS_vslei_w
        0, // INS_vslei_d
        0, // INS_vslti_b
        0, // INS_vslti_h
        0, // INS_vslti_w
        0, // INS_vslti_d
        0, // INS_vmaxi_b
        0, // INS_vmaxi_h
        0, // INS_vmaxi_w
        0, // INS_vmaxi_d
        0, // INS_vmini_b
        0, // INS_vmini_h
        0, // INS_vmini_w
        0, // INS_vmini_d
        LD, // INS_vldrepl_d
        LD, // INS_vldrepl_w
        LD, // INS_vldrepl_h
        LD, // INS_vld
        LD, // INS_vldrepl_b
        ST, // INS_vst
        0, // INS_vinsgr2vr_d
        0, // INS_vreplvei_d
        0, // INS_vpickve2gr_d
        0, // INS_vpickve2gr_du
        0, // INS_vpickve2gr_w
        0, // INS_vpickve2gr_wu
        0, // INS_vinsgr2vr_w
        0, // INS_vreplvei_w
        0, // INS_vslli_b
        0, // INS_vsrli_b
        0, // INS_vsrai_b
        0, // INS_vsrlri_b
        0, // INS_vsrari_b
        0, // INS_vrotri_b
        0, // INS_vsllwil_h_b
        0, // INS_vsllwil_hu_bu
        0, // INS_vbitclri_b
        0, // INS_vbitseti_b
        0, // INS_vbitrevi_b
        0, // INS_vsat_b
        0, // INS_vsat_bu
        0, // INS_vreplvei_h
        0, // INS_vinsgr2vr_h
        0, // INS_vpickve2gr_h
        0, // INS_vpickve2gr_hu
        0, // INS_vpickve2gr_b
        0, // INS_vpickve2gr_bu
        0, // INS_vslli_h
        0, // INS_vsrli_h
        0, // INS_vsrai_h
        0, // INS_vsrlri_h
        0, // INS_vsrari_h
        0, // INS_vrotri_h
        0, // INS_vsllwil_w_h
        0, // INS_vsllwil_wu_hu
        0, // INS_vsrlni_b_h
        0, // INS_vsrlrni_b_h
        0, // INS_vssrlni_b_h
        0, // INS_vssrlni_bu_h
        0, // INS_vssrlrni_b_h
        0, // INS_vssrlrni_bu_h
        0, // INS_vsrani_b_h
        0, // INS_vsrarni_b_h
        0, // INS_vssrani_b_h
        0, // INS_vssrani_bu_h
        0, // INS_vssrarni_b_h
        0, // INS_vssrarni_bu_h
        0, // INS_vbitclri_h
        0, // INS_vbitseti_h
        0, // INS_vbitrevi_h
        0, // INS_vsat_h
        0, // INS_vsat_hu
        0, // INS_vinsgr2vr_b
        0, // INS_vreplvei_b
        0, // INS_vslei_bu
        0, // INS_vslei_hu
        0, // INS_vslei_wu
        0, // INS_vslei_du
        0, // INS_vslti_bu
        0, // INS_vslti_hu
        0, // INS_vslti_wu
        0, // INS_vslti_du
        0, // INS_vaddi_bu
        0, // INS_vaddi_hu
        0, // INS_vaddi_wu
        0, // INS_vaddi_du
        0, // INS_vsubi_bu
        0, // INS_vsubi_hu
        0, // INS_vsubi_wu
        0, // INS_vsubi_du
        0, // INS_vslli_w
        0, // INS_vsrli_w
        0, // INS_vsrai_w
        0, // INS_vbsll_v
        0, // INS_vbsrl_v
        0, // INS_vsrlri_w
        0, // INS_vsrari_w
        0, // INS_vrotri_w
        0, // INS_vsllwil_d_w
        0, // INS_vsllwil_du_wu
        0, // INS_vsrlni_h_w
        0, // INS_vsrlrni_h_w
        0, // INS_vssrlni_h_w
        0, // INS_vssrlni_hu_w
        0, // INS_vssrlrni_h_w
        0, // INS_vssrlrni_hu_w
        0, // INS_vsrani_h_w
        0, // INS_vsrarni_h_w
        0, // INS_vssrani_h_w
        0, // INS_vssrani_hu_w
        0, // INS_vssrarni_h_w
        0, // INS_vssrarni_hu_w
        0, // INS_vbitclri_w
        0, // INS_vbitseti_w
        0, // INS_vbitrevi_w
        0, // INS_vmaxi_bu
        0, // INS_vmaxi_hu
        0, // INS_vmaxi_wu
        0, // INS_vmaxi_du
        0, // INS_vmini_bu
        0, // INS_vmini_hu
        0, // INS_vmini_wu
        0, // INS_vmini_du
        0, // INS_vfrstpi_b
        0, // INS_vfrstpi_h
        0, // INS_vsat_w
        0, // INS_vsat_wu
        0, // INS_vslli_d
        0, // INS_vsrli_d
        0, // INS_vsrai_d
        0, // INS_vrotri_d
        0, // INS_vsrlri_d
        0, // INS_vsrari_d
        0, // INS_vsrlni_w_d
        0, // INS_vsrlrni_w_d
        0, // INS_vssrlni_w_d
        0, // INS_vssrlni_wu_d
        0, // INS_vssrlrni_w_d
        0, // INS_vssrlrni_wu_d
        0, // INS_vsrani_w_d
        0, // INS_vsrarni_w_d
        0, // INS_vssrani_w_d
        0, // INS_vssrani_wu_d
        0, // INS_vssrarni_w_d
        0, // INS_vssrarni_wu_d
        0, // INS_vbitclri_d
        0, // INS_vbitseti_d
        0, // INS_vbitrevi_d
        0, // INS_vsat_d
        0, // INS_vsat_du
        0, // INS_vsrlni_d_q
        0, // INS_vsrlrni_d_q
        0, // INS_vssrlni_d_q
        0, // INS_vssrlni_du_q
        0, // INS_vssrlrni_d_q
        0, // INS_vssrlrni_du_q
        0, // INS_vsrani_d_q
        0, // INS_vsrarni_d_q
        0, // INS_vssrani_d_q
        0, // INS_vssrani_du_q
        0, // INS_vssrarni_d_q
        0, // INS_vssrarni_du_q
        0, // INS_vextrins_d
        0, // INS_vextrins_w
        0, // INS_vextrins_h
        0, // INS_vextrins_b
        0, // INS_vshuf4i_b
        0, // INS_vshuf4i_h
        0, // INS_vshuf4i_w
        0, // INS_vshuf4i_d
        0, // INS_vbitseli_b
        0, // INS_vandi_b
        0, // INS_vori_b
        0, // INS_vxori_b
        0, // INS_vnori_b
        0, // INS_vpermi_w
        0, // INS_xvfmadd_s
        0, // INS_xvfmadd_d
        0, // INS_xvfmsub_s
        0, // INS_xvfmsub_d
        0, // INS_xvfnmadd_s
        0, // INS_xvfnmadd_d
        0, // INS_xvfnmsub_s
        0, // INS_xvfnmsub_d
        0, // INS_xvbitsel_v
        0, // INS_xvshuf_b
        LD, // INS_xvldx
        ST, // INS_xvstx
        0, // INS_xvreplve_b
        0, // INS_xvreplve_h
        0, // INS_xvreplve_w
        0, // INS_xvreplve_d
        0, // INS_xvfcmp_caf_s
        0, // INS_xvfcmp_cun_s
        0, // INS_xvfcmp_ceq_s
        0, // INS_xvfcmp_cueq_s
        0, // INS_xvfcmp_clt_s
        0, // INS_xvfcmp_cult_s
        0, // INS_xvfcmp_cle_s
        0, // INS_xvfcmp_cule_s
        0, // INS_xvfcmp_cne_s
        0, // INS_xvfcmp_cor_s
        0, // INS_xvfcmp_cune_s
        0, // INS_xvfcmp_saf_s
        0, // INS_xvfcmp_sun_s
        0, // INS_xvfcmp_seq_s
        0, // INS_xvfcmp_sueq_s
        0, // INS_xvfcmp_slt_s
        0, // INS_xvfcmp_sult_s
        0, // INS_xvfcmp_sle_s
        0, // INS_xvfcmp_sule_s
        0, // INS_xvfcmp_sne_s
        0, // INS_xvfcmp_sor_s
        0, // INS_xvfcmp_sune_s
        0, // INS_xvfcmp_caf_d
        0, // INS_xvfcmp_cun_d
        0, // INS_xvfcmp_ceq_d
        0, // INS_xvfcmp_cueq_d
        0, // INS_xvfcmp_clt_d
        0, // INS_xvfcmp_cult_d
        0, // INS_xvfcmp_cle_d
        0, // INS_xvfcmp_cule_d
        0, // INS_xvfcmp_cne_d
        0, // INS_xvfcmp_cor_d
        0, // INS_xvfcmp_cune_d
        0, // INS_xvfcmp_saf_d
        0, // INS_xvfcmp_sun_d
        0, // INS_xvfcmp_seq_d
        0, // INS_xvfcmp_sueq_d
        0, // INS_xvfcmp_slt_d
        0, // INS_xvfcmp_sult_d
        0, // INS_xvfcmp_sle_d
        0, // INS_xvfcmp_sule_d
        0, // INS_xvfcmp_sne_d
        0, // INS_xvfcmp_sor_d
        0, // INS_xvfcmp_sune_d
        0, // INS_xvseq_b
        0, // INS_xvseq_h
        0, // INS_xvseq_w
        0, // INS_xvseq_d
        0, // INS_xvsle_b
        0, // INS_xvsle_h
        0, // INS_xvsle_w
        0, // INS_xvsle_d
        0, // INS_xvsle_bu
        0, // INS_xvsle_hu
        0, // INS_xvsle_wu
        0, // INS_xvsle_du
        0, // INS_xvslt_b
        0, // INS_xvslt_h
        0, // INS_xvslt_w
        0, // INS_xvslt_d
        0, // INS_xvslt_bu
        0, // INS_xvslt_hu
        0, // INS_xvslt_wu
        0, // INS_xvslt_du
        0, // INS_xvadd_b
        0, // INS_xvadd_h
        0, // INS_xvadd_w
        0, // INS_xvadd_d
        0, // INS_xvsub_b
        0, // INS_xvsub_h
        0, // INS_xvsub_w
        0, // INS_xvsub_d
        0, // INS_xvaddwev_h_b
        0, // INS_xvaddwev_w_h
        0, // INS_xvaddwev_d_w
        0, // INS_xvaddwev_q_d
        0, // INS_xvsubwev_h_b
        0, // INS_xvsubwev_w_h
        0, // INS_xvsubwev_d_w
        0, // INS_xvsubwev_q_d
        0, // INS_xvaddwod_h_b
        0, // INS_xvaddwod_w_h
        0, // INS_xvaddwod_d_w
        0, // INS_xvaddwod_q_d
        0, // INS_xvsubwod_h_b
        0, // INS_xvsubwod_w_h
        0, // INS_xvsubwod_d_w
        0, // INS_xvsubwod_q_d
        0, // INS_xvaddwev_h_bu
        0, // INS_xvaddwev_w_hu
        0, // INS_xvaddwev_d_wu
        0, // INS_xvaddwev_q_du
        0, // INS_xvsubwev_h_bu
        0, // INS_xvsubwev_w_hu
        0, // INS_xvsubwev_d_wu
        0, // INS_xvsubwev_q_du
        0, // INS_xvaddwod_h_bu
        0, // INS_xvaddwod_w_hu
        0, // INS_xvaddwod_d_wu
        0, // INS_xvaddwod_q_du
        0, // INS_xvsubwod_h_bu
        0, // INS_xvsubwod_w_hu
        0, // INS_xvsubwod_d_wu
        0, // INS_xvsubwod_q_du
        0, // INS_xvaddwev_h_bu_b
        0, // INS_xvaddwev_w_hu_h
        0, // INS_xvaddwev_d_wu_w
        0, // INS_xvaddwev_q_du_d
        0, // INS_xvaddwod_h_bu_b
        0, // INS_xvaddwod_w_hu_h
        0, // INS_xvaddwod_d_wu_w
        0, // INS_xvaddwod_q_du_d
        0, // INS_xvsadd_b
        0, // INS_xvsadd_h
        0, // INS_xvsadd_w
        0, // INS_xvsadd_d
        0, // INS_xvssub_b
        0, // INS_xvssub_h
        0, // INS_xvssub_w
        0, // INS_xvssub_d
        0, // INS_xvsadd_bu
        0, // INS_xvsadd_hu
        0, // INS_xvsadd_wu
        0, // INS_xvsadd_du
        0, // INS_xvssub_bu
        0, // INS_xvssub_hu
        0, // INS_xvssub_wu
        0, // INS_xvssub_du
        0, // INS_xvhaddw_h_b
        0, // INS_xvhaddw_w_h
        0, // INS_xvhaddw_d_w
        0, // INS_xvhaddw_q_d
        0, // INS_xvhsubw_h_b
        0, // INS_xvhsubw_w_h
        0, // INS_xvhsubw_d_w
        0, // INS_xvhsubw_q_d
        0, // INS_xvhaddw_hu_bu
        0, // INS_xvhaddw_wu_hu
        0, // INS_xvhaddw_du_wu
        0, // INS_xvhaddw_qu_du
        0, // INS_xvhsubw_hu_bu
        0, // INS_xvhsubw_wu_hu
        0, // INS_xvhsubw_du_wu
        0, // INS_xvhsubw_qu_du
        0, // INS_xvadda_b
        0, // INS_xvadda_h
        0, // INS_xvadda_w
        0, // INS_xvadda_d
        0, // INS_xvabsd_b
        0, // INS_xvabsd_h
        0, // INS_xvabsd_w
        0, // INS_xvabsd_d
        0, // INS_xvabsd_bu
        0, // INS_xvabsd_hu
        0, // INS_xvabsd_wu
        0, // INS_xvabsd_du
        0, // INS_xvavg_b
        0, // INS_xvavg_h
        0, // INS_xvavg_w
        0, // INS_xvavg_d
        0, // INS_xvavg_bu
        0, // INS_xvavg_hu
        0, // INS_xvavg_wu
        0, // INS_xvavg_du
        0, // INS_xvavgr_b
        0, // INS_xvavgr_h
        0, // INS_xvavgr_w
        0, // INS_xvavgr_d
        0, // INS_xvavgr_bu
        0, // INS_xvavgr_hu
        0, // INS_xvavgr_wu
        0, // INS_xvavgr_du
        0, // INS_xvmax_b
        0, // INS_xvmax_h
        0, // INS_xvmax_w
        0, // INS_xvmax_d
        0, // INS_xvmin_b
        0, // INS_xvmin_h
        0, // INS_xvmin_w
        0, // INS_xvmin_d
        0, // INS_xvmax_bu
        0, // INS_xvmax_hu
        0, // INS_xvmax_wu
        0, // INS_xvmax_du
        0, // INS_xvmin_bu
        0, // INS_xvmin_hu
        0, // INS_xvmin_wu
        0, // INS_xvmin_du
        0, // INS_xvmul_b
        0, // INS_xvmul_h
        0, // INS_xvmul_w
        0, // INS_xvmul_d
        0, // INS_xvmuh_b
        0, // INS_xvmuh_h
        0, // INS_xvmuh_w
        0, // INS_xvmuh_d
        0, // INS_xvmuh_bu
        0, // INS_xvmuh_hu
        0, // INS_xvmuh_wu
        0, // INS_xvmuh_du
        0, // INS_xvmulwev_h_b
        0, // INS_xvmulwev_w_h
        0, // INS_xvmulwev_d_w
        0, // INS_xvmulwev_q_d
        0, // INS_xvmulwod_h_b
        0, // INS_xvmulwod_w_h
        0, // INS_xvmulwod_d_w
        0, // INS_xvmulwod_q_d
        0, // INS_xvmulwev_h_bu
        0, // INS_xvmulwev_w_hu
        0, // INS_xvmulwev_d_wu
        0, // INS_xvmulwev_q_du
        0, // INS_xvmulwod_h_bu
        0, // INS_xvmulwod_w_hu
        0, // INS_xvmulwod_d_wu
        0, // INS_xvmulwod_q_du
        0, // INS_xvmulwev_h_bu_b
        0, // INS_xvmulwev_w_hu_h
        0, // INS_xvmulwev_d_wu_w
        0, // INS_xvmulwev_q_du_d
        0, // INS_xvmulwod_h_bu_b
        0, // INS_xvmulwod_w_hu_h
        0, // INS_xvmulwod_d_wu_w
        0, // INS_xvmulwod_q_du_d
        0, // INS_xvmadd_b
        0, // INS_xvmadd_h
        0, // INS_xvmadd_w
        0, // INS_xvmadd_d
        0, // INS_xvmsub_b
        0, // INS_xvmsub_h
        0, // INS_xvmsub_w
        0, // INS_xvmsub_d
        0, // INS_xvmaddwev_h_b
        0, // INS_xvmaddwev_w_h
        0, // INS_xvmaddwev_d_w
        0, // INS_xvmaddwev_q_d
        0, // INS_xvmaddwod_h_b
        0, // INS_xvmaddwod_w_h
        0, // INS_xvmaddwod_d_w
        0, // INS_xvmaddwod_q_d
        0, // INS_xvmaddwev_h_bu
        0, // INS_xvmaddwev_w_hu
        0, // INS_xvmaddwev_d_wu
        0, // INS_xvmaddwev_q_du
        0, // INS_xvmaddwod_h_bu
        0, // INS_xvmaddwod_w_hu
        0, // INS_xvmaddwod_d_wu
        0, // INS_xvmaddwod_q_du
        0, // INS_xvmaddwev_h_bu_b
        0, // INS_xvmaddwev_w_hu_h
        0, // INS_xvmaddwev_d_wu_w
        0, // INS_xvmaddwev_q_du_d
        0, // INS_xvmaddwod_h_bu_b
        0, // INS_xvmaddwod_w_hu_h
        0, // INS_xvmaddwod_d_wu_w
        0, // INS_xvmaddwod_q_du_d
        0, // INS_xvdiv_b
        0, // INS_xvdiv_h
        0, // INS_xvdiv_w
        0, // INS_xvdiv_d
        0, // INS_xvmod_b
        0, // INS_xvmod_h
        0, // INS_xvmod_w
        0, // INS_xvmod_d
        0, // INS_xvdiv_bu
        0, // INS_xvdiv_hu
        0, // INS_xvdiv_wu
        0, // INS_xvdiv_du
        0, // INS_xvmod_bu
        0, // INS_xvmod_hu
        0, // INS_xvmod_wu
        0, // INS_xvmod_du
        0, // INS_xvsll_b
        0, // INS_xvsll_h
        0, // INS_xvsll_w
        0, // INS_xvsll_d
        0, // INS_xvsrl_b
        0, // INS_xvsrl_h
        0, // INS_xvsrl_w
        0, // INS_xvsrl_d
        0, // INS_xvsra_b
        0, // INS_xvsra_h
        0, // INS_xvsra_w
        0, // INS_xvsra_d
        0, // INS_xvrotr_b
        0, // INS_xvrotr_h
        0, // INS_xvrotr_w
        0, // INS_xvrotr_d
        0, // INS_xvsrlr_b
        0, // INS_xvsrlr_h
        0, // INS_xvsrlr_w
        0, // INS_xvsrlr_d
        0, // INS_xvsrar_b
        0, // INS_xvsrar_h
        0, // INS_xvsrar_w
        0, // INS_xvsrar_d
        0, // INS_xvsrln_b_h
        0, // INS_xvsrln_h_w
        0, // INS_xvsrln_w_d
        0, // INS_xvsran_b_h
        0, // INS_xvsran_h_w
        0, // INS_xvsran_w_d
        0, // INS_xvsrlrn_b_h
        0, // INS_xvsrlrn_h_w
        0, // INS_xvsrlrn_w_d
        0, // INS_xvsrarn_b_h
        0, // INS_xvsrarn_h_w
        0, // INS_xvsrarn_w_d
        0, // INS_xvssrln_b_h
        0, // INS_xvssrln_h_w
        0, // INS_xvssrln_w_d
        0, // INS_xvssran_b_h
        0, // INS_xvssran_h_w
        0, // INS_xvssran_w_d
        0, // INS_xvssrlrn_b_h
        0, // INS_xvssrlrn_h_w
        0, // INS_xvssrlrn_w_d
        0, // INS_xvssrarn_b_h
        0, // INS_xvssrarn_h_w
        0, // INS_xvssrarn_w_d
        0, // INS_xvssrln_bu_h
        0, // INS_xvssrln_hu_w
        0, // INS_xvssrln_wu_d
        0, // INS_xvssran_bu_h
        0, // INS_xvssran_hu_w
        0, // INS_xvssran_wu_d
        0, // INS_xvssrlrn_bu_h
        0, // INS_xvssrlrn_hu_w
        0, // INS_xvssrlrn_wu_d
        0, // INS_xvssrarn_bu_h
        0, // INS_xvssrarn_hu_w
        0, // INS_xvssrarn_wu_d
        0, // INS_xvbitclr_b
        0, // INS_xvbitclr_h
        0, // INS_xvbitclr_w
        0, // INS_xvbitclr_d
        0, // INS_xvbitset_b
        0, // INS_xvbitset_h
        0, // INS_xvbitset_w
        0, // INS_xvbitset_d
        0, // INS_xvbitrev_b
        0, // INS_xvbitrev_h
        0, // INS_xvbitrev_w
        0, // INS_xvbitrev_d
        0, // INS_xvpackev_b
        0, // INS_xvpackev_h
        0, // INS_xvpackev_w
        0, // INS_xvpackev_d
        0, // INS_xvpackod_b
        0, // INS_xvpackod_h
        0, // INS_xvpackod_w
        0, // INS_xvpackod_d
        0, // INS_xvilvl_b
        0, // INS_xvilvl_h
        0, // INS_xvilvl_w
        0, // INS_xvilvl_d
        0, // INS_xvilvh_b
        0, // INS_xvilvh_h
        0, // INS_xvilvh_w
        0, // INS_xvilvh_d
        0, // INS_xvpickev_b
        0, // INS_xvpickev_h
        0, // INS_xvpickev_w
        0, // INS_xvpickev_d
        0, // INS_xvpickod_b
        0, // INS_xvpickod_h
        0, // INS_xvpickod_w
        0, // INS_xvpickod_d
        0, // INS_xvand_v
        0, // INS_xvor_v
        0, // INS_xvxor_v
        0, // INS_xvnor_v
        0, // INS_xvandn_v
        0, // INS_xvorn_v
        0, // INS_xvfrstp_b
        0, // INS_xvfrstp_h
        0, // INS_xvadd_q
        0, // INS_xvsub_q
        0, // INS_xvsigncov_b
        0, // INS_xvsigncov_h
        0, // INS_xvsigncov_w
        0, // INS_xvsigncov_d
        0, // INS_xvfadd_s
        0, // INS_xvfadd_d
        0, // INS_xvfsub_s
        0, // INS_xvfsub_d
        0, // INS_xvfmul_s
        0, // INS_xvfmul_d
        0, // INS_xvfdiv_s
        0, // INS_xvfdiv_d
        0, // INS_xvfmax_s
        0, // INS_xvfmax_d
        0, // INS_xvfmin_s
        0, // INS_xvfmin_d
        0, // INS_xvfmaxa_s
        0, // INS_xvfmaxa_d
        0, // INS_xvfmina_s
        0, // INS_xvfmina_d
        0, // INS_xvfcvt_h_s
        0, // INS_xvfcvt_s_d
        0, // INS_xvffint_s_l
        0, // INS_xvftint_w_d
        0, // INS_xvftintrm_w_d
        0, // INS_xvftintrp_w_d
        0, // INS_xvftintrz_w_d
        0, // INS_xvftintrne_w_d
        0, // INS_xvshuf_h
        0, // INS_xvshuf_w
        0, // INS_xvshuf_d
        0, // INS_xvperm_w
        0, // INS_xvreplgr2vr_b
        0, // INS_xvreplgr2vr_h
        0, // INS_xvreplgr2vr_w
        0, // INS_xvreplgr2vr_d
        0, // INS_xvclo_b
        0, // INS_xvclo_h
        0, // INS_xvclo_w
        0, // INS_xvclo_d
        0, // INS_xvclz_b
        0, // INS_xvclz_h
        0, // INS_xvclz_w
        0, // INS_xvclz_d
        0, // INS_xvpcnt_b
        0, // INS_xvpcnt_h
        0, // INS_xvpcnt_w
        0, // INS_xvpcnt_d
        0, // INS_xvneg_b
        0, // INS_xvneg_h
        0, // INS_xvneg_w
        0, // INS_xvneg_d
        0, // INS_xvmskltz_b
        0, // INS_xvmskltz_h
        0, // INS_xvmskltz_w
        0, // INS_xvmskltz_d
        0, // INS_xvmskgez_b
        0, // INS_xvmsknz_b
        0, // INS_xvflogb_s
        0, // INS_xvflogb_d
        0, // INS_xvfclass_s
        0, // INS_xvfclass_d
        0, // INS_xvfsqrt_s
        0, // INS_xvfsqrt_d
        0, // INS_xvfrecip_s
        0, // INS_xvfrecip_d
        0, // INS_xvfrsqrt_s
        0, // INS_xvfrsqrt_d
        0, // INS_xvfrecipe_s
        0, // INS_xvfrecipe_d
        0, // INS_xvfrsqrte_s
        0, // INS_xvfrsqrte_d
        0, // INS_xvfrint_s
        0, // INS_xvfrint_d
        0, // INS_xvfrintrm_s
        0, // INS_xvfrintrm_d
        0, // INS_xvfrintrp_s
        0, // INS_xvfrintrp_d
        0, // INS_xvfrintrz_s
        0, // INS_xvfrintrz_d
        0, // INS_xvfrintrne_s
        0, // INS_xvfrintrne_d
        0, // INS_xvfcvtl_s_h
        0, // INS_xvfcvth_s_h
        0, // INS_xvfcvtl_d_s
        0, // INS_xvfcvth_d_s
        0, // INS_xvffint_s_w
        0, // INS_xvffint_s_wu
        0, // INS_xvffint_d_l
        0, // INS_xvffint_d_lu
        0, // INS_xvffintl_d_w
        0, // INS_xvffinth_d_w
        0, // INS_xvftint_w_s
        0, // INS_xvftint_l_d
        0, // INS_xvftintrm_w_s
        0, // INS_xvftintrm_l_d
        0, // INS_xvftintrp_w_s
        0, // INS_xvftintrp_l_d
        0, // INS_xvftintrz_w_s
        0, // INS_xvftintrz_l_d
        0, // INS_xvftintrne_w_s
        0, // INS_xvftintrne_l_d
        0, // INS_xvftint_wu_s
        0, // INS_xvftint_lu_d
        0, // INS_xvftintrz_wu_s
        0, // INS_xvftintrz_lu_d
        0, // INS_xvftintl_l_s
        0, // INS_xvftinth_l_s
        0, // INS_xvftintrml_l_s
        0, // INS_xvftintrmh_l_s
        0, // INS_xvftintrpl_l_s
        0, // INS_xvftintrph_l_s
        0, // INS_xvftintrzl_l_s
        0, // INS_xvftintrzh_l_s
        0, // INS_xvftintrnel_l_s
        0, // INS_xvftintrneh_l_s
        0, // INS_xvexth_h_b
        0, // INS_xvexth_w_h
        0, // INS_xvexth_d_w
        0, // INS_xvexth_q_d
        0, // INS_xvexth_hu_bu
        0, // INS_xvexth_wu_hu
        0, // INS_xvexth_du_wu
        0, // INS_xvexth_qu_du
        0, // INS_vext2xv_h_b
        0, // INS_vext2xv_w_b
        0, // INS_vext2xv_d_b
        0, // INS_vext2xv_w_h
        0, // INS_vext2xv_d_h
        0, // INS_vext2xv_d_w
        0, // INS_vext2xv_hu_bu
        0, // INS_vext2xv_wu_bu
        0, // INS_vext2xv_du_bu
        0, // INS_vext2xv_wu_hu
        0, // INS_vext2xv_du_hu
        0, // INS_vext2xv_du_wu
        0, // INS_xvreplve0_b
        0, // INS_xvreplve0_h
        0, // INS_xvreplve0_w
        0, // INS_xvreplve0_d
        0, // INS_xvreplve0_q
        0, // INS_xvextl_q_d
        0, // INS_xvextl_qu_du
        ST, // INS_xvstelm_b
        ST, // INS_xvstelm_h
        ST, // INS_xvstelm_w
        ST, // INS_xvstelm_d
        0, // INS_xvseqi_b
        0, // INS_xvseqi_h
        0, // INS_xvseqi_w
        0, // INS_xvseqi_d
        0, // INS_xvslei_b
        0, // INS_xvslei_h
        0, // INS_xvslei_w
        0, // INS_xvslei_d
        0, // INS_xvslti_b
        0, // INS_xvslti_h
        0, // INS_xvslti_w
        0, // INS_xvslti_d
        0, // INS_xvmaxi_b
        0, // INS_xvmaxi_h
        0, // INS_xvmaxi_w
        0, // INS_xvmaxi_d
        0, // INS_xvmini_b
        0, // INS_xvmini_h
        0, // INS_xvmini_w
        0, // INS_xvmini_d
        LD, // INS_xvldrepl_d
        LD, // INS_xvldrepl_w
        LD, // INS_xvldrepl_h
        LD, // INS_xvld
        LD, // INS_xvldrepl_b
        ST, // INS_xvst
        0, // INS_xvrepl128vei_d
        0, // INS_xvinsve0_d
        0, // INS_xvrepl128vei_w
        0, // INS_xvpickve_d
        0, // INS_xvinsgr2vr_d
        0, // INS_xvpickve2gr_d
        0, // INS_xvpickve2gr_du
        0, // INS_xvpickve2gr_w
        0, // INS_xvpickve2gr_wu
        0, // INS_xvslli_b
        0, // INS_xvsrli_b
        0, // INS_xvsrai_b
        0, // INS_xvrotri_b
        0, // INS_xvsrlri_b
        0, // INS_xvsrari_b
        0, // INS_xvsllwil_h_b
        0, // INS_xvsllwil_hu_bu
        0, // INS_xvsrlni_b_h
        0, // INS_xvsrlrni_b_h
        0, // INS_xvssrlni_b_h
        0, // INS_xvssrlni_bu_h
        0, // INS_xvssrlrni_b_h
        0, // INS_xvssrlrni_bu_h
        0, // INS_xvsrani_b_h
        0, // INS_xvsrarni_b_h
        0, // INS_xvssrani_b_h
        0, // INS_xvssrani_bu_h
        0, // INS_xvssrarni_b_h
        0, // INS_xvssrarni_bu_h
        0, // INS_xvinsve0_w
        0, // INS_xvrepl128vei_h
        0, // INS_xvpickve_w
        0, // INS_xvbitclri_b
        0, // INS_xvbitseti_b
        0, // INS_xvbitrevi_b
        0, // INS_xvinsgr2vr_w
        0, // INS_xvsat_b
        0, // INS_xvsat_bu
        0, // INS_xvslli_h
        0, // INS_xvsrli_h
        0, // INS_xvsrai_h
        0, // INS_xvrotri_h
        0, // INS_xvrepl128vei_b
        0, // INS_xvsrlri_h
        0, // INS_xvsrari_h
        0, // INS_xvsllwil_w_h
        0, // INS_xvsllwil_wu_hu
        0, // INS_xvbitclri_h
        0, // INS_xvbitseti_h
        0, // INS_xvbitrevi_h
        0, // INS_xvsat_h
        0, // INS_xvsat_hu
        0, // INS_xvslei_bu
        0, // INS_xvslei_hu
        0, // INS_xvslei_wu
        0, // INS_xvslei_du
        0, // INS_xvslti_bu
        0, // INS_xvslti_hu
        0, // INS_xvslti_wu
        0, // INS_xvslti_du
        0, // INS_xvaddi_bu
        0, // INS_xvaddi_hu
        0, // INS_xvaddi_wu
        0, // INS_xvaddi_du
        0, // INS_xvsubi_bu
        0, // INS_xvsubi_hu
        0, // INS_xvsubi_wu
        0, // INS_xvsubi_du
        0, // INS_xvmaxi_bu
        0, // INS_xvmaxi_hu
        0, // INS_xvmaxi_wu
        0, // INS_xvmaxi_du
        0, // INS_xvmini_bu
        0, // INS_xvmini_hu
        0, // INS_xvmini_wu
        0, // INS_xvmini_du
        0, // INS_xvfrstpi_b
        0, // INS_xvfrstpi_h
        0, // INS_xvslli_w
        0, // INS_xvsrli_w
        0, // INS_xvsrai_w
        0, // INS_xvbsll_v
        0, // INS_xvbsrl_v
        0, // INS_xvsrlri_w
        0, // INS_xvsrari_w
        0, // INS_xvrotri_w
        0, // INS_xvsllwil_d_w
        0, // INS_xvsllwil_du_wu
        0, // INS_xvsrlni_h_w
        0, // INS_xvsrlrni_h_w
        0, // INS_xvssrlni_h_w
        0, // INS_xvssrlni_hu_w
        0, // INS_xvssrlrni_h_w
        0, // INS_xvssrlrni_hu_w
        0, // INS_xvsrani_h_w
        0, // INS_xvsrarni_h_w
        0, // INS_xvssrani_h_w
        0, // INS_xvssrani_hu_w
        0, // INS_xvssrarni_h_w
        0, // INS_xvssrarni_hu_w
        0, // INS_xvbitclri_w
        0, // INS_xvbitseti_w
        0, // INS_xvbitrevi_w
        0, // INS_xvsat_w
        0, // INS_xvsat_wu
        0, // INS_xvslli_d
        0, // INS_xvsrli_d
        0, // INS_xvsrai_d
        0, // INS_xvrotri_d
        0, // INS_xvsrlri_d
        0, // INS_xvsrari_d
        0, // INS_xvsrlni_w_d
        0, // INS_xvsrlrni_w_d
        0, // INS_xvssrlni_w_d
        0, // INS_xvssrlni_wu_d
        0, // INS_xvssrlrni_w_d
        0, // INS_xvssrlrni_wu_d
        0, // INS_xvsrani_w_d
        0, // INS_xvsrarni_w_d
        0, // INS_xvssrani_w_d
        0, // INS_xvssrani_wu_d
        0, // INS_xvssrarni_w_d
        0, // INS_xvssrarni_wu_d
        0, // INS_xvbitclri_d
        0, // INS_xvbitseti_d
        0, // INS_xvbitrevi_d
        0, // INS_xvsat_d
        0, // INS_xvsat_du
        0, // INS_xvsrlni_d_q
        0, // INS_xvsrlrni_d_q
        0, // INS_xvssrlni_d_q
        0, // INS_xvssrlni_du_q
        0, // INS_xvssrlrni_d_q
        0, // INS_xvssrlrni_du_q
        0, // INS_xvsrani_d_q
        0, // INS_xvsrarni_d_q
        0, // INS_xvssrani_d_q
        0, // INS_xvssrani_du_q
        0, // INS_xvssrarni_d_q
        0, // INS_xvssrarni_du_q
        0, // INS_xvextrins_d
        0, // INS_xvextrins_w
        0, // INS_xvextrins_h
        0, // INS_xvextrins_b
        0, // INS_xvshuf4i_b
        0, // INS_xvshuf4i_h
        0, // INS_xvshuf4i_w
        0, // INS_xvshuf4i_d
        0, // INS_xvbitseli_b
        0, // INS_xvandi_b
        0, // INS_xvori_b
        0, // INS_xvxori_b
        0, // INS_xvnori_b
        0, // INS_xvpermi_w
        0, // INS_xvpermi_d
        0, // INS_xvpermi_q
        LD, // INS_xvldi
        0, // INS_xvseteqz_v
        0, // INS_xvsetnez_v
        0, // INS_xvsetanyeqz_b
        0, // INS_xvsetanyeqz_h
        0, // INS_xvsetanyeqz_w
        0, // INS_xvsetanyeqz_d
        0, // INS_xvsetallnez_b
        0, // INS_xvsetallnez_h
        0, // INS_xvsetallnez_w
        0, // INS_xvsetallnez_d
#endif
#elif TARGET_RISCV64
#if !TARGET_RISCV64
#error Unexpected target type
#endif
        0, // INS_invalid
        0, // INS_nop
        0, // INS_mov
        0, // INS_sext_w
        0, // INS_not
        0, // INS_lui
        0, // INS_auipc
        0, // INS_addi
        0, // INS_slti
        0, // INS_sltiu
        0, // INS_xori
        0, // INS_ori
        0, // INS_andi
        0, // INS_slli
        0, // INS_srli
        0, // INS_srai
        0, // INS_add
        0, // INS_sub
        0, // INS_sll
        0, // INS_slt
        0, // INS_sltu
        0, // INS_xor
        0, // INS_srl
        0, // INS_sra
        0, // INS_or
        0, // INS_and
        0, // INS_fence
        0, // INS_fence_i
        0, // INS_csrrw
        0, // INS_csrrs
        0, // INS_csrrc
        0, // INS_csrrwi
        0, // INS_csrrsi
        0, // INS_csrrci
        0, // INS_ecall
        0, // INS_ebreak
        LD, // INS_lb
        LD, // INS_lh
        LD, // INS_lw
        LD, // INS_lbu
        LD, // INS_lhu
        ST, // INS_sb
        ST, // INS_sh
        ST, // INS_sw
        0, // INS_jal
        0, // INS_j
        0, // INS_beqz
        0, // INS_bnez
        0, // INS_jalr
        0, // INS_beq
        0, // INS_bne
        0, // INS_blt
        0, // INS_bge
        0, // INS_bltu
        0, // INS_bgeu
        0, // INS_addiw
        0, // INS_slliw
        0, // INS_srliw
        0, // INS_sraiw
        0, // INS_addw
        0, // INS_subw
        0, // INS_sllw
        0, // INS_srlw
        0, // INS_sraw
        LD, // INS_lwu
        LD, // INS_ld
        ST, // INS_sd
        0, // INS_mul
        0, // INS_mulh
        0, // INS_mulhsu
        0, // INS_mulhu
        0, // INS_div
        0, // INS_divu
        0, // INS_rem
        0, // INS_remu
        0, // INS_mulw
        0, // INS_divw
        0, // INS_divuw
        0, // INS_remw
        0, // INS_remuw
        0, // INS_fmadd_s
        0, // INS_fmsub_s
        0, // INS_fnmsub_s
        0, // INS_fnmadd_s
        0, // INS_fadd_s
        0, // INS_fsub_s
        0, // INS_fmul_s
        0, // INS_fdiv_s
        0, // INS_fsqrt_s
        0, // INS_fsgnj_s
        0, // INS_fsgnjn_s
        0, // INS_fsgnjx_s
        0, // INS_fmin_s
        0, // INS_fmax_s
        0, // INS_fcvt_w_s
        0, // INS_fcvt_wu_s
        0, // INS_fmv_x_w
        0, // INS_feq_s
        0, // INS_flt_s
        0, // INS_fle_s
        0, // INS_fclass_s
        0, // INS_fcvt_s_w
        0, // INS_fcvt_s_wu
        0, // INS_fmv_w_x
        0, // INS_fmadd_d
        0, // INS_fmsub_d
        0, // INS_fnmsub_d
        0, // INS_fnmadd_d
        0, // INS_fadd_d
        0, // INS_fsub_d
        0, // INS_fmul_d
        0, // INS_fdiv_d
        0, // INS_fsqrt_d
        0, // INS_fsgnj_d
        0, // INS_fsgnjn_d
        0, // INS_fsgnjx_d
        0, // INS_fmin_d
        0, // INS_fmax_d
        0, // INS_fcvt_s_d
        0, // INS_fcvt_d_s
        0, // INS_feq_d
        0, // INS_flt_d
        0, // INS_fle_d
        0, // INS_fclass_d
        0, // INS_fcvt_w_d
        0, // INS_fcvt_wu_d
        0, // INS_fcvt_d_w
        0, // INS_fcvt_d_wu
        LD, // INS_flw
        ST, // INS_fsw
        LD, // INS_fld
        ST, // INS_fsd
        0, // INS_fcvt_l_s
        0, // INS_fcvt_lu_s
        0, // INS_fcvt_s_l
        0, // INS_fcvt_s_lu
        0, // INS_fcvt_l_d
        0, // INS_fcvt_lu_d
        0, // INS_fmv_x_d
        0, // INS_fcvt_d_l
        0, // INS_fcvt_d_lu
        0, // INS_fmv_d_x
        0, // INS_lr_w
        0, // INS_lr_d
        0, // INS_sc_w
        0, // INS_sc_d
        0, // INS_amoswap_w
        0, // INS_amoswap_d
        0, // INS_amoadd_w
        0, // INS_amoadd_d
        0, // INS_amoxor_w
        0, // INS_amoxor_d
        0, // INS_amoand_w
        0, // INS_amoand_d
        0, // INS_amoor_w
        0, // INS_amoor_d
        0, // INS_amomin_w
        0, // INS_amomin_d
        0, // INS_amomax_w
        0, // INS_amomax_d
        0, // INS_amominu_w
        0, // INS_amominu_d
        0, // INS_amomaxu_w
        0, // INS_amomaxu_d
        0, // INS_clz
        0, // INS_clzw
        0, // INS_ctz
        0, // INS_ctzw
        0, // INS_cpop
        0, // INS_cpopw
        0, // INS_sext_b
        0, // INS_sext_h
        0, // INS_zext_h
        0, // INS_rev8
        0, // INS_rol
        0, // INS_rolw
        0, // INS_ror
        0, // INS_rorw
        0, // INS_xnor
        0, // INS_orn
        0, // INS_andn
        0, // INS_min
        0, // INS_minu
        0, // INS_max
        0, // INS_maxu
        0, // INS_rori
        0, // INS_roriw
        0, // INS_sh1add
        0, // INS_sh2add
        0, // INS_sh3add
        0, // INS_add_uw
        0, // INS_sh1add_uw
        0, // INS_sh2add_uw
        0, // INS_sh3add_uw
        0, // INS_slli_uw
        0, // INS_bset
        0, // INS_bclr
        0, // INS_bext
        0, // INS_binv
        0, // INS_bseti
        0, // INS_bclri
        0, // INS_bexti
        0, // INS_binvi
        0, // INS_czero_eqz
        0, // INS_czero_nez
        0, // INS_c_mv
        0, // INS_c_add
        0, // INS_c_and
        0, // INS_c_or
        0, // INS_c_xor
        0, // INS_c_sub
        0, // INS_c_addw
        0, // INS_c_subw
#elif TARGET_WASM
#if !TARGET_WASM
#error Unexpected target type
#endif
        0, // INS_invalid
        0, // INS_unreachable
        0, // INS_label
        0, // INS_catch_ref
        0, // INS_local_cnt
        0, // INS_local_decl
        0, // INS_code_size
        0, // INS_nop
        0, // INS_block
        0, // INS_loop
        0, // INS_if
        0, // INS_else
        0, // INS_throw_ref
        0, // INS_end
        0, // INS_br
        0, // INS_br_if
        0, // INS_br_table
        0, // INS_return
        0, // INS_call
        0, // INS_call_indirect
        0, // INS_return_call
        0, // INS_return_call_indirect
        0, // INS_call_funclet
        0, // INS_drop
        0, // INS_try_table
        0, // INS_local_get
        0, // INS_local_set
        0, // INS_local_tee
        0, // INS_global_get
        0, // INS_global_set
        0, // INS_i32_load
        0, // INS_i64_load
        0, // INS_f32_load
        0, // INS_f64_load
        0, // INS_i32_load8_s
        0, // INS_i32_load8_u
        0, // INS_i32_load16_s
        0, // INS_i32_load16_u
        0, // INS_i64_load8_s
        0, // INS_i64_load8_u
        0, // INS_i64_load16_s
        0, // INS_i64_load16_u
        0, // INS_i64_load32_s
        0, // INS_i64_load32_u
        0, // INS_i32_store
        0, // INS_i64_store
        0, // INS_f32_store
        0, // INS_f64_store
        0, // INS_i32_store8
        0, // INS_i32_store16
        0, // INS_i32_const
        0, // INS_i32_const_address
        0, // INS_i32_const_funcptr
        0, // INS_i32_const_funcletptr
        0, // INS_i32_const_dataoffs
        0, // INS_i64_const
        0, // INS_f32_const
        0, // INS_f64_const
        0, // INS_i32_eqz
        0, // INS_i32_eq
        0, // INS_i32_ne
        0, // INS_i32_lt_s
        0, // INS_i32_lt_u
        0, // INS_i32_gt_s
        0, // INS_i32_gt_u
        0, // INS_i32_le_s
        0, // INS_i32_le_u
        0, // INS_i32_ge_s
        0, // INS_i32_ge_u
        0, // INS_i64_eqz
        0, // INS_i64_eq
        0, // INS_i64_ne
        0, // INS_i64_lt_s
        0, // INS_i64_lt_u
        0, // INS_i64_gt_s
        0, // INS_i64_gt_u
        0, // INS_i64_le_s
        0, // INS_i64_le_u
        0, // INS_i64_ge_s
        0, // INS_i64_ge_u
        0, // INS_f32_eq
        0, // INS_f32_ne
        0, // INS_f32_lt
        0, // INS_f32_gt
        0, // INS_f32_le
        0, // INS_f32_ge
        0, // INS_f64_eq
        0, // INS_f64_ne
        0, // INS_f64_lt
        0, // INS_f64_gt
        0, // INS_f64_le
        0, // INS_f64_ge
        0, // INS_i32_clz
        0, // INS_i32_ctz
        0, // INS_i32_popcnt
        0, // INS_i32_add
        0, // INS_i32_sub
        0, // INS_i32_mul
        0, // INS_i32_div_s
        0, // INS_i32_div_u
        0, // INS_i32_rem_s
        0, // INS_i32_rem_u
        0, // INS_i32_and
        0, // INS_i32_or
        0, // INS_i32_xor
        0, // INS_i32_shl
        0, // INS_i32_shr_s
        0, // INS_i32_shr_u
        0, // INS_i32_rotl
        0, // INS_i32_rotr
        0, // INS_i64_clz
        0, // INS_i64_ctz
        0, // INS_i64_popcnt
        0, // INS_i64_add
        0, // INS_i64_sub
        0, // INS_i64_mul
        0, // INS_i64_div_s
        0, // INS_i64_div_u
        0, // INS_i64_rem_s
        0, // INS_i64_rem_u
        0, // INS_i64_and
        0, // INS_i64_or
        0, // INS_i64_xor
        0, // INS_i64_shl
        0, // INS_i64_shr_s
        0, // INS_i64_shr_u
        0, // INS_i64_rotl
        0, // INS_i64_rotr
        0, // INS_f32_abs
        0, // INS_f32_neg
        0, // INS_f32_ceil
        0, // INS_f32_floor
        0, // INS_f32_trunc
        0, // INS_f32_nearest
        0, // INS_f32_sqrt
        0, // INS_f32_add
        0, // INS_f32_sub
        0, // INS_f32_mul
        0, // INS_f32_div
        0, // INS_f32_min
        0, // INS_f32_max
        0, // INS_f32_copysign
        0, // INS_f64_abs
        0, // INS_f64_neg
        0, // INS_f64_ceil
        0, // INS_f64_floor
        0, // INS_f64_trunc
        0, // INS_f64_nearest
        0, // INS_f64_sqrt
        0, // INS_f64_add
        0, // INS_f64_sub
        0, // INS_f64_mul
        0, // INS_f64_div
        0, // INS_f64_min
        0, // INS_f64_max
        0, // INS_f64_copysign
        0, // INS_i32_wrap_i64
        0, // INS_i32_trunc_s_f32
        0, // INS_i32_trunc_u_f32
        0, // INS_i32_trunc_s_f64
        0, // INS_i32_trunc_u_f64
        0, // INS_i64_extend_s_i32
        0, // INS_i64_extend_u_i32
        0, // INS_i64_trunc_s_f32
        0, // INS_i64_trunc_u_f32
        0, // INS_i64_trunc_s_f64
        0, // INS_i64_trunc_u_f64
        0, // INS_f32_convert_s_i32
        0, // INS_f32_convert_u_i32
        0, // INS_f32_convert_s_i64
        0, // INS_f32_convert_u_i64
        0, // INS_f32_demote_f64
        0, // INS_f64_convert_s_i32
        0, // INS_f64_convert_u_i32
        0, // INS_f64_convert_s_i64
        0, // INS_f64_convert_u_i64
        0, // INS_f64_promote_f32
        0, // INS_i32_reinterpret_f32
        0, // INS_i64_reinterpret_f64
        0, // INS_f32_reinterpret_i32
        0, // INS_f64_reinterpret_i64
        0, // INS_i32_extend8_s
        0, // INS_i32_extend16_s
        0, // INS_i64_extend8_s
        0, // INS_i64_extend16_s
        0, // INS_i64_extend32_s
        0, // INS_i32_trunc_sat_f32_s
        0, // INS_i32_trunc_sat_f32_u
        0, // INS_i32_trunc_sat_f64_s
        0, // INS_i32_trunc_sat_f64_u
        0, // INS_i64_trunc_sat_f32_s
        0, // INS_i64_trunc_sat_f32_u
        0, // INS_i64_trunc_sat_f64_s
        0, // INS_i64_trunc_sat_f64_u
        0, // INS_memory_copy
        0, // INS_memory_fill
        0, // INS_v128_load
        0, // INS_v128_load8x8_s
        0, // INS_v128_load8x8_u
        0, // INS_v128_load16x4_s
        0, // INS_v128_load16x4_u
        0, // INS_v128_load32x2_s
        0, // INS_v128_load32x2_u
        0, // INS_v128_load8_splat
        0, // INS_v128_load16_splat
        0, // INS_v128_load32_splat
        0, // INS_v128_load64_splat
        0, // INS_v128_store
        0, // INS_v128_const
        0, // INS_i8x16_shuffle
        0, // INS_i8x16_swizzle
        0, // INS_i8x16_splat
        0, // INS_i16x8_splat
        0, // INS_i32x4_splat
        0, // INS_i64x2_splat
        0, // INS_f32x4_splat
        0, // INS_f64x2_splat
        2, // INS_i8x16_extract_lane_s
        2, // INS_i8x16_extract_lane_u
        2, // INS_i8x16_replace_lane
        4, // INS_i16x8_extract_lane_s
        4, // INS_i16x8_extract_lane_u
        4, // INS_i16x8_replace_lane
        8, // INS_i32x4_extract_lane
        8, // INS_i32x4_replace_lane
        16, // INS_i64x2_extract_lane
        16, // INS_i64x2_replace_lane
        8, // INS_f32x4_extract_lane
        8, // INS_f32x4_replace_lane
        16, // INS_f64x2_extract_lane
        16, // INS_f64x2_replace_lane
        0, // INS_i8x16_eq
        0, // INS_i8x16_ne
        0, // INS_i8x16_lt_s
        0, // INS_i8x16_lt_u
        0, // INS_i8x16_gt_s
        0, // INS_i8x16_gt_u
        0, // INS_i8x16_le_s
        0, // INS_i8x16_le_u
        0, // INS_i8x16_ge_s
        0, // INS_i8x16_ge_u
        0, // INS_i16x8_eq
        0, // INS_i16x8_ne
        0, // INS_i16x8_lt_s
        0, // INS_i16x8_lt_u
        0, // INS_i16x8_gt_s
        0, // INS_i16x8_gt_u
        0, // INS_i16x8_le_s
        0, // INS_i16x8_le_u
        0, // INS_i16x8_ge_s
        0, // INS_i16x8_ge_u
        0, // INS_i32x4_eq
        0, // INS_i32x4_ne
        0, // INS_i32x4_lt_s
        0, // INS_i32x4_lt_u
        0, // INS_i32x4_gt_s
        0, // INS_i32x4_gt_u
        0, // INS_i32x4_le_s
        0, // INS_i32x4_le_u
        0, // INS_i32x4_ge_s
        0, // INS_i32x4_ge_u
        0, // INS_i64x2_eq
        0, // INS_i64x2_ne
        0, // INS_i64x2_lt_s
        0, // INS_i64x2_gt_s
        0, // INS_i64x2_le_s
        0, // INS_i64x2_ge_s
        0, // INS_f32x4_eq
        0, // INS_f32x4_ne
        0, // INS_f32x4_lt
        0, // INS_f32x4_gt
        0, // INS_f32x4_le
        0, // INS_f32x4_ge
        0, // INS_f64x2_eq
        0, // INS_f64x2_ne
        0, // INS_f64x2_lt
        0, // INS_f64x2_gt
        0, // INS_f64x2_le
        0, // INS_f64x2_ge
        0, // INS_v128_not
        0, // INS_v128_and
        0, // INS_v128_andnot
        0, // INS_v128_or
        0, // INS_v128_xor
        0, // INS_v128_bitselect
        0, // INS_v128_any_true
        2, // INS_v128_load8_lane
        4, // INS_v128_load16_lane
        8, // INS_v128_load32_lane
        16, // INS_v128_load64_lane
        2, // INS_v128_store8_lane
        4, // INS_v128_store16_lane
        8, // INS_v128_store32_lane
        16, // INS_v128_store64_lane
        0, // INS_v128_load32_zero
        0, // INS_v128_load64_zero
        0, // INS_f32x4_demote_f64x2_zero
        0, // INS_f64x2_promote_low_f32x4
        0, // INS_i8x16_abs
        0, // INS_i8x16_neg
        0, // INS_i8x16_popcnt
        0, // INS_i8x16_all_true
        0, // INS_i8x16_bitmask
        0, // INS_i8x16_narrow_i16x8_s
        0, // INS_i8x16_narrow_i16x8_u
        0, // INS_f32x4_ceil
        0, // INS_f32x4_floor
        0, // INS_f32x4_trunc
        0, // INS_f32x4_nearest
        0, // INS_i8x16_shl
        0, // INS_i8x16_shr_s
        0, // INS_i8x16_shr_u
        0, // INS_i8x16_add
        0, // INS_i8x16_add_sat_s
        0, // INS_i8x16_add_sat_u
        0, // INS_i8x16_sub
        0, // INS_i8x16_sub_sat_s
        0, // INS_i8x16_sub_sat_u
        0, // INS_f64x2_ceil
        0, // INS_f64x2_floor
        0, // INS_i8x16_min_s
        0, // INS_i8x16_min_u
        0, // INS_i8x16_max_s
        0, // INS_i8x16_max_u
        0, // INS_f64x2_trunc
        0, // INS_i8x16_avgr_u
        0, // INS_i16x8_extadd_pairwise_s_i8x16
        0, // INS_i16x8_extadd_pairwise_u_i8x16
        0, // INS_i32x4_extadd_pairwise_s_i16x8
        0, // INS_i32x4_extadd_pairwise_u_i16x8
        0, // INS_i16x8_abs
        0, // INS_i16x8_neg
        0, // INS_i16x8_q15mulr_sat_s
        0, // INS_i16x8_all_true
        0, // INS_i16x8_bitmask
        0, // INS_i16x8_narrow_i32x4_s
        0, // INS_i16x8_narrow_i32x4_u
        0, // INS_i16x8_extend_low_s_i8x16
        0, // INS_i16x8_extend_high_s_i8x16
        0, // INS_i16x8_extend_low_u_i8x16
        0, // INS_i16x8_extend_high_u_i8x16
        0, // INS_i16x8_shl
        0, // INS_i16x8_shr_s
        0, // INS_i16x8_shr_u
        0, // INS_i16x8_add
        0, // INS_i16x8_add_sat_s
        0, // INS_i16x8_add_sat_u
        0, // INS_i16x8_sub
        0, // INS_i16x8_sub_sat_s
        0, // INS_i16x8_sub_sat_u
        0, // INS_f64x2_nearest
        0, // INS_i16x8_mul
        0, // INS_i16x8_min_s
        0, // INS_i16x8_min_u
        0, // INS_i16x8_max_s
        0, // INS_i16x8_max_u
        0, // INS_i16x8_avgr_u
        0, // INS_i16x8_extmul_low_s_i8x16
        0, // INS_i16x8_extmul_high_s_i8x16
        0, // INS_i16x8_extmul_low_u_i8x16
        0, // INS_i16x8_extmul_high_u_i8x16
        0, // INS_i32x4_abs
        0, // INS_i32x4_neg
        0, // INS_i32x4_all_true
        0, // INS_i32x4_bitmask
        0, // INS_i32x4_extend_low_s_i16x8
        0, // INS_i32x4_extend_high_s_i16x8
        0, // INS_i32x4_extend_low_u_i16x8
        0, // INS_i32x4_extend_high_u_i16x8
        0, // INS_i32x4_shl
        0, // INS_i32x4_shr_s
        0, // INS_i32x4_shr_u
        0, // INS_i32x4_add
        0, // INS_i32x4_sub
        0, // INS_i32x4_mul
        0, // INS_i32x4_min_s
        0, // INS_i32x4_min_u
        0, // INS_i32x4_max_s
        0, // INS_i32x4_max_u
        0, // INS_i32x4_dot_i16x8_s
        0, // INS_i32x4_extmul_low_s_i16x8
        0, // INS_i32x4_extmul_high_s_i16x8
        0, // INS_i32x4_extmul_low_u_i16x8
        0, // INS_i32x4_extmul_high_u_i16x8
        0, // INS_i64x2_abs
        0, // INS_i64x2_neg
        0, // INS_i64x2_all_true
        0, // INS_i64x2_bitmask
        0, // INS_i64x2_extend_low_s_i32x4
        0, // INS_i64x2_extend_high_s_i32x4
        0, // INS_i64x2_extend_low_u_i32x4
        0, // INS_i64x2_extend_high_u_i32x4
        0, // INS_i64x2_shl
        0, // INS_i64x2_shr_s
        0, // INS_i64x2_shr_u
        0, // INS_i64x2_add
        0, // INS_i64x2_sub
        0, // INS_i64x2_mul
        0, // INS_i64x2_extmul_low_s_i32x4
        0, // INS_i64x2_extmul_high_s_i32x4
        0, // INS_i64x2_extmul_low_u_i32x4
        0, // INS_i64x2_extmul_high_u_i32x4
        0, // INS_f32x4_abs
        0, // INS_f32x4_neg
        0, // INS_f32x4_sqrt
        0, // INS_f32x4_add
        0, // INS_f32x4_sub
        0, // INS_f32x4_mul
        0, // INS_f32x4_div
        0, // INS_f32x4_min
        0, // INS_f32x4_max
        0, // INS_f32x4_pmin
        0, // INS_f32x4_pmax
        0, // INS_f64x2_abs
        0, // INS_f64x2_neg
        0, // INS_f64x2_sqrt
        0, // INS_f64x2_add
        0, // INS_f64x2_sub
        0, // INS_f64x2_mul
        0, // INS_f64x2_div
        0, // INS_f64x2_min
        0, // INS_f64x2_max
        0, // INS_f64x2_pmin
        0, // INS_f64x2_pmax
        0, // INS_i32x4_trunc_sat_s_f32x4
        0, // INS_i32x4_trunc_sat_u_f32x4
        0, // INS_f32x4_convert_s_i32x4
        0, // INS_f32x4_convert_u_i32x4
        0, // INS_i32x4_trunc_sat_s_f64x2_zero
        0, // INS_i32x4_trunc_sat_u_f64x2_zero
        0, // INS_f64x2_convert_low_s_i32x4
        0, // INS_f64x2_convert_low_u_i32x4
#else
#error Unsupported or unset target architecture
#endif

#if TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
        0, // INS_lea is zero-initialized in native instInfo[INS_count].
#endif
    ];
#endif
}