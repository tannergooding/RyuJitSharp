// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Globalization;
using System.IO;
using CallArgumentStatisticsData = RyuJitSharp.Compiler.MeasurementData.CallArgumentStatistics;

namespace RyuJitSharp;

public partial class Compiler
{
    internal static partial class MeasurementData
    {
        internal static class CallArgumentStatistics
        {
            internal static uint argTotalCalls;
            internal static uint argHelperCalls;
            internal static uint argStaticCalls;
            internal static uint argNonVirtualCalls;
            internal static uint argVirtualCalls;

            internal static uint argTotalArgs;
            internal static uint argTotalDWordArgs;
            internal static uint argTotalLongArgs;
            internal static uint argTotalFloatArgs;
            internal static uint argTotalDoubleArgs;

            internal static uint argTotalRegArgs;
            internal static uint argTotalTemps;
            internal static uint argTotalLclVar;
            internal static uint argTotalDeferred;
            internal static uint argTotalConst;
            internal static uint argTotalObjPtr;

            internal static uint argMaxTempsPerMethod;

            private static readonly uint[]? argCntBuckets =
                Configuration.CALL_ARG_STATS ? [0, 1, 2, 3, 4, 5, 6, 10, 0] : null;
            private static readonly uint[]? argDWordCntBuckets =
                Configuration.CALL_ARG_STATS ? [0, 1, 2, 3, 4, 5, 6, 10, 0] : null;
            private static readonly uint[]? argDWordLngCntBuckets =
                Configuration.CALL_ARG_STATS ? [0, 1, 2, 3, 4, 5, 6, 10, 0] : null;
            private static readonly uint[]? argTempsCntBuckets =
                Configuration.CALL_ARG_STATS ? [0, 1, 2, 3, 4, 5, 6, 10, 0] : null;

            internal static readonly Histogram argCntTable =
                Configuration.CALL_ARG_STATS ? new(argCntBuckets!) : null!;
            internal static readonly Histogram argDWordCntTable =
                Configuration.CALL_ARG_STATS ? new(argDWordCntBuckets!) : null!;
            internal static readonly Histogram argDWordLngCntTable =
                Configuration.CALL_ARG_STATS ? new(argDWordLngCntBuckets!) : null!;
            internal static readonly Histogram argTempsCntTable =
                Configuration.CALL_ARG_STATS ? new(argTempsCntBuckets!) : null!;
        }
    }

    public void compCallArgStats()
    {
        if (!Configuration.CALL_ARG_STATS)
        {
            return;
        }

        uint argTempsThisMethod = 0;

        assert(fgNodeThreading is NodeThreading.LIR);

        foreach (var block in Blocks)
        {
            // Compilation finish has already converted statement trees to LIR.
            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                if (!node.OperIs(genTreeOps.GT_CALL))
                {
                    continue;
                }

                var call = node.AsCall();
                CallArgumentStatisticsData.argTotalCalls =
                    unchecked(CallArgumentStatisticsData.argTotalCalls + 1);

                if (call.Args.ThisArg is null)
                {
                    if (call.IsHelperCall())
                    {
                        CallArgumentStatisticsData.argHelperCalls =
                            unchecked(CallArgumentStatisticsData.argHelperCalls + 1);
                    }
                    else
                    {
                        CallArgumentStatisticsData.argStaticCalls =
                            unchecked(CallArgumentStatisticsData.argStaticCalls + 1);
                    }
                }
                else
                {
                    CallArgumentStatisticsData.argTotalObjPtr =
                        unchecked(CallArgumentStatisticsData.argTotalObjPtr + 1);

                    if (call.IsVirtual)
                    {
                        CallArgumentStatisticsData.argVirtualCalls =
                            unchecked(CallArgumentStatisticsData.argVirtualCalls + 1);
                    }
                    else
                    {
                        CallArgumentStatisticsData.argNonVirtualCalls =
                            unchecked(CallArgumentStatisticsData.argNonVirtualCalls + 1);
                    }
                }
            }
        }

        CallArgumentStatisticsData.argTempsCntTable.record(argTempsThisMethod);

        if (CallArgumentStatisticsData.argMaxTempsPerMethod < argTempsThisMethod)
        {
            CallArgumentStatisticsData.argMaxTempsPerMethod = argTempsThisMethod;
        }
    }

    internal static void compDispCallArgStats(StreamWriter output)
    {
        if (!Configuration.CALL_ARG_STATS)
        {
            return;
        }

        if (CallArgumentStatisticsData.argTotalCalls is 0)
        {
            return;
        }

        output.Write("\n");
        output.Write("--------------------------------------------------\n");
        output.Write("Call stats\n");
        output.Write("--------------------------------------------------\n");

        var callsPerMethod =
            (float)CallArgumentStatisticsData.argTotalCalls / MeasurementData.MethodCounts.genMethodCnt;
        output.Write(System.FormattableString.Invariant(
            $"Total # of calls = {FormatCallStatCount(CallArgumentStatisticsData.argTotalCalls)}, calls / method = {FormatCallStat(callsPerMethod, "F3")}\n\n"));

        output.Write(System.FormattableString.Invariant(
            $"Percentage of      helper calls = {FormatCallStatPercentage(CallArgumentStatisticsData.argHelperCalls, CallArgumentStatisticsData.argTotalCalls)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of      static calls = {FormatCallStatPercentage(CallArgumentStatisticsData.argStaticCalls, CallArgumentStatisticsData.argTotalCalls)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of     virtual calls = {FormatCallStatPercentage(CallArgumentStatisticsData.argVirtualCalls, CallArgumentStatisticsData.argTotalCalls)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of non-virtual calls = {FormatCallStatPercentage(CallArgumentStatisticsData.argNonVirtualCalls, CallArgumentStatisticsData.argTotalCalls)} %\n\n"));

        var averageArgsPerCall = (float)CallArgumentStatisticsData.argTotalArgs / CallArgumentStatisticsData.argTotalCalls;
        output.Write(System.FormattableString.Invariant(
            $"Average # of arguments per call = {FormatCallStat(averageArgsPerCall, "F2")}%\n\n"));

        output.Write(System.FormattableString.Invariant(
            $"Percentage of DWORD  arguments   = {FormatCallStatPercentage(CallArgumentStatisticsData.argTotalDWordArgs, CallArgumentStatisticsData.argTotalArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of LONG   arguments   = {FormatCallStatPercentage(CallArgumentStatisticsData.argTotalLongArgs, CallArgumentStatisticsData.argTotalArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of FLOAT  arguments   = {FormatCallStatPercentage(CallArgumentStatisticsData.argTotalFloatArgs, CallArgumentStatisticsData.argTotalArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of DOUBLE arguments   = {FormatCallStatPercentage(CallArgumentStatisticsData.argTotalDoubleArgs, CallArgumentStatisticsData.argTotalArgs)} %\n\n"));

        if (CallArgumentStatisticsData.argTotalRegArgs is 0)
        {
            return;
        }

        output.Write("\nRegister Arguments:\n\n");

        output.Write(System.FormattableString.Invariant(
            $"Percentage of deferred arguments = {FormatCallStatPercentage(CallArgumentStatisticsData.argTotalDeferred, CallArgumentStatisticsData.argTotalRegArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of temp arguments     = {FormatCallStatPercentage(CallArgumentStatisticsData.argTotalTemps, CallArgumentStatisticsData.argTotalRegArgs)} %\n\n"));

        output.Write(System.FormattableString.Invariant(
            $"Maximum # of temps per method    = {FormatCallStatCount(CallArgumentStatisticsData.argMaxTempsPerMethod)}\n\n"));

        output.Write(System.FormattableString.Invariant(
            $"Percentage of ObjPtr arguments   = {FormatCallStatPercentage(CallArgumentStatisticsData.argTotalObjPtr, CallArgumentStatisticsData.argTotalRegArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of constant arguments = {FormatCallStatPercentage(CallArgumentStatisticsData.argTotalConst, CallArgumentStatisticsData.argTotalRegArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of lcl var arguments  = {FormatCallStatPercentage(CallArgumentStatisticsData.argTotalLclVar, CallArgumentStatisticsData.argTotalRegArgs)} %\n\n"));

        output.Write("--------------------------------------------------\n");
        output.Write("Argument count frequency table (includes ObjPtr):\n");
        output.Write("--------------------------------------------------\n");
        CallArgumentStatisticsData.argCntTable.dump(output);
        output.Write("--------------------------------------------------\n");

        output.Write("--------------------------------------------------\n");
        output.Write("DWORD argument count frequency table (w/o LONG):\n");
        output.Write("--------------------------------------------------\n");
        CallArgumentStatisticsData.argDWordCntTable.dump(output);
        output.Write("--------------------------------------------------\n");

        output.Write("--------------------------------------------------\n");
        output.Write("Temps count frequency table (per method):\n");
        output.Write("--------------------------------------------------\n");
        CallArgumentStatisticsData.argTempsCntTable.dump(output);
        output.Write("--------------------------------------------------\n");
    }

    private static string FormatCallStatPercentage(uint value, uint total)
    {
        var scaledValue = unchecked(100U * value);
        return FormatCallStat((float)scaledValue / total, "F2", 4);
    }

    private static string FormatCallStatCount(uint value)
    {
        // The native format is %d although these globals are unsigned.
        return unchecked((int)value).ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatCallStat(float value, string format, int width = 0)
    {
        string text;

        if (float.IsNaN(value))
        {
            text = "nan";
        }
        else if (float.IsPositiveInfinity(value))
        {
            text = "inf";
        }
        else if (float.IsNegativeInfinity(value))
        {
            text = "-inf";
        }
        else
        {
            text = value.ToString(format, CultureInfo.InvariantCulture);
        }

        return text.PadLeft(width);
    }
}
