// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TRACK_LSRA_STATS
using System.Globalization;
using System.IO;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    public void dumpLsraStatsSummary(StreamWriter streamWriter)
    {
        var sumStats = new uint[(int)LsraStat.STAT_FREE];
        var weightedStats = new double[(int)LsraStat.STAT_FREE];
        var blockInfo = _blockInfo
            ?? throw new FatalJitException("LSRA statistics require initialized block information.");

        for (var index = 0; index < sumStats.Length; index++)
        {
            var stat = blockInfo[0].stats?[index]
                ?? throw new FatalJitException("The entry block has no LSRA statistics.");
            sumStats[index] = unchecked(sumStats[index] + stat);
            weightedStats[index] += stat * blockInfo[0].weight;
        }

        foreach (var block in _compiler.Blocks)
        {
            if ((uint)block.bbNum > _bbNumMaxBeforeResolution)
            {
                continue;
            }

            var stats = blockInfo[block.bbNum].stats
                ?? throw new FatalJitException("A pre-resolution block has no LSRA statistics.");
            for (var index = 0; index < sumStats.Length; index++)
            {
                var stat = stats[index];
                sumStats[index] = unchecked(sumStats[index] + stat);
                weightedStats[index] += stat * block.bbWeight;
            }
        }

        for (var index = 0; index < sumStats.Length; index++)
        {
            var name = s_lsraStatNames[index];
            streamWriter.Write(", ");
            streamWriter.Write(name);
            streamWriter.Write(' ');
            streamWriter.Write(sumStats[index].ToString(CultureInfo.InvariantCulture));
            streamWriter.Write(' ');
            streamWriter.Write(name);
            streamWriter.Write("Wt ");
            streamWriter.Write(weightedStats[index].ToString("F6", CultureInfo.InvariantCulture));
        }
    }
}
#endif
