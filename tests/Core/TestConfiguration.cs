using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static partial class TestConfiguration
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "verbose")]
    private static extern ref bool Verbose(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "verboseSsa")]
    private static extern ref bool VerboseSsa(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "dspMetrics")]
    private static extern ref bool DspMetrics(ref Compiler.Options options);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "dspCode")]
    private static extern ref bool DspCode(ref Compiler.Options options);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "dspEHTable")]
    private static extern ref bool DspEHTable(ref Compiler.Options options);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compEnablePCRelAddr")]
    private static extern ref bool CompEnablePCRelAddr(ref Compiler.Options options);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIssuing")]
    private static extern ref bool EmitIssuing(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "minRegCandidateCount")]
    private static extern ref uint MinRegCandidateCount(RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_doNotEnregReason")]
    private static extern ref DoNotEnregisterReason DoNotEnregisterReasonField(ref LclVarDsc descriptor);

    public static bool IsDebug => Configuration.DEBUG;

    public static bool IsArm32 => Configuration.TARGET_ARM;

    public static void SetScalableVectorConfiguration(bool enabled)
    {
        object config = JitConfig;
        var field = typeof(JitConfigValues).GetField(
            "_jitUseScalableVectorT",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected scalable-vector configuration field.");
        field.SetValue(config, enabled ? 1 : 0);
        JitConfig = (JitConfigValues)config;
    }

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
        compiler.fgSafeBasicBlockCreation = true;
    }

    [Conditional("DEBUG")]
    public static void DisableSafeBasicBlockCreation(Compiler compiler)
    {
        compiler.fgSafeBasicBlockCreation = false;
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

    public static void SetDspCode(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            DspCode(ref compiler.opts) = enabled;
        }
    }

    public static void SetDoNotEnregisterReason(ref LclVarDsc descriptor, DoNotEnregisterReason reason)
    {
        if (IsDebug)
        {
            DoNotEnregisterReasonField(ref descriptor) = reason;
        }
    }

    public static void SetDspEHTable(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            DspEHTable(ref compiler.opts) = enabled;
        }
    }

    public static void SetCompEnablePCRelAddr(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            CompEnablePCRelAddr(ref compiler.opts) = enabled;
        }
    }

    [Conditional("DEBUG")]
    public static void EnableEmitterIssuing(Emitter emitter)
    {
        EmitIssuing(emitter) = true;
    }

    public static void SetMinimumRegisterCandidateCount(RefPosition reference, uint count)
    {
        if (IsDebug)
        {
            MinRegCandidateCount(reference) = count;
        }
    }

    [Conditional("DEBUG")]
    public static void EnableSafeFlowEdgeCreation(Compiler compiler)
    {
        compiler.fgSafeFlowEdgeCreation = true;
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

    public static void SetVerboseSsa(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            VerboseSsa(compiler) = enabled;
        }
    }
}
