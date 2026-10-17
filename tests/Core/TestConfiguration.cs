using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp.UnitTests;

internal static partial class TestConfiguration
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "verbose")]
    private static extern ref bool Verbose(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "dspMetrics")]
    private static extern ref bool DspMetrics(ref Compiler.Options options);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "fgSafeBasicBlockCreation")]
    private static extern ref bool SafeBasicBlockCreation(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "fgSafeFlowEdgeCreation")]
    private static extern ref bool SafeFlowEdgeCreation(Compiler compiler);

    public static bool IsDebug => Configuration.DEBUG;

    public static unsafe IDisposable? CreateJitTls()
    {
        return CreateJitTls(null);
    }

    public static unsafe IDisposable? CreateJitTls(ICorJitInfo* jitInfo)
        => JitTls.CreateScope(jitInfo);

    public static unsafe IDisposable? CreateJitTlsWithCompiler(Compiler? compiler)
    {
        return CreateJitTlsWithCompiler(compiler, null);
    }

    public static unsafe IDisposable? CreateJitTlsWithCompiler(Compiler? compiler, ICorJitInfo* jitInfo)
    {
        var jitTls = JitTls.CreateScope(jitInfo);
        if (IsDebug)
        {
            JitTls.Compiler = compiler;
        }
        return jitTls;
    }

    public static unsafe IDisposable? CreateJitTlsWithCompilerAndLogEnv(
        Compiler? compiler, ICorJitInfo* jitInfo)
    {
        var jitTls = JitTls.CreateScope(jitInfo);
        if (IsDebug)
        {
            JitTls.Compiler = compiler;
            JitTls.LogEnv.Compiler = compiler;
        }
        return jitTls;
    }

    [Conditional("DEBUG")]
    public static void SetJitTlsLogCompiler(Compiler? compiler)
    {
        JitTls.LogEnv.Compiler = compiler;
    }

    [Conditional("DEBUG")]
    public static void EnableSafeBasicBlockCreation(Compiler compiler)
    {
        SafeBasicBlockCreation(compiler) = true;
    }

    [Conditional("DEBUG")]
    public static void DisableSafeBasicBlockCreation(Compiler compiler)
    {
        SafeBasicBlockCreation(compiler) = false;
    }

    public static void SetCompilerFullName(Compiler compiler, string name)
    {
        if (IsDebug)
        {
            compiler.info.compFullName = name;
        }
    }

    public static void SetDspMetrics(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            DspMetrics(ref compiler.opts) = enabled;
        }
    }

    [Conditional("DEBUG")]
    public static void EnableSafeFlowEdgeCreation(Compiler compiler)
    {
        SafeFlowEdgeCreation(compiler) = true;
    }

    public static void EnableVerbose(Compiler compiler)
    {
        SetVerbose(compiler, true);
    }

    public static void SetVerbose(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            Verbose(compiler) = enabled;
        }
    }
}
