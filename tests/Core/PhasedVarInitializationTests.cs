// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
#if DEBUG
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class PhasedVarInitializationTests
{
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
#if DEBUG
            Assert.That(value.HasFinalValue, Is.False);
#endif
            value.OverrideAssign(assigned);
#if DEBUG
            Assert.That(value.HasFinalValue, Is.False);
#endif
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
#if DEBUG
            Assert.That(original.HasFinalValue, Is.False);
#endif
            Assert.That(original.Value, Is.EqualTo(7));
            original.OverrideAssign(11);
            Assert.That(copy.Value, Is.EqualTo(9));
            Assert.That(original.Value, Is.EqualTo(11));
        });
    }

    private static void CheckInitialized<T>(T initial, T replacement)
    {
        var value = new PhasedVar<T>(initial);
#if DEBUG
        Assert.That(value.HasFinalValue, Is.False);
#endif
        Assert.That(value.Value, Is.EqualTo(initial));
        Assert.That(value.HasFinalValue, Is.True);
        value.ResetWritePhase();

        value.OverrideAssign(replacement);
#if DEBUG
        Assert.That(value.HasFinalValue, Is.False);
#endif
        Assert.That(value.Value, Is.EqualTo(replacement));
        Assert.That(value.HasFinalValue, Is.True);

        value.OverrideAssign(initial);
        Assert.That(value.HasFinalValue, Is.True);
        Assert.That(value.Value, Is.EqualTo(initial));

        value.ResetWritePhase();
#if DEBUG
        Assert.That(value.HasFinalValue, Is.False);
#endif
        value.OverrideAssign(replacement);
#if DEBUG
        Assert.That(value.HasFinalValue, Is.False);
#endif
        Assert.That(value.Value, Is.EqualTo(replacement));
        Assert.That(value.HasFinalValue, Is.True);
    }

    private static unsafe void WithoutAssertions(Action action)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = new() { doAssert = &RecordAssertion };
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertionCount = 0;
#endif
        action();
#if DEBUG
        Assert.That(s_assertionCount, Is.Zero);
#endif
    }

#if DEBUG
    private static int s_assertionCount;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertionCount++;
        return 0;
    }
#endif
}
