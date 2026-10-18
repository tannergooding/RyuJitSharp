// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class RiscV64UnwindTests
{
    [TestCase(0x00, 1)]
    [TestCase(0x40, 2)]
    [TestCase(0xC8, 3)]
    [TestCase(0xE0, 4)]
    public static void DiagnosticInstructionSizesMatchRiscVEncoding(byte header, int codeSize)
    {
        Assume.That(Configuration.TARGET_RISCV64);
        var codes = stackalloc byte[5];
        codes[0] = header;
        codes[codeSize] = 0xE4;

        var source = new DiagnosticCodes(codes);
        if (!TestConfiguration.IsDebug)
        {
            _ = Assert.Throws<InvalidOperationException>(() => source.GetCodeSizeFromUnwindCodes(true));
            return;
        }

        Assert.That(source.GetCodeSizeFromUnwindCodes(true), Is.EqualTo(4));
    }

    private sealed class DiagnosticCodes(byte* codes) : RiscV64UnwindCodesBase
    {
        public override void AddCode(byte b1)
        {
            throw new NotSupportedException();
        }

        public override void AddCode(byte b1, byte b2)
        {
            throw new NotSupportedException();
        }

        public override void AddCode(byte b1, byte b2, byte b3)
        {
            throw new NotSupportedException();
        }

        public override void AddCode(byte b1, byte b2, byte b3, byte b4)
        {
            throw new NotSupportedException();
        }

        public override byte* GetCodes()
        {
            return codes;
        }
    }
}
