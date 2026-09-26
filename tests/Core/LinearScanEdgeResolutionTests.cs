// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using SetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LinearScanEdgeResolutionTests
{
    [TestCase(REG_STK, REG_RCX, GT_LCL_VAR, GTF_SPILLED)]
    [TestCase(REG_RCX, REG_STK, GT_LCL_VAR, GTF_SPILL)]
    [TestCase(REG_RCX, REG_RDX, GT_COPY, GTF_EMPTY)]
    public static void JoinMovesPreserveStackAndRegisterHomes(
        regNumber from, regNumber to, genTreeOps expectedOper, GenTreeFlags expectedFlag)
    {
        WithEdge(1, (compiler, allocator, source, target, _) =>
        {
            SetOps.AddElemD(compiler, target.bbLiveIn, 0);
            var interval = MapInterval(compiler, allocator, 0);
            interval.isSpilled = true;
            allocator.setOutVarRegForBB((uint)source.bbNum, 0, from);
            allocator.setInVarRegForBB((uint)target.bbNum, 0, to);
            var liveSet = SetOps.MakeEmpty(compiler);
            SetOps.AddElemD(compiler, liveSet, 0);

            ResolveEdge(allocator, source, target, LinearScan.ResolveType.ResolveJoin, liveSet, RBM_NONE);

            var inserted = source.LastNode
                ?? throw new AssertionException("Join resolution did not insert a node.");
            Assert.That(inserted.Oper, Is.EqualTo(expectedOper));
            Assert.That((inserted.Flags & expectedFlag) == expectedFlag, Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(allocator.getOutVarToRegMap((uint)source.bbNum)![0], Is.EqualTo(to));
            Assert.That(allocator.getInVarToRegMap((uint)target.bbNum)![0], Is.EqualTo(to));
            if (to is REG_RDX)
            {
                Assert.That(inserted.AsCopyOrReload().Op1.RegNum, Is.EqualTo(REG_RCX));
                Assert.That(inserted.RegNum, Is.EqualTo(REG_RDX));
            }
        });
    }

    [Test]
    public static void RegisterCycleUsesSwapWhenNoIntegerScratchRegisterIsFree()
    {
        WithEdge(2, (compiler, allocator, source, target, _) =>
        {
            AvailableIntegerRegisters(allocator) = SRBM_RCX | SRBM_RDX;
            var liveSet = SetOps.MakeEmpty(compiler);
            for (var index = 0; index < 2; index++)
            {
                MapInterval(compiler, allocator, index).isSplit = true;
                SetOps.AddElemD(compiler, liveSet, index);
                SetOps.AddElemD(compiler, target.bbLiveIn, index);
            }
            allocator.setOutVarRegForBB((uint)source.bbNum, 0, REG_RCX);
            allocator.setOutVarRegForBB((uint)source.bbNum, 1, REG_RDX);
            allocator.setInVarRegForBB((uint)target.bbNum, 0, REG_RDX);
            allocator.setInVarRegForBB((uint)target.bbNum, 1, REG_RCX);

            ResolveEdge(allocator, source, target, LinearScan.ResolveType.ResolveJoin, liveSet, RBM_NONE);

            var swap = source.LastNode ?? throw new AssertionException("Missing register swap.");
            Assert.That(swap.Oper, Is.EqualTo(GT_SWAP));
            Assert.That(swap.AsOp().Op1.RegNum, Is.EqualTo(REG_RCX));
            Assert.That(swap.AsOp().Op2!.RegNum, Is.EqualTo(REG_RDX));
            Assert.That(allocator.getOutVarToRegMap((uint)source.bbNum)![0], Is.EqualTo(REG_RDX));
            Assert.That(allocator.getOutVarToRegMap((uint)source.bbNum)![1], Is.EqualTo(REG_RCX));
        });
    }

    [Test]
    public static void ThreeWayRegisterCycleUsesFreeCallerTrasherAsScratch()
    {
        WithEdge(3, (compiler, allocator, source, target, _) =>
        {
            AvailableIntegerRegisters(allocator) = SRBM_RAX | SRBM_RCX | SRBM_RDX | SRBM_RBX;
            var liveSet = SetOps.MakeEmpty(compiler);
            var registers = new[] { REG_RCX, REG_RDX, REG_RBX };
            for (var index = 0; index < registers.Length; index++)
            {
                MapInterval(compiler, allocator, index).isSplit = true;
                SetOps.AddElemD(compiler, liveSet, index);
                SetOps.AddElemD(compiler, target.bbLiveIn, index);
                allocator.setOutVarRegForBB((uint)source.bbNum, (uint)index, registers[index]);
                allocator.setInVarRegForBB((uint)target.bbNum, (uint)index,
                    registers[(index + 1) % registers.Length]);
            }

            ResolveEdge(allocator, source, target, LinearScan.ResolveType.ResolveJoin, liveSet, RBM_NONE);

            var moveCount = 0;
            for (var node = source.FirstNode; node is not null; node = node.Next)
            {
                if (node.Oper is GT_COPY)
                {
                    moveCount++;
                }
                Assert.That(node.Oper, Is.Not.EqualTo(GT_SWAP));
                Assert.That((node.Flags & (GTF_SPILL | GTF_SPILLED)) == 0, Is.True);
            }
            Assert.That(moveCount, Is.EqualTo(4));
            Assert.That((compiler.codeGen!.RegSet.rsGetModifiedRegsMask().Lower & SRBM_RAX) != 0, Is.True);
            for (var index = 0; index < registers.Length; index++)
            {
                Assert.That(allocator.getOutVarToRegMap((uint)source.bbNum)![index],
                    Is.EqualTo(registers[(index + 1) % registers.Length]));
            }
        });
    }

    [Test]
    public static void FloatingCycleSpillsOneMemberThenReloadsAfterOtherMoves()
    {
        WithEdge(2, (compiler, allocator, source, target, _) =>
        {
            compiler.compFloatingPointUsed = true;
            AvailableFloatingRegisters(allocator) = SRBM_XMM0 | SRBM_XMM1;
            var liveSet = SetOps.MakeEmpty(compiler);
            for (var index = 0; index < 2; index++)
            {
                compiler.lvaTable[index].Type = TYP_FLOAT;
                var interval = MapInterval(compiler, allocator, index, TYP_FLOAT);
                interval.isSplit = true;
                SetOps.AddElemD(compiler, liveSet, index);
                SetOps.AddElemD(compiler, target.bbLiveIn, index);
            }
            allocator.setOutVarRegForBB((uint)source.bbNum, 0, REG_XMM0);
            allocator.setOutVarRegForBB((uint)source.bbNum, 1, REG_XMM1);
            allocator.setInVarRegForBB((uint)target.bbNum, 0, REG_XMM1);
            allocator.setInVarRegForBB((uint)target.bbNum, 1, REG_XMM0);

            ResolveEdge(allocator, source, target, LinearScan.ResolveType.ResolveJoin, liveSet, RBM_NONE);

            var spilled = false;
            var reloaded = false;
            for (var node = source.FirstNode; node is not null; node = node.Next)
            {
                spilled |= (node.Flags & GTF_SPILL) != 0;
                reloaded |= (node.Flags & GTF_SPILLED) != 0;
            }
            Assert.That(spilled && reloaded, Is.True);
            Assert.That(source.LastNode!.RegNum, Is.EqualTo(REG_XMM1));
            Assert.That(allocator.getOutVarToRegMap((uint)source.bbNum)![0], Is.EqualTo(REG_XMM1));
            Assert.That(allocator.getOutVarToRegMap((uint)source.bbNum)![1], Is.EqualTo(REG_XMM0));
        });
    }

    [Test]
    public static void WriteThroughSplitRetainsValidMemoryHome()
    {
        WithEdge(1, (compiler, allocator, source, target, _) =>
        {
            var interval = MapInterval(compiler, allocator, 0);
            interval.isWriteThru = true;
            interval.isSpilled = true;
            allocator.setOutVarRegForBB((uint)source.bbNum, 0, REG_RCX);
            SetOps.AddElemD(compiler, target.bbLiveIn, 0);
            var liveSet = SetOps.MakeEmpty(compiler);
            SetOps.AddElemD(compiler, liveSet, 0);

            ResolveEdge(allocator, source, target, LinearScan.ResolveType.ResolveSplit, liveSet, RBM_NONE);

            Assert.That(target.FirstNode, Is.Null);
            Assert.That(allocator.getInVarToRegMap((uint)target.bbNum)![0], Is.EqualTo(REG_STK));
            Assert.That(allocator.getOutVarToRegMap((uint)source.bbNum)![0], Is.EqualTo(REG_RCX));
        });
    }

    [Test]
    public static void JoinEmitsVirtualEhMoveForExtraLiveWriteThroughLocal()
    {
        WithEdge(1, (compiler, allocator, source, target, _) =>
        {
            compiler.compHndBBtabCount = 1;
            var interval = MapInterval(compiler, allocator, 0);
            interval.isWriteThru = true;
            interval.isSpilled = true;
            SetOps.AddElemD(compiler, source.bbLiveOut, 0);
            SetOps.AddElemD(compiler, ExceptVars(allocator), 0);
            allocator.setOutVarRegForBB((uint)source.bbNum, 0, REG_RCX);

            ResolveEdge(allocator, source, target, LinearScan.ResolveType.ResolveJoin,
                SetOps.MakeEmpty(compiler), RBM_NONE);

            Assert.That(source.LastNode!.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That((source.LastNode.Flags & GTF_SPILL) != 0, Is.True);
            Assert.That(allocator.getOutVarToRegMap((uint)source.bbNum)![0], Is.EqualTo(REG_STK));
        });
    }

#if DEBUG
    [Test]
    public static void VerboseJoinReportsSourceDestinationAndResolutionKind()
    {
        WithEdge(1, (compiler, allocator, source, target, _) =>
        {
            compiler.verbose = true;
            MapInterval(compiler, allocator, 0).isSplit = true;
            SetOps.AddElemD(compiler, target.bbLiveIn, 0);
            allocator.setOutVarRegForBB((uint)source.bbNum, 0, REG_RCX);
            allocator.setInVarRegForBB((uint)target.bbNum, 0, REG_RDX);
            var liveSet = SetOps.MakeEmpty(compiler);
            SetOps.AddElemD(compiler, liveSet, 0);

            var dump = CodeGenLifeTransitionTests.Capture(() =>
                ResolveEdge(allocator, source, target, LinearScan.ResolveType.ResolveJoin, liveSet, RBM_NONE));

            Assert.That(dump, Does.Contain(
                $"{FMT_BB(source.bbNum)} bottom ({FMT_BB(source.bbNum)}->{FMT_BB(target.bbNum)}): " +
                $"move V00 from {REG_RCX.Name} to {REG_RDX.Name} (Join)"));
        });
    }
#endif

    [TestCase(false)]
    [TestCase(true)]
    public static void CriticalEdgeGetsItsOwnResolutionBlockAndMappedLocations(bool duplicateSwitchTarget)
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) =>
        {
            compiler.compRationalIRForm = true;
            compiler.fgPredsComputed = true;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            var source = BasicBlock.New(compiler, duplicateSwitchTarget ? BBJ_SWITCH : BBJ_COND);
            var splitSuccessor = BasicBlock.New(compiler, BBJ_RETURN);
            var other = BasicBlock.New(compiler, BBJ_ALWAYS);
            var join = BasicBlock.New(compiler, BBJ_RETURN);
            source.Next = splitSuccessor;
            splitSuccessor.Next = other;
            other.Next = join;
            compiler.fgFirstBB = source;
            compiler.fgLastBB = join;
            var first = compiler.fgAddRefPred(join, source);
            var second = compiler.fgAddRefPred(splitSuccessor, source);
            other.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(join, other));
            other.TargetEdge.Likelihood = 1;
            var operand = new GenTreeLclVar(TYP_INT, 0) { RegNum = REG_RAX };
            source.InsertAtEnd(operand);
            if (duplicateSwitchTarget)
            {
                var duplicate = compiler.fgAddRefPred(join, source, first);
                first.Likelihood = 2.0 / 3;
                second.Likelihood = 1.0 / 3;
                var switchTargets = new BBswtDesc([first, second], [0, 0, 1], hasDefault: true);
                switchTargets.Cases[0] = first;
                switchTargets.Cases[1] = duplicate;
                switchTargets.Cases[2] = second;
                source.SwitchTargets = switchTargets;
                var table = new GenTreeIntCon(TYP_INT, 0) { RegNum = REG_RDX };
                source.InsertAtEnd(table);
                source.InsertAtEnd(new GenTreeOp(GT_SWITCH_TABLE, TYP_VOID, operand, table));
            }
            else
            {
                first.Likelihood = 0.5;
                second.Likelihood = 0.5;
                source.SetCond(first, second);
                source.InsertAtEnd(new GenTreeUnOp(GT_JTRUE, TYP_VOID, operand));
            }

            Setup(compiler, allocator);
            var interval = MapInterval(compiler, allocator, 0);
            interval.isSplit = true;
            SetOps.AddElemD(compiler, ResolutionCandidates(allocator), 0);
            SetOps.AddElemD(compiler, SplitOrSpilled(allocator)!, 0);
            SetOps.AddElemD(compiler, source.bbLiveOut, 0);
            SetOps.AddElemD(compiler, other.bbLiveOut, 0);
            SetOps.AddElemD(compiler, join.bbLiveIn, 0);
            SetOps.AddElemD(compiler, splitSuccessor.bbLiveIn, 0);
            allocator.setOutVarRegForBB((uint)source.bbNum, 0, REG_RCX);
            allocator.setOutVarRegForBB((uint)other.bbNum, 0, REG_RDX);
            allocator.setInVarRegForBB((uint)join.bbNum, 0, REG_RDX);
            allocator.setInVarRegForBB((uint)splitSuccessor.bbNum, 0, REG_RBX);
            HasCriticalEdges(allocator) = true;
            BlockInfo(allocator)![source.bbNum].hasCriticalOutEdge = true;
            var originalMaximum = compiler.fgBBNumMax;

            ResolveEdges(allocator);

            Assert.That(compiler.fgBBNumMax, Is.GreaterThan(originalMaximum));
            var resolution = duplicateSwitchTarget
                ? source.SwitchTargets.Cases[0].DestinationBlock : source.TrueTarget;
            Assert.That(resolution, Is.Not.SameAs(join));
            if (duplicateSwitchTarget)
            {
                Assert.That(source.SwitchTargets.Cases[1].DestinationBlock, Is.SameAs(resolution));
            }
            Assert.That(resolution.UniqueSucc, Is.SameAs(join));
            Assert.That(resolution.FirstNode, Is.Not.Null);
            Assert.That(allocator.getInVarToRegMap((uint)resolution.bbNum)![0], Is.EqualTo(REG_RCX));
            Assert.That(allocator.getOutVarToRegMap((uint)resolution.bbNum)![0], Is.EqualTo(REG_RDX));
            Assert.That(SetOps.IsMember(compiler, resolution.bbLiveIn, 0), Is.True);
            Assert.That(SetOps.IsMember(compiler, resolution.bbLiveOut, 0), Is.True);
        });
    }

    [Test]
    public static void CriticalEdgeWithOnlyEhVariablesReloadsAtJoinWithoutSplitting()
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) =>
        {
            compiler.compRationalIRForm = true;
            compiler.fgPredsComputed = true;
            compiler.compHndBBtabCount = 1;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            var source = BasicBlock.New(compiler, BBJ_COND);
            var other = BasicBlock.New(compiler, BBJ_ALWAYS);
            var join = BasicBlock.New(compiler, BBJ_RETURN);
            source.Next = other;
            other.Next = join;
            compiler.fgFirstBB = source;
            compiler.fgLastBB = join;
            var joinEdge = compiler.fgAddRefPred(join, source);
            joinEdge.Likelihood = 0.5;
            var otherEdge = compiler.fgAddRefPred(other, source);
            otherEdge.Likelihood = 0.5;
            source.SetCond(joinEdge, otherEdge);
            other.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(join, other));
            other.TargetEdge.Likelihood = 1;
            var operand = new GenTreeLclVar(TYP_INT, 0) { RegNum = REG_RCX };
            source.InsertAtEnd(operand);
            source.InsertAtEnd(new GenTreeUnOp(GT_JTRUE, TYP_VOID, operand));

            Setup(compiler, allocator);
            var interval = MapInterval(compiler, allocator, 0);
            interval.isWriteThru = true;
            interval.isSpilled = true;
            SetOps.AddElemD(compiler, ResolutionCandidates(allocator), 0);
            SetOps.AddElemD(compiler, SplitOrSpilled(allocator)!, 0);
            SetOps.AddElemD(compiler, ExceptVars(allocator), 0);
            SetOps.AddElemD(compiler, source.bbLiveOut, 0);
            SetOps.AddElemD(compiler, join.bbLiveIn, 0);
            allocator.setInVarRegForBB((uint)join.bbNum, 0, REG_RCX);
            HasCriticalEdges(allocator) = true;
            BlockInfo(allocator)![source.bbNum].hasCriticalOutEdge = true;
            var originalMaximum = compiler.fgBBNumMax;

            ResolveEdges(allocator);

            Assert.That(compiler.fgBBNumMax, Is.EqualTo(originalMaximum));
            Assert.That(join.FirstNode!.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That((join.FirstNode.Flags & GTF_SPILLED) != 0, Is.True);
            Assert.That(join.FirstNode.RegNum, Is.EqualTo(REG_RCX));
            Assert.That(allocator.getInVarToRegMap((uint)join.bbNum)![0], Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void DisabledLocalAllocationRejectsResolutionBeforeChangingMaps()
    {
        WithEdge(1, (compiler, allocator, source, target, _) =>
        {
            EnregisterLocals(allocator) = false;
            var originalMaximum = compiler.fgBBNumMax;
            var exception = Assert.Throws<FatalJitException>(() => ResolveEdges(allocator));
            Assert.That(exception!.Message, Does.Contain("requires allocated local maps"));
            Assert.That(compiler.fgBBNumMax, Is.EqualTo(originalMaximum));
            Assert.That(SetOps.IsEmpty(compiler, ResolutionCandidates(allocator)), Is.True);
        });
    }

#if DEBUG
    [TestCase(0x1, TYP_INT, SRBM_RAX | SRBM_RBX, SRBM_RBX)]
    [TestCase(0x1, TYP_INT, SRBM_RAX, SRBM_RAX)]
    [TestCase(0x2, TYP_INT, SRBM_RAX | SRBM_RBX, SRBM_RAX)]
    [TestCase(0x2, TYP_FLOAT, SRBM_XMM0 | SRBM_XMM6, SRBM_XMM0)]
    [TestCase(0x3, TYP_INT, SRBM_RAX | SRBM_RBX, SRBM_RAX | SRBM_RBX)]
    [TestCase(0x2000, TYP_FLOAT, SRBM_XMM0 | SRBM_XMM16, SRBM_XMM16)]
    [TestCase(0x2000, TYP_INT, SRBM_RAX | SRBM_RBX, SRBM_RAX | SRBM_RBX)]
    [TestCase(0x4000, TYP_INT, SRBM_RAX | SRBM_R16, SRBM_R16)]
    [TestCase(0x4000, TYP_FLOAT, SRBM_XMM0 | SRBM_XMM6, SRBM_XMM0 | SRBM_XMM6)]
    public static void NullResolutionStressUsesOneCandidateWithoutFixedRegister(
        int stressMask, var_types type, regMask candidates, regMask expected)
    {
        WithEdge(1, (_, allocator, _, _, _) =>
        {
            StressMask(allocator) = stressMask;
            Assert.That(StressLimit(allocator, null, type, candidates), Is.EqualTo(expected));
        });
    }

    [TestCase(1u, false, SRBM_RAX)]
    [TestCase(2u, false, SRBM_RAX | SRBM_RBX)]
    [TestCase(1u, true, SRBM_RAX | SRBM_RBX)]
    public static void NonnullStressStillRespectsMinimumCandidatesAndFixedRegister(
        uint minimumCount, bool fixedReference, regMask expected)
    {
        WithEdge(1, (_, allocator, _, _, _) =>
        {
            StressMask(allocator) = 0x2;
            var reference = new RefPosition(1, 2, null, RefType.RefTypeUse)
            {
                registerAssignment = SRBM_RBX,
                isFixedRegRef = fixedReference,
                minRegCandidateCount = minimumCount,
            };
            Assert.That(StressLimit(allocator, reference, TYP_INT, SRBM_RAX | SRBM_RBX),
                Is.EqualTo(expected));
        });
    }

    [TestCase(0x1, REG_RBX)]
    [TestCase(0x2, REG_RAX)]
    [TestCase(0x3, REG_NA)]
    public static void ScratchSelectionUsesNullableStressContract(int stressMask, regNumber expected)
    {
        WithEdge(1, (compiler, allocator, source, target, _) =>
        {
            StressMask(allocator) = stressMask;
            AvailableIntegerRegisters(allocator) = SRBM_RAX | SRBM_RBX;
            var scratch = TempReg(allocator, source, target, TYP_INT,
                SetOps.MakeEmpty(compiler), RBM_NONE);
            Assert.That(scratch, Is.EqualTo(expected));
        });
    }
#endif

    private static void WithEdge(int localCount,
        Action<Compiler, LinearScan, BasicBlock, BasicBlock, BasicBlock> action)
    {
        LinearScanLocalCandidatesTests.WithCandidates(localCount, (compiler, allocator) =>
        {
            compiler.compRationalIRForm = true;
            compiler.fgPredsComputed = true;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            var source = BasicBlock.New(compiler, BBJ_ALWAYS);
            var other = BasicBlock.New(compiler, BBJ_ALWAYS);
            var target = BasicBlock.New(compiler, BBJ_RETURN);
            source.Next = other;
            other.Next = target;
            compiler.fgFirstBB = source;
            compiler.fgLastBB = target;
            source.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, source));
            other.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, other));
            Setup(compiler, allocator);
            action(compiler, allocator, source, target, other);
        });
    }

    private static void Setup(Compiler compiler, LinearScan allocator)
    {
        compiler.lvaTrackedFixed = true;
        compiler.fgLocalVarLivenessDone = true;
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
        compiler.srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
        compiler.codeGen!.RegSet.rsClearRegsModified();
        foreach (ref var local in compiler.lvaTable.AsSpan())
        {
            local.RegNum = REG_STK;
            local.lvLRACandidate = true;
        }
        EnregisterLocals(allocator) = true;
        BlockSequencingDone(allocator) = true;
        MaxBlockBeforeResolution(allocator) = (uint)compiler.fgBBNumMax;
        BlockInfo(allocator) = new LsraBlockInfo[compiler.fgBBNumMax + 1];
        ResolutionCandidates(allocator) = SetOps.MakeEmpty(compiler);
        SplitOrSpilled(allocator) = SetOps.MakeEmpty(compiler);
        ExceptVars(allocator) = SetOps.MakeEmpty(compiler);
        allocator.localVarIntervals = new Interval?[compiler.lvaTrackedCount];
        allocator.initVarRegMaps();
    }

    private static Interval MapInterval(Compiler compiler, LinearScan allocator, int index,
        var_types type = TYP_INT)
    {
        var interval = new Interval(type, SRBM_RCX | SRBM_RDX)
        {
            isLocalVar = true,
            varNum = (uint)index,
        };
        allocator.localVarIntervals![index] = interval;
        return interval;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "resolveEdge")]
    private static extern void ResolveEdge(LinearScan allocator, BasicBlock from, BasicBlock? to,
        LinearScan.ResolveType type, nint[] liveSet, regMaskTP consumedRegs);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "resolveEdges")]
    private static extern void ResolveEdges(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "stressLimitRegs")]
    private static extern regMask StressLimit(
        LinearScan allocator, RefPosition? reference, var_types type, regMask candidates);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getTempRegForResolution")]
    private static extern regNumber TempReg(LinearScan allocator, BasicBlock from, BasicBlock? to,
        var_types type, nint[] sharedCriticalLiveSet, regMaskTP consumedRegisters);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enregisterLocalVars")]
    private static extern ref bool EnregisterLocals(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockSequencingDone")]
    private static extern ref bool BlockSequencingDone(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint MaxBlockBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hasCriticalEdges")]
    private static extern ref bool HasCriticalEdges(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_resolutionCandidateVars")]
    private static extern ref nint[] ResolutionCandidates(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_splitOrSpilledVars")]
    private static extern ref nint[]? SplitOrSpilled(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_exceptVars")]
    private static extern ref nint[] ExceptVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntegerRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableFloatRegs")]
    private static extern ref regMask AvailableFloatingRegisters(LinearScan allocator);
}
