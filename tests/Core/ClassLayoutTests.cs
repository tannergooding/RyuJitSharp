// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
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

    [TestCase(0x8000_0000u, 0x1000_0000)]
    [TestCase(0xffff_fff0u, 0x1fff_fffe)]
    [TestCase(0xffff_fff8u, 0x1fff_ffff)]
    [TestCase(0xffff_fff9u, 0)]
    [TestCase(uint.MaxValue, 0)]
    public static void UnsignedSlotCountPreservesNativeRoundUpTruncation(uint size, int expectedSlots)
    {
        var layout = new ClassLayout(size);
        Assert.That(layout.Size, Is.EqualTo(size));
        Assert.That(layout.SlotCount, Is.EqualTo(expectedSlots));
        if (expectedSlots > 0)
        {
            Assert.That(layout.GetGCPtrType(0), Is.EqualTo(TYP_I_IMPL));
        }
    }

    [TestCase(0x8000_0000u)]
    [TestCase(0xffff_fff0u)]
    public static void UnsignedCustomLayoutRetainsKeyAndNonPaddingWithoutGcAllocation(uint size)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var builder = new ClassLayoutBuilder(compiler, size);
        var layout = ClassLayout.Create(compiler, builder);

        Assert.That(layout.Size, Is.EqualTo(size));
        Assert.That(layout._gcPtrs, Is.Null);
        Assert.That(new CustomLayoutKey(layout), Is.EqualTo(new CustomLayoutKey(builder)));
        Assert.That(layout.GetGCPtrType(0), Is.EqualTo(TYP_I_IMPL));

        var segments = layout.GetNonPadding(compiler);
        using var iterator = segments.GetEnumerator();
        Assert.That(iterator.MoveNext(), Is.True);
        Assert.That(iterator.Current.Start, Is.Zero);
        Assert.That(iterator.Current.End, Is.EqualTo(size));
        Assert.That(iterator.MoveNext(), Is.False);
    }

    [Test]
    public static void CustomLayoutTableSeparatesUnsignedSizesAndReusesMatchingBlocks()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var lower = compiler.typGetBlkLayout(0x7fff_fff0u);
        var first = compiler.typGetBlkLayout(0x8000_0000u);
        var second = compiler.typGetBlkLayout(0xffff_fff0u);
        var third = compiler.typGetBlkLayout(0x8000_0001u);

        Assert.That(first.Size, Is.EqualTo(0x8000_0000u));
        Assert.That(first, Is.SameAs(compiler.typGetBlkLayout(0x8000_0000u)));
        Assert.That(second, Is.SameAs(compiler.typGetBlkLayout(0xffff_fff0u)));
        Assert.That(third, Is.Not.SameAs(first));
        Assert.That(lower, Is.Not.SameAs(first));
        Assert.That(first._gcPtrs, Is.Null);
        Assert.That(second._gcPtrs, Is.Null);
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
