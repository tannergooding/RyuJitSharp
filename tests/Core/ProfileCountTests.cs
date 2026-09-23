// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ICorJitInfo;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ProfileCountTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void CountsUseUnsignedWidthsAndFirstMatchingProbe(bool haveWeights)
    {
        WithCompiler(compiler => {
            var data = stackalloc ulong[2] { uint.MaxValue, ulong.MaxValue };
            var schema = stackalloc PgoInstrumentationSchema[5] {
                new() { ILOffset = 0, InstrumentationKind = PgoInstrumentationKind.NumRuns },
                new() { ILOffset = 0, InstrumentationKind = PgoInstrumentationKind.BasicBlockIntCount },
                new() { ILOffset = 0, InstrumentationKind = PgoInstrumentationKind.BasicBlockLongCount, Offset = 8 },
                new() { ILOffset = 10, InstrumentationKind = PgoInstrumentationKind.BasicBlockLongCount, Offset = 8 },
                new() { ILOffset = 20, InstrumentationKind = PgoInstrumentationKind.GetLikelyClass },
            };
            compiler.fgPgoData = (byte*)data;
            compiler.fgPgoSchema = schema;
            compiler.fgPgoSchemaCount = 5;
            compiler.fgPgoHaveWeights = haveWeights;
            double weight = 123;
            Assert.That(compiler.fgGetProfileWeightForBasicBlock(0, ref weight), Is.EqualTo(haveWeights));
            Assert.That(weight, Is.EqualTo(haveWeights ? uint.MaxValue : 123.0));
            Assert.That(compiler.fgGetProfileWeightForBasicBlock(10, ref weight), Is.EqualTo(haveWeights));
            Assert.That(weight, Is.EqualTo(haveWeights ? (double)ulong.MaxValue : 123.0));
            Assert.That(compiler.fgGetProfileWeightForBasicBlock(20, ref weight), Is.EqualTo(haveWeights));
            Assert.That(weight, Is.EqualTo(haveWeights ? 0.0 : 123.0));
            Assert.That(compiler.fgGetProfileWeightForBasicBlock(30, ref weight), Is.EqualTo(haveWeights));
            Assert.That(weight, Is.EqualTo(haveWeights ? 0.0 : 123.0));

            var first = new BasicBlock(null, null) { bbCodeOffs = 0, bbWeight = 5 };
            var second = new BasicBlock(null, null) { Prev = first, bbCodeOffs = 10, bbWeight = 6 };
            var third = new BasicBlock(null, null) { Prev = second, bbCodeOffs = 20, bbWeight = 7 };
            first.Next = second;
            second.Next = third;
            compiler.fgFirstBB = first;
            compiler.fgLastBB = third;
            Assert.That(compiler.fgIncorporateBlockCounts(), Is.True);
            Assert.That(first.bbWeight, Is.EqualTo(haveWeights ? uint.MaxValue : 5.0));
            Assert.That(second.bbWeight, Is.EqualTo(haveWeights ? (double)ulong.MaxValue : 6.0));
            Assert.That(third.bbWeight, Is.EqualTo(haveWeights ? 0.0 : 7.0));
            foreach (var block in compiler.Blocks)
            {
                Assert.That(block.hasProfileWeight, Is.EqualTo(haveWeights));
            }
            Assert.That(third.isRunRarely, Is.EqualTo(haveWeights));
        });
    }

    [Test]
    public static void RootProfileScaleDoesNotRequireAnInlineeGraph()
    {
        WithCompiler(compiler => {
            compiler.fgApplyProfileScale();
            Assert.That(compiler.Metrics.ProfileInconsistentInlineeScale, Is.Zero);
        });
    }

    [TestCase(true, 100.0, 20.0, 10.0, 0.125, false)]
    [TestCase(false, 100.0, 20.0, 10.0, 0.125, false)]
    [TestCase(true, 100.0, 20.0, 160.0, 2.0, false)]
    [TestCase(true, 100.0, 20.0, 0.0, 0.0, false)]
    [TestCase(true, 20.0, 20.0, 10.0, 10.0, true)]
    [TestCase(false, 20.0, 20.0, 10.0, 0.1, true)]
    [TestCase(true, 10.0, 20.0, 10.0, 10.0, true)]
    [TestCase(true, 0.0, 0.0, 10.0, 10.0, true)]
    public static void InlineScalingExcludesBackedgesAndPreservesProfileFlags(
        bool haveWeights, double entryWeight, double backedgeWeight, double callWeight, double scale, bool inconsistent)
    {
        WithCompiler(compiler => {
            var callSite = new BasicBlock(null, null) { bbWeight = callWeight };
            var entry = new BasicBlock(null, null) { bbWeight = entryWeight };
            var loop = new BasicBlock(null, null) { Prev = entry, bbWeight = backedgeWeight * 2 };
            var exit = new BasicBlock(null, null) { Prev = loop, bbWeight = 5 };
            entry.Next = loop;
            loop.Next = exit;
            entry.bbPreds = new FlowEdge(loop, entry, null) { Likelihood = 0.5 };
            if (haveWeights)
            {
                entry.SetFlags(BBF_PROF_WEIGHT);
                callSite.SetFlags(BBF_PROF_WEIGHT);
            }
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = exit;
            compiler.impInlineInfo = new InlineInfo { iciBlock = callSite };
            compiler.opts.compFlags = CLFLG_INLINING;
            compiler.fgPgoHaveWeights = haveWeights;
            compiler.fgPgoConsistent = true;
            compiler.fgApplyProfileScale();
            Assert.That(entry.bbWeight, Is.EqualTo(entryWeight * scale));
            Assert.That(loop.bbWeight, Is.EqualTo(backedgeWeight * 2 * scale));
            Assert.That(exit.bbWeight, Is.EqualTo(5 * scale));
            Assert.That(callSite.bbWeight, Is.EqualTo(callWeight));
            Assert.That(entry.hasProfileWeight, Is.EqualTo(haveWeights));
            Assert.That(loop.hasProfileWeight, Is.False);
            Assert.That(exit.hasProfileWeight, Is.False);
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!inconsistent));
            Assert.That(compiler.Metrics.ProfileInconsistentInlineeScale, Is.EqualTo(inconsistent ? 1 : 0));
            if (inconsistent)
            {
                compiler.fgApplyProfileScale();
                Assert.That(compiler.Metrics.ProfileInconsistentInlineeScale, Is.EqualTo(1));
            }
        });
    }

#if DEBUG
    [TestCase(1)]
    [TestCase(7)]
    [TestCase(-1)]
    public static void StressCountsPreserveTheUnsignedSeed(int seed)
    {
        WithCompiler(compiler => {
            var config = JitConfig;
            StressSeed(ref config) = seed;
            JitConfig = config;
            var methodHash = unchecked((uint)compiler.info.compMethodHash());
            for (var offset = -1; offset < 128; offset++)
            {
                var hash = unchecked((uint)(((ulong)methodHash * (uint)seed) ^ ((ulong)(uint)offset * 1027)));
                var expected = (hash % 3) == 0 ? 0.0
                    : (hash % 11) == 0 ? (double)(hash % 23) * (hash % 29) * (hash % 31)
                    : (double)(hash % 17) * (hash % 19);
                if (offset == 0 && expected == 0)
                {
                    expected = 1 + (hash % 5);
                }
                double weight = -1;
                Assert.That(compiler.fgGetProfileWeightForBasicBlock(offset, ref weight), Is.True);
                Assert.That(weight, Is.EqualTo(expected), $"Offset: {offset}");
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitStressBBProf")]
    private static extern ref int StressSeed(ref JitConfigValues config);
#endif

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info = new Compiler.Info();
#if DEBUG
        compiler.info.compFullName = "ProfileCountTests";
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
