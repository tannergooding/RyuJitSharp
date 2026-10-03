// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Globalization;
using System.IO;

namespace RyuJitSharp;

public partial class Emitter
{
#if EMITTER_STATS
#if TARGET_XARCH
    private const uint emitTotalIDescLblCnt = 0;
#endif

    internal static void emitterStats(StreamWriter output)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "emitterStats outside xarch is not ported.");
#else
        if (totAllocdSize > 0)
        {
            assert(totActualSize <= totAllocdSize);

            output.Write(FormattableString.Invariant($"\nTotal allocated code size = {totAllocdSize}\n"));

            if (totActualSize < totAllocdSize)
            {
                output.Write(FormattableString.Invariant($"Total generated code size = {totActualSize}  ("));
                WriteEmitterStatistic(output,
                    100 * ((totAllocdSize - totActualSize) / (double)totActualSize), 4, 3);
                output.Write("% waste)\n");
            }

            assert(emitTotalInsCnt > 0);

            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)totActualSize / emitTotalInsCnt, 4, 2);
            output.Write(" bytes of code generated per instruction\n");
        }

        output.Write("\nInstruction format frequency table:\n\n");

        uint instructionCount = 0;
        uint displayedInstructionCount = 0;

        for (var format = 0; format < emitIFcounts.Length; format++)
        {
            instructionCount = unchecked(instructionCount + emitIFcounts[format]);
        }

        for (var format = 0; format < emitIFcounts.Length; format++)
        {
            var count = emitIFcounts[format];

            if ((count > 0) && (unchecked(1000u * count) >= instructionCount))
            {
                displayedInstructionCount = unchecked(displayedInstructionCount + count);
                output.Write(FormattableString.Invariant($"          {emitIfName((uint)format),-14} {count,8} ("));
                WriteEmitterStatistic(output, (100.0 * count) / instructionCount, 5, 2);
                output.Write("%)\n");
            }
        }

        output.Write("         ---------------------------------\n");
        output.Write(FormattableString.Invariant($"          {"Total shown",-14} {displayedInstructionCount,8} ("));
        WriteEmitterStatistic(output, (100.0 * displayedInstructionCount) / instructionCount, 5, 2);
        output.Write("%)\n");

        if (emitTotalIGmcnt > 0)
        {
            output.Write("\n");
            output.Write(FormattableString.Invariant($"Total of {emitTotalIGmcnt,8} methods\n"));
            output.Write(FormattableString.Invariant($"Total of {emitTotalIGcnt,8} insGroup\n"));
            output.Write(FormattableString.Invariant($"Total of {emitTotalPhIGcnt,8} insPlaceholderGroupData\n"));
            output.Write(FormattableString.Invariant($"Total of {emitTotalIGExtend,8} extend insGroup\n"));
            output.Write(FormattableString.Invariant($"Total of {emitTotalIGicnt,8} instructions\n"));
            output.Write(FormattableString.Invariant($"Total of {emitTotalIGjmps,8} jumps\n"));
            output.Write(FormattableString.Invariant($"Total of {emitTotalIGptrs,8} GC livesets\n"));
            output.Write("\n");
            output.Write(FormattableString.Invariant($"Max prolog instrDesc count: {emitMaxPrologInsCnt,8}\n"));
            output.Write(FormattableString.Invariant($"Max prolog insGroup size  : {emitMaxPrologIGSize,8}\n"));
            output.Write("\n");

            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalIGcnt / emitTotalIGmcnt, 8, 1);
            output.Write(" insGroup     per method\n");
            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalPhIGcnt / emitTotalIGmcnt, 8, 1);
            output.Write(" insPhGroup   per method\n");
            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalIGExtend / emitTotalIGmcnt, 8, 1);
            output.Write(" extend IG    per method\n");
            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalIGicnt / emitTotalIGmcnt, 8, 1);
            output.Write(" instructions per method\n");
            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalIGsize / emitTotalIGmcnt, 8, 1);
            output.Write(" desc.  bytes per method\n");
            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalIGjmps / emitTotalIGmcnt, 8, 1);
            output.Write(" jumps        per method\n");
            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalIGptrs / emitTotalIGmcnt, 8, 1);
            output.Write(" GC livesets  per method\n");
            output.Write("\n");

            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalIGicnt / emitTotalIGcnt, 8, 1);
            output.Write(" instructions per group \n");
            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalIGsize / emitTotalIGcnt, 8, 1);
            output.Write(" desc.  bytes per group \n");
            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalIGjmps / emitTotalIGcnt, 8, 1);
            output.Write(" jumps        per group \n");
            output.Write("\n");

            output.Write("Average of ");
            WriteEmitterStatistic(output, (double)emitTotalIGsize / emitTotalIGicnt, 8, 1);
            output.Write(" bytes        per instrDesc\n");
            output.Write("\n");
            output.Write(FormattableString.Invariant($"A total of {emitTotalIGsize,8} desc.  bytes\n"));
            output.Write("\n");

            output.Write(FormattableString.Invariant($"Total instructions:           {emitTotalInsCnt,8}\n"));
            WriteEmitterDescriptorCount(output, "Total small instrDesc:", emitTotalIDescSmallCnt);
            WriteEmitterDescriptorCount(output, "Total instrDesc:", emitTotalIDescCnt);
            WriteEmitterDescriptorCount(output, "Total instrDescCns:", emitTotalIDescCnsCnt);
            WriteEmitterDescriptorCount(output, "Total instrDescDsp:", emitTotalIDescDspCnt);
            WriteEmitterDescriptorCount(output, "Total instrDescAmd:", emitTotalIDescAmdCnt);
            WriteEmitterDescriptorCount(output, "Total instrDescCnsAmd:", emitTotalIDescCnsAmdCnt);
            WriteEmitterDescriptorCount(output, "Total instrDescCnsDsp:", emitTotalIDescCnsDspCnt);
#if FEATURE_LOOP_ALIGN
            WriteEmitterDescriptorCount(output, "Total instrDescAlign:", emitTotalIDescAlignCnt);
#endif
            WriteEmitterDescriptorCount(output, "Total instrDescJmp:", emitTotalIDescJmpCnt);
            WriteEmitterDescriptorCount(output, "Total instrDescLbl:", emitTotalIDescLblCnt);
            WriteEmitterDescriptorCount(output, "Total instrDescCGCA:", emitTotalIDescCGCACnt);

            output.Write("\n");
        }

        output.Write("Descriptor size distribution:\n");
        emitSizeTable.dump(output);
        output.Write("\n");

        output.Write("GC ref frame variable counts:\n");
        GCrefsTable.dump(output);
        output.Write("\n");

        output.Write("Max. stack depth distribution:\n");
        stkDepthTable.dump(output);
        output.Write("\n");

        var totalConstantCount = unchecked(emitLargeCnsCnt + emitSmallCnsCnt);

        if (totalConstantCount != 0)
        {
            WriteEmitterConstantCount(output, "SmallCnsCnt", emitSmallCnsCnt, totalConstantCount);
            WriteEmitterConstantCount(output, "LargeCnsCnt", emitLargeCnsCnt, totalConstantCount);
            WriteEmitterConstantCount(output, "Int8CnsCnt ", emitInt8CnsCnt, totalConstantCount);
            WriteEmitterConstantCount(output, "Int16CnsCnt", emitInt16CnsCnt, totalConstantCount);
            WriteEmitterConstantCount(output, "Int32CnsCnt", emitInt32CnsCnt, totalConstantCount);
            WriteEmitterConstantCount(output, "NegCnsCnt  ", emitNegCnsCnt, totalConstantCount);
            WriteEmitterConstantCount(output, "Pow2CnsCnt ", emitPow2CnsCnt, totalConstantCount);
        }

        if (emitSmallCnsCnt != 0)
        {
            output.Write("\n\n");
            output.Write(FormattableString.Invariant($"Common small constants >= {ID_MIN_SMALL_CNS,2}, <= {ID_MAX_SMALL_CNS,2}\n"));

            var minimumCount = unchecked((emitSmallCnsCnt / 1000) + 1);

            for (var index = 0; index < SMALL_CNS_TSZ; index++)
            {
                var count = emitSmallCns[index];

                if (count >= minimumCount)
                {
                    assert((ID_MIN_SMALL_CNS < 0) && (ID_MAX_SMALL_CNS > 0));

                    var value = index - (SMALL_CNS_TSZ / 2);

                    if (index == 0)
                    {
                        output.Write(FormattableString.Invariant($"cns[<={value,4}] = {count,8} ("));
                    }
                    else if (index == (SMALL_CNS_TSZ - 1))
                    {
                        output.Write(FormattableString.Invariant($"cns[>={value,4}] = {count,8} ("));
                    }
                    else
                    {
                        output.Write(FormattableString.Invariant($"cns[  {value,4}] = {count,8} ("));
                    }

                    WriteEmitterStatistic(output, (100.0 * count) / totalConstantCount, 5, 2);
                    output.Write("%)\n");
                }
            }
        }

        output.Write(FormattableString.Invariant($"{emitTotMemAlloc,8} bytes allocated in the emitter\n"));
#endif
    }

#if TARGET_XARCH
    private static void WriteEmitterDescriptorCount(StreamWriter output, string name, uint count)
    {
        output.Write(FormattableString.Invariant($"{name,-30}{count,8} ("));
        WriteEmitterStatistic(output, (100.0 * count) / emitTotalInsCnt, 5, 2);
        output.Write("%)\n");
    }
#endif

    private static void WriteEmitterConstantCount(StreamWriter output, string name, uint count, uint total)
    {
        output.Write(FormattableString.Invariant($"{name} = {count,8} ("));
        WriteEmitterStatistic(output, (100.0 * count) / total, 5, 2);
        output.Write("%)\n");
    }

    private static void WriteEmitterStatistic(StreamWriter output, double value, int width, int precision)
    {
        if (double.IsNaN(value))
        {
            output.Write("nan".PadLeft(width));
        }
        else if (double.IsPositiveInfinity(value))
        {
            output.Write("inf".PadLeft(width));
        }
        else if (double.IsNegativeInfinity(value))
        {
            output.Write("-inf".PadLeft(width));
        }
        else
        {
            var format = precision switch
            {
                1 => "F1",
                2 => "F2",
                3 => "F3",
                _ => throw new ArgumentOutOfRangeException(nameof(precision)),
            };

            output.Write(value.ToString(format, CultureInfo.InvariantCulture).PadLeft(width));
        }
    }
#endif
}
