// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.CorJitAllocMemFlag;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenEmissionPhaseTests
{
#if DEBUG
    private static string? s_assertion;
#endif

    [Test]
    public static void GenerationFinalizesFrameAndMaterializesReturnBeforeBinding()
    {
        CompilerFinalFrameLayoutTests.WithFrame((compiler, codeGen) =>
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
            compiler.opts.compDbgInfo = true;
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

            WithAllocationContext(compiler, codeGen, (_, generator, state) =>
            {
#if DEBUG
                var savedHalt = HaltSelection(ref JitConfig);
                var savedHash = HaltHash(ref JitConfig);
                var savedEmitterTests = EmitterTests(ref JitConfig);
                HaltSelection(ref JitConfig) = new(null, null);
                HaltHash(ref JitConfig) = -1;
                EmitterTests(ref JitConfig) = new(null, null);
                try
                {
                    generator.genGenerateMachineCode();
                }
                finally
                {
                    HaltSelection(ref JitConfig) = savedHalt;
                    HaltHash(ref JitConfig) = savedHash;
                    EmitterTests(ref JitConfig) = savedEmitterTests;
                }
#else
                generator.genGenerateMachineCode();
#endif

                Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.FINAL_FRAME_LAYOUT));
                Assert.That(block.bbEmitCookie, Is.Not.Null);
                Assert.That(generator.Emitter.emitCurIG, Is.Null);
                Assert.That(generator.Emitter.emitGetFirstPrologIG().igFlags & InsGroupFlags.Placeholder,
                    Is.EqualTo(InsGroupFlags.None));
                var epilog = CodeGenBlockDriverTests.LastPlaceholder(generator.Emitter)
                    ?? throw new AssertionException("Missing generated epilog reservation.");
                Assert.That(epilog.igPhData, Is.Null);
                Assert.That(compiler.funCurrentFunc().unwindHeader.Version, Is.EqualTo(1));
                Assert.That(state->Calls, Is.Zero);
            });
        });
    }

    [Test]
    public static void EmissionReservesUnwindBeforeAllocationAndPublishesExecutableCode()
    {
        WithEmission((compiler, codeGen, state) =>
        {
            void* hotCode = null;
            var nativeSize = -1;
            CodePointerAddress(codeGen) = &hotCode;
            NativeSizeAddress(codeGen) = &nativeSize;
#if DEBUG
            Assert.That(compiler.opts.dspMetrics, Is.False);
#endif

            var output = Capture(codeGen.genEmitMachineCode);

            Assert.That(state->Calls, Is.EqualTo(2));
            Assert.That(state->ReserveOrder, Is.EqualTo(1));
            Assert.That(state->AllocateOrder, Is.EqualTo(2));
            Assert.That(state->ReservedSize, Is.EqualTo(4));
            Assert.That(state->AllocatedSize, Is.EqualTo(2));
            Assert.That(state->InvalidRequests, Is.Zero);
            Assert.That((nuint)hotCode, Is.EqualTo((nuint)state->HotExec));
            Assert.That((nuint)CodePointerRW(codeGen), Is.EqualTo((nuint)state->HotRW));
            Assert.That((nuint)ColdCodePointer(codeGen), Is.EqualTo((nuint)0));
            Assert.That((nuint)ColdCodePointerRW(codeGen), Is.EqualTo((nuint)0));
            Assert.That(nativeSize, Is.EqualTo(2));
            Assert.That(CodeSize(codeGen), Is.EqualTo(2u));
            Assert.That(PrologSize(codeGen), Is.EqualTo(1u));
            Assert.That(EpilogSize(codeGen), Is.Zero);
            Assert.That(compiler.info.compNativeCodeSize, Is.EqualTo(2));
            Assert.That(compiler.info.compTotalHotCodeSize, Is.EqualTo(2));
            Assert.That(new ReadOnlySpan<byte>(state->HotRW, 2).ToArray(),
                Is.EqualTo(Convert.FromHexString("90C3")));
            Assert.That(state->HotExec[0], Is.EqualTo(0xA5));
            Assert.That(output, Does.Not.Contain("; Total bytes of code"));
#if DEBUG
            Assert.That(compiler.compCodeGenDone, Is.True);
#endif
        });
    }

#if DEBUG
#if LATE_DISASM
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void LateDisassemblyRejectsBeforePhaseMutationOrPublication(int phaseNumber)
    {
        WithEmission((compiler, codeGen, state) =>
        {
            void* hotCode = null;
            var nativeSize = -1;
            CodePointerAddress(codeGen) = &hotCode;
            NativeSizeAddress(codeGen) = &nativeSize;
            compiler.info.compTotalHotCodeSize = 19;
            compiler.lvaDoneFrameLayout = Compiler.TENTATIVE_FRAME_LAYOUT;
            compiler.opts.doLateDisasm = true;
            Action phase = phaseNumber switch
            {
                0 => codeGen.genGenerateMachineCode,
                1 => codeGen.genEmitMachineCode,
                2 => codeGen.genEmitUnwindDebugGCandEH,
                _ => throw new ArgumentOutOfRangeException(nameof(phaseNumber)),
            };

            var error = Assert.Throws<FatalJitException>(() => phase());

            Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(state->Calls, Is.Zero);
            Assert.That((nuint)hotCode, Is.EqualTo((nuint)0));
            Assert.That(nativeSize, Is.EqualTo(-1));
            Assert.That(compiler.info.compTotalHotCodeSize, Is.EqualTo(19));
            Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.TENTATIVE_FRAME_LAYOUT));
        });
    }
#endif

    [Test]
    public static void MetricsModeRejectsBeforeCodeSizingReservationAllocationAndPublication()
    {
        WithEmission((compiler, codeGen, state) =>
        {
            var hotCode = (void*)0x1110;
            var nativeSize = 67;
            CodePointerAddress(codeGen) = &hotCode;
            NativeSizeAddress(codeGen) = &nativeSize;
            CodePointerRW(codeGen) = (void*)0x2220;
            ColdCodePointer(codeGen) = (void*)0x3330;
            ColdCodePointerRW(codeGen) = (void*)0x4440;
            CodeSize(codeGen) = 77;
            PrologSize(codeGen) = 88;
            EpilogSize(codeGen) = 99;
            compiler.info.compNativeCodeSize = 55;
            compiler.info.compTotalHotCodeSize = 19;
            compiler.info.compTotalColdCodeSize = 13;
            compiler.opts.dspMetrics = true;

            var error = Assert.Throws<FatalJitException>(codeGen.genEmitMachineCode);

            Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(state->Calls, Is.Zero);
            Assert.That((nuint)hotCode, Is.EqualTo((nuint)0x1110));
            Assert.That((nuint)CodePointerRW(codeGen), Is.EqualTo((nuint)0x2220));
            Assert.That((nuint)ColdCodePointer(codeGen), Is.EqualTo((nuint)0x3330));
            Assert.That((nuint)ColdCodePointerRW(codeGen), Is.EqualTo((nuint)0x4440));
            Assert.That(nativeSize, Is.EqualTo(67));
            Assert.That(CodeSize(codeGen), Is.EqualTo(77u));
            Assert.That(PrologSize(codeGen), Is.EqualTo(88u));
            Assert.That(EpilogSize(codeGen), Is.EqualTo(99u));
            Assert.That(compiler.info.compNativeCodeSize, Is.EqualTo(55));
            Assert.That(compiler.info.compTotalHotCodeSize, Is.EqualTo(19));
            Assert.That(compiler.info.compTotalColdCodeSize, Is.EqualTo(13));
        });
    }

    [Test]
    public static void ForcedFallbackOccursBeforeUnwindReservationAndCodeAllocation()
    {
        WithEmission((compiler, codeGen, state) =>
        {
            void* hotCode = null;
            var nativeSize = -1;
            CodePointerAddress(codeGen) = &hotCode;
            NativeSizeAddress(codeGen) = &nativeSize;
            var previous = ForceFallback(ref Globals.JitConfig);
            try
            {
                ForceFallback(ref Globals.JitConfig) = 1;
                _ = Assert.Throws<FatalJitException>(codeGen.genEmitMachineCode);
            }
            finally
            {
                ForceFallback(ref Globals.JitConfig) = previous;
            }

            Assert.That(state->Calls, Is.Zero);
            Assert.That((nuint)hotCode, Is.EqualTo((nuint)0));
            Assert.That(nativeSize, Is.EqualTo(-1));
        });
    }
#endif

#if TRACK_LSRA_STATS && DEBUG
    [Test]
    public static void LsraSummaryIncludesBlockZeroAndWeightedPreResolutionBlocksOnly()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compFloatingPointUsed = true;
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
            CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
            CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
            CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
            var first = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var resolution = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            first.Next = resolution;
            resolution.Prev = first;
            first.bbWeight = 2.5;
            resolution.bbWeight = 100;
            compiler.fgFirstBB = first;
            compiler.fgLastBB = resolution;
            compiler.fgBBcount = 2;
            compiler.fgBBNumMax = resolution.bbNum;
            compiler.fgPredsComputed = true;

            codeGen.CopyRegisterInfo();
            var allocator = new LinearScan(compiler);
            var blockInfo = new LsraBlockInfo[resolution.bbNum + 1];
            blockInfo[0].weight = 1.25;
            blockInfo[0].stats = new uint[(int)LsraStat.COUNT];
            blockInfo[first.bbNum].stats = new uint[(int)LsraStat.COUNT];
            blockInfo[resolution.bbNum].stats = new uint[(int)LsraStat.COUNT];
            var entryStats = blockInfo[0].stats ?? throw new AssertionException("Missing entry statistics.");
            var firstStats = blockInfo[first.bbNum].stats ?? throw new AssertionException("Missing block statistics.");
            var resolutionStats = blockInfo[resolution.bbNum].stats
                ?? throw new AssertionException("Missing resolution statistics.");
            entryStats[(int)LsraStat.STAT_SPILL] = 2;
            entryStats[(int)LsraStat.STAT_FREE] = 5;
            firstStats[(int)LsraStat.STAT_SPILL] = 3;
            firstStats[(int)LsraStat.STAT_COPY_REG] = 4;
            resolutionStats[(int)LsraStat.STAT_SPILL] = 99;
            BlockInfo(allocator) = blockInfo;
            BbNumMaxBeforeResolution(allocator) = (uint)first.bbNum;

            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            writer.Write("prefix");
            allocator.dumpLsraStatsSummary(writer);
            writer.Flush();
            var output = Encoding.UTF8.GetString(stream.ToArray());

            Assert.That(output, Is.EqualTo("prefix, SpillCount 5 SpillCountWt 10.000000" +
                ", CopyReg 4 CopyRegWt 10.000000" +
                ", ResolutionMovs 0 ResolutionMovsWt 0.000000" +
                ", SplitEdges 0 SplitEdgesWt 0.000000"));
        });
    }
#endif

    private static void WithEmission(EmissionAction action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            compiler.genRichIPmappings = [];
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.info.compMatchedVM = true;

            WithAllocationContext(compiler, codeGen, (currentCompiler, currentCodeGen, state) =>
            {
                var emitter = currentCodeGen.Emitter;
                emitter.emitIns(INS_ret);
                emitter.emitStartPrologEpilogGeneration();
                emitter.emitBegProlog();
                currentCompiler.unwindBegProlog();
                emitter.emitIns(INS_nop);
                emitter.emitMarkPrologEnd();
                currentCompiler.unwindEndProlog();
                emitter.emitEndProlog();
                emitter.emitFinishPrologEpilogGeneration();
                emitter.emitJumpDistBind();
                Assert.That(emitter.emitCurIG, Is.Null);
                action(currentCompiler, currentCodeGen, state);
            });
        });
    }

    private static void WithAllocationContext(Compiler compiler, CodeGen codeGen, EmissionAction action)
    {
        var arena = stackalloc byte[160];
        var hotExec = (byte*)(((nuint)arena + 15u) & ~(nuint)15);
        var hotRW = hotExec + 64;
        new Span<byte>(hotExec, 128).Fill(0xA5);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.reserveUnwindInfo =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, bool, bool, int, void>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, byte, byte, int, void>)&Reserve;
        vtable.allocMem = &Allocate;
#if DEBUG
        vtable.doAssert = &RecordAssertion;
#endif
        var state = new EmissionState
        {
            JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            HotExec = hotExec,
            HotRW = hotRW,
        };
        compiler.info.compCompHnd = &state.JitInfo;
        codeGen.Emitter.emitCmpHandle = &state.JitInfo;
#if DEBUG
        using var tls = new JitTls(&state.JitInfo);
        JitTls.Compiler = compiler;
        s_assertion = null;
        try
        {
            action(compiler, codeGen, &state);
        }
        finally
        {
            var assertion = s_assertion;
            s_assertion = null;
            if (assertion is not null)
            {
                Assert.Fail($"JIT assertion: {assertion}");
            }
        }
#else
        action(compiler, codeGen, &state);
#endif
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        finally
        {
            s_jitstdout = previous;
        }
    }

    private delegate void EmissionAction(Compiler compiler, CodeGen codeGen, EmissionState* state);

    private struct EmissionState
    {
        public ICorJitInfo JitInfo;
        public byte* HotExec;
        public byte* HotRW;
        public int Calls;
        public int ReserveOrder;
        public int AllocateOrder;
        public int ReservedSize;
        public int AllocatedSize;
        public int InvalidRequests;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Reserve(ICorJitInfo* jitInfo, byte isFunclet, byte isCold, int size)
    {
        var state = (EmissionState*)jitInfo;
        state->ReserveOrder = ++state->Calls;
        state->ReservedSize = size;
        if (isFunclet != 0 || isCold != 0)
        {
            state->InvalidRequests++;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Allocate(ICorJitInfo* jitInfo, AllocMemArgs* args)
    {
        var state = (EmissionState*)jitInfo;
        state->AllocateOrder = ++state->Calls;
        if (args->chunksCount != 1 || args->xcptnsCount != 0)
        {
            state->InvalidRequests++;
        }

        for (var index = 0; index < args->chunksCount; index++)
        {
            ref var chunk = ref args->chunks[index];
            if (chunk.flags != CORJIT_ALLOCMEM_HOT_CODE || chunk.size > 64)
            {
                state->InvalidRequests++;
                continue;
            }
            state->AllocatedSize = chunk.size;
            chunk.block = state->HotExec;
            chunk.blockRW = state->HotRW;
        }
    }

#if DEBUG
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_coldCodePtr")]
    private static extern ref void* ColdCodePointer(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_coldCodePtrRW")]
    private static extern ref void* ColdCodePointerRW(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_codeSize")]
    private static extern ref uint CodeSize(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_prologSize")]
    private static extern ref uint PrologSize(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_epilogSize")]
    private static extern ref uint EpilogSize(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regAlloc")]
    private static extern ref IRegAlloc? Allocator(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitForceFallback")]
    private static extern ref int ForceFallback(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitHalt")]
    private static extern ref JitConfigValues.MethodSet HaltSelection(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitHashHalt")]
    private static extern ref int HaltHash(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEmitUnitTests")]
    private static extern ref JitConfigValues.MethodSet EmitterTests(ref JitConfigValues config);
#endif

#if TRACK_LSRA_STATS && DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint BbNumMaxBeforeResolution(LinearScan allocator);
#endif
}
#endif
