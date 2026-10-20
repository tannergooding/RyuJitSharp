// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Globals
{
    private static class BlockStatisticsData
    {
        internal static readonly Histogram? BasicBlockCounts =
            Configuration.COUNT_BASIC_BLOCKS ? new([1, 2, 3, 5, 10, 20, 50, 100, 1000, 10000, 0]) : null;

        internal static readonly Histogram? SingleBlockMethodSizes =
            Configuration.COUNT_BASIC_BLOCKS ? new([1, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 0]) : null;

        internal static readonly Histogram? ReachabilityIterations =
            Configuration.COUNT_BASIC_BLOCKS ? new([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 0]) : null;
    }

    public static Histogram? bbCntTable => BlockStatisticsData.BasicBlockCounts;

    public static Histogram? bbOneBBSizeTable => BlockStatisticsData.SingleBlockMethodSizes;

    public static Histogram? computeReachabilitySetsIterationTable => BlockStatisticsData.ReachabilityIterations;
}
