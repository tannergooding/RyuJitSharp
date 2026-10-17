// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.CorInfoReloc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm32HelperCallTests
{
    [SetUp]
    public static void RequireArm32Target()
    {
        Assume.That(Configuration.TARGET_ARM, "This fixture requires the ARM target.");
    }

    [Test]
    public static void InRangeHelperAddressesUseDirectThumbCalls()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            Assert.That(compiler.opts.OptimizationDisabled, Is.False);
            const nint helperAddress = 0x1234;
            using var callbacks = new Arm32HelperCallbacks(
                compiler, IAT_VALUE, ARM32_THUMB_BRANCH24, (void*)helperAddress);
            compiler.opts.compReloc = true;
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            codeGen.genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN);

            var call = LastInstruction(codeGen.Emitter);
            Assert.That(Arm32HelperCallbacks.Helper, Is.EqualTo(CORINFO_HELP_FAIL_FAST));
            Assert.That(Arm32HelperCallbacks.Assertions, Is.Zero);
            Assert.That(call.idIns(), Is.EqualTo(INS_bl));
            Assert.That((nint)call.idAddr().iiaAddr, Is.EqualTo(helperAddress));
            Assert.That(call.idIsDspReloc(), Is.True);
            AssertCalleeTrashAccounting(codeGen);
        }, minOpts: false);
    }

    [Test]
    public static void OutOfRangeHelperAddressesUseTheDefaultCallTargetRegister()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            const nint helperAddress = 0x12345678;
            using var callbacks = new Arm32HelperCallbacks(compiler, IAT_VALUE, NONE, (void*)helperAddress);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            codeGen.genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(Arm32HelperCallbacks.Assertions, Is.Zero);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_movw, INS_movt, INS_blx]));
            Assert.That(descriptors[^1].idReg3(), Is.EqualTo(REG_R12));
            AssertCalleeTrashAccounting(codeGen);
        }, minOpts: false);
    }

    [Test]
    public static void IndirectHelperAddressesLoadTheCellBeforeCallingThroughTheDefaultRegister()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            using var callbacks = new Arm32HelperCallbacks(compiler, IAT_PVALUE, NONE, (void*)0x12345678);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            codeGen.genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(Arm32HelperCallbacks.Assertions, Is.Zero);
            Assert.That(descriptors.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_movw, INS_movt, INS_ldr, INS_blx]));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_R12));
            Assert.That(descriptors[3].idReg3(), Is.EqualTo(REG_R12));
            AssertCalleeTrashAccounting(codeGen);
        }, minOpts: false);
    }

    [TestCase(CORINFO_HELP_PROF_FCN_ENTER, false)]
    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, false)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, false)]
    [TestCase(CORINFO_HELP_PROF_FCN_ENTER, true)]
    [TestCase(CORINFO_HELP_FAIL_FAST, false)]
    public static void ProfilerStressUsesTheMethodHandleOnlyForEligibleDebugCalls(
        CorInfoHelpFunc helper, bool profilerHookNeeded)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            ProfilerHookNeeded(compiler) = profilerHookNeeded;
            compiler.opts.compJitELTHookEnabled = true;
            compiler.compProfilerMethHnd = (void*)0x5678;
            compiler.opts.compReloc = true;
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            using var callbacks = new Arm32HelperCallbacks(
                compiler, IAT_VALUE, ARM32_THUMB_BRANCH24, (void*)0x1234);

            codeGen.genEmitHelperCall(helper, 0, EA_UNKNOWN);

            var isProfilerHelper = helper is CORINFO_HELP_PROF_FCN_ENTER
                or CORINFO_HELP_PROF_FCN_LEAVE
                or CORINFO_HELP_PROF_FCN_TAILCALL;
            var usesProfilerMethodHandle = TestConfiguration.IsDebug && Configuration.PROFILING_SUPPORTED &&
                !profilerHookNeeded && isProfilerHelper;
            var expectedAddress = usesProfilerMethodHandle ? (nint)0x5678 : (nint)0x1234;
            var call = LastInstruction(codeGen.Emitter);

            Assert.That(call.idIns(), Is.EqualTo(INS_bl));
            Assert.That((nint)call.idAddr().iiaAddr, Is.EqualTo(expectedAddress));
            Assert.That(Arm32HelperCallbacks.Helper,
                Is.EqualTo(usesProfilerMethodHandle ? CORINFO_HELP_UNDEF : helper));
            AssertCalleeTrashAccounting(codeGen);
        }, minOpts: false);
    }

    private static void AssertCalleeTrashAccounting(CodeGen codeGen)
        => Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(),
            Is.EqualTo(new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH)));

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
        => CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    private static Emitter.instrDesc LastInstruction(Emitter emitter)
        => Descriptors(emitter)[^1];

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);
}
