// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class LoongArchEmitterStatisticsTests
{
    [Test]
    public static void JumpStatisticsHelperWrapsTheNativeCounter()
    {
        Assume.That(Configuration.TARGET_LOONGARCH64 && Configuration.EMITTER_STATS);

        const BindingFlags staticNonPublic = BindingFlags.NonPublic | BindingFlags.Static;
        var counter = typeof(Emitter).GetField("emitTotalIGjmps", staticNonPublic) ??
            throw new InvalidOperationException($"{typeof(Emitter).FullName}.emitTotalIGjmps is not available.");
        var increment = typeof(Emitter).GetMethod("IncrementTotalIGjmps", staticNonPublic) ??
            throw new InvalidOperationException($"{typeof(Emitter).FullName}.IncrementTotalIGjmps is not available.");
        var previous = (uint)counter.GetValue(null)!;

        try
        {
            counter.SetValue(null, uint.MaxValue);
            _ = increment.Invoke(null, null);

            Assert.That(counter.GetValue(null), Is.EqualTo(0u));
        }
        finally
        {
            counter.SetValue(null, previous);
        }
    }
}
