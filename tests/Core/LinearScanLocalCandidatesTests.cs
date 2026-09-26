// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LinearScanLocalCandidatesTests
{
    [Test]
    public static void CandidateIntervalsExcludePinnedUntrackedAndUnusedLocals()
    {
        WithCandidates(4, (compiler, allocator) => {
            compiler.lvaTable[1].lvPinned = true;
            compiler.lvaTable[2].lvTracked = false;
            compiler.lvaTable[3].setLvRefCnt(0);
            Identify(allocator);

            var mappings = GetLocalIntervals(allocator);
            var interval = mappings[0] ?? throw new AssertionException("Candidate interval is missing.");
            Assert.That(interval.varNum, Is.Zero);
            Assert.That(interval.isLocalVar, Is.True);
            Assert.That(compiler.lvaTable[0].lvLRACandidate, Is.True);
            Assert.That(compiler.lvaTable[0].lvRegister, Is.False);
            Assert.That(compiler.lvaTable[0].lvMustInit, Is.False);
            Assert.That((int)SetOps.Count(compiler, Candidates(allocator)), Is.EqualTo(1));
            for (var index = 1; index < 4; index++)
            {
                Assert.That(mappings[index], Is.Null);
                Assert.That(compiler.lvaTable[index].lvLRACandidate, Is.False);
            }
            Assert.That(compiler.lvaTable[1].lvDoNotEnregister, Is.True);
            Assert.That(compiler.lvaTable[3].lvRefCntWtd(), Is.Zero);
            foreach (var local in compiler.lvaTable)
            {
                Assert.That(local.RegNum, Is.EqualTo(REG_STK));
            }
        });
    }

    [TestCase(1.999, false, false)]
    [TestCase(2.0, false, false)]
    [TestCase(3.999, false, false)]
    [TestCase(4.0, false, true)]
    [TestCase(4.0, true, false)]
    [TestCase(5.0, true, true)]
    public static void FloatingPreferenceUsesWeightedThresholdAndRegisterArgumentAdjustment(
        double weight, bool registerArgument, bool expected)
    {
        WithCandidates(1, (compiler, allocator) => {
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            compiler.lvaTable[0].lvIsParam = registerArgument;
            compiler.lvaTable[0].lvIsRegArg = registerArgument;
            compiler.lvaTable[0].setLvRefCntWtd(weight * BB_UNITY_WEIGHT);
            Identify(allocator);
            Assert.That(SetOps.IsMember(compiler, FloatingCandidates(allocator), 0), Is.EqualTo(expected));
            Assert.That(compiler.compFloatingPointUsed, Is.True);
        });
    }

    [TestCase(6, true, true, false)]
    [TestCase(7, false, true, false)]
    [TestCase(7, true, false, false)]
    [TestCase(7, true, true, true)]
    public static void AggressiveFloatingPreferenceRequiresManyLocalsLoopAndSingleExit(
        int count, bool hasLoops, bool singleExit, bool expected)
    {
        WithCandidates(count, (compiler, allocator) => {
            compiler.fgHasLoops = hasLoops;
            if (!singleExit)
            {
                compiler.fgReturnBlocks = new BasicBlockList(BasicBlock.New(compiler, BBJ_RETURN),
                    new BasicBlockList(BasicBlock.New(compiler, BBJ_RETURN)));
            }
            foreach (ref var local in compiler.lvaTable.AsSpan())
            {
                local.Type = TYP_DOUBLE;
                local.setLvRefCntWtd(2 * BB_UNITY_WEIGHT);
            }
            Identify(allocator);
            Assert.That((int)SetOps.Count(compiler, FloatingCandidates(allocator)),
                Is.EqualTo(expected ? count : 0));
        });
    }

    [Test]
    public static void HandlerCandidatesAreWriteThroughSpilledAndFilteredFromExceptionSet()
    {
        WithCandidates(2, (compiler, allocator) => {
            compiler.lvaEnregEHVars = true;
            compiler.compHndBBtabCount = 1;
            for (var index = 0; index < 2; index++)
            {
                compiler.lvaTable[index].Type = TYP_REF;
                compiler.lvaTable[index].lvSingleDefRegCandidate = true;
                LiveInOutOfHandler(ref compiler.lvaTable[index], true);
            }
            compiler.lvaTable[1].lvPinned = true;
            var block = BasicBlock.New(compiler, BBJ_EHFINALLYRET);
            block.CatchType = bbCatchType.BBCT_FINALLY;
            block.bbLiveIn = SetOps.MakeEmpty(compiler);
            block.bbLiveOut = SetOps.MakeEmpty(compiler);
            SetOps.AddElemD(compiler, block.bbLiveIn, 0);
            SetOps.AddElemD(compiler, block.bbLiveOut, 1);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;

            Identify(allocator);
            var interval = GetLocalIntervals(allocator)[0]
                ?? throw new AssertionException("Handler candidate interval is missing.");
            Assert.That(interval.isWriteThru, Is.True);
            Assert.That(interval.isSpilled, Is.True);
            Assert.That(SetOps.IsMember(compiler, Spilled(allocator), 0), Is.True);
            Assert.That(SetOps.IsMember(compiler, Except(allocator), 0), Is.True);
            Assert.That(SetOps.IsMember(compiler, Except(allocator), 1), Is.False);
            Assert.That(SetOps.IsMember(compiler, Finally(allocator), 1), Is.True);
        });
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [TestCase(TYP_SIMD32, 3.999, false)]
    [TestCase(TYP_SIMD32, 4.0, true)]
    [TestCase(TYP_SIMD64, 4.0, true)]
    public static void LargeVectorCandidatesCreateRelatedUpperIntervals(var_types type, double weight, bool preferred)
    {
        WithCandidates(1, (compiler, allocator) => {
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[0].setLvRefCntWtd(weight * BB_UNITY_WEIGHT);
            Identify(allocator);
            var mappings = GetLocalIntervals(allocator);
            Assert.That(SetOps.IsMember(compiler, LargeVectors(allocator), 0), Is.True);
            Assert.That(SetOps.IsMember(compiler, LargeVectorCandidates(allocator), 0), Is.EqualTo(preferred));
            Assert.That(allocator.intervals.Count, Is.EqualTo(2));
            Assert.That(allocator.intervals[1].isUpperVector, Is.True);
            Assert.That(allocator.intervals[1].registerType, Is.EqualTo(TYP_SIMD16));
            Assert.That(allocator.intervals[1].relatedInterval, Is.SameAs(mappings[0]));
        });
    }
#endif

    [Test]
    public static void RejectingOnePromotedFieldRevokesEarlierFieldCandidates()
    {
        WithCandidates(3, (compiler, allocator) => {
            ref var parent = ref compiler.lvaTable[0];
            parent.Type = TYP_STRUCT;
            parent.lvTracked = false;
            parent.lvPromoted = true;
            parent.lvIsMultiRegDest = true;
            parent.lvFieldCnt = 2;
            parent.lvFieldLclStart = 1;
            for (var index = 1; index < 3; index++)
            {
                compiler.lvaTable[index].lvIsStructField = true;
                compiler.lvaTable[index].lvParentLcl = 0;
            }
            compiler.lvaTable[2].lvPinned = true;
            Identify(allocator);
            Assert.That(parent.lvDoNotEnregister, Is.True);
            Assert.That(parent.lvRefCnt(), Is.EqualTo(9));
            var mappings = GetLocalIntervals(allocator);
            Assert.That(mappings[1], Is.Null);
            Assert.That(mappings[2], Is.Null);
            Assert.That(SetOps.IsEmpty(compiler, Candidates(allocator)), Is.True);
            Assert.That(compiler.lvaTable[1].lvLRACandidate, Is.False);
        });
    }

    private static void WithCandidates(int count, Action<Compiler, LinearScan> action)
    {
        LinearScanMinimalCandidatesTests.WithCompiler(compiler => {
            compiler.opts.compFlags |= CLFLG_REGVAR;
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.lvaCount = count;
            compiler.lvaTrackedCount = count;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaTable = new LclVarDsc[count];
            compiler.lvaTrackedToVarNum = new int[count];
            compiler.fgBBVarSetsInited = true;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            for (var index = 0; index < count; index++)
            {
                compiler.lvaTrackedToVarNum[index] = index;
                compiler.lvaTable[index].Type = TYP_INT;
                compiler.lvaTable[index].lvTracked = true;
                compiler.lvaTable[index]._varIndex = checked((ushort)index);
                compiler.lvaTable[index].lvMustInit = true;
                compiler.lvaTable[index].setLvRefCnt(3);
                compiler.lvaTable[index].setLvRefCntWtd(3 * BB_UNITY_WEIGHT);
            }
            action(compiler, new LinearScan(compiler));
        }, minOpts: false);
    }

    private static Interval?[] GetLocalIntervals(LinearScan allocator)
        => allocator.localVarIntervals ?? throw new AssertionException("Local interval mapping is missing.");

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "identifyCandidatesWithLocals")]
    private static extern void Identify(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_registerCandidateVars")]
    private static extern ref nint[] Candidates(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_fpCalleeSaveCandidateVars")]
    private static extern ref nint[] FloatingCandidates(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_splitOrSpilledVars")]
    private static extern ref nint[] Spilled(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_exceptVars")]
    private static extern ref nint[] Except(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_finallyVars")]
    private static extern ref nint[] Finally(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set__lvLiveInOutOfHandler")]
    private static extern void LiveInOutOfHandler(ref LclVarDsc local, bool value);

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_largeVectorVars")]
    private static extern ref nint[] LargeVectors(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_largeVectorCalleeSaveCandidateVars")]
    private static extern ref nint[] LargeVectorCandidates(LinearScan allocator);
#endif
}
