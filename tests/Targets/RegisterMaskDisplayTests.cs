// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.IO;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class RegisterMaskDisplayTests
{
    [Test]
    [NonParallelizable]
    public static void DspRegMaskPreservesTargetRegisterRangeFormatting()
    {
        regMaskTP mask;
        string expected;
        regNumber notAvailableRegister;
        if (Configuration.TARGET_X86)
        {
            mask = new regMaskTP(regMask.SRBM_X86_EAX | regMask.SRBM_X86_ECX);
            expected = "[eax ecx]";
            notAvailableRegister = REG_X86_NA;
        }
        else if (Configuration.TARGET_AMD64)
        {
            mask = new regMaskTP(regMask.SRBM_X64_R8 | regMask.SRBM_X64_R9);
            expected = "[r8-r9]";
            notAvailableRegister = REG_X64_NA;
        }
        else if (Configuration.TARGET_ARM)
        {
            mask = new regMaskTP(regMask.SRBM_ARM32_R0 | regMask.SRBM_ARM32_R1);
            expected = "[r0-r1]";
            notAvailableRegister = REG_ARM32_NA;
        }
        else if (Configuration.TARGET_ARM64)
        {
            mask = new regMaskTP(regMask.SRBM_ARM64_R19 | regMask.SRBM_ARM64_R20);
            expected = "[x19-x20]";
            notAvailableRegister = REG_ARM64_NA;
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            mask = new regMaskTP(regMask.SRBM_LOONGARCH64_A0 | regMask.SRBM_LOONGARCH64_A1);
            expected = "[a0-a1]";
            notAvailableRegister = REG_LOONGARCH64_NA;
        }
        else if (Configuration.TARGET_RISCV64)
        {
            mask = new regMaskTP(regMask.SRBM_RISCV64_A0 | regMask.SRBM_RISCV64_A1);
            expected = "[a0-a1]";
            notAvailableRegister = REG_RISCV64_NA;
        }
        else if (Configuration.TARGET_WASM)
        {
            mask = default;
            expected = "[]";
            notAvailableRegister = REG_WASM_NA;
        }
        else
        {
            throw new FatalJitException("Register mask formatting is not defined for this target.");
        }

        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previousWriter = Globals.s_jitstdout;

        try
        {
            Globals.s_jitstdout = writer;
            Globals.dspRegMask(mask);
            writer.Flush();
        }
        finally
        {
            Globals.s_jitstdout = previousWriter;
        }

        Assert.That(notAvailableRegister.Name, Is.EqualTo("NA"));
        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
    }
}
