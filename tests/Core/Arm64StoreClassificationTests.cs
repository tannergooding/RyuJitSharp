// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm64StoreClassificationTests
{
    private static readonly Emitter? s_emitter = Configuration.TARGET_ARM64
        ? (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter))
        : null;

    [TestCase(INS_str, true)]
    [TestCase(INS_strb, true)]
    [TestCase(INS_stp, true)]
    [TestCase(INS_ldr, false)]
    [TestCase(INS_ldp, false)]
    [TestCase(INS_add, false)]
    [TestCase(INS_lea, false)]
    public static void StoreClassificationKeepsNativeInstructionKinds(instruction ins, bool expected)
    {
        Assume.That(Configuration.TARGET_ARM64, "requires the ARM64 target");
        var emitter = s_emitter ?? throw new AssertionException("The ARM64 emitter was not initialized.");

        Assert.That(emitter.emitInsIsStore(ins), Is.EqualTo(expected));
    }

    [Test]
    public static void EveryTableEntryUsesTheNativeStoreFlag()
    {
        Assume.That(Configuration.TARGET_ARM64, "requires the ARM64 target");
        var emitter = s_emitter ?? throw new AssertionException("The ARM64 emitter was not initialized.");

        for (var index = 0; index < CodeGen.instInfo.Length; index++)
        {
            var ins = InstructionOrdinalMaps.GetInstructionFromNativeOrdinal(index);
            var expected = ((byte)CodeGen.instInfo[index] & CodeGen.ST) != 0;

            Assert.That(emitter.emitInsIsStore(ins), Is.EqualTo(expected), $"Instruction {ins}");
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4096)]
    public static void InstructionsAtOrBeyondTheTableHaveNoStoreFlag(int offset)
    {
        Assume.That(Configuration.TARGET_ARM64, "requires the ARM64 target");
        var emitter = s_emitter ?? throw new AssertionException("The ARM64 emitter was not initialized.");

        var ins = (instruction)(CodeGen.instInfo.Length + offset);

        Assert.That(emitter.emitInsIsStore(ins), Is.False);
    }

    [Test]
    public static void MaximumInstructionRepresentationHasNoStoreFlag()
    {
        Assume.That(Configuration.TARGET_ARM64, "requires the ARM64 target");
        var emitter = s_emitter ?? throw new AssertionException("The ARM64 emitter was not initialized.");

        var ins = unchecked((instruction)uint.MaxValue);

        Assert.That(emitter.emitInsIsStore(ins), Is.False);
    }
}
