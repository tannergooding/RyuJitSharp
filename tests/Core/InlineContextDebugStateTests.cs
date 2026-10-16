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
}
