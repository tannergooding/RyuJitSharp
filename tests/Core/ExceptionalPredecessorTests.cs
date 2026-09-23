// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.Globals;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ExceptionalPredecessorTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void NonHandlerPredecessorsAreReturnedWithoutCaching(bool hasPred)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN, BBJ_RETURN);
            var edge = hasPred ? new FlowEdge(blocks[0], blocks[1], null) : null;
            blocks[1].bbPreds = edge;
            Assert.That(compiler.bbIsHandlerBeg(blocks[1]), Is.False);
            Assert.That(compiler.BlockPredsWithEH(blocks[1]), Is.SameAs(edge));
            Assert.That(compiler._blockToEHPreds, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HandlerAndFilterCachePrependingOrderAcrossNoncontiguousTries(bool filter)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            blocks[0].TryIndex = 0;
            blocks[2].TryIndex = 0;
            blocks[3].HndIndex = 0;
            blocks[4].HndIndex = 0;
            blocks[5].HndIndex = 0;
            compiler.compHndBBtab = [
                Clause(filter ? EH_HANDLER_FILTER : EH_HANDLER_CATCH, blocks[0], blocks[4], blocks[5]),
            ];
            compiler.compHndBBtab[0].ebdTryLast = blocks[2];
            compiler.compHndBBtab[0].ebdFilter = blocks[3];
            compiler.compHndBBtabCount = 1;
            var target = filter ? blocks[3] : blocks[4];
            var regular = new FlowEdge(blocks[1], target, null);
            target.bbPreds = regular;
            Assert.That(compiler.bbIsHandlerBeg(blocks[3]), Is.EqualTo(filter));
            Assert.That(compiler.bbIsHandlerBeg(blocks[4]), Is.True);
            Assert.That(compiler.bbIsHandlerBeg(blocks[5]), Is.False);
            var head = compiler.BlockPredsWithEH(target);
            Assert.That(Sources(head), Is.EqualTo([blocks[2], blocks[0], blocks[1]]));
            Assert.That(head?.NextPredEdge?.NextPredEdge, Is.SameAs(regular));
            Assert.That(target.bbPreds, Is.SameAs(regular));
            Assert.That(compiler.BlockPredsWithEH(target), Is.SameAs(head));

            if (filter)
            {
                Assert.That(Sources(compiler.BlockPredsWithEH(blocks[4])), Is.EqualTo([blocks[2], blocks[0]]));
            }
        });
    }

    [Test]
    public static void CallfinallyTailIsNotAnExceptionalPredecessor()
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_CALLFINALLY, BBJ_CALLFINALLYRET, BBJ_EHFINALLYRET);
            blocks[0].TryIndex = 0;
            blocks[1].TryIndex = 0;
            blocks[2].HndIndex = 0;
            var regular = new FlowEdge(blocks[0], blocks[2], null);
            blocks[0].SetKindAndTargetEdge(BBJ_CALLFINALLY, regular);
            blocks[2].bbPreds = regular;
            compiler.compHndBBtab = [Clause(EH_HANDLER_FINALLY, blocks[0], blocks[2], blocks[2])];
            compiler.compHndBBtab[0].ebdTryLast = blocks[1];
            compiler.compHndBBtabCount = 1;
            Assert.That(blocks[1].isBBCallFinallyPairTail, Is.True);
            Assert.That(Sources(compiler.BlockPredsWithEH(blocks[2])), Is.EqualTo([blocks[0], blocks[0]]));
        });
    }

    [Test]
    public static void NullHandlerListRemainsCachedUntilInvalidation()
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN, BBJ_RETURN);
            blocks[1].HndIndex = 0;
            compiler.compHndBBtab = [Clause(EH_HANDLER_CATCH, blocks[0], blocks[1], blocks[1])];
            compiler.compHndBBtabCount = 1;
            Assert.That(compiler.BlockPredsWithEH(blocks[1]), Is.Null);
            Assert.That(compiler.GetBlockToEHPreds().ContainsKey(blocks[1]), Is.True);
            blocks[0].TryIndex = 0;
            Assert.That(compiler.BlockPredsWithEH(blocks[1]), Is.Null);
            compiler._blockToEHPreds = null;
            Assert.That(Sources(compiler.BlockPredsWithEH(blocks[1])), Is.EqualTo([blocks[0]]));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void TwoPassFilterFlowPreservesRegionOrderAndAbort(bool abort, bool inHandler)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN, BBJ_RETURN);
            blocks[0].TryIndex = 2;
            blocks[1].HndIndex = 0;
            blocks[2].HndIndex = 1;
            blocks[3].HndIndex = 2;
            blocks[4].HndIndex = blocks[5].HndIndex = blocks[6].HndIndex = 3;
            compiler.compHndBBtab = [
                Clause(EH_HANDLER_CATCH, blocks[0], blocks[1], blocks[1]),
                Clause(EH_HANDLER_FINALLY, blocks[0], blocks[2], blocks[2]),
                Clause(EH_HANDLER_FAULT, blocks[0], blocks[3], blocks[3]),
                Clause(EH_HANDLER_FILTER, blocks[0], blocks[6], blocks[6]),
            ];
            compiler.compHndBBtabCount = 4;
            compiler.compHndBBtab[1].ebdEnclosingTryIndex = inHandler ? EHblkDsc.NO_ENCLOSING_INDEX : (ushort)3;
            compiler.compHndBBtab[1].ebdEnclosingHndIndex = inHandler ? (ushort)3 : EHblkDsc.NO_ENCLOSING_INDEX;
            compiler.compHndBBtab[2].ebdEnclosingTryIndex = 3;
            compiler.compHndBBtab[3].ebdFilter = blocks[4];
            var visited = new List<BasicBlock>();
            var result = blocks[4].VisitEHEnclosedHandlerSecondPassSuccs(compiler, block => {
                visited.Add(block);
                return abort ? BasicBlockVisit.Abort : BasicBlockVisit.Continue;
            });
            Assert.That(result, Is.EqualTo(abort ? BasicBlockVisit.Abort : BasicBlockVisit.Continue));
            Assert.That(visited, Is.EqualTo(abort || inHandler ? new[] { blocks[3] } : [blocks[3], blocks[2]]));
            Assert.That(GetAllSuccessors(compiler, blocks[4], false),
                Is.EqualTo(inHandler ? new[] { blocks[3] } : [blocks[3], blocks[2]]));
            Assert.That(Sources(compiler.BlockPredsWithEH(blocks[3])), Is.EqualTo([blocks[5], blocks[4], blocks[0]]));
            Assert.That(blocks[6].VisitEHEnclosedHandlerSecondPassSuccs(compiler, _ => throw new InvalidOperationException()),
                Is.EqualTo(BasicBlockVisit.Continue));
        });
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(65535)]
    [TestCase(65536)]
    [TestCase(-1)]
    public static void StressHashPreservesNativeSeedInterpretation(int seed)
    {
        WithCompiler(compiler => {
            var config = JitConfig;
            SsaStress(ref config) = seed;
            JitConfig = config;
            var expected = seed == 1 ? unchecked((uint)compiler.info.compMethodHash())
                : seed is >= 0 and <= 65535 ? (uint)((ulong)(uint)seed * 65537)
                : unchecked((uint)seed);
            Assert.That(SsaStressHashHelper(), Is.EqualTo(expected));
        });
    }

    [TestCase(1u, 0)]
    [TestCase(1u, 1)]
    [TestCase(1u, 8)]
    [TestCase(65535u, 8)]
    [TestCase(0x80000000u, 8)]
    public static void StressShuffleMatchesPositionalSwapsWithoutLosingEdges(uint hash, int count)
    {
        WithCompiler(compiler => {
            var blocks = CreateBlocks(compiler, BBJ_RETURN);
            var edges = new List<FlowEdge>();
            FlowEdge? head = null;

            for (var i = count - 1; i >= 0; i--)
            {
                var source = new BasicBlock(null, null) { bbNum = unchecked((i * 1879) ^ (i << 29)) };
                head = new FlowEdge(source, blocks[0], head);
                edges.Insert(0, head);
            }

            for (var i = 1; i < edges.Count; i++)
            {
                var number = unchecked((uint)edges[i].SourceBlock.bbNum);
                var value = hash ^ ((number * 65536ul) & uint.MaxValue) ^ number;

                if (((value % 1879) & 1) != 0)
                {
                    (edges[0], edges[i]) = (edges[i], edges[0]);
                }
            }

            head = ShuffleHelper(hash, head);

            foreach (var edge in edges)
            {
                Assert.That(head, Is.SameAs(edge));
                head = edge.NextPredEdge;
            }

            Assert.That(head, Is.Null);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitSsaStress")]
    private static extern ref int SsaStress(ref JitConfigValues config);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgGetAllSuccessors")]
    private static extern List<BasicBlock> GetAllSuccessors(Compiler compiler, BasicBlock block, bool useProfile);

    private static List<BasicBlock> Sources(FlowEdge? edge)
    {
        var result = new List<BasicBlock>();
        var visited = new HashSet<FlowEdge>();

        while (edge is not null)
        {
            Assert.That(visited.Add(edge), Is.True, "Predecessor list must not contain a cycle.");
            result.Add(edge.SourceBlock);
            edge = edge.NextPredEdge;
        }

        return result;
    }

    private static EHblkDsc Clause(EHHandlerType kind, BasicBlock tryBlock, BasicBlock handler, BasicBlock last) => new() {
        ebdHandlerType = kind,
        ebdTryBeg = tryBlock,
        ebdTryLast = tryBlock,
        ebdHndBeg = handler,
        ebdHndLast = last,
        ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
        ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
    };

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
        compiler.info = new Compiler.Info();
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(ExceptionalPredecessorTests);
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
