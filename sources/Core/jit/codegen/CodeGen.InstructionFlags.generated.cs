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

    ];
#endif
}