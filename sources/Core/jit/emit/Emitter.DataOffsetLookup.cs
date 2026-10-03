// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe byte* emitDataOffsetToPtr(uint offset)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Constant-data chunk lookup requires AMD64.");
#else
        noway_assert(offset < emitConsDsc.dsdOffs);
        noway_assert(emitNumDataChunks > 0);
        noway_assert(emitDataChunks is not null && emitDataChunkOffsets is not null);

        var min = 0;
        var max = emitNumDataChunks;
        while (min < max)
        {
            var mid = min + ((max - min) / 2);
            var chunkOffset = unchecked((uint)emitDataChunkOffsets[mid]);
            if (chunkOffset == offset)
            {
                return emitDataChunks[mid].block;
            }

            if (chunkOffset < offset)
            {
                min = mid + 1;
            }
            else
            {
                max = mid;
            }
        }

        noway_assert(min > 0 && min <= emitNumDataChunks);
        var chunkIndex = min - 1;
        var relativeOffset = offset - unchecked((uint)emitDataChunkOffsets[chunkIndex]);
        noway_assert(relativeOffset < emitDataChunks[chunkIndex].size);
        return emitDataChunks[chunkIndex].block + relativeOffset;
#endif
    }
}
