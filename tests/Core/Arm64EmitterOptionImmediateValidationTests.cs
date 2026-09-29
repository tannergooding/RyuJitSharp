// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;

namespace RyuJitSharp.UnitTests;

internal static class Arm64EmitterOptionImmediateValidationTests
{
    [TestCase(0UL, true, true)]
    [TestCase(0x1FFFUL, true, true)]
    [TestCase(0x2000UL, true, false)]
    [TestCase(0x3FFFFUL, true, false)]
    [TestCase(0x40000UL, false, false)]
    [TestCase(ulong.MaxValue, false, false)]
    public static void EncodedImmediateWidthsIgnoreTheUnusedOperandSize(
        ulong value, bool halfword, bool logical)
    {
        Assert.That(IsValidImmHWVal(null, (nuint)value, EA_4BYTE), Is.EqualTo(halfword));
        Assert.That(IsValidImmHWVal(null, (nuint)value, EA_8BYTE), Is.EqualTo(halfword));
        Assert.That(IsValidImmNRS(null, (nuint)value, EA_4BYTE), Is.EqualTo(logical));
        Assert.That(IsValidImmNRS(null, (nuint)value, EA_8BYTE), Is.EqualTo(logical));
    }

    [TestCase(-1L, false, false, false)]
    [TestCase(0L, true, true, true)]
    [TestCase(13L, true, true, true)]
    [TestCase(14L, false, false, false)]
    [TestCase(15L, false, false, false)]
    [TestCase(0x1DL, false, true, true)]
    [TestCase(0x1EL, false, false, false)]
    [TestCase(0xFDL, false, true, true)]
    [TestCase(0xFFL, false, false, false)]
    [TestCase(0x10DL, false, false, true)]
    [TestCase(0x1FFDL, false, false, true)]
    [TestCase(0x1FFEL, false, false, false)]
    [TestCase(0x2000L, false, false, false)]
    public static void ConditionalEncodingsCheckWidthAndLowConditionNibble(
        long value, bool condition, bool flags, bool flagsAndImm5)
    {
        Assert.That(IsValidImmCond(null, (nint)value), Is.EqualTo(condition));
        Assert.That(IsValidImmCondFlags(null, (nint)value), Is.EqualTo(flags));
        Assert.That(IsValidImmCondFlagsImm5(null, (nint)value), Is.EqualTo(flagsAndImm5));
    }

    [Test]
    public static void VectorArrangementsPreserveLaneWidthsAndDatasizes()
    {
        var options = new[]
        {
            (INS_OPTS_8B, EA_8BYTE, EA_1BYTE),
            (INS_OPTS_16B, EA_16BYTE, EA_1BYTE),
            (INS_OPTS_4H, EA_8BYTE, EA_2BYTE),
            (INS_OPTS_8H, EA_16BYTE, EA_2BYTE),
            (INS_OPTS_2S, EA_8BYTE, EA_4BYTE),
            (INS_OPTS_4S, EA_16BYTE, EA_4BYTE),
            (INS_OPTS_1D, EA_8BYTE, EA_8BYTE),
            (INS_OPTS_2D, EA_16BYTE, EA_8BYTE),
        };

        foreach (var (option, dataSize, elementSize) in options)
        {
            Assert.That(IsValidArrangement(null, dataSize, option), Is.True, option.ToString());
            Assert.That(IsValidArrangement(null, dataSize == EA_8BYTE ? EA_16BYTE : EA_8BYTE, option),
                Is.False, option.ToString());
            Assert.That(OptGetElemsize(null, option), Is.EqualTo(elementSize), option.ToString());
        }

        Assert.That(IsValidArrangement(null, EA_4BYTE, INS_OPTS_8B), Is.False);
        Assert.That(IsValidArrangement(null, EA_8BYTE, INS_OPTS_NONE), Is.False);
    }

    [TestCase(INS_OPTS_S_TO_4BYTE, EA_4BYTE, EA_4BYTE)]
    [TestCase(INS_OPTS_D_TO_4BYTE, EA_4BYTE, EA_8BYTE)]
    [TestCase(INS_OPTS_S_TO_8BYTE, EA_8BYTE, EA_4BYTE)]
    [TestCase(INS_OPTS_D_TO_8BYTE, EA_8BYTE, EA_8BYTE)]
    [TestCase(INS_OPTS_H_TO_4BYTE, EA_4BYTE, EA_2BYTE)]
    [TestCase(INS_OPTS_H_TO_8BYTE, EA_8BYTE, EA_2BYTE)]
    [TestCase(INS_OPTS_4BYTE_TO_S, EA_4BYTE, EA_4BYTE)]
    [TestCase(INS_OPTS_4BYTE_TO_D, EA_8BYTE, EA_4BYTE)]
    [TestCase(INS_OPTS_8BYTE_TO_S, EA_4BYTE, EA_8BYTE)]
    [TestCase(INS_OPTS_8BYTE_TO_D, EA_8BYTE, EA_8BYTE)]
    [TestCase(INS_OPTS_4BYTE_TO_H, EA_2BYTE, EA_4BYTE)]
    [TestCase(INS_OPTS_8BYTE_TO_H, EA_2BYTE, EA_8BYTE)]
    [TestCase(INS_OPTS_S_TO_D, EA_8BYTE, EA_4BYTE)]
    [TestCase(INS_OPTS_D_TO_S, EA_4BYTE, EA_8BYTE)]
    [TestCase(INS_OPTS_H_TO_S, EA_4BYTE, EA_2BYTE)]
    [TestCase(INS_OPTS_H_TO_D, EA_8BYTE, EA_2BYTE)]
    [TestCase(INS_OPTS_S_TO_H, EA_2BYTE, EA_4BYTE)]
    [TestCase(INS_OPTS_D_TO_H, EA_2BYTE, EA_8BYTE)]
    public static void ConversionOptionsRetainBothOperandWidths(
        insOpts option, emitAttr destination, emitAttr source)
    {
        Assert.That(OptGetDstsize(null, option), Is.EqualTo(destination));
        Assert.That(OptGetSrcsize(null, option), Is.EqualTo(source));
    }

    [Test]
    public static void VectorIndicesUseTheNativeSignedBoundsForEveryLaneSize()
    {
        foreach (var dataSize in new[] { EA_8BYTE, EA_16BYTE })
        {
            foreach (var elementSize in new[] { EA_1BYTE, EA_2BYTE, EA_4BYTE, EA_8BYTE })
            {
                var count = (int)dataSize / (int)elementSize;
                Assert.That(IsValidVectorIndex(null, dataSize, elementSize, -1), Is.False);
                Assert.That(IsValidVectorIndex(null, dataSize, elementSize, 0), Is.True);
                Assert.That(IsValidVectorIndex(null, dataSize, elementSize, count - 1), Is.True);
                Assert.That(IsValidVectorIndex(null, dataSize, elementSize, count), Is.False);
            }
        }
    }

    [TestCase(INS_sshr, true)]
    [TestCase(INS_ushr, true)]
    [TestCase(INS_shl, false)]
    [TestCase(INS_lea, false)]
    public static void VectorRightShiftChecksFlagTableAndSyntheticBoundary(instruction ins, bool expected)
    {
        var emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));
        Assert.That(IsVectorRightShift(emitter, ins), Is.EqualTo(expected));
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidImmHWVal")]
    private static extern bool IsValidImmHWVal(Emitter? emitter, nuint value, emitAttr size);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidImmNRS")]
    private static extern bool IsValidImmNRS(Emitter? emitter, nuint value, emitAttr size);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidImmCond")]
    private static extern bool IsValidImmCond(Emitter? emitter, nint imm);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidImmCondFlags")]
    private static extern bool IsValidImmCondFlags(Emitter? emitter, nint imm);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidImmCondFlagsImm5")]
    private static extern bool IsValidImmCondFlagsImm5(Emitter? emitter, nint imm);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidArrangement")]
    private static extern bool IsValidArrangement(Emitter? emitter, emitAttr dataSize, insOpts option);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "optGetElemsize")]
    private static extern emitAttr OptGetElemsize(Emitter? emitter, insOpts option);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "optGetDstsize")]
    private static extern emitAttr OptGetDstsize(Emitter? emitter, insOpts option);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "optGetSrcsize")]
    private static extern emitAttr OptGetSrcsize(Emitter? emitter, insOpts option);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isValidVectorIndex")]
    private static extern bool IsValidVectorIndex(Emitter? emitter, emitAttr dataSize, emitAttr elementSize, nint index);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsIsVectorRightShift")]
    private static extern bool IsVectorRightShift(Emitter emitter, instruction ins);
}
#endif
