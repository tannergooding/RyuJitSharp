// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    public static unsafe void compShutdown()
    {
        s_pAltJitExcludeAssembliesList = null;
#if DEBUG
        s_pJitDisasmIncludeAssembliesList = null;
#endif

#if MEASURE_NOWAY
        DisplayNowayAssertMap();
#endif

        Emitter.emitDone();

#if DEBUG
        if (JitConfig.JitInlineDumpXmlFile is not null)
        {
            var path = Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(JitConfig.JitInlineDumpXmlFile));
            using var file = OpenJitOutputFile(path);
            if (file is not null)
            {
                using var writer = new JitTextWriter(file, leaveOpen: true);
                InlineStrategy.FinalizeXml(writer);
            }
            else
            {
                InlineStrategy.FinalizeXml();
            }
        }
#endif

#if DEBUG || MEASURE_NODE_SIZE || MEASURE_BLOCK_SIZE || DISPLAY_SIZES || CALL_ARG_STATS
        if (genMethodCnt == 0)
        {
            return;
        }
#endif

#if NODEBASH_STATS
        GenTree.ReportOperBashing(jitstdout());
#endif

#if FEATURE_JIT_METHOD_PERF
        if (compJitTimeLogFilename != 0)
        {
            var path = Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)compJitTimeLogFilename));
            using var file = OpenJitOutputFile(path);
            if (file is not null)
            {
                using var writer = new JitTextWriter(file, leaveOpen: true);
                CompTimeSummaryInfo.s_compTimeSummary.Print(writer);
            }
        }

        JitTimer.Shutdown();
#endif

#if COUNT_AST_OPERS
        unchecked
        {
            uint totalCount = 0;
            for (uint op = 0; op < (uint)GT_COUNT; op++)
            {
                totalCount += GenTree.GetNodeCount(op);
            }

            if (totalCount > 0)
            {
                Span<OperInfo> opers = stackalloc OperInfo[(int)GT_COUNT];
                for (uint op = 0; op < (uint)GT_COUNT; op++)
                {
                    opers[(int)op] = new OperInfo(GenTree.GetNodeCount(op), GenTree.GetTrueSize(op), (genTreeOps)op);
                }

                SortNative(opers, new OperInfoLess());

                var remainingCount = totalCount;
                uint remainingCountLarge = 0;
                uint remainingCountSmall = 0;
                uint countLarge = 0;
                uint countSmall = 0;

                jitprintf("\nGenTree operator counts (approximate):\n\n");

                foreach (var oper in opers)
                {
                    var size = oper.Size;
                    var count = oper.Count;
                    var percentage = 100.0 * count / totalCount;

                    if (size > TREE_NODE_SZ_SMALL)
                    {
                        countLarge += count;
                    }
                    else
                    {
                        countSmall += count;
                    }

                    if (percentage >= 0.5)
                    {
                        jitprintf($"    GT_{oper.Oper.Name,-17}   {count,7} ({formatFloat(percentage, "F1"),4}%) {size,3} bytes each\n");
                        remainingCount -= count;
                    }
                    else
                    {
                        if (size > TREE_NODE_SZ_SMALL)
                        {
                            remainingCountLarge += count;
                        }
                        else
                        {
                            remainingCountSmall += count;
                        }
                    }
                }

                if (remainingCount > 0)
                {
                    jitprintf($"    All other GT_xxx ...   {remainingCount,7} ({formatFloat(100.0 * remainingCount / totalCount, "F1"),4}%) ... " +
                        $"{formatFloat(100.0 * remainingCountSmall / totalCount, "F1"),4}% small + " +
                        $"{formatFloat(100.0 * remainingCountLarge / totalCount, "F1"),4}% large\n");
                }

                jitprintf("    -----------------------------------------------------\n");
                jitprintf($"    Total    .......   {totalCount,11} --ALL-- ... {formatFloat(100.0 * countSmall / totalCount, "F1"),4}% small + " +
                    $"{formatFloat(100.0 * countLarge / totalCount, "F1"),4}% large\n");
                jitprintf("\n");
            }
        }
#endif

#if DISPLAY_SIZES
        unchecked
        {
            if ((grossVMsize != 0) && (grossNCsize != 0))
            {
                jitprintf("\n");
                jitprintf("--------------------------------------\n");
                jitprintf("Function and GC info size stats\n");
                jitprintf("--------------------------------------\n");

                // Native accumulates size_t values but prints them through unsigned32 %u fields.
                jitprintf($"[{(uint)grossVMsize,7} VM, {(uint)grossNCsize,8} {Target.TgtCpuName,6} " +
                    $"{(uint)(100 * grossNCsize / grossVMsize),4}%] Total (excluding GC info)\n");
                jitprintf($"[{(uint)grossVMsize,7} VM, {(uint)totalNCsize,8} {Target.TgtCpuName,6} " +
                    $"{(uint)(100 * totalNCsize / grossVMsize),4}%] Total (including GC info)\n");

                if ((gcHeaderISize != 0) || (gcHeaderNSize != 0))
                {
                    jitprintf("\n");
                    jitprintf($"GC tables   : [{(uint)(gcHeaderISize + gcPtrMapISize),7}I,{(uint)(gcHeaderNSize + gcPtrMapNSize),7}N] " +
                        $"{(uint)(totalNCsize - grossNCsize),7} byt  ({(uint)(100 * (totalNCsize - grossNCsize) / grossVMsize)}% of IL, " +
                        $"{(uint)(100 * (totalNCsize - grossNCsize) / grossNCsize)}% of {Target.TgtCpuName}).\n");
                    jitprintf($"GC headers  : [{(uint)gcHeaderISize,7}I,{(uint)gcHeaderNSize,7}N] {(uint)(gcHeaderISize + gcHeaderNSize),7} byt, " +
                        $"[{formatFloat((float)gcHeaderISize / (genMethodICnt + 0.001), "F1"),4}I," +
                        $"{formatFloat((float)gcHeaderNSize / (genMethodNCnt + 0.001), "F1"),4}N] " +
                        $"{formatFloat((float)(gcHeaderISize + gcHeaderNSize) / genMethodCnt, "F1"),4} byt/meth\n");
                    jitprintf($"GC ptr maps : [{(uint)gcPtrMapISize,7}I,{(uint)gcPtrMapNSize,7}N] {(uint)(gcPtrMapISize + gcPtrMapNSize),7} byt, " +
                        $"[{formatFloat((float)gcPtrMapISize / (genMethodICnt + 0.001), "F1"),4}I," +
                        $"{formatFloat((float)gcPtrMapNSize / (genMethodNCnt + 0.001), "F1"),4}N] " +
                        $"{formatFloat((float)(gcPtrMapISize + gcPtrMapNSize) / genMethodCnt, "F1"),4} byt/meth\n");
                }
                else
                {
                    jitprintf("\n");
                    jitprintf($"GC tables   take up {(uint)(totalNCsize - grossNCsize)} bytes " +
                        $"({(uint)(100 * (totalNCsize - grossNCsize) / grossVMsize)}% of instr, " +
                        $"{(uint)(100 * (totalNCsize - grossNCsize) / grossNCsize)}% of {Target.TgtCpuName,6} code).\n");
                }

#if DEBUG && DOUBLE_ALIGN
                jitprintf($"{(uint)s_lvaDoubleAlignedProcsCount} out of {genMethodCnt} methods generated with double-aligned stack\n");
#endif
            }
        }
#endif

#if CALL_ARG_STATS
        compDispCallArgStats(jitstdout());
#endif

#if COUNT_BASIC_BLOCKS
        jitprintf("--------------------------------------------------\n");
        jitprintf("Basic block count frequency table:\n");
        jitprintf("--------------------------------------------------\n");
        bbCntTable.dump(jitstdout());
        jitprintf("--------------------------------------------------\n");

        jitprintf("\n");

        jitprintf("--------------------------------------------------\n");
        jitprintf("IL method size frequency table for methods with a single basic block:\n");
        jitprintf("--------------------------------------------------\n");
        bbOneBBSizeTable.dump(jitstdout());
        jitprintf("--------------------------------------------------\n");

        jitprintf("--------------------------------------------------\n");
        jitprintf("fgComputeReachabilitySets `while (change)` iterations:\n");
        jitprintf("--------------------------------------------------\n");
        computeReachabilitySetsIterationTable.dump(jitstdout());
        jitprintf("--------------------------------------------------\n");
#endif

#if MEASURE_NODE_SIZE
        unchecked
        {
            jitprintf("\n");
            jitprintf("---------------------------------------------------\n");
            jitprintf("GenTree node allocation stats\n");
            jitprintf("---------------------------------------------------\n");

            jitprintf($"Allocated {genNodeSizeStats.genTreeNodeCnt,6} tree nodes ({genNodeSizeStats.genTreeNodeSize,7} bytes total, " +
                $"avg {genNodeSizeStats.genTreeNodeSize / genMethodCnt,4} bytes per method)\n");
            jitprintf($"Allocated {genNodeSizeStats.genTreeNodeSize - genNodeSizeStats.genTreeNodeActualSize,7} bytes of unused tree node space " +
                $"({formatFloat((float)(100 * (genNodeSizeStats.genTreeNodeSize - genNodeSizeStats.genTreeNodeActualSize)) / genNodeSizeStats.genTreeNodeSize, "F2"),3}%)\n");

            jitprintf("\n");
            jitprintf("---------------------------------------------------\n");
            jitprintf("Distribution of per-method GenTree node counts:\n");
            genTreeNcntHist.dump(jitstdout());

            jitprintf("\n");
            jitprintf("---------------------------------------------------\n");
            jitprintf("Distribution of per-method GenTree node  allocations (in bytes):\n");
            genTreeNsizHist.dump(jitstdout());
        }
#endif

#if MEASURE_BLOCK_SIZE
        unchecked
        {
            jitprintf("\n");
            jitprintf("---------------------------------------------------\n");
            jitprintf("BasicBlock and FlowEdge/BasicBlockList allocation stats\n");
            jitprintf("---------------------------------------------------\n");

            // Existing signed storage represents native size_t bits, not signed division.
            jitprintf($"Allocated {(uint)BasicBlock.s_Count,6} basic blocks ({(uint)BasicBlock.s_Size,7} bytes total, " +
                $"avg {(uint)((nuint)BasicBlock.s_Size / genMethodCnt),4} bytes per method)\n");
            jitprintf($"Allocated {(uint)genFlowNodeCnt,6} flow nodes ({(uint)genFlowNodeSize,7} bytes total, " +
                $"avg {(uint)(genFlowNodeSize / genMethodCnt),4} bytes per method)\n");
        }
#endif

#if MEASURE_MEM_ALLOC
        if (s_dspMemStats)
        {
            jitprintf("\nAll allocations:\n");
            JitMemStatsInfo.dumpAggregateMemStats(jitstdout());

            jitprintf("\nLargest method:\n");
            JitMemStatsInfo.dumpMaxMemStats(jitstdout());

            jitprintf("\n");
            jitprintf("---------------------------------------------------\n");
            jitprintf("Distribution of total memory allocated per method (in KB):\n");
            memAllocHist.dump(jitstdout());

            jitprintf("\n");
            jitprintf("---------------------------------------------------\n");
            jitprintf("Distribution of total memory used      per method (in KB):\n");
            memUsedHist.dump(jitstdout());
        }
#endif

#if LOOP_HOIST_STATS
#if DEBUG
        if (JitConfig.DisplayLoopHoistStats != 0)
#endif
        {
            PrintAggregateLoopHoistStats(jitstdout());
        }
#endif

#if TRACK_ENREG_STATS
        if (JitConfig.JitEnregStats != 0)
        {
            s_enregisterStats.Dump(jitstdout());
        }
#endif

#if MEASURE_PTRTAB_SIZE
        unchecked
        {
            jitprintf("\n");
            jitprintf("---------------------------------------------------\n");
            jitprintf("GC pointer table stats\n");
            jitprintf("---------------------------------------------------\n");

            jitprintf($"Reg pointer descriptor size (internal): {(uint)GCInfo.s_gcRegPtrDscSize,8} " +
                $"(avg {(uint)(GCInfo.s_gcRegPtrDscSize / genMethodCnt),4} per method)\n");
            jitprintf($"Total pointer table size: {(uint)GCInfo.s_gcTotalPtrTabSize,8} " +
                $"(avg {(uint)(GCInfo.s_gcTotalPtrTabSize / genMethodCnt),4} per method)\n");
        }
#endif

#if MEASURE_NODE_SIZE || MEASURE_BLOCK_SIZE || MEASURE_PTRTAB_SIZE || DISPLAY_SIZES
        if (genMethodCnt != 0)
        {
            jitprintf("\n");
            jitprintf($"A total of {genMethodCnt,6} methods compiled");
#if DISPLAY_SIZES
            if ((genMethodICnt != 0) || (genMethodNCnt != 0))
            {
                jitprintf($" ({genMethodICnt} interruptible, {genMethodNCnt} non-interruptible)");
            }
#endif
            jitprintf(".\n");
        }
#endif

#if EMITTER_STATS
        emitterStats(jitstdout());
#endif

#if MEASURE_FATAL
        jitprintf("\n");
        jitprintf("---------------------------------------------------\n");
        jitprintf("Fatal errors stats\n");
        jitprintf("---------------------------------------------------\n");
        jitprintf($"   badCode:             {s_fatalBadCodeCount}\n");
        jitprintf($"   noWay:               {s_fatalNoWayCount}\n");
        jitprintf($"   implLimitation:      {s_fatalImplLimitationCount}\n");
        jitprintf($"   NOMEM:               {s_fatalNoMemCount}\n");
        jitprintf($"   noWayAssertBody:     {s_fatalNoWayAssertBodyCount}\n");
#if DEBUG
        jitprintf($"   noWayAssertBodyArgs: {s_fatalNoWayAssertBodyArgsCount}\n");
#endif
        jitprintf($"   NYI:                 {s_fatalNyiCount}\n");
#endif

#if CALL_ARG_STATS || COUNT_BASIC_BLOCKS || EMITTER_STATS || MEASURE_NODE_SIZE || MEASURE_MEM_ALLOC
        DumpOnShutdown.DumpAll();
#endif
    }

    internal static FileStream? OpenJitOutputFile(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
