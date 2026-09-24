// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using SingleTypeRegSet = RyuJitSharp.regMask;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanMinimalAllocationTests
{
    [Test]
    public static void MinimalAllocationAssignsRegisterSelectedAfterBusyFiltering()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            allocator.physRegs[(int)regNumber.REG_RAX].regOrder = 1;
            allocator.physRegs[(int)regNumber.REG_RBX].regOrder = 0;
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX);

            var selected = AllocateRegMinimal(allocator, interval, definition);

            Assert.That(selected, Is.EqualTo(regNumber.REG_RBX));
            Assert.That(definition.registerAssignment, Is.EqualTo(SRBM_RBX));
            Assert.That(interval.assignedReg, Is.SameAs(allocator.physRegs[(int)regNumber.REG_RBX]));
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_RBX));
            Assert.That(interval.isActive, Is.True);
        });
    }

    [Test]
    public static void OptionalReferenceReturnsWithoutAssignmentWhenAllCandidatesAreBusy()
    {
        WithAllocator((compiler, allocator) => {
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(
                interval, 10, RefType.RefTypeDef, tree, SRBM_RAX | SRBM_RBX);
            definition.setRegOptional(true);
            BusyUntilKill(allocator) = new regMaskTP(SRBM_RAX | SRBM_RBX);

            var selected = AllocateRegMinimal(allocator, interval, definition);

            Assert.That(selected, Is.EqualTo(regNumber.REG_NA));
            Assert.That(interval.assignedReg, Is.Null);
            Assert.That(interval.physReg, Is.Not.EqualTo(regNumber.REG_RAX));
            Assert.That(interval.physReg, Is.Not.EqualTo(regNumber.REG_RBX));
        });
    }

    [Test]
    public static void RequiredReferenceReusesRegisterFromInactiveInterval()
    {
        WithAllocator((compiler, allocator) => {
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var previousInterval = NewInterval(allocator, TYP_INT);
            _ = allocator.newRefPosition(previousInterval, 2, RefType.RefTypeDef, tree, SRBM_RAX);
            var previousDefinition = previousInterval.recentRefPosition;
            var previousRegister = AllocateRegMinimal(
                allocator, previousInterval, previousDefinition
                    ?? throw new InvalidOperationException("The previous interval requires a definition."));
            Assert.That(previousRegister, Is.EqualTo(regNumber.REG_RAX));
            previousInterval.isActive = false;

            var interval = NewInterval(allocator, TYP_INT);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);

            var selected = AllocateRegMinimal(allocator, interval, definition);

            Assert.That(selected, Is.EqualTo(regNumber.REG_RAX));
            Assert.That(allocator.physRegs[(int)regNumber.REG_RAX].assignedInterval, Is.SameAs(interval));
            Assert.That(interval.assignedReg, Is.SameAs(allocator.physRegs[(int)regNumber.REG_RAX]));
            Assert.That(previousInterval.physReg, Is.EqualTo(regNumber.REG_NA));
            Assert.That(previousInterval.isActive, Is.False);
        });
    }

#if DEBUG
    [Test]
    public static void SpillingEmitsNativeAllocationEventText()
    {
        WithAllocator((compiler, allocator) => {
            compiler.verbose = true;
            var interval = NewInterval(allocator, TYP_INT);
            var tree = compiler.gtNewIconNode(TYP_INT, 1);
            var definition = allocator.newRefPosition(interval, 10, RefType.RefTypeDef, tree, SRBM_RAX);
            _ = allocator.newRefPosition(interval, 20, RefType.RefTypeUse, tree, SRBM_RAX);
            interval.recentRefPosition = definition;
            allocator.setCurrentBlockStartLocation(0);

            var register = allocator.physRegs[(int)regNumber.REG_RAX];
            AssignPhysReg(allocator, register, interval);

            var output = Capture(() => UnassignPhysReg(allocator, register, interval.registerType));

            Assert.That(output, Does.Contain("Spill    RAX "));
            Assert.That(definition.spillAfter, Is.True);
            Assert.That(interval.isSpilled, Is.True);
            Assert.That(register.assignedInterval, Is.Null);
        });
    }
#endif

    private static Interval NewInterval(LinearScan allocator, var_types type) => NewIntervalCore(allocator, type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewIntervalCore(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "allocateRegMinimal")]
    private static extern regNumber AllocateRegMinimal(
        LinearScan allocator, Interval interval, RefPosition refPosition);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignPhysReg")]
    private static extern void AssignPhysReg(LinearScan allocator, RegRecord register, Interval interval);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "unassignPhysReg")]
    private static extern void UnassignPhysReg(
        LinearScan allocator, RegRecord register, var_types newRegisterType);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regsBusyUntilKill")]
    private static extern ref regMaskTP BusyUntilKill(LinearScan allocator);

    private static void WithAllocator(Action<Compiler, LinearScan> action)
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

            BlockInfo(allocator) = [new LsraBlockInfo { weight = 1 }];
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

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
}
