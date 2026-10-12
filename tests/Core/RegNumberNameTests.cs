// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class RegNumberNameTests
{
    [Test]
    public static void RegisterNamesUseRuntimeTargetConfiguration()
    {
        Assert.That(REG_NA.Name, Is.EqualTo("NA"));

        if (Configuration.TARGET_AMD64)
        {
            Assert.That(REG_RAX_AMD64.Name, Is.EqualTo("rax"));
            Assert.That(REG_XMM0_AMD64.Name, Is.EqualTo("mm0"));
            Assert.That(REG_K0_AMD64.Name, Is.EqualTo("k0"));
            Assert.That(REG_STK_AMD64.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_X86)
        {
            Assert.That(REG_EAX_X86.Name, Is.EqualTo("eax"));
            Assert.That(REG_XMM0_X86.Name, Is.EqualTo("mm0"));
            Assert.That(REG_K0_X86.Name, Is.EqualTo("k0"));
            Assert.That(REG_STK_X86.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_ARM64)
        {
            Assert.That(REG_R0_ARM64.Name, Is.EqualTo("x0"));
            Assert.That(REG_V0_ARM64.Name, Is.EqualTo("d0"));
            Assert.That(REG_P0_ARM64.Name, Is.EqualTo("p0"));
            Assert.That(REG_FFR_ARM64.Name, Is.EqualTo("ffr"));
            Assert.That(REG_STK_ARM64.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_ARM)
        {
            Assert.That(REG_R0_ARM.Name, Is.EqualTo("r0"));
            Assert.That(REG_F0_ARM.Name, Is.EqualTo("f0"));
            Assert.That(REG_STK_ARM.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            Assert.That(REG_A0_LOONGARCH64.Name, Is.EqualTo("a0"));
            Assert.That(REG_F0_LOONGARCH64.Name, Is.EqualTo("f0"));
            Assert.That(REG_STK_LOONGARCH64.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_RISCV64)
        {
            Assert.That(REG_SP_RISCV64.Name, Is.EqualTo("sp"));
            Assert.That(REG_FT0_RISCV64.Name, Is.EqualTo("ft0"));
            Assert.That(REG_STK_RISCV64.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_WASM)
        {
            Assert.That(((regNumber)0x20000011u).Name, Is.EqualTo("$17"));
            Assert.That(((regNumber)0x20000014u).Name, Is.EqualTo("$<too large to print>"));
            Assert.That(regNumberExtensions.GetWasmRegName(REG_NA_WASM), Is.EqualTo("NA"));
        }
        else
        {
            Assert.Fail("Unsupported target architecture.");
        }
    }

    [Test]
    public static void WasmRegisterNameHelperRequiresWasmTarget()
    {
        if (Configuration.TARGET_WASM)
        {
            Assert.That(regNumberExtensions.GetWasmRegName(REG_NA_WASM), Is.EqualTo("NA"));
        }
        else
        {
            Assert.Throws<FatalJitException>(() => regNumberExtensions.GetWasmRegName((regNumber)0));
        }
    }
}
