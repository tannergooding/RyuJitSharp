// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm64SveInstructionCodeTests
{
    [TestCase(INS_sve_mov, IF_SVE_AU_3A, 0x04603000u)]
    [TestCase(INS_sve_mov, IF_SVE_BT_1A, 0x05C00000u)]
    [TestCase(INS_sve_mov, IF_SVE_BV_2A, 0x05100000u)]
    [TestCase(INS_sve_mov, IF_SVE_BV_2A_J, 0x05104000u)]
    [TestCase(INS_sve_mov, IF_SVE_BW_2A, 0x05202000u)]
    [TestCase(INS_sve_mov, IF_SVE_CB_2A, 0x05203800u)]
    [TestCase(INS_sve_mov, IF_SVE_CP_3A, 0x05208000u)]
    [TestCase(INS_sve_mov, IF_SVE_CQ_3A, 0x0528A000u)]
    [TestCase(INS_sve_mov, IF_SVE_CW_4A, 0x0520C000u)]
    [TestCase(INS_sve_mov, IF_SVE_CZ_4A, 0x25004000u)]
    [TestCase(INS_sve_mov, IF_SVE_CZ_4A_K, 0x25004210u)]
    [TestCase(INS_sve_mov, IF_SVE_CZ_4A_L, 0x25804000u)]
    [TestCase(INS_sve_mov, IF_SVE_EB_1A, 0x2538C000u)]
    [TestCase(INS_sve_st1w, IF_SVE_JD_4B, 0xE5404000u)]
    [TestCase(INS_sve_st1w, IF_SVE_JD_4C, 0xE5004000u)]
    [TestCase(INS_sve_st1w, IF_SVE_JN_3B, 0xE540E000u)]
    [TestCase(INS_sve_st1w, IF_SVE_JN_3C, 0xE500E000u)]
    [TestCase(INS_sve_ld1w, IF_SVE_HW_4A, 0x85204000u)]
    [TestCase(INS_sve_ld1w, IF_SVE_II_4A_H, 0xA5000000u)]
    [TestCase(INS_sve_ldff1h, IF_SVE_HW_4A, 0x84A06000u)]
    [TestCase(INS_sve_ldff1h, IF_SVE_IG_4A_G, 0xA4806000u)]
    [TestCase(INS_sve_ld1sw, IF_SVE_IJ_3A, 0xA480A000u)]
    [TestCase(INS_sve_ld1sw, IF_SVE_IV_3A, 0xC5208000u)]
    [TestCase(INS_sve_mul, IF_SVE_AA_3A, 0x04100000u)]
    [TestCase(INS_sve_mul, IF_SVE_FD_3C, 0x44E0F800u)]
    [TestCase(INS_sve_asr, IF_SVE_AM_2A, 0x04008000u)]
    [TestCase(INS_sve_asr, IF_SVE_BG_3A, 0x04208000u)]
    [TestCase(INS_sve_and, IF_SVE_AA_3A, 0x041A0000u)]
    [TestCase(INS_sve_and, IF_SVE_CZ_4A, 0x25004000u)]
    [TestCase(INS_sve_ldnt1d, IF_SVE_IM_3A, 0xA580E000u)]
    [TestCase(INS_sve_ldnt1d, IF_SVE_IX_4A, 0xC580C000u)]
    [TestCase(INS_sve_stnt1d, IF_SVE_JM_3A, 0xE590E000u)]
    [TestCase(INS_sve_st4q, IF_SVE_JE_3A, 0xE4C00000u)]
    [TestCase(INS_sve_st4q, IF_SVE_JF_4A, 0xE4E00000u)]
    [TestCase(INS_sve_abs, IF_SVE_AQ_3A, 0x0416A000u)]
    [TestCase(INS_sve_st1q, IF_SVE_IY_4A, 0xE4202000u)]
    public static void LookupSelectsTheNativeInstructionColumn(instruction ins, Emitter.insFormat format, uint expected)
    {
        Assert.That(Code(NewEmitter(), ins, format), Is.EqualTo(expected));
    }

    [TestCase(1, 606)]
    [TestCase(2, 242)]
    [TestCase(3, 117)]
    [TestCase(4, 54)]
    [TestCase(5, 33)]
    [TestCase(6, 25)]
    [TestCase(7, 14)]
    [TestCase(8, 13)]
    [TestCase(9, 9)]
    [TestCase(10, 3)]
    [TestCase(11, 3)]
    [TestCase(12, 2)]
    [TestCase(13, 2)]
    public static void TablesKeepTheNativeCompactPrefixAndInvalidEntry(int encoding, int length)
    {
        var codes = encoding switch
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
            10 => Codes10(null),
            11 => Codes11(null),
            12 => Codes12(null),
            13 => Codes13(null),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
        };
        Assert.That(codes.Length, Is.EqualTo(length));
        Assert.That(codes[0], Is.EqualTo(BAD_CODE));
        Assert.That(unchecked((uint)INS_sve_mov - (uint)INS_sve_invalid), Is.EqualTo(1u));
        Assert.That(unchecked((uint)INS_sve_st1q - (uint)INS_sve_invalid), Is.EqualTo(605u));
    }

#if DEBUG
    [TestCase(INS_sve_mov)]
    [TestCase(INS_sve_abs)]
    public static void WrongFormatRetainsNativeAssertionAndBadCodeContinuation(instruction ins)
    {
        uint result = 0;
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(() => result = Code(NewEmitter(), ins, IF_EN5A));
        Assert.That(assertions, Is.EqualTo<string[]>(["encoding_found", "(code != BAD_CODE)"]));
        Assert.That(result, Is.EqualTo(BAD_CODE));
    }

    [Test]
    public static void InvalidEntryRetainsFormatAndBadCodeAssertions()
    {
        uint result = 0;
        var (_, assertions) = Arm64SveInstructionSanityTests.Capture(
            () => result = Code(NewEmitter(), INS_sve_invalid, IF_NONE));
        Assert.That(assertions, Is.EqualTo<string[]>([
            "s_instructionFormats[(int)ins] != IF_NONE", "(code != BAD_CODE)",
        ]));
        Assert.That(result, Is.EqualTo(BAD_CODE));
    }
#endif

    private static Emitter NewEmitter()
    {
        return (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsCodeSve")]
    private static extern uint Code(Emitter emitter, instruction ins, Emitter.insFormat format);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes1")]
    private static extern ReadOnlySpan<uint> Codes1(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes2")]
    private static extern ReadOnlySpan<uint> Codes2(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes3")]
    private static extern ReadOnlySpan<uint> Codes3(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes4")]
    private static extern ReadOnlySpan<uint> Codes4(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes5")]
    private static extern ReadOnlySpan<uint> Codes5(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes6")]
    private static extern ReadOnlySpan<uint> Codes6(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes7")]
    private static extern ReadOnlySpan<uint> Codes7(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes8")]
    private static extern ReadOnlySpan<uint> Codes8(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes9")]
    private static extern ReadOnlySpan<uint> Codes9(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes10")]
    private static extern ReadOnlySpan<uint> Codes10(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes11")]
    private static extern ReadOnlySpan<uint> Codes11(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes12")]
    private static extern ReadOnlySpan<uint> Codes12(Emitter? emitter);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_insCodes13")]
    private static extern ReadOnlySpan<uint> Codes13(Emitter? emitter);
}
#endif
