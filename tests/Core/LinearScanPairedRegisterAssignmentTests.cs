// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_AMD64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanPairedRegisterAssignmentTests
{
    [Test]
    public static void DoubleConstantAssignmentAndUnassignmentPreserveSingleRegisterState()
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
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.CopyRegisterInfo();
            codeGen.RegSet.rsClearRegsModified();
            var allocator = new LinearScan(compiler);
            var register = allocator.physRegs[(int)regNumber.REG_XMM0];
            register.init(regNumber.REG_XMM0);
            var interval = NewInterval(allocator, TYP_DOUBLE);
            interval.isConstant = true;

            AssignPhysReg(allocator, register, interval);

            Assert.That(register.assignedInterval, Is.SameAs(interval));
            Assert.That(interval.physReg, Is.EqualTo(regNumber.REG_XMM0));
            Assert.That(RegistersWithConstants(allocator).IsSet(regNumber.REG_XMM0), Is.True);

            interval.isActive = false;
            UnassignPhysReg(allocator, register, (RefPosition?)null);

            Assert.That(register.assignedInterval, Is.Null);
            Assert.That(RegistersWithConstants(allocator).IsSet(regNumber.REG_XMM0), Is.False);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "assignPhysReg")]
    private static extern void AssignPhysReg(LinearScan allocator, RegRecord register, Interval interval);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "unassignPhysReg")]
    private static extern void UnassignPhysReg(
        LinearScan allocator, RegRecord register, RefPosition? spillRefPosition);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_registersWithConstants")]
    private static extern ref regMaskTP RegistersWithConstants(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);
}
#endif
