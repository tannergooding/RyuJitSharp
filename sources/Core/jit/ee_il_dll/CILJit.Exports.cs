// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial struct CILJit
{
    internal static unsafe ICorJitHost* s_jitHost;
    private static bool s_isJitInitialized;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "getJit")]
    private static unsafe ICorJitCompiler* getJit() => s_isJitInitialized ? (ICorJitCompiler*)(s_instance) : null;

    private static void jitShutdown(bool processIsTerminating)
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)], EntryPoint = "jitStartup")]
    private static unsafe void jitStartup(ICorJitHost* jitHost)
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
}
