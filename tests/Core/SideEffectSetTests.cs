// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class SideEffectSetTests
{
    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void IsLirInvariantInRangeStopsAtTheExclusiveEndpoint(bool middleHasOrderedSideEffect, bool expected)
    {
        WithCompiler(compiler =>
        {
            var start = OrderedDivide(1, 2);
            var middle = CreateMiddleNode(middleHasOrderedSideEffect);
            var end = OrderedDivide(5, 6);
            start.Next = middle;
            middle.Next = end;

            var sideEffects = new SideEffectSet(compiler, start);
            Assert.That(sideEffects.IsLirInvariantInRange(compiler, start, end), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void LclVarSetPreservesPinnedSingleElementEmptiness()
    {
        WithCompiler(compiler =>
        {
            var locals = new LclVarSet();
            Assert.That(locals.IsEmpty, Is.True);

            locals.Add(compiler, 17);
            Assert.That(locals.Contains(17), Is.True);
            Assert.That(locals.IsEmpty, Is.True);

            locals.Add(compiler, 23);
            Assert.That(locals.Contains(17), Is.True);
            Assert.That(locals.Contains(23), Is.True);
            Assert.That(locals.IsEmpty, Is.False);

            locals.Clear();
            Assert.That(locals.IsEmpty, Is.True);
        });
    }

    private static GenTreeOp OrderedDivide(int left, int right)
        => new GenTreeOp(GT_DIV, TYP_INT, new GenTreeIntCon(TYP_INT, left), new GenTreeIntCon(TYP_INT, right))
        {
            Flags = GTF_ORDER_SIDEEFF,
        };

    private static GenTree CreateMiddleNode(bool hasOrderedSideEffect)
        => hasOrderedSideEffect ? OrderedDivide(3, 4) : new GenTreeIntCon(TYP_INT, 2);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
