// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class InlineContextDebugStateTests
{
    [Test]
    public static void DebugStateUsesPerContextStorageAndReleaseAccessFails()
    {
        const BindingFlags instanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
        Assert.That(typeof(InlineContext).GetField("_policy", instanceFields), Is.Null);
        Assert.That(typeof(InlineContext).GetField("_treeId", instanceFields), Is.Null);
        Assert.That(typeof(InlineContext).GetField("_ilInstsSet", instanceFields), Is.Null);

        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        var context = new InlineContext(strategy);
        var otherContext = new InlineContext(strategy);

        if (TestConfiguration.IsDebug)
        {
            Assert.That(context._ilInstsSet.Count, Is.Zero);
            Assert.That(context._ilInstsSet, Is.Not.SameAs(otherContext._ilInstsSet));

            var ilInstsSet = new BitArray(7, true);
            context._ilInstsSet = ilInstsSet;
            Assert.That(context._ilInstsSet, Is.SameAs(ilInstsSet));

            var policy = (InlinePolicy)RuntimeHelpers.GetUninitializedObject(typeof(DefaultPolicy));
            context._policy = policy;
            Assert.That(context._policy, Is.SameAs(policy));

            context._treeId = 42;
            Assert.That(context._treeId, Is.EqualTo(42));

            context._flags = InlineContext.Flags.AsyncCall
                | InlineContext.Flags.Devirtualized
                | InlineContext.Flags.Guarded
                | InlineContext.Flags.Unboxed;

            Assert.That(context.IsAsyncCall, Is.True);
            Assert.That(context.IsDevirtualized, Is.True);
            Assert.That(context.IsGuarded, Is.True);
            Assert.That(context.IsUnboxed, Is.True);
            Assert.That(context.ILInstsSet, Is.SameAs(ilInstsSet));
        }
        else
        {
            _ = Assert.Throws<InvalidOperationException>(() => _ = context._ilInstsSet);
            _ = Assert.Throws<InvalidOperationException>(() => context._ilInstsSet = new BitArray(1));
            _ = Assert.Throws<InvalidOperationException>(() => _ = context.ILInstsSet);
            _ = Assert.Throws<InvalidOperationException>(() => _ = context._policy);
            _ = Assert.Throws<InvalidOperationException>(() => context._policy = null);
            _ = Assert.Throws<InvalidOperationException>(() => _ = context._treeId);
            _ = Assert.Throws<InvalidOperationException>(() => context._treeId = 42);
            _ = Assert.Throws<InvalidOperationException>(() => _ = context.IsAsyncCall);
            _ = Assert.Throws<InvalidOperationException>(() => _ = context.IsDevirtualized);
            _ = Assert.Throws<InvalidOperationException>(() => _ = context.IsGuarded);
            _ = Assert.Throws<InvalidOperationException>(() => _ = context.IsUnboxed);
        }
    }

    [Test]
    public static void DebugStatePublicationWorksForUninitializedContexts()
    {
        var context = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));

        if (TestConfiguration.IsDebug)
        {
            var ilInstsSet = new BitArray(11, true);
            context._ilInstsSet = ilInstsSet;
            Assert.That(context._ilInstsSet, Is.SameAs(ilInstsSet));
        }
        else
        {
            _ = Assert.Throws<InvalidOperationException>(() => context._ilInstsSet = new BitArray(11, true));
        }
    }

    [Test]
    public static void InlineCallDiagnosticStateIsDebugOnly()
    {
        var call = (GenTreeCall)RuntimeHelpers.GetUninitializedObject(typeof(GenTreeCall));

        if (TestConfiguration.IsDebug)
        {
            var expectedFlags = GenTreeCallDebugFlags.GTF_CALL_MD_DEVIRTUALIZED
                | GenTreeCallDebugFlags.GTF_CALL_MD_UNBOXED;
            call._callDebugFlags = expectedFlags;
            call._inlineObservation = InlineObservation.CALLSITE_IS_CALL_TO_HELPER;
            call._rawILOffset = 42;
            var debugData = Compiler.DebugData.Get(call);
            Assert.That(debugData.CallDebugFlags, Is.EqualTo(expectedFlags));
            Assert.That(debugData.InlineObservation, Is.EqualTo(InlineObservation.CALLSITE_IS_CALL_TO_HELPER));
            Assert.That(debugData.RawILOffset, Is.EqualTo(42));
            Assert.That(call._callDebugFlags, Is.EqualTo(expectedFlags));
            Assert.That(call._inlineObservation, Is.EqualTo(InlineObservation.CALLSITE_IS_CALL_TO_HELPER));
            Assert.That(call._rawILOffset, Is.EqualTo(42));
            Assert.That(call.IsDevirtualized, Is.True);
            Assert.That(call.IsUnboxed, Is.True);
            Assert.That(call.IsGuarded, Is.False);
            Assert.That(call.IsStressTailCall, Is.False);
            Assert.That(call.WasInlineCandidate, Is.False);

            call.IsGuarded = true;
            Assert.That(call.IsGuarded, Is.True);

            call._callDebugFlags |= GenTreeCallDebugFlags.GTF_CALL_MD_STRESS_TAILCALL;
            Assert.That(call.IsStressTailCall, Is.True);

            call.WasInlineCandidate = true;
            Assert.That(call.WasInlineCandidate, Is.True);
        }
        else
        {
            _ = Assert.Throws<InvalidOperationException>(() => _ = call._callDebugFlags);
            _ = Assert.Throws<InvalidOperationException>(
                () => call._callDebugFlags = GenTreeCallDebugFlags.GTF_CALL_MD_EMPTY);
            _ = Assert.Throws<InvalidOperationException>(() => _ = call._inlineObservation);
            _ = Assert.Throws<InvalidOperationException>(
                () => call._inlineObservation = InlineObservation.CALLSITE_IS_CALL_TO_HELPER);
            _ = Assert.Throws<InvalidOperationException>(() => _ = call._rawILOffset);
            _ = Assert.Throws<InvalidOperationException>(() => call._rawILOffset = 42);
            _ = Assert.Throws<InvalidOperationException>(() => _ = call.IsDevirtualized);
            _ = Assert.Throws<InvalidOperationException>(() => _ = call.IsUnboxed);
            _ = Assert.Throws<InvalidOperationException>(() => _ = call.IsGuarded);
            _ = Assert.Throws<InvalidOperationException>(() => call.IsGuarded = true);
            Assert.That(call.IsStressTailCall, Is.False);
            _ = Assert.Throws<InvalidOperationException>(() => _ = call.WasInlineCandidate);
            _ = Assert.Throws<InvalidOperationException>(() => call.WasInlineCandidate = true);
        }
    }
}
