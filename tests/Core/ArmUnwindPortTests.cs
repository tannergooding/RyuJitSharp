// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ArmUnwindPortTests
{
    [Test]
    public static void UnimplementedRegisterSaveRemainsAnUnreachableBoundary()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var failure = Assert.Throws<FatalJitException>(() => compiler.unwindSaveReg(REG_R0, 0));

        Assert.That(failure?.Result, Is.EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
    }

#if DEBUG
    [TestCase(0xFFFFFFFFu, 28u, 4u, 15u)]
    [TestCase(0xFFFFFFFFu, 0u, 18u, 0x3FFFFu)]
    [TestCase(0x80000000u, 31u, 1u, 1u)]
    [TestCase(0x12345678u, 16u, 8u, 0x34u)]
    [TestCase(0x00000003u, 0u, 2u, 3u)]
    public static void HeaderBitExtractionPreservesUnsignedFields(
        uint word, uint start, uint length, uint expected)
    {
        Assert.That(ExtractBits(word, start, length), Is.EqualTo(expected));
    }

    [TestCase(new byte[] { 0x01 }, "add sp, sp, #4")]
    [TestCase(new byte[] { 0x80, 0x01 }, "pop {r0}")]
    [TestCase(new byte[] { 0xC3 }, "mov sp, r3")]
    [TestCase(new byte[] { 0xD0 }, "pop {r4}")]
    [TestCase(new byte[] { 0xD8 }, "pop {r4,r5,r6,r7,r8}")]
    [TestCase(new byte[] { 0xE0 }, "vpop {d8}")]
    [TestCase(new byte[] { 0xE8, 0x01 }, "addw sp, sp, #4")]
    [TestCase(new byte[] { 0xEC, 0x01 }, "pop {r0}")]
    [TestCase(new byte[] { 0xEE, 0x01 }, "Microsoft-specific (x = 01)")]
    [TestCase(new byte[] { 0xEE, 0x12 }, "Available (x = 01, y = 02)")]
    [TestCase(new byte[] { 0xEF, 0x03 }, "ldr lr, [sp], #12")]
    [TestCase(new byte[] { 0xF0 }, "Available (x = 00)")]
    [TestCase(new byte[] { 0xF5, 0x12 }, "vpop {d1,d2}")]
    [TestCase(new byte[] { 0xF6, 0x12 }, "vpop {d17,d18}")]
    [TestCase(new byte[] { 0xF7, 0x00, 0x02 }, "add sp, sp, #8")]
    [TestCase(new byte[] { 0xF8, 0x00, 0x00, 0x03 }, "add sp, sp, #12")]
    [TestCase(new byte[] { 0xF9, 0x00, 0x02 }, "add sp, sp, #8")]
    [TestCase(new byte[] { 0xFA, 0x00, 0x00, 0x03 }, "add sp, sp, #12")]
    [TestCase(new byte[] { 0xFB }, "nop; opsize 16")]
    [TestCase(new byte[] { 0xFC }, "nop; opsize 32")]
    [TestCase(new byte[] { 0xFD }, "end + nop; opsize 16")]
    [TestCase(new byte[] { 0xFE }, "end + nop; opsize 32")]
    [TestCase(new byte[] { 0xFF }, "end")]
    public static void UnalignedHeaderDecoderFormatsEveryArmOpcode(byte[] codes, string expected)
    {
        var text = Dump((1u << 28) | 2u, [], codes, 0, 4);

        var opsizeSeparator = expected.IndexOf("; opsize ", StringComparison.Ordinal);
        if (opsizeSeparator < 0)
        {
            Assert.That(text, Does.Contain(expected));
        }
        else
        {
            var opcodeText = expected[..opsizeSeparator];
            var opsizeText = expected[opsizeSeparator..];
            var decodedOnSingleLine = false;

            foreach (var line in text.Split('\n'))
            {
                if (line.Contains(opcodeText, StringComparison.Ordinal)
                    && line.Contains(opsizeText, StringComparison.Ordinal))
                {
                    decodedOnSingleLine = true;
                    break;
                }
            }

            Assert.That(decodedOnSingleLine, Is.True);
        }
    }

    [Test]
    public static void DecoderPreservesExtendedCountsAndEpilogScopeOffsets()
    {
        var extended = Dump(2u, [1u << 16], [0xFF, 0xFF, 0xFF, 0xFF], 0, 4);
        Assert.That(extended, Does.Contain("  Extended Code Words        : 1"));

        var header = (1u << 28) | (1u << 23) | 2u;
        var scope = 2u | (14u << 20);
        var epilog = Dump(header, [scope], [0xFF, 0xFF, 0xFF, 0xFF], 16, 20);

        Assert.That(epilog, Does.Contain("Offset from main function begin = 20 (0x000014)"));
        Assert.That(epilog, Does.Contain("Condition                  : 14 (0xe) (always)"));
        Assert.That(epilog, Does.Contain("---- Epilog start at index 0 ----"));
    }

    [Test]
    public static void DecoderHandlesExtendedCodeStreamsBeyondEpilogIndexRange()
    {
        var codes = new byte[65 * sizeof(uint)];
        Array.Fill(codes, (byte)0xFF);
        var text = Dump(2u, [65u << 16], codes, 0, 4);

        Assert.That(text, Does.Contain("FF          end"));
    }

    [Test]
    public static void DecoderPreservesSingleEpilogAndPhantomPrologFlags()
    {
        var header = (1u << 28) | (1u << 22) | (1u << 21) | 2u;
        var text = Dump(header, [], [0xFF, 0xFF, 0xFF, 0xFF], 0, 4);

        Assert.That(text, Does.Contain("--- One epilog, unwind codes at 0"));
        Assert.That(text, Does.Contain("---- Note: 'F' bit is set. Prolog codes are for a 'phantom' prolog."));
        Assert.That(text, Does.Contain("---- Epilog start at index 0 ----"));
    }

    private static string Dump(uint header, uint[] metadataWords, byte[] codes, uint startOffset, uint endOffset)
    {
        var codeWordCount = (codes.Length + 3) / 4;
        var blob = new byte[1 + ((1 + metadataWords.Length + codeWordCount) * sizeof(uint))];
        BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(1), header);

        var offset = 1 + sizeof(uint);
        foreach (var word in metadataWords)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(offset), word);
            offset += sizeof(uint);
        }

        blob.AsSpan(offset, codeWordCount * sizeof(uint)).Fill(0xFF);
        codes.CopyTo(blob, offset);

        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            fixed (byte* pointer = blob)
            {
                DumpUnwindInfo(compiler, true, startOffset, endOffset, pointer + 1, unchecked((uint)blob.Length - 1));
            }

            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif
}
#endif
