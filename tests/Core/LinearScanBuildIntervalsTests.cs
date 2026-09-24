// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanBuildIntervalsTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void PhysicalRegisterRecordsUseNativeRegisterAndEncodingOrders(bool evex)
    {
        WithCompiler((compiler, _) => {
            if (evex)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
                compiler.opts.compSupportsISAReported.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX512);
            }
            var allocator = new LinearScan(compiler);
            Assert.That(EvexIsSupported(allocator), Is.EqualTo(evex));

            BuildPhysRegRecords(allocator);

            for (var index = 0; index < AvailableRegisterCount(allocator); index++)
            {
                var registerNumber = (regNumber)index;
                var record = allocator.physRegs[(int)registerNumber];
                Assert.That(record.regNum, Is.EqualTo(registerNumber));
                if (genIsValidIntReg(registerNumber))
                {
                    Assert.That(record.registerType, Is.EqualTo(TYP_INT));
                }
                else if (genIsValidFloatReg(registerNumber))
                {
                    Assert.That(record.registerType, Is.EqualTo(TYP_FLOAT));
                }
#if FEATURE_MASKED_HW_INTRINSICS
                else
                {
                    Assert.That(record.registerType, Is.EqualTo(TYP_MASK));
                }
#endif

                Assert.That(
                    record.isCalleeSave,
                    Is.EqualTo((LinearScan.calleeSaveRegs(record.registerType) &
                        genSingleTypeRegMask(registerNumber)) != SRBM_NONE));
            }

            AssertOrder(allocator, REG_VAR_ORDER);
            AssertOrder(
                allocator,
                EvexIsSupported(allocator) ? REG_VAR_ORDER_FLT_EVEX : REG_VAR_ORDER_FLT);
            if (EvexIsSupported(allocator))
            {
                AssertOrder(allocator, REG_VAR_ORDER_MSK);
            }

            ResetAllRegistersState(allocator);
            VerifyFreeRegisters(allocator, RBM_NONE);
        });
    }

    private static void AssertOrder(LinearScan allocator, ReadOnlySpan<regNumber> order)
    {
        for (var index = 0; index < order.Length; index++)
        {
            Assert.That(allocator.physRegs[(int)order[index]].regOrder, Is.EqualTo((byte)index));
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableRegCount")]
    private static extern ref int AvailableRegisterCount(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "resetAllRegistersState")]
    private static extern void ResetAllRegistersState(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "verifyFreeRegisters")]
    private static extern void VerifyFreeRegisters(LinearScan allocator, regMaskTP registersToFree);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_evexIsSupported")]
    private static extern ref bool EvexIsSupported(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.CopyRegisterInfo();
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
