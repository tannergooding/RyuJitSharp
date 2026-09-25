// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    public enum insFormat : uint
    {
#if TARGET_XARCH
        IF_NONE,
        IF_LABEL,
        IF_RWR_LABEL,
        IF_SWR_LABEL,
        IF_METHOD,
        IF_METHPTR,
        IF_CNS,
        IF_RRD,
        IF_RWR,
        IF_RRW,
        IF_RRD_CNS,
        IF_RWR_CNS,
        IF_RRW_CNS,
        IF_RRW_SHF,
        IF_RRD_RRD,
        IF_RWR_RRD,
        IF_RRW_RRD,
        IF_RRW_RRW,
        IF_RRD_RRD_CNS,
        IF_RWR_RRD_CNS,
        IF_RRW_RRD_CNS,
        IF_RWR_RRD_SHF,
        IF_RRD_RRD_RRD,
        IF_RWR_RRD_RRD,
        IF_RRW_RRD_RRD,
        IF_RWR_RWR_RRD,
        IF_RWR_RRD_RRD_CNS,
        IF_RWR_RRD_RRD_RRD,
        IF_MRD,
        IF_MWR,
        IF_MRW,
        IF_MRD_CNS,
        IF_MWR_CNS,
        IF_MRW_CNS,
        IF_MRW_SHF,
        IF_MRD_RRD,
        IF_MWR_RRD,
        IF_MRW_RRD,
        IF_MRW_RRW,
        IF_MRD_RRD_CNS,
        IF_MWR_RRD_CNS,
        IF_MRW_RRD_CNS,
        IF_MWR_RRD_RRD,
        IF_RRD_MRD,
        IF_RWR_MRD,
        IF_RRW_MRD,
        IF_RRD_MRD_CNS,
        IF_RWR_MRD_CNS,
        IF_RRW_MRD_CNS,
        IF_RRD_MRD_RRD,
        IF_RWR_MRD_RRD,
        IF_RRW_MRD_RRD,
        IF_RRD_RRD_MRD,
        IF_RWR_RRD_MRD,
        IF_RRW_RRD_MRD,
        IF_RWR_RWR_MRD,
        IF_RWR_RRD_MRD_CNS,
        IF_RWR_RRD_MRD_RRD,
        IF_MRD_OFF,
        IF_RWR_MRD_OFF,
        IF_SRD,
        IF_SWR,
        IF_SRW,
        IF_SRD_CNS,
        IF_SWR_CNS,
        IF_SRW_CNS,
        IF_SRW_SHF,
        IF_SRD_RRD,
        IF_SWR_RRD,
        IF_SRW_RRD,
        IF_SRW_RRW,
        IF_SRD_RRD_CNS,
        IF_SWR_RRD_CNS,
        IF_SRW_RRD_CNS,
        IF_SWR_RRD_RRD,
        IF_RRD_SRD,
        IF_RWR_SRD,
        IF_RRW_SRD,
        IF_RRD_SRD_CNS,
        IF_RWR_SRD_CNS,
        IF_RRW_SRD_CNS,
        IF_RRD_SRD_RRD,
        IF_RWR_SRD_RRD,
        IF_RRW_SRD_RRD,
        IF_RRD_RRD_SRD,
        IF_RWR_RRD_SRD,
        IF_RRW_RRD_SRD,
        IF_RWR_RWR_SRD,
        IF_RWR_RRD_SRD_CNS,
        IF_RWR_RRD_SRD_RRD,
        IF_ARD,
        IF_AWR,
        IF_ARW,
        IF_ARD_CNS,
        IF_AWR_CNS,
        IF_ARW_CNS,
        IF_ARW_SHF,
        IF_ARD_RRD,
        IF_AWR_RRD,
        IF_ARW_RRD,
        IF_ARW_RRW,
        IF_ARD_RRD_CNS,
        IF_AWR_RRD_CNS,
        IF_ARW_RRD_CNS,
        IF_AWR_RRD_RRD,
        IF_RRD_ARD,
        IF_RWR_ARD,
        IF_RRW_ARD,
        IF_RRD_ARD_CNS,
        IF_RWR_ARD_CNS,
        IF_RRW_ARD_CNS,
        IF_RRD_ARD_RRD,
        IF_RWR_ARD_RRD,
        IF_RRW_ARD_RRD,
        IF_RRD_RRD_ARD,
        IF_RWR_RRD_ARD,
        IF_RRW_RRD_ARD,
        IF_RWR_RWR_ARD,
        IF_RWR_RRD_ARD_CNS,
        IF_RWR_RRD_ARD_RRD,
#endif
        IF_COUNT,
    }

#if TARGET_XARCH
    internal static ReadOnlySpan<byte> emitFmtToOps => [
        (byte)ID_OP_NONE, // IF_NONE
        (byte)ID_OP_JMP, // IF_LABEL
        (byte)ID_OP_JMP, // IF_RWR_LABEL
        (byte)ID_OP_LBL, // IF_SWR_LABEL
        (byte)ID_OP_CALL, // IF_METHOD
        (byte)ID_OP_CALL, // IF_METHPTR
        (byte)ID_OP_SCNS, // IF_CNS
        (byte)ID_OP_NONE, // IF_RRD
        (byte)ID_OP_NONE, // IF_RWR
        (byte)ID_OP_NONE, // IF_RRW
        (byte)ID_OP_SCNS, // IF_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_CNS
        (byte)ID_OP_SCNS, // IF_RRW_CNS
        (byte)ID_OP_SCNS, // IF_RRW_SHF
        (byte)ID_OP_NONE, // IF_RRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_RRD
        (byte)ID_OP_NONE, // IF_RRW_RRD
        (byte)ID_OP_NONE, // IF_RRW_RRW
        (byte)ID_OP_SCNS, // IF_RRD_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RRW_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_RRD_SHF
        (byte)ID_OP_NONE, // IF_RRD_RRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_RRD_RRD
        (byte)ID_OP_NONE, // IF_RRW_RRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_RWR_RRD
        (byte)ID_OP_SCNS, // IF_RWR_RRD_RRD_CNS
        (byte)ID_OP_SCNS, // IF_RWR_RRD_RRD_RRD
        (byte)ID_OP_SPEC, // IF_MRD
        (byte)ID_OP_DSP, // IF_MWR
        (byte)ID_OP_DSP, // IF_MRW
        (byte)ID_OP_DSP_CNS, // IF_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_MWR_CNS
        (byte)ID_OP_DSP_CNS, // IF_MRW_CNS
        (byte)ID_OP_DSP_CNS, // IF_MRW_SHF
        (byte)ID_OP_DSP, // IF_MRD_RRD
        (byte)ID_OP_DSP, // IF_MWR_RRD
        (byte)ID_OP_DSP, // IF_MRW_RRD
        (byte)ID_OP_DSP, // IF_MRW_RRW
        (byte)ID_OP_DSP_CNS, // IF_MRD_RRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_MWR_RRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_MRW_RRD_CNS
        (byte)ID_OP_DSP, // IF_MWR_RRD_RRD
        (byte)ID_OP_DSP, // IF_RRD_MRD
        (byte)ID_OP_DSP, // IF_RWR_MRD
        (byte)ID_OP_DSP, // IF_RRW_MRD
        (byte)ID_OP_DSP_CNS, // IF_RRD_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_RWR_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_RRW_MRD_CNS
        (byte)ID_OP_DSP, // IF_RRD_MRD_RRD
        (byte)ID_OP_DSP, // IF_RWR_MRD_RRD
        (byte)ID_OP_DSP, // IF_RRW_MRD_RRD
        (byte)ID_OP_DSP, // IF_RRD_RRD_MRD
        (byte)ID_OP_DSP, // IF_RWR_RRD_MRD
        (byte)ID_OP_DSP, // IF_RRW_RRD_MRD
        (byte)ID_OP_DSP, // IF_RWR_RWR_MRD
        (byte)ID_OP_DSP_CNS, // IF_RWR_RRD_MRD_CNS
        (byte)ID_OP_DSP_CNS, // IF_RWR_RRD_MRD_RRD
        (byte)ID_OP_DSP, // IF_MRD_OFF
        (byte)ID_OP_DSP, // IF_RWR_MRD_OFF
        (byte)ID_OP_SPEC, // IF_SRD
        (byte)ID_OP_NONE, // IF_SWR
        (byte)ID_OP_NONE, // IF_SRW
        (byte)ID_OP_CNS, // IF_SRD_CNS
        (byte)ID_OP_CNS, // IF_SWR_CNS
        (byte)ID_OP_CNS, // IF_SRW_CNS
        (byte)ID_OP_CNS, // IF_SRW_SHF
        (byte)ID_OP_NONE, // IF_SRD_RRD
        (byte)ID_OP_NONE, // IF_SWR_RRD
        (byte)ID_OP_NONE, // IF_SRW_RRD
        (byte)ID_OP_NONE, // IF_SRW_RRW
        (byte)ID_OP_CNS, // IF_SRD_RRD_CNS
        (byte)ID_OP_CNS, // IF_SWR_RRD_CNS
        (byte)ID_OP_CNS, // IF_SRW_RRD_CNS
        (byte)ID_OP_NONE, // IF_SWR_RRD_RRD
        (byte)ID_OP_NONE, // IF_RRD_SRD
        (byte)ID_OP_NONE, // IF_RWR_SRD
        (byte)ID_OP_NONE, // IF_RRW_SRD
        (byte)ID_OP_CNS, // IF_RRD_SRD_CNS
        (byte)ID_OP_CNS, // IF_RWR_SRD_CNS
        (byte)ID_OP_CNS, // IF_RRW_SRD_CNS
        (byte)ID_OP_NONE, // IF_RRD_SRD_RRD
        (byte)ID_OP_NONE, // IF_RWR_SRD_RRD
        (byte)ID_OP_NONE, // IF_RRW_SRD_RRD
        (byte)ID_OP_NONE, // IF_RRD_RRD_SRD
        (byte)ID_OP_NONE, // IF_RWR_RRD_SRD
        (byte)ID_OP_NONE, // IF_RRW_RRD_SRD
        (byte)ID_OP_NONE, // IF_RWR_RWR_SRD
        (byte)ID_OP_CNS, // IF_RWR_RRD_SRD_CNS
        (byte)ID_OP_CNS, // IF_RWR_RRD_SRD_RRD
        (byte)ID_OP_SPEC, // IF_ARD
        (byte)ID_OP_AMD, // IF_AWR
        (byte)ID_OP_AMD, // IF_ARW
        (byte)ID_OP_AMD_CNS, // IF_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_AWR_CNS
        (byte)ID_OP_AMD_CNS, // IF_ARW_CNS
        (byte)ID_OP_AMD_CNS, // IF_ARW_SHF
        (byte)ID_OP_AMD, // IF_ARD_RRD
        (byte)ID_OP_AMD, // IF_AWR_RRD
        (byte)ID_OP_AMD, // IF_ARW_RRD
        (byte)ID_OP_AMD, // IF_ARW_RRW
        (byte)ID_OP_AMD_CNS, // IF_ARD_RRD_CNS
        (byte)ID_OP_AMD_CNS, // IF_AWR_RRD_CNS
        (byte)ID_OP_AMD_CNS, // IF_ARW_RRD_CNS
        (byte)ID_OP_AMD_CNS, // IF_AWR_RRD_RRD
        (byte)ID_OP_AMD, // IF_RRD_ARD
        (byte)ID_OP_AMD, // IF_RWR_ARD
        (byte)ID_OP_AMD, // IF_RRW_ARD
        (byte)ID_OP_AMD_CNS, // IF_RRD_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_RWR_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_RRW_ARD_CNS
        (byte)ID_OP_AMD, // IF_RRD_ARD_RRD
        (byte)ID_OP_AMD, // IF_RWR_ARD_RRD
        (byte)ID_OP_AMD, // IF_RRW_ARD_RRD
        (byte)ID_OP_AMD, // IF_RRD_RRD_ARD
        (byte)ID_OP_AMD, // IF_RWR_RRD_ARD
        (byte)ID_OP_AMD, // IF_RRW_RRD_ARD
        (byte)ID_OP_AMD, // IF_RWR_RWR_ARD
        (byte)ID_OP_AMD_CNS, // IF_RWR_RRD_ARD_CNS
        (byte)ID_OP_AMD_CNS, // IF_RWR_RRD_ARD_RRD
    ];

    private static ReadOnlySpan<IS_INFO> emitFmtToSchedInfo => [
        IS_NONE, // IF_NONE
        IS_NONE, // IF_LABEL
        IS_R1_WR, // IF_RWR_LABEL
        IS_SF_WR, // IF_SWR_LABEL
        IS_NONE, // IF_METHOD
        IS_NONE, // IF_METHPTR
        IS_NONE, // IF_CNS
        IS_R1_RD, // IF_RRD
        IS_R1_WR, // IF_RWR
        IS_R1_RW, // IF_RRW
        IS_R1_RD, // IF_RRD_CNS
        IS_R1_WR, // IF_RWR_CNS
        IS_R1_RW, // IF_RRW_CNS
        IS_R1_RW, // IF_RRW_SHF
        IS_R1_RD | IS_R2_RD, // IF_RRD_RRD
        IS_R1_WR | IS_R2_RD, // IF_RWR_RRD
        IS_R1_RW | IS_R2_RD, // IF_RRW_RRD
        IS_R1_RW | IS_R2_RW, // IF_RRW_RRW
        IS_R1_RD | IS_R2_RD, // IF_RRD_RRD_CNS
        IS_R1_WR | IS_R2_RD, // IF_RWR_RRD_CNS
        IS_R1_RW | IS_R2_RD, // IF_RRW_RRD_CNS
        IS_R1_WR | IS_R2_RD, // IF_RWR_RRD_SHF
        IS_R1_RD | IS_R2_RD | IS_R3_RD, // IF_RRD_RRD_RRD
        IS_R1_WR | IS_R2_RD | IS_R3_RD, // IF_RWR_RRD_RRD
        IS_R1_RW | IS_R2_RD | IS_R3_RD, // IF_RRW_RRD_RRD
        IS_R1_WR | IS_R2_WR | IS_R3_RD, // IF_RWR_RWR_RRD
        IS_R1_WR | IS_R2_RD | IS_R3_RD, // IF_RWR_RRD_RRD_CNS
        IS_R1_WR | IS_R2_RD | IS_R3_RD | IS_R4_RD, // IF_RWR_RRD_RRD_RRD
        IS_GM_RD, // IF_MRD
        IS_GM_WR, // IF_MWR
        IS_GM_RW, // IF_MRW
        IS_GM_RD, // IF_MRD_CNS
        IS_GM_WR, // IF_MWR_CNS
        IS_GM_RW, // IF_MRW_CNS
        IS_GM_RW, // IF_MRW_SHF
        IS_GM_RD | IS_R1_RD, // IF_MRD_RRD
        IS_GM_WR | IS_R1_RD, // IF_MWR_RRD
        IS_GM_RW | IS_R1_RD, // IF_MRW_RRD
        IS_GM_RW | IS_R1_RW, // IF_MRW_RRW
        IS_GM_RD | IS_R1_RD, // IF_MRD_RRD_CNS
        IS_GM_WR | IS_R1_RD, // IF_MWR_RRD_CNS
        IS_GM_RW | IS_R1_RD, // IF_MRW_RRD_CNS
        IS_GM_WR | IS_R1_RD | IS_R2_RD, // IF_MWR_RRD_RRD
        IS_R1_RD | IS_GM_RD, // IF_RRD_MRD
        IS_R1_WR | IS_GM_RD, // IF_RWR_MRD
        IS_R1_RW | IS_GM_RD, // IF_RRW_MRD
        IS_R1_RD | IS_GM_RD, // IF_RRD_MRD_CNS
        IS_R1_WR | IS_GM_RD, // IF_RWR_MRD_CNS
        IS_R1_RW | IS_GM_RD, // IF_RRW_MRD_CNS
        IS_R1_RD | IS_GM_RD | IS_R2_RD, // IF_RRD_MRD_RRD
        IS_R1_WR | IS_GM_RD | IS_R2_RD, // IF_RWR_MRD_RRD
        IS_R1_RW | IS_GM_RD | IS_R2_RD, // IF_RRW_MRD_RRD
        IS_R1_RD | IS_R2_RD | IS_GM_RD, // IF_RRD_RRD_MRD
        IS_R1_WR | IS_R2_RD | IS_GM_RD, // IF_RWR_RRD_MRD
        IS_R1_RW | IS_R2_RD | IS_GM_RD, // IF_RRW_RRD_MRD
        IS_R1_WR | IS_R2_WR | IS_GM_RD, // IF_RWR_RWR_MRD
        IS_R1_WR | IS_R2_RD | IS_GM_RD, // IF_RWR_RRD_MRD_CNS
        IS_R1_WR | IS_R2_RD | IS_GM_RD | IS_R3_RD, // IF_RWR_RRD_MRD_RRD
        IS_GM_RD, // IF_MRD_OFF
        IS_R1_WR | IS_GM_RD, // IF_RWR_MRD_OFF
        IS_SF_RD, // IF_SRD
        IS_SF_WR, // IF_SWR
        IS_SF_RW, // IF_SRW
        IS_SF_RD, // IF_SRD_CNS
        IS_SF_WR, // IF_SWR_CNS
        IS_SF_RW, // IF_SRW_CNS
        IS_SF_RW, // IF_SRW_SHF
        IS_SF_RD | IS_R1_RD, // IF_SRD_RRD
        IS_SF_WR | IS_R1_RD, // IF_SWR_RRD
        IS_SF_RW | IS_R1_RD, // IF_SRW_RRD
        IS_SF_RW | IS_R1_RW, // IF_SRW_RRW
        IS_SF_RD | IS_R1_RD, // IF_SRD_RRD_CNS
        IS_SF_WR | IS_R1_RD, // IF_SWR_RRD_CNS
        IS_SF_RW | IS_R1_RD, // IF_SRW_RRD_CNS
        IS_SF_WR | IS_R1_RD | IS_R2_RD, // IF_SWR_RRD_RRD
        IS_R1_RD | IS_SF_RD, // IF_RRD_SRD
        IS_R1_WR | IS_SF_RD, // IF_RWR_SRD
        IS_R1_RW | IS_SF_RD, // IF_RRW_SRD
        IS_R1_RD | IS_SF_RD, // IF_RRD_SRD_CNS
        IS_R1_WR | IS_SF_RD, // IF_RWR_SRD_CNS
        IS_R1_RW | IS_SF_RD, // IF_RRW_SRD_CNS
        IS_R1_RD | IS_SF_RD | IS_R2_RD, // IF_RRD_SRD_RRD
        IS_R1_WR | IS_SF_RD | IS_R2_RD, // IF_RWR_SRD_RRD
        IS_R1_RW | IS_SF_RD | IS_R2_RD, // IF_RRW_SRD_RRD
        IS_R1_RD | IS_R2_RD | IS_SF_RD, // IF_RRD_RRD_SRD
        IS_R1_WR | IS_R2_RD | IS_SF_RD, // IF_RWR_RRD_SRD
        IS_R1_RW | IS_R2_RD | IS_SF_RD, // IF_RRW_RRD_SRD
        IS_R1_WR | IS_R2_WR | IS_SF_RD, // IF_RWR_RWR_SRD
        IS_R1_WR | IS_R2_RD | IS_SF_RD, // IF_RWR_RRD_SRD_CNS
        IS_R1_WR | IS_R2_RD | IS_SF_RD | IS_R3_RD, // IF_RWR_RRD_SRD_RRD
        IS_AM_RD, // IF_ARD
        IS_AM_WR, // IF_AWR
        IS_AM_RW, // IF_ARW
        IS_AM_RD, // IF_ARD_CNS
        IS_AM_WR, // IF_AWR_CNS
        IS_AM_RW, // IF_ARW_CNS
        IS_AM_RW, // IF_ARW_SHF
        IS_AM_RD | IS_R1_RD, // IF_ARD_RRD
        IS_AM_WR | IS_R1_RD, // IF_AWR_RRD
        IS_AM_RW | IS_R1_RD, // IF_ARW_RRD
        IS_AM_RW | IS_R1_RW, // IF_ARW_RRW
        IS_AM_RD | IS_R1_RD, // IF_ARD_RRD_CNS
        IS_AM_WR | IS_R1_RD, // IF_AWR_RRD_CNS
        IS_AM_RW | IS_R1_RD, // IF_ARW_RRD_CNS
        IS_AM_WR | IS_R1_RD | IS_R2_RD, // IF_AWR_RRD_RRD
        IS_R1_RD | IS_AM_RD, // IF_RRD_ARD
        IS_R1_WR | IS_AM_RD, // IF_RWR_ARD
        IS_R1_RW | IS_AM_RD, // IF_RRW_ARD
        IS_R1_RD | IS_AM_RD, // IF_RRD_ARD_CNS
        IS_R1_WR | IS_AM_RD, // IF_RWR_ARD_CNS
        IS_R1_RW | IS_AM_RD, // IF_RRW_ARD_CNS
        IS_R1_RD | IS_AM_RD | IS_R2_RD, // IF_RRD_ARD_RRD
        IS_R1_WR | IS_AM_RD | IS_R2_RD, // IF_RWR_ARD_RRD
        IS_R1_RW | IS_AM_RD | IS_R2_RD, // IF_RRW_ARD_RRD
        IS_R1_RD | IS_R2_RD | IS_AM_RD, // IF_RRD_RRD_ARD
        IS_R1_WR | IS_R2_RD | IS_AM_RD, // IF_RWR_RRD_ARD
        IS_R1_RW | IS_R2_RD | IS_AM_RD, // IF_RRW_RRD_ARD
        IS_R1_WR | IS_R2_WR | IS_AM_RD, // IF_RWR_RWR_ARD
        IS_R1_WR | IS_R2_RD | IS_AM_RD, // IF_RWR_RRD_ARD_CNS
        IS_R1_WR | IS_R2_RD | IS_AM_RD | IS_R3_RD, // IF_RWR_RRD_ARD_RRD
    ];
#endif
}