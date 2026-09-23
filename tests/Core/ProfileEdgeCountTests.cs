// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ICorJitInfo;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ProfileEdgeCountTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void CountersPreserveUnsignedWidthsAndInternalBlockKeys(bool wide, bool internalReturn)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(blocks[0], blocks[1]));
            if (internalReturn)
            {
                blocks[1].SetFlags(BBF_INTERNAL);
            }
            var key = internalReturn ? int.MinValue | blocks[1].bbNum : 10;
            Assert.That(EfficientEdgeCountBlockToKey(blocks[1]), Is.EqualTo(key));
            var count = wide ? ulong.MaxValue : uint.MaxValue;
            WithProfile(compiler, [new(key, 0, count, wide)], () => {
                Assert.That(compiler.fgIncorporateEdgeCounts(), Is.True);
                Assert.That(compiler.fgPgoHaveWeights, Is.True);
                foreach (var block in blocks)
                {
                    Assert.That(block.bbWeight, Is.EqualTo((double)count));
                    Assert.That(block.hasProfileWeight, Is.True);
                }
                Assert.That(blocks[0].TargetEdge.Likelihood, Is.EqualTo(1));
            });
        });
    }

    [TestCase(100ul, 30ul, false)]
    [TestCase(100ul, 0ul, true)]
    [TestCase(20ul, 30ul, false)]
    public static void DiamondReconstructsMissingEdgesAndRepairsNegativeCounts(ulong entryCount, ulong sideCount, bool omitSide)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_COND, BBJ_ALWAYS, BBJ_ALWAYS, BBJ_RETURN);
            var left = Connect(blocks[0], blocks[1], 0.5);
            var right = Connect(blocks[0], blocks[2], 0.5);
            blocks[0].SetCond(right, left);
            blocks[1].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(blocks[1], blocks[3]));
            blocks[2].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(blocks[2], blocks[3]));
            Probe[] probes = omitSide ? [new(30, 0, entryCount)] : [new(30, 0, entryCount), new(20, 30, sideCount)];
            WithProfile(compiler, probes, () => {
                var inconsistent = sideCount > entryCount;
                var leftWeight = inconsistent ? entryCount * 0.001 : entryCount - sideCount;
                Assert.That(compiler.fgIncorporateEdgeCounts(), Is.EqualTo(!inconsistent));
                Assert.That(compiler.fgPgoHaveWeights, Is.True);
                Assert.That(blocks[0].bbWeight, Is.EqualTo((double)entryCount));
                Assert.That(blocks[1].bbWeight, Is.EqualTo(leftWeight));
                Assert.That(blocks[2].bbWeight, Is.EqualTo((double)sideCount));
                Assert.That(blocks[3].bbWeight, Is.EqualTo((double)entryCount));
                Assert.That(left.Likelihood, Is.EqualTo(leftWeight / (leftWeight + sideCount)));
                Assert.That(right.Likelihood, Is.EqualTo(sideCount / (leftWeight + sideCount)));
                Assert.That(blocks[2].isRunRarely, Is.EqualTo(sideCount == 0));
            });
        });
    }

    [TestCase(29ul, 0ul, 0ul, true, false, -1)]
    [TestCase(30ul, 0ul, 0ul, true, false, 0)]
    [TestCase(55ul, 25ul, 20ul, true, false, 0)]
    [TestCase(54ul, 26ul, 20ul, true, false, -1)]
    [TestCase(10ul, 10ul, 80ul, true, false, -1)]
    [TestCase(10ul, 10ul, 80ul, false, false, 2)]
    [TestCase(80ul, 10ul, 10ul, true, true, -1)]
    public static void DominantSwitchCasesRespectSamplesFractionDefaultAndDuplicates(
        ulong firstCount, ulong secondCount, ulong thirdCount, bool hasDefault, bool duplicate, int expectedCase)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_SWITCH, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            FlowEdge[] successors = [
                Connect(blocks[0], blocks[1], 1.0 / 3),
                Connect(blocks[0], blocks[2], 1.0 / 3),
                Connect(blocks[0], blocks[3], 1.0 / 3),
            ];
            var cases = duplicate ? [successors[0], successors[0], successors[1], successors[2]] : successors;
            if (duplicate)
            {
                blocks[1].bbRefs++;
            }
            var descriptor = new BBswtDesc(successors, new int[cases.Length], hasDefault);
            cases.CopyTo(descriptor.Cases);
            blocks[0].SwitchTargets = descriptor;
            WithProfile(compiler, [new(10, 0, firstCount), new(20, 0, secondCount), new(30, 0, thirdCount)], () => {
                Assert.That(compiler.fgIncorporateEdgeCounts(), Is.True);
                Assert.That(blocks[0].bbWeight, Is.EqualTo((double)(firstCount + secondCount + thirdCount)));
                Assert.That(descriptor.HasDominantCase, Is.EqualTo(expectedCase >= 0));
                Assert.That(descriptor.HasDefaultCase, Is.EqualTo(hasDefault));
                if (expectedCase >= 0)
                {
                    Assert.That(descriptor.DominantCase, Is.EqualTo(expectedCase));
                }
            });
        });
    }

    [TestCase("source", "PGO data available, but IL did not match")]
    [TestCase("target", "PGO data available, but IL did not match")]
    [TestCase("tree", "PGO data available, but IL did not match")]
    [TestCase("zero", "PGO data available, profile data was all zero")]
    [TestCase("badcode", "PGO data available, but IL was malformed")]
    public static void UnusableProfilesAreDiscardedWithoutSettingWeights(string failure, string expectedReason)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(blocks[0], blocks[1]));
            Probe[] probes = failure switch {
                "source" => [new(99, 0, 1)],
                "target" => [new(10, 99, 1)],
                "tree" => [new(0, 10, 1)],
                "zero" => [new(10, 0, 0)],
                _ => [new(10, 0, 1)],
            };
            WithProfile(compiler, probes, () => {
                var reconstructor = new EfficientEdgeCountReconstructor(compiler);
                reconstructor.Prepare();
                compiler.WalkSpanningTree(reconstructor);
                if (failure == "badcode")
                {
                    reconstructor.Badcode();
                }
                reconstructor.Solve();
                reconstructor.Propagate();
                Assert.That(compiler.fgPgoHaveWeights, Is.False);
                Assert.That(compiler.fgPgoFailReason, Is.EqualTo(expectedReason));
                // IsGood requests repair only for negative counts or a zero entry, not discarded data.
                Assert.That(reconstructor.IsGood, Is.True);
                foreach (var block in blocks)
                {
                    Assert.That(block.hasProfileWeight, Is.False);
                    Assert.That(block.bbWeight, Is.EqualTo(BB_UNITY_WEIGHT));
                }
            });
        });
    }

    [Test]
    public static void ZeroWeightForkGetsEqualSuccessorLikelihoods()
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_COND, BBJ_RETURN, BBJ_COND, BBJ_RETURN, BBJ_RETURN);
            blocks[0].SetCond(Connect(blocks[0], blocks[1], 0.5), Connect(blocks[0], blocks[2], 0.5));
            var first = Connect(blocks[2], blocks[3], 0.1);
            var second = Connect(blocks[2], blocks[4], 0.9);
            blocks[2].SetCond(first, second);
            WithProfile(compiler, [new(10, 0, 100)], () => {
                Assert.That(compiler.fgIncorporateEdgeCounts(), Is.True);
                Assert.That(blocks[2].bbWeight, Is.Zero);
                Assert.That(first.Likelihood, Is.EqualTo(0.5));
                Assert.That(second.Likelihood, Is.EqualTo(0.5));
            });
        });
    }

    [TestCase(false, true, 10ul)]
    [TestCase(true, true, 10ul)]
    [TestCase(false, false, 0ul)]
    public static void LoopCountsAndOsrLikelihoodsPreservePseudoFlow(bool entryIsSelfLoop, bool osr, ulong returnCount)
    {
        WithCompiler(compiler => {
            if (osr)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            }
            var blocks = entryIsSelfLoop
                ? CreateBlocks(compiler, BBJ_COND, BBJ_RETURN)
                : CreateBlocks(compiler, BBJ_ALWAYS, BBJ_COND, BBJ_ALWAYS, BBJ_RETURN);
            var header = entryIsSelfLoop ? blocks[0] : blocks[1];
            var body = entryIsSelfLoop ? header : blocks[2];
            var exit = blocks[^1];
            var loop = Connect(header, body, 0.5);
            var leave = Connect(header, exit, 0.5);
            header.SetCond(loop, leave);
            compiler.fgOSREntryBB = header;
            if (!entryIsSelfLoop)
            {
                blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(blocks[0], header));
                body.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(body, header));
            }
            WithProfile(compiler, [new(body.bbCodeOffs, header.bbCodeOffs, 90), new(exit.bbCodeOffs, 0, returnCount)], () => {
                var pseudoWeight = osr && !entryIsSelfLoop ? 1 : 0;
                Assert.That(compiler.fgIncorporateEdgeCounts(), Is.EqualTo(osr || returnCount > 0));
                Assert.That(compiler.fgPgoHaveWeights, Is.True);
                Assert.That(header.bbWeight, Is.EqualTo(90.0 + returnCount + pseudoWeight));
                Assert.That(exit.bbWeight, Is.EqualTo((double)returnCount));
                Assert.That(loop.Likelihood, Is.EqualTo(90.0 / (90.0 + returnCount)));
                Assert.That(leave.Likelihood, Is.EqualTo(returnCount / (90.0 + returnCount)));
                if (!entryIsSelfLoop)
                {
                    Assert.That(blocks[0].bbWeight, Is.EqualTo((double)returnCount + pseudoWeight));
                    Assert.That(body.bbWeight, Is.EqualTo(90));
                }
            });
        });
    }

#if DEBUG
    [TestCase(1)]
    [TestCase(7)]
    [TestCase(int.MinValue)]
    public static void RandomCountsReuseTheInlineStrategyAndKeepTheLastReturnNonzero(int seed)
    {
        WithCompiler(compiler => {
            var config = JitConfig;
            RandomEdgeCounts(ref config) = 17;
            JitConfig = config;
            var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
            var random = new CLRRandom(seed);
            StrategyRandom(strategy) = random;
            compiler._inlineStrategy = strategy;
            var expectedRandom = new CLRRandom(seed);
            var blocks = CreateBlocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(blocks[0], blocks[1]));
            WithProfile(compiler, [new(10, 0, 0)], () => {
                var value = expectedRandom.NextDouble();
                var expected = value <= 0.85 ? expectedRandom.Next(1, 101)
                    : value <= 0.96 ? expectedRandom.Next(101, 10001)
                    : value <= 0.995 ? expectedRandom.Next(10001, 100001)
                    : expectedRandom.Next(100001, 1000001);
                Assert.That(compiler.fgIncorporateEdgeCounts(), Is.True);
                Assert.That(blocks[0].bbWeight, Is.EqualTo(expected));
                Assert.That(blocks[1].bbWeight, Is.EqualTo(expected));
                Assert.That(random.Next(), Is.EqualTo(expectedRandom.Next()));
            });
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRandomEdgeCounts")]
    private static extern ref int RandomEdgeCounts(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_random")]
    private static extern ref CLRRandom? StrategyRandom(InlineStrategy strategy);
#endif

    private readonly record struct Probe(int Source, int Target, ulong Count, bool Wide = false);

    private static void WithProfile(Compiler compiler, Probe[] probes, Action action)
    {
        var counts = new ulong[probes.Length];
        var schema = new PgoInstrumentationSchema[probes.Length + 1];
        schema[0] = new() { ILOffset = 999, Other = 999, InstrumentationKind = PgoInstrumentationKind.NumRuns };
        for (var i = 0; i < probes.Length; i++)
        {
            var probe = probes[i];
            counts[i] = probe.Count;
            schema[i + 1] = new() {
                ILOffset = probe.Source, Other = probe.Target, Offset = i * sizeof(ulong),
                InstrumentationKind = probe.Wide ? PgoInstrumentationKind.EdgeLongCount : PgoInstrumentationKind.EdgeIntCount,
            };
        }
        fixed (ulong* data = counts)
        fixed (PgoInstrumentationSchema* entries = schema)
        {
            compiler.fgPgoData = (byte*)data;
            compiler.fgPgoSchema = entries;
            compiler.fgPgoSchemaCount = schema.Length;
            compiler.fgPgoHaveWeights = true;
            action();
        }
    }

    private static BasicBlock[] CreateBlocks(Compiler compiler, params BBKinds[] kinds)
    {
        var blocks = new BasicBlock[kinds.Length];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = BasicBlock.New(compiler, kinds[i]);
            blocks[i].bbCodeOffs = i * 10;
            blocks[i].bbRefs = i == 0 ? 1 : 0;
            if (i > 0)
            {
                blocks[i - 1].Next = blocks[i];
                blocks[i].Prev = blocks[i - 1];
            }
        }
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        return blocks;
    }

    private static FlowEdge Connect(BasicBlock source, BasicBlock target, double likelihood = 1)
    {
        var edge = new FlowEdge(source, target, target.bbPreds) { Likelihood = likelihood };
        target.bbPreds = edge;
        target.bbRefs++;
        return edge;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(ProfileEdgeCountTests);
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
