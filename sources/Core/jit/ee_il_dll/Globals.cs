// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RyuJitSharp;

public partial class Globals
{
    private static unsafe ICorJitHost* s_jitHost;
    private static bool s_isJitInitialized;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "jitStartup")]
    public static unsafe void jitStartup(ICorJitHost* jitHost)
    {
        if (s_isJitInitialized)
        {
            if (jitHost != s_jitHost)
            {
                // We normally don't expect jitStartup() to be invoked more than once.
                // (We check whether it has been called once due to an abundance of caution.)
                // However, during SuperPMI playback of MCH file, we need to JIT many different methods.
                // Each one carries its own environment configuration state.
                // So, we need the JIT to reload the JitConfig state for each change in the environment state of the
                // replayed compilations.
                // We do this by calling jitStartup with a different ICorJitHost,
                // and have the JIT re-initialize its JitConfig state when this happens.

                JitConfig.destroy(s_jitHost);
                JitConfig.initialize(jitHost);

                s_jitHost = jitHost;
            }
            return;
        }

#if HOST_UNIX
        var err = PAL_InitializeDLL();

        if (err != 0)
        {
            return;
        }
#endif

        s_jitHost = jitHost;

        assert(!JitConfig.isInitialized());
        JitConfig.initialize(jitHost);
        Compiler.compStartup();

        s_isJitInitialized = true;
    }

    private static volatile StreamWriter? s_jitstdout;

    private static unsafe StreamWriter jitstdoutInit()
    {
        var jitStdOutFile = JitConfig[ConfigString.JitStdOutFile];

        StreamWriter jitstdout;

        if (jitStdOutFile is not null)
        {
            jitstdout = new StreamWriter(Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(jitStdOutFile)), append: true);
        }
        else
        {
            jitstdout = new StreamWriter(Console.OpenStandardOutput(), leaveOpen: true);
        }

        var observed = Interlocked.CompareExchange(ref s_jitstdout, jitstdout, null);

        if (observed is not null)
        {
            jitstdout.Dispose();
            return observed;
        }
        return jitstdout;
    }

    public static void jitShutdown(bool processIsTerminating)
    {
        if (!s_isJitInitialized)
        {
            return;
        }

        Compiler.compShutdown();

        var jitstdout = s_jitstdout;
        jitstdout?.Dispose();

        s_isJitInitialized = false;
    }

    private static readonly unsafe CILJit* g_CILJit = InitCILJit();

    private static unsafe CILJit* InitCILJit()
    {
        CILJit* instance = (CILJit*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(Globals), sizeof(CILJit));
        instance->lpVtbl = CILJit.s_vtbl;
        return instance;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "getJit")]
    public static unsafe ICorJitCompiler* getJit()
    {
        if (!s_isJitInitialized)
        {
            return null;
        }
        return (ICorJitCompiler*)g_CILJit;
    }

    // Information kept in thread-local storage. This is used in the noway_assert exceptional path.
    // If you are using it more broadly in retail code, you would need to understand the
    // performance implications of accessing TLS.

#if DEBUG
    [ThreadStatic]
    private static JitTls? t_jitTls;

    internal static JitTls? GetJitTls()
    {
        return t_jitTls;
    }

    internal static void SetJitTls(JitTls? value)
    {
        t_jitTls = value;
    }
#else
    [ThreadStatic]
    private static Compiler? t_jitTls;

    internal static Compiler? GetJitTls()
    {
        return t_jitTls;
    }

    internal static void SetJitTls(Compiler? value)
    {
        t_jitTls = value;
    }
#endif
}
