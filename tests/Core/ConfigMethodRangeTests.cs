// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ConfigMethodRangeTests
{
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
        using var jitTls = new JitTls(null);
        var bytes = text is null ? null : Encoding.ASCII.GetBytes(text + '\0');
        fixed (byte* pointer = bytes)
        {
            ConfigMethodRange range = default;
            range.EnsureInit(pointer);
            Assert.Multiple(() => {
                Assert.That(range.IsInit, Is.True);
                Assert.That(range.Contains(hash), Is.EqualTo(expected));
                Assert.That(range.Error, Is.False);
                Assert.That(range.BadCharIndex, Is.EqualTo(-1));
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
        using var jitTls = new JitTls(null);
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
        using var jitTls = new JitTls(null);
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
        var rangeField = typeof(Compiler).GetField("s_jitOptimizeAwaitRange", BindingFlags.Static | BindingFlags.NonPublic)
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
}
#endif
