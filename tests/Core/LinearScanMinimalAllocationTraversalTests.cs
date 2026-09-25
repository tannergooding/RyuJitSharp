// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Globalization;
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanMinimalAllocationTraversalTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void MinimalTraversalAllocatesAndReleasesScalarDefinitionAndUse(bool evex)
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var use = allocator.newRefPosition(interval, 4, RefType.RefTypeUse, tree, SRBM_RAX);

            AllocateRegistersMinimal(allocator);

            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(use.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(use.lastUse, Is.True);
            Assert.That(interval.isActive, Is.False);
            Assert.That(interval.isSpilled, Is.False);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval, Is.Null);
            Assert.That(CurrentAllocationLocation(allocator), Is.EqualTo(4u));
        }, evex);
    }

#if DEBUG
    [Test]
    public static void MinimalAllocationDiagnosticsMatchNativeIntervalAndReferenceShapes()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;

            var interval = NewInterval(allocator, TYP_INT);
            interval.registerPreferences = SRBM_RAX;
            interval.registerAversion = SRBM_RBX;
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var use = allocator.newRefPosition(interval, 4, RefType.RefTypeUse, tree, SRBM_RAX);
            var weight = allocator.GetReferenceDiagnosticWeight(definition)
                .ToString("F2", CultureInfo.InvariantCulture);

            Assert.That(definition.rpNum, Is.EqualTo(1));
            Assert.That(use.rpNum, Is.EqualTo(3));

            var intervalOutput = Capture(() => DumpLsraIntervals(allocator, "test"));
            var referenceOutput = Capture(() => DumpRefPositions(allocator, "BEFORE ALLOCATION"));
            var definitionOutput = Capture(() => definition.dump(allocator));
            var useOutput = Capture(() => use.dump(allocator));
            var variableOutput = Capture(() => DumpVarRefPositions(allocator, "BEFORE ALLOCATION"));

            Assert.That(intervalOutput, Is.EqualTo(
                $"{Environment.NewLine}Linear scan intervals test:{Environment.NewLine}" +
                $"Interval  0: int RefPositions {{#{definition.rpNum}@2 #{use.rpNum}@4}} " +
                $"physReg:NA Preferences=[rax] Aversions=[rbx]{Environment.NewLine}{Environment.NewLine}"));
            Assert.That(referenceOutput, Does.StartWith(
                $"------------{Environment.NewLine}" +
                $"REFPOSITIONS BEFORE ALLOCATION: {Environment.NewLine}" +
                $"------------{Environment.NewLine}"));
            Assert.That(definitionOutput, Is.EqualTo(
                $"<RefPosition #{definition.rpNum,-3} @{definition.nodeLocation,-3} RefTypeDef " +
                $"<Ivl:{interval.intervalIndex}> CNS_INT BB{block.bbNum:D2} regmask=[rax] " +
                $"minReg={definition.minRegCandidateCount} fixed wt={weight}>{Environment.NewLine}"));
            Assert.That(useOutput, Is.EqualTo(
                $"<RefPosition #{use.rpNum,-3} @{use.nodeLocation,-3} RefTypeUse " +
                $"<Ivl:{interval.intervalIndex}> CNS_INT BB{block.bbNum:D2} regmask=[rax] " +
                $"minReg={use.minRegCandidateCount} last fixed wt=" +
                $"{allocator.GetReferenceDiagnosticWeight(use).ToString("F2", CultureInfo.InvariantCulture)}>" +
                Environment.NewLine));
            Assert.That(referenceOutput, Does.Contain(definitionOutput));
            Assert.That(referenceOutput, Does.Contain(useOutput));
            Assert.That(variableOutput, Is.Empty);
        });
    }

    [Test]
    public static void MinimalAllocationVariableReferenceDumpMatchesNativeShape()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true, lvLRACandidate = true },
            ];
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            allocator.localVarIntervals = new Interval?[1];
            EnregisterLocalVars(allocator) = true;

            var interval = NewInterval(allocator, TYP_INT);
            interval.setLocalNumber(compiler, 0, allocator);
            var definition = allocator.newRefPosition(
                interval, 2, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 1), SRBM_RAX);
            var weight = allocator.GetReferenceDiagnosticWeight(definition)
                .ToString("F2", CultureInfo.InvariantCulture);

            var output = Capture(() => DumpVarRefPositions(allocator, "BEFORE ALLOCATION"));

            Assert.That(output, Is.EqualTo(
                $"{Environment.NewLine}VAR REFPOSITIONS BEFORE ALLOCATION{Environment.NewLine}" +
                $"--- V00  (Interval {interval.intervalIndex}){Environment.NewLine}" +
                $"<RefPosition #{definition.rpNum,-3} @{definition.nodeLocation,-3} RefTypeDef " +
                $"<Ivl:{interval.intervalIndex} V00> CNS_INT BB{block.bbNum:D2} regmask=[rax] " +
                $"minReg={definition.minRegCandidateCount} last fixed wt={weight}>{Environment.NewLine}" +
                Environment.NewLine));
        });
    }

    [Test]
    public static void MinimalAllocationDumpsMatchNativeCallerOrder()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var interval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(
                interval, 2, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 1), SRBM_RAX);
            _ = allocator.newRefPosition(
                interval, 4, RefType.RefTypeUse, compiler.gtNewIconNode(TYP_INT, 1), SRBM_RAX);
            compiler.verbose = true;

            var output = Capture(() => AllocateRegistersMinimal(allocator));
            var intervals = output.IndexOf("Linear scan intervals before allocateRegistersMinimal:", StringComparison.Ordinal);
            var before = output.IndexOf("REFPOSITIONS BEFORE ALLOCATION: ", StringComparison.Ordinal);
            var allocation = output.IndexOf("Allocating Registers", StringComparison.Ordinal);
            var after = output.IndexOf("REFPOSITIONS AFTER ALLOCATION: ", StringComparison.Ordinal);
            var active = output.IndexOf("Active intervals at end of allocation:", StringComparison.Ordinal);

            Assert.That(intervals, Is.GreaterThanOrEqualTo(0));
            Assert.That(before, Is.GreaterThan(intervals));
            Assert.That(allocation, Is.GreaterThan(before));
            Assert.That(after, Is.GreaterThan(allocation));
            Assert.That(active, Is.GreaterThan(after));
        });
    }

    [Test]
    public static void MinimalTraversalReportsSelectionScoreForAllocation()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(
                interval, 2, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            _ = allocator.newRefPosition(
                interval, 4, RefType.RefTypeUse, tree, SRBM_RAX | SRBM_RBX);
            compiler.verbose = true;

            var output = Capture(() => AllocateRegistersMinimal(allocator));

            Assert.That(output, Does.Contain("ORDER(A) rax "));
        });
    }

    [Test]
    public static void MinimalTraversalReportsSelectionScoreForRegisterReuse()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var interval = NewInterval(allocator, TYP_INT);
            interval.isConstant = true;
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            tree.IsReuseRegVal = true;
            _ = allocator.newRefPosition(
                interval, 2, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            compiler.verbose = true;

            var output = Capture(() => AllocateRegistersMinimal(allocator));

            Assert.That(output, Does.Contain("ORDER(R) rax "));
        });
    }
#endif

    [Test]
    public static void MinimalTraversalResetsAvailabilityAtBlockBoundary()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateBlocks(compiler, BBJ_ALWAYS, BBJ_RETURN);
            compiler.fgPredsComputed = true;
            blocks[0].SetKindAndTargetEdge(BBJ_ALWAYS, Connect(compiler, blocks[0], blocks[1]));
            SetBlockSequence(allocator);

            CurrentBlockNumber(allocator) = (uint)blocks[0].bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);
            var firstInterval = NewInterval(allocator, TYP_INT);
            var firstTree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(firstInterval, 2, RefType.RefTypeDef, firstTree, SRBM_RAX);
            _ = allocator.newRefPosition(firstInterval, 4, RefType.RefTypeUse, firstTree, SRBM_RAX);

            CurrentBlockNumber(allocator) = (uint)blocks[1].bbNum;
            _ = allocator.newRefPosition(null, 8, RefType.RefTypeBB, null, SRBM_NONE);
            var secondInterval = NewInterval(allocator, TYP_INT);
            var secondTree = compiler.gtNewIconNode(TYP_INT, 2);
            var secondDefinition = allocator.newRefPosition(
                secondInterval, 10, RefType.RefTypeDef, secondTree, SRBM_RBX);
            var secondUse = allocator.newRefPosition(
                secondInterval, 12, RefType.RefTypeUse, secondTree, SRBM_RBX);

            AllocateRegistersMinimal(allocator);

            Assert.That(firstInterval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(secondDefinition.registerAssignment, Is.EqualTo(SRBM_RBX));
            Assert.That(secondUse.registerAssignment, Is.EqualTo(SRBM_RBX));
            Assert.That(secondInterval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(CurrentBlockNumber(allocator), Is.EqualTo((uint)blocks[1].bbNum));
            Assert.That(CurrentAllocationLocation(allocator), Is.EqualTo(12u));
        });
    }

    [Test]
    public static void MinimalTraversalProcessesKillBeforeReloadingFollowingUse()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(interval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var preKillUse = allocator.newRefPosition(interval, 10, RefType.RefTypeUse, tree, SRBM_RAX);
            var kill = AddKillForRegs(allocator, new regMaskTP(SRBM_RAX), 20);
            var postKillUse = allocator.newRefPosition(interval, 30, RefType.RefTypeUse, tree, SRBM_RAX);
            preKillUse.lastUse = false;

            AllocateRegistersMinimal(allocator);

            Assert.That(kill.refType, Is.EqualTo(RefType.RefTypeKill));
            Assert.That(preKillUse.spillAfter, Is.True);
            Assert.That(interval.isSpilled, Is.True);
            Assert.That(postKillUse.reload, Is.True);
            Assert.That(interval.isActive, Is.False);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
        });
    }

    [Test]
    public static void MinimalTraversalSpillsGcReferencesAtGcKill()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var interval = NewInterval(allocator, TYP_REF);
            var tree = compiler.gtNewIconNode(TYP_REF, 1);
            _ = allocator.newRefPosition(interval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var preKillUse = allocator.newRefPosition(interval, 10, RefType.RefTypeUse, tree, SRBM_RAX);
            var gcKill = allocator.newRefPosition(
                null, 20, RefType.RefTypeKillGCRefs, null, SRBM_RAX);
            var postKillUse = allocator.newRefPosition(interval, 30, RefType.RefTypeUse, tree, SRBM_RAX);
            preKillUse.lastUse = false;

            AllocateRegistersMinimal(allocator);

            Assert.That(gcKill.refType, Is.EqualTo(RefType.RefTypeKillGCRefs));
            Assert.That(preKillUse.spillAfter, Is.True);
            Assert.That(interval.isSpilled, Is.True);
            Assert.That(postKillUse.reload, Is.True);
            Assert.That(interval.isActive, Is.False);
        });
    }

    [Test]
    public static void MinimalTraversalAllocatesAndFreesCopyRegisterWithNativeMoveState()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var interval = NewInterval(allocator, TYP_INT);
            interval.hasInterferingUses = true;
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(
                interval, 2, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            var copyUse = allocator.newRefPosition(interval, 4, RefType.RefTypeUse, tree, SRBM_RBX);
            allocator.physRegs[(int)regNumber.REG_RAX].regOrder = 0;
            allocator.physRegs[(int)regNumber.REG_RBX].regOrder = 1;

            AllocateRegistersMinimal(allocator);

            Assert.That(copyUse.registerAssignment, Is.EqualTo(SRBM_RBX));
            Assert.That(copyUse.copyReg, Is.False);
            Assert.That(copyUse.moveReg, Is.True);
            Assert.That(interval.isActive, Is.False);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval, Is.Null);
            Assert.That(allocator.physRegs[(int)regNumber.REG_RBX].assignedInterval, Is.Null);
        });
    }

    [Test]
    public static void MinimalTraversalHonorsDelayedLastUseAcrossTheNextReferenceLocation()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var delayedInterval = NewInterval(allocator, TYP_INT);
            var delayedTree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(delayedInterval, 2, RefType.RefTypeDef, delayedTree, SRBM_RAX);
            var delayedUse = allocator.newRefPosition(
                delayedInterval, 5, RefType.RefTypeUse, delayedTree, SRBM_RAX);
            delayedUse.delayRegFree = true;

            var nextInterval = NewInterval(allocator, TYP_INT);
            var nextTree = compiler.gtNewIconNode(TYP_INT, 2);
            var nextDefinition = allocator.newRefPosition(nextInterval, 6, RefType.RefTypeDef, nextTree, SRBM_RBX);
            var nextUse = allocator.newRefPosition(nextInterval, 7, RefType.RefTypeUse, nextTree, SRBM_RBX);

            AllocateRegistersMinimal(allocator);

            Assert.That(delayedUse.lastUse, Is.True);
            Assert.That(delayedInterval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(nextDefinition.registerAssignment, Is.EqualTo(SRBM_RBX));
            Assert.That(nextUse.registerAssignment, Is.EqualTo(SRBM_RBX));
            Assert.That(nextInterval.physReg, Is.EqualTo(regNumber.REG_NA));
        });
    }

    [Test]
    public static void MinimalTraversalReusesRegisterAfterLastUseAtLaterLocation()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var firstInterval = NewInterval(allocator, TYP_INT);
            var firstTree = compiler.gtNewIconNode(TYP_INT, 1);
            var firstDefinition = allocator.newRefPosition(
                firstInterval, 2, RefType.RefTypeDef, firstTree, SRBM_RAX);
            var firstUse = allocator.newRefPosition(
                firstInterval, 4, RefType.RefTypeUse, firstTree, SRBM_RAX);

            var secondInterval = NewInterval(allocator, TYP_INT);
            var secondTree = compiler.gtNewIconNode(TYP_INT, 2);
            var secondDefinition = allocator.newRefPosition(
                secondInterval, 6, RefType.RefTypeDef, secondTree, SRBM_RAX | SRBM_RBX);
            var secondUse = allocator.newRefPosition(
                secondInterval, 8, RefType.RefTypeUse, secondTree, SRBM_RAX | SRBM_RBX);

            AllocateRegistersMinimal(allocator);

            Assert.That(firstDefinition.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(firstUse.lastUse, Is.True);
            Assert.That(secondDefinition.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(secondUse.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(firstInterval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(secondInterval.physReg, Is.EqualTo(regNumber.REG_NA));
        });
    }

    [Test]
    public static void MinimalTraversalHonorsDelayedFreeAndReleasesRegisterAfterInterferingLocation()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var delayedInterval = NewInterval(allocator, TYP_INT);
            var delayedTree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(
                delayedInterval, 2, RefType.RefTypeDef, delayedTree, SRBM_RAX);
            var delayedUse = allocator.newRefPosition(
                delayedInterval, 4, RefType.RefTypeUse, delayedTree, SRBM_RAX);
            delayedUse.delayRegFree = true;

            var interferingInterval = NewInterval(allocator, TYP_INT);
            var interferingTree = compiler.gtNewIconNode(TYP_INT, 2);
            var interferingDefinition = allocator.newRefPosition(
                interferingInterval, 5, RefType.RefTypeDef, interferingTree, SRBM_RAX | SRBM_RBX);

            var laterInterval = NewInterval(allocator, TYP_INT);
            var laterTree = compiler.gtNewIconNode(TYP_INT, 3);
            var laterDefinition = allocator.newRefPosition(
                laterInterval, 6, RefType.RefTypeDef, laterTree, SRBM_RAX | SRBM_RBX);

            AllocateRegistersMinimal(allocator);

            Assert.That(delayedUse.lastUse, Is.True);
            Assert.That(interferingDefinition.registerAssignment, Is.EqualTo(SRBM_RBX));
            Assert.That(laterDefinition.registerAssignment, Is.EqualTo(SRBM_RAX));
        });
    }

    [Test]
    public static void MinimalTraversalMakesTemporaryCopyRegisterAvailableAtNextLocation()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var copiedInterval = NewInterval(allocator, TYP_INT);
            copiedInterval.hasInterferingUses = true;
            var copiedTree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(
                copiedInterval, 2, RefType.RefTypeDef, copiedTree, SRBM_RAX | SRBM_RBX);
            var copyUse = allocator.newRefPosition(
                copiedInterval, 4, RefType.RefTypeUse, copiedTree, SRBM_RBX);
            copyUse.lastUse = false;

            var reuseInterval = NewInterval(allocator, TYP_INT);
            var reuseTree = compiler.gtNewIconNode(TYP_INT, 2);
            var reuseDefinition = allocator.newRefPosition(
                reuseInterval, 6, RefType.RefTypeDef, reuseTree, SRBM_RBX | SRBM_RCX);
            var copiedUse = allocator.newRefPosition(
                copiedInterval, 8, RefType.RefTypeUse, copiedTree, SRBM_RAX);
            copiedUse.outOfOrder = true;
            allocator.physRegs[(int)regNumber.REG_RAX].regOrder = 0;
            allocator.physRegs[(int)regNumber.REG_RBX].regOrder = 1;
            allocator.physRegs[(int)regNumber.REG_RCX].regOrder = 2;

            AllocateRegistersMinimal(allocator);

            Assert.That(copyUse.moveReg, Is.True);
            Assert.That(reuseDefinition.registerAssignment, Is.EqualTo(SRBM_RBX));
        });
    }

#if DEBUG
    [Test]
    public static void MinimalTraversalStressSpillPrecedesNextReferenceAndPreservesReload()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var firstInterval = NewInterval(allocator, TYP_INT);
            var firstTree = compiler.gtNewIconNode(TYP_INT, 1);
            var firstDefinition = allocator.newRefPosition(
                firstInterval, 2, RefType.RefTypeDef, firstTree, SRBM_RAX);

            var interveningInterval = NewInterval(allocator, TYP_INT);
            var interveningTree = compiler.gtNewIconNode(TYP_INT, 2);
            var interveningDefinition = allocator.newRefPosition(
                interveningInterval, 4, RefType.RefTypeDef, interveningTree, SRBM_RAX);
            var firstUse = allocator.newRefPosition(
                firstInterval, 8, RefType.RefTypeUse, firstTree, SRBM_RAX);
            StressMask(allocator) = 0x800;

            AllocateRegistersMinimal(allocator);

            Assert.That(firstDefinition.spillAfter, Is.True);
            Assert.That(firstUse.reload, Is.True);
            Assert.That(interveningDefinition.registerAssignment, Is.EqualTo(SRBM_RAX));
        });
    }
#endif

#if DEBUG
    [Test]
    public static void MinimalTraversalHonorsOptionalRegisterNoAllocationStress()
    {
        WithAllocator((compiler, allocator) => {
            var block = CreateBlocks(compiler, BBJ_RETURN)[0];
            compiler.fgPredsComputed = true;
            SetBlockSequence(allocator);
            CurrentBlockNumber(allocator) = (uint)block.bbNum;
            _ = allocator.newRefPosition(null, 0, RefType.RefTypeBB, null, SRBM_NONE);

            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 2, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            definition.setRegOptional(true);
            StressMask(allocator) = 0x1000;

            AllocateRegistersMinimal(allocator);

            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_NONE));
            Assert.That(interval.isSpilled, Is.True);
            Assert.That(interval.isActive, Is.False);
        });
    }
#endif

    private static Interval NewInterval(LinearScan allocator, var_types type) =>
        NewIntervalCore(allocator, type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewIntervalCore(LinearScan allocator, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "allocateRegistersMinimal")]
    private static extern void AllocateRegistersMinimal(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "addKillForRegs")]
    private static extern RefPosition AddKillForRegs(
        LinearScan allocator, regMaskTP registers, uint location);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setBlockSequence")]
    private static extern void SetBlockSequence(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpLsraIntervals")]
    private static extern void DumpLsraIntervals(LinearScan allocator, string message);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpRefPositions")]
    private static extern void DumpRefPositions(LinearScan allocator, string title);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "dumpVarRefPositions")]
    private static extern void DumpVarRefPositions(LinearScan allocator, string title);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enregisterLocalVars")]
    private static extern ref bool EnregisterLocalVars(LinearScan allocator);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlockNumber")]
    private static extern ref uint CurrentBlockNumber(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentAllocationLocation")]
    private static extern ref uint CurrentAllocationLocation(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);
#endif
    private static BasicBlock[] CreateBlocks(Compiler compiler, params BBKinds[] kinds)
    {
        var blocks = new BasicBlock[kinds.Length];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = BasicBlock.New(compiler, kinds[index]);
            blocks[index].bbRefs = index == 0 ? 1 : 0;
            if (index > 0)
            {
                blocks[index - 1].Next = blocks[index];
                blocks[index].Prev = blocks[index - 1];
            }
        }

        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        compiler.fgBBcount = blocks.Length;
        compiler.fgBBNumMax = blocks[^1].bbNum;
        return blocks;
    }

    private static FlowEdge Connect(Compiler compiler, BasicBlock source, BasicBlock target)
    {
        var edge = compiler.fgAddRefPred(target, source);
        edge.Likelihood = 1;
        return edge;
    }

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool evex = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT;
        compiler.compFloatingPointUsed = true;
        if (evex)
        {
            compiler.opts.compSupportsISA.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
        }
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        codeGen.RegSet.rsClearRegsModified();

        try
        {
            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);

            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

#if DEBUG
    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);
}
