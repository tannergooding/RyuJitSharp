// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RyuJitSharp;

public unsafe partial class Globals
{
    private static ICorJitHost* g_jitHost;

    private static bool g_jitInitialized;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "jitStartup")]
    public static void jitStartup(ICorJitHost* jitHost)
    {
        if (g_jitInitialized)
        {
            if (jitHost != g_jitHost)
            {
                // We normally don't expect jitStartup() to be invoked more than once.
                // (We check whether it has been called once due to an abundance of caution.)
                // However, during SuperPMI playback of MCH file, we need to JIT many different methods.
                // Each one carries its own environment configuration state.
                // So, we need the JIT to reload the JitConfig state for each change in the environment state of the
                // replayed compilations.
                // We do this by calling jitStartup with a different ICorJitHost,
                // and have the JIT re-initialize its JitConfig state when this happens.

                JitConfig.destroy(g_jitHost);
                JitConfig.initialize(jitHost);

                g_jitHost = jitHost;
            }
            return;
        }

        g_jitHost = jitHost;

        Debug.Assert(!JitConfig.isInitialized());
        JitConfig.initialize(jitHost);
        Compiler.compStartup();

        g_jitInitialized = true;
    }

    private static volatile StreamWriter? s_jitstdout;

    private static StreamWriter jitstdoutInit()
    {
        // TODO: Port jitstdoutInit

        StreamWriter stdout = new StreamWriter(Console.OpenStandardOutput());
        StreamWriter? observed = Interlocked.CompareExchange(ref s_jitstdout, stdout, null);

        if (observed is not null)
        {
            stdout.Dispose();
            return observed;
        }
        return stdout;
    }

    private static StreamWriter jitstdout()
    {
        StreamWriter? jitstdout = s_jitstdout;
        jitstdout ??= jitstdoutInit();
        return jitstdout;
    }

    /// <summary>Like printf/logf, but only outputs to jitstdout -- skips call back into EE.</summary>
    private static void jitprintf(string fmt)
    {
        jitstdout().Write(fmt);
    }

    public static void jitShutdown(bool processIsTerminating)
    {
        if (!g_jitInitialized)
        {
            return;
        }

        Compiler.compShutdown();
        s_jitstdout?.Dispose();

        g_jitInitialized = false;
    }

    private static readonly CILJit* g_CILJit = InitCILJit();

    private static CILJit* InitCILJit()
    {
        CILJit* instance = (CILJit*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(Globals), sizeof(CILJit));
        instance->lpVtbl = CILJit.s_vtbl;
        return instance;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "getJit")]
    public static ICorJitCompiler* getJit()
    {
        if (!g_jitInitialized)
        {
            return null;
        }
        return (ICorJitCompiler*)g_CILJit;
    }

    // Information kept in thread-local storage. This is used in the noway_assert exceptional path.
    // If you are using it more broadly in retail code, you would need to understand the
    // performance implications of accessing TLS.

    [ThreadStatic]
    private static object? gJitTls;

    internal static object? GetJitTls()
    {
        return gJitTls;
    }

    internal static void SetJitTls(object? value)
    {
        gJitTls = value;
    }
}
