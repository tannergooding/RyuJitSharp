extern alias RyuJitSharpCore;

using System;
using System.Globalization;
using System.Reflection;
using System.Xml;
using NUnit.Framework;
using CoreGlobals = RyuJitSharpCore::RyuJitSharp.Globals;
using CoreRegMask = RyuJitSharpCore::RyuJitSharp.regMask;
using CoreRegNumber = RyuJitSharpCore::RyuJitSharp.regNumber;
using TargetDefinition = RyuJitSharpCore::RyuJitSharp.Target;

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
        Assert.That(TargetDefinition.TgtCpuName, Is.EqualTo(expectedCpuName));
        Assert.That(TargetDefinition.TgtArgOrder, Is.EqualTo(
            Configuration.TARGET_X86 ? TargetDefinition.ARG_ORDER_L2R : TargetDefinition.ARG_ORDER_R2L));
        Assert.That(TargetDefinition.TgtUnmanagedArgOrder, Is.EqualTo(TargetDefinition.ARG_ORDER_R2L));

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

    [Test]
    public static void ExceptionObjectRegisterMappingPreservesTargetAndAbiAliases()
    {
        var expectedRegister = Configuration.TARGET_X86
            ? CoreRegNumber.REG_X86_EAX
            : Configuration.TARGET_AMD64
                ? Configuration.UNIX_AMD64_ABI ? CoreRegNumber.REG_X64_EDI : CoreRegNumber.REG_X64_ECX
                : Configuration.TARGET_ARM
                    ? CoreRegNumber.REG_ARM32_R0
                    : Configuration.TARGET_ARM64
                        ? CoreRegNumber.REG_ARM64_R0
                        : Configuration.TARGET_LOONGARCH64
                            ? CoreRegNumber.REG_LOONGARCH64_A0
                            : Configuration.TARGET_RISCV64
                                ? CoreRegNumber.REG_RISCV64_A0
                                : Configuration.TARGET_WASM
                                    ? CoreRegNumber.REG_WASM_NA
                                    : throw new InvalidOperationException("Unsupported target architecture.");
        var expectedMask = Configuration.TARGET_X86
            ? CoreRegMask.SRBM_X86_EAX
            : Configuration.TARGET_AMD64
                ? Configuration.UNIX_AMD64_ABI ? CoreRegMask.SRBM_X64_EDI : CoreRegMask.SRBM_X64_ECX
                : Configuration.TARGET_ARM
                    ? CoreRegMask.SRBM_ARM32_R0
                    : Configuration.TARGET_ARM64
                        ? CoreRegMask.SRBM_ARM64_R0
                        : Configuration.TARGET_LOONGARCH64
                            ? CoreRegMask.SRBM_LOONGARCH64_A0
                            : Configuration.TARGET_RISCV64
                                ? CoreRegMask.SRBM_RISCV64_A0
                                : Configuration.TARGET_WASM
                                    ? CoreRegMask.SRBM_NONE
                                    : throw new InvalidOperationException("Unsupported target architecture.");

        Assert.That(CoreGlobals.REG_EXCEPTION_OBJECT, Is.EqualTo(expectedRegister));
        Assert.That(CoreGlobals.SRBM_EXCEPTION_OBJECT, Is.EqualTo(expectedMask));
    }

    [Test]
    public static void EmbeddedConfigurationSubstitutionsCoverAllGetters()
    {
        var substitutionsStream =
            typeof(Configuration).Assembly.GetManifestResourceStream("ILLink.Substitutions.xml");
        Assert.That(substitutionsStream, Is.Not.Null);

        using (substitutionsStream!)
        {
            var substitutions = new XmlDocument();
            substitutions.Load(substitutionsStream!);

            foreach (var property in typeof(Configuration).GetProperties(BindingFlags.NonPublic | BindingFlags.Static))
            {
                var returnType = property.PropertyType == typeof(bool)
                    ? "System.Boolean"
                    : property.PropertyType == typeof(int)
                        ? "System.Int32"
                        : throw new InvalidOperationException($"Unsupported Configuration property type: {property.PropertyType}.");

                var signature = $"{returnType} get_{property.Name}()";
                var substitution = substitutions.SelectSingleNode($"//method[@signature='{signature}']");
                Assert.That(substitution, Is.Not.Null, signature);

                if (property.PropertyType == typeof(int))
                {
                    var substitutedValue = int.Parse(
                        substitution!.Attributes!["value"]!.Value,
                        CultureInfo.InvariantCulture);
                    Assert.That(substitutedValue, Is.EqualTo(property.GetValue(null)), signature);
                }
            }
        }
    }
}
