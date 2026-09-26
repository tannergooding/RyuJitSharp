// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HeadTailMergeTests
{
    private static readonly MethodInfo s_canMove = typeof(Compiler).GetMethod("fgCanMoveFirstStatementIntoPredCore",
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new AssertionException("Missing head merge predicate.");

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void CommonSuccessorAbsorbsMatchingLastStores(bool early, bool protectPredecessors)
    {
        WithCompiler(compiler => {
            var entry = NewBlock(compiler, BBJ_COND);
            var left = NewBlock(compiler, BBJ_ALWAYS);
            var right = NewBlock(compiler, BBJ_ALWAYS);
            var join = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, entry, left, right, join);
            AddConditional(compiler, entry, left, right);
            Jump(compiler, left, join);
            Jump(compiler, right, join);
            var leftStmt = AddStore(compiler, left, 1, 17);
            var rightStmt = AddStore(compiler, right, 1, 17);
            _ = AddReturn(compiler, join, 0);
            if (protectPredecessors)
            {
                left.SetFlags(BBF_DONT_REMOVE);
                right.SetFlags(BBF_DONT_REMOVE);
            }

            Assert.That(GenTree.Compare(leftStmt.RootNode, rightStmt.RootNode), Is.True);
            Assert.That(join.CountOfInEdges, Is.EqualTo(2));
            Assert.That(Merge(compiler, early), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(join.FirstStmt, Is.SameAs(leftStmt));
            Assert.That(left.LastStmt, Is.Null);
            Assert.That(right.LastStmt, Is.Null);
            Assert.That(left.HasFlag(BBF_REMOVED), Is.EqualTo(!protectPredecessors));
            Assert.That(right.HasFlag(BBF_REMOVED), Is.EqualTo(!protectPredecessors));
            if (protectPredecessors)
            {
                Assert.That(entry.TrueTarget, Is.SameAs(right));
                Assert.That(entry.FalseTarget, Is.SameAs(left));
                Assert.That(join.CountOfInEdges, Is.EqualTo(2));
            }
            else
            {
                Assert.That(entry.Kind, Is.EqualTo(BBJ_ALWAYS));
                Assert.That(entry.Target, Is.SameAs(join));
                Assert.That(join.CountOfInEdges, Is.EqualTo(1));
            }
            Assert.That(rightStmt, Is.Not.SameAs(join.FirstStmt));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TerminalReturnsCrossJumpWithoutRemovingEntry(bool early)
    {
        WithCompiler(compiler => {
            var entry = NewBlock(compiler, BBJ_RETURN);
            var second = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, entry, second);
            entry.bbRefs = 1;
            var original = AddReturn(compiler, entry, 42);
            var retained = AddReturn(compiler, second, 42);

            Assert.That(GenTree.Compare(original.RootNode, retained.RootNode), Is.True);
            Assert.That(Merge(compiler, early), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(entry.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(entry.Target, Is.SameAs(second));
            Assert.That(entry.LastStmt, Is.Null);
            Assert.That(second.LastStmt, Is.SameAs(retained));
            Assert.That(second.CountOfInEdges, Is.EqualTo(1));
            Assert.That(original, Is.Not.SameAs(second.LastStmt));
        });
    }

    [Test]
    public static void SubsetCrossJumpSplitsTheFallThroughPredecessor()
    {
        WithCompiler(compiler => {
            var entry = NewBlock(compiler, BBJ_COND);
            var first = NewBlock(compiler, BBJ_ALWAYS);
            var second = NewBlock(compiler, BBJ_ALWAYS);
            var other = NewBlock(compiler, BBJ_ALWAYS);
            var join = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, entry, first, other, second, join);
            AddConditional(compiler, entry, first, second);
            Jump(compiler, first, join);
            Jump(compiler, second, join);
            Jump(compiler, other, join);
            var firstPrefix = AddStore(compiler, first, 0, 11);
            var firstCommon = AddStore(compiler, first, 1, 17);
            var secondPrefix = AddStore(compiler, second, 0, 12);
            _ = AddStore(compiler, second, 1, 17);
            var otherStmt = AddStore(compiler, other, 1, 18);
            _ = AddReturn(compiler, join, 0);

            Assert.That(Merge(compiler, early: false), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var split = second.Next!;
            Assert.That(second.FirstStmt, Is.SameAs(secondPrefix));
            Assert.That(second.Target, Is.SameAs(split));
            Assert.That(first.FirstStmt, Is.SameAs(firstPrefix));
            Assert.That(first.LastStmt, Is.SameAs(firstPrefix));
            Assert.That(first.Target, Is.SameAs(split));
            Assert.That(split.FirstStmt?.RootNode, Is.Not.SameAs(firstCommon.RootNode));
            Assert.That(GenTree.Compare(split.FirstStmt!.RootNode, firstCommon.RootNode), Is.True);
            Assert.That(split.Target, Is.SameAs(join));
            Assert.That(other.LastStmt, Is.SameAs(otherStmt));
            Assert.That(other.Target, Is.SameAs(join));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MatchingHeadsMoveAheadOfBranch(bool early)
    {
        WithCompiler(compiler => {
            var branch = NewBlock(compiler, BBJ_COND);
            var left = NewBlock(compiler, BBJ_RETURN);
            var right = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, branch, left, right);
            AddConditional(compiler, branch, left, right);
            var leftStore = AddStore(compiler, left, 1, 7);
            _ = AddReturn(compiler, left, 1);
            var rightStore = AddStore(compiler, right, 1, 7);
            _ = AddReturn(compiler, right, 2);

            Assert.That(GenTree.Compare(leftStore.RootNode, rightStore.RootNode), Is.True);
            Assert.That(Merge(compiler, early), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(branch.FirstStmt, Is.SameAs(leftStore));
            Assert.That(branch.LastStmt?.RootNode.Oper, Is.EqualTo(GT_JTRUE));
            Assert.That(left.FirstStmt?.RootNode.Oper, Is.EqualTo(GT_RETURN));
            Assert.That(right.FirstStmt?.RootNode.Oper, Is.EqualTo(GT_RETURN));
            Assert.That(rightStore, Is.Not.SameAs(branch.FirstStmt));
        });
    }

    [TestCase("different-tree")]
    [TestCase("critical-edge")]
    [TestCase("different-eh")]
    public static void RejectsIneligibleHeads(string reason)
    {
        WithCompiler(compiler => {
            var branch = NewBlock(compiler, BBJ_COND);
            var left = NewBlock(compiler, BBJ_RETURN);
            var right = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, branch, left, right);
            AddConditional(compiler, branch, left, right);
            var first = AddStore(compiler, left, 1, 7);
            _ = AddReturn(compiler, left, 1);
            var second = AddStore(compiler, right, 1, reason == "different-tree" ? 8 : 7);
            _ = AddReturn(compiler, right, 2);

            if (reason == "critical-edge")
            {
                var extra = NewBlock(compiler, BBJ_ALWAYS);
                right.Next = extra;
                compiler.fgLastBB = extra;
                Jump(compiler, extra, left);
            }
            else if (reason == "different-eh")
            {
                right.TryIndex = 0;
            }

            Assert.That(Merge(compiler, early: false), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(branch.FirstStmt?.RootNode.Oper, Is.EqualTo(GT_JTRUE));
            Assert.That(left.FirstStmt, Is.SameAs(first));
            Assert.That(right.FirstStmt, Is.SameAs(second));
        });
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, true, true)]
    public static void ExposedStoreReorderingUsesEarlyOrLateAddressState(
        bool early, bool earlyAddressTaken, bool lateAddressExposed)
    {
        WithCompiler(compiler => {
            var branch = NewBlock(compiler, BBJ_COND);
            var storeBlock = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, branch, storeBlock);
            AddConditional(compiler, branch, storeBlock, storeBlock);
            var statement = AddStore(compiler, storeBlock, 1, 7);
            compiler.lvaTable[1].lvHasLdAddrOp = earlyAddressTaken;
            if (lateAddressExposed)
            {
                compiler.lvaTable[1].SetAddressExposed(true, AddressExposedReason.ESCAPE_ADDRESS);
            }

            branch.LastStmt!.RootNode.Flags |= GTF_EXCEPT;
            var expected = !(early ? earlyAddressTaken : lateAddressExposed);
            Assert.That(s_canMove.Invoke(compiler, [early, statement, branch]), Is.EqualTo(expected));
        });
    }

    [TestCase(GTF_ASG, GTF_EMPTY)]
    [TestCase(GTF_EMPTY, GTF_ASG)]
    [TestCase(GTF_CALL, GTF_EXCEPT)]
    [TestCase(GTF_GLOB_REF, GTF_ORDER_SIDEEFF)]
    [TestCase(GTF_ORDER_SIDEEFF, GTF_GLOB_REF)]
    [TestCase(GTF_EXCEPT, GTF_SIDE_EFFECT)]
    public static void TerminatorReorderingRejectsNativeEffectConflicts(
        GenTreeFlags terminatorFlags, GenTreeFlags statementFlags)
    {
        WithCompiler(compiler => {
            var branch = NewBlock(compiler, BBJ_COND);
            var successor = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, branch, successor);
            AddConditional(compiler, branch, successor, successor);
            var stmt = compiler.gtNewStmt(new GenTree(GT_NOP, TYP_VOID));
            branch.LastStmt!.RootNode.Flags |= terminatorFlags;
            stmt.RootNode.Flags |= statementFlags;

            Assert.That(s_canMove.Invoke(compiler, [false, stmt, branch]), Is.EqualTo(false));
        });
    }

    [Test]
    public static void ConfigurationGateLeavesMatchingReturnsUntouched()
    {
        WithCompiler(compiler => {
            Globals.JitConfig = new JitConfigValues();
            var entry = NewBlock(compiler, BBJ_RETURN);
            var other = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, entry, other);
            var first = AddReturn(compiler, entry, 42);
            var second = AddReturn(compiler, other, 42);

            Assert.That(Merge(compiler, early: false), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(entry.LastStmt, Is.SameAs(first));
            Assert.That(other.LastStmt, Is.SameAs(second));
        });
    }

    private static PhaseStatus Merge(Compiler compiler, bool early)
        => compiler.fgHeadTailMerge(early);

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;
        return block;
    }

    private static void Link(Compiler compiler, params BasicBlock[] blocks)
    {
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        for (var i = 1; i < blocks.Length; i++)
        {
            blocks[i - 1].Next = blocks[i];
        }
    }

    private static void Jump(Compiler compiler, BasicBlock from, BasicBlock to)
        => from.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(to, from));

    private static void AddConditional(Compiler compiler, BasicBlock from, BasicBlock whenFalse, BasicBlock whenTrue)
    {
        from.SetCond(compiler.fgAddRefPred(whenTrue, from), compiler.fgAddRefPred(whenFalse, from));
        var comparison = compiler.gtNewBinaryNode(GT_EQ, TYP_INT,
            compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 0));
        var stmt = compiler.gtNewStmt(new GenTreeUnOp(GT_JTRUE, TYP_VOID, comparison));
        compiler.gtSetStmtInfo(stmt);
        compiler.fgInsertStmtAtEnd(from, stmt);
    }

    private static Statement AddStore(Compiler compiler, BasicBlock block, int local, int value)
    {
        var stmt = compiler.gtNewStmt(compiler.gtNewStoreLclVarNode(local, compiler.gtNewIconNode(TYP_INT, value)));
        compiler.gtSetStmtInfo(stmt);
        compiler.fgInsertStmtAtEnd(block, stmt);
        return stmt;
    }

    private static Statement AddReturn(Compiler compiler, BasicBlock block, int value)
    {
        var stmt = compiler.gtNewStmt(compiler.gtNewUnaryNode(GT_RETURN, TYP_INT,
            compiler.gtNewIconNode(TYP_INT, value)));
        compiler.gtSetStmtInfo(stmt);
        compiler.fgInsertStmtAtEnd(block, stmt);
        return stmt;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = Globals.JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        compiler.info = new Compiler.Info();
#if DEBUG
        compiler.info.compFullName = nameof(HeadTailMergeTests);
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 2;
        object config = new JitConfigValues();
        var enable = typeof(JitConfigValues).GetField("_jitEnableHeadTailMerge",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertionException("Missing head/tail merge configuration.");
        enable.SetValue(config, 1);
        Globals.JitConfig = (JitConfigValues)config;
        Assert.That(Globals.JitConfig.JitEnableHeadTailMerge, Is.EqualTo(1));
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            Globals.JitConfig = previousConfig;
        }
    }
}
