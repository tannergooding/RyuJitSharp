// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class PhasedVarInitializationTests
{
    private static bool IsDebug => Configuration.DEBUG;

    [TestCase(0, 1)]
    [TestCase(-1, 0)]
    [TestCase(int.MinValue, int.MaxValue)]
    public static void InitializedIntegerCanBeOverriddenWithoutChangingPhase(int initial, int replacement)
    {
        WithoutAssertions(() => CheckInitialized(initial, replacement));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void InitializedBooleanRetainsTheUnconstrainedGenericContract(bool initial, bool replacement)
    {
        WithoutAssertions(() => CheckInitialized(initial, replacement));
    }

    [Test]
    public static void ReferenceValuesRetainIdentityAndAllowNullableInitialization()
    {
        WithoutAssertions(() => {
            var first = new object();
            var second = new object();
            var value = new PhasedVar<object?>(null);
            Assert.That(value.Value, Is.Null);

            value.OverrideAssign(first);
            Assert.That(value.Value, Is.SameAs(first));
            value.OverrideAssign(second);
            Assert.That(value.Value, Is.SameAs(second));
            value.OverrideAssign(null);
            Assert.That(value.Value, Is.Null);
            Assert.That(value.HasFinalValue, Is.True);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public static void OverrideInitializesAnUnwrittenDefaultValue(int assigned)
    {
        WithoutAssertions(() => {
            PhasedVar<int> value = default;
            if (IsDebug)
            {
                Assert.That(value.HasFinalValue, Is.False);
            }
            value.OverrideAssign(assigned);
            if (IsDebug)
            {
                Assert.That(value.HasFinalValue, Is.False);
            }
            Assert.That(value.Value, Is.EqualTo(assigned));
            Assert.That(value.HasFinalValue, Is.True);
        });
    }

    [Test]
    public static void CopiesRetainIndependentPhaseAndValueState()
    {
        WithoutAssertions(() => {
            var original = new PhasedVar<int>(7);
            var copy = original;
            copy.OverrideAssign(9);
            Assert.That(copy.Value, Is.EqualTo(9));
            if (IsDebug)
            {
                Assert.That(original.HasFinalValue, Is.False);
            }
            Assert.That(original.Value, Is.EqualTo(7));
            original.OverrideAssign(11);
            Assert.That(copy.Value, Is.EqualTo(9));
            Assert.That(original.Value, Is.EqualTo(11));
        });
    }

    private static void CheckInitialized<T>(T initial, T replacement)
    {
        var value = new PhasedVar<T>(initial);
        if (IsDebug)
        {
            Assert.That(value.HasFinalValue, Is.False);
        }
        Assert.That(value.Value, Is.EqualTo(initial));
        Assert.That(value.HasFinalValue, Is.True);
        value.ResetWritePhase();

        value.OverrideAssign(replacement);
        if (IsDebug)
        {
            Assert.That(value.HasFinalValue, Is.False);
        }
        Assert.That(value.Value, Is.EqualTo(replacement));
        Assert.That(value.HasFinalValue, Is.True);

        value.OverrideAssign(initial);
        Assert.That(value.HasFinalValue, Is.True);
        Assert.That(value.Value, Is.EqualTo(initial));

        value.ResetWritePhase();
        if (IsDebug)
        {
            Assert.That(value.HasFinalValue, Is.False);
        }
        value.OverrideAssign(replacement);
        if (IsDebug)
        {
            Assert.That(value.HasFinalValue, Is.False);
        }
        Assert.That(value.Value, Is.EqualTo(replacement));
        Assert.That(value.HasFinalValue, Is.True);
    }

    private static void WithoutAssertions(Action action)
    {
        if (TestConfiguration.IsDebug)
        {
            var (_, assertions) = TestConfiguration.CaptureDebugAssertions(action);
            Assert.That(assertions, Is.Empty);
        }
        else
        {
            action();
        }
    }
}
