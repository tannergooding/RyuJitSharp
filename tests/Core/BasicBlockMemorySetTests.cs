// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class BasicBlockMemorySetTests
{
    private const int UnusedBits = unchecked((int)0xA5A5_FC00);

    private static readonly PropertyInfo[] s_memorySets = [
        GetProperty(nameof(BasicBlock.bbMemoryUse)),
        GetProperty(nameof(BasicBlock.bbMemoryDef)),
        GetProperty(nameof(BasicBlock.bbMemoryLiveIn)),
        GetProperty(nameof(BasicBlock.bbMemoryLiveOut)),
        GetProperty(nameof(BasicBlock.bbMemoryHavoc)),
    ];

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public static void EveryMaskRoundTripsWithoutChangingNeighborsOrUnusedBits(int selectedField)
    {
        var block = new BasicBlock(null, null);
        for (var rotation = 0; rotation < 4; rotation++)
        {
            for (var mask = 0; mask < 4; mask++)
            {
                var expected = InitializeNeighbors(block, rotation);
                expected[selectedField] = mask;

                SetMask(s_memorySets[selectedField], block, mask);

                AssertState(block, expected);
            }
        }
    }

    [TestCase(4)]
    [TestCase(7)]
    [TestCase(-1)]
    public static void AssignmentTruncatesToTheNativeBitfieldWidth(int value)
    {
        var block = new BasicBlock(null, null);
        for (var selectedField = 0; selectedField < s_memorySets.Length; selectedField++)
        {
            var expected = InitializeNeighbors(block, 0);
            expected[selectedField] = value & 3;

            SetMask(s_memorySets[selectedField], block, value);

            AssertState(block, expected);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void VariableSetInitializationClearsFourMemorySetsAndPreservesAllHavocMasks(int havoc)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 65;
        compiler.lvaTrackedCountInSizeTUnits = 2;
        var block = new BasicBlock(null, null);
        _ = InitializeNeighbors(block, 0);
        foreach (var property in s_memorySets)
        {
            SetMask(property, block, 3);
        }
        SetMask(s_memorySets[4], block, havoc);
        block.bbMemorySsaNumIn[0] = 13;
        block.bbMemorySsaNumOut[1] = 17;

        block.InitVarSets(compiler);

        AssertState(block, [0, 0, 0, 0, havoc]);
        Assert.That(block.bbVarUse, Has.Length.EqualTo(2).And.All.EqualTo((nint)0));
        Assert.That(block.bbVarDef, Has.Length.EqualTo(2).And.All.EqualTo((nint)0));
        Assert.That(block.bbLiveIn, Has.Length.EqualTo(2).And.All.EqualTo((nint)0));
        Assert.That(block.bbLiveOut, Has.Length.EqualTo(2).And.All.EqualTo((nint)0));
        Assert.That(block.bbMemorySsaNumIn[0], Is.EqualTo(13));
        Assert.That(block.bbMemorySsaNumOut[1], Is.EqualTo(17));
    }

    [Test]
    public static void AccessorsRepresentSetsRatherThanIndividualMemoryKindEnums()
    {
        foreach (var property in s_memorySets)
        {
            Assert.That(property.PropertyType, Is.EqualTo(typeof(int)), property.Name);
        }
    }

    private static int[] InitializeNeighbors(BasicBlock block, int rotation)
    {
        PackedMemory(block) = UnusedBits;
        var values = new int[s_memorySets.Length];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = (rotation + index) & 3;
            SetMask(s_memorySets[index], block, values[index]);
        }

        return values;
    }

    private static void AssertState(BasicBlock block, int[] expected)
    {
        var packed = UnusedBits;
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.That(Convert.ToInt32(s_memorySets[index].GetValue(block), CultureInfo.InvariantCulture), Is.EqualTo(expected[index]),
                s_memorySets[index].Name);
            packed |= expected[index] << (2 * index);
        }
        Assert.That(PackedMemory(block), Is.EqualTo(packed));
    }

    private static PropertyInfo GetProperty(string name)
    {
        return typeof(BasicBlock).GetProperty(name) ?? throw new MissingMemberException(nameof(BasicBlock), name);
    }

    private static void SetMask(PropertyInfo property, BasicBlock block, int mask)
    {
        // Reflection lets the packing checks detect incorrect accessor types as well as bit widths.
        var value = property.PropertyType.IsEnum ? Enum.ToObject(property.PropertyType, mask) : mask;
        property.SetValue(block, value);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bitfield")]
    private static extern ref int PackedMemory(BasicBlock block);
}
