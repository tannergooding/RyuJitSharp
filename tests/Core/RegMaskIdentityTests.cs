// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static class RegMaskIdentityTests
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
    public static void TargetQualifiedRegisterMasksMatchTheSelectedNativeRegisterSet()
    {
        var target = GetTargetSuffix();
        var names = Enum.GetNames<regMask>();

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
                Assert.Fail($"Missing target-qualified register-mask identity '{targetQualifiedName}'.");
                continue;
            }

            Assert.That(
                Enum.Parse<regMask>(targetQualifiedName),
                Is.EqualTo(Enum.Parse<regMask>(name)),
                targetQualifiedName);
        }
    }

    [Test]
    public static void RegisterMaskIdentitiesUseUniversal64BitStorage()
    {
        Assert.That(Unsafe.SizeOf<regMask>(), Is.EqualTo(sizeof(long)));
        Assert.That(SRBM_XMM31_AMD64, Is.EqualTo((regMask)long.MinValue));
        Assert.That(SRBM_V31_ARM64, Is.EqualTo((regMask)long.MinValue));
        Assert.That(SRBM_STK_WASM, Is.EqualTo((regMask)0));
    }

    [Test]
    public static void TargetQualifiedTablesAndFactoriesMatchTheCurrentRegisterSet()
    {
        var suffix = $"_{GetTargetSuffix()}";
        var checkedEntries = 0;

        foreach (var maskName in Enum.GetNames<regMask>())
        {
            if (!maskName.StartsWith("SRBM_", StringComparison.Ordinal) ||
                !maskName.EndsWith(suffix, StringComparison.Ordinal) ||
                maskName.StartsWith("SRBM_NONE_", StringComparison.Ordinal))
            {
                continue;
            }

            var registerName = $"REG_{maskName[5..^suffix.Length]}{suffix}";
            var register = Enum.Parse<regNumber>(registerName);
            var scalarMask = Enum.Parse<regMask>(maskName);
            var expectedFullMask = Configuration.HAS_MORE_THAN_64_REGISTERS && ((int)register >= 64)
                ? new regMaskTP(SRBM_NONE, scalarMask)
                : new regMaskTP(scalarMask);
            var factoryName = $"RBM_{registerName[4..^suffix.Length]}";
            var factory = typeof(Globals).GetProperty(factoryName);

            Assert.That(register.SingleTypeMask, Is.EqualTo(scalarMask), registerName);
            Assert.That(Globals.genRegMask(register), Is.EqualTo(expectedFullMask), registerName);
            Assert.That(factory?.GetValue(null), Is.EqualTo(expectedFullMask), factoryName);
            checkedEntries++;
        }

        Assert.That(checkedEntries, Is.GreaterThan(0));
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
