// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

internal static class GCPollCases
{
    [DllImport("kernel32.dll", EntryPoint = "GetCurrentProcessId", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [SuppressGCTransition]
    private static extern uint GetCurrentProcessIdSuppressed();

    [DllImport("kernel32.dll", EntryPoint = "GetCurrentProcessId", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetCurrentProcessIdRegular();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static uint InlinePoll(bool takeSuppressedPath)
    {
        if (takeSuppressedPath)
        {
            return GetCurrentProcessIdSuppressed();
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static uint CalloutPoll()
    {
        return GetCurrentProcessIdSuppressed();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static uint MixedCalls()
    {
        var suppressed = GetCurrentProcessIdSuppressed();
        var regular = GetCurrentProcessIdRegular();

        return suppressed == regular ? regular : 0;
    }

    public static int FinallyCount;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static uint EhCallout()
    {
        try
        {
            return GetCurrentProcessIdSuppressed();
        }
        finally
        {
            FinallyCount++;
        }
    }
}

internal static class Program
{
    public static int Main()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 1;
        }

        var expected = (uint)Environment.ProcessId;
        if (expected == 0 ||
            GCPollCases.InlinePoll(true) != expected ||
            GCPollCases.InlinePoll(false) != 0 ||
            GCPollCases.CalloutPoll() != expected ||
            GCPollCases.MixedCalls() != expected ||
            GCPollCases.EhCallout() != expected ||
            GCPollCases.FinallyCount != 1)
        {
            return 2;
        }

#pragma warning disable CA1303 // Fixed output identifies the successful native oracle run.
        Console.WriteLine("GC poll corpus: suppressed and regular calls returned the current process ID");
#pragma warning restore CA1303
        return 0;
    }
}
