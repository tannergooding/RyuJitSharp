// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class RegNumberIdentityTests
{
    private static readonly string[] s_targetSuffixes =
    [
        "_X86",
        "_AMD64",
        "_ARM",
        "_ARM64",
        "_LOONGARCH64",
        "_RISCV64",
        "_WASM",
    ];

    [Test]
    public static void TargetQualifiedRegisterNumbersPreserveNativeOrdinalsAndAliases()
    {
        Assert.That(REG_XMM0_X86, Is.EqualTo((regNumber)8));
        Assert.That(REG_K0_X86, Is.EqualTo((regNumber)16));
        Assert.That(REG_RAX_X86, Is.EqualTo(REG_EAX_X86));
        Assert.That(REG_STK_X86, Is.EqualTo((regNumber)24));
        Assert.That(REG_COUNT_X86, Is.EqualTo((regNumber)25));
        Assert.That(REG_NA_X86, Is.EqualTo(REG_COUNT_X86));
        Assert.That(ACTUAL_REG_COUNT_X86, Is.EqualTo((regNumber)24));

        Assert.That(REG_R8_AMD64, Is.EqualTo((regNumber)8));
        Assert.That(REG_XMM0_AMD64, Is.EqualTo((regNumber)32));
        Assert.That(REG_K0_AMD64, Is.EqualTo((regNumber)64));
        Assert.That(REG_EAX_AMD64, Is.EqualTo(REG_RAX_AMD64));
        Assert.That(REG_STK_AMD64, Is.EqualTo((regNumber)72));
        Assert.That(REG_COUNT_AMD64, Is.EqualTo((regNumber)73));
        Assert.That(REG_NA_AMD64, Is.EqualTo(REG_COUNT_AMD64));
        Assert.That(ACTUAL_REG_COUNT_AMD64, Is.EqualTo((regNumber)72));

        Assert.That(REG_SP_ARM, Is.EqualTo((regNumber)13));
        Assert.That(REG_FP_ARM, Is.EqualTo(REG_R11_ARM));
        Assert.That(REG_F0_ARM, Is.EqualTo((regNumber)16));
        Assert.That(REG_STK_ARM, Is.EqualTo((regNumber)48));
        Assert.That(REG_COUNT_ARM, Is.EqualTo((regNumber)49));
        Assert.That(REG_NA_ARM, Is.EqualTo(REG_COUNT_ARM));
        Assert.That(ACTUAL_REG_COUNT_ARM, Is.EqualTo((regNumber)48));

        Assert.That(REG_FP_ARM64, Is.EqualTo((regNumber)29));
        Assert.That(REG_R29_ARM64, Is.EqualTo(REG_FP_ARM64));
        Assert.That(REG_P0_ARM64, Is.EqualTo((regNumber)64));
        Assert.That(REG_SP_ARM64, Is.EqualTo((regNumber)80));
        Assert.That(REG_FFR_ARM64, Is.EqualTo((regNumber)81));
        Assert.That(REG_STK_ARM64, Is.EqualTo((regNumber)82));
        Assert.That(REG_COUNT_ARM64, Is.EqualTo((regNumber)83));
        Assert.That(REG_NA_ARM64, Is.EqualTo(REG_COUNT_ARM64));
        Assert.That(ACTUAL_REG_COUNT_ARM64, Is.EqualTo((regNumber)82));

        Assert.That(REG_SP_LOONGARCH64, Is.EqualTo((regNumber)3));
        Assert.That(REG_R21_LOONGARCH64, Is.EqualTo(REG_X0_LOONGARCH64));
        Assert.That(REG_F0_LOONGARCH64, Is.EqualTo((regNumber)32));
        Assert.That(REG_STK_LOONGARCH64, Is.EqualTo((regNumber)64));
        Assert.That(REG_COUNT_LOONGARCH64, Is.EqualTo((regNumber)65));
        Assert.That(REG_NA_LOONGARCH64, Is.EqualTo(REG_COUNT_LOONGARCH64));
        Assert.That(ACTUAL_REG_COUNT_LOONGARCH64, Is.EqualTo((regNumber)64));

        Assert.That(REG_SP_RISCV64, Is.EqualTo((regNumber)2));
        Assert.That(REG_R8_RISCV64, Is.EqualTo(REG_FP_RISCV64));
        Assert.That(REG_ZERO_RISCV64, Is.EqualTo(REG_R0_RISCV64));
        Assert.That(REG_FT0_RISCV64, Is.EqualTo((regNumber)32));
        Assert.That(REG_STK_RISCV64, Is.EqualTo((regNumber)64));
        Assert.That(REG_COUNT_RISCV64, Is.EqualTo((regNumber)65));
        Assert.That(REG_NA_RISCV64, Is.EqualTo(REG_COUNT_RISCV64));
        Assert.That(ACTUAL_REG_COUNT_RISCV64, Is.EqualTo((regNumber)64));

        Assert.That(REG_STK_WASM, Is.EqualTo((regNumber)1));
        Assert.That(REG_COUNT_WASM, Is.EqualTo((regNumber)2));
        Assert.That(REG_NA_WASM, Is.EqualTo(REG_COUNT_WASM));
        Assert.That(ACTUAL_REG_COUNT_WASM, Is.EqualTo((regNumber)1));
    }

    [Test]
    public static void TargetQualifiedRegisterNumbersMatchTheSelectedNativeRegisterSet()
    {
        var target = GetTargetSuffix();
        var names = Enum.GetNames<regNumber>();

        foreach (var name in names)
        {
            var isTargetQualified = false;
            foreach (var suffix in s_targetSuffixes)
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    isTargetQualified = true;
                    break;
                }
            }

            if (isTargetQualified)
            {
                continue;
            }

            var targetQualifiedName = $"{name}_{target}";
            if (Array.IndexOf(names, targetQualifiedName) is -1)
            {
                if ((name is "REG_FFR") && (target is not "ARM64"))
                {
                    continue;
                }

                Assert.Fail($"Missing target-qualified register identity '{targetQualifiedName}'.");
                continue;
            }

            Assert.That(
                Enum.Parse<regNumber>(targetQualifiedName),
                Is.EqualTo(Enum.Parse<regNumber>(name)),
                targetQualifiedName);
        }
    }

    private static string GetTargetSuffix()
    {
        if (Configuration.TARGET_X86)
        {
            return "X86";
        }

        if (Configuration.TARGET_AMD64)
        {
            return "AMD64";
        }

        if (Configuration.TARGET_ARM)
        {
            return "ARM";
        }

        if (Configuration.TARGET_ARM64)
        {
            return "ARM64";
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return "LOONGARCH64";
        }

        if (Configuration.TARGET_RISCV64)
        {
            return "RISCV64";
        }

        if (Configuration.TARGET_WASM)
        {
            return "WASM";
        }

        throw new InvalidOperationException("Unsupported target architecture.");
    }
}
