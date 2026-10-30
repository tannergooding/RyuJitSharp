// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static class RegMaskIdentityTests
{
    private static readonly string[] s_targetIdentities =
    [
        "X86",
        "X64",
        "ARM32",
        "ARM64",
        "LOONGARCH64",
        "RISCV64",
        "WASM",
    ];

    [Test]
    public static void NoneAndStackMasksHaveSingleUniversalIdentities()
    {
        var names = Enum.GetNames<regMask>();

        Assert.That(names, Does.Contain(nameof(SRBM_NONE)));
        Assert.That(names, Does.Contain(nameof(SRBM_STK)));

        foreach (var target in s_targetIdentities)
        {
            Assert.That(names, Does.Not.Contain($"SRBM_{target}_NONE"));
            Assert.That(names, Does.Not.Contain($"SRBM_{target}_STK"));
        }
    }

    [Test]
    public static void RegisterMaskIdentitiesUseUniversal64BitStorage()
    {
        Assert.That(Unsafe.SizeOf<regMask>(), Is.EqualTo(sizeof(long)));
        Assert.That(SRBM_X64_XMM31, Is.EqualTo((regMask)long.MinValue));
        Assert.That(SRBM_ARM64_V31, Is.EqualTo((regMask)long.MinValue));
        Assert.That(SRBM_STK, Is.EqualTo((regMask)0));
    }

    [Test]
    public static void XarchRegisterMaskPropertiesSelectTheConfiguredTarget()
    {
        Assume.That(Configuration.TARGET_X86 || Configuration.TARGET_AMD64);
        var expectedRbx = Configuration.TARGET_AMD64 ? Globals.RBM_X64_RBX : Globals.RBM_X86_RBX;
        var expectedRcx = Configuration.TARGET_AMD64 ? Globals.RBM_X64_RCX : Globals.RBM_X86_RCX;

        Assert.That(TestConfiguration.XarchRbxMask, Is.EqualTo(expectedRbx));
        Assert.That(TestConfiguration.XarchRcxMask, Is.EqualTo(expectedRcx));
    }

    [Test]
    public static void TargetQualifiedTablesAndFactoriesMatchTheCurrentRegisterSet()
    {
        var target = GetTargetIdentity();
        var targetPrefix = $"SRBM_{target}_";
        var checkedEntries = 0;

        foreach (var maskName in Enum.GetNames<regMask>())
        {
            if (!maskName.StartsWith(targetPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var registerName = $"REG_{target}_{maskName[targetPrefix.Length..]}";
            var register = Enum.Parse<regNumber>(registerName);
            var scalarMask = Enum.Parse<regMask>(maskName);
            var expectedFullMask = Configuration.HAS_MORE_THAN_64_REGISTERS && ((int)register >= 64)
                ? new regMaskTP(SRBM_NONE, scalarMask)
                : new regMaskTP(scalarMask);
            var factoryName = $"RBM_{target}_{maskName[targetPrefix.Length..]}";
            var factory = typeof(Globals).GetProperty(factoryName);

            Assert.That(register.SingleTypeMask, Is.EqualTo(scalarMask), registerName);
            Assert.That(Globals.genRegMask(register), Is.EqualTo(expectedFullMask), registerName);
            Assert.That(factory?.GetValue(null), Is.EqualTo(expectedFullMask), factoryName);
            checkedEntries++;
        }

        if (Configuration.TARGET_WASM)
        {
            var expectedStackMask = new regMaskTP(SRBM_STK);
            Assert.That(checkedEntries, Is.Zero);
            Assert.That(regNumber.REG_WASM_STK.SingleTypeMask, Is.EqualTo(SRBM_STK));
            Assert.That(Globals.genRegMask(regNumber.REG_WASM_STK), Is.EqualTo(expectedStackMask));
            Assert.That(Globals.RBM_WASM_STK, Is.EqualTo(expectedStackMask));
        }
        else
        {
            Assert.That(checkedEntries, Is.GreaterThan(0));
        }
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
