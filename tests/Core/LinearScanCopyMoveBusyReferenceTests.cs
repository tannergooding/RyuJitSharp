// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanCopyMoveBusyReferenceTests
{
    [TestCase(false, false, false, 5u, false)]
    [TestCase(false, false, true, 10u, false)]
    [TestCase(true, false, false, 5u, true)]
    [TestCase(false, true, false, 10u, true)]
    [TestCase(true, true, false, 10u, true)]
    [TestCase(true, false, false, 11u, false)]
    [TestCase(false, true, true, 11u, true)]
    [TestCase(true, false, true, 12u, false)]
    public static void CopyMoveFlagsAndInclusiveReferenceEndDetermineBusyState(
        bool copy, bool move, bool delayed, uint location, bool expected)
    {
        var interval = new Interval(TYP_INT, SRBM_NONE);
        var reference = new RefPosition(1, 10, null, RefType.RefTypeUse) {
            copyReg = copy,
            moveReg = move,
            delayRegFree = delayed,
        };
        reference.setInterval(interval);

        Assert.That(copyOrMoveRegInUse(reference, location), Is.EqualTo(expected));
        Assert.That(reference.getRefEndLocation(), Is.EqualTo(delayed ? 11u : 10u));
        Assert.That(reference.copyReg, Is.EqualTo(copy));
        Assert.That(reference.moveReg, Is.EqualTo(move));
        Assert.That(interval.recentRefPosition, Is.Null);
    }

    [TestCase(false, false, 10u, false)]
    [TestCase(true, false, 10u, true)]
    [TestCase(false, true, 11u, true)]
    public static void FastPathsDoNotReadAnInterval(
        bool copy, bool move, uint location, bool expected)
    {
        var reference = new RefPosition(1, 10, null, RefType.RefTypeUse) {
            copyReg = copy,
            moveReg = move,
            delayRegFree = true,
        };

        Assert.That(copyOrMoveRegInUse(reference, location), Is.EqualTo(expected));
        Assert.That(reference.referent, Is.Null);
    }

    [TestCase(false, false, 19u, true)]
    [TestCase(false, false, 20u, true)]
    [TestCase(false, false, 21u, false)]
    [TestCase(true, true, 21u, true)]
    [TestCase(true, true, 22u, false)]
    public static void IntervalNextReferenceExtendsTheSameTreeLifetime(
        bool move, bool delayed, uint location, bool expected)
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var interval = new Interval(TYP_INT, SRBM_NONE);
            var reference = new RefPosition(1, 10, tree, RefType.RefTypeUse) {
                copyReg = !move,
                moveReg = move,
            };
            var next = new RefPosition(1, 20, tree, RefType.RefTypeUse) { delayRegFree = delayed };
            reference.setInterval(interval);
            next.setInterval(interval);
            interval.firstRefPosition = reference;
            interval.recentRefPosition = reference;
            reference.nextRefPosition = next;

            Assert.That(copyOrMoveRegInUse(reference, location), Is.EqualTo(expected));
            Assert.That(interval.getNextRefPosition(), Is.SameAs(next));
            Assert.That(interval.recentRefPosition, Is.SameAs(reference));
            Assert.That(reference.nextRefPosition, Is.SameAs(next));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IdenticalLookingTreesDoNotExtendAnotherTreeLifetime(bool move)
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var otherTree = compiler.gtNewIconNode(TYP_INT, 1);
            var interval = new Interval(TYP_INT, SRBM_NONE);
            var reference = new RefPosition(1, 10, tree, RefType.RefTypeUse) {
                copyReg = !move,
                moveReg = move,
            };
            var next = new RefPosition(1, 20, otherTree, RefType.RefTypeUse);
            reference.setInterval(interval);
            next.setInterval(interval);
            interval.firstRefPosition = next;

            Assert.That(tree.Oper, Is.EqualTo(otherTree.Oper));
            Assert.That(tree.AsIntCon().IconValue, Is.EqualTo(otherTree.AsIntCon().IconValue));
            Assert.That(copyOrMoveRegInUse(reference, 20), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NextReferenceComesFromTheIntervalCursorRatherThanTheInputReference(bool hasRecent)
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var otherTree = compiler.gtNewIconNode(TYP_INT, 2);
            var interval = new Interval(TYP_INT, SRBM_NONE);
            var reference = new RefPosition(1, 10, tree, RefType.RefTypeUse) { copyReg = true };
            var unrelatedNext = new RefPosition(1, 30, otherTree, RefType.RefTypeUse);
            var next = new RefPosition(1, 20, tree, RefType.RefTypeUse);
            reference.setInterval(interval);
            reference.nextRefPosition = unrelatedNext;
            next.setInterval(interval);
            interval.firstRefPosition = next;
            if (hasRecent)
            {
                var recent = new RefPosition(1, 15, otherTree, RefType.RefTypeUse) { nextRefPosition = next };
                recent.setInterval(interval);
                interval.recentRefPosition = recent;
                interval.firstRefPosition = unrelatedNext;
            }

            Assert.That(copyOrMoveRegInUse(reference, 20), Is.True);
            Assert.That(reference.nextRefPosition, Is.SameAs(unrelatedNext));
            Assert.That(interval.getNextRefPosition(), Is.SameAs(next));
        });
    }

    [Test]
    public static void NullTreePointersCompareEqualButAnAbsentNextReferenceDoesNot()
    {
        var interval = new Interval(TYP_INT, SRBM_NONE);
        var reference = new RefPosition(1, 10, null, RefType.RefTypeUse) { moveReg = true };
        var next = new RefPosition(1, 20, null, RefType.RefTypeUse);
        reference.setInterval(interval);
        next.setInterval(interval);
        interval.firstRefPosition = next;

        Assert.That(copyOrMoveRegInUse(reference, 20), Is.True);
        interval.firstRefPosition = null;
        Assert.That(copyOrMoveRegInUse(reference, 20), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OneNullTreePointerDoesNotAliasANode(bool nullCurrent)
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var interval = new Interval(TYP_INT, SRBM_NONE);
            var reference = new RefPosition(1, 10, nullCurrent ? null : tree, RefType.RefTypeUse) {
                copyReg = true,
            };
            var next = new RefPosition(1, 20, nullCurrent ? tree : null, RefType.RefTypeUse);
            reference.setInterval(interval);
            next.setInterval(interval);
            interval.firstRefPosition = next;

            Assert.That(copyOrMoveRegInUse(reference, 20), Is.False);
        });
    }

    [TestCase(false, uint.MaxValue, true)]
    [TestCase(true, 20u, false)]
    public static void IntervalNextReferenceUsesItsOwnUnsignedEnd(bool delayed, uint location, bool expected)
    {
        var interval = new Interval(TYP_INT, SRBM_NONE);
        var reference = new RefPosition(1, 10, null, RefType.RefTypeUse) { moveReg = true };
        var next = new RefPosition(1, uint.MaxValue, null, RefType.RefTypeUse) { delayRegFree = delayed };
        reference.setInterval(interval);
        next.setInterval(interval);
        interval.firstRefPosition = next;

        Assert.That(copyOrMoveRegInUse(reference, location), Is.EqualTo(expected));
    }

    [TestCase(false, 0u, true)]
    [TestCase(false, uint.MaxValue, true)]
    [TestCase(true, 0u, true)]
    [TestCase(true, 1u, false)]
    [TestCase(true, uint.MaxValue, false)]
    public static void ReferenceEndUsesNativeUnsignedWraparound(bool delayed, uint location, bool expected)
    {
        var interval = new Interval(TYP_INT, SRBM_NONE);
        var reference = new RefPosition(1, uint.MaxValue, null, RefType.RefTypeUse) {
            copyReg = true,
            delayRegFree = delayed,
        };
        reference.setInterval(interval);

        Assert.That(reference.getRefEndLocation(), Is.EqualTo(delayed ? 0u : uint.MaxValue));
        Assert.That(copyOrMoveRegInUse(reference, location), Is.EqualTo(expected));
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&jitInfo);
        compiler.info.compCompHnd = &jitInfo;
#endif
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

#if DEBUG
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression) => 0;
#endif
}
