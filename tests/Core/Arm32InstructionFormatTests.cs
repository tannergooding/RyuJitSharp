// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitInsFormat")]
    private static extern Emitter.insFormat Format(Emitter emitter, instruction ins);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_s_instructionFormats")]
    private static extern ReadOnlySpan<Emitter.insFormat> InstructionFormats(Emitter? emitter);
}
#endif
