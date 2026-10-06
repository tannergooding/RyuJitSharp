// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm32InstructionCodeTests
{
    [TestCase(INS_add, IF_T1_D0, 0x4400u)]
    [TestCase(INS_add, IF_T1_H, 0x1800u)]
    [TestCase(INS_add, IF_T1_J0, 0x3000u)]
    [TestCase(INS_add, IF_T1_G, 0x1C00u)]
    [TestCase(INS_add, IF_T2_L0, 0xF1000000u)]
    [TestCase(INS_add, IF_T2_C0, 0xEB000000u)]
    [TestCase(INS_add, IF_T1_F, 0xB000u)]
    [TestCase(INS_add, IF_T1_J2, 0xA800u)]
    [TestCase(INS_add, IF_T1_J3, 0xA000u)]
    [TestCase(INS_ldr, IF_T1_H, 0x5800u)]
    [TestCase(INS_ldr, IF_T1_C, 0x6800u)]
    [TestCase(INS_ldr, IF_T2_E0, 0xF8500000u)]
    [TestCase(INS_ldr, IF_T2_H0, 0xF8500800u)]
    [TestCase(INS_ldr, IF_T2_K1, 0xF8D00000u)]
    [TestCase(INS_ldr, IF_T2_K4, 0xF85F0000u)]
    [TestCase(INS_ldr, IF_T1_J2, 0x9800u)]
    [TestCase(INS_ldr, IF_T1_J3, 0x4800u)]
    [TestCase(INS_nop, IF_NONE, 0xBF00u)]
    public static void NativeGoldenOpcodesPreserveEncodingColumnsAndDirectFormatBehavior(
        instruction ins, Emitter.insFormat format, uint expected)
    {
        Assert.That(Code(NewEmitter(), ins, format), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(EncodingGroups))]
    public static void EveryGroupedInstructionSelectsEachAvailableNativeColumn(
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
                var codes = Codes(column + 1);
                if (ordinal >= codes.Length)
                {
                    continue;
                }

                var expected = codes[ordinal];
                if (expected == BAD_CODE)
                {
                    continue;
                }

                Assert.That(Code(emitter, ins, formats[column]), Is.EqualTo(expected),
                    $"{ins}: column {column + 1}, {formats[column]}");
            }
        }

        Assert.That(instructions, Is.GreaterThan(0), $"Missing native group {group}.");
    }

    [Test]
    public static void DirectFormatsSelectColumnOne()
    {
        var emitter = NewEmitter();
        var direct = 0;
        for (var ordinal = 1; ordinal < Codes(1).Length; ordinal++)
        {
            var ins = (instruction)ordinal;
            var format = Format(emitter, ins);
            if (IsGroupedFormat(format))
            {
                continue;
            }

            var expected = Codes(1)[ordinal];
            if (expected == BAD_CODE)
            {
                continue;
            }

            Assert.That(Code(emitter, ins, format), Is.EqualTo(expected), $"{ins}: {format}");
            direct++;
        }

        Assert.That(direct, Is.GreaterThan(0));
    }

#if FEATURE_ITINSTRUCTION
    [TestCase(1, 146)]
#else
    [TestCase(1, 130)]
#endif
#if FEATURE_PLI_INSTRUCTION
    [TestCase(2, 61)]
    [TestCase(3, 37)]
    [TestCase(4, 22)]
#else
    [TestCase(2, 60)]
    [TestCase(3, 36)]
    [TestCase(4, 21)]
#endif
    [TestCase(5, 13)]
    [TestCase(6, 11)]
    [TestCase(7, 4)]
    [TestCase(8, 4)]
    [TestCase(9, 3)]
    public static void TablesPreserveCompactInstructionPrefixes(int encoding, int expectedLength)
    {
        var codes = Codes(encoding);
        Assert.That(codes.Length, Is.EqualTo(expectedLength));
        Assert.That(codes[0], Is.EqualTo(BAD_CODE));
        Assert.That((int)INS_invalid, Is.Zero);
        Assert.That((int)INS_add, Is.EqualTo(1));
    }

    private static IEnumerable<TestCaseData> EncodingGroups()
    {
        yield return Group(IF_EN9, [
            IF_T1_D0, IF_T1_H, IF_T1_J0, IF_T1_G, IF_T2_L0,
            IF_T2_C0, IF_T1_F, IF_T1_J2, IF_T1_J3,
        ]);
        yield return Group(IF_EN8, [IF_T1_H, IF_T1_C, IF_T2_E0, IF_T2_H0, IF_T2_K1, IF_T2_K4, IF_T1_J2, IF_T1_J3]);
        yield return Group(IF_EN6A, [IF_T1_H, IF_T1_C, IF_T2_E0, IF_T2_H0, IF_T2_K1, IF_T2_K4]);
        yield return Group(IF_EN6B, [IF_T1_H, IF_T1_C, IF_T2_E0, IF_T2_H0, IF_T2_K1, IF_T1_J2]);
        yield return Group(IF_EN5A, [IF_T1_E, IF_T1_D0, IF_T1_J0, IF_T2_L1, IF_T2_C3]);
        yield return Group(IF_EN5B, [IF_T1_E, IF_T1_D0, IF_T1_J0, IF_T2_L2, IF_T2_C8]);
        yield return Group(IF_EN4A, [IF_T1_E, IF_T1_C, IF_T2_C4, IF_T2_C2]);
        yield return Group(IF_EN4B, [IF_T2_K2, IF_T2_H2, IF_T2_C7, IF_T2_K3]);
        yield return Group(IF_EN4C, [IF_T2_N, IF_T2_N1, IF_T2_N2, IF_T2_N3]);
        yield return Group(IF_EN3A, [IF_T1_E, IF_T2_C0, IF_T2_L0]);
        yield return Group(IF_EN3B, [IF_T1_E, IF_T2_C8, IF_T2_L2]);
        yield return Group(IF_EN3C, [IF_T1_E, IF_T2_C1, IF_T2_L1]);
        yield return Group(IF_EN3D, [IF_T1_L1, IF_T2_E2, IF_T2_I1]);
        yield return Group(IF_EN3E, [IF_T1_M, IF_T2_J2, IF_T2_J3]);
        yield return Group(IF_EN2A, [IF_T1_K, IF_T2_J1]);
        yield return Group(IF_EN2B, [IF_T1_D1, IF_T1_D2]);
        yield return Group(IF_EN2C, [IF_T1_D2, IF_T2_J3]);
        yield return Group(IF_EN2D, [IF_T1_J1, IF_T2_I0]);
        yield return Group(IF_EN2E, [IF_T1_E, IF_T2_C6]);
        yield return Group(IF_EN2F, [IF_T1_E, IF_T2_C5]);
        yield return Group(IF_EN2G, [IF_T1_J3, IF_T2_M1]);
    }

    private static TestCaseData Group(Emitter.insFormat group, Emitter.insFormat[] formats)
    {
        return new TestCaseData(group, formats).SetName($"NativeColumns_{group}");
    }

    private static bool IsGroupedFormat(Emitter.insFormat format)
    {
        return format is IF_EN9 or IF_EN8 or IF_EN6A or IF_EN6B or IF_EN5A or IF_EN5B
            or IF_EN4A or IF_EN4B or IF_EN4C or IF_EN3A or IF_EN3B or IF_EN3C or IF_EN3D or IF_EN3E
            or IF_EN2A or IF_EN2B or IF_EN2C or IF_EN2D or IF_EN2E or IF_EN2F or IF_EN2G;
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
