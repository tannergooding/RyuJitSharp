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
#endif
}