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

#if DISPLAY_SIZES
        if (genMethodCnt != 0)
        {
            jitprintf("\n");
            jitprintf($"A total of {genMethodCnt,6} methods compiled");
            if ((genMethodICnt != 0) || (genMethodNCnt != 0))
            {
                jitprintf($" ({genMethodICnt} interruptible, {genMethodNCnt} non-interruptible)");
            }
            jitprintf(".\n");
        }
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
