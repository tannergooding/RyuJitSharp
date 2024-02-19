// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

#if DEBUG
public sealed unsafe class JitTls : IDisposable
{
    private Compiler? m_compiler;

    // private LogEnv m_logEnv;

    private readonly JitTls? m_next;

    public JitTls(ICorJitInfo* jitInfo)
    {
        // m_logEnv = new LogEnv(jitInfo);
        m_next = GetJitTls() as JitTls;
        SetJitTls(this);
    }

    ~JitTls()
    {
        Dispose(isDisposing: false);
    }

    public void Dispose()
    {
        Dispose(isDisposing: true);
        GC.SuppressFinalize(this);
    }

    // public static ref LogEnv GetLogEnv()
    // {
    //     if (GetJitTls() is JitTls jitTls)
    //     {
    //         return ref jitTls.m_logEnv;
    //     }
    //     else
    //     {
    //         return ref Unsafe.NullRef<LogEnv>();
    //     }
    // }

    public static Compiler? GetCompiler()
    {
        return (GetJitTls() as JitTls)!.m_compiler;
    }

    public static void SetCompiler(Compiler compiler)
    {
        (GetJitTls() as JitTls)!.m_compiler = compiler;
    }

    private void Dispose(bool isDisposing)
    {
        SetJitTls(m_next);
    }
}
#else
public static class JitTls
{
    public static Compiler? GetCompiler()
    {
        return GetJitTls() as Compiler;
    }

    public static void SetCompiler(Compiler compiler)
    {
        SetJitTls(compiler);
    }
}
#endif
