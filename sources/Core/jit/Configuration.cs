using System;

namespace RyuJitSharp;

internal static class Configuration
{
    internal static bool FEATURE_HAS_ZERO_REG => GetFeatureSwitch(nameof(FEATURE_HAS_ZERO_REG));

    internal static bool TARGET_64BIT => GetFeatureSwitch(nameof(TARGET_64BIT));

    internal static bool TARGET_ARM => GetFeatureSwitch(nameof(TARGET_ARM));

    internal static bool TARGET_ARM64 => GetFeatureSwitch(nameof(TARGET_ARM64));

    internal static bool TARGET_ARMARCH => GetFeatureSwitch(nameof(TARGET_ARMARCH));

    internal static bool TARGET_LOONGARCH64 => GetFeatureSwitch(nameof(TARGET_LOONGARCH64));

    internal static bool TARGET_RISCV64 => GetFeatureSwitch(nameof(TARGET_RISCV64));

    internal static bool TARGET_XARCH => GetFeatureSwitch(nameof(TARGET_XARCH));

    internal static bool TARGET_WASM => GetFeatureSwitch(nameof(TARGET_WASM));

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
