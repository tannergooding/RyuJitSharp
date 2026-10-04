// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if CALL_ARG_STATS
using System.Globalization;
using System.IO;

namespace RyuJitSharp;

public partial class Compiler
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

    private static readonly uint[] argCntBuckets = [0, 1, 2, 3, 4, 5, 6, 10, 0];
    private static readonly uint[] argDWordCntBuckets = [0, 1, 2, 3, 4, 5, 6, 10, 0];
    private static readonly uint[] argDWordLngCntBuckets = [0, 1, 2, 3, 4, 5, 6, 10, 0];
    private static readonly uint[] argTempsCntBuckets = [0, 1, 2, 3, 4, 5, 6, 10, 0];

    internal static readonly Histogram argCntTable = new(argCntBuckets);
    internal static readonly Histogram argDWordCntTable = new(argDWordCntBuckets);
    internal static readonly Histogram argDWordLngCntTable = new(argDWordLngCntBuckets);
    internal static readonly Histogram argTempsCntTable = new(argTempsCntBuckets);

    public void compCallArgStats()
    {
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
                argTotalCalls = unchecked(argTotalCalls + 1);

                if (call.Args.ThisArg is null)
                {
                    if (call.IsHelperCall())
                    {
                        argHelperCalls = unchecked(argHelperCalls + 1);
                    }
                    else
                    {
                        argStaticCalls = unchecked(argStaticCalls + 1);
                    }
                }
                else
                {
                    argTotalObjPtr = unchecked(argTotalObjPtr + 1);

                    if (call.IsVirtual)
                    {
                        argVirtualCalls = unchecked(argVirtualCalls + 1);
                    }
                    else
                    {
                        argNonVirtualCalls = unchecked(argNonVirtualCalls + 1);
                    }
                }
            }
        }

        argTempsCntTable.record(argTempsThisMethod);

        if (argMaxTempsPerMethod < argTempsThisMethod)
        {
            argMaxTempsPerMethod = argTempsThisMethod;
        }
    }

    internal static void compDispCallArgStats(StreamWriter output)
    {
        if (argTotalCalls is 0)
        {
            return;
        }

        output.Write("\n");
        output.Write("--------------------------------------------------\n");
        output.Write("Call stats\n");
        output.Write("--------------------------------------------------\n");

        var callsPerMethod = (float)argTotalCalls / genMethodCnt;
        output.Write(System.FormattableString.Invariant(
            $"Total # of calls = {FormatCallStatCount(argTotalCalls)}, calls / method = {FormatCallStat(callsPerMethod, "F3")}\n\n"));

        output.Write(System.FormattableString.Invariant(
            $"Percentage of      helper calls = {FormatCallStatPercentage(argHelperCalls, argTotalCalls)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of      static calls = {FormatCallStatPercentage(argStaticCalls, argTotalCalls)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of     virtual calls = {FormatCallStatPercentage(argVirtualCalls, argTotalCalls)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of non-virtual calls = {FormatCallStatPercentage(argNonVirtualCalls, argTotalCalls)} %\n\n"));

        var averageArgsPerCall = (float)argTotalArgs / argTotalCalls;
        output.Write(System.FormattableString.Invariant(
            $"Average # of arguments per call = {FormatCallStat(averageArgsPerCall, "F2")}%\n\n"));

        output.Write(System.FormattableString.Invariant(
            $"Percentage of DWORD  arguments   = {FormatCallStatPercentage(argTotalDWordArgs, argTotalArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of LONG   arguments   = {FormatCallStatPercentage(argTotalLongArgs, argTotalArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of FLOAT  arguments   = {FormatCallStatPercentage(argTotalFloatArgs, argTotalArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of DOUBLE arguments   = {FormatCallStatPercentage(argTotalDoubleArgs, argTotalArgs)} %\n\n"));

        if (argTotalRegArgs is 0)
        {
            return;
        }

        output.Write("\nRegister Arguments:\n\n");

        output.Write(System.FormattableString.Invariant(
            $"Percentage of deferred arguments = {FormatCallStatPercentage(argTotalDeferred, argTotalRegArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of temp arguments     = {FormatCallStatPercentage(argTotalTemps, argTotalRegArgs)} %\n\n"));

        output.Write(System.FormattableString.Invariant(
            $"Maximum # of temps per method    = {FormatCallStatCount(argMaxTempsPerMethod)}\n\n"));

        output.Write(System.FormattableString.Invariant(
            $"Percentage of ObjPtr arguments   = {FormatCallStatPercentage(argTotalObjPtr, argTotalRegArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of constant arguments = {FormatCallStatPercentage(argTotalConst, argTotalRegArgs)} %\n"));
        output.Write(System.FormattableString.Invariant(
            $"Percentage of lcl var arguments  = {FormatCallStatPercentage(argTotalLclVar, argTotalRegArgs)} %\n\n"));

        output.Write("--------------------------------------------------\n");
        output.Write("Argument count frequency table (includes ObjPtr):\n");
        output.Write("--------------------------------------------------\n");
        argCntTable.dump(output);
        output.Write("--------------------------------------------------\n");

        output.Write("--------------------------------------------------\n");
        output.Write("DWORD argument count frequency table (w/o LONG):\n");
        output.Write("--------------------------------------------------\n");
        argDWordCntTable.dump(output);
        output.Write("--------------------------------------------------\n");

        output.Write("--------------------------------------------------\n");
        output.Write("Temps count frequency table (per method):\n");
        output.Write("--------------------------------------------------\n");
        argTempsCntTable.dump(output);
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
#endif
