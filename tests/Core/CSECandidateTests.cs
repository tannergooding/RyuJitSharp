// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
using BitVecOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CSECandidateTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_Init")]
    private static extern void Initialize(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_Index")]
    private static extern int Index(Compiler compiler, GenTree tree, Statement statement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_Locate")]
    private static extern bool Locate(Compiler compiler, CSE_HeuristicCommon heuristic);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optCSEstop")]
    private static extern void Stop(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_InitDataFlow")]
    private static extern void InitializeDataFlow(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_DataFlow")]
    private static extern void RunDataFlow(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "optValnumCSE_Availability")]
    private static extern void Classify(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEhash")]
    private static extern ref CSEdsc?[] Hash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEtab")]
    private static extern ref CSEdsc?[] Table(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEhashSize")]
    private static extern ref nint HashSize(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEhashMaxCountBeforeResize")]
    private static extern ref nint HashLimit(Compiler compiler);

    [Test]
    public static void DescriptorCountsDistinctAndRepeatedLocalsWithNativeEightLocalBudget()
    {
        WithCompiler(compiler =>
        {
            var first = compiler.gtNewLclvNode(TYP_INT, 1);
            var repeated = compiler.gtNewLclvNode(TYP_INT, 1);
            var third = compiler.gtNewLclvNode(TYP_INT, 2);
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, first,
                compiler.gtNewBinaryNode(GT_ADD, TYP_INT, repeated, third));
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            var descriptor = new CSEdsc(tree, compiler.gtNewStmt(tree), block);

            descriptor.ComputeNumLocals(compiler);

            Assert.Multiple(() =>
            {
                Assert.That(descriptor.numDistinctLocals, Is.EqualTo(2));
                Assert.That(descriptor.numLocalOccurrences, Is.EqualTo(3));
                Assert.That(descriptor.csdTreeLast, Is.SameAs(descriptor.csdTreeList));
            });
        });
    }

    [Test]
    public static void DuplicateCandidatesShareHashDescriptorAndRetainOccurrenceOrder()
    {
        WithCompiler(compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.compCurBB = block;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            Initialize(compiler);

            var vn = store.VNForIntCon(142);
            var first = compiler.gtNewIconNode(TYP_INT, 142);
            var second = compiler.gtNewIconNode(TYP_INT, 142);
            var third = compiler.gtNewIconNode(TYP_INT, 142);
            first._vnPair.SetBoth(vn);
            second._vnPair.SetBoth(vn);
            third._vnPair.SetBoth(vn);
            var firstStmt = compiler.gtNewStmt(first);
            var secondStmt = compiler.gtNewStmt(second);
            var thirdStmt = compiler.gtNewStmt(third);

            Assert.That(Index(compiler, first, firstStmt), Is.Zero);
            Assert.That(Index(compiler, second, secondStmt), Is.EqualTo(1));
            Assert.That(Index(compiler, third, thirdStmt), Is.EqualTo(1));
            Stop(compiler);

            var descriptor = Table(compiler)[0];
            Assert.That(descriptor, Is.Not.Null);
            var bucketDescriptor = Array.Find(Hash(compiler),
                bucket => ReferenceEquals(bucket, descriptor));
            Assert.Multiple(() =>
            {
                Assert.That(HashSize(compiler), Is.EqualTo((nint)128));
                Assert.That(HashLimit(compiler), Is.EqualTo((nint)512));
                Assert.That(descriptor!.csdTreeList.tslTree, Is.SameAs(first));
                Assert.That(descriptor.csdTreeList.tslNext!.tslTree, Is.SameAs(second));
                Assert.That(descriptor.csdTreeLast.tslTree, Is.SameAs(third));
                Assert.That(descriptor.csdTreeLast.tslNext, Is.Null);
                Assert.That(bucketDescriptor, Is.SameAs(descriptor));
            });
        });
    }

    [Test]
    public static void CandidateIndexStopsAtNativeSixtyFourCandidateLimit()
    {
        WithCompiler(compiler =>
        {
            compiler.compCurBB = BasicBlock.New(compiler, BBJ_RETURN);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            Initialize(compiler);

            for (var index = 0; index <= 64; index++)
            {
                var value = index + 1000;
                var vn = store.VNForIntCon(value);
                var first = compiler.gtNewIconNode(TYP_INT, value);
                var second = compiler.gtNewIconNode(TYP_INT, value);
                first._vnPair.SetBoth(vn);
                second._vnPair.SetBoth(vn);
                Assert.That(Index(compiler, first, compiler.gtNewStmt(first)), Is.Zero);
                Assert.That(Index(compiler, second, compiler.gtNewStmt(second)),
                    Is.EqualTo(index < 64 ? index + 1 : 0));
            }

            Stop(compiler);
            Assert.That(Table(compiler), Has.Length.EqualTo(64));
        });
    }

    [Test]
    public static void HeuristicRejectsNonCandidatesAndAddressModeNodes()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var heuristic = new CSE_Heuristic(compiler);
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewLclvNode(TYP_INT, 1);
            left.SetCosts(3, 3);
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, left, right);
            tree._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            tree.SetCosts(3, 3);
            Assert.That(heuristic.ConsiderTree(tree, false), Is.True);

            tree.CanCse = false;
            Assert.That(heuristic.ConsiderTree(tree, false), Is.False);
            tree.CanCse = true;
            tree.Flags |= GenTreeFlags.GTF_ADDRMODE_NO_CSE;
            Assert.That(heuristic.ConsiderTree(tree, false), Is.False);
            Assert.That(heuristic.ConsiderTree(left, false), Is.False);
        });
    }

    [Test]
    public static void LocateVisitsEligibleTreesInStatementOrder()
    {
        WithCompiler(compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var vn = store.VNForExpr(null, TYP_INT);
            Initialize(compiler);

            GenTree NewExpression()
            {
                var operand = compiler.gtNewLclvNode(TYP_INT, 0);
                operand.SetCosts(3, 3);
                var expression = compiler.gtNewUnaryNode(GT_NEG, TYP_INT, operand);
                expression._vnPair.SetBoth(vn);
                expression.SetCosts(3, 3);
                return expression;
            }

            var first = NewExpression();
            var second = NewExpression();
            var firstStmt = compiler.gtNewStmt(first);
            var secondStmt = compiler.gtNewStmt(second);
            compiler.fgInsertStmtAtEnd(block, firstStmt);
            compiler.fgInsertStmtAtEnd(block, secondStmt);
            compiler.fgSetStmtSeq(firstStmt);
            compiler.fgSetStmtSeq(secondStmt);

            Assert.That(Locate(compiler, new CSE_Heuristic(compiler)), Is.True);
            var descriptor = Table(compiler)[0]!;
            Assert.Multiple(() =>
            {
                Assert.That(first._cseNum, Is.EqualTo(1));
                Assert.That(second._cseNum, Is.EqualTo(1));
                Assert.That(descriptor.csdTreeList.tslTree, Is.SameAs(first));
                Assert.That(descriptor.csdTreeLast.tslTree, Is.SameAs(second));
            });
        });
    }

    [Test]
    public static void AvailabilityLabelsFirstOccurrenceDefAndSecondUse()
    {
        WithCompiler(compiler =>
        {
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            compiler.compCurBB = block;
            compiler.vnStore = new ValueNumStore(compiler);
            var vn = compiler.vnStore.VNForIntCon(43);
            Initialize(compiler);

            var first = compiler.gtNewIconNode(TYP_INT, 43);
            var second = compiler.gtNewIconNode(TYP_INT, 43);
            first._vnPair.SetBoth(vn);
            second._vnPair.SetBoth(vn);
            var firstStmt = compiler.gtNewStmt(first);
            var secondStmt = compiler.gtNewStmt(second);
            compiler.fgInsertStmtAtEnd(block, firstStmt);
            compiler.fgInsertStmtAtEnd(block, secondStmt);
            compiler.fgSetStmtSeq(firstStmt);
            compiler.fgSetStmtSeq(secondStmt);

            Assert.That(Index(compiler, first, firstStmt), Is.Zero);
            Assert.That(Index(compiler, second, secondStmt), Is.EqualTo(1));
            Stop(compiler);
            InitializeDataFlow(compiler);
            RunDataFlow(compiler);
            Classify(compiler);

            var descriptor = Table(compiler)[0]!;
            Assert.Multiple(() =>
            {
                Assert.That(first._cseNum, Is.EqualTo(-1));
                Assert.That(second._cseNum, Is.EqualTo(1));
                Assert.That(descriptor.csdDefCount, Is.EqualTo(1));
                Assert.That(descriptor.csdUseCount, Is.EqualTo(1));
                Assert.That(descriptor.IsViable(), Is.True);
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DataFlowPreservesAvailabilityButKillsCrossCallBit(bool prohibitCseIn)
    {
        WithCompiler(compiler =>
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var callBlock = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = callBlock;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = callBlock;
            entry.bbRefs = 1;
            callBlock.bbRefs = 0;
            compiler.fgPredsComputed = true;
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(callBlock, entry));
            callBlock.SetFlags(BBF_HAS_CALL);
            if (prohibitCseIn)
            {
                callBlock.SetFlags(BBF_NO_CSE_IN);
            }
            compiler.compCurBB = entry;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var vn = store.VNForIntCon(13);
            Initialize(compiler);
            var first = compiler.gtNewIconNode(TYP_INT, 13);
            var second = compiler.gtNewIconNode(TYP_INT, 13);
            first._vnPair.SetBoth(vn);
            second._vnPair.SetBoth(vn);
            Assert.That(Index(compiler, first, compiler.gtNewStmt(first)), Is.Zero);
            Assert.That(Index(compiler, second, compiler.gtNewStmt(second)), Is.EqualTo(1));
            Stop(compiler);

            InitializeDataFlow(compiler);
            RunDataFlow(compiler);

            var traits = new BitVecTraits(compiler, 3);
            Assert.Multiple(() =>
            {
                Assert.That(BitVecOps.IsMember(traits, entry.bbCseOut!, 0), Is.True);
                Assert.That(BitVecOps.IsMember(traits, entry.bbCseOut!, 1), Is.True);
                Assert.That(BitVecOps.IsMember(traits, callBlock.bbCseIn!, 0), Is.EqualTo(!prohibitCseIn));
                Assert.That(BitVecOps.IsMember(traits, callBlock.bbCseOut!, 0), Is.EqualTo(!prohibitCseIn));
                Assert.That(BitVecOps.IsMember(traits, callBlock.bbCseOut!, 1), Is.False);
            });
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
