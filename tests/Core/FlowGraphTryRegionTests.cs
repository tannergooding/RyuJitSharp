// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.EHblkDsc;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class FlowGraphTryRegionTests
{
    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(5, false)]
    [TestCase(5, true)]
    public static void EmptyAndDeletedEhIdsHaveNoRegions(int nextId, bool useDfs)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, 1);
            compiler.compHndBBtab = [];
            compiler.compHndBBtabCount = 0;
            compiler.compEHID = (ushort)nextId;
            var dfs = useDfs ? Dfs(compiler, blocks) : null;
            var regions = FlowGraphTryRegions.Build(compiler, dfs, includeHandlerBlocks: true);

            Assert.That(regions.GetCompiler(), Is.SameAs(compiler));
            Assert.That(regions.GetDfsTree(), Is.SameAs(dfs));
            Assert.That(regions.NumTryRegions(), Is.Zero);
            Assert.That(regions.NumTryCatchRegions(), Is.Zero);
            Assert.That(regions.TryRegionsIncludeHandlerBlocks(), Is.True);
            Assert.That(regions.HasSideEntry(), Is.False);
            Assert.That(regions.GetTryRegionByHeader(blocks[0]), Is.Null);
            Assert.That(RegionSlots(regions).Length, Is.EqualTo(nextId));
            Assert.That(BitVecTraits.GetSize(regions.GetBlockBitVecTraits()), Is.EqualTo(useDfs ? 1 : 2));
#if DEBUG
            Assert.That(CodeGenLifeTransitionTests.Capture(() => FlowGraphTryRegions.Dump(regions)),
                Is.EqualTo($"No try regions in this method{Environment.NewLine}"));
#endif
        });
    }

    [TestCase(EH_HANDLER_CATCH, true)]
    [TestCase(EH_HANDLER_FILTER, true)]
    [TestCase(EH_HANDLER_FINALLY, false)]
    [TestCase(EH_HANDLER_FAULT, false)]
    [TestCase(EH_HANDLER_FAULT_WAS_FINALLY, false)]
    public static void CatchCountsAreBuildSnapshotsWhileAccessorsReadTheDescriptor(
        EHHandlerType handlerType, bool hasCatch)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, 4);
            blocks[1].TryIndex = 0;
            blocks[2].HndIndex = 0;
            blocks[3].HndIndex = 0;
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(blocks[0], blocks[1]));
            var clause = Clause(0, blocks[1], blocks[1], blocks[3], handlerType);
            if (handlerType is EH_HANDLER_FILTER)
            {
                clause.ebdFilter = blocks[2];
            }
            SetClauses(compiler, 1, clause);
            var regions = FlowGraphTryRegions.Build(compiler, null);
            var region = Region(regions, blocks[1]);

            Assert.That(region.HasCatchHandler(), Is.EqualTo(hasCatch));
            Assert.That(regions.NumTryCatchRegions(), Is.EqualTo(hasCatch ? 1 : 0));
            compiler.compHndBBtab[0].ebdHandlerType = hasCatch ? EH_HANDLER_FINALLY : EH_HANDLER_CATCH;
            Assert.That(region.HasCatchHandler(), Is.EqualTo(!hasCatch));
            Assert.That(regions.NumTryCatchRegions(), Is.EqualTo(hasCatch ? 1 : 0));
        });
    }

    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(63, false)]
    [TestCase(64, false)]
    [TestCase(65, false)]
    [TestCase(130, false)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(63, true)]
    [TestCase(64, true)]
    [TestCase(65, true)]
    [TestCase(130, true)]
    public static void MembershipUsesTheSelectedIndexSpaceAcrossBitVectorWords(int count, bool useDfs)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, count + 2, numberStride: 3);
            var edges = new FlowEdge[blocks.Length - 1];
            for (var i = 0; i < edges.Length; i++)
            {
                edges[i] = Connect(blocks[i], blocks[i + 1]);
                blocks[i].SetKindAndTargetEdge(BBJ_ALWAYS, edges[i]);
            }

            for (var i = 1; i <= count; i++)
            {
                blocks[i].TryIndex = 0;
            }

            SetClauses(compiler, 4, Clause(3, blocks[1], blocks[count], blocks[^1], EH_HANDLER_CATCH));
            var dfs = useDfs ? Dfs(compiler, blocks) : null;
            var regions = FlowGraphTryRegions.Build(compiler, dfs);
            var region = Region(regions, blocks[1]);
            Assert.That(region.GetHeaderBlock(), Is.SameAs(blocks[1]));
            Assert.That(region.NumBlocks(), Is.EqualTo(count));
            Assert.That(region.HasCatchHandler(), Is.True);
            Assert.That(region.EnclosingRegion(), Is.Null);
            Assert.That(region.CanEnumerateInReversePostOrder(), Is.EqualTo(useDfs));
            Assert.That(region.EntryEdges().ToArray(), Is.EqualTo<FlowEdge[]>([edges[0]]));
            Assert.That(region.UnreachableBlocks().Length, Is.Zero);
            Assert.That(region.RequiresRuntimeResumption(), Is.False);
            Assert.That(regions.NumTryRegions(), Is.EqualTo(1));
            Assert.That(regions.NumTryCatchRegions(), Is.EqualTo(1));
            Assert.That(regions.TryRegionsIncludeHandlerBlocks(), Is.False);
            Assert.That(regions.GetTryRegionByHeader(blocks[0]), Is.Null);
            Assert.That(regions.GetTryRegionByHeader(blocks[^1]), Is.Null);
            if (count > 1)
            {
                Assert.That(regions.GetTryRegionByHeader(blocks[2]), Is.Null);
            }

            Assert.That(regions.GetBlockIndex(blocks[1]), Is.EqualTo(useDfs ? count : 6));
            Assert.That(BitVecTraits.GetSize(regions.GetBlockBitVecTraits()),
                Is.EqualTo(useDfs ? count + 2 : (3 * (count + 2)) + 1));

            if (useDfs)
            {
                var visited = new List<BasicBlock>();
                Assert.That(region.VisitTryRegionBlocksReversePostOrder(block =>
                {
                    visited.Add(block);
                    return BasicBlockVisit.Continue;
                }), Is.EqualTo(BasicBlockVisit.Continue));
                Assert.That(visited, Is.EqualTo(blocks.AsSpan(1, count).ToArray()));

                var stopAfter = (count / 2) + 1;
                visited.Clear();
                Assert.That(region.VisitTryRegionBlocksReversePostOrder(block =>
                {
                    visited.Add(block);
                    return visited.Count == stopAfter ? BasicBlockVisit.Abort : BasicBlockVisit.Continue;
                }), Is.EqualTo(BasicBlockVisit.Abort));
                Assert.That(visited, Is.EqualTo(blocks.AsSpan(1, stopAfter).ToArray()));
            }
        });
    }

    [TestCase(false, false, false, false, 1)]
    [TestCase(false, false, true, false, 2)]
    [TestCase(true, false, true, false, 1)]
    [TestCase(true, true, false, false, 2)]
    [TestCase(true, false, false, false, 1)]
    [TestCase(false, false, false, true, 2)]
    [TestCase(true, true, false, true, 2)]
    public static void UnreachableBlocksUseDfsMembershipOrThePredlessHeuristic(
        bool useDfs, bool inDfs, bool hasPred, bool throwHelper, int expectedCount)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, 4);
            var header = blocks[1];
            var body = blocks[2];
            header.TryIndex = 0;
            body.TryIndex = 0;
            if (hasPred)
            {
                header.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(header, body));
            }
            if (throwHelper)
            {
                body.SetFlags(BBF_THROW_HELPER);
            }

            SetClauses(compiler, 1, Clause(0, header, body, blocks[^1], EH_HANDLER_FAULT));
            // Header has no predecessors, but is never put in UnreachableBlocks.
            var dfsBlocks = inDfs ? blocks : [blocks[0], header, blocks[^1]];
            var dfs = useDfs ? Dfs(compiler, dfsBlocks) : null;
            if (useDfs && !inDfs)
            {
                body.bbPostorderNum = int.MaxValue;
            }

            var regions = FlowGraphTryRegions.Build(compiler, dfs);
            var region = Region(regions, header);
            Assert.That(region.NumBlocks(), Is.EqualTo(expectedCount));
            Assert.That(region.HasCatchHandler(), Is.False);
            Assert.That(region.UnreachableBlocks().ToArray(),
                Is.EqualTo<BasicBlock[]>(expectedCount == 1 ? [body] : []));
            Assert.That(region.EntryEdges().Length, Is.Zero);
            Assert.That(regions.HasSideEntry(), Is.False);
            if (useDfs)
            {
                var visited = new List<BasicBlock>();
                _ = region.VisitTryRegionBlocksReversePostOrder(block =>
                {
                    visited.Add(block);
                    return BasicBlockVisit.Continue;
                });
                Assert.That(visited, Is.EqualTo<BasicBlock[]>(expectedCount == 1 ? [header] : [header, body]));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MutualProtectionUsesBlockIdentityAndRetainsTheBackingDescriptor(bool useDfs)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, 7);
            ConnectChain(blocks.AsSpan(0, 6));
            blocks[1].TryIndex = 2;
            blocks[2].TryIndex = 0;
            blocks[3].TryIndex = 0;
            blocks[4].TryIndex = 2;
            blocks[6].HndIndex = 1;
            blocks[6].SetKindAndTargetEdge(BBJ_EHCATCHRET, Connect(blocks[6], blocks[5]));

            SetClauses(compiler, 6,
                Clause(5, blocks[2], blocks[3], blocks[6], EH_HANDLER_CATCH, parent: 1),
                Clause(1, blocks[2], blocks[3], blocks[6], EH_HANDLER_FILTER, parent: 2),
                Clause(3, blocks[1], blocks[4], blocks[5], EH_HANDLER_FINALLY));
            var regions = FlowGraphTryRegions.Build(compiler, useDfs ? Dfs(compiler, blocks) : null);
            var inner = RegionSlot(regions, 5);
            var mutual = RegionSlot(regions, 1);
            var outer = RegionSlot(regions, 3);

            Assert.That(regions.GetTryRegionByHeader(blocks[2]), Is.SameAs(inner));
            Assert.That(inner.EnclosingRegion(), Is.SameAs(outer));
            Assert.That(mutual.EnclosingRegion(), Is.SameAs(outer));
            Assert.That(outer.EnclosingRegion(), Is.Null);
            Assert.That(inner.NumBlocks(), Is.EqualTo(2));
            Assert.That(mutual.NumBlocks(), Is.EqualTo(2));
            Assert.That(outer.NumBlocks(), Is.EqualTo(4));
            Assert.That(regions.NumTryRegions(), Is.EqualTo(3));
            Assert.That(regions.NumTryCatchRegions(), Is.EqualTo(2));
            Assert.That(inner.EntryEdges().ToArray(), Is.EqualTo(mutual.EntryEdges().ToArray()));
            Assert.That(inner.RequiresRuntimeResumption(), Is.True);
            Assert.That(mutual.RequiresRuntimeResumption(), Is.False);
            Assert.That(outer.RequiresRuntimeResumption(), Is.False);

            // Equal IL offsets do not make different block ranges mutual-protect.
            compiler.compHndBBtab[1].ebdTryLast = blocks[4];
            Assert.That(inner.EnclosingRegion(), Is.SameAs(mutual));
            compiler.compHndBBtab[0].ebdTryBeg = blocks[3];
            Assert.That(inner.GetHeaderBlock(), Is.SameAs(blocks[3]));
        });
    }

    [TestCase(false, false, 3)]
    [TestCase(false, true, 6)]
    [TestCase(true, false, 3)]
    [TestCase(true, true, 6)]
    public static void HandlerExclusionAppliesAtEachAncestor(
        bool useDfs, bool includeHandlers, int expectedOuterCount)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, 8);
            ConnectChain(blocks);
            blocks[1].TryIndex = 2;
            blocks[2].TryIndex = 1;
            blocks[3].TryIndex = 2;
            blocks[3].HndIndex = 1;
            blocks[4].TryIndex = 0;
            blocks[4].HndIndex = 1;
            blocks[5].TryIndex = 0;
            blocks[5].HndIndex = 1;
            blocks[6].TryIndex = 2;
            SetClauses(compiler, 3,
                Clause(0, blocks[4], blocks[5], blocks[7], EH_HANDLER_FAULT, parent: 2, enclosingHandler: 1),
                Clause(1, blocks[2], blocks[2], blocks[3], EH_HANDLER_CATCH, parent: 2),
                Clause(2, blocks[1], blocks[6], blocks[7], EH_HANDLER_FINALLY));

            var regions = FlowGraphTryRegions.Build(compiler, useDfs ? Dfs(compiler, blocks) : null, includeHandlers);
            var inner = Region(regions, blocks[4]);
            var outer = Region(regions, blocks[1]);
            Assert.That(inner.EnclosingRegion(), Is.SameAs(outer));
            Assert.That(inner.NumBlocks(), Is.EqualTo(2));
            Assert.That(outer.NumBlocks(), Is.EqualTo(expectedOuterCount));
            Assert.That(Region(regions, blocks[2]).NumBlocks(), Is.EqualTo(1));
            Assert.That(inner.UnreachableBlocks().Length, Is.Zero);
            Assert.That(outer.UnreachableBlocks().Length, Is.Zero);
            Assert.That(regions.TryRegionsIncludeHandlerBlocks(), Is.EqualTo(includeHandlers));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void EntryEdgesKeepPredOrderAndExcludeInternalAndCatchretEdges(bool useDfs, bool sideEntry)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, 6);
            var header = blocks[2];
            var body = blocks[3];
            header.TryIndex = 0;
            body.TryIndex = 0;
            blocks[4].HndIndex = 0;
            var first = Connect(blocks[0], header);
            var second = Connect(blocks[1], header);
            second.incrementDupCount(2);
            header.bbRefs += 2;
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, first);
            blocks[1].SwitchTargets = new BBswtDesc([second, second, second], [0, 1, 2],
                hasDefault: true, dominantCase: 0);
            body.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(body, header));
            blocks[4].SetKindAndTargetEdge(BBJ_EHCATCHRET, Connect(blocks[4], header));
            header.SetKindAndTargetEdge(BBJ_ALWAYS, Connect(header, body));
            if (sideEntry)
            {
                blocks[0].SetCond(first, Connect(blocks[0], body));
            }

            SetClauses(compiler, 1, Clause(0, header, body, blocks[4], EH_HANDLER_CATCH));
            var regions = FlowGraphTryRegions.Build(compiler, useDfs ? Dfs(compiler, blocks, hasCycle: true) : null);
            var region = Region(regions, header);
            Assert.That(region.EntryEdges().ToArray(), Is.EqualTo<FlowEdge[]>([second, first]));
            Assert.That(second.DupCount, Is.EqualTo(3));
            Assert.That(region.NumBlocks(), Is.EqualTo(2));
            Assert.That(region.RequiresRuntimeResumption(), Is.True);
            Assert.That(regions.HasSideEntry(), Is.EqualTo(sideEntry));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnreachableNestedBlocksAreNotAddedToAncestors(bool useDfs)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, 5);
            _ = Connect(blocks[0], blocks[1]);
            _ = Connect(blocks[1], blocks[2]);
            blocks[1].TryIndex = 1;
            blocks[2].TryIndex = 0;
            blocks[3].TryIndex = 0;
            SetClauses(compiler, 2,
                Clause(0, blocks[2], blocks[3], blocks[4], EH_HANDLER_CATCH, parent: 1),
                Clause(1, blocks[1], blocks[3], blocks[4], EH_HANDLER_FINALLY));
            var dfs = useDfs ? Dfs(compiler, [blocks[0], blocks[1], blocks[2], blocks[4]]) : null;
            blocks[3].bbPostorderNum = int.MaxValue;
            var regions = FlowGraphTryRegions.Build(compiler, dfs);
            var inner = Region(regions, blocks[2]);
            var outer = Region(regions, blocks[1]);

            Assert.That(inner.NumBlocks(), Is.EqualTo(1));
            Assert.That(outer.NumBlocks(), Is.EqualTo(2));
            Assert.That(inner.UnreachableBlocks().ToArray(), Is.EqualTo<BasicBlock[]>([blocks[3]]));
            Assert.That(outer.UnreachableBlocks().Length, Is.Zero);
        });
    }

#if DEBUG
    [TestCase(false, " [bbNum]: BB02 BB03 BB04")]
    [TestCase(true, " [rpo]: BB02 BB04 BB03")]
    public static void DumpUsesBitOrderOrReverseDfsOrder(bool useDfs, string expectedMembers)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, 5);
            var entry = Connect(blocks[0], blocks[1]);
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, entry);
            blocks[0].SetFlags(BBF_ASYNC_RESUMPTION | BBF_CATCH_RESUMPTION);
            blocks[1].SetCond(Connect(blocks[1], blocks[2]), Connect(blocks[1], blocks[3]));
            blocks[2].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(blocks[2], blocks[4]));
            blocks[3].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(blocks[3], blocks[4]));
            for (var i = 1; i <= 3; i++)
            {
                blocks[i].TryIndex = 0;
            }

            SetClauses(compiler, 1, Clause(0, blocks[1], blocks[3], blocks[4], EH_HANDLER_CATCH));
            var dfs = useDfs ? PostOrder(compiler, [blocks[4], blocks[2], blocks[3], blocks[1], blocks[0]]) : null;
            var regions = FlowGraphTryRegions.Build(compiler, dfs);
            Assert.That(CodeGenLifeTransitionTests.Capture(() => FlowGraphTryRegion.Dump(
                Region(regions, blocks[1]))), Is.EqualTo(
                "EH#00: 3 blocks [excluding handler blocks] [outermost]:" +
                expectedMembers + " [entries]:  BB01->BB02[async][catch]"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CollectionDumpUsesStableEhIdOrderAndSkipsGaps(bool includeHandlers)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            var blocks = Blocks(compiler, 6);
            ConnectChain(blocks);
            blocks[1].TryIndex = 0;
            blocks[2].TryIndex = 0;
            blocks[3].TryIndex = 1;
            blocks[4].TryIndex = 1;
            SetClauses(compiler, 5,
                Clause(4, blocks[1], blocks[2], blocks[5], EH_HANDLER_CATCH),
                Clause(1, blocks[3], blocks[4], blocks[5], EH_HANDLER_FAULT));
            var regions = FlowGraphTryRegions.Build(compiler, null, includeHandlers);
            var exclusion = includeHandlers ? "" : " [excluding handler blocks]";

            Assert.That(CodeGenLifeTransitionTests.Capture(() => FlowGraphTryRegions.Dump(regions)),
                Is.EqualTo($"2 try regions:{Environment.NewLine}" +
                    $"EH#01: 2 blocks{exclusion} [outermost]: [bbNum]: BB04 BB05 [entries]:  BB03->BB04{Environment.NewLine}" +
                    $"EH#00: 2 blocks{exclusion} [outermost]: [bbNum]: BB02 BB03 [entries]:  BB01->BB02{Environment.NewLine}"));
        });
    }

    [Test]
    public static void NestedDumpShowsImmediateMutualProtectParent()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var blocks = Blocks(compiler, 3);
            _ = Connect(blocks[0], blocks[1]);
            blocks[1].TryIndex = 0;
            SetClauses(compiler, 3,
                Clause(2, blocks[1], blocks[1], blocks[2], EH_HANDLER_CATCH, parent: 1),
                Clause(0, blocks[1], blocks[1], blocks[2], EH_HANDLER_CATCH));
            var regions = FlowGraphTryRegions.Build(compiler, null);
            var inner = Region(regions, blocks[1]);

            Assert.That(inner.EnclosingRegion(), Is.Null);
            Assert.That(CodeGenLifeTransitionTests.Capture(() => FlowGraphTryRegion.Dump(inner)),
                Is.EqualTo("EH#00: 1 blocks [excluding handler blocks] [ in EH#01]: [bbNum]: BB02 [entries]:  BB01->BB02"));
        });
    }
#endif

    private static BasicBlock[] Blocks(Compiler compiler, int count, int numberStride = 1)
    {
        var blocks = new BasicBlock[count];
        for (var i = 0; i < count; i++)
        {
            blocks[i] = new BasicBlock(null, null) { bbNum = (i + 1) * numberStride, bbRefs = i == 0 ? 1 : 0 };
            blocks[i].SetKindAndTargetEdge(BBJ_RETURN, null);
            if (i > 0)
            {
                blocks[i - 1].Next = blocks[i];
                blocks[i].Prev = blocks[i - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBNumMax = blocks[^1].bbNum;
        compiler.fgBBcount = count;

        return blocks;
    }

    private static FlowGraphTryRegion Region(FlowGraphTryRegions regions, BasicBlock header)
        => regions.GetTryRegionByHeader(header) ?? throw new AssertionException("Missing try region at the expected header.");

    private static FlowGraphTryRegion RegionSlot(FlowGraphTryRegions regions, int id)
        => RegionSlots(regions)[id] ?? throw new AssertionException("Missing try region at the expected EH ID.");

    private static FlowEdge Connect(BasicBlock source, BasicBlock target)
    {
        var edge = new FlowEdge(source, target, target.bbPreds);
        edge.incrementDupCount();
        target.bbPreds = edge;
        target.bbRefs++;

        return edge;
    }

    private static void ConnectChain(ReadOnlySpan<BasicBlock> blocks)
    {
        for (var i = 1; i < blocks.Length; i++)
        {
            blocks[i - 1].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(blocks[i - 1], blocks[i]));
        }
    }

    private static EHblkDsc Clause(
        ushort id, BasicBlock first, BasicBlock last, BasicBlock handler, EHHandlerType type,
        ushort parent = NO_ENCLOSING_INDEX, ushort enclosingHandler = NO_ENCLOSING_INDEX)
    {
        return new EHblkDsc
        {
            ebdID = id,
            ebdTryBeg = first,
            ebdTryLast = last,
            ebdHndBeg = handler,
            ebdHndLast = handler,
            ebdHandlerType = type,
            ebdEnclosingTryIndex = parent,
            ebdEnclosingHndIndex = enclosingHandler,
        };
    }

    private static void SetClauses(Compiler compiler, ushort nextId, params EHblkDsc[] clauses)
    {
        compiler.compHndBBtab = clauses;
        compiler.compHndBBtabCount = (ushort)clauses.Length;
        compiler.compEHID = nextId;
    }

    private static FlowGraphDfsTree Dfs(Compiler compiler, ReadOnlySpan<BasicBlock> chain, bool hasCycle = false)
    {
        var postOrder = chain.ToArray();
        Array.Reverse(postOrder);

        return PostOrder(compiler, postOrder, hasCycle);
    }

    private static FlowGraphDfsTree PostOrder(Compiler compiler, BasicBlock[] blocks, bool hasCycle = false)
    {
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i].bbPostorderNum = i;
        }

        return new FlowGraphDfsTree(compiler, blocks, blocks.Length, hasCycle, profileAware: false);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_tryRegions")]
    private static extern ref FlowGraphTryRegion?[] RegionSlots(FlowGraphTryRegions regions);
}
