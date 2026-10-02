// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm64InstructionCodeTests
{
    [TestCase(INS_mov, IF_DR_2E, 0x2A0003E0u)]
    [TestCase(INS_mov, IF_DR_2G, 0x11000000u)]
    [TestCase(INS_mov, IF_DI_1B, 0x52800000u)]
    [TestCase(INS_mov, IF_DI_1D, 0x320003E0u)]
    [TestCase(INS_mov, IF_DV_3C, 0x0EA01C00u)]
    [TestCase(INS_mov, IF_DV_2B, 0x0E003C00u)]
    [TestCase(INS_mov, IF_DV_2C, 0x4E001C00u)]
    [TestCase(INS_mov, IF_DV_2E, 0x5E000400u)]
    [TestCase(INS_mov, IF_DV_2F, 0x6E000400u)]
    [TestCase(INS_add, IF_DR_3A, 0x0B000000u)]
    [TestCase(INS_add, IF_DR_3B, 0x0B000000u)]
    [TestCase(INS_add, IF_DR_3C, 0x0B200000u)]
    [TestCase(INS_add, IF_DI_2A, 0x11000000u)]
    [TestCase(INS_add, IF_DV_3A, 0x0E208400u)]
    [TestCase(INS_add, IF_DV_3E, 0x5E208400u)]
    [TestCase(INS_ldr, IF_LS_2A, 0xB9400000u)]
    [TestCase(INS_ldr, IF_LS_2B, 0xB9400000u)]
    [TestCase(INS_ldr, IF_LS_2C, 0xB8400000u)]
    [TestCase(INS_ldr, IF_LS_3A, 0xB8600800u)]
    [TestCase(INS_ldr, IF_LS_1A, 0x18000000u)]
    [TestCase(INS_fmov, IF_DV_2G, 0x1E204000u)]
    [TestCase(INS_fmov, IF_DV_2H, 0x1E260000u)]
    [TestCase(INS_fmov, IF_DV_2I, 0x1E270000u)]
    [TestCase(INS_fmov, IF_DV_1A, 0x1E201000u)]
    [TestCase(INS_fmov, IF_DV_1B, 0x0F00F400u)]
    [TestCase(INS_adr, IF_DI_1E, 0x10000000u)]
    [TestCase(INS_adrp, IF_DI_1E, 0x90000000u)]
    [TestCase(INS_b, IF_BI_0A, 0x14000000u)]
    [TestCase(INS_bl, IF_BI_0C, 0x94000000u)]
    [TestCase(INS_br, IF_BR_1A, 0xD61F0000u)]
    [TestCase(INS_blr, IF_BR_1B, 0xD63F0000u)]
    [TestCase(INS_ret, IF_BR_1A, 0xD65F0000u)]
    [TestCase(INS_cbz, IF_BI_1A, 0x34000000u)]
    [TestCase(INS_tbz, IF_BI_1B, 0x36000000u)]
    [TestCase(INS_movk, IF_DI_1B, 0x72800000u)]
    [TestCase(INS_movn, IF_DI_1B, 0x12800000u)]
    [TestCase(INS_movz, IF_DI_1B, 0x52800000u)]
    [TestCase(INS_nop, IF_SN_0A, 0xD503201Fu)]
    [TestCase(INS_fcvt, IF_DV_2J, 0x1E224000u)]
    [TestCase(INS_sm4e, IF_DV_2V, 0xCEC08400u)]
    public static void NativeGoldenOpcodesPreserveColumnsAndFullUnsignedWords(
        instruction ins, Emitter.insFormat format, uint expected)
    {
        Assert.That(Code(NewEmitter(), ins, format), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(EncodingGroups))]
    public static void EveryGroupedInstructionSelectsEachNativeColumn(
        Emitter.insFormat group, Emitter.insFormat[] formats)
    {
        var emitter = NewEmitter();
        var instructions = 0;
        for (var ordinal = 1; ordinal < Codes(1).Length; ordinal++)
        {
            var ins = (instruction)ordinal;
            if (Format(emitter, ins) != group)
            {
                continue;
            }

            instructions++;
            for (var column = 0; column < formats.Length; column++)
            {
                Assert.That(Code(emitter, ins, formats[column]), Is.EqualTo(Codes(column + 1)[ordinal]),
                    $"{ins}: column {column + 1}, {formats[column]}");
            }
        }

        Assert.That(instructions, Is.GreaterThan(0), $"Missing native group {group}.");
    }

    [Test]
    public static void DirectFormatsSelectColumnOneWithoutTreatingBadEntriesAsOpcodes()
    {
        var emitter = NewEmitter();
        var direct = 0;
        for (var ordinal = 1; ordinal < Codes(1).Length; ordinal++)
        {
            var ins = (instruction)ordinal;
            var format = Format(emitter, ins);
            if (format is >= IF_EN9 and <= IF_EN2Q)
            {
                continue;
            }

#if FEATURE_LOOP_ALIGN
            if (ins == INS_align)
            {
                Assert.That(Codes(1)[ordinal], Is.EqualTo(BAD_CODE));
                continue;
            }
#endif

            Assert.That(Codes(1)[ordinal], Is.Not.EqualTo(BAD_CODE), $"{ins}: {format}");
            Assert.That(Code(emitter, ins, format), Is.EqualTo(Codes(1)[ordinal]), $"{ins}: {format}");
            direct++;
        }

        Assert.That(direct, Is.EqualTo(357));
    }

#if FEATURE_LOOP_ALIGN
    [TestCase(1, 552)]
#else
    [TestCase(1, 551)]
#endif
    [TestCase(2, 194)]
    [TestCase(3, 80)]
    [TestCase(4, 48)]
    [TestCase(5, 16)]
    [TestCase(6, 12)]
    [TestCase(7, 2)]
    [TestCase(8, 2)]
    [TestCase(9, 2)]
    public static void TablesPreserveCompactInstructionPrefixes(int encoding, int expectedLength)
    {
        var codes = Codes(encoding);
        Assert.That(codes.Length, Is.EqualTo(expectedLength));
        Assert.That(codes[0], Is.EqualTo(BAD_CODE));
        Assert.That((int)INS_invalid, Is.Zero);
        Assert.That((int)INS_mov, Is.EqualTo(1));
        if (encoding == 1)
        {
            Assert.That((int)INS_sm4e, Is.EqualTo(expectedLength - 1));
            Assert.That(codes[^1], Is.EqualTo(0xCEC08400u));
#if FEATURE_LOOP_ALIGN
            Assert.That(codes[(int)INS_align], Is.EqualTo(BAD_CODE));
            Assert.That((int)INS_eor3, Is.EqualTo((int)INS_align + 1));
#endif
        }
    }

#if DEBUG
    [TestCase(INS_mov)]
    [TestCase(INS_nop)]
    public static void WrongFormatsPreserveNativeAssertionOrderAndDefinedBadCodeContinuation(instruction ins)
    {
        uint result = 0;
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
            () => result = Code(NewEmitter(), ins, IF_NONE));

        Assert.That(assertions, Is.EqualTo<string[]>(["encoding_found", "(code != BAD_CODE)"]));
        Assert.That(result, Is.EqualTo(BAD_CODE));
    }

    [Test]
    public static void InvalidSentinelPreservesFormatThenBadCodeAssertions()
    {
        uint result = 0;
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
            () => result = Code(NewEmitter(), INS_invalid, IF_NONE));

        Assert.That(assertions, Is.EqualTo<string[]>([
            "s_instructionFormats[(int)ins] != IF_NONE", "(code != BAD_CODE)",
        ]));
        Assert.That(result, Is.EqualTo(BAD_CODE));
    }

#if FEATURE_LOOP_ALIGN
    [Test]
    public static void AlignmentPlaceholderPreservesTheNativeBadOpcodeAssertion()
    {
        uint result = 0;
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
            () => result = Code(NewEmitter(), INS_align, IF_SN_0A));

        Assert.That(assertions, Is.EqualTo<string[]>(["(code != BAD_CODE)"]));
        Assert.That(result, Is.EqualTo(BAD_CODE));
    }
#endif
#endif

    private static IEnumerable<TestCaseData> EncodingGroups()
    {
        yield return Group(IF_EN9, [
            IF_DR_2E, IF_DR_2G, IF_DI_1B, IF_DI_1D, IF_DV_3C,
            IF_DV_2B, IF_DV_2C, IF_DV_2E, IF_DV_2F,
        ]);
        yield return Group(IF_EN6A, [IF_DR_3A, IF_DR_3B, IF_DR_3C, IF_DI_2A, IF_DV_3A, IF_DV_3E]);
        yield return Group(IF_EN6B, [IF_LS_2D, IF_LS_3F, IF_LS_2E, IF_LS_2F, IF_LS_3G, IF_LS_2G]);
        yield return Group(IF_EN5A, [IF_LS_2A, IF_LS_2B, IF_LS_2C, IF_LS_3A, IF_LS_1A]);
        yield return Group(IF_EN5B, [IF_DV_2G, IF_DV_2H, IF_DV_2I, IF_DV_1A, IF_DV_1B]);
        yield return Group(IF_EN5C, [IF_DR_3A, IF_DR_3B, IF_DI_2C, IF_DV_3C, IF_DV_1B]);
        yield return Group(IF_EN4A, [IF_LS_2A, IF_LS_2B, IF_LS_2C, IF_LS_3A]);
        yield return Group(IF_EN4B, [IF_DR_3A, IF_DR_3B, IF_DR_3C, IF_DI_2A]);
        yield return Group(IF_EN4C, [IF_DR_2A, IF_DR_2B, IF_DR_2C, IF_DI_1A]);
        yield return Group(IF_EN4D, [IF_DV_3B, IF_DV_3D, IF_DV_3BI, IF_DV_3DI]);
        yield return Group(IF_EN4E, [IF_DR_3A, IF_DR_3B, IF_DI_2C, IF_DV_3C]);
        yield return Group(IF_EN4F, [IF_DR_3A, IF_DR_3B, IF_DV_3C, IF_DV_1B]);
        yield return Group(IF_EN4G, [IF_DR_2E, IF_DR_2F, IF_DV_2M, IF_DV_2L]);
        yield return Group(IF_EN4H, [IF_DV_3E, IF_DV_3A, IF_DV_2L, IF_DV_2M]);
        yield return Group(IF_EN4I, [IF_DV_3D, IF_DV_3B, IF_DV_2G, IF_DV_2A]);
        yield return Group(IF_EN4J, [IF_DV_2N, IF_DV_2O, IF_DV_3E, IF_DV_3A]);
        yield return Group(IF_EN4K, [IF_DV_3E, IF_DV_3A, IF_DV_3EI, IF_DV_3AI]);
        yield return Group(IF_EN3A, [IF_DR_3A, IF_DR_3B, IF_DI_2C]);
        yield return Group(IF_EN3B, [IF_DR_2A, IF_DR_2B, IF_DI_1C]);
        yield return Group(IF_EN3C, [IF_DR_3A, IF_DR_3B, IF_DV_3C]);
        yield return Group(IF_EN3D, [IF_DV_2C, IF_DV_2D, IF_DV_2E]);
        yield return Group(IF_EN3E, [IF_DV_3B, IF_DV_3BI, IF_DV_3DI]);
        yield return Group(IF_EN3F, [IF_DV_2A, IF_DV_2G, IF_DV_2H]);
        yield return Group(IF_EN3G, [IF_DV_2A, IF_DV_2G, IF_DV_2I]);
        yield return Group(IF_EN3H, [IF_DR_3A, IF_DV_3A, IF_DV_3AI]);
        yield return Group(IF_EN3I, [IF_DR_2E, IF_DR_2F, IF_DV_2M]);
        yield return Group(IF_EN3J, [IF_LS_2D, IF_LS_3F, IF_LS_2E]);
        yield return Group(IF_EN2A, [IF_DR_2E, IF_DR_2F]);
        yield return Group(IF_EN2B, [IF_DR_3A, IF_DR_3B]);
        yield return Group(IF_EN2C, [IF_DR_3A, IF_DI_2D]);
        yield return Group(IF_EN2D, [IF_DR_3A, IF_DI_2B]);
        yield return Group(IF_EN2E, [IF_LS_3B, IF_LS_3C]);
        yield return Group(IF_EN2F, [IF_DR_2I, IF_DI_1F]);
        yield return Group(IF_EN2G, [IF_DV_3B, IF_DV_3D]);
        yield return Group(IF_EN2H, [IF_DV_2C, IF_DV_2F]);
        yield return Group(IF_EN2I, [IF_DV_2K, IF_DV_1C]);
        yield return Group(IF_EN2J, [IF_DV_2A, IF_DV_2G]);
        yield return Group(IF_EN2K, [IF_DV_2M, IF_DV_2L]);
        yield return Group(IF_EN2L, [IF_DR_2G, IF_DV_2M]);
        yield return Group(IF_EN2M, [IF_DV_3A, IF_DV_3AI]);
        yield return Group(IF_EN2N, [IF_DV_2N, IF_DV_2O]);
        yield return Group(IF_EN2O, [IF_DV_3E, IF_DV_3A]);
        yield return Group(IF_EN2P, [IF_DV_2Q, IF_DV_3B]);
        yield return Group(IF_EN2Q, [IF_DV_2S, IF_DV_3A]);
    }

    private static TestCaseData Group(Emitter.insFormat group, Emitter.insFormat[] formats)
    {
        return new TestCaseData(group, formats).SetName($"NativeColumns_{group}");
    }

    private static Emitter NewEmitter()
    {
        return (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));
    }

    private static ReadOnlySpan<uint> Codes(int encoding)
    {
        return encoding switch
        {
            1 => Codes1(null),
            2 => Codes2(null),
            3 => Codes3(null),
            4 => Codes4(null),
            5 => Codes5(null),
            6 => Codes6(null),
            7 => Codes7(null),
            8 => Codes8(null),
            9 => Codes9(null),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
        };
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsCode")]
    private static extern uint Code(Emitter emitter, instruction ins, Emitter.insFormat format);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsFormat")]
    private static extern Emitter.insFormat Format(Emitter emitter, instruction ins);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ordinaryInsCodes1")]
    private static extern ReadOnlySpan<uint> Codes1(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ordinaryInsCodes2")]
    private static extern ReadOnlySpan<uint> Codes2(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ordinaryInsCodes3")]
    private static extern ReadOnlySpan<uint> Codes3(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ordinaryInsCodes4")]
    private static extern ReadOnlySpan<uint> Codes4(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ordinaryInsCodes5")]
    private static extern ReadOnlySpan<uint> Codes5(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ordinaryInsCodes6")]
    private static extern ReadOnlySpan<uint> Codes6(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ordinaryInsCodes7")]
    private static extern ReadOnlySpan<uint> Codes7(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ordinaryInsCodes8")]
    private static extern ReadOnlySpan<uint> Codes8(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_ordinaryInsCodes9")]
    private static extern ReadOnlySpan<uint> Codes9(Emitter? emitter);
}
#endif
