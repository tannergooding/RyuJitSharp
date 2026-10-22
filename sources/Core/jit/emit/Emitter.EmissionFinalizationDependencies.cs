// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    internal static class EmitterStatistics
    {
        internal const int SMALL_CNS_TSZ = 256;

        internal static uint totAllocdSize;
        internal static uint totActualSize;

        internal static uint emitTotalIDescCnsCnt;
        internal static uint emitTotalIDescDspCnt;
        internal static uint emitTotalIDescCnsDspCnt;
        internal static uint emitTotalIDescAmdCnt;
        internal static uint emitTotalIDescCnsAmdCnt;
        internal static uint emitTotalIDescRelocCnt;
        internal static uint emitTotalIDescCGCACnt;
        internal static uint emitTotalIDescLclVarPairCnt;
        internal static uint emitTotalIDescLclVarPairCnsCnt;

        internal static uint emitSmallCnsCnt;
        internal static uint emitLargeCnsCnt;
        internal static uint emitInt8CnsCnt;
        internal static uint emitInt16CnsCnt;
        internal static uint emitInt32CnsCnt;
        internal static uint emitNegCnsCnt;
        internal static uint emitPow2CnsCnt;
        internal static uint emitSmallDspCnt;
        internal static uint emitLargeDspCnt;

        internal static nuint emitSizeMethod;
        internal static uint emitCurPrologInsCnt;
        internal static nuint emitCurPrologIGSize;
        internal static uint emitMaxPrologInsCnt;
        internal static nuint emitMaxPrologIGSize;
        internal static uint emitTotalIGptrs;
        internal static uint emitTotalIGicnt;
        internal static nuint emitTotalIGsize;
        internal static uint emitTotalIGExtend;
        internal static nuint emitTotMemAlloc;

        internal static uint emitTotalInsCnt;
        internal static uint emitTotalIDescCnt;
        internal static uint emitTotalIDescSmallCnt;
        internal static uint emitTotalIDescJmpCnt;
        internal static uint emitTotalIDescLblCnt;
        internal static uint emitTotalIDescAlignCnt;
        internal static uint emitTotalIGcnt;
        internal static uint emitTotalIGjmps;
        internal static uint emitTotalIGmcnt;
        internal static uint emitTotalPhIGcnt;

        internal static readonly Histogram? SizeTable = Configuration.EMITTER_STATS
            ? new Histogram([100, 1024 * 1, 1024 * 2, 1024 * 3, 1024 * 4, 1024 * 5, 1024 * 10, 0])
            : null;
        internal static readonly Histogram? GCReferencesTable = Configuration.EMITTER_STATS
            ? new Histogram([0, 1, 2, 5, 10, 20, 50, 128, 256, 512, 1024, 0])
            : null;
        internal static readonly Histogram? StackDepthTable = Configuration.EMITTER_STATS
            ? new Histogram([0, 1, 2, 5, 10, 16, 32, 128, 1024, 0])
            : null;
        internal static readonly uint[]? SmallConstants =
            Configuration.EMITTER_STATS ? new uint[SMALL_CNS_TSZ] : null;
        internal static readonly uint[] InstructionFormatCounts =
            Configuration.EMITTER_STATS ? new uint[emitFmtToOps.Length] : null!;
    }
}
