// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ConfigMethodRangeTests
{
    private static void AssumeDebug()
    {
        Assume.That(TestConfiguration.IsDebug);
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(3, true)]
    public static void InlineDisableRangePreservesUnsetAndCachedSelection(int selection, bool expected)
    {
        AssumeDebug();

        using var jitTls = TestConfiguration.CreateJitTls();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compFullName = "InlineRange";
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        var compilerField = typeof(InlineStrategy).GetField("_compiler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        compilerField.SetValue(strategy, compiler);
        var previousRange = Compiler.DebugData.MethodRangeData.DisabledRange;
        var previousConfig = Globals.JitConfig;
        var config = previousConfig;
        var hash = unchecked((uint)compiler.info.compMethodHash());
        var text = selection switch {
            0 => null,
            1 => $"{hash:x8}",
            2 => $"{unchecked(hash + 1):x8}",
            _ => "",
        };
        var bytes = text is null ? null : Encoding.ASCII.GetBytes(text + '\0');

        try
        {
            Compiler.DebugData.MethodRangeData.DisabledRange = default;
            fixed (byte* pointer = bytes)
            {
                TestConfiguration.GetJitConfigDebugDataForWrite(ref config)._jitNoInlineRange = pointer;
                Globals.JitConfig = config;

                for (var attempt = 0; attempt < 3; attempt++)
                {
                    Assert.That(strategy.IsInliningDisabled(), Is.EqualTo(expected), $"attempt {attempt}");
                }

                TestConfiguration.GetJitConfigDebugDataForWrite(ref config)._jitNoInlineRange = null;
                Globals.JitConfig = config;
                Assert.That(strategy.IsInliningDisabled(), Is.False);
            }
        }
        finally
        {
            Globals.JitConfig = previousConfig;
            Compiler.DebugData.MethodRangeData.DisabledRange = previousRange;
        }
    }

    [TestCase(null, -1, true)]
    [TestCase("", int.MinValue, true)]
    [TestCase("0", 0, true)]
    [TestCase("7fffffff", int.MaxValue, true)]
    [TestCase("80000000", int.MinValue, true)]
    [TestCase("ffffffff", -1, true)]
    [TestCase("80000000-ffffffff", -1, true)]
    [TestCase("80000000-ffffffff", int.MaxValue, false)]
    [TestCase("1 2", 2, true)]
    [TestCase("1,2", 3, false)]
    [TestCase("2-", -1, true)]
    [TestCase("2-", 1, false)]
    [TestCase("-2", 0, true)]
    [TestCase("-2", 3, false)]
    [TestCase("1 ", 0, false)]
    [TestCase("1, ", 0, true)]
    [TestCase(" ", 0, true)]
    [TestCase(" ", 1, false)]
    [TestCase("2- ", 2, false)]
    [TestCase("3-1", 2, false)]
    [TestCase("g", 0, true)]
    [TestCase("g", 1, false)]
    [TestCase("100000000", 0, true)]
    [TestCase("00000000", 0, true)]
    public static void RangesPreserveNativeHexadecimalAndBoundarySemantics(string? text, int hash, bool expected)
    {
        AssumeDebug();

        using var jitTls = TestConfiguration.CreateJitTls();
        var bytes = text is null ? null : Encoding.ASCII.GetBytes(text + '\0');

        fixed (byte* pointer = bytes)
        {
            ConfigMethodRange range = default;
            range.EnsureInit(pointer);
            Assert.Multiple(() => {
                Assert.That(range.IsInit, Is.True);
                Assert.That(range.Contains(hash), Is.EqualTo(expected));
                Assert.That(range.Error, Is.False);
            });
            range.EnsureInit(null);
            Assert.That(range.Contains(hash), Is.EqualTo(expected));
        }
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(2, true)]
    public static void CapacityTruncationRetainsNativeSelection(int capacity, bool containsSecond)
    {
        AssumeDebug();

        using var jitTls = TestConfiguration.CreateJitTls();

        fixed (byte* pointer = "1,2"u8)
        {
            ConfigMethodRange range = default;
            range.EnsureInit(pointer, capacity);
            Assert.That(range.Contains(2), Is.EqualTo(containsSecond));
            Assert.That(range.Error, Is.False);
        }
    }

    [TestCase(false, 0, 0)]
    [TestCase(false, 99, 123)]
    [TestCase(true, 7, 123)]
    [TestCase(true, 17, -1)]
    public static void AwaitSelectionHashesRootMethodInlineOrdinalAndOffset(bool inlining, int ordinal, int offset)
    {
        AssumeDebug();

        using var jitTls = TestConfiguration.CreateJitTls();
        var root = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        root.info = new Compiler.Info { compFullName = "AwaitRangeRoot" };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info = new Compiler.Info { compFullName = "AwaitRangeInlinee" };
        compiler.compInlineContext = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
        var ordinalField = typeof(InlineContext).GetField("_ordinal", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing inline ordinal.");
        ordinalField.SetValue(compiler.compInlineContext, ordinal);

        if (inlining)
        {
            compiler.opts.compFlags = Globals.CLFLG_INLINING;
            compiler.impInlineInfo = new InlineInfo { InlineRoot = root };
        }
        else
        {
            root = compiler;
        }

        var methodHash = unchecked((uint)root.info.compMethodHash());
        var expectedHash = unchecked((uint)(((methodHash * 33UL) ^ (uint)(inlining ? ordinal : 0)) * 33UL) ^ (uint)offset);
        var rangeField = typeof(Compiler.DebugData.MethodRangeData).GetField(
            "s_jitOptimizeAwaitRange",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing await range.");
        var oldRange = rangeField.GetValue(null);

        try
        {
            var bytes = Encoding.ASCII.GetBytes($"{expectedHash:x8}\0");

            fixed (byte* pointer = bytes)
            {
                ConfigMethodRange range = default;
                range.EnsureInit(pointer);
                rangeField.SetValue(null, range);
            }

            Assert.That(compiler.impCheckOptimizeAwait(offset), Is.True);
            Assert.That(compiler.impCheckOptimizeAwait(offset ^ 1), Is.False);
        }
        finally
        {
            rangeField.SetValue(null, oldRange);
        }
    }

    [Test]
    public static void ReleaseRangeApisFailExplicitly()
    {
        Assume.That(!TestConfiguration.IsDebug);

        var range = default(ConfigMethodRange);
        _ = Assert.Throws<InvalidOperationException>(() => _ = range.Error);
        _ = Assert.Throws<InvalidOperationException>(() => _ = range.IsEmpty);
        _ = Assert.Throws<InvalidOperationException>(() => _ = range.IsInit);
        _ = Assert.Throws<InvalidOperationException>(() => _ = range.Contains(0));
        _ = Assert.Throws<InvalidOperationException>(() => range.EnsureInit(null));

        var dump = typeof(ConfigMethodRange).GetMethod(nameof(ConfigMethodRange.Dump), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Missing ConfigMethodRange.Dump.");
        var exception = Assert.Throws<TargetInvocationException>(() => dump.Invoke(range, null));
        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public static void ReleaseRangeConfigurationApisFailExplicitly()
    {
        Assume.That(!TestConfiguration.IsDebug);

        var config = default(JitConfigValues);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitRange);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitR2RUnsupportedRange);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitEnableRboRange);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitOnlyOptimizeRange);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitStressOnly);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitInstrumentIfOptimizing);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitEnableLocalAddrPropagationRange);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitGuardedDevirtualizationRange);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitEnablePatchpointRange);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitOptimizeAwaitRange);
        _ = Assert.Throws<InvalidOperationException>(() => _ = config.JitAwaitHashBreak);
        _ = Assert.Throws<InvalidOperationException>(() => _ = JitTls.LogEnv);

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        Assert.That(compiler.impCheckOptimizeAwait(0), Is.True);
        _ = Assert.Throws<InvalidOperationException>(() => compiler.SkipMethod());
    }
}
