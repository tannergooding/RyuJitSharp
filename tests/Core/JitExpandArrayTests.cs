// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using RyuJitSharp;

namespace RyuJitSharp.UnitTests;

internal static class JitExpandArrayTests
{
    private sealed class TestArray<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : JitExpandArray<T>
    {
        internal TestArray(uint minSize = 1)
            : base(minSize)
        {
        }

        internal uint Capacity => m_size;
    }

    private struct InitializedValue
    {
        internal int Value;

        public InitializedValue()
        {
            Value = 42;
        }
    }

    [Test]
    public static void IndexedAccessUsesMinimumAndDoublingGrowthAndInitializesNewElements()
    {
        var array = new TestArray<int>(3);

        Assert.That(array.Capacity, Is.Zero);
        Assert.That(array.Get(1), Is.Zero);
        Assert.That(array.Capacity, Is.EqualTo(3u));

        array.GetRef(2) = 12;
        array.Set(3, 13);

        Assert.That(array.Capacity, Is.EqualTo(6u));
        Assert.That(array.Get(4), Is.Zero);
        Assert.That(array.Get(2), Is.EqualTo(12));

        array[10] = 20;

        Assert.That(array.Capacity, Is.EqualTo(12u));
        Assert.That(array.Get(3), Is.EqualTo(13));
        Assert.That(array.Get(10), Is.EqualTo(20));
    }

    [Test]
    public static void ResetClearsStorageAndInitRestoresUnallocatedState()
    {
        var array = new TestArray<int>();

        array.Reset(3);
        array.Set(2, 22);
        array.Set(3, 33);
        Assert.That(array.Capacity, Is.EqualTo(6u));

        array.Reset(8);

        Assert.That(array.Capacity, Is.EqualTo(12u));
        Assert.That(array.Get(2), Is.Zero);
        Assert.That(array.Get(3), Is.Zero);
        Assert.That(array.Get(11), Is.Zero);

        array.Init(4);

        Assert.That(array.Capacity, Is.Zero);
        Assert.That(array.Get(0), Is.Zero);
        Assert.That(array.Capacity, Is.EqualTo(4u));
    }

    [Test]
    public static void InitializationRunsExplicitValueTypeDefaultConstructor()
    {
        var array = new TestArray<InitializedValue>(3);

        Assert.That(array.Get(0).Value, Is.EqualTo(42));
        Assert.That(array.Get(2).Value, Is.EqualTo(42));

        array.GetRef(0).Value = 11;
        array.Reset();

        Assert.That(array.Get(0).Value, Is.EqualTo(42));
        Assert.That(array.Get(1).Value, Is.EqualTo(42));
    }

    [Test]
    public static void StackIndexedAccessPushAndTopMaintainDepthAndValues()
    {
        var stack = new JitExpandArrayStack<int>(2);

        Assert.That(stack.Push(10), Is.Zero);
        Assert.That(stack.Push(20), Is.EqualTo(1u));
        Assert.That(stack.Push(30), Is.EqualTo(2u));

        stack.GetRef(5) = 60;
        stack.Set(6, 70);

        Assert.That(stack.Size(), Is.EqualTo(7u));
        Assert.That(stack.GetNoExpand(0), Is.EqualTo(10));
        Assert.That(stack.GetNoExpand(2), Is.EqualTo(30));
        stack.GetRefNoExpand(3) = 33;
        Assert.That(stack.GetNoExpand(3), Is.EqualTo(33));
        Assert.That(stack.GetNoExpand(5), Is.EqualTo(60));
        Assert.That(stack.Top(), Is.EqualTo(70));

        stack.TopRef() = 71;

        Assert.That(stack.Top(), Is.EqualTo(71));
    }

    [Test]
    public static void StackRemovePopAndResetPreserveRemainingOrder()
    {
        var stack = new JitExpandArrayStack<int>();

        _ = stack.Push(1);
        _ = stack.Push(2);
        _ = stack.Push(3);
        stack.Remove(1);

        Assert.That(stack.Size(), Is.EqualTo(2u));
        Assert.That(stack.GetNoExpand(1), Is.EqualTo(3));
        Assert.That(stack.Pop(), Is.EqualTo(3));
        Assert.That(stack.Pop(), Is.EqualTo(1));
        Assert.That(stack.Size(), Is.Zero);

        _ = stack.Push(4);
        stack.Reset();

        Assert.That(stack.Size(), Is.Zero);
        Assert.That(stack.Push(5), Is.Zero);
        Assert.That(stack.Top(), Is.EqualTo(5));
    }

    [Test]
    public static void MaximumIndexOverflowIsRejectedBeforeAllocation()
    {
        var array = new TestArray<int>();

        void AccessMaximumIndex()
        {
            _ = array.Get(uint.MaxValue);
        }

        _ = Assert.Throws<OverflowException>(AccessMaximumIndex);

        Assert.That(array.Capacity, Is.Zero);
    }
}
