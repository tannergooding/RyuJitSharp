// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.IO;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

#if TARGET_X86 || TARGET_AMD64 || TARGET_ARM || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
internal static class RegisterMaskDisplayTests
{
    [Test]
    [NonParallelizable]
    public static void DspRegMaskPreservesTargetRegisterRangeFormatting()
    {
#if TARGET_X86
        var mask = new regMaskTP(regMask.SRBM_EAX | regMask.SRBM_ECX);
        var expected = "[eax ecx]";
#elif TARGET_AMD64
        var mask = new regMaskTP(regMask.SRBM_R8 | regMask.SRBM_R9);
        var expected = "[r8-r9]";
#elif TARGET_ARM
        var mask = new regMaskTP(regMask.SRBM_R0 | regMask.SRBM_R1);
        var expected = "[r0-r1]";
#elif TARGET_ARM64
        var mask = new regMaskTP(regMask.SRBM_R19 | regMask.SRBM_R20);
        var expected = "[x19-x20]";
#elif TARGET_LOONGARCH64
        var mask = new regMaskTP(regMask.SRBM_A0 | regMask.SRBM_A1);
        var expected = "[a0-a1]";
#elif TARGET_RISCV64
        var mask = new regMaskTP(regMask.SRBM_A0 | regMask.SRBM_A1);
        var expected = "[a0-a1]";
#else
        var mask = default(regMaskTP);
        var expected = "[]";
#endif

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

        Assert.That(REG_NA.Name, Is.EqualTo("NA"));
        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected));
    }
}
#endif
