// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.CorInfoGCType;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

internal static class ClassLayoutTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(ushort.MaxValue * TARGET_POINTER_SIZE)]
    [TestCase((ushort.MaxValue * TARGET_POINTER_SIZE) + 1)]
    [TestCase((ushort.MaxValue + 1) * TARGET_POINTER_SIZE)]
    [TestCase(int.MaxValue)]
    public static void SlotCountPreservesLargeSizesAndRoundsPartialSlots(int size)
    {
        var layout = new ClassLayout(size);
        var expected = ((long)size + TARGET_POINTER_SIZE - 1) / TARGET_POINTER_SIZE;

        Assert.That(layout.SlotCount, Is.EqualTo(expected));
        Assert.That(layout.Size, Is.EqualTo(size));
    }

    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(9, false)]
    [TestCase(9, true)]
    [TestCase(65537, false)]
    [TestCase(65537, true)]
    public static void SlotQueriesPreserveGcClassification(int slotCount, bool hasGCPtr)
    {
        var layout = new ClassLayout(slotCount * TARGET_POINTER_SIZE);
        if (hasGCPtr)
        {
            layout.GCPtrCount = 1;
            layout._gcPtrs = new CorInfoGCType[slotCount];
            layout._gcPtrs[slotCount - 1] = TYPE_GC_BYREF;
        }

        Assert.That(layout.IsGCByRef(slotCount - 1), Is.EqualTo(hasGCPtr));
        Assert.That(layout.HasGCByRef(), Is.EqualTo(hasGCPtr));
    }
}
