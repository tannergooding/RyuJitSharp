// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ProfileSpanningTreeTests
{
    [TestCase("ordinary", "B1,T1:3,T1:2,B2,T2:4,B4,NPseudo4:1,B3,NPostdominatesSource3:4,B5")]
    [TestCase("critical", "B1,T1:2,T1:3,B3,T3:4,B4,NPseudo4:1,B2,NPostdominatesSource2:4,B5")]
    [TestCase("rare", "B1,T1:2,T1:3,B3,T3:4,B4,NPseudo4:1,B2,NPostdominatesSource2:4,B5")]
    [TestCase("rare-source", "B1,T1:3,T1:2,B2,T2:4,B4,NPseudo4:1,B3,NPostdominatesSource3:4,B5")]
    public static void ForkCostOrderingAndUnreachableNotification(string shape, string expected)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_COND, BBJ_ALWAYS, BBJ_ALWAYS, BBJ_RETURN, BBJ_THROW);
            blocks[0].SetCond(new FlowEdge(blocks[0], blocks[2], null), new FlowEdge(blocks[0], blocks[1], null));
            SetTarget(blocks[1], blocks[3]);
            SetTarget(blocks[2], blocks[3]);
            blocks[3].bbRefs = 2;

            if (shape == "critical")
            {
                blocks[2].bbRefs = 2;
            }

            if (shape is "rare" or "rare-source")
            {
                blocks[1].bbWeight = 0;
            }

            if (shape == "rare-source")
            {
                blocks[0].bbWeight = 0;
            }

            AssertTrace(compiler, expected);
        });
    }

    [Test]
    public static void LargeGraphsUseLogicalBlockIdsAcrossBitVectorWords()
    {
        WithCompiler(compiler => {
            compiler.compBasicBlockID = 70;
            var kinds = new BBKinds[130];
            Array.Fill(kinds, BBJ_ALWAYS);
            kinds[^1] = BBJ_RETURN;
            var blocks = CreateBlocks(compiler, kinds);

            for (var i = 0; i < blocks.Length - 1; i++)
            {
                SetTarget(blocks[i], blocks[i + 1]);
            }

            var visitor = new RecordingVisitor();
            compiler.WalkSpanningTree(visitor);
            Assert.That(visitor.Events.Count, Is.EqualTo(2 * blocks.Length));

            for (var i = 0; i < blocks.Length; i++)
            {
                Assert.That(visitor.Events[2 * i], Is.EqualTo($"B{i + 1}"));
                Assert.That(visitor.Events[(2 * i) + 1],
                    Is.EqualTo(i + 1 < blocks.Length ? $"T{i + 1}:{i + 2}" : $"NPseudo{i + 1}:1"));
            }
        });
    }

    [Test]
    public static void QueuedNodesAlreadyCountAsMarked()
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_COND, BBJ_ALWAYS, BBJ_RETURN);
            blocks[0].SetCond(new FlowEdge(blocks[0], blocks[2], null), new FlowEdge(blocks[0], blocks[1], null));
            SetTarget(blocks[1], blocks[2]);
            AssertTrace(compiler, "B1,T1:3,T1:2,B2,NPostdominatesSource2:3,B3,NPseudo3:1");
        });
    }

    [Test]
    public static void SwitchSlotsPreserveReverseOrderAndDuplicateClassification()
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_SWITCH, BBJ_RETURN, BBJ_RETURN, BBJ_THROW);
            var duplicate = new FlowEdge(blocks[0], blocks[1], null);
            var single = new FlowEdge(blocks[0], blocks[2], null);
            blocks[0].SwitchTargets = new BBswtDesc([duplicate, single, duplicate], [0, 1, 2], hasDefault: true, dominantCase: 0);
            blocks[1].bbRefs = 2;
            AssertTrace(compiler, "B1,T1:3,T1:2,NCriticalEdge1:2,B2,NPseudo2:1,B3,NPseudo3:1,B4");
        });
    }

    [Test]
    public static void HandlerAndFilterRootsFollowNativeStackOrder()
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN, BBJ_EHFINALLYRET, BBJ_EHFILTERRET, BBJ_EHFAULTRET, BBJ_THROW);
            blocks[1].HndIndex = 0;
            blocks[2].HndIndex = 1;
            blocks[3].HndIndex = 1;
            compiler.compHndBBtab = [
                new() { ebdHandlerType = EH_HANDLER_FINALLY, ebdHndBeg = blocks[1], ebdHndLast = blocks[1] },
                new() { ebdHandlerType = EH_HANDLER_FILTER, ebdFilter = blocks[2], ebdHndBeg = blocks[3], ebdHndLast = blocks[3] },
            ];
            compiler.compHndBBtabCount = 2;
            AssertTrace(compiler, "B1,NPseudo1:1,B3,NPseudo3:4,B4,NPseudo4:4,B2,NPseudo2:2,B5");
        });
    }

    [TestCase(false, "B1,T1:2,B2,NCriticalEdge2:1,B4,NPseudo4:4,B3")]
    [TestCase(true, "B1,B4,NPseudo4:4,B2,B3")]
    public static void CallFinallyContinuationAndRetlessCalls(bool retless, string expected)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_CALLFINALLY, BBJ_CALLFINALLYRET, BBJ_RETURN, BBJ_EHFINALLYRET);
            SetTarget(blocks[0], blocks[3]);
            SetTarget(blocks[1], blocks[0]);
            blocks[3].HndIndex = 0;
            compiler.compHndBBtab = [
                new() { ebdHandlerType = EH_HANDLER_FINALLY, ebdHndBeg = blocks[3], ebdHndLast = blocks[3] },
            ];
            compiler.compHndBBtabCount = 1;

            if (retless)
            {
                blocks[0].SetFlags(BBF_RETLESS_CALL);
            }

            AssertTrace(compiler, expected);
        });
    }

    [TestCase(false, "B1,T1:3,B3,NPseudo3:1,B2,NPostdominatesSource2:3")]
    [TestCase(true, "B1,T1:3,B3,NPseudo3:1,B2,Badcode")]
    public static void TryAndCatchLeavesAndMalformedCatchReturn(bool malformed, string expected)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_LEAVE, malformed ? BBJ_EHCATCHRET : BBJ_LEAVE, BBJ_RETURN);
            SetTarget(blocks[0], blocks[2]);
            SetTarget(blocks[1], blocks[2]);
            blocks[0].TryIndex = 0;
            blocks[1].HndIndex = 0;
            compiler.compHndBBtab = [
                new() {
                    ebdHandlerType = EH_HANDLER_CATCH, ebdTryBeg = blocks[0], ebdTryLast = blocks[0],
                    ebdHndBeg = blocks[1], ebdHndLast = blocks[1],
                },
            ];
            compiler.compHndBBtabCount = 1;
            AssertTrace(compiler, expected);
        });
    }

    [TestCase(0, 0, "B1,NPseudo1:1,B2")]
    [TestCase(0, 1, "B1,NPseudo1:1,B2")]
    [TestCase(1, 0, "B1,NPseudo1:1,B2")]
    [TestCase(1, 1, "B1,B2")]
    public static void MinimalProfilingOmitsThrowsOnlyWhenTheMethodCanReturn(int minimal, int returns, string expected)
    {
        WithCompiler(compiler => {
            _ = CreateBlocks(compiler, BBJ_THROW, BBJ_RETURN);
            var config = JitConfig;
            MinimalProfiling(ref config) = minimal;
            JitConfig = config;
            compiler.fgReturnCount = returns;
            AssertTrace(compiler, expected);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitMinimalJitProfiling")]
    private static extern ref int MinimalProfiling(ref JitConfigValues config);

    private static BasicBlock[] CreateBlocks(Compiler compiler, params BBKinds[] kinds)
    {
        var blocks = new BasicBlock[kinds.Length];

        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = BasicBlock.New(compiler, kinds[i]);

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

    private static void SetTarget(BasicBlock source, BasicBlock target)
    {
        source.SetKindAndTargetEdge(source.Kind, new FlowEdge(source, target, null));
    }

    private static void AssertTrace(Compiler compiler, string expected)
    {
        var visitor = new RecordingVisitor();
        compiler.WalkSpanningTree(visitor);
        Assert.That(string.Join(',', visitor.Events), Is.EqualTo(expected));
    }

    private sealed class RecordingVisitor : SpanningTreeVisitor
    {
        public List<string> Events { get; } = [];

        public override void Badcode() => Events.Add("Badcode");

        public override void VisitBlock(BasicBlock block) => Events.Add($"B{block.bbNum}");

        public override void VisitTreeEdge(BasicBlock source, BasicBlock target) => Events.Add($"T{source.bbNum}:{target.bbNum}");

        public override void VisitNonTreeEdge(BasicBlock source, BasicBlock target, EdgeKind kind)
            => Events.Add($"N{kind}{source.bbNum}:{target.bbNum}");
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
        compiler.compHndBBtab = [];
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
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
