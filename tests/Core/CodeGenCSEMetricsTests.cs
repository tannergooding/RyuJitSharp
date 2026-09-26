// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_AMD64 && WINDOWS_AMD64_ABI && DEBUG
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
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CodeGenCSEMetricsTests
{
    private struct EmissionState
    {
        public ICorJitInfo JitInfo;
        public byte* HotCode;
        public byte* HotCodeRW;
        public int Calls;
        public int ReserveOrder;
        public int AllocateOrder;
        public int InvalidRequests;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_codePtr")]
    private static extern ref void** CodePointerAddress(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_nativeSizeOfCode")]
    private static extern ref int* NativeSizeAddress(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEcount")]
    private static extern ref int CseCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSECandidateCount")]
    private static extern ref int CseCandidateCount(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optCSEheuristic")]
    private static extern ref CSE_HeuristicCommon? Heuristic(Compiler compiler);

    [TestCase(false, -1)]
    [TestCase(true, 17)]
    public static void MetricsOnlyConstructsPolicyAndPreservesNativeSummaryFields(bool disassemble, int spmiIndex)
    {
        WithEmission((compiler, codeGen, state, hotCode, nativeSize) =>
        {
            compiler.opts.dspMetrics = true;
            compiler.opts.disAsm = disassemble;
            compiler.info.compMethodSpmiIndex = spmiIndex;
            compiler.info.compFullName = "MetricsFixture()";
            compiler.Metrics.PerfScore = 12.25;
            compiler.Metrics.CseCount = 99;
            CseCount(compiler) = 2;
            CseCandidateCount(compiler) = 3;

            var output = Capture(codeGen.genEmitMachineCode);

            Assert.That(state->Calls, Is.EqualTo(2));
            Assert.That(state->ReserveOrder, Is.EqualTo(1));
            Assert.That(state->AllocateOrder, Is.EqualTo(2));
            Assert.That(state->InvalidRequests, Is.Zero);
            Assert.That((nuint)(*hotCode), Is.EqualTo((nuint)state->HotCode));
            Assert.That(*nativeSize, Is.EqualTo(2));
            Assert.That(Heuristic(compiler), Is.TypeOf<CSE_Heuristic>());
            Assert.That(output, disassemble
                ? Does.Contain("\n; Total bytes of code ")
                : Does.StartWith("; Total bytes of code "));
            Assert.That(output, Does.Contain(
                ", prolog size 1, PerfScore 12.25, instruction count 2, allocated bytes for code 2"));
            Assert.That(output, Does.Contain(
                ", num cse 2 num cand 3 Standard CSE Heuristic seq "));
            Assert.That(output, Does.Not.Contain("num cse 99"));
            Assert.That(output, Does.Contain(spmiIndex >= 0 ? " spmi index 17" : " seq  (MethodHash="));
            Assert.That(output, Does.Contain(
                $"(MethodHash={compiler.info.compMethodHash():x8}) for method MetricsFixture() ({compiler.compGetTieringName(true)})"));
            Assert.That(output.Contains("; ============================================================", StringComparison.Ordinal),
                Is.EqualTo(disassemble));
        });
    }

    [Test]
    public static void MetricsUsePreselectedPolicyWithoutFabricatingEmptyResults()
    {
        WithEmission((compiler, codeGen, state, _, _) =>
        {
            compiler.opts.dspMetrics = true;
            compiler.info.compFullName = "ParameterizedMetricsFixture()";
            compiler.info.compMethodSpmiIndex = -1;
            var heuristic = new CSE_HeuristicParameterized(compiler);
            heuristic.Cleanup();
            Heuristic(compiler) = heuristic;

            var output = Capture(codeGen.genEmitMachineCode);

            Assert.That(state->Calls, Is.EqualTo(2));
            Assert.That(Heuristic(compiler), Is.SameAs(heuristic));
            Assert.That(output, Does.Contain(" num cand 0 Parameterized CSE Heuristic seq 0 params 0.242500,0.247900"));
            Assert.That(output, Does.Not.Contain(" spmi index "));
            Assert.That(output, Does.Not.Contain("; ============================================================"));
        });
    }

    private delegate void EmissionAction(Compiler compiler, CodeGen codeGen, EmissionState* state,
        void** hotCode, int* nativeSize);

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

            var arena = stackalloc byte[160];
            var hotExec = (byte*)(((nuint)arena + 15u) & ~(nuint)15);
            var hotRW = hotExec + 64;
            new Span<byte>(hotExec, 128).Fill(0xA5);
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.reserveUnwindInfo =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, bool, bool, int, void>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, byte, byte, int, void>)&Reserve;
            vtable.allocMem = &Allocate;
            var state = new EmissionState
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                HotCode = hotExec,
                HotCodeRW = hotRW,
            };
            compiler.info.compCompHnd = &state.JitInfo;
            codeGen.Emitter.emitCmpHandle = &state.JitInfo;

            var emitter = codeGen.Emitter;
            emitter.emitIns(INS_ret);
            emitter.emitStartPrologEpilogGeneration();
            emitter.emitBegProlog();
            compiler.unwindBegProlog();
            emitter.emitIns(INS_nop);
            emitter.emitMarkPrologEnd();
            compiler.unwindEndProlog();
            emitter.emitEndProlog();
            emitter.emitFinishPrologEpilogGeneration();
            emitter.emitJumpDistBind();

            void* code = null;
            var size = -1;
            CodePointerAddress(codeGen) = &code;
            NativeSizeAddress(codeGen) = &size;
            action(compiler, codeGen, &state, &code, &size);
        });
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Reserve(ICorJitInfo* jitInfo, byte isFunclet, byte isCold, int size)
    {
        var state = (EmissionState*)jitInfo;
        state->ReserveOrder = ++state->Calls;
        if ((isFunclet != 0) || (isCold != 0) || (size != 4))
        {
            state->InvalidRequests++;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Allocate(ICorJitInfo* jitInfo, AllocMemArgs* args)
    {
        var state = (EmissionState*)jitInfo;
        state->AllocateOrder = ++state->Calls;
        if ((args->chunksCount != 1) || (args->xcptnsCount != 0))
        {
            state->InvalidRequests++;
        }
        for (var index = 0; index < args->chunksCount; index++)
        {
            ref var chunk = ref args->chunks[index];
            if ((chunk.flags != CORJIT_ALLOCMEM_HOT_CODE) || (chunk.size > 64))
            {
                state->InvalidRequests++;
                continue;
            }
            chunk.block = state->HotCode;
            chunk.blockRW = state->HotCodeRW;
        }
    }
}
#endif
