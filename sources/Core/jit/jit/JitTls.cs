// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

#if DEBUG
public sealed class JitTls : IDisposable
{
    private Compiler? _compiler;
    private LogEnv _logEnv;
    private readonly JitTls? _next;

    public unsafe JitTls(ICorJitInfo* jitInfo)
    {
        _logEnv = new LogEnv(jitInfo);
        _next = GetJitTls();
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

    public static Compiler? GetCompiler()
    {
        var jitTls = GetJitTls();
        assert(jitTls is not null);
        return jitTls._compiler;
    }

    public static ref LogEnv GetLogEnv()
    {
        var jitTls = GetJitTls();
        assert(jitTls is not null);
        return ref jitTls._logEnv;
    }

    public static void SetCompiler(Compiler? compiler)
    {
        var jitTls = GetJitTls();
        assert(jitTls is not null);
        jitTls._compiler = compiler;
    }

    private void Dispose(bool isDisposing)
    {
        SetJitTls(_next);
    }
}
#else
public static class JitTls
{
    public static Compiler? GetCompiler()
    {
        return GetJitTls();
    }

    public static void SetCompiler(Compiler? compiler)
    {
        SetJitTls(compiler);
    }
}
#endif
