// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.GCInfo.WriteBarrierForm;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class SharedWriteBarrierDispatchTests
{
    [TestCase(WBF_NoBarrier)]
    [TestCase(WBF_BarrierUnknown)]
    [TestCase(WBF_BarrierChecked)]
    [TestCase(WBF_BarrierUnchecked)]
    [TestCase((GCInfo.WriteBarrierForm)255)]
    public static void OptimizedPolicyDoesNotDependOnTheForm(GCInfo.WriteBarrierForm form)
    {
        var codeGen = Create();
        Assert.That(codeGen.genUseOptimizedWriteBarriers(form),
            Is.EqualTo(Configuration.TARGET_X86 && Configuration.NOGC_WRITE_BARRIERS));
    }

    [TestCase(WBF_BarrierChecked, CORINFO_HELP_CHECKED_ASSIGN_REF)]
    [TestCase(WBF_BarrierUnchecked, CORINFO_HELP_ASSIGN_REF)]
    public static unsafe void WindowsDispatchRecordsTheSelectedHelperCall(
        GCInfo.WriteBarrierForm form, CorInfoHelpFunc expected)
    {
        Assume.That(Configuration.TARGET_AMD64);
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getHelperFtn = &GetHelperFtn;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMatchedVM = true;
            s_helper = CORINFO_HELP_UNDEF;

            codeGen.genGCWriteBarrier(form);

            var descriptors = CodeGenShiftTests.Descriptors(codeGen);
            Assert.That(s_helper, Is.EqualTo(expected));
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(instruction.INS_call));
            Assert.That(descriptors[0].idIsNoGC(), Is.True);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(codeGen.genWriteBarrierUsed, Is.True);
            }
        });
    }

    private static CorInfoHelpFunc s_helper;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe void GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        s_helper = helper;
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x1234;

    }

    [TestCase(WBF_BarrierChecked, CORINFO_HELP_CHECKED_ASSIGN_REF)]
    [TestCase(WBF_BarrierUnchecked, CORINFO_HELP_ASSIGN_REF)]
    public static void SelectionRetainsTheNativeHelper(GCInfo.WriteBarrierForm form, CorInfoHelpFunc expected)
    {
        var codeGen = Create();
        Assert.That(codeGen.genWriteBarrierHelperForWriteBarrierForm(form), Is.EqualTo(expected));
        if (TestConfiguration.IsDebug)
        {
            Assert.That(codeGen.genWriteBarrierUsed, Is.True);
        }
    }

    [TestCase(WBF_NoBarrier)]
    [TestCase(WBF_BarrierUnknown)]
    [TestCase((GCInfo.WriteBarrierForm)255)]
    public static void InvalidFormsRetainTheExistingSelectionFailure(GCInfo.WriteBarrierForm form)
    {
        var codeGen = Create();
        var error = Assert.Throws<FatalJitException>(() => codeGen.genWriteBarrierHelperForWriteBarrierForm(form));

        Assert.That(error!.Result, Is.EqualTo(CORJIT_INTERNALERROR));
        Assert.That(error.ErrorCode, Is.EqualTo(FatalJitErrorCode.UnsupportedWriteBarrierForm));
        var expectedMessage = Configuration.DETAILED_FATAL_ERROR_MESSAGES
            ? $"No write-barrier helper exists for {form}."
            : "RJS-0004";
        Assert.That(error.Message, Is.EqualTo(expectedMessage));
        if (TestConfiguration.IsDebug)
        {
            Assert.That(codeGen.genWriteBarrierUsed, Is.True);
        }
    }

    [TestCase(WBF_BarrierChecked)]
    [TestCase(WBF_BarrierUnchecked)]
    public static void SharedDispatchReachesTheGenuineHelperCallBoundary(GCInfo.WriteBarrierForm form)
    {
        Assume.That(!Configuration.TARGET_XARCH);
        var codeGen = Create();
        var error = Assert.Throws<FatalJitException>(() => codeGen.genGCWriteBarrier(form));

        Assert.That(error!.Result, Is.EqualTo(CORJIT_SKIPPED));
        Assert.That(error.Message, Is.EqualTo("Helper call generation is not implemented for this target."));
        if (TestConfiguration.IsDebug)
        {
            Assert.That(codeGen.genWriteBarrierUsed, Is.True);
        }
    }

    [TestCase(WBF_NoBarrier)]
    [TestCase(WBF_BarrierUnknown)]
    [TestCase((GCInfo.WriteBarrierForm)255)]
    public static void SharedDispatchRejectsInvalidFormsBeforeCallingTheHelper(GCInfo.WriteBarrierForm form)
    {
        Assume.That(!Configuration.TARGET_XARCH);
        var codeGen = Create();
        var error = Assert.Throws<FatalJitException>(() => codeGen.genGCWriteBarrier(form));

        Assert.That(error!.Result, Is.EqualTo(CORJIT_INTERNALERROR));
        Assert.That(error.ErrorCode, Is.EqualTo(FatalJitErrorCode.UnsupportedWriteBarrierForm));
        var expectedMessage = Configuration.DETAILED_FATAL_ERROR_MESSAGES
            ? $"No write-barrier helper exists for {form}."
            : "RJS-0004";
        Assert.That(error.Message, Is.EqualTo(expectedMessage));
        if (TestConfiguration.IsDebug)
        {
            Assert.That(codeGen.genWriteBarrierUsed, Is.True);
        }
    }

    private static CodeGen Create()
    {
        return (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
    }
}
