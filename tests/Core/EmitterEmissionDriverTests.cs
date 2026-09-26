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
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.CorJitAllocMemFlag;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterEmissionDriverTests
{
#if DEBUG
    [Test]
    public static void DspCodePrintsWhileRecordingWithoutChangingEmittedBytes()
    {
        WithDriver((compiler, _, emitter, allocation) =>
        {
            compiler.opts.dspCode = true;
            var recording = Capture(() => emitter.emitIns(INS_ret));
            Assert.That(recording, Does.Contain("ret"));
            FinishRecording(compiler, emitter);

            var result = End(compiler, emitter);
            Assert.That(result.Actual, Is.EqualTo(2u));
            Assert.That(new ReadOnlySpan<byte>(allocation->HotRW, 2).ToArray(),
                Is.EqualTo(Convert.FromHexString("90C3")));
            Assert.That(compiler.Metrics.ActualCodeBytes, Is.EqualTo(2));
        });
    }
#endif

    [TestCase(true, false)]
#if DEBUG
    [TestCase(false, true)]
#endif
    public static void DiagnosticIssuingRetainsFastLoopBytesAndMetrics(bool disassembly, bool verbose)
    {
        var (fastBytes, fastSize, fastAllocated, fastActual, fastPerfScore, _) =
            RunSmallMethod(disassembly: false, verbose: false);
        var (diagnosticBytes, diagnosticSize, diagnosticAllocated, diagnosticActual, diagnosticPerfScore, diagnostics) =
            RunSmallMethod(disassembly, verbose);

        Assert.That(diagnosticBytes, Is.EqualTo(fastBytes));
        Assert.That(diagnosticBytes, Is.EqualTo(Convert.FromHexString("90C3")));
        Assert.That(diagnosticSize, Is.EqualTo(fastSize));
        Assert.That(diagnosticAllocated, Is.EqualTo(fastAllocated));
        Assert.That(diagnosticActual, Is.EqualTo(fastActual));
        Assert.That(diagnosticPerfScore, Is.EqualTo(fastPerfScore));
        Assert.That(diagnostics, Does.Contain("G_M000_IG01"));
        Assert.That(diagnostics, Does.Contain(" nop"));
        Assert.That(diagnostics, Does.Contain(" ret"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SavedPrologAndBodyIssueToWritableAliasAndReportActualSize(bool fullyInterruptible)
    {
        WithDriver((compiler, _, emitter, allocation) =>
        {
            emitter.emitIns(INS_ret);
            FinishRecording(compiler, emitter);

            Assert.That(emitter.emitCurIG, Is.Null);
            var result = End(compiler, emitter, fullyInterruptible: fullyInterruptible, exceptions: 2);

            Assert.That(allocation->Calls, Is.EqualTo(1));
            Assert.That(allocation->Collections, Is.EqualTo(1));
            Assert.That(allocation->Exceptions, Is.EqualTo(2));
            Assert.That(allocation->Chunks, Is.EqualTo(1));
            Assert.That(allocation->Hot.flags, Is.EqualTo(CORJIT_ALLOCMEM_HOT_CODE));
            Assert.That(allocation->Hot.size, Is.EqualTo(2));
            Assert.That(allocation->Hot.alignment, Is.EqualTo(1));
            Assert.That(allocation->InvalidRequests, Is.Zero);
            Assert.That(result.Actual, Is.EqualTo(2u));
            Assert.That(result.Prolog, Is.EqualTo(1u));
            Assert.That(result.Epilog, Is.Zero);
#if DEBUG
            Assert.That(result.Instructions, Is.EqualTo(2u));
#endif
            Assert.That((nuint)result.Hot, Is.EqualTo((nuint)allocation->HotExec));
            Assert.That((nuint)result.HotRW, Is.EqualTo((nuint)allocation->HotRW));
            Assert.That((nuint)result.Cold, Is.EqualTo((nuint)0));
            Assert.That((nuint)result.ColdRW, Is.EqualTo((nuint)0));
            Assert.That(emitter.emitFullyInt, Is.EqualTo(fullyInterruptible));
            Assert.That(emitter.emitFullGCinfo, Is.False);
            Assert.That(emitter.emitFullArgInfo, Is.False);
            Assert.That((nuint)emitter.emitCodeBlock, Is.EqualTo((nuint)allocation->HotExec));
            Assert.That(new ReadOnlySpan<byte>(allocation->HotRW, 2).ToArray(),
                Is.EqualTo(Convert.FromHexString("90C3")));
            Assert.That(allocation->HotExec[0], Is.EqualTo(0xA5));
            Assert.That(compiler.Metrics.AllocatedHotCodeBytes, Is.EqualTo(2));
            Assert.That(compiler.Metrics.ActualCodeBytes, Is.EqualTo(2));
        });
    }

    [Test]
    public static void LateInstructionShrinkPadsAllocationWithoutExtendingActualCode()
    {
        WithDriver((compiler, _, emitter, allocation) =>
        {
            emitter.emitIns(INS_ret);
            var body = emitter.emitCurIG ?? throw new AssertionException("Missing body group.");
            FinishRecording(compiler, emitter);
            var id = body.igData?[0] ?? throw new AssertionException("Missing saved return.");
            id.idCodeSize(3);
            body.igSize += 2;
            TotalCodeSize(emitter) += 2;
            emitter.emitTotalHotCodeSize += 2;
            compiler.info.compTotalHotCodeSize += 2;

            var result = End(compiler, emitter);

            Assert.That(allocation->Hot.size, Is.EqualTo(4));
            Assert.That(result.Actual, Is.EqualTo(2u));
            Assert.That(body.igSize, Is.EqualTo(1));
            Assert.That(id.idCodeSize(), Is.EqualTo(1u));
            Assert.That(new ReadOnlySpan<byte>(allocation->HotRW, 4).ToArray(),
                Is.EqualTo(Convert.FromHexString("90C3CCCC")));
            Assert.That(allocation->HotExec[2], Is.EqualTo(0xA5));
            Assert.That(compiler.Metrics.AllocatedHotCodeBytes, Is.EqualTo(4));
            Assert.That(compiler.Metrics.ActualCodeBytes, Is.EqualTo(2));
        });
    }

    [Test]
    public static void ForwardBranchShrinksAcrossGroupsAndPatchesTheIssuedBytes()
    {
        WithDriver((compiler, _, emitter, allocation) =>
        {
            var label = new BasicBlock(null, null);
            label.SetFlags(BBF_HAS_LABEL);
            emitter.emitIns_J(INS_jne, label);
            emitter.emitIns_Nop(3);
            var target = emitter.emitAddInlineLabel();
            label.bbEmitCookie = target;
            emitter.emitIns(INS_ret);

            FinishRecording(compiler, emitter);
            Assert.That(target.igOffs, Is.EqualTo(6u));

            var result = End(compiler, emitter);

            Assert.That(result.Actual, Is.EqualTo(7u));
            Assert.That(allocation->Hot.size, Is.EqualTo(7));
            Assert.That(new ReadOnlySpan<byte>(allocation->HotRW, 7).ToArray(),
                Is.EqualTo(Convert.FromHexString("9075030F1F00C3")));
            Assert.That(allocation->HotExec[1], Is.EqualTo(0xA5));
#if DEBUG
            Assert.That(result.Instructions, Is.EqualTo(4u));
#endif
            Assert.That(compiler.Metrics.ActualCodeBytes, Is.EqualTo(7));
        });
    }

    [Test]
    public static void ColdSplitAndAlignedDataSectionsKeepDistinctWritableAliases()
    {
        WithDriver((compiler, _, emitter, allocation) =>
        {
            emitter.emitIns(INS_nop);
            var coldBlock = new BasicBlock(null, null);
            coldBlock.SetFlags(BBF_COLD | BBF_HAS_LABEL);
            var coldGroup = emitter.emitAddInlineLabel();
            coldBlock.bbEmitCookie = coldGroup;
            compiler.fgFirstColdBlock = coldBlock;
            emitter.emitSetFirstColdIGCookie(coldGroup);
            emitter.emitIns(INS_ret);

            Assert.That(emitter.emitDataConst([0x12, 0x34, 0x56, 0x78], 4, TYP_INT), Is.Zero);
            Assert.That(emitter.emitDataConst([1, 2, 3, 4, 5, 6, 7, 8], 8, TYP_DOUBLE), Is.EqualTo(8u));
            FinishRecording(compiler, emitter, coldGroup);

            var result = End(compiler, emitter);

            Assert.That(result.Actual, Is.EqualTo(3u));
            Assert.That(result.Prolog, Is.EqualTo(1u));
            Assert.That((nuint)result.Hot, Is.EqualTo((nuint)allocation->HotExec));
            Assert.That((nuint)result.HotRW, Is.EqualTo((nuint)allocation->HotRW));
            Assert.That((nuint)result.Cold, Is.EqualTo((nuint)allocation->ColdExec));
            Assert.That((nuint)result.ColdRW, Is.EqualTo((nuint)allocation->ColdRW));
            Assert.That(allocation->Chunks, Is.EqualTo(4));
            Assert.That(allocation->Hot.size, Is.EqualTo(2));
            Assert.That(allocation->Cold.size, Is.EqualTo(1));
            Assert.That(allocation->Cold.flags, Is.EqualTo(CORJIT_ALLOCMEM_COLD_CODE));
            Assert.That(allocation->FirstData.size, Is.EqualTo(4));
            Assert.That(allocation->FirstData.alignment, Is.EqualTo(4));
            Assert.That(allocation->FirstData.flags, Is.EqualTo(CORJIT_ALLOCMEM_READONLY_DATA));
            Assert.That(allocation->SecondData.size, Is.EqualTo(8));
            Assert.That(allocation->SecondData.alignment, Is.EqualTo(8));
            Assert.That(allocation->SecondData.flags, Is.EqualTo(CORJIT_ALLOCMEM_READONLY_DATA));
            Assert.That(allocation->InvalidRequests, Is.Zero);
            Assert.That(emitter.emitNumDataChunks, Is.EqualTo(2));
            Assert.That(emitter.emitDataChunkOffsets[0], Is.Zero);
            Assert.That(emitter.emitDataChunkOffsets[1], Is.EqualTo(8));
            Assert.That((nuint)emitter.emitDataOffsetToPtr(8),
                Is.EqualTo((nuint)allocation->SecondDataExec));
            Assert.That((nuint)emitter.emitDataOffsetToPtr(11),
                Is.EqualTo((nuint)(allocation->SecondDataExec + 3)));
            Assert.That(new ReadOnlySpan<byte>(allocation->HotRW, 2).ToArray(),
                Is.EqualTo(Convert.FromHexString("9090")));
            Assert.That(allocation->ColdRW[0], Is.EqualTo(0xC3));
            Assert.That(new ReadOnlySpan<byte>(allocation->FirstDataRW, 4).ToArray(),
                Is.EqualTo(Convert.FromHexString("12345678")));
            Assert.That(new ReadOnlySpan<byte>(allocation->SecondDataRW, 8).ToArray(),
                Is.EqualTo(Convert.FromHexString("0102030405060708")));
            Assert.That(allocation->HotExec[0], Is.EqualTo(0xA5));
            Assert.That(allocation->ColdExec[0], Is.EqualTo(0xA5));
            Assert.That(allocation->FirstDataExec[0], Is.EqualTo(0xA5));
            Assert.That(allocation->SecondDataExec[0], Is.EqualTo(0xA5));
            Assert.That(compiler.Metrics.AllocatedHotCodeBytes, Is.EqualTo(2));
            Assert.That(compiler.Metrics.AllocatedColdCodeBytes, Is.EqualTo(1));
            Assert.That(compiler.Metrics.ReadOnlyDataBytes, Is.EqualTo(12));
            Assert.That(compiler.Metrics.ActualCodeBytes, Is.EqualTo(3));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FullPointerMapClosesRegisterLifetimesAtTheIssuedEnd(bool useLargeArgumentTable)
    {
        WithDriver((compiler, codeGen, emitter, allocation) =>
        {
            codeGen.IsFullPtrRegMapRequired = true;
            allocation->ExpectInlineArgTracking = useLargeArgumentTable ? 0 : 1;
            allocation->ExpectLargeArgTracking = useLargeArgumentTable ? 1 : 0;
            if (useLargeArgumentTable)
            {
                emitter.emitMaxStackDepth = 80;
            }
            var vars = VarSetOps.MakeEmpty(compiler);
            _ = emitter.emitAddLabel(vars, new regMaskTP(SRBM_RBX), new regMaskTP(SRBM_RDX));
            emitter.emitIns(INS_nop);
            FinishRecording(compiler, emitter);

            var result = End(compiler, emitter, fullPtrMap: true);
            Assert.That(result.Actual, Is.EqualTo(2u));

            var first = RegisterPointerList(ref codeGen.GCInfo);
            Assert.That(first, Is.Not.Null);
            Assert.That(first!.rpdOffs, Is.EqualTo(1u));
            Assert.That(first.rpdCompiler.rpdAdd, Is.EqualTo(SRBM_RBX));
            var second = first.rpdNext;
            Assert.That(second, Is.Not.Null);
            Assert.That(second!.rpdOffs, Is.EqualTo(1u));
            Assert.That(second.rpdCompiler.rpdAdd, Is.EqualTo(SRBM_RDX));
            var third = second.rpdNext;
            Assert.That(third, Is.Not.Null);
            Assert.That(third!.rpdOffs, Is.EqualTo(2u));
            Assert.That(third.rpdCompiler.rpdDel, Is.EqualTo(SRBM_RDX));
            var fourth = third.rpdNext;
            Assert.That(fourth, Is.Not.Null);
            Assert.That(fourth!.rpdOffs, Is.EqualTo(2u));
            Assert.That(fourth.rpdCompiler.rpdDel, Is.EqualTo(SRBM_RBX));
            Assert.That(fourth.rpdNext, Is.Null);
            Assert.That(allocation->ArgumentTrackingPreserved, Is.EqualTo(1));
            Assert.That(allocation->InvalidRequests, Is.Zero);
        });
    }

    [TestCase(TYP_REF, false, 0u)]
    [TestCase(TYP_REF, true, 0u)]
    [TestCase(TYP_BYREF, false, 1u)]
    [TestCase(TYP_BYREF, true, 1u)]
    public static void TrackedFrameSlotsKeepEncodedOffsetsAndDieBeforeTrailingPadding(
        var_types type, bool contiguousPointerLocals, uint byrefFlag)
    {
        WithDriver((compiler, codeGen, emitter, allocation) =>
        {
            ref var local = ref compiler.lvaTable[0];
            local.Type = type;
            local.RegNum = REG_STK;
            local._varIndex = 0;
            local.setLvRefCnt(1);
            _ = emitter.emitAddLabel(VarSetOps.MakeSingleton(compiler, 0),
                new regMaskTP(SRBM_NONE), new regMaskTP(SRBM_NONE));
            emitter.emitIns(INS_ret);
            var body = emitter.emitCurIG ?? throw new AssertionException("Missing tracked body group.");
            FinishRecording(compiler, emitter, frameSlots: true);

            var id = body.igData?[0] ?? throw new AssertionException("Missing saved return.");
            id.idCodeSize(3);
            body.igSize += 2;
            TotalCodeSize(emitter) += 2;
            emitter.emitTotalHotCodeSize += 2;
            compiler.info.compTotalHotCodeSize += 2;

            var result = End(compiler, emitter, contiguousPointerLocals: contiguousPointerLocals);
            Assert.That(FrameOffsets(emitter)[0], Is.EqualTo(-16 + (int)byrefFlag));
            var life = VariablePointerList(ref emitter.GCInfo);
            Assert.That(life, Is.Not.Null);
            Assert.That(life!.vpdVarNum, Is.EqualTo(unchecked((uint)-16) | byrefFlag));
            Assert.That(life.vpdBegOfs, Is.EqualTo(1u));
            Assert.That(life.vpdEndOfs, Is.EqualTo(2u));
            Assert.That(life.vpdNext, Is.Null);
            Assert.That(result.Actual, Is.EqualTo(2u));
            Assert.That(allocation->Hot.size, Is.EqualTo(4));
            Assert.That(new ReadOnlySpan<byte>(allocation->HotRW, 4).ToArray(),
                Is.EqualTo(Convert.FromHexString("90C3CCCC")));
            Assert.That(compiler.Metrics.ActualCodeBytes, Is.EqualTo(2));
        });
    }

    private static void FinishRecording(Compiler compiler, Emitter emitter, insGroup? cold = null,
        bool frameSlots = false)
    {
        emitter.emitStartPrologEpilogGeneration();
        emitter.emitBegProlog();
        if (frameSlots)
        {
            emitter.emitSetFrameRangeGCRs(-16, -8);
        }
        emitter.emitIns(INS_nop);
        emitter.emitMarkPrologEnd();
        emitter.emitEndProlog();
        emitter.emitFinishPrologEpilogGeneration();
        emitter.emitJumpDistBind();

        var total = TotalCodeSize(emitter);
        emitter.emitTotalHotCodeSize = cold is null ? total : checked((int)cold.igOffs);
        emitter.emitTotalColdCodeSize = total - emitter.emitTotalHotCodeSize;
        compiler.info.compTotalHotCodeSize = emitter.emitTotalHotCodeSize;
        compiler.info.compTotalColdCodeSize = emitter.emitTotalColdCodeSize;
    }

    private static (byte[] Bytes, uint Size, int Allocated, int Actual, double PerfScore, string Diagnostics)
        RunSmallMethod(bool disassembly, bool verbose)
    {
        (byte[] Bytes, uint Size, int Allocated, int Actual, double PerfScore, string Diagnostics) result = default;
        WithDriver((compiler, _, emitter, allocation) =>
        {
            emitter.emitIns(INS_ret);
            FinishRecording(compiler, emitter);
            compiler.opts.disAsm = disassembly;
#if DEBUG
            compiler.verbose = verbose;
#endif
            EndResult end = default;
            var diagnostics = Capture(() => end = End(compiler, emitter));
            Assert.That(allocation->Collections, Is.EqualTo(1));
            Assert.That(allocation->InvalidRequests, Is.Zero);
            result = (new ReadOnlySpan<byte>(allocation->HotRW, 2).ToArray(), end.Actual,
                compiler.Metrics.AllocatedHotCodeBytes, compiler.Metrics.ActualCodeBytes,
                compiler.Metrics.PerfScore, diagnostics);
        });
        return result;
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
        }
        finally
        {
            s_jitstdout = previous;
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static EndResult End(Compiler compiler, Emitter emitter, bool fullyInterruptible = false,
        bool fullPtrMap = false, uint exceptions = 0, bool contiguousPointerLocals = false)
    {
        var prolog = uint.MaxValue;
        var epilog = uint.MaxValue;
        void* hot = null;
        void* hotRW = null;
        void* cold = null;
        void* coldRW = null;
#if DEBUG
        var instructions = uint.MaxValue;
#endif
        var actual = emitter.emitEndCodeGen(compiler, contiguousPointerLocals, fullyInterruptible, fullPtrMap, exceptions,
            &prolog, &epilog, &hot, &hotRW, &cold, &coldRW
#if DEBUG
            , &instructions
#endif
            );
        return new EndResult(actual, prolog, epilog, hot, hotRW, cold, coldRW
#if DEBUG
            , instructions
#endif
            );
    }

    private static void WithDriver(DriverAction action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            compiler.genRichIPmappings = [];
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;

            var arena = stackalloc byte[1040];
            var baseAddress = (byte*)(((nuint)arena + 15u) & ~(nuint)15);
            new Span<byte>(baseAddress, 1024).Fill(0xA5);
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.allocMem = &AllocMem;
            var allocation = stackalloc AllocationContext[1];
            *allocation = new AllocationContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                HotExec = baseAddress,
                HotRW = baseAddress + 128,
                ColdExec = baseAddress + 256,
                ColdRW = baseAddress + 384,
                FirstDataExec = baseAddress + 512,
                FirstDataRW = baseAddress + 576,
                SecondDataExec = baseAddress + 640,
                SecondDataRW = baseAddress + 704,
            };
            compiler.info.compCompHnd = &allocation->JitInfo;
            codeGen.Emitter.emitCmpHandle = &allocation->JitInfo;
            var emitterRoot = GCHandle.Alloc(codeGen.Emitter);
            try
            {
                allocation->EmitterRoot = (nint)GCHandle.ToIntPtr(emitterRoot);
                action(compiler, codeGen, codeGen.Emitter, allocation);
            }
            finally
            {
                emitterRoot.Free();
            }
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void AllocMem(ICorJitInfo* jitInfo, AllocMemArgs* args)
    {
        var state = (AllocationContext*)jitInfo;
        state->Calls++;
        state->Exceptions = args->xcptnsCount;
        state->Chunks = args->chunksCount;
        for (var i = 0; i < args->chunksCount; i++)
        {
            ref var chunk = ref args->chunks[i];
            switch (chunk.flags)
            {
                case CORJIT_ALLOCMEM_HOT_CODE:
                {
                    state->Hot = chunk;
                    chunk.block = state->HotExec;
                    chunk.blockRW = state->HotRW;
                    if (chunk.size > 128)
                    {
                        state->InvalidRequests++;
                    }
                    break;
                }

                case CORJIT_ALLOCMEM_COLD_CODE:
                {
                    state->Cold = chunk;
                    chunk.block = state->ColdExec;
                    chunk.blockRW = state->ColdRW;
                    if (chunk.size > 128)
                    {
                        state->InvalidRequests++;
                    }
                    break;
                }

                case CORJIT_ALLOCMEM_READONLY_DATA:
                {
                    if (state->DataCount == 0)
                    {
                        state->FirstData = chunk;
                        chunk.block = state->FirstDataExec;
                        chunk.blockRW = state->FirstDataRW;
                    }
                    else
                    {
                        state->SecondData = chunk;
                        chunk.block = state->SecondDataExec;
                        chunk.blockRW = state->SecondDataRW;
                    }
                    state->DataCount++;
                    if (chunk.size > 64 || state->DataCount > 2)
                    {
                        state->InvalidRequests++;
                    }
                    break;
                }

                default:
                {
                    state->InvalidRequests++;
                    break;
                }
            }
        }

        if (GCHandle.FromIntPtr((IntPtr)state->EmitterRoot).Target is Emitter emitter)
        {
            var dataChunks = (nuint)emitter.emitDataChunks;
            var dataOffsets = (nuint)emitter.emitDataChunkOffsets;
            var argTracking = (nuint)emitter.u2.emitArgTrackTab;
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            state->Collections++;
            if (dataChunks != (nuint)emitter.emitDataChunks ||
                dataOffsets != (nuint)emitter.emitDataChunkOffsets)
            {
                state->InvalidRequests++;
            }
            if (state->ExpectInlineArgTracking != 0)
            {
                fixed (byte* inlineStorage = &emitter.u2.emitArgTrackLcl[0])
                {
                    state->ArgumentTrackingPreserved = !emitter.emitSimpleStkUsed &&
                        argTracking == (nuint)inlineStorage &&
                        argTracking == (nuint)emitter.u2.emitArgTrackTab &&
                        (nuint)emitter.u2.emitArgTrackTop == argTracking ? 1 : 0;
                }
            }
            else if (state->ExpectLargeArgTracking != 0)
            {
                var backing = LargeArgumentTracking(emitter);
                if (backing is not null)
                {
                    fixed (byte* largeStorage = backing)
                    {
                        state->ArgumentTrackingPreserved = !emitter.emitSimpleStkUsed &&
                            argTracking == (nuint)largeStorage &&
                            argTracking == (nuint)emitter.u2.emitArgTrackTab &&
                            (nuint)emitter.u2.emitArgTrackTop == argTracking ? 1 : 0;
                    }
                }
            }
        }
        else
        {
            state->InvalidRequests++;
        }
    }

    private delegate void DriverAction(Compiler compiler, CodeGen codeGen, Emitter emitter,
        AllocationContext* allocation);

    private readonly struct EndResult
    {
        public readonly uint Actual;
        public readonly uint Prolog;
        public readonly uint Epilog;
        public readonly void* Hot;
        public readonly void* HotRW;
        public readonly void* Cold;
        public readonly void* ColdRW;
#if DEBUG
        public readonly uint Instructions;
#endif

        public EndResult(uint actual, uint prolog, uint epilog,
            void* hot, void* hotRW, void* cold, void* coldRW
#if DEBUG
            , uint instructions
#endif
            )
        {
            Actual = actual;
            Prolog = prolog;
            Epilog = epilog;
            Hot = hot;
            HotRW = hotRW;
            Cold = cold;
            ColdRW = coldRW;
#if DEBUG
            Instructions = instructions;
#endif
        }
    }

    private struct AllocationContext
    {
        public ICorJitInfo JitInfo;
        public byte* HotExec;
        public byte* HotRW;
        public byte* ColdExec;
        public byte* ColdRW;
        public byte* FirstDataExec;
        public byte* FirstDataRW;
        public byte* SecondDataExec;
        public byte* SecondDataRW;
        public int Calls;
        public int Chunks;
        public int Exceptions;
        public int DataCount;
        public int InvalidRequests;
        public int Collections;
        public int ExpectInlineArgTracking;
        public int ExpectLargeArgTracking;
        public int ArgumentTrackingPreserved;
        public nint EmitterRoot;
        public AllocMemChunk Hot;
        public AllocMemChunk Cold;
        public AllocMemChunk FirstData;
        public AllocMemChunk SecondData;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitTotalCodeSize")]
    private static extern ref int TotalCodeSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_emissionArgumentTracking")]
    private static extern ref byte[]? LargeArgumentTracking(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsTab")]
    private static extern ref int* FrameOffsets(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcVarPtrList")]
    private static extern ref GCInfo.varPtrDsc? VariablePointerList(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrList")]
    private static extern ref GCInfo.regPtrDsc? RegisterPointerList(ref GCInfo info);
}
#endif
