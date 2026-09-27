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
