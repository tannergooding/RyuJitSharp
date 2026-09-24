// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanConstructionTests
{
    [Test]
    public static void NewIntervalUsesItsTypeBankAndRetainsOwnership()
    {
        WithCompiler((compiler, codeGen) => {
            var reservedRegisters = SRBM_RAX | SRBM_RBX;
            codeGen.RegSet.rsMaskResvd = new regMaskTP(reservedRegisters);
            var allocator = new LinearScan(compiler);

            var integerInterval = NewInterval(allocator, TYP_INT);
            var floatingInterval = NewInterval(allocator, TYP_DOUBLE);

            Assert.That(integerInterval.registerPreferences, Is.EqualTo(SRBM_ALLINT_INIT & ~reservedRegisters));
            Assert.That(floatingInterval.registerPreferences, Is.EqualTo(SRBM_ALLFLOAT_INIT));
            Assert.That(allocator.intervals, Has.Count.EqualTo(2));
            Assert.That(allocator.intervals[0], Is.SameAs(integerInterval));
            Assert.That(allocator.intervals[1], Is.SameAs(floatingInterval));
#if DEBUG
            Assert.That(integerInterval.intervalIndex, Is.EqualTo(0u));
            Assert.That(floatingInterval.intervalIndex, Is.EqualTo(1u));
#endif
        });
    }

    [Test]
    public static void NewIntervalHonorsEditAndContinueCalleeSaveRestrictions()
    {
        WithCompiler((compiler, codeGen) => {
            codeGen.RegSet.rsMaskResvd = new regMaskTP(SRBM_RAX);
            var allocator = new LinearScan(compiler);

            var integerInterval = NewInterval(allocator, TYP_INT);
            var floatingInterval = NewInterval(allocator, TYP_FLOAT);

            var availableIntRegs = SRBM_ALLINT_INIT & ~SRBM_RAX;
            var encIntRegs = availableIntRegs & (~SRBM_INT_CALLEE_SAVED | SRBM_ENC_CALLEE_SAVED);
            Assert.That(integerInterval.registerPreferences, Is.EqualTo(encIntRegs));
            Assert.That(floatingInterval.registerPreferences, Is.EqualTo(SRBM_ALLFLOAT_INIT & ~SRBM_FLT_CALLEE_SAVED));
        }, debugEnC: true);
    }

    [Test]
    public static void NewIntervalHonorsPatchpointFloatCalleeSaveRestrictions()
    {
        WithCompiler((compiler, _) => {
            var allocator = new LinearScan(compiler);

            var integerInterval = NewInterval(allocator, TYP_INT);
            var floatingInterval = NewInterval(allocator, TYP_FLOAT);

            Assert.That(integerInterval.registerPreferences, Is.EqualTo(SRBM_ALLINT_INIT));
            Assert.That(floatingInterval.registerPreferences, Is.EqualTo(SRBM_ALLFLOAT_INIT & ~SRBM_FLT_CALLEE_SAVED));
        }, hasPatchpoint: true);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types registerType);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithCompiler(Action<Compiler, CodeGen> action, bool debugEnC = false, bool hasPatchpoint = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        if (debugEnC)
        {
            flags.Set(JitFlags.JIT_FLAG_DEBUG_EnC);
            compiler.opts.compDbgEnC = true;
        }
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
        compiler.MethodHasPatchpoint = hasPatchpoint;
        try
        {
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
