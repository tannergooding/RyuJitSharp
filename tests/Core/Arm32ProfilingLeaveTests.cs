// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM && PROFILING_SUPPORTED
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.CorInfoReloc;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm32ProfilingLeaveTests
{
    [Test]
    public static void UnhookedLeaveDoesNotEmitInstructionsOrChangeCallbackState()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_LEAVE);

            Assert.That(Descriptors(codeGen.Emitter), Is.Empty);
            Assert.That(compiler.info.compProfilerCallback, Is.False);
        });
    }

#if DEBUG
    // ARM32 relocatable-instruction recording is skipped by the debug descriptor path.
    [TestCase(false)]
#else
    [TestCase(false)]
    [TestCase(true)]
#endif
    public static void HookedReferenceReturnPreservesTheResultAroundTheProfilerCall(bool indirect)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            CORINFO_METHOD_INFO methodInfo = default;
            PrepareLeave(compiler, codeGen, &methodInfo, TYP_REF, indirect);
            codeGen.GCInfo.gcRegGCrefSetCur = new regMaskTP(SRBM_R0);
            using var callbacks = new Arm32HelperCallTests.HelperCallbacks(
                compiler, IAT_VALUE, ARM32_THUMB_BRANCH24, (void*)0x1234);

            codeGen.genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_LEAVE);

            var descriptors = Descriptors(codeGen.Emitter);
            var expectedInstructions = indirect
                ? (instruction[])[INS_mov, INS_movw, INS_movt, INS_ldr, INS_bl, INS_mov]
                : (instruction[])[INS_mov, INS_movw, INS_movt, INS_bl, INS_mov];
            Assert.That(descriptors.Select(static descriptor => descriptor.idIns()), Is.EqualTo(expectedInstructions));
            Assert.That(compiler.info.compProfilerCallback, Is.True);
            Assert.That(Arm32HelperCallTests.HelperCallbacks.Helper, Is.EqualTo(CORINFO_HELP_PROF_FCN_LEAVE));
            Assert.That(Arm32HelperCallTests.HelperCallbacks.Assertions, Is.Zero);
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R2));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_R0));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(descriptors[^1].idReg2(), Is.EqualTo(REG_R2));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(new regMaskTP(SRBM_R0)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));
            if (indirect)
            {
                Assert.That(descriptors[1].idIsCnsReloc(), Is.True);
                Assert.That(descriptors[2].idIsCnsReloc(), Is.True);
            }
        }, captureAssertions: true, minOpts: false);
    }

    [Test]
    public static void TailcallLeaveDoesNotPreserveTheIntegerReturnRegister()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            CORINFO_METHOD_INFO methodInfo = default;
            PrepareLeave(compiler, codeGen, &methodInfo, TYP_REF, indirect: false);
            using var callbacks = new Arm32HelperCallTests.HelperCallbacks(
                compiler, IAT_VALUE, ARM32_THUMB_BRANCH24, (void*)0x1234);

            codeGen.genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_TAILCALL);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Select(static descriptor => descriptor.idIns()),
                Is.EqualTo((instruction[])[INS_movw, INS_movt, INS_bl]));
            Assert.That(compiler.info.compProfilerCallback, Is.True);
            Assert.That(Arm32HelperCallTests.HelperCallbacks.Helper, Is.EqualTo(CORINFO_HELP_PROF_FCN_TAILCALL));
            Assert.That(descriptors.Any(static descriptor =>
                descriptor.idIns() == INS_mov && descriptor.idReg1() == REG_R2), Is.False);
        }, captureAssertions: true, minOpts: false);
    }

    [TestCase(TYP_VOID, false, false)]
    [TestCase(TYP_FLOAT, false, false)]
    [TestCase(TYP_FLOAT, true, false)]
#if CONFIGURABLE_ARM_ABI
    [TestCase(TYP_FLOAT, false, true)]
#endif
    public static void VoidAndFloatingReturnsFollowTheArm32RegisterAbi(
        var_types returnType, bool isVarArgs, bool useSoftFp)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            CORINFO_METHOD_INFO methodInfo = default;
            PrepareLeave(compiler, codeGen, &methodInfo, returnType, indirect: false, isVarArgs: isVarArgs);
            using var callbacks = new Arm32HelperCallTests.HelperCallbacks(
                compiler, IAT_VALUE, ARM32_THUMB_BRANCH24, (void*)0x1234);
#if CONFIGURABLE_ARM_ABI
            compiler.opts.compUseSoftFP = useSoftFp;
            var softFp = useSoftFp;
#else
            var softFp = Compiler.Options.compUseSoftFP;
#endif
            var r0InUse = (returnType is TYP_FLOAT) && (isVarArgs || softFp);

            codeGen.genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_LEAVE);

            var descriptors = Descriptors(codeGen.Emitter);
            var expectedInstructions = r0InUse
                ? (instruction[])[INS_mov, INS_movw, INS_movt, INS_bl, INS_mov]
                : (instruction[])[INS_movw, INS_movt, INS_bl];
            Assert.That(descriptors.Select(static descriptor => descriptor.idIns()), Is.EqualTo(expectedInstructions));
            Assert.That(compiler.info.compProfilerCallback, Is.True);
            Assert.That(Arm32HelperCallTests.HelperCallbacks.Helper, Is.EqualTo(CORINFO_HELP_PROF_FCN_LEAVE));
            Assert.That(Arm32HelperCallTests.HelperCallbacks.Assertions, Is.Zero);
        }, captureAssertions: true, minOpts: false);
    }

    private static void PrepareLeave(
        Compiler compiler, CodeGen codeGen, CORINFO_METHOD_INFO* methodInfo, var_types returnType, bool indirect,
        bool isVarArgs = false)
    {
        methodInfo->args.retTypeClass = NO_CLASS_HANDLE;
        compiler.info.compMethodInfo = methodInfo;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.info.compRetType = returnType;
        compiler.info.compRetNativeType = returnType;
        compiler.info.compIsVarArgs = isVarArgs;
        ProfilerHookNeeded(compiler) = true;
        compiler.compProfilerMethHnd = (void*)0x12345678;
        compiler.compProfilerMethHndIndirected = indirect;
        compiler.opts.compReloc = true;
        compiler.compCurBB = new BasicBlock(null, null);
        codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
        codeGen.GCInfo.gcRegGCrefSetCur = RBM_NONE;
        codeGen.GCInfo.gcRegByrefSetCur = RBM_NONE;
    }

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
        => CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);
}
#endif
