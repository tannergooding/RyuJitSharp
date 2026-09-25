// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp.UnitTests;

internal static class TernaryLogicInfoTests
{
    [Test]
    public static void NativeTableReconstructsEveryControlAndOperandPermutation()
    {
        byte[] keys = [0xF0, 0xCC, 0xAA];
        foreach (var first in keys)
        {
            foreach (var second in keys)
            {
                foreach (var third in keys)
                {
                    if ((first == second) || (first == third) || (second == third))
                    {
                        continue;
                    }

                    for (var value = 0; value < 256; value++)
                    {
                        var control = (byte)value;
                        var info = TernaryLogicInfo.Lookup(control);
                        var expected = SubstituteInputs(control, first, second, third);

                        Assert.That(TernaryLogicInfo.GetTernaryControlByte(info, first, second, third),
                            Is.EqualTo(expected), $"Control 0x{control:X2}, inputs {first:X2}/{second:X2}/{third:X2}");
                    }
                }
            }
        }
    }

    [Test]
    public static void NativeUsageFlagsMatchTruthTableDependencies()
    {
        for (var value = 0; value < 256; value++)
        {
            var control = (byte)value;
            var flags = TernaryLogicInfo.Lookup(control).GetAllUseFlags();
            var expected = TernaryLogicUseFlags.None;
            if (((control ^ (control >> 4)) & 0x0F) != 0)
            {
                expected |= TernaryLogicUseFlags.A;
            }
            if (((control ^ (control >> 2)) & 0x33) != 0)
            {
                expected |= TernaryLogicUseFlags.B;
            }
            if (((control ^ (control >> 1)) & 0x55) != 0)
            {
                expected |= TernaryLogicUseFlags.C;
            }

            Assert.That(flags, Is.EqualTo(expected), $"Control 0x{control:X2}");
        }
    }

    [Test]
    public static void AllNativeEntriesReconstructTheirOwnControlByte()
    {
        for (var value = 0; value < 256; value++)
        {
            var control = (byte)value;
            var info = TernaryLogicInfo.Lookup(control);
            Assert.That(TernaryLogicInfo.GetTernaryControlByte(info, 0xF0, 0xCC, 0xAA),
                Is.EqualTo(control), $"Control 0x{control:X2}");
        }
    }

    [TestCase(GT_AND, 0xC0)]
    [TestCase(GT_AND_NOT, 0x0C)]
    [TestCase(GT_OR, 0xFC)]
    [TestCase(GT_XOR, 0x3C)]
    public static void BinaryControlByteFollowsNativeOperandOrder(genTreeOps oper, int expected)
    {
        Assert.That(TernaryLogicInfo.GetTernaryControlByte(oper, 0xF0, 0xCC), Is.EqualTo(expected));
    }

    private static byte SubstituteInputs(byte control, byte first, byte second, byte third)
    {
        var result = 0;
        for (var bit = 0; bit < 8; bit++)
        {
            var source = (((first >> bit) & 1) << 2) |
                (((second >> bit) & 1) << 1) | ((third >> bit) & 1);
            result |= ((control >> source) & 1) << bit;
        }
        return (byte)result;
    }
}
