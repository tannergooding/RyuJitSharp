// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class RangeEliminationTests
{
    [TestCase(false, 0, 0, false)]
    [TestCase(true, -1, 0, true)]
    [TestCase(true, 0, 0, false)]
    [TestCase(true, -1, -1, false)]
    [TestCase(true, -1, 1, true)]
    public static void SymbolicUpperBoundRequiresMatchingCheckedLengthAndNegativeOffset(
        bool checkedBound, int offset, int lower, bool expected)
    {
        WithCompiler((compiler, store) =>
        {
            var lengthVN = store.VNForExpr(null, TYP_INT);
            if (checkedBound)
            {
                store.SetVNIsCheckedBound(lengthVN);
            }

            var length = compiler.gtNewLclvNode(TYP_INT, 0);
            length._vnPair.SetBoth(lengthVN);
            var range = new Range(new(LimitType.Constant, lower),
                new(LimitType.BinOpArray, lengthVN, offset));
            Assert.That(compiler.GetRangeCheck().BetweenBounds(range, length, 0),
                Is.EqualTo(expected));
        });
    }

    [TestCase(-3, -2, 10, true)]
    [TestCase(-1, -1, 10, true)]
    [TestCase(-11, -1, 10, false)]
    [TestCase(int.MinValue, -1, 10, false)]
    [TestCase(-3, -2, 0, false)]
    public static void SymbolicLowerBoundUsesNonoverflowingArraySizeComparison(
        int lowerOffset, int upperOffset, int arraySize, bool expected)
    {
        WithCompiler((compiler, store) =>
        {
            var lengthVN = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(lengthVN);
            var length = compiler.gtNewLclvNode(TYP_INT, 0);
            length._vnPair.SetBoth(lengthVN);
            var range = new Range(new(LimitType.BinOpArray, lengthVN, lowerOffset),
                new(LimitType.BinOpArray, lengthVN, upperOffset));
            Assert.That(compiler.GetRangeCheck().BetweenBounds(range, length, arraySize),
                Is.EqualTo(expected));
        });
    }

    [TestCase(0, 9, 10, true)]
    [TestCase(-1, 9, 10, false)]
    [TestCase(0, 10, 10, false)]
    [TestCase(0, 9, 0, false)]
    public static void ConstantBoundsMustFitKnownArraySize(int lower, int upper, int arraySize,
        bool expected)
    {
        WithCompiler((compiler, store) =>
        {
            var length = compiler.gtNewLclvNode(TYP_INT, 0);
            length._vnPair.SetBoth(store.VNForExpr(null, TYP_INT));
            var range = new Range(new(LimitType.Constant, lower), new(LimitType.Constant, upper));
            Assert.That(compiler.GetRangeCheck().BetweenBounds(range, length, arraySize),
                Is.EqualTo(expected));
        });
    }

    [TestCase(-2, 9, true)]
    [TestCase(-1, 8, false)]
    [TestCase(-10, 0, true)]
    [TestCase(-11, 0, false)]
    public static void SymbolicLowerAndConstantUpperRequireKnownArraySize(
        int lowerOffset, int upper, bool expected)
    {
        WithCompiler((compiler, store) =>
        {
            var lengthVN = store.VNForExpr(null, TYP_INT);
            var length = compiler.gtNewLclvNode(TYP_INT, 0);
            length._vnPair.SetBoth(lengthVN);
            var range = new Range(new(LimitType.BinOpArray, lengthVN, lowerOffset),
                new(LimitType.Constant, upper));
            Assert.That(compiler.GetRangeCheck().BetweenBounds(range, length, 10),
                Is.EqualTo(expected));
        });
    }

    [Test]
    public static void SymbolicBoundsRequireTheActualArrayLengthVN()
    {
        WithCompiler((compiler, store) =>
        {
            var lengthVN = store.VNForExpr(null, TYP_INT);
            var differentVN = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(lengthVN);
            var length = compiler.gtNewLclvNode(TYP_INT, 0);
            length._vnPair.SetBoth(lengthVN);
            var range = new Range(new(LimitType.Constant, 0),
                new(LimitType.BinOpArray, differentVN, -1));
            Assert.That(compiler.GetRangeCheck().BetweenBounds(range, length, 10), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemovingRootCheckRepairsExceptionFlagsAcrossThreadingModes(bool allTrees)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.fgNodeThreading = allTrees ? NodeThreading.AllTrees : NodeThreading.AllLocals;
            var index = compiler.gtNewIconNode(TYP_INT, 0);
            index._vnPair.SetBoth(store.VNForIntCon(0));
            var length = compiler.gtNewIconNode(TYP_INT, 10);
            length._vnPair.SetBoth(store.VNForIntCon(10));
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);
            if (allTrees)
            {
                compiler.gtSetStmtInfo(statement);
                compiler.fgSetStmtSeq(statement);
            }

            Assert.That(compiler.optRemoveRangeCheck(check, null, statement), Is.SameAs(check));
            Assert.That(check.Oper, Is.EqualTo(GT_NOP));
            Assert.That(statement.RootNode, Is.SameAs(check));
            Assert.That((check.Flags & GTF_EXCEPT) == 0, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RemovingRootCheckRetainsIndexStoreAcrossThreadingModes(bool allTrees)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.fgNodeThreading = allTrees ? NodeThreading.AllTrees : NodeThreading.AllLocals;
            var indexStore = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 3));
            var index = compiler.gtNewCommaNode(TYP_INT, indexStore, compiler.gtNewIconNode(TYP_INT, 0));
            index._vnPair.SetBoth(store.VNForIntCon(0));
            var length = compiler.gtNewIconNode(TYP_INT, 10);
            length._vnPair.SetBoth(store.VNForIntCon(10));
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);
            if (allTrees)
            {
                compiler.gtSetStmtInfo(statement);
                compiler.fgSetStmtSeq(statement);
            }

            _ = compiler.optRemoveRangeCheck(check, null, statement);
            Assert.That(statement.RootNode, Is.SameAs(indexStore));
            Assert.That((statement.RootNode.Flags & GTF_ASG) != 0, Is.True);
            Assert.That((statement.RootNode.Flags & GTF_EXCEPT) == 0, Is.True);
        });
    }

    [Test]
    public static void RemovingCommaCheckPreservesOrderedIndexAndLengthStoresAndRepairsAncestors()
    {
        WithCompiler((compiler, store) =>
        {
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            var indexStore = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 3));
            var lengthStore = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 10));
            var index = compiler.gtNewCommaNode(TYP_INT, indexStore, compiler.gtNewIconNode(TYP_INT, 0));
            index._vnPair.SetBoth(store.VNForIntCon(0));
            var length = compiler.gtNewCommaNode(TYP_INT, lengthStore, compiler.gtNewIconNode(TYP_INT, 10));
            length._vnPair.SetBoth(store.VNForIntCon(10));
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var comma = compiler.gtNewCommaNode(TYP_INT, check, compiler.gtNewIconNode(TYP_INT, 42));
            var parent = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, comma,
                compiler.gtNewIconNode(TYP_INT, 1));
            var statement = compiler.gtNewStmt(parent);
            compiler.gtSetStmtInfo(statement);
            compiler.fgSetStmtSeq(statement);

            _ = compiler.optRemoveRangeCheck(check, comma, statement);
            Assert.That(comma.Oper, Is.EqualTo(GT_COMMA));
            Assert.That((comma.Flags & GTF_DONT_CSE) != 0, Is.True);
            Assert.That((parent.Flags & GTF_EXCEPT) == 0, Is.True);
            Assert.That((parent.Flags & GTF_ASG) != 0, Is.True);
            Assert.That(statement.RootNode, Is.SameAs(parent));
            Assert.That(comma.AsOp().Op1.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(comma.AsOp().Op1.AsOp().Op1, Is.SameAs(indexStore));
            Assert.That(comma.AsOp().Op1.AsOp().Op2, Is.SameAs(lengthStore));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EliminationDriverMarksBoundsBlockAndRepairsStatementOnlyOnChange(bool redundant)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            var block = new BasicBlock(null, null) { bbNum = 1 };
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var index = compiler.gtNewIconNode(TYP_INT, redundant ? 0 : 10);
            index._vnPair.SetBoth(store.VNForIntCon(redundant ? 0 : 10));
            var length = compiler.gtNewIconNode(TYP_INT, 10);
            length._vnPair.SetBoth(store.VNForIntCon(10));
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);
            compiler.fgInsertStmtAtEnd(block, statement);
            compiler.gtSetStmtInfo(statement);
            compiler.fgSetStmtSeq(statement);

            var rangeCheck = compiler.GetRangeCheck();
            rangeCheck.SetBudget(0);
            Assert.That(rangeCheck.OptimizeRangeChecks(), Is.EqualTo(redundant));
            Assert.That(block.HasFlag(BBF_MAY_HAVE_BOUNDS_CHECKS), Is.True);
            Assert.That(statement.RootNode.Oper, Is.EqualTo(redundant ? GT_NOP : GT_BOUNDS_CHECK));
            Assert.That(statement.TreeListBegin, Is.SameAs(redundant ? statement.RootNode : index));
        });
    }

    [TestCase(false, 0, true)]
    [TestCase(true, 0, true)]
    [TestCase(false, 1, true)]
    [TestCase(true, 1, false)]
    [TestCase(true, 1, true)]
    public static void PhaseRequiresBoundsChecksAndSsaAndReportsActualChanges(
        bool hasBoundsChecks, int ssaPasses, bool redundant)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.MethodHasBoundsChecks = hasBoundsChecks;
            compiler.fgSsaPassesCompleted = ssaPasses;
            compiler.fgNodeThreading = NodeThreading.AllTrees;
            var block = new BasicBlock(null, null) { bbNum = 1 };
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var index = compiler.gtNewIconNode(TYP_INT, redundant ? 0 : 10);
            index._vnPair.SetBoth(store.VNForIntCon(redundant ? 0 : 10));
            var length = compiler.gtNewIconNode(TYP_INT, 10);
            length._vnPair.SetBoth(store.VNForIntCon(10));
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var statement = compiler.gtNewStmt(check);
            compiler.fgInsertStmtAtEnd(block, statement);
            compiler.gtSetStmtInfo(statement);
            compiler.fgSetStmtSeq(statement);

            var runs = hasBoundsChecks && (ssaPasses != 0);
            var changed = runs && redundant;
            Assert.That(compiler.rangeCheckPhase(), Is.EqualTo(changed
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.HasFlag(BBF_MAY_HAVE_BOUNDS_CHECKS), Is.EqualTo(runs));
            Assert.That(statement.RootNode.Oper, Is.EqualTo(changed ? GT_NOP : GT_BOUNDS_CHECK));
        });
    }

    private static void WithCompiler(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
        JitTls.Compiler = compiler;
        try
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
