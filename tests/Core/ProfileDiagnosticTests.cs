// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[SetCulture("en-US")]
internal static class ProfileDiagnosticTests
{
    [TestCase(0.0, 1)]
    [TestCase(-1.0, 1)]
    [TestCase(double.NaN, 1)]
    [TestCase(double.NegativeInfinity, 1)]
    [TestCase(0.5, 1)]
    [TestCase(9.5, 1)]
    [TestCase(65.0, 2)]
    [TestCase(130.0, 3)]
    [TestCase(999.0, 3)]
    [TestCase(12345.67, 5)]
    [TestCase(double.MaxValue, 309)]
    public static void DecimalDigitsMatchNativeRepeatedDivision(double value, int expected)
    {
        Assert.That(CountDigits(value), Is.EqualTo(expected));
    }

    [TestCase(10.0, 1, 2)]
    [TestCase(100.0, 2, 3)]
    [TestCase(1000.0, 3, 4)]
    public static void DecimalDigitsRespectAdjacentRepresentableValues(double boundary, int below, int at)
    {
        Assert.That(CountDigits(double.BitDecrement(boundary)), Is.EqualTo(below));
        Assert.That(CountDigits(boundary), Is.EqualTo(at));
        Assert.That(CountDigits(double.BitIncrement(boundary)), Is.EqualTo(at));
    }

    [TestCase(0.0, "0", "0")]
    [TestCase(0.0001, "0.0001", "0.0001")]
    [TestCase(0.00001, "1e-05", "1e-05")]
    [TestCase(-0.00001, "-1e-05", "-1e-05")]
    [TestCase(1e12, "1e+12", "1e+12")]
    [TestCase(3.6e13, "3.6e+13", "3.6e+13")]
    [TestCase(12345678.0, "1.234568e+07", "1.23e+07")]
    [TestCase(12345.67, "12345.67", "1.23e+04")]
    [TestCase(1.23456789, "1.234568", "1.23")]
    public static void ProfileWeightsRetainNativePrecisionAndExponentCase(
        double value, string regular, string narrow)
    {
        Assert.That(FMT_WT(value), Is.EqualTo(regular));
        Assert.That(FMT_WT_NARROW(value), Is.EqualTo(narrow));
    }
}
