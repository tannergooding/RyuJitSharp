// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using RyuJitSharp;

namespace RyuJitSharp.UnitTests;

internal static class ArrayStackTests
{
    private static readonly int[] s_expectedAfterGrowth =
    [
        0, 101, 2, 3, 4, 5, 6, 7, 8, 9,
        10, 11, 12, 13, 14, 15, 16, 17, 18, 100,
    ];

    private static readonly int[] s_expectedBottomUp = [1, 2, 3];
    private static readonly int[] s_expectedTopDown = [3, 2, 1];
    private static readonly int[] s_expectedAfterReferenceIteration = [2, 3, 4];
    private static readonly int[] s_expectedAfterReverse = [4, 3, 2, 1];

    [Test]
    public static void PushGrowsTheStackAndPreservesReferences()
    {
        var stack = new ArrayStack<int>(initialCapacity: 9);

        for (var i = 0; i < 20; i++)
        {
            stack.Push(i);
        }

        Assert.That(stack.Empty(), Is.False);
        Assert.That(stack.Height(), Is.EqualTo(20));
        Assert.That(stack.Top(), Is.EqualTo(19));
        Assert.That(stack.Top(4), Is.EqualTo(15));
        Assert.That(stack.Bottom(), Is.EqualTo(0));
        Assert.That(stack.Bottom(19), Is.EqualTo(19));

        stack.TopRef() = 100;
        stack.Data()[1] = 101;

        Assert.That(stack.Data().ToArray(), Is.EqualTo(s_expectedAfterGrowth));
    }

    [Test]
    public static void ViewsTraverseBottomUpAndTopDownByReference()
    {
        var stack = new ArrayStack<int>();
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        var bottomUp = new List<int>();
        foreach (var item in stack.BottomUpOrder())
        {
            bottomUp.Add(item);
        }

        var topDown = new List<int>();
        foreach (var item in stack.TopDownOrder())
        {
            topDown.Add(item);
        }

        Assert.That(bottomUp, Is.EqualTo(s_expectedBottomUp));
        Assert.That(topDown, Is.EqualTo(s_expectedTopDown));

        foreach (ref var item in stack.BottomUpOrder())
        {
            item++;
        }

        Assert.That(stack.Data().ToArray(), Is.EqualTo(s_expectedAfterReferenceIteration));
    }

    [Test]
    public static void PopResetAndReversePreserveStackOrder()
    {
        var stack = new ArrayStack<int>();
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        stack.Push(4);

        stack.Reverse();

        Assert.That(stack.Data().ToArray(), Is.EqualTo(s_expectedAfterReverse));
        Assert.That(stack.Pop(), Is.EqualTo(1));
        stack.Pop(2);
        Assert.That(stack.Height(), Is.EqualTo(1));
        Assert.That(stack.Top(), Is.EqualTo(4));

        stack.Reset();
        Assert.That(stack.Empty(), Is.True);
        Assert.That(stack.Height(), Is.Zero);
        Assert.That(stack.Data().IsEmpty, Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void EnumeratorsDetectMutations(bool bottomUp)
    {
        var stack = new ArrayStack<int>();
        stack.Push(1);

        if (bottomUp)
        {
            void IterateAndMutate()
            {
                var enumerator = stack.BottomUpOrder().GetEnumerator();
                Assert.That(enumerator.MoveNext(), Is.True);
                stack.Push(2);
                enumerator.Dispose();
            }

            if (TestConfiguration.IsDebug)
            {
                Assert.That(
                    (Action)IterateAndMutate,
                    Throws.Exception.With.Message.Contains("ArrayStack was modified during BottomUpOrder iteration"));
            }
            else
            {
                Assert.That((Action)IterateAndMutate, Throws.Nothing);
            }
        }
        else
        {
            void IterateAndMutate()
            {
                var enumerator = stack.TopDownOrder().GetEnumerator();
                Assert.That(enumerator.MoveNext(), Is.True);
                stack.Push(2);
                enumerator.Dispose();
            }

            if (TestConfiguration.IsDebug)
            {
                Assert.That(
                    (Action)IterateAndMutate,
                    Throws.Exception.With.Message.Contains("ArrayStack was modified during TopDownOrder iteration"));
            }
            else
            {
                Assert.That((Action)IterateAndMutate, Throws.Nothing);
            }
        }
    }
}
