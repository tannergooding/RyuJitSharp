// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ManagedInstructionSet;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LinearScanInitializationRegisterBanksTests
{
    [TestCase(TYP_UNDEF, 1)]
    [TestCase(TYP_VOID, 1)]
    [TestCase(TYP_INT, 1)]
    [TestCase(TYP_FLOAT, 2)]
    [TestCase(TYP_DOUBLE, 4)]
    [TestCase(TYP_SIMD8, 4)]
    [TestCase(TYP_SIMD12, 4)]
    [TestCase(TYP_SIMD16, 4)]
    [TestCase(TYP_SIMD32, 4)]
    [TestCase(TYP_SIMD64, 4)]
    [TestCase(TYP_SIMD_ARM64, 4)]
    [TestCase(TYP_MASK, 8)]
    public static void TypeMappingPreservesDistinctDoubleAndFloatBanks(var_types type, int expectedMask)
    {
        if (type is TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16 or TYP_SIMD32 or TYP_SIMD64 or TYP_SIMD_ARM64)
        {
            Assume.That(Configuration.FEATURE_SIMD, "SIMD register types require SIMD support.");
        }
        if (type is TYP_SIMD32 or TYP_SIMD64)
        {
            Assume.That(Configuration.TARGET_XARCH, "These SIMD widths require the xarch target.");
        }
        else if (type == TYP_SIMD_ARM64)
        {
            Assume.That(Configuration.TARGET_ARM64, "This SIMD type requires the ARM64 target.");
        }
        else if (type == TYP_MASK)
        {
            Assume.That(Configuration.FEATURE_MASKED_HW_INTRINSICS,
                "Mask register types require masked hardware intrinsics.");
        }

        var allocator = (LinearScan)RuntimeHelpers.GetUninitializedObject(typeof(LinearScan));
        AvailableIntegerRegisters(allocator) = (regMask)1;
        AvailableFloatRegisters(allocator) = (regMask)2;
        AvailableDoubleRegisters(allocator) = (regMask)4;
        AvailableMaskRegisters(allocator) = (regMask)8;

        InitializeAvailableRegisters(allocator);

        Assert.That(AvailableRegisters(allocator)[(int)type], Is.EqualTo((regMask)expectedMask));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ConstructorPreservesTargetExclusionsAndCalleeSavePolicies(bool debugEnC, bool patchpoints)
    {
        WithConstructor(debugEnC, patchpoints, false, false, (_, allocator) => {
            regMask expectedInteger;
            regMask expectedFloat;
            regMask expectedDouble;
            var expectedMask = SRBM_NONE;
            if (Configuration.TARGET_AMD64)
            {
                expectedInteger = SRBM_ALLINT_INIT;
                expectedFloat = SRBM_ALLFLOAT_INIT;
                expectedDouble = expectedFloat;
                expectedMask = SRBM_ALLMASK_INIT;
            }
            else if (Configuration.TARGET_WASM)
            {
                expectedInteger = SRBM_NONE;
                expectedFloat = SRBM_NONE;
                expectedDouble = SRBM_NONE;
            }
            else
            {
                expectedInteger = SRBM_ALLINT;
                expectedFloat = SRBM_ALLFLOAT;
                expectedDouble = SRBM_ALLDOUBLE;
                if (Configuration.TARGET_X86)
                {
                    expectedMask = SRBM_ALLMASK_INIT;
                }
                else if (Configuration.TARGET_ARM64)
                {
                    expectedMask = SRBM_ALLMASK;
                }
            }

            expectedInteger &= ~ReservedIntegerRegister;
            if (Configuration.TARGET_ARM64)
            {
                expectedInteger &= ~(SRBM_ARM64_PR | SRBM_ARM64_FP | SRBM_ARM64_LR);
            }
            else if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
            {
                expectedInteger &= ~(SRBM_FP | SRBM_RA);
            }
            if (Configuration.ETW_EBP_FRAMED)
            {
                expectedInteger &= ~SRBM_FPBASE;
            }
            if (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64)
            {
                if (debugEnC)
                {
                    expectedInteger &= ~SRBM_INT_CALLEE_SAVED | SRBM_ENC_CALLEE_SAVED;
                    expectedFloat &= ~SRBM_FLT_CALLEE_SAVED;
                    expectedDouble &= ~SRBM_FLT_CALLEE_SAVED;
                    if (Configuration.TARGET_XARCH)
                    {
                        expectedMask &= ~SRBM_MSK_CALLEE_SAVED;
                    }
                }
            }
            if (Configuration.TARGET_AMD64 && patchpoints)
            {
                expectedFloat &= ~SRBM_FLT_CALLEE_SAVED;
                expectedDouble &= ~SRBM_FLT_CALLEE_SAVED;
                expectedMask &= ~SRBM_MSK_CALLEE_SAVED;
            }
            Assert.That(AvailableIntegerRegisters(allocator), Is.EqualTo(expectedInteger));
            Assert.That(AvailableFloatRegisters(allocator), Is.EqualTo(expectedFloat));
            Assert.That(AvailableDoubleRegisters(allocator), Is.EqualTo(expectedDouble));
            Assert.That(AvailableRegisters(allocator)[(int)TYP_DOUBLE], Is.EqualTo(expectedDouble));
            if (Configuration.TARGET_XARCH || Configuration.TARGET_ARM64)
            {
                Assert.That(AvailableMaskRegisters(allocator), Is.EqualTo(expectedMask));
            }
            if (Configuration.TARGET_AMD64)
            {
                Assert.That(LowGeneralPurposeRegisters(allocator), Is.EqualTo(expectedInteger & SRBM_LOWINT));
            }
            else if (Configuration.TARGET_X86)
            {
                Assert.That(LowGeneralPurposeRegisters(allocator), Is.EqualTo(expectedInteger));
            }
            if (Configuration.TARGET_WASM)
            {
                Assert.That(CallerSaveRegisters(allocator, TYP_INT), Is.EqualTo(SRBM_NONE));
                Assert.That(CallerSaveRegisters(allocator, TYP_DOUBLE), Is.EqualTo(SRBM_NONE));
                Assert.That(LinearScan.calleeSaveRegs(TYP_INT), Is.EqualTo(SRBM_NONE));
                Assert.That(LinearScan.calleeSaveRegs(TYP_DOUBLE), Is.EqualTo(SRBM_NONE));
            }
            else
            {
                if (Configuration.TARGET_AMD64)
                {
                    Assert.That(CallerSaveRegisters(allocator, TYP_INT), Is.EqualTo(SRBM_INT_CALLEE_TRASH_INIT));
                    Assert.That(CallerSaveRegisters(allocator, TYP_DOUBLE), Is.EqualTo(SRBM_FLT_CALLEE_TRASH_INIT));
                }
                else
                {
                    Assert.That(CallerSaveRegisters(allocator, TYP_INT), Is.EqualTo(SRBM_INT_CALLEE_TRASH));
                    Assert.That(CallerSaveRegisters(allocator, TYP_DOUBLE), Is.EqualTo(SRBM_FLT_CALLEE_TRASH));
                }
                Assert.That(LinearScan.calleeSaveRegs(TYP_INT), Is.EqualTo(SRBM_INT_CALLEE_SAVED));
                Assert.That(LinearScan.calleeSaveRegs(TYP_DOUBLE), Is.EqualTo(SRBM_FLT_CALLEE_SAVED));
            }
            var indices = RegisterIndices(allocator);
            if (Configuration.TARGET_AMD64)
            {
                Assert.That(indices[15], Is.EqualTo(REG_X64_R15));
                Assert.That(indices[16], Is.EqualTo(REG_X64_XMM0));
                Assert.That(indices[^1], Is.EqualTo(REG_COUNT));
            }
            else
            {
                Assert.That(indices, Has.Length.EqualTo((int)ACTUAL_REG_COUNT + 1));
                for (var index = 0; index < indices.Length; index++)
                {
                    Assert.That(indices[index], Is.EqualTo((regNumber)index));
                }
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EvexPreservesNativeRegisterUpperBoundAndMaskBank(bool evex)
    {
        Assume.That(Configuration.TARGET_XARCH, "This test requires an xarch target.");

        WithConstructor(false, false, evex, false, (_, allocator) => {
            var expectedCount = (int)ACTUAL_REG_COUNT;
            if (!evex)
            {
                expectedCount -= CNT_HIGHFLOAT + CNT_MASK_REGS;
            }

            Assert.That(AvailableRegisterCount(allocator), Is.EqualTo(expectedCount));
            Assert.That(AvailableMaskRegisters(allocator),
                Is.EqualTo(evex ? SRBM_ALLMASK_EVEX : SRBM_ALLMASK_INIT));
            Assert.That(CallerSaveRegisters(allocator, TYP_MASK),
                Is.EqualTo(evex ? SRBM_MSK_CALLEE_TRASH_EVEX : SRBM_MSK_CALLEE_TRASH_INIT));
            Assert.That(AvailableFloatRegisters(allocator), Is.EqualTo(Configuration.TARGET_AMD64
                ? (evex ? SRBM_ALLFLOAT_INIT | SRBM_HIGHFLOAT : SRBM_ALLFLOAT_INIT)
                : SRBM_ALLFLOAT));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ApxChoosesCompactOrIdentityRegisterIndices(bool apx)
    {
        Assume.That(Configuration.TARGET_AMD64, "This test requires AMD64.");

        WithConstructor(false, false, false, apx, (_, allocator) => {
            var indices = RegisterIndices(allocator);
            if (apx)
            {
                Assert.That(indices, Has.Length.EqualTo((int)ACTUAL_REG_COUNT + 1));
                for (var index = 0; index < indices.Length; index++)
                {
                    Assert.That(indices[index], Is.EqualTo((regNumber)index));
                }
            }
            else
            {
                Assert.That(indices[16], Is.EqualTo(REG_X64_XMM0));
                Assert.That(indices[^1], Is.EqualTo(REG_COUNT));
            }
        });
    }

    [Test]
    public static void ArmDoubleBankContainsOnlyEvenPhysicalHomes()
    {
        Assume.That(Configuration.TARGET_ARM, "This test requires ARM32.");

        WithConstructor(false, false, false, false, (_, allocator) => {
            Assert.That(AvailableFloatRegisters(allocator) & (SRBM_F1 | SRBM_F31),
                Is.EqualTo(SRBM_F1 | SRBM_F31));
            Assert.That(AvailableDoubleRegisters(allocator) & (SRBM_F1 | SRBM_F31), Is.EqualTo(SRBM_NONE));
            Assert.That(AvailableDoubleRegisters(allocator) & (SRBM_F0 | SRBM_F30),
                Is.EqualTo(SRBM_F0 | SRBM_F30));
        });
    }

    [Test]
    public static void LoongArchFloatBankPreservesPinnedRestrictedCallerBank()
    {
        Assume.That(Configuration.TARGET_LOONGARCH64, "This test requires LoongArch64.");

        WithConstructor(false, false, false, false, (_, allocator) => {
            Assert.That(AvailableFloatRegisters(allocator) & (SRBM_F8 | SRBM_F23), Is.EqualTo(SRBM_NONE));
            Assert.That(AvailableFloatRegisters(allocator) & (SRBM_F0 | SRBM_F7 | SRBM_F24 | SRBM_F31),
                Is.EqualTo(SRBM_F0 | SRBM_F7 | SRBM_F24 | SRBM_F31));
        });
    }

    private static regMask ReservedIntegerRegister =>
        Configuration.TARGET_X86 ? SRBM_EAX :
        Configuration.TARGET_AMD64 ? SRBM_X64_RAX :
        Configuration.TARGET_ARM ? SRBM_R0 :
        Configuration.TARGET_ARM64 ? SRBM_ARM64_R0 :
        Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64 ? SRBM_A0 :
        SRBM_NONE;

    private static void WithConstructor(
        bool debugEnC, bool patchpoints, bool evex, bool apx, Action<Compiler, LinearScan> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.SetMinOpts(true);
        compiler.opts.compDbgEnC = debugEnC;
        compiler.MethodHasPatchpoint = patchpoints;
        if (debugEnC)
        {
            flags.Set(JitFlags.JIT_FLAG_DEBUG_EnC);
        }

        if (Configuration.TARGET_AMD64)
        {
            CompilerIntegerRegisters(compiler) = apx ? SRBM_ALLINT_INIT | SRBM_HIGHINT : SRBM_ALLINT_INIT;
            CompilerFloatRegisters(compiler) = evex ? SRBM_ALLFLOAT_INIT | SRBM_HIGHFLOAT : SRBM_ALLFLOAT_INIT;
            CompilerIntegerTrashRegisters(compiler) =
                apx ? SRBM_INT_CALLEE_TRASH_INIT | SRBM_HIGHINT : SRBM_INT_CALLEE_TRASH_INIT;
            CompilerFloatTrashRegisters(compiler) =
                evex ? SRBM_FLT_CALLEE_TRASH_INIT | SRBM_HIGHFLOAT : SRBM_FLT_CALLEE_TRASH_INIT;
            CompilerLastIntegerRegister(compiler) = apx ? REG_X64_R31 : REG_X64_R15;
            if (apx)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_APX);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_APX);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_APX);
            }
        }
        if (Configuration.TARGET_XARCH)
        {
            CompilerMaskRegisters(compiler) = evex ? SRBM_ALLMASK_EVEX : SRBM_ALLMASK_INIT;
            CompilerMaskTrashRegisters(compiler) = evex ? SRBM_MSK_CALLEE_TRASH_EVEX : SRBM_MSK_CALLEE_TRASH_INIT;
            if (evex)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
            }
        }
        JitTls.Compiler = compiler;
        try
        {
            // The constructor reads only this existing codegen boundary, not emitter or frame state.
            var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
            codeGen.RegSet.rsMaskResvd = new regMaskTP(ReservedIntegerRegister);
            compiler.codeGen = codeGen;
            action(compiler, new LinearScan(compiler));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "initializeAvailableRegs")]
    private static extern void InitializeAvailableRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntegerRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableFloatRegs")]
    private static extern ref regMask AvailableFloatRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableDoubleRegs")]
    private static extern ref regMask AvailableDoubleRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableMaskRegs")]
    private static extern ref regMask AvailableMaskRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableRegs")]
    private static extern ref InlineArrayTypCount<regMask> AvailableRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regIndices")]
    private static extern ref regNumber[] RegisterIndices(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "callerSaveRegs")]
    private static extern regMask CallerSaveRegisters(LinearScan allocator, var_types type);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableRegCount")]
    private static extern ref int AvailableRegisterCount(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lowGprRegs")]
    private static extern ref regMask LowGeneralPurposeRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerMaskRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmMskCalleeTrash")]
    private static extern ref regMask CompilerMaskTrashRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerIntegerRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerFloatRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmIntCalleeTrash")]
    private static extern ref regMask CompilerIntegerTrashRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask CompilerFloatTrashRegisters(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "regIntLast")]
    private static extern ref regNumber CompilerLastIntegerRegister(Compiler compiler);
}
