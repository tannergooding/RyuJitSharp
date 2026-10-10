// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class EmitterDataSectionDiagnosticsTests
{
    [TestCase(0x7F80FF80u, "nan")]
    [TestCase(0xFF80FF80u, "-nan")]
    public static void FormatDataFloatPreservesNaNSign(uint bits, string expected)
    {
        var value = (double)BitConverter.Int32BitsToSingle(unchecked((int)bits));

        Assert.That(FormatDataFloat(null, value, 6, 9), Is.EqualTo(expected.PadLeft(9)));
    }

    [Test]
    public static void FormatDataFloatFormatsNegativeIndefiniteFloatNaN()
    {
        var value = (double)BitConverter.Int32BitsToSingle(unchecked((int)0xFFC00000u));
        var expected = OperatingSystem.IsWindows() ? "-nan(ind)" : "-nan";

        Assert.That(FormatDataFloat(null, value, 6, 9), Is.EqualTo(expected.PadLeft(9)));
    }

    [Test]
    public static void FormatDataFloatFormatsNegativeIndefiniteDoubleNaN()
    {
        var value = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000000ul));
        var expected = OperatingSystem.IsWindows() ? "-nan(ind)" : "-nan";

        Assert.That(FormatDataFloat(null, value, 9, 12), Is.EqualTo(expected.PadLeft(12)));
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "FormatDataFloat")]
    private static extern string FormatDataFloat(Emitter? emitter, double value, int precision, int width);
}
