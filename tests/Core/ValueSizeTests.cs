// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class ValueSizeTests
{
    private static ValueSize[] Sizes => [new(0), new(8), new(uint.MaxValue), ValueSize.Mask, ValueSize.Vector, ValueSize.Unknown];

    [TestCaseSource(nameof(Sizes))]
    public static void BoxedEqualityPreservesKindAndSize(ValueSize value)
    {
        object boxed = value;

        foreach (var other in Sizes)
        {
            var expected = value == other;
            Assert.That(value.Equals((object)other), Is.EqualTo(expected));
            Assert.That(boxed.Equals(other), Is.EqualTo(expected));
            Assert.That(value.Equals(other), Is.EqualTo(expected));

            if (expected)
            {
                Assert.That(boxed.GetHashCode(), Is.EqualTo(other.GetHashCode()));
            }
        }

        Assert.That(value.Equals(null), Is.False);
        Assert.That(value.Equals((object)0u), Is.False);
        Assert.That(value.Equals((object)false), Is.False);
        Assert.That(value.Equals((object)"8"), Is.False);
    }
}
