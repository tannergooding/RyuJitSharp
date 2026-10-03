// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64CodeGenOSRFrameTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void Tier0CalleeSavesAreRestoredBeforeTheFrameAndUseTheCorrectStackBase(
        bool isVarArgs, bool hasFrameRegisters)
    {
        WithOSRFrame(isVarArgs, hasFrameRegisters, (compiler, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(
                () => codeGen.genOSRHandleTier0CalleeSavedRegistersAndFrame()) ??
                throw new AssertionException("The unported ARM64 unwind allocation dependency did not fail.");

            Assert.That(failure.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("Unwind recording requires Windows AMD64."));

            var descriptors = Arm64CodeGenLocalVariableTests.Descriptors(codeGen.Emitter);
            var instructions = descriptors.Select(descriptor => descriptor.idIns()).ToArray();
            var topOfCalleeSaves = 1024 - (isVarArgs ? MAX_REG_ARG * REGSIZE_BYTES : 0);
            var firstRestoreIndex = hasFrameRegisters ? 0 : 1;
            var restoreBase = hasFrameRegisters ? REG_FP : REG_IP0;
            Assert.That(instructions.Skip(firstRestoreIndex).Take(3),
                Is.EqualTo((instruction[])[INS_ldp, INS_ldp, INS_ldp]));

            if (hasFrameRegisters)
            {
                Assert.That(descriptors[0].idReg3(), Is.EqualTo(REG_FP));
            }
            else
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_add));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_IP0));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_SPBASE));
                Assert.That(Emitter.emitGetInsSC(descriptors[0]), Is.EqualTo((nint)topOfCalleeSaves));
            }

            Assert.That(descriptors[firstRestoreIndex].idReg3(), Is.EqualTo(restoreBase));
            Assert.That(descriptors[firstRestoreIndex + 1].idReg3(), Is.EqualTo(restoreBase));
            Assert.That(descriptors[firstRestoreIndex + 2].idReg1(), Is.EqualTo(REG_FP));
            Assert.That(descriptors[firstRestoreIndex + 2].idReg2(), Is.EqualTo(REG_LR));
            Assert.That(descriptors[firstRestoreIndex + 2].idReg3(), Is.EqualTo(REG_FP));

            if (JitConfig.JitPacEnabled != 0)
            {
                Assert.That(instructions.Skip(firstRestoreIndex + 3),
                    Is.EqualTo((instruction[])[INS_add, INS_mov,
                        TargetOS.IsWindows ? INS_autib1716 : INS_autia1716, INS_mov]));
            }
            else
            {
                Assert.That(instructions, Has.Length.EqualTo(firstRestoreIndex + 3));
            }

            Assert.That(compiler.opts.IsOSR, Is.True);
        });
    }

    private static void WithOSRFrame(bool isVarArgs, bool hasFrameRegisters, Action<Compiler, CodeGen> action)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.info.compIsVarArgs = isVarArgs;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();

            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 1024);
            var calleeSaveRegisters = SRBM_R19 | SRBM_R20 | SRBM_V8 | SRBM_V9;
            if (hasFrameRegisters)
            {
                calleeSaveRegisters |= SRBM_FP | SRBM_LR;
            }
            patchpoint->CalleeSaveRegisters = (long)calleeSaveRegisters;
            compiler.info.compPatchpointInfo = patchpoint;

            action(compiler, codeGen);
        });
    }
}
#endif
