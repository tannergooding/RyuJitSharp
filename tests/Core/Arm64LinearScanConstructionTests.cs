// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64LinearScanConstructionTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void IntervalsUseNativeRegisterBanks(bool debugEnC, bool hasPatchpoint)
    {
        WithCompiler(debugEnC, hasPatchpoint, (compiler, codeGen) => {
            var reserved = SRBM_R0 | SRBM_R19;
            codeGen.RegSet.rsMaskResvd = new regMaskTP(reserved);
            var allocator = new LinearScan(compiler);

            var integers = SRBM_ALLINT & ~(SRBM_PR | SRBM_FP | SRBM_LR | reserved);
            var floats = SRBM_ALLFLOAT;
            if (debugEnC)
            {
                integers &= ~SRBM_INT_CALLEE_SAVED | SRBM_ENC_CALLEE_SAVED;
                floats &= ~SRBM_FLT_CALLEE_SAVED;
            }

            (var_types Type, regMask Available, regMask CalleeSaved, regMask CallerSaved)[] banks =
            [
                (TYP_INT, integers, SRBM_INT_CALLEE_SAVED, SRBM_INT_CALLEE_TRASH),
                (TYP_REF, integers, SRBM_INT_CALLEE_SAVED, SRBM_INT_CALLEE_TRASH),
                (TYP_FLOAT, floats, SRBM_FLT_CALLEE_SAVED, SRBM_FLT_CALLEE_TRASH),
                (TYP_DOUBLE, floats, SRBM_FLT_CALLEE_SAVED, SRBM_FLT_CALLEE_TRASH),
                (TYP_SIMD16, floats, SRBM_FLT_CALLEE_SAVED, SRBM_FLT_CALLEE_TRASH),
                (TYP_SIMD, floats, SRBM_FLT_CALLEE_SAVED, SRBM_FLT_CALLEE_TRASH),
                (TYP_MASK, SRBM_ALLMASK, SRBM_MSK_CALLEE_SAVED, SRBM_MSK_CALLEE_TRASH),
            ];

            foreach (var bank in banks)
            {
                var interval = NewInterval(allocator, bank.Type);
                Assert.Multiple(() => {
                    Assert.That(interval.registerPreferences, Is.EqualTo(bank.Available), bank.Type.ToString());
                    Assert.That(LinearScan.calleeSaveRegs(bank.Type), Is.EqualTo(bank.CalleeSaved));
                    Assert.That(CallerSaveRegs(allocator, bank.Type), Is.EqualTo(bank.CallerSaved));
                    Assert.That(allocator.intervals[^1], Is.SameAs(interval));
                });
            }

            var indices = RegisterIndices(allocator);
            Assert.That(AvailableRegCount(allocator), Is.EqualTo((int)ACTUAL_REG_COUNT));
            Assert.That(indices, Has.Length.EqualTo((int)ACTUAL_REG_COUNT + 1));
            for (var index = 0; index < indices.Length; index++)
            {
                Assert.That(indices[index], Is.EqualTo((regNumber)index));
            }

            Assert.That(allocator.intervals, Has.Count.EqualTo(banks.Length));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "newInterval")]
    private static extern Interval NewInterval(LinearScan allocator, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "callerSaveRegs")]
    private static extern regMask CallerSaveRegs(LinearScan allocator, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regIndices")]
    private static extern ref regNumber[] RegisterIndices(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableRegCount")]
    private static extern ref int AvailableRegCount(LinearScan allocator);

    private static void WithCompiler(bool debugEnC, bool hasPatchpoint, Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compDbgEnC = debugEnC;
#if DEBUG
        compiler.info.compFullName = nameof(Arm64LinearScanConstructionTests);
#endif
        if (debugEnC)
        {
            flags.Set(JitFlags.JIT_FLAG_DEBUG_EnC);
        }

        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            compiler.MethodHasPatchpoint = hasPatchpoint;
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
