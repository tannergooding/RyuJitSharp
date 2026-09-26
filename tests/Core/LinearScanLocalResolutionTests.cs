// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LinearScanLocalResolutionTests
{
    [Test]
    public static void StableHomeAndChangedHomeUpdateDescriptorAndRegisterRecords()
    {
        WithResolution(1, (compiler, allocator, block, interval) =>
        {
            var first = new GenTreeLclVar(TYP_INT, 0, compiler.gtNewIconNode(TYP_INT, 1));
            var firstReference = MakeReference(interval, first, RefType.RefTypeDef, REG_RCX);
            Resolve(allocator, block, first, firstReference);

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_RCX));
            Assert.That(interval.physReg, Is.EqualTo(REG_RCX));
            Assert.That(interval.isActive, Is.True);
            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval, Is.SameAs(interval));
            Assert.That(interval.recentRefPosition, Is.SameAs(firstReference));

            var second = new GenTreeLclVar(TYP_INT, 0);
            var secondReference = MakeReference(interval, second, RefType.RefTypeDef, REG_RDX);
            Resolve(allocator, block, second, secondReference);

            Assert.That(interval.isSplit, Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval, Is.Null);
            Assert.That(allocator.physRegs[(int)REG_RDX].assignedInterval, Is.SameAs(interval));
            Assert.That(interval.physReg, Is.EqualTo(REG_RDX));
        });
    }

    [Test]
    public static void NonLastReferenceClearsStaleTreeLastUse()
    {
        WithResolution(1, (compiler, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0, compiler.gtNewIconNode(TYP_INT, 1));
            tree.SetLastUse(0, true);
            var reference = MakeReference(interval, tree, RefType.RefTypeDef, REG_RCX);

            Resolve(allocator, block, tree, reference);

            Assert.That(tree.IsLastUse(0), Is.False);
            Assert.That(interval.isActive, Is.True);
        });
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void CopyUsesHomeRegisterAndOnlyMaterializesNonfixedCopies(
        bool fixedRegister, bool expectedCopy)
    {
        WithResolution(1, (compiler, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0);
            var other = compiler.gtNewIconNode(TYP_INT, 1);
            var parent = new GenTreeOp(GT_ADD, TYP_INT, tree, other);
            block.InsertAtEnd(tree);
            block.InsertAtEnd(other);
            block.InsertAtEnd(parent);
            var reference = MakeReference(interval, tree, RefType.RefTypeUse, REG_RDX);
            reference.copyReg = true;
            reference.isFixedRegRef = fixedRegister;
            Assign(allocator, allocator.physRegs[(int)REG_RCX], interval);

            Resolve(allocator, block, tree, reference);

            Assert.That(tree.RegNum, Is.EqualTo(REG_RCX));
            Assert.That(interval.physReg, Is.EqualTo(REG_RCX));
            Assert.That(interval.assignedReg, Is.SameAs(allocator.physRegs[(int)REG_RCX]));
            Assert.That(allocator.physRegs[(int)REG_RDX].assignedInterval, Is.Null);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(parent.Op1.Oper is GT_COPY, Is.EqualTo(expectedCopy));
            if (expectedCopy)
            {
                var copy = parent.Op1.AsCopyOrReload();
                Assert.That(copy.Op1, Is.SameAs(tree));
                Assert.That(copy.RegNum, Is.EqualTo(REG_RDX));
                Assert.That(copy.IsLastUse(0), Is.True);
                Assert.That(tree.Next, Is.SameAs(copy));
            }
        });
    }

    [Test]
    public static void MoveTransfersHomeAndWrapsOwningUse()
    {
        WithResolution(1, (compiler, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0);
            var parent = new GenTreeUnOp(GT_NEG, TYP_INT, tree);
            block.InsertAtEnd(tree);
            block.InsertAtEnd(parent);
            var reference = MakeReference(interval, tree, RefType.RefTypeUse, REG_RDX);
            reference.moveReg = true;
            interval.isSplit = true;
            Assign(allocator, allocator.physRegs[(int)REG_RCX], interval);

            Resolve(allocator, block, tree, reference);

            Assert.That(tree.RegNum, Is.EqualTo(REG_RCX));
            Assert.That(interval.physReg, Is.EqualTo(REG_RDX));
            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval, Is.Null);
            Assert.That(allocator.physRegs[(int)REG_RDX].assignedInterval, Is.SameAs(interval));
            Assert.That(parent.Op1.Oper, Is.EqualTo(GT_COPY));
            Assert.That(parent.Op1.AsCopyOrReload().Op1, Is.SameAs(tree));
        });
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    public static void ReloadAndLastUsePreserveStackHomeAndSpillFlags(
        bool spillAfter, bool lastUse, bool expectActive)
    {
        WithResolution(1, (compiler, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0);
            var reference = MakeReference(interval, tree, RefType.RefTypeUse, REG_RCX);
            reference.spillAfter = spillAfter;
            reference.lastUse = lastUse;
            interval.isSpilled = true;

            Resolve(allocator, block, tree, reference);

            Assert.That(reference.reload, Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(tree.IsLastUse(0), Is.EqualTo(lastUse));
            Assert.That((tree.Flags & GTF_SPILLED) != 0, Is.True);
            Assert.That((tree.Flags & GTF_SPILL) != 0, Is.EqualTo(spillAfter));
            Assert.That(interval.isActive, Is.EqualTo(expectActive));
            Assert.That(interval.physReg, Is.EqualTo(expectActive ? REG_RCX : REG_NA));
            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval,
                expectActive ? Is.SameAs(interval) : Is.Null);
        });
    }

    [Test]
    public static void OptionalLastUseOfPredecessorSpillBecomesContained()
    {
        WithResolution(1, (compiler, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0);
            var reference = MakeReference(interval, tree, RefType.RefTypeUse, REG_RCX);
            reference.regOptional = true;
            reference.lastUse = true;
            interval.isSpilled = true;

            Resolve(allocator, block, tree, reference);

            Assert.That(reference.registerAssignment, Is.EqualTo(SRBM_NONE));
            Assert.That(tree.IsContained, Is.True);
            Assert.That(tree.RegNum, Is.EqualTo(REG_NA));
            Assert.That(interval.isActive, Is.False);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }

    [Test]
    public static void RegisterOptionalUseWithoutAssignmentClearsPreviousHome()
    {
        WithResolution(1, (compiler, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0);
            var reference = MakeReference(interval, tree, RefType.RefTypeUse, REG_RCX);
            reference.registerAssignment = SRBM_NONE;
            reference.regOptional = true;
            interval.isSpilled = true;
            Assign(allocator, allocator.physRegs[(int)REG_RCX], interval);
            compiler.lvaTable[0].RegNum = REG_RCX;

            Resolve(allocator, block, tree, reference);

            Assert.That(tree.IsContained, Is.True);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(interval.physReg, Is.EqualTo(REG_NA));
            Assert.That(interval.assignedReg, Is.Null);
            Assert.That(interval.isActive, Is.False);
            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval, Is.Null);
        });
    }

    [Test]
    public static void OptionalReloadAndImmediateSpillUseMemoryDirectly()
    {
        WithResolution(1, (compiler, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0);
            var reference = MakeReference(interval, tree, RefType.RefTypeUse, REG_RCX);
            reference.reload = true;
            reference.spillAfter = true;
            reference.regOptional = true;
            interval.isSpilled = true;

            Resolve(allocator, block, tree, reference);

            Assert.That(tree.IsContained, Is.True);
            Assert.That(tree.RegNum, Is.EqualTo(REG_NA));
            Assert.That((tree.Flags & GTF_SPILL) != 0, Is.False);
            Assert.That((tree.Flags & GTF_SPILLED) != 0, Is.False);
            Assert.That(interval.isActive, Is.False);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PureDefinitionSpillWritesStackDirectly(bool singleDefinition)
    {
        WithResolution(1, (compiler, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0, compiler.gtNewIconNode(TYP_INT, 1));
            var reference = MakeReference(interval, tree, RefType.RefTypeDef, REG_RCX);
            reference.spillAfter = true;
            reference.singleDefSpill = singleDefinition;
            interval.isSpilled = true;

            Resolve(allocator, block, tree, reference);

            Assert.That(tree.RegNum, Is.EqualTo(REG_NA));
            Assert.That((tree.Flags & GTF_SPILL) != 0, Is.False);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(compiler.lvaTable[0].lvSpillAtSingleDef, Is.EqualTo(singleDefinition));
            Assert.That(interval.isActive, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultiRegisterStoreWithScalarSourceSpillsAfterExtraction(bool singleDefinition)
    {
        WithResolution(2, (compiler, allocator, block, _) =>
        {
            var tree = new GenTreeLclVar(TYP_STRUCT, 0, compiler.gtNewIconNode(TYP_INT, 3));
            tree.SetMultiReg();
            var interval = new Interval(TYP_INT, SRBM_RCX)
            {
                isLocalVar = true,
                varNum = 1,
                isSpilled = true,
            };
            var reference = MakeReference(interval, tree, RefType.RefTypeDef, REG_RCX);
            reference.spillAfter = true;
            reference.singleDefSpill = singleDefinition;

            Resolve(allocator, block, tree, reference);

            Assert.That((tree.Flags & GTF_SPILL) != 0, Is.True);
            Assert.That((tree.Flags & GTF_SPILLED) != 0, Is.EqualTo(singleDefinition));
            Assert.That(compiler.lvaTable[1].lvSpillAtSingleDef, Is.EqualTo(singleDefinition));
            Assert.That(compiler.lvaTable[1].RegNum, Is.EqualTo(REG_STK));
            Assert.That(interval.isActive, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void WriteThroughDefinitionPreservesRegisterOnlyWhenLive(bool lastUse)
    {
        WithResolution(1, (compiler, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0, compiler.gtNewIconNode(TYP_INT, 2));
            var reference = MakeReference(interval, tree, RefType.RefTypeDef, REG_RCX);
            reference.writeThru = true;
            reference.lastUse = lastUse;

            Resolve(allocator, block, tree, reference);

            Assert.That((tree.Flags & GTF_SPILL) != 0, Is.True);
            Assert.That((tree.Flags & GTF_SPILLED) != 0, Is.EqualTo(!lastUse));
            Assert.That(interval.isActive, Is.EqualTo(!lastUse));
            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval,
                lastUse ? Is.Null : Is.SameAs(interval));
        });
    }

    [Test]
    public static void ExposedUseWithoutTreePreservesCompletedReload()
    {
        WithResolution(1, (compiler, allocator, _, interval) =>
        {
            var reference = MakeReference(interval, null, RefType.RefTypeExpUse, REG_RCX);
            reference.reload = true;
            interval.isSpilled = true;

            Resolve(allocator, null, null, reference);

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_STK));
            Assert.That(interval.isActive, Is.True);
            Assert.That(interval.physReg, Is.EqualTo(REG_RCX));
            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval, Is.SameAs(interval));
        });
    }

    [Test]
    public static void PromotedMultiRegisterParentReceivesFieldHome()
    {
        WithResolution(3, (compiler, allocator, block, _) =>
        {
            compiler.lvaEnregMultiRegVars = true;
            compiler.lvaTable[0].lvPromoted = true;
            compiler.lvaTable[0].lvFieldLclStart = 1;
            compiler.lvaTable[0].lvFieldCnt = 2;
            var tree = new GenTreeLclVar(TYP_STRUCT, 0);
            tree.SetMultiReg();
            var interval = new Interval(TYP_INT, SRBM_RCX | SRBM_RDX)
            {
                isLocalVar = true,
                varNum = 2,
            };
            var reference = MakeReference(interval, tree, RefType.RefTypeUse, REG_RDX);
            reference.copyReg = true;
            reference.isFixedRegRef = true;
            reference.setMultiRegIdx(1);
            Assign(allocator, allocator.physRegs[(int)REG_RCX], interval);

            Resolve(allocator, block, tree, reference);

            Assert.That(tree.GetRegNumByIdx(1), Is.EqualTo(REG_RCX));
            Assert.That(interval.physReg, Is.EqualTo(REG_RCX));
            Assert.That(allocator.physRegs[(int)REG_RCX].assignedInterval, Is.SameAs(interval));
        });
    }

    [Test]
    public static void ExistingMultiRegisterCopyReceivesOnlyTheRequestedRegister()
    {
        WithResolution(2, (compiler, allocator, block, _) =>
        {
            compiler.lvaEnregMultiRegVars = true;
            compiler.lvaTable[0].lvPromoted = true;
            compiler.lvaTable[0].lvFieldLclStart = 1;
            compiler.lvaTable[0].lvFieldCnt = 1;
            var tree = new GenTreeLclVar(TYP_STRUCT, 0);
            tree.SetMultiReg();
            var copy = new GenTreeCopyOrReload(GT_COPY, TYP_STRUCT, tree);
            block.InsertAtEnd(tree);
            block.InsertAtEnd(copy);
            var interval = new Interval(TYP_INT, SRBM_RCX | SRBM_RDX)
            {
                isLocalVar = true,
                varNum = 1,
            };
            var reference = MakeReference(interval, tree, RefType.RefTypeUse, REG_RDX);
            reference.copyReg = true;
            Assign(allocator, allocator.physRegs[(int)REG_RCX], interval);

            Resolve(allocator, block, tree, reference);

            Assert.That(tree.RegNum, Is.EqualTo(REG_RCX));
            Assert.That(copy.RegNum, Is.EqualTo(REG_RDX));
            Assert.That(copy.Op1, Is.SameAs(tree));
            Assert.That(tree.Next, Is.SameAs(copy));
            Assert.That(interval.physReg, Is.EqualTo(REG_RCX));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void InvalidResolutionModeOrOwnershipFailsBeforeMutatingInterval(bool disableEnregistration)
    {
        WithResolution(1, (_, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0);
            var reference = MakeReference(interval, tree, RefType.RefTypeUse, REG_RCX);
            if (disableEnregistration)
            {
                EnregisterLocals(allocator) = false;
            }

            var exception = Assert.Throws<FatalJitException>(() =>
                Resolve(allocator, disableEnregistration ? block : null, tree, reference));

            Assert.That(exception!.Message, Does.Contain("requires matching block/node ownership"));
            Assert.That(interval.recentRefPosition, Is.Null);
            Assert.That(interval.isActive, Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void ExtendedLifetimeStressLeavesPrecomputedTreeLastUseUntouched()
    {
        WithResolution(1, (_, allocator, block, interval) =>
        {
            var tree = new GenTreeLclVar(TYP_INT, 0);
            tree.SetLastUse(0, true);
            var reference = MakeReference(interval, tree, RefType.RefTypeUse, REG_RCX);
            StressMask(allocator) = 0x80;
            Assign(allocator, allocator.physRegs[(int)REG_RCX], interval);

            Resolve(allocator, block, tree, reference);

            Assert.That(reference.lastUse, Is.False);
            Assert.That(tree.IsLastUse(0), Is.True);
            Assert.That(interval.isActive, Is.True);
        });
    }
#endif

    private static RefPosition MakeReference(Interval interval, GenTreeLclVar? tree,
        RefType type, regNumber register)
    {
        var reference = new RefPosition(1, 2, tree, type)
        {
            registerAssignment = genSingleTypeRegMask(register),
        };
        reference.setInterval(interval);
        interval.firstRefPosition = reference;
        return reference;
    }

    private static void WithResolution(int count, Action<Compiler, LinearScan, BasicBlock, Interval> action)
    {
        LinearScanLocalCandidatesTests.WithCandidates(count, (compiler, allocator) =>
        {
            compiler.lvaTrackedFixed = true;
            compiler.fgBBNumMax = 1;
            compiler.codeGen!.RegSet.rsClearRegsModified();
            foreach (ref var local in compiler.lvaTable.AsSpan())
            {
                local.RegNum = REG_STK;
                local.lvLRACandidate = true;
            }
            var block = new BasicBlock(null, null) { bbNum = 1 };
            var interval = new Interval(TYP_INT, SRBM_RCX | SRBM_RDX)
            {
                isLocalVar = true,
                varNum = 0,
            };
            CurrentBlockNumber(allocator) = 1;
            MaxBlockBeforeResolution(allocator) = 1;
            BlockInfo(allocator) = new LsraBlockInfo[2];
            BlockInfo(allocator)![1].weight = BB_UNITY_WEIGHT;
            BuildPhysicalRegisters(allocator);
            allocator.initVarRegMaps();
            action(compiler, allocator, block, interval);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "resolveLocalRef")]
    private static extern void Resolve(LinearScan allocator, BasicBlock? block,
        GenTreeLclVar? tree, RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignPhysReg")]
    private static extern void Assign(LinearScan allocator, RegRecord record, Interval interval);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysicalRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBlockNumber")]
    private static extern ref uint CurrentBlockNumber(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint MaxBlockBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enregisterLocalVars")]
    private static extern ref bool EnregisterLocals(LinearScan allocator);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);
#endif
}
