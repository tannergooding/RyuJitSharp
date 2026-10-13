// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class RegNumberNameTests
{
    [Test]
    public static void RegisterNamesUseRuntimeTargetConfiguration()
    {
        Assert.That(Globals.REG_NA.Name, Is.EqualTo("NA"));

        if (Configuration.TARGET_AMD64)
        {
            Assert.That(REG_X64_RAX.Name, Is.EqualTo("rax"));
            Assert.That(REG_X64_XMM0.Name, Is.EqualTo("mm0"));
            Assert.That(REG_X64_K0.Name, Is.EqualTo("k0"));
            Assert.That(REG_X64_STK.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_X86)
        {
            Assert.That(REG_X86_EAX.Name, Is.EqualTo("eax"));
            Assert.That(REG_X86_XMM0.Name, Is.EqualTo("mm0"));
            Assert.That(REG_X86_K0.Name, Is.EqualTo("k0"));
            Assert.That(REG_X86_STK.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_ARM64)
        {
            Assert.That(REG_ARM64_R0.Name, Is.EqualTo("x0"));
            Assert.That(REG_ARM64_V0.Name, Is.EqualTo("d0"));
            Assert.That(REG_ARM64_P0.Name, Is.EqualTo("p0"));
            Assert.That(REG_ARM64_FFR.Name, Is.EqualTo("ffr"));
            Assert.That(REG_ARM64_STK.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_ARM)
        {
            Assert.That(REG_ARM32_R0.Name, Is.EqualTo("r0"));
            Assert.That(REG_ARM32_F0.Name, Is.EqualTo("f0"));
            Assert.That(REG_ARM32_STK.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            Assert.That(REG_LOONGARCH64_A0.Name, Is.EqualTo("a0"));
            Assert.That(REG_LOONGARCH64_F0.Name, Is.EqualTo("f0"));
            Assert.That(REG_LOONGARCH64_STK.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_RISCV64)
        {
            Assert.That(REG_RISCV64_SP.Name, Is.EqualTo("sp"));
            Assert.That(REG_RISCV64_FT0.Name, Is.EqualTo("ft0"));
            Assert.That(REG_RISCV64_STK.Name, Is.EqualTo("STK"));
        }
        else if (Configuration.TARGET_WASM)
        {
            Assert.That(((regNumber)0x20000011u).Name, Is.EqualTo("$17"));
            Assert.That(((regNumber)0x20000014u).Name, Is.EqualTo("$<too large to print>"));
            Assert.That(regNumberExtensions.GetWasmRegName(REG_WASM_NA), Is.EqualTo("NA"));
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
            Assert.That(regNumberExtensions.GetWasmRegName(REG_WASM_NA), Is.EqualTo("NA"));
        }
        else
        {
            _ = Assert.Throws<FatalJitException>(() => regNumberExtensions.GetWasmRegName((regNumber)0));
        }
    }
}
