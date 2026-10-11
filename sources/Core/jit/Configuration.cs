using System;

namespace RyuJitSharp;

internal static class Configuration
{
    internal static bool FEATURE_HW_INTRINSICS => GetFeatureSwitch(nameof(FEATURE_HW_INTRINSICS));

    internal static bool FEATURE_MASKED_HW_INTRINSICS => GetFeatureSwitch(nameof(FEATURE_MASKED_HW_INTRINSICS));

    internal static bool FEATURE_READYTORUN => GetFeatureSwitch(nameof(FEATURE_READYTORUN));

    internal static bool FEATURE_HAS_ZERO_REG => GetFeatureSwitch(nameof(FEATURE_HAS_ZERO_REG));

    // Arm64 Windows uses a split ABI distinct from the usual Arm64 ABI.
    // ARM32, LoongArch64, and RISC-V64 also use split arguments.
    internal static bool FEATURE_ARG_SPLIT => TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64;

    internal static bool FEATURE_MULTIREG_ARGS =>
        FEATURE_ARG_SPLIT || (TARGET_AMD64 && TARGET_UNIX);

    internal static bool FEATURE_FIXED_OUT_ARGS =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_AMD64;

    internal static bool FEATURE_FASTTAILCALL =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM || TARGET_AMD64;

    internal static bool FEATURE_MULTIREG_RET => GetFeatureSwitch(nameof(FEATURE_MULTIREG_RET));

    internal static bool FEATURE_SIMD => GetFeatureSwitch(nameof(FEATURE_SIMD));

    internal static bool FEATURE_TAILCALL_OPT =>
        TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_AMD64;

    // Shared returns allow implicit tail calls through a call/jump/return-block pattern.
    internal static bool FEATURE_TAILCALL_OPT_SHARED_RETURN => FEATURE_TAILCALL_OPT;

    internal static bool HAS_FIXED_REGISTER_SET => !TARGET_WASM;

    internal static bool TARGET_64BIT => GetFeatureSwitch(nameof(TARGET_64BIT));

    internal static bool TARGET_ARM => GetFeatureSwitch(nameof(TARGET_ARM));

    internal static bool TARGET_ARM64 => GetFeatureSwitch(nameof(TARGET_ARM64));

    internal static bool TARGET_ARMARCH => GetFeatureSwitch(nameof(TARGET_ARMARCH));

    internal static bool TARGET_AMD64 => TARGET_XARCH && TARGET_64BIT;

    internal static bool TARGET_32BIT => !TARGET_64BIT;

    internal static bool TARGET_LOONGARCH64 => GetFeatureSwitch(nameof(TARGET_LOONGARCH64));

    internal static bool TARGET_RISCV64 => GetFeatureSwitch(nameof(TARGET_RISCV64));

    internal static bool TARGET_X86 => TARGET_XARCH && !TARGET_64BIT;

    internal static bool TARGET_XARCH => GetFeatureSwitch(nameof(TARGET_XARCH));

    internal static bool TARGET_WASM => GetFeatureSwitch(nameof(TARGET_WASM));

    internal static bool TARGET_WINDOWS => GetFeatureSwitch(nameof(TARGET_WINDOWS));

    internal static bool TARGET_UNIX => GetFeatureSwitch(nameof(TARGET_UNIX));

    internal static bool TARGET_APPLE => GetFeatureSwitch(nameof(TARGET_APPLE));

    internal static bool SWIFT_SUPPORT => TARGET_ARM64 || (TARGET_AMD64 && TARGET_UNIX);

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
