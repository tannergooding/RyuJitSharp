using System;

namespace RyuJitSharp;

internal static class Configuration
{
    internal static bool FEATURE_HAS_ZERO_REG => GetFeatureSwitch(nameof(FEATURE_HAS_ZERO_REG));

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
