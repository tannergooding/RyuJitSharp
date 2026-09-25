// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
global using static RyuJitSharp.insOpts;

using System;

namespace RyuJitSharp;

[Flags]
public enum insOpts : uint
{
    INS_OPTS_NONE = 0,

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
}
#endif
