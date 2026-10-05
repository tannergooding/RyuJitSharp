// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64 && DEBUG
using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64UnwindDiagnosticsTests
{
    [Test]
    public static void HeaderSizeTableRetainsEveryOpcodeIncludingReservedEntries()
    {
        for (var opcode = 0; opcode < 256; opcode++)
        {
            var expected = opcode switch
            {
                >= 0xC0 and <= 0xDE => 2,
                0xE0 => 4,
                0xE2 => 2,
                _ => 1,
            };
            Assert.That(GetUnwindSizeFromUnwindHeader((byte)opcode), Is.EqualTo(expected));
        }
    }

    [TestCase(0xFFFFFFFFu, 27u, 5u, 31u)]
    [TestCase(0xFFFFFFFFu, 22u, 5u, 31u)]
    [TestCase(0xFFFFFFFFu, 0u, 18u, 0x3FFFFu)]
    [TestCase(0x80000000u, 31u, 1u, 1u)]
    [TestCase(0x80000000u, 0u, 31u, 0u)]
    [TestCase(0x12345678u, 16u, 8u, 0x34u)]
    public static void HeaderBitExtractionPreservesUnsignedFields(uint word, uint start, uint length, uint expected)
    {
        Assert.That(ExtractBits(word, start, length), Is.EqualTo(expected));
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void CodeSizeCountsInstructionsRatherThanEncodedBytes(bool isProlog)
    {
        byte[] codes = [0x01, 0xC0, 0x20, 0xE0, 0x01, 0x02, 0x03, 0xE2, 0x01, 0xE4];
        fixed (byte* pointer = codes)
        {
            var source = new DiagnosticCodes(pointer);

            Assert.That(source.GetCodeSizeFromUnwindCodes(isProlog), Is.EqualTo(16));
        }
    }

    [TestCase(new byte[] { 0xE4 }, 0u)]
    [TestCase(new byte[] { 0xE5, 0xE4 }, 4u)]
    [TestCase(new byte[] { 0xFD, 0xE4 }, 4u)]
    public static void EndCodeUsesTheExactArm64Sentinel(byte[] codes, uint expectedSize)
    {
        fixed (byte* pointer = codes)
        {
            var source = new DiagnosticCodes(pointer);

            Assert.That(source.GetCodeSizeFromUnwindCodes(false), Is.EqualTo(expectedSize));
        }
    }

    [TestCase(new byte[] { 0x1F }, "alloc_s #31 (0x1F); sub sp, sp, #496 (0x1F0)")]
    [TestCase(new byte[] { 0x3F }, "save_r19r20_x #31 (0x1F); stp x19, x20, [sp, #-248]!")]
    [TestCase(new byte[] { 0x7F }, "save_fplr #63 (0x3F); stp fp, lr, [sp, #504]")]
    [TestCase(new byte[] { 0xBF }, "save_fplr_x #63 (0x3F); stp fp, lr, [sp, #-512]!")]
    [TestCase(new byte[] { 0xC7, 0xFF }, "alloc_m #2047 (0x7FF); sub sp, sp, #32752 (0x7FF0)")]
    [TestCase(new byte[] { 0xC8, 0x3F }, "save_regp X#0 Z#63 (0x3F); stp x19, x20, [sp, #504]")]
    [TestCase(new byte[] { 0xCC, 0x3F }, "save_regp_x X#0 Z#63 (0x3F); stp x19, x20, [sp, #-512]!")]
    [TestCase(new byte[] { 0xD2, 0xFF }, "save_reg X#11 Z#63 (0x3F); str lr, [sp, #504]")]
    [TestCase(new byte[] { 0xD5, 0x7F }, "save_reg_x X#11 Z#31 (0x1F); str lr, [sp, #-256]!")]
    [TestCase(new byte[] { 0xD7, 0x3F }, "save_lrpair X#4 Z#63 (0x3F); stp x27, lr, [sp, #504]")]
    [TestCase(new byte[] { 0xD9, 0xBF }, "save_fregp X#6 Z#63 (0x3F); stp d14, d15, [sp, #504]")]
    [TestCase(new byte[] { 0xDB, 0xBF }, "save_fregp_x X#6 Z#63 (0x3F); stp d14, d15, [sp, #-512]!")]
    [TestCase(new byte[] { 0xDD, 0xFF }, "save_freg X#7 Z#63 (0x3F); str d15, [sp, #504]")]
    [TestCase(new byte[] { 0xDE, 0xFF }, "save_freg_x X#7 Z#31 (0x1F); str d15, [sp, #-256]!")]
    [TestCase(new byte[] { 0xE0, 0x01, 0x02, 0x03 }, "alloc_l 66051 (0x010203); sub sp, sp, #1056816 (102030)")]
    [TestCase(new byte[] { 0xE1 }, "set_fp; mov")]
    [TestCase(new byte[] { 0xE2, 0xFF }, "add_fp 255 (0xFF); add")]
    [TestCase(new byte[] { 0xE3 }, "nop")]
    [TestCase(new byte[] { 0xE4 }, "end")]
    [TestCase(new byte[] { 0xE5 }, "end_c")]
    [TestCase(new byte[] { 0xE6 }, "save_next")]
    [TestCase(new byte[] { 0xFC }, "pac_sign_lr")]
    public static void DumpDecodesAllDefinedOpcodesFromAnUnalignedHeader(byte[] code, string expected)
    {
        var blob = new byte[9];
        BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(1), (1u << 27) | 1u);
        blob.AsSpan(5).Fill(0xE4);
        code.CopyTo(blob, 5);
        var text = Dump(blob, 1, 0, 4, 8);

        Assert.That(text, Does.Contain(expected));
        Assert.That(text, Does.Contain("  No epilogs"));
        Assert.That(text, Does.Contain("  Function Length   : 1 (0x00001) Actual length = 4 (0x000004)"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DumpPreservesBothEpilogScopeFormsAndFuncletOffsets(bool singleEpilog)
    {
        var blob = new byte[singleEpilog ? 8 : 12];
        var header = (1u << 27) | (singleEpilog ? 1u << 21 : 1u << 22) | 2u;
        BinaryPrimitives.WriteUInt32LittleEndian(blob, header);
        if (!singleEpilog)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(4), (2u << 22) | 1u);
        }

        blob.AsSpan(blob.Length - 4).Fill(0xE4);
        var text = Dump(blob, 0, 16, 24, (uint)blob.Length);

        Assert.That(text, Does.Contain(singleEpilog
            ? "  --- One epilog, unwind codes at 0"
            : "Offset from main function begin = 20 (0x000014)"));
        Assert.That(text, Does.Contain($"    ---- Epilog start at index {(singleEpilog ? 0 : 2)} ----"));
    }

    [Test]
    public static void DumpPreservesExtendedHeaderCounts()
    {
        var blob = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(blob, 1u);
        BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(4), 1u << 16);
        blob.AsSpan(8).Fill(0xE4);
        var text = Dump(blob, 0, 0, 4, 12);

        Assert.That(text, Does.Contain("  ---- Extension word ----"));
        Assert.That(text, Does.Contain("  Extended Code Words        : 1"));
        Assert.That(text, Does.Contain("  Extended Epilog Count      : 0"));
    }

    private sealed class DiagnosticCodes(byte* codes) : UnwindCodesBase
    {
        public override byte* GetCodes()
        {
            return codes;
        }
    }

    private static string Dump(byte[] blob, int headerOffset, uint start, uint end, uint size)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            fixed (byte* pointer = blob)
            {
                DumpUnwindInfo(compiler, true, start, end, pointer + headerOffset, size);
            }

            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
#endif
