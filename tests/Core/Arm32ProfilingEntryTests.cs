// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
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
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm32ProfilingEntryTests
{
    [Test]
    public static void UnhookedEntryDoesNotEmitInstructionsOrClearScratchState()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            BeginProlog(compiler, codeGen);
            var initRegZeroed = true;

            codeGen.genProfilingEnterCallback(REG_R0, ref initRegZeroed);

            Assert.That(Descriptors(codeGen.Emitter), Is.Empty);
            Assert.That(initRegZeroed, Is.True);
        });
    }

#if DEBUG
    // ARM32 relocatable-instruction recording is skipped by the debug descriptor path.
    [TestCase(false, REG_R0, false)]
    [TestCase(false, REG_R4, true)]
#else
    [TestCase(false, REG_R0, false)]
    [TestCase(false, REG_R4, true)]
    [TestCase(true, REG_R0, false)]
    [TestCase(true, REG_R4, true)]
#endif
    public static void HookedEntryLoadsTheMethodHandleAndCallsTheProfiler(
        bool indirect, regNumber initReg, bool expectedInitRegZeroed)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            BeginProlog(compiler, codeGen);
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHndIndirected = indirect;
            compiler.compProfilerMethHnd = (void*)0x12345678;
            compiler.compCurBB = new BasicBlock(null, null);
            compiler.opts.compReloc = true;
            codeGen.RegSet.rsMaskPreSpillRegArg = new regMaskTP(SRBM_R0);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
            using var callbacks = new Arm32HelperCallTests.HelperCallbacks(
                compiler, IAT_VALUE, ARM32_THUMB_BRANCH24, (void*)0x1234);
            var initRegZeroed = true;

            codeGen.genProfilingEnterCallback(initReg, ref initRegZeroed);

            var descriptors = Descriptors(codeGen.Emitter);
            Assert.That(Arm32HelperCallTests.HelperCallbacks.Helper, Is.EqualTo(CORINFO_HELP_PROF_FCN_ENTER));
            Assert.That(Arm32HelperCallTests.HelperCallbacks.Assertions, Is.Zero);
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_bl));
            Assert.That((nint)descriptors[^1].idAddr().iiaAddr, Is.EqualTo((nint)0x1234));
            if (indirect)
            {
                Assert.That(descriptors.Take(3).Select(static descriptor => descriptor.idIns()),
                    Is.EqualTo((instruction[])[INS_movw, INS_movt, INS_ldr]));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R0));
                Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R0));
                Assert.That(descriptors[2].idReg1(), Is.EqualTo(REG_R0));
                Assert.That(descriptors[0].idIsCnsReloc(), Is.True);
                Assert.That(descriptors[1].idIsCnsReloc(), Is.True);
            }
            else
            {
                Assert.That(descriptors.Take(2).Select(static descriptor => descriptor.idIns()),
                    Is.EqualTo((instruction[])[INS_movw, INS_movt]));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R0));
                Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R0));
            }
            Assert.That(initRegZeroed, Is.EqualTo(expectedInitRegZeroed));
        }, captureAssertions: true, minOpts: false);
    }

    private static void BeginProlog(Compiler compiler, CodeGen codeGen)
    {
        codeGen.Emitter.emitBegProlog();
        compiler.unwindBegProlog();
    }

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
        => CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);
}
#endif
