// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanRegisterLifetimeTests
{
    [Test]
    public static void NoSpillUnassignmentFreesRegisterWithoutChangingIntervalActivity()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_X64_RAX);
            _ = allocator.newRefPosition(interval, 20, RefType.RefTypeUse, tree, SRBM_X64_RAX);
            interval.recentRefPosition = definition;
            var register = allocator.physRegs[(int)regNumber.REG_X64_RAX];
            AssignPhysReg(allocator, register, interval);

            UnassignPhysRegNoSpill(allocator, register);

            Assert.That(register.assignedInterval, Is.Null);
            Assert.That(interval.isActive, Is.True);
            Assert.That(interval.isSpilled, Is.False);
            Assert.That(definition.spillAfter, Is.False);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_X64_NA));
            Assert.That(GetFreeCandidates(allocator, SRBM_X64_RAX, TYP_INT), Is.EqualTo(SRBM_X64_RAX));
        });
    }

    [Test]
    public static void FreeRegisterRetainsInactiveIntervalWithFutureUse()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_X64_RAX);
            var recentUse = allocator.newRefPosition(interval, 20, RefType.RefTypeUse, tree, SRBM_X64_RAX);
            var nextUse = allocator.newRefPosition(interval, 30, RefType.RefTypeUse, tree, SRBM_X64_RAX);
            interval.recentRefPosition = recentUse;
            var register = allocator.physRegs[(int)regNumber.REG_X64_RAX];
            AssignPhysReg(allocator, register, interval);

            FreeRegister(allocator, register);

            Assert.That(register.assignedInterval, Is.SameAs(interval));
            Assert.That(interval.assignedReg, Is.SameAs(register));
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_X64_RAX));
            Assert.That(interval.isActive, Is.False);
            Assert.That(interval.isSpilled, Is.False);
            Assert.That(recentUse.nextRefPosition, Is.SameAs(nextUse));
            Assert.That(GetFreeCandidates(allocator, SRBM_X64_RAX, TYP_INT), Is.EqualTo(SRBM_X64_RAX));
            Assert.That(NextIntervalRef(allocator)[(int)regNumber.REG_X64_RAX], Is.EqualTo(nextUse.nodeLocation));
            Assert.That(SpillCost(allocator)[(int)regNumber.REG_X64_RAX], Is.EqualTo(0));
        });
    }

    [Test]
    public static void FreeRegisterUnassignsIntervalWhoseNextReferenceIsDefinition()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(interval, 5, RefType.RefTypeDef, tree, SRBM_X64_RAX);
            var recentUse = allocator.newRefPosition(interval, 10, RefType.RefTypeUse, tree, SRBM_X64_RAX);
            _ = allocator.newRefPosition(interval, 20, RefType.RefTypeDef, tree, SRBM_X64_RAX);
            interval.recentRefPosition = recentUse;
            var register = allocator.physRegs[(int)regNumber.REG_X64_RAX];
            AssignPhysReg(allocator, register, interval);

            FreeRegister(allocator, register);

            Assert.That(register.assignedInterval, Is.Null);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_X64_NA));
            Assert.That(interval.isActive, Is.False);
            Assert.That(interval.isSpilled, Is.False);
            Assert.That(GetFreeCandidates(allocator, SRBM_X64_RAX, TYP_INT), Is.EqualTo(SRBM_X64_RAX));
            Assert.That(NextIntervalRef(allocator)[(int)regNumber.REG_X64_RAX], Is.EqualTo(MaxLocation));
        });
    }

    [TestCase(var_types.TYP_LONG, TYP_INT)]
    [TestCase(TYP_INT, var_types.TYP_LONG)]
    [TestCase(TYP_REF, TYP_BYREF)]
    [TestCase(TYP_BYREF, TYP_REF)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT)]
    [TestCase(TYP_FLOAT, TYP_DOUBLE)]
    public static void AllocationAndFreeingShareAvailabilityAcrossTypeAliases(var_types type, var_types alias)
    {
        WithAllocator((compiler, allocator) => {
            var floating = varTypeUsesFloatReg(type);
            var interval = NewInterval(allocator, type);
            GenTree tree = floating ? compiler.gtNewDconNode(type, 1) : compiler.gtNewIconNode(type, 0);
            var mask = floating ? SRBM_X64_XMM0 : SRBM_X64_RAX;
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, mask);
            interval.recentRefPosition = definition;
            var register = allocator.physRegs[(int)(floating ? regNumber.REG_X64_XMM0 : regNumber.REG_X64_RAX)];

            AssignPhysReg(allocator, register, interval);
            Assert.That(GetFreeCandidates(allocator, mask, type), Is.EqualTo(SRBM_NONE));
            Assert.That(GetFreeCandidates(allocator, mask, alias), Is.EqualTo(SRBM_NONE));

            FreeRegister(allocator, register);
            Assert.That(GetFreeCandidates(allocator, mask, type), Is.EqualTo(mask));
            Assert.That(GetFreeCandidates(allocator, mask, alias), Is.EqualTo(mask));
        });
    }

    [Test]
    public static void FreeRegisterRestoresBothFloatingRegisterViews()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_DOUBLE);
            var tree = compiler.gtNewDconNode(TYP_DOUBLE, 1);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_X64_XMM0);
            interval.recentRefPosition = definition;
            var register = allocator.physRegs[(int)regNumber.REG_X64_XMM0];
            AssignPhysReg(allocator, register, interval);

            FreeRegister(allocator, register);

            Assert.That(register.assignedInterval, Is.Null);
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_X64_NA));
            Assert.That(GetFreeCandidates(allocator, SRBM_X64_XMM0, TYP_FLOAT), Is.EqualTo(SRBM_X64_XMM0));
            Assert.That(GetFreeCandidates(allocator, SRBM_X64_XMM0, TYP_DOUBLE), Is.EqualTo(SRBM_X64_XMM0));
        });
    }

    [Test]
    public static void FreeRegistersHandlesIntegerAndMaskedRegisterBanks()
    {
        WithAllocator((compiler, allocator) => {
            var intInterval = NewAssignedInterval(compiler, allocator, TYP_INT, regNumber.REG_X64_RAX);
            var registers = regMaskTP.CreateFromRegNum(regNumber.REG_X64_RAX, SRBM_X64_RAX) |
                regMaskTP.CreateFromRegNum(regNumber.REG_X64_K1, SRBM_X64_K1);

            FreeRegisters(allocator, registers);

            Assert.That(intInterval.assignedReg!.assignedInterval, Is.Null);
            Assert.That(intInterval.physReg, Is.EqualTo(regNumber.REG_X64_NA));
            Assert.That(GetFreeCandidates(allocator, SRBM_X64_RAX, TYP_INT), Is.EqualTo(SRBM_X64_RAX));
            Assert.That(GetFreeCandidates(allocator, SRBM_X64_K1, TYP_MASK), Is.EqualTo(SRBM_X64_K1));
        });
    }

    [Test]
    public static void ResetAllRegistersStateRestoresRegisterAndConstantState()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            interval.isConstant = true;
            var tree = compiler.gtNewIconNode(TYP_INT, 7);
            _ = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_X64_RAX);
            var register = allocator.physRegs[(int)regNumber.REG_X64_RAX];
            AssignPhysReg(allocator, register, interval);
            Assert.That(IsRegConstant(allocator, regNumber.REG_X64_RAX, TYP_INT), Is.True);
            BusyUntilKill(allocator) = new regMaskTP(SRBM_X64_RAX);
            FixedRegsLow(allocator) = SRBM_X64_RBX;

            ResetAllRegistersState(allocator);

            Assert.That(register.assignedInterval, Is.Null);
            Assert.That(IsRegConstant(allocator, regNumber.REG_X64_RAX, TYP_INT), Is.False);
            Assert.That(GetFreeCandidates(allocator, SRBM_X64_RAX, TYP_INT), Is.EqualTo(SRBM_X64_RAX));
            Assert.That(NextIntervalRef(allocator)[(int)regNumber.REG_X64_RAX], Is.EqualTo(MaxLocation));
            Assert.That(SpillCost(allocator)[(int)regNumber.REG_X64_RAX], Is.EqualTo(0));
            Assert.That(BusyUntilKill(allocator), Is.EqualTo(new regMaskTP(SRBM_X64_RAX)));
            Assert.That(FixedRegsLow(allocator), Is.EqualTo(SRBM_X64_RBX));
        });
    }

    [Test]
    public static void FreeBusyStateTracksImmediateAndDelayedLastUseMasks()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(interval, 5, RefType.RefTypeDef, tree, SRBM_X64_RAX | SRBM_X64_RBX);
            var immediate = allocator.newRefPosition(interval, 10, RefType.RefTypeUse, tree, SRBM_X64_RAX);
            immediate.lastUse = true;
            var delayed = allocator.newRefPosition(interval, 20, RefType.RefTypeUse, tree, SRBM_X64_RBX);
            delayed.lastUse = true;
            delayed.delayRegFree = true;
            BusyUntilKill(allocator) = new regMaskTP(SRBM_X64_RCX);
            var registersToFree = new regMaskTP(SRBM_NONE);
            var delayRegistersToFree = new regMaskTP(SRBM_NONE);

            UpdateRegsFreeBusyState(
                allocator, immediate, TYP_INT, SRBM_X64_RAX, ref registersToFree, ref delayRegistersToFree);
            UpdateRegsFreeBusyState(
                allocator, delayed, TYP_INT, SRBM_X64_RBX, ref registersToFree, ref delayRegistersToFree);

            Assert.That(registersToFree, Is.EqualTo(new regMaskTP(SRBM_X64_RAX)));
            Assert.That(delayRegistersToFree, Is.EqualTo(new regMaskTP(SRBM_X64_RBX)));
            Assert.That(RegsInUseThisLocation(allocator), Is.EqualTo(new regMaskTP(SRBM_X64_RAX | SRBM_X64_RBX)));
            Assert.That(RegsInUseNextLocation(allocator), Is.EqualTo(new regMaskTP(SRBM_X64_RBX)));
            Assert.That(BusyUntilKill(allocator), Is.EqualTo(new regMaskTP(SRBM_X64_RCX)));
        });
    }

    [TestCase(TYP_DOUBLE, 0)]
    [TestCase(TYP_FLOAT, 1)]
    public static void VerifyFreeRegistersTreatsDoubleSecondHalfAsPairedState(
        var_types type, int expectedAssertions)
    {
        Assume.That(Configuration.TARGET_ARM);
        Assume.That(TestConfiguration.IsDebug);

        WithAllocator((_, allocator) =>
        {
            allocator.physRegs[(int)REG_F0].assignedInterval = new Interval(type, SRBM_F0)
            {
                physReg = REG_F0,
            };
            SetConstantReg(allocator, REG_F1);

            Assert.That(VerifyWithAssertCounter(allocator, new regMaskTP(SRBM_NONE)),
                Is.EqualTo(expectedAssertions));
        });
    }

    [Test]
    public static void VerifyFreeRegistersAcceptsNativeInactiveRetainedAndReleasedStates()
    {
        Assume.That(TestConfiguration.IsDebug);

        WithAllocator((compiler, allocator) => {
            var retainedInterval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            _ = allocator.newRefPosition(retainedInterval, 10, RefType.RefTypeDef, tree, SRBM_X64_RAX);
            retainedInterval.recentRefPosition =
                allocator.newRefPosition(retainedInterval, 20, RefType.RefTypeUse, tree, SRBM_X64_RAX);
            _ = allocator.newRefPosition(retainedInterval, 30, RefType.RefTypeUse, tree, SRBM_X64_RAX);
            var retainedRegister = allocator.physRegs[(int)regNumber.REG_X64_RAX];
            AssignPhysReg(allocator, retainedRegister, retainedInterval);
            FreeRegister(allocator, retainedRegister);

            var releasedInterval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(releasedInterval, 40, RefType.RefTypeDef, tree, SRBM_X64_RBX);
            releasedInterval.recentRefPosition = definition;
            var releasedRegister = allocator.physRegs[(int)regNumber.REG_X64_RBX];
            AssignPhysReg(allocator, releasedRegister, releasedInterval);
            FreeRegister(allocator, releasedRegister);
            CurrentAllocationLocation(allocator) = 50;

            VerifyFreeRegisters(
                allocator,
                new regMaskTP(SRBM_NONE));
        });
    }

    [Test]
    public static void VerifyFreeRegistersSkipsPendingRegistersAndChecksNonpendingRegisters()
    {
        Assume.That(TestConfiguration.IsDebug);

        WithAllocator((_, allocator) => {
            var pendingRegisters = new regMaskTP(SRBM_X64_RAX);

            SetConstantReg(allocator, regNumber.REG_X64_RAX);
            Assert.That(VerifyWithAssertCounter(allocator, pendingRegisters), Is.Zero);

            SetConstantReg(allocator, regNumber.REG_X64_RBX);
            Assert.That(VerifyWithAssertCounter(allocator, pendingRegisters), Is.EqualTo(1));
        });
    }

    [Test]
    public static void AllocationLifecycleEventsPreserveNativeVerboseDumpGating()
    {
        Assume.That(TestConfiguration.IsDebug);

        WithAllocator((compiler, allocator) => {
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var delayedInterval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(delayedInterval, 5, RefType.RefTypeDef, tree, SRBM_X64_RAX);
            var delayed = allocator.newRefPosition(delayedInterval, 10, RefType.RefTypeUse, tree, SRBM_X64_RAX);
            delayed.lastUse = true;
            delayed.delayRegFree = true;
            delayedInterval.recentRefPosition = delayed;

            var immediateInterval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(immediateInterval, 15, RefType.RefTypeDef, tree, SRBM_X64_RAX);
            var immediate = allocator.newRefPosition(immediateInterval, 20, RefType.RefTypeUse, tree, SRBM_X64_RAX);
            immediate.lastUse = true;
            immediateInterval.recentRefPosition = immediate;
            TestConfiguration.EnableVerbose(compiler);

            var verboseOutput = Capture(() => {
                var registersToFree = new regMaskTP(SRBM_NONE);
                var delayedRegisters = new regMaskTP(SRBM_NONE);
                UpdateRegsFreeBusyState(
                    allocator, delayed, TYP_INT, SRBM_X64_RAX, ref registersToFree, ref delayedRegisters,
                    delayedInterval, regNumber.REG_X64_R10);
                UpdateRegsFreeBusyState(
                    allocator, immediate, TYP_INT, SRBM_X64_RAX, ref registersToFree, ref delayedRegisters,
                    immediateInterval, regNumber.REG_X64_R10);
                FreeRegisters(allocator, new regMaskTP(SRBM_R10));
            });

            Assert.That(verboseOutput, Does.Contain("Allocating Registers"));
            Assert.That(verboseOutput, Does.Contain("r10"));

            TestConfiguration.SetVerbose(compiler, false);
            var quietOutput = Capture(() => {
                var registersToFree = new regMaskTP(SRBM_NONE);
                var delayedRegisters = new regMaskTP(SRBM_NONE);
                UpdateRegsFreeBusyState(
                    allocator, delayed, TYP_INT, SRBM_X64_RAX, ref registersToFree, ref delayedRegisters,
                    delayedInterval, regNumber.REG_X64_R10);
                FreeRegisters(allocator, new regMaskTP(SRBM_R10));
            });

            Assert.That(quietOutput, Is.Empty);
        });
    }

    [Test]
    public static void VerifyFreeRegistersHonorsSpillAlwaysStressForConstantMarker()
    {
        Assume.That(TestConfiguration.IsDebug);

        WithAllocator((_, allocator) => {
            SetConstantReg(allocator, regNumber.REG_X64_RAX);
            StressMask(allocator) = 0x800;

            VerifyFreeRegisters(allocator, new regMaskTP(SRBM_NONE));
        });
    }

    private static Interval NewAssignedInterval(
        Compiler compiler, LinearScan allocator, var_types type, regNumber registerNumber)
    {
        var interval = NewInterval(allocator, type);
        var tree = compiler.gtNewIconNode(type, 1);
        var reference = allocator.newRefPosition(
            interval, 10, RefType.RefTypeDef, tree, genSingleTypeRegMask(registerNumber));
        interval.recentRefPosition = reference;
        AssignPhysReg(allocator, allocator.physRegs[(int)registerNumber], interval);
        return interval;
    }

    private static Interval NewInterval(LinearScan allocator, var_types type) => NewIntervalCore(allocator, type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewIntervalCore(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignPhysReg")]
    private static extern void AssignPhysReg(LinearScan allocator, RegRecord register, Interval interval);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "unassignPhysRegNoSpill")]
    private static extern void UnassignPhysRegNoSpill(LinearScan allocator, RegRecord register);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "freeRegister")]
    private static extern void FreeRegister(LinearScan allocator, RegRecord register);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "freeRegisters")]
    private static extern void FreeRegisters(LinearScan allocator, regMaskTP registers);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "resetAllRegistersState")]
    private static extern void ResetAllRegistersState(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "updateRegsFreeBusyState")]
    private static extern void UpdateRegsFreeBusyState(
        LinearScan allocator,
        RefPosition refPosition,
        var_types registerType,
        regMask registersBusy,
        ref regMaskTP registersToFree,
        ref regMaskTP delayRegistersToFree,
        Interval? interval = null,
        regNumber assignedRegister = regNumber.REG_X64_NA);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "verifyFreeRegisters")]
    private static extern void VerifyFreeRegisters(LinearScan allocator, regMaskTP registersToFree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "isRegConstant")]
    private static extern bool IsRegConstant(LinearScan allocator, regNumber register, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "setConstantReg")]
    private static extern void SetConstantReg(LinearScan allocator, regNumber register);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getFreeCandidates")]
    private static extern regMask GetFreeCandidates(
        LinearScan allocator, regMask candidates, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_nextIntervalRef")]
    private static extern ref uint[] NextIntervalRef(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_spillCost")]
    private static extern ref double[] SpillCost(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsInUseThisLocation")]
    private static extern ref regMaskTP RegsInUseThisLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsInUseNextLocation")]
    private static extern ref regMaskTP RegsInUseNextLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_fixedRegsLow")]
    private static extern ref regMask FixedRegsLow(LinearScan allocator);

    private static int VerifyWithAssertCounter(LinearScan allocator, regMaskTP registersToFree)
    {
        var compiler = JitTls.Compiler ?? throw new AssertionException("Missing assertion compiler.");
        var originalFlags = compiler.opts.jitFlags;
        var flags = *originalFlags;
        compiler.opts.jitFlags = &flags;
        try
        {
            return TestConfiguration.CaptureDebugAssertions(
                () => VerifyFreeRegisters(allocator, registersToFree)).Assertions.Length;
        }
        finally
        {
            compiler.opts.jitFlags = originalFlags;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentAllocationLocation")]
    private static extern ref uint CurrentAllocationLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lsraStressMask")]
    private static extern ref int StressMask(LinearScan allocator);

    private static void WithAllocator(Action<Compiler, LinearScan> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_MINOPT;
        compiler.compFloatingPointUsed = true;
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
            for (var index = 0; index < (int)regNumber.ACTUAL_REG_X64_COUNT; index++)
            {
                var register = allocator.physRegs[index];
                register.init((regNumber)index);
                register.regOrder = (byte)index;
            }

            BlockInfo(allocator) = [new LsraBlockInfo { weight = 1 }];
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static string Capture(Action action)
    {
        return TestConfiguration.CaptureDebugOutput(action);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);
}
