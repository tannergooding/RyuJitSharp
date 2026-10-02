// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
#if TARGET_AMD64
using System.Runtime.InteropServices;
#endif
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
#if TARGET_X86 && NOGC_WRITE_BARRIERS
        Assert.That(codeGen.genUseOptimizedWriteBarriers(form), Is.True);
#else
        Assert.That(codeGen.genUseOptimizedWriteBarriers(form), Is.False);
#endif
    }

#if TARGET_AMD64
    [TestCase(WBF_BarrierChecked, CORINFO_HELP_CHECKED_ASSIGN_REF)]
    [TestCase(WBF_BarrierUnchecked, CORINFO_HELP_ASSIGN_REF)]
    public static unsafe void WindowsDispatchRecordsTheSelectedHelperCall(
        GCInfo.WriteBarrierForm form, CorInfoHelpFunc expected)
    {
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
#if DEBUG
            Assert.That(codeGen.genWriteBarrierUsed, Is.True);
#endif
        });
    }

    private static CorInfoHelpFunc s_helper;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static unsafe void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        s_helper = helper;
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x1234;

        return lookup->addr;
    }
#endif

    [TestCase(WBF_BarrierChecked, CORINFO_HELP_CHECKED_ASSIGN_REF)]
    [TestCase(WBF_BarrierUnchecked, CORINFO_HELP_ASSIGN_REF)]
    public static void SelectionRetainsTheNativeHelper(GCInfo.WriteBarrierForm form, CorInfoHelpFunc expected)
    {
        var codeGen = Create();
        Assert.That(codeGen.genWriteBarrierHelperForWriteBarrierForm(form), Is.EqualTo(expected));
#if DEBUG
        Assert.That(codeGen.genWriteBarrierUsed, Is.True);
#endif
    }

    [TestCase(WBF_NoBarrier)]
    [TestCase(WBF_BarrierUnknown)]
    [TestCase((GCInfo.WriteBarrierForm)255)]
    public static void InvalidFormsRetainTheExistingSelectionFailure(GCInfo.WriteBarrierForm form)
    {
        var codeGen = Create();
        var error = Assert.Throws<FatalJitException>(() => codeGen.genWriteBarrierHelperForWriteBarrierForm(form));

        Assert.That(error!.Result, Is.EqualTo(CORJIT_INTERNALERROR));
        Assert.That(error.Message, Is.EqualTo($"No write-barrier helper exists for {form}."));
#if DEBUG
        Assert.That(codeGen.genWriteBarrierUsed, Is.True);
#endif
    }

#if !TARGET_XARCH
    [TestCase(WBF_BarrierChecked)]
    [TestCase(WBF_BarrierUnchecked)]
    public static void SharedDispatchReachesTheGenuineHelperCallBoundary(GCInfo.WriteBarrierForm form)
    {
        var codeGen = Create();
        var error = Assert.Throws<FatalJitException>(() => codeGen.genGCWriteBarrier(form));

        Assert.That(error!.Result, Is.EqualTo(CORJIT_SKIPPED));
        Assert.That(error.Message, Is.EqualTo("Helper call generation is not implemented for this target."));
#if DEBUG
        Assert.That(codeGen.genWriteBarrierUsed, Is.True);
#endif
    }

    [TestCase(WBF_NoBarrier)]
    [TestCase(WBF_BarrierUnknown)]
    [TestCase((GCInfo.WriteBarrierForm)255)]
    public static void SharedDispatchRejectsInvalidFormsBeforeCallingTheHelper(GCInfo.WriteBarrierForm form)
    {
        var codeGen = Create();
        var error = Assert.Throws<FatalJitException>(() => codeGen.genGCWriteBarrier(form));

        Assert.That(error!.Result, Is.EqualTo(CORJIT_INTERNALERROR));
        Assert.That(error.Message, Is.EqualTo($"No write-barrier helper exists for {form}."));
#if DEBUG
        Assert.That(codeGen.genWriteBarrierUsed, Is.True);
#endif
    }
#endif

    private static CodeGen Create()
    {
        return (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
    }
}
