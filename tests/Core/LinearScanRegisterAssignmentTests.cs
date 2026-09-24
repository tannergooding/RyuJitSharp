// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using SingleTypeRegSet = RyuJitSharp.regMask;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanRegisterAssignmentTests
{
    [Test]
    public static void AssignmentAndUnassignmentMaintainPhysicalRegisterState()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
            var register = allocator.physRegs[(int)regNumber.REG_RAX];

            AssignPhysReg(allocator, register, interval);

            Assert.That(register.assignedInterval, Is.SameAs(interval));
            Assert.That(interval.assignedReg, Is.SameAs(register));
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_RAX));
            Assert.That(interval.isActive, Is.True);
            Assert.That(IsAssigned(allocator, register, interval.registerType), Is.True);
            Assert.That(GetFreeCandidates(allocator, SRBM_RAX, interval.registerType), Is.EqualTo(SRBM_NONE));

            interval.isActive = false;
            UnassignPhysReg(allocator, register, interval.registerType);

            Assert.That(register.assignedInterval, Is.Null);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(IsAssigned(allocator, register, interval.registerType), Is.False);
            Assert.That(GetFreeCandidates(allocator, SRBM_RAX, interval.registerType), Is.EqualTo(SRBM_RAX));
            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RAX));
        });
    }

    [Test]
    public static void UnassigningCopyPreservesIntervalsPrimaryRegister()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
            var copyRegister = allocator.physRegs[(int)regNumber.REG_RAX];
            var primaryRegister = allocator.physRegs[(int)regNumber.REG_RBX];
            copyRegister.assignedInterval = interval;
            primaryRegister.assignedInterval = interval;
            interval.assignedReg = primaryRegister;
            interval.physReg = regNumber.REG_RBX;

            UnassignPhysReg(allocator, copyRegister, interval.registerType);

            Assert.That(copyRegister.assignedInterval, Is.Null);
            Assert.That(primaryRegister.assignedInterval, Is.SameAs(interval));
            Assert.That(interval.assignedReg, Is.SameAs(primaryRegister));
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_RBX));
            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RAX));
        });
    }

    [Test]
    public static void UnassigningLiveIntervalMarksSpillAndKeepsNextReferenceOwnership()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
            var nextUse = allocator.newRefPosition(interval, 20, RefType.RefTypeUse, tree, SRBM_RAX);
            interval.recentRefPosition = definition;
            allocator.setCurrentBlockStartLocation(0);

            var register = allocator.physRegs[(int)regNumber.REG_RAX];
            AssignPhysReg(allocator, register, interval);
            UnassignPhysReg(allocator, register, interval.registerType);

            Assert.That(definition.spillAfter, Is.True);
            Assert.That(interval.isSpilled, Is.True);
            Assert.That(interval.isActive, Is.False);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(interval.assignedReg, Is.SameAs(register));
            Assert.That(register.assignedInterval, Is.Null);
            Assert.That(definition.nextRefPosition, Is.SameAs(nextUse));
        });
    }

    [Test]
    public static void UnassigningIntervalRestoresPreviousIntervalWithFutureReferences()
    {
        WithAllocator((compiler, allocator) => {
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var previousInterval = NewInterval(allocator, TYP_INT);
            var previousDefinition = allocator.newRefPosition(
                previousInterval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            _ = allocator.newRefPosition(previousInterval, 5, RefType.RefTypeUse, tree, SRBM_RAX);
            previousInterval.recentRefPosition = previousDefinition;

            var interval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
            var register = allocator.physRegs[(int)regNumber.REG_RAX];
            AssignPhysReg(allocator, register, interval);
            register.previousInterval = previousInterval;
            previousInterval.assignedReg = register;
            previousInterval.physReg = regNumber.REG_RAX;
            interval.isActive = false;

            UnassignPhysReg(allocator, register, interval.registerType);

            Assert.That(register.assignedInterval, Is.SameAs(previousInterval));
            Assert.That(register.previousInterval, Is.Null);
            Assert.That(previousInterval.assignedReg, Is.SameAs(register));
            Assert.That(
                GetNextIntervalRef(allocator, regNumber.REG_RAX, previousInterval.registerType), Is.EqualTo(5));
        });
    }

    [Test]
    public static void SpillingAtBlockEntryWritesTheCurrentBlockVariableMap()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateTrackedBlocks(compiler);
            Assert.That(StartBlockSequence(allocator), Is.SameAs(blocks[0]));
            var interval = NewInterval(allocator, TYP_INT);
            interval.isLocalVar = true;
            interval.varNum = 0;
            var tree = compiler.gtNewLclvNode(TYP_INT, 0);
            _ = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
            var spillReference = allocator.newRefPosition(interval, 20, RefType.RefTypeUse, tree, SRBM_RAX);
            _ = allocator.newRefPosition(interval, 40, RefType.RefTypeUse, tree, SRBM_RAX);
            interval.recentRefPosition = spillReference;
            allocator.setInVarRegForBB(1, 0, regNumber.REG_RBX);
            Assert.That(MoveToNextBlock(allocator), Is.SameAs(blocks[1]));
            allocator.setCurrentBlockStartLocation(30);

            var register = allocator.physRegs[(int)regNumber.REG_RAX];
            AssignPhysReg(allocator, register, interval);
            UnassignPhysReg(allocator, register, interval.registerType);

            Assert.That(InVarReg(allocator, 1, 0), Is.EqualTo(regNumber.REG_RBX));
            Assert.That(InVarReg(allocator, 2, 0), Is.EqualTo(regNumber.REG_STK));
            Assert.That(interval.isSpilled, Is.True);
            Assert.That(
                BitSetOps<Compiler, TrackedVarBitSetTraits>.IsMember(compiler, SplitOrSpilledVars(allocator)!, 0),
                Is.True);
        }, trackedLocal: true);
    }

    [Test]
    [Platform("Win")]
    public static void PartialSimdSpillUsesNativeSpillWeightWithCurrentLocation()
    {
        WithAllocator((compiler, allocator) => {
            var localInterval = NewInterval(allocator, TYP_FLOAT);
            localInterval.isLocalVar = true;
            localInterval.varNum = 0;
            var local = compiler.gtNewLclvNode(TYP_FLOAT, 0);
            _ = allocator.newRefPosition(localInterval, 5, RefType.RefTypeDef, local, SRBM_XMM6);
            var recentReference = allocator.newRefPosition(
                localInterval, 10, RefType.RefTypeUse, local, SRBM_XMM6);
            localInterval.recentRefPosition = recentReference;
            var register = allocator.physRegs[(int)regNumber.REG_XMM6];
            AssignPhysReg(allocator, register, localInterval);

            var currentLocation = 20u;
            CurrentAllocationLocation(allocator) = currentLocation;
            Assert.That(recentReference.nodeLocation, Is.LessThan(currentLocation));
            var expectedSpillWeight = GetSpillWeight(allocator, register);

            var upperInterval = NewInterval(allocator, TYP_FLOAT);
            IsUpperVector(upperInterval) = true;
            upperInterval.relatedInterval = localInterval;

            SetIntervalAsSpilled(allocator, upperInterval);

            Assert.That(upperInterval.isSpilled, Is.True);
            Assert.That(localInterval.isSpilled, Is.True);
            Assert.That(SpillCost(allocator)[(int)regNumber.REG_XMM6], Is.EqualTo(expectedSpillWeight));
            Assert.That(
                BitSetOps<Compiler, TrackedVarBitSetTraits>.IsMember(compiler, SplitOrSpilledVars(allocator)!, 0),
                Is.True);
        }, trackedLocal: true);
    }

#if DEBUG
    [Test]
    public static void ExtendLifetimeStressKeepsLastUseFromBeingSpilled()
    {
        WithAllocator((compiler, allocator) => {
            var blocks = CreateTrackedBlocks(compiler);
            Assert.That(StartBlockSequence(allocator), Is.SameAs(blocks[0]));
            var interval = NewInterval(allocator, TYP_INT);
            interval.isLocalVar = true;
            interval.varNum = 0;
            var definitionTree = compiler.gtNewLclvNode(TYP_INT, 0);
            _ = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, definitionTree, SRBM_RAX);
            var lastUseTree = compiler.gtNewLclvNode(TYP_INT, 0);
            lastUseTree.SetLastUse(0, true);
            var lastUse = allocator.newRefPosition(interval, 20, RefType.RefTypeUse, lastUseTree, SRBM_RAX);
            _ = allocator.newRefPosition(interval, 40, RefType.RefTypeUse, definitionTree, SRBM_RAX);
            interval.recentRefPosition = lastUse;
            StressMask(allocator) = 0x80;
            Assert.That(MoveToNextBlock(allocator), Is.SameAs(blocks[1]));
            allocator.setCurrentBlockStartLocation(30);

            var register = allocator.physRegs[(int)regNumber.REG_RAX];
            AssignPhysReg(allocator, register, interval);
            UnassignPhysReg(allocator, register, interval.registerType);

            Assert.That(lastUse.lastUse, Is.False);
            Assert.That(lastUse.spillAfter, Is.False);
            Assert.That(interval.isSpilled, Is.True);
            Assert.That(InVarReg(allocator, 2, 0), Is.EqualTo(regNumber.REG_STK));
        }, trackedLocal: true);
    }
#endif

    private static BasicBlock[] CreateTrackedBlocks(Compiler compiler)
    {
        compiler.fgBBNumMax = 0;
        var first = BasicBlock.New(compiler, BBJ_RETURN);
        var second = BasicBlock.New(compiler, BBJ_RETURN);
        first.Next = second;
        second.Prev = first;
        compiler.fgFirstBB = first;
        compiler.fgLastBB = second;
        compiler.fgBBcount = 2;
        compiler.fgBBNumMax = second.bbNum;
        compiler.fgPredsComputed = true;
        return [first, second];
    }

    private static Interval NewInterval(LinearScan allocator, var_types type) => NewIntervalCore(allocator, type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewIntervalCore(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignPhysReg")]
    private static extern void AssignPhysReg(LinearScan allocator, RegRecord register, Interval interval);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "isAssigned")]
    private static extern bool IsAssigned(
        LinearScan allocator, RegRecord register, var_types newRegisterType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "unassignPhysReg")]
    private static extern void UnassignPhysReg(
        LinearScan allocator, RegRecord register, var_types newRegisterType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getFreeCandidates")]
    private static extern SingleTypeRegSet GetFreeCandidates(
        LinearScan allocator, SingleTypeRegSet candidates, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getNextIntervalRef")]
    private static extern uint GetNextIntervalRef(
        LinearScan allocator, regNumber register, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setIntervalAsSpilled")]
    private static extern void SetIntervalAsSpilled(LinearScan allocator, Interval interval);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getSpillWeight")]
    private static extern double GetSpillWeight(LinearScan allocator, RegRecord register);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentAllocationLocation")]
    private static extern ref uint CurrentAllocationLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_spillCost")]
    private static extern ref double[] SpillCost(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "isUpperVector")]
    private static extern ref bool IsUpperVector(Interval interval);

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool trackedLocal = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT | (trackedLocal ? CLFLG_REGVAR : 0);
        compiler.compFloatingPointUsed = true;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        if (trackedLocal)
        {
            compiler.fgBBNumMax = 2;
            compiler.lvaCount = 1;
            compiler.lvaTrackedCount = 1;
            compiler.lvaTrackedCountInSizeTUnits = 1;
            compiler.lvaTrackedFixed = true;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, _varIndex = 0, lvTracked = true }];
        }
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        ref var registerSet = ref codeGen.RegSet;
        registerSet.rsClearRegsModified();

        try
        {
            var allocator = new LinearScan(compiler);
            for (var index = 0; index < (int)regNumber.ACTUAL_REG_COUNT; index++)
            {
                var register = allocator.physRegs[index];
                register.init((regNumber)index);
                register.regOrder = (byte)index;
            }

            if (trackedLocal)
            {
                var blockInfo = new LsraBlockInfo[compiler.fgBBNumMax + 1];
                Array.Fill(blockInfo, new LsraBlockInfo { weight = 1 });
                BlockInfo(allocator) = blockInfo;
                BbNumMaxBeforeResolution(allocator) = (uint)compiler.fgBBNumMax;
                allocator.initVarRegMaps();
            }
            else
            {
                BlockInfo(allocator) = [new LsraBlockInfo { weight = 1 }];
            }

            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint BbNumMaxBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "startBlockSequence")]
    private static extern BasicBlock StartBlockSequence(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "moveToNextBlock")]
    private static extern BasicBlock? MoveToNextBlock(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_splitOrSpilledVars")]
    private static extern ref nint[]? SplitOrSpilledVars(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    private static regNumber InVarReg(LinearScan allocator, uint blockNumber, uint varNumber)
        => allocator.getInVarToRegMap(blockNumber)![(int)varNumber];
}
