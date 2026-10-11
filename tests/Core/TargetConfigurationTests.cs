using System;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class TargetConfigurationTests
{
    [Test]
    public static void RuntimeConfigurationMatchesTargetArchitectureAndOS()
    {
        Assert.That(TargetArchitecture.Is64Bit, Is.EqualTo(Configuration.TARGET_64BIT));
        Assert.That(TargetArchitecture.IsWasm, Is.EqualTo(Configuration.TARGET_WASM));
        Assert.That(TargetArchitecture.IsX86, Is.EqualTo(Configuration.TARGET_X86));
        Assert.That(TargetArchitecture.IsX64, Is.EqualTo(Configuration.TARGET_AMD64));
        Assert.That(TargetArchitecture.IsArm32, Is.EqualTo(Configuration.TARGET_ARM));
        Assert.That(TargetArchitecture.IsArm64, Is.EqualTo(Configuration.TARGET_ARM64));
        Assert.That(TargetArchitecture.IsArmArch, Is.EqualTo(Configuration.TARGET_ARMARCH));
        Assert.That(TargetArchitecture.IsLoongArch64, Is.EqualTo(Configuration.TARGET_LOONGARCH64));
        Assert.That(TargetArchitecture.IsRiscV64, Is.EqualTo(Configuration.TARGET_RISCV64));

        var expectedCpuName = Configuration.TARGET_X86 ? "x86"
            : Configuration.TARGET_AMD64 ? "x64"
            : Configuration.TARGET_ARM ? "arm"
            : Configuration.TARGET_ARM64 ? "arm64"
            : Configuration.TARGET_LOONGARCH64 ? "loongarch64"
            : Configuration.TARGET_RISCV64 ? "riscv64"
            : Configuration.TARGET_WASM && !Configuration.TARGET_64BIT ? "wasm32"
            : throw new InvalidOperationException("Unsupported target architecture.");
        Assert.That(Target.TgtCpuName, Is.EqualTo(expectedCpuName));
        Assert.That(Target.TgtArgOrder, Is.EqualTo(
            Configuration.TARGET_X86 ? Target.ARG_ORDER_L2R : Target.ARG_ORDER_R2L));
        Assert.That(Target.TgtUnmanagedArgOrder, Is.EqualTo(Target.ARG_ORDER_R2L));

        var configuredArchitectures =
            (TargetArchitecture.IsX86 ? 1 : 0)
            + (TargetArchitecture.IsX64 ? 1 : 0)
            + (TargetArchitecture.IsArm32 ? 1 : 0)
            + (TargetArchitecture.IsArm64 ? 1 : 0)
            + (TargetArchitecture.IsLoongArch64 ? 1 : 0)
            + (TargetArchitecture.IsRiscV64 ? 1 : 0)
            + (TargetArchitecture.IsWasm ? 1 : 0);
        Assert.That(configuredArchitectures, Is.EqualTo(1));

        Assert.That(TargetOS.IsWindows, Is.EqualTo(Configuration.TARGET_WINDOWS));
        Assert.That(TargetOS.IsUnix, Is.EqualTo(Configuration.TARGET_UNIX));
        Assert.That(TargetOS.IsApplePlatform, Is.EqualTo(Configuration.TARGET_APPLE));
    }
}
