// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.insOpts;

namespace RyuJitSharp;

[System.Flags]
public enum insOpts : uint
{
    INS_OPTS_NONE = 0,

    // Xarch consumes these values as bit flags; the other backends use option identities.
    INS_OPTS_EVEX_b_MASK = 0x03,
    INS_OPTS_EVEX_eb = 1,
    INS_OPTS_EVEX_cd = 2,
    INS_OPTS_EVEX_er_rd = 1,
    INS_OPTS_EVEX_er_ru = 2,
    INS_OPTS_EVEX_er_rz = 3,

    INS_OPTS_EVEX_aaa_MASK = 0x1C,
    INS_OPTS_EVEX_em_k1 = 1 << 2,
    INS_OPTS_EVEX_em_k2 = 2 << 2,
    INS_OPTS_EVEX_em_k3 = 3 << 2,
    INS_OPTS_EVEX_em_k4 = 4 << 2,
    INS_OPTS_EVEX_em_k5 = 5 << 2,
    INS_OPTS_EVEX_em_k6 = 6 << 2,
    INS_OPTS_EVEX_em_k7 = 7 << 2,

    INS_OPTS_EVEX_z_MASK = 0x20,
    INS_OPTS_EVEX_em_zero = 1 << 5,

    INS_OPTS_EVEX_nd_MASK = 0x40,
    INS_OPTS_EVEX_nd = 1 << 6,

    INS_OPTS_EVEX_nf_MASK = 0x80,
    INS_OPTS_EVEX_nf = 1 << 7,

    INS_OPTS_EVEX_dfv_shift = 8,
    INS_OPTS_EVEX_dfv_cf = 1 << 8,
    INS_OPTS_EVEX_dfv_zf = 1 << 9,
    INS_OPTS_EVEX_dfv_sf = 1 << 10,
    INS_OPTS_EVEX_dfv_of = 1 << 11,
    INS_OPTS_EVEX_dfv_MASK = 0xF00,

    INS_OPTS_EVEX_NoApxPromotion = 1 << 12,

    INS_OPTS_APX_ppx = 1 << 13,
    INS_OPTS_APX_ppx_MASK = 0x2000,

    INS_OPTS_EVEX_zu = 1 << 14,
    INS_OPTS_EVEX_zu_MASK = 0x4000,

    INS_OPTS_LDST_PRE_DEC = 1,
    INS_OPTS_LDST_POST_INC = 2,
    INS_OPTS_RRX = 3,

    INS_OPTS_PRE_INDEX = 1,
    INS_OPTS_POST_INDEX = 2,
    INS_OPTS_LSL12 = 3,
    INS_OPTS_LSL = 4,
    INS_OPTS_LSR = 5,
    INS_OPTS_ASR = 6,
    INS_OPTS_ROR = 7,

    INS_OPTS_UXTB = 8,
    INS_OPTS_UXTH = 9,
    INS_OPTS_UXTW = 10,
    INS_OPTS_UXTX = 11,
    INS_OPTS_SXTB = 12,
    INS_OPTS_SXTH = 13,
    INS_OPTS_SXTW = 14,
    INS_OPTS_SXTX = 15,

    INS_OPTS_8B = 16,
    INS_OPTS_16B = 17,
    INS_OPTS_4H = 18,
    INS_OPTS_8H = 19,
    INS_OPTS_2S = 20,
    INS_OPTS_4S = 21,
    INS_OPTS_1D = 22,
    INS_OPTS_2D = 23,

    INS_OPTS_SCALABLE_B = 24,
    INS_OPTS_SCALABLE_H = 25,
    INS_OPTS_SCALABLE_S = 26,
    INS_OPTS_SCALABLE_D = 27,
    INS_OPTS_SCALABLE_Q = 28,

    INS_OPTS_SCALABLE_S_UXTW = 29,
    INS_OPTS_SCALABLE_S_SXTW = 30,
    INS_OPTS_SCALABLE_D_UXTW = 31,
    INS_OPTS_SCALABLE_D_SXTW = 32,

    INS_OPTS_MSL = 33,

    INS_OPTS_S_TO_4BYTE = 34,
    INS_OPTS_D_TO_4BYTE = 35,

    INS_OPTS_S_TO_8BYTE = 36,
    INS_OPTS_D_TO_8BYTE = 37,

    INS_OPTS_H_TO_4BYTE = 38,
    INS_OPTS_H_TO_8BYTE = 39,

    INS_OPTS_4BYTE_TO_S = 40,
    INS_OPTS_4BYTE_TO_D = 41,

    INS_OPTS_8BYTE_TO_S = 42,
    INS_OPTS_8BYTE_TO_D = 43,

    INS_OPTS_4BYTE_TO_H = 44,
    INS_OPTS_8BYTE_TO_H = 45,

    INS_OPTS_S_TO_D = 46,
    INS_OPTS_D_TO_S = 47,

    INS_OPTS_H_TO_S = 48,
    INS_OPTS_H_TO_D = 49,

    INS_OPTS_S_TO_H = 50,
    INS_OPTS_D_TO_H = 51,
    INS_OPTS_ALIGN = 52,

    INS_OPTS_RC = 1, // see ::emitIns_R_C().
    INS_OPTS_RL = 2, // see ::emitIns_R_L().
    INS_OPTS_JIRL = 3, // see ::emitIns_J_R().
    INS_OPTS_J = 4, // see ::emitIns_J().
    INS_OPTS_J_cond = 5, // see ::emitIns_J_cond_la().
    INS_OPTS_I_LOONGARCH64 = 6, // see ::emitIns_I_la().
    INS_OPTS_C_LOONGARCH64 = 7, // see ::emitIns_Call().
    INS_OPTS_RELOC_LOONGARCH64 = 8, // see ::emitIns_R_AI().

    INS_OPTS_JUMP = 3, // see ::emitIns_J() and ::emitIns_J_cond_la().
    INS_OPTS_I_RISCV64 = 4, // see ::emitLoadImmediate().
    INS_OPTS_C_RISCV64 = 5, // see ::emitIns_Call().
    INS_OPTS_RELOC_RISCV64 = 6, // see ::emitIns_R_AI().
}
