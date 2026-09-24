// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class NaturalLoopTests
{
    [Test]
    public static void AcyclicGraphsIgnoreUnreachableCyclesAndInvalidateEhCache()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN, BBJ_ALWAYS);
            _ = Jump(blocks[0], blocks[1]);
            _ = Jump(blocks[2], blocks[2]);
            compiler.GetBlockToEHPreds()[blocks[0]] = null;
            var loops = Find(compiler);
            Assert.That(loops.NumLoops, Is.Zero);
            Assert.That(loops.ImproperLoopHeaders, Is.Zero);
            Assert.That(compiler._blockToEHPreds, Is.Null);
            Assert.That(loops.GetLoopByHeader(blocks[2]), Is.Null);
        });
    }

    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(130, false)]
    [TestCase(2, true)]
    public static void LoopMembershipTraversalAndExitClassification(int count, bool secondExit)
    {
        WithCompiler(compiler => {
            var kinds = new BBKinds[count + 2];
            Array.Fill(kinds, BBJ_ALWAYS);
            kinds[1] = BBJ_COND;
            kinds[^1] = BBJ_RETURN;
            var blocks = Blocks(compiler, kinds);
            var header = blocks[1];
            var entry = Jump(blocks[0], header);
            var back = count == 1 ? Connect(header, header) : Connect(blocks[count], header);
            var exit = Connect(header, blocks[^1]);
            header.SetCond(count == 1 ? back : Connect(header, blocks[2]), exit);

            for (var i = 2; i < count; i++)
            {
                _ = Jump(blocks[i], blocks[i + 1]);
            }

            if (count > 1)
            {
                if (secondExit)
                {
                    blocks[count].SetCond(back, Connect(blocks[count], blocks[^1]));
                }
                else
                {
                    blocks[count].SetKindAndTargetEdge(BBJ_ALWAYS, back);
                }
            }

            var loops = Find(compiler);
            Assert.That(loops.NumLoops, Is.EqualTo(1));
            var loop = loops.GetLoopByIndex(0);
            Assert.That(loops.GetLoopByHeader(header), Is.SameAs(loop));
            Assert.That(loops.GetLoopByHeader(blocks[0]), Is.Null);
            Assert.That(loops.GetLoopByHeader(blocks[^1]), Is.Null);
            Assert.That(loop.NumLoopBlocks(), Is.EqualTo(count));
            Assert.That(loop.ContainsBlock(blocks[0]), Is.False);
            Assert.That(loop.ContainsBlock(blocks[^1]), Is.False);
            Assert.That(loop.GetPreheader(), Is.SameAs(blocks[0]));
            Assert.That(loop.EntryEdge(0), Is.SameAs(entry));
            Assert.That(loop.BackEdge(0), Is.SameAs(back));
            Assert.That(loop.ExitEdge(0), Is.SameAs(exit));
            Assert.That(loop.ExitEdges.Length, Is.EqualTo(secondExit ? 2 : 1));
            Assert.That(loops.IsLoopBackEdge(back), Is.True);
            Assert.That(loops.IsLoopBackEdge(entry), Is.False);
            Assert.That(loops.IsLoopExitEdge(exit), Is.True);
            Assert.That(loops.IsLoopExitEdge(new FlowEdge(header, blocks[^1], null)), Is.False);
            Assert.That(loop.Parent, Is.Null);
            Assert.That(loop.Child, Is.Null);
            Assert.That(loop.GetDepth(), Is.Zero);
            var visited = new List<BasicBlock>();
            Assert.That(loop.VisitLoopBlocksReversePostOrder(block => {
                visited.Add(block);
                return BasicBlockVisit.Continue;
            }), Is.EqualTo(BasicBlockVisit.Continue));
            Assert.That(visited, Is.EqualTo(blocks[1..^1]));
            visited.Reverse();
            var postOrder = new List<BasicBlock>();
            _ = loop.VisitLoopBlocksPostOrder(block => {
                postOrder.Add(block);
                return BasicBlockVisit.Continue;
            });
            Assert.That(postOrder, Is.EqualTo(visited));
            var exitVisits = new List<BasicBlock>();
            _ = loop.VisitRegularExitBlocks(block => {
                exitVisits.Add(block);
                return BasicBlockVisit.Continue;
            });
            Assert.That(exitVisits, Is.EqualTo([blocks[^1]]));
            Assert.That(loop.VisitLoopBlocks(_ => BasicBlockVisit.Abort), Is.EqualTo(BasicBlockVisit.Abort));
            Assert.That(loop.VisitRegularExitBlocks(_ => BasicBlockVisit.Abort), Is.EqualTo(BasicBlockVisit.Abort));
            loop.SetEntryEdge(entry);
            Assert.That(loop.EntryEdges.Length, Is.EqualTo(1));
        });
    }

    [Test]
    public static void NestedLoopsKeepParentChildSiblingAndIterationOrder()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_COND, BBJ_COND, BBJ_COND, BBJ_ALWAYS, BBJ_RETURN);
            _ = Jump(blocks[0], blocks[1]);
            blocks[1].SetCond(Connect(blocks[1], blocks[2]), Connect(blocks[1], blocks[5]));
            blocks[2].SetCond(Connect(blocks[2], blocks[2]), Connect(blocks[2], blocks[3]));
            blocks[3].SetCond(Connect(blocks[3], blocks[3]), Connect(blocks[3], blocks[4]));
            _ = Jump(blocks[4], blocks[1]);
            var loops = Find(compiler);
            Assert.That(loops.NumLoops, Is.EqualTo(3));
            var outer = loops.GetLoopByIndex(0);
            var first = loops.GetLoopByIndex(1);
            var second = loops.GetLoopByIndex(2);
            var map = BlockToNaturalLoopMap.Build(loops);
            Assert.That(map.GetLoop(blocks[0]), Is.Null);
            Assert.That(map.GetLoop(blocks[1]), Is.SameAs(outer));
            Assert.That(map.GetLoop(blocks[2]), Is.SameAs(first));
            Assert.That(map.GetLoop(blocks[3]), Is.SameAs(second));
            Assert.That(map.GetLoop(blocks[4]), Is.SameAs(outer));
            Assert.That(map.GetLoop(blocks[5]), Is.Null);
            Assert.That(outer.NumLoopBlocks(), Is.EqualTo(4));
            Assert.That(outer.Child, Is.SameAs(first));
            Assert.That(first.Sibling, Is.SameAs(second));
            Assert.That(second.Sibling, Is.Null);
            Assert.That(first.Parent, Is.SameAs(outer));
            Assert.That(second.Parent, Is.SameAs(outer));
            Assert.That(first.GetDepth(), Is.EqualTo(1));
            Assert.That(outer.ContainsLoop(first), Is.True);
            Assert.That(first.ContainsLoop(outer), Is.False);
            Assert.That(loops.InReversePostOrder().ToArray(), Is.EqualTo([outer, first, second]));
            Assert.That(loops.InPostOrder(), Is.EqualTo([second, first, outer]));

            for (var i = 0; i < loops.NumLoops; i++)
            {
                var loop = loops.GetLoopByIndex(i);
                Assert.That(loop.Index, Is.EqualTo(i));
                Assert.That(loops.GetLoopByHeader(loop.Header), Is.SameAs(loop));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IrreducibleCyclesAreRejectedAndMarkTheirEnclosingLoop(bool enclosingLoop)
    {
        WithCompiler(compiler => {
            BasicBlock[] blocks;

            if (enclosingLoop)
            {
                blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_COND, BBJ_COND, BBJ_ALWAYS, BBJ_COND, BBJ_RETURN);
                _ = Jump(blocks[0], blocks[1]);
                blocks[1].SetCond(Connect(blocks[1], blocks[2]), Connect(blocks[1], blocks[5]));
                blocks[2].SetCond(Connect(blocks[2], blocks[3]), Connect(blocks[2], blocks[4]));
                _ = Jump(blocks[3], blocks[4]);
                blocks[4].SetCond(Connect(blocks[4], blocks[3]), Connect(blocks[4], blocks[1]));
            }
            else
            {
                blocks = Blocks(compiler, BBJ_COND, BBJ_ALWAYS, BBJ_ALWAYS);
                blocks[0].SetCond(Connect(blocks[0], blocks[1]), Connect(blocks[0], blocks[2]));
                _ = Jump(blocks[1], blocks[2]);
                _ = Jump(blocks[2], blocks[1]);
            }

            var loops = Find(compiler);
            Assert.That(loops.ImproperLoopHeaders, Is.EqualTo(1));
            Assert.That(loops.NumLoops, Is.EqualTo(enclosingLoop ? 1 : 0));

            if (enclosingLoop)
            {
                Assert.That(loops.GetLoopByIndex(0).ContainsImproperHeader, Is.True);
                Assert.That(loops.GetLoopByIndex(0).NumLoopBlocks(), Is.EqualTo(4));
            }
        });
    }

    [Test]
    public static void ExceptionalPredecessorsParticipateInLoopMembership()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_COND, BBJ_ALWAYS, BBJ_ALWAYS, BBJ_RETURN);
            _ = Jump(blocks[0], blocks[1]);
            blocks[1].SetCond(Connect(blocks[1], blocks[2]), Connect(blocks[1], blocks[4]));
            _ = Jump(blocks[2], blocks[1]);
            _ = Jump(blocks[3], blocks[2]);
            blocks[2].TryIndex = 0;
            blocks[3].HndIndex = 0;
            compiler.compHndBBtab = [new() {
                ebdHandlerType = EHHandlerType.EH_HANDLER_CATCH,
                ebdTryBeg = blocks[2], ebdTryLast = blocks[2], ebdHndBeg = blocks[3], ebdHndLast = blocks[3],
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX, ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            }];
            compiler.compHndBBtabCount = 1;
            var loops = Find(compiler);
            Assert.That(loops.NumLoops, Is.EqualTo(2));
            Assert.That(loops.GetLoopByIndex(0).NumLoopBlocks(), Is.EqualTo(3));
            Assert.That(loops.GetLoopByIndex(1).NumLoopBlocks(), Is.EqualTo(2));
            Assert.That(loops.GetLoopByIndex(1).ContainsBlock(blocks[3]), Is.True);
            Assert.That(loops.GetLoopByIndex(1).Parent, Is.SameAs(loops.GetLoopByIndex(0)));
        });
    }

    [Test]
    public static void CallfinallyBackedgesIntoHandlerHeadersCannotBeCanonicalized()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_CALLFINALLY);
            _ = Jump(blocks[0], blocks[1]);
            blocks[1].SetKindAndTargetEdge(BBJ_CALLFINALLY, Connect(blocks[1], blocks[0]));
            blocks[1].SetFlags(BBF_RETLESS_CALL);
            blocks[0].HndIndex = blocks[1].HndIndex = 0;
            compiler.compHndBBtab = [new() {
                ebdHandlerType = EHHandlerType.EH_HANDLER_FINALLY,
                ebdTryBeg = blocks[0], ebdTryLast = blocks[0], ebdHndBeg = blocks[0], ebdHndLast = blocks[1],
                ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX, ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
            }];
            compiler.compHndBBtabCount = 1;
            var loops = Find(compiler);
            Assert.That(loops.NumLoops, Is.Zero);
            Assert.That(loops.ImproperLoopHeaders, Is.EqualTo(1));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void BitVisitsSnapshotCurrentWordAndReadLaterWordsLive(bool reverse, bool abort)
    {
        WithCompiler(compiler => {
            var width = IntPtr.Size * 8;
            var traits = new BitVecTraits(compiler, 2 * width);
            var bits = BitVecOps.MakeEmpty(traits);
            int[] setBits = [0, 1, width - 1, width, width + 1, (2 * width) - 1];

            foreach (var index in setBits)
            {
                BitVecOps.AddElemD(traits, bits, index);
            }

            var visited = new List<int>();
            bool Visit(int index)
            {
                if (visited.Count == 0)
                {
                    Array.Clear(bits);
                    BitVecOps.AddElemD(traits, bits, reverse ? 3 : width + 3);
                }

                visited.Add(index);
                return !abort;
            }
            var result = reverse ? BitVecOps.VisitBitsReverse(traits, bits, Visit) : BitVecOps.VisitBits(traits, bits, Visit);
            int[] expected = reverse ? [(2 * width) - 1, width + 1, width, 3] : [0, 1, width - 1, width + 3];
            Assert.That(visited, Is.EqualTo(abort ? expected[..1] : expected));
            Assert.That(result, Is.EqualTo(!abort));
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void DumpsPreserveNativeRangesEdgesAndNewlines(bool gap)
    {
        WithCompiler(compiler => {
            var blocks = gap
                ? Blocks(compiler, BBJ_ALWAYS, BBJ_COND, BBJ_RETURN, BBJ_ALWAYS, BBJ_RETURN)
                : Blocks(compiler, BBJ_ALWAYS, BBJ_COND, BBJ_ALWAYS, BBJ_RETURN);
            var body = blocks[^2];
            _ = Jump(blocks[0], blocks[1]);
            blocks[1].SetCond(Connect(blocks[1], body), Connect(blocks[1], blocks[^1]));
            _ = Jump(body, blocks[1]);
            var loops = Find(compiler);
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;

            try
            {
                s_jitstdout = writer;
                FlowGraphNaturalLoops.Dump(loops);
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }

            var expected = "\n***************  Natural loop graph\nL00 header: BB02\n  Members (2): " +
                (gap ? "BB02;BB04" : "[BB02..BB03]") +
                $"\n  Entry: BB01 -> BB02\n  Exit: BB02 -> BB{blocks.Length:D2}\n  Back: BB{blocks.Length - 1:D2} -> BB02\n\n";
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()),
                Is.EqualTo(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
        });
    }
#endif

    [Test]
    public static void LocalDefinitionMapsPreserveNativeCollisionAndOverwriteGrowthOrder()
    {
        var map = new LoopDefinitions.LocalDefinitionsMap();

        foreach (var key in new[] { 0, 23, 46, 9, 32, 55 })
        {
            map.Set(key);
        }

        var keys = new List<int>();
        map.VisitKeys(keys.Add);
        Assert.That(keys, Is.EqualTo([9, 0, 55, 46, 32, 23]));

        map.Set(9);
        keys.Clear();
        map.VisitKeys(keys.Add);
        Assert.That(keys, Is.EqualTo([23, 46, 0, 32, 55, 9]));
    }

    [Test]
    public static void LoopDefinitionsVisitExclusiveChildMapsBeforeTheirParent()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_COND, BBJ_COND, BBJ_COND, BBJ_ALWAYS, BBJ_RETURN);
            _ = Jump(blocks[0], blocks[1]);
            blocks[1].SetCond(Connect(blocks[1], blocks[2]), Connect(blocks[1], blocks[5]));
            blocks[2].SetCond(Connect(blocks[2], blocks[2]), Connect(blocks[2], blocks[3]));
            blocks[3].SetCond(Connect(blocks[3], blocks[3]), Connect(blocks[3], blocks[4]));
            _ = Jump(blocks[4], blocks[1]);
            compiler.lvaCount = 3;
            compiler.lvaTable = new LclVarDsc[3];

            for (var i = 0; i < 3; i++)
            {
                compiler.lvaTable[i].Type = TYP_INT;
                var store = compiler.gtNewStoreLclVarNode(i, compiler.gtNewIconNode(TYP_INT, i));
                compiler.fgInsertStmtAtEnd(blocks[i + 1], compiler.gtNewStmt(store));
            }

            var loops = Find(compiler);
            var definitions = new LoopDefinitions(loops);
            var locals = new List<int>();
            definitions.VisitDefinedLocalNums(loops.GetLoopByIndex(0), locals.Add);
            Assert.That(locals, Is.EqualTo([1, 2, 0]));

            locals.Clear();
            definitions.VisitDefinedLocalNums(loops.GetLoopByIndex(0), locals.Add);
            Assert.That(locals, Is.EqualTo([1, 2, 0]));
        });
    }

    [Test]
    public static void LocalAddressAssertionsDistinguishOutgoingAndAlwaysTrueFacts()
    {
        WithCompiler(compiler => {
            var blocks = Blocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            _ = Jump(blocks[0], blocks[1]);
            var assertions = CreateLocalAddressAssertions(compiler, Find(compiler));
            assertions.Record(0, 1, 4);
            assertions.Record(2, 1, 8);
            assertions.EndBlock(blocks[0]);
            assertions.StartBlock(blocks[1]);
            Assert.That(assertions.CurrentAssertions, Is.EqualTo(3UL));
            Assert.That(assertions.AlwaysAssertions, Is.EqualTo(3UL));

            assertions.Clear(0);
            assertions.Record(0, 1, 4);
            Assert.That(assertions.CurrentAssertions, Is.EqualTo(3UL));
            Assert.That(assertions.AlwaysAssertions, Is.EqualTo(2UL));
            Assert.That(assertions.GetCurrentAssertion(0),
                Is.EqualTo(new LocalEqualsLocalAddrAssertion(0, 1, 4)));
        });
    }

    [Test]
    public static void FullLocalAddressAssertionTablesReuseExistingFacts()
    {
        WithCompiler(compiler => {
            _ = Blocks(compiler, BBJ_RETURN);
            var assertions = CreateLocalAddressAssertions(compiler, Find(compiler));

            for (uint i = 0; i < 64; i++)
            {
                assertions.Clear(0);
                assertions.Record(0, 1, i);
            }

            Assert.That(assertions.CurrentAssertions, Is.EqualTo(1UL << 63));
            assertions.Clear(0);
            assertions.Record(0, 1, 64);
            Assert.That(assertions.GetCurrentAssertion(0), Is.Null);
            assertions.Record(0, 1, 17);
            Assert.That(assertions.CurrentAssertions, Is.EqualTo(1UL << 17));

            compiler.lvaTable[2].lvIsStructField = true;
            compiler.lvaTable[2].lvParentLcl = 1;
            assertions.OnExposed(1);
            Assert.That(assertions.IsMarkedForExposure(2), Is.True);
        });
    }

    private static LocalEqualsLocalAddrAssertions CreateLocalAddressAssertions(Compiler compiler, FlowGraphNaturalLoops loops)
    {
        compiler._dfsTree = loops.DfsTree;
        compiler._loops = loops;
        compiler.lvaCount = 3;
        compiler.lvaTable = new LclVarDsc[3];
        return new LocalEqualsLocalAddrAssertions(compiler, new LoopDefinitions(loops));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgComputeDfs")]
    private static extern FlowGraphDfsTree ComputeDfs(Compiler compiler, bool useProfile);

    private static FlowGraphNaturalLoops Find(Compiler compiler) => FlowGraphNaturalLoops.Find(ComputeDfs(compiler, false));

    private static BasicBlock[] Blocks(Compiler compiler, params BBKinds[] kinds)
    {
        var blocks = new BasicBlock[kinds.Length];

        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = BasicBlock.New(compiler, kinds[i]);
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

    private static FlowEdge Connect(BasicBlock source, BasicBlock target)
    {
        var edge = new FlowEdge(source, target, target.bbPreds) { Likelihood = 0.5 };
        target.bbPreds = edge;
        target.bbRefs++;
        return edge;
    }

    private static FlowEdge Jump(BasicBlock source, BasicBlock target)
    {
        var edge = Connect(source, target);
        edge.Likelihood = 1;
        source.SetKindAndTargetEdge(BBJ_ALWAYS, edge);
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
        compiler.info.compFullName = nameof(NaturalLoopTests);
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
