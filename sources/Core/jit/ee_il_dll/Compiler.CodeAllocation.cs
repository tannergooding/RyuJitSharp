// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void eeAllocMem(ref AllocMemChunk codeChunk, AllocMemChunk* coldCodeChunk,
        AllocMemChunk* dataChunks, uint numDataChunks, uint numExceptions)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Code allocation requires Windows AMD64.");
#else
        var chunks = new AllocMemChunk[checked((int)numDataChunks + 2)];
        var chunksCount = 0;
        chunks[chunksCount++] = codeChunk;

        var coldCodeChunkIndex = -1;
        if (coldCodeChunk is not null)
        {
            coldCodeChunkIndex = chunksCount;
            chunks[chunksCount++] = *coldCodeChunk;
        }

#if DEBUG
        // Fake splitting keeps the cold offset in the caller's pointers until the EE returns the hot allocation.
        if ((JitConfig.JitFakeProcedureSplitting != 0) && (coldCodeChunk is not null))
        {
            coldCodeChunk->block = (byte*)(nuint)unchecked((uint)chunks[0].size);
            coldCodeChunk->blockRW = (byte*)(nuint)unchecked((uint)chunks[0].size);
            chunks[0].size = unchecked(chunks[0].size + coldCodeChunk->size);
            chunksCount--;
            coldCodeChunkIndex = -1;
        }
#endif

        var firstDataChunk = chunksCount;
        for (var i = 0; i < (int)numDataChunks; i++)
        {
            chunks[chunksCount++] = dataChunks[i];
        }

        fixed (AllocMemChunk* nativeChunks = chunks)
        {
            var args = new AllocMemArgs
            {
                chunks = nativeChunks,
                chunksCount = chunksCount,
                xcptnsCount = unchecked((int)numExceptions),
            };
            info.compCompHnd->allocMem(&args);

            codeChunk.block = nativeChunks[0].block;
            codeChunk.blockRW = nativeChunks[0].blockRW;

#pragma warning disable CA1508 // A non-null cold chunk retains index 1 unless DEBUG fake splitting removes it.
            if (coldCodeChunkIndex != -1)
            {
                coldCodeChunk->block = nativeChunks[coldCodeChunkIndex].block;
                coldCodeChunk->blockRW = nativeChunks[coldCodeChunkIndex].blockRW;
            }
#pragma warning restore CA1508

#if DEBUG
            if ((JitConfig.JitFakeProcedureSplitting != 0) && (coldCodeChunk is not null))
            {
                coldCodeChunk->block = unchecked((byte*)((nuint)codeChunk.block + (nuint)coldCodeChunk->block));
                coldCodeChunk->blockRW = unchecked((byte*)((nuint)codeChunk.blockRW + (nuint)coldCodeChunk->blockRW));
            }
#endif

            var curDataChunk = firstDataChunk;
            for (var i = 0; i < (int)numDataChunks; i++)
            {
                dataChunks[i].block = nativeChunks[curDataChunk].block;
                dataChunks[i].blockRW = nativeChunks[curDataChunk].blockRW;
                curDataChunk++;
            }
        }
#endif
    }
}
