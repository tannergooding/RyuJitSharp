// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
#if TARGET_AMD64 || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
using System.Runtime.InteropServices;
using static RyuJitSharp.Compiler;
using static RyuJitSharp.CorInfoOptions;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
#endif

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

#if TARGET_AMD64
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
#if SWIFT_SUPPORT
            compiler.lvaSwiftErrorArg = BAD_VAR_NUM;
            codeGen.RegSet = new RegSet(codeGen);
            codeGen.RegSet.rsClearRegsModified();
#endif
            codeGen.RegSet.rsSetRegsModified(RBM_RBX | RBM_R12 | RBM_RAX | RBM_XMM6);
            compiler.lvaDoneFrameLayout = FINAL_FRAME_LAYOUT;

            compiler.generatePatchpointInfo();

            Assert.That(context.Allocations, Is.EqualTo(1));
            Assert.That(context.Publications, Is.EqualTo(1));
            Assert.That(context.CallbackOrder, Is.EqualTo(12));
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
            var expectedSaves = RBM_RBX | RBM_R12 | RBM_RBP;
#if !UNIX_AMD64_ABI
            expectedSaves |= RBM_XMM6;
#endif
            Assert.That(result->CalleeSaveRegisters, Is.EqualTo((long)expectedSaves.Lower));

            compiler.compDone();
            Assert.That(result->Offset(0), Is.EqualTo(shadow ? -72 : -8));
        });
    }
#endif

#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
#if TARGET_ARM64
    [TestCase(false, false, 1, 112, -80)]
    [TestCase(true, false, 2, 112, -16)]
    [TestCase(false, true, 2, 176, -144)]
    [TestCase(true, true, 1, 176, -80)]
#else
    [TestCase(false, false, 1, 112, -32)]
#endif
    public static void NonXarchPublicationAdjustsLocalsAndSpecialOffsets(
        bool saveFpLr, bool varArgs, int contextKind, int frameSize, int offsetAdjust)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.allocateArray = &Allocate;
        vtable.Base.Base.setPatchpointInfo = &Publish;
        var storage = stackalloc byte[PatchpointInfo.ComputeSize(9)];
        var initial = (PatchpointInfo*)storage;
        initial->SetOffsetAndExposure(8, -1234, true);
        var context = new Context
        {
            JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            Storage = storage,
        };
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compCompHnd = &context.JitInfo;
        compiler.info.compLocalsCount = 9;
        compiler.info.compIsStatic = contextKind != 2;
        compiler.info.compIsVarArgs = varArgs;
        compiler.info.compTypeCtxtArg = contextKind == 1 ? 0 : BAD_VAR_NUM;
        methodInfo.options = contextKind == 1
            ? CORINFO_GENERICS_CTXT_FROM_METHODDESC
            : CORINFO_GENERICS_CTXT_FROM_THIS;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.MethodHasPatchpoint = true;
        compiler.lvaCount = 10;
        compiler.lvaTable = new LclVarDsc[10];
        compiler.gsShadowVarInfo = new ShadowParamVarInfo[9];
        for (var index = 0; index < compiler.lvaCount; index++)
        {
            ref var local = ref compiler.lvaGetDesc(index);
            local.Type = index == 0 && contextKind == 2 ? TYP_REF : TYP_INT;
            local.lvOnFrame = true;
            local.lvFramePointerBased = true;
            local.StackOffset = (saveFpLr ? -4 : 4) * (index + 1);
            local.lvHasLdAddrOp = index is 1 or 9;
            if (index < compiler.info.compLocalsCount)
            {
                compiler.gsShadowVarInfo[index] = new ShadowParamVarInfo();
            }
        }
        compiler.gsShadowVarInfo[0].ShadowCopy = 9;
#if TARGET_ARM64
        compiler.lvaTable[8].Type = TYP_SIMD;
        compiler.lvaTable[8].lvOnFrame = false;
        compiler.lvaTable[8].lvFramePointerBased = false;
        compiler.gsShadowVarInfo[8].ShadowCopy = 1000;
#endif
        compiler.lvaCachedGenericContextArgOffs = saveFpLr ? -48 : 48;
        compiler.compGSReorderStackLayout = true;
        compiler.lvaGSSecurityCookie = 2;
        compiler.lvaMonAcquired = 3;
        compiler.lvaResumedIndicator = 4;
        compiler.lvaAsyncThreadObjectVar = 5;
        compiler.lvaAsyncExecutionContextVar = 6;
        compiler.lvaAsyncSynchronizationContextVar = 7;
        compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
        compiler.lvaOutgoingArgSpaceSize.Value = 32;
        compiler.lvaDoneFrameLayout = REGALLOC_FRAME_LAYOUT;
#if SWIFT_SUPPORT
        compiler.lvaSwiftErrorArg = BAD_VAR_NUM;
#endif
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler) { IsFramePointerUsed = true };
            compiler.codeGen = codeGen;
#if TARGET_ARM64
            compiler.compCalleeRegsPushed = 4;
            compiler.compLclFrameSize = 80;
#else
            compiler.compCalleeRegsPushed = 3;
            compiler.compLclFrameSize = 88;
#endif
            codeGen.RegSet.rsClearRegsModified();
#if TARGET_ARM64
            codeGen.SetSaveFpLrWithAllCalleeSavedRegisters(saveFpLr);
            var saved = RBM_R19 | RBM_R20;
            codeGen.RegSet.rsSetRegsModified(saved | RBM_R0);
            var expectedSaves = saveFpLr ? saved | RBM_FP | RBM_LR : saved;
#else
            var saved = RBM_S1;
            codeGen.RegSet.rsSetRegsModified(saved | RBM_A0);
            var expectedSaves = saved | RBM_FP | RBM_RA;
#endif
            compiler.lvaDoneFrameLayout = FINAL_FRAME_LAYOUT;

            compiler.generatePatchpointInfo();

            Assert.That(context.Allocations, Is.EqualTo(1));
            Assert.That(context.Publications, Is.EqualTo(1));
            Assert.That(context.CallbackOrder, Is.EqualTo(12));
            Assert.That(context.Bytes, Is.EqualTo((nint)PatchpointInfo.ComputeSize(9)));
            Assert.That(context.Published == initial, Is.True);
            var result = context.Published;
            Assert.That(result->TotalFrameSize, Is.EqualTo(frameSize));
            Assert.That(result->NumberOfLocals, Is.EqualTo(9));
            Assert.That(result->Offset(0), Is.EqualTo(compiler.lvaTable[9].StackOffset + offsetAdjust));
            Assert.That(result->IsExposed(0), Is.True);
            Assert.That(result->Offset(1), Is.EqualTo(compiler.lvaTable[1].StackOffset + offsetAdjust));
            Assert.That(result->IsExposed(1), Is.True);
#if TARGET_ARM64
            Assert.That(result->Offset(8), Is.EqualTo(-1234));
            Assert.That(result->IsExposed(8), Is.True);
#else
            Assert.That(result->Offset(8), Is.EqualTo(compiler.lvaTable[8].StackOffset + offsetAdjust));
#endif
            var contextOffset = compiler.lvaCachedGenericContextArgOffs + offsetAdjust;
            Assert.That(result->GenericContextArgOffset, Is.EqualTo(contextKind == 1 ? contextOffset : -1));
            Assert.That(result->KeptAliveThisOffset, Is.EqualTo(contextKind == 2 ? contextOffset : -1));
            Assert.That(result->SecurityCookieOffset, Is.EqualTo(compiler.lvaTable[2].StackOffset + offsetAdjust));
            Assert.That(result->MonitorAcquiredOffset, Is.EqualTo(compiler.lvaTable[3].StackOffset + offsetAdjust));
            Assert.That(result->ResumedIndicatorOffset, Is.EqualTo(compiler.lvaTable[4].StackOffset + offsetAdjust));
            Assert.That(result->AsyncThreadOffset, Is.EqualTo(compiler.lvaTable[5].StackOffset + offsetAdjust));
            Assert.That(result->AsyncExecutionContextOffset,
                Is.EqualTo(compiler.lvaTable[6].StackOffset + offsetAdjust));
            Assert.That(result->AsyncSynchronizationContextOffset,
                Is.EqualTo(compiler.lvaTable[7].StackOffset + offsetAdjust));
            Assert.That(result->CalleeSaveRegisters, Is.EqualTo(unchecked((long)expectedSaves.Lower)));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
#endif

#if TARGET_AMD64 || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
    private struct Context
    {
        public ICorJitInfo JitInfo;
        public byte* Storage;
        public PatchpointInfo* Published;
        public nint Bytes;
        public int Allocations;
        public int Publications;
        public int CallbackOrder;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Allocate(ICorJitInfo* jitInfo, nint bytes)
    {
        var context = (Context*)jitInfo;
        context->Allocations++;
        context->CallbackOrder = (context->CallbackOrder * 10) + 1;
        context->Bytes = bytes;
        return context->Storage;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Publish(ICorJitInfo* jitInfo, PatchpointInfo* patchpoint)
    {
        var context = (Context*)jitInfo;
        context->Publications++;
        context->CallbackOrder = (context->CallbackOrder * 10) + 2;
        context->Published = patchpoint;
    }
#endif
}
