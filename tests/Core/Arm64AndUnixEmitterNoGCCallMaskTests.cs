// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64AndUnixEmitterNoGCCallMaskTests
{
    [Test]
    public static void NoGcHelpersRetainTargetWriteBarrierAndProfilerMasks()
    {
        Assume.That(Configuration.TARGET_ARM64 || Configuration.UNIX_AMD64_ABI);

        WithEmitter((compiler, emitter) =>
        {
            regMaskTP allInt;
            regMaskTP writeBarrier;
            regMaskTP profilerEnter;
            regMaskTP profilerLeave;
            regMaskTP defaultKill;
            regMaskTP interfaceTrash;
            regMaskTP normalSaved;
            regMaskTP validateTrash;

            if (Configuration.TARGET_ARM64)
            {
                allInt = new regMaskTP(SRBM_ALLINT);
                writeBarrier = new regMaskTP(SRBM_CALLEE_GCTRASH_WRITEBARRIER_ARM64);
                profilerEnter = SRBM_CALLEE_TRASH_ARM64 & ~new regMaskTP(
                    SRBM_ARG_REGS | SRBM_ARG_RET_BUFF | SRBM_FLTARG_REGS | SRBM_ARM64_FP);
                profilerLeave = profilerEnter;
                defaultKill = new regMaskTP(SRBM_CALLEE_TRASH_NOGC_ARM64);
                interfaceTrash = SRBM_INTERFACELOOKUP_FOR_SLOT_TRASH;
                normalSaved = new regMaskTP(
                    TargetRuntimeMetadata.IntegerCalleeSavedMask | TargetRuntimeMetadata.FloatingCalleeSavedMask);
                validateTrash = new regMaskTP(SRBM_VALIDATE_INDIRECT_CALL_TRASH_ARM64);
            }
            else
            {
                allInt = new regMaskTP(compiler.SRBM_ALLINT);
                writeBarrier = new regMaskTP(SRBM_INT_CALLEE_TRASH_INIT);
                var scratch = new regMaskTP(
                    compiler.SRBM_INT_CALLEE_TRASH | compiler.SRBM_FLT_CALLEE_TRASH,
                    compiler.SRBM_MSK_CALLEE_TRASH);
                profilerEnter = scratch & ~new regMaskTP(SRBM_ARG_REGS | SRBM_FLTARG_REGS);
                profilerLeave = scratch & ~new regMaskTP(
                    SRBM_FLOATRET | SRBM_INTRET | SRBM_FLOATRET_1_AMD64 | SRBM_INTRET_1_AMD64);
                defaultKill = scratch;
                interfaceTrash = profilerEnter;
                normalSaved = new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED,
                    SRBM_MSK_CALLEE_SAVED);
                validateTrash = new regMaskTP(compiler.SRBM_INT_CALLEE_TRASH & ~(SRBM_R10 | SRBM_RCX));
            }

            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_ASSIGN_REF),
                Is.EqualTo(writeBarrier));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_PROF_FCN_ENTER),
                Is.EqualTo(profilerEnter));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_PROF_FCN_LEAVE),
                Is.EqualTo(profilerLeave));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_PROF_FCN_TAILCALL),
                Is.EqualTo(profilerLeave));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_VALIDATE_INDIRECT_CALL),
                Is.EqualTo(validateTrash));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_INIT_PINVOKE_FRAME),
                Is.EqualTo(defaultKill));

            Assert.That(emitter.emitGetGCRegsSavedOrModified(Compiler.eeFindHelper(CORINFO_HELP_ASSIGN_REF)),
                Is.EqualTo(allInt & ~writeBarrier));
            Assert.That(emitter.emitGetGCRegsSavedOrModified(
                    Compiler.eeFindHelper(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT)),
                Is.EqualTo(allInt & ~interfaceTrash));
            Assert.That(emitter.emitGetGCRegsSavedOrModified((CORINFO_METHOD_STRUCT_*)0x2000),
                Is.EqualTo(normalSaved));
        });
    }

    private static void WithEmitter(Action<Compiler, Emitter> test)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        if (Configuration.UNIX_AMD64_ABI)
        {
            AllInt(compiler) = SRBM_ALLINT_INIT;
            IntTrash(compiler) = SRBM_INT_CALLEE_TRASH_INIT;
            FloatTrash(compiler) = SRBM_FLT_CALLEE_TRASH_INIT;
            MaskTrash(compiler) = SRBM_MSK_CALLEE_TRASH_EVEX;
        }
        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        var emitter = new TestEmitter(codeGen);
        emitter.emitBegCG(compiler, default);
        test(compiler, emitter);
    }

    private sealed class TestEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask AllInt(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmIntCalleeTrash")]
    private static extern ref regMask IntTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask FloatTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmMskCalleeTrash")]
    private static extern ref regMask MaskTrash(Compiler compiler);
}
