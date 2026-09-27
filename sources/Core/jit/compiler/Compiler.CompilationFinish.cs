// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG || MEASURE_NODE_SIZE || MEASURE_BLOCK_SIZE || DISPLAY_SIZES || CALL_ARG_STATS
    internal static uint genMethodCnt;
#endif

#if DEBUG
    private static bool s_compOrderHeaderPrinted;
#endif

    public unsafe void compCompileFinish()
    {
#if DEBUG || MEASURE_NODE_SIZE || MEASURE_BLOCK_SIZE || DISPLAY_SIZES || CALL_ARG_STATS
        genMethodCnt = unchecked(genMethodCnt + 1);
#endif

        if (JitConfig.JitReportMetrics != 0)
        {
            // D001: measure this compilation's managed allocations, not native arena pages.
            Metrics.BytesAllocated = ManagedBytesAllocated;
        }

#if LOOP_HOIST_STATS
        AddLoopHoistStats();
#endif

#if DEBUG
        assert(_inlineStrategy is not null);
        _inlineStrategy.DumpData();
        if (JitConfig.JitInlineDumpXmlFile is not null)
        {
            var path = Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(JitConfig.JitInlineDumpXmlFile));
            using var file = OpenJitOutputFile(path);
            if (file is not null)
            {
                using var writer = new JitTextWriter(file, leaveOpen: true);
                _inlineStrategy.DumpXml(writer);
            }
            else
            {
                _inlineStrategy.DumpXml();
            }
        }
        else
        {
            _inlineStrategy.DumpXml();
        }

        if (opts.dspOrder)
        {
            compDumpOrderSummary();
        }

        JITDUMP("Final metrics:\n");
        if (JitConfig.JitReportMetrics != 0)
        {
            Metrics.report(this);
        }
        if (verbose)
        {
            Metrics.dump();
            jitprintf($"\n****** DONE compiling {info.compFullName}\n");
            jitstdout().Flush();
        }

#if TRACK_ENREG_STATS
        for (var i = 0; i < lvaCount; i++)
        {
            ref readonly var descriptor = ref lvaGetDesc(i);
            if (descriptor.lvRefCnt() != 0)
            {
                s_enregisterStats.RecordLocal(in descriptor);
            }
        }
#endif
#else
        if (JitConfig.JitReportMetrics != 0)
        {
            Metrics.report(this);
        }
#endif
    }

#if DEBUG
    private unsafe void compDumpOrderSummary()
    {
        var token = info.compCompHnd->getMethodDefFromMethod(info.compMethodHnd);
        if (!s_compOrderHeaderPrinted)
        {
            s_compOrderHeaderPrinted = true;
            jitprintf($"         |  Profiled   | Method   |   Method has    |   calls   | Num |LclV |AProp| CSE |   Perf  |bytes | {Target.TgtCpuName,3} codesize| \n");
            jitprintf(" mdToken |  CNT |  RGN |    Hash  | EH | FRM | LOOP | NRM | IND | BBs | Cnt | Cnt | Cnt |  Score  |  IL  |   HOT | CLD | method name \n");
            jitprintf("---------+------+------+----------+----+-----+------+-----+-----+-----+-----+-----+-----+---------+------+-------+-----+\n");
        }

        jitprintf($"{token:X8} | ");
        if (fgHaveProfileWeights)
        {
            if (fgCalledCount < 1000)
            {
                jitprintf($"{formatFloat(fgCalledCount, "F0"),4} | ");
            }
            else if (fgCalledCount < 1000000)
            {
                jitprintf($"{formatFloat(fgCalledCount / 1000, "F0"),3}K | ");
            }
            else
            {
                jitprintf($"{formatFloat(fgCalledCount / 1000000, "F0"),3}M | ");
            }
        }
        else
        {
            jitprintf("     | ");
        }

        if (opts.altJit)
        {
            jitprintf("ALT | ");
        }
        else
        {
            jitprintf(info.compMethodInfo->regionKind switch {
                CORINFO_REGION_NONE => "     | ",
                CORINFO_REGION_HOT => " HOT | ",
                CORINFO_REGION_COLD => "COLD | ",
                CORINFO_REGION_JIT => " JIT | ",
                _ => "UNKN | ",
            });
        }
        jitprintf($"{unchecked((uint)info.compMethodHash()):x8} | ");
        jitprintf(compHndBBtabCount > 0 ? "EH | " : "   | ");
        if (rpFrameType == FT_EBP_FRAME)
        {
            jitprintf($"{STR_FPBASE,3} | ");
        }
        else if (rpFrameType == FT_ESP_FRAME)
        {
            jitprintf($"{STR_SPBASE,3} | ");
        }
#if DOUBLE_ALIGN
        else if (rpFrameType == FT_DOUBLE_ALIGN_FRAME)
        {
            jitprintf("dbl | ");
        }
#endif
        else
        {
            jitprintf("??? | ");
        }

        jitprintf(fgHasLoops ? "LOOP |" : "     |");
        jitprintf($" {optCallCount,3} |");
        jitprintf($" {optIndirectCallCount,3} |");
        jitprintf($" {Metrics.BasicBlocksAtCodegen,3} |");
        jitprintf($" {lvaCount,3} |");
        if (opts.MinOpts)
        {
            jitprintf("  MinOpts  |");
        }
        else
        {
            jitprintf($" {optAssertionCount,3} |");
            jitprintf($" {optCSEcount,3} |");
        }

        jitprintf($" {formatFloat(Metrics.PerfScore, Metrics.PerfScore < 9999.995 ? "F2" : "F0"),7} |");
        jitprintf($" {unchecked((int)info.compMethodInfo->ILCodeSize),4} |");
        jitprintf($" {unchecked((int)info.compTotalHotCodeSize),5} |");
        jitprintf($" {unchecked((int)info.compTotalColdCodeSize),3} |");
        jitprintf($" {eeGetMethodFullName(info.compMethodHnd)}\n");
        jitstdout().Flush();
    }
#endif

#if LOOP_HOIST_STATS
    internal void AddLoopHoistStats()
    {
        lock (s_loopHoistStatsLock)
        {
            s_loopsConsidered = unchecked(s_loopsConsidered + _loopsConsidered);
            s_loopsWithHoistedExpressions = unchecked(s_loopsWithHoistedExpressions + _loopsWithHoistedExpressions);
            s_totalHoistedExpressions = unchecked(s_totalHoistedExpressions + _totalHoistedExpressions);
        }
    }

    internal static void PrintAggregateLoopHoistStats(TextWriter writer)
    {
        writer.Write("\n---------------------------------------------------\n");
        writer.Write("Loop hoisting stats\n");
        writer.Write("---------------------------------------------------\n");
        var considered = unchecked((uint)s_loopsConsidered);
        var hoisted = unchecked((uint)s_loopsWithHoistedExpressions);
        var expressions = unchecked((uint)s_totalHoistedExpressions);
        var percent = considered > 0 ? 100.0 * (hoisted / (double)considered) : 0;
        var average = hoisted > 0 ? expressions / (double)hoisted : 0;
        writer.Write($"Considered {s_loopsConsidered} loops.  Of these, we hoisted expressions out of " +
            $"{s_loopsWithHoistedExpressions} ({formatFloat(percent, "F2"),6}%).\n");
        writer.Write($"  A total of {s_totalHoistedExpressions} expressions were hoisted, an average of " +
            $"{formatFloat(average, "F2"),5} per loop-with-hoisted-expr.\n");
    }
#endif
}
