// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.CorJitAllocMemFlag;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Phases;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenMetadataPhaseTests
{
#if DEBUG
    private static string? s_assertion;
#endif

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
#if DEBUG
    [TestCase(true, false, true)]
#endif
    public static void CompleteMachineAndMetadataPhasesPublishInNativeOrder(
        bool useDriver, bool debugInfo, bool rejectMetrics)
    {
        var previousConfig = JitConfig;
        try
        {
            RichMappings(ref JitConfig) = 0;
#if DEBUG
            HaltSelection(ref JitConfig) = new(null, null);
            HaltHash(ref JitConfig) = -1;
            EmitterTests(ref JitConfig) = new(null, null);
            RawHexSelection(ref JitConfig) = new(null, null);
            ForceFallback(ref JitConfig) = 0;
#endif
            CompilerFinalFrameLayoutTests.WithFrame((compiler, codeGen) =>
            {
                PrepareVoidReturn(compiler, codeGen, debugInfo);

                var arena = stackalloc byte[1152];
                var hotExec = (byte*)(((nuint)arena + 15u) & ~(nuint)15);
                var hotRW = hotExec + 512;
                new Span<byte>(hotExec, 1024).Fill(0xA5);
                var gcBuffer = stackalloc byte[2048];
                var debugBuffer = stackalloc byte[2048];
                var events = stackalloc PublicationEvent[16];
                ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
                vtable.reserveUnwindInfo =
                    (delegate* unmanaged[MemberFunction]<ICorJitInfo*, bool, bool, int, void>)
                    (delegate* unmanaged[MemberFunction]<ICorJitInfo*, byte, byte, int, void>)&ReserveUnwind;
                vtable.allocMem = &AllocateCode;
                vtable.allocUnwindInfo = &PublishUnwind;
                vtable.allocGCInfo = &AllocateGC;
                vtable.setEHcount = &SetEHCount;
                vtable.setEHinfo = &SetEHInfo;
                vtable.Base.Base.allocateArray = &AllocateArray;
                vtable.Base.Base.setBoundaries = &SetBoundaries;
                vtable.Base.Base.setVars = &SetVars;
#if DEBUG
                vtable.Base.Base.printMethodName = &PrintMethodName;
                vtable.Base.Base.printClassName = &PrintClassName;
                vtable.Base.Base.getMethodClass = &GetMethodClass;
                vtable.doAssert = &RecordAssertion;
#endif
                var state = new PublicationState
                {
                    JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                    HotExec = hotExec,
                    HotRW = hotRW,
                    GCBuffer = gcBuffer,
                    DebugBuffer = debugBuffer,
                    Events = events,
                };
                compiler.info.compCompHnd = &state.JitInfo;
                compiler.info.compMatchedVM = true;
                codeGen.Emitter.emitCmpHandle = &state.JitInfo;
                void* code = null;
                var size = -1;

#if DEBUG
                using var tls = new JitTls(&state.JitInfo);
                JitTls.Compiler = compiler;
                s_assertion = null;
                compiler.opts.dspMetrics = rejectMetrics;
#endif
                if (useDriver)
                {
                    Assert.That((nuint)CodePointerAddress(codeGen), Is.EqualTo((nuint)0));
                    Assert.That((nuint)NativeSizeAddress(codeGen), Is.EqualTo((nuint)0));
                    if (rejectMetrics)
                    {
#if DEBUG
                        var failedCode = (void*)0x1234;
                        var failedSize = -1;
                        var error = Assert.Throws<FatalJitException>(() => codeGen.genGenerateCode(out failedCode, out failedSize));
                        Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
                        Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(PHASE_EMIT_CODE));
                        Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.FINAL_FRAME_LAYOUT));
                        Assert.That(state.EventCount, Is.Zero);
                        Assert.That(state.InvalidRequests, Is.Zero);
                        Assert.That((nuint)failedCode, Is.EqualTo((nuint)0x1234));
                        Assert.That(failedSize, Is.EqualTo(-1));
                        Assert.That((nuint)CodePointerAddress(codeGen), Is.EqualTo((nuint)0));
                        Assert.That((nuint)NativeSizeAddress(codeGen), Is.EqualTo((nuint)0));
                        Assert.That(s_assertion, Is.Null);
                        s_assertion = null;
                        return;
#else
                        Assert.Fail("Metrics rejection is only supported in Debug builds.");
#endif
                    }
                    codeGen.genGenerateCode(out code, out size);
                    Assert.That((nuint)CodePointerAddress(codeGen), Is.EqualTo((nuint)0));
                    Assert.That((nuint)NativeSizeAddress(codeGen), Is.EqualTo((nuint)0));
                    Assert.That(compiler.mostRecentlyActivePhase, Is.EqualTo(PHASE_EMIT_GCEH));
                }
                else
                {
                    CodePointerAddress(codeGen) = &code;
                    NativeSizeAddress(codeGen) = &size;
                    codeGen.genGenerateMachineCode();
                    Assert.That(state.EventCount, Is.Zero);
                    codeGen.genEmitMachineCode();
                    codeGen.genEmitUnwindDebugGCandEH();
                }

#if DEBUG
                var assertion = s_assertion;
                s_assertion = null;
                if (assertion is not null)
                {
                    Assert.Fail($"JIT assertion: {assertion}");
                }
#endif
                Assert.That(state.InvalidRequests, Is.Zero);
                Assert.That(state.EventCount, Is.EqualTo(debugInfo ? 5 : 4));
                PublicationEvent[] expected = debugInfo
                    ? [PublicationEvent.ReserveUnwind, PublicationEvent.AllocateCode,
                        PublicationEvent.PublishUnwind, PublicationEvent.PublishBoundaries, PublicationEvent.AllocateGC]
                    : [PublicationEvent.ReserveUnwind, PublicationEvent.AllocateCode,
                        PublicationEvent.PublishUnwind, PublicationEvent.AllocateGC];
                Assert.That(new ReadOnlySpan<PublicationEvent>(events, state.EventCount).ToArray(),
                    Is.EqualTo(expected));
                Assert.That(state.ReservedUnwindSize, Is.GreaterThanOrEqualTo(4));
                Assert.That(state.PublishedUnwindSize, Is.EqualTo(state.ReservedUnwindSize));
                Assert.That(state.UnwindStart, Is.Zero);
                Assert.That(state.UnwindEnd, Is.EqualTo(size));
                Assert.That(state.BoundaryCount, Is.EqualTo(debugInfo ? compiler.genIPmappings.Count : 0));
                Assert.That(state.DebugAllocations, Is.EqualTo(debugInfo ? 1 : 0));
                if (debugInfo)
                {
                    Assert.That(state.BoundaryCount, Is.GreaterThan(0));
                }
                Assert.That(state.ScopeCalls, Is.Zero);
                Assert.That(state.EHCalls, Is.Zero);
                Assert.That(size, Is.GreaterThan(0));
                Assert.That(state.AllocatedSize, Is.GreaterThanOrEqualTo(size));
                Assert.That(compiler.info.compNativeCodeSize, Is.EqualTo(size));
                Assert.That(compiler.info.compTotalHotCodeSize, Is.EqualTo(state.AllocatedSize));
                Assert.That((nuint)code, Is.EqualTo((nuint)hotExec));
                Assert.That((nuint)CodePointerRW(codeGen), Is.EqualTo((nuint)hotRW));
                Assert.That(hotRW[0], Is.Not.EqualTo(0xA5));
                Assert.That(hotExec[0], Is.EqualTo(0xA5));
                Assert.That(state.GCSize, Is.GreaterThan((nint)0));
                Assert.That((nuint)compiler.compInfoBlkAddr, Is.EqualTo((nuint)gcBuffer));
                Assert.That(compiler.compInfoBlkSize, Is.EqualTo(state.GCSize));
                Assert.That(compiler.Metrics.GCInfoBytes, Is.EqualTo((int)state.GCSize));
#if DEBUG
                Assert.That(codeGen.RegSet.tmpGetAllFree(), Is.True);
#endif
                Assert.That(codeGen.Emitter.emitCurIG, Is.Null);
            });
        }
        finally
        {
            JitConfig = previousConfig;
        }
    }

    private static void PrepareVoidReturn(Compiler compiler, CodeGen codeGen, bool debugInfo)
    {
        compiler.lvaDoneFrameLayout = Compiler.TENTATIVE_FRAME_LAYOUT;
        compiler.lvaTrackedCount = 0;
        compiler.lvaTrackedCountInSizeTUnits = 0;
        compiler.lvaTrackedToVarNum = [];
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.lvaArg0Var = BAD_VAR_NUM;
        compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
        compiler.lvaStubArgumentVar = BAD_VAR_NUM;
        compiler.compCalleeRegsPushed = 0;
        compiler.srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
        compiler.srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
        compiler.srbmMskCalleeTrash = SRBM_MSK_CALLEE_TRASH_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        compiler.info.compIsStatic = true;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
        compiler.opts.compDbgInfo = debugInfo;
        compiler.opts.compScopeInfo = false;
#if LATE_DISASM
        compiler.opts.doLateDisasm = false;
#endif
        compiler.opts.disAsm = false;
#if DEBUG
        compiler.opts.dspMetrics = false;
#endif
        compiler.genIPmappings = [];
        compiler.genRichIPmappings = [];
        compiler.compHndBBtab = [];
        compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
        compiler.compFuncInfoCount = 1;
        compiler.fgFuncletsCreated = true;
        var block = CodeGenBlockDriverTests.Blocks(compiler, BBKinds.BBJ_RETURN)[0];
        block.bbRefs = 1;
        compiler.fgPredsComputed = true;
        codeGen.CopyRegisterInfo();
        codeGen.RegSet.rsClearRegsModified();
        codeGen.RegSet.tmpInit();
        compiler.compRetTypeDesc.InitializeReturnType(compiler, TYP_VOID, null, compiler.info.compCallConv);
        Allocator(compiler) = new LinearScan(compiler);
        codeGen.resetFramePointerUsedWritePhase();
        codeGen.IsFramePointerUsed = true;
    }

    private enum PublicationEvent : byte
    {
        ReserveUnwind,
        AllocateCode,
        PublishUnwind,
        PublishBoundaries,
        AllocateGC,
    }

    private struct PublicationState
    {
        public ICorJitInfo JitInfo;
        public byte* HotExec;
        public byte* HotRW;
        public byte* GCBuffer;
        public byte* DebugBuffer;
        public PublicationEvent* Events;
        public int EventCount;
        public int InvalidRequests;
        public int ReservedUnwindSize;
        public int PublishedUnwindSize;
        public int UnwindStart;
        public int UnwindEnd;
        public int AllocatedSize;
        public int BoundaryCount;
        public int DebugAllocations;
        public int ScopeCalls;
        public int EHCalls;
        public nint GCSize;
    }

    private static void Record(PublicationState* state, PublicationEvent item)
    {
        if (state->EventCount >= 16)
        {
            state->InvalidRequests++;
            return;
        }
        state->Events[state->EventCount++] = item;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void ReserveUnwind(ICorJitInfo* jitInfo, byte isFunclet, byte isCold, int size)
    {
        var state = (PublicationState*)jitInfo;
        Record(state, PublicationEvent.ReserveUnwind);
        state->ReservedUnwindSize = size;
        if ((isFunclet != 0) || (isCold != 0))
        {
            state->InvalidRequests++;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void AllocateCode(ICorJitInfo* jitInfo, AllocMemArgs* args)
    {
        var state = (PublicationState*)jitInfo;
        Record(state, PublicationEvent.AllocateCode);
        if ((args->chunksCount != 1) || (args->xcptnsCount != 0))
        {
            state->InvalidRequests++;
            return;
        }
        ref var chunk = ref args->chunks[0];
        if ((chunk.flags != CORJIT_ALLOCMEM_HOT_CODE) || (chunk.size <= 0) || (chunk.size > 512))
        {
            state->InvalidRequests++;
            return;
        }
        state->AllocatedSize = chunk.size;
        chunk.block = state->HotExec;
        chunk.blockRW = state->HotRW;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void PublishUnwind(ICorJitInfo* jitInfo, byte* hot, byte* cold, int start, int end,
        int size, byte* block, CorJitFuncKind kind)
    {
        var state = (PublicationState*)jitInfo;
        Record(state, PublicationEvent.PublishUnwind);
        state->PublishedUnwindSize = size;
        state->UnwindStart = start;
        state->UnwindEnd = end;
        if ((hot != state->HotExec) || (cold is not null) || (kind != CorJitFuncKind.CORJIT_FUNC_ROOT) ||
            (size < 4) || (block is null) || (block[0] != 1))
        {
            state->InvalidRequests++;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocateArray(ICorJitInfo* jitInfo, nint size)
    {
        var state = (PublicationState*)jitInfo;
        state->DebugAllocations++;
        if (size is <= 0 or > 2048)
        {
            state->InvalidRequests++;
            return null;
        }
        return state->DebugBuffer;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetBoundaries(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method,
        int count, ICorDebugInfo.OffsetMapping* boundaries)
    {
        var state = (PublicationState*)jitInfo;
        Record(state, PublicationEvent.PublishBoundaries);
        state->BoundaryCount = count;
        if ((count < 0) || ((count > 0) && (boundaries != (ICorDebugInfo.OffsetMapping*)state->DebugBuffer)))
        {
            state->InvalidRequests++;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetVars(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method, int count,
        ICorDebugInfo.NativeVarInfo* variables)
    {
        ((PublicationState*)jitInfo)->ScopeCalls++;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetEHCount(ICorJitInfo* jitInfo, int count)
    {
        ((PublicationState*)jitInfo)->EHCalls++;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void SetEHInfo(ICorJitInfo* jitInfo, int index, CORINFO_EH_CLAUSE* clause)
    {
        ((PublicationState*)jitInfo)->EHCalls++;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* AllocateGC(ICorJitInfo* jitInfo, nint size)
    {
        var state = (PublicationState*)jitInfo;
        Record(state, PublicationEvent.AllocateGC);
        state->GCSize = size;
        if (size is <= 0 or > 2048)
        {
            state->InvalidRequests++;
            return null;
        }
        return state->GCBuffer;
    }

#if DEBUG
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetMethodClass(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method)
        => default;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintMethodName(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method,
        byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        buffer[0] = 0;
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintClassName(ICorJitInfo* jitInfo, CORINFO_CLASS_STRUCT_* cls,
        byte* buffer, nint bufferSize, nint* requiredBufferSize)
    {
        buffer[0] = 0;
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* jitInfo, byte* file, int line, byte* expression)
    {
        s_assertion = $"{Marshal.PtrToStringUTF8((nint)file)}:{line}: {Marshal.PtrToStringUTF8((nint)expression)}";
        return 0;
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_codePtr")]
    private static extern ref void** CodePointerAddress(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_nativeSizeOfCode")]
    private static extern ref int* NativeSizeAddress(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_codePtrRW")]
    private static extern ref void* CodePointerRW(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_richDebugInfo")]
    private static extern ref int RichMappings(ref JitConfigValues config);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitHalt")]
    private static extern ref JitConfigValues.MethodSet HaltSelection(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitHashHalt")]
    private static extern ref int HaltHash(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEmitUnitTests")]
    private static extern ref JitConfigValues.MethodSet EmitterTests(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitRawHexCode")]
    private static extern ref JitConfigValues.MethodSet RawHexSelection(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitForceFallback")]
    private static extern ref int ForceFallback(ref JitConfigValues config);
#endif
}
#endif
