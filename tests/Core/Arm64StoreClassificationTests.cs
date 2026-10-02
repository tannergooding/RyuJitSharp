// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm64StoreClassificationTests
{
    private static readonly Emitter s_emitter = (Emitter)RuntimeHelpers.GetUninitializedObject(typeof(Emitter));

    [TestCase(INS_str, true)]
    [TestCase(INS_strb, true)]
    [TestCase(INS_stp, true)]
    [TestCase(INS_ldr, false)]
    [TestCase(INS_ldp, false)]
    [TestCase(INS_add, false)]
    [TestCase(INS_lea, false)]
    public static void StoreClassificationKeepsNativeInstructionKinds(instruction ins, bool expected)
    {
        Assert.That(s_emitter.emitInsIsStore(ins), Is.EqualTo(expected));
    }

    [Test]
    public static void EveryTableEntryUsesTheNativeStoreFlag()
    {
        for (var index = 0; index < CodeGen.instInfo.Length; index++)
        {
            var ins = (instruction)index;
            var expected = (CodeGen.instInfo[index] & CodeGen.ST) != 0;

            Assert.That(s_emitter.emitInsIsStore(ins), Is.EqualTo(expected), $"Instruction {ins}");
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4096)]
    public static void InstructionsAtOrBeyondTheTableHaveNoStoreFlag(int offset)
    {
        var ins = (instruction)(CodeGen.instInfo.Length + offset);

        Assert.That(s_emitter.emitInsIsStore(ins), Is.False);
    }

    [Test]
    public static void MaximumInstructionRepresentationHasNoStoreFlag()
    {
        var ins = unchecked((instruction)uint.MaxValue);

        Assert.That(s_emitter.emitInsIsStore(ins), Is.False);
    }
}
#endif
