// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64IndirectionPairingTests
{
    [TestCase(0, false, true)]
    [TestCase(13, false, true)]
    [TestCase(14, false, false)]
    [TestCase(0, true, false)]
    public static void PairReorderingPreservesDistanceCallBarriersAndMarks(int gap, bool call, bool expected)
    {
        WithLowering(false, (compiler, lowering, block) => {
            var first = Load(compiler, block, 0);
            var noise = new GenTreeIntCon(TYP_INT, 42) { IsUnusedValue = true };
            for (var i = 0; i < gap; i++)
            {
                block.InsertAtEnd(i == 0 ? noise : new GenTreeIntCon(TYP_INT, i) { IsUnusedValue = true });
            }
            if (call)
            {
                block.InsertAtEnd(new GenTreeCall(TYP_VOID));
            }

            var second = Load(compiler, block, 8);
            var tail = Consume(block, first, second);
            var originalPredecessor = second.Prev;

            Assert.That(OptimizeForLdpStp(lowering, first), Is.False);
            Assert.That(OptimizeForLdpStp(lowering, second), Is.EqualTo(expected));
            Assert.That(second.Next, Is.SameAs(expected && gap != 0 ? noise : tail));
            if (!expected)
            {
                Assert.That(second.Prev, Is.SameAs(originalPredecessor));
            }
            AssertUnmarked(block);
#if DEBUG
            if (!call)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
#endif
        });
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    public static void PairReorderingDoesNotCrossAddressRedefinitions(int localNumber, bool expected)
    {
        WithLowering(false, (compiler, lowering, block) => {
            var first = Load(compiler, block, 0);
            var value = new GenTreeIntCon(TYP_LONG, 24);
            block.InsertAtEnd(value);
            var definition = compiler.gtNewStoreLclVarNode(localNumber, value);
            block.InsertAtEnd(definition);
            var second = Load(compiler, block, 8);
            _ = Consume(block, first, second);

            Assert.That(OptimizeForLdpStp(lowering, first), Is.False);
            Assert.That(OptimizeForLdpStp(lowering, second), Is.EqualTo(expected));
            AssertUnmarked(block);
        });
    }

    [TestCase(16, 0, false, false, true)]
    [TestCase(8, 0, false, false, false)]
    [TestCase(16, 0, true, false, false)]
    [TestCase(16, 1, false, false, false)]
    [TestCase(16, 1, false, true, true)]
    public static void PairReorderingUsesNativeMemoryAliasExceptions(
        int offset, int localNumber, bool isVolatile, bool referenceBases, bool expected)
    {
        WithLowering(false, (compiler, lowering, block) => {
            if (referenceBases)
            {
                compiler.lvaTable[0].Type = TYP_REF;
                compiler.lvaTable[1].Type = TYP_REF;
            }

            var first = Load(compiler, block, 0);
            var store = Store(compiler, block, offset, 42, localNumber);
            if (isVolatile)
            {
                store.Flags |= GTF_IND_VOLATILE | GTF_ORDER_SIDEEFF;
            }
            var second = Load(compiler, block, 8);
            _ = Consume(block, first, second);

            Assert.That(OptimizeForLdpStp(lowering, first), Is.False);
            Assert.That(OptimizeForLdpStp(lowering, second), Is.EqualTo(expected));
            AssertUnmarked(block);
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    public static void StorePairMovesPreviousStoreAfterDataUnlessConstantsCanBeReused(int secondValue)
    {
        WithLowering(false, (compiler, lowering, block) => {
            var first = Store(compiler, block, 0, 1);
            var noise = new GenTreeIntCon(TYP_INT, 42) { IsUnusedValue = true };
            block.InsertAtEnd(noise);
            var second = Store(compiler, block, 8, secondValue);

            Assert.That(OptimizeForLdpStp(lowering, first), Is.False);
            Assert.That(OptimizeForLdpStp(lowering, second), Is.True);
            Assert.That(second.Next, Is.SameAs(noise));
            if (secondValue == 1)
            {
                Assert.That(first.Next, Is.Not.SameAs(second));
            }
            else
            {
                Assert.That(second.Prev, Is.SameAs(first));
                Assert.That(first.Prev, Is.SameAs(second.Data));
            }
            AssertUnmarked(block);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void MatchedIndirectionsDoNotParticipateInAnotherPair()
    {
        WithLowering(false, (compiler, lowering, block) => {
            var first = Store(compiler, block, 8, 1);
            var second = Store(compiler, block, 0, 1);
            var third = Store(compiler, block, 16, 1);
            var fourth = Store(compiler, block, 24, 1);

            Assert.That(OptimizeForLdpStp(lowering, first), Is.False);
            Assert.That(OptimizeForLdpStp(lowering, second), Is.True);
            Assert.That(OptimizeForLdpStp(lowering, third), Is.False);
            Assert.That(OptimizeForLdpStp(lowering, fourth), Is.True);
            AssertUnmarked(block);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void VolatileAndUnsupportedAccessesAreNotPairCandidates(bool isVolatile)
    {
        WithLowering(false, (compiler, lowering, block) => {
            var first = Store(compiler, block, 0, 1);
            var second = Store(compiler, block, isVolatile ? 8 : 2, 1);
            if (isVolatile)
            {
                first.Flags |= GTF_IND_VOLATILE;
                second.Flags |= GTF_IND_VOLATILE;
            }
            else
            {
                first.Type = TYP_SHORT;
                second.Type = TYP_SHORT;
            }

            Assert.That(OptimizeForLdpStp(lowering, first), Is.False);
            Assert.That(OptimizeForLdpStp(lowering, second), Is.False);
            AssertUnmarked(block);
        });
    }

    [TestCase(false, false, 0, true)]
    [TestCase(true, false, 0, true)]
    [TestCase(false, true, 0, false)]
    [TestCase(true, true, 0, false)]
    [TestCase(false, false, 97, true)]
    [TestCase(false, false, 98, false)]
    [TestCase(false, false, 99, false)]
    public static void LoopForwardingSearchesBeforeLoadsAndAcrossBackedges(
        bool storeAfterLoads, bool redefineBase, int padding, bool expected)
    {
        WithLowering(true, (compiler, lowering, block) => {
            if (!storeAfterLoads)
            {
                _ = Store(compiler, block, 0, 1);
            }
            for (var i = 0; i < padding; i++)
            {
                block.InsertAtEnd(new GenTreeIntCon(TYP_INT, i) { IsUnusedValue = true });
            }
            if (redefineBase)
            {
                var value = new GenTreeIntCon(TYP_LONG, 24);
                block.InsertAtEnd(value);
                block.InsertAtEnd(compiler.gtNewStoreLclVarNode(0, value));
            }

            var first = Load(compiler, block, 0);
            var second = Load(compiler, block, 8);
            _ = Consume(block, first, second);
            if (storeAfterLoads)
            {
                _ = Store(compiler, block, 8, 1);
            }

            Assert.That(IsStoreToLoadForwardingCandidateInLoop(lowering, first, second), Is.EqualTo(expected));
            Assert.That(compiler._dfsTree, Is.Not.Null);
            Assert.That(compiler._loops, Is.Not.Null);
            Assert.That(OptimizeForLdpStp(lowering, first), Is.False);
            Assert.That(OptimizeForLdpStp(lowering, second), Is.EqualTo(!expected));
            AssertUnmarked(block);
        });
    }

    [Test]
    public static void DataflowRejectionUnmarksBothIndirectionTrees()
    {
        WithLowering(false, (compiler, lowering, block) => {
            var first = Load(compiler, block, 0);
            var second = new GenTreeIndir(GT_IND, TYP_LONG, first) { IsUnusedValue = true };
            block.InsertAtEnd(second);

            Assert.That(TryMakeIndirsAdjacent(lowering, first, second), Is.False);
            Assert.That(second.Prev, Is.SameAs(first));
            AssertUnmarked(block);
        });
    }

    private static GenTree Address(Compiler compiler, BasicBlock block, int offset, int localNumber)
    {
        var local = compiler.gtNewLclvNode(compiler.lvaGetDesc(localNumber).Type, localNumber);
        block.InsertAtEnd(local);
        if (offset == 0)
        {
            return local;
        }

        var address = new GenTreeAddrMode(local.Type is TYP_REF ? TYP_BYREF : TYP_LONG, local, null, 1, offset) {
            IsContained = true,
        };
        block.InsertAtEnd(address);
        return address;
    }

    private static GenTreeIndir Load(Compiler compiler, BasicBlock block, int offset)
    {
        var indir = new GenTreeIndir(GT_IND, TYP_LONG, Address(compiler, block, offset, 0));
        indir.Flags |= GTF_IND_NONFAULTING | GTF_GLOB_REF;
        block.InsertAtEnd(indir);
        return indir;
    }

    private static GenTreeStoreInd Store(Compiler compiler, BasicBlock block, int offset, int value, int localNumber = 0)
    {
        var address = Address(compiler, block, offset, localNumber);
        var data = new GenTreeIntCon(TYP_LONG, value);
        var store = new GenTreeStoreInd(TYP_LONG, address, data);
        store.Flags |= GTF_IND_NONFAULTING | GTF_GLOB_REF;
        block.InsertAtEnd(data);
        block.InsertAtEnd(store);
        return store;
    }

    private static GenTreeOp Consume(BasicBlock block, GenTree first, GenTree second)
    {
        var add = new GenTreeOp(GT_ADD, TYP_LONG, first, second) { IsUnusedValue = true };
        block.InsertAtEnd(add);
        return add;
    }

    private static void AssertUnmarked(BasicBlock block)
    {
        for (var node = block.FirstNode; node is not null; node = node.Next)
        {
            Assert.That(node._lirFlags & LIR.Flags.Mark, Is.EqualTo((LIR.Flags)0));
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OptimizeForLdpStp")]
    private static extern bool OptimizeForLdpStp(Lowering lowering, GenTreeIndir indir);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryMakeIndirsAdjacent")]
    private static extern bool TryMakeIndirsAdjacent(Lowering lowering, GenTreeIndir first, GenTreeIndir second);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "IsStoreToLoadForwardingCandidateInLoop")]
    private static extern bool IsStoreToLoadForwardingCandidateInLoop(Lowering lowering, GenTreeIndir first, GenTreeIndir second);

    private static void WithLowering(bool loop, Action<Compiler, Lowering, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.compHndBBtab = [];
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_LONG;
        compiler.lvaTable[1].Type = TYP_LONG;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.info.compFullName = nameof(Arm64IndirectionPairingTests);
#endif
        JitTls.Compiler = compiler;

        try
        {
            compiler.codeGen = new CodeGen(compiler);
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            entry.bbRefs = 1;
            entry.MakeLir(null, null);
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = entry;
            var block = entry;
            if (loop)
            {
                block = BasicBlock.New(compiler, BBJ_COND);
                var exit = BasicBlock.New(compiler, BBJ_RETURN);
                entry.Next = block;
                block.Prev = entry;
                block.Next = exit;
                exit.Prev = block;
                compiler.fgLastBB = exit;
                block.MakeLir(null, null);
                exit.MakeLir(null, null);
                var entryEdge = new FlowEdge(entry, block, null) { Likelihood = 1 };
                entry.SetKindAndTargetEdge(BBJ_ALWAYS, entryEdge);
                var backedge = new FlowEdge(block, block, entryEdge) { Likelihood = 0.5 };
                var exitEdge = new FlowEdge(block, exit, null) { Likelihood = 0.5 };
                block.bbPreds = backedge;
                block.bbRefs = 2;
                exit.bbPreds = exitEdge;
                exit.bbRefs = 1;
                block.SetCond(backedge, exitEdge);
            }

            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
