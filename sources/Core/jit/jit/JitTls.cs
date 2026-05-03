// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

// Information kept in thread-local storage. This is used in the noway_assert exceptional path.
// If you are using it more broadly in retail code, you would need to understand the
// performance implications of accessing TLS.

#if DEBUG
public sealed class JitTls : IDisposable
{
    [ThreadStatic]
    private static JitTls? t_jitTls;

    private Compiler? _compiler;
    private LogEnv _logEnv;
    private readonly JitTls? _next;

    public unsafe JitTls(ICorJitInfo* jitInfo)
    {
        _logEnv = new LogEnv(jitInfo);
        _next = t_jitTls;
        t_jitTls = this;
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

    public static Compiler? Compiler
    {
        get
        {
            var jitTls = t_jitTls;
            return jitTls?._compiler;
        }

        set
        {
            var jitTls = t_jitTls;
            assert(jitTls is not null);
            jitTls._compiler = value;
        }
    }

    public static ref LogEnv LogEnv
    {
        get
        {
            var jitTls = t_jitTls;
            return ref ((jitTls is not null) ? ref jitTls._logEnv : ref Unsafe.NullRef<LogEnv>());
        }
    }

    private void Dispose(bool isDisposing)
    {
        t_jitTls = _next;
    }
}
#else
public static class JitTls
{
    [ThreadStatic]
    private static Compiler? t_compiler;

    public static Compiler? Compiler
    {
        get
        {
            return t_compiler;
        }

        set
        {
            t_compiler = value;
        }
    }
}
#endif
