// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && PROFILING_SUPPORTED
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64CodeGenProfilingEntryTests
{
    [Test]
    public static void UnhookedEntryDoesNotEmitInstructionsOrClearScratchState()
    {
        WithEntry((_, codeGen) =>
        {
            var initRegZeroed = true;

            codeGen.genProfilingEnterCallback(REG_R10, ref initRegZeroed);

            Assert.That(Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter), Is.Empty);
            Assert.That(initRegZeroed, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HookedEntryPreparesTheFunctionIdAndCallerStackBeforeTheCallRecorder(bool indirect)
    {
        WithEntry((compiler, codeGen) =>
        {
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHndIndirected = indirect;
            compiler.compProfilerMethHnd = (void*)0x12345678;
            var initRegZeroed = true;

            var failure = Assert.Throws<FatalJitException>(
                () => codeGen.genProfilingEnterCallback(REG_R10, ref initRegZeroed)) ??
                throw new AssertionException("The unported ARM64 call recorder did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("Call instruction recording requires xarch."));

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            Assert.That(descriptors.Any(descriptor => descriptor.idReg1() == REG_R10), Is.True);
            Assert.That(descriptors.Any(descriptor => descriptor.idIns() == INS_ldr
                && descriptor.idReg1() == REG_R10 && descriptor.idReg2() == REG_R10), Is.EqualTo(indirect));

            var callerStackAddress = descriptors.Last(descriptor =>
                descriptor.idReg1() == REG_R11 && descriptor.idIns() is INS_add or INS_sub);
            Assert.That(callerStackAddress.idReg2(),
                Is.EqualTo(codeGen.IsFramePointerUsed ? REG_FPBASE : REG_SPBASE));
        });
    }

    private static void WithEntry(Action<Compiler, CodeGen> action)
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
