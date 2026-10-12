// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class AbiPassingSegmentStorageTests
{
    [Test]
    public static void RegisterNumberAndAbiSegmentKeepTheirStorageWidths()
    {
        Assert.That(Unsafe.SizeOf<regNumber>(), Is.EqualTo(sizeof(uint)));
        Assert.That(Unsafe.SizeOf<AbiPassingSegment>(), Is.EqualTo(16));
    }

    [Test]
    public static void RegisterSegmentsPreserveWasmTypeBits()
    {
        var wasmF64Register = (regNumber)0x80000000u;
        var segment = AbiPassingSegment.InRegister(wasmF64Register, 0, 8);

        Assert.That(segment.IsPassedInRegister, Is.True);
        Assert.That(segment.Register, Is.EqualTo(wasmF64Register));
    }

    [Test]
    public static void StackSegmentsPreserveFullAndPartialSlotSemantics()
    {
        var fullSlotSegment = AbiPassingSegment.OnStack(12, 4, 3);
        var partialSlotSegment = AbiPassingSegment.OnStackWithoutConsumingFullSlot(20, 4, 3);

        Assert.That(fullSlotSegment.IsPassedOnStack, Is.True);
        Assert.That(fullSlotSegment.StackOffset, Is.EqualTo(12));
        Assert.That(fullSlotSegment.StackSize, Is.GreaterThan(partialSlotSegment.StackSize));

        Assert.That(partialSlotSegment.IsPassedOnStack, Is.True);
        Assert.That(partialSlotSegment.StackOffset, Is.EqualTo(20));
        Assert.That(partialSlotSegment.StackSize, Is.EqualTo(3));
    }
}
