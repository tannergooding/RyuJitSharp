// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && PROFILING_SUPPORTED
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64CodeGenProfilingLeaveTests
{
    [Test]
    public static void UnhookedLeaveDoesNotEmitInstructionsOrChangeCallbackState()
    {
        WithLeave((compiler, codeGen) =>
        {
            codeGen.GCInfo.gcRegGCrefSetCur = RBM_R10;
            codeGen.GCInfo.gcRegByrefSetCur = RBM_R11;

            codeGen.genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_LEAVE);

            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
            Assert.That(compiler.info.compProfilerCallback, Is.False);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_R10));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_R11));
        });
    }

    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, false)]
    [TestCase(CORINFO_HELP_PROF_FCN_LEAVE, true)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, false)]
    [TestCase(CORINFO_HELP_PROF_FCN_TAILCALL, true)]
    public static void HookedLeavePreparesAndUntracksCallbackArgumentsBeforeTheCallRecorder(
        CorInfoHelpFunc helper, bool indirect)
    {
        WithLeave((compiler, codeGen) =>
        {
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHndIndirected = indirect;
            compiler.compProfilerMethHnd = (void*)0x12345678;
            codeGen.GCInfo.gcRegGCrefSetCur = RBM_R10;
            codeGen.GCInfo.gcRegByrefSetCur = RBM_R11;

            var failure = Assert.Throws<FatalJitException>(
                () => codeGen.genProfilingLeaveCallback(helper)) ??
                throw new AssertionException("The unported ARM64 call recorder did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("Call instruction recording requires xarch."));
            Assert.That(compiler.info.compProfilerCallback, Is.True);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Any(descriptor => descriptor.idReg1() == REG_R10), Is.True);
            Assert.That(descriptors.Any(descriptor => descriptor.idIns() == INS_ldr
                && descriptor.idReg1() == REG_R10 && descriptor.idReg2() == REG_R10), Is.EqualTo(indirect));

            var callerStackAddress = descriptors.Last(descriptor =>
                descriptor.idReg1() == REG_R11 && descriptor.idIns() is INS_add or INS_sub);
            Assert.That(callerStackAddress.idReg2(), Is.EqualTo(REG_FPBASE));
        });
    }

    private static void WithLeave(Action<Compiler, CodeGen> action)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.info.compMatchedVM = false;
            codeGen.GCInfo.gcVarPtrSetCur = [0];
            codeGen.GCInfo.gcRegGCrefSetCur = default;
            codeGen.GCInfo.gcRegByrefSetCur = default;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;
            compiler.compLclFrameSize = 64;
            compiler.compCalleeRegsPushed = 2;

            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);
}
#endif
