using System;

namespace RyuJitSharp;

internal static class Configuration
{
    internal static bool FEATURE_HW_INTRINSICS => GetFeatureSwitch(nameof(FEATURE_HW_INTRINSICS));

    internal static bool FEATURE_HAS_ZERO_REG => GetFeatureSwitch(nameof(FEATURE_HAS_ZERO_REG));

    internal static bool FEATURE_SIMD => GetFeatureSwitch(nameof(FEATURE_SIMD));

    internal static bool HAS_FIXED_REGISTER_SET => !TARGET_WASM;

    internal static bool TARGET_64BIT => GetFeatureSwitch(nameof(TARGET_64BIT));

    internal static bool TARGET_ARM => GetFeatureSwitch(nameof(TARGET_ARM));

    internal static bool TARGET_ARM64 => GetFeatureSwitch(nameof(TARGET_ARM64));

    internal static bool TARGET_ARMARCH => GetFeatureSwitch(nameof(TARGET_ARMARCH));

    internal static bool TARGET_AMD64 => TARGET_XARCH && TARGET_64BIT;

    internal static bool TARGET_LOONGARCH64 => GetFeatureSwitch(nameof(TARGET_LOONGARCH64));

    internal static bool TARGET_RISCV64 => GetFeatureSwitch(nameof(TARGET_RISCV64));

    internal static bool TARGET_X86 => TARGET_XARCH && !TARGET_64BIT;

    internal static bool TARGET_XARCH => GetFeatureSwitch(nameof(TARGET_XARCH));

    internal static bool TARGET_WASM => GetFeatureSwitch(nameof(TARGET_WASM));

    internal static bool TARGET_WINDOWS => GetFeatureSwitch(nameof(TARGET_WINDOWS));

    internal static bool TARGET_UNIX => GetFeatureSwitch(nameof(TARGET_UNIX));

    internal static bool TARGET_APPLE => GetFeatureSwitch(nameof(TARGET_APPLE));

    internal static bool TARGET_OS_RUNTIMEDETERMINED => GetFeatureSwitch(nameof(TARGET_OS_RUNTIMEDETERMINED));

    internal static bool TARGET_UNIX_OS_RUNTIMEDETERMINED => GetFeatureSwitch(nameof(TARGET_UNIX_OS_RUNTIMEDETERMINED));

    internal static bool UNIX_X86_ABI => GetFeatureSwitch(nameof(UNIX_X86_ABI));

    private static bool GetFeatureSwitch(string featureName)
    {
        var switchName = $"RyuJitSharp.{featureName}";
        if (AppContext.TryGetSwitch(switchName, out var isEnabled))
        {
            return isEnabled;
        }

        throw new InvalidOperationException($"Required compiler configuration switch '{switchName}' is not set.");
    }
}
