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
    private static Compiler? t_compiler;

    public unsafe JitTls(ICorJitInfo* jitInfo)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitTlsScopesAreOnlyAvailableInDebugBuilds);
            return;
        }

        var currentJitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
        currentJitTls?.FlushCompGenTreeID();

        var data = global::RyuJitSharp.Compiler.DebugData.Get(this);
        data.LogEnv = new LogEnv(jitInfo);
        data.Next = currentJitTls;

        global::RyuJitSharp.Compiler.DebugData.CurrentJitTls = this;
    }

    public static unsafe IDisposable? CreateScope(ICorJitInfo* jitInfo)
    {
        return Configuration.DEBUG ? new JitTls(jitInfo) : null;
    }

    internal static ref global::RyuJitSharp.Compiler.DebugData.JitTlsImporterDebugState CurrentImporterDebugState
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ImporterDebugStateIsOnlyAvailableInDebugBuilds);
                return ref Unsafe.NullRef<global::RyuJitSharp.Compiler.DebugData.JitTlsImporterDebugState>();
            }

            var jitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
            if (jitTls is null)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ImporterDebugStateRequiresAnActiveJitTlsScope);
                return ref Unsafe.NullRef<global::RyuJitSharp.Compiler.DebugData.JitTlsImporterDebugState>();
            }

            return ref global::RyuJitSharp.Compiler.DebugData.Get(jitTls).ImporterDebugState;
        }
    }

    internal static ImporterDebugStateScope PushImporterDebugState()
    {
        if (!Configuration.DEBUG)
        {
            return default;
        }

        var jitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
        if (jitTls is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ImporterDebugStateRequiresAnActiveJitTlsScope);
            return default;
        }

        return new ImporterDebugStateScope(jitTls);
    }

    // Native JitTls is a stack scope; restore TLS on the owning thread, not a finalizer thread.
    public void Dispose()
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

        var currentJitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
        if (ReferenceEquals(currentJitTls, this))
        {
            var data = global::RyuJitSharp.Compiler.DebugData.Get(this);
            FlushCompGenTreeID();
            global::RyuJitSharp.Compiler.DebugData.CurrentJitTls = data.Next;
            data.Next?.ReloadCompGenTreeID();
        }
    }

    internal readonly ref struct ImporterDebugStateScope : IDisposable
    {
        private readonly JitTls? _jitTls;
        private readonly global::RyuJitSharp.Compiler.DebugData.JitTlsImporterDebugState _previousState;

        internal ImporterDebugStateScope(JitTls jitTls)
        {
            _jitTls = jitTls;
            var data = global::RyuJitSharp.Compiler.DebugData.Get(jitTls);
            _previousState = data.ImporterDebugState;
            data.ImporterDebugState = default;
        }

        public readonly void Dispose()
        {
            if (_jitTls is { } jitTls)
            {
                global::RyuJitSharp.Compiler.DebugData.Get(jitTls).ImporterDebugState = _previousState;
            }
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

            var jitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
            return jitTls is null ? null : global::RyuJitSharp.Compiler.DebugData.Get(jitTls).Compiler;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                t_compiler = value;
                return;
            }

            var jitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
            assert(jitTls is not null);
            var activeJitTls = jitTls!;
            var data = global::RyuJitSharp.Compiler.DebugData.Get(activeJitTls);

            if (ReferenceEquals(data.Compiler, value))
            {
                return;
            }

            activeJitTls.FlushCompGenTreeID();
            data.Compiler = value;
            activeJitTls.ReloadCompGenTreeID();
        }
    }

    internal static int GetCompGenTreeID(Compiler compiler)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.TreeIDsAreOnlyAvailableInDebugBuilds);
            return default;
        }

        var jitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
        if (jitTls is { } active)
        {
            var data = global::RyuJitSharp.Compiler.DebugData.Get(active);
            if (ReferenceEquals(data.Compiler, compiler))
            {
                return data.CompGenTreeID;
            }
        }

        return compiler.GetCompGenTreeIDFromDebugSidecar();
    }

    internal static void SetCompGenTreeID(Compiler compiler, int value)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.TreeIDsAreOnlyAvailableInDebugBuilds);
            return;
        }

        var jitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
        if (jitTls is { } active)
        {
            var data = global::RyuJitSharp.Compiler.DebugData.Get(active);
            if (ReferenceEquals(data.Compiler, compiler))
            {
                data.CompGenTreeID = value;
                return;
            }
        }

        compiler.SetCompGenTreeIDInDebugSidecar(value);
    }

    internal static void IncrementCompGenTreeID(Compiler compiler)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.TreeIDsAreOnlyAvailableInDebugBuilds);
            return;
        }

        var jitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
        if (jitTls is { } active)
        {
            var data = global::RyuJitSharp.Compiler.DebugData.Get(active);
            if (ReferenceEquals(data.Compiler, compiler))
            {
                data.CompGenTreeID++;
                return;
            }
        }

        compiler.IncrementCompGenTreeIDInDebugSidecar();
    }

    internal static int GetNextCompGenTreeID()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.TreeIDsAreOnlyAvailableInDebugBuilds);
            return default;
        }

        var jitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
        if (jitTls is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.GenTreeConstructionRequiresAnActiveCompilerInDebugBuilds);
            return default;
        }

        var data = global::RyuJitSharp.Compiler.DebugData.Get(jitTls);
        if (data.Compiler is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.GenTreeConstructionRequiresAnActiveCompilerInDebugBuilds);
            return default;
        }

        return data.CompGenTreeID++;
    }

    public static ref LogEnv LogEnv
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitTlsLogEnvIsOnlyAvailableInDebugBuilds);
                return ref Unsafe.NullRef<LogEnv>();
            }

            var jitTls = global::RyuJitSharp.Compiler.DebugData.CurrentJitTls;
            if (jitTls is null)
            {
                return ref Unsafe.NullRef<LogEnv>();
            }

            return ref global::RyuJitSharp.Compiler.DebugData.Get(jitTls).LogEnv;
        }
    }

    private void FlushCompGenTreeID()
    {
        var data = global::RyuJitSharp.Compiler.DebugData.Get(this);
        data.Compiler?.SetCompGenTreeIDInDebugSidecar(data.CompGenTreeID);
    }

    private void ReloadCompGenTreeID()
    {
        var data = global::RyuJitSharp.Compiler.DebugData.Get(this);
        data.CompGenTreeID = data.Compiler?.GetCompGenTreeIDFromDebugSidecar() ?? 0;
    }
}
