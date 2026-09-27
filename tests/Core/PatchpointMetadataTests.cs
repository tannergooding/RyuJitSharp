// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Compiler;
using static RyuJitSharp.CorInfoOptions;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class PatchpointMetadataTests
{
    [Test]
    public static void MethodsWithoutPatchpointsDoNotCallTheEE()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.generatePatchpointInfo();
    }

    [TestCase(false, -1)]
    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    [TestCase(true, 1)]
    [TestCase(false, 2)]
    [TestCase(true, 2)]
    public static void PublicationPreservesFrameLayoutAndEEOwnership(bool shadow, int contextKind)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.allocateArray = &Allocate;
            vtable.Base.Base.setPatchpointInfo = &Publish;
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(8)];
            var context = new Context
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                Storage = storage,
            };
            CORINFO_METHOD_INFO methodInfo = default;
            compiler.info.compMethodInfo = &methodInfo;
            compiler.info.compCompHnd = &context.JitInfo;
            compiler.info.compLocalsCount = 8;
            compiler.info.compIsStatic = contextKind != 2;
            compiler.MethodHasPatchpoint = true;
            compiler.lvaCount = 9;
            compiler.lvaTable = new LclVarDsc[9];

            for (var index = 0; index < compiler.lvaCount; index++)
            {
                ref var local = ref compiler.lvaGetDesc(index);
                local.Type = TYP_INT;
                local.lvOnFrame = true;
                local.lvFramePointerBased = true;
                local.StackOffset = -8 * (index + 1);
                local.lvHasLdAddrOp = index is 1 or 8;
            }

            if (shadow)
            {
                compiler.gsShadowVarInfo = new Compiler.ShadowParamVarInfo[8];
                for (var index = 0; index < 8; index++)
                {
                    compiler.gsShadowVarInfo[index] = new Compiler.ShadowParamVarInfo();
                }
                compiler.gsShadowVarInfo[0].ShadowCopy = 8;
            }

            compiler.lvaDoneFrameLayout = REGALLOC_FRAME_LAYOUT;
            compiler.lvaCachedGenericContextArgOffs = -80;
            if (contextKind == 1)
            {
                methodInfo.options = CORINFO_GENERICS_CTXT_FROM_METHODDESC;
                compiler.info.compTypeCtxtArg = 0;
            }
            else if (contextKind == 2)
            {
                methodInfo.options = CORINFO_GENERICS_CTXT_FROM_THIS;
                compiler.lvaTable[0].Type = TYP_REF;
                compiler.lvaGenericsContextInUse = true;
            }

            compiler.compGSReorderStackLayout = contextKind >= 0;
            compiler.lvaGSSecurityCookie = contextKind >= 0 ? 2 : BAD_VAR_NUM;
            compiler.lvaMonAcquired = contextKind >= 0 ? 3 : BAD_VAR_NUM;
            compiler.lvaResumedIndicator = contextKind >= 0 ? 4 : BAD_VAR_NUM;
            compiler.lvaAsyncThreadObjectVar = contextKind >= 0 ? 5 : BAD_VAR_NUM;
            compiler.lvaAsyncExecutionContextVar = contextKind >= 0 ? 6 : BAD_VAR_NUM;
            compiler.lvaAsyncSynchronizationContextVar = contextKind >= 0 ? 7 : BAD_VAR_NUM;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 80;
            codeGen.resetFramePointerUsedWritePhase();
            codeGen.IsFramePointerUsed = true;
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_R12 | RBM_RAX | RBM_XMM6);
            compiler.lvaDoneFrameLayout = FINAL_FRAME_LAYOUT;

            compiler.generatePatchpointInfo();

            Assert.That(context.Allocations, Is.EqualTo(1));
            Assert.That(context.Publications, Is.EqualTo(1));
            Assert.That(context.Bytes, Is.EqualTo((nint)PatchpointInfo.ComputeSize(8)));
            Assert.That(context.Published == (PatchpointInfo*)storage, Is.True);
            var result = context.Published;
            Assert.That(result->NumberOfLocals, Is.EqualTo(8));
            Assert.That(result->TotalFrameSize, Is.EqualTo(104));
            Assert.That(result->Offset(0), Is.EqualTo(shadow ? -72 : -8));
            Assert.That(result->IsExposed(0), Is.EqualTo(shadow));
            Assert.That(result->Offset(1), Is.EqualTo(-16));
            Assert.That(result->IsExposed(1), Is.True);
            Assert.That(result->GenericContextArgOffset, Is.EqualTo(contextKind == 1 ? -80 : -1));
            Assert.That(result->KeptAliveThisOffset, Is.EqualTo(contextKind == 2 ? -80 : -1));
            Assert.That(result->SecurityCookieOffset, Is.EqualTo(contextKind >= 0 ? -24 : -1));
            Assert.That(result->MonitorAcquiredOffset, Is.EqualTo(contextKind >= 0 ? -32 : -1));
            Assert.That(result->ResumedIndicatorOffset, Is.EqualTo(contextKind >= 0 ? -40 : -1));
            Assert.That(result->AsyncThreadOffset, Is.EqualTo(contextKind >= 0 ? -48 : -1));
            Assert.That(result->AsyncExecutionContextOffset, Is.EqualTo(contextKind >= 0 ? -56 : -1));
            Assert.That(result->AsyncSynchronizationContextOffset, Is.EqualTo(contextKind >= 0 ? -64 : -1));
            Assert.That(result->CalleeSaveRegisters,
                Is.EqualTo((long)(RBM_RBX | RBM_R12 | RBM_XMM6 | RBM_RBP).Lower));

            compiler.compDone();
            Assert.That(result->Offset(0), Is.EqualTo(shadow ? -72 : -8));
        });
    }

    private struct Context
    {
        public ICorJitInfo JitInfo;
        public byte* Storage;
        public PatchpointInfo* Published;
        public nint Bytes;
        public int Allocations;
        public int Publications;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Allocate(ICorJitInfo* jitInfo, nint bytes)
    {
        var context = (Context*)jitInfo;
        context->Allocations++;
        context->Bytes = bytes;
        return context->Storage;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Publish(ICorJitInfo* jitInfo, PatchpointInfo* patchpoint)
    {
        var context = (Context*)jitInfo;
        context->Publications++;
        context->Published = patchpoint;
    }
}
