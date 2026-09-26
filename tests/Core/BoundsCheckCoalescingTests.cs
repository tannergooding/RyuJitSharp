// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.var_types;
using ValueNum = System.Int32;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BoundsCheckCoalescingTests
{
    private static PhaseStatus Coalesce(Compiler compiler)
        => compiler.optBoundsCheckCoalesce();

    [Test]
    public static void MissingChecksOrSsaLeavesGraphUntouched()
    {
        WithCompiler((compiler, block, lengthVN) => {
            var check = AddCheck(compiler, block, 1, lengthVN);
            var later = AddCheck(compiler, block, 3, lengthVN);
            compiler.MethodHasBoundsChecks = false;
#if DEBUG
            compiler.verbose = true;
            var output = CodeGenLifeTransitionTests.Capture(() =>
                Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING)));
            Assert.That(output, Does.Contain("Method has no bounds checks"));
#else
            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
#endif
            Assert.That(IndexValue(check), Is.EqualTo(1));

            compiler.MethodHasBoundsChecks = true;
            compiler.fgSsaPassesCompleted = 0;
            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(IndexValue(check), Is.EqualTo(1));
            Assert.That(IndexValue(later), Is.EqualTo(3));
        });
    }

    [Test]
    public static void LargestFollowingIndexStrengthensFirstCheckAndUpdatesItsValueNumber()
    {
        WithCompiler((compiler, block, lengthVN) => {
            var first = AddCheck(compiler, block, 0, lengthVN);
            var second = AddCheck(compiler, block, 3, lengthVN);
            var third = AddCheck(compiler, block, 2, lengthVN);
#if DEBUG
            compiler.verbose = true;
            var status = PhaseStatus.MODIFIED_NOTHING;
            var output = CodeGenLifeTransitionTests.Capture(() => status = Coalesce(compiler));
            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING), output);
            Assert.That(output, Does.Contain(
                $"BC coalesce in BB01: strengthen [{first.TreeId:D6}] offset 0 -> 3 (lenVN ${lengthVN:x})"));
#else
            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
#endif
            Assert.That(IndexValue(first), Is.EqualTo(3));
            Assert.That(first.Index._vnPair.Liberal, Is.EqualTo(Store(compiler).VNForIntCon(3)));
            Assert.That(first.Index._vnPair.Conservative, Is.EqualTo(Store(compiler).VNForIntCon(3)));
            Assert.That(IndexValue(second), Is.EqualTo(3));
            Assert.That(IndexValue(third), Is.EqualTo(2));
            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }

    [Test]
    public static void EachLengthAndBlockHasIndependentFirstCheck()
    {
        WithCompiler((compiler, block, lengthVN) => {
            var otherVN = Store(compiler).VNForExpr(null, TYP_INT);
            var secondBlock = BasicBlock.New(compiler, BBJ_RETURN);
            block.Next = secondBlock;
            secondBlock.Prev = block;
            compiler.fgLastBB = secondBlock;

            var first = AddCheck(compiler, block, 0, lengthVN);
            var other = AddCheck(compiler, block, 1, otherVN);
            _ = AddCheck(compiler, block, 3, lengthVN);
            _ = AddCheck(compiler, block, 4, otherVN);
            var nextBlockFirst = AddCheck(compiler, secondBlock, 2, lengthVN);
            _ = AddCheck(compiler, secondBlock, 5, lengthVN);

            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(IndexValue(first), Is.EqualTo(3));
            Assert.That(IndexValue(other), Is.EqualTo(4));
            Assert.That(IndexValue(nextBlockFirst), Is.EqualTo(5));
        });
    }

    [Test]
    public static void EqualNormalLengthVNsWithDifferentExceptionsShareGroup()
    {
        WithCompiler((compiler, block, lengthVN) => {
            var store = Store(compiler);
            var first = AddCheck(compiler, block, 0, lengthVN);
            var second = AddCheck(compiler, block, 3, lengthVN);
            var differentLengthVN = store.VNForExpr(null, TYP_INT);
            var third = AddCheck(compiler, block, 7, differentLengthVN);
            var nullException = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_NullPtrExc,
                ValueNumStore.VNForNull()));
            var overflowException = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNF_OverflowExc,
                ValueNumStore.VNForVoid()));
            first.ArrayLength._vnPair.SetBoth(store.VNWithExc(lengthVN, nullException));
            second.ArrayLength._vnPair.SetBoth(store.VNWithExc(lengthVN, overflowException));
            third.ArrayLength._vnPair.SetBoth(store.VNWithExc(differentLengthVN, overflowException));

            Assert.That(first.ArrayLength._vnPair.Conservative,
                Is.Not.EqualTo(second.ArrayLength._vnPair.Conservative));
            Assert.That(store.VNNormalValue(first.ArrayLength._vnPair.Conservative),
                Is.EqualTo(store.VNNormalValue(second.ArrayLength._vnPair.Conservative)));
            Assert.That(store.VNNormalValue(third.ArrayLength._vnPair.Conservative),
                Is.Not.EqualTo(lengthVN));
            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(IndexValue(first), Is.EqualTo(3));
            Assert.That(IndexValue(second), Is.EqualTo(3));
            Assert.That(IndexValue(third), Is.EqualTo(7));
        });
    }

    [TestCase(SCK_ARG_EXCPN)]
    [TestCase(SCK_ARG_RNG_EXCPN)]
    public static void DifferentThrowKindSeparatesGroups(SpecialCodeKind throwKind)
    {
        WithCompiler((compiler, block, lengthVN) => {
            var first = AddCheck(compiler, block, 0, lengthVN);
            var intervening = AddCheck(compiler, block, 2, lengthVN, throwKind);
            var later = AddCheck(compiler, block, 3, lengthVN);

            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(IndexValue(first), Is.EqualTo(0));
            Assert.That(IndexValue(intervening), Is.EqualTo(2));
            Assert.That(IndexValue(later), Is.EqualTo(3));
        });
    }

    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public static void NegativeIndicesAreNotCoalesced(int index)
    {
        WithCompiler((compiler, block, lengthVN) => {
            var first = AddCheck(compiler, block, index, lengthVN);
            var later = AddCheck(compiler, block, 2, lengthVN);

            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(IndexValue(first), Is.EqualTo(index));
            Assert.That(IndexValue(later), Is.EqualTo(2));
        });
    }

    [Test]
    public static void UnnumberedLengthAndWideIndexCannotJoinGroup()
    {
        WithCompiler((compiler, block, lengthVN) => {
            var first = AddCheck(compiler, block, 0, ValueNumStore.NoVN);
            var wideValue = unchecked((nint)((long)int.MaxValue + 1));
            var wideIndex = compiler.gtNewIconNode(TYP_LONG, wideValue);
            wideIndex._vnPair.SetBoth(Store(compiler).VNForLongCon((long)int.MaxValue + 1));
            var wideLength = compiler.gtNewLclvNode(TYP_INT, 0);
            wideLength._vnPair.SetBoth(lengthVN);
            var wide = new GenTreeBoundsChk(wideIndex, wideLength, SCK_RNGCHK_FAIL);
            AddStatement(compiler, block, wide);
            var later = AddCheck(compiler, block, 4, lengthVN);

            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(IndexValue(first), Is.EqualTo(0));
            Assert.That(wide.Index.AsIntCon().IconValue, Is.EqualTo(wideValue));
            Assert.That(IndexValue(later), Is.EqualTo(4));
        });
    }

    [Test]
    public static void CommaWrappedLengthUsesTheEffectiveValueNumber()
    {
        WithCompiler((compiler, block, lengthVN) => {
            var first = AddCheck(compiler, block, 0, lengthVN);
            var index = compiler.gtNewIconNode(TYP_INT, int.MaxValue);
            index._vnPair.SetBoth(Store(compiler).VNForIntCon(int.MaxValue));
            var length = compiler.gtNewLclvNode(TYP_INT, 0);
            length._vnPair.SetBoth(lengthVN);
            var comma = compiler.gtNewCommaNode(TYP_INT, compiler.gtNewIconNode(TYP_INT, 7), length);
            var later = new GenTreeBoundsChk(index, comma, SCK_RNGCHK_FAIL);
            AddStatement(compiler, block, later);

            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(IndexValue(first), Is.EqualTo(int.MaxValue));
            Assert.That(first.Index._vnPair.Liberal,
                Is.EqualTo(Store(compiler).VNForIntCon(int.MaxValue)));
            Assert.That(IndexValue(later), Is.EqualTo(int.MaxValue));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CallsAndNonRangeExceptionsSeparateCandidateGroups(bool call)
    {
        WithCompiler((compiler, block, lengthVN) => {
            var first = AddCheck(compiler, block, 0, lengthVN);
            GenTree barrier = call
                ? compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null)
                : compiler.gtNewNullCheck(compiler.gtNewIconNode(TYP_REF, 0));
            AddStatement(compiler, block, barrier);
            var later = AddCheck(compiler, block, 4, lengthVN);

            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(IndexValue(first), Is.Zero);
            Assert.That(IndexValue(later), Is.EqualTo(4));
        });
    }

    [Test]
    public static void OrderingEffectSeparatesCandidateGroups()
    {
        WithCompiler((compiler, block, lengthVN) => {
            var first = AddCheck(compiler, block, 0, lengthVN);
            var barrier = compiler.gtNewLclvNode(TYP_BYREF, 0);
            barrier.HasOrderingSideEffect = true;
            AddStatement(compiler, block, barrier);
            var later = AddCheck(compiler, block, 4, lengthVN);

            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(IndexValue(first), Is.Zero);
            Assert.That(IndexValue(later), Is.EqualTo(4));
        });
    }

    [Test]
    public static void IndirectStoreSeparatesCandidateGroupsWithoutEHSuccs()
    {
        WithCompiler((compiler, block, lengthVN) => {
            var first = AddCheck(compiler, block, 0, lengthVN);
            var address = compiler.gtNewLclVarAddrNode(TYP_BYREF, 0);
            var store = compiler.gtNewStoreIndNode(TYP_INT, address, compiler.gtNewIconNode(TYP_INT, 1));
            AddStatement(compiler, block, store);
            var later = AddCheck(compiler, block, 4, lengthVN);

            Assert.That(Coalesce(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(IndexValue(first), Is.Zero);
            Assert.That(IndexValue(later), Is.EqualTo(4));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public static void LocalStoreOnlyBlocksAcrossEHSuccsWhenUntrackedOrHandlerLive(
        bool ehSuccs, bool tracked, bool liveInOutOfHandler)
    {
        WithCompiler((compiler, block, lengthVN) => {
            if (ehSuccs)
            {
                block.bbTryIndex = 1;
            }
            compiler.lvaTable[0].lvTracked = tracked;
            compiler.lvaTable[0]._lvLiveInOutOfHandler = liveInOutOfHandler;
            if (tracked)
            {
                Assert.That(compiler.lvaTable[0].IsLiveInOutOfHandler, Is.EqualTo(liveInOutOfHandler));
            }
            var first = AddCheck(compiler, block, 0, lengthVN);
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1));
            AddStatement(compiler, block, store);
            var later = AddCheck(compiler, block, 4, lengthVN);

            var shouldCoalesce = !ehSuccs || (tracked && !liveInOutOfHandler);
            Assert.That(Coalesce(compiler), Is.EqualTo(shouldCoalesce
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(IndexValue(first), Is.EqualTo(shouldCoalesce ? 4 : 0));
            Assert.That(IndexValue(later), Is.EqualTo(4));
        });
    }

    private static GenTreeBoundsChk AddCheck(Compiler compiler, BasicBlock block, int index,
        ValueNum lengthVN, SpecialCodeKind kind = SCK_RNGCHK_FAIL)
    {
        var indexNode = compiler.gtNewIconNode(TYP_INT, index);
        indexNode._vnPair.SetBoth(Store(compiler).VNForIntCon(index));
        var lengthNode = compiler.gtNewLclvNode(TYP_INT, 0);
        lengthNode._vnPair.SetBoth(lengthVN);
        var check = new GenTreeBoundsChk(indexNode, lengthNode, kind);
        AddStatement(compiler, block, check);
        return check;
    }

    private static int IndexValue(GenTreeBoundsChk check)
        => checked((int)check.Index.AsIntCon().IconValue);

    private static void AddStatement(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = compiler.gtNewStmt(root);
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);
        compiler.fgInsertStmtAtEnd(block, statement);
    }

    private static ValueNumStore Store(Compiler compiler)
        => compiler.vnStore ?? throw new InvalidOperationException("The test requires a VN store.");

    private static void WithCompiler(Action<Compiler, BasicBlock, ValueNum> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compHndBBtab = [];
        compiler.info = new Compiler.Info();
#if DEBUG
        compiler.info.compFullName = nameof(BoundsCheckCoalescingTests);
        compiler.fgSafeBasicBlockCreation = true;
#endif
        var store = new ValueNumStore(compiler);
        compiler.vnStore = store;
        compiler.fgSsaPassesCompleted = 1;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.MethodHasBoundsChecks = true;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_INT;
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        JitTls.Compiler = compiler;
        try
        {
            action(compiler, block, store.VNForExpr(null, TYP_INT));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
