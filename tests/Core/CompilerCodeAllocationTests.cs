// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerCodeAllocationTests
{
    [TestCase(false, 0, 5u)]
    [TestCase(true, 0, 5u)]
    [TestCase(false, 2, 5u)]
    [TestCase(true, 2, uint.MaxValue)]
    public static void AllocationPassesOrderedChunksAndExceptionCountAndCopiesBothAliases(
        bool hasCold, int dataCount, uint exceptionCount)
    {
        WithAllocation((compiler, context) =>
        {
            var captured = stackalloc AllocMemChunk[4];
            var executable = stackalloc byte*[4];
            var writable = stackalloc byte*[4];
            var executableStorage = stackalloc byte[64];
            var writableStorage = stackalloc byte[64];
            for (var i = 0; i < 4; i++)
            {
                executable[i] = executableStorage + (16 * i);
                writable[i] = writableStorage + (16 * i);
            }
            context->Captured = captured;
            context->Executable = executable;
            context->Writable = writable;
            context->Capacity = 4;

            var hot = new AllocMemChunk
            {
                alignment = 32,
                size = 19,
                flags = CorJitAllocMemFlag.CORJIT_ALLOCMEM_HOT_CODE,
                block = (byte*)1,
                blockRW = (byte*)2,
            };
            var cold = new AllocMemChunk
            {
                alignment = 1,
                size = 7,
                flags = CorJitAllocMemFlag.CORJIT_ALLOCMEM_COLD_CODE,
                block = (byte*)3,
                blockRW = (byte*)4,
            };
            var data = stackalloc AllocMemChunk[2];
            data[0] = new AllocMemChunk
            {
                alignment = 16,
                size = 12,
                flags = CorJitAllocMemFlag.CORJIT_ALLOCMEM_READONLY_DATA,
                block = (byte*)5,
                blockRW = (byte*)6,
            };
            data[1] = new AllocMemChunk
            {
                alignment = 64,
                size = 8,
                flags = CorJitAllocMemFlag.CORJIT_ALLOCMEM_READONLY_DATA |
                    CorJitAllocMemFlag.CORJIT_ALLOCMEM_HAS_POINTERS_TO_CODE,
                block = (byte*)7,
                blockRW = (byte*)8,
            };

            compiler.eeAllocMem(ref hot, hasCold ? &cold : null,
                dataCount == 0 ? null : data, (uint)dataCount, exceptionCount);

            Assert.That(context->Calls, Is.EqualTo(1));
            Assert.That(context->InvalidCount, Is.False);
            Assert.That(context->ChunkCount, Is.EqualTo(1 + (hasCold ? 1 : 0) + dataCount));
            Assert.That(context->ExceptionCount, Is.EqualTo(unchecked((int)exceptionCount)));
            AssertChunk(captured[0], 32, 19, CorJitAllocMemFlag.CORJIT_ALLOCMEM_HOT_CODE, 1, 2);
            Assert.That(hot.block == executable[0], Is.True);
            Assert.That(hot.blockRW == writable[0], Is.True);
            Assert.That(hot.size, Is.EqualTo(19));

            var firstData = 1;
            if (hasCold)
            {
                AssertChunk(captured[1], 1, 7, CorJitAllocMemFlag.CORJIT_ALLOCMEM_COLD_CODE, 3, 4);
                Assert.That(cold.block == executable[1], Is.True);
                Assert.That(cold.blockRW == writable[1], Is.True);
                firstData++;
            }
            else
            {
                Assert.That(cold.block == (byte*)3, Is.True);
                Assert.That(cold.blockRW == (byte*)4, Is.True);
            }

            for (var i = 0; i < dataCount; i++)
            {
                var expectedFlags = i == 0
                    ? CorJitAllocMemFlag.CORJIT_ALLOCMEM_READONLY_DATA
                    : CorJitAllocMemFlag.CORJIT_ALLOCMEM_READONLY_DATA |
                        CorJitAllocMemFlag.CORJIT_ALLOCMEM_HAS_POINTERS_TO_CODE;
                AssertChunk(captured[firstData + i], i == 0 ? 16 : 64, i == 0 ? 12 : 8,
                    expectedFlags, 5 + (2 * i), 6 + (2 * i));
                Assert.That(data[i].block == executable[firstData + i], Is.True);
                Assert.That(data[i].blockRW == writable[firstData + i], Is.True);
            }
        });
    }

#if DEBUG
    [TestCase(19, 26)]
    [TestCase(unchecked((int)0x80000013u), unchecked((int)0x8000001Au))]
    public static void FakeSplittingCombinesAllocationThenRestoresColdExecutableAndWritableOffsets(
        int hotSize, int combinedSize)
    {
        WithAllocation((compiler, context) =>
        {
            var captured = stackalloc AllocMemChunk[2];
            var executable = stackalloc byte*[2];
            var writable = stackalloc byte*[2];
            var executableStorage = stackalloc byte[64];
            var writableStorage = stackalloc byte[64];
            executable[0] = executableStorage;
            writable[0] = writableStorage;
            executable[1] = executableStorage + 32;
            writable[1] = writableStorage + 32;
            context->Captured = captured;
            context->Executable = executable;
            context->Writable = writable;
            context->Capacity = 2;

            var hot = new AllocMemChunk
            {
                alignment = 32,
                size = hotSize,
                flags = CorJitAllocMemFlag.CORJIT_ALLOCMEM_HOT_CODE,
            };
            var cold = new AllocMemChunk
            {
                alignment = 1,
                size = 7,
                flags = CorJitAllocMemFlag.CORJIT_ALLOCMEM_COLD_CODE,
            };
            var data = new AllocMemChunk
            {
                alignment = 8,
                size = 8,
                flags = CorJitAllocMemFlag.CORJIT_ALLOCMEM_READONLY_DATA,
            };

            compiler.eeAllocMem(ref hot, &cold, &data, 1, 3);

            Assert.That(context->Calls, Is.EqualTo(1));
            Assert.That(context->InvalidCount, Is.False);
            Assert.That(context->ChunkCount, Is.EqualTo(2));
            Assert.That(context->ExceptionCount, Is.EqualTo(3));
            AssertChunk(captured[0], 32, combinedSize, CorJitAllocMemFlag.CORJIT_ALLOCMEM_HOT_CODE, 0, 0);
            AssertChunk(captured[1], 8, 8, CorJitAllocMemFlag.CORJIT_ALLOCMEM_READONLY_DATA, 0, 0);
            Assert.That(hot.size, Is.EqualTo(hotSize));
            Assert.That(hot.block == executable[0], Is.True);
            Assert.That(hot.blockRW == writable[0], Is.True);
            var executableCold = unchecked((byte*)((nuint)executable[0] + (uint)hotSize));
            var writableCold = unchecked((byte*)((nuint)writable[0] + (uint)hotSize));
            Assert.That(cold.block == executableCold, Is.True);
            Assert.That(cold.blockRW == writableCold, Is.True);
            Assert.That(data.block == executable[1], Is.True);
            Assert.That(data.blockRW == writable[1], Is.True);
        }, fakeSplitting: true);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitFakeProcedureSplitting")]
    private static extern ref int FakeProcedureSplitting(ref JitConfigValues config);
#endif

    private static void AssertChunk(AllocMemChunk chunk, int alignment, int size,
        CorJitAllocMemFlag flags, int initialBlock, int initialBlockRW)
    {
        Assert.That(chunk.alignment, Is.EqualTo(alignment));
        Assert.That(chunk.size, Is.EqualTo(size));
        Assert.That(chunk.flags, Is.EqualTo(flags));
        Assert.That((nint)chunk.block, Is.EqualTo((nint)initialBlock));
        Assert.That((nint)chunk.blockRW, Is.EqualTo((nint)initialBlockRW));
    }

    private delegate void AllocationAction(Compiler compiler, AllocationContext* context);

    private static void WithAllocation(AllocationAction action, bool fakeSplitting = false)
    {
        var previousConfig = Globals.JitConfig;
        try
        {
            var config = default(JitConfigValues);
#if DEBUG
            if (fakeSplitting)
            {
                FakeProcedureSplitting(ref config) = 1;
            }
#else
            if (fakeSplitting)
            {
                throw new InvalidOperationException("Fake splitting is only available in DEBUG.");
            }
#endif
            Globals.JitConfig = config;

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.allocMem = &Allocate;
            var context = new AllocationContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            };
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            compiler.info.compCompHnd = &context.JitInfo;

            action(compiler, &context);
        }
        finally
        {
            Globals.JitConfig = previousConfig;
        }
    }

    private struct AllocationContext
    {
        public ICorJitInfo JitInfo;
        public AllocMemChunk* Captured;
        public byte** Executable;
        public byte** Writable;
        public int Capacity;
        public int ChunkCount;
        public int ExceptionCount;
        public int Calls;
        public bool InvalidCount;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Allocate(ICorJitInfo* jitInfo, AllocMemArgs* args)
    {
        var context = (AllocationContext*)jitInfo;
        context->Calls++;
        context->ChunkCount = args->chunksCount;
        context->ExceptionCount = args->xcptnsCount;
        if ((args->chunksCount < 1) || (args->chunksCount > context->Capacity))
        {
            context->InvalidCount = true;
            return;
        }

        GC.Collect();
        for (var i = 0; i < args->chunksCount; i++)
        {
            context->Captured[i] = args->chunks[i];
            args->chunks[i].block = context->Executable[i];
            args->chunks[i].blockRW = context->Writable[i];
        }
    }
}
