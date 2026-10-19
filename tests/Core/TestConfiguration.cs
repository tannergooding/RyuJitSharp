using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static partial class TestConfiguration
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_verbose")]
    private static extern void SetVerboseProperty(Compiler compiler, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_verboseTrees")]
    private static extern void SetVerboseTreesProperty(Compiler compiler, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_verboseSsa")]
    private static extern void SetVerboseSsaProperty(Compiler compiler, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compEnablePCRelAddr")]
    private static extern ref bool CompEnablePCRelAddr(ref Compiler.Options options);

    private static FieldInfo FatalNoWayAssertBodyArgsCountField =>
        typeof(Globals).GetField(
            "s_fatalNoWayAssertBodyArgsCount",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Missing Debug fatal argument diagnostic state.");

    public static bool IsDebug => Configuration.DEBUG;

    public static bool IsArm32 => Configuration.TARGET_ARM;

    public static uint GetFatalNoWayAssertBodyArgsCount()
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Fatal argument diagnostics are available only in Debug.");
        }

        var value = FatalNoWayAssertBodyArgsCountField.GetValue(null);
        return value is uint count
            ? count
            : throw new InvalidOperationException("Invalid Debug fatal argument diagnostic state.");
    }

    public static void SetFatalNoWayAssertBodyArgsCount(uint count)
    {
        if (!IsDebug)
        {
            throw new InvalidOperationException("Fatal argument diagnostics are available only in Debug.");
        }

        FatalNoWayAssertBodyArgsCountField.SetValue(null, count);
    }

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
            compiler.opts.dspMetrics = enabled;
        }
    }

    public static void SetDspCode(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            compiler.opts.dspCode = enabled;
        }
    }

    public static void SetDspEHTable(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            compiler.opts.dspEHTable = enabled;
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
        emitter.emitIssuing = true;
    }

    public static void SetMinimumRegisterCandidateCount(RefPosition reference, uint count)
    {
        if (IsDebug)
        {
            reference.minRegCandidateCount = count;
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

    public static void SetEhNormalizationDone(Compiler compiler, bool done)
    {
        if (IsDebug)
        {
            compiler.fgNormalizeEHDone = done;
        }
    }

    public static void SetVerbose(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            SetVerboseProperty(compiler, enabled);
        }
    }

    public static void SetVerboseSsa(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            SetVerboseSsaProperty(compiler, enabled);
        }
    }

    public static void SetVerboseTrees(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            SetVerboseTreesProperty(compiler, enabled);
        }
    }
}
