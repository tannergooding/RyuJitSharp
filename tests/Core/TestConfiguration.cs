using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static partial class TestConfiguration
{
    private static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing object copy helper.");

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_verbose")]
    private static extern void SetVerboseProperty(Compiler compiler, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_verboseTrees")]
    private static extern void SetVerboseTreesProperty(Compiler compiler, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_verboseSsa")]
    private static extern void SetVerboseSsaProperty(Compiler compiler, bool value);

    private static FieldInfo FatalNoWayAssertBodyArgsCountField =>
        typeof(Compiler.DebugData.FatalStatisticsData).GetField(
            "s_fatalNoWayAssertBodyArgsCount",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Missing Debug fatal argument diagnostic state.");

    public static bool IsDebug => Configuration.DEBUG;

    public static bool IsArm32 => Configuration.TARGET_ARM;

    public static regMaskTP XarchRbxMask => GetXarchRegisterMask(RBM_X86_RBX, RBM_X64_RBX);

    public static regMaskTP XarchRcxMask => GetXarchRegisterMask(RBM_X86_RCX, RBM_X64_RCX);

    private static regMaskTP GetXarchRegisterMask(regMaskTP x86Mask, regMaskTP amd64Mask)
    {
        if (Configuration.TARGET_X86)
        {
            return x86Mask;
        }

        if (Configuration.TARGET_AMD64)
        {
            return amd64Mask;
        }

        throw new InvalidOperationException("Xarch register masks require an x86 or AMD64 target.");
    }

    public static void WithLsraStatistics(Action action)
    {
        const string switchName = "RyuJitSharp.TRACK_LSRA_STATS";
        var hadOriginalValue = AppContext.TryGetSwitch(switchName, out var originalValue);
        AppContext.SetSwitch(switchName, true);
        try
        {
            action();
        }
        finally
        {
            AppContext.SetSwitch(switchName, hadOriginalValue && originalValue);
        }
    }

    public static void SetLinearScanStressMask(LinearScan allocator, int value)
    {
        if (IsDebug)
        {
            Compiler.DebugData.Get(allocator).StressMask = value;
        }
    }

    public static void SetExpensiveDebugCheckLevel(Compiler compiler, int value)
    {
        if (IsDebug)
        {
            compiler.expensiveDebugCheckLevel = value;
        }
    }

    public static Compiler.NowayAssertMap? GetNowayAssertMap()
        => Compiler.MeasurementData.NowayAssertData.Map;

    public static void SetNowayAssertMap(Compiler.NowayAssertMap? map)
        => Compiler.MeasurementData.NowayAssertData.Map = map;

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "The generated JIT configuration sidecar field names are reflected only by these tests, and active sidecar fields are directly accessed by their generated properties.")]
    public static JitConfigField? GetJitConfigField(
        string name,
        BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic)
    {
        var configField = typeof(JitConfigValues).GetField(name, bindingFlags);
        if (configField is not null)
        {
            return new JitConfigField(configField, null, null);
        }

        var configurationDataType = typeof(JitConfigValues).GetNestedType(
            "ConfigurationData",
            BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing JIT configuration sidecar data.");
        var configurationDataField = typeof(JitConfigValues).GetField(
            "_configurationData",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing JIT configuration sidecar field.");

        if (IsDebug)
        {
            var debugDataType = typeof(Compiler.DebugData.JitConfigValuesData);
            var debugField = debugDataType.GetField(
                name,
                bindingFlags | BindingFlags.Instance | BindingFlags.NonPublic);
            if (debugField is not null)
            {
                var debugDataField = configurationDataType.GetField(
                    "_debugData",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Missing JIT configuration DebugData field.");
                return new JitConfigField(debugField, configurationDataField, debugDataField);
            }
        }

        foreach (var sidecarType in typeof(JitConfigValues).GetNestedTypes(BindingFlags.NonPublic))
        {
            if (sidecarType == configurationDataType || !IsJitConfigSidecarActive(sidecarType))
            {
                continue;
            }

            var field = sidecarType.GetField(
                name,
                bindingFlags | BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is null)
            {
                continue;
            }

            var sidecarField = configurationDataType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.FieldType == sidecarType);
            if (sidecarField is not null)
            {
                return new JitConfigField(field, configurationDataField, sidecarField);
            }
        }

        return null;
    }

    public static Compiler.DebugData.JitConfigValuesData GetJitConfigDebugDataForWrite(ref JitConfigValues config)
        => config.getDebugDataForWrite();

    public static void SetJitConfigValue(ref JitConfigValues config, string name, object? value)
    {
        object boxedConfig = config;
        var field = GetJitConfigField(name)
            ?? throw new InvalidOperationException($"Missing JIT configuration field '{name}'.");
        field.SetValue(boxedConfig, value);
        config = (JitConfigValues)boxedConfig;
    }

    internal sealed class JitConfigField
    {
        private readonly FieldInfo _field;
        private readonly FieldInfo? _configurationDataField;
        private readonly FieldInfo? _sidecarField;

        internal JitConfigField(FieldInfo field, FieldInfo? configurationDataField, FieldInfo? sidecarField)
        {
            _field = field;
            _configurationDataField = configurationDataField;
            _sidecarField = sidecarField;
        }

        public Type FieldType => _field.FieldType;

        [UnconditionalSuppressMessage("Trimming", "IL2072",
            Justification = "The reflected sidecar types are generated by JitConfigValues and only instantiated here for tests.")]
        public object? GetValue(object? target)
        {
            if (_configurationDataField is null)
            {
                return _field.GetValue(target);
            }

            var configurationData = _configurationDataField.GetValue(target);
            var sidecar = configurationData is null
                ? RuntimeHelpers.GetUninitializedObject(_sidecarField!.FieldType)
                : _sidecarField!.GetValue(configurationData);
            return _field.GetValue(sidecar);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2072",
            Justification = "The reflected sidecar types are generated by JitConfigValues and only instantiated here for tests.")]
        public void SetValue(object? target, object? value)
        {
            if (_configurationDataField is null)
            {
                _field.SetValue(target, value);
                return;
            }

            ArgumentNullException.ThrowIfNull(target);

            var configurationData = _configurationDataField.GetValue(target);
            var updatedConfigurationData = configurationData is null
                ? RuntimeHelpers.GetUninitializedObject(_configurationDataField.FieldType)
                : MemberwiseCloneMethod.Invoke(configurationData, null);
            var sidecar = _sidecarField!.GetValue(updatedConfigurationData);
            if (_sidecarField.FieldType.IsClass)
            {
                sidecar = sidecar is null
                    ? RuntimeHelpers.GetUninitializedObject(_sidecarField.FieldType)
                    : MemberwiseCloneMethod.Invoke(sidecar, null);
            }

            _field.SetValue(sidecar, value);
            _sidecarField.SetValue(updatedConfigurationData, sidecar);
            _configurationDataField.SetValue(target, updatedConfigurationData);
        }
    }

    private static bool IsJitConfigSidecarActive(Type sidecarType)
    {
        if (sidecarType == typeof(Compiler.DebugData.JitConfigValuesData))
        {
            return Configuration.DEBUG;
        }

        var name = sidecarType.Name;
        var flagName = name.StartsWith("Target", StringComparison.Ordinal)
            ? $"TARGET_{name["Target".Length..(name.Length - "Data".Length)].ToUpperInvariant()}"
            : name.StartsWith("Feature", StringComparison.Ordinal)
                ? $"FEATURE_{name["Feature".Length..(name.Length - "Data".Length)].ToUpperInvariant()}"
                : null;
        var property = flagName is null
            ? null
            : typeof(Configuration).GetProperty(
                flagName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return property?.GetValue(null) is true;
    }

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
        var field = GetJitConfigField("_jitUseScalableVectorT")
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
            compiler.dspMetrics = enabled;
        }
    }

    public static void SetDspCode(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            compiler.dspCode = enabled;
        }
    }

    public static void SetDspEHTable(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            compiler.dspEHTable = enabled;
        }
    }

    public static void SetCompEnablePCRelAddr(Compiler compiler, bool enabled)
    {
        if (IsDebug)
        {
            Compiler.DebugData.Get(compiler).CompEnablePCRelAddr = enabled;
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

    public static void SetCodeGenVerbose(CodeGen codeGen, bool enabled)
    {
        if (IsDebug)
        {
            codeGen.Verbose = enabled;
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
