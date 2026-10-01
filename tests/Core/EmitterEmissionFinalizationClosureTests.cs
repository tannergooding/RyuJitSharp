// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
using System;
using System.Runtime.InteropServices;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class EmitterEmissionFinalizationClosureTests
{
    [TestCase(0, 0u)]
    [TestCase(1, 1u)]
    [TestCase(int.MaxValue, 0x7FFFFFFFu)]
    [TestCase(int.MinValue, 0x80000000u)]
    [TestCase(-1, uint.MaxValue)]
    public static void ProfileRunCountPreservesTheNativeUnsignedBits(int runs, uint expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.fgPgoHaveWeights = true;
        compiler.fgNumProfileRuns = runs;

        Assert.That(compiler.fgProfileRunsCount(), Is.EqualTo(expected));
    }

#if !DEBUG
    [TestCase(0)]
    [TestCase(-1)]
    public static void WithoutProfileWeightsTheRunCountReturnsUnity(int storedRuns)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.fgNumProfileRuns = storedRuns;

        Assert.That(compiler.fgProfileRunsCount(), Is.EqualTo(BB_UNITY_WEIGHT_UNSIGNED));
    }
#endif

#if !TARGET_AMD64
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void FinalizationInitializesTargetGcAndEpilogPolicyBeforeAnUnportedDependency(
        bool framePointer, bool fullPointerMap)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new View(compiler) { emitHasFramePtr = framePointer };
        emitter.SetEpilog();
        var prolog = uint.MaxValue;
        var epilog = uint.MaxValue;
        void* hot = null;
        void* hotRW = null;
        void* cold = null;
        void* coldRW = null;
#if DEBUG
        var instructions = uint.MaxValue;
#endif
        FatalJitException? failure = null;
        try
        {
            _ = emitter.emitEndCodeGen(compiler, false, true, fullPointerMap, 0,
                &prolog, &epilog, &hot, &hotRW, &cold, &coldRW
#if DEBUG
                , &instructions
#endif
                );
        }
        catch (FatalJitException exception)
        {
            failure = exception;
        }

        Assert.That(failure, Is.Not.Null);
        Assert.That(failure, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
        Assert.That(emitter.emitFullyInt, Is.True);
        Assert.That(emitter.emitFullGCinfo, Is.EqualTo(fullPointerMap));
#if TARGET_X86
        Assert.That(emitter.emitFullArgInfo, Is.EqualTo(fullPointerMap));
#else
        Assert.That(emitter.emitFullArgInfo, Is.EqualTo(!framePointer));
#endif
#if TARGET_XARCH
        Assert.That(epilog, Is.EqualTo(10u));
#else
        Assert.That(epilog, Is.EqualTo(7u));
#endif
        Assert.That(prolog, Is.EqualTo(uint.MaxValue));
    }

    private sealed class View : Emitter
    {
        public View(Compiler compiler) : base(new CodeGen(compiler))
        {
            _compiler = compiler;
        }

        public void SetEpilog()
        {
            emitEpilogCnt = 1;
            emitEpilogSize = 7;
#if TARGET_XARCH
            emitExitSeqSize = 3;
#endif
        }
    }
#endif

#if TARGET_AMD64 && WINDOWS_AMD64_ABI
    [TestCase(16, 0, 1)]
    [TestCase(17, 0, 32)]
    [TestCase(int.MinValue, 0, 32)]
    [TestCase(2, int.MinValue, 1)]
    public static void AllocationUsesUnsignedCodeWidthsForLoopAlignmentAndColdPresence(
        int hotAllocationSize, int coldAllocationSize, int expectedHotAlignment)
    {
        CodeGenSpillVariableTests.WithCompiler(var_types.TYP_LONG, regNumber.REG_RAX,
            (compiler, codeGen, tree) =>
            {
                compiler.eeInfoInitialized = true;
                compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
                compiler.genRichIPmappings = [];
                compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
                compiler.fgHasLoops = true;
                codeGen.ShouldAlignLoops = true;
                var emitter = codeGen.Emitter;
                var hasCold = coldAllocationSize != 0;
                if (hasCold)
                {
                    emitter.emitIns(instruction.INS_nop);
                    var coldBlock = new BasicBlock(null, null);
                    coldBlock.SetFlags(BasicBlockFlags.BBF_COLD | BasicBlockFlags.BBF_HAS_LABEL);
                    var coldGroup = emitter.emitAddInlineLabel();
                    coldBlock.bbEmitCookie = coldGroup;
                    compiler.fgFirstColdBlock = coldBlock;
                    emitter.emitSetFirstColdIGCookie(coldGroup);
                }
                emitter.emitIns(instruction.INS_ret);
                emitter.emitStartPrologEpilogGeneration();
                emitter.emitBegProlog();
                emitter.emitIns(instruction.INS_nop);
                emitter.emitMarkPrologEnd();
                emitter.emitEndProlog();
                emitter.emitFinishPrologEpilogGeneration();
                emitter.emitJumpDistBind();
                emitter.emitComputeCodeSizes();
                emitter.emitTotalHotCodeSize = hotAllocationSize;
                emitter.emitTotalColdCodeSize = coldAllocationSize;

                // Only two or three real bytes are issued. Large sizes exercise the
                // unsigned allocation headers, not a multi-gigabyte buffer allocation.
                var arena = stackalloc byte[160];
                var storage = (byte*)(((nuint)arena + 31u) & ~(nuint)31);
                new Span<byte>(storage, 128).Fill(0xA5);
                ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
                vtable.allocMem = &Allocate;
                var context = new AllocationContext
                {
                    JitInfo = new ICorJitInfo { lpVtbl = &vtable },
                    Storage = storage,
                };
                compiler.info.compCompHnd = &context.JitInfo;
                emitter.emitCmpHandle = &context.JitInfo;
                var prolog = uint.MaxValue;
                var epilog = uint.MaxValue;
                void* hot = null;
                void* hotRW = null;
                void* cold = null;
                void* coldRW = null;
#if DEBUG
                var instructions = uint.MaxValue;
#endif
                var actual = emitter.emitEndCodeGen(compiler, false, false, false, 0,
                    &prolog, &epilog, &hot, &hotRW, &cold, &coldRW
#if DEBUG
                    , &instructions
#endif
                    );

                Assert.That(context.Calls, Is.EqualTo(1));
                Assert.That(context.Chunks, Is.EqualTo(hasCold ? 2 : 1));
                Assert.That(context.HotSize, Is.EqualTo(hotAllocationSize));
                Assert.That(context.HotAlignment, Is.EqualTo(expectedHotAlignment));
                Assert.That(context.ColdSize, Is.EqualTo(coldAllocationSize));
                Assert.That(actual, Is.EqualTo(hasCold ? 3u : 2u));
                Assert.That(prolog, Is.EqualTo(1u));
                Assert.That((nuint)hot, Is.EqualTo((nuint)storage));
                Assert.That((nuint)hotRW, Is.EqualTo((nuint)(storage + 64)));
                Assert.That((nuint)cold, Is.EqualTo(hasCold ? (nuint)(storage + 16) : 0));
                Assert.That((nuint)coldRW, Is.EqualTo(hasCold ? (nuint)(storage + 80) : 0));
                Assert.That(storage[64], Is.EqualTo(0x90));
                Assert.That(storage[65], Is.EqualTo(hasCold ? 0x90 : 0xC3));
                if (hasCold)
                {
                    Assert.That(storage[80], Is.EqualTo(0xC3));
                }
            });
    }

    private struct AllocationContext
    {
        public ICorJitInfo JitInfo;
        public byte* Storage;
        public int Calls;
        public int Chunks;
        public int HotSize;
        public int HotAlignment;
        public int ColdSize;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Allocate(ICorJitInfo* self, AllocMemArgs* args)
    {
        var context = (AllocationContext*)self;
        context->Calls++;
        context->Chunks = args->chunksCount;
        context->HotSize = args->chunks[0].size;
        context->HotAlignment = args->chunks[0].alignment;
        args->chunks[0].block = context->Storage;
        args->chunks[0].blockRW = context->Storage + 64;
        if (args->chunksCount == 2)
        {
            context->ColdSize = args->chunks[1].size;
            args->chunks[1].block = context->Storage + 16;
            args->chunks[1].blockRW = context->Storage + 80;
        }
    }
#endif
}
