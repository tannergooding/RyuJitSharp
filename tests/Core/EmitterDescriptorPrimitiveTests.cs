// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class EmitterDescriptorPrimitiveTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void InstructionMembershipMatchesEachPositionWithoutChangingTheDescriptor(int position)
    {
        var descriptor = new Descriptor();
        descriptor.idIns(INS_nop);
        Span<instruction> candidates = stackalloc instruction[4];
        candidates.Clear();
        candidates[position] = INS_nop;

        Assert.That(descriptor.idInsIs(candidates[0], candidates[1..]), Is.True);
        Assert.That(descriptor.idIns(), Is.EqualTo(INS_nop));
    }

    [Test]
    public static void InstructionMembershipReturnsFalseWhenEveryCandidateDiffers()
    {
        var descriptor = new Descriptor();
        descriptor.idIns(INS_nop);

        Assert.That(descriptor.idInsIs(INS_invalid, INS_invalid, INS_invalid), Is.False);
        Assert.That(descriptor.idIns(), Is.EqualTo(INS_nop));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EmptyTailKeepsTheSingleInstructionPredicate(bool matches)
    {
        var descriptor = new Descriptor();
        descriptor.idIns(INS_nop);
        var candidate = matches ? INS_nop : INS_invalid;

        Assert.That(descriptor.idInsIs(candidate), Is.EqualTo(matches));
        Assert.That(descriptor.idInsIs(candidate, []), Is.EqualTo(matches));
        Assert.That(descriptor.idIns(), Is.EqualTo(INS_nop));
    }

    private sealed class Descriptor : Emitter.instrDesc
    {
        public override int NativeLogicalSize => throw new NotSupportedException();
    }
}
