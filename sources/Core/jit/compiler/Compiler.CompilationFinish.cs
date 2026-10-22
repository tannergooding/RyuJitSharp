// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    internal static uint genMethodCnt;
    internal static uint genMethodICnt;
    internal static uint genMethodNCnt;

    private static void IncrementMethodCount()
    {
        genMethodCnt = unchecked(genMethodCnt + 1);
    }

    private static void InitializeNodeSizeStatistics()
    {
        Debug.Assert(Configuration.MEASURE_NODE_SIZE);
        genNodeSizeStatsPerFunc.Init();
    }

    private static bool s_compOrderHeaderPrinted;

    public unsafe void compCompileFinish()
    {
        if (Configuration.DEBUG ||
            Configuration.MEASURE_NODE_SIZE ||
            Configuration.MEASURE_BLOCK_SIZE ||
            Configuration.DISPLAY_SIZES ||
            Configuration.CALL_ARG_STATS)
        {
            IncrementMethodCount();
        }

        if (Configuration.MEASURE_MEM_ALLOC)
        {
            var managedBytesAllocated = ManagedBytesAllocated;
            JitMemStatsInfo.finishMemStats(managedBytesAllocated);

            var managedBytes = unchecked((ulong)managedBytesAllocated);
            memAllocHist.record(unchecked((uint)((managedBytes + 1023) / 1024)));

            if (Configuration.DEBUG && (ManagedAllocationStatsData.s_dspMemStats || verbose))
            {
                jitprintf($"\nAllocations for {eeGetMethodFullName(info.compMethodHnd)} " +
                    $"(MethodHash={unchecked((uint)info.compMethodHash()):x8})\n");
                JitMemStatsInfo.dumpMethodMemStats(jitstdout(), managedBytesAllocated);
            }
        }

        if (JitConfig.JitReportMetrics != 0)
        {
            // D001: measure this compilation's managed allocations, not native arena pages.
            Metrics.BytesAllocated = ManagedBytesAllocated;
        }

        AddLoopHoistStats();

        if (Configuration.DEBUG)
        {
            assert(_inlineStrategy is not null);
        }
        if (Configuration.DEBUG)
        {
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

            for (var i = 0; i < lvaCount; i++)
            {
                ref readonly var descriptor = ref lvaGetDesc(i);
                if (descriptor.lvRefCnt() != 0)
                {
                    DebugData.EnregisterStatsData.s_enregisterStats.RecordLocal(
                        in descriptor,
                        GetLocalDoNotEnregisterReason(i),
                        GetLocalAddressExposedReason(i));
                }
            }
        }
        else if (JitConfig.JitReportMetrics != 0)
        {
            Metrics.report(this);
        }
    }

    [Conditional("DEBUG")]
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
            jitprintf($"{TargetRuntimeMetadata.FramePointerBaseName,3} | ");
        }
        else if (rpFrameType == FT_ESP_FRAME)
        {
            jitprintf($"{TargetRuntimeMetadata.StackPointerBaseName,3} | ");
        }
        else if (Configuration.DOUBLE_ALIGN && (rpFrameType == FT_DOUBLE_ALIGN_FRAME))
        {
            jitprintf("dbl | ");
        }
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

    [Conditional("DEBUG")]
    internal void AddLoopHoistStats()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LoopHoistStatisticsAreOnlyAvailableInDebugBuilds);
        }

        var data = GetDebugData();

        global::System.Threading.Lock __jitExceptionResult124;
        var __jitExceptionCoalesceLeft125 = DebugData.LoopHoistStatsData.s_loopHoistStatsLock;
        if (__jitExceptionCoalesceLeft125 != null)
        {
            __jitExceptionResult124 = __jitExceptionCoalesceLeft125;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LoopHoistStatisticsLockIsUnavailable);
            __jitExceptionResult124 = default;
        }
        var gate = __jitExceptionResult124;

        lock (gate)
        {
            DebugData.LoopHoistStatsData.s_loopsConsidered =
                unchecked(DebugData.LoopHoistStatsData.s_loopsConsidered + data.LoopsConsidered);
            DebugData.LoopHoistStatsData.s_loopsWithHoistedExpressions =
                unchecked(DebugData.LoopHoistStatsData.s_loopsWithHoistedExpressions + data.LoopsWithHoistedExpressions);
            DebugData.LoopHoistStatsData.s_totalHoistedExpressions =
                unchecked(DebugData.LoopHoistStatsData.s_totalHoistedExpressions + data.TotalHoistedExpressions);
        }
    }

    [Conditional("DEBUG")]
    internal static void PrintAggregateLoopHoistStats(TextWriter writer)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LoopHoistStatisticsAreOnlyAvailableInDebugBuilds);
        }

        writer.Write("\n---------------------------------------------------\n");
        writer.Write("Loop hoisting stats\n");
        writer.Write("---------------------------------------------------\n");
        var considered = unchecked((uint)DebugData.LoopHoistStatsData.s_loopsConsidered);
        var hoisted = unchecked((uint)DebugData.LoopHoistStatsData.s_loopsWithHoistedExpressions);
        var expressions = unchecked((uint)DebugData.LoopHoistStatsData.s_totalHoistedExpressions);
        var percent = considered > 0 ? 100.0 * (hoisted / (double)considered) : 0;
        var average = hoisted > 0 ? expressions / (double)hoisted : 0;
        writer.Write($"Considered {DebugData.LoopHoistStatsData.s_loopsConsidered} loops.  Of these, we hoisted expressions out of " +
            $"{DebugData.LoopHoistStatsData.s_loopsWithHoistedExpressions} ({formatFloat(percent, "F2"),6}%).\n");
        writer.Write($"  A total of {DebugData.LoopHoistStatsData.s_totalHoistedExpressions} expressions were hoisted, an average of " +
            $"{formatFloat(average, "F2"),5} per loop-with-hoisted-expr.\n");
    }
}
