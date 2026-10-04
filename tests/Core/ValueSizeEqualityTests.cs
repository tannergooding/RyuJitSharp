// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class ValueSizeEqualityTests
{
    [Test]
    public static void UnknownSizesAreNeverEqual()
    {
        var first = ValueSize.Unknown;
        var second = ValueSize.Unknown;

        Assert.That(first == second, Is.False);
        Assert.That(first != second, Is.True);
        Assert.That(first.Equals(second), Is.False);
    }
}
