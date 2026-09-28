// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CfgDebugInvariantTests
{
    private static int s_assertions;

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void EdgeVisitsRequireStateTransitions(bool initial, bool value)
    {
        WithAssertions(() => {
            var block = new BasicBlock(null, null);
            var edge = new FlowEdge(block, block, null);
            if (initial)
            {
                edge.Visited = true;
            }

            edge.Visited = value;

            Assert.That(s_assertions, Is.EqualTo(initial == value ? 1 : 0));
            Assert.That(edge.Visited, Is.EqualTo(value));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void BlockMembershipSearchesEvenWhenScratchRangeChecksAreDisabled(int level)
    {
        WithAssertions(() => {
            var previous = ExpensiveCheckLevel(ref JitConfig);
            try
            {
                ExpensiveCheckLevel(ref JitConfig) = level;
                var block = new BasicBlock(null, null);
                var first = new GenTree(GT_NOP, TYP_VOID);
                var last = new GenTree(GT_NOP, TYP_VOID);
                var outside = new GenTree(GT_NOP, TYP_VOID);
                block.MakeLir(null, null);
                block.InsertAtEnd(first);
                block.InsertAtEnd(last);
                var scratch = new LIR.ReadOnlyRange(first, last);
                var empty = new BasicBlock(null, null);
                empty.MakeLir(null, null);

                Assert.That(block.Contains(first), Is.True);
                Assert.That(block.Contains(last), Is.True);
                Assert.That(block.Contains(outside), Is.False);
                Assert.That(empty.Contains(outside), Is.False);
                Assert.That(scratch.Contains(outside), Is.EqualTo(level < 2));
                Assert.That(((LIR.ReadOnlyRange)block).Contains(outside), Is.EqualTo(level < 2));
                Assert.That(s_assertions, Is.Zero);
            }
            finally
            {
                ExpensiveCheckLevel(ref JitConfig) = previous;
            }
        });
    }

    [TestCase(false, 0)]
    [TestCase(false, 1)]
    [TestCase(false, 2)]
    [TestCase(true, 0)]
    [TestCase(true, 1)]
    [TestCase(true, 2)]
    public static void IteratorsDetectUnlinkedCurrentBlocks(bool bounded, int brokenLink)
    {
        WithAssertions(() => {
            var first = new BasicBlock(null, null);
            var middle = new BasicBlock(null, null);
            var last = new BasicBlock(null, null);
            Link(first, middle);
            Link(middle, last);
            using var iterator = GetIterator(first, last, bounded);
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.Current, Is.SameAs(middle));
            Assert.That(s_assertions, Is.Zero);

            if (brokenLink is 0 or 2)
            {
                PreviousBlock(last) = first;
            }

            if (brokenLink is 1 or 2)
            {
                NextBlock(first) = last;
            }

            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.Current, Is.SameAs(last));
            Assert.That(s_assertions, Is.EqualTo(brokenLink == 2 ? 2 : 1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IteratorsAllowInsertionAndRemovalAwayFromTheCurrentBlock(bool bounded)
    {
        WithAssertions(() => {
            var first = new BasicBlock(null, null);
            var middle = new BasicBlock(null, null);
            var last = new BasicBlock(null, null);
            var inserted = new BasicBlock(null, null);
            var end = new BasicBlock(null, null);
            Link(first, middle);
            Link(middle, last);
            Link(last, end);
            using var iterator = GetIterator(first, last, bounded);
            Assert.That(iterator.MoveNext(), Is.True);

            Link(first, inserted);
            Link(inserted, middle);
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.Current, Is.SameAs(inserted));

            Link(inserted, last);
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.Current, Is.SameAs(last));
            Assert.That(iterator.MoveNext(), Is.EqualTo(!bounded));
            Assert.That(iterator.MoveNext(), Is.False);
            Assert.That(s_assertions, Is.Zero);
        });
    }

    private static IEnumerator<BasicBlock> GetIterator(BasicBlock first, BasicBlock last, bool bounded)
    {
        return bounded ? new BasicBlockRangeList(first, last).GetEnumerator() : new BasicBlockSimpleList(first).GetEnumerator();
    }

    private static void Link(BasicBlock first, BasicBlock second)
    {
        first.Next = second;
        second.Prev = first;
    }

    private static void WithAssertions(Action action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;
        s_assertions = 0;
        try
        {
            action();
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions++;
        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitExpensiveDebugCheckLevel")]
    private static extern ref int ExpensiveCheckLevel(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_prev")]
    private static extern ref BasicBlock? PreviousBlock(BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_next")]
    private static extern ref BasicBlock? NextBlock(BasicBlock block);
}
#endif
