// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class RegNumberIdentityTests
{
    [Test]
    public static void TargetQualifiedRegisterNumbersPreserveNativeOrdinalsAndAliases()
    {
        Assert.That(REG_X86_XMM0, Is.EqualTo((regNumber)8));
        Assert.That(REG_X86_K0, Is.EqualTo((regNumber)16));
        Assert.That(REG_X86_RAX, Is.EqualTo(REG_X86_EAX));
        Assert.That(REG_X86_STK, Is.EqualTo((regNumber)24));
        Assert.That(REG_X86_COUNT, Is.EqualTo((regNumber)25));
        Assert.That(REG_X86_NA, Is.EqualTo(REG_X86_COUNT));
        Assert.That(ACTUAL_REG_X86_COUNT, Is.EqualTo((regNumber)24));

        Assert.That(REG_X64_R8, Is.EqualTo((regNumber)8));
        Assert.That(REG_X64_XMM0, Is.EqualTo((regNumber)32));
        Assert.That(REG_X64_K0, Is.EqualTo((regNumber)64));
        Assert.That(REG_X64_EAX, Is.EqualTo(REG_X64_RAX));
        Assert.That(REG_X64_STK, Is.EqualTo((regNumber)72));
        Assert.That(REG_X64_COUNT, Is.EqualTo((regNumber)73));
        Assert.That(REG_X64_NA, Is.EqualTo(REG_X64_COUNT));
        Assert.That(ACTUAL_REG_X64_COUNT, Is.EqualTo((regNumber)72));

        Assert.That(REG_ARM32_SP, Is.EqualTo((regNumber)13));
        Assert.That(REG_ARM32_FP, Is.EqualTo(REG_ARM32_R11));
        Assert.That(REG_ARM32_F0, Is.EqualTo((regNumber)16));
        Assert.That(REG_ARM32_STK, Is.EqualTo((regNumber)48));
        Assert.That(REG_ARM32_COUNT, Is.EqualTo((regNumber)49));
        Assert.That(REG_ARM32_NA, Is.EqualTo(REG_ARM32_COUNT));
        Assert.That(ACTUAL_REG_ARM32_COUNT, Is.EqualTo((regNumber)48));

        Assert.That(REG_ARM64_FP, Is.EqualTo((regNumber)29));
        Assert.That(REG_ARM64_R29, Is.EqualTo(REG_ARM64_FP));
        Assert.That(REG_ARM64_P0, Is.EqualTo((regNumber)64));
        Assert.That(REG_ARM64_SP, Is.EqualTo((regNumber)80));
        Assert.That(REG_ARM64_FFR, Is.EqualTo((regNumber)81));
        Assert.That(REG_ARM64_STK, Is.EqualTo((regNumber)82));
        Assert.That(REG_ARM64_COUNT, Is.EqualTo((regNumber)83));
        Assert.That(REG_ARM64_NA, Is.EqualTo(REG_ARM64_COUNT));
        Assert.That(ACTUAL_REG_ARM64_COUNT, Is.EqualTo((regNumber)82));

        Assert.That(REG_LOONGARCH64_SP, Is.EqualTo((regNumber)3));
        Assert.That(REG_LOONGARCH64_R21, Is.EqualTo(REG_LOONGARCH64_X0));
        Assert.That(REG_LOONGARCH64_F0, Is.EqualTo((regNumber)32));
        Assert.That(REG_LOONGARCH64_STK, Is.EqualTo((regNumber)64));
        Assert.That(REG_LOONGARCH64_COUNT, Is.EqualTo((regNumber)65));
        Assert.That(REG_LOONGARCH64_NA, Is.EqualTo(REG_LOONGARCH64_COUNT));
        Assert.That(ACTUAL_REG_LOONGARCH64_COUNT, Is.EqualTo((regNumber)64));

        Assert.That(REG_RISCV64_SP, Is.EqualTo((regNumber)2));
        Assert.That(REG_RISCV64_R8, Is.EqualTo(REG_RISCV64_FP));
        Assert.That(REG_RISCV64_ZERO, Is.EqualTo(REG_RISCV64_R0));
        Assert.That(REG_RISCV64_FT0, Is.EqualTo((regNumber)32));
        Assert.That(REG_RISCV64_STK, Is.EqualTo((regNumber)64));
        Assert.That(REG_RISCV64_COUNT, Is.EqualTo((regNumber)65));
        Assert.That(REG_RISCV64_NA, Is.EqualTo(REG_RISCV64_COUNT));
        Assert.That(ACTUAL_REG_RISCV64_COUNT, Is.EqualTo((regNumber)64));

        Assert.That(REG_WASM_STK, Is.EqualTo((regNumber)1));
        Assert.That(REG_WASM_COUNT, Is.EqualTo((regNumber)2));
        Assert.That(REG_WASM_NA, Is.EqualTo(REG_WASM_COUNT));
        Assert.That(ACTUAL_REG_WASM_COUNT, Is.EqualTo((regNumber)1));
    }

    [Test]
    public static void TargetQualifiedRegisterNumbersMatchTheSelectedNativeRegisterSet()
    {
        var target = GetTargetIdentity();
        var names = Enum.GetNames<regNumber>();
        var targetPrefix = $"REG_{target}_";
        var targetStackRegisterName = $"{targetPrefix}STK";
        var selectedRegisterCount = 0;

        foreach (var name in names)
        {
            if (name.StartsWith(targetPrefix, StringComparison.Ordinal))
            {
                selectedRegisterCount++;
            }
        }

        Assert.That(selectedRegisterCount, Is.GreaterThan(0));
        Assert.That(names, Does.Contain(targetStackRegisterName));
        Assert.That(
            Enum.Parse<regNumber>(targetStackRegisterName),
            Is.EqualTo(target switch
            {
                "X86" => REG_X86_STK,
                "X64" => REG_X64_STK,
                "ARM32" => REG_ARM32_STK,
                "ARM64" => REG_ARM64_STK,
                "LOONGARCH64" => REG_LOONGARCH64_STK,
                "RISCV64" => REG_RISCV64_STK,
                "WASM" => REG_WASM_STK,
                _ => throw new InvalidOperationException("Unsupported target architecture."),
            }));
    }

    private static string GetTargetIdentity()
    {
        if (Configuration.TARGET_X86)
        {
            return "X86";
        }

        if (Configuration.TARGET_AMD64)
        {
            return "X64";
        }

        if (Configuration.TARGET_ARM)
        {
            return "ARM32";
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
