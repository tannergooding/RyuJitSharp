// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.regMask;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterNoGCCallMaskTests
{
    [Test]
    public static void NoGcClassificationDistinguishesMethodsFromHelpers()
    {
        Assert.That(Emitter.emitNoGChelper((CORINFO_METHOD_STRUCT_*)0x2000), Is.False);
        Assert.That(Emitter.emitNoGChelper(Compiler.eeFindHelper(CORINFO_HELP_ASSIGN_REF)), Is.True);
        Assert.That(Emitter.emitNoGChelper(Compiler.eeFindHelper(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT)), Is.False);
    }

    [Test]
    public static void HelperKillMasksAndSavedSetsPreserveAmd64CallRegisters()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var scratch = new regMaskTP(
                compiler.SRBM_INT_CALLEE_TRASH | compiler.SRBM_FLT_CALLEE_TRASH, compiler.SRBM_MSK_CALLEE_TRASH);
            var allInt = new regMaskTP(compiler.SRBM_ALLINT);
            var writeBarrier = new regMaskTP(SRBM_INT_CALLEE_TRASH_INIT);
            var profilerLeave = scratch & ~new regMaskTP(SRBM_FLOATRET | SRBM_INTRET);
            var interfaceTrash = scratch & ~new regMaskTP(SRBM_ARG_REGS | SRBM_FLTARG_REGS);

            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_ASSIGN_REF),
                Is.EqualTo(writeBarrier));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_CHECKED_ASSIGN_REF),
                Is.EqualTo(writeBarrier));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_PROF_FCN_ENTER),
                Is.EqualTo(scratch));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_PROF_FCN_LEAVE),
                Is.EqualTo(profilerLeave));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_PROF_FCN_TAILCALL),
                Is.EqualTo(profilerLeave));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_VALIDATE_INDIRECT_CALL),
                Is.EqualTo(new regMaskTP(compiler.SRBM_INT_CALLEE_TRASH & ~(SRBM_R10 | SRBM_RCX))));
            Assert.That(emitter.emitGetGCRegsKilledByNoGCCall(CORINFO_HELP_INIT_PINVOKE_FRAME),
                Is.EqualTo(scratch));

            Assert.That(emitter.emitGetGCRegsSavedOrModified(Compiler.eeFindHelper(CORINFO_HELP_ASSIGN_REF)),
                Is.EqualTo(allInt & ~writeBarrier));
            Assert.That(emitter.emitGetGCRegsSavedOrModified(
                    Compiler.eeFindHelper(CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT)),
                Is.EqualTo(allInt & ~interfaceTrash));
            Assert.That(emitter.emitGetGCRegsSavedOrModified((CORINFO_METHOD_STRUCT_*)0x2000),
                Is.EqualTo(new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED,
                    SRBM_MSK_CALLEE_SAVED)));
        });
    }
}
#endif
