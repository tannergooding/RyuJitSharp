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

public sealed class JitTls : IDisposable
{
    [ThreadStatic]
    private static JitTls? t_jitTls;

    [ThreadStatic]
    private static Compiler? t_compiler;

    private Compiler? _compiler;
    private LogEnv _logEnv;
    private readonly JitTls? _next;

    public unsafe JitTls(ICorJitInfo* jitInfo)
    {
        if (!Configuration.DEBUG)
        {
            throw new InvalidOperationException("JitTls scopes are only available in Debug builds.");
        }

        _logEnv = new LogEnv(jitInfo);
        _next = t_jitTls;
        t_jitTls = this;
    }

    public static unsafe IDisposable? CreateScope(ICorJitInfo* jitInfo)
    {
        return Configuration.DEBUG ? new JitTls(jitInfo) : null;
    }

    // Native JitTls is a stack scope; restore TLS on the owning thread, not a finalizer thread.
    public void Dispose()
    {
        if (Configuration.DEBUG)
        {
            t_jitTls = _next;
        }
    }

    public static Compiler? Compiler
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                return t_compiler;
            }

            var jitTls = t_jitTls;
            return jitTls?._compiler;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                t_compiler = value;
                return;
            }

            var jitTls = t_jitTls;
            assert(jitTls is not null);
            jitTls._compiler = value;
        }
    }

    public static ref LogEnv LogEnv
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                throw new InvalidOperationException("JitTls.LogEnv is only available in Debug builds.");
            }

            var jitTls = t_jitTls;
            return ref ((jitTls is not null) ? ref jitTls._logEnv : ref Unsafe.NullRef<LogEnv>());
        }
    }
}
