// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm32InstructionFormatTests
{
    [TestCase(INS_add, IF_EN9)]
    [TestCase(INS_ldr, IF_EN8)]
    public static void InstructionFormatsMatchTheNativeTable(instruction ins, Emitter.insFormat expectedFormat)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
            Assert.That(Format(codeGen.Emitter, ins), Is.EqualTo(expectedFormat)));
    }

    [Test]
    public static void EveryArmInstructionHasAFormat()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var formats = InstructionFormats(null);
            for (var ordinal = 1; ordinal < formats.Length; ordinal++)
            {
                var expectedFormat = formats[ordinal];
                if (expectedFormat == IF_NONE)
                {
                    continue;
                }

                var ins = (instruction)ordinal;
                Assert.That(Format(codeGen.Emitter, ins), Is.EqualTo(expectedFormat), $"{ins}");
            }
        });
    }

    [TestCase(IF_T1_A, ISZ_16BIT)]
    [TestCase(IF_T1_M, ISZ_16BIT)]
    [TestCase(IF_T2_A, ISZ_32BIT)]
    [TestCase(IF_T2_M1, ISZ_32BIT)]
    [TestCase(IF_LARGEJMP, ISZ_48BIT)]
    public static void InstructionSizesMatchNativeFormatRanges(
        Emitter.insFormat format, Emitter.insSize expectedSize)
    {
        Assert.That(InstructionSize(null, format), Is.EqualTo(expectedSize));
    }

    [TestCase(INS_nop, IF_T1_A, ISZ_16BIT, 2u)]
    [TestCase(INS_nopw, IF_T2_A, ISZ_32BIT, 4u)]
    public static void ZeroOperandInstructionsUseTheirNativeFormatAndSize(
        instruction ins, Emitter.insFormat expectedFormat, Emitter.insSize expectedSize, uint expectedCodeSize)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
#if !DEBUG
            var initialGroupSize = InstructionGroupSize(emitter);
#endif
            emitter.emitIns(ins);
#if !DEBUG
            Assert.That(InstructionGroupSize(emitter), Is.EqualTo(initialGroupSize + (int)expectedCodeSize));
#endif

            var descriptor = LastInstruction(emitter)
                ?? throw new AssertionException("No zero-operand instruction was recorded.");
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(descriptor.idInsSize(), Is.EqualTo(expectedSize));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int InstructionGroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsFormat")]
    private static extern Emitter.insFormat Format(Emitter emitter, instruction ins);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitInsSize")]
    private static extern Emitter.insSize InstructionSize(Emitter? emitter, Emitter.insFormat format);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_s_instructionFormats")]
    private static extern ReadOnlySpan<Emitter.insFormat> InstructionFormats(Emitter? emitter);
}
#endif
