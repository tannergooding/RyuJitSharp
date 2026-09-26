// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;
using VisitedSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LinearScanLocalBlockLocationsTests
{
    [TestCase(REG_RCX, false, false, REG_RCX)]
    [TestCase(REG_RCX, true, false, REG_RCX)]
    [TestCase(REG_RCX, true, true, REG_STK)]
    [TestCase(REG_RCX, false, true, REG_RCX)]
    [TestCase(REG_STK, false, false, REG_STK)]
    public static void IncomingLocationsRespectPredecessorAndWriteThroughHomes(
        regNumber predecessorReg, bool writeThrough, bool ehPredecessor, regNumber expected)
    {
        WithLocations((compiler, allocator, block, interval) =>
        {
            SetOps.AddElemD(compiler, block.bbLiveIn, 0);
            allocator.setOutVarRegForBB(2, 0, predecessorReg);
            BlockInfo(allocator)![1].predBBNum = 2;
            BlockInfo(allocator)![1].hasEHPred = ehPredecessor;
            interval.isWriteThru = writeThrough;

            ProcessStart(allocator, block);

            Assert.That(allocator.getInVarToRegMap(1)![0], Is.EqualTo(expected));
            Assert.That(interval.isActive, Is.EqualTo(expected != REG_STK));
            if (expected != REG_STK)
            {
                Assert.That(allocator.physRegs[(int)expected].assignedInterval, Is.SameAs(interval));
            }
        });
    }

    [Test]
    public static void EntryFromEhBoundaryKeepsWriteThroughLocalOnStack()
    {
        WithLocations((compiler, allocator, block, interval) =>
        {
            SetOps.AddElemD(compiler, block.bbLiveIn, 0);
            interval.isWriteThru = true;
            BlockInfo(allocator)![1].hasEHBoundaryIn = true;

            ProcessStart(allocator, block);

            Assert.That(allocator.getInVarToRegMap(1)![0], Is.EqualTo(REG_STK));
            Assert.That(interval.isActive, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void WriteThroughLocalWithoutAnIncomingUseRetainsStackHome(bool nextReferenceIsDefinition)
    {
        WithLocations((compiler, allocator, block, interval) =>
        {
            SetOps.AddElemD(compiler, block.bbLiveIn, 0);
            BlockInfo(allocator)![1].predBBNum = 2;
            allocator.setOutVarRegForBB(2, 0, REG_RCX);
            interval.isWriteThru = true;
            interval.firstRefPosition = nextReferenceIsDefinition
                ? new RefPosition(1, 2, null, RefType.RefTypeDef) : null;

            ProcessStart(allocator, block);

            Assert.That(allocator.getInVarToRegMap(1)![0], Is.EqualTo(REG_STK));
            Assert.That(interval.isActive, Is.False);
        });
    }

    [TestCase(false, REG_STK)]
    [TestCase(true, REG_RCX)]
    public static void ResolutionRetainsCopyHomeAfterPredecessorSpill(bool copyReg, regNumber expected)
    {
        WithLocations((compiler, allocator, block, interval) =>
        {
            SetOps.AddElemD(compiler, block.bbLiveIn, 0);
            BlockInfo(allocator)![1].predBBNum = 2;
            allocator.setInVarRegForBB(1, 0, REG_RCX);
            AllocationPassComplete(allocator) = true;
            interval.firstRefPosition!.copyReg = copyReg;

            ProcessStart(allocator, block);

            Assert.That(allocator.getInVarToRegMap(1)![0], Is.EqualTo(expected));
            Assert.That(interval.isActive, Is.EqualTo(expected != REG_STK));
        });
    }

    [Test]
    public static void ResolutionRequiresNextReferenceForPredecessorSpilledRegister()
    {
        WithLocations((compiler, allocator, block, interval) =>
        {
            SetOps.AddElemD(compiler, block.bbLiveIn, 0);
            BlockInfo(allocator)![1].predBBNum = 2;
            allocator.setInVarRegForBB(1, 0, REG_RCX);
            AllocationPassComplete(allocator) = true;
            interval.isWriteThru = true;
            interval.firstRefPosition = null;

            var exception = Assert.Throws<FatalJitException>(() => ProcessStart(allocator, block));

            Assert.That(exception!.Message, Does.Contain("requires a next reference"));
            Assert.That(allocator.getInVarToRegMap(1)![0], Is.EqualTo(REG_RCX));
            Assert.That(interval.isActive, Is.False);
        });
    }

    [TestCase("start")]
    [TestCase("end")]
    [TestCase("end-allocation")]
    public static void BlockLocationEntrypointsRejectDisabledEnregistrationBeforeMutation(string entrypoint)
    {
        WithLocations((_, allocator, block, interval) =>
        {
            var incomingMap = allocator.getInVarToRegMap(1)!;
            var outgoingMap = allocator.getOutVarToRegMap(1)!;
            EnregisterLocals(allocator) = false;

            var exception = Assert.Throws<FatalJitException>(() =>
            {
                switch (entrypoint)
                {
                    case "start":
                    {
                        ProcessStart(allocator, block);
                        break;
                    }
                    case "end":
                    {
                        ProcessEnd(allocator, block);
                        break;
                    }
                    case "end-allocation":
                    {
                        ProcessEndAllocation(allocator, block);
                        break;
                    }
                    default:
                    {
                        throw new AssertionException($"Unexpected block-location entrypoint: {entrypoint}");
                    }
                }
            });

            Assert.That(exception!.Message, Does.Contain("require enregistered locals"));
            Assert.That(incomingMap[0], Is.EqualTo(REG_STK));
            Assert.That(outgoingMap[0], Is.EqualTo(REG_STK));
            Assert.That(interval.isActive, Is.False);
        });
    }

    [Test]
    public static void EndMapCapturesActiveHomeAndStackSpill()
    {
        WithLocations((compiler, allocator, block, interval) =>
        {
            SetOps.AddElemD(compiler, block.bbLiveOut, 0);
            CurrentBlockNumber(allocator) = 1;
            Assign(allocator, allocator.physRegs[(int)REG_RCX], interval);

            ProcessEnd(allocator, block);
            Assert.That(allocator.getOutVarToRegMap(1)![0], Is.EqualTo(REG_RCX));

            interval.isActive = false;
            ProcessEnd(allocator, block);
            Assert.That(allocator.getOutVarToRegMap(1)![0], Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void EndAllocationVisitsBlockAndStartsNextBlockFromItsOutgoingMap()
    {
        WithLocations((compiler, allocator, block, interval) =>
        {
            var successor = new BasicBlock(null, null)
            {
                bbNum = 2,
                bbPostorderNum = 1,
                bbLiveIn = SetOps.MakeSingleton(compiler, 0),
                bbLiveOut = SetOps.MakeEmpty(compiler),
            };
            BlockInfo(allocator)![2].predBBNum = 1;
            SetOps.AddElemD(compiler, block.bbLiveOut, 0);
            CurrentBlockNumber(allocator) = 1;
            BlockSequence(allocator) = [block, successor];
            BlockSequenceCount(allocator) = 2;
            BlockSequencingDone(allocator) = true;
            VisitedTraits(allocator) = new BitVecTraits(compiler, 2);
            VisitedBlocks(allocator) = VisitedSetOps.MakeEmpty(VisitedTraits(allocator)!);
            Assign(allocator, allocator.physRegs[(int)REG_RCX], interval);

            ProcessEndAllocation(allocator, block);

            Assert.That(allocator.getOutVarToRegMap(1)![0], Is.EqualTo(REG_RCX));
            Assert.That(allocator.getInVarToRegMap(2)![0], Is.EqualTo(REG_RCX));
            Assert.That(VisitedSetOps.IsMember(VisitedTraits(allocator)!, VisitedBlocks(allocator), 0), Is.True);
        });
    }

    [Test]
    public static void DeadCandidateReleasesRegisterAndRecordsStackHome()
    {
        WithLocations((_, allocator, block, interval) =>
        {
            interval.firstRefPosition = null;
            interval.isWriteThru = true;
            Assign(allocator, allocator.physRegs[(int)REG_RCX], interval);
            allocator.setInVarRegForBB(1, 0, REG_RCX);
            BlockInfo(allocator)![1].predBBNum = 2;

            ProcessStart(allocator, block);

            Assert.That(interval.isActive, Is.False);
            Assert.That(allocator.getInVarToRegMap(1)![0], Is.EqualTo(REG_STK));
            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval, Is.Null);
        });
    }

    [Test]
    public static void DeadConstantClearsRegisterAndConstantMask()
    {
        WithLocations((_, allocator, block, _) =>
        {
            var constant = new Interval(TYP_INT, SRBM_RCX) { isConstant = true };
            Assign(allocator, allocator.physRegs[(int)REG_RCX], constant);
            Assert.That(ConstantRegisters(allocator).IsSet(REG_RCX), Is.True);

            ProcessStart(allocator, block);

            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval, Is.Null);
            Assert.That(ConstantRegisters(allocator).IsSet(REG_RCX), Is.False);
        });
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    [Test]
    public static void DeadUpperVectorReleasesItsRegisterWithoutChangingLocalMap()
    {
        WithLocations((_, allocator, block, _) =>
        {
            var upper = new Interval(TYP_SIMD16, SRBM_XMM1) { isUpperVector = true };
            ActualRegisters(allocator) = new regMaskTP(SRBM_XMM1);
            Assign(allocator, allocator.physRegs[(int)REG_XMM1], upper);

            ProcessStart(allocator, block);

            Assert.That(allocator.physRegs[(int)REG_XMM1].assignedInterval, Is.Null);
            Assert.That(allocator.getInVarToRegMap(1)![0], Is.EqualTo(REG_STK));
        });
    }
#endif

#if DEBUG
    [Test]
    public static void BoundaryRotationSplitsAndMovesTheIncomingHome()
    {
        WithLocations((compiler, allocator, block, interval) =>
        {
            SetOps.AddElemD(compiler, block.bbLiveIn, 0);
            BlockInfo(allocator)![1].predBBNum = 2;
            allocator.setOutVarRegForBB(2, 0, REG_RCX);
            StressMask(allocator) = 0x200;

            ProcessStart(allocator, block);

            Assert.That(allocator.getInVarToRegMap(1)![0], Is.Not.EqualTo(REG_RCX));
            Assert.That(interval.isSplit, Is.True);
            Assert.That(SetOps.IsMember(compiler, SplitVars(allocator), 0), Is.True);
        });
    }
#endif

    private static void WithLocations(Action<Compiler, LinearScan, BasicBlock, Interval> action)
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) =>
        {
            compiler.lvaTrackedFixed = true;
            compiler.lvaTable[0].lvLRACandidate = true;
            compiler.codeGen!.RegSet.rsClearRegsModified();
            compiler.fgBBNumMax = 2;
            var block = new BasicBlock(null, null)
            {
                bbNum = 1,
                bbLiveIn = SetOps.MakeEmpty(compiler),
                bbLiveOut = SetOps.MakeEmpty(compiler),
            };
            var interval = new Interval(TYP_INT, SRBM_RCX | SRBM_RDX)
            {
                isLocalVar = true,
                varNum = 0,
                firstRefPosition = new RefPosition(1, 2, null, RefType.RefTypeUse),
            };
            allocator.localVarIntervals = [interval];
            Candidates(allocator) = SetOps.MakeSingleton(compiler, 0);
            BlockInfo(allocator) = new LsraBlockInfo[3];
            MaxBlockBeforeResolution(allocator) = 2;
            ActualRegisters(allocator) = new regMaskTP(SRBM_RCX | SRBM_RDX);
            BuildPhysicalRegisters(allocator);
            allocator.initVarRegMaps();
            action(compiler, allocator, block, interval);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "processBlockStartLocations")]
    private static extern void ProcessStart(LinearScan allocator, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "processBlockEndLocations")]
    private static extern void ProcessEnd(LinearScan allocator, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "processBlockEndAllocationWithLocals")]
    private static extern void ProcessEndAllocation(LinearScan allocator, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignPhysReg")]
    private static extern void Assign(LinearScan allocator, RegRecord record, Interval interval);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysicalRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_registerCandidateVars")]
    private static extern ref nint[] Candidates(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint MaxBlockBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_actualRegistersMask")]
    private static extern ref regMaskTP ActualRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allocationPassComplete")]
    private static extern ref bool AllocationPassComplete(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enregisterLocalVars")]
    private static extern ref bool EnregisterLocals(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_registersWithConstants")]
    private static extern ref regMaskTP ConstantRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockSequence")]
    private static extern ref BasicBlock[]? BlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockSequenceCount")]
    private static extern ref int BlockSequenceCount(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockSequencingDone")]
    private static extern ref bool BlockSequencingDone(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockSequenceVisitedTraits")]
    private static extern ref BitVecTraits? VisitedTraits(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockSequenceVisitedSet")]
    private static extern ref nint[] VisitedBlocks(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlockNumber")]
    private static extern ref uint CurrentBlockNumber(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_splitOrSpilledVars")]
    private static extern ref nint[] SplitVars(LinearScan allocator);
#endif
}
