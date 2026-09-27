// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class AbiPassingInformationTests
{
    [TestCase(1, 8)]
    [TestCase(4, 8)]
    [TestCase(8, 8)]
    [TestCase(9, 16)]
    public static void OnStackConsumesFullSlots(int size, int expectedStackSize)
    {
        var segment = AbiPassingSegment.OnStack(16, 3, size);

        Assert.That(segment.IsPassedOnStack, Is.True);
        Assert.That(segment.StackOffset, Is.EqualTo(16));
        Assert.That(segment.Offset, Is.EqualTo(3));
        Assert.That(segment.Size, Is.EqualTo(size));
        Assert.That(segment.StackSize, Is.EqualTo(expectedStackSize));
    }

    [TestCase(1)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(9)]
    public static void PackedStackSegmentsConsumeTheirExactSize(int size)
    {
        var segment = AbiPassingSegment.OnStackWithoutConsumingFullSlot(17, 5, size);

        Assert.That(segment.IsPassedOnStack, Is.True);
        Assert.That(segment.StackOffset, Is.EqualTo(17));
        Assert.That(segment.Offset, Is.EqualTo(5));
        Assert.That(segment.Size, Is.EqualTo(size));
        Assert.That(segment.StackSize, Is.EqualTo(size));
    }

    [Test]
    public static void FromSegmentsCopiesOrderedRegisterSegmentsByValue()
    {
        var first = AbiPassingSegment.InRegister(Globals.IntArgRegs[0], 0, 8);
        var second = AbiPassingSegment.InRegister(Globals.IntArgRegs[1], 8, 8);
        var info = AbiPassingInformation.FromSegments(null!, in first, in second);

        Assert.That(info.NumSegments, Is.EqualTo(2));
        Assert.That(info.IsPassedByReference, Is.False);
        Assert.That(info.HasAnyRegisterSegment, Is.True);
        Assert.That(info.HasAnyStackSegment, Is.False);
        Assert.That(info.IsSplitAcrossRegistersAndStack, Is.False);
        Assert.That(info.Segments[0].Register, Is.EqualTo(Globals.IntArgRegs[0]));
        Assert.That(info.Segments[1].Register, Is.EqualTo(Globals.IntArgRegs[1]));
        Assert.That(info.Segments[1].Offset, Is.EqualTo(8));

        second.Offset = 24;
        Assert.That(second.Offset, Is.EqualTo(24));
        Assert.That(info.Segments[1].Register, Is.EqualTo(Globals.IntArgRegs[1]));
        Assert.That(info.Segments[1].Offset, Is.EqualTo(8));
    }

    [Test]
    public static void FromSegmentsPreservesRegisterAndPackedStackSegment()
    {
        var first = AbiPassingSegment.InRegister(Globals.IntArgRegs[0], 0, 8);
        var second = AbiPassingSegment.OnStackWithoutConsumingFullSlot(24, 8, 3);
        var info = AbiPassingInformation.FromSegments(null!, in first, in second);

        Assert.That(info.NumSegments, Is.EqualTo(2));
        Assert.That(info.IsPassedByReference, Is.False);
        Assert.That(info.HasAnyRegisterSegment, Is.True);
        Assert.That(info.HasAnyStackSegment, Is.True);
        Assert.That(info.IsSplitAcrossRegistersAndStack, Is.True);
        Assert.That(info.Segments[0].Register, Is.EqualTo(Globals.IntArgRegs[0]));
        Assert.That(info.Segments[1].StackOffset, Is.EqualTo(24));
        Assert.That(info.Segments[1].Offset, Is.EqualTo(8));
        Assert.That(info.Segments[1].Size, Is.EqualTo(3));
        Assert.That(info.Segments[1].StackSize, Is.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FromSegmentPreservesReferencePassing(bool passedByRef)
    {
        var segment = AbiPassingSegment.InRegister(Globals.IntArgRegs[0], 0, 8);
        var info = AbiPassingInformation.FromSegment(null!, passedByRef, in segment);

        Assert.That(info.NumSegments, Is.EqualTo(1));
        Assert.That(info.IsPassedByReference, Is.EqualTo(passedByRef));
        Assert.That(info.Segments[0].Register, Is.EqualTo(Globals.IntArgRegs[0]));
    }
}
