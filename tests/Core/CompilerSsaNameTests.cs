// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class CompilerSsaNameTests
{
    [TestCase(0x1234u, 0x5678u, 0x12345678u)]
    [TestCase(0x10001u, 0x20002u, 0x00030002u)]
    public static void HashCodePreservesNativeFieldPacking(uint localNumber, uint ssaNumber, uint expected)
    {
        var name = new Compiler.SSAName(localNumber, ssaNumber);

        Assert.That(Compiler.SSAName.GetHashCode(name), Is.EqualTo(expected));
    }

    [TestCase(1u, 2u, 1u, 2u, true)]
    [TestCase(1u, 2u, 3u, 2u, false)]
    [TestCase(1u, 2u, 1u, 3u, false)]
    public static void EqualityComparesBothNumbers(
        uint firstLocalNumber, uint firstSsaNumber, uint secondLocalNumber, uint secondSsaNumber, bool expected)
    {
        var first = new Compiler.SSAName(firstLocalNumber, firstSsaNumber);
        var second = new Compiler.SSAName(secondLocalNumber, secondSsaNumber);

        Assert.That(Compiler.SSAName.Equals(first, second), Is.EqualTo(expected));
    }
}
