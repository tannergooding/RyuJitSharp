// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using SingleTypeRegSet = RyuJitSharp.regMask;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanRegisterSelectionTests
{
    [Test]
    public static void MinimalSelectionChoosesLowestRegisterOrderAfterBusyFiltering()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);

            allocator.physRegs[(int)regNumber.REG_RAX].regOrder = 1;
            allocator.physRegs[(int)regNumber.REG_RBX].regOrder = 0;
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(selected, Is.EqualTo(SRBM_RBX));
        });
    }

    [Test]
    public static void OptionalReferenceReturnsNoRegisterWhenEveryCandidateIsBusy()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            definition.setRegOptional(true);
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX | SRBM_RBX);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(selected, Is.EqualTo(SRBM_NONE));
            Assert.That(interval.assignedReg, Is.Null);
        });
    }

    [Test]
    public static void ConflictingDefinitionInheritsFixedUseRegisterAfterPreviousLastUse()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var priorInterval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(priorInterval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var priorLastUse = allocator.newRefPosition(priorInterval, 9, RefType.RefTypeUse, tree, SRBM_RAX);
            allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval = priorInterval;

            var interval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RBX);
            _ = allocator.newRefPosition(interval, 12, RefType.RefTypeUse, tree, SRBM_RAX);
            UpdateNextFixedReference(allocator, regNumber.REG_RAX);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(priorLastUse.lastUse, Is.True);
            Assert.That(interval.hasConflictingDefUse, Is.True);
            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RAX));
            Assert.That(definition.isFixedRegRef, Is.True);
            Assert.That(selected, Is.EqualTo(SRBM_RAX));
        });
    }

    [Test]
    public static void BusyFixedUseWidensConflictingDefinitionBeforeSelection()
    {
        WithCompiler(compiler => {
            var allocator = CreateAllocator(compiler);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var priorInterval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(priorInterval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var priorUse = allocator.newRefPosition(priorInterval, 11, RefType.RefTypeUse, tree, SRBM_RAX);
            priorInterval.isActive = true;
            allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval = priorInterval;

            var interval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RBX);
            _ = allocator.newRefPosition(interval, 12, RefType.RefTypeUse, tree, SRBM_RAX);
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX);
            UpdateNextFixedReference(allocator, regNumber.REG_RAX);

            var selected = allocator.selectMinimal(interval, definition);

            Assert.That(priorUse.nodeLocation, Is.GreaterThan(definition.nodeLocation));
            Assert.That(interval.hasConflictingDefUse, Is.True);
            Assert.That(definition.registerAssignment, Is.EqualTo(AllRegs(allocator, TYP_INT)));
            Assert.That(definition.isFixedRegRef, Is.False);
            Assert.That(selected, Is.Not.EqualTo(SRBM_RAX));
            Assert.That(definition.registerAssignment & selected, Is.EqualTo(selected));
        });
    }

    private static LinearScan CreateAllocator(Compiler compiler)
    {
        var allocator = new LinearScan(compiler);
        BlockInfo(allocator) = [new LsraBlockInfo()];
        for (var index = 0; index < (int)regNumber.ACTUAL_REG_COUNT; index++)
        {
            var register = allocator.physRegs[index];
            register.init((regNumber)index);
            register.regOrder = (byte)index;
        }

        return allocator;
    }

    private static Interval NewInterval(LinearScan allocator, var_types type) => NewIntervalCore(allocator, type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewIntervalCore(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "allRegs")]
    private static extern SingleTypeRegSet AllRegs(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "updateNextFixedRef")]
    private static extern void UpdateNextFixedRef(
        LinearScan allocator, RegRecord regRecord, RefPosition? nextRefPosition, RefPosition? nextKill);

    private static void UpdateNextFixedReference(LinearScan allocator, regNumber register)
    {
        var regRecord = allocator.physRegs[(int)register];
        var nextRefPosition = regRecord.lastRefPosition
            ?? throw new InvalidOperationException("The fixed register must have a linked reference.");
        UpdateNextFixedRef(allocator, regRecord, nextRefPosition, null);
    }

    private static void WithCompiler(Action<Compiler> action)
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
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        try
        {
            action(compiler);
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
}
