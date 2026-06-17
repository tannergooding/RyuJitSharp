// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
#if TARGET_XARCH
    public const instruction FIRST_SSE_INSTRUCTION = INS_addpd;
    public const instruction LAST_SSE_INSTRUCTION = INS_gf2p8mulb;
    public const instruction FIRST_AVX_INSTRUCTION = INS_vblendvpd;
    public const instruction FIRST_FMA_INSTRUCTION = INS_vfmadd132pd;
    public const instruction LAST_FMA_INSTRUCTION = INS_vfnmsub231ss;
    public const instruction FIRST_BMI_INSTRUCTION = INS_andn;
    public const instruction LAST_BMI_INSTRUCTION = INS_shrx;
    public const instruction FIRST_AVXVNNI_INSTRUCTION = INS_vpdpbusd;
    public const instruction LAST_AVXVNNI_INSTRUCTION = INS_vpdpwssds;
    public const instruction FIRST_AVXVNNIINT8_INSTRUCTION = INS_vpdpwsud;
    public const instruction LAST_AVXVNNIINT8_INSTRUCTION = INS_vpdpwuuds;
    public const instruction FIRST_AVXVNNIINT16_INSTRUCTION = INS_vpdpbssd;
    public const instruction LAST_AVXVNNIINT16_INSTRUCTION = INS_vpdpbuuds;
    public const instruction FIRST_AVX512BMM_INSTRUCTION = INS_vbmacor16x16x16;
    public const instruction LAST_AVX512BMM_INSTRUCTION = INS_vbitrev;
    public const instruction FIRST_AVXIFMA_INSTRUCTION = INS_vpmadd52huq;
    public const instruction LAST_AVXIFMA_INSTRUCTION = INS_vpmadd52luq;
    public const instruction LAST_AVX_INSTRUCTION = INS_vpmadd52luq;
    public const instruction FIRST_AVX512_INSTRUCTION = INS_kaddb;
    public const instruction LAST_AVX512_INSTRUCTION = INS_vucomxss;
    public const instruction FIRST_APX_INSTRUCTION = INS_ccmpo;
    public const instruction FIRST_CCMP_INSTRUCTION = INS_ccmpo;
    public const instruction LAST_CCMP_INSTRUCTION = INS_ccmpg;
    public const instruction FIRST_CFCMOV_INSTRUCTION = INS_cfcmovo;
    public const instruction LAST_CFCMOV_INSTRUCTION = INS_cfcmovg;
    public const instruction FIRST_CTEST_INSTRUCTION = INS_ctesto;
    public const instruction LAST_CTEST_INSTRUCTION = INS_ctestg;
    public const instruction LAST_APX_INSTRUCTION = INS_setg_apx;
#endif
}
