// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
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

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void MissingProfilePreservesPhaseStatusAndScalesInlinees(bool dynamicPgo, bool inlinee)
    {
        WithCompiler(compiler => {
            var blocks = CreateLinearGraph(compiler);
            compiler.opts.SetMinOpts(false);
            compiler.fgPgoDynamic = dynamicPgo;

            if (inlinee)
            {
                compiler.impInlineInfo = new InlineInfo { iciBlock = new BasicBlock(null, null) { bbWeight = 25 } };
            }

            var status = IncorporateProfile(compiler);
            Assert.That(status, Is.EqualTo(inlinee ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgPgoHaveWeights, Is.EqualTo(dynamicPgo));
            Assert.That(compiler.fgPgoSynthesized, Is.EqualTo(dynamicPgo));
            Assert.That(blocks[0].bbWeight, Is.EqualTo(inlinee ? 25 : 100));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(inlinee ? 25 : 100));
        });
    }

    [Test]
    public static void MinOptsSkipsProfileIncorporation()
    {
        WithCompiler(compiler => {
            _ = CreateLinearGraph(compiler);
            compiler.opts.SetMinOpts(true);
            compiler.fgPgoDynamic = true;
            Assert.That(IncorporateProfile(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.fgPgoHaveWeights, Is.False);
            Assert.That(compiler.Metrics.ProfileSynthesizedBlendedOrRepaired, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EdgeCountsTakePrecedenceOverBlockCounts(bool includeEdges)
    {
        WithCompiler(compiler => {
            var blocks = CreateLinearGraph(compiler);
            compiler.opts.SetMinOpts(false);
            var data = stackalloc ulong[3] { 40, 40, 75 };
            var schema = stackalloc PgoInstrumentationSchema[3] {
                new() { ILOffset = 0, InstrumentationKind = PgoInstrumentationKind.BasicBlockLongCount, Offset = 0 },
                new() { ILOffset = 10, InstrumentationKind = PgoInstrumentationKind.BasicBlockIntCount, Offset = 8 },
                new() { ILOffset = 10, Other = 0, InstrumentationKind = PgoInstrumentationKind.EdgeLongCount, Offset = 16 },
            };
            compiler.fgPgoSchema = schema;
            compiler.fgPgoSchemaCount = includeEdges ? 3 : 2;
            compiler.fgPgoData = (byte*)data;
            compiler.fgPgoSource = PgoSource.Dynamic;
            Assert.That(IncorporateProfile(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.fgPgoBlockCounts, Is.EqualTo(2));
            Assert.That(compiler.fgPgoEdgeCounts, Is.EqualTo(includeEdges ? 1 : 0));
            Assert.That(compiler.fgNumProfileRuns, Is.EqualTo(1));
            Assert.That(compiler.fgPgoSource, Is.EqualTo(PgoSource.Dynamic));
            Assert.That(compiler.fgCalledCount, Is.EqualTo(includeEdges ? 75 : 40));
            Assert.That(blocks[1].bbWeight, Is.EqualTo(includeEdges ? 75 : 40));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DiscardedEdgeCountsRestartSynthesis(bool mismatched)
    {
        WithCompiler(compiler => {
            var blocks = CreateLinearGraph(compiler);
            compiler.opts.SetMinOpts(false);
            var data = mismatched ? 40ul : 0ul;
            PgoInstrumentationSchema schema = new() {
                ILOffset = mismatched ? 99 : 10,
                Other = 0,
                InstrumentationKind = PgoInstrumentationKind.EdgeLongCount,
            };
            compiler.fgPgoSchema = &schema;
            compiler.fgPgoSchemaCount = 1;
            compiler.fgPgoData = (byte*)&data;
            compiler.fgPgoSource = PgoSource.Dynamic;
            _ = IncorporateProfile(compiler);
            Assert.That(compiler.fgPgoSource, Is.EqualTo(PgoSource.Synthesis));
            Assert.That(compiler.fgPgoHaveWeights, Is.True);
            Assert.That(compiler.fgPgoFailReason, Is.Not.Null);
            Assert.That(blocks[1].bbWeight, Is.EqualTo(100));
            Assert.That(blocks[0].TargetEdge.Likelihood, Is.EqualTo(1));
        });
    }

    [TestCase(0, 0, 1)]
    [TestCase(3, 4, 7)]
    [TestCase(int.MaxValue, 1, int.MinValue)]
    [TestCase(-1, 1, 1)]
    public static void SchemaSummaryRecognizesPairsAndWrapsRunCounts(int firstRuns, int secondRuns, int expectedRuns)
    {
        WithCompiler(compiler => {
            _ = CreateLinearGraph(compiler);
            compiler.opts.SetMinOpts(false);
            var schema = stackalloc PgoInstrumentationSchema[10] {
                new() { InstrumentationKind = PgoInstrumentationKind.NumRuns, Other = firstRuns },
                new() { InstrumentationKind = PgoInstrumentationKind.NumRuns, Other = secondRuns },
                new() { InstrumentationKind = PgoInstrumentationKind.GetLikelyClass },
                new() { InstrumentationKind = PgoInstrumentationKind.GetLikelyMethod },
                new() { InstrumentationKind = PgoInstrumentationKind.HandleHistogramIntCount },
                new() { InstrumentationKind = PgoInstrumentationKind.HandleHistogramTypes },
                new() { InstrumentationKind = PgoInstrumentationKind.HandleHistogramLongCount },
                new() { InstrumentationKind = PgoInstrumentationKind.HandleHistogramMethods },
                new() { InstrumentationKind = (PgoInstrumentationKind)(-1) },
                new() { InstrumentationKind = PgoInstrumentationKind.HandleHistogramIntCount },
            };
            compiler.fgPgoSchema = schema;
            compiler.fgPgoSchemaCount = 10;
#if DEBUG
            var output = Capture(compiler, () => _ = IncorporateProfile(compiler));
            Assert.That(output, Does.Contain("Unknown PGO record type 0xffffffff in schema entry 8"));
            Assert.That(output, Does.Contain($"Profile summary: {expectedRuns} runs, 0 block probes, 0 edge probes, 2 class profiles, 2 method profiles, 2 other records"));
#else
            _ = IncorporateProfile(compiler);
#endif
            Assert.That(compiler.fgNumProfileRuns, Is.EqualTo(expectedRuns));
            Assert.That(compiler.fgPgoClassProfiles, Is.EqualTo(2));
            Assert.That(compiler.fgPgoMethodProfiles, Is.EqualTo(2));
            Assert.That(compiler.fgPgoHaveWeights, Is.False);
        });
    }

    private static BasicBlock[] CreateLinearGraph(Compiler compiler)
    {
        var first = BasicBlock.New(compiler, BBJ_ALWAYS);
        var second = BasicBlock.New(compiler, BBJ_RETURN);
        first.Next = second;
        second.Prev = first;
        first.bbRefs = 1;
        second.bbRefs = 1;
        first.bbCodeOffs = 0;
        second.bbCodeOffs = 10;
        var edge = new FlowEdge(first, second, null) { Likelihood = 1 };
        edge.incrementDupCount();
        second.bbPreds = edge;
        first.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
        compiler.fgFirstBB = first;
        compiler.fgLastBB = second;
        compiler.fgPredsComputed = true;
        compiler.info.compILCodeSize = 20;

        return [first, second];
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgIncorporateProfileData")]
    private static extern PhaseStatus IncorporateProfile(Compiler compiler);

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void MissingProfileDiagnosticMatchesNative(bool bbopt)
    {
        WithCompiler(compiler => {
            _ = CreateLinearGraph(compiler);
            compiler.opts.SetMinOpts(false);
            compiler.fgPgoQueryResult = unchecked((int)0x80004001);

            if (bbopt)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_BBOPT);
            }

            var output = Capture(compiler, () => _ = IncorporateProfile(compiler));
            var expected = bbopt ? "BBOPT set, but no profile data available (hr=80004001)" : "BBOPT not set";
            Assert.That(output, Is.EqualTo(expected + Environment.NewLine));
        });
    }

    [TestCase(1, false, false, 1)]
    [TestCase(1, true, false, 1)]
    [TestCase(2, false, false, 1)]
    [TestCase(2, true, false, 0)]
    [TestCase(3, true, false, 1)]
    [TestCase(3, false, false, 0)]
    [TestCase(0, true, true, 1)]
    public static void DebugSynthesisOptionsRespectProfilePresence(int mode, bool hasSchema, bool propagate, int expectedRuns)
    {
        WithCompiler(compiler => {
            _ = CreateLinearGraph(compiler);
            compiler.opts.SetMinOpts(false);
            SynthesisMode(ref JitConfig) = mode;
            PgoInstrumentationSchema schema = new() { InstrumentationKind = PgoInstrumentationKind.GetLikelyClass };

            if (hasSchema)
            {
                compiler.fgPgoSchema = &schema;
                compiler.fgPgoSchemaCount = 1;
            }

            if (propagate)
            {
                PropagateSynthesis(ref JitConfig) = 1;
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_BBINSTR);
            }

            _ = IncorporateProfile(compiler);
            Assert.That(compiler.Metrics.ProfileSynthesizedBlendedOrRepaired, Is.EqualTo(expectedRuns));
        });
    }

    [Test]
    public static void StressPrecedesTheOptimizationGate()
    {
        WithCompiler(compiler => {
            _ = CreateLinearGraph(compiler);
            compiler.opts.SetMinOpts(true);
            StressSeed(ref JitConfig) = 7;
            Assert.That(IncorporateProfile(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Metrics.ProfileSynthesizedBlendedOrRepaired, Is.EqualTo(1));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitSynthesizeCounts")]
    private static extern ref int SynthesisMode(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitPropagateSynthesizedCountsToProfileData")]
    private static extern ref int PropagateSynthesis(ref JitConfigValues config);

    private static string Capture(Compiler compiler, Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        var verbose = compiler.verbose;

        try
        {
            s_jitstdout = writer;
            compiler.verbose = true;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
            compiler.verbose = verbose;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

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
        compiler.compHndBBtab = [];
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
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
