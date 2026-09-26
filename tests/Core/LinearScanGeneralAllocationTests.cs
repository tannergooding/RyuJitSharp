// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class LinearScanGeneralAllocationTests
{
    [TestCase(false, REG_RAX)]
    [TestCase(true, REG_RBX)]
    public static void SelectionUsesCallerOrCalleePreference(bool calleeSave, regNumber expected)
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            interval.preferCalleeSave = calleeSave;
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 7), SRBM_RAX | SRBM_RBX);
            var selected = Allocate(allocator, interval, definition);

            Assert.That(selected, Is.EqualTo(expected));
            Assert.That(interval.physReg, Is.EqualTo(expected));
            Assert.That(interval.isActive, Is.True);
            Assert.That(definition.registerAssignment, Is.EqualTo(genSingleTypeRegMask(expected)));
        });
    }

    [Test]
    public static void WriteThroughPreferenceDoesNotIntroduceUnusedCalleeSave()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            interval.preferCalleeSave = true;
            interval.isWriteThru = true;
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 7), SRBM_RAX | SRBM_RBX);

            Assert.That(Allocate(allocator, interval, definition), Is.EqualTo(REG_RAX));
        });
    }

    [TestCase(TYP_INT, REG_RAX, REG_RBX)]
    [TestCase(TYP_DOUBLE, REG_XMM0, REG_XMM1)]
    [TestCase(TYP_MASK, REG_K1, REG_K2)]
    public static void ConflictingFixedUseRemovesTheCorrectRegisterBank(
        var_types type, regNumber conflicting, regNumber expected)
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, type);
            var candidates = genSingleTypeRegMask(conflicting) | genSingleTypeRegMask(expected);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, null, candidates);
            NextFixed(allocator)[(int)conflicting] = 11;
            definition.delayRegFree = true;
            if (type == TYP_MASK)
            {
                FixedHigh(allocator) = genSingleTypeRegMask(conflicting);
            }
            else
            {
                FixedLow(allocator) = genSingleTypeRegMask(conflicting);
            }

            Assert.That(Allocate(allocator, interval, definition), Is.EqualTo(expected));
        }, evex: type == TYP_MASK);
    }

    [Test]
    public static void OptionalReferenceClearsPreviousAssociationWhenAllRegistersAreBusy()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            interval.assignedReg = allocator.physRegs[(int)REG_RAX];
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 7), SRBM_RAX | SRBM_RBX);
            definition.setRegOptional(true);
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX | SRBM_RBX);

            Assert.That(Allocate(allocator, interval, definition), Is.EqualTo(REG_NA));
            Assert.That(interval.assignedReg, Is.Null);
        });
    }

    [TestCase(7, TYP_INT, TYP_LONG, true)]
    [TestCase(-7, TYP_INT, TYP_LONG, false)]
    [TestCase(-7, TYP_LONG, TYP_LONG, true)]
    [TestCase(0, TYP_INT, TYP_REF, true)]
    public static void ConstantReuseRespectsWidthAndNullRules(
        long value, var_types previousType, var_types type, bool expectedReuse)
    {
        WithAllocator((compiler, allocator) => {
            var previous = NewInterval(allocator, TYP_INT);
            previous.isConstant = true;
            _ = allocator.newRefPosition(previous, 1, RefType.RefTypeDef,
                compiler.gtNewIconNode(previousType, (nint)value), SRBM_RBX);
            var register = allocator.physRegs[(int)REG_RBX];
            Assign(allocator, register, previous);
            previous.isActive = false;
            MakeAvailable(allocator, REG_RBX, TYP_INT);
            InUse(allocator) = default;

            var interval = NewInterval(allocator, TYP_INT);
            interval.isConstant = true;
            var tree = compiler.gtNewIconNode(type, (nint)value);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            var selected = Allocate(allocator, interval, definition);

            Assert.That(tree.IsReuseRegVal, Is.EqualTo(expectedReuse));
            Assert.That(selected, Is.EqualTo(expectedReuse ? REG_RBX : REG_RAX));
        });
    }

    [TestCase(0L, 0L, true)]
    [TestCase(long.MinValue, 0L, false)]
    [TestCase(0x7FF8000000000001L, 0x7FF8000000000001L, true)]
    [TestCase(0x7FF8000000000001L, 0x7FF8000000000002L, false)]
    public static void FloatingConstantMatchingPreservesSignedZeroAndNaNPayload(
        long previousBits, long bits, bool expected)
    {
        WithAllocator((compiler, allocator) => {
            var previous = NewInterval(allocator, TYP_DOUBLE);
            previous.isConstant = true;
            _ = allocator.newRefPosition(previous, 1, RefType.RefTypeDef,
                compiler.gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(previousBits)), SRBM_XMM0);
            Assign(allocator, allocator.physRegs[(int)REG_XMM0], previous);

            var interval = NewInterval(allocator, TYP_DOUBLE);
            interval.isConstant = true;
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef,
                compiler.gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(bits)), SRBM_XMM0);

            Assert.That(Matches(allocator, allocator.physRegs[(int)REG_XMM0], definition), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void ReassignmentRetainsThePreviousInactiveInterval()
    {
        WithAllocator((compiler, allocator) => {
            var previous = NewInterval(allocator, TYP_INT);
            var previousDefinition = allocator.newRefPosition(
                previous, 1, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 1), SRBM_RAX);
            var interval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 2), SRBM_RAX);
            _ = allocator.newRefPosition(
                previous, 30, RefType.RefTypeUse, compiler.gtNewIconNode(TYP_INT, 1), SRBM_RAX);
            previous.recentRefPosition = previousDefinition;
            var record = allocator.physRegs[(int)REG_RAX];
            Assign(allocator, record, previous);
            previous.isActive = false;
            MakeAvailable(allocator, REG_RAX, TYP_INT);
            InUse(allocator) = default;

            _ = Allocate(allocator, interval, definition);

            Assert.That(record.previousInterval, Is.SameAs(previous));
            Assert.That(record.assignedInterval, Is.SameAs(interval));
            Assert.That(previous.physReg, Is.EqualTo(REG_NA));
        });
    }

    [Test]
    public static void RequiredFixedRegisterSpillsAtTheDisplacedIntervalsRecentReference()
    {
        WithAllocator((compiler, allocator) => {
            var previous = NewInterval(allocator, TYP_INT);
            var oldDefinition = allocator.newRefPosition(
                previous, 1, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 1), SRBM_RAX);
            var interval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 2), SRBM_RAX);
            _ = allocator.newRefPosition(
                previous, 30, RefType.RefTypeUse, compiler.gtNewIconNode(TYP_INT, 1), SRBM_RAX);
            previous.recentRefPosition = oldDefinition;
            Assign(allocator, allocator.physRegs[(int)REG_RAX], previous);
            InUse(allocator) = default;
            allocator.setCurrentBlockStartLocation(0);

            Assert.That(Allocate(allocator, interval, definition), Is.EqualTo(REG_RAX));
            Assert.That(oldDefinition.spillAfter, Is.True);
            Assert.That(previous.isSpilled, Is.True);
            Assert.That(previous.isActive, Is.False);
            Assert.That(interval.isActive, Is.True);
        });
    }

    [Test]
    public static void RelatedPreferencesTerminateOnACycleAndFavorTheCompatibleRegister()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var related = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 1), SRBM_RAX | SRBM_RBX);
            _ = allocator.newRefPosition(
                related, 30, RefType.RefTypeDef, compiler.gtNewIconNode(TYP_INT, 2), SRBM_RBX);
            interval.relatedInterval = related;
            related.relatedInterval = interval;
            interval.recentRefPosition = null;
            related.recentRefPosition = null;

            Assert.That(Allocate(allocator, interval, definition), Is.EqualTo(REG_RBX));
        });
    }

    [Test]
    public static void CopyAssignmentRestoresTheHomeAndRelatedInterval()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 1, RefType.RefTypeDef, tree, SRBM_RAX);
            var use = allocator.newRefPosition(interval, 10, RefType.RefTypeUse, tree, SRBM_RBX);
            interval.recentRefPosition = definition;
            var home = allocator.physRegs[(int)REG_RAX];
            Assign(allocator, home, interval);
            var related = NewInterval(allocator, TYP_INT);
            interval.relatedInterval = related;

            Assert.That(AssignCopy(allocator, use), Is.EqualTo(REG_RBX));
            Assert.That(use.copyReg, Is.True);
            Assert.That(use.RegOptional(), Is.False);
            Assert.That(interval.physReg, Is.EqualTo(REG_RAX));
            Assert.That(interval.assignedReg, Is.SameAs(home));
            Assert.That(interval.relatedInterval, Is.SameAs(related));
            Assert.That(interval.isActive, Is.True);
            Assert.That(allocator.physRegs[(int)REG_RBX].assignedInterval, Is.SameAs(interval));
        });
    }

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool evex = false)
    {
        LinearScanLocalCandidatesTests.WithCandidates(1, (compiler, allocator) => {
            compiler.compFloatingPointUsed = true;
            var codeGen = compiler.codeGen ?? throw new AssertionException("Codegen state is missing.");
            if (evex)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
                compiler.opts.compSupportsISAReported.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
                CompilerAllMask(compiler) |= SRBM_ALLMASK_EVEX;
                codeGen.CopyRegisterInfo();
                allocator = new LinearScan(compiler);
            }
            codeGen.RegSet.rsClearRegsModified();
            BlockInfo(allocator) = [new LsraBlockInfo { weight = 1 }];
            for (var index = 0; index < (int)ACTUAL_REG_COUNT; index++)
            {
                allocator.physRegs[index].init((regNumber)index);
                allocator.physRegs[index].regOrder = (byte)index;
            }
            action(compiler, allocator);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "allocateReg")]
    private static extern regNumber Allocate(LinearScan allocator, Interval interval, RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignCopyReg")]
    private static extern regNumber AssignCopy(LinearScan allocator, RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignPhysReg")]
    private static extern void Assign(LinearScan allocator, RegRecord register, Interval interval);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "makeRegAvailable")]
    private static extern void MakeAvailable(LinearScan allocator, regNumber register, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "isMatchingConstant")]
    private static extern bool Matches(LinearScan allocator, RegRecord register, RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[] BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsInUseThisLocation")]
    private static extern ref regMaskTP InUse(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_nextFixedRef")]
    private static extern ref uint[] NextFixed(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_fixedRegsLow")]
    private static extern ref regMask FixedLow(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_fixedRegsHigh")]
    private static extern ref regMask FixedHigh(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMask(Compiler compiler);
}
