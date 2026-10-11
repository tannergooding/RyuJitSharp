// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class InstructionOrdinalMappingTests
{
    [Test]
    public static void NativeInstructionOrdinalsRoundTrip()
    {
        var nativeOrdinals = new HashSet<int>();
        var mappedCount = 0;
        var unsupportedCount = 0;
        var unionInstructionCount = (int)INS_none;

        for (var instructionIndex = 0; instructionIndex <= unionInstructionCount; instructionIndex++)
        {
            var ins = (instruction)instructionIndex;
            if (!InstructionOrdinalMaps.TryGetNativeInstructionOrdinal(ins, out var nativeOrdinal))
            {
                unsupportedCount++;
                continue;
            }

            mappedCount++;
            Assert.That(nativeOrdinals.Add(nativeOrdinal), Is.True, $"Duplicate native ordinal {nativeOrdinal}.");
            Assert.That(InstructionOrdinalMaps.GetInstructionFromNativeOrdinal(nativeOrdinal), Is.EqualTo(ins));
        }

        var nativeInstructionCount = InstructionOrdinalMaps.GetNativeInstructionCount();
        Assert.That(nativeInstructionCount, Is.GreaterThan(0));
        Assert.That(mappedCount, Is.EqualTo(nativeInstructionCount + 1));
        Assert.That(unsupportedCount, Is.GreaterThan(0));
        Assert.That(nativeOrdinals.Count, Is.EqualTo(mappedCount));
        Assert.That(InstructionOrdinalMaps.TryGetNativeInstructionOrdinal((instruction)int.MaxValue, out _), Is.False);
        Assert.That(InstructionOrdinalMaps.TryGetNativeInstructionOrdinal((instruction)(-1), out _), Is.False);
        Assert.That(InstructionOrdinalMaps.GetInstructionFromNativeOrdinal(nativeInstructionCount), Is.EqualTo(INS_none));
        _ = Assert.Throws<PlatformNotSupportedException>(() =>
            InstructionOrdinalMaps.GetNativeInstructionOrdinal((instruction)int.MaxValue));
        _ = Assert.Throws<PlatformNotSupportedException>(() =>
            InstructionOrdinalMaps.GetNativeInstructionOrdinal((instruction)(-1)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => InstructionOrdinalMaps.GetInstructionFromNativeOrdinal(-1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionOrdinalMaps.GetInstructionFromNativeOrdinal(nativeInstructionCount + 1));
    }
}
