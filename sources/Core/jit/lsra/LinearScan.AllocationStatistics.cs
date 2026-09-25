// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TRACK_LSRA_STATS
using System.Globalization;
using System.IO;
#if DEBUG
using System.Runtime.InteropServices;
#endif

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private unsafe void dumpLsraStats(StreamWriter streamWriter)
    {
        var sumStats = new uint[(int)LsraStat.COUNT];
        var weightedStats = new double[(int)LsraStat.COUNT];
        var blockInfo = _blockInfo
            ?? throw new FatalJitException("LSRA statistics require initialized block information.");

        streamWriter.Write("----------\n");
        streamWriter.Write("LSRA Stats");
#if DEBUG
        if (!VERBOSE)
        {
            streamWriter.Write(" : ");
            streamWriter.Write(_compiler.info.compFullName);
            streamWriter.Write('\n');
        }
        else
        {
            streamWriter.Write('\n');
        }
#else
        streamWriter.Write(" : ");
        streamWriter.Write(_compiler.eeGetMethodFullName(_compiler.info.compCompHnd));
        streamWriter.Write('\n');
#endif

        streamWriter.Write("----------\n");
#if DEBUG
        var orderingPointer = JitConfig.JitLsraOrdering;
        var ordering = orderingPointer is null
            ? "ABCDEFGHIJKLMNOPQ"
            : Marshal.PtrToStringAnsi((nint)orderingPointer);
        streamWriter.Write("Register selection order: ");
        streamWriter.Write(ordering);
        streamWriter.Write('\n');
#endif
        streamWriter.Write("Total Tracked Vars:  ");
        streamWriter.Write(_compiler.lvaTrackedCount.ToString(CultureInfo.InvariantCulture));
        streamWriter.Write('\n');

        // Native construction increments regCandidateVarCount once per local interval.
        var registerCandidateCount = 0;
        foreach (var interval in intervals)
        {
            if (interval.isLocalVar)
            {
                registerCandidateCount++;
            }
        }
        streamWriter.Write("Total Reg Cand Vars: ");
        streamWriter.Write(registerCandidateCount.ToString(CultureInfo.InvariantCulture));
        streamWriter.Write('\n');
        streamWriter.Write("Total number of Intervals: ");
        streamWriter.Write((intervals.Count == 0 ? 0 : intervals.Count - 1).ToString(CultureInfo.InvariantCulture));
        streamWriter.Write('\n');
        streamWriter.Write("Total number of RefPositions: ");
        streamWriter.Write((refPositions.Count - 1).ToString(CultureInfo.InvariantCulture));
        streamWriter.Write('\n');

        uint spillTemps = 0;
        for (var index = 0; index < (int)TYP_COUNT; index++)
        {
            spillTemps = unchecked(spillTemps + _maxSpill[index]);
        }
        streamWriter.Write("Total Number of spill temps created: ");
        streamWriter.Write(spillTemps.ToString(CultureInfo.InvariantCulture));
        streamWriter.Write('\n');
        streamWriter.Write("..........\n");

        void DumpBlockStats(int blockNumber, double weight)
        {
            if ((uint)blockNumber >= (uint)blockInfo.Length)
            {
                throw new FatalJitException("LSRA statistics do not cover a pre-resolution block.");
            }

            var stats = blockInfo[blockNumber].stats
                ?? throw new FatalJitException("A pre-resolution block has no LSRA statistics.");
            var addedBlockHeader = false;
            for (var index = 0; index < (int)LsraStat.COUNT; index++)
            {
                var value = stats[index];
                if (value == 0)
                {
                    continue;
                }

                if (!addedBlockHeader)
                {
                    addedBlockHeader = true;
                    streamWriter.Write(FMT_BB(blockNumber));
                    streamWriter.Write(" [");
                    streamWriter.Write(weight.ToString("F2", CultureInfo.InvariantCulture).PadLeft(8));
                    streamWriter.Write("]: ");
                }
                else
                {
                    streamWriter.Write(", ");
                }

                streamWriter.Write(s_lsraStatNames[index]);
                streamWriter.Write(" = ");
                streamWriter.Write(value.ToString(CultureInfo.InvariantCulture));
                sumStats[index] = unchecked(sumStats[index] + value);
                weightedStats[index] += value * weight;
            }

            if (addedBlockHeader)
            {
                streamWriter.Write('\n');
            }
        }

        DumpBlockStats(0, blockInfo[0].weight);
        foreach (var block in _compiler.Blocks)
        {
            if ((uint)block.bbNum > _bbNumMaxBeforeResolution)
            {
                continue;
            }

            DumpBlockStats(block.bbNum, block.bbWeight);
        }

        streamWriter.Write("..........\n");
        for (var index = 0; index < (int)LsraStat.COUNT; index++)
        {
            if (index == (int)LsraStat.STAT_FREE)
            {
                streamWriter.Write("..........\n");
            }
            if ((index < (int)LsraStat.STAT_FREE) || (sumStats[index] != 0))
            {
                streamWriter.Write("Total ");
                streamWriter.Write(s_lsraStatNames[index]);
                if (index >= (int)LsraStat.STAT_FREE)
                {
                    streamWriter.Write(" [#");
                    streamWriter.Write((index - (int)LsraStat.STAT_FREE + 1).ToString(CultureInfo.InvariantCulture).PadLeft(2));
                    streamWriter.Write(']');
                }
                streamWriter.Write(" : ");
                streamWriter.Write(sumStats[index].ToString(CultureInfo.InvariantCulture));
                streamWriter.Write("   Weighted: ");
                streamWriter.Write(weightedStats[index].ToString("F6", CultureInfo.InvariantCulture));
                streamWriter.Write('\n');
            }
        }

        jitprintf("\n");
    }
}
#endif
